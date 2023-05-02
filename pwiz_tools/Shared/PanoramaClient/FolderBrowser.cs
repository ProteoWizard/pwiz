using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace pwiz.PanoramaClient
{
    public partial class FolderBrowser : UserControl
    {
        private bool _uploadPerms;
        private PanoramaFormUtil _formUtil;
          // NOTE (vsharma): make this a property with private setter. Same for all other public variables.
        private Stack<TreeNode> _previous = new Stack<TreeNode>();
        private TreeNode _priorNode;
        private Stack<TreeNode> _next = new Stack<TreeNode>();
        private readonly List<PanoramaServer> _serverList;// = new List<PanoramaServer>();
        private TreeNode _lastSelected;
        private TreeViewStateRestorer _restorer;
        private PanoramaServer _server;
        private JToken _folderJson;

        //Needs to take server information
        public FolderBrowser(bool uploadPerms, bool showSkyFolders, string state, List<PanoramaServer> servers)
        {
            InitializeComponent();
            treeView.ImageList = imageList1;
            _serverList = servers;

            _uploadPerms = uploadPerms;
            ShowSky = showSkyFolders;
            State = state;
            _restorer = new TreeViewStateRestorer(treeView);
        }

        public FolderBrowser(PanoramaServer server, JToken folderJson)
        {
            InitializeComponent();
            treeView.ImageList = imageList1;
            _restorer = new TreeViewStateRestorer(treeView);
            _server = server;
            _folderJson = folderJson;
        }

        public event EventHandler NodeClick;
        public event EventHandler AddFiles;
        public string FolderPath { get; private set; }
        public TreeNode Clicked { get; private set; }
        public bool ShowSky { get; private set; }
        public string Path { get; private set; }
        public string State { get; private set; }
        public PanoramaServer ActiveServer { get; private set; }
        public bool Testing = false;
        public int NodeCount { get; private set; }

        private void FolderBrowser_Load(object sender, EventArgs e)
        {
            if (_serverList != null)
            {
                _formUtil = new PanoramaFormUtil();

                foreach (var server in _serverList)
                {
                    _formUtil.InitializeTreeView(server, treeView, _uploadPerms, true, ShowSky);
                }

                if (!string.IsNullOrEmpty(State))
                {
                    _restorer.RestoreExpansionAndSelection(State);
                    _restorer.UpdateTopNode();
                    AddSelectedFiles(treeView.Nodes, EventArgs.Empty);
                }
                else
                {
                    treeView.TopNode.Expand();
                }
                NodeCount = treeView.Nodes.Count;
            }
            else
            {
                _formUtil = new PanoramaFormUtil();
                _formUtil.InitializeTreeViewTest(_server, treeView, _folderJson);
            }
        }


        public void SwitchFolderType(bool type)
        {
            treeView.Nodes.Clear();
            ShowSky = type;
            foreach (var server in _serverList)
            {
                _formUtil.InitializeTreeView(server, treeView, _uploadPerms, true, type);
            }
            _next.Clear();
            _previous.Clear();
            Clicked = null;
            _lastSelected = null;
            _priorNode = null;
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            _lastSelected = e.Node;
            if (e.Node.Tag != null)
            {
                FolderPath = e.Node.Tag.ToString();
            }
        }

        public void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (_lastSelected == null || (_lastSelected != null && !_lastSelected.Equals(e.Node)))
            {
                if (e.Node.Bounds.Contains(e.Location))
                {
                    var hit = e.Node.TreeView.HitTest(e.Location);
                    if (hit.Location != TreeViewHitTestLocations.PlusMinus)
                    {
                        ClearTreeRecursive(treeView.Nodes);
                        ActiveServer = CheckServer(e.Node);
                        Path = e.Node.Tag != null ? e.Node.Tag.ToString() : string.Empty;
                        //If there's a file browser observer, add corresponding files
                        AddFiles?.Invoke(this, e);
                        if (_priorNode != null && _priorNode != e.Node)
                        {
                            _previous.Push(_priorNode);
                        }
                        _priorNode = e.Node;
                        Clicked = e.Node;
                        _next.Clear();
                        //Observer pattern for navigation buttons
                        NodeClick?.Invoke(this, e);
                    }
                }
            }
        }

        /// <summary>
        /// Determines which server corresponds with a given node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private PanoramaServer CheckServer(TreeNode node)
        {
            if (node != null)
            {
                if (node.Parent == null)
                {
                    foreach (var pServer in _serverList)
                    {
                        if (pServer.URI.ToString().Equals(node.Text))
                        {
                            return pServer;
                        }
                    }
                }
                else
                {
                    var result = CheckServer(node.Parent);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        private static void ClearTreeRecursive(IEnumerable nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.BackColor = Color.White;
                node.ForeColor = Color.Black;
                ClearTreeRecursive(node.Nodes);
            }
        }

        public void UpClick()
        {
            if (Clicked?.Parent != null)
            {
                _lastSelected.BackColor = Color.White;
                _lastSelected.ForeColor = Color.Black;
                var parent = _lastSelected.Parent;
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
                _priorNode = parent;
                treeView.SelectedNode = parent;
                _lastSelected = parent;
                Clicked = parent;
                Path = Clicked.Tag != null ? Clicked.Tag.ToString() : string.Empty;
                treeView.Focus();
                AddFiles?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool UpEnabled()
        {
            return Clicked != null && Clicked.Parent != null;
        }

        public void BackClick()
        {
            var prior = _previous.Pop();
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
            _lastSelected = prior;
            treeView.SelectedNode = prior;
            treeView.Focus();
            _priorNode = prior;
            Clicked = prior;
            Path = prior.Tag != null ? prior.Tag.ToString() : string.Empty;
            AddFiles?.Invoke(this, EventArgs.Empty);
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
            _lastSelected.BackColor = Color.White;
            _lastSelected.ForeColor = Color.Black;
            treeView.SelectedNode = nextNode;
            _lastSelected = nextNode;
            Clicked = nextNode;
            treeView.Focus();
            Path = nextNode.Tag != null ? nextNode.Tag.ToString() : string.Empty;
            AddFiles?.Invoke(this, EventArgs.Empty);
        }

        public bool ForwardEnabled()
        {
            return _next != null && _next.Count != 0;
        }

        public string ClosingState()
        { 
            return State = _restorer.GetPersistentString();
        }

        /// <summary>
        /// If there was a previous selection made in FilePicker, reload the files in the
        /// selected folder
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="e"></param>
        private void AddSelectedFiles(IEnumerable nodes, EventArgs e)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.IsSelected)
                {
                    //Highlight the selected node
                    ActiveServer = CheckServer(node);
                    _priorNode = node;
                    node.BackColor = SystemColors.MenuHighlight;
                    node.ForeColor = Color.White;
                    treeView.Focus();
                    _lastSelected = node;
                    Clicked = node;
                    Path = (string)node.Tag;
                    treeView.Focus();
                    AddFiles?.Invoke(this, e);
                }
                else
                {
                    AddSelectedFiles(node.Nodes, e);
                }

            }
        }

        public void SelectNode(string nodeName)
        {
            Testing = true;
            var node = SearchTree(treeView.Nodes, nodeName);
            if (node != null)
            {
                ActiveServer = _server;
                treeView.SelectedNode = node;
                Path = node.Tag != null ? node.Tag.ToString() : string.Empty;
                AddFiles?.Invoke(this, EventArgs.Empty);
                //If there's a file browser observer, add corresponding files
                if (_priorNode != null && _priorNode != node)
                {
                    _previous.Push(_priorNode);
                }
                _priorNode = node;
                Clicked = node;
                _next.Clear();
                NodeClick?.Invoke(this, EventArgs.Empty);
            }
        }

        private TreeNode SearchTree(IEnumerable nodes, string nodeName)
        {
            foreach (TreeNode node in nodes)
            {
                Debug.WriteLine(node.Text);
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
    }
}
