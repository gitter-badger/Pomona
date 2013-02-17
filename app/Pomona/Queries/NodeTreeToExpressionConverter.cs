#region License

// ----------------------------------------------------------------------------
// Pomona source code
// 
// Copyright � 2013 Karsten Nikolai Strand
// 
// Permission is hereby granted, free of charge, to any person obtaining a 
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// ----------------------------------------------------------------------------

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Pomona.Common.Internals;
using Pomona.Internals;

namespace Pomona.Queries
{
    internal class NodeTreeToExpressionConverter
    {
        private readonly IQueryTypeResolver propertyResolver;
        private Dictionary<string, ParameterExpression> parameters;

        private ParameterExpression thisParam;


        public NodeTreeToExpressionConverter(IQueryTypeResolver propertyResolver)
        {
            if (propertyResolver == null)
                throw new ArgumentNullException("propertyResolver");
            this.propertyResolver = propertyResolver;
        }


        public Expression ParseExpression(NodeBase node)
        {
            return ParseExpression(node, null, null);
        }


        public Expression ParseExpression(NodeBase node, Expression memberExpression, Type expectedType)
        {
            if (memberExpression == null)
                memberExpression = this.thisParam;

            if (node.NodeType == NodeType.ArrayLiteral)
                return ParseArrayLiteral((ArrayNode)node, memberExpression);

            if (node.NodeType == NodeType.MethodCall)
                return ParseMethodCallNode((MethodCallNode)node, memberExpression);

            if (node.NodeType == NodeType.IndexerAccess)
                return ParseIndexerAccessNode((IndexerAccessNode)node, memberExpression);

            if (node.NodeType == NodeType.Symbol)
                return ResolveSymbolNode((SymbolNode)node, memberExpression);

            var binaryOperatorNode = node as BinaryOperator;

            if (binaryOperatorNode != null)
                return ParseBinaryOperator(binaryOperatorNode, memberExpression);

            if (node.NodeType == NodeType.GuidLiteral)
            {
                var guidNode = (GuidNode)node;
                return Expression.Constant(guidNode.Value);
            }

            if (node.NodeType == NodeType.DateTimeLiteral)
            {
                var dateTimeNode = (DateTimeNode)node;
                return Expression.Constant(dateTimeNode.Value);
            }

            if (node.NodeType == NodeType.StringLiteral)
            {
                var stringNode = (StringNode)node;
                return Expression.Constant(stringNode.Value);
            }

            if (node.NodeType == NodeType.NumberLiteral)
            {
                var intNode = (NumberNode)node;
                return Expression.Constant(intNode.Parse());
            }

            if (node.NodeType == NodeType.Lambda)
            {
                var lambdaNode = (LambdaNode)node;
                return ParseLambda(lambdaNode, memberExpression, expectedType);
            }

            throw new NotImplementedException();
        }


        private Expression ParseArrayLiteral(ArrayNode node, Expression memberExpression)
        {
            var arrayElements = node.Children.Select(x => ParseExpression(x, thisParam, null)).ToList();

            if (arrayElements.Count == 0)
                throw new NotSupportedException("Does not support empty arrays.");

            Type elementType = arrayElements[0].Type;

            // TODO: Check that all array members are of same type

            if (arrayElements.All(x => x is ConstantExpression))
            {
                var array = Array.CreateInstance(elementType, arrayElements.Count);
                int index = 0;
                foreach (var elementValue in arrayElements.OfType<ConstantExpression>().Select(x => x.Value))
                {
                    array.SetValue(elementValue, index++);
                }
                return Expression.Constant(array);
            }

            return Expression.NewArrayInit(elementType, arrayElements);
        }


        public LambdaExpression ToLambdaExpression(Type thisType, NodeBase node)
        {
            var param = Expression.Parameter(thisType, "_this");
            return ToLambdaExpression(param, param.WrapAsEnumerable(), null, node);
        }


