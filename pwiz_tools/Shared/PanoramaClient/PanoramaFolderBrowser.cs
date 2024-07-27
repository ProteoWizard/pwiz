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
        private readonly TreeViewStateRestorer _restorer;
        private TreeNode _selectedNode;

        protected PanoramaServer ActiveServer { get; set; }

        protected List<PanoramaServer> ServerList { get; }
        protected string InitialPath { get; }

        public event EventHandler NodeClick;
        public event EventHandler AddFiles;

        public string TreeState { get; private set; }

        protected PanoramaFolderBrowser(List<PanoramaServer> servers, string state, string selectedPath = null)
        {
            InitializeComponent();
            ServerList = servers;
            TreeState = state;
            InitialPath = selectedPath?.TrimEnd('/');
            _restorer = new TreeViewStateRestorer(treeView);
        }

        public abstract void InitializeTreeView(TreeView treeView);
        public abstract void DynamicLoad(TreeNode node);

        /// <summary>
        /// Builds the TreeView of folders and restores any
        /// previous state of the TreeView
        /// </summary>
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
                ActiveServer = ServerList.FirstOrDefault();
                tree.TopNode?.Expand();
            }

            if (string.IsNullOrEmpty(InitialPath) || GetType() == typeof(WebDavBrowser))
                return;
            var uriFolderTokens = InitialPath.Split('/');
            SelectNode(uriFolderTokens.LastOrDefault());
        }

        /// <summary>
        /// When a node is clicked on, add any corresponding files 
        /// </summary>
        private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (_selectedNode == null || !_selectedNode.Equals(e.Node))
            {
                var hitTest = treeView.HitTest(e.Location);
                if (hitTest.Location == TreeViewHitTestLocations.Label || hitTest.Location == TreeViewHitTestLocations.Image)
                {
                    var folderInfo = GetFolderInformation(e.Node);
                    if (folderInfo != null)
                    {
                        treeView.SelectedNode = e.Node;
                        treeView.Focus();
                        ActiveServer = folderInfo.Server;
                        UpdateNavButtons(e.Node);
                    }
                } 
            }
        }

        /// <summary>
        /// This method gets used in the WebDav folder browser in order to load @files node subfolders before
        /// they get expanded
        /// </summary>
        private void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            DynamicLoad(e.Node);
        }

        protected FolderInformation GetFolderInformation(TreeNode node)
        {
            return node?.Tag as FolderInformation;
        }

        public virtual string GetSelectedUri()
        {
            return GetSelectedUri(this, false);
        }

        public static string GetSelectedUri(PanoramaFolderBrowser browser, bool webdav)
        {
            var folderInfo = browser.GetFolderInformation(browser._selectedNode);
            return folderInfo != null
                ? string.Concat(folderInfo.Server.URI,
                    webdav ? PanoramaUtil.WEBDAV_W_SLASH : string.Empty, folderInfo.FolderPath.TrimStart('/'))
                : string.Empty;
        }

        public void UpButtonClick()
        {
            if (_selectedNode?.Parent != null)
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
                var parent = _selectedNode.Parent;
                _priorNode = parent;
                UpdateNavData(parent);
            }
        }
        /// <summary>
        /// Enabled if a root node is not clicked
        /// </summary>
        public bool UpEnabled => _selectedNode is { Parent: { } };

        public void BackButtonClick()
        {
            if (_next.Count != 0)
            {
                if (!_next.Peek().Equals(_selectedNode))
                {
                    _next.Push(_selectedNode);
                }
            }
            else
            {
                _next.Push(_selectedNode);
            }
            var prior = _previous.Pop();
            _priorNode = prior;
            UpdateNavData(prior);
        }

        public bool BackEnabled => _previous != null && _previous.Count != 0;

        public void ForwardButtonClick()
        {
            if (_previous.Count != 0)
            {
                if (!_previous.Peek().Equals(_selectedNode))
                {
                    _previous.Push(_selectedNode);
                }
            }
            else
            {
                _previous.Push(_selectedNode);
            }
            var nextNode = _next.Pop();
            UpdateNavData(nextNode);
        }

        private void UpdateNavData(TreeNode node)
        {
            treeView.SelectedNode = node;
            _selectedNode = node;
            treeView.Focus();
            AddFiles?.Invoke(this, EventArgs.Empty);
        }

        public bool ForwardEnabled => _next != null && _next.Count != 0;

        public string GetClosingTreeState()
        { 
            return TreeState = _restorer.GetPersistentString();
        }

        /// <summary>
        /// If there was a previous selection made in FilePicker, reload the files in the
        /// selected folder
        /// </summary>
        protected void AddFilesForSelectedNode(TreeNode node, EventArgs e)
        {
            var folderInfo = GetFolderInformation(node);
            if (folderInfo != null)
            {
                _priorNode = node;
                _selectedNode = node;
                treeView.Focus();
                AddFiles?.Invoke(this, e);
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
                _selectedNode = node;
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
            return (_selectedNode?.Tag as FolderInformation)?.FolderPath;
        }

        public string GetFolderPath()
        {
            var folderInfo = GetFolderInformation(_selectedNode);
            return folderInfo?.FolderPath ?? string.Empty;
        }

        public bool GetNodeIsTargetedMS()
        {
            var folderInfo = GetFolderInformation(_selectedNode);
            return folderInfo?.IsTargetedMS ?? false;
        }

        public PanoramaServer GetActiveServer()
        {
            var folderInfo = GetFolderInformation(_selectedNode);
            return folderInfo?.Server;
        }

        #region Test Support
        public int TreeviewIcon => treeView.SelectedNode.ImageIndex;

        public bool IsSelected(string nodeName)
        {
            var node = SearchTree(treeView.Nodes, nodeName);
            return node is { IsSelected: true };
        }

        public bool SelectNode(string nodeName)
        {
            var node = SearchTree(treeView.Nodes, nodeName);
            if (node?.Tag is FolderInformation)
            {
                UpdateNavButtons(node);
                return true;
            }

            return false;
        }

        public string SelectedNodeText => _selectedNode.Text;

        /// <summary>
        /// Searches for a given TreeNode in a TreeView and
        /// returns the TreeNode if it is found, used for testing
        /// </summary>
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

    public override void DynamicLoad(TreeNode node)
    {
        // Do nothing
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

                    listErrorServers.Add(new Tuple<PanoramaServer, string>(server, ex.Message ?? string.Empty));
                }
            }
        }
        if (listErrorServers.Count > 0)
        {
            throw new Exception(CommonTextUtil.LineSeparate(Resources.PanoramaFolderBrowser_InitializeServers_Failed_attempting_to_retrieve_information_from_the_following_servers,
                string.Empty,
                ServersToString(listErrorServers)));
        }
    }

    private static string ServersToString(IEnumerable<Tuple<PanoramaServer, string>> servers)
    {
        return CommonTextUtil.LineSeparate(servers.Select(t => CommonTextUtil.LineSeparate(t.Item1.URI.ToString(), t.Item2)));
    }

    /// <summary>
    /// Generates JSON containing the folder structure for the given server
    /// </summary>
    public virtual void InitializeTreeServers(PanoramaServer server, List<KeyValuePair<PanoramaServer, JToken>> listServers)
    {
        IPanoramaClient panoramaClient = new WebPanoramaClient(server.URI, server.Username, server.Password);
        listServers.Add(new KeyValuePair<PanoramaServer, JToken>(server, panoramaClient.GetInfoForFolders(null)));
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
    public static void AddChildContainers(TreeNode node, JToken folder, bool requireUploadPerms, bool showFiles, PanoramaServer server)
    {
        var subFolders = folder[@"children"].Children();
        foreach (var subFolder in subFolders)
        {
            if (!PanoramaUtil.CheckReadPermissions(subFolder))
            {
                // Do not add the folder if user does not have read permissions in the folder. 
                // Any subfolders, even if they have read permissions, will also not be added.
                continue;
            }

            var folderName = (string)subFolder[@"name"];

            var folderNode = new TreeNode(folderName);
            AddChildContainers(folderNode, subFolder, requireUploadPerms, showFiles, server);

            var hasTargetedMsModule = PanoramaUtil.HasTargetedMsModule(subFolder);
            // User can only upload to folders where TargetedMS is an active module.
            var canUpload = hasTargetedMsModule && PanoramaUtil.CheckInsertPermissions(subFolder);

            if (requireUploadPerms && folderNode.Nodes.Count == 0 && !canUpload)
            {
                // If the user does not have write permissions in this folder or any
                // of its subfolders, do not add it to the tree.
                continue;
            }

            node.Nodes.Add(folderNode);

            if (requireUploadPerms && !(hasTargetedMsModule && canUpload))
            {
                // User cannot upload files to folder
                folderNode.ForeColor = Color.Gray;
                folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.folder;
            }
            else if (!hasTargetedMsModule)
            {
                folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.folder;
            }
            else
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

            if (showFiles)
            {
                folderNode.Tag = new FolderInformation(server, (string)subFolder[@"path"], hasTargetedMsModule);
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
        : base(new List<PanoramaServer> {server}, state, initialPath)
    {

    }

    public override void InitializeTreeView(TreeView treeView)
    {
        if (InitialPath != null)
        {
            ActiveServer = ServerList.FirstOrDefault();
            InitializeTreeFromPath(treeView, ActiveServer);
            AddWebDavFolders(treeView.SelectedNode);
            AddFilesForSelectedNode(treeView.SelectedNode, EventArgs.Empty);
        }
    }

    private void AddWebDavFolders(TreeNode node)
    {
        var listErrors = new List<Tuple<string, string, string>>();
        var folderInfo = GetFolderInformation(node);
        if (folderInfo != null)
        {
            Uri query = null;
            try
            {
                query = new Uri(string.Concat(folderInfo.Server.URI, PanoramaUtil.WEBDAV, folderInfo.FolderPath, "?method=json"));
                using var requestHelper = new PanoramaRequestHelper(new WebClientWithCredentials(query, folderInfo.Server.Username, folderInfo.Server.Password));
                JToken json = requestHelper.Get(query);
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
                            if (!canRead || fileName.Equals("assaydata"))
                            {
                                continue;
                            }

                            var newNode = new TreeNode(fileName)
                            {
                                Tag = new FolderInformation(ActiveServer, string.Concat(folderInfo.FolderPath, @"/", fileName), false),
                                ImageIndex = (int)ImageId.folder, SelectedImageIndex = (int)ImageId.folder
                            };
                            CreateDummyNode(newNode);
                            node.Nodes.Add(newNode);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                listErrors.Add(new Tuple<string, string, string>(error ?? string.Empty, folderInfo.FolderPath, query?.ToString() ?? string.Empty));
            }
        }
        
        if (listErrors.Count > 0)
        {
            throw new Exception(CommonTextUtil.LineSeparate(
                Resources
                    .WebDavBrowser_AddWebDavFolders_Failed_attempting_to_retrieve_information_from_the_following_folders_,
                CommonTextUtil.LineSeparate(listErrors.Select(t =>
                    CommonTextUtil.LineSeparate(t.Item1, t.Item2, t.Item3)))));
        }
    }

    private static void CreateDummyNode(TreeNode node)
    {
        var dummyNode = new TreeNode();
        node.Nodes.Add(dummyNode);
    }

    private static bool IsDummyNode(TreeNode node)
    {
        return node.Text.Equals(string.Empty);
    }

    public override void DynamicLoad(TreeNode node)
    {
        if (IsDummyNode(node.FirstNode))
        {
            node.FirstNode.Remove();
            AddWebDavFolders(node);

        }
    }

    /// <summary>
    /// Given a path to a folder, builds a TreeView of folders
    /// contained in the path
    /// </summary>
    private void InitializeTreeFromPath(TreeView treeViewFolders, PanoramaServer server)
    {
        var uriFolderTokens = InitialPath.Split('/');
        var treeNode = new TreeNode(server.URI.ToString())
        {
            Tag = new FolderInformation(server, string.Empty, false)
        };
        treeViewFolders.SelectedImageIndex = (int)ImageId.panorama;
        treeViewFolders.Nodes.Add(treeNode);
        var selectedFolder = LoadFromPath(uriFolderTokens, treeNode, server);
        treeViewFolders.SelectedNode = selectedFolder;
        var lastFolder = uriFolderTokens.LastOrDefault();
        if (lastFolder != null && !lastFolder.Equals(PanoramaUtil.FILES))
        {
            var fileNode = new TreeNode(PanoramaUtil.FILES);
            selectedFolder.Nodes.Add(fileNode);
            treeViewFolders.SelectedNode = fileNode;
            if (selectedFolder.Tag is FolderInformation folderInfo)
                fileNode.Tag = new FolderInformation(server, string.Concat(folderInfo.FolderPath, PanoramaUtil.FILES_W_SLASH), false);
        }
        treeViewFolders.SelectedImageIndex = treeViewFolders.ImageIndex = (int)ImageId.folder;
        selectedFolder.Expand();
    }

    /// <summary>
    /// Given an array of folders, add each folder to a TreeView
    /// </summary>
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

    public override string GetSelectedUri()
    {
        return GetSelectedUri(this, true);
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
        ActiveServer = server;
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
