/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using System.Collections.Generic;
using System;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using System.Linq;
using System.Drawing;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.FileUI
{
    public class PublishDocumentDlgPanorama : PublishDocumentDlgBase
    {
        /// <summary>
        /// Enum of images used in the server tree, in index order.
        /// </summary>
        private enum ImageId
        {
            panorama,
            labkey,
            chrom_lib,
            folder
        }

        private readonly SettingsList<Server> _panoramaServers;
        private readonly List<Server> _anonymousServers;

        public PublishDocumentDlgPanorama(
            IDocumentUIContainer docContainer,
            SettingsList<Server> servers,
            string fileName,
            DocumentFormat? fileFormatOnDisk) : base(docContainer, fileName, fileFormatOnDisk)
        {
            _panoramaServers = servers;
            tbFilePath.Text = FileTimeEx.GetTimeStampedFileName(fileName);
            _anonymousServers = new List<Server>(servers.Where(server => !server.HasUserAccount()));

            treeViewFolders.ImageList.Images.Add(Resources.Panorama);   // 24bpp
            treeViewFolders.ImageList.Images.Add(Resources.LabKey);     // 8bpp
            treeViewFolders.ImageList.Images.Add(Resources.ChromLib);   // 8bpp
            treeViewFolders.ImageList.Images.Add(Resources.Folder);     // 32bpp

            cbAnonymousServers.Visible = _anonymousServers.Count > 0;
        }

        /// <summary>
        /// Creates and initializes a PublishDocumentDlgPanorama by loading server folder data.
        /// Returns null if the user cancels or if an error occurs during loading.
        /// </summary>
        /// <param name="docContainer">Document container</param>
        /// <param name="servers">List of Panorama servers</param>
        /// <param name="fileName">Document file name</param>
        /// <param name="fileFormatOnDisk">Document format</param>
        /// <param name="parent">Parent control for progress dialog</param>
        /// <param name="publishClient">Optional test client for dependency injection (null uses default clients)</param>
        /// <returns>Initialized dialog, or null if canceled/failed</returns>
        public static PublishDocumentDlgPanorama Create(
            IDocumentUIContainer docContainer,
            SettingsList<Server> servers,
            string fileName,
            DocumentFormat? fileFormatOnDisk,
            Control parent,
            IPanoramaPublishClient publishClient = null)
        {
            var dialog = new PublishDocumentDlgPanorama(docContainer, servers, fileName, fileFormatOnDisk);

            // Set test client BEFORE loading data (critical for testing seam)
            dialog.PanoramaPublishClient = publishClient;

            var listServerFolders = new List<ServerFolders>();

            try
            {
                using var waitDlg = new LongWaitDlg();
                waitDlg.Text = FileUIResources.PublishDocumentDlg_PublishDocumentDlg_Load_Retrieving_information_on_servers;
                var status = waitDlg.PerformWork(parent, 800, progressMonitor =>
                {
                    // Set initial message with indeterminate progress (marquee/busy-wait)
                    progressMonitor.UpdateProgress(new ProgressStatus(PanoramaClient.Properties.Resources.PanoramaFolderBrowser_InitializeServers_Requesting_remote_server_folders).ChangePercentComplete(-1));
                    dialog.PublishDocumentDlgLoad(listServerFolders, progressMonitor);
                });

                if (status.IsCanceled)
                    return null; // User canceled - don't show dialog
            }
            catch (Exception x)
            {
                // Use ExceptionUtil for proper defect vs user error handling
                ExceptionUtil.DisplayOrReportException(parent, x);
                return null; // Error occurred - don't show dialog
            }

            // Populate the dialog with loaded data
            dialog.PopulateServerTree(listServerFolders);

            return dialog;
        }

        public IPanoramaPublishClient PanoramaPublishClient { get; set; }
        public ShareType ShareType { get; private set; }
        public bool ShowAnonymousServers { get { return cbAnonymousServers.Checked; } set { cbAnonymousServers.Checked = value; } }

        internal override string LoadExpansionAndSelection()
        {
            return Settings.Default.PanoramaServerExpansion;
        }

        internal override void SaveExpansionAndSelection()
        {
            Settings.Default.PanoramaServerExpansion = ServerTreeStateRestorer.GetPersistentString();
        }

        internal override void HandleDialogLoad()
        {
            // Only UI setup now - data already loaded via Create() factory method
            cbAnonymousServers.CheckedChanged += cbAnonymousServers_CheckedChanged;
            lblServerFolders.Text = FileUIResources.PublishDocumentDlg_Panorama_ServerFolders;
            lblAvailableStorage.Visible = false;
        }

        private void PopulateServerTree(List<ServerFolders> listServerFolders)
        {
            foreach (var serverFolder in listServerFolders)
            {
                var server = serverFolder.Server;
                var treeNode = new TreeNode(server.GetKey()) { Tag = new FolderInformation(server, false) };
                treeViewFolders.Nodes.Add(treeNode);
                if (serverFolder.FoldersJson != null)
                    AddSubFolders(server, treeNode, serverFolder.FoldersJson);
            }
        }

        internal override void HandleDialogOk()
        {
            var folderInfo = treeViewFolders.SelectedNode.Tag as FolderInformation;
            if (folderInfo == null)
            {
                MessageDlg.Show(this, FileUIResources.PublishDocumentDlg_UploadSharedZipFile_Error_obtaining_server_information);
                return;
            }
            if (!folderInfo.HasWritePermission)
            {
                MessageDlg.Show(this, Resources.PublishDocumentDlg_UploadSharedZipFile_You_do_not_have_permission_to_upload_to_the_given_folder);
                return;
            }

            // If a test client was provided in SkylineWindow.ShowPublishDlg(IPanoramaPublishClient publishClient), use that.
            // Otherwise, create a client for the selected server.
            PanoramaPublishClient ??= GetDefaultPublishClient(folderInfo.Server);

            try
            {
                var cancelled = false;
                ShareType = PanoramaPublishClient.GetShareType(DocumentUIContainer.DocumentUI, DocumentUIContainer.DocumentFilePath, DocumentFormat, this, ref cancelled);
                if (cancelled)
                {
                    return;
                }
            }
            catch (PanoramaServerException panoramaServerException)
            {
                MessageDlg.ShowWithException(this, panoramaServerException.Message, panoramaServerException);
                return;
            }

            Assume.IsNotNull(ShareType);

            DialogResult = DialogResult.OK;
        }

        internal override void HandleUpload(Control parent, string destinationFolder, string localZipFilePath)
        {
            var folderInfo = treeViewFolders.SelectedNode.Tag as FolderInformation;
            if (folderInfo != null)
            {
                PanoramaPublishClient ??= GetDefaultPublishClient(folderInfo.Server);
                PanoramaPublishClient.UploadSharedZipFile(parent, localZipFilePath, destinationFolder);
            }
        }

        private void PublishDocumentDlgLoad(List<ServerFolders> listServerFolders, IProgressMonitor progressMonitor)
        {
            var listErrorServers = new List<ServerError>();
            var serversWithAccounts = _panoramaServers.Where(s => s.HasUserAccount()).ToList();
            IProgressStatus progressStatus = new ProgressStatus(PanoramaClient.Properties.Resources.PanoramaFolderBrowser_InitializeServers_Requesting_remote_server_folders);
            
            for (int i = 0; i < serversWithAccounts.Count; i++)
            {
                var server = serversWithAccounts[i];
                
                // Update progress as we iterate through servers after the first server
                if (i > 0)
                {
                    progressStatus = progressStatus.ChangePercentComplete(i * 100 / serversWithAccounts.Count);
                    progressMonitor.UpdateProgress(progressStatus);
                }
                
                // Check for cancellation
                if (progressMonitor.IsCanceled)
                    throw new OperationCanceledException();

                JToken folders = null;
                try
                {
                    // Create a client for the server if we were not given one in SkylineWindow.ShowPublishDlg(IPanoramaPublishClient publishClient).
                    var panoramaClient = PanoramaPublishClient != null
                        ? PanoramaPublishClient.PanoramaClient
                        : GetDefaultPublishClient(server).PanoramaClient;

                    // Request folders from server, using its FolderPath which may be null for the entire server
                    folders = panoramaClient.GetInfoForFolders(server.FolderPath, progressMonitor, progressStatus);

                    // If FolderPath is set, wrap the response to include the folder path as the root node
                    // This ensures the TreeView displays the folder (e.g., "MacCoss") even though
                    // the server only returned its children
                    if (!string.IsNullOrEmpty(server.FolderPath) && folders != null)
                    {
                        folders = LKContainerBrowser.WrapFolderResponse(folders, server.FolderPath, server.URI);
                    }
                }
                catch (Exception ex)
                {
                    // PanoramaClient uses HttpClientWithProgress, which throws NetworkRequestException
                    if (ex is NetworkRequestException || ex is PanoramaServerException)
                    {
                        var error = ex.Message;
                        if (error != null && error.Contains(PanoramaClient.Properties.Resources
                                .UserState_GetErrorMessage_The_username_and_password_could_not_be_authenticated_with_the_panorama_server_))
                        {
                            error = TextUtil.LineSeparate(error, FileUIResources
                                .PublishDocumentDlg_PublishDocumentDlgLoad_Go_to_Tools___Options___Panorama_tab_to_update_the_username_and_password_);

                        }

                        listErrorServers.Add(new ServerError(server, error ?? string.Empty));
                    }
                    else
                    {
                        throw;
                    }
                }
                listServerFolders.Add(new ServerFolders(server, folders));
            }
            
            if (listErrorServers.Count > 0)
            {
                throw new PanoramaServerException(TextUtil.LineSeparate(
                    FileUIResources.PublishDocumentDlg_PublishDocumentDlgLoad_Failed_attempting_to_retrieve_information_from_the_following_servers_,
                    string.Empty,
                    ServersToString(listErrorServers)));
            }
        }

        private static string ServersToString(IEnumerable<ServerError> serverErrors)
        {
            return TextUtil.LineSeparate(serverErrors.Select(t => t.ToString()));
        }

        private void AddSubFolders(Server server, TreeNode node, JToken folder)
        {
            try
            {
                AddChildContainers(server, node, folder);
            }
            catch (Exception x)
            {
                var message = 
                    TextUtil.LineSeparate(FileUIResources.PublishDocumentDlg_addSubFolders_Error_retrieving_server_folders, x.Message);
                MessageDlg.ShowWithException(this, message, x);
            }
        }

        public static void AddChildContainers(Server server, TreeNode node, JToken folder)
        {
            JEnumerable<JToken> subFolders = folder[@"children"].Children();
            foreach (var subFolder in subFolders)
            {
                string folderName = (string)subFolder[@"name"];

                TreeNode folderNode = new TreeNode(folderName);
                AddChildContainers(server, folderNode, subFolder);

                // User can only upload to folders where TargetedMS is an active module.
                var canUpload = PanoramaUtil.HasUploadPermissions(subFolder) && PanoramaUtil.HasTargetedMsModule(subFolder);

                // If the user does not have write permissions in this folder or any
                // of its subfolders, do not add it to the tree.
                if (folderNode.Nodes.Count == 0 && !canUpload)
                {
                    continue;
                }

                node.Nodes.Add(folderNode);

                // User cannot upload files to folder
                if (!canUpload)
                {
                    folderNode.ForeColor = Color.Gray;
                    folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.folder;
                }
                else
                {
                    JToken moduleProperties = subFolder[@"moduleProperties"];
                    if (moduleProperties == null)
                        folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.labkey;
                    else
                    {
                        string effectiveValue = (string)moduleProperties[0]![@"effectiveValue"];
                        folderNode.ImageIndex =
                            folderNode.SelectedImageIndex =
                            (effectiveValue!.Equals(@"Library") || effectiveValue.Equals(@"LibraryProtein"))
                                ? (int)ImageId.chrom_lib
                                : (int)ImageId.labkey;
                    }
                }

                folderNode.Tag = new FolderInformation(server, canUpload);
            }
        }

        private void cbAnonymousServers_CheckedChanged(object sender, EventArgs e)
        {
            if (ShowAnonymousServers)
            {
                foreach (var server in _anonymousServers)
                {
                    var treeNode = new TreeNode(server.GetKey())
                    {
                        Tag = new FolderInformation(server, false),
                        ForeColor = Color.Gray
                    };
                    treeViewFolders.Nodes.Add(treeNode);
                }
            }
            else
            {
                var anonymousServerCount = _anonymousServers.Count;
                for (var iNode = treeViewFolders.Nodes.Count - 1;
                     iNode >= 0 && anonymousServerCount > 0;
                     iNode--, anonymousServerCount--)
                {
                    treeViewFolders.Nodes.RemoveAt(iNode);
                }
            }
        }

        public List<string> GetServers()
        {
            return new List<string>(treeViewFolders.Nodes.Cast<TreeNode>().Select(node => node.Text));
        }

        public static IPanoramaPublishClient GetDefaultPublishClient(PanoramaServer server)
        {
            return new WebPanoramaPublishClient(server.URI, server.Username, server.Password);
        }

        private class ServerFolders
        {
            public Server Server { get; }
            public JToken FoldersJson { get; }

            public ServerFolders(Server server, JToken foldersJson)
            {
                Server = server;
                FoldersJson = foldersJson;
            }
        }

        internal class ServerError
        {
            private Server _server;
            private string _errorMessage;

            public ServerError(Server server, string errorMessage)
            {
                _server = server;
                _errorMessage = errorMessage;
            }

            public override string ToString()
            {
                return TextUtil.LineSeparate(_server.URI.ToString(), _errorMessage);
            }
        }
    }
}