using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tsql2pgsql.visitors
{
    using grammar;

    internal class VariableScanner : TSQLBaseVisitor<object>
    {
        /// <summary>
        /// Gets or sets the variables.
        /// </summary>
        /// <value>
        /// The variables.
        /// </value>
        public IDictionary<string, TSQLParser.VariableDeclarationContext> Variables { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableScanner"/> class.
        /// </summary>
        public VariableScanner()
        {
            Variables = new SortedDictionary<string, TSQLParser.VariableDeclarationContext>();
        }

        /// <summary>
        /// Visits the variable declaration.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitVariableDeclaration(TSQLParser.VariableDeclarationContext context)
        {
            Variables[context.variable().GetText()] = context;
            return base.VisitVariableDeclaration(context);
        }
    }
}
