using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    //instead of having a TreeView, have the user pass in a treeView that has things done to it that way RemoteFileDialog can still detect a click event?
    public partial class FolderBrowser : UserControl
    {
        private PanoramaServer _server;
        private bool _uploadPerms;
        private PanoramaFormUtil _formUtil;
        public string FolderPath;
        private RemoteFileDialog _dialog;
        public TreeNode clicked;


        //Needs to take server information
        public FolderBrowser(Uri serverUri, string user, string pass, bool uploadPerms)
        {
            InitializeComponent();
            treeView.ImageList = imageList1;
            _server = string.IsNullOrEmpty(user) ? new PanoramaServer(serverUri) : new PanoramaServer(serverUri, user, pass);
            _uploadPerms = uploadPerms;
        }

        /// <summary>
        /// This constructor takes a reference to RemoteFileDialog to call methods inside it for adding files 
        /// </summary>
        /// <param name="serverUri"></param>
        /// <param name="user"></param>
        /// <param name="pass"></param>
        /// <param name="dlg"></param>
        public FolderBrowser(Uri serverUri, string user, string pass, RemoteFileDialog dlg)
        {
            InitializeComponent();
            treeView.ImageList = imageList1;
            _server = new PanoramaServer(serverUri, user, pass);
            _uploadPerms = false;
            _dialog = dlg;
        }

        public void SwitchFolderType(bool type)
        {
            treeView.Nodes.Clear();
            _formUtil.InitializeTreeView(_server, treeView, _uploadPerms, true, type);
            treeView.TopNode.Expand();
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag != null)
            {
                FolderPath = e.Node.Tag.ToString();
            }
        }

        private void FolderBrowser_Load(object sender, EventArgs e)
        {
            _formUtil = new PanoramaFormUtil();
            _formUtil.InitializeTreeView(_server, treeView, _uploadPerms, true, false);
            treeView.TopNode.Expand();
        }

        private void treeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            clicked = e.Node;
            if (_dialog != null)
            {
                ClearTreeRecursive(treeView.Nodes);
                var hit = e.Node.TreeView.HitTest(e.Location);
                if (hit.Location != TreeViewHitTestLocations.PlusMinus)
                {
                    _dialog.AddFiles(e.Node);
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

   
    }
}
