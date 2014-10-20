using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tsql2pgsql.visitors
{
    using grammar;

    /// <summary>
    /// Attempts to guess what type of return value will come from a parse tree.
    /// </summary>
    internal class ReturnTypeVisitor : TSQLBaseVisitor<string>
    {
        /// <summary>
        /// Gets or sets the type of the return.
        /// </summary>
        /// <value>
        /// The type of the return.
        /// </value>
        internal string ReturnType { get; set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="ReturnTypeVisitor"/> class from being created.
        /// </summary>
        internal ReturnTypeVisitor()
        {
            ReturnType = "void";
        }

        /// <summary>
        /// Visits the specified tree.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <returns></returns>
        public override string Visit(Antlr4.Runtime.Tree.IParseTree tree)
        {
            base.Visit(tree);
            return ReturnType;
        }

        /// <summary>
        /// Visits the return expression.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override string VisitReturnExpression(TSQLParser.ReturnExpressionContext context)
        {
            var expressionTypeVisitor = new ExpressionTypeVisitor();
            var expressionType = expressionTypeVisitor.VisitExpression(context.expression());
            if (expressionType == typeof(string))
                ReturnType = "text";
            else if (expressionType == typeof(decimal))
                ReturnType = "numeric";
            else if (expressionType == typeof(int))
                ReturnType = "int";
            else if (expressionType == typeof(DateTime))  // how do we capture date vs timestamp
                ReturnType = "timestamp";
            else if (expressionType == typeof(bool))
                ReturnType = "boolean";

            return null;
        }
    }
}
