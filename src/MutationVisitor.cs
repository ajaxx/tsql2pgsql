// --------------------------------------------------------------------------------
// Copyright (c) 2014, XLR8 Development
// --------------------------------------------------------------------------------
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// --------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using Common.Logging;

namespace tsql2pgsql
{
    internal class MutationVisitor : TSQLBaseVisitor<TSQLParser.CompileUnitContext>
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static ILog _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The collection of lines.
        /// </summary>
        private readonly List<string> _lines;

        /// <summary>
        /// A collection functions that when applied to a line, apply any transformations to
        /// find the target location in the modified source.
        /// </summary>
        private readonly List<List<Func<int, int>>> _lineDisplacementActions =
            new List<List<Func<int, int>>>();

        /// <summary>
        /// Creates a common mutation engine.
        /// </summary>
        public MutationVisitor(IEnumerable<string> procedureLines)
            : base()
        {
            _lines = new List<string>(procedureLines);
        }

        /// <summary>
        /// Mutates the procedure from T-SQL to PL/PGSQL
        /// </summary>
        public void Mutate()
        {
            var source = string.Join("\n", _lines);
            var stream = new CaseInsensitiveStream(source);
            var lexer = new TSQLLexer(stream);
            var parser = new TSQLParser(new CommonTokenStream(lexer));

            Visit(parser.compileUnit());
        }

        /// <summary>
        /// Adds a displacement to the specified line at the given column.
        /// </summary>
        /// <param name="line">Line number to displace</param>
        /// <param name="column">Column number to displace</param>
        /// <param name="displacement">Amount to displace by</param>
        internal void AddLineDisplacement(int line, int column, int displacement)
        {
            if (displacement == 0)
                return;
            if (line >= _lineDisplacementActions.Count)
                throw new InvalidOperationException();

            var actionList = _lineDisplacementActions[line];
            if (actionList == null)
                actionList = _lineDisplacementActions[line] = new List<Func<int, int>>();

            _log.DebugFormat("S|{0}", line);

            actionList.Add(
                srcColumnValue =>
                {
                    if (srcColumnValue < column)
                    {
                        _log.DebugFormat("!{0}/{1}/{2}", displacement, srcColumnValue, column);
                        return srcColumnValue;
                    }

                    _log.DebugFormat("|{0}/{1}/{2}/{3}", displacement, srcColumnValue, column, srcColumnValue + displacement);
                    return srcColumnValue + displacement;
                });
        }

        /// <summary>
        /// Applies displacement to the specified line and column.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="column"></param>
        internal void ApplyLineDisplacement(ref int line, ref int column)
        {
            if (line >= _lineDisplacementActions.Count)
                return;
            if (_lineDisplacementActions[line] != null)
            {
                _log.DebugFormat("D({0}|{1})", line, column);
                column = _lineDisplacementActions[line]
                    .Aggregate(column, (current, action) => action.Invoke(current));
            }
        }

        /// <summary>
        /// Replaces the text at the specified token with the new text.  This method assumes that you
        /// know the old text and can provide it.  See other versions for more common use cases.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="oldText"></param>
        /// <param name="newText"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void ReplaceText(
            IToken token,
            string oldText,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            var tLine = token.Line;
            var tColumn = token.Column;
            int tColumnOrig = tColumn;

            ApplyLineDisplacement(ref tLine, ref tColumn);

            _log.DebugFormat("DeleteToken: {0} @ {1}.{2}", token, tLine, tColumn);

            var tlen = oldText.Length;
            var tpos = tColumn;
            var line = _lines[tLine - 1];
            var baseLine = line;
            var head = line.Substring(0, tpos);
            var tail = line.Substring(tpos + tlen);

            if (_log.IsDebugEnabled)
            {
                _log.DebugFormat("DeleteToken: tpos = {0}", tpos);
                _log.DebugFormat("DeleteToken: head = {0}", head);
                _log.DebugFormat("DeleteToken: tail = {0}", tail);
            }

            tlen -= newText.Length;

