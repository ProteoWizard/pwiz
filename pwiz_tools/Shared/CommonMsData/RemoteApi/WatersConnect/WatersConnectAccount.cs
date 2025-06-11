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
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    [XmlRoot("waters_connect_account")]
    public class WatersConnectAccount : RemoteAccount
    {
        public static readonly WatersConnectAccount DEFAULT
            = new WatersConnectAccount(@"https://devconnect.waters.com:48444", string.Empty, string.Empty)
            {
                IdentityServer = @"https://devconnect.waters.com:48333"
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
            ClientScope = @"webapi";
            ClientSecret = @"secret";
        }

        public string IdentityServer { get; private set; }

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

        private enum ATTR
        {
            identity_server,
            client_scope,
            client_secret,
        }

        protected override void ReadXElement(XElement xElement)
        {
            base.ReadXElement(xElement);
            IdentityServer = (string) xElement.Attribute(ATTR.identity_server.ToString());
            ClientScope = (string) xElement.Attribute(ATTR.client_scope.ToString());
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
            if (ClientSecret != null)
            {
                writer.WriteAttributeIfString2(ATTR.client_secret, CommonTextUtil.EncryptString(ClientSecret));
            }
        }

        public string GetFoldersUrl()
        {
            return ServerUrl + @"/waters_connect/v1.0/folders";
        }

        private string IdentityConnectEndpoint => @"/connect/token";

        public TokenResponse Authenticate()
        {
            var tokenClient = new TokenClient(IdentityServer + IdentityConnectEndpoint, @"resourceownerclient_jwt",
                ClientSecret, new HttpClientHandler());
            return tokenClient.RequestResourceOwnerPasswordAsync(Username, Password, ClientScope).Result;
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

            var services = new ServiceCollection();
            var builder = services.AddHttpClient("customClient");
            builder.ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new WebRequestHandler();
                handler.UnsafeAuthenticatedConnectionSharing = true;
                handler.PreAuthenticate = true;
                return handler;
            });
            var provider = services.BuildServiceProvider();
            var httpClientFactory = provider.GetService<IHttpClientFactory>();

            var httpClient = httpClientFactory.CreateClient();
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
                   string.Equals(ClientScope, other.ClientScope) && string.Equals(ClientSecret, other.ClientSecret);
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
                return hashCode;
            }
        }
    }
}
