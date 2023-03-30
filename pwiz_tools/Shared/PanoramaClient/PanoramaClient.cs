using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient.Properties;


namespace pwiz.PanoramaClient
{
    //PanoramaUtil should become PanoramaClient
    
    public class PanoramaClient
    {
        public enum ImageId
        {
            panorama,
            labkey,
            chrom_lib,
            folder
        }
        
        public string DownloadAndSave(Uri serverUri, string user, string pass, string fileName, string downloadName)
        {
            var dlg = new FolderBrowserDialog();
            dlg.Description = Resources.RemoteFileDialog_open_Click_Select_the_folder_the_file_will_be_downloaded_to;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var path = dlg.SelectedPath;
                var selected = dlg.SelectedPath;
                DownloadFile(path, serverUri, user, pass, fileName, downloadName);
                return Path.Combine(selected, fileName);
            }
            else
            {
                return string.Empty;
            }
        }

        private void DownloadFile(string path, Uri server, string user, string pass, string fileName, string downloadName)
        {
            var serverUri = server;
            
            
            var downloadUri = server + downloadName;
            using (var wc = new WebClientWithCredentials(serverUri, user, pass))
            {
                wc.DownloadFile(

                    // Param1 = Link of file
                    new Uri(downloadUri),
                    // Param2 = Path to save
                    Path.Combine(path, fileName)
                );
            }
        }

        public void InitializeTreeView(Uri serverUri, string user, string pass, TreeView treeViewFolders, bool requireUploadPerms, bool showFiles)
        {
            var folder = GetInfoForFolders(serverUri, user, pass, null);
            var treeNode = new TreeNode(serverUri.ToString());

            treeViewFolders.Invoke(new Action(() => treeViewFolders.Nodes.Add(treeNode)));
            treeViewFolders.Invoke(new Action(() => AddChildContainers(treeNode, folder, requireUploadPerms, showFiles)));

        }

        public JToken GetInfoForFolders(Uri serUri, string user, string pass, string folder)
        {

            // Retrieve folders from server.
            var uri = GetContainersUri(serUri, folder, true);

            using (var webClient = new WebClientWithCredentials(serUri, user, pass))
            {
                return webClient.Get(uri);
            }
        }

        public static Uri GetContainersUri(Uri serverUri, string folder, bool includeSubfolders)
        {
            var queryString = string.Format(@"includeSubfolders={0}&moduleProperties=TargetedMS",
                includeSubfolders ? @"true" : @"false");
            return Call(serverUri, @"project", folder, @"getContainers", queryString);
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, string query,
            bool isApi = false)
        {
            string path = controller + @"/" + (folderPath ?? string.Empty) + @"/" +
                          method + (isApi ? @".api" : @".view");

            if (!string.IsNullOrEmpty(query))
            {
                path = path + @"?" + query;
            }

            return new Uri(serverUri, path);
        }

        public static bool CheckFolderPermissions(JToken folderJson)
        {
            if (folderJson != null)
            {
                var userPermissions = folderJson.Value<int?>(@"userPermissions");
                return userPermissions != null && Equals(userPermissions & 2, 2);
            }

            return false;
        }

        public static bool CheckFolderType(JToken folderJson)
        {
            if (folderJson != null)
            {

                var folderType = (string)folderJson[@"folderType"];
                var modules = folderJson[@"activeModules"];
                return modules != null && ContainsTargetedMSModule(modules) &&
                       Equals(@"Targeted MS", folderType);
            }

            return false;
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
                    canUpload = CheckFolderPermissions(subFolder) &&
                                CheckFolderType(subFolder);
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


    }
}