            line = head + newText + tail;
            if (eatTrailingWhitespace)
            {
                while ((tpos < line.Length) && (char.IsWhiteSpace(line[tpos])))
                {
                    tlen++;
                    line = line.Remove(tpos, 1);
                }
            }

            _lines[tLine - 1] = line;

            //Console.WriteLine("DeleteToken[tail]: {0}", tlen);
            //Console.WriteLine("DeleteToken[line]: {0}", line);

            // Adjust the line displacement
            if (tlen > 0)
            {
                _log.DebugFormat("d#|{0}|{1}|{2}|{3}", tLine, tColumn, tlen, tColumn + tlen);
                AddLineDisplacement(tLine, tColumn + tlen, -tlen);
            }
            else
            {
                _log.DebugFormat("d*|{0}|{1}|{2}|{3}", tLine, tColumn, tlen, tColumn - tlen);
                AddLineDisplacement(tLine, tColumn - tlen, -tlen);
            }
        }

        /// <summary>
        /// Removes the text between two tokens.
        /// </summary>
        /// <param name="startToken"></param>
        /// <param name="endToken"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void RemoveBetween(
            IToken startToken,
            IToken endToken,
            bool eatTrailingWhitespace = true)
        {
            var stLine = startToken.Line;
            var stColumn = startToken.Column + 1;
            var etLine = endToken.Line;
            var etColumn = endToken.Column;

            ApplyLineDisplacement(ref stLine, ref stColumn);
            ApplyLineDisplacement(ref etLine, ref etColumn);

            if (stLine != etLine)
            {
                throw new InvalidOperationException();
            }

            if (etColumn - stColumn < 0)
                return;

            var tlen = etColumn - stColumn;
            var tpos = stColumn;
            var line = _lines[stLine - 1];
            var head = line.Substring(0, tpos);
            var tail = line.Substring(tpos + tlen);

            line = head + tail;
            if (eatTrailingWhitespace)
            {
                while ((tpos < line.Length) && (char.IsWhiteSpace(line[tpos])))
                {
                    tlen++;
                    line = line.Remove(tpos, 1);
                }
            }

            _lines[stLine - 1] = line;

            //Console.WriteLine("DeleteToken[tail]: {0}", tlen);
            //Console.WriteLine("DeleteToken[line]: {0}", line);

            // Adjust the line displacement
            _log.DebugFormat("d#|{0}|{1}|{2}|{3}", stLine, stColumn, tlen, stColumn + tlen);
            AddLineDisplacement(stLine, stColumn + tlen, -tlen);
        }

        /// <summary>
        /// Replaces the content associated with the given token with new text.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="newText"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void ReplaceToken(
            IToken token,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            ReplaceText(token, token.Text, newText, eatTrailingWhitespace);
        }

        /// <summary>
        /// Removes the content associated with the given token.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void RemoveToken(IToken token, bool eatTrailingWhitespace = true)
        {
            ReplaceText(token, token.Text, "", eatTrailingWhitespace);
        }

        /// <summary>
        /// Descends into the parse tree and removes any tokens that are identified
        /// as leaves.
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void RemoveLeaves(IParseTree tree, bool eatTrailingWhitespace = true)
        {
            if (tree.ChildCount != 0)
            {
                for (int ii = 0; ii < tree.ChildCount; ii++)
                {
                    RemoveLeaves(tree.GetChild(ii));
                }
            }
            else if (tree is TerminalNodeImpl)
            {
                RemoveToken(
                    ((TerminalNodeImpl)tree).Symbol);
            }
            else if (tree is ParserRuleContext)
            {
                RemoveToken(
                    ((ParserRuleContext)tree).Start);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Removes all elements of the tree.
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void Remove(IParseTree tree, bool eatTrailingWhitespace = true)
        {
            if (tree is TerminalNodeImpl)
            {
                RemoveToken(
                    ((TerminalNodeImpl)tree).Symbol);
            }
            else if (tree is ParserRuleContext)
            {
                RemoveToken(
                    ((ParserRuleContext)tree).Start);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Returns the text between two tokens.
        /// </summary>
        /// <param name="startToken"></param>
        /// <param name="endToken"></param>
        /// <returns></returns>
        internal string TextBetween(IToken startToken, IToken endToken)
        {
            var stLine = startToken.Line;
            var stColumn = startToken.Column;
            var etLine = endToken.Line;
            var etColumn = endToken.Column;

            ApplyLineDisplacement(ref stLine, ref stColumn);
            ApplyLineDisplacement(ref etLine, ref etColumn);

            if (stLine != etLine)
            {
                throw new InvalidOperationException();
            }

            var xLine = _lines[stLine - 1];
            if (stColumn >= xLine.Length)
                return string.Empty;
            if (etColumn >= xLine.Length)
                etColumn = xLine.Length - 1;

            return xLine.Substring(
                stColumn, etColumn - stColumn);
        }

        /// <summary>
        /// Inserts text before the specified token.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="text"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void InsertBefore(IToken token, string text, bool eatTrailingWhitespace = true)
        {
            var tLine = token.Line;
            var tColumn = token.Column;

            ApplyLineDisplacement(ref tLine, ref tColumn);

            var tlen = 0;
            var tpos = tColumn;
            var line = _lines[tLine - 1];
            var head = line.Substring(0, tpos);
            var tail = line.Substring(tpos + tlen);

            tlen -= text.Length;

            line = head + text + tail;
            if (eatTrailingWhitespace)
            {
                while ((tpos < line.Length) && (char.IsWhiteSpace(line[tpos])))
                {
                    tlen++;
                    line = line.Remove(tpos, 1);
                }
            }

            _lines[tLine - 1] = line;

            // Adjust the line displacement
            if (tlen != 0)
            {
                AddLineDisplacement(tLine, tColumn + tlen, -tlen);
            }
        }

        /// <summary>
        /// Inserts text before the specified parse tree.
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="text"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void InsertBefore(IParseTree tree, string text, bool eatTrailingWhitespace = true)
        {
            if (tree is TerminalNodeImpl)
            {
                InsertBefore(
                    ((TerminalNodeImpl)tree).Symbol, text, eatTrailingWhitespace);
            }
            else if (tree is ParserRuleContext)
            {
                InsertBefore(
                    ((ParserRuleContext)tree).Start, text, eatTrailingWhitespace);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Called when we encounter a type that has been quoted according to TSQL convention.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override TSQLParser.CompileUnitContext VisitTypeInBracket(TSQLParser.TypeInBracketContext context)
        {
            Console.WriteLine(context.type().GetText());
            return base.VisitTypeInBracket(context);
        }

        /// <summary>
        /// Called when we encounter a type.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override TSQLParser.CompileUnitContext VisitType(TSQLParser.TypeContext context)
        {
            return base.VisitType(context);
        }

        /// <summary>
        /// Called when we encounter a name part.  Since nameparts often contain quotation symbology specific to
        /// TSQL, we need to convert it to PL/PGSQL friendly notation.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override TSQLParser.CompileUnitContext VisitQualifiedNamePart(TSQLParser.QualifiedNamePartContext context)
        {
            var identifierTree = context.Identifier();
            var identifierToken = identifierTree.Symbol;
            var identifier = context.Identifier().GetText().Trim();
            if ((identifier.Length > 2) &&
                (identifier[0] == '[') &&
                (identifier[identifier.Length - 1] == ']'))
            {
                identifier = string.Format("\"{0}\"", identifier.Substring(1, identifier.Length - 2));
                ReplaceToken(identifierToken, identifier);
            }

            return base.VisitQualifiedNamePart(context);
        }

        /// <summary>
        /// Called when we encounter "CREATE PROCEDURE"
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override TSQLParser.CompileUnitContext VisitCreateProcedure(TSQLParser.CreateProcedureContext context)
        {
            return base.VisitCreateProcedure(context);
        }
    }
}
