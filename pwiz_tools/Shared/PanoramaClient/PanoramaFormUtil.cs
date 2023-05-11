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
            IPanoramaClient panoramaClient = new WebPanoramaClient(server.URI);
            listServers.Add(new KeyValuePair<PanoramaServer, JToken>(server, panoramaClient.GetInfoForFolders(server, null)));
        }

        public void InitializeFolder(TreeView treeViewFolders, bool requireUploadPerms, bool showFiles, JToken folder, PanoramaServer server)
        {
            var treeNode = new TreeNode(server.URI.ToString());
            treeViewFolders.Invoke(new Action(() => treeViewFolders.Nodes.Add(treeNode)));
            treeViewFolders.Invoke(new Action(() => AddChildContainers(treeNode, folder, requireUploadPerms, showFiles)));
        }

        public void InitializeTreeViewTest(PanoramaServer server, TreeView treeView, JToken folderJson)
        {
            var treeNode = new TreeNode(server.URI.ToString());
            treeView.Invoke(new Action(() => treeView.Nodes.Add(treeNode)));
            treeView.Invoke(new Action(() => AddChildContainers(treeNode, folderJson, false, true)));
        }

        public static void AddChildContainers(TreeNode node, JToken folder, bool requireUploadPerms, bool showFiles)
        {
            JEnumerable<JToken> subFolders = folder[@"children"].Children();
            foreach (var subFolder in subFolders)
            {
                var folderName = (string)subFolder[@"name"];

                var folderNode = new TreeNode(folderName);
                AddChildContainers(folderNode, subFolder, requireUploadPerms, showFiles);

                // User can only upload to folders where TargetedMS is an active module.
                bool canUpload;

                if (requireUploadPerms)
                {
                    canUpload = PanoramaUtil.CheckFolderPermissions(subFolder) &&
                                PanoramaUtil.CheckFolderType(subFolder);
                }
                else
                {
                    var userPermissions = subFolder.Value<int?>(@"userPermissions");
                    canUpload = userPermissions != null && Equals(userPermissions & 1, 1);
                }
                // If the user does not have write permissions in this folder or any
                // of its subfolders, do not add it to the tree.
                if (requireUploadPerms)
                {
                    if (folderNode.Nodes.Count == 0 && !canUpload)
                    {
                        continue;
                    }
                }
                else
                {
                    if (!canUpload)
                    {
                        continue;
                    }
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
                    if ((PanoramaUtil.CheckFolderType(subFolder) && !requireUploadPerms) || requireUploadPerms)
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
                    } else
                    {
                        folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.folder;
                    }
                }

                if (showFiles)
                {
                    folderNode.Tag = (string)subFolder[@"path"];
                    folderNode.Name = PanoramaUtil.CheckFolderType(subFolder).ToString();
                }
                else
                {
                    //folderNode.Tag = new FolderInformation(server, canUpload);
                }

            }
        }

        private void LoadSkyFolders(JToken folders, TreeNode node, HashSet<string> prevFolders)
        {
            JToken rows = folders[@"rows"];
            foreach (var row in rows)
            {
                //get the path
                var fullPath = (string)row[@"Container/Path"];

                if (!prevFolders.Contains(fullPath))
                {
                    prevFolders.Add(fullPath);
                    AddFolderPath(fullPath, fullPath, node);
                }
            }
        }

        public const char SLASH = '/';

        private void AddFolderPath(string full, string path, TreeNode node)
        {
            if (!string.IsNullOrEmpty(path))
            {
                var folders = path.Split(SLASH);
                if (folders.Length > 1)
                {
                    var nextFolder = folders[1];
                    var replaced = string.Concat(SLASH.ToString(), nextFolder); //"/" + nextFolder;
                    var replaceTest = path.Substring(replaced.Length);
                    if (!node.Nodes.ContainsKey(nextFolder))
                    {
                        var newNode = new TreeNode(nextFolder);
                        newNode.Name = nextFolder;
                        if (node.Tag == null)
                        {
                            newNode.Tag =
                                replaced; //string.Concat(Path.DirectorySeparatorChar, nextFolder); //string.Concat(SLASH.ToString(), nextFolder); //"/" + nextFolder;
                        }
                        else
                        {
                            newNode.Tag = string.Concat(node.Tag.ToString(), SLASH.ToString(), nextFolder); //node.Tag + "/" + nextFolder;
                        }

                        node.Nodes.Add(newNode);

                        AddFolderPath(full, replaceTest, newNode);
                    }
                    else
                    {
                        var getKey = node.Nodes.IndexOfKey(nextFolder);

                        AddFolderPath(full, replaceTest, node.Nodes[getKey]);
                    }
                }
            }
        }
    }


}
