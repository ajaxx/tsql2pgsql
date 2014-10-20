using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace tsql2pgsql.visitors
{
    using antlr;
    using grammar;

    internal class ExpressionTypeVisitor : TSQLBaseVisitor<Type>
    {
        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.expression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitExpression(TSQLParser.ExpressionContext context)
        {
            return
                context.primary() != null ?
                VisitPrimary(context.primary()) :
                VisitConditionalExpression(context.conditionalExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.conditionalExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitConditionalExpression(TSQLParser.ConditionalExpressionContext context)
        {
            return VisitConditionalOrExpression(context.conditionalOrExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.conditionalOrExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitConditionalOrExpression(TSQLParser.ConditionalOrExpressionContext context)
        {
            if (context.conditionalOrExpression() != null && context.conditionalAndExpression() != null)
                return typeof(bool);
            return VisitConditionalAndExpression(context.conditionalAndExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.conditionalAndExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitConditionalAndExpression(TSQLParser.ConditionalAndExpressionContext context)
        {
            if (context.conditionalAndExpression() != null && context.inclusiveOrExpression() != null)
                return typeof(bool);
            return VisitInclusiveOrExpression(context.inclusiveOrExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.inclusiveOrExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitInclusiveOrExpression(TSQLParser.InclusiveOrExpressionContext context)
        {
            if (context.IS() != null && context.NULL() != null)
                return typeof(bool);

            return base.VisitInclusiveOrExpression(context);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.exclusiveOrExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitExclusiveOrExpression(TSQLParser.ExclusiveOrExpressionContext context)
        {
            if (context.exclusiveOrExpression() != null)
            {
                return EvalTypeWidening(
                    VisitExclusiveOrExpression(context.exclusiveOrExpression()),
                    VisitAndExpression(context.andExpression()));
            }
            return VisitAndExpression(context.andExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.andExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitAndExpression(TSQLParser.AndExpressionContext context)
        {
            if (context.andExpression() != null)
                return typeof(void);
            return VisitEqualityExpression(context.equalityExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.equalityExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitEqualityExpression(TSQLParser.EqualityExpressionContext context)
        {
            if (context.equalityExpression() != null)
                return typeof(bool);
            return VisitRelationalExpression(context.relationalExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.relationalExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitRelationalExpression(TSQLParser.RelationalExpressionContext context)
        {
            if (context.relationalExpression() != null)
                return typeof(bool);
            return VisitAdditiveExpression(context.additiveExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.additiveExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitAdditiveExpression(TSQLParser.AdditiveExpressionContext context)
        {
            if (context.additiveExpression() != null)
            {
                return EvalTypeWidening(
                    VisitAdditiveExpression(context.additiveExpression()),
                    VisitMultiplicativeExpression(context.multiplicativeExpression()));
            }

            return VisitMultiplicativeExpression(context.multiplicativeExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.multiplicativeExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitMultiplicativeExpression(TSQLParser.MultiplicativeExpressionContext context)
        {
            if (context.multiplicativeExpression() != null)
            {
                if (context.GetToken(TSQLParser.PERCENT, 0) != null) // modulus
                    return typeof(int);
                return EvalTypeWidening(
                    VisitMultiplicativeExpression(context.multiplicativeExpression()),
                    VisitUnaryExpression(context.unaryExpression()));
            }

            return VisitUnaryExpression(context.unaryExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.unaryExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitUnaryExpression(TSQLParser.UnaryExpressionContext context)
        {
            if (context.unaryExpression() != null)
                return VisitUnaryExpression(context.unaryExpression());
            return VisitUnaryExpressionNotPlusMinus(context.unaryExpressionNotPlusMinus());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.unaryExpressionNotPlusMinus" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitUnaryExpressionNotPlusMinus(TSQLParser.UnaryExpressionNotPlusMinusContext context)
        {
            if (context.NOT() != null)
                return typeof(bool);
            return VisitPostfixExpression(context.postfixExpression());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.postfixExpression" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitPostfixExpression(TSQLParser.PostfixExpressionContext context)
        {
            if (context.primary() != null)
                return VisitPrimary(context.primary());
            return typeof(bool);
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.primary" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitPrimary(TSQLParser.PrimaryContext context)
        {
            if (context.EXISTS() != null)
                return typeof(bool);
            if (context.COUNT() != null)
                return typeof(int);
            if (context.COUNT_BIG() != null)
                return typeof(long);
            if (context.literalValue() != null)
                return VisitLiteralValue(context.literalValue());
            if (context.convertExpression() != null)
                return VisitConvertExpression(context.convertExpression());
            if (context.CAST() != null)
                return VisitType(context.type());
            if (context.LPAREN() != null)
                return VisitExpression(context.expression());
            if (context.subSelectExpression() != null)
                return typeof(IDataRecord);
            if (context.functionCall() != null)
                return typeof(void);
            if (context.variable() != null)
                return typeof(void);
            if (context.qualifiedColumnName() != null)
                return typeof(void);

            throw new ArgumentException("invalid primary object");
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.type" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitType(TSQLParser.TypeContext context)
        {
            if (context.qualifiedName() != null)
                return typeof(object);
            if (context.characterStringType() != null)
                return typeof(string);
            if (context.numericType() != null)
                return typeof(decimal);
            if (context.identityType() != null)
                return typeof(int);
            if (context.integerType() != null)
                return typeof(int);
            if (context.XML() != null)
                return typeof(string);
            if (context.CURSOR() != null)
                return typeof(void);
            if (context.typeInBracket() != null)
                return VisitType(context.typeInBracket().type());

            throw new ArgumentException("invalid type");
        }

        /// <summary>
        /// Visits the convert expression.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public override Type VisitConvertExpression(TSQLParser.ConvertExpressionContext context)
        {
            return VisitType(context.type());
        }

        /// <summary>
        /// Visit a parse tree produced by <see cref="TSQLParser.literalValue" />.
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <returns></returns>
        /// <return>The visitor result.</return>
        public override Type VisitLiteralValue(TSQLParser.LiteralValueContext context)
        {
            if (context.StringLiteral() != null)
                return typeof(string);
            if (context.FloatingPointLiteral() != null)
                return typeof(decimal);
            if (context.NULL() != null)
                return typeof(void);
            if (context.integerValue() != null)
                return typeof(int);

            throw new ArgumentException(
                "unable to determine type of literalValue");
        }

        /// <summary>
        /// Applies standard rules of precedence to determine what would happen in an operation
        /// between the lhs and rhs.  Does not account for modulus and other variants, so use this
        /// where it makes sense.
        /// </summary>
        /// <param name="lhs">The LHS.</param>
        /// <param name="rhs">The RHS.</param>
        /// <returns></returns>
        private Type EvalTypeWidening(Type lhs, Type rhs)
        {
            if (lhs == typeof(string) || rhs == typeof(string))
                return typeof(string);
            if (lhs == typeof(decimal) || rhs == typeof(decimal))
                return typeof(decimal);
            if (lhs == typeof(long) || rhs == typeof(long))
                return typeof(long);
            if (lhs == typeof(int) || rhs == typeof(int))
                return typeof(int);
            return lhs;
        }
    }
}
