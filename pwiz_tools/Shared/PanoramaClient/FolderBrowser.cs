using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    public partial class FolderBrowser : UserControl
    {
        private PanoramaServer _server;
        private bool _uploadPerms;
        private PanoramaFormUtil _formUtil;
        public string FolderPath;
        public TreeNode clicked;
        private Stack<TreeNode> previous = new Stack<TreeNode>();
        private TreeNode priorNode;
        private Stack<TreeNode> next = new Stack<TreeNode>();
        public event EventHandler NodeClick;
        public event EventHandler AddFiles;
        private TreeNode lastSelected;
        public bool showSky;
        public string Path;
        public string state;
        public TreeViewStateRestorer Restorer;

        //Needs to take server information
        public FolderBrowser(Uri serverUri, string user, string pass, bool uploadPerms, bool showSkyFolders, string state)
        {
            InitializeComponent();
            treeView.ImageList = imageList1;
            _server = string.IsNullOrEmpty(user) ? new PanoramaServer(serverUri) : new PanoramaServer(serverUri, user, pass);
            _uploadPerms = uploadPerms;
            showSky = showSkyFolders;
            this.state = state;
            Restorer = new TreeViewStateRestorer(treeView);
        }


        public void SwitchFolderType(bool type)
        {
            treeView.Nodes.Clear();
            showSky = type;
            _formUtil.InitializeTreeView(_server, treeView, _uploadPerms, true, type);
            treeView.TopNode.Expand();
            next.Clear();
            previous.Clear();
            clicked = null;
            lastSelected = null;
            priorNode = null;
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            lastSelected = e.Node;
            if (e.Node.Tag != null)
            {
                FolderPath = e.Node.Tag.ToString();
            }
        }

        private void FolderBrowser_Load(object sender, EventArgs e)
        {
            _formUtil = new PanoramaFormUtil();
            _formUtil.InitializeTreeView(_server, treeView, _uploadPerms, true, showSky);
            if (!string.IsNullOrEmpty(state))
            {
                Restorer.RestoreExpansionAndSelection(state);
                Restorer.UpdateTopNode();
                AddSelectedFiles(treeView.Nodes, e);
            }
        }

        public void treeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (lastSelected == null || (lastSelected != null && !lastSelected.Equals(e.Node)))
            {
                if (e.Node.Bounds.Contains(e.Location))
                {
                    ClearTreeRecursive(treeView.Nodes);
                    var hit = e.Node.TreeView.HitTest(e.Location);
                    if (hit.Location != TreeViewHitTestLocations.PlusMinus)
                    {
                        Path = e.Node.Tag != null ? e.Node.Tag.ToString() : string.Empty;
                        //If there's a file browser observer, add corresponding files
                        if (AddFiles != null)
                        {
                            AddFiles(this, e);
                        }
                        if (priorNode != null && priorNode != e.Node)
                        {
                            previous.Push(priorNode);
                        }
                        priorNode = e.Node;
                        clicked = e.Node;
                        next.Clear();
                        //Observer pattern for navigation buttons
                        if (NodeClick != null)
                        {
                            NodeClick(this, e);
                        }
                    }
                }
            }
        }

        private void ClearTreeRecursive(IEnumerable nodes)
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
            if (clicked != null && clicked.Parent != null)
            {
                lastSelected.BackColor = Color.White;
                lastSelected.ForeColor = Color.Black;
                var parent = lastSelected.Parent;
                next.Clear();
                if (previous.Count != 0)
                {
                    if (!previous.Peek().Equals(priorNode))
                    {
                        previous.Push(priorNode);
                    }
                }
                else
                {
                    previous.Push(priorNode);
                }
                priorNode = parent;
                treeView.SelectedNode = parent;
                lastSelected = parent;
                clicked = parent;
                Path = clicked.Tag != null ? clicked.Tag.ToString() : string.Empty;
                treeView.Focus();
                if (AddFiles != null)
                {
                    AddFiles(this, EventArgs.Empty);
                }
            }
        }

        public bool UpEnabled()
        {
            return clicked != null && clicked.Parent != null;
        }

        public void BackClick()
        {
            var prior = previous.Pop();
            if (next.Count != 0)
            {
                if (!next.Peek().Equals(lastSelected))
                {
                    next.Push(lastSelected);
                }
            }
            else
            {
                next.Push(lastSelected);
            }
            lastSelected.BackColor = Color.White;
            lastSelected.ForeColor = Color.Black;
            lastSelected = prior;
            treeView.SelectedNode = prior;
            treeView.Focus();
            priorNode = prior;
            clicked = prior;
            Path = prior.Tag != null ? prior.Tag.ToString() : string.Empty;
            if (AddFiles != null)
            {
                AddFiles(this, EventArgs.Empty);
            }
        }

        public bool BackEnabled()
        {
            return previous != null && previous.Count != 0;
        }

        public void ForwardClick()
        {
            if (previous.Count != 0)
            {
                if (!previous.Peek().Equals(lastSelected))
                {
                    previous.Push(lastSelected);
                }
            }
            else
            {
                previous.Push(lastSelected);
            }

            var nextNode = next.Pop();
            lastSelected.BackColor = Color.White;
            lastSelected.ForeColor = Color.Black;
            treeView.SelectedNode = nextNode;
            lastSelected = nextNode;
            clicked = nextNode;
            treeView.Focus();
            Path = nextNode.Tag != null ? nextNode.Tag.ToString() : string.Empty;
            if (AddFiles != null)
            {
                AddFiles(this, EventArgs.Empty);
            }
        }

        public bool ForwardEnabled()
        {
            return next != null && next.Count != 0;
        }

        public string ClosingState()
        { 
            return state = Restorer.GetPersistentString();
        }

        private void AddSelectedFiles(IEnumerable nodes, EventArgs e)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.IsSelected)
                {
                    //Highlight the selected node
                    priorNode = node;
                    node.BackColor = SystemColors.MenuHighlight;
                    node.ForeColor = Color.White;
                    lastSelected = node;
                    clicked = node;
                    Path = (string)node.Tag;
                    if (AddFiles != null)
                    {
                        AddFiles(this, e);
                    }
                }
                else
                {
                    AddSelectedFiles(node.Nodes, e);
                }

            }
        }
    }
}
