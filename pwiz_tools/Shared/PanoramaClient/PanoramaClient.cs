using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient.Properties;
using pwiz.Skyline;
using pwiz.Skyline.Util;

namespace pwiz.PanoramaClient
{
    //PanoramaUtil should become PanoramaClient
    
    public class PanoramaClient
    {
        public IPanoramaPublishClient PanoramaPublishClient { get; set; }
        public PanoramaClient()
        {

        }

        public string DownloadAndSave(Uri serverUri, string user, string pass, string fileName, string downloadName)
        {
            var dlg = new FolderBrowserDialog();
            string selected = string.Empty;
            dlg.Description = Resources.RemoteFileDialog_open_Click_Select_the_folder_the_file_will_be_downloaded_to;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var path = dlg.SelectedPath;
                selected = dlg.SelectedPath;
                /*using (var longWaitDlg = new LongWaitDlg
                       {
                           Text = Resources.RemoteFileDialog_open_Click_Downloading_selected_file,
                       })
                {
                    longWaitDlg.PerformWork(SkylineWindow.ActiveForm, 1000, () =>
                        DownloadFile(path, serverUri, user, pass, fileName, downloadName));
                    if (longWaitDlg.IsCanceled)
                        return string.Empty;
                }*/

            }
            return Path.Combine(selected, fileName);
        }

        private void DownloadFile(string path, Uri server, string user, string pass, string fileName, string downloadName)
        {
            var serverUri = server;
            
            
            var downloadUri = server + downloadName;
            using (var wc = new WebClientWithCredentials(serverUri, user, pass))
            {
                wc.DownloadFile(

                    // Param1 = Link of file
                    new System.Uri(downloadUri),
                    // Param2 = Path to save
                    Path.Combine(path, fileName)
                );
            }
        }

        public void InitializeTreeView(pwiz.Skyline.Util.Server server, TreeView treeViewFolders, bool requireUploadPerms)
        {
            
            PanoramaPublishClient = new WebPanoramaPublishClient();
            var folder = PanoramaPublishClient.GetInfoForFolders(server, null);
            var treeNode = new TreeNode(server.GetKey());
            treeViewFolders.Invoke(new Action(() => treeViewFolders.Nodes.Add(treeNode)));
            treeViewFolders.Invoke(new Action(() => PublishDocumentDlg.AddChildContainers(server, treeNode, folder, requireUploadPerms)));

        }
    }
}
