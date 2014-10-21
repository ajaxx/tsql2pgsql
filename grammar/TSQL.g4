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

grammar TSQL;

/*
 * Parser Rules
 */

compileUnit
	:	statementList EOF
	;

dropTable
	:	DROP TABLE qualifiedName
	|	DROP TABLE tempTable
	;

alterTable
	:	ALTER TABLE tableTargetWithOptions
		(	alterTableSwitchPartition
		|	alterTableAddConstraint
		|	alterTableDropConstraint
		|	alterTableTrigger
		)
	;

alterTableSwitchPartition
	:	SWITCH PARTITION (integerValue | variable) TO partitionName PARTITION (integerValue | variable)
	;

alterTableDropConstraint
	:	DROP CONSTRAINT qualifiedName basicOptionList?
	;

alterTableAddConstraint
	:	ADD CONSTRAINT qualifiedName
		(
			(PRIMARY KEY clusterType?) LPAREN orderedIndexColumnList RPAREN basicOptionList?
			(ON partitionIdent)?
		|	DEFAULT LPAREN literalValue RPAREN FOR qualifiedName
		|	FOREIGN KEY LPAREN columnList RPAREN
			 REFERENCES tableSource LPAREN columnList RPAREN
		)
	;

partitionIdent
	:	PRIMARY
	|	LBRACKET PRIMARY RBRACKET
	|	qualifiedName LPAREN columnList RPAREN
	;

alterTableTrigger
	:	(	DISABLE
		|	ENABLE
		) TRIGGER qualifiedName
	;

alterIndex
	:	ALTER INDEX (Index = qualifiedName|ALL) ON (Table = qualifiedName)
			(Operation = qualifiedName)
			basicOptionList?
	;

alterPartitionScheme
	:	ALTER PARTITION SCHEME qualifiedName
			NEXT USED partitionIdent
	;

alterPartitionFunction
	:	ALTER PARTITION FUNCTION functionName '(' ')'
		(	MERGE RANGE '(' variable ')'
		|	SPLIT RANGE '(' variable ')'
		)
	;		

basicOptionList
	:	WITH LPAREN basicOption (',' basicOption)* RPAREN
	|	WITH basicOption
	;

basicOption
	:	Identifier ('=' (ON|OFF))?
	;

createTable
	:	CREATE TABLE (qualifiedName | tempTable) LPAREN columnDefinitionList (',' tableDeclarationOptions)? (',')? RPAREN
			(ON partitionIdent)?
	;

dropIndex
	:	DROP INDEX qualifiedName (ON tableTarget basicOptionList?)?
	;

createIndex
	:	CREATE UNIQUE? clusterType? INDEX (qualifiedName | tempIndex)
			ON tableTarget LPAREN orderedIndexColumnList RPAREN
			createIndexIncludeList?
			whereClause?
			basicOptionList?
			createIndexPartition?
	;

createIndexIncludeList
	:	(INCLUDE LPAREN columnList RPAREN)
	;

createIndexPartition
	:	ON
		(	qualifiedName LPAREN columnList RPAREN
		|	LBRACKET PRIMARY RBRACKET
		|	PRIMARY
		)
	;

orderedIndexColumnList
	:	orderedIndexColumn (',' orderedIndexColumn)*
	;

orderedIndexColumn
	:	columnName (ASC | DESC)?
	;

createStatistics
	:	CREATE STATISTICS qualifiedName ON tableTarget LPAREN qualifiedColumnNameList RPAREN
	;

dropProcedure
	:	DROP PROCEDURE qualifiedName
	;

createProcedure
	:	CREATE PROCEDURE qualifiedName procedureParameters? procedureOptions? AS procedureBody			
	;

truncateTable
	:	TRUNCATE TABLE tableTarget
	;

obscureCommands
	:	'dbcc' Identifier
		(	expression
		|	'(' argumentList? ')'
		)
		basicOptionList?
	|	'checkpoint'
	;

predicateList
	:	expression
	;

primary
	:	literalValue
	|	qualifiedColumnName collate?
	|	variable collate?
	|	convertExpression
	|	functionCall
	|	CAST '(' expression AS type ')'
	|	(	COUNT 
		|	COUNT_BIG
		)
		(	minSelectElement
		|	'(' DISTINCT minSelectElement ')'
		)
	|	EXISTS '(' selectStatement ')'
	|	CASE caseWhen+ caseElse? END
	|	CASE expression caseWhen+ caseElse? END
	|	LPAREN expression RPAREN
	|	subSelectExpression
	;

expression
	:	primary
	|	conditionalExpression
	;

convertExpression
	:	CONVERT LPAREN type ',' expression (',' Style = integerValue)? RPAREN
	;

conditionalExpression
	:	conditionalOrExpression
	;

