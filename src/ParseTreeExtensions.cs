using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Antlr4.Runtime.Tree;

namespace tsql2pgsql
{
    public static class ParseTreeExtensions
    {
        /// <summary>
        /// Returns the rightmost leaf node.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static IParseTree RightMostChild(this IParseTree context)
        {
            if (context.ChildCount == 0)
                return context;
            return RightMostChild(context.GetChild(context.ChildCount - 1));
        }
    }
}
