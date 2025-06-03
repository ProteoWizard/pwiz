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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Ardia;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public class PublishDocumentDlgArdia : PublishDocumentDlgBase
    {
        // RFC 3986 specified path separator for URIs
        // CONSIDER: add UrlBuilder class to Skyline. Related PRs also define this constant and helper methods narrowly scoped to a remote server vendor
        private const string URL_PATH_SEPARATOR = @"/";

        private enum ImageId { ardia, folder, empty }

        private readonly IList<ArdiaAccount> _ardiaAccounts;
        // TODO: support multiple Ardia accounts
        private readonly ArdiaSession _ardiaSession;

        private RemoteUrl _contentsAvailable_RemoteUrl;
        private TreeNode _contentsAvailable_TreeNode;

        // TODO: change signature (from RemoteAccount to ArdiaAccount) when old way of choosing directory is removed
        public PublishDocumentDlgArdia(
            IDocumentUIContainer docContainer,
            IList<RemoteAccount> accounts,
            string fileName,
            DocumentFormat? fileFormatOnDisk) : base(docContainer, fileName, fileFormatOnDisk)
        {
            _ardiaAccounts = accounts.Cast<ArdiaAccount>().ToList();
            _ardiaSession = (ArdiaSession)_ardiaAccounts[0].CreateSession();
            _ardiaSession.ContentsAvailable += ArdiaSession_ContentsAvailableWrapper;

            // TODO: update icons for Ardia rather than Panorama
            treeViewFolders.ImageList.TransparentColor = Color.Magenta;
            treeViewFolders.ImageList.Images.Add(ArdiaResources.ArdiaIcon); // 32bpp
            treeViewFolders.ImageList.Images.Add(Resources.Folder);         // 32bpp
            treeViewFolders.ImageList.Images.Add(Resources.Blank);          // 32bpp
        }

        internal override void HandleDialogLoad()
        {
            // Do not show - Ardia does not support anon servers
            cbAnonymousServers.Visible = false;

            // TODO: implement [Create Folder] button
            createRemoteFolder.Visible = true;
            createRemoteFolder.Click += CreateRemoteFolder_Click;

            // Panorama directory browser ignores single click events, so Ardia ignores them
            // too and only expands a folder with a click on the expand (plus sign) icon
            treeViewFolders.BeforeExpand += TreeViewFolders_BeforeExpand;

            treeViewFolders.BeginUpdate();
            foreach (var account in _ardiaAccounts)
            {
                // CONSIDER: add username to label? To distinguish multiple Ardia accounts with the same server.
                // Get the URL for the root (/) directory of the Ardia server
                var rootUrl = account.GetRootUrl();

                var treeNode = new TreeNode(account.ServerUrl)
                {
                    Tag = new ArdiaFolderInfo(rootUrl),
                    ImageIndex = (int)ImageId.ardia,
                    SelectedImageIndex = (int)ImageId.ardia
                };

                // Assume the server has sub-folders and add this node so the TreeView's expand
                // icon appears without having actually called the Ardia API to get the child
                // nodes. SrmTreeNode does something similar.
                treeNode.Nodes.Add(CreateEmptyNode());

                treeViewFolders.Nodes.Add(treeNode);
            }
            treeViewFolders.EndUpdate();
        }

        private void ExpandFolder(TreeNode parentTreeNode) 
        {
            var parentUrl = (ArdiaUrl)(parentTreeNode.Tag as ArdiaFolderInfo)?.RemoteUrl;

            // Reuse results if they were already fetched from the API during this remote session.
            // Also skip re-rendering TreeView nodes for these files should already exist in the tree.
            if (_ardiaSession.HasResultsFor(parentUrl))
                return;

            treeViewFolders.Cursor = Cursors.WaitCursor;

            _contentsAvailable_RemoteUrl = parentUrl;
            _contentsAvailable_TreeNode = parentTreeNode;

            // TODO: improve "waiting for network response" screen. Wrap each call in LongWaitDlg? But 
            //       does LongWaitDlg work with RemoteSession's different callback model?
            // TODO: error handling!
            _ardiaSession.AsyncFetchContents(parentUrl, out _);
        }

        // NB: RemoteSession's programming model uses a generic event fired anytime the session returns
        //     results from a remote API. Callers are responsible for obtaining the correct response
        //     using the requested URL, which means holding onto state often in class-level variables.
        //     This differs from alternative models - for example, providing an Action closed over local
        //     variables or working in a separate thread / invoke.
        //     
        //     An alternative might be register / un-register a RemoteSession.ContentsAvailable event handler
        //     for each network request? This might look like:
        // 
        //          _ardiaSession.AsyncFetchContents(parentUrl, out var exception);
        //          _ardiaSession.ContentsAvailable += Action;
        //          return;
        //
        //          void Action()
        //          {
        //              CommonActionUtil.SafeBeginInvoke(this,() => {
        //                  ArdiaSession_ContentsAvailable(parentUrl, parentTreeNode);
        //              });
        //              _ardiaSession.ContentsAvailable -= Action;
        //          }
        // 
        //     This works but seems strange and error-prone.
        //
        // CONSIDER: add a new programming model to RemoteSession
        private void ArdiaSession_ContentsAvailableWrapper()
        {
            CommonActionUtil.SafeBeginInvoke(this, () =>
            {
                ArdiaSession_ContentsAvailable(_contentsAvailable_RemoteUrl, _contentsAvailable_TreeNode);
            });
        }

        private void ArdiaSession_ContentsAvailable(RemoteUrl parentUrl, TreeNode parentTreeNode)
        {
            try
            {
                // TODO: does Ardia API include whether the current user has permission to upload to a given directory?
                // TODO: configure properties of new TreeNode - ForeColor, ImageIndex
                // CONSIDER: items are sorted lexicographically. Consider adding other / more sort options?
                // CONSIDER: can Ardia API include whether the current user has permission to upload to a given directory?
                // CONSIDER: can Ardia API include whether a given directory includes 1+ folders? The current "hasChildren" flag 
                //           is true when the directory contains only files - but Skyline doesn't know that without making the 
                //           API call
                var remoteItems = _ardiaSession.ListContents(parentUrl).ToList();
                remoteItems.Sort((item1, item2) => string.Compare(item1.Label, item2.Label, StringComparison.CurrentCultureIgnoreCase));

                treeViewFolders.BeginUpdate();
                parentTreeNode.Nodes.Clear();

                foreach (var remoteItem in remoteItems)
                {
                    if (!DataSourceUtil.IsFolderType(remoteItem.Type))
                        continue;

                    var folderName = remoteItem.Label;
                    // For more info on this cast, see ArdiaSession @ ListContents
                    var folderUrl = remoteItem.MsDataFileUri as ArdiaUrl; 
                    var folderHasChildren = remoteItem.HasChildren;

                    var childTreeNode = new TreeNode(folderName)
                    {
                        Tag = new ArdiaFolderInfo(folderUrl),
                        ImageIndex = (int)ImageId.folder,
                        SelectedImageIndex = (int)ImageId.folder
                    };

                    // If this node has sub-folders, add a node so the expand icon appears without 
                    // actually having added child nodes. SrmTreeNode does something similar.
                    if (folderHasChildren)
                    {
                        childTreeNode.Nodes.Add(CreateEmptyNode());
                    }

                    parentTreeNode.Nodes.Add(childTreeNode);
                    parentTreeNode.Expand();
                }
            }
            finally
            {
                treeViewFolders.EndUpdate();
                treeViewFolders.Cursor = Cursors.Default;
            }
        }

        internal override void HandleDialogOk()
        {
            // CONSIDER: Panorama sets the ShareType using ShareTypeDlg. Do the same here? For now, use the default.
            ShareType = ShareType.DEFAULT;
            DialogResult = DialogResult.OK;
        }

        public override void Upload(Control parent)
        {
            var selectedTreeNode = treeViewFolders.SelectedNode;

            // CONSIDER: support multiple Ardia accounts?
            var ardiaAccount = _ardiaSession.ArdiaAccount;
            var destinationFolderPath = GetFolderPath(selectedTreeNode);
            var localZipFilePath = tbFilePath.Text;

            // NB: Fixup destination folder path for Ardia API. Necessary because Ardia API accepts different
            //     leading / trailing slashes from Panorama.
            destinationFolderPath = URL_PATH_SEPARATOR + destinationFolderPath;
            if (destinationFolderPath.EndsWith(@"/"))
                destinationFolderPath = destinationFolderPath.Substring(0, destinationFolderPath.Length - 1);

            var message = string.Format(ArdiaResources.Ardia_FileUpload_ConfirmUploadToPath, Path.GetFileName(localZipFilePath), destinationFolderPath);
            MessageDlg.Show(this, message);

            var isCanceled = false;
            using var waitDlg = new LongWaitDlg();
            waitDlg.Text = UtilResources.PublishDocumentDlg_UploadSharedZipFile_Uploading_File;
            waitDlg.PerformWork(this, 1000, longWaitBroker =>
            {
                var ardiaClient = ArdiaClient.Instance(ardiaAccount);
                ardiaClient.SendZipFile(destinationFolderPath, localZipFilePath, longWaitBroker, out _);

                if (longWaitBroker.IsCanceled)
                    isCanceled = true;
            });

            if (!isCanceled)
            {
                // CONSIDER: Ardia API could respond with the URL of the uploaded file in Ardia Data Explorer and Skyline
                //           could offer to open that file in the browser. Panorama does similar.
                MessageDlg.Show(this, ArdiaResources.Ardia_FileUpload_SuccessfulUpload);
            }
        }

        private void TreeViewFolders_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            ExpandFolder(e.Node);
        }

        private void CreateRemoteFolder_Click(object sender, EventArgs eventArgs)
        {
            MessageDlg.Show(this, $@"TODO: Create remote folder");
        }

        private static TreeNode CreateEmptyNode()
        {
            return new TreeNode(@"        ")
            {
                ImageIndex = (int)ImageId.empty
            };
        }
    }

    internal class ArdiaFolderInfo
    {
        internal ArdiaFolderInfo(RemoteUrl remoteUrl)
        {
            RemoteUrl = remoteUrl;
        }

        internal RemoteUrl RemoteUrl { get; }
    }
}