conditionalOrExpression
	:	conditionalAndExpression
	|	conditionalOrExpression OR conditionalAndExpression
	|	conditionalOrExpression '||' conditionalAndExpression
	;

conditionalAndExpression
	:	inclusiveOrExpression
	|	conditionalAndExpression AND inclusiveOrExpression
	|	conditionalAndExpression '&&' inclusiveOrExpression
	;

inclusiveOrExpression
	:	exclusiveOrExpression
	|	inclusiveOrExpression '|' exclusiveOrExpression
	|	inclusiveOrExpression IS NOT? NULL
	;

exclusiveOrExpression
	:	andExpression
	|	exclusiveOrExpression '^' andExpression
	;

andExpression
	:	equalityExpression
	|	andExpression '&' equalityExpression
	;

equalityExpression
	:	relationalExpression
	|	equalityExpression '=' relationalExpression
	|	equalityExpression '==' relationalExpression
	|	equalityExpression '!=' relationalExpression
	;

relationalExpression
	:	additiveExpression
	|	relationalExpression '<' additiveExpression
	|	relationalExpression '>' additiveExpression
	|	relationalExpression '<=' additiveExpression
	|	relationalExpression '<' '=' additiveExpression
	|	relationalExpression '>=' additiveExpression
	|	relationalExpression '>' '=' additiveExpression
	|	relationalExpression '<>' additiveExpression
	|	relationalExpression LIKE likeTestExpression
	|	relationalExpression NOT LIKE likeTestExpression
	;

additiveExpression
	:	multiplicativeExpression
	|	additiveExpression '+' multiplicativeExpression
	|	additiveExpression '-' multiplicativeExpression
	;

multiplicativeExpression
	:	unaryExpression
	|	multiplicativeExpression '*' unaryExpression
	|	multiplicativeExpression '/' unaryExpression
	|	multiplicativeExpression '%' unaryExpression
	;

unaryExpression
	:	'+' unaryExpression
	|	'-' unaryExpression
	|	unaryExpressionNotPlusMinus
	;

unaryExpressionNotPlusMinus
	:	postfixExpression
	|	NOT unaryExpression
	;

postfixExpression
	:	primary
	|	postfixExpression IN expressionSet
	|	postfixExpression NOT IN expressionSet
	|	postfixExpression NOT? BETWEEN expression AND expression
	|	postfixExpression NOT? LIKE likeTestExpression
	;

likeTestExpression
	:	stringValue (ESCAPE StringLiteral)?
	|	functionCall
	;

minSelectElement
	:	'*'
	|	qualifiedColumnName
	|	literalValue
	|	functionCall
	|	LPAREN minSelectElement RPAREN
	;

expressionSet
	:	'('	expression (',' expression)* ')'
	|	variable collate?
	|	functionCall
	|	subSelectExpression
	;

caseWhen
	:	WHEN expression THEN expression
	;

caseElse
	:	ELSE expression
	;

subSelectExpression
	:	'(' selectStatement ')'
	;

type
	:	qualifiedName
	|	characterStringType
	|	numericType
	|	identityType
	|	integerType
	|	XML
	|	CURSOR
	|	typeInBracket
	;

typeInBracket
	:	LBRACKET type RBRACKET
	;

expressionInRest
	:	'(' ')'
	|	'(' selectStatement ')'
	|	'(' expression (',' expression)* ')'
	;

literalValue
	:	StringLiteral
	|	integerValue
	|	FloatingPointLiteral
	|	NULL
	|	'(' literalValue (',' literalValue)* ')'
	;

stringValue
	:	StringLiteral
	|	variable
	;

integerValue
	:	('+' | '-')? IntegerLiteral
	|	LPAREN integerValue RPAREN
	;

qualifiedNamePart
	:	LBRACKET keyword+ RBRACKET
	|	Identifier
	;

qualifiedName
    :   qualifiedNamePart ('.'+ qualifiedNamePart)*
	|	keyword
    ;

qualifiedNameList
	:	qualifiedName (',' qualifiedName)*
	;

tempIndex
	:	HASH+ (qualifiedNamePart | keyword)
	;

tempTable
	:	HASH+ (qualifiedNamePart | keyword)
	|	qualifiedName DOT tempTable
	;

variable
	:	AT+ Identifier
	|	AT+ keyword
	;

procedureBody
	:	statementList
	;

procedureParameters
	:	LPAREN RPAREN
	|	LPAREN procedureParameter (',' procedureParameter)* RPAREN
	|	procedureParameter (',' procedureParameter)*
	;

procedureParameter
	:	procedureParameterName AS? type (NOT? NULL?) procedureParameterInitialValue? READONLY? (OUTPUT | OUT)?
	;

procedureParameterInitialValue
	:	'=' literalValue
	|	'=' NULL
	;

procedureParameterName
	:	variable
	;

procedureOptions
	:	WITH Identifier (',' Identifier)*
	;