        public LambdaExpression ToLambdaExpression(
            ParameterExpression thisParam,
            IEnumerable<ParameterExpression> lamdbaParameters,
            IEnumerable<ParameterExpression> outerParameters,
            NodeBase node)
        {
            if (thisParam == null)
                throw new ArgumentNullException("thisParam");
            if (lamdbaParameters == null)
                throw new ArgumentNullException("lamdbaParameters");
            try
            {
                this.thisParam = thisParam;
                this.parameters =
                    lamdbaParameters
                        .Where(x => x != thisParam)
                        .Concat(outerParameters ?? Enumerable.Empty<ParameterExpression>())
                        .ToDictionary(x => x.Name, x => x);

                return Expression.Lambda(ParseExpression(node), lamdbaParameters);
            }
            finally
            {
                this.thisParam = null;
            }
        }


        private Exception CreateParseException(MethodCallNode node, string message)
        {
            return new QueryParseException(message);
        }


        private Expression ParseBinaryOperator(BinaryOperator binaryOperatorNode, Expression memberExpression)
        {
            if (binaryOperatorNode.NodeType == NodeType.Dot)
            {
                if (binaryOperatorNode.Right.NodeType == NodeType.MethodCall)
                {
                    var origCallNode = (MethodCallNode)binaryOperatorNode.Right;
                    // Rewrite extension method call to static method call of tree:
                    // We do this by taking inserting the first node before arg nodes of extension method call.
                    var staticMethodArgs = binaryOperatorNode
                        .Left.WrapAsEnumerable()
                        .Concat(binaryOperatorNode.Right.Children);
                    var staticMethodCall = new MethodCallNode(origCallNode.Name, staticMethodArgs);

                    return ParseExpression(staticMethodCall);
                }
                var left = ParseExpression(binaryOperatorNode.Left);
                return ParseExpression(binaryOperatorNode.Right, left, null);
            }

            // Break dot chain
            var leftChild = ParseExpression(binaryOperatorNode.Left);
            var rightChild = ParseExpression(binaryOperatorNode.Right);

            switch (binaryOperatorNode.NodeType)
            {
                case NodeType.AndAlso:
                    return Expression.AndAlso(leftChild, rightChild);
                case NodeType.OrElse:
                    return Expression.OrElse(leftChild, rightChild);
                case NodeType.Add:
                    return Expression.Add(leftChild, rightChild);
                case NodeType.Subtract:
                    return Expression.Subtract(leftChild, rightChild);
                case NodeType.Multiply:
                    return Expression.Multiply(leftChild, rightChild);
                case NodeType.Modulo:
                    return Expression.Modulo(leftChild, rightChild);
                case NodeType.Div:
                    return Expression.Divide(leftChild, rightChild);
                case NodeType.Equal:
                    return ParseEqualOperator(leftChild, rightChild);
                case NodeType.LessThan:
                    return Expression.LessThan(leftChild, rightChild);
                case NodeType.GreaterThan:
                    return Expression.GreaterThan(leftChild, rightChild);
                case NodeType.GreaterThanOrEqual:
                    return Expression.GreaterThanOrEqual(leftChild, rightChild);
                case NodeType.LessThanOrEqual:
                    return Expression.LessThanOrEqual(leftChild, rightChild);
                case NodeType.In:
                    return ParseInOperator(leftChild, rightChild);
                default:
                    throw new NotImplementedException(
                        "Don't know how to handle node type " + binaryOperatorNode.NodeType);
            }
        }


        private Expression ParseInOperator(Expression leftChild, Expression rightChild)
        {
            if (!rightChild.Type.IsArray)
                throw new QueryParseException("in operator requires array on right side.");

            var arrayElementType = rightChild.Type.GetElementType();
            if (leftChild.Type != arrayElementType)
                throw new QueryParseException("Left and right side of in operator does not have matching types.");

            return Expression.Call(OdataFunctionMapping.EnumerableContainsMethod.MakeGenericMethod(arrayElementType), rightChild, leftChild);
        }


