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
using System.IO;
using System.Linq;
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
        /// <summary>
        /// Root folder to limit the scope of the folder tree request.
        /// If set, only this folder (and its subfolders) will be requested from the server,
        /// significantly reducing response size for tests that only need a specific project folder.
        /// For example, "SkylineTest" or "Panorama Public".
        /// </summary>
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
        public abstract void LoadServerData(IProgressMonitor progressMonitor);
        public abstract void DynamicLoad(TreeNode node);

        /// <summary>
        /// Populates the TreeView with data after server data has been loaded.
        /// Must be called on the UI thread.
        /// Note: This is no longer called automatically from FolderBrowser_Load.
        /// It should be called explicitly from the parent form's Load event (e.g., FilePicker_Load)
        /// after server data has been loaded.
        /// </summary>
        public void PopulateTreeView()
        {
            InitializeTreeView(treeView);
            InitializeTreeState(treeView);
        }

        private void InitializeTreeState(TreeView tree)
        {
            // Get the server from the root tree node (if it exists) or fallback to first server
            var rootNode = tree.TopNode;
            if (rootNode != null)
            {
                var rootFolderInfo = GetFolderInformation(rootNode);
                if (rootFolderInfo != null)
                {
                    ActiveServer = rootFolderInfo.Server;
                }
            }
            
            // Fallback to first server if we couldn't get it from the tree
            ActiveServer ??= ServerList.FirstOrDefault();
            
            // Determine which path to use for initial selection
            // Use InitialPath if provided, otherwise use FolderPath from the first server
            string initialPath = InitialPath;
            if (string.IsNullOrEmpty(initialPath) && GetType() != typeof(WebDavBrowser) && ActiveServer != null)
            {
                initialPath = ActiveServer.FolderPath;
            }
            
            // Check if existing TreeState is compatible with the current tree structure
            bool shouldUseTreeState = !string.IsNullOrEmpty(TreeState);
            if (shouldUseTreeState)
            {
                // Validate TreeState compatibility by checking all node indices are within bounds
                shouldUseTreeState = _restorer.IsTreeStateCompatible(TreeState);
            }
            
            if (shouldUseTreeState)
            {
                // Use existing TreeState
                _restorer.RestoreExpansionAndSelection(TreeState);
                _restorer.UpdateTopNode();
                AddFilesForSelectedNode(treeView.SelectedNode, EventArgs.Empty);
            }
            else if (!string.IsNullOrEmpty(initialPath) && GetType() != typeof(WebDavBrowser) && rootNode != null)
            {
                // Generate new TreeState for the initial path by finding actual nodes in the tree
                var segments = initialPath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
                string newTreeState = _restorer.GenerateTreeStateForPath(segments, rootNode);
                
                if (newTreeState != null)
                {
                    _restorer.RestoreExpansionAndSelection(newTreeState);
                    _restorer.UpdateTopNode();
                    AddFilesForSelectedNode(treeView.SelectedNode, EventArgs.Empty);
                }
                else
                {
                    // Path not found - just expand root
                    tree.TopNode?.Expand();
                }
            }
            else
            {
                // No path and no TreeState - just expand root
                tree.TopNode?.Expand();
            }
            UpdateNavButtons(_selectedNode);
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
            if (folderInfo == null)
                return string.Empty;
            
            // Use PanoramaServer.GetUri() to handle path normalization and avoid double slashes
            return folderInfo.Server.GetUri(folderInfo.FolderPath, webdav);
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

        protected void UpdateNavButtons(TreeNode node)
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
                // Expand the node if it's not already expanded, so its children are loaded (for lazy-loaded nodes like WebDav folders)
                if (!node.IsExpanded)
                {
                    node.Expand();
                }
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
    // JSON property name constants for folder structure
    private const string JSON_PROP_CHILDREN = @"children";
    private const string JSON_PROP_NAME = @"name";
    private const string JSON_PROP_PATH = @"path";
    private const string JSON_PROP_TITLE = @"title";
    private const string JSON_PROP_PARENT_PATH = @"parentPath";
    private const string JSON_PROP_PARENT_ID = @"parentId";
    private const string JSON_PROP_MODULE_PROPERTIES = @"moduleProperties";
    private const string JSON_PROP_EFFECTIVE_VALUE = @"effectiveValue";
    private const string JSON_PROP_LIBRARY = @"Library";
    private const string JSON_PROP_LIBRARY_PROTEIN = @"LibraryProtein";

    // Properties that should not be copied when wrapping folder responses
    private static readonly string[] DO_NOT_COPY_PROPERTIES =
    {
        JSON_PROP_NAME,
        JSON_PROP_PATH,
        JSON_PROP_TITLE,
        JSON_PROP_CHILDREN,
        JSON_PROP_PARENT_PATH,
        JSON_PROP_PARENT_ID
    };

    private readonly bool _uploadPerms;
    private readonly List<KeyValuePair<PanoramaServer, JToken>> _listServerFolders = new List<KeyValuePair<PanoramaServer, JToken>>();

    public LKContainerBrowser(List<PanoramaServer> servers, string state, bool uploadPerms, string initialPath) 
        : base(servers, state, initialPath)
    {
        _uploadPerms = uploadPerms;
        // Note: InitializeServers() must be called explicitly on a background thread after control creation
        // Control creation must happen on the UI thread to avoid thread handle leaks
    }

    public override void DynamicLoad(TreeNode node)
    {
        // Do nothing
    }

    /// <summary>
    /// Fetches server folder data from the web on a background thread.
    /// Must be called after control creation on the UI thread.
    /// This method should NOT create any UI controls.
    /// </summary>
    public override void LoadServerData(IProgressMonitor progressMonitor)
    {
        InitializeServers(progressMonitor);
    }

    /// <summary>
    /// Initializes the JSON that will be
    /// used to build the TreeView of folders
    /// </summary>
    private void InitializeServers(IProgressMonitor progressMonitor)
    {
        if (ServerList == null)
        {
            return;
        }

        var listErrorServers = new List<Tuple<PanoramaServer, string>>();
        IProgressStatus progressStatus = new ProgressStatus(Resources.PanoramaFolderBrowser_InitializeServers_Requesting_remote_server_folders);
        
        for (int i = 0; i < ServerList.Count; i++)
        {
            var server = ServerList[i];
            
            // Update progress for multiple servers after the first server
            if (i > 0)
            {
                progressStatus = progressStatus.ChangePercentComplete(i * 100 / ServerList.Count);
                progressMonitor.UpdateProgress(progressStatus);
            }
            
            // Check for cancellation
            if (progressMonitor is { IsCanceled: true })
                throw new OperationCanceledException();
            
            try
            {
                InitializeTreeServers(server, _listServerFolders, progressMonitor, progressStatus);
            }
            catch (IOException ex)
            {
                // Network errors are expected when servers are unreachable
                // NetworkRequestException extends IOException
                listErrorServers.Add(new Tuple<PanoramaServer, string>(server, ex.Message ?? string.Empty));
            }
            // Let all other exceptions propagate (ArgumentException, NullReferenceException, etc. are programming defects)
        }
        if (listErrorServers.Count > 0)
        {
            throw new IOException(CommonTextUtil.LineSeparate(
                Resources.PanoramaFolderBrowser_InitializeServers_Failed_attempting_to_retrieve_information_from_the_following_servers_,
                string.Empty,
                ServersToString(listErrorServers)));
        }
    }

    private static string ServersToString(IEnumerable<Tuple<PanoramaServer, string>> servers)
    {
        return CommonTextUtil.LineSeparate(servers.Select(t => CommonTextUtil.LineSeparate(t.Item1.URI.ToString(), t.Item2)));
    }

    /// <summary>
    /// Generates JSON containing the folder structure for the given server.
    /// If server.FolderPath is set, the server returns only that folder's children (much faster).
    /// We then wrap the response to include the FolderPath as the root node so the TreeView displays it correctly.
    /// </summary>
    public virtual void InitializeTreeServers(PanoramaServer server, List<KeyValuePair<PanoramaServer, JToken>> listServers, 
        IProgressMonitor progressMonitor, IProgressStatus progressStatus)
    {
        IPanoramaClient panoramaClient = new WebPanoramaClient(server.URI, server.Username, server.Password);
        
        // Request folders from server, using its FolderPath which may be null for the entire server
        JToken folderJson = panoramaClient.GetInfoForFolders(server.FolderPath, progressMonitor, progressStatus);
        
        // If FolderPath is set, wrap the response to include the folder path as the root node
        // This ensures the TreeView displays the folder (e.g., "SkylineTest") even though
        // the server only returned its children
        if (!string.IsNullOrEmpty(server.FolderPath) && folderJson != null)
        {
            folderJson = WrapFolderResponse(folderJson, server.FolderPath, server.URI);
        }
        
        listServers.Add(new KeyValuePair<PanoramaServer, JToken>(server, folderJson));
    }

    /// <summary>
    /// Wraps a folder response JSON to include the specified folder path as a nested tree structure.
    /// This is needed when requesting a specific folder, as the server returns only that folder's children,
    /// not the folder itself or its parent folders.
    /// 
    /// For example, if folderPath = "SkylineTest/Part1/Part2", this builds:
    ///   SkylineTest (path: "/SkylineTest", parentPath: "/")
    ///     Part1 (path: "/SkylineTest/Part1", parentPath: "/SkylineTest")
    ///       Part2 (path: "/SkylineTest/Part1/Part2", parentPath: "/SkylineTest/Part1")
    ///         [actual response children]
    /// 
    /// Note: folderPath is assumed to be URL-encoded (e.g., "Panorama%20Public"). Each segment
    /// is decoded before being used in the JSON structure.
    /// </summary>
    public static JToken WrapFolderResponse(JToken folderJson, string folderPath, Uri baseServerUri)
    {
        if (folderJson == null)
            return null;

        // Build the folder tree structure
        var (rootFolder, leafFolder) = BuildFolderTree(folderPath);
        
        if (rootFolder == null || leafFolder == null)
            return folderJson;
        
        // Copy moduleProperties and other properties from the first child if available
        // This ensures the leaf folder (and all parent folders) have the same properties as their children
        // This is critical for effectivePermissions - synthetic folders need this to pass HasReadPermissions check
        if (folderJson[JSON_PROP_CHILDREN] is JArray childrenArray && childrenArray.Count > 0)
        {
            if (childrenArray[0] is JObject firstChild)
            {
                // Propagate properties from first child to all folders in the path (root to leaf)
                // This ensures all synthetic folders have effectivePermissions and other required properties
                PropagatePropertiesToAllFolders(rootFolder, leafFolder, firstChild);
            }
        }
        
        // The children of the leaf folder are the children returned by the server
        leafFolder[JSON_PROP_CHILDREN] = folderJson[JSON_PROP_CHILDREN]?.DeepClone() ?? new JArray();

        return WrapFolderResponse(folderJson, rootFolder);
    }

    /// <summary>
    /// Builds a nested folder tree structure from a folder path.
    /// 
    /// For example, if folderPath = "SkylineTest/Part1/Part2", this builds:
    ///   SkylineTest (path: "/SkylineTest", parentPath: "/")
    ///     Part1 (path: "/SkylineTest/Part1", parentPath: "/SkylineTest")
    ///       Part2 (path: "/SkylineTest/Part1/Part2", parentPath: "/SkylineTest/Part1")
    /// 
    /// Note: folderPath is assumed to be URL-encoded (e.g., "Panorama%20Public"). Each segment
    /// is decoded before being used in the JSON structure.
    /// </summary>
    /// <param name="folderPath">The folder path (URL-encoded, e.g., "Panorama%20Public" or "SkylineTest/Part1/Part2")</param>
    /// <returns>A tuple of (rootFolder, leafFolder) where rootFolder is the top-level folder node
    /// and leafFolder is the deepest folder node in the tree</returns>
    private static (JObject rootFolder, JObject leafFolder) BuildFolderTree(string folderPath)
    {
        // Split folderPath into segments and decode each one
        // folderPath is server.FolderPath which is URL-encoded (e.g., "Panorama%20Public" or "SkylineTest/Part1/Part2")
        var encodedSegments = folderPath.Split('/');
        var decodedSegments = encodedSegments.Select(Uri.UnescapeDataString).ToArray();

        if (decodedSegments.Length == 0)
            return (null, null);

        JObject rootFolder = null;
        JObject currentFolder = null;
        string currentPath = string.Empty;

        // Build nested structure from root to leaf
        foreach (var segmentName in decodedSegments)
        {
            var segmentPath = currentPath.Length == 0 ? @"/" + segmentName : currentPath + @"/" + segmentName;

            var folderNode = new JObject();
            folderNode[JSON_PROP_NAME] = segmentName;
            folderNode[JSON_PROP_PATH] = segmentPath;
            folderNode[JSON_PROP_TITLE] = segmentName;
            folderNode[JSON_PROP_PARENT_PATH] = currentPath.Length == 0 ? @"/" : currentPath;
            folderNode[JSON_PROP_CHILDREN] = new JArray(); // Will be populated with children

            // Add this folder as a child of the previous folder (or track as root if first iteration)
            if (currentFolder == null)
            {
                // This is the root segment - track it separately
                rootFolder = folderNode;
            }
            else
            {
                // Add as child of previous folder
                var childrenArray = currentFolder[JSON_PROP_CHILDREN] as JArray;
                childrenArray?.Add(folderNode);
            }

            // Move to next level
            currentFolder = folderNode;
            currentPath = segmentPath;
        }

        return (rootFolder, currentFolder);
    }

    private static JToken WrapFolderResponse(JToken folderJson, JObject rootFolder)
    {
        // Copy top-level properties from the original response (formats, etc.)
        var wrappedResponse = new JObject();
        if (folderJson is JObject originalObj)
        {
            CopyProperties(originalObj, wrappedResponse,
                prop => prop.Name != JSON_PROP_CHILDREN);
        }

        // Wrap the root folder in a children array (the structure expected by InitializeFolder)
        wrappedResponse[JSON_PROP_CHILDREN] = new JArray { rootFolder };
        
        return wrappedResponse;
    }

    /// <summary>
    /// Propagates properties (especially effectivePermissions) from a source folder to all folders
    /// in the tree path from root to leaf. Traverses down from root to leaf, copying properties
    /// to each folder along the path.
    /// This ensures all synthetic folders created by BuildFolderTree have the required permission properties.
    /// </summary>
    private static void PropagatePropertiesToAllFolders(JObject rootFolder, JObject leafFolder, JObject sourceFolder)
    {
        // Traverse from root down to leaf, copying properties to each folder along the path
        var targetPath = (string)leafFolder[JSON_PROP_PATH];
        var rootPath = (string)rootFolder[JSON_PROP_PATH];
        if (targetPath == null || rootPath == null)
            return; // Should not happen, but to be safe and avoid ReShaper warnings

        // Split paths into segments (filter out empty segments from leading/trailing slashes)
        var targetSegments = targetPath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToList();
        var rootSegments = rootPath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToList();
        
        // Get segments from root to leaf (skip root segments since we start there)
        var pathSegments = targetSegments.Skip(rootSegments.Count).ToList();
        
        // Start from root folder - copy properties to it
        CopyProperties(sourceFolder, rootFolder,
            prop => DO_NOT_COPY_PROPERTIES.All(p => prop.Name != p));
        
        // Traverse down from root to leaf, copying properties to each folder
        var currentFolder = rootFolder;
        foreach (var segment in pathSegments)
        {
            // Move to next folder in path (find child with matching name)
            if (currentFolder[JSON_PROP_CHILDREN] is JArray children)
            {
                var nextFolder = children.FirstOrDefault(child => 
                    child is JObject childObj && 
                    (string)childObj[JSON_PROP_NAME] == segment) as JObject;
                
                if (nextFolder != null)
                {
                    // Copy properties to this folder
                    CopyProperties(sourceFolder, nextFolder,
                        prop => DO_NOT_COPY_PROPERTIES.All(p => prop.Name != p));
                    
                    currentFolder = nextFolder;
                }
                else
                {
                    // Should not happen - we built the tree with all segments
                    break;
                }
            }
            else
            {
                break;
            }
        }
    }
    
    private static void CopyProperties(JObject sourceObj, JObject destinationObj, Func<JProperty, bool> includeProp)
    {
        foreach (var property in sourceObj.Properties())
        {
            if (includeProp(property))
            {
                destinationObj[property.Name] = property.Value.DeepClone();
            }
        }
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
        var subFolders = folder[JSON_PROP_CHILDREN].Children();
        foreach (var subFolder in subFolders)
        {
            if (!PanoramaUtil.HasReadPermissions(subFolder))
            {
                // Do not add the folder if user does not have read permissions in the folder. 
                // Any subfolders, even if they have read permissions, will also not be added.
                continue;
            }

            var folderName = (string)subFolder[JSON_PROP_NAME];

            var folderNode = new TreeNode(folderName);
            AddChildContainers(folderNode, subFolder, requireUploadPerms, showFiles, server);

            var hasTargetedMsModule = PanoramaUtil.HasTargetedMsModule(subFolder);
            // User can only upload to folders where TargetedMS is an active module.
            var canUpload = hasTargetedMsModule && PanoramaUtil.HasUploadPermissions(subFolder);

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
                var moduleProperties = subFolder[JSON_PROP_MODULE_PROPERTIES];
                if (moduleProperties == null)
                    folderNode.ImageIndex = folderNode.SelectedImageIndex = (int)ImageId.labkey;
                else
                {
                    var effectiveValue = (string)moduleProperties[0]![JSON_PROP_EFFECTIVE_VALUE];

                    folderNode.ImageIndex =
                        folderNode.SelectedImageIndex =
                            (effectiveValue!.Equals(JSON_PROP_LIBRARY) || effectiveValue.Equals(JSON_PROP_LIBRARY_PROTEIN))
                                ? (int)ImageId.chrom_lib
                                : (int)ImageId.labkey;
                }
            }

            if (showFiles)
            {
                folderNode.Tag = new FolderInformation(server, (string)subFolder[JSON_PROP_PATH], hasTargetedMsModule);
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

    /// <summary>
    /// Fetches server folder data from the web on a background thread.
    /// Must be called after control creation on the UI thread.
    /// This method should NOT create any UI controls.
    /// </summary>
    public override void LoadServerData(IProgressMonitor progressMonitor)
    {
        // For WebDavBrowser, the tree structure is built from InitialPath in InitializeTreeView (no HTTP needed)
        // Folder data is loaded lazily via DynamicLoad when nodes are expanded
        // Files are loaded when nodes are selected via AddFilesForSelectedNode
        // So there's no bulk data loading needed here - just mark as loaded so PopulateTreeView knows to proceed
    }

    public override void InitializeTreeView(TreeView treeView)
    {
        if (InitialPath != null)
        {
            ActiveServer = ServerList.FirstOrDefault();
            // Build the tree structure from the path (no HTTP requests - this is fast)
            InitializeTreeFromPath(treeView, ActiveServer);
            
            // Don't call AddWebDavFolders or AddFilesForSelectedNode here - they make HTTP requests
            // AddWebDavFolders will be called lazily via DynamicLoad when nodes are expanded
            // AddFilesForSelectedNode will be called when the node is actually selected/clicked
            // This avoids blocking the UI thread during form load
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
                // Use GetUri() to construct the webdav URI properly, handling slashes and server.FolderPath
                // GetFullPath() returns path with leading slash, but GetUri() handles that
                string folderPath = folderInfo.Server.GetFullPath(folderInfo.FolderPath);
                string uriString = folderInfo.Server.GetUri(folderPath, webdav: true);
                query = new Uri(uriString + @"?method=json");
                using var requestHelper = new HttpPanoramaRequestHelper(folderInfo.Server);
                JToken json = requestHelper.Get(query);
                if ((int)json[@"fileCount"] != 0)
                {
                    var files = json[@"files"];
                    foreach (var file in files!)
                    {
                        var fileName = (string)file[@"text"];
                        var isFile = (bool)file[@"leaf"];
                        if (!isFile)
                        {
                            var canRead = (bool)file[@"canRead"];
                            if (!canRead || fileName!.Equals(@"assaydata"))
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
            // Create a dummy node so that DynamicLoad will be called when @files is expanded
            // This follows the same lazy-loading pattern used for other WebDav folders created by AddWebDavFolders
            CreateDummyNode(fileNode);
        }
        treeViewFolders.SelectedImageIndex = treeViewFolders.ImageIndex = (int)ImageId.folder;
        selectedFolder.Expand();
        
        // After setting the selected node, ensure the selection is properly handled
        // This triggers UpdateNavButtons which sets _selectedNode and fires AddFiles event
        if (treeViewFolders.SelectedNode != null)
        {
            UpdateNavButtons(treeViewFolders.SelectedNode);
        }
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
        List<KeyValuePair<PanoramaServer, JToken>> listServers,
        IProgressMonitor progressMonitor, IProgressStatus progressStatus)
    {
        // Do nothing - test class uses pre-loaded JSON
    }
}
