﻿namespace FakeItEasy.Expressions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using FakeItEasy.Core;

    /// <summary>
    /// Handles the matching of fake object calls to expressions.
    /// </summary>
    internal class ExpressionCallMatcher
        : ICallMatcher
    {
        private IEnumerable<IArgumentConstraint> argumentConstraints;
        private MethodInfoManager methodInfoManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpressionCallMatcher"/> class.
        /// </summary>
        /// <param name="callSpecification">The call specification.</param>
        /// <param name="constraintFactory">The constraint factory.</param>
        /// <param name="methodInfoManager">The method infor manager to use.</param>
        public ExpressionCallMatcher(LambdaExpression callSpecification, ArgumentConstraintFactory constraintFactory, MethodInfoManager methodInfoManager)
        {
            this.methodInfoManager = methodInfoManager;
            this.Method = GetMethodInfo(callSpecification);

            this.argumentConstraints = GetArgumentConstraints(callSpecification, constraintFactory).ToArray();
            this.argumentsPredicate = this.ArgumentsMatchesArgumentConstraints;
        }

        private MethodInfo Method { get; set; }

        private static MethodInfo GetMethodInfo(LambdaExpression callSpecification)
        {
            var methodExpression = callSpecification.Body as MethodCallExpression;
            if (methodExpression != null)
            {
                return methodExpression.Method;
            }

            var memberExpression = callSpecification.Body as MemberExpression;
            if (memberExpression != null && memberExpression.Member.MemberType == MemberTypes.Property)
            {
                var property = memberExpression.Member as PropertyInfo;
                return property.GetGetMethod(true);
            }

            throw new ArgumentException(ExceptionMessages.CreatingExpressionCallMatcherWithNonMethodOrPropertyExpression);
        }

        private static IEnumerable<IArgumentConstraint> GetArgumentConstraints(LambdaExpression callSpecification, ArgumentConstraintFactory constraintFactory)
        {
            var methodExpression = callSpecification.Body as MethodCallExpression;
            if (methodExpression != null)
            {
                return
                    (from argument in methodExpression.Arguments
                     select constraintFactory.GetArgumentConstraint(argument));
            }

            return Enumerable.Empty<IArgumentConstraint>();
        }

        /// <summary>
        /// Matcheses the specified call against the expression.
        /// </summary>
        /// <param name="call">The call to match.</param>
        /// <returns>True if the call is matched by the expression.</returns>
        public virtual bool Matches(IFakeObjectCall call)
        {
            return this.InvokesSameMethodOnTarget(call.FakedObject.GetType(), call.Method, this.Method)
                && this.ArgumentsMatches(call.Arguments);
        }

        public virtual void UsePredicateToValidateArguments(Func<ArgumentCollection, bool> argumentsPredicate)
        {
            this.argumentsPredicate = argumentsPredicate;

            var numberOfValdiators = this.argumentConstraints.Count();
            this.argumentConstraints = Enumerable.Repeat<IArgumentConstraint>(new PredicatedArgumentConstraint(), numberOfValdiators);
        }

        private bool InvokesSameMethodOnTarget(Type type, MethodInfo first, MethodInfo second)
        {
            return this.methodInfoManager.WillInvokeSameMethodOnTarget(type, first, second);
        }

        public override string ToString()
        {
            var result = new StringBuilder();

            result.Append(this.Method.DeclaringType.FullName);
            result.Append(".");
            result.Append(this.Method.Name);
            this.AppendArgumentsListString(result);

            return result.ToString();
        }

        private void AppendArgumentsListString(StringBuilder result)
        {
            result.Append("(");
            bool firstArgument = true;

            foreach (var constraint in this.argumentConstraints)
            {
                if (!firstArgument)
                {
                    result.Append(", ");
                }
                else
                {
                    firstArgument = false;
                }

                result.Append(constraint.ToString());
            }

            result.Append(")");
        }

        private Func<ArgumentCollection, bool> argumentsPredicate;

        private bool ArgumentsMatches(ArgumentCollection argumentCollection)
        {
            return this.argumentsPredicate(argumentCollection);
        }

        private bool ArgumentsMatchesArgumentConstraints(ArgumentCollection argumentCollection)
        {
            foreach (var argumentConstraintPair in argumentCollection.AsEnumerable().Zip(this.argumentConstraints))
            {
                if (!argumentConstraintPair.Second.IsValid(argumentConstraintPair.First))
                {
                    return false;
                }
            }

            return true;
        }

        private class PredicatedArgumentConstraint
            : IArgumentConstraint
        {
            public bool IsValid(object argument)
            {
                return true;
            }

            public override string ToString()
            {
                return "<Predicated>";
            }
        }
    }

    internal interface IExpressionCallMatcherFactory
    {
        ICallMatcher CreateCallMathcer(LambdaExpression callSpecification);
    }
}