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
        private string _returnType;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReturnTypeVisitor"/> class.
        /// </summary>
        public ReturnTypeVisitor()
        {
            _returnType = "VOID";
        }

        public override string VisitReturnExpression(TSQLParser.ReturnExpressionContext context)
        {
            if (context.expression() == null)
            {
                _returnType = "VOID";
                return null;
            }

            // guess we're going to have to unwrap this expression to see what we can introspect
            return base.VisitReturnExpression(context);
        }
    }
}
