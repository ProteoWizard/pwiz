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

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class PublishDocumentDlg : FormEx
    {
        private readonly SettingsList<Server> _panoramaServers;
        public static LongWaitDlg WaitDlg { get; set; }
        public IPanoramaPublishClient PanoramaPublishClient { get; set; }
        public bool IsLoaded { get; set; }

        public PublishDocumentDlg(SettingsList<Server> servers, string fileName)
        {
            IsLoaded = false;
            InitializeComponent();
            Icon = Resources.Skyline;

            _panoramaServers = servers;
            tbFilePath.Text = fileName;
        }

        private void PublishDocumentDlg_Load(object sender, EventArgs e)
        {
            var listServerFolders = new List<KeyValuePair<Server, JToken>>();

            try
            {
                WaitDlg = new LongWaitDlg
                              {
                                  Text =
                                      Resources.
                                      PublishDocumentDlg_PublishDocumentDlg_Load_Retrieving_information_on_servers
                              };
                WaitDlg.PerformWork(this, 1000, () => PublishDocumentDlgLoad(listServerFolders));
            }
            catch (Exception x)
            {
                MessageDlg.Show(this, x.Message);
            }

            foreach (var serverFolder in listServerFolders)
            {
                var server = serverFolder.Key;
                TreeNode treeNode = new TreeNode(server.URI.ToString()) {Tag = new FolderInformation(server, false)};
                treeViewFolders.Nodes.Add(treeNode);
                if (serverFolder.Value != null)
                    addSubFolders(server, treeNode, serverFolder.Value);
            }
            IsLoaded = true;
        }

        private void PublishDocumentDlgLoad(List<KeyValuePair<Server, JToken>> listServerFolders)
        {
            if (PanoramaPublishClient == null)
                PanoramaPublishClient = new WebPanoramaPublishClient();
            var listErrorServers = new List<Server>();
            foreach (var server in _panoramaServers)
            {
                JToken folders = null;
                try
                {
                    folders = PanoramaPublishClient.GetInfoForFolders(server);
                }
                catch (WebException)
                {
                    listErrorServers.Add(server);
                }
                listServerFolders.Add(new KeyValuePair<Server, JToken>(server, folders));

            }
            if (listErrorServers.Count > 0)
            {
                throw new Exception(string.Format(
                    Resources.
                        PublishDocumentDlg_PublishDocumentDlgLoad_Error_attempting_to_retreive_server_information_on_the_following_servers__0__,
                    ServersToString(listErrorServers)));
            }
        }

        private string ServersToString(List<Server> servers)
        {
            string message = string.Empty;
            if (servers.Count == 0)
                return string.Empty;
            else if (servers.Count == 1)
                return servers[0].URI.ToString();
            else
            {
                for (int i = 0; i < servers.Count - 1; i++)
                {
                    message += servers[i].URI + ", "; // Not L10N
                }
                message += string.Format(Resources.PublishDocumentDlg_ServersToString_and__0__,
                                         servers[servers.Count - 1].URI);
            }
            return message;
        }

        private class FolderInformation
        {
            private readonly Server _server;
            private readonly bool _hasWritePermission;

            public FolderInformation(Server server, bool hasWritePermission)
            {
                _server = server;
                _hasWritePermission = hasWritePermission;
            }

            public Server Server
            {
                get { return _server; }
            }

            public bool HasWritePermission
            {
                get { return _hasWritePermission; }
            }
        }

        private void addSubFolders(Server server, TreeNode node, JToken folder)
        {
            try
            {
                JEnumerable<JToken> subFolders = folder["children"].Children(); // Not L10N
                foreach (var subFolder in subFolders)
                {
                    string folderName = (string) subFolder["name"]; // Not L10N
                    int userPermissions = (int) subFolder["userPermissions"]; // Not L10N

                    // Do not show folders user doesn't have read permissions for
                    if (!Equals(userPermissions & 1, 1))
                        return;

                    TreeNode folderNode = new TreeNode(folderName);
                    node.Nodes.Add(folderNode);

                    // User can only upload to folders where TargetedMS is an active module.
                    JToken modules = subFolder["activeModules"]; // Not L10N
                    bool canUpload = ContainsMS2Module(modules) && Equals(userPermissions & 2, 2);

                    // User cannot upload files to folder
                    if (!canUpload)
                        folderNode.ForeColor = Color.Gray;

                    folderNode.Tag = new FolderInformation(server, canUpload);
                    addSubFolders(server, folderNode, subFolder);
                }
            }
            catch (Exception x)
            {
                MessageDlg.Show(this,
                                TextUtil.LineSeparate(
                                    Resources.PublishDocumentDlg_addSubFolders_Error_retrieving_server_folders,
                                    x.Message));
            }
        }

        private bool ContainsMS2Module(IEnumerable<JToken> modules)
        {
            foreach (var module in modules)
            {
                if (string.Equals(module.ToString(), "TargetedMS")) // Not L10N
                    return true;
            }
            return false;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            FolderInformation folderInfo = treeViewFolders.SelectedNode.Tag as FolderInformation;
            if (folderInfo == null)
            {
                MessageDlg.Show(this,
                                Resources.PublishDocumentDlg_UploadSharedZipFile_Error_obtaining_server_information);
                return;
            }
            if (!folderInfo.HasWritePermission)
            {
                MessageDlg.Show(this,
                                Resources.
                                    PublishDocumentDlg_UploadSharedZipFile_You_do_not_have_permission_to_upload_to_the_given_folder);
                return;
            }
            DialogResult = DialogResult.OK;
        }

        public void UploadSharedZipFile()
        {
            var folderPath = getFolderPath(treeViewFolders.SelectedNode);
            var zipFilePath = tbFilePath.Text;
            FolderInformation folderInfo = treeViewFolders.SelectedNode.Tag as FolderInformation;
            if (folderInfo == null)
                return;

            try
            {
                WaitDlg = new LongWaitDlg {Text = Resources.PublishDocumentDlg_UploadSharedZipFile_Uploading_File};
                WaitDlg.PerformWork(this, 1000, () =>
                                                PanoramaPublishClient.SendZipFile(folderInfo.Server, folderPath,
                                                                                  zipFilePath));
            }
            catch (Exception x)
            {
                MessageDlg.Show(this, x.Message);
            }
        }

        private string getFolderPath(TreeNode folderNode)
        {
            string nodePath = folderNode.FullPath;
            string[] folderPathSegments = nodePath.Split(new[] {"\\"}, StringSplitOptions.RemoveEmptyEntries);
                // Not L10N

            string folderPath = string.Empty;
            // First segment is server name. 
            for (int i = 1; i < folderPathSegments.Length; i++)
            {
                folderPath += folderPathSegments[i] + "/"; // Not L10N
            }
            // Folder paths cannot have spaces
            return Uri.EscapeUriString(folderPath);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
                                 {
                                     InitialDirectory = Settings.Default.LibraryDirectory,
                                     SupportMultiDottedExtensions = true,
                                     DefaultExt = SrmDocumentSharing.EXT,
                                     Filter =
                                         TextUtil.FileDialogFiltersAll(
                                             Resources.PublishDocumentDlg_btnBrowse_Click_Skyline_Shared_Documents,
                                             SrmDocumentSharing.EXT),
                                     FileName = tbFilePath.Text,
                                     Title = Resources.PublishDocumentDlg_btnBrowse_Click_Publish_Document
                                 })
            {
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
    }

    public interface IPanoramaPublishClient
    {
        JToken GetInfoForFolders(Server server);
        void SendZipFile(Server server, string folderPath, string zipFilePath);
    }

    class WebPanoramaPublishClient : IPanoramaPublishClient
    {
        private static NameValueCollection DataImportInformation { get; set; }
        private static Server Server { get; set; }
        private static string FolderPath { get; set; }

        public JToken GetInfoForFolders(Server server)
        {
            // Retrieve folders from server.
            Uri uri = new Uri(server.URI, "labkey/project/getContainers.view?includeSubfolders=true"); // Not L10N

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add(HttpRequestHeader.Authorization, server.AuthHeader);
                string folderInfo = webClient.UploadString(uri, "POST", string.Empty); // Not L10N
                return JObject.Parse(folderInfo);
            }
        }

        public void SendZipFile(Server server, string folderPath, string zipFilePath)
        {
            Server = server;
            FolderPath = folderPath;
            var zipFileName = Path.GetFileName(zipFilePath) ?? string.Empty;

            // Upload zip file to pipeline folder.
            Uri webDav = new Uri(Server.URI, String.Format("labkey/pipeline/{0}getPipelineContainer.api", FolderPath)); // Not L10N

            DataImportInformation = new NameValueCollection
                                                     {
                                                         // For now, we only have one root that user can upload to
                                                         {"path", "./"}, // Not L10N 
                                                         {"file", zipFileName} // Not L10N
                                                     };
            using (WebClient webClient = new WebClient())
            {
                webClient.UploadProgressChanged += webClient_UploadProgressChanged;
                webClient.UploadFileCompleted += webClient_UploadFileCompleted;

                webClient.Headers.Add(HttpRequestHeader.Authorization, server.AuthHeader);
                var webDavInfo = webClient.UploadString(webDav, "POST", string.Empty); // Not L10N
                JObject jsonWebDavInfo = JObject.Parse(webDavInfo);

                string webDavUrl = (string)jsonWebDavInfo["webDavURL"]; // Not L10N

                // Must include the name of the zip file in the destination path. 
                Uri uploadUri = new Uri(server.URI, webDavUrl + Uri.EscapeUriString(zipFileName));

                webClient.UploadFileAsync(uploadUri, "PUT", zipFilePath); // Not L10N
            }
        }

        public static void webClient_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)
        {
            PublishDocumentDlg.WaitDlg.ProgressValue = (int)(e.BytesSent / (1.0 * e.TotalBytesToSend)) * 100;

            PublishDocumentDlg.WaitDlg.Message = String.Format(Resources.WebPanoramaPublishClient_webClient_UploadProgressChanged__0__uploaded__1__of__2__bytes,
                                                               e.UserState, e.BytesSent, e.TotalBytesToSend);
        }

        private static void webClient_UploadFileCompleted(object sender, UploadFileCompletedEventArgs e)
        {
            // We need the data to be completely uploaded before we can import. 
            importDataOnServer();
        }

        public static void importDataOnServer()
        {
            Uri importUrl = new Uri(Server.URI, string.Format("labkey/targetedms/{0}skylineDocUploadApi.view", FolderPath)); // Not L10N
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add(HttpRequestHeader.Authorization, Server.AuthHeader);
                // Need to tell server which uploaded file to import.
                byte[] responseBytes = webClient.UploadValues(importUrl, "POST", DataImportInformation); // Not L10N
                string response = Encoding.UTF8.GetString(responseBytes);
                JToken importResponse = JObject.Parse(response);

                // ID to check import status.
                var details = importResponse["UploadedJobDetails"]; // Not L10N
                int rowId = (int)details[0]["RowId"]; // Not L10N
                Uri statusUri = new Uri(Server.URI,
                                        string.Format(
                                            "labkey/query/{0}selectRows.view?query.queryName=job&schemaName=pipeline&query.rowId)~eq={1}", FolderPath, // Not L10N
                                            rowId));
                bool complete = false;
                // Wait for import to finish before returning.
                while (!complete)
                {
                    string statusResponse = webClient.UploadString(statusUri, "POST", String.Empty); // Not L10N
                    JToken jStatusResponse = JObject.Parse(statusResponse);
                    JToken rows = jStatusResponse["rows"]; // Not L10N
                    foreach (var row in rows)
                    {
                        if ((int)row["RowId"] == rowId) // Not L10N
                        {
                            string status = (string)row["Status"]; // Not L10N
                            if (string.Equals(status, "ERROR"))
                            {
                                throw new WebException(string.Format("Error importing Skyline file to Panorama server {0}", Server.URI));
                            }
                            complete = string.Equals(status, "COMPLETE"); // Not L10N
                            break;
                        }
                    }
                }
            }
        }  
    }
}
