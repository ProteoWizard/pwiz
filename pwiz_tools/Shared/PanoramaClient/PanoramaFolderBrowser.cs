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

    public abstract partial class PanoramaFolderBrowser : UserControl
    {
        private readonly Stack<TreeNode> _previous = new Stack<TreeNode>();
        private TreeNode _priorNode;
        private readonly Stack<TreeNode> _next = new Stack<TreeNode>();
        private TreeNode _clicked;
        private TreeNode _lastSelected;
        private readonly TreeViewStateRestorer _restorer;
        private string _folderPath;
        private bool _curNodeIsTargetedMS;
        protected PanoramaServer _activeServer;


        protected List<PanoramaServer> ServerList { get; private set; }
        protected string InitialPath { get; private set; }

        public event EventHandler NodeClick;
        public event EventHandler AddFiles;

        public string TreeState { get; private set; }

        public PanoramaFolderBrowser(List<PanoramaServer> servers, string state, string selectedPath = null, bool showWebDav = false)
        {
            InitializeComponent();
            ServerList = servers;
            TreeState = state;
            InitialPath = selectedPath?.TrimEnd('/');
            _restorer = new TreeViewStateRestorer(treeView);
        }

        // public abstract void InitializeServers();
        public abstract void InitializeTreeView(TreeView treeView);

        /// <summary>
        /// Builds the TreeView of folders and restores any
        /// previous state of the TreeView
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FolderBrowser_Load(object sender, EventArgs e)
        {
            InitializeTreeView(treeView);
            InitializeTreeState(treeView);
        }


        private void InitializeTreeState(TreeView tree)
        {
            if (!string.IsNullOrEmpty(TreeState))
            {
                _restorer.RestoreExpansionAndSelection(TreeState);
                _restorer.UpdateTopNode();
                AddFilesForSelectedNode(treeView.SelectedNode, EventArgs.Empty);
            }
            else
            {
                _folderPath = string.Empty;
                _activeServer = ServerList.FirstOrDefault();
                tree.TopNode.Expand();
            }

            if (!string.IsNullOrEmpty(InitialPath))
            {
                var uriFolderTokens = InitialPath.Split('/');
                SelectNode(uriFolderTokens.LastOrDefault());
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
                    var folderInfo = GetFolderInformation(e.Node);
                    if (folderInfo != null)
                    {
                        treeView.SelectedNode = e.Node;
                        treeView.Focus();
                        _activeServer = folderInfo.Server;
                        _folderPath = folderInfo.FolderPath;
                        UpdateNavButtons(e.Node);
                        _lastSelected = e.Node;
                    }
                }
            }
        }

        protected FolderInformation GetFolderInformation(TreeNode node)
        {
            return node?.Tag as FolderInformation;
        }

        public string GetSelectedUri()
        {
            return GetSelectedUri(_folderPath);
        }

        protected virtual string GetSelectedUri(string folderPath)
        {
            return _activeServer != null && folderPath != null
                ? string.Concat(_activeServer.URI, folderPath.TrimStart('/'))
                : string.Empty;
        }

        public static string GetSelectedUri(PanoramaFolderBrowser browser, bool webdav)
        {
            return browser._activeServer != null && browser._folderPath != null
                ? string.Concat(browser._activeServer.URI,
                    webdav ? PanoramaUtil.WEBDAV_W_SLASH : string.Empty, browser._folderPath.TrimStart('/'))
                : string.Empty;
        }

        public void UpButtonClick()
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

        public void BackButtonClick()
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

        public void ForwardButtonClick()
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
            _curNodeIsTargetedMS = folderInfo.IsTargetedMS;
            _folderPath = folderInfo.FolderPath ?? string.Empty;
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
        protected void AddFilesForSelectedNode(TreeNode node, EventArgs e)
        {
            var folderInfo = GetFolderInformation(node);
            if (folderInfo != null)
            {
                _activeServer = folderInfo.Server; 
                _priorNode = node;
                treeView.Focus();
                // Issue with @files not showing selected files when using webdav version
                _lastSelected = node;
                _clicked = node;
                _curNodeIsTargetedMS = folderInfo.IsTargetedMS;
                _folderPath = folderInfo.FolderPath;
                treeView.Focus();
                if (AddFiles != null) AddFiles(this, e);
            }
        }

        private void TreeView_KeyUp(object sender, KeyEventArgs e)
        {
            if (treeView.SelectedNode != null) 
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
                _curNodeIsTargetedMS = folderInfo.IsTargetedMS;
                _folderPath = folderInfo.FolderPath ?? string.Empty;
                _clicked = node;
            }

            // If there's a file browser observer, add corresponding files
            AddFiles?.Invoke(this, EventArgs.Empty);

            if (_priorNode != null && _priorNode != node)
            {
                _previous.Push(_priorNode);
            }
            _priorNode = node;
            _next.Clear();
            NodeClick?.Invoke(this, EventArgs.Empty);
        }

        public string GetSelectedFolderPath()
        {
            return (_clicked?.Tag as FolderInformation)?.FolderPath;
        }

        public string GetFolderPath()
        {
            return _folderPath;
        }

        public bool GetNodeIsTargetedMS()
        {
            return _curNodeIsTargetedMS;
        }

        public PanoramaServer GetActiveServer()
        {
            return _activeServer;
        }

        #region MethodsForTests
        public bool IsExpanded(string nodeName)
        {
            var node = SearchTree(treeView.Nodes, nodeName);
            return node is { IsExpanded: true };
        }

        public void ClickLeft()
        {
            TreeView_KeyUp(this, new KeyEventArgs(Keys.Left));
        }

        public void ClickRight()
        {
            TreeView_KeyUp(this, new KeyEventArgs(Keys.Right));
        }

        public void ClickUp()
        {
            TreeView_KeyUp(this, new KeyEventArgs(Keys.Up));
        }

        public void ClickDown()
        {
            TreeView_KeyUp(this, new KeyEventArgs(Keys.Down));
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
                _activeServer = fileInfo.Server;
                UpdateNavButtons(node);
                _lastSelected = node;
            }
        }

        public string GetSelectedNodeText()
        {
            return _clicked.Text;
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

        #endregion
    }
}

