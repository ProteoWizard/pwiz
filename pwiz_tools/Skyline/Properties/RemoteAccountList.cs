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
using System.Windows.Forms;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;
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

        public override string Title { get { return Resources.RemoteAccountList_Title_Edit_Remote_Accounts; } }

        public override string Label { get { return Resources.RemoteAccountList_Label_Remote_Accounts; } }

        public override RemoteAccount EditItem(Control owner, RemoteAccount item, IEnumerable<RemoteAccount> existing, object tag)
        {
            using (EditRemoteAccountDlg editRemoteAccountDlg = new EditRemoteAccountDlg(item ?? UnifiAccount.DEFAULT, existing ?? this))
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
            };
        }
    }
}