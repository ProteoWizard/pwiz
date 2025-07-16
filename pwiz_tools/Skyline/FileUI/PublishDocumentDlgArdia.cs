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
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
using TreeNode = System.Windows.Forms.TreeNode;

namespace pwiz.Skyline.FileUI
{
    // TODO: improve handling error case - server unreachable / unavailable
    // TODO: improve handling error case - token invalid
    public class PublishDocumentDlgArdia : PublishDocumentDlgBase
    {
        public enum ValidateInputResult { valid, invalid_blank, invalid_character }
        private enum ImageId { ardia, folder, empty }

        private static readonly char[] ILLEGAL_FOLDER_NAME_CHARS = { ':', '\\', '/', '*', '?', '"', '<', '>', '|' };
        private static readonly RemoteItemComparer REMOTE_ITEM_COMPARER = new RemoteItemComparer();

        public PublishDocumentDlgArdia(IDocumentUIContainer docContainer, ArdiaAccount account, string fileName, DocumentFormat? fileFormatOnDisk) 
            : base(docContainer, fileName, fileFormatOnDisk)
        {
            Account = account;
            Client = ArdiaClient.Create(Account);

            treeViewFolders.ImageList.TransparentColor = Color.Magenta;
            treeViewFolders.ImageList.Images.Add(Resources.ArdiaIcon);  // 32bpp
            treeViewFolders.ImageList.Images.Add(Resources.Folder);     // 32bpp
            treeViewFolders.ImageList.Images.Add(Resources.Blank);      // 32bpp
        }

        /// <summary>
        /// Fully qualified path for where to put the Skyline document on an Ardia server.
        /// </summary>
        public string DestinationPath { get; private set; }
        private ArdiaAccount Account { get; }
        private ArdiaClient Client { get; }

        /// <summary>
        /// If publishing succeeded, this is a <see cref="CreateDocumentResponse"/> representing the new Ardia document.
        /// </summary>
        public CreateDocumentResponse PublishedDocument { get; private set; }

        /// <summary>
        /// Used in tests.
        /// </summary>
        public TreeView FoldersTree => treeViewFolders;

        // TODO: remove when re-enabling publishing
        /// <summary>
        /// Used in tests to skip publishing the current Skyline document to a remote server. Does not skip any other read or write network operations.
        /// </summary>
        public bool SkipPublish { get; set; } = false;

        /// <summary>
        /// Used in tests to find a <see cref="TreeNode"/> by name.
        /// </summary>
        /// <param name="nodes">Tree nodes to search</param>
        /// <param name="name">Name of node to find</param>
        /// <returns>TreeNode with matching name, null otherwise</returns>
        public TreeNode FindByName(TreeNodeCollection nodes, string name)
        {
            return nodes.Cast<TreeNode>().FirstOrDefault(node => string.Equals(node.Text, name, StringComparison.CurrentCulture));
        }

        internal override void HandleDialogOk()
        {
            DestinationPath = GetFolderPath(treeViewFolders.SelectedNode);

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
                // CONSIDER: do not need to support multiple accounts for now - so revisit the root node label
                var label = $@"{Account.Username} @ {new Uri(Account.ServerUrl).Host}";
                var treeNode = new TreeNode(label, (int)ImageId.ardia, (int)ImageId.ardia)
                {
                    Tag = new ArdiaAccountInfo(Account, Account.GetRootUrl()),
                };

                // Assume the server has sub-folders and add this node so the TreeView's expand
                // icon appears without having actually called the Ardia API to get the child
                // nodes. SrmTreeNode does something similar.
                treeNode.Nodes.Add(CreateEmptyNode());

                treeViewFolders.Nodes.Add(treeNode);
            });