public class LKContainerBrowser : PanoramaFolderBrowser
{
    private readonly bool _uploadPerms;
    private readonly List<KeyValuePair<PanoramaServer, JToken>> _listServerFolders = new List<KeyValuePair<PanoramaServer, JToken>>();

    public LKContainerBrowser(List<PanoramaServer> servers, string state, bool uploadPerms, string initialPath) : base(servers, state, initialPath)
    {
        _uploadPerms = uploadPerms;
        InitializeServers();
    }

    /// <summary>
    /// Initializes the JSON that will be
    /// used to build the TreeView of folders
    /// </summary>
    private void InitializeServers()
    {
        if (ServerList == null)
        {
            return;
        }

        var listErrorServers = new List<Tuple<PanoramaServer, string>>();
        foreach (var server in ServerList)
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

    private static string ServersToString(IEnumerable<Tuple<PanoramaServer, string>> servers)
    {
        return TextUtil.LineSeparate(servers.Select(t => TextUtil.LineSeparate(t.Item1.URI.ToString(), t.Item2)));
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

    public override void InitializeTreeView(TreeView tree)
    {
        foreach (var server in _listServerFolders)
        {
            InitializeFolder(tree, _uploadPerms, true, server.Value, server.Key);
        }
    }

    /// <summary>
    /// Builds a TreeView of folders using JSON data
    /// </summary>
    /// <param name="treeViewFolders"></param>
    /// <param name="requireUploadPerms"></param>
    /// <param name="showFiles"></param>
    /// <param name="folder"></param>
    /// <param name="server"></param>
    private void InitializeFolder(TreeView treeViewFolders, bool requireUploadPerms, bool showFiles, JToken folder, PanoramaServer server)
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
}


public class WebDavBrowser : PanoramaFolderBrowser
{
    public WebDavBrowser(PanoramaServer server, string state, string initialPath) 
        : base(new List<PanoramaServer> {server}, state, initialPath, true)
    {

    }

    public override void InitializeTreeView(TreeView treeView)
    {
        if (InitialPath != null)
        {
            _activeServer = ServerList.FirstOrDefault();
            InitializeTreeFromPath(treeView, _activeServer);
            AddWebDavFolders(treeView.SelectedNode);
            AddFilesForSelectedNode(treeView.SelectedNode, EventArgs.Empty);
        }
    }

    private void AddWebDavFolders(TreeNode node)
    {
        var listErrors = new List<Tuple<string, string>>();
        var folderInfo = GetFolderInformation(node);
        if (folderInfo != null)
        {
            try
            {
                var query = new Uri(string.Concat(_activeServer.URI, PanoramaUtil.WEBDAV, folderInfo.FolderPath, "?method=json"));
                var webClient = new WebClientWithCredentials(query, _activeServer.Username, _activeServer.Password);
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
                                Tag = new FolderInformation(_activeServer, string.Concat(folderInfo.FolderPath, @"/", fileName), false),
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
            catch (Exception ex)
            {
                var error = ex.Message;
                if (ex is WebException)
                {
                    listErrors.Add(new Tuple<string, string>(folderInfo.FolderPath, error ?? string.Empty));
                }
            }
        }
        
        if (listErrors.Count > 0)
        {
            throw new Exception(TextUtil.LineSeparate(Resources.WebDavBrowser_AddWebDavFolders_Failed_attempting_to_retrieve_information_from_the_following_folders_, TextUtil.LineSeparate(listErrors.Select(t => TextUtil.LineSeparate(t.Item1, t.Item2)))));
        }
    }

    /// <summary>
    /// Given a path to a folder, builds a TreeView of folders
    /// contained in the path
    /// </summary>
    /// <param name="treeViewFolders"></param>
    /// <param name="server"></param>
    public void InitializeTreeFromPath(TreeView treeViewFolders, PanoramaServer server)
    {
        var uriFolderTokens = InitialPath.Split('/');
        var treeNode = new TreeNode(server.URI.ToString())
        {
            Tag = new FolderInformation(server, string.Empty, false)
        };
        treeViewFolders.SelectedImageIndex = (int)ImageId.panorama;
        treeViewFolders.Nodes.Add(treeNode);

        var fileNode = new TreeNode(PanoramaUtil.FILES);
        var selectedFolder = LoadFromPath(uriFolderTokens, treeNode, server);
        selectedFolder.Nodes.Add(fileNode);
        treeViewFolders.SelectedImageIndex = treeViewFolders.ImageIndex = (int)ImageId.folder;
        if (selectedFolder.Tag is FolderInformation folderInfo)
            fileNode.Tag = new FolderInformation(server, string.Concat(folderInfo.FolderPath, PanoramaUtil.FILES_W_SLASH), false);
        selectedFolder.Expand();
        treeViewFolders.SelectedNode = fileNode;
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

    protected override string GetSelectedUri(string folderPath)
    {
        return _activeServer != null && folderPath != null
            ? string.Concat(_activeServer.URI, PanoramaUtil.WEBDAV_W_SLASH, folderPath.TrimStart('/'))
            : string.Empty;
    }
}
/// <summary>
/// This class is used for testing purposes
/// </summary>
public class TestPanoramaFolderBrowser : LKContainerBrowser
{
    private PanoramaServer _server;
    private JToken _folderJson;

    public TestPanoramaFolderBrowser(PanoramaServer server, JToken folderJson) :
        base(new List<PanoramaServer>(), null, false, string.Empty)
    {
        _server = server;
        _folderJson = folderJson;
        _activeServer = server;
    }

    public override void InitializeTreeView(TreeView treeViewFolders)
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
