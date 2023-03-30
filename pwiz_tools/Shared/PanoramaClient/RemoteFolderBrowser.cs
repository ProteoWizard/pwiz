using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    public partial class RemoteFolderBrowser : UserControl
    {
        private Uri _serverUri;
        private string _user;
        private string _pass;
        private bool _uploadPerms;
        private PanoramaClient pc;
        public string FolderPath;

        //Needs to take server information
        //public void InitializeTreeView(Uri serverUri, string user, string pass, TreeView treeViewFolders, bool requireUploadPerms, bool showFiles)
        public RemoteFolderBrowser(Uri serverUri, string user, string pass, bool uploadPerms)
        {
            InitializeComponent();
            folderView.ImageList = imageList1;
            _serverUri = serverUri;
            _user = user;
            _pass = pass;
            _uploadPerms = uploadPerms;
        }


        private void RemoteFolderBrowser_Load(object sender, EventArgs e)
        {
            pc = new PanoramaClient();
            pc.InitializeTreeView(_serverUri, _user, _pass, folderView, _uploadPerms, true, false);
            folderView.TopNode.Expand();
        }

        public void SwitchFolderType(bool type)
        {
            folderView.Nodes.Clear();
            pc.InitializeTreeView(_serverUri, _user, _pass, folderView, _uploadPerms, true, type);
            folderView.TopNode.Expand();
        }

        private void folderView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag != null)
            {
                FolderPath = e.Node.Tag.ToString();
            }
        }
    }
}
