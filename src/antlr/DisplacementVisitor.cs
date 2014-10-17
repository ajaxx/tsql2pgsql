﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using Common.Logging;

namespace tsql2pgsql.antlr
{
    using grammar;

    internal class DisplacementVisitor<T> : TSQLBaseVisitor<T>
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static readonly ILog _log = LogManager.GetCurrentClassLogger();

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
        /// The compiled unit context
        /// </summary>
        private TSQLParser.CompileUnitContext _unitContext;

        /// <summary>
        /// Returns an enumeration of lines.
        /// </summary>
        public IEnumerable<string> Lines
        {
            get { return _lines; }
        }

        /// <summary>
        /// Gets the compile unit context.
        /// </summary>
        /// <value>
        /// The compile unit context.
        /// </value>
        public virtual TSQLParser.CompileUnitContext UnitContext
        {
            get
            {
                if (_unitContext == null)
                {
                    _unitContext = CreateParser().compileUnit();
                }

                return _unitContext;
            }
        }

        /// <summary>
        /// Creates a common mutation engine.
        /// </summary>
        public DisplacementVisitor(IEnumerable<string> lines) : base()
        {
            _lines = new List<string>(lines);
            for (int ii = 0; ii < _lines.Count; ii++)
                _lineDisplacementActions.Add(null);
        }

