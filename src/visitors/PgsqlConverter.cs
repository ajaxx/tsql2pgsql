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

namespace tsql2pgsql.visitors
{
    using antlr;
    using collections;
    using grammar;
    using pipeline;

    internal class PgsqlConverter : PipelineVisitor
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Collection of all known parameters.
        /// </summary>
        private readonly ISet<string> _parameters = new HashSet<string>();

        /// <summary>
        /// Map of all variables.
        /// </summary>
        private IDictionary<string, TSQLParser.VariableDeclarationContext> Variables;

        private string _returnType;

        /// <summary>
        /// An index that indicates the line after which the declare block should be inserted.
        /// </summary>
        private int _declareBlockAfter;

        /// <summary>
        /// Defines the string that will be used to replace the '@' in front of parameters.
        /// </summary>
        public string ParameterPrefix { get; set; }

        /// <summary>
        /// Defines the string that will be used to replace the '@' in front of variables.
        /// </summary>
        public string VariablePrefix { get; set; }

        /// <summary>
        /// Gets the basic function map table.
        /// </summary>
        /// <value>
        /// The basic function map table.
        /// </value>
        public IDictionary<string, string> BasicFunctionMapTable { get; private set; }

        /// <summary>
        /// Gets the advanced function map table.
        /// </summary>
        /// <value>
        /// The advanced function map table.
        /// </value>
        public IDictionary<string, Func<string, string>> AdvancedFunctionMapTable { get; private set; }

        /// <summary>
        /// Creates a pgsql converter.
        /// </summary>
        public PgsqlConverter()
        {
            Variables = new Dictionary<string, TSQLParser.VariableDeclarationContext>();
            VariablePrefix = "_v";
            ParameterPrefix = "_p";

            // basic function mapping
            BasicFunctionMapTable = new Dictionary<string, string>();
            BasicFunctionMapTable["getdate"] = "utcnow";
            BasicFunctionMapTable["ERROR_NUMBER"] = "SQLSTATE";
            BasicFunctionMapTable["ERROR_MESSAGE"] = "SQLERRM";

            // advanced function mapping
            AdvancedFunctionMapTable = new Dictionary<string, Func<string, string>>();
        }

        /// <summary>
        /// Visits the specified pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <returns></returns>
        public override PipelineResult Visit(Pipeline pipeline)
        {
            base.Visit(pipeline.ParseTree);
            return new PipelineResult
            {
                RebuildPipeline = false,
                Contents = GetContent()
            };
        }

        /// <summary>
        /// Gets the refined and filtered content for the procedure.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<string> GetContent()
        {
            var prevLine = string.Empty;
            foreach (var line in GetRawContent())
            {
                var trimLine = line.TrimEnd();
                if (trimLine.TrimStart() == ";")
                    trimLine = string.Empty;
                if (trimLine != string.Empty || prevLine != string.Empty)
                    yield return trimLine;

                prevLine = trimLine;
            }
        }

        /// <summary>
        /// Gets the unfiltered raw content for the procedure.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<string> GetRawContent()
        {
            var contents = Pipeline.Contents;

            foreach (var line in contents.Take(_declareBlockAfter))
                yield return line;
            foreach (var line in GetDeclareBlock())
                yield return line;

            yield return "BEGIN";

            var bodyLines = string.Join("\n", contents.Skip(_declareBlockAfter)).Split('\n');
            foreach (var line in bodyLines.Select(l => "\t" + l))
                yield return line;

            yield return "END";
            yield return "$$ LANGUAGE plpgsql";
        }

        /// <summary>
        /// Unwraps a function name
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private string Unwrap(TSQLParser.FunctionNameContext context)
        {
            var functionName = 
                context.qualifiedName() != null ?
                Unwrap(context.qualifiedName()) :
                Unwrap(context.keyword().GetText());

            return functionName;
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
            {
                return 
                    (_parameters.Contains(variableName)) ?
                    ParameterPrefix + variableName.Substring(1) : 
                    VariablePrefix + variableName.Substring(1) ;
            }
            return variableName;
        }

        /// <summary>
        /// Ports the name of a table.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <returns></returns>
        private string PortTableName(string tableName)
        {
            if (tableName.StartsWith("#tmp"))
                return "_tmp" + tableName.Substring(4);
            if (tableName.StartsWith("#"))
                return "_tmp" + tableName.Substring(1);
            return tableName;
        }

