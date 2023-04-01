using System;
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    //instead of having a TreeView, have the user pass in a treeView that has things done to it that way RemoteFileDialog can still detect a click event?
    public partial class FolderBrowser : UserControl
    {
        private Uri _serverUri;
        private string _user;
        private string _pass;
        private bool _uploadPerms;
        private PanoramaClient pc;
        public string FolderPath;

        //Needs to take server information
        //public void InitializeTreeView(Uri serverUri, string user, string pass, TreeView treeViewFolders, bool requireUploadPerms, bool showFiles)
        public FolderBrowser(Uri serverUri, string user, string pass, bool uploadPerms)
        {
            InitializeComponent();
            treeView.ImageList = imageList1;
            _serverUri = serverUri;
            _user = user;
            _pass = pass;
            _uploadPerms = uploadPerms;
        }

        public void SwitchFolderType(bool type)
        {
            treeView.Nodes.Clear();
            pc.InitializeTreeView(_serverUri, _user, _pass, treeView, _uploadPerms, true, type);
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
            pc = new PanoramaClient();
            pc.InitializeTreeView(_serverUri, _user, _pass, treeView, _uploadPerms, true, false);
            treeView.TopNode.Expand();
        }
    }
}
