using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

        public string Server;
        public string User;
        public string Pass;
        public void InitializeTreeView(PanoramaServer server, TreeView treeViewFolders, bool requireUploadPerms, bool showFiles, bool showSky)
        {
            IPanoramaClient panoramaClient = new WebPanoramaClient(server.URI);
            var folder = panoramaClient.GetInfoForFolders(server, null);
            var treeNode = new TreeNode(server.URI.ToString());
            Server = server.URI.ToString();
            User = server.Username;
            Pass = server.Password;
            treeViewFolders.Invoke(new Action(() => treeViewFolders.Nodes.Add(treeNode)));
            treeViewFolders.Invoke(new Action(() => AddChildContainers(treeNode, folder, requireUploadPerms, showFiles)));
        }

        private static bool ContainsTargetedMSModule(IEnumerable<JToken> modules)
        {
            foreach (var module in modules)
            {
                if (string.Equals(module.ToString(), @"TargetedMS"))
                    return true;
            }

            return false;
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

                if (showFiles)
                {
                    folderNode.Tag = (string)subFolder[@"path"];
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

        private JToken GetJson(string query, string user, string pass)
        {
            var queryUri = new Uri(query);
            var webClient = new WebClientWithCredentials(queryUri, user, pass);
            JToken json = webClient.Get(queryUri);
            return json;
        }

        private static string BuildQuery(string server, string folderPath, string queryName, string folderFilter, string[] columns, string sortParam, string equalityParam)
        {
            var query =
                $@"{server}{folderPath}query-selectRows.view?schemaName=targetedms&query.queryName={queryName}&query.containerFilterName={folderFilter}";
            if (columns != null)
            {
                query = $@"{query}&query.columns=";
                var allCols = columns.Aggregate(string.Empty, (current, col) => $@"{col},{current}");

                query = $@"{query}{allCols}";
            }

            if (!string.IsNullOrEmpty(sortParam))
            {
                query = $@"{query}&query.sort={sortParam}";
            }

            if (!string.IsNullOrEmpty(equalityParam))
            {
                query = $@"{query}&query.{equalityParam}~eq=";
            }
            return query;
        }

        public void AddChildFiles(Uri newUri, ListView listView)
        {
            JToken json = GetJson(newUri.ToString(), User, Pass);
            if ((int)json[@"fileCount"] != 0)
            {
                JToken files = json[@"files"];
                foreach (dynamic file in files)
                {
                    var listItem = new string[2];
                    var fileName = (string)file[@"text"];
                    listItem[0] = fileName;
                    var isFile = (bool)file[@"leaf"];
                    if (isFile)
                    {
                        var canRead = (bool)file[@"canRead"];
                        if (!canRead)
                        {
                            continue;
                        }
                        var size = (long)file[@"size"];
                        var sizeObj = new FileSize(size);
                        listItem[1] = sizeObj.ToString();
                        ListViewItem fileNode;
                        if (fileName.Contains(".sky"))
                        {
                            fileNode = new ListViewItem(listItem, 1);

                        }
                        else
                        {
                            fileNode = new ListViewItem(listItem, 0);
                        }
                        fileNode.Tag = (string)file[@"id"];
                        fileNode.Name = Path.GetFullPath((string)file[@"href"]);
                        listView.Items.Add(fileNode);
                    }
                }
            }
        }

    }


}
