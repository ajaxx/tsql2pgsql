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
        /// Determines whether the variable context represents the special rowcount variable.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static bool IsRowCountVariable(this TSQLParser.VariableContext context)
        {
            return IsSpecialVariable(context) && 
                string.Equals(context.Unwrap(), "@@rowcount", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
