/*
 * Copyright 2025 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public sealed class ArdiaError
    {
        public static ArdiaError CreateFromResponse(HttpStatusCode? statusCode, string responseBody)
        {
            return new ArdiaError
            {
                StatusCode = statusCode,
                Message = GetIfErrorInResponse(responseBody)
            };
        }

        // CONSIDER: this drops the stack trace. Ok for this scenario?
        public static ArdiaError CreateFromException(Exception e)
        {
            return new ArdiaError
            {
                StatusCode = null,
                Message = e.Message
            };
        }

        private ArdiaError() { }

        public string Message { get; private set; }
        public HttpStatusCode? StatusCode { get; private set; }
        public int? StatusCodeInt => StatusCode != null ? (int)StatusCode : (int?)null;
        public string StatusCodeIntStr => StatusCode != null ? StatusCodeInt.ToString() : string.Empty;
        public string StatusCodeStr => StatusCode != null ? StatusCode.ToString() : string.Empty;

        public override string ToString()
        {
            var serverError = Message;
            if (StatusCode != null)
                serverError = CommonTextUtil.LineSeparate(serverError, string.Format(ArdiaResources.Error_ResponseStatus, StatusCode));
            return serverError;
        }

        public static string GetIfErrorInResponse(string responseBody)
        {
            try
            {
                var jsonResponse = JObject.Parse(responseBody);
                if (jsonResponse?[@"title"] != null)
                {
                    return jsonResponse[@"title"].ToString();
                }
            }
            catch (JsonReaderException)
            {
                // ignore
            }

            return string.Empty;
        }

    }
}