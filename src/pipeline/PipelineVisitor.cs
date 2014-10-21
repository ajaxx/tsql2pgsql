using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using tsql2pgsql.antlr;

namespace tsql2pgsql.pipeline
{
    using grammar;

    /// <summary>
    /// A visitor specially designed for use with the Pipeline object.
    /// </summary>
    internal class PipelineVisitor : TSQLBaseVisitor<object>
    {
        /// <summary>
        /// Gets or sets the pipeline.
        /// </summary>
        /// <value>
        /// The pipeline.
        /// </value>
        public Pipeline Pipeline { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to rebuild the pipeline.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [rebuild pipeline]; otherwise, <c>false</c>.
        /// </value>
        public bool RebuildPipeline { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineVisitor"/> class.
        /// </summary>
        public PipelineVisitor()
        {
            RebuildPipeline = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineVisitor"/> class.
        /// </summary>
        /// <param name="rebuildPipeline">if set to <c>true</c> [rebuild pipeline].</param>
        public PipelineVisitor(bool rebuildPipeline)
        {
            RebuildPipeline = rebuildPipeline;
        }

        /// <summary>
        /// Visits the specified pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        public virtual PipelineResult Visit(Pipeline pipeline)
        {
            base.Visit(pipeline.ParseTree);
            return new PipelineResult
            {
                RebuildPipeline = this.RebuildPipeline
            };
        }

        /// <summary>
        /// Returns the contents of the requested line.
        /// </summary>
        /// <param name="iLine">The line number.</param>
        /// <returns></returns>
        public virtual string GetLine(int iLine)
        {
            return Pipeline.GetLine(iLine);
        }

        /// <summary>
        /// Eats any whitespace at the given location.
        /// </summary>
        /// <param name="tLine">The line.</param>
        /// <param name="tColumn">The column.</param>
        public virtual void EatWhitespace(
            int tLine,
            int tColumn)
        {
            Pipeline.EatWhitespace(tLine, tColumn);
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
        public virtual void ReplaceText(
            int tLine,
            int tColumn,
            string oldText,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            Pipeline.ReplaceText(tLine, tColumn, oldText, newText, eatTrailingWhitespace);
        }

        /// <summary>
        /// Replaces the text at the specified token with the new text.  This method assumes that you
        /// know the old text and can provide it.  See other versions for more common use cases.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="oldText"></param>
        /// <param name="newText"></param>
        /// <param name="eatTrailingWhitespace"></param>
        public virtual void ReplaceText(
            IToken token,
            string oldText,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            Pipeline.ReplaceText(token, oldText, newText, eatTrailingWhitespace);
        }

        /// <summary>
        /// Removes the text between two tokens.
        /// </summary>
        /// <param name="startToken"></param>
        /// <param name="endToken"></param>
        /// <param name="eatTrailingWhitespace"></param>
        public virtual void RemoveBetween(
            IToken startToken,
            IToken endToken,
            bool eatTrailingWhitespace = true)
        {
            Pipeline.RemoveBetween(startToken, endToken, eatTrailingWhitespace);
        }

        /// <summary>
        /// Replaces the content associated with the given token with new text.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="newText">The new text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        public virtual void ReplaceToken(
            IToken token,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            Pipeline.ReplaceToken(token, newText, eatTrailingWhitespace);
        }

        /// <summary>
        /// Replaces the content associated with the given token with new text.
        /// </summary>
        /// <param name="terminalNode">The terminal node.</param>
        /// <param name="newText">The new text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        public virtual void ReplaceToken(
            ITerminalNode terminalNode,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            Pipeline.ReplaceToken(terminalNode, newText, eatTrailingWhitespace);
        }

        /// <summary>
        /// Replaces the content associated with the given parse tree with new text.
        /// </summary>
        /// <param name="parseTree">The parse tree.</param>
        /// <param name="newText">The new text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        public virtual void Replace(
            IParseTree parseTree,
            string newText,
            bool eatTrailingWhitespace = true)
        {
            if (parseTree is ITerminalNode)
            {
                Replace((IParseTree) parseTree, newText, eatTrailingWhitespace);
            }
            else if (parseTree is ParserRuleContext)
            {
                var ruleContext = (ParserRuleContext) parseTree;
                Pipeline.ReplaceText(
                    ruleContext.Start.Line,
                    ruleContext.Start.Column,
                    ruleContext.GetText().Length,
                    newText,
                    eatTrailingWhitespace);
            }
        }

        /// <summary>
        /// Removes the content associated with the given token.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="eatTrailingWhitespace"></param>
        public virtual void RemoveToken(IToken token, bool eatTrailingWhitespace = true)
        {
            Pipeline.RemoveToken(token, eatTrailingWhitespace);
        }

        /// <summary>
        /// Descends into the parse tree and removes any tokens that are identified
        /// as leaves.
        /// </summary>
        /// <param name="tree">The tree.</param>
        public virtual void RemoveLeaves(IParseTree tree)
        {
            Pipeline.RemoveLeaves(tree);
        }

        /// <summary>
        /// Removes all elements of the tree.
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="eatTrailingWhitespace"></param>
        public virtual void Remove(IParseTree tree, bool eatTrailingWhitespace = true)
        {
            Pipeline.Remove(tree, eatTrailingWhitespace);
        }

        /// <summary>
        /// Returns the text between two tokens.
        /// </summary>
        /// <param name="startToken"></param>
        /// <param name="endToken"></param>
        /// <returns></returns>
        public virtual string TextBetween(IToken startToken, IToken endToken)
        {
            return Pipeline.TextBetween(startToken, endToken);
        }

        /// <summary>
        /// Inserts text at the specified location.
        /// </summary>
        /// <param name="iLine">The i line.</param>
        /// <param name="iColumn">The i column.</param>
        /// <param name="text">The text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        public virtual void InsertAt(int iLine, int iColumn, string text, bool eatTrailingWhitespace = true)
        {
            Pipeline.InsertAt(iLine, iColumn, text, eatTrailingWhitespace);
        }

        /// <summary>
        /// Inserts text before the specified token.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="text"></param>
        /// <param name="eatTrailingWhitespace"></param>
        public virtual void InsertBefore(IToken token, string text, bool eatTrailingWhitespace = true)
        {
            Pipeline.InsertBefore(token, text, eatTrailingWhitespace);
        }

        /// <summary>
        /// Inserts text before the specified parse tree.
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="text"></param>
        /// <param name="eatTrailingWhitespace"></param>
        public virtual void InsertBefore(IParseTree tree, string text, bool eatTrailingWhitespace = true)
        {
            Pipeline.InsertBefore(tree, text, eatTrailingWhitespace);
        }

        /// <summary>
        /// Inserts text after the token.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="text">The text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        public virtual void InsertAfter(IToken token, string text, bool eatTrailingWhitespace = true)
        {
            Pipeline.InsertAfter(token, text, eatTrailingWhitespace);
        }

        /// <summary>
        /// Inserts text after the parseTree.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="text">The text.</param>
        /// <param name="eatTrailingWhitespace">if set to <c>true</c> [eat trailing whitespace].</param>
        public virtual void InsertAfter(IParseTree tree, string text, bool eatTrailingWhitespace = true)
        {
            Pipeline.InsertAfter(tree, text, eatTrailingWhitespace);
        }

        /// <summary>
        /// Gets the text currently at a specified location.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="startIndex">The start index.</param>
        /// <param name="length">The length.</param>
        /// <returns></returns>
        public virtual string GetTextAt(int line, int startIndex, int length)
        {
            return Pipeline.GetTextAt(line, startIndex, length);
        }

        /// <summary>
        /// Gets the text currently associated with the token.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns></returns>
        public virtual string GetTextFor(IToken token)
        {
            return Pipeline.GetTextFor(token);
        }

        /// <summary>
        /// Gets the text currently associated with the parse tree.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <returns></returns>
        public virtual string GetTextFor(IParseTree tree)
        {
            return Pipeline.GetTextFor(tree);
        }

        /// <summary>
        /// Confirms the consistency.  Returns true if the text currently associated with the
        /// parse tree matches the original data as read into the parse tree.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="isCaseSensitive">if set to <c>true</c> [is case sensitive].</param>
        /// <returns></returns>
        public virtual bool ConfirmConsistency(IParseTree tree, bool isCaseSensitive = false)
        {
            return Pipeline.ConfirmConsistency(tree, isCaseSensitive);
        }

        /// <summary>
        /// Unwraps a string that may have been bound with TSQL brackets for quoting.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual string Unwrap(string value)
        {
            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        /// <summary>
        /// Unwraps a qualified name part.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public virtual string Unwrap(TSQLParser.QualifiedNamePartContext context)
        {
            var identifier = context.Identifier();
            if (identifier != null)
            {
                return Unwrap(identifier.GetText());
            }

            return string.Join(" ", context.keyword().Select(k => k.GetText()));
        }

        /// <summary>
        /// Unwraps the qualified name.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public virtual string Unwrap(TSQLParser.QualifiedNameContext context)
        {
            var nameParts = context.qualifiedNamePart();
            if (nameParts != null)
            {
                return string.Join(".", context.qualifiedNamePart().Select(Unwrap));
            }

            return context.keyword().GetText();
        }

        /// <summary>
        /// Unwraps a variable context and returns the variable name.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public virtual string Unwrap(TSQLParser.VariableContext context)
        {
            return context.Unwrap();
        }

        /// <summary>
        /// Gets the indentation for a given parse tree.
        /// </summary>
        /// <param name="parseTree">The parse tree.</param>
        public string GetIndentationFor(IParseTree parseTree)
        {
            if (parseTree is TerminalNodeImpl)
            {
                var terminalNode = (TerminalNodeImpl)parseTree;
                return GetLine(terminalNode.Symbol.Line).Substring(0, terminalNode.Symbol.Column);
            }
            else if (parseTree is ParserRuleContext)
            {
                var ruleContext = (ParserRuleContext)parseTree;
                return GetLine(ruleContext.Start.Line).Substring(0, ruleContext.Start.Column);
            }

            throw new InvalidOperationException();
        }
    }
}
