#region License

// ----------------------------------------------------------------------------
// Pomona source code
// 
// Copyright � 2014 Karsten Nikolai Strand
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
using System.Reflection;

namespace Pomona.Common.TypeSystem
{
    public class ComplexProperty : RuntimePropertySpec
    {
        private readonly Lazy<ComplexPropertyDetails> exportedPropertyDetails;


        public ComplexProperty(IExportedTypeResolver typeResolver,
                               PropertyInfo propertyInfo,
                               ComplexType reflectedType)
            : base(typeResolver, propertyInfo, reflectedType)
        {
            this.exportedPropertyDetails = CreateLazy(() => typeResolver.LoadComplexPropertyDetails(this));
        }


        public virtual bool ExposedAsRepository
        {
            get { return Details.ExposedAsRepository; }
        }

        public virtual bool ExposedOnUrl
        {
            get
            {
                // TODO: Make this configurable
                return PropertyType is ResourceType || PropertyType is EnumerableTypeSpec;
            }
        }

        public virtual bool IsAttributesProperty
        {
            get { return Details.IsAttributesProperty; }
        }

        public virtual bool IsEtagProperty
        {
            get { return Details.IsEtagProperty; }
        }

        public virtual bool IsPrimaryKey
        {
            get { return Details.IsPrimaryKey; }
        }

        public override HttpMethod AccessMode
        {
            get { return Details.AccessMode; }
        }

        public ExpandMode ExpandMode
        {
            get { return Details.ExpandMode; }
        }

        public override bool IsSerialized
        {
            get { return Details.IsSerialized; }
        }

        public override HttpMethod ItemAccessMode
        {
            get { return Details.ItemAccessMode; }
        }

        public new ComplexType ReflectedType
        {
            get { return (ComplexType)base.ReflectedType; }
        }

        public string UriName
        {
            get { return Details.UriName; }
        }

        protected virtual ComplexPropertyDetails Details
        {
            get { return this.exportedPropertyDetails.Value; }
        }
    }
}