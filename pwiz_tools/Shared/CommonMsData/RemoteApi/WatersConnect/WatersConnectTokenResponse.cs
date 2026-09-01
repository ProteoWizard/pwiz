/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Newtonsoft.Json;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    /// <summary>
    /// How a token request failed, when it did.
    /// </summary>
    public enum TokenErrorType
    {
        None,
        /// <summary>The identity server answered with an OAuth error body.</summary>
        Protocol,
        /// <summary>The server answered, but not with a usable token response.</summary>
        Http,
        /// <summary>The request never produced a response at all.</summary>
        Exception
    }

    /// <summary>
    /// The identity server's answer to a token request, parsed from its JSON body.
    /// <para>waters_connect makes its own token request through
    /// <see cref="pwiz.Common.SystemUtil.HttpClientWithProgress"/> and classifies its own failures,
    /// so the only thing IdentityModel was still providing here was this shape. Owning it keeps
    /// this code compiling the same way whichever IdentityModel version is referenced - 3.9 and 7
    /// disagree about whether the equivalent type can be constructed at all - and follows the same
    /// pattern as the Ardia response types alongside it.</para>
    /// </summary>
    public class WatersConnectTokenResponse
    {
        private WatersConnectTokenResponse() { }

        [JsonProperty(@"access_token")]
        public string AccessToken { get; private set; }

        [JsonProperty(@"refresh_token")]
        public string RefreshToken { get; private set; }

        [JsonProperty(@"expires_in")]
        public int ExpiresIn { get; private set; }

        [JsonProperty(@"error")]
        public string Error { get; private set; }

        [JsonProperty(@"error_description")]
        public string ErrorDescription { get; private set; }

        /// <summary>
        /// The response body verbatim. <see cref="WatersConnectAccount.HandleAuthenticationException"/>
        /// re-parses this to tell invalid_scope, invalid_client and invalid_grant apart, so it must
        /// stay exactly what the server sent.
        /// </summary>
        [JsonIgnore]
        public string Raw { get; private set; }

        [JsonIgnore]
        public TokenErrorType ErrorType { get; private set; }

        [JsonIgnore]
        public bool IsError => ErrorType != TokenErrorType.None;

        /// <summary>
        /// Parses a token response body. A body that is not JSON, or that carries no access token,
        /// is an error rather than an exception - the caller routes every failure through the
        /// authentication-error path, and a proxy answering 200 with an HTML page must not throw
        /// past it.
        /// </summary>
        public static WatersConnectTokenResponse FromJson(string json)
        {
            WatersConnectTokenResponse response;
            try
            {
                response = JsonConvert.DeserializeObject<WatersConnectTokenResponse>(json)
                           ?? new WatersConnectTokenResponse();
            }
            catch (Exception e)
            {
                return new WatersConnectTokenResponse
                {
                    Raw = json, ErrorType = TokenErrorType.Exception, Error = e.Message
                };
            }

            response.Raw = json;
            if (!string.IsNullOrEmpty(response.Error))
                response.ErrorType = TokenErrorType.Protocol;
            else if (string.IsNullOrEmpty(response.AccessToken))
                response.ErrorType = TokenErrorType.Http;
            return response;
        }

        /// <summary>
        /// An HTTP failure carrying no usable OAuth error body. <paramref name="body"/> is kept as
        /// <see cref="Raw"/> even when it is a proxy's HTML, so the caller can show it.
        /// </summary>
        public static WatersConnectTokenResponse FromHttpError(HttpStatusCode statusCode, string reason, string body)
        {
            return new WatersConnectTokenResponse
            {
                Raw = body,
                ErrorType = TokenErrorType.Http,
                Error = statusCode.ToString(),
                ErrorDescription = reason
            };
        }

        /// <summary>
        /// A request that never reached the server, or whose failure carried no response at all.
        /// </summary>
        public static WatersConnectTokenResponse FromException(Exception exception)
        {
            return new WatersConnectTokenResponse
            {
                ErrorType = TokenErrorType.Exception,
                Error = exception.Message
            };
        }
    }
}
