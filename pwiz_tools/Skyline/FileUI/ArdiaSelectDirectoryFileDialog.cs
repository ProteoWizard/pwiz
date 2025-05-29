/*
 * Copyright 2025 University of Washington - Seattle, WA
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
    /// <summary>
    /// File Dialog for browsing directories on an Ardia server. Skyline uses this UI for
    /// selecting the destination directory for files uploaded to a remote server.
    ///
    /// This subclass of <see cref="BaseFileDialogNE"/> customizes the parent's UI to turn off
    /// parts of the parent UI related to local files - for example: Desktop, My Documents, etc -
    /// which are hidden rather than just disabled.
    /// </summary>
    public class ArdiaSelectDirectoryFileDialog : BaseFileDialogNE
    {
        // RFC 3986 specified path separator for URIs
        // CONSIDER: add UrlBuilder class to Skyline. Related PRs also define this constant and helper methods narrowly scoped to a remote server vendor
        private const string URL_PATH_SEPARATOR = @"/";

        // TODO: include remote folder hierarchy in lookInComboBox (added in PR3170?)
        // TODO: hide lookInComboBox items not related to Ardia.
        //       NB: need to update BaseFileDialogNE.populateComboBoxFromDirectory to be resilient when local
        //           items (ex: My Computer) missing from ComboBox
        // TODO: improve appearance of items on remote account selection screen
        // CONSIDER: customize appearance of "Source name" to show selected path?
        // CONSIDER: support for creating a new folder?
        // CONSIDER: include Recent Documents filtered to Ardia paths
        // CONSIDER: improve testability of BaseFileDialogNE - ex: SelectPath, OkDialog. See examples in ArdiaFileUploadTest
        // Fyi, BaseFileDialogNE supports choosing a specific account if multiple Ardia accounts registered
        public ArdiaSelectDirectoryFileDialog(IList<RemoteAccount> remoteAccounts) :
            base(new[] { DataSourceUtil.FOLDER_TYPE }, remoteAccounts)
        {
            // TODO: this causes BaseFileDialogNE to show Ardia instead of the local file system. But when only
            //       working with 1 remote account, how to show the Ardia server's root directory and skip the
            //       "pick remote account" screen?
            InitialDirectory = ArdiaUrl.Empty;
            actionButton.Text = ArdiaResources.Ardia_FileUpload_SelectDestinationButtonLabel;

            RecentDocumentsButton.Visible = false;
            MyComputerButton.Visible = false;
            MyDocumentsButton.Visible = false;
            DesktopButton.Visible = false;

            ListViewControl.MultiSelect = false;

            OnlyShowFolders = true;

            IsLoaded = true;
        }

        public bool IsLoaded { get; private set; }
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

            // TODO: make URL building testable
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

        public void SelectItemAndActivate(string label)
        {
            SelectFile(label);
            ActivateItem();
        }

        public void SelectItemAndActivate(int index)
        {
            if (index < 0 || listView.Items.Count < index)
                return;

            listView.SelectedIndices.Add(index);
            
            ActivateItem();
        }

        public int ListCount()
        {
            return listView.Items.Count;
        }

        public void OkDialog()
        {
            DoMainAction();
        }
    }
}