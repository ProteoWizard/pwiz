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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
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
    public class PublishDocumentDlgArdia : PublishDocumentDlgBase
    {
        public enum ValidateInputResult { valid, invalid_blank, invalid_character }
        private enum ImageId { ardia, folder, empty }

        private static readonly char[] ILLEGAL_FOLDER_NAME_CHARS = { ':', '\\', '/', '*', '?', '"', '<', '>', '|' };
        private static readonly RemoteItemComparer REMOTE_ITEM_COMPARER = new RemoteItemComparer();

        private readonly IList<ArdiaAccount> _ardiaAccounts;

        public PublishDocumentDlgArdia(IDocumentUIContainer docContainer, IList<ArdiaAccount> accounts, string fileName, DocumentFormat? fileFormatOnDisk) 
            : base(docContainer, fileName, fileFormatOnDisk)
        {
            _ardiaAccounts = accounts;

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
        /// Used in tests to exercise the UI without uploading a document.
        /// </summary>
        public bool SkipUpload { get; set; }

        internal override void HandleDialogOk()
        {
            DestinationPath = DestinationPathFor(treeViewFolders.SelectedNode);

            DialogResult = DialogResult.OK;
        }

        internal override void HandleDialogLoad()
        {
            // Do not show, Ardia does not support anon servers
            cbAnonymousServers.Visible = false;
            
            createRemoteFolder.Visible = true;
            createRemoteFolder.Enabled = false; // disabled until a TreeNode is selected
            createRemoteFolder.Click += CreateRemoteFolder_Click;

            // Panorama directory browser ignores single click events, so Ardia ignores them
            // too and only expands a folder with a click on the expand (plus sign) icon
            treeViewFolders.BeforeExpand += TreeViewFolders_BeforeExpand;
            treeViewFolders.AfterLabelEdit += TreeViewFolders_AfterLabelEdit;
            treeViewFolders.AfterSelect += TreeViewFolders_AfterSelect;

            UpdateTree(() =>
            {
                foreach (var account in _ardiaAccounts)
                {
                    // CONSIDER: does adding username ot the label improve the UI when choosing between multiple registered Ardia accounts?
                    var label = $@"{account.Username} @ {new Uri(account.ServerUrl).Host}";
                    var treeNode = new TreeNode(label, (int)ImageId.ardia, (int)ImageId.ardia)
                    {
                        Tag = new ArdiaAccountInfo(account, account.GetRootUrl()),
                    };

                    // Assume the server has sub-folders and add this node so the TreeView's expand
                    // icon appears without having actually called the Ardia API to get the child
                    // nodes. SrmTreeNode does something similar.
                    treeNode.Nodes.Add(CreateEmptyNode());

                    treeViewFolders.Nodes.Add(treeNode);
                }
            });
        }

        internal override string LoadExpansionAndSelection()
        {
            return Settings.Default.ArdiaServerExpansion;
        }

        internal override void SaveExpansionAndSelection()
        {
            Settings.Default.ArdiaServerExpansion = ServerTreeStateRestorer.GetPersistentString();
        }

        private void TreeViewFolders_AfterSelect(object sender, TreeViewEventArgs treeViewEventArgs)
        {
            createRemoteFolder.Enabled = treeViewFolders.SelectedNode != null;
        }

        // TODO: error handling?
        // TODO: how does LongWaitDlg behave when network takes a long time to respond? How does that affect UI?
        private void ExpandFolder(TreeNode parentTreeNode)
        {
            // Short-circuit if contents of this folder are already loaded. This means items added on the server
            // after this Dlg opens will not appear until the Dlg closes and re-opens.
            if (((ArdiaFolderInfo)parentTreeNode.Tag).RemoteContentsLoaded)
            {
                return;
            }

            var ardiaAccount = ArdiaAccountForTreeNode(parentTreeNode);
            var parentUrl = (ArdiaUrl)((ArdiaFolderInfo)parentTreeNode.Tag)?.RemoteUrl;

            try
            {
                treeViewFolders.Cursor = Cursors.WaitCursor;

                IList<RemoteItem> remoteItems = new List<RemoteItem>();

                using var waitDlg = new LongWaitDlg();
                waitDlg.Text = string.Format(ArdiaResources.OpenFolder_Title, parentTreeNode.Text);
                waitDlg.PerformWork(this, 1000, progressMonitor =>
                {
                    RemoteCallPending = true;

                    var ardiaClient = ArdiaClient.Create(ardiaAccount);
                    remoteItems = ardiaClient.GetFolders(parentUrl, progressMonitor);

                    RemoteCallPending = false;
                });

                AddItemsToFolder(remoteItems, parentTreeNode);

                ((ArdiaFolderInfo)parentTreeNode.Tag)!.RemoteContentsLoaded = true;
            }
            finally
            {
                treeViewFolders.Cursor = Cursors.Default;
            }
        }

        // TODO: does Ardia API include whether the current user has permission to upload to a given directory?
        // TODO: can Ardia API include whether a given directory includes 1+ folders? The "hasChildren" flag
        //       tells Skyline that a folder has child nodes - but is not enough information to tell whether
        //       1 or more children are folders without making N additional API calls. So, Skyline every
        //       folder can be expanded even if expanding adds no child nodes. 
        private void AddItemsToFolder(IList<RemoteItem> remoteItems, TreeNode parentTreeNode)
        {
            // Before expanding, remove the placeholder node to avoid showing a tree node with an empty label.
            // The placeholder node was added so TreeView displays the '+' for expand / collapse.
            RemovePlaceholderFrom(parentTreeNode); 

            if (remoteItems.Count == 0)
                return;

            // Sort lexicographically
            remoteItems.Sort(REMOTE_ITEM_COMPARER);

            UpdateTree(() =>
            {
                foreach (var remoteItem in remoteItems)
                {
                    var childTreeNode = CreateFolderNode(remoteItem.Label);

                    // For more info on this cast, see ArdiaSession @ ListContents
                    childTreeNode.Tag = new ArdiaFolderInfo(remoteItem.MsDataFileUri as ArdiaUrl);

                    // If this node has sub-folders, add a node so the expand icon appears without 
                    // actually having added child nodes. SrmTreeNode does something similar.
                    if (remoteItem.HasChildren)
                    {
                        childTreeNode.Nodes.Add(CreateEmptyNode());
                    }

                    parentTreeNode.Nodes.Add(childTreeNode);
                }
            });
        }

        public override void Upload(Control parent)
        {
            var ardiaAccount = ArdiaAccountForTreeNode(treeViewFolders.SelectedNode);

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

            if (isCanceled)
                return;

            // CONSIDER: Ardia API could respond with the URL of the uploaded file in Ardia Data Explorer and Skyline
            //           could offer to open that file in the browser. Panorama does similar.
            MessageDlg.Show(this, ArdiaResources.Ardia_FileUpload_SuccessfulUpload);
        }

        public void CreateFolder()
        {
            var parentNode = treeViewFolders.SelectedNode;
            if (parentNode == null)
                return;

            // avoid highlighting two tree nodes at the same time by de-selecting this node
            treeViewFolders.SelectedNode = null;

            // temporarily enable editing - will be disabled shortly
            treeViewFolders.LabelEdit = true;

            var newFolder = CreateFolderNode(ArdiaResources.CreateFolder_DefaultFolderName);

            // If parent has child nodes, UI is better if parent expands before adding a new child
            if (parentNode.Nodes.Count > 0)
            {
                parentNode.Expand();

                // New child goes at the end of the list, similar to what happens in Windows Explorer. If the
                // parent folder is collapsed and re-expanded, this new child will be sorted lexicographically.
                parentNode.Nodes.Add(newFolder);
            }
            // If parent has no child nodes, child must be added prior to calling expand - otherwise calling Expand() does nothing
            else
            {
                parentNode.Nodes.Add(newFolder);
                parentNode.Expand();
            }

            newFolder.BeginEdit();
        }

        /// <summary>
        /// Create a new folder on the remote server.
        /// </summary>
        public void TreeViewFolders_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
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

                var message = validateResult == ValidateInputResult.invalid_blank ? 
                    ArdiaResources.CreateFolder_Error_BlankName :
                    string.Format(ArdiaResources.CreateFolder_Error_IllegalCharacter, new string(ILLEGAL_FOLDER_NAME_CHARS));

                var alertDlg = new AlertDlg(message, MessageBoxButtons.OKCancel);
                alertDlg.ShowAndDispose(this);

                if (alertDlg.DialogResult == DialogResult.OK)
                {
                    e.Node.Text = e.Label;
                    e.Node.BeginEdit();
                }
                else
                {
                    var parentNode = e.Node.Parent;

                    e.Node.EndEdit(true);
                    e.Node.Remove();
                    treeViewFolders.LabelEdit = false;
                    treeViewFolders.SelectedNode = parentNode;
                }

                return;
            }

            var ardiaAccount = ArdiaAccountForTreeNode(newTreeNode);
            var parentFolderPath = DestinationPathFor(newTreeNode.Parent);
            ArdiaError serverError = null;

            using var waitDlg = new LongWaitDlg();
            waitDlg.Text = ArdiaResources.CreateFolder_Title;
            waitDlg.PerformWork(this, 1000, longWaitBroker =>
            {
                var ardiaClient = ArdiaClient.Create(ardiaAccount);
                ardiaClient.CreateFolder(parentFolderPath, newFolderName, longWaitBroker, out serverError);
            });

            if (serverError == null)
            {
                treeViewFolders.SelectedNode = newTreeNode;
            }
            else 
            {
                switch (serverError.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        MessageDlg.Show(this, string.Format(ArdiaResources.CreateFolder_Error_NotAuthenticated, newFolderName));
                        break;
                    case HttpStatusCode.BadRequest:
                    case HttpStatusCode.Conflict:
                        MessageDlg.Show(this, string.Format(ArdiaResources.CreateFolder_Error_FileAlreadyExists, newFolderName));
                        break;
                    case HttpStatusCode.Forbidden:
                        MessageDlg.Show(this, string.Format(ArdiaResources.CreateFolder_Error_Forbidden, parentFolderPath, newFolderName));
                        break;
                    default:
                        // CONSIDER: not sure about this pattern of error handling. URI is useful yet not available in this context. Is it better
                        //           to throw from ArdiaClient and catch here, optionally deciding whether to handle locally throw up to the caller?
                        //           With a cavet that LongWaitDlg wraps and re-throws exceptions, which probably isn't what we want here.
                        throw ArdiaServerException.Create(@"Error creating folder", null, serverError, null);
                }

                treeViewFolders.SelectedNode = e.Node.Parent;
                e.Node.Remove();
            }
        }

        private void TreeViewFolders_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            ExpandFolder(e.Node);
        }

        private void CreateRemoteFolder_Click(object sender, EventArgs eventArgs)
        {
            CreateFolder();
        }

        // CONSIDER: refactor - make the base class's virtual and move impl to Panorama
        private string DestinationPathFor(TreeNode treeNode)
        {
            // Ardia paths differ from what the baseclass provides with GetFolderPath. Differences:
            // (1) Add a leading slash ('/')
            // (2) Remove any trailing slash ('/')
            return ArdiaClient.URL_PATH_SEPARATOR + GetFolderPath(treeNode)?.TrimEnd('/');
        }

        private void UpdateTree(Action action)
        {
            Assume.IsNotNull(action);
            try
            {
                treeViewFolders.BeginUpdate();

                action.Invoke();
            }
            finally
            {
                treeViewFolders.EndUpdate();
            }
        }

        private static void RemovePlaceholderFrom(TreeNode treeNode)
        {
            if (treeNode.Nodes.Count == 0 || treeNode.Nodes.Count > 1)
                return;

            if (treeNode.Nodes[0] is PlaceholderTreeNode)
                treeNode.Nodes.RemoveAt(0);
        }

        /// <summary>
        /// Helper that walks up to the root of this tree, which refers to the Ardia server associated with the given <param name="treeNode"></param>
        /// </summary>
        /// <param name="treeNode">A node from the tree view</param>
        /// <returns>the <see cref="ArdiaAccount"/> associated with the given <param name="treeNode"></param></returns>
        private static ArdiaAccount ArdiaAccountForTreeNode(TreeNode treeNode)
        {
            var root = treeNode;
            while (root.Parent != null)
            {
                root = root.Parent;
            }

            return ((ArdiaAccountInfo)root.Tag).Account;
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
                return PublishDocumentDlgArdia.ValidateInputResult.invalid_blank;
            else if (folderName.IndexOfAny(ILLEGAL_FOLDER_NAME_CHARS) != -1)
                return PublishDocumentDlgArdia.ValidateInputResult.invalid_character;
            else return PublishDocumentDlgArdia.ValidateInputResult.valid;
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

        private class RemoteItemComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                return string.Compare(((RemoteItem)x)?.Label, ((RemoteItem)y)?.Label, StringComparison.CurrentCulture);
            }
        }

        private class ArdiaFolderInfo
        {
            internal ArdiaFolderInfo(RemoteUrl remoteUrl)
            {
                RemoteUrl = remoteUrl;
            }

            internal RemoteUrl RemoteUrl { get; }
            internal bool RemoteContentsLoaded { get; set; }
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