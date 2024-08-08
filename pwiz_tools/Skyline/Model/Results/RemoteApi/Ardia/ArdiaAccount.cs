/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
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
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Ardia
{
    [XmlRoot("ardia_account")]
    public class ArdiaAccount : RemoteAccount
    {
        public static readonly ArdiaAccount DEFAULT = new ArdiaAccount(string.Empty, string.Empty, string.Empty);

        public bool DeleteRawAfterImport { get; private set; }
        // BffHostCookie_PersistedButNeverSet is never set in a way that gets out of the ArdiaLoginDlg
        public string BffHostCookie_PersistedButNeverSet { get; private set; }
        public string BffHostCookie_NotPersisted { get; set; }

        //  Following 'TestingOnly...' properties are for only supporting the automated tests in class ArdiaTest
        public string TestingOnly_NotSerialized_Role { get; private set; }
        public string TestingOnly_NotSerialized_Username { get; private set; }
        public string TestingOnly_NotSerialized_Password { get; private set; }


        public ArdiaAccount(string serverUrl, string username, string password)
        {
            ServerUrl = serverUrl;
            Username = username;
            Password = password;
        }

        public string GetFolderContentsUrl(ArdiaUrl ardiaUrl)
        {
            if (ardiaUrl.SequenceKey != null)
                return ardiaUrl.SequenceUrl;
            else if (ardiaUrl.EncodedPath != null)
                return GetRootArdiaUrl().NavigationBaseUrl + $@"/path?itemPath=/{ardiaUrl.EncodedPath}";
            return GetRootArdiaUrl().NavigationBaseUrl;
        }

        public string GetFolderContentsUrl(string folder = "")
        {
            return GetRootArdiaUrl().NavigationBaseUrl + ((folder?.TrimStart('/')).IsNullOrEmpty() ? "" : $@"/path?itemPath={folder}");
        }

        public string GetPathFromFolderContentsUrl(string folderUrl)
        {
            var rootUrl = GetRootArdiaUrl();
            return folderUrl.Replace(rootUrl.NavigationBaseUrl, "").Replace(rootUrl.ServerUrl, "").Replace(@"/path?itemPath=", "").TrimEnd('/');
        }

        private enum ATTR
        {
            delete_after_import
    }

        protected override void ReadXElement(XElement xElement)
        {
            base.ReadXElement(xElement);
            DeleteRawAfterImport = Convert.ToBoolean((string) xElement.Attribute(ATTR.delete_after_import.ToString()));
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.delete_after_import, DeleteRawAfterImport);
        }

        private Func<HttpClient> _authenticatedHttpClientFactory;

        public void copy_authenticatedHttpClientFactory(ArdiaAccount ardiaAccount)
        {
            _authenticatedHttpClientFactory = ardiaAccount._authenticatedHttpClientFactory;
        }

        public bool authenticatedHttpClientFactoryIsPopulated()
        {
            if (_authenticatedHttpClientFactory != null)
            {
                return true;
            }

            return false;
        }

        public HttpClient GetAuthenticatedHttpClient()
        {
            if (_authenticatedHttpClientFactory != null)
            {
                // Check that the factory still makes a valid client
                using var client = _authenticatedHttpClientFactory();
                try
                {
                    CheckAuthentication(client);
                }
                catch (Exception)
                {
                    _authenticatedHttpClientFactory = null;
                }
            }

            if (_authenticatedHttpClientFactory == null)
            {
                // If RemoteAccountUserInteraction is null, some top level interface didn't set it when it should have
                Assume.IsNotNull(RemoteSession.RemoteAccountUserInteraction, @"RemoteSession.UserInteraction is not set");
                _authenticatedHttpClientFactory = RemoteSession.RemoteAccountUserInteraction.UserLogin(this);
            }

            if (_authenticatedHttpClientFactory == null)
            {
                throw new Exception("Get Ardia Connection Failed");
                // return null;
            }

            return _authenticatedHttpClientFactory();
        }

        private void CheckAuthentication(HttpClient httpClient)
        {
            var response = httpClient.GetAsync(GetFolderContentsUrl()).Result;
            response.EnsureSuccessStatusCode();
        }

        // testing only
        public void ResetAuthenticatedHttpClientFactory()
        {
            _authenticatedHttpClientFactory = null;
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.ARDIA; }
        }

        public override RemoteSession CreateSession()
        {
            return new ArdiaSession(this);
        }


        public ArdiaAccount ChangeDeleteRawAfterImport(bool deleteAfterImport)
        {
            var result = ChangeProp(ImClone(this), im => im.DeleteRawAfterImport = deleteAfterImport);
            result._authenticatedHttpClientFactory = _authenticatedHttpClientFactory;
            return result;
        }

        // BffHostCookie_PersistedButNeverSet is never set in a way that gets out of the ArdiaLoginDlg
        // public ArdiaAccount ChangeBffHostCookie(string bffHostCookie)
        // {
        //     var result = ChangeProp(ImClone(this), im => im.BffHostCookie_PersistedButNeverSet = bffHostCookie);
        //     result._authenticatedHttpClientFactory = _authenticatedHttpClientFactory;
        //     return result;
        // }

        public ArdiaAccount ChangeTestingOnly_NotSerialized_Role(string role)
        {
            var result = ChangeProp(ImClone(this), im => im.TestingOnly_NotSerialized_Role = role);
            result._authenticatedHttpClientFactory = _authenticatedHttpClientFactory;
            return result;
        }
        public ArdiaAccount ChangeTestingOnly_NotSerialized_Username(string username)
        {
            var result = ChangeProp(ImClone(this), im => im.TestingOnly_NotSerialized_Username = username);
            result._authenticatedHttpClientFactory = _authenticatedHttpClientFactory;
            return result;
        }
        public ArdiaAccount ChangeTestingOnly_NotSerialized_Password(string password)
        {
            var result = ChangeProp(ImClone(this), im => im.TestingOnly_NotSerialized_Password= password);
            result._authenticatedHttpClientFactory = _authenticatedHttpClientFactory;
            return result;
        }

        public ArdiaUrl GetRootArdiaUrl()
        {
            return (ArdiaUrl) ArdiaUrl.Empty.ChangeServerUrl(ServerUrl).ChangeUsername(Username); //  Copy along the Username value for code that matches the username works
        }

        public override RemoteUrl GetRootUrl()
        {
            return GetRootArdiaUrl();
        }

        private ArdiaAccount()
        {
        }
        public static ArdiaAccount Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ArdiaAccount());
        }

        protected bool Equals(ArdiaAccount other)
        {
            return base.Equals(other) &&
                   Equals(TestingOnly_NotSerialized_Role, other.TestingOnly_NotSerialized_Role) &&
                   Equals(TestingOnly_NotSerialized_Username, other.TestingOnly_NotSerialized_Username) &&
                   Equals(TestingOnly_NotSerialized_Password, other.TestingOnly_NotSerialized_Password) &&
                   DeleteRawAfterImport == other.DeleteRawAfterImport
                   // &&
                   // Equals(BffHostCookie_PersistedButNeverSet, other.BffHostCookie_PersistedButNeverSet)
                   ;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (TestingOnly_NotSerialized_Role?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (TestingOnly_NotSerialized_Username?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (TestingOnly_NotSerialized_Password?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ DeleteRawAfterImport.GetHashCode();
                // hashCode = (hashCode * 397) ^ (BffHostCookie_PersistedButNeverSet?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}
