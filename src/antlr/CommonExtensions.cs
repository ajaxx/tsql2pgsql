using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tsql2pgsql.antlr
{
    using grammar;

    public static class CommonExtensions
    {
        /// <summary>
        /// Unwraps a string that may have been bound with TSQL brackets for quoting.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string Unwrap(string value)
        {
            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        /// <summary>
        /// Unwraps a qualified name part.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string Unwrap(this TSQLParser.QualifiedNamePartContext context)
        {
            var identifier = context.Identifier();
            if (identifier != null)
            {
                return Unwrap(identifier.GetText());
            }

            return string.Join(" ", context.keyword().Select(k => k.GetText()));
        }

        /// <summary>
        /// Unwraps the qualified name.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string Unwrap(this TSQLParser.QualifiedNameContext context)
        {
            var nameParts = context.qualifiedNamePart();
            if (nameParts != null && nameParts.Length > 0)
            {
                return string.Join(".", context.qualifiedNamePart().Select(Unwrap));
            }

            return context.escapedKeyword().keyword().GetText();
        }

        /// <summary>
        /// Unwraps a function name
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string Unwrap(this TSQLParser.FunctionNameContext context)
        {
            var functionName =
                context.qualifiedName() != null ?
                Unwrap(context.qualifiedName()) :
                Unwrap(context.keyword().GetText());

            return functionName;
        }

        /// <summary>
        /// Unwraps the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static string Unwrap(this TSQLParser.TableTargetContext context)
        {
            return
                context.qualifiedName() != null ?
                Unwrap(context.qualifiedName()) :
                context.variable() != null ?
                context.variable().Unwrap() :
                context.tempTable().Unwrap();
        }

        /// <summary>
        /// Unwraps the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static string Unwrap(this TSQLParser.KeywordContext context)
        {
            return Unwrap(context.GetText());
        }

        /// <summary>
        /// Unwraps the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static string Unwrap(this TSQLParser.TempTableContext context)
        {
            return Unwrap(context.GetText());
        }

        /// <summary>
        /// Unwraps the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static string Unwrap(this TSQLParser.TypeContext context)
        {
            if (context.qualifiedName() != null)
                return context.qualifiedName().Unwrap();
            if (context.typeInBracket() != null)
                return context.typeInBracket().type().Unwrap();

            return Unwrap(context.GetText());
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
    }
}
