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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using Pomona.Common;

namespace Pomona.CodeGen
{
    public struct StringEnumTemplate : IEquatable<StringEnumTemplate>, IStringEnum<StringEnumTemplate>
    {
        public static readonly StringEnumTemplate MemberTemplate = new StringEnumTemplate("MemberTemplate");
        private static readonly StringEnumTemplate defaultValue;

        private static ReadOnlyCollection<StringEnumTemplate> values;
        private readonly string value;


        private StringEnumTemplate(string value)
        {
            this.value = value;
        }


        public static ReadOnlyCollection<StringEnumTemplate> AllValues
        {
            get
            {
                if (values == null)
                {
                    values = new ReadOnlyCollection<StringEnumTemplate>(StringEnumExtensions.ScanStringEnumValues<StringEnumTemplate>().ToList());
                }
                return values;
            }
        }

        private static Dictionary<string, StringEnumTemplate> knownValuesMap;

        private static Dictionary<string, StringEnumTemplate> KnownValuesMap
        {
            get
            {
                if (knownValuesMap == null)
                    knownValuesMap = AllValues.ToDictionary(x => x.Value, x => x, StringComparer.InvariantCultureIgnoreCase);
                return knownValuesMap;
            }
        }


        public static bool TryParse(string str, out StringEnumTemplate value)
        {
            value = (StringEnumTemplate)str;
            return true;
        }

        public static StringEnumTemplate Parse(string str)
        {
            return (StringEnumTemplate)str;
        }

        public bool IsDefault
        {
            get { return defaultValue == this; }
        }

        public bool IsKnown
        {
            get { return KnownValuesMap.ContainsKey(Value); }
        }

        public string Value
        {
            get { return this.value ?? defaultValue.value; }
        }


        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is StringEnumTemplate && Equals((StringEnumTemplate)obj);
        }


        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }


        public bool Equals(StringEnumTemplate other)
        {
            return string.Equals(Value, other.Value, StringComparison.InvariantCultureIgnoreCase);
        }


        public override string ToString()
        {
            return Value;
        }

        #region Operators

        public static bool operator ==(StringEnumTemplate left, StringEnumTemplate right)
        {
            return left.Equals(right);
        }


        public static explicit operator StringEnumTemplate(string str)
        {
            StringEnumTemplate val;
            if (KnownValuesMap.TryGetValue(str, out val))
                return val;
            return new StringEnumTemplate(str);
        }


        public static explicit operator string(StringEnumTemplate value)
        {
            return value.Value;
        }


        public static bool operator !=(StringEnumTemplate left, StringEnumTemplate right)
        {
            return !left.Equals(right);
        }

        #endregion
    }
}