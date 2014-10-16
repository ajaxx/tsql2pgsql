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
using System.Net.Mime;
using System.Text;

using Common.Logging;

namespace tsql2pgsql
{
    using antlr;
    using collections;
    using grammar;

    /// <summary>
    /// This class is used to rearrange and otherwise clean up a TSQL grammar prior to running
    /// it through the mutation visitor.
    /// </summary>
    internal class FormattingVisitor : DisplacementVisitor<object>
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static ILog _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Creates a common mutation engine.
        /// </summary>
        public FormattingVisitor(IEnumerable<string> lines) : base(lines)
        {
        }

        /// <summary>
        /// Visits the procedure parameters.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitProcedureParameters(TSQLParser.ProcedureParametersContext context)
        {
            var parameters = context.procedureParameter();
            if (parameters.Length != 0)
            {
                var previousContext = (TSQLParser.CreateProcedureContext) context.parent;
                var previousItemLine = previousContext.qualifiedName().Stop.Line;
                if (context.LPAREN() != null)
                {
                    previousItemLine = context.LPAREN().Symbol.Line;
                }

                foreach (var parameter in parameters)
                {
                    if (parameter.Start.Line == previousItemLine)
                    {
                        InsertBefore(parameter, "\n    ", false);
                    }

                    previousItemLine = parameter.Start.Line;
                }

                if (context.RPAREN() != null && context.RPAREN().Symbol.Line == previousItemLine)
                {
                    InsertBefore(context.RPAREN(), "\n", false);
                }
            }
            
            return base.VisitProcedureParameters(context);
        }

        /// <summary>
        /// Visits the create procedure.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitCreateProcedure(TSQLParser.CreateProcedureContext context)
        {
            // We want the CREATE PROCEDURE to be segmented into pieces so
            // that the "AS" portion of the procedure sits on a line of its
            // own.

            var asTerminal = context.AS().Symbol;

            // We know that comments are "skipped" and are not visible to use through
            // the grammar.  That's fine, what we care about is the terminal or token
            // that occurs just prior to this one and just after this one.

            var nextParseTree = context.procedureBody();
            if (nextParseTree.Start.Line == asTerminal.Line)
            {
                // this happens when the "AS" is followed by a statement on the same line.
                // we correct this by placing a carriage return just before the next terminal.

                InsertBefore(nextParseTree.Start, "\n", false);
            }

            if (((context.procedureOptions() != null) &&
                 (context.procedureOptions().Stop.Line == asTerminal.Line)) ||
                ((context.procedureParameters() != null) &&
                 (context.procedureParameters().Stop.Line == asTerminal.Line)) ||
                (context.qualifiedName().Stop.Line == asTerminal.Line))
            {
                InsertBefore(asTerminal, "\n", false);
            }

            return base.VisitCreateProcedure(context);
        }
    }
}
