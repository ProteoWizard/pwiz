using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Alerts;

namespace pwiz.Skyline.FileUI
{
    public sealed class WatersConnectSaveMethodFileDialog : WatersConnectMethodFileDialog
    {
        public string MethodName { get; private set; }
        /// <summary>
        /// This property is used to trigger file name check for multiple method uploads
        /// </summary>
        public List<string> MethodNameSuffixes { get; set; }

        public WatersConnectSaveMethodFileDialog(IList<RemoteAccount> remoteAccounts,  IList<string> specificDataSourceFilter = null)
            : base(remoteAccounts, specificDataSourceFilter)
        {
            Text = string.Format(FileUIResources.ExportMethodDlg_OkDialog_Export__0__Method, InstrumentType);
            actionButton.Text = FileUIResources.WatersConnectSaveMethodFileDialog_SaveButtonText;
        }
        protected override void DoMainAction()
        {
            Open();
        }

        protected override void OnCurrentDirectoryChange()
        {
            base.OnCurrentDirectoryChange();
            SetButtonQue();
        }

        protected override void OnFileNameTyped()
        {
            base.OnFileNameTyped();
            SetButtonQue();
        }

        private void SetButtonQue()
        {
            var currentDir = CurrentDirectory as WatersConnectUrl;
            if (currentDir == null)
                return;
            var haveFolderId = currentDir.FolderOrSampleSetId != null;
            var fileStatus = FileNameExists(sourcePathTextBox.Text);
            bool canWrite = false;
            if (RemoteSession is WatersConnectSession session && session.TryGetFolderByUrl(currentDir, out var folder))
            {
                canWrite = folder.CanWrite;
            }

            switch (fileStatus)
            {
                case FileStatus.file:   // File already exists, cannot export
                    actionButton.Font = new Font(actionButton.Font, FontStyle.Bold); // Save not allowed
                    actionButton.ForeColor = Color.Red;
                    actionButton.Text = FileUIResources.WatersConnectSaveMethodFileDialog_SaveButtonText;
                    return;
                case FileStatus.folder: // Can always open a folder
                    actionButton.Font = new Font(actionButton.Font, FontStyle.Regular);
                    actionButton.ForeColor = SystemColors.ControlText;
                    actionButton.Text = FileUIResources.WatersConnectSaveMethodFileDialog_OpenButtonText;
                    return;
                case FileStatus.does_not_exist:
                    actionButton.Text = FileUIResources.WatersConnectSaveMethodFileDialog_SaveButtonText;


                    if (!haveFolderId || !canWrite || string.IsNullOrEmpty(sourcePathTextBox.Text) 
                        || IsMultipleMethodConflict(sourcePathTextBox.Text, out _))     // Cannot save if root or no write permission.
                    {
                        actionButton.Font = new Font(actionButton.Font, FontStyle.Bold); // Save not allowed
                        actionButton.ForeColor = Color.Red;
                    }
                    else
                    {
                        actionButton.Font = new Font(actionButton.Font, FontStyle.Regular);
                        actionButton.ForeColor = SystemColors.ControlText;
                    }
                    return;
            }
        }

        private bool IsMultipleMethodConflict(string fileName, out string conflictName)
        {
            conflictName = null;
            if (MethodNameSuffixes == null || !MethodNameSuffixes.Any() || listView.Items.Count == 0)
                return false;
             var conflictItem = listView.Items.OfType<ListViewItem>()
                .FirstOrDefault(lvi => MethodNameSuffixes.Any(sufx => lvi.Text.Equals(fileName + sufx)));
             if (conflictItem == null)
                 return false;
             conflictName = conflictItem.Text;
             return true;
        }

        protected override bool ItemSelected(ListViewItem item)
        {
            MessageDlg.Show(this,
                string.Format(FileUIResources.WatersConnectSaveMethodFileDialog_ItemSelected_The_method_file__0__already_exists, item.Text));
            return false;
        }

        protected override bool ItemSelected(string methodName)
        {
            if (IsMultipleMethodConflict(methodName, out var conflictName))
            {
                MessageDlg.Show(this,
                    string.Format(
                        FileUIResources.WatersConnectSaveMethodFileDialog_ItemSelected_The_name__0__conflicts_with_the_existing_method__1__since_this_is_a_multiple_file_upload_, methodName, conflictName));
                return false;
            }
            MethodName = methodName.Trim();
            return true;
        }
    }

    
}
