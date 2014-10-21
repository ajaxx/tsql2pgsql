using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tsql2pgsql.visitors
{
    using antlr;
    using grammar;
    using pipeline;

    internal class SingleLineDeclarationVisitor : PipelineVisitor
    {
        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.declareStatement" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitDeclareStatement(TSQLParser.DeclareStatementContext context)
        {
            var indentation = GetIndentationFor(context);
            var variableDeclarationArray = context.variableDeclaration();
            if (variableDeclarationArray.Length <= 1)
                return null;

            foreach (var token in context.GetTokens(TSQLParser.COMMA))
            {
                ReplaceToken(token.Symbol, ";");
            }

            foreach (var variableDeclaration in variableDeclarationArray.Skip(1))
            {
                InsertBefore(variableDeclaration, string.Format("\n{0}DECLARE ", indentation));
            }
            
            return base.VisitDeclareStatement(context);
        }

    }
}
