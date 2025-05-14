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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Ardia
{
    /// <summary>
    /// Wiki docs about configuring Ardia accounts in Skyline:
    ///     https://skyline.ms/wiki/home/software/Skyline/page.view?name=Ardia%20setup%20and%20importing%20a%20file%20from%20Ardia
    ///
    /// Once added, the account is listed under Tools => Options => Remote Accounts and can be accessed by Skyline developers using
    /// <see cref="Properties.Settings.RemoteAccountList"/>.
    ///
    /// When Skyline closes, configuration info about the account is stored in Skyline's user.config file whose path
    /// can be found under Tools => Options => Miscellaneous and is stored in a directory like:
    /// 
    ///     C:\Users\%USERNAME%\AppData\Local\University_of_Washington\Skyline-daily.exe_Url_db2lbzhuk4iiqiyc522okkxewhxy1qsq\24.1.1.493\user.config
    /// 
    /// </summary>
    [XmlRoot("ardia_account")]
    public class ArdiaAccount : RemoteAccount
    {
        private static IDictionary<(string, string), string> _sessionCookieStrings = new ConcurrentDictionary<(string, string), string>();

        private static (string, string) MakeKey(ArdiaAccount ardiaAccount)
        {
            return (ardiaAccount.ServerUrl, ardiaAccount.Username);
        }

        public static string GetSessionCookieString(ArdiaAccount ardiaAccount)
        {
            var key = MakeKey(ardiaAccount);
            _sessionCookieStrings.TryGetValue(key, out var sessionCookieString);
            return sessionCookieString;
        }

        public static void SetSessionCookieString(ArdiaAccount ardiaAccount, string sessionCookieString)
        {
            var key = MakeKey(ardiaAccount);
            _sessionCookieStrings[key] = sessionCookieString;
        }

        public static void ClearSessionCookieStrings()
        {
            _sessionCookieStrings.Clear();
        }

        // Use only in tests?
        public static readonly ArdiaAccount DEFAULT = new ArdiaAccount(string.Empty, string.Empty, string.Empty);

        public bool DeleteRawAfterImport { get; private set; }
        
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

        public void SetAuthenticatedHttpClientFactory(ArdiaAccount ardiaAccount)
        {
            _authenticatedHttpClientFactory = ardiaAccount._authenticatedHttpClientFactory;
        }

        public bool HasAuthenticatedHttpClientFactory()
        {
            return _authenticatedHttpClientFactory != null;
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
                throw new Exception(ArdiaResources.ArdiaAccount_GetAuthenticatedHttpClient_Failed_to_open_Ardia_connection);
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

        public override RemoteAccountType AccountType => RemoteAccountType.ARDIA;

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
                   DeleteRawAfterImport == other.DeleteRawAfterImport;
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
                return hashCode;
            }
        }
    }
}
