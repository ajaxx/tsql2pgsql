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
        /// Returns true if the child has a parent of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="child"></param>
        /// <returns></returns>
        public static bool HasParent<T>(this IParseTree child)
        {
            return FindParent<T>(child) != null;
        }

        /// <summary>
        /// Returns the first parent of type T.
        /// Returns null if no type is found.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="child"></param>
        /// <returns></returns>
        public static IParseTree FindParent<T>(this IParseTree child)
        {
            if (child.Parent == null)
                return null;
            else if (child.Parent is T)
                return child.Parent;
            else
                return FindParent<T>(child.Parent);
        }

        /// <summary>
        /// Returns the rightmost leaf node.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IParseTree RightMostChild(this IParseTree context)
        {
            if (context.ChildCount == 0)
                return context;
            return RightMostChild(context.GetChild(context.ChildCount - 1));
        }
    }
}
