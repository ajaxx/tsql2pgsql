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
using System.Text.RegularExpressions;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using Common.Logging;
using Common.Logging.Configuration;

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
        /// Gets or sets the parameters.
        /// </summary>
        /// <value>
        /// The parameters.
        /// </value>
        public ISet<string> Parameters { get; set; }

        /// <summary>
        /// Gets or sets the capitalization style.
        /// </summary>
        /// <value>
        /// The capitalization style.
        /// </value>
        public CapitalizationStyle CapitalizationStyle { get; set; }

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
        /// Provides us with an indication that we have used a smart record.
        /// </summary>
        private bool _useSmartRecord;

        /// <summary>
        /// Creates a pgsql converter.
        /// </summary>
        public PgsqlConverter() : base(false)
        {
            _useSmartRecord = false;

            CapitalizationStyle = CapitalizationStyle.PascalCase;
            Parameters = new HashSet<string>();
            ParameterPrefix = "_p";
            Variables = new Dictionary<string, TSQLParser.VariableDeclarationContext>();
            VariablePrefix = "_v";

            // basic function mapping
            BasicFunctionMapTable = new Dictionary<string, string>();
            BasicFunctionMapTable["getdate"] = "utcnow";
            BasicFunctionMapTable["scope_identity"] = "lastval";
            BasicFunctionMapTable["error_number"] = "SQLSTATE";
            BasicFunctionMapTable["error_message"] = "SQLERRM";

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
                while (trimLine.EndsWith(";;"))
                    trimLine = trimLine.Substring(0, trimLine.Length - 1);
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

            var bodyLines = string.Join("\n", contents.Skip(_declareBlockAfter)).Split('\n');

            // next line should be the "BEGIN" token for the block
            yield return bodyLines[0];

            foreach (var line in bodyLines.Skip(1).Take(bodyLines.Length - 2).Select(l => "\t" + l))
                yield return line;

            // next line should be the "END" token for the block
            yield return bodyLines[bodyLines.Length - 1];

            yield return "$$ LANGUAGE plpgsql";
        }

        /// <summary>
        /// Parses the embedded parameter list.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns></returns>
        private TSQLParser.EmbeddedParameterListContext EmbeddedParameterList(string content)
        {
            var stream = new CaseInsensitiveStream(content);
            var lexer = new TSQLLexer(stream);
            var parser = new TSQLParser(new CommonTokenStream(lexer));
            return parser.embeddedParameterList();
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
                case "sysname":
                    return "text";
            }

            return typeName;
        }

        /// <summary>
        /// Ports the type of the data.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        private string PortDataType(TSQLParser.NumericTypeContext context)
        {
            return context.GetText();
        }

        /// <summary>
        /// Ports the type of the data.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        private string PortDataType(TSQLParser.CharacterStringTypeContext context)
        {
            var characterStringTypeLength = context.characterStringTypeLength();
            if (characterStringTypeLength != null)
            {
                if (characterStringTypeLength.MAX() != null)
                {
                    return "text";
                }

                if (context.NVARCHAR() != null)
                    return string.Format("varchar{0}", characterStringTypeLength.GetText());
                if (context.NCHAR() != null)
                    return string.Format("char{0}", characterStringTypeLength.GetText());
            }
            else
            {
                if (context.NVARCHAR() != null)
                    return "varchar";
                if (context.NCHAR() != null)
                    return "char";
            }

            return context.GetText();
        }

        /// <summary>
        /// Ports the type.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        private string PortDataType(TSQLParser.TypeContext context)
        {
            if (context.integerType() != null)
                return PortDataType(context.integerType().GetText());
            if (context.typeInBracket() != null)
                return PortDataType(context.typeInBracket().type());
            if (context.XML() != null)
                return PortDataType(context.GetText());
            if (context.CURSOR() != null)
                return PortDataType(context.GetText());
            if (context.qualifiedName() != null)
                return PortDataType(context.GetText());
            if (context.numericType() != null)
                return PortDataType(context.numericType());
            if (context.characterStringType() != null)
                return PortDataType(context.characterStringType());

            throw new ArgumentException("you used something we didnt plan on");
        }

        /// <summary>
        /// Capitalizes to style.
        /// </summary>
        /// <param name="value">The value.</param>
        private string Capitalize(string value)
        {
            switch (CapitalizationStyle)
            {
                case CapitalizationStyle.None:
                    return value;
                case CapitalizationStyle.PascalCase:
                    return Char.ToUpperInvariant(value[0]) + value.Substring(1);
                case CapitalizationStyle.CamelCase:
                    return Char.ToLowerInvariant(value[0]) + value.Substring(1);
            }

            return value;
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
                    ParameterPrefix + Capitalize(variableName.Substring(1)) : 
                    VariablePrefix + Capitalize(variableName.Substring(1)) ;
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

                foreach (var variable in GetVariables())
                {
                    var pgsqlDeclaration = new StringBuilder();
                    pgsqlDeclaration.Append('\t');
                    pgsqlDeclaration.Append(variable.A);
                    pgsqlDeclaration.Append(' ');
                    pgsqlDeclaration.Append(variable.B);
                    pgsqlDeclaration.Append(';');

                    yield return pgsqlDeclaration.ToString();
                }
            }
        }

        public IEnumerable<Pair<string, string>> GetVariables()
        {
            foreach (var variableDeclarationContext in Variables.Values)
            {
                if (variableDeclarationContext.type() != null)
                {
                    yield return new Pair<string, string>(
                        PortVariableName(variableDeclarationContext.variable().GetText()),
                        PortDataType(variableDeclarationContext.type()));
                }
            }

            if (_useSmartRecord)
            {
                yield return new Pair<string, string>(
                    "_vSmartRecord",
                    "RECORD");
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
        /// Visits the variable declaration.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitVariableDeclaration(TSQLParser.VariableDeclarationContext context)
        {
            Variables[context.variable().Unwrap()] = context;

            if (context.TABLE() != null)
            {
                var parentContext = (TSQLParser.DeclareStatementContext)context.Parent;
                ReplaceToken(parentContext.DECLARE(), "CREATE TEMPORARY TABLE");
                Remove(context.TABLE());
            }
            else
            {
                var assignment = context.variableDeclarationAssignment();
                if (assignment != null)
                {
                    Console.WriteLine("assignment");
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
                }
                else
                {
                    Log.DebugFormat("VisitVariableDeclaration: Removing declaration {0}", context);
                    RemoveStatement(context);

                    // no further processing to be performed
                    return null;
                }
            }
            //else
            //{
            //    RemoveStatement(context);
            //}

            return base.VisitVariableDeclaration(context);
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
                    false);
            }

            return base.VisitVariable(context);
        }

        /// <summary>
        /// Called when we encounter a type that has been quoted according to TSQL convention.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitTypeInBracket(TSQLParser.TypeInBracketContext context)
        {
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
                if (ConfirmConsistency(name))
                {
                    var nameText = name.Unwrap();
                    var nameTextNew = PortDataType(nameText);
                    if (nameText != nameTextNew)
                    {
                        ReplaceText(
                            name.Start.Line,
                            name.Start.Column,
                            nameText.Length,
                            nameTextNew);
                    }
                }
            }

            return base.VisitType(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.characterStringType" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitCharacterStringType(TSQLParser.CharacterStringTypeContext context)
        {
            var characterStringTypeLength = context.characterStringTypeLength();
            if (characterStringTypeLength != null)
            {
                if (characterStringTypeLength.MAX() != null)
                {
                    Replace(context, "text");
                    return null;
                }
            }

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
        /// Visit a parse tree produced by <see cref="TSQLParser.insertStatement" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitInsertStatement(TSQLParser.InsertStatementContext context)
        {
            // there is a case, where T-SQL can use an insert with an OUTPUT clause.  to my
            // knowledge, there is no exact equivalent in PGSQL, however, the RETURNING keyword
            // should be more than adequate for providing equivalent behavior.  The catch is that
            // the RETURNING clause returns the value to the caller which can then insert
            // that data into a table if that's what desired.

            var insertOutputClause = context.insertOutputClause();
            if (insertOutputClause != null)
            {
                if (insertOutputClause.INTO() != null)
                {
                    var indentation = GetIndentationFor(context);
                    var selectListText = GetTextFor(insertOutputClause.selectList());
                    var targetTable = insertOutputClause.tableTarget();
                    var targetColumns = insertOutputClause.qualifiedColumnNameList();
                    var targetName =
                        targetTable.variable() != null ?
                        PortVariableName(targetTable.variable().Unwrap()) :
                        targetTable.tempTable() != null ?
                        PortTableName(targetTable.tempTable().Unwrap()) :
                        targetTable.Unwrap();

                    var returningText = string.Format("\n{0}\tRETURNING {1}", indentation, selectListText);

                    var insertText = string.Format("\n{0}) INSERT INTO {1} SELECT * FROM _tempContext", indentation, targetName);
                    if (targetColumns != null)
                        insertText = insertText + '(' + targetColumns + ')';

                    InsertBefore(context.insertPreamble(), "WITH _tempContext AS (\n" + indentation);
                    InsertAfter(context.RightMostToken(), returningText, false);
                    InsertAfter(context.RightMostToken(), insertText, false);
                    IndentRegion(context.insertPreamble().Start, context.RightMostToken());

                    RemoveLeaves(insertOutputClause);
                }
                else
                {
                    ReplaceToken(insertOutputClause.OUTPUT(), "RETURNING", false);
                }
            }

            return base.VisitInsertStatement(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.insertPreamble" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitInsertPreamble(TSQLParser.InsertPreambleContext context)
        {
            if (context.INTO() == null)
            {
                InsertAfter(context.INSERT(), " INTO ", false);
            }

            return base.VisitInsertPreamble(context);
        }
        
        /// <summary>
        /// Visits the transaction block.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitTransactionBlock(TSQLParser.TransactionBlockContext context)
        {
            // PL/PGSQL functions are automatically enrolled into transactions
            RemoveToken(context.BEGIN(), false);
            RemoveToken(context.TRANSACTION(), false);
            return base.VisitTransactionBlock(context);
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
            else if (!context.setSessionParameter().IsNullOrEmpty())
            {
                var sessionParameterList = context.setSessionParameter();
                if (sessionParameterList.Length == 1)
                {
                    switch (sessionParameterList[0].GetText().ToLower())
                    {
                        case "nocount":
                        case "quoted_identifier":
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
            ReplaceToken(context.RAISE_ERROR().Symbol, "RAISE EXCEPTION ");
            if (context.LPAREN() != null) RemoveToken(context.LPAREN());
            if (context.RPAREN() != null) RemoveToken(context.RPAREN());

            if (context.argumentList() != null)
            {
                RaiseErrorCheckArguments(context.argumentList().argument());
            }
            else if (context.argument() != null)
            {
                RaiseErrorCheckArguments(context.argument());
            }

            return base.VisitRaiseError(context);
        }

        private void RaiseErrorCheckArguments(TSQLParser.ArgumentContext[] argumentList)
        {
            var argument0 = argumentList[0];
            if (argument0.expression() != null &&
                argument0.expression().primary() != null &&
                argument0.expression().primary().literalValue() != null &&
                argument0.expression().primary().literalValue().StringLiteral() != null)
            {
                // all this to determine if this is a valid argument0
            }
            else
            {
                // argument0 cannot be a variable or other like item...
                var stringText = new StringBuilder("'");
                for (int ii = 0; ii < argumentList.Length; ii++)
                    stringText.Append('%');

                stringText.Append("', ");

                InsertBefore(argument0, stringText.ToString(), false);
            }
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
        /// Set of temp tables that have been declared.
        /// </summary>
        private readonly ISet<string> _declaredTempTables =
            new HashSet<string>();

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
                _declaredTempTables.Add(context.tempTable().Unwrap());
                InsertAfter(context.CREATE(), " TEMPORARY", false);
            }

            return base.VisitCreateTable(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.columnDefinition" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitColumnDefinition(TSQLParser.ColumnDefinitionContext context)
        {
            var identity = context.identitySpec();
            if (identity == null)
            {
                var type = context.type();
                if (type != null && type.identityType() != null)
                {
                    identity = type.identityType().identitySpec();
                }

                if (identity != null)
                {
                    Replace(type, "SERIAL");
                }
            }


            return base.VisitColumnDefinition(context);
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
                    tmpTableSuffix = context.qualifiedNamePart().Unwrap();
                }
                else
                {
                    tmpTableSuffix = context.keyword().Unwrap();
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
        /// A set of initial values to ignore.
        /// </summary>
        private readonly ISet<TSQLParser.ProcedureParameterInitialValueContext> _initialValuesToIgnore =
            new HashSet<TSQLParser.ProcedureParameterInitialValueContext>(); 

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.procedureParameters" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitProcedureParameters(TSQLParser.ProcedureParametersContext context)
        {
            // PGSQL does not allow non-defaulted parameters after a defaulted parameter.  Therefore,
            // if we detect a non-default parameter in one of the children, then any defaulted
            // parameters that occur before that one will have their default values stripped.
            var procedureParameters = context.procedureParameter();

            // Find the last index that contains a non-defaulted parameter
            var lastNonDefaultParameter = Array.FindLastIndex(procedureParameters, p => p.procedureParameterInitialValue() == null);
            if (lastNonDefaultParameter != -1)
            {
                for (int ii = 0; ii < lastNonDefaultParameter; ii++)
                {
                    var procedureParam = procedureParameters[ii];
                    var procedureParamInitial = procedureParam.procedureParameterInitialValue();
                    if (procedureParamInitial != null)
                    {
                        _initialValuesToIgnore.Add(procedureParamInitial);
                        RemoveLeaves(procedureParamInitial);
                    }
                }
            }

            return base.VisitProcedureParameters(context);
        }

        /// <summary>
        /// Visits the procedure parameter.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitProcedureParameter(TSQLParser.ProcedureParameterContext context)
        {
            if (context != null)
            {
                Parameters.Add(context.procedureParameterName().variable().Unwrap());

                var procedureParameterInitialValue = context.procedureParameterInitialValue();
                if (procedureParameterInitialValue != null && !_initialValuesToIgnore.Contains(procedureParameterInitialValue))
                {
                    switch(context.type().Unwrap().ToLowerInvariant())
                    {
                        case "bit":
                            // we know that bits are converted into booleans and that the
                            // default values are not portable as a result.  convert the
                            // bit value to a boolean value.

                            if (procedureParameterInitialValue.literalValue() != null && 
                                procedureParameterInitialValue.literalValue().integerValue() != null)
                            {
                                var integerValue = procedureParameterInitialValue.literalValue().integerValue();
                                var integerValueText = integerValue.GetText().Replace("(", "").Replace(")", "");
                                var integerValueValue = Int32.Parse(integerValueText);
                                Replace(procedureParameterInitialValue.literalValue(), integerValueValue == 1 ? "TRUE" : "FALSE");
                            }

                            break;
                    }
                }

                var outputToken = context.OUT() ?? context.OUTPUT();
                if (outputToken != null)
                {
                    InsertBefore(context, "OUT ", false);
                    RemoveToken(outputToken.Symbol, false);
                }
            }

            return base.VisitProcedureParameter(context);
        }

        #endregion

        /// <summary>
        /// Visits the execute statement.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitExecuteStatement(TSQLParser.ExecuteStatementContext context)
        {
            var result = base.VisitExecuteStatement(context);

            if (context.qualifiedName() != null)
            {
                // this appears to be a function call...
                var functionName = context.qualifiedName().Unwrap().ToLowerInvariant();
                if (functionName == "sp_executesql")
                {
                    // sp_executesql is special because we're being given a "command" to run and probably a
                    // host of out parameters.  However, out parameters are different in pgsql than from tsql
                    // instead, they are returned to the caller in the resulting rows.  As such, we need to
                    // use EXECUTE instead of PERFORM
                    Replace(context.EXECUTE(), "EXECUTE", false);
                    RemoveLeaves(context.qualifiedName());

                    var sargs = context.executeArgumentList().executeArgument();

                    // @stmt is arg[0]
                    var pstmt = sargs[0];
                    // @params is args[1] - if it exists at all
                    var pparams = sargs.Length > 1 ? sargs[1] : null;
                    if (pparams != null)
                    {
                        // alright, we no longer care about this argument - not relevant to PGSQL execute,
                        // so go ahead and remove it
                        RemoveLeaves(pparams);
                        RemoveToken(context.executeArgumentList().GetToken(TSQLParser.COMMA, 0));
                    }
                    // @param1+ are args[2+] and must match any parameters that are visible in stmt and
                    //     subsequently declared in @params.  depending on direction, these may or may
                    //     not need to be passed along.  out parameters are not translated as they are
                    //     returned and must be part of the "INTO" conversion.
                    var usingDelimiter = "";
                    var usingBlock = new StringBuilder("");

                    var intoBlock = false;

                    var pparamValues = sargs.Skip(2).ToArray();
                    // each argument must be rewritten in pstmt in addition to being added to a special
                    // using block at the end of the execute call
                    for (int ii = 0; ii < pparamValues.Length; ii++)
                    {
                        var pparamValue = pparamValues[ii];
                        if (pparamValue.OUT() != null && pparamValue.OUTPUT() != null)
                        {
                            usingBlock.Append(usingDelimiter);
                            usingBlock.Append(pparamValue.variable().Unwrap());
                            usingDelimiter = ",";
                        }
                        else
                        {
                            // output parameters need to be removed ... at the same time they need to function
                            // as part of the INTO portion of the return
                            intoBlock = _useSmartRecord = true;
                            RemoveBetween(
                                context.executeArgumentList().GetToken(TSQLParser.COMMA, 1 + ii).Symbol,
                                pparamValue.Stop);
                        }
                    }

                    var insertAfter = intoBlock ? " INTO _vSmartRecord" : "";
                    if (usingBlock.Length > 0)
                    {
                        insertAfter += " USING " + usingBlock;
                    }

                    if (! string.IsNullOrWhiteSpace(insertAfter))
                    {
                        InsertAfter(pparams, insertAfter, false);
                    }
                }
                else
                {
                    Replace(context.EXECUTE(), "PERFORM", false);

                    if (context.executeArgumentList() != null)
                    {
                        InsertBefore(context.executeArgumentList(), "(");
                        InsertAfter(context.executeArgumentList(), ")");

                        // the argument list in T-SQL is a stream of named arguments... in
                        // pgsql, the arguments are unnamed and assumed to positional.  this
                        // makes ensuring soundness more difficult as we actually need the
                        // order of positional arguments for the given stored procedure.

                        // for the time-being, we assume that the named order matches the positional
                        // order... this is a *bad* assumption but we will need to get the positional
                        // argument order in order to make the magic happen.

                        foreach (var executeArgument in context.executeArgumentList().executeArgument())
                        {
                            if (executeArgument.EQUALS() != null)
                            {
                                RemoveLeaves(executeArgument.variable());
                                RemoveToken(executeArgument.EQUALS());
                            }
                        }
                    }
                    else
                    {
                        InsertAfter(context.qualifiedName(), "()");
                    }
                }
            }
            else
            {
                Replace(context.EXECUTE(), "PERFORM", false);
            }

            return result;
        }

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

            var topStatement = context.statement(0);
            if (topStatement.BEGIN() != null)
            {
                RemoveToken(topStatement.BEGIN());
                RemoveToken(topStatement.END());
            }

            var botStatement = context.statement(1);
            if (botStatement != null && botStatement.BEGIN() != null)
            {
                RemoveToken(botStatement.BEGIN());
                RemoveToken(botStatement.END());
            }

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
            var functionName = context.functionName().Unwrap().ToLowerInvariant();
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

        /// <summary>
        /// Visits the additive expression.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitAdditiveExpression(TSQLParser.AdditiveExpressionContext context)
        {
            // check additive expressions ... if they contain string literals, then the additive form must be
            // modified to use the '||' operator rather than the '+' operator.
            if (context.additiveExpression() != null && context.GetToken(TSQLParser.PLUS, 0) != null)
            {
                var expressionTypeVisitor = new ExpressionTypeVisitor(Variables);
                var expressionType = expressionTypeVisitor.VisitAdditiveExpression(context);
                if (expressionType == typeof(string))
                {
                    ReplaceToken(context.GetToken(TSQLParser.PLUS, 0), "||", false);
                }
            }

            return base.VisitAdditiveExpression(context);
        }

        /// <summary>
        /// Visits the print expression.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override object VisitPrintExpression(TSQLParser.PrintExpressionContext context)
        {
            ReplaceToken(context.PRINT(), "RAISE DEBUG '%',");
            return base.VisitPrintExpression(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.selectStatementPart" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitSelectStatementPart(TSQLParser.SelectStatementPartContext context)
        {
            var result = base.VisitSelectStatementPart(context);

            if (context.selectTopLimit() != null)
            {
                // limits under MS-SQL support a few options that dont translate well into ANSI
                // limits ... look for percent as this doesnt translate
                var topLimit = context.selectTopLimit();
                if (topLimit.PERCENT != null)
                {
                    throw new ArgumentException("unable to handle percent based limits");
                }

                var limitLevel = topLimit.integerValue() != null
                        ? topLimit.integerValue().GetText()
                        : topLimit.variable().GetText();

                var limitText = string.Format(" LIMIT {0}", limitLevel);
                RemoveToken(topLimit.TOP());
                Remove(topLimit.integerValue());
                Remove(topLimit.GetToken(TSQLParser.LPAREN, 1));
                Remove(topLimit.variable());
                Remove(topLimit.GetToken(TSQLParser.RPAREN, 1));

                InsertAfter(context.RightMostToken(), limitText, false);
            }

            if (context.selectList() != null)
            {
                // Look select statements that are saving state into a variable.  These are typically
                // queries with a single-row result.  There must only be one assignment in the entire
                // block for this to work.

                var selectListElements = context.selectList().selectListElement();
                if (selectListElements.Count(s => s.variable() != null) == 1)
                {
                    foreach (var selectListElement in selectListElements)
                    {
                        var selectListVariable = selectListElement.variable();
                        if (selectListVariable != null)
                        {
                            // this needs to be moved into the "INTO" section of the query
                            var insertText = string.Format(" INTO {0} ", PortVariableName(selectListVariable.Unwrap()));
                            Remove(selectListVariable);
                            RemoveToken(selectListElement.op);
                            InsertAfter(context.selectList(), insertText, false);
                        }
                    }
                }
            }

            return result;
        }

        public override object VisitCaseExpression(TSQLParser.CaseExpressionContext context)
        {
            return base.VisitCaseExpression(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.tableTarget" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitTableTarget(TSQLParser.TableTargetContext context)
        {
            var result = base.VisitTableTarget(context);

            if (context.tempTable() != null)
            {
                string tempTableName = context.tempTable().Unwrap();
                if (!_declaredTempTables.Contains(tempTableName))
                {
                    // if my parent is part of a select into or an insert, then I can manufacture
                    // the schema for the temporary table from the parent call.

                    if (context.IsParentChain<TSQLParser.IntoClauseContext, TSQLParser.SelectStatementPartContext>())
                    {
                        InsertBefore(
                            context.Parent.Parent,
                            string.Format("CREATE TEMPORARY TABLE {0} ON COMMIT DROP AS ", PortTableName(tempTableName)));
                        RemoveLeaves(context.Parent);
                        EatWhitespaceAt(context.Parent);
                    }
                    else if (context.IsParentChain<TSQLParser.InsertPreambleContext>())
                    {
                        var insertPreamble = context.Parent as TSQLParser.InsertPreambleContext;
                    }
                    else if (context.IsParentChain<TSQLParser.InsertOutputClauseContext>())
                    {
                        var insertOutputClause = context.Parent as TSQLParser.InsertOutputClauseContext;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.joinOrApply" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitJoinOrApply(TSQLParser.JoinOrApplyContext context)
        {
            if (context.APPLY() != null)
            {
#if PG93
                ReplaceToken(context.APPLY(), "LATERAL");
#else
                ReplaceToken(context.APPLY(), "JOIN");
#endif
            }

            return base.VisitJoinOrApply(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.tableTargetOptions" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitTableTargetOptions(TSQLParser.TableTargetOptionsContext context)
        {
            RemoveLeaves(context);
            return base.VisitTableTargetOptions(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.tableSourceOptions" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override object VisitTableSourceOptions(TSQLParser.TableSourceOptionsContext context)
        {
            RemoveLeaves(context);
            return base.VisitTableSourceOptions(context);
        }
    }
}
