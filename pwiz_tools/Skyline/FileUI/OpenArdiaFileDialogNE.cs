/*
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

using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Ardia;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public class OpenArdiaFileDialogNE : BaseFileDialogNE
    {
        private const string URL_PATH_SEPARATOR = @"/";

        // TODO: hide all items from lookInComboBox (except remote)
        // TODO: include remote folder hierarchy in lookInComboBox
        // TODO: improve appearance of items on remote account selection screen
        // CONSIDER: support creating a new folder?
        // CONSIDER: include Recent Documents filtered to show Ardia paths
        // BaseFileDialogNE supports picking from multiple registered remote accounts
        public OpenArdiaFileDialogNE(IList<RemoteAccount> remoteAccounts) :
            base(new[] { DataSourceUtil.FOLDER_TYPE }, remoteAccounts, null)
        {
            InitialDirectory = ArdiaUrl.Empty;
            actionButton.Text = ArdiaResources.Ardia_FileUpload_SelectDestinationFolderLabel;

            RecentDocumentsButton.Visible = false;
            MyComputerButton.Visible = false;
            MyDocumentsButton.Visible = false;
            DesktopButton.Visible = false;

            ListViewControl.MultiSelect = false;

            OnlyShowFolders = true;
        }

        public string DestinationFolder { get; private set; }
        public ArdiaAccount SelectedAccount { get; private set; }

        // Callback triggered when user clicks the confirm button ([Select])
        protected override void DoMainAction()
        {
            if (!(CurrentDirectory is ArdiaUrl ardiaUrl))
            {
                DialogResult = DialogResult.Abort;
                return;
            }

            var remoteAccount = GetRemoteAccount(ardiaUrl);
            if (!(remoteAccount is ArdiaAccount ardiaAccount))
            {
                DialogResult = DialogResult.Abort;
                return;
            }

            var sb = new StringBuilder();
            sb.Append(URL_PATH_SEPARATOR);
            sb.Append(ardiaUrl.GetFilePath());
            if (ListViewControl.SelectedIndices.Count > 0)
            {
                sb.Append(URL_PATH_SEPARATOR);
                sb.Append(ListViewControl.SelectedItems[0].Text);
            }

            // TODO: verify this is a valid Ardia path
            DestinationFolder = sb.ToString();
            SelectedAccount = ardiaAccount;

            DialogResult = DialogResult.OK;
        }
    }
}