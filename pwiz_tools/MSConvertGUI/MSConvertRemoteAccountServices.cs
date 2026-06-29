/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2024 Matt Chambers
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
using System.IO;
using System.Net.Http;
using System.Xml;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Unifi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;

namespace MSConvertGUI
{
    /// <summary>
    /// Provides remote account storage and user interaction for MSConvertGUI.
    /// Implements the interfaces needed by CommonMsData's RemoteUrl and RemoteSession.
    /// </summary>
    public class MSConvertRemoteAccountServices : IRemoteAccountStorage, IRemoteAccountUserInteraction
    {
        public static readonly MSConvertRemoteAccountServices INSTANCE = new MSConvertRemoteAccountServices();

        private readonly List<RemoteAccount> _accounts = new List<RemoteAccount>();
        private static readonly string AccountsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MSConvertGUI", "RemoteAccounts.xml");

        public static void Initialize()
        {
            RemoteUrl.RemoteAccountStorage = INSTANCE;
            RemoteSession.RemoteAccountUserInteraction = INSTANCE;
            INSTANCE.LoadAccounts();
        }

        public IEnumerable<RemoteAccount> GetRemoteAccounts()
        {
            return _accounts;
        }

        public IList<RemoteAccount> GetRemoteAccountList()
        {
            return _accounts;
        }

        public void SetAccounts(IEnumerable<RemoteAccount> accounts)
        {
            _accounts.Clear();
            _accounts.AddRange(accounts);
            SaveAccounts();
        }

        public void AddAccount(RemoteAccount account)
        {
            _accounts.Add(account);
            SaveAccounts();
        }

        public void RemoveAccount(RemoteAccount account)
        {
            _accounts.Remove(account);
            SaveAccounts();
        }

        public Func<HttpClient> UserLogin(RemoteAccount account)
        {
            // For MSConvertGUI, we don't support interactive browser-based login flows.
            // UNIFI uses resource-owner-password grant which doesn't need this.
            // Waters Connect browser auth would need UI support added later.
            throw new NotSupportedException(
                "Interactive login is not currently supported in MSConvertGUI. " +
                "Please configure your account credentials in the Remote Accounts settings.");
        }

        #region Persistence

        private void LoadAccounts()
        {
            if (!File.Exists(AccountsFilePath))
                return;

            try
            {
                using (var reader = XmlReader.Create(AccountsFilePath))
                {
                    reader.ReadStartElement("RemoteAccounts");
                    while (reader.IsStartElement())
                    {
                        var elementName = reader.LocalName;
                        RemoteAccount account = null;
                        switch (elementName)
                        {
                            case "unifi_account":
                                account = new UnifiAccount(string.Empty, string.Empty, string.Empty);
                                account.ReadXml(reader);
                                break;
                            case "waters_connect_account":
                                account = new WatersConnectAccount(string.Empty, string.Empty, string.Empty);
                                account.ReadXml(reader);
                                break;
                            default:
                                reader.Skip();
                                break;
                        }
                        if (account != null)
                            _accounts.Add(account);
                    }
                }
            }
            catch (Exception)
            {
                // If we can't load accounts, start with empty list
            }
        }

        private void SaveAccounts()
        {
            try
            {
                var dir = Path.GetDirectoryName(AccountsFilePath);
                if (dir != null)
                    Directory.CreateDirectory(dir);

                using (var writer = XmlWriter.Create(AccountsFilePath, new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("RemoteAccounts");

                    foreach (var account in _accounts)
                    {
                        string elementName;
                        if (account is UnifiAccount)
                            elementName = "unifi_account";
                        else if (account is WatersConnectAccount)
                            elementName = "waters_connect_account";
                        else
                            continue;

                        writer.WriteStartElement(elementName);
                        account.WriteXml(writer);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }
            catch (Exception)
            {
                // Best effort persistence
            }
        }

        #endregion
    }
}
