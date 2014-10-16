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
using System.Text;
using System.Text.RegularExpressions;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using Common.Logging;

using tsql2pgsql.visitors;

namespace tsql2pgsql
{
    using antlr;
    using collections;
    using grammar;

    internal class MutationVisitor : DisplacementVisitor<object>
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static readonly ILog _log = LogManager.GetCurrentClassLogger();

        private IDictionary<string, TSQLParser.VariableDeclarationContext> _variables;
        private string _returnType;

        /// <summary>
        /// An index that indicates the line after which the declare block should be inserted.
        /// </summary>
        private int _declareBlockAfter;

        /// <summary>
        /// Creates a common mutation engine.
        /// </summary>
        public MutationVisitor(IEnumerable<string> lines) : base(lines)
        {
            _variables = null;
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        /// <returns></returns>
        public override string[] Process()
        {
            // Collect the variables prior to executing
            var variableVisitor = new VariablesVisitor();
            variableVisitor.Visit(UnitContext);
            _variables = variableVisitor.Variables;

            // Apply the mutationt
            Visit(UnitContext);

            // Get the content
            var result = new List<string>();
            result.AddRange(Lines.Take(_declareBlockAfter));
            result.AddRange(GetDeclareBlock());
            result.AddRange(Lines.Skip(_declareBlockAfter));

            return result.ToArray();
        }

        /// <summary>
        /// Ports the type.
        /// </summary>
        /// <param name="typeName">Name of the type.</param>
        /// <returns></returns>
        private string PortDataType(string typeName)
        {
            switch (typeName.ToLowerInvariant())
            {
                case "bit":
                    return "boolean";
                case "date":
                case "datetime":
                case "smalldatetime":
                    return "date";
            }

            return typeName;
        }

        /// <summary>
        /// Ports the name of the variable.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns></returns>
        private string PortVariableName(string variableName)
        {
            if (variableName[0] == '@')
                return "_v" + variableName.Substring(1);
            return variableName;
        }

        /// <summary>
        /// Gets the declare block
        /// </summary>
        /// <returns></returns>
        private IEnumerable<string> GetDeclareBlock()
        {
            if (_variables.Values.Count > 0)
            {
                yield return "DECLARE";

                foreach (var variableDeclarationContext in _variables.Values)
                {
                    var pgsqlDeclaration = new StringBuilder();
                    pgsqlDeclaration.Append('\t');
                    pgsqlDeclaration.Append(PortVariableName(variableDeclarationContext.variable().GetText()));
                    pgsqlDeclaration.Append(' ');
                    pgsqlDeclaration.Append(PortDataType(variableDeclarationContext.type().GetText()));
                    pgsqlDeclaration.Append(';');

                    yield return pgsqlDeclaration.ToString();
                }
            }
        }

        /// <summary>
        /// Finds the statement context.
        /// </summary>
        /// <param name="parseTree">The parse tree.</param>
        /// <returns></returns>
        public TSQLParser.StatementContext GetStatementContext(IParseTree parseTree)
        {
            return parseTree.FindParent<TSQLParser.StatementContext>();
        }

        /// <summary>
        /// Removes the statement.
        /// </summary>
        /// <param name="parseTree">The parse tree.</param>
        public void RemoveStatement(IParseTree parseTree)
        {
            var statementContext = GetStatementContext(parseTree);
            if (statementContext != null)
            {
                RemoveLeaves(statementContext);
            }
        }

        #region return

        /// <summary>
        /// Visits the return expression.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitReturnExpression(TSQLParser.ReturnExpressionContext context)
        {
            return base.VisitReturnExpression(context);
        }

        #endregion

        /// <summary>
        /// Visits the variable declaration.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitVariableDeclaration(TSQLParser.VariableDeclarationContext context)
        {
            if (context.TABLE() != null)
            {

            }
            else
            {
                var assignment = context.variableDeclarationAssignment();
                var assignmentExpression = assignment.expression();
                if (assignmentExpression != null)
                {
                    // convert the statement into an assignment ... all variable declarations should be
                    // single line by the time they get to this point.  this allows us to go up to the
                    // parent and remove the unnecessary parts
                    var parentContext = (TSQLParser.DeclareStatementContext) context.Parent;
                    Remove(parentContext.DECLARE());
                    InsertAfter(context.variable(), " := ", false);
                }
                else
                {
                    RemoveStatement(context);
                }
            }
            //else
            //{
            //    RemoveStatement(context);
            //}

            return base.VisitVariableDeclaration(context);
        }

        /// <summary>
        /// Called when we encounter a type that has been quoted according to TSQL convention.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitTypeInBracket(TSQLParser.TypeInBracketContext context)
        {
            Console.WriteLine(context.type().GetText());
            return base.VisitTypeInBracket(context);
        }

        /// <summary>
        /// Called when we encounter a name part.  Since nameparts often contain quotation symbology specific to
        /// TSQL, we need to convert it to PL/PGSQL friendly notation.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitQualifiedNamePart(TSQLParser.QualifiedNamePartContext context)
        {
            var identifierTree = context.Identifier();
            var identifier = identifierTree.GetText().Trim();
            if ((identifier.Length > 2) &&
                (identifier[0] == '[') &&
                (identifier[identifier.Length - 1] == ']'))
            {
                identifier = identifier.Substring(1, identifier.Length - 2);
                if (!Regex.IsMatch(identifier, "^[a-zA-Z][a-zA-Z0-9_]*"))
                {
                    identifier = string.Format("\"{0}\"", identifier);
                }

                ReplaceToken(identifierTree.Symbol, identifier);
            }

            return base.VisitQualifiedNamePart(context);
        }

        #region type

        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitType(TSQLParser.TypeContext context)
        {
            // some of the internal types are going to get modified ...
            if (context.qualifiedName() != null)
            {
                var name = context.qualifiedName();
                var nameTextA = name.GetText().ToLowerInvariant();
                var nameTextB = GetTextFor(name).ToLowerInvariant();
                if (nameTextB != nameTextA)
                    return null;

                switch (nameTextA)
                {
                    case "bit":
                        ReplaceToken(name.Start, "boolean");
                        break;
                    case "datetime":
                        ReplaceToken(name.Start, "date");
                        break;
                }
            }

            return base.VisitType(context);
        }

        public override object VisitCharacterStringType(TSQLParser.CharacterStringTypeContext context)
        {
            if (context.NVARCHAR() != null)
                ReplaceToken(context.NVARCHAR(), "varchar");
            else if (context.NCHAR() != null)
                ReplaceToken(context.NCHAR(), "char");

            return base.VisitCharacterStringType(context);
        }

        #endregion

        public override object VisitVariable(TSQLParser.VariableContext context)
        {
            var tsqlVariableName = context.GetText();
            if (tsqlVariableName.StartsWith("@@"))
            {

            }
            else if (ConfirmConsistency(context))
            {
                ReplaceText(
                    context.Start.Line,
                    context.Start.Column,
                    context.GetText(),
                    PortVariableName(tsqlVariableName),
                    true);
            }

            return base.VisitVariable(context);
        }

        #region variable assignment

        /// <summary>
        /// Visits the set variable assignment.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitSetVariableAssignment(TSQLParser.SetVariableAssignmentContext context)
        {
            var setContext = (TSQLParser.SetStatementContext) context.Parent;
            var setAssignment = context.assignmentOperator();

            Remove(setContext.SET());

            var tokEquals = setAssignment.GetToken(TSQLParser.EQUALS, 0);
            if (tokEquals != null)
            {
                ReplaceToken(tokEquals, ":=", false);
            }

            return base.VisitSetVariableAssignment(context);
        }

        /// <summary>
        /// Visits the set session other.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitSetSessionOther(TSQLParser.SetSessionOtherContext context)
        {
            if (context.TRANSACTION() != null)
            {
                // SET TRANSACTION is supported by PGSQL
            }
            else if (context.ROWCOUNT() != null)
            {
                RemoveStatement(context);
            }
            else if (!context.qualifiedName().IsNullOrEmpty())
            {
                var qualifiedNameList = context.qualifiedName();
                if (qualifiedNameList.Length == 1)
                {
                    switch (qualifiedNameList[0].GetText().ToLower())
                    {
                        case "nocount":
                            // delete the item
                            RemoveStatement(context);
                            return null;
                    }
                }
            }

            return base.VisitSetSessionOther(context);
        }
        
        #endregion

        #region create procedure

        /// <summary>
        /// Called when we encounter "CREATE PROCEDURE"
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitCreateProcedure(TSQLParser.CreateProcedureContext context)
        {
            ReplaceToken(context.CREATE(), "CREATE OR REPLACE");
            ReplaceToken(context.PROCEDURE(), "FUNCTION");

            // we need to add a return value for this function... assuming there is one, is there
            // any way that we can introspect the rest of the file to determine the return type?
            // in the absence of a return type, we're returning SETOF RECORD
            var functionReturnType = "VOID";

            ReplaceToken(context.AS(), string.Format("RETURNS {0} AS\n$$", functionReturnType));

            _declareBlockAfter = context.AS().Symbol.Line;

            return base.VisitCreateProcedure(context);
        }

        /// <summary>
        /// Visits the procedure parameters.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitProcedureParameters(TSQLParser.ProcedureParametersContext context)
        {
            Console.WriteLine("LPAREN: {0}", context.LPAREN());
            Console.WriteLine("RPAREN: {0}", context.RPAREN());
            return base.VisitProcedureParameters(context);
        }

        /// <summary>
        /// Visits the procedure parameter.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitProcedureParameter(TSQLParser.ProcedureParameterContext context)
        {
            return base.VisitProcedureParameter(context);
        }

        #endregion

        public override object VisitStatement(TSQLParser.StatementContext context)
        {
            var dml = context.dml();
            if (dml != null && context.SEMICOLON() == null)
            {
                InsertAfter(dml, ";");
            }

            return base.VisitStatement(context);
        }
    }
}
