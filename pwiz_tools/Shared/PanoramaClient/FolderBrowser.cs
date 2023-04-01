using System;
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

        //Needs to take server information
        //public void InitializeTreeView(Uri serverUri, string user, string pass, TreeView treeViewFolders, bool requireUploadPerms, bool showFiles)
        public FolderBrowser(Uri serverUri, string user, string pass, bool uploadPerms)
        {
            InitializeComponent();
            treeView.ImageList = imageList1;
            _server = new PanoramaServer(serverUri, user, pass);
            _uploadPerms = uploadPerms;
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
    }
}
