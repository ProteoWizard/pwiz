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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    // CONSIDER: Esc key cancels creating a new folder
    public class PublishDocumentDlgArdia : PublishDocumentDlgBase
    {
        private enum ImageId { ardia, folder, empty }
        public enum ValidateInputResult { valid, invalid_blank, invalid_character }

        private static readonly char[] ILLEGAL_FOLDER_NAME_CHARS = { ':', '\\', '/', '*', '?', '"', '<', '>', '|' };

        private readonly IList<ArdiaAccount> _ardiaAccounts;
        private readonly IDictionary<ArdiaAccount, ArdiaSession> _ardiaSessions;

        // These fields are communicate state between request / responses made using an ArdiaSession
        // since it does not use lambdas.
        private RemoteUrl _contentsAvailableRemoteUrl;
        private TreeNode _contentsAvailableTreeNode;
        private string _contentsAvailableFolderToSelect;

        public PublishDocumentDlgArdia(IDocumentUIContainer docContainer, IList<ArdiaAccount> accounts, string fileName, DocumentFormat? fileFormatOnDisk) 
            : base(docContainer, fileName, fileFormatOnDisk)
        {
            _ardiaAccounts = accounts;
            _ardiaSessions = new Dictionary<ArdiaAccount, ArdiaSession>();

            foreach (var account in accounts)
            {
                _ardiaSessions[account] = (ArdiaSession)account.CreateSession();
                _ardiaSessions[account].ContentsAvailable += ArdiaSession_ContentsAvailableWrapper;
            }
            _ardiaSessions = new ImmutableDictionary<ArdiaAccount, ArdiaSession>(_ardiaSessions);

            treeViewFolders.ImageList.TransparentColor = Color.Magenta;
            treeViewFolders.ImageList.Images.Add(Resources.ArdiaIcon);  // 32bpp
            treeViewFolders.ImageList.Images.Add(Resources.Folder);     // 32bpp
            treeViewFolders.ImageList.Images.Add(Resources.Blank);      // 32bpp
        }

        public string DestinationPath { get; private set; }
        /// <summary>
        /// Used in tests.
        /// </summary>
        public bool RemoteCallPending { get; private set; }
        /// <summary>
        /// Used in tests.
        /// </summary>
        public TreeView FoldersTree => treeViewFolders;
        /// <summary>
        /// Used to test UI without uploading the current document.
        /// </summary>
        public bool SkipUpload { get; set; }

        internal override void HandleDialogLoad()
        {
            // Do not show - Ardia does not support anon servers
            cbAnonymousServers.Visible = false;
            
            createRemoteFolder.Visible = true;
            createRemoteFolder.Enabled = false; // disabled until a TreeNode is selected
            createRemoteFolder.Click += CreateRemoteFolder_Click;

            // Panorama directory browser ignores single click events, so Ardia ignores them
            // too and only expands a folder with a click on the expand (plus sign) icon
            treeViewFolders.BeforeExpand += TreeViewFolders_BeforeExpand;
            treeViewFolders.AfterLabelEdit += TreeViewFolders_AfterLabelEdit;
            treeViewFolders.AfterSelect += TreeViewFolders_AfterSelect;

            treeViewFolders.BeginUpdate();
            foreach (var account in _ardiaAccounts)
            {
                // CONSIDER: add username to label? To distinguish multiple Ardia accounts with the same server.
                var treeNode = new TreeNode(account.ServerUrl, (int)ImageId.ardia, (int)ImageId.ardia)
                {
                    Tag = new ArdiaAccountInfo(account, account.GetRootUrl()),
                };

                // Assume the server has sub-folders and add this node so the TreeView's expand
                // icon appears without having actually called the Ardia API to get the child
                // nodes. SrmTreeNode does something similar.
                treeNode.Nodes.Add(CreateEmptyNode());

                treeViewFolders.Nodes.Add(treeNode);
            }
            treeViewFolders.EndUpdate();
        }

        private void TreeViewFolders_AfterSelect(object sender, TreeViewEventArgs treeViewEventArgs)
        {
            createRemoteFolder.Enabled = treeViewFolders.SelectedNode != null;
        }

        private void ExpandFolder(TreeNode parentTreeNode)
        {
            var ardiaSession = ArdiaSessionForTreeNode(parentTreeNode);
            var parentUrl = (ArdiaUrl)(parentTreeNode.Tag as ArdiaFolderInfo)?.RemoteUrl;

            // Reuse results if they were already fetched from the API during this remote session.
            // Also skip re-rendering TreeView nodes for these files should already exist in the tree.
            if (ardiaSession.HasResultsFor(parentUrl))
            {
                return;
            }

            treeViewFolders.Cursor = Cursors.WaitCursor;
            parentTreeNode.Nodes.Clear();

            RemoteCallPending = true;

            _contentsAvailableRemoteUrl = parentUrl;
            _contentsAvailableTreeNode = parentTreeNode;

            // TODO: should this be wrapped in LongWaitDlg? Does that work with RemoteSession's callback model?
            // TODO: error handling
            ardiaSession.AsyncFetchContents(parentUrl, out _);
        }

        private void ArdiaSession_ContentsAvailableWrapper()
        {
            CommonActionUtil.SafeBeginInvoke(this, () =>
            {
                ArdiaSession_ContentsAvailable(_contentsAvailableRemoteUrl, _contentsAvailableTreeNode);
            });
        }

        private void ArdiaSession_ContentsAvailable(RemoteUrl parentUrl, TreeNode parentTreeNode)
        {
            try
            {
                // TODO: does Ardia API include whether the current user has permission to upload to a given directory?
                // CONSIDER: can Ardia API include whether a given directory includes 1+ folders? The current "hasChildren" flag 
                //           is true when the directory contains only files - but Skyline doesn't know that without making the 
                //           API call
                var ardiaSession = ArdiaSessionForTreeNode(parentTreeNode);
                var remoteItems = ardiaSession.ListContents(parentUrl).ToList();

                // Sort folders lexicographically
                // CONSIDER: would more sort options be useful?
                // TODO: folders with new items may be out of order, so re-sort before opening
                remoteItems.Sort((item1, item2) => string.Compare(item1.Label, item2.Label, StringComparison.CurrentCultureIgnoreCase));

                treeViewFolders.BeginUpdate();
                foreach (var remoteItem in remoteItems)
                {
                    // Skip files and other non-folder items in this folder
                    if (!DataSourceUtil.IsFolderType(remoteItem.Type))
                        continue;

                    var folderName = remoteItem.Label;
                    // For more info on this cast, see ArdiaSession @ ListContents
                    var folderUrl = remoteItem.MsDataFileUri as ArdiaUrl;
                    var folderHasChildren = remoteItem.HasChildren;

                    var childTreeNode = CreateFolderNode(folderName);
                    childTreeNode.Tag = new ArdiaFolderInfo(folderUrl);

                    // If this node has sub-folders, add a node so the expand icon appears without 
                    // actually having added child nodes. SrmTreeNode does something similar.
                    if (folderHasChildren)
                    {
                        childTreeNode.Nodes.Add(CreateEmptyNode());
                    }

                    parentTreeNode.Nodes.Add(childTreeNode);
                }

                parentTreeNode.Expand();

                if (_contentsAvailableFolderToSelect != null)
                {
                    foreach (TreeNode node in parentTreeNode.Nodes)
                    {
                        if (string.Equals(node.Text, _contentsAvailableFolderToSelect))
                        {
                            treeViewFolders.SelectedNode = node;
                            treeViewFolders.SelectedNode.EnsureVisible();
                        }
                    }
                }
            }
            finally
            {
                treeViewFolders.EndUpdate();
                treeViewFolders.Cursor = Cursors.Default;
                RemoteCallPending = false;

                // Reset local variables used to pass state between making the request and handling the request
                _contentsAvailableFolderToSelect = null;
                _contentsAvailableRemoteUrl = null;
                _contentsAvailableTreeNode = null;
            }
        }

        /// <summary>
        /// Helper that walks up to the root of this tree, which refers to the Ardia server associated with the given <param name="treeNode"></param>
        /// </summary>
        /// <param name="treeNode">A node from the tree view</param>
        /// <returns>the ArdiaSession associated with the given <param name="treeNode"></param></returns>
        private ArdiaSession ArdiaSessionForTreeNode(TreeNode treeNode)
        {
            var root = treeNode;
            while (root.Parent != null)
            {
                root = root.Parent;
            }

            var account = ((ArdiaAccountInfo)root.Tag).Account;
            return _ardiaSessions[account];
        }

        private string DestinationPathFor(TreeNode treeNode)
        {
            // Ardia needs a path different from what the baseclass provides with GetFolderPath. So
            // fixup with (1) leading '/' and (2) remove any trailing '/'
            return ArdiaClient.URL_PATH_SEPARATOR + GetFolderPath(treeNode)?.TrimEnd('/');
        }

        internal override void HandleDialogOk()
        {
            DestinationPath = DestinationPathFor(treeViewFolders.SelectedNode);

            DialogResult = DialogResult.OK;
        }

        public override void Upload(Control parent)
        {
            var ardiaAccount = ArdiaSessionForTreeNode(treeViewFolders.SelectedNode).ArdiaAccount;

            var isCanceled = false;
            if (!SkipUpload)
            {
                using var waitDlg = new LongWaitDlg();
                waitDlg.Text = UtilResources.PublishDocumentDlg_UploadSharedZipFile_Uploading_File;
                waitDlg.PerformWork(this, 1000, longWaitBroker =>
                {
                    var ardiaClient = ArdiaClient.Create(ardiaAccount);
                    ardiaClient.SendZipFile(DestinationPath, FileName, longWaitBroker, out _);

                    isCanceled = longWaitBroker.IsCanceled;
                });
            }

            if (!isCanceled)
            {
                // CONSIDER: Ardia API could respond with the URL of the uploaded file in Ardia Data Explorer and Skyline
                //           could offer to open that file in the browser. Panorama does similar.
                MessageDlg.Show(this, ArdiaResources.Ardia_FileUpload_SuccessfulUpload);
            }
        }

        // TODO: for now, nodes can only be added to an already expanded folder
        public void CreateFolder()
        {
            var parentNode = treeViewFolders.SelectedNode;
            if (parentNode == null)
                return;

            var newFolder = CreateFolderNode(ArdiaResources.FileUpload_DefaultNewFolderName);

            treeViewFolders.SelectedNode = null;
            treeViewFolders.LabelEdit = true;

            // NB: order is important - UI is better when parent expands before adding child node
            // TODO: fetch parentNode's contents and display if not already expanded
            parentNode.Expand();
            RemovePlaceholderNode(parentNode);

            // Add new folder at end and leave it there. Windows Explorer does something similar.
            parentNode.Nodes.Add(newFolder);

            parentNode.Expand();

            newFolder.BeginEdit();
        }

        /// <summary>
        /// Create a new folder on the remote server.
        /// </summary>
        public void TreeViewFolders_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            // CONSIDER: interaction weirdness after (1) Create Folder and (2) press Esc to cancel
            var newFolderName = e.Label;
            var newTreeNode = e.Node;

            var validateResult = ValidateFolderName(newFolderName);

            if (validateResult == ValidateInputResult.valid)
            {
                e.Node.EndEdit(false);
                treeViewFolders.LabelEdit = false;

                var parentUrl = (ArdiaUrl)((ArdiaFolderInfo)newTreeNode.Parent.Tag).RemoteUrl;
                Assume.IsNotNull(parentUrl);

                var childUrl = parentUrl.ChangePathParts(parentUrl.GetPathParts().Concat(new[] { newFolderName }));

                newTreeNode.Tag = new ArdiaFolderInfo(childUrl);
            }
            else
            {
                e.CancelEdit = true;

                var message =
                    validateResult == ValidateInputResult.invalid_blank
                        ? ArdiaResources.CreateFolder_Error_BlankName
                        : string.Format(ArdiaResources.CreateFolder_Error_IllegalCharacter, new string(ILLEGAL_FOLDER_NAME_CHARS));

                var alertDlg = new AlertDlg(message, MessageBoxButtons.OKCancel);
                alertDlg.ShowAndDispose(this);

                if (alertDlg.DialogResult == DialogResult.OK)
                {
                    e.Node.Text = e.Label;
                    e.Node.BeginEdit();
                }
                else
                {
                    e.Node.EndEdit(true);
                    e.Node.Remove();
                    treeViewFolders.LabelEdit = false;
                }

                return;
            }

            var ardiaAccount = ArdiaSessionForTreeNode(newTreeNode).ArdiaAccount;
            var parentFolderPath = DestinationPathFor(newTreeNode.Parent);

            using var waitDlg = new LongWaitDlg();
            waitDlg.Text = ArdiaResources.CreateFolder_Title;
            waitDlg.PerformWork(this, 1000, longWaitBroker =>
            {
                var ardiaClient = ArdiaClient.Create(ardiaAccount);
                ardiaClient.CreateFolder(parentFolderPath, newFolderName, longWaitBroker);
            });

            treeViewFolders.SelectedNode = newTreeNode;
            treeViewFolders.LabelEdit = false;
        }

        private void TreeViewFolders_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            ExpandFolder(e.Node);
        }

        private void CreateRemoteFolder_Click(object sender, EventArgs eventArgs)
        {
            CreateFolder();
        }

        private static void RemovePlaceholderNode(TreeNode treeNode)
        {
            if (treeNode.Nodes.Count == 0 || treeNode.Nodes.Count > 1)
                return;

            if (treeNode.Nodes[0] is PlaceholderTreeNode)
                treeNode.Nodes.RemoveAt(0);
        }

        /// <summary>
        /// Test <param name="folderName"></param> meets these criteria for a valid folder name:
        ///     (1) Allowed Chars A-Z, a-z,0-9, space, - and _
        ///     (2) Names and paths are case-insensitive
        ///     (3) As Folder Name should not contain any of the following characters : \ / : * ? " &lt; > |
        ///     (4) There is not any Max Length Char limit
        /// </summary>
        public static ValidateInputResult ValidateFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return ValidateInputResult.invalid_blank;
            else if (folderName.IndexOfAny(ILLEGAL_FOLDER_NAME_CHARS) != -1)
                return ValidateInputResult.invalid_character;
            else return ValidateInputResult.valid;
        }

        private static TreeNode CreateEmptyNode()
        {
            return new PlaceholderTreeNode();
        }

        private static TreeNode CreateFolderNode(string label)
        {
            return new TreeNode(label, (int)ImageId.folder, (int)ImageId.folder);
        }

        private class PlaceholderTreeNode : TreeNode
        {
            public PlaceholderTreeNode() : base(@"        ", (int)ImageId.empty, (int)ImageId.empty) { }
        }

        private class ArdiaFolderInfo
        {
            internal ArdiaFolderInfo(RemoteUrl remoteUrl)
            {
                RemoteUrl = remoteUrl;
            }

            internal RemoteUrl RemoteUrl { get; }
        }

        private class ArdiaAccountInfo : ArdiaFolderInfo
        {
            internal ArdiaAccountInfo(ArdiaAccount account, RemoteUrl url) : base(url)
            {
                Account = account;
            }

            internal ArdiaAccount Account { get; }
        }
    }
}