statementList
	:	statement (statement)*
	;

statement
	:	BEGIN statementList END
	|	ddl SEMICOLON?
	|	dml SEMICOLON?
	|	SEMICOLON
	;

ddl
	:	createProcedure
	|	createTable
	|	createIndex
	|	createStatistics
	|	dropProcedure
	|	dropTable
	|	dropIndex
	|	truncateTable
	|	alterTable
	|	alterIndex
	|	alterPartitionFunction
	|	alterPartitionScheme
	|	obscureCommands
	;

dml
	:	selectStatement
	|	insertStatement
	|	deleteStatement
	|	updateStatement
	|	executeStatement
	|	mergeStatement
	|	computeStatement
	|	setStatement
	|	declareStatement
	|	tryBlock
	|	transactionBlock
	|	raiseError
	|	waitFor
	|	commonTableExpression
	|	cursorStatement
	|	PRINT expression
	|	ifStatement
	|	whileStatement
	|	BREAK qualifiedName?
	|	CONTINUE qualifiedName?
	|	COMMIT TRANSACTION? qualifiedName?
	|	GOTO qualifiedName?
	|	ROLLBACK TRANSACTION? qualifiedName?
	|	returnExpression
	|	Identifier COLON  /* Label */
	;

returnExpression
	:	RETURN expression?
	;

ifStatement
	:	IF predicateList statement (ELSE statement)?
	;

whileStatement
	:	WHILE predicateList statement
	;

waitFor
	:	WAITFOR DELAY StringLiteral
	;

raiseError
	:	RAISE_ERROR '(' argumentList ')' (WITH LOG)?
	|	RAISE_ERROR argument+ (WITH LOG)?
	;

tryBlock
	:	BEGIN TRY
			statementList 
		END TRY
		BEGIN CATCH
			statementList
		END CATCH
	;

transactionBlock
	:	BEGIN TRANSACTION qualifiedName? statementList
	;

deleteTop
	:	TOP (integerValue | variable | '(' variable ')')
	;

deleteStatement
	:	DELETE deleteTop? deleteFromClause+ deleteOutput? joinOrApply* whereClause? dmlOptions?
	|	DELETE deleteTop? tableTarget deleteFromClauseLoose+ deleteOutput? joinOrApply* whereClause? dmlOptions?
	;

deleteFromClause
	:	FROM? tableTargetWithOptions
			(',' tableSourceWithOptions)*
	;

deleteFromClauseLoose
	:	FROM? tableSourceWithOptions		// use tableSource since it allows for the table to declared before the from clause
			(',' tableSourceWithOptions)*	// and only a select clause in the from stanza... its a strange case
	;

deleteOutput
	:	OUTPUT qualifiedColumnNameList INTO tableTargetWithOptions
	;

commonTableExpression
	:	WITH commonTableExpressionAtom (',' commonTableExpressionAtom)* dml
	;

commonTableExpressionAtom
	:	qualifiedName (LPAREN columnList RPAREN)? AS LPAREN dml RPAREN
	;

insertStatement
	:	insertPreamble insertOutputClause? insertDataSource dmlOptions?
	;

insertPreamble
	:	INSERT INTO? tableTarget tableTargetOptions? (LPAREN qualifiedNameList RPAREN)?
	;

insertOutputClause
	:	OUTPUT selectList (INTO tableTarget (LPAREN qualifiedColumnNameList RPAREN)? )?
	;

insertDataSource
	:	VALUES insertValueList (',' insertValueList)*
	|	DEFAULT VALUES
	|	selectStatement
	|	tableSource
	;

insertValueList
	:	LPAREN insertValue (',' insertValue)* RPAREN
	;

insertValue
	:	expression
	;

declareStatement
	:	DECLARE variableDeclaration (',' variableDeclaration)*
	|	DECLARE qualifiedName CURSOR Identifier?
			FOR	selectStatement
			(FOR UPDATE OF tableTarget)?			
	;

variableDeclaration
	:	variable
		(	AS? type variableDeclarationAssignment?
		|	TABLE LPAREN columnDefinitionList (',' tableDeclarationOptions)? (',')? RPAREN
		)
	;

variableDeclarationAssignment
	:	('=' expression)?
	;

columnList
	:	columnName (',' columnName)*
	;

columnDefinitionList
	:	columnDefinition (',' columnDefinition)*
	;

columnDefinition
	:	columnName type
			(PRIMARY KEY clusterType?)? NOT? NULL?
			(DEFAULT (literalValue | functionCall))?
	|	columnName integerType
			(PRIMARY KEY clusterType?)? NOT? NULL?
			identitySpec?
			(DEFAULT (literalValue | functionCall))?
	;

tableDeclarationOptions
	:	tableDeclarationOption (',' tableDeclarationOption)*
	;

