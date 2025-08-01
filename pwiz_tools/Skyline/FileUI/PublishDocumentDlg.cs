﻿/*
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class PublishDocumentDlg : FormEx
    {
        private readonly IDocumentUIContainer _docContainer;
        private readonly SettingsList<Server> _panoramaServers;
        private readonly DocumentFormat? _fileFormatOnDisk;
        private readonly List<Server> _anonymousServers;
        public IPanoramaPublishClient PanoramaPublishClient { get; set; }
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Enum of images used in the server tree, in index order.
        /// </summary>
        public enum ImageId
        {
            panorama,
            labkey,
            chrom_lib,
            folder
        }

        public PublishDocumentDlg(IDocumentUIContainer docContainer, SettingsList<Server> servers, string fileName, DocumentFormat? fileFormatOnDisk)
        {
            IsLoaded = false;
            InitializeComponent();
            Icon = Resources.Skyline;

            _docContainer = docContainer;
            _fileFormatOnDisk = fileFormatOnDisk;

            _panoramaServers = servers;
            tbFilePath.Text = FileTimeEx.GetTimeStampedFileName(fileName);

            treeViewFolders.ImageList = new ImageList { ColorDepth = ColorDepth.Depth32Bit };
            treeViewFolders.ImageList.Images.Add(Resources.Panorama);   // 24bpp
            treeViewFolders.ImageList.Images.Add(Resources.LabKey);     // 8bpp
            treeViewFolders.ImageList.Images.Add(Resources.ChromLib);   // 8bpp
            treeViewFolders.ImageList.Images.Add(Resources.Folder);     // 32bpp

            ServerTreeStateRestorer = new TreeViewStateRestorer(treeViewFolders);

            _anonymousServers = new List<Server>(servers.Where(server => !server.HasUserAccount()));
            cbAnonymousServers.Visible = _anonymousServers.Count > 0;
        }

        public string FileName { get { return tbFilePath.Text; } }
        public ShareType ShareType { get; set; }

        public bool ShowAnonymousServers { get { return cbAnonymousServers.Checked; } set { cbAnonymousServers.Checked = value; } }


        private void PublishDocumentDlg_Load(object sender, EventArgs e)
        {
            var listServerFolders = new List<ServerFolders>();

            try
            {
                using (var waitDlg = new LongWaitDlg())
                {
                    waitDlg.Text = FileUIResources.PublishDocumentDlg_PublishDocumentDlg_Load_Retrieving_information_on_servers;
                    waitDlg.PerformWork(this, 800, () => PublishDocumentDlgLoad(listServerFolders));
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowException(this, x);
            }

            foreach (var serverFolder in listServerFolders)
            {
                var server = serverFolder.Server;
                var treeNode = new TreeNode(server.GetKey()) { Tag = new FolderInformation(server, false) };
                treeViewFolders.Nodes.Add(treeNode);
                if (serverFolder.FoldersJson != null)
                    AddSubFolders(server, treeNode, serverFolder.FoldersJson);
            }

            ServerTreeStateRestorer.RestoreExpansionAndSelection(Settings.Default.PanoramaServerExpansion);
            ServerTreeStateRestorer.UpdateTopNode();

            IsLoaded = true;
        }

        private void PublishDocumentDlgLoad(List<ServerFolders> listServerFolders)
        {
            var listErrorServers = new List<ServerError>();
            foreach (var server in _panoramaServers)
            {
                if (!server.HasUserAccount())
                {
                    // User has to be logged in to be able to upload a document to the server.
                    continue;
                }

                JToken folders = null;
                try
                {
                    // Create a client for the server if we were not given one in SkylineWindow.ShowPublishDlg(IPanoramaPublishClient publishClient).
                    var panoramaClient = PanoramaPublishClient != null
                        ? PanoramaPublishClient.PanoramaClient
                        : GetDefaultPublishClient(server).PanoramaClient;
                    folders = panoramaClient.GetInfoForFolders(null);
                }
                catch (Exception ex)
                {
                    if (ex is WebException || ex is PanoramaServerException)
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
                throw new Exception(TextUtil.LineSeparate(FileUIResources.PublishDocumentDlg_PublishDocumentDlgLoad_Failed_attempting_to_retrieve_information_from_the_following_servers_,
                                                          string.Empty,
                                                          ServersToString(listErrorServers)));
            }
        }

        private string ServersToString(IEnumerable<ServerError> serverErrors)
        {
            return TextUtil.LineSeparate(serverErrors.Select(t => t.ToString()));
        }

        private class ServerError
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

        private TreeViewStateRestorer ServerTreeStateRestorer { get; set; }

        private void SaveServerTreeExpansion()
        {
            Settings.Default.PanoramaServerExpansion = ServerTreeStateRestorer.GetPersistentString();
        }

        private void AddSubFolders(Server server, TreeNode node, JToken folder)
        {
            try
            {
                AddChildContainers(server, node, folder);
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, TextUtil.LineSeparate(FileUIResources.PublishDocumentDlg_addSubFolders_Error_retrieving_server_folders,
                                                            x.Message), x);
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
                    folderNode.ImageIndex = folderNode.SelectedImageIndex = (int) ImageId.folder;
                }
                else
                {
                    JToken moduleProperties = subFolder[@"moduleProperties"];
                    if (moduleProperties == null)
                        folderNode.ImageIndex = folderNode.SelectedImageIndex = (int) ImageId.labkey;
                    else
                    {
                        string effectiveValue = (string) moduleProperties[0][@"effectiveValue"];
                        folderNode.ImageIndex =
                            folderNode.SelectedImageIndex =
                            (effectiveValue.Equals(@"Library") || effectiveValue.Equals(@"LibraryProtein"))
                                ? (int)ImageId.chrom_lib
                                : (int)ImageId.labkey;
                    }
                }

                folderNode.Tag = new FolderInformation(server, canUpload);
            } 
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (treeViewFolders.SelectedNode == null || treeViewFolders.SelectedNode.Level == 0)
            {
                // Prompt the user to select a folder if no node is selected or only the top-level node
                // (the server name) is selected.
                MessageDlg.Show(this, Resources.PublishDocumentDlg_OkDialog_Please_select_a_folder);
                return;
            }

            FolderInformation folderInfo = treeViewFolders.SelectedNode.Tag as FolderInformation;
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
                ShareType = PanoramaPublishClient.GetShareType(_docContainer.DocumentUI,
                    _docContainer.DocumentFilePath, _fileFormatOnDisk, this, ref cancelled);
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

        public void Upload(Control parent)
        {
            string folderPath = GetFolderPath(treeViewFolders.SelectedNode);
            var zipFilePath = tbFilePath.Text;
            FolderInformation folderInfo = treeViewFolders.SelectedNode.Tag as FolderInformation;
            if (folderInfo != null)
            {
                PanoramaPublishClient ??= GetDefaultPublishClient(folderInfo.Server);
                PanoramaPublishClient.UploadSharedZipFile(parent, zipFilePath, folderPath);
            }
        }

        public static IPanoramaPublishClient GetDefaultPublishClient(PanoramaServer server)
        {
            return new WebPanoramaPublishClient(server.URI, server.Username, server.Password);
        }

        private string GetFolderPath(TreeNode folderNode)
        {
            string nodePath = folderNode.FullPath;
            // ReSharper disable LocalizableElement
            string[] folderPathSegments = nodePath.Split(new[] {"\\"}, StringSplitOptions.RemoveEmptyEntries);
            // ReSharper restore LocalizableElement

            string folderPath = string.Empty;
            // First segment is server name. 
            for (int i = 1; i < folderPathSegments.Length; i++)
            {
                folderPath += folderPathSegments[i] + @"/";
            }
            return folderPath;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.InitialDirectory = Settings.Default.LibraryDirectory;
                dlg.SupportMultiDottedExtensions = true;
                dlg.DefaultExt = SrmDocumentSharing.EXT_SKY_ZIP;
                dlg.Filter = TextUtil.FileDialogFiltersAll(
                    FileUIResources.PublishDocumentDlg_btnBrowse_Click_Skyline_Shared_Documents,
                    SrmDocumentSharing.EXT);
                dlg.FileName = tbFilePath.Text;
                dlg.Title = FileUIResources.PublishDocumentDlg_btnBrowse_Click_Upload_Document;
                if (dlg.ShowDialog(Parent) == DialogResult.OK)
                {
                    tbFilePath.Text = dlg.FileName;
                }
            }
        }

        private TreeNode FindNode(TreeNode node, string item)
        {
            if (node.Text == item)
                return node;
            else
            {
                foreach (TreeNode childNode in node.Nodes)
                {
                    TreeNode nodeFound = FindNode(childNode, item);
                    if (nodeFound != null)
                        return nodeFound;
                }
            }
            return null;
        }

        public void SelectItem(string item)
        {
            foreach (TreeNode node in treeViewFolders.Nodes)
            {
                TreeNode selectedNode = FindNode(node, item);
                if (selectedNode != null)
                {
                    treeViewFolders.SelectedNode = selectedNode;
                    return;
                }
            }
        }

        public string GetSelectedNodeText()
        {
            return treeViewFolders.SelectedNode != null ? treeViewFolders.SelectedNode.Text : string.Empty;
        }

        private void treeViewFolders_DoubleClick(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void PublishDocumentDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveServerTreeExpansion();
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

        public bool CbAnonymousServersVisible => cbAnonymousServers.Visible;

        public List<string> GetServers()
        {
            return new List<string>(treeViewFolders.Nodes.Cast<TreeNode>().Select(node => node.Text));
        }
    }
}
