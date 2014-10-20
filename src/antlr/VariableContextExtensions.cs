using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tsql2pgsql.antlr
{
    using grammar;

    public static class VariableContextExtensions
    {
        /// <summary>
        /// Determines whether [is special variable] [the specified context].
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static bool IsSpecialVariable(this TSQLParser.VariableContext context)
        {
            return
                context.AT() != null &&
                context.AT().Length == 2;
        }

        /// <summary>
        /// Unwraps a string that may have been bound with TSQL brackets for quoting.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string Unwrap(string value)
        {
            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        /// <summary>
        /// Unwraps a variable context and returns the variable name.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string Unwrap(this TSQLParser.VariableContext context)
        {
            var parameterName = context;
            var parameterPart = Unwrap(
                parameterName.Identifier() != null ?
                parameterName.Identifier().GetText() :
                parameterName.keyword().GetText());

            return string.Join("", context.AT().Select(a => "@")) + parameterPart;
        }

        /// <summary>
        /// Determines whether the variable context represents the special rowcount variable.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static bool IsRowCountVariable(this TSQLParser.VariableContext context)
        {
            return IsSpecialVariable(context) && 
                string.Equals(Unwrap(context), "@@rowcount", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