tableDeclarationOption
	:	UNIQUE clusterType? LPAREN columnList RPAREN
	|	PRIMARY KEY clusterType? LPAREN orderedIndexColumnList RPAREN basicOptionList?
	;

partitionName
	:	qualifiedName
	;

setStatement
	:	SET
	(	setVariableAssignment
	|	setVariableToCursor
	|	setSessionOther
	)
	;

setSessionOther
	:	ROWCOUNT integerValue
	|	TRANSACTION ISOLATION LEVEL transactionIsolationLevel
	|	qualifiedName+ (ON|OFF)
	|	qualifiedName
	;

setVariableAssignment
	:	variable propertyOrField? assignmentOperator expression
	;

setVariableToCursor
	:	variable propertyOrField? EQUALS CURSOR FOR	selectStatement
	;

transactionIsolationLevel
	:	'read' ('committed' | 'uncommitted')
	|	'repeatable' 'read'
	|	'snapshot'
	|	'serializable'
	;

updateTop
	:	TOP (integerValue | variable | '(' variable ')')
	;

updateStatement
	:	UPDATE updateTop? tableTargetWithOptions
		SET updateStatementSetClauseRest (',' updateStatementSetClauseRest)* fromClause? joinOrApply* moreInnerJoin* whereClause? dmlOptions?
	|	UPDATE STATISTICS tableTarget basicOptionList?
	;

updateStatementSetClauseRest
	:	qualifiedColumnName assignmentOperator (expression | DEFAULT | NULL)
	|	variable assignmentOperator  (qualifiedColumnName EQUALS)? expression
	|	qualifiedColumnName '.' qualifiedNamePart (
				'=' expression
			|	'(' ')'
			|	'(' argumentList ')'
		)
	;

argument
	:	expression
	|	tempTable
	|	type
	;

argumentList
	:	argument (',' argument)*
	;

computeStatement
	:	COMPUTE expression (',' expression)* 
			(BY expression (',' expression)*)?
	;

selectStatement
	:	selectStatementPart (UNION ALL? selectStatementPart)*
	|	selectStatementPart ((EXCEPT | INTERSECT) selectStatementPart)*
	;

selectStatementPart
	:	SELECT selectTopLimit? selectList intoClause? fromClause? xmlWithOption? joinOrApply* moreInnerJoin* whereClause? groupByClause? havingClause? orderByClause? dmlOptions? forXmlClause?
	|	LPAREN selectStatement RPAREN
	;

xmlWithOption
	:	WITH LPAREN xmlDefinitionList RPAREN (Alias = qualifiedName)?
	;

xmlDefinitionList
	:	xmlDefinition (',' xmlDefinition)*
	;

xmlDefinition
	:	columnName type (literalValue | functionCall)?
	;

forXmlClause
	:	FOR XML 
		(	AUTO
		|	RAW
		|	EXPLICIT
		|	PATH '(' argumentList? ')'
		)
	;

selectTopLimit
	:	DISTINCT? TOP
		(	integerValue 'percent'?
		|	variable
		|	'(' variable ')'
		)
	;

