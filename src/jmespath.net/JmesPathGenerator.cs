﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DevLab.JmesPath.Expressions;
using DevLab.JmesPath.Interop;

namespace DevLab.JmesPath
{
    sealed class JmesPathGenerator : IJmesPathGenerator
    {
        /// <summary>
        /// holds the functions available to the parser
        /// </summary>
        readonly IFunctionRepository repository_;

        readonly Stack<IDictionary<string, JmesPathExpression>> selectHashes_
            = new Stack<IDictionary<string, JmesPathExpression>>()
            ;

        readonly Stack<IList<JmesPathExpression>> selectLists_
            = new Stack<IList<JmesPathExpression>>()
            ;

        readonly Stack<IList<JmesPathExpression>> functions_
            = new Stack<IList<JmesPathExpression>>();

        readonly Stack<JmesPathExpression> expressions_
            = new Stack<JmesPathExpression>()
            ;

        JmesPathExpression expression_;

        public JmesPathGenerator(IFunctionRepository repository)
        {
            repository_ = repository;
        }

        public JmesPathExpression Expression => expression_;

        public void OnExpression()
        {
            if (expression_ == null)
                expression_ = expressions_.Pop();
        }

        bool Prolog()
        {
            if (expression_ != null)
            {
                expressions_.Push(expression_);
                expression_ = null;
                return true;
            }

            return false;
        }

        public bool IsProjection()
        {
            var pop = Prolog();

            try
            {
                if (expressions_.Count == 0)
                    return false;

                return expressions_.Peek() is JmesPathProjection;
            }
            finally
            {
                if (pop)
                    OnExpression();
            }
        }

        #region Expressions

        public void OnSubExpression()
            => PopPush((left, right) => new JmesPathSubExpression(left, right));

        #region index_expression

        public void OnIndex(int index)
        {
            Prolog();

            var expression = new JmesPathIndex(index);

            expressions_.Push(expression);
        }

        public void OnFilterProjection()
        {
            Prolog();

            System.Diagnostics.Debug.Assert(expressions_.Count >= 1);

            var comparison = expressions_.Pop();
            var expression = new JmesPathFilterProjection(comparison);

            expressions_.Push(expression);
        }

        public void OnFlattenProjection()
        {
            Prolog();

            expressions_.Push(new JmesPathFlattenProjection());
        }

        public void OnListWildcardProjection()
        {
            Prolog();

            expressions_.Push(new JmesPathListWildcardProjection());
        }

        public void OnIndexExpression()
            => PopPush((left, right) => new JmesPathIndexExpression(left, right));

        public void OnSliceExpression(int? start, int? stop, int? step)
        {
            Prolog();

            var sliceExpression = new JmesPathSliceProjection(start, stop, step);

            expressions_.Push(sliceExpression);
        }

        #endregion

        #region comparator_expression

        public void OnComparisonEqual() =>
            PopPush((left, right) => new JmesPathEqualOperator(left, right));

        public void OnComparisonNotEqual() =>
            PopPush((left, right) => new JmesPathNotEqualOperator(left, right));

        public void OnComparisonGreaterOrEqual() =>
            PopPush((left, right) => new JmesPathGreaterThanOrEqualOperator(left, right));

        public void OnComparisonGreater() =>
            PopPush((left, right) => new JmesPathGreaterThanOperator(left, right));

        public void OnComparisonLesserOrEqual() =>
            PopPush((left, right) => new JmesPathLessThanOrEqualOperator(left, right));

        public void OnComparisonLesser() =>
            PopPush((left, right) => new JmesPathLessThanOperator(left, right));

        #endregion

        #region arithmetic_expression

        public void OnArithmeticUnaryPlus()
        {
            Prolog();

            var expression = expressions_.Pop();
            var arithmetic = new JmesPathUnaryPlusExpression(expression);
            expressions_.Push(arithmetic);
        }

        public void OnArithmeticUnaryMinus()
        {
            Prolog();

            var expression = expressions_.Pop();
            var arithmetic = new JmesPathUnaryMinusExpression(expression);
            expressions_.Push(arithmetic);
        }

        public void OnArithmeticAddition()
            => PopPush((left, right) => new JmesPathAdditionExpression(left, right));
        public void OnArithmeticSubtraction() 
            => PopPush((left, right) => new JmesPathSubtractionExpression(left, right));
        public void OnArithmeticMultiplication() 
            => PopPush((left, right) => new JmesPathMultiplicationExpression(left, right));
        public void OnArithmeticDivision() 
            => PopPush((left, right) => new JmesPathDivisionExpression(left, right));
        public void OnArithmeticModulo() 
            => PopPush((left, right) => new JmesPathModuloExpression(left, right));
        public void OnArithmeticIntegerDivision() 
            => PopPush((left, right) => new JmesPathIntegerDivisionExpression(left, right));

