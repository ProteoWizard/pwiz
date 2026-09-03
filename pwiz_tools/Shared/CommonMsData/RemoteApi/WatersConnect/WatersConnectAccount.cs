/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Fable 5) <noreply .at. anthropic.com>
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using IdentityModel.Client;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public enum AuthenticationErrorType
    {
        InvalidIdentityServer,
        InvalidClientScope,
        InvalidClientSecret,
        InvalidPassword,
        Generic,
        InvalidResponse
    }

    public static class AuthenticationErrorTypeExtension
    {
        public static string ToUserMessage(this AuthenticationErrorType errorType)
        {
            switch (errorType)
            {
                case AuthenticationErrorType.InvalidIdentityServer:
                    return WatersConnectResources.WatersConnectAccount_AuthenticationErrorType_InvalidIdentityServer;
                case AuthenticationErrorType.InvalidClientScope:
                    return WatersConnectResources.WatersConnectAccount_AuthenticationErrorType_InvalidClientScope;
                case AuthenticationErrorType.InvalidClientSecret:
                    return WatersConnectResources.WatersConnectAccount_AuthenticationErrorType_InvalidClientSecret;
                case AuthenticationErrorType.InvalidPassword:
                    return WatersConnectResources.WatersConnectAccount_AuthenticationErrorType_InvalidPassword;
                case AuthenticationErrorType.InvalidResponse:
                    return WatersConnectResources.WatersConnectAccount_AuthenticationErrorType_InvalidServerResponse;
                case AuthenticationErrorType.Generic:
                default:
                    return WatersConnectResources.WatersConnectAccount_AuthenticationErrorType_Generic;
            }
        }
    }

    [XmlRoot("waters_connect_account")]
    public class WatersConnectAccount : RemoteAccount
    {
        public static readonly string TOKEN_DATA = @"token";
        public static readonly string GET_FOLDERS = @"/waters_connect/v2.0/folders";

        public class TokenCacheEntry
        {
            public TokenResponse TokenResponse { get; set; }
            public DateTime ExpirationDateTime { get; set; }
        }

        public static readonly Dictionary<WatersConnectAccount, TokenCacheEntry> _authenticationTokens = new Dictionary<WatersConnectAccount, TokenCacheEntry>();

        public static readonly WatersConnectAccount DEFAULT
            = new WatersConnectAccount(@"https://localhost:48444", string.Empty, string.Empty)
            {
                IdentityServer = @"https://localhost:48333",
                ClientScope = @"webapi",
                ClientSecret = @"method-develop-secret",
                ClientId = @"method-develop"
            };
        public static readonly WatersConnectAccount DEV_DEFAULT
            = new WatersConnectAccount(@"https://devconnect.waters.com:48444", string.Empty, string.Empty)
            {
                IdentityServer = @"https://devconnect.waters.com:48333",
                ClientScope = @"webapi",
                ClientSecret = @"secret",
                ClientId = @"resourceownerclient_jwt"
            };

        public WatersConnectAccount(string serverUrl, string username, string password)
        {
            ServerUrl = serverUrl;
            Username = username;
            Password = password;

            string strPort = @":48333";
            int ichLastColon = ServerUrl.LastIndexOf(':');
            if (ichLastColon == ServerUrl.IndexOf(':'))
            {
                IdentityServer = ServerUrl + strPort;
            }
            else
            {
                IdentityServer = ServerUrl.Substring(0, ichLastColon) + strPort;
            }
        }

        public string IdentityServer { get; private set; }

        public bool SupportsMethodDevelopment(out string reason)
        {
            reason = null;
            if (!DEFAULT.ClientId.Equals(ClientId))
            {
                reason = WatersConnectResources
                    .WatersConnectAccount_SupportsMethodDevelopment_Not_supported_by_the_waters_connect_server_;
                return false;
            }
            try
            {
                Authenticate();
                return true;
            }
            catch (AuthenticationException ex)
            {
                var authReason = HandleAuthenticationException(ex, out _);
                reason = WatersConnectResources.WatersConnectAccount_SupportsMethodDevelopment_Cannot_authenticate__ + authReason.ToUserMessage();
                return false;
            }
        }

        public WatersConnectAccount ChangeIdentityServer(string identityServer)
        {
            return ChangeProp(ImClone(this), im => im.IdentityServer = identityServer);
        }
        public string ClientScope { get; private set; }

        public WatersConnectAccount ChangeClientScope(string clientScope)
        {
            return ChangeProp(ImClone(this), im => im.ClientScope = clientScope);
        }
        public string ClientSecret { get; private set; }

        public WatersConnectAccount ChangeClientSecret(string clientSecret)
        {
            return ChangeProp(ImClone(this), im => im.ClientSecret = clientSecret);
        }
        public string ClientId { get; private set; }

        public WatersConnectAccount ChangeClientId(string clientId)
        {
            return ChangeProp(ImClone(this), im => im.ClientId = clientId);
        }

        private enum ATTR
        {
            identity_server,
            client_scope,
            client_secret,
            client_id
        }

        protected override void ReadXElement(XElement xElement)
        {
            base.ReadXElement(xElement);
            IdentityServer = (string) xElement.Attribute(ATTR.identity_server.ToString());
            ClientScope = (string) xElement.Attribute(ATTR.client_scope.ToString());
            ClientId = (string)xElement.Attribute(ATTR.client_id.ToString()) ?? DEFAULT.ClientId;
            string clientSecret = (string) xElement.Attribute(ATTR.client_secret.ToString());
            if (clientSecret != null)
            {
                ClientSecret = CommonTextUtil.DecryptString(clientSecret);
            }
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttributeIfString2(ATTR.identity_server, IdentityServer);
            writer.WriteAttributeIfString2(ATTR.client_scope, ClientScope);
            writer.WriteAttributeIfString2(ATTR.client_id, ClientId);
            if (ClientSecret != null)
            {
                writer.WriteAttributeIfString2(ATTR.client_secret, CommonTextUtil.EncryptString(ClientSecret));
            }
        }

        public string GetFoldersUrl()
        {
            return ServerUrl + GET_FOLDERS;
        }

        private string IdentityConnectEndpoint => @"/connect/token";

        public TokenResponse Authenticate()
        {
            // First check the cache for a valid token
            if (_authenticationTokens.TryGetValue(this, out var tokenCacheEntry) && tokenCacheEntry.ExpirationDateTime > DateTime.UtcNow)
            {
                return tokenCacheEntry.TokenResponse;
            }
            // Try to refresh the token if we have an expired one
            if (_authenticationTokens.TryGetValue(this, out var expiredTokenCacheEntry))
            {
                var refreshedToken = RequestToken(new NameValueCollection
                {
                    [@"grant_type"] = @"refresh_token",
                    [@"refresh_token"] = expiredTokenCacheEntry.TokenResponse.RefreshToken
                });
                if (!refreshedToken.IsError)
                {
                    // If the refresh token worked, update the cache with the new token
                    _authenticationTokens[this] = new TokenCacheEntry()
                        { TokenResponse = refreshedToken, ExpirationDateTime = DateTime.UtcNow.AddSeconds(refreshedToken.ExpiresIn) };
                    return refreshedToken;
                }
            }
            // Otherwise, request a new token using the username and password
            var newToken = RequestToken(OAuthPasswordGrantClient.PasswordGrantForm(Username, Password, ClientScope));
            if (newToken.IsError)
            {
                AuthenticationException ex;
                if (newToken.ErrorType == ResponseErrorType.Exception)
                    ex = new AuthenticationException(newToken.Error);
                else
                    ex = new AuthenticationException(string.Format(CultureInfo.CurrentCulture,
                        WatersConnectResources.WatersConnectAccount_Authenticate_Failed_to_authenticate_waters_connect_account__0__with_error___1_,
                        Username, newToken.ErrorDescription ?? newToken.Error));
                ex.Data[TOKEN_DATA] = newToken.Raw;
                throw ex;
            }
            _authenticationTokens[this] = new TokenCacheEntry()
                { TokenResponse = newToken, ExpirationDateTime = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn) };
            return newToken;
        }

        /// <summary>
        /// POSTs a token request to the identity server and returns the parsed response. Client
        /// credentials go in an HTTP Basic authorization header with each half URL-escaped per
        /// RFC 6749 section 2.3.1, matching the wire format of the IdentityModel TokenClient this
        /// replaced. Every failure is returned as an error <see cref="TokenResponse"/> - the same
        /// contract TokenClient had - so callers route all failures through the
        /// authentication-error path: a 400 is an OAuth protocol error whose JSON body carries
        /// error/error_description; any other HTTP failure becomes an HTTP-error response (IsError
        /// true even when the body is a proxy's HTML page); a transport or URL-format exception
        /// becomes an exception-type response.
        /// </summary>
        private TokenResponse RequestToken(NameValueCollection form)
        {
            // Shared with UnifiAccount.Authenticate, which authenticates against a sibling
            // Waters-hosted identity server the same way - see OAuthPasswordGrantClient for the
            // POST, response parsing, and why both needed to stop constructing TokenResponse
            // directly once IdentityModel 7 removed its constructors.
            return OAuthPasswordGrantClient.RequestToken(new Uri(IdentityServer + IdentityConnectEndpoint), ClientId, ClientSecret, form);
        }

        public static AuthenticationErrorType HandleAuthenticationException(AuthenticationException ex, out string message)
        {
            message = null;
            if (!ex.Data.Contains(TOKEN_DATA) || string.IsNullOrEmpty(ex.Data[TOKEN_DATA] as string))
            {
                message = CommonTextUtil.LineSeparate(WatersConnectResources.WatersConnectAccount_HandleAuthenticationException_waters_connect_server_returned_non_JSON_body__, ex.Message);
                return AuthenticationErrorType.Generic;
            }
            try
            {
                var tokenResponse = JObject.Parse((string)ex.Data[TOKEN_DATA]);
                // error_description is frequently empty (e.g. Waters' invalid_scope response is just
                // {"error":"invalid_scope"}), so fall back to the bare error code rather than leaving
                // the caller with nothing to show - every classified branch below sets message for the
                // same reason. Only EditRemoteAccountDlg's InvalidClientSecret case overrides this with
                // a friendlier string; the others show this raw (deliberately non-L10N) server text.
                string error = (tokenResponse[@"error_description"] ?? tokenResponse[@"error"] ?? "").ToString();
                var errorType = (tokenResponse[@"error"] ?? "").ToString();
                if (errorType == @"invalid_scope")
                {
                    message = error;
                    return AuthenticationErrorType.InvalidClientScope;
                }
                else if (errorType == @"invalid_client")
                {
                    message = error;
                    return AuthenticationErrorType.InvalidClientSecret;
                }
                else if (errorType == @"invalid_grant")
                {
                    message = error;
                    return AuthenticationErrorType.InvalidPassword;
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    message = error;
                    return AuthenticationErrorType.Generic;
                }
                else
                {
                    message = ex.Message;
                    return AuthenticationErrorType.InvalidIdentityServer;
                }
            }
            catch(Exception)
            {
                message = CommonTextUtil.LineSeparate(WatersConnectResources.WatersConnectAccount_HandleAuthenticationException_waters_connect_server_returned_non_JSON_body__, ex.Message);
                return AuthenticationErrorType.InvalidResponse;
            }
        }

        /// <summary>
        /// Creates an <see cref="HttpClientWithProgress"/> carrying this account's bearer token,
        /// authenticating first if no valid token is cached. Waits are bounded by
        /// <see cref="HttpClientWithProgress.ResponseTimeoutMilliseconds"/>, which replaced the
        /// 100-second default the removed IHttpClientFactory clients had, so a black-holed
        /// connection surfaces as a timeout instead of hanging the UI thread indefinitely.
        /// </summary>
        public HttpClientWithProgress CreateAuthenticatedClient()
        {
            var tokenResponse = Authenticate();
            var httpClient = new HttpClientWithProgress();
            httpClient.AddAuthorizationHeader(@"Bearer " + tokenResponse.AccessToken);
            return httpClient;
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.WATERS_CONNECT; }
        }

        public override RemoteSession CreateSession()
        {
            return new WatersConnectSession(this);
        }

        public override RemoteUrl GetRootUrl()
        {
            return WatersConnectUrl.Empty.ChangeServerUrl(ServerUrl).ChangeUsername(Username);
        }

        private WatersConnectAccount()
        {
        }
        public static WatersConnectAccount Deserialize(XmlReader reader)
        {
            var objNew = new WatersConnectAccount();
            objNew.ReadXml(reader);
            return objNew;
        }

        protected bool Equals(WatersConnectAccount other)
        {
            return base.Equals(other) && string.Equals(IdentityServer, other.IdentityServer) &&
                   string.Equals(ClientScope, other.ClientScope) && string.Equals(ClientSecret, other.ClientSecret) &&
                   string.Equals(ClientId, other.ClientId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((WatersConnectAccount) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (IdentityServer != null ? IdentityServer.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ClientScope != null ? ClientScope.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ClientSecret != null ? ClientSecret.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ClientId != null ? ClientId.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
