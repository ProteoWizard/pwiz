using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util.Extensions;

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
            newFolderButton.Visible = true;
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

        protected override void ListViewPostprocessing()
        {
            base.ListViewPostprocessing();
            // Re-evaluate the button after the list (re)populates: when the dialog first opens, the
            // folder data may not have loaded yet on the initial OnCurrentDirectoryChange, so the
            // writability check could not run until the async fetch completed and repopulated the list.
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

            // A new folder can only be created inside an existing writable folder. canWrite is true only
            // when the current folder resolved (its data has loaded) and is writable, which is exactly
            // when CreateNewFolder can succeed.
            newFolderButton.Enabled = canWrite;

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

        /// <summary>
        /// Creates a new waters_connect folder under the current directory via the Folders API and,
        /// on success, refreshes the list so it appears. The network call runs inside a wait dialog;
        /// permission and naming errors are reported to the user.
        /// </summary>
        protected override void CreateNewFolder(string folderName)
        {
            if (!TryGetWritableParent(out var session, out var parentFolder, out var description))
                return;

            var statusCode = default(HttpStatusCode);
            string responseBody = null;
            using (var waitDlg = new LongWaitDlg())
            {
                waitDlg.Text = FileUIResources.WatersConnectSaveMethodFileDialog_CreateNewFolder_Creating_folder;
                waitDlg.PerformWork(this, 1000,
                    () => statusCode = session.CreateFolder(parentFolder.Id, folderName, description, out responseBody));
            }

            HandleCreateFolderResult(folderName, statusCode, responseBody);
        }

        /// <summary>
        /// Resolves the current directory to a writable folder and builds the folder description.
        /// Shows a message and returns false if the current location is not a writable folder.
        /// </summary>
        private bool TryGetWritableParent(out WatersConnectSession session, out WatersConnectFolderObject parentFolder, out string description)
        {
            session = null;
            parentFolder = null;
            description = null;

            var currentDir = CurrentDirectory as WatersConnectUrl;
            if (!(RemoteSession is WatersConnectSession wcSession) || currentDir == null)
                return false;

            if (!wcSession.TryGetFolderByUrl(currentDir, out parentFolder) || !parentFolder.CanWrite)
            {
                MessageDlg.Show(this,
                    FileUIResources.WatersConnectSaveMethodFileDialog_CreateNewFolder_You_do_not_have_permission_to_create_folders_in_this_location_);
                return false;
            }

            session = wcSession;
            description = string.Format(
                FileUIResources.WatersConnectSaveMethodFileDialog_CreateNewFolder_Created_by__0__using_Skyline,
                wcSession.WatersConnectAccount.Username);
            return true;
        }

        private void HandleCreateFolderResult(string folderName, HttpStatusCode statusCode, string responseBody)
        {
            if (statusCode < HttpStatusCode.BadRequest)
            {
                RefreshCurrentDirectory();
                return;
            }

            string message;
            switch (statusCode)
            {
                case HttpStatusCode.Forbidden:
                    message = string.Format(
                        FileUIResources.WatersConnectSaveMethodFileDialog_CreateNewFolder_You_do_not_have_permission_to_create_the_folder__0__, folderName);
                    break;
                case HttpStatusCode.Conflict:
                    message = string.Format(
                        FileUIResources.WatersConnectSaveMethodFileDialog_CreateNewFolder_A_folder_named__0__already_exists_, folderName);
                    break;
                default:
                    message = string.Format(
                        FileUIResources.WatersConnectSaveMethodFileDialog_CreateNewFolder_Could_not_create_the_folder__0__, folderName);
                    break;
            }

            MessageDlg.Show(this, string.IsNullOrEmpty(responseBody) ? message : TextUtil.LineSeparate(message, responseBody));
        }

        #region Test support

        public bool NewFolderButtonVisible => newFolderButton.Visible;
        public bool NewFolderButtonEnabled => newFolderButton.Enabled;

        /// <summary>
        /// Runs the folder creation synchronously (without the wait dialog) so functional tests can
        /// drive it directly. Exercises the same parent resolution, API call, and result handling as
        /// <see cref="CreateNewFolder"/>.
        /// </summary>
        public void CreateNewFolderForTest(string folderName)
        {
            if (!TryGetWritableParent(out var session, out var parentFolder, out var description))
                return;
            var statusCode = session.CreateFolder(parentFolder.Id, folderName, description, out var responseBody);
            HandleCreateFolderResult(folderName, statusCode, responseBody);
        }

        #endregion
    }
}
