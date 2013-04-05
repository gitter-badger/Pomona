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
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nancy;
using Nancy.Responses.Negotiation;
using Pomona.Common.Serialization;
using ISerializer = Pomona.Common.Serialization.ISerializer;

namespace Pomona
{
    public abstract class PomonaResponseProcessorBase : IResponseProcessor
    {
        private readonly ISerializer serializer;
        private readonly ISerializerFactory serializerFactory;

        protected PomonaResponseProcessorBase(ISerializerFactory serializerFactory)
        {
            this.serializerFactory = serializerFactory;
            serializer = serializerFactory.GetSerialier();
        }

        protected abstract string ContentType { get; }

        /// <summary>
        /// Gets a set of mappings that map a given extension (such as .json)
        /// to a media range that can be sent to the client in a vary header.
        /// </summary>
        public abstract IEnumerable<Tuple<string, MediaRange>> ExtensionMappings { get; }


        /// <summary>
        /// Process the response
        /// </summary>
        /// <param name="requestedMediaRange">Content type requested by the client</param>
        /// <param name="model">The model for the given media range</param>
        /// <param name="context">The nancy context</param>
        /// <returns>A response</returns>
        public virtual Response Process(MediaRange requestedMediaRange, dynamic model, NancyContext context)
        {
            var pomonaResponse = (PomonaResponse) model;

            string jsonString;
            using (var strWriter = new StringWriter())
            {
                var pq = (PomonaQuery) pomonaResponse.Query;
                var serializationContext = new ServerSerializationContext(pq.ExpandedPaths, false,
                                                                          pomonaResponse.Session);
                serializer.Serialize(serializationContext, pomonaResponse.Entity, strWriter, pq.ResultType);
                jsonString = strWriter.ToString();
            }

            if (IsTextHtmlContentType(requestedMediaRange))
            {
                // Wrap in html
                var response = new Response();
                HtmlJsonPrettifier.CreatePrettifiedHtmlJsonResponse(response, string.Empty, jsonString,
                                                                    "http://failfailtodo");
                return response;
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(jsonString);
                return new Response
                    {
                        Contents = s => s.Write(bytes, 0, bytes.Length),
                        ContentType = ContentType
                    };
            }
        }

        public abstract ProcessorMatch CanProcess(MediaRange requestedMediaRange, dynamic model, NancyContext context);

        protected bool IsTextHtmlContentType(MediaRange requestedMediaRange)
        {
            return requestedMediaRange.Matches("text/html");
        }
    }
}