﻿// ----------------------------------------------------------------------------
// Pomona source code
// 
// Copyright © 2013 Karsten Nikolai Strand
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

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Pomona.Common
{
    public class QuerySelectBuilder
    {
        private readonly LambdaExpression lambda;

        public QuerySelectBuilder(LambdaExpression lambda)
        {
            if (lambda == null) throw new ArgumentNullException("lambda");
            this.lambda = lambda;
        }


        public override string ToString()
        {
            var sb = new StringBuilder();

            var newExprBody = lambda.Body as NewExpression;

            if (newExprBody != null)
            {
                foreach (
                    var arg in
                        newExprBody.Arguments.Zip(
                            newExprBody.Members, (e, p) => new {p.Name, Expr = e}))
                {
                    if (sb.Length > 0)
                        sb.Append(',');
                    var argLambda = Expression.Lambda(arg.Expr, lambda.Parameters);
                    var predicateBuilder = new QueryPredicateBuilder(argLambda);
                    sb.Append(predicateBuilder);
                    sb.Append(" as ");
                    sb.Append(arg.Name);
                }
            }
            else
            {
                var predicateBuilder = new QueryPredicateBuilder(lambda);
                sb.Append(predicateBuilder);
                sb.Append(" as this");
            }

            return sb.ToString();
        }
    }
}