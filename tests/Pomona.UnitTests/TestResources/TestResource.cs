#region License

// ----------------------------------------------------------------------------
// Pomona source code
// 
// Copyright � 2016 Karsten Nikolai Strand
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

using System.Collections.Generic;

namespace Pomona.UnitTests.TestResources
{
    public class TestResource : ITestResource
    {
        public IDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();

        public IList<ITestResource> Children { get; } = new List<ITestResource>();

        public IDictionary<string, string> Dictionary { get; } = new Dictionary<string, string>();

        public ITestResource Friend { get; set; }
        public int Id { get; set; }
        public string Info { get; set; }
        public ISet<ITestResource> Set { get; } = new HashSet<ITestResource>();
        public ITestResource Spouse { get; set; }
    }
}