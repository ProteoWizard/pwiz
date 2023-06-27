/*
 * Original author: ?? <?? .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using Newtonsoft.Json.Linq;

namespace pwiz.PanoramaClient
{
    public enum ImageId
    {
        panorama,
        labkey,
        chrom_lib,
        folder
    }

    internal class PanoramaFormUtil
    {

        public void InitializeTreeView(PanoramaServer server, List<KeyValuePair<PanoramaServer, JToken>> listServers)
        {
            IPanoramaClient panoramaClient = new WebPanoramaClient(server.URI, server.Username, server.Password);
            listServers.Add(new KeyValuePair<PanoramaServer, JToken>(server, panoramaClient.GetInfoForFolders(null)));
        }

        public void InitializeFolder(TreeView treeViewFolders, bool requireUploadPerms, bool showFiles, JToken folder, PanoramaServer server)
        {
            var treeNode = new TreeNode(server.URI.ToString());
            treeViewFolders.Invoke(new Action(() => treeViewFolders.Nodes.Add(treeNode)));
            treeViewFolders.Invoke(new Action(() => treeViewFolders.SelectedImageIndex = (int)ImageId.panorama));
            treeViewFolders.Invoke(new Action(() => AddChildContainers(treeNode, folder, requireUploadPerms, showFiles)));
        }

        public void InitializeTreeFromPath(TreeView treeViewFolders, string folderPath, PanoramaServer server)
        {
            var uriPath = new Uri(folderPath);
            var serverText = uriPath.GetLeftPart(UriPartial.Authority);
            var uriFolders = folderPath.Substring(string.Concat(serverText, "/_webdav").Length);
            var uriFolderTokens = uriFolders.Split('/');
            var folder = uriFolderTokens.LastOrDefault();
            var treeNode = new TreeNode(server.URI.ToString());
            var fileNode = new TreeNode("@files");
            TreeNode selectedFolder = null;
            treeViewFolders.Invoke(new Action(() => treeViewFolders.Nodes.Add(treeNode)));
            treeViewFolders.Invoke(new Action(() => treeViewFolders.SelectedImageIndex = (int)ImageId.panorama));
            treeViewFolders.Invoke(new Action(() => selectedFolder = LoadFromPath(uriFolderTokens, treeNode)));
            treeViewFolders.Invoke(new Action(() => selectedFolder.Nodes.Add(fileNode)));
            treeViewFolders.Invoke(new Action(() => treeViewFolders.SelectedImageIndex = treeViewFolders.ImageIndex = (int)ImageId.folder));
            fileNode.Tag = string.Concat(selectedFolder.Tag, "/@files");
            treeViewFolders.Invoke(new Action(() => selectedFolder.Expand()));
            treeViewFolders.Invoke(new Action(() => treeViewFolders.SelectedNode = fileNode));
        }

        public void InitializeTreeViewTest(PanoramaServer server, TreeView treeView, JToken folderJson)
        {
            var treeNode = new TreeNode(server.URI.ToString());
            treeView.Invoke(new Action(() => treeView.Nodes.Add(treeNode)));
            treeView.Invoke(new Action(() => treeView.SelectedImageIndex = (int)ImageId.panorama));
            treeView.Invoke(new Action(() => AddChildContainers(treeNode, folderJson, false, true)));
        }

        public static void AddChildContainers(TreeNode node, JToken folder, bool requireUploadPerms, bool showFiles)
        {
            JEnumerable<JToken> subFolders = folder[@"children"].Children();
            foreach (var subFolder in subFolders)
            {
                if (!PanoramaUtil.CheckReadPermissions(subFolder))
                {
                    // Do not add a node for the folder if user does not have read permissions in the folder. 
                    // Any subfolders, even if they have read permissions, will be ignored.
                    continue;
                }

                var folderName = (string)subFolder[@"name"];

                var folderNode = new TreeNode(folderName);
                AddChildContainers(folderNode, subFolder, requireUploadPerms, showFiles);

                var isTargetedMsFolder = PanoramaUtil.IsTargetedMsFolder(subFolder);
                // User can only upload to folders that are of the type "TargetedMS", and where where they have at least insert permissions.
                var canUpload = PanoramaUtil.CheckInsertPermissions(subFolder) && isTargetedMsFolder;

                if (requireUploadPerms && folderNode.Nodes.Count == 0 && !canUpload)
                {
                    // If the user does not have write permissions in this folder or any
                    // of its subfolders, do not add it to the tree.
                    continue;
                }
                
                node.Nodes.Add(folderNode);

                if (requireUploadPerms && !(isTargetedMsFolder && canUpload))
                {
                    // User cannot upload files to folder
                    folderNode.ForeColor = Color.Gray;
                    folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.folder;
                }
                else
                {
                    if (isTargetedMsFolder)
                    {
                        JToken moduleProperties = subFolder[@"moduleProperties"];
                        if (moduleProperties == null)
                            folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.labkey;
                        else
                        {
                            string effectiveValue = (string)moduleProperties[0][@"effectiveValue"];

                            folderNode.ImageIndex =
                                folderNode.SelectedImageIndex =
                                    (effectiveValue.Equals(@"Library") || effectiveValue.Equals(@"LibraryProtein"))
                                        ? (int)ImageId.chrom_lib
                                        : (int)ImageId.labkey;
                        }
                    }
                    else
                    {
                        folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.folder;
                    }
                }

                if (showFiles)
                {
                    folderNode.Tag = (string)subFolder[@"path"];
                    folderNode.Name = PanoramaUtil.HasTargetedMsModule(subFolder).ToString();
                }
                else
                {
                    //folderNode.Tag = new FolderInformation(server, canUpload);
                }
            }
        }

        private TreeNode LoadFromPath(string[] folderTokens, TreeNode node)
        {
            foreach (var folder in folderTokens)
            {
                if (!string.IsNullOrEmpty(folder))
                {
                    var subfolder = new TreeNode(folder);
                    subfolder.Tag = string.Concat(node.Tag, "/", folder);
                    subfolder.ImageIndex = subfolder.SelectedImageIndex = (int)ImageId.folder;
                    node.Nodes.Add(subfolder);
                    node = subfolder;
                    
                }
            }
            return node;
        }
    }


}