        /// <summary>
        /// Creates the parser.
        /// </summary>
        /// <returns></returns>
        public virtual TSQLParser CreateParser()
        {
            var source = string.Join("\n", _lines);
            var stream = new CaseInsensitiveStream(source);
            var lexer = new TSQLLexer(stream);
            var parser = new TSQLParser(new CommonTokenStream(lexer));

            return parser;
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        public virtual string[] Process()
        {
            Visit(UnitContext);
            return string.Join("\n", _lines).Split('\n');
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

            _log.DebugFormat("AddLineDisplacement: line={0}, column={1}, displacement={2}", line, column, displacement);

            actionList.Add(
                srcColumnValue =>
                {
                    if (srcColumnValue < column)
                    {
                        _log.DebugFormat(
                            "Lambda: srcColumnValue={0}, referenceColumn={1}, displacement={2} | ignoring because srcColumn occurs before reference column",
                            srcColumnValue, column, displacement);
                        return srcColumnValue;
                    }

                    _log.DebugFormat(
                        "Lambda: srcColumnValue={0}, referenceColumn={1}, displacement={2}, dstColumnValue={3}", 
                        srcColumnValue, column, displacement, srcColumnValue + displacement);
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
                _log.DebugFormat("ApplyLineDisplacement: D({0}|{1})", line, column);
                column = _lineDisplacementActions[line]
                    .Aggregate(column, (current, action) => action.Invoke(current));
            }
        }

        /// <summary>
        /// Replaces the text at the specified token with the new text.  This method assumes that you
        /// know the old text and can provide it.  See other versions for more common use cases.
        /// </summary>
        /// <param name="tLine">The line.</param>
        /// <param name="tColumn">The column.</param>
        /// <param name="oldText">The old text.</param>
        /// <param name="newText">The new text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        internal void ReplaceText(
            int tLine,
            int tColumn,
            string oldText,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            var dLine = tLine;
            var dColumn = tColumn + oldText.Length;

            ApplyLineDisplacement(ref tLine, ref tColumn);
            ApplyLineDisplacement(ref dLine, ref dColumn);

            _log.DebugFormat("ReplaceText: position = {0}.{1} | oldText = {2}", tLine, tColumn, oldText);
            _log.DebugFormat("ReplaceText: tlen = {0}, true-len = {1}", dColumn - tColumn, oldText.Length);

            var line = _lines[tLine - 1];
            var tlen = dColumn - tColumn;
            var tpos = Math.Min(tColumn, line.Length);

            var head = line.Substring(0, tpos);
            var tail = (tpos + tlen) < line.Length ? line.Substring(tpos + tlen) : string.Empty;

            if (_log.IsDebugEnabled)
            {
                _log.DebugFormat("ReplaceText: tpos = {0}", tpos);
                _log.DebugFormat("ReplaceText: head = {0}", head);
                _log.DebugFormat("ReplaceText: tail = {0}", tail);
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

            _log.DebugFormat("ReplaceText: old = {0}", _lines[tLine - 1]);
            _lines[tLine - 1] = line;
            _log.DebugFormat("ReplaceText: new = {0}", line);

            // Adjust the line displacement
            if (tlen > 0)
            {
                _log.DebugFormat("ReplaceText: d#|{0}|{1}|{2}|{3}", tLine, tColumn, tlen, tColumn + tlen);
                AddLineDisplacement(tLine, tColumn + tlen, -tlen);
            }
            else
            {
                _log.DebugFormat("ReplaceText: d*|{0}|{1}|{2}|{3}", tLine, tColumn, tlen, tColumn - tlen);
                AddLineDisplacement(tLine, tColumn, -tlen);
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
            ReplaceText(
                token.Line,
                token.Column,
                oldText,
                newText,
                eatTrailingWhitespace);
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
            _log.DebugFormat("RemoveBetween: d#|{0}|{1}|{2}|{3}", stLine, stColumn, tlen, stColumn + tlen);
            AddLineDisplacement(stLine, stColumn + tlen, -tlen);
        }

        /// <summary>
        /// Replaces the content associated with the given token with new text.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="newText">The new text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        internal void ReplaceToken(
            IToken token,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            ReplaceText(token, token.Text, newText, eatTrailingWhitespace);
        }

        /// <summary>
        /// Replaces the content associated with the given token with new text.
        /// </summary>
        /// <param name="terminalNode">The terminal node.</param>
        /// <param name="newText">The new text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        internal void ReplaceToken(
            ITerminalNode terminalNode,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            ReplaceToken(terminalNode.Symbol, newText, eatTrailingWhitespace);
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
        internal void RemoveLeaves(IParseTree tree, int depth = 0)
        {
            _log.DebugFormat("RemoveLeaves: depth = {0} | {1}", depth, tree.GetText());

            if (tree.ChildCount != 0)
            {
                for (int ii = 0; ii < tree.ChildCount; ii++)
                {
                    RemoveLeaves(tree.GetChild(ii), depth + 1);
                }
            }
            else if (tree is TerminalNodeImpl)
            {
                _log.DebugFormat("RemoveLeaves: TerminalNodeImpl | {0}", tree);
                RemoveToken(
                    ((TerminalNodeImpl) tree).Symbol);
            }
            else if (tree is ParserRuleContext)
            {
                _log.DebugFormat("RemoveLeaves: ParserRuleContext | {0}", tree);
                //RemoveToken(
                //    ((ParserRuleContext) tree).Start);
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
                    ((TerminalNodeImpl) tree).Symbol);
            }
            else if (tree is ParserRuleContext)
            {
                RemoveToken(
                    ((ParserRuleContext) tree).Start);
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
        /// Inserts text at the specified location.
        /// </summary>
        /// <param name="iLine">The i line.</param>
        /// <param name="iColumn">The i column.</param>
        /// <param name="text">The text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        internal void InsertAt(int iLine, int iColumn, string text, bool eatTrailingWhitespace = true)
        {
            ApplyLineDisplacement(ref iLine, ref iColumn);

            var tlen = 0;
            var tpos = iColumn;
            var line = _lines[iLine - 1];
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

            _lines[iLine - 1] = line;

            // Adjust the line displacement
            if (tlen != 0)
            {
                AddLineDisplacement(iLine, iColumn + tlen, -tlen);
            }
        }

        /// <summary>
        /// Inserts text before the specified token.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="text"></param>
        /// <param name="eatTrailingWhitespace"></param>
        internal void InsertBefore(IToken token, string text, bool eatTrailingWhitespace = true)
        {
            InsertAt(
                token.Line,
                token.Column,
                text,
                eatTrailingWhitespace);
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
                    ((TerminalNodeImpl) tree).Symbol, text, eatTrailingWhitespace);
            }
            else if (tree is ParserRuleContext)
            {
                InsertBefore(
                    ((ParserRuleContext) tree).Start, text, eatTrailingWhitespace);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }


        internal void InsertAfter(IToken token, string text, bool eatTrailingWhitespace = true)
        {
            InsertAt(
                token.Line,
                token.Column + token.Text.Length,
                text,
                eatTrailingWhitespace);
        }

        internal void InsertAfter(IParseTree tree, string text, bool eatTrailingWhitespace = true)
        {
            if (tree is TerminalNodeImpl)
            {
                InsertAfter(
                    ((TerminalNodeImpl) tree).Symbol, text, eatTrailingWhitespace);
            }
            else if (tree is ParserRuleContext)
            {
                InsertAfter(
                    ((ParserRuleContext) tree).Stop, text, eatTrailingWhitespace);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        internal string GetTextAt(int line, int startIndex, int length)
        {
            var endLine = line;
            var endIndex = startIndex + length;

            ApplyLineDisplacement(ref line, ref startIndex);
            ApplyLineDisplacement(ref endLine, ref endIndex);

            var reqLine = _lines[line - 1];

            if (startIndex >= reqLine.Length)
                return string.Empty;
            if (endIndex > reqLine.Length)
                return reqLine.Substring(startIndex);

            return reqLine.Substring(startIndex, endIndex - startIndex);
        }

        internal string GetTextFor(IToken token)
        {
            _log.DebugFormat(
                "GetTextFor: {0}|{1}|{2}",
                token.Line,
                token.Column,
                token.Text.Length);
            return GetTextAt(
                token.Line,
                token.Column,
                token.Text.Length);
        }

        internal string GetTextFor(IParseTree tree)
        {
            if (tree is TerminalNodeImpl)
            {
                var terminalNode = (TerminalNodeImpl) tree;
                _log.DebugFormat("GetTextFor: Terminal|{0}|{1}|{2}",
                    terminalNode.Symbol.Line,
                    terminalNode.Symbol.Column,
                    terminalNode.Symbol.Text.Length);
                return GetTextAt(
                    terminalNode.Symbol.Line,
                    terminalNode.Symbol.Column,
                    terminalNode.Symbol.Text.Length);
            }
            else if (tree is ParserRuleContext)
            {
                var ruleContext = (ParserRuleContext) tree;
                _log.DebugFormat("GetTextFor: ParserRule|{0}|{1}|{2}",
                    ruleContext.Start.Line,
                    ruleContext.Start.Column,
                    ruleContext.Stop.Column + ruleContext.Stop.Text.Length - ruleContext.Start.Column);
                return GetTextAt(
                    ruleContext.Start.Line,
                    ruleContext.Start.Column,
                    ruleContext.Stop.Column + ruleContext.Stop.Text.Length - ruleContext.Start.Column);
            }

            throw new InvalidOperationException();
        }

        internal bool ConfirmConsistency(IParseTree tree, bool isCaseSensitive = false)
        {
            var comparisonType = isCaseSensitive
                ? StringComparison.InvariantCultureIgnoreCase
                : StringComparison.InvariantCulture;
            var fragmentA = tree.GetText();
            var fragmentB = GetTextFor(tree);
            _log.DebugFormat("ConfirmConstistency: {0} | {1} | {2} | {3}", fragmentA, fragmentB, tree, tree.ToRangeString());
            return String.Equals(fragmentA, fragmentB, comparisonType);
        }
    }
}