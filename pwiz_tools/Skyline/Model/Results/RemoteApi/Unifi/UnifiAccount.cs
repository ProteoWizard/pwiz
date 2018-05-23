/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using IdentityModel.Client;
using Newtonsoft.Json.Linq;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    [XmlRoot("unify_account")]
    public class UnifiAccount : RemoteAccount
    {
        public static readonly UnifiAccount DEFAULT 
            = new UnifiAccount("https://unifiapi.waters.com:50034", string.Empty, string.Empty) // Not L10N
        {
            IdentityServer = "https://unifiapi.waters.com:50333" // Not L10N
        };
        public UnifiAccount(string serverUrl, string username, string password)
        {
            ServerUrl = serverUrl;
            Username = username;
            Password = password;
            string strPort = ":50333"; // Not L10N
            int ichLastColon = ServerUrl.LastIndexOf(':');
            if (ichLastColon == ServerUrl.IndexOf(':'))
            {
                IdentityServer = ServerUrl + strPort;
            }
            else
            {
                IdentityServer = ServerUrl.Substring(0, ichLastColon) + strPort;
            }
            ClientScope = "unifi"; // Not L10N
            ClientSecret = "secret"; // Not L10N
        }

        public string IdentityServer { get; private set; }

        public UnifiAccount ChangeIdentityServer(string identityServer)
        {
            return ChangeProp(ImClone(this), im => im.IdentityServer = identityServer);
        }
        public string ClientScope { get; private set; }

        public UnifiAccount ChangeClientScope(string clientScope)
        {
            return ChangeProp(ImClone(this), im => im.ClientScope = clientScope);
        }
        public string ClientSecret { get; private set; }

        public UnifiAccount ChangeClientSecret(string clientSecret)
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
                ClientSecret = TextUtil.DecryptString(clientSecret);
            }
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttributeIfString(ATTR.identity_server, IdentityServer);
            writer.WriteAttributeIfString(ATTR.client_scope, ClientScope);
            if (ClientSecret != null)
            {
                writer.WriteAttributeIfString(ATTR.client_secret, TextUtil.EncryptString(ClientSecret));
            }
        }

        public string GetFoldersUrl()
        {
            return ServerUrl + "/unifi/v1/folders"; // Not L10N
        }

        public TokenResponse Authenticate()
        {
            var tokenClient = new TokenClient(IdentityServer + "/identity/connect/token", "resourceownerclient", // Not L10N
                ClientSecret, new HttpClientHandler());
            return tokenClient.RequestResourceOwnerPasswordAsync(Username, Password, ClientScope).Result;
        }

        public IEnumerable<UnifiFolderObject> GetFolders()
        {
            var httpClient = GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(GetFoldersUrl()).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);

            var foldersValue = jsonObject["value"] as JArray; // Not L10N
            if (foldersValue == null)
            {
                return new UnifiFolderObject[0];
            }
            return foldersValue.OfType<JObject>().Select(f => new UnifiFolderObject(f));
        }

        public IEnumerable<UnifiFileObject> GetFiles(UnifiFolderObject folder)
        {
            var httpClient = GetAuthenticatedHttpClient();
            string url = string.Format("/unifi/v1/folders({0})/items", folder.Id); // Not L10N
            var response = httpClient.GetAsync(ServerUrl + url).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);
            var itemsValue = jsonObject["value"] as JArray; // Not L10N
            if (itemsValue == null)
            {
                return new UnifiFileObject[0];
            }
            return itemsValue.OfType<JObject>().Select(f => new UnifiFileObject(f));
        }

        public HttpClient GetAuthenticatedHttpClient()
        {
            var tokenResponse = Authenticate();
            var httpClient = new HttpClient();
            httpClient.SetBearerToken(tokenResponse.AccessToken);
            httpClient.DefaultRequestHeaders.Remove("Accept"); // Not L10N
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json;odata.metadata=minimal"); // Not L10N
            return httpClient;
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.UNIFI; }
        }

        public override RemoteSession CreateSession()
        {
            return new UnifiSession(this);
        }

        public override RemoteUrl GetRootUrl()
        {
            return UnifiUrl.Empty.ChangeServerUrl(ServerUrl).ChangeUsername(Username);
        }

        private UnifiAccount()
        {
        }
        public static UnifiAccount Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new UnifiAccount());
        }

        protected bool Equals(UnifiAccount other)
        {
            return base.Equals(other) && string.Equals(IdentityServer, other.IdentityServer) &&
                   string.Equals(ClientScope, other.ClientScope) && string.Equals(ClientSecret, other.ClientSecret);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((UnifiAccount) obj);
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
