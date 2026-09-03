/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Text;
using IdentityModel.Client;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Posts an OAuth 2.0 token request (RFC 6749) to an identity server and parses the result,
    /// shared by every account type that authenticates against one directly rather than through
    /// a UI-driven login (CommonMsData's WatersConnectAccount and UnifiAccount - both connect to
    /// a Waters-hosted identity server of the same design, one with a password grant and one with
    /// password+refresh) as well as the native C++/CLI UNIFI/WatersConnect vendor readers, which
    /// call this directly instead of duplicating the IdentityModel-7 request shape in C++/CLI.
    /// </summary>
    public static class OAuthPasswordGrantClient
    {
        /// <summary>
        /// Builds the RFC 6749 4.3 "resource owner password credentials" grant form - the same
        /// four fields for every caller here, so this is the one place that spells them out.
        /// </summary>
        public static NameValueCollection PasswordGrantForm(string username, string password, string scope)
        {
            return new NameValueCollection
            {
                [@"grant_type"] = @"password",
                [@"username"] = username,
                [@"password"] = password,
                [@"scope"] = scope
            };
        }

        /// <summary>
        /// POSTs <paramref name="form"/> to <paramref name="tokenEndpoint"/> with the client
        /// credentials in an HTTP Basic authorization header, and returns the parsed response.
        /// Every failure - protocol, HTTP, or transport - is returned as an error
        /// <see cref="TokenResponse"/> rather than thrown, so callers route all failures through
        /// a single path: a 400 is an OAuth protocol error whose JSON body carries
        /// error/error_description; any other HTTP failure becomes an HTTP-error response
        /// (IsError true even when the body is a proxy's HTML page); a transport or URL-format
        /// exception becomes an exception-type response.
        /// </summary>
        public static TokenResponse RequestToken(Uri tokenEndpoint, string clientId, string clientSecret, NameValueCollection form)
        {
            try
            {
                using var httpClient = new HttpClientWithProgress();
                httpClient.AddAuthorizationHeader(@"Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    EscapeClientCredential(clientId) + @":" + EscapeClientCredential(clientSecret))));
                httpClient.AddHeader(@"Accept", @"application/json");
                var raw = Encoding.UTF8.GetString(httpClient.UploadValues(tokenEndpoint, @"POST", form));
                return ParseTokenResponse(HttpStatusCode.OK, raw);
            }
            catch (NetworkRequestException ex)
            {
                // HttpClientWithProgress throws on every non-2xx, so the OAuth error body arrives
                // here rather than as a response. Hand the status and body to IdentityModel and let
                // it decide protocol-error vs HTTP-error instead of restating those rules.
                return ParseTokenResponse(ex.StatusCode ?? HttpStatusCode.ServiceUnavailable, ex.ResponseBody, ex.Message);
            }
            catch (Exception ex)
            {
                return ProtocolResponse.FromException<TokenResponse>(ex);
            }
        }

        /// <summary>
        /// Builds a <see cref="TokenResponse"/> from a status and body. IdentityModel 7 left
        /// <see cref="TokenResponse"/> with only a parameterless constructor;
        /// <see cref="ProtocolResponse.FromHttpResponseAsync{T}"/> replaced the ones this code was
        /// originally written against, and it takes an <see cref="HttpResponseMessage"/>.
        /// <see cref="HttpClientWithProgress"/> never yields one for a failure - it throws - so the
        /// failure path in <see cref="RequestToken"/> reconstitutes the response here.
        /// </summary>
        private static TokenResponse ParseTokenResponse(HttpStatusCode status, string body, string reason = null)
        {
            using var response = new HttpResponseMessage(status);
            response.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, @"application/json");
            if (reason != null)
                response.ReasonPhrase = reason;
            // The body is already in memory, so FromHttpResponseAsync's only await completes
            // synchronously and the returned task is ALREADY COMPLETE. There is no continuation to
            // post to the captured WinForms SynchronizationContext, so reading .Result cannot
            // deadlock and needs no Task.Run to escape it. That safety depends on the content being
            // in memory; do not turn this into an async chain.
            return ProtocolResponse.FromHttpResponseAsync<TokenResponse>(response).Result;
        }

        /// <summary>
        /// RFC 6749 section 2.3.1: client_id and client_secret are form-urlencoded before being
        /// combined into the Basic authorization credential.
        /// </summary>
        private static string EscapeClientCredential(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty).Replace(@"%20", @"+");
        }
    }
}