            // Expand the top-level folder if it's collapsed. This improves the UX when the
            // top-level folder is collapsed, especially when there's no start to restore.
            if (!treeViewFolders.Nodes[0].IsExpanded)
            {
                treeViewFolders.Nodes[0].Expand();
            }
        }

        // CONSIDER: if the previously selected item is unavailable, set focus to top node (better than
        //           current behavior which selects the topmost visible node). Maybe add a callback
        //           from TreeViewStateRestorer?
        internal override string LoadExpansionAndSelection()
        {
            return Settings.Default.ArdiaServerExpansion;
        }

        internal override void SaveExpansionAndSelection()
        {
            Settings.Default.ArdiaServerExpansion = ServerTreeStateRestorer.GetPersistentString();
        }

        private void ExpandFolder(TreeNode parentTreeNode)
        {
            // Short-circuit if contents of this folder are already loaded in the tree. This saves
            // making the UI more responsive at the expense of missing any items added on the server
            // since the dialog opened.
            if (((ArdiaFolderInfo)parentTreeNode.Tag).RemoteContentsLoaded)
            {
                return;
            }

            var parentUrl = (ArdiaUrl)((ArdiaFolderInfo)parentTreeNode.Tag)?.RemoteUrl;

            var result = ArdiaResult<IList<RemoteItem>>.Default;

            using var waitDlg = new LongWaitDlg();
            waitDlg.Text = string.Format(ArdiaResources.OpenFolder_Title, parentTreeNode.Text);
            waitDlg.PerformWork(this, 1000, progressMonitor =>
            {
                result = Client.GetFolders(parentUrl, progressMonitor);
            });

            if (result.IsSuccess)
            {
                AddItemsToFolder(result.Value, parentTreeNode);

                ((ArdiaFolderInfo)parentTreeNode.Tag)!.RemoteContentsLoaded = true;
            }
            else
            {
                string message;
                if (result.ErrorStatusCode == HttpStatusCode.Unauthorized)
                    message = ArdiaResources.Error_InvalidToken;
                else if (parentTreeNode.Parent == null)
                    message = string.Format(ArdiaResources.OpenFolder_Error_Server, Account.ServerUrl);
                else
                    message = string.Format(ArdiaResources.OpenFolder_Error_Folder, parentTreeNode.Text);

                MessageDlg.ShowWithExceptionAndNetworkDetail( this, message, result.ErrorMessage, result.ErrorException);

                Close();
            }
        }

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

        // TODO: re-work how files are read from the temp directory
        private static string[] GatherLocalZipFiles(string fileName)
        {
            var directory = Path.GetDirectoryName(fileName);
            if (directory == null)
                return new string[] {};

            var tmp = Directory.GetFiles(directory);

            var paths = new List<string>(tmp);
            return paths.ToArray();
        }

        public void Upload(Control parent, string fileName)
        {
            var result = ArdiaResult<CreateDocumentResponse>.Default;

            var isCanceled = false;

            if (!SkipPublish)
            {
                var paths = GatherLocalZipFiles(fileName);

                using var waitDlg = new LongWaitDlg();
                waitDlg.Text = UtilResources.PublishDocumentDlg_UploadSharedZipFile_Uploading_File;
                waitDlg.PerformWork(parent, 1000, longWaitBroker =>
                {
                    result = Client.PublishDocument(DestinationPath, paths, longWaitBroker);

                    isCanceled = longWaitBroker.IsCanceled;
                });
            }
            else result = ArdiaResult<CreateDocumentResponse>.Success(new CreateDocumentResponse());

            if (isCanceled)
                return;

            if (result.IsSuccess)
            {
                PublishedDocument = result.Value;

                string successMessage = ArdiaResources.FileUpload_Success_Open_DataExplorer;
                if (MultiButtonMsgDlg.Show(parent, successMessage, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false)
                        == DialogResult.Yes)
                {
                    var getUrlResult = Client.GetDataExplorerUrl(DestinationPath);
                    if (getUrlResult.IsSuccess)
                    {
                        Process.Start(getUrlResult.Value);
                    }
                    else
                    {
                        MessageDlg.ShowWithExceptionAndNetworkDetail(parent, ArdiaResources.Error_StatusCode_Unexpected, getUrlResult.ErrorMessage, getUrlResult.ErrorException);
                    }
                }
            }
            else
            {
                string message;
                if (result.ErrorStatusCode == HttpStatusCode.Unauthorized)
                    message = ArdiaResources.Error_InvalidToken;
                else message = ArdiaResources.FileUpload_Error;

                MessageDlg.ShowWithExceptionAndNetworkDetail(parent, message, result.ErrorMessage, result.ErrorException);
            }
        }

        public TreeNode CreateFolder()
        {
            var parentNode = treeViewFolders.SelectedNode;
            if (parentNode == null)
                return null;

            // avoid highlighting two tree nodes at the same time by de-selecting this node
            treeViewFolders.SelectedNode = null;

            // temporarily enable editing - will be disabled shortly
            treeViewFolders.LabelEdit = true;

            var newFolder = CreateFolderNode(ArdiaResources.CreateFolder_DefaultFolderName);

            // If parent has child nodes, expanding the parent before adding the new child is a better UI experience
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

            return newFolder;
        }

        /// <summary>
        /// Create a new folder on the remote server.
        /// </summary>
        public void TreeViewFolders_AfterLabelEdit(object sender, NodeLabelEditEventArgs args)
        {
            var newFolderName = args.Label;
            var newTreeNode = args.Node;

            var validateResult = ValidateFolderName(newFolderName);

            if (validateResult == ValidateInputResult.valid)
            {
                args.Node.EndEdit(false);
                treeViewFolders.LabelEdit = false;

                var parentUrl = (ArdiaUrl)((ArdiaFolderInfo)newTreeNode.Parent.Tag).RemoteUrl;
                Assume.IsNotNull(parentUrl);

                var childUrl = parentUrl.ChangePathParts(parentUrl.GetPathParts().Concat(new[] { newFolderName }));

                newTreeNode.Tag = new ArdiaFolderInfo(childUrl);
            }
            else
            {
                args.CancelEdit = true;

                var alertMsg = validateResult == ValidateInputResult.invalid_blank ? 
                    ArdiaResources.CreateFolder_InputValidationError_BlankName :
                    string.Format(ArdiaResources.CreateFolder_InputValidationError_IllegalCharacter, new string(ILLEGAL_FOLDER_NAME_CHARS));

                var alertDlg = new AlertDlg(alertMsg, MessageBoxButtons.OKCancel);
                alertDlg.ShowAndDispose(this);

                if (alertDlg.DialogResult == DialogResult.OK)
                {
                    args.Node.Text = args.Label;
                    args.Node.BeginEdit();
                }
                else
                {
                    var parentNode = args.Node.Parent;

                    args.Node.EndEdit(true);
                    args.Node.Remove();
                    treeViewFolders.LabelEdit = false;
                    treeViewFolders.SelectedNode = parentNode;
                }

                return;
            }

            var parentFolderPath = GetFolderPath(newTreeNode.Parent);

            var result = ArdiaResult.Default;

            using var waitDlg = new LongWaitDlg();
            waitDlg.Text = ArdiaResources.CreateFolder_Title;
            waitDlg.PerformWork(this, 1000, longWaitBroker =>
            {
                result = Client.CreateFolder(parentFolderPath, newFolderName, longWaitBroker);
            });

            if (result.IsSuccess)
            {
                treeViewFolders.SelectedNode = newTreeNode;
            }
            else
            {
                string message;
                switch (result.ErrorStatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        message = ArdiaResources.Error_InvalidToken;
                        break;
                    case HttpStatusCode.BadRequest:
                    case HttpStatusCode.Conflict:
                        message = string.Format(ArdiaResources.CreateFolder_InputValidationError_FileAlreadyExists, newFolderName);
                        break;
                    case HttpStatusCode.Forbidden:
                        message = string.Format(ArdiaResources.CreateFolder_Error_Forbidden, newFolderName, parentFolderPath);
                        break;
                    default:
                        message = string.Format(ArdiaResources.CreateFolder_Error, newFolderName);
                        break;
                }

                treeViewFolders.SelectedNode = args.Node.Parent;
                args.Node.Remove();

                MessageDlg.ShowWithExceptionAndNetworkDetail( this, message, result.ErrorMessage, result.ErrorException);
            }
        }

        private void TreeViewFolders_AfterSelect(object sender, TreeViewEventArgs treeViewEventArgs)
        {
            createRemoteFolder.Enabled = treeViewFolders.SelectedNode != null;
        }

        private void TreeViewFolders_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            ExpandFolder(e.Node);
        }

        private void CreateRemoteFolder_Click(object sender, EventArgs eventArgs)
        {
            CreateFolder();
        }

        public override string GetFolderPath(string folderPath)
        {
            var baseFolderPath = base.GetFolderPath(folderPath);

            // Ardia paths differ from what the baseclass provides with GetFolderPath. 
            //   (1) Add a leading slash ('/')
            //   (2) Remove any trailing slash ('/')
            return ArdiaClient.URL_PATH_SEPARATOR + baseFolderPath?.TrimEnd('/');
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
        /// Validate <param name="folderName"></param> meets these criteria for a valid folder name:
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