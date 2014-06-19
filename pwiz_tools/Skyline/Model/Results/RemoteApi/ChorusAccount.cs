/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    [XmlRoot("chorus_account")]
    public sealed class ChorusAccount : Immutable, IKeyContainer<string>, IXmlSerializable
    {
        public const string DEFAULT_SERVER = "https://chorusproject.org"; // Not L10N
        public static readonly ChorusAccount BLANK = new ChorusAccount().SetServerUrl(DEFAULT_SERVER); // Not L10N
        public ChorusAccount(string serverUrl, string username, string password)
        {
            ServerUrl = serverUrl;
            Username = username;
            Password = password;
        }

        private ChorusAccount(ChorusAccount chorusAccount)
        {
            ServerUrl = chorusAccount.ServerUrl;
            Username = chorusAccount.Username;
            Password = chorusAccount.Password;
        }

        public string ServerUrl { get; private set; }

        public ChorusAccount SetServerUrl(string serverUrl)
        {
            return new ChorusAccount(this){ServerUrl = serverUrl};
        }
        public string Username { get; private set; }

        public ChorusAccount SetUserName(string userName)
        {
            return new ChorusAccount(this){Username = userName};
        }
        public string Password { get; private set; }

        public ChorusAccount SetPassword(string password)
        {
            return new ChorusAccount(this){Password = password};
        }

        public ChorusUrl GetChorusUrl()
        {
            return ChorusUrl.EMPTY.SetServerUrl(ServerUrl).SetUsername(Username);
        }

        public ChorusAccount Clone()
        {
            return new ChorusAccount(this);
        }

        public string GetKey()
        {
            if (ServerUrl == DEFAULT_SERVER)
            {
                return Username;
            }
            return Username + '@' + ServerUrl;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private ChorusAccount()
        {
        }

        private enum ATTR
        {
            username,
            password,
            server_url,
        }

        public static ChorusAccount Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChorusAccount());
        }

        private void Validate()
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            Username = reader.GetAttribute(ATTR.username) ?? string.Empty;
            Password = reader.GetAttribute(ATTR.password) ?? string.Empty;
            ServerUrl = reader.GetAttribute(ATTR.server_url);
            // Consume tag
            reader.Read();
            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttributeString(ATTR.server_url, ServerUrl);
            writer.WriteAttributeString(ATTR.username, Username);
            writer.WriteAttributeString(ATTR.password, Password);
        }
        #endregion

        #region object overrides

        private bool Equals(ChorusAccount other)
        {
            return string.Equals(Username, other.Username) &&
                string.Equals(Password, other.Password) &&
                Equals(ServerUrl, other.ServerUrl);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ChorusAccount && Equals((ChorusAccount)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Username != null ? Username.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Password != null ? Password.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ServerUrl != null ? ServerUrl.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }

    public sealed class ChorusAccountList : SettingsList<ChorusAccount>
    {
        public override IEnumerable<ChorusAccount> GetDefaults(int revisionIndex)
        {
            yield break;
        }

        public override string Title { get { return Resources.ChorusAccountList_Title_Edit_Chorus_Accounts; } }

        public override string Label { get { return Resources.ChorusAccountList_Label_Chorus_Accounts; } }

        public override ChorusAccount EditItem(Control owner, ChorusAccount item, IEnumerable<ChorusAccount> existing, object tag)
        {
            using (EditChorusAccountDlg editChorusAccountDlg = new EditChorusAccountDlg(item ?? ChorusAccount.BLANK, existing ?? this))
            {
                if (editChorusAccountDlg.ShowDialog(owner) == DialogResult.OK)
                    return editChorusAccountDlg.GetChorusAccount();

                return null;
            }
        }

        public override ChorusAccount CopyItem(ChorusAccount item)
        {
            return item.Clone();
        }
    }
}