        private Expression ParseEqualOperator(Expression leftChild, Expression rightChild)
        {
            TryDetectAndConvertEnumComparison(ref leftChild, ref rightChild, true);
            return Expression.Equal(leftChild, rightChild);
        }


        private Expression ParseIndexerAccessNode(IndexerAccessNode node, Expression memberExpression)
        {
            var property = this.propertyResolver.ResolveProperty(memberExpression, node.Name);
            if (typeof(IDictionary<string, string>).IsAssignableFrom(property.Type))
            {
                return Expression.Call(
                    property, OdataFunctionMapping.DictGetMethod, ParseExpression(node.Children[0]));
            }
            throw new NotImplementedException();
        }


        private Expression ParseLambda(LambdaNode lambdaNode, Expression memberExpression, Type expectedType)
        {
            if (expectedType.MetadataToken == typeof(Expression<>).MetadataToken)
            {
                // Quote if expression
                return Expression.Quote(
                    ParseLambda(lambdaNode, memberExpression, expectedType.GetGenericArguments()[0]));
            }

            var nestedConverter = new NodeTreeToExpressionConverter(this.propertyResolver);

            // TODO: Check that we don't already have a arg with same name.

            // TODO: Proper check that we have a func here
            if (expectedType.MetadataToken != typeof(Func<,>).MetadataToken)
                throw new QueryParseException("Can't parse lambda to expected type that is not a Func delegate..");

            if (expectedType.GetGenericArguments()[0].IsGenericParameter)
                throw new QueryParseException("Unable to resolve generic type for parsing lambda.");

            var funcTypeArgs = expectedType.GetGenericArguments();

            // TODO: Support multiple lambda args..(?)
            var lambdaParams =
                lambdaNode.Argument.WrapAsEnumerable().Select(
                    (x, idx) => Expression.Parameter(funcTypeArgs[idx], x.Name)).ToList();

            return nestedConverter.ToLambdaExpression(
                this.thisParam, lambdaParams, this.parameters.Values, lambdaNode.Body);
        }


        private Expression ParseMethodCallNode(MethodCallNode node, Expression memberExpression)
        {
            if (memberExpression == null)
                throw new ArgumentNullException("memberExpression");
            if (memberExpression == this.thisParam)
            {
                if (node.HasArguments)
                {
                    Expression expression;
                    if (TryResolveOdataExpression(node, memberExpression, out expression))
                        return expression;
                }
            }
            throw CreateParseException(node, "Could not recognize method " + node.Name);
        }


        private Expression ResolveSymbolNode(SymbolNode node, Expression memberExpression)
        {
            if (memberExpression == null)
                throw new ArgumentNullException("memberExpression");
            if (memberExpression == this.thisParam)
            {
                if (node.Name == "this")
                    return this.thisParam;
                if (node.Name == "true")
                    return Expression.Constant(true);
                if (node.Name == "false")
                    return Expression.Constant(false);
                if (node.Name == "null")
                    return Expression.Constant(null);
                ParameterExpression parameter;
                if (this.parameters.TryGetValue(node.Name, out parameter))
                    return parameter;
            }

            return this.propertyResolver.ResolveProperty(memberExpression, node.Name);
        }


