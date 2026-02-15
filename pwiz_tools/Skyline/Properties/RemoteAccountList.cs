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
using System.Windows.Forms;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.CommonMsData.RemoteApi.Unifi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Properties
{
    public sealed class RemoteAccountList : SettingsList<RemoteAccount>
    {
        public override IEnumerable<RemoteAccount> GetDefaults(int revisionIndex)
        {
            yield break;
        }

        public IEnumerable<RemoteAccount> GetAccountsOfType(RemoteAccountType type)
        {
            return Items.Where(item => item.AccountType == type);
        }

        public override string Title { get { return PropertiesResources.RemoteAccountList_Title_Edit_Remote_Accounts; } }

        public override string Label { get { return PropertiesResources.RemoteAccountList_Label_Remote_Accounts; } }

        public override RemoteAccount EditItem(Control owner, RemoteAccount item, IEnumerable<RemoteAccount> existing, object tag)
        {
            using (EditRemoteAccountDlg editRemoteAccountDlg = new EditRemoteAccountDlg(item, existing ?? this))
            {
                if (editRemoteAccountDlg.ShowDialog(owner) == DialogResult.OK)
                    return editRemoteAccountDlg.GetRemoteAccount();

                return null;
            }
        }

        public override RemoteAccount CopyItem(RemoteAccount item)
        {
            return item.ChangeUsername(string.Empty).ChangePassword(string.Empty);
        }

        protected override IXmlElementHelper<RemoteAccount>[] GetXmlElementHelpers()
        {
            return new IXmlElementHelper<RemoteAccount>[]
            {
                new XmlElementHelper<UnifiAccount>(),
                new XmlElementHelper<ArdiaAccount>(),
                new XmlElementHelper<WatersConnectAccount>(),
            };
        }

        /// <summary>
        /// Retrieves the remote account for the given url.
        /// </summary>
        /// <param name="remoteUrl">Server and username from this url are used to search for the account in the list.</param>
        /// <returns>Matching account or null if nothing is matching.</returns>
        public RemoteAccount GetRemoteAccount(RemoteUrl remoteUrl)
        {
            return
                this.FirstOrDefault(
                    remoteAccount =>
                        Equals(remoteAccount.ServerUrl, remoteUrl.ServerUrl) &&
                        Equals(remoteAccount.Username, remoteUrl.Username));
        }

    }
}