        #endregion
        
        #region logical_expression

        public void OnOrExpression() =>
            PopPush((left, right) => new JmesPathOrExpression(left, right));

        public void OnAndExpression() =>
            PopPush((left, right) => new JmesPathAndExpression(left, right));

        public void OnNotExpression() =>
            PopPush(e => new JmesPathNotExpression(e));

        #endregion

public void OnIdentifier(string name)
        {
            Prolog();

            var expression = new JmesPathIdentifier(name);
            expressions_.Push(expression);
        }

        static readonly JmesPathHashWildcardProjection JmesPathHashWildcardProjection = new JmesPathHashWildcardProjection();

        public void OnHashWildcardProjection()
        {
            Prolog();

            expressions_.Push(JmesPathHashWildcardProjection);
        }

        #region multi_select_hash

        public void PushMultiSelectHash()
        {
            selectHashes_.Push(new Dictionary<string, JmesPathExpression>());
        }

        public void AddMultiSelectHashExpression()
        {
            Prolog();

            System.Diagnostics.Debug.Assert(expressions_.Count >= 2);
            System.Diagnostics.Debug.Assert(selectHashes_.Count > 0);

            var expression = expressions_.Pop();

            var identifier = expressions_.Pop() as JmesPathIdentifier;
            System.Diagnostics.Debug.Assert(identifier != null);
            var name = identifier.Name;
            System.Diagnostics.Debug.Assert(name != null);

            var items = selectHashes_.Peek();
            items.Add(name, expression);
        }

        public void PopMultiSelectHash()
        {
            System.Diagnostics.Debug.Assert(selectHashes_.Count > 0);
            var items = selectHashes_.Pop();
            var expression = new JmesPathMultiSelectHash(items);
            expressions_.Push(expression);
        }

        #endregion

        #region multi_select_list

        public void PushMultiSelectList()
        {
            selectLists_.Push(new List<JmesPathExpression>());
        }

        public void AddMultiSelectListExpression()
        {
            Prolog();

            System.Diagnostics.Debug.Assert(selectLists_.Count > 0);
            var expression = expressions_.Pop();
            var items = selectLists_.Peek();
            items.Add(expression);
        }

        public void PopMultiSelectList()
        {
            System.Diagnostics.Debug.Assert(selectLists_.Count > 0);
            var items = selectLists_.Pop();
            var expression = new JmesPathMultiSelectList(items);

            expressions_.Push(expression);
        }

        #endregion

        public void OnLiteralString(string literal)
        {
            Prolog();

            var token = JToken.Parse(literal);
            var expression = new JmesPathLiteral(token);
            expressions_.Push(expression);
        }

        public void OnPipeExpression()
            => PopPush((left, right) => new JmesPathPipeExpression(left, right));

        #region function_expression

        public void PushFunction()
        {
            functions_.Push(new List<JmesPathExpression>());
        }

        public void PopFunction(string name)
        {
            System.Diagnostics.Debug.Assert(functions_.Count > 0);

            var args = functions_.Pop();
            var expressions = args.ToArray();

            var expression = new JmesPathFunctionExpression(repository_, name, expressions);

            expressions_.Push(expression);
        }

        public void AddFunctionArg()
        {
            Prolog();

            System.Diagnostics.Debug.Assert(functions_.Count > 0);

            var expression = expressions_.Pop();
            functions_.Peek().Add(expression);
        }

        public void OnExpressionType()
        {
            Prolog();

            var expression = expressions_.Pop();
            JmesPathExpression.MakeExpressionType(expression);
            expressions_.Push(expression);
        }

        #endregion

        public void OnRawString(string value)
        {
            Prolog();

            var expression = new JmesPathRawString(value);
            expressions_.Push(expression);
        }

        public void OnCurrentNode()
        {
            Prolog();

            expressions_.Push(new JmesPathCurrentNodeExpression());
        }

        #endregion // Expressions

        void PopPush<T>(Func<JmesPathExpression, T> factory)
            where T : JmesPathExpression
        {
            Prolog();

            System.Diagnostics.Debug.Assert(expressions_.Count >= 1);

            var arg = expressions_.Pop();

            expressions_.Push(factory(arg));
        }

        void PopPush<T>(Func<JmesPathExpression, JmesPathExpression, T> factory)
            where T : JmesPathExpression
        {
            Prolog();

            System.Diagnostics.Debug.Assert(expressions_.Count >= 2);

            var right = expressions_.Pop();
            var left = expressions_.Pop();

            expressions_.Push(factory(left, right));
        }
    }
}
