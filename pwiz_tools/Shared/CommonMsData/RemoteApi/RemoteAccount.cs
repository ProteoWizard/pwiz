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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi
{
    public abstract class RemoteAccount : Immutable, IKeyContainer<string>, IXmlSerializable
    {
        private enum ATTR
        {
            username,
            password,
            server_url,
            alias
        }

        public abstract RemoteSession CreateSession();
        public abstract RemoteUrl GetRootUrl();

        private string _accountAlias;
        public string AccountAlias {
            get
            {
                if (string.IsNullOrEmpty(_accountAlias))
                    return ServerUrl;
                return _accountAlias;
            }
            set
            {
                _accountAlias = value;
            }
        } 
        public bool HasAlias => !string.IsNullOrEmpty(_accountAlias);
        public bool CanHandleUrl(RemoteUrl remoteUrl)
        {
            if (remoteUrl.AccountType != AccountType)
            {
                return false;
            }
            return ServerUrl == remoteUrl.ServerUrl;
        }
        public abstract RemoteAccountType AccountType { get; }
        public string ServerUrl { get; protected set; }

        public virtual RemoteAccount ChangeServerUrl(string serverUrl)
        {
            return ChangeProp(ImClone(this), im => im.ServerUrl = serverUrl);
        }
        public string Username { get; protected set; }

        public virtual RemoteAccount ChangeUsername(string username)
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
            string prefix = accountType.Name + @":";
            if (ServerUrl == accountType.GetEmptyUrl().ServerUrl)
            {
                return prefix + Username;
            }
            return prefix + Username + @"@" + ServerUrl;
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
                    Password = CommonTextUtil.DecryptString(encryptedPassword);
                }
                catch (Exception)
                {
                    Password = string.Empty;
                }
            }

            ServerUrl = (string) xElement.Attribute(ATTR.server_url.ToString());
            AccountAlias = (string)xElement.Attribute(ATTR.alias.ToString()) ?? string.Empty;
        }

        public virtual void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeIfString2(ATTR.server_url, ServerUrl);
            writer.WriteAttributeIfString2(ATTR.username, Username);
            writer.WriteAttributeIfString2(ATTR.alias, AccountAlias);
            if (!string.IsNullOrEmpty(Password))
            {
                writer.WriteAttributeString2(ATTR.password, CommonTextUtil.EncryptString(Password));
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

    public static class XmlExtensions
    {
        public static void WriteAttributeString2(this XmlWriter writer, Enum name, string value)
        {
            // ReSharper disable SpecifyACultureInStringConversionExplicitly
            writer.WriteAttributeString(name.ToString(), value);
            // ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        public static void WriteAttributeIfString2(this XmlWriter writer, Enum name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                writer.WriteAttributeString2(name, value);
        }
    }
}
