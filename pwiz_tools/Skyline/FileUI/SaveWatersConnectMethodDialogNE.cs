using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using NHibernate.Engine;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Ardia;
using pwiz.Skyline.Model.Results.RemoteApi.WatersConnect;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public class UrlPath
    {
        public const string PATH_SEPARATOR = "/";
        public static string Combine(string path1, string path2)
        {
            if (path1 == null)
                return path2;
            if (path2 == null)
                return path1;
            return path1 + PATH_SEPARATOR + path2;
        }
        public static string[] Split(string path)
        {
            if (path == null)
                return new string[0];
            return path.Split(new[] { PATH_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool IsRooted(string path)
        {
            return path != null && path.StartsWith(PATH_SEPARATOR);
        }

        public static IEnumerable<string> GetFilePathParts(string path)
        {
            if (path == null)
                return null;
            var parts = Split(path);
            if (parts.Length == 0)
                return null;
            return parts.Take(parts.Length - 1);
        }

        public static string GetFilePath(string path)
        {
            return string.Join(PATH_SEPARATOR, GetFilePathParts(path));
        }
        public static bool CanBeFileName(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) &&
                   fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }


    }

    class SaveWatersConnectMethodDialogNE : BaseFileDialogNE
    {
        public SaveWatersConnectMethodDialogNE(IList<RemoteAccount> remoteAccounts,  IList<string> specificDataSourceFilter = null)
            : base(null, remoteAccounts, specificDataSourceFilter)
        {
            var instrumentType = ExportInstrumentType.WATERS_XEVO_TQ_WATERS_CONNECT;
            Text = string.Format(FileUIResources.ExportMethodDlg_OkDialog_Export__0__Method, instrumentType);
            listView.MultiSelect = false;
        }
        protected override void DoMainAction()
        {
            // take the current directory and combine it with the file name entered in the text box.
            // Make sure the entered string is a valid file name
            // if not list items are selected check if the text box has a path
            if (RemoteSession is WatersConnectSession watersSession) // if it is a remote open it remotely
            {
                if (listView.SelectedItems.Count > 0)
                {
                    var selectedItem = listView.SelectedItems[0];
                    if (DataSourceUtil.IsFolderType(selectedItem.SubItems[1].Text))
                        OpenFolderItem(selectedItem);
                    else
                    {
                        if (ConfirmReplace(selectedItem))
                            DialogResult = DialogResult.OK;
                    }
                }
                else
                {
                    // if nothing is selected check it there is a file name in the text box
                    if (string.IsNullOrEmpty(sourcePathTextBox.Text))
                        return;
                    var fileOrDirName = sourcePathTextBox.Text;
                    var item = listView.Items.Cast<ListViewItem>().FirstOrDefault(i =>
                        i.Text.Equals(fileOrDirName, StringComparison.CurrentCultureIgnoreCase));
                    // if the text in the box is one of the items in the list view
                    if (item != null)
                    {
                        if (DataSourceUtil.IsFolderType(item.SubItems[1].Text))
                            OpenFolderItem(item);
                        else
                        {
                            if (ConfirmReplace(item))
                                DialogResult = DialogResult.OK;
                        }
                    }
                    else if (CurrentDirectory is WatersConnectUrl currentDir)
                    {
                        FileNames = new[]
                        {
                            currentDir.ChangeType(WatersConnectUrl.ItemType.acquisition_method)
                                .ChangeAcquisitionMethodId(null)
                                .ChangePathParts(currentDir.GetPathParts().Concat(new[] { fileOrDirName }))
                                
                        };
                        DialogResult = DialogResult.OK;
                    }
                }
            }
        }

        private bool ConfirmReplace(ListViewItem item)
        {
            var dlgResult = MessageDlg.Show(this,
                string.Format("Are you sure you want to overwrite {0}?", item.Text), false,
                MessageBoxButtons.YesNo);
            if (dlgResult == DialogResult.No)
                return false;
            FileNames = new[] { ((SourceInfo)item.Tag).MsDataFileUri };
            return true;
        }

        protected override void CreateNewRemoteSession(RemoteAccount remoteAccount)
        {
            if (remoteAccount is WatersConnectAccount wcAccount)
            {
                RemoteSession = new WatersConnectSessionAcquisitionMethod(wcAccount);
                return;
            }

            throw new Exception("remoteAccount is NOT WatersConnectAccount");
        }
    }

    
}
