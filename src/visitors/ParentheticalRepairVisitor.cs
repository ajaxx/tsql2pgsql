using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Antlr4.Runtime.Tree;

namespace tsql2pgsql.visitors
{
    using antlr;
    using grammar;
    using pipeline;

    /// <summary>
    /// This pipeline visitor cleans up and adds parenthesis to statements in the
    /// pipeline.
    /// </summary>
    internal class ParentheticalRepairVisitor : PipelineVisitor
    {
        /// <summary>
        /// Determines whether the parse tree is wrapped in parenthesis.
        /// </summary>
        /// <param name="parseTree">The parse tree.</param>
        /// <returns></returns>
        private bool IsWrappedInParenthesis(IParseTree parseTree)
        {
            var lMostToken = parseTree.LeftMostToken();
            var rMostToken = parseTree.RightMostToken();
            return
                lMostToken != null && lMostToken.Type == TSQLParser.LPAREN &&
                rMostToken != null && rMostToken.Type == TSQLParser.RPAREN;
        }

        /// <summary>
        /// Wraps the parse tree in parenthesis.
        /// </summary>
        /// <param name="parseTree">The parse tree.</param>
        private void WrapInParenthesis(IParseTree parseTree)
        {
            var lMostToken = parseTree.LeftMostToken();
            var rMostToken = parseTree.RightMostToken();
            if (lMostToken != null && lMostToken.Type == TSQLParser.LPAREN &&
                rMostToken != null && rMostToken.Type == TSQLParser.RPAREN)
            {
                // wrapped
            }
            else
            {
                InsertBefore(
                    parseTree, "(", false);
                InsertAfter(
                    parseTree, ")", false);
            }
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.ifStatement" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitIfStatement(TSQLParser.IfStatementContext context)
        {
            WrapInParenthesis(context.predicateList());
            return base.VisitIfStatement(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.createProcedure" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitCreateProcedure(TSQLParser.CreateProcedureContext context)
        {
            var procedureParameters = context.procedureParameters();
            if (!IsWrappedInParenthesis(procedureParameters))
            {
                if (procedureParameters == null || procedureParameters.procedureParameter().Length == 0)
                {
                    InsertAfter(context.qualifiedName(), "()", false);
                }
                else
                {
                    InsertAfter(context.qualifiedName(), "\n(", false);
                    InsertAfter(procedureParameters, "\n)", false);
                }
            }

            return base.VisitCreateProcedure(context);
        }
    }
}