        /// <summary>
        /// Gets the declare block
        /// </summary>
        /// <returns></returns>
        private IEnumerable<string> GetDeclareBlock()
        {
            if (Variables.Values.Count > 0)
            {
                yield return "DECLARE";

                foreach (var variableDeclarationContext in Variables.Values)
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

        /// <summary>
        /// Gets the indentation for a given parse tree.
        /// </summary>
        /// <param name="parseTree">The parse tree.</param>
        public string GetIndentationFor(IParseTree parseTree)
        {
            if (parseTree is TerminalNodeImpl)
            {
                var terminalNode = (TerminalNodeImpl)parseTree;
                return GetLine(terminalNode.Symbol.Line).Substring(0, terminalNode.Symbol.Column);
            }
            else if (parseTree is ParserRuleContext)
            {
                var ruleContext = (ParserRuleContext)parseTree;
                return GetLine(ruleContext.Start.Line).Substring(0, ruleContext.Start.Column);
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Visits the variable declaration.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitVariableDeclaration(TSQLParser.VariableDeclarationContext context)
        {
            Variables[Unwrap(context.variable())] = context;

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
                    Log.DebugFormat("VisitVariableDeclaration: Removing declaration {0}", context);
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

            // see if the target expression is using @@ROWCOUNT as this requires use of the
            // get diagnostics function
            if (IsUsingRowCount(context.expression()))
            {
                var expression = context.expression();
                if (expression.primary() != null &&
                    expression.primary().variable() != null)
                {
                    // this means that the expression is a direct assignment from the @@ROWCOUNT - this
                    // can be translated directly into a GET DIAGNOSTIC call rather than needing an
                    // intermediary
                    ReplaceToken(setContext.SET(), "GET DIAGNOSTICS");
                    Replace(expression.primary().variable(), "ROW_COUNT");
                }
            }
            else
            {
                Remove(setContext.SET());

                var tokEquals = setAssignment.GetToken(TSQLParser.EQUALS, 0);
                if (tokEquals != null)
                {
                    ReplaceToken(tokEquals, ":=", false);
                }
            }

            return base.VisitSetVariableAssignment(context);
        }

        /// <summary>
        /// Determines whether the parse tree is using @@ROWCOUNT
        /// </summary>
        /// <param name="parseTree">The parse tree.</param>
        /// <returns></returns>
        private static bool IsUsingRowCount(IParseTree parseTree)
        {
            if (parseTree is TSQLParser.VariableContext)
            {
                var variableContext = (TSQLParser.VariableContext) parseTree;
                if (variableContext.IsRowCountVariable())
                    return true;
            }
            else if (parseTree is ITerminalNode)
            {
                return false; // @@ROWCOUNT is not a terminal
            }
            else
            {
                for (int ii = 0; ii < parseTree.ChildCount; ii++)
                {
                    if (IsUsingRowCount(parseTree.GetChild(ii)))
                    {
                        return true;
                    }
                }
            }

            return false;
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

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.raiseError" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitRaiseError(TSQLParser.RaiseErrorContext context)
        {
            ReplaceToken(context.RAISE_ERROR().Symbol, "RAISE NOTICE");
            return base.VisitRaiseError(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.tryBlock" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitTryBlock(TSQLParser.TryBlockContext context)
        {
            RemoveToken(context.TRY(0).Symbol, false);
            RemoveToken(context.TRY(1).Symbol, false);
            RemoveToken(context.END(0).Symbol, false);
            RemoveToken(context.CATCH(0).Symbol, false);
            RemoveToken(context.CATCH(1).Symbol, false);

            // exception handling in PLPGSQL is exception specific, much like
            // try catch blocks in other languages.  however, SQL Server provides
            // the error details in variables that are exposed to the exception
            // handler.

            ReplaceToken(context.BEGIN(1).Symbol, "EXCEPTION WHEN OTHERS THEN ");

            return base.VisitTryBlock(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.createTable" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitCreateTable(TSQLParser.CreateTableContext context)
        {
            if (context.tempTable() != null)
            {
                InsertAfter(context.CREATE(), " TEMPORARY", false);
            }

            return base.VisitCreateTable(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.createIndex" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitCreateIndex(TSQLParser.CreateIndexContext context)
        {
            if (context.clusterType() != null)
            {
                RemoveLeaves(context.clusterType());
            }
            
            return base.VisitCreateIndex(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.tempTable" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitTempTable(TSQLParser.TempTableContext context)
        {
            // We need to determine which type of temporary table reference this is.
            // The first kind is the traditional #name or ##name.
            // The second kind is qualified by schema like app.#name
            //
            // We will only concern ourselves with the first kind since the second
            // kind will be handled in a recursive visit.

            var hash = context.HASH();
            if (hash != null && hash.Length > 0)
            {
                var tmpTablePrefix = string.Join("", hash.Select(h => "#"));
                var tmpTableSuffix = string.Empty;
                if (context.qualifiedNamePart() != null)
                {
                    tmpTableSuffix = Unwrap(context.qualifiedNamePart());
                }
                else
                {
                    tmpTableSuffix = Unwrap(context.keyword().GetText());
                }

                if (ConfirmConsistency(context))
                {
                    ReplaceText(
                        context.Start.Line,
                        context.Start.Column,
                        context.GetText(),
                        PortTableName(tmpTablePrefix + tmpTableSuffix),
                        true);
                }
                else
                {
                    Console.WriteLine("FAILED: \"{0}\" | \"{1}\"", context.GetText(), GetTextFor(context));
                }
            }

            return base.VisitTempTable(context);
        }

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
            var returnTypeVisitor = new ReturnTypeVisitor();
            var returnType = returnTypeVisitor.Visit(context.procedureBody());

            ReplaceToken(context.AS(), string.Format("RETURNS {0} AS\n$$", returnType));

            _declareBlockAfter = context.AS().Symbol.Line;

            return base.VisitCreateProcedure(context);
        }

        /// <summary>
        /// Visits the procedure parameter.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitProcedureParameter(TSQLParser.ProcedureParameterContext context)
        {
            _parameters.Add(Unwrap(context.procedureParameterName().variable()));
            return base.VisitProcedureParameter(context);
        }

        #endregion

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.ifStatement" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitIfStatement(TSQLParser.IfStatementContext context)
        {
            var result = base.VisitIfStatement(context);

            // lets find out what our indentation looks like.. sucks, but we like to
            // ensure consistent indentation on the line.
            var indentation = GetIndentationFor(context.IF());
            var endIfText = string.Format("\n{0}{1}", indentation, "END IF");
            var thenText = string.Format("{1}\n{0}\t", indentation, " THEN");

            InsertAfter(context.predicateList(), thenText, false);

            if (context.ELSE() == null)
            {
                InsertAfter(context.statement(0), endIfText, false);
            }
            else
            {
                InsertAfter(context.statement(1), endIfText, false);
            }

            return result;
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.statement" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitStatement(TSQLParser.StatementContext context)
        {
            var result = base.VisitStatement(context);

            if (context.SEMICOLON() == null)
            {
                var dml = context.dml();
                if (dml != null)
                {
                    InsertAfter(dml, ";");
                    return result;
                }

                var ddl = context.ddl();
                if (ddl != null)
                {
                    InsertAfter(ddl, ";");
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Visits the convert expression.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitConvertExpression(TSQLParser.ConvertExpressionContext context)
        {
            var result = base.VisitConvertExpression(context);

            // CONVERT is a SQL Server specific conversion
            // CAST is an ANSI-SQL conversion.

            if (context.integerValue() == null)
            {
                var expressionText = GetTextFor(context.expression());
                var typeText = GetTextFor(context.type());
                var newText = expressionText + " AS " + typeText;

                ReplaceToken(context.CONVERT(), "CAST");
                RemoveBetween(context.LPAREN().Symbol, context.RPAREN().Symbol);
                InsertAfter(context.LPAREN().Symbol, newText);
            }

            return result;
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.functionCall" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitFunctionCall(TSQLParser.FunctionCallContext context)
        {
            var functionName = Unwrap(context.functionName()).ToLowerInvariant();
            var functionArgs = context.argumentList();
            
            // use a lookup table to determine how we map from T-SQL functions to PL/PGSQL functions
            string remapFunctionName;
            if (BasicFunctionMapTable.TryGetValue(functionName, out remapFunctionName))
            {
                ReplaceText(
                    context.functionName().Start.Line,
                    context.functionName().Start.Column,
                    context.functionName().GetText(),     // for soundness
                    remapFunctionName);
            }

            return base.VisitFunctionCall(context);
        }
    }
}
