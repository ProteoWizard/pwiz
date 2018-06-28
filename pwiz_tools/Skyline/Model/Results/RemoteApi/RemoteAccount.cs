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
using System;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public abstract class RemoteAccount : Immutable, IKeyContainer<string>, IXmlSerializable
    {
        private enum ATTR
        {
            username,
            password,
            server_url
        }

        public abstract RemoteSession CreateSession();
        public abstract RemoteUrl GetRootUrl();

        public bool CanHandleUrl(RemoteUrl remoteUrl)
        {
            if (remoteUrl.AccountType != AccountType)
            {
                return false;
            }
            return Username == remoteUrl.Username;
        }
        public abstract RemoteAccountType AccountType { get; }
        public string ServerUrl { get; protected set; }

        public RemoteAccount ChangeServerUrl(string serverUrl)
        {
            return ChangeProp(ImClone(this), im => im.ServerUrl = serverUrl);
        }
        public string Username { get; protected set; }

        public RemoteAccount ChangeUsername(string username)
        {
            return ChangeProp(ImClone(this), im => im.Username = username);
        }
        public string Password { get; protected set; }

        public RemoteAccount ChangePassword(string password)
        {
            return ChangeProp(ImClone(this), im => im.Password = password);
        }

        public string GetKey()
        {
            var accountType = AccountType;
            string prefix = accountType.Name + ":"; // Not L10N
            if (ServerUrl == accountType.GetEmptyUrl().ServerUrl)
            {
                return prefix + Username;
            }
            return prefix + Username + "@" + ServerUrl; // Not L10N
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            var xElement = (XElement) XNode.ReadFrom(reader);
            ReadXElement(xElement);
        }

        protected virtual void ReadXElement(XElement xElement)
        {
            Username = (string) xElement.Attribute(ATTR.username.ToString()) ?? string.Empty;
            string encryptedPassword = (string) xElement.Attribute(ATTR.password.ToString());
            if (encryptedPassword != null)
            {
                try
                {
                    Password = TextUtil.DecryptString(encryptedPassword);
                }
                catch (Exception)
                {
                    Password = string.Empty;
                }
            }

            ServerUrl = (string) xElement.Attribute(ATTR.server_url.ToString());
        }

        public virtual void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeIfString(ATTR.server_url, ServerUrl);
            writer.WriteAttributeIfString(ATTR.username, Username);
            if (!string.IsNullOrEmpty(Password))
            {
                writer.WriteAttributeString(ATTR.password, TextUtil.EncryptString(Password));
            }
        }

        protected bool Equals(RemoteAccount other)
        {
            return string.Equals(ServerUrl, other.ServerUrl) && string.Equals(Username, other.Username) && string.Equals(Password, other.Password);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RemoteAccount) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (ServerUrl != null ? ServerUrl.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Username != null ? Username.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Password != null ? Password.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
