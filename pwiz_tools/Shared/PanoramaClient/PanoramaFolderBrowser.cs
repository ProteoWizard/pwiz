/*
 * Original author: Sophie Pallanck <srpall .at. uw.edu>,
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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.PanoramaClient.Properties;

namespace pwiz.PanoramaClient
{
    public enum ImageId
    {
        panorama,
        labkey,
        chrom_lib,
        folder
    }

    public partial class PanoramaFolderBrowser : UserControl
    {
        private readonly bool _uploadPerms;
        private readonly List<PanoramaServer> _serverList;
        private readonly bool _showWebDav;
        private readonly string _initialPath;
        private readonly Stack<TreeNode> _previous = new Stack<TreeNode>();
        private TreeNode _priorNode;
        private readonly Stack<TreeNode> _next = new Stack<TreeNode>();
        private TreeNode _clicked;
        private TreeNode _lastSelected;
        private readonly TreeViewStateRestorer _restorer;
        private readonly List<KeyValuePair<PanoramaServer, JToken>> _listServerFolders = new List<KeyValuePair<PanoramaServer, JToken>>();

        public event EventHandler NodeClick;
        public event EventHandler AddFiles;
        public string FolderPath { get; private set; }
        public string TreeState { get; private set; }
        public bool CurNodeIsTargetedMS { get; private set; }
        public PanoramaServer ActiveServer { get; protected set; }

        public PanoramaFolderBrowser(List<PanoramaServer> servers, string state, bool uploadPerms, string selectedPath = null, bool showWebDav = false)
        {
            InitializeComponent();
            _serverList = servers;
            _uploadPerms = uploadPerms;
            TreeState = state;
            _showWebDav = showWebDav;
            _initialPath = selectedPath?.TrimEnd('/');
            _restorer = new TreeViewStateRestorer(treeView);
            InitializeServers();
        }

        /// <summary>
        /// Builds the TreeView of folders and restores any
        /// previous state of the TreeView
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FolderBrowser_Load(object sender, EventArgs e)
        {
            if (_showWebDav && _initialPath != null)
            {
                ActiveServer = _serverList.FirstOrDefault();
                InitializeTreeFromPath(treeView, ActiveServer);
                AddWebDavFolders(treeView.SelectedNode);
                AddSelectedFiles(treeView.SelectedNode, EventArgs.Empty);

            }
            InitializeTreeView(treeView);
            
        }

        protected virtual void InitializeTreeView(TreeView tree)
        {
            foreach (var server in _listServerFolders)
            {
                InitializeFolder(tree, _uploadPerms, true, server.Value, server.Key);
            }
            //TODO: Only if we don't have an initial path, we restore the state, otherwise, just select the given node
            //TODO: Only AddSelectedFiles if there is a state or initial path
            if (!string.IsNullOrEmpty(TreeState))
            {
                _restorer.RestoreExpansionAndSelection(TreeState);
                _restorer.UpdateTopNode();
                AddSelectedFiles(treeView.SelectedNode, EventArgs.Empty);
            }
            else
            {
                FolderPath = string.Empty;
                ActiveServer = _serverList.FirstOrDefault();
                tree.TopNode.Expand();
            }

            if (!string.IsNullOrEmpty(_initialPath))
            {
                var uriFolderTokens = _initialPath.Split('/');
                SelectNode(uriFolderTokens.LastOrDefault());
            }
        }

        /// <summary>
        /// Initializes the JSON that will be
        /// used to build the TreeView of folders
        /// </summary>
        private void InitializeServers()
        {
            if (_serverList == null || _showWebDav)
            {
                return;
            }

            var listErrorServers = new List<Tuple<PanoramaServer, string>>();
            foreach (var server in _serverList)
            {
                try
                {
                    InitializeTreeServers(server, _listServerFolders);
                }
                catch (Exception ex)
                {
                    if (ex is WebException || ex is PanoramaServerException)
                    {
                        var error = ex.Message;
                        if (error != null && error.Contains(Resources
                                .UserState_GetErrorMessage_The_username_and_password_could_not_be_authenticated_with_the_panorama_server_))
                        {
                            error = TextUtil.LineSeparate(error, Resources.PanoramaFolderBrowser_InitializeServers_Go_to_Tools___Options___Panorama_tab_to_update_the_username_and_password);

                        }

                        listErrorServers.Add(new Tuple<PanoramaServer, string>(server, error ?? string.Empty));
                    }
                }
            }
            if (listErrorServers.Count > 0)
            {
                throw new Exception(TextUtil.LineSeparate(Resources.PanoramaFolderBrowser_InitializeServers_Failed_attempting_to_retrieve_information_from_the_following_servers,
                    string.Empty,
                    ServersToString(listErrorServers)));
            }
        }

        private string ServersToString(IEnumerable<Tuple<PanoramaServer, string>> servers)
        {
            return TextUtil.LineSeparate(servers.Select(t => TextUtil.LineSeparate(t.Item1.URI.ToString(), t.Item2)));
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            _lastSelected = e.Node;
            var folderInfo = GetFolderInformation(e.Node);
            if (folderInfo != null)
            {
                FolderPath = folderInfo.FolderPath;
            }
        }

        /// <summary>
        /// When a node is clicked on, add any corresponding files 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (_lastSelected == null || !_lastSelected.Equals(e.Node))
            {
                var hitTest = treeView.HitTest(e.Location);
                if (hitTest.Location == TreeViewHitTestLocations.Label || hitTest.Location == TreeViewHitTestLocations.Image)
                {
                    var hit = e.Node.TreeView.HitTest(e.Location);
                    if (hit.Location != TreeViewHitTestLocations.PlusMinus)
                    {
                        var fileInfo = GetFolderInformation(e.Node);
                        if (fileInfo != null)
                        {
                            treeView.SelectedNode = e.Node;
                            treeView.Focus();
                            ActiveServer = fileInfo.Server;
                            UpdateNavButtons(e.Node);
                        }
                    }
                }
            }
        }

        private FolderInformation GetFolderInformation(TreeNode node)
        {
            return node?.Tag as FolderInformation;
        }

        public string GetSelectedUri()
        {
            return GetSelectedUri(FolderPath, _showWebDav);
        }

        private string GetSelectedUri(string folderPath, bool webdav)
        {
            return string.Concat(ActiveServer.URI, webdav ? PanoramaUtil.WEBDAV_W_SLASH : string.Empty,
                folderPath.TrimStart('/'));
        }
        public void UpClick()
        {
            if (_clicked?.Parent != null)
            {
                _next.Clear();
                if (_previous.Count != 0)
                {
                    if (!_previous.Peek().Equals(_priorNode))
                    {
                        _previous.Push(_priorNode);
                    }
                }
                else
                {
                    _previous.Push(_priorNode);
                }
                var parent = _lastSelected.Parent;
                _priorNode = parent;
                UpdateNavData(parent);
            }
        }

        public bool UpEnabled()
        {
            return _clicked is { Parent: { } };
        }

        public void BackClick()
        {
            if (_next.Count != 0)
            {
                if (!_next.Peek().Equals(_lastSelected))
                {
                    _next.Push(_lastSelected);
                }
            }
            else
            {
                _next.Push(_lastSelected);
            }
            var prior = _previous.Pop();
            _priorNode = prior;
            UpdateNavData(prior);
        }

        public bool BackEnabled()
        {
            return _previous != null && _previous.Count != 0;
        }

        public void ForwardClick()
        {
            if (_previous.Count != 0)
            {
                if (!_previous.Peek().Equals(_lastSelected))
                {
                    _previous.Push(_lastSelected);
                }
            }
            else
            {
                _previous.Push(_lastSelected);
            }
            var nextNode = _next.Pop();
            UpdateNavData(nextNode);
        }

        private void UpdateNavData(TreeNode node)
        {
            treeView.SelectedNode = node;
            _lastSelected = node;
            _clicked = node;
            var folderInfo = (FolderInformation)_clicked.Tag;
            CurNodeIsTargetedMS = folderInfo.IsTargetedMS;
            FolderPath = folderInfo.FolderPath ?? string.Empty;
            treeView.Focus();
            if (AddFiles != null) AddFiles(this, EventArgs.Empty);
        }

        public bool ForwardEnabled()
        {
            return _next != null && _next.Count != 0;
        }

        public string ClosingState()
        { 
            return TreeState = _restorer.GetPersistentString();
        }

        /// <summary>
        /// If there was a previous selection made in FilePicker, reload the files in the
        /// selected folder
        /// </summary>
        /// <param name="node"></param>
        /// <param name="e"></param>
        private void AddSelectedFiles(TreeNode node, EventArgs e)
        {
            var folderInfo = GetFolderInformation(node);
            if (folderInfo != null)
            {
                ActiveServer = folderInfo.Server; 
                _priorNode = node;
                treeView.Focus();
                _lastSelected = node;
                _clicked = node;
                CurNodeIsTargetedMS = folderInfo.IsTargetedMS;
                FolderPath = folderInfo.FolderPath;
                treeView.Focus();
                if (AddFiles != null) AddFiles(this, e);
            }
        }

        /// <summary>
        /// Searches for a given TreeNode in a TreeView and
        /// returns the TreeNode if it is found, used for testing
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="nodeName"></param>
        /// <returns></returns>
        private TreeNode SearchTree(IEnumerable nodes, string nodeName)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Text.Equals(nodeName))
                {
                    return node;
                }
                if (node.Nodes.Count > 0)
                {
                    var result = SearchTree(node.Nodes, nodeName);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        private void TreeView_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == Convert.ToChar(Keys.Return) && treeView.SelectedNode != null)
            {
                treeView.SelectedNode.Expand();
            } 

        }

        private void TreeView_KeyUp(object sender, KeyEventArgs e)
        {
            if (treeView.SelectedNode != null && _lastSelected != null)
            {
                if (e.KeyValue == Convert.ToChar(Keys.Up) || e.KeyValue == Convert.ToChar(Keys.Down) || e.KeyValue == Convert.ToChar(Keys.Right) || e.KeyValue == Convert.ToChar(Keys.Left))
                {
                    var node = treeView.SelectedNode;
                    UpdateNavButtons(node);
                }
            }
        }

        private void UpdateNavButtons(TreeNode node)
        {
            treeView.SelectedNode = node;
            var folderInfo = GetFolderInformation(node);
            if (folderInfo != null)
            {
                CurNodeIsTargetedMS = folderInfo.IsTargetedMS;
                FolderPath = folderInfo.FolderPath ?? string.Empty;
                _clicked = node;
            }

            if (AddFiles != null) AddFiles(this, EventArgs.Empty);
            // If there's a file browser observer, add corresponding files
            if (_priorNode != null && _priorNode != node)
            {
                _previous.Push(_priorNode);
            }
            _priorNode = node;
            _next.Clear();
            if (NodeClick != null) NodeClick(this, EventArgs.Empty);
        }

        private void AddWebDavFolders(TreeNode node)
        {
            try
            {
                var folderInfo = GetFolderInformation(node);
                if (folderInfo != null)
                {
                    var query = new Uri(string.Concat(ActiveServer.URI, PanoramaUtil.WEBDAV, folderInfo.FolderPath, "?method=json"));
                    var webClient = new WebClientWithCredentials(query, ActiveServer.Username, ActiveServer.Password);
                    JToken json = webClient.Get(query);
                    if ((int)json[@"fileCount"] != 0)
                    {
                        var files = json[@"files"];
                        foreach (var file in files)
                        {
                            var listItem = new string[5];
                            var fileName = (string)file[@"text"];
                            listItem[0] = fileName;
                            var isFile = (bool)file[@"leaf"];
                            if (!isFile)
                            {
                                var canRead = (bool)file[@"canRead"];
                                if (!canRead)
                                {
                                    continue;
                                }

                                var newNode = new TreeNode(fileName)
                                {
                                    Tag = new FolderInformation(ActiveServer, string.Concat(folderInfo.FolderPath, @"/", fileName), false),
                                    ImageIndex = 3,
                                    SelectedImageIndex = 3
                                };
                                node.Nodes.Add(newNode);
                                if (newNode.Text.Equals(@"RawFiles"))
                                {
                                    AddWebDavFolders(newNode);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignored
                //TODO: Do we really need a try catch block? If we do, document why we ignore this exception
                
            }
        }

        /// <summary>
        /// Generates JSON containing the folder structure for the given server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="listServers"></param>
        public virtual void InitializeTreeServers(PanoramaServer server, List<KeyValuePair<PanoramaServer, JToken>> listServers)
        {
            IPanoramaClient panoramaClient = new WebPanoramaClient(server.URI);
            listServers.Add(new KeyValuePair<PanoramaServer, JToken>(server, panoramaClient.GetInfoForFolders(server, null)));
        }

        /// <summary>
        /// Builds a TreeView of folders using JSON data
        /// </summary>
        /// <param name="treeViewFolders"></param>
        /// <param name="requireUploadPerms"></param>
        /// <param name="showFiles"></param>
        /// <param name="folder"></param>
        /// <param name="server"></param>
        public void InitializeFolder(TreeView treeViewFolders, bool requireUploadPerms, bool showFiles, JToken folder, PanoramaServer server)
        {
            var treeNode = new TreeNode(server.URI.ToString())
            {
                Tag = new FolderInformation(server, string.Empty, false)
            };
            treeViewFolders.Nodes.Add(treeNode);
            treeViewFolders.SelectedImageIndex = (int)ImageId.panorama;
            AddChildContainers(treeNode, folder, requireUploadPerms, showFiles, server);
        }

        /// <summary>
        /// Given a path to a folder, builds a TreeView of folders
        /// contained in the path
        /// </summary>
        /// <param name="treeViewFolders"></param>
        /// <param name="server"></param>
        public void InitializeTreeFromPath(TreeView treeViewFolders, PanoramaServer server)
        {
            var uriFolderTokens = _initialPath.Split('/');
            var treeNode = new TreeNode(server.URI.ToString())
            {
                Tag = new FolderInformation(server, string.Empty, false)
            };
            treeViewFolders.SelectedImageIndex = (int)ImageId.panorama;
            treeViewFolders.Nodes.Add(treeNode);

            var fileNode = new TreeNode(@"@files");
            var selectedFolder = LoadFromPath(uriFolderTokens, treeNode, server);
            selectedFolder.Nodes.Add(fileNode);
            treeViewFolders.SelectedImageIndex = treeViewFolders.ImageIndex = (int)ImageId.folder;
            if (selectedFolder.Tag is FolderInformation folderInfo)
                fileNode.Tag = new FolderInformation(server, string.Concat(folderInfo.FolderPath, @"/@files"), false);
            selectedFolder.Expand();
            treeViewFolders.SelectedNode = fileNode;
        }

        /// <summary>
        /// Traverses JSON containing a folder structure and adds each
        /// folder TreeNode to a TreeView
        /// nodes that 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="folder"></param>
        /// <param name="requireUploadPerms"></param>
        /// <param name="showFiles"></param>
        /// <param name="server"></param>
        public static void AddChildContainers(TreeNode node, JToken folder, bool requireUploadPerms, bool showFiles, PanoramaServer server)
        {
            var subFolders = folder[@"children"].Children();
            foreach (var subFolder in subFolders)
            {
                var folderName = (string)subFolder[@"name"];

                var folderNode = new TreeNode(folderName);
                AddChildContainers(folderNode, subFolder, requireUploadPerms, showFiles, server);

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
                        var moduleProperties = subFolder[@"moduleProperties"];
                        if (moduleProperties == null)
                            folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.labkey;
                        else
                        {
                            var effectiveValue = (string)moduleProperties[0][@"effectiveValue"];

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
                    var modules = subFolder[@"activeModules"];
                    var containsTargetedMs = false;
                    foreach (var module in modules)
                    {
                        if (string.Equals(module.ToString(), @"TargetedMS"))
                        {
                            containsTargetedMs = true;
                        }

                    }
                    folderNode.Tag = new FolderInformation(server, (string)subFolder[@"path"], containsTargetedMs);
                }
                else
                {
                    folderNode.Tag = new FolderInformation(server, canUpload);
                }

            }
        }

        /// <summary>
        /// Given an array of folders, add each folder to a
        /// TreeView
        /// </summary>
        /// <param name="folderTokens"></param>
        /// <param name="node"></param>
        /// <param name="server"></param>
        /// <returns></returns>
        private TreeNode LoadFromPath(IEnumerable<string> folderTokens, TreeNode node, PanoramaServer server)
        {
            foreach (var folder in folderTokens)
            {
                if (!string.IsNullOrEmpty(folder))
                {
                    var folderInfo = GetFolderInformation(node);
                    var subfolder = new TreeNode(folder);
                    if (folderInfo != null)
                        subfolder.Tag =
                            new FolderInformation(server, string.Concat(folderInfo.FolderPath, @"/", folder), false);
                    subfolder.ImageIndex = subfolder.SelectedImageIndex = (int)ImageId.folder;
                    node.Nodes.Add(subfolder);
                    node = subfolder;
                }
            }
            return node;
        }

        public string GetSelectedFolderPath()
        {
            return (_clicked?.Tag as FolderInformation)?.FolderPath;
        }

        #region MethodsForTests
        public bool IsExpanded(string nodeName)
        {
            var node = SearchTree(treeView.Nodes, nodeName);
            return node is { IsExpanded: true };
        }

        public void ClickEnter()
        {
            TreeView_KeyPress(this, new KeyPressEventArgs((char)13));
        }

        public int GetIcon()
        {
            return treeView.SelectedNode.ImageIndex;
        }

        public bool IsSelected(string nodeName)
        {
            var node = SearchTree(treeView.Nodes, nodeName);
            return node is { IsSelected: true };
        }

        public void SelectNode(string nodeName)
        {
            var node = SearchTree(treeView.Nodes, nodeName);
            if (node?.Tag is FolderInformation fileInfo)
            {
                ActiveServer = fileInfo.Server;
                UpdateNavButtons(node);
            }
        }

        public string GetSelectedNodeText()
        {
            return _clicked.Text;
        }

        #endregion
    }
}

/// <summary>
/// This class is used for testing purposes
/// </summary>
public class TestPanoramaFolderBrowser : PanoramaFolderBrowser
{
    private PanoramaServer _server;
    private JToken _folderJson;

    //public PanoramaFolderBrowser(List<PanoramaServer> servers, string state, bool uploadPerms, string selectedPath = null, bool showWebDav = false)
    public TestPanoramaFolderBrowser(PanoramaServer server, JToken folderJson) :
        base(new List<PanoramaServer>(), null, false)
    {
        _server = server;
        _folderJson = folderJson;
        ActiveServer = server;
    }

    protected override void InitializeTreeView(TreeView treeViewFolders)
    {
        var treeNode = new TreeNode(_server.URI.ToString())
        {
            Tag = new FolderInformation(_server, string.Empty, false)
        };
        treeViewFolders.Nodes.Add(treeNode);
        treeViewFolders.SelectedImageIndex = (int)ImageId.panorama;
        AddChildContainers(treeNode, _folderJson, false, true, _server);

    }

    public override void InitializeTreeServers(PanoramaServer server,
        List<KeyValuePair<PanoramaServer, JToken>> listServers)
    {
        // Do nothing
    }

}