selectVariableAssignment
	:	variable ('=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '^=' | '|=') expression
	;

selectList
	:	selectListElement (',' selectListElement)*
	;

selectListElement
	:	DISTINCT?
		(	expression overClause? (AS? columnAlias)?
		|	qualifiedName DOT STAR
		|	STAR
		)
	|	variable ('=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '^=' | '|=') expression (AS? columnAlias)?
	|	qualifiedColumnName ('=') expression
	;

dmlOptions
	:	OPTION LPAREN dmlOption (',' dmlOption)* RPAREN
	;

dmlOption
	:	Identifier literalValue?
	|	OPTIMIZE FOR
		LPAREN
			( variable '=' literalValue )
		RPAREN
	;

overClause
	:	OVER
		LPAREN
			(	PARTITION BY expression (',' expression)*
			|	ORDER BY orderByElement (',' orderByElement)*
			)+
		RPAREN
	;

intoClause
	:	INTO tableTarget
	;

fromClause
	:	FROM tableSourceWithOptions (',' tableSourceWithOptions)*
	;

moreInnerJoin
	:	(',' tableSourceWithOptions)+
	;

groupByClause
	:	GROUP BY groupByElement (',' groupByElement)*
	;

groupByElement
	:	columnIndexOrName
	|	expression
	;

havingClause
	:	HAVING predicateList
	;

joinOrApply
	:	joinType? JOIN tableSourceWithOptions (ON predicateList)*
	|	joinType? APPLY tableSourceWithOptions
	;

joinType
	:	LEFT
	|	RIGHT
	|	OUTER
	|	INNER
	|	CROSS
	|	FULL
	|	FULL OUTER
	|	LEFT OUTER
	|	RIGHT OUTER
	;

whereClause
	:	WHERE predicateList
	;

orderByClause
	:	ORDER BY orderByElement (',' orderByElement)*
	;

orderByElement
	:	(	columnIndexOrName
		|	expression
		)
		(	ASC 
		|	DESC
		)?
	;

mergeStatement
	:	MERGE INTO? tableTarget
		USING tableSource
		ON predicateList
		(	WHEN MATCHED (AND predicateList)? THEN mergeMatched
		|	WHEN NOT MATCHED (BY TARGET)? (AND predicateList)? THEN mergeNotMatched
		|	WHEN NOT MATCHED BY SOURCE (AND predicateList)? THEN mergeMatched
		)+
		( OUTPUT selectList INTO tableTargetWithOptions )?
	;

mergeMatched
	:	UPDATE SET updateStatementSetClauseRest (',' updateStatementSetClauseRest)*
	|	DELETE
	;

mergeNotMatched
	:	INSERT (LPAREN qualifiedNameList RPAREN)?
			VALUES insertValueList (',' insertValueList)*
	;

tableTargetWithOptions
	:	tableTarget tableTargetOptions?
	|	tableTarget tableTargetOptions? (AS? tableAlias)?
	;

tableTarget
	:	(	qualifiedName 
		|	variable
		|	tempTable
		)  (AS? tableAlias)?
	;

tableTargetOptions
	:	WITH LPAREN	Identifier RPAREN
	|	WITH Identifier
	;

tableSourceWithOptions
	:	tableSource tableSourceOptions?
	;

tableSource
	:	(	qualifiedName 
		|	variable  ('.' functionCall)?
		|	tempTable ('.' functionCall)?
		|	'(' selectStatement ')'
		|	executeStatement
		|	functionCall (WITH LPAREN xmlDefinitionList RPAREN)?
		) (AS? tableAlias)?
	;

tableSourceOptions
	:	WITH LPAREN Identifier RPAREN
	|	LPAREN Identifier RPAREN
	;

tableAlias
	:	qualifiedNamePart
	|	StringLiteral
	;

functionName
	:	( qualifiedName | keyword ) 
	;

functionCall
	:	functionName '(' argumentList? ')'
	;

executeStatement
	:	EXECUTE
			(	qualifiedName executeArgumentList?
			|	LPAREN expression RPAREN
			)
		basicOptionList?
	;

executeArgumentList
	:	executeArgument (',' executeArgument)*
	;

executeArgument
	:	variable '=' expression?
	|	expression (OUTPUT | OUT)?
	;

characterStringTypeLength
	:	LPAREN (integerValue | MAX) RPAREN
	;

characterStringType
	:	(CHARACTER | CHAR | NCHAR) characterStringTypeLength?
	|	(CHARACTER | CHAR | NCHAR) VARYING characterStringTypeLength
	|	(VARCHAR | NVARCHAR) characterStringTypeLength
	;

numericType
	:	(NUMERIC | DECIMAL) ('(' Scale = IntegerLiteral ( ',' Precision = IntegerLiteral )? ')')?
	|	FLOAT
	;

integerType
	:	INT
	|	BIGINT
	|	TINYINT
	|	SMALLINT
	;

identityType
	:	integerType? identitySpec
	;

identitySpec
	:	IDENTITY ('(' Seed = integerValue (',' Increment = integerValue)? ')')?
	;

propertyOrField
	:	'.' qualifiedNamePart
	;

assignmentOperator
	:	('=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '^=' | '|=')
	;

qualifiedColumnNameList
	:	qualifiedColumnName (',' qualifiedColumnName)*
	;

qualifiedColumnName
	:	qualifiedName
	|	qualifiedName '.' keyword
	|	tempTable '.' qualifiedNamePart
	;

columnIndexOrName
	:	(ColumnIndex = integerValue | ColumnName = qualifiedName)
	;

columnName
	:	qualifiedNamePart
	;

columnAlias
	:	qualifiedNamePart
	|	StringLiteral
	;

cursorId
	:	qualifiedName
	|	variable
	;

cursorStatement
	:	cursorOpen
	|	cursorClose
	|	cursorFetch
	|	cursorDeallocate
	;

cursorOpen
	:	OPEN cursorId
	;

cursorClose
	:	CLOSE cursorId
	;

cursorFetch
	:	FETCH 
		(	NEXT
		|	PRIOR
		|	FIRST
		|	LAST
		|	ABSOLUTE
			(	integerValue
			|	variable
			)
		|	RELATIVE
			(	integerValue
			|	variable
			)
		|	qualifiedName
		)
		(	FROM cursorId )?
		INTO
		variable (',' variable)*
	;

cursorDeallocate
	:	DEALLOCATE cursorId
	;

stringExpression
	:	StringLiteral
	;

collate
	:	COLLATE Identifier
	;

clusterType
	:	CLUSTERED
	|	NONCLUSTERED
	;

keyword
	:	READONLY
	|	TRY
	|	CATCH
	|	TRANSACTION
	|	COMMIT
	|	ROLLBACK
	|	RAISE_ERROR
	|	PRINT
	|	CLUSTERED
	|	NONCLUSTERED
	|	TABLE
	|	PROCEDURE
	|	PARTITION
	|	INDEX
	|	DATABASE
	|	CONSTRAINT
	|	PRIMARY
	|	KEY
	|	LEFT
	|	RIGHT
	|	CROSS
	|	OUTER
	|	INNER
	|	FULL
	|	CURSOR
	|	FETCH
	|	OPEN
	|	CLOSE
	|	DEALLOCATE
	|	FIRST
	|	LAST
	|	PRIOR
	|	NEXT
	|	ABSOLUTE
	|	RELATIVE
	|	CASE
	|	CAST
	|	CONVERT
	|	COLLATE
	|	COUNT
	|	COUNT_BIG
	|	DISTINCT
	|	UNIQUE
	|	MAX
	|	TOP
	|	WHEN
	|	THEN
	|	SWITCH
	|	OVER
	|	INCLUDE
	|	UNION
	|	EXCEPT
	|	INTERSECT
	|	ALL
	|	APPLY
	|	IF
	|	ELSE
	|	WHILE
	|	GOTO
	|	CONTINUE
	|	BREAK
	|	TRUNCATE
	|	ADD
	|	REMOVE
	|	ALTER
	|	CREATE
	|	DROP
	|	AS
	|	WITH
	|	FOR
	|	REPLICATION
	|	BEGIN
	|	END
	|	EXISTS
	|	DEFAULT
	|	VARYING
	|	SELECT
	|	UPDATE
	|	INSERT
	|	DELETE
	|	WHERE
	|	FROM
	|	JOIN
	|	DECLARE
	|	OPTION
	|	SET
	|	TO
	|	ON
	|	OFF
	|	GROUP
	|	HAVING
	|	ORDER
	|	BY
	|	EXECUTE
	|	INTO
	|	VALUES
	|	IS
	|	IN
	|	NOT
	|	NULL
	|	BETWEEN
	|	RETURN
	|	OUTPUT
	|	OUT
	|	LIKE
	|	ESCAPE
	|	OF
	|	DECIMAL
	|	NUMERIC
	|	VARCHAR
	|	NVARCHAR
	|	CHAR
	|	CHARACTER
	|	NCHAR
	|	INT
	|	TINYINT
	|	SMALLINT
	|	BIGINT
	|	FLOAT
	|	IDENTITY
	|	LOG
	|	AND
	|	OR
	|	ASC
	|	DESC
	|	STATISTICS
	|	USING
	|	MERGE
	|	MATCHED
	|	TARGET
	|	SOURCE
	|	XML
	|	RAW
	|	AUTO
	|	EXPLICIT
	|	PATH
	|	ENABLE
	|	DISABLE
	|	TRIGGER
	|	WAITFOR
	|	DELAY
	|	FOREIGN
	|	REFERENCES
	|	COMPUTE
	|	LEVEL
	|	ISOLATION
	|	SPLIT
	|	RANGE
	|	FUNCTION
	|	SCHEME
	|	USED
	|	OPTIMIZE
	|	ROWCOUNT
	;

// LEXER

READONLY		:	'readonly';

TRY				:	'try';
CATCH			:	'catch';
TRANSACTION		:	'transaction'|'tran';
COMMIT			:	'commit';
ROLLBACK		:	'rollback';
RAISE_ERROR		:	'raiserror';
PRINT			:	'print';

CLUSTERED		:	'clustered';
NONCLUSTERED	:	'nonclustered';

TABLE			:	'table';
PROCEDURE		:	'procedure'|'proc';
PARTITION		:	'partition';
INDEX			:	'index';
DATABASE		:	'database';
CONSTRAINT		:	'constraint';

FOREIGN			:	'foreign'|'[foreign]';
PRIMARY			:	'primary'|'[primary]';
KEY				:	'key';
REFERENCES		:	'references';

LEFT			:	'left';
RIGHT			:	'right';
CROSS			:	'cross';
OUTER			:	'outer';
INNER			:	'inner';
FULL			:	'full';

CURSOR			:	'cursor';
FETCH			:	'fetch';

OPEN			:	'open';
CLOSE			:	'close';
DEALLOCATE		:	'deallocate';

FIRST			:	'first';
LAST			:	'last';
PRIOR			:	'prior';
NEXT			:	'next';
ABSOLUTE		:	'absolute';
RELATIVE		:	'relative';

CASE			:	'case';
CAST			:	'cast';
CONVERT			:	'convert';
COLLATE			:	'collate';
COUNT			:	'count';
COUNT_BIG		:	'count_big';
DISTINCT		:	'distinct';
UNIQUE			:	'unique';
MAX				:	'max';
TOP				:	'top';
WHEN			:	'when';
THEN			:	'then';
SWITCH			:	'switch';
OVER			:	'over';

INCLUDE			:	'include';

INTERSECT		:	'intersect';
EXCEPT			:	'except';
UNION			:	'union';
ALL				:	'all';
APPLY			:	'apply';

IF				:	'if';
ELSE			:	'else';
WHILE			:	'while';
GOTO			:	'goto';
CONTINUE		:	'continue';
BREAK			:	'break';

TRUNCATE		:	'truncate';

ADD				:	'add';
REMOVE			:	'remove';

ALTER			:	'alter';
CREATE			:	'create';
DROP			:	'drop';
AS				:	'as';
WITH			:	'with';
FOR				:	'for';
REPLICATION		:	'replication';
BEGIN			:	'begin';
END				:	'end';
EXISTS			:	'exists';
DEFAULT			:	'default';
VARYING			:	'varying';
SELECT			:	'select';
UPDATE			:	'update';
INSERT			:	'insert';
DELETE			:	'delete';
WHERE			:	'where';
FROM			:	'from';
JOIN			:	'join';
DECLARE			:	'declare';
OPTION			:	'option';
SET				:	'set';
TO				:	'to';
ON				:	'on';
OFF				:	'off';
GROUP			:	'group';
HAVING			:	'having';
ORDER			:	'order';
BY				:	'by';
EXECUTE			:	'execute'|'exec';
INTO			:	'into';
VALUES			:	'values';
IS				:	'is';
IN				:	'in';
NOT				:	'not';
NULL			:	'null';
BETWEEN			:	'between';
RETURN			:	'return';
OUTPUT			:	'output';
OUT				:	'out';
LIKE			:	'like';
ESCAPE			:	'escape';
OF				:	'of';

DECIMAL			:	'decimal'|'[decimal]';
NUMERIC			:	'numeric'|'[numeric]';

VARCHAR			:	'varchar'|'[varchar]';
NVARCHAR		:	'nvarchar'|'[nvarchar]';
CHAR			:	'char'|'[char]';
CHARACTER		:	'character'|'[character]';
NCHAR			:	'nchar'|'[nchar]';

INT				:	'int'|'[int]';
TINYINT			:	'tinyint'|'[tinyint]';
SMALLINT		:	'smallint'|'[smallint]';
BIGINT			:	'bigint'|'[bigint]';

FLOAT			:	'float'|'[float]';

IDENTITY		:	'identity'|'[identity]';

LOG				:	'log';

AND				:	'and';
OR				:	'or';

ASC				:	'asc';
DESC			:	'desc';

STATISTICS		:	'statistics';

USING			:	'using';
MERGE			:	'merge';
MATCHED			:	'matched';
TARGET			:	'target';
SOURCE			:	'source';

XML				:	'xml';
RAW				:	'raw';
AUTO			:	'auto';
EXPLICIT		:	'explicit';
PATH			:	'path';

ENABLE			:	'enable';
DISABLE			:	'disable';
TRIGGER			:	'trigger';

WAITFOR			:	'waitfor';
DELAY			:	'delay';

COMPUTE			:	'compute';
LEVEL			:	'level';
ISOLATION		:	'isolation';

SPLIT			:	'split';
RANGE			:	'range';
FUNCTION		:	'function';
SCHEME			:	'scheme';
USED			:	'used';
OPTIMIZE		:	'optimize';
ROWCOUNT		:	'rowcount';


LT				:	'<';
LTE				:	'<=';
GT				:	'>';
GTE				:	'>=';
GT_LT			:	'<>';

EQUAL_EQUAL		:	'==';
NOT_EQUAL		:	'!=';
ADD_ASSIGN		:	'+=';
SUB_ASSIGN		:	'-=';
MUL_ASSIGN      :	'*=';
DIV_ASSIGN      :	'/=';
AND_ASSIGN      :	'&=';
OR_ASSIGN       :	'|=';
XOR_ASSIGN      :	'^=';
MOD_ASSIGN      :	'%=';

HASH			:	'#';
LPAREN			:	'(';
RPAREN			:	')';
LBRACKET		:	'[';
RBRACKET		:	']';
EQUALS			:	'=';
AT				:	'@';
SEMICOLON		:	';';
COLON			:	':';
STAR			:	'*';
SLASH			:	'/';
AMPERSAND		:	'&';
PIPE			:	'|';
PERCENT			:	'%';
CARET			:	'^';
PLUS			:	'+';
MINUS			:	'-';
DOT				:	'.';
COMMA			:	',';

fragment
DecimalIntegerLiteral
    :   DecimalNumeral IntegerTypeSuffix?
    ;

fragment
HexIntegerLiteral
    :   HexNumeral IntegerTypeSuffix?
    ;

fragment
OctalIntegerLiteral
    :   OctalNumeral IntegerTypeSuffix?
    ;

fragment
BinaryIntegerLiteral
    :   BinaryNumeral IntegerTypeSuffix?
    ;

fragment
IntegerTypeSuffix
    :   [lL]
    ;

fragment
DecimalNumeral
    :   '0'
    |   NonZeroDigit (Digits? | Underscores Digits)
    ;

fragment
Digits
    :   Digit (DigitOrUnderscore* Digit)?
    ;

fragment
Digit
    :   '0'
    |   NonZeroDigit
    ;

fragment
NonZeroDigit
    :   [1-9]
    ;

fragment
DigitOrUnderscore
    :   Digit
    |   '_'
    ;

fragment
Underscores
    :   '_'+
    ;

fragment
HexNumeral
    :   '0' [xX] HexDigits
    ;

fragment
HexDigits
    :   HexDigit (HexDigitOrUnderscore* HexDigit)?
    ;

fragment
HexDigit
    :   [0-9a-fA-F]
    ;

fragment
HexDigitOrUnderscore
    :   HexDigit
    |   '_'
    ;

fragment
OctalNumeral
    :   '0' Underscores? OctalDigits
    ;

fragment
OctalDigits
    :   OctalDigit (OctalDigitOrUnderscore* OctalDigit)?
    ;

fragment
OctalDigit
    :   [0-7]
    ;

fragment
OctalDigitOrUnderscore
    :   OctalDigit
    |   '_'
    ;

fragment
BinaryNumeral
    :   '0' [bB] BinaryDigits
    ;

fragment
BinaryDigits
    :   BinaryDigit (BinaryDigitOrUnderscore* BinaryDigit)?
    ;

fragment
BinaryDigit
    :   [01]
    ;

fragment
BinaryDigitOrUnderscore
    :   BinaryDigit
    |   '_'
    ;

// §3.10.2 Floating-Point Literals

FloatingPointLiteral
    :   DecimalFloatingPointLiteral
    |   HexadecimalFloatingPointLiteral
    ;

fragment
DecimalFloatingPointLiteral
    :   Digits '.' Digits? ExponentPart? FloatTypeSuffix?
    |   '.' Digits ExponentPart? FloatTypeSuffix?
    |   Digits ExponentPart FloatTypeSuffix?
    |   Digits FloatTypeSuffix
    ;

fragment
ExponentPart
    :   ExponentIndicator SignedInteger
    ;

fragment
ExponentIndicator
    :   [eE]
    ;

fragment
SignedInteger
    :   Sign? Digits
    ;

fragment
Sign
    :   [+-]
    ;

fragment
FloatTypeSuffix
    :   [fFdD]
    ;

fragment
HexadecimalFloatingPointLiteral
    :   HexSignificand BinaryExponent FloatTypeSuffix?
    ;

fragment
HexSignificand
    :   HexNumeral '.'?
    |   '0' [xX] HexDigits? '.' HexDigits
    ;

fragment
BinaryExponent
    :   BinaryExponentIndicator SignedInteger
    ;

fragment
BinaryExponentIndicator
    :   [pP]
    ;

// §3.10.4 Character Literals

fragment
SingleCharacter
    :   ~['\\]
    ;

// §3.10.5 String Literals

StringLiteral
    :   ["] ((~["\\])*)? ["]
	|	[n]? StringLiteralEdge ((StringEscapeSequence|~['])*)? StringLiteralEdge
    ;

fragment
StringLiteralEdge
	:	[']['][']
	|	[']
	;

fragment
StringEscapeSequence
	:	['][']
	;

fragment
StringCharacters
    :   (~["\\])+
    ;

fragment
StringCharacter
    :   ~["\\]
    ;

fragment
Letter
    :   [a-zA-Z$_] // these are the "letters" below 0xFF
    ;

fragment
LetterOrDigit
    :   [a-zA-Z0-9$_] // these are the "letters or digits" below 0xFF
    ;

Identifier
	:	QuotedIdentifier
	|   Letter [a-zA-Z0-9$_#%]*
    ;

fragment
QuotedIdentifier
	:	'[' [a-zA-Z0-9$_#%() ]+ ']'
	;

IntegerLiteral
    :   DecimalIntegerLiteral
    |   HexIntegerLiteral
    |   OctalIntegerLiteral
    |   BinaryIntegerLiteral
    ;

//
// Whitespace and comments
//

WS  :  [ \t\r\n\u000C]+ -> skip
    ;

COMMENT
    :   NESTED_COMMENT -> skip
    ;

fragment NESTED_COMMENT
	:	'/*' (NESTED_COMMENT | .)*? '*/'
	;		

LINE_COMMENT
    :   '--' ~[\r\n]* -> skip
    ;