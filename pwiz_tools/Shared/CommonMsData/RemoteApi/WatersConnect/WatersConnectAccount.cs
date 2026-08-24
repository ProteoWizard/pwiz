/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using System.Globalization;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using pwiz.Common;
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
        public static readonly string HANDLER_NAME = @"WatersConnect.Handler.Main";
        public static readonly string AUTH_HANDLER_NAME = @"WatersConnect.Handler.Authentication";
        public static readonly string TOKEN_DATA = @"token";
        public static readonly string GET_FOLDERS = @"/waters_connect/v2.0/folders";

        public class TokenCacheEntry
        {
            public TokenResponse TokenResponse { get; set; }
            public DateTime ExpirationDateTime { get; set; }
        }

        public static readonly Dictionary<WatersConnectAccount, TokenCacheEntry> _authenticationTokens = new Dictionary<WatersConnectAccount, TokenCacheEntry>();
        public static IHttpClientFactory _httpClientFactory;

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

        static WatersConnectAccount()
        {
            var services = new ServiceCollection();
            var builder = services.AddHttpClient(@"customClient");
            builder.ConfigurePrimaryHttpMessageHandler(() =>
                // Get mock handler for testing purposes.
                CommonApplicationSettings.HttpMessageHandlerFactory.getMessageHandler(
                    HANDLER_NAME,
#if NET472
                    () => new WebRequestHandler()
                        { UnsafeAuthenticatedConnectionSharing = true, PreAuthenticate = true }
#else
                    // WebRequestHandler is not available on net8. HttpClientHandler.PreAuthenticate
                    // is enough for the standard OAuth flow; the connection-sharing option is a
                    // System.Net legacy toggle unnecessary for our .NET 8 usage.
                    () => new HttpClientHandler { PreAuthenticate = true }
#endif
                )
            );
            var provider = services.BuildServiceProvider();
            _httpClientFactory = provider.GetService<IHttpClientFactory>();
        }

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
            // IdentityModel 7: TokenClient/TokenClientOptions were removed. Use the
            // HttpClient.RequestPasswordTokenAsync / RequestRefreshTokenAsync extensions with
            // PasswordTokenRequest / RefreshTokenRequest instead. Get mock handler for testing purposes.
            var authHandler = CommonApplicationSettings.HttpMessageHandlerFactory.getMessageHandler(AUTH_HANDLER_NAME, () => new HttpClientHandler());
            using var tokenClient = new HttpClient(authHandler, disposeHandler: false);
            var tokenEndpoint = IdentityServer + IdentityConnectEndpoint;
            // Try to refresh the token if we have an expired one
            if (_authenticationTokens.TryGetValue(this, out var expiredTokenCacheEntry))
            {
                // Offload the blocking token call to the thread pool. Authenticate() runs on the UI
                // thread (e.g. via SupportsMethodDevelopment) and blocking on the HTTP call's result
                // directly would deadlock the captured WinForms SynchronizationContext on net8.
                var refreshedToken = Task.Run(() => tokenClient.RequestRefreshTokenAsync(new RefreshTokenRequest
                {
                    Address = tokenEndpoint,
                    ClientId = ClientId,
                    ClientSecret = ClientSecret,
                    RefreshToken = expiredTokenCacheEntry.TokenResponse.RefreshToken
                })).Result;
                if (!refreshedToken.IsError)
                {
                    // If the refresh token worked, update the cache with the new token
                    _authenticationTokens[this] = new TokenCacheEntry()
                        { TokenResponse = refreshedToken, ExpirationDateTime = DateTime.UtcNow.AddSeconds(refreshedToken.ExpiresIn) };
                    return refreshedToken;
                }
            }
            // Otherwise, request a new token using the username and password
            var newToken = Task.Run(() => tokenClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = tokenEndpoint,
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                UserName = Username,
                Password = Password,
                Scope = ClientScope
            })).Result;
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
                string error = (tokenResponse[@"error_description"] ?? tokenResponse[@"error"] ?? "").ToString();
                var errorType = (tokenResponse[@"error"] ?? "").ToString();
                if (errorType == @"invalid_scope")
                {
                    // Surface the identity server's raw (non-localized) error detail so the user sees the
                    // rejected scope. On net472 IdentityModel left TokenResponse.Raw empty, so this went
                    // through the generic path below that showed the same detail; net8's IdentityModel 7
                    // populates Raw and reaches this branch, which otherwise left the message blank.
                    message = error;
                    return AuthenticationErrorType.InvalidClientScope;
                }
                else if (errorType == @"invalid_client")
                {
                    return AuthenticationErrorType.InvalidClientSecret;
                }
                else if (errorType == @"invalid_grant")
                {
                    // As with invalid_scope, surface the identity server's raw (non-localized) detail
                    // (e.g. "password entered for this user is incorrect") that net472 showed via the
                    // generic path before net8's IdentityModel 7 started populating TokenResponse.Raw.
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
                    return AuthenticationErrorType.InvalidIdentityServer;
                }
            }
            catch(Exception)
            {
                message = CommonTextUtil.LineSeparate(WatersConnectResources.WatersConnectAccount_HandleAuthenticationException_waters_connect_server_returned_non_JSON_body__, ex.Message);
                return AuthenticationErrorType.InvalidResponse;
            }
        }

        /*public IEnumerable<WatersConnectFolderObject> GetFolders()
        {
            var httpClient = GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(GetFoldersUrl()).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);

            var foldersValue = jsonObject[@"value"] as JArray;
            if (foldersValue == null)
            {
                return new WatersConnectFolderObject[0];
            }
            return foldersValue.OfType<JObject>().Select(f => new WatersConnectFolderObject(f));
        }

        public IEnumerable<WatersConnectFileObject> GetFiles(WatersConnectFolderObject folder)
        {
            var httpClient = GetAuthenticatedHttpClient();
            string url = string.Format(@"/waters_connect/v2.0/sample-sets?folderId={0}", folder.Id);
            var response = httpClient.GetAsync(ServerUrl + url).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);
            var itemsValue = jsonObject[@"value"] as JArray;
            if (itemsValue == null)
            {
                return new WatersConnectFileObject[0];
            }
            return itemsValue.OfType<JObject>().Select(f => new WatersConnectFileObject(f));
        }*/

        public HttpClient GetAuthenticatedHttpClient()
        {
            var tokenResponse = Authenticate();
            var httpClient = _httpClientFactory.CreateClient(@"customClient");
            httpClient.SetBearerToken(tokenResponse.AccessToken);
            //httpClient.DefaultRequestHeaders.Remove(@"Accept");
            //httpClient.DefaultRequestHeaders.Add(@"Accept", @"application/json");
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
