using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Common.Logging;

using tsql2pgsql.grammar;

namespace tsql2pgsql.visitors
{
    using pipeline;

    /// <summary>
    /// This pipeline visitor changes the names of variables in the source parseTree.
    /// The result of this pipeline visitor is valid T-SQL.
    /// </summary>
    internal class VariableNameConverter : PipelineVisitor
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Defines the string that will be used to replace the '@' in front of parameters.
        /// </summary>
        public string ParameterPrefix { get; set; }

        /// <summary>
        /// Defines the string that will be used to replace the '@' in front of variables.
        /// </summary>
        public string VariablePrefix { get; set; }

        /// <summary>
        /// Gets or sets the parameters.
        /// </summary>
        /// <value>
        /// The parameters.
        /// </value>
        internal ISet<string> Parameters { get; set; } 

        /// <summary>
        /// Creates a pgsql converter.
        /// </summary>
        public VariableNameConverter()
        {
            Parameters = new HashSet<string>();
            ParameterPrefix = "_p";
            VariablePrefix = "_v";
        }

        /// <summary>
        /// Ports the name of the variable.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns></returns>
        private string PortVariableName(string variableName)
        {
            if (variableName[0] == '@')
            {
                return
                    Parameters.Contains(variableName) ?
                    ParameterPrefix + variableName.Substring(1) :
                    VariablePrefix + variableName.Substring(1);
            }
            return variableName;
        }

        /// <summary>
        /// Visits the procedure parameter.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitProcedureParameter(TSQLParser.ProcedureParameterContext context)
        {
            Parameters.Add(Unwrap(context.procedureParameterName().variable()));
            return base.VisitProcedureParameter(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.variable" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitVariable(TSQLParser.VariableContext context)
        {
            var variableName = context.GetText();
            if (variableName.StartsWith("@@"))
            {

            }
            else if (ConfirmConsistency(context))
            {
                ReplaceText(
                    context.Start.Line,
                    context.Start.Column,
                    context.GetText(),
                    PortVariableName(variableName),
                    true);
            }

            return base.VisitVariable(context);
        }

    }
}
