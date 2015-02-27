﻿#region License

// ----------------------------------------------------------------------------
// Pomona source code
// 
// Copyright © 2014 Karsten Nikolai Strand
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

using System.Text;

namespace Pomona.Common.Web
{
    public class WebClientRequestMessage
    {
        private readonly byte[] data;
        private readonly HttpHeaders headers = new HttpHeaders();
        private readonly string method;
        private string uri;


        public WebClientRequestMessage(string uri, byte[] data = null, string method = null)
        {
            this.uri = uri;
            this.data = data;
            this.method = method;
        }


        public byte[] Data
        {
            get { return this.data; }
        }

        public HttpHeaders Headers
        {
            get { return this.headers; }
        }

        public string Method
        {
            get { return this.method; }
        }

        public string ProtocolVersion
        {
            get { return "1.1"; }
        }

        public string Uri
        {
            get { return this.uri; }
            set { this.uri = value; }
        }


        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0} {1} HTTP/{2}\r\n", this.method, this.uri, ProtocolVersion);
            foreach (var h in this.headers)
            {
                foreach (var v in h.Value)
                    sb.AppendFormat("{0}: {1}\r\n", h.Key, v);
            }
            sb.AppendLine();
            if (this.data != null)
                sb.Append(Encoding.UTF8.GetString(this.data));
            sb.AppendLine();
            return sb.ToString();
        }
    }
}