        private bool ResolveTypeArgs(
            Type wantedType, Type actualType, Type[] methodTypeArgs, out bool typeArgsWasResolved)
        {
            typeArgsWasResolved = false;
            if (wantedType.IsGenericTypeDefinition)
            {
                throw new ArgumentException(
                    "Does not expect genDefArgType to be a generic type definition.", "genDefArgType");
            }
            if (actualType.IsGenericTypeDefinition)
            {
                throw new ArgumentException(
                    "Does not expect instanceArgType to be a generic type definition.", "instanceArgType");
            }

            if (!wantedType.IsGenericType)
            {
                if (!wantedType.IsAssignableFrom(actualType))
                    return false;
            }
            else
            {
                var wantedTypeArgs = wantedType.GetGenericArguments();
                Type[] actualTypeArgs;
                if (!TryExtractTypeArguments(wantedType.GetGenericTypeDefinition(), actualType, out actualTypeArgs))
                    return false;

                for (var i = 0; i < wantedTypeArgs.Length; i++)
                {
                    var wantedTypeArg = wantedTypeArgs[i];
                    var actualTypeArg = actualTypeArgs[i];

                    if (wantedTypeArg.IsGenericParameter)
                    {
                        if (methodTypeArgs[wantedTypeArg.GenericParameterPosition] != actualTypeArg)
                        {
                            typeArgsWasResolved = true;
                            methodTypeArgs[wantedTypeArg.GenericParameterPosition] = actualTypeArg;
                        }
                    }
                    else
                    {
                        bool innerTypeArgsWasResolved;
                        if (!ResolveTypeArgs(wantedTypeArg, actualTypeArg, methodTypeArgs, out innerTypeArgsWasResolved))
                            return false;

                        if (innerTypeArgsWasResolved)
                            typeArgsWasResolved = true;
                    }
                }
            }

            return true;
        }


        private void TryDetectAndConvertEnumComparison(ref Expression left, ref Expression right, bool tryAgainSwapped)
        {
            if (left.Type.IsEnum && right.NodeType == ExpressionType.Constant && right.Type == typeof(string))
            {
                var enumType = left.Type;
                left = Expression.Convert(left, enumType.UnderlyingSystemType);
                var enumStringValue = (string)((ConstantExpression)right).Value;
                var enumIntvalue = Convert.ChangeType(
                    Enum.Parse(enumType, enumStringValue),
                    enumType.UnderlyingSystemType);
                right = Expression.Constant(enumIntvalue, enumType.UnderlyingSystemType);
                return;
            }

            if (tryAgainSwapped)
                TryDetectAndConvertEnumComparison(ref right, ref left, false);
        }


        private bool TryExtractTypeArguments(Type genTypeDef, Type typeInstance, out Type[] typeArgs)
        {
            if (typeInstance.GetGenericTypeDefinition() == genTypeDef)
            {
                typeArgs = typeInstance.GetGenericArguments();
                return true;
            }
            foreach (var interfaceType in typeInstance.GetInterfaces())
            {
                if (TryExtractTypeArguments(genTypeDef, interfaceType, out typeArgs))
                    return true;
            }

            typeArgs = null;
            return false;
        }


        private bool TryResolveGenericInstanceMethod<TMemberInfo>(Expression instance, ref TMemberInfo member)
            where TMemberInfo : MemberInfo
        {
            var declaringType = member.DeclaringType;
            if (declaringType.IsGenericTypeDefinition)
            {
                Type[] typeArgs;
                if (TryExtractTypeArguments(declaringType, instance.Type, out typeArgs))
                {
                    var memberLocal = member;
                    member = declaringType
                        .MakeGenericType(typeArgs)
                        .GetMember(memberLocal.Name)
                        .OfType<TMemberInfo>()
                        .Single(x => x.MetadataToken == memberLocal.MetadataToken);
                }
                else
                {
                    // Neither type nor any of interfaces of instance matches declaring type.
                    return false;
                }
            }
            return true;
        }


        private bool TryResolveMemberMapping(
            OdataFunctionMapping.MemberMapping memberMapping, MethodCallNode node, out Expression expression)
        {
            expression = null;
            var reorderedArgs = memberMapping.ReorderArguments(node.Children);
            var method = memberMapping.Member as MethodInfo;
            var property = memberMapping.Member as PropertyInfo;
            if (method != null)
            {
                Expression instance = null;

                if (!method.IsStatic)
                {
                    instance = ParseExpression(reorderedArgs[0], this.thisParam, null);
                    if (!TryResolveGenericInstanceMethod(instance, ref method))
                        return false;
                }

                // Convert each node and check whether argument matches..
                var argArrayOffset = method.IsStatic ? 0 : 1;
                var methodParameters = method.GetParameters();
                if (methodParameters.Length != reorderedArgs.Count - argArrayOffset)
                {
                    throw new PomonaExpressionSyntaxException(
                        string.Format(
                            "Number parameters count ({0}) for method {1}.{2} does not match provided argument count ({3})",
                            methodParameters.Length,
                            method.DeclaringType.FullName,
                            method.Name,
                            (reorderedArgs.Count - argArrayOffset)));
                }

                var argExprArray = new Expression[methodParameters.Length];

                if (!method.IsGenericMethodDefinition)
                {
                    for (var i = 0; i < methodParameters.Length; i++)
                    {
                        var param = methodParameters[i];
                        var argNode = reorderedArgs[i + argArrayOffset];
                        var argExpr = ParseExpression(argNode, this.thisParam, param.ParameterType);

                        if (!param.ParameterType.IsAssignableFrom(argExpr.Type))
                            return false;

                        argExprArray[i] = argExpr;
                    }
                }
                else
                {
                    var methodDefinition = method;
                    var methodTypeArgs = method.GetGenericArguments();

                    for (var i = 0; i < methodParameters.Length; i++)
                    {
                        var param = methodParameters[i];
                        var argNode = reorderedArgs[i + argArrayOffset];
                        var argExpr = ParseExpression(argNode, this.thisParam, param.ParameterType);

                        bool typeArgsWasResolved;
                        if (!ResolveTypeArgs(param.ParameterType, argExpr.Type, methodTypeArgs, out typeArgsWasResolved))
                            return false;

                        if (typeArgsWasResolved)
                        {
                            // Upgrade to real method when all type args are resolved!!
                            method = methodDefinition.MakeGenericMethod(methodTypeArgs);
                            methodParameters = method.GetParameters();
                        }

                        argExprArray[i] = argExpr;
                    }
                }

                expression = Expression.Call(instance, method, argExprArray);
                return true;
            }
            if (property != null)
            {
                var instance = ParseExpression(reorderedArgs[0], this.thisParam, null);
                if (!TryResolveGenericInstanceMethod(instance, ref property))
                    return false;

                expression = Expression.MakeMemberAccess(instance, property);
                return true;
            }
            return false;
        }


        private bool TryResolveOdataExpression(
            MethodCallNode node, Expression memberExpression, out Expression expression)
        {
            expression = null;

            switch (node.Name)
            {
                case "isof":
                    var checkType = this.propertyResolver.ResolveType(((SymbolNode)node.Children[0]).Name);
                    expression = Expression.TypeIs(this.thisParam, checkType);
                    return true;
                case "cast":
                    //var 
                    if (node.Children.Count > 2 || node.Children.Count < 1)
                        throw new PomonaExpressionSyntaxException("Only one or two arguments to cast operator is allowed.");
                    NodeBase castTypeArg;
                    Expression operand;

                    if (node.Children.Count == 1)
                    {
                        castTypeArg = node.Children[0];
                        operand = this.thisParam;
                    }
                    else
                    {
                        operand = ParseExpression(node.Children[0]);
                        castTypeArg = node.Children[1];
                    }

                    string typeName;
                    if (castTypeArg is SymbolNode)
                    {
                        typeName = ((SymbolNode) castTypeArg).Name;
                    }
                    else if (castTypeArg is StringNode)
                    {
                        typeName = ((StringNode) castTypeArg).Value;
                    }
                    else
                    {
                        throw new PomonaExpressionSyntaxException("Did not expect node type " + castTypeArg.GetType().Name);
                    }

                    var castToType = this.propertyResolver.ResolveType(typeName);
                    expression = Expression.Convert(operand, castToType);
                    return true;
            }

            var memberCandidates = OdataFunctionMapping.GetMemberCandidates(node.Name, node.Children.Count);

            foreach (var memberMapping in memberCandidates)
            {
                if (TryResolveMemberMapping(memberMapping, node, out expression))
                    return true;
            }

            return false;
        }
    }
}