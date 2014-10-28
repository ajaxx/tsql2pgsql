using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tsql2pgsql.visitors
{
    using antlr;
    using grammar;
    using pipeline;

    internal class StatementTerminatorVisitor : PipelineVisitor
    {
        /// <summary>
        /// Visits the statement.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitStatement(TSQLParser.StatementContext context)
        {
            var result = base.VisitStatement(context);

            if (context.SEMICOLON() == null)
            {
                var dml = context.dml();
                if (dml != null)
                {
                    var rMostToken = dml.RightMostToken();
                    if (rMostToken != null && rMostToken.Type != TSQLParser.SEMICOLON)
                        InsertAfter(dml, ";");
                }

                var ddl = context.ddl();
                if (ddl != null)
                {
                    var rMostToken = ddl.RightMostToken();
                    if (rMostToken != null && rMostToken.Type != TSQLParser.SEMICOLON)
                        InsertAfter(ddl, ";");
                }
            }

            return result;
        }

        /// <summary>
        /// Visits the set session other.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitSetSessionOther(TSQLParser.SetSessionOtherContext context)
        {
            return base.VisitSetSessionOther(context);
        }
    }
}
