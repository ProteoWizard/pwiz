using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.CommonMsData;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.FileUI
{
    public class WatersConnectMethodFileDialog : BaseFileDialogNE
    {
        protected string InstrumentType { get; set; }
        public WatersConnectMethodFileDialog(IList<RemoteAccount> remoteAccounts, IList<string> specificDataSourceFilter = null)
            : base(null, remoteAccounts, specificDataSourceFilter, true)
        {
            InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ_WATERS_CONNECT;
            listView.MultiSelect = false;
            var acctList = remoteAccounts.OfType<WatersConnectAccount>().ToList();
            if (acctList.Count == 1)  // if there is only one account, set the initial directory to its root path
                InitialDirectory = (acctList.First().GetRootUrl() as WatersConnectUrl)?.ChangeType(WatersConnectUrl.ItemType.folder_with_methods);
            else
                InitialDirectory = RemoteUrl.EMPTY;
            labelSourcePath.Text = FileUIResources.WatersConnectSaveMethodFileDialog_SourcePathLabelText;
        }

        protected override void ListViewPostprocessing()
        {
            if (string.IsNullOrEmpty((CurrentDirectory as RemoteUrl)?.ServerUrl))
            {
                listView.ShowItemToolTips = true;
                foreach (ListViewItem item in listView.Items)
                {
                    if (item.Tag is SourceInfo sourceInfo && sourceInfo.MsDataFileUri is WatersConnectUrl wcu)
                    {
                        if (wcu.FindMatchingAccount() is WatersConnectAccount wca && !wca.SupportsMethodDevelopment)
                        {
                            item.ToolTipText = FileUIResources.WatersConnectMethodFileDialog_ListViewPostprocessing_This_account_does_not_support_method_development;
                            item.BackColor = System.Drawing.Color.LightCoral;
                        }
                    }
                }
            }
        }

        protected override void CreateNewRemoteSession(RemoteAccount remoteAccount)
        {
            if (remoteAccount is WatersConnectAccount wcAccount)
            {
                RemoteSession = new WatersConnectSessionAcquisitionMethod(wcAccount);
                return;
            }

            throw new Exception(FileUIResources.OpenFileDialogNEWatersConnectMethod_CreateNewRemoteSession_remoteAccount_is_NOT_WatersConnectAccount);
        }

        protected override RemoteUrl GetRootUrl(RemoteAccount account)
        {   // Making sure the root URL has the correct type for method retrieval
            return (base.GetRootUrl(account) as WatersConnectUrl)?.ChangeType(WatersConnectUrl.ItemType.folder_with_methods);
        }

        protected override ImageIndex GetRemoteItemImageIndex(RemoteItem item)
        {
            var imageIndex = base.GetRemoteItemImageIndex(item);
            if (item.Access != AccessType.unknown)
            {
                imageIndex = item.Access switch
                {
                    AccessType.read => ImageIndex.ReadOnlyFolder,
                    AccessType.read_write => ImageIndex.ReadWriteFolder,
                    AccessType.no_access => ImageIndex.NoAccessFolder,
                    _ => imageIndex
                };
            }
            return imageIndex;
        }

        protected virtual bool ItemSelected(ListViewItem item)
        {
            return false;
        }

        protected virtual bool ItemSelected(string methodName)
        {
            return false;
        }

        protected override void SelectItem()
        {
            if (listView.SelectedItems.Count > 0)
            {
                var selectedItem = listView.SelectedItems[0];
                if (DataSourceUtil.IsFolderType(selectedItem.SubItems[1].Text))
                    OpenFolderItem(selectedItem);
                else
                {
                    if (ItemSelected(selectedItem))
                    {
                        SetFolderId();
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                }
            }
        }

        protected override void SetRemoteAccounts(IEnumerable<RemoteAccount> accounts)
        {
            base.SetRemoteAccounts(accounts.OfType<WatersConnectAccount>());
        }

        protected override void DoMainAction()
        {
            Open();
        }

        protected void Open()
        {
            // take the current directory and combine it with the file name entered in the text box.
            // Make sure the entered string is a valid file name
            // if no list items are selected check if the text box has a path
            if (RemoteSession is WatersConnectSession watersSession) // if it is a remote open it remotely
            {
                SetFolderId();
                if (listView.SelectedItems.Count > 0)
                {
                    var selectedItem = listView.SelectedItems[0];
                    if (DataSourceUtil.IsFolderType(selectedItem.SubItems[1].Text))
                        OpenFolderItem(selectedItem);
                    else
                    {
                        if (ItemSelected(selectedItem))
                            DialogResult = DialogResult.OK;
                    }
                }
                else
                {
                    // if nothing is selected check it there is a file name in the text box
                    if (string.IsNullOrEmpty(sourcePathTextBox.Text))
                        return;
                    // TODO: [RC] support paths, not just file names
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
                            if (ItemSelected(item))
                                DialogResult = DialogResult.OK;
                        }
                    }
                    else
                    {
                        if (ItemSelected(fileOrDirName))
                            DialogResult = DialogResult.OK;
                    }
                }
            }
        }

        protected enum FileStatus
        {
            folder,
            file,
            does_not_exist
        }
        protected FileStatus FileNameExists(string fileName)
        {
            // check if the file name exists in the list view
            var item = listView.Items.Cast<ListViewItem>().FirstOrDefault(i => i.Text.Equals(fileName, StringComparison.CurrentCultureIgnoreCase));
            if (item == null)
                return FileStatus.does_not_exist;
            if (DataSourceUtil.IsFolderType(item.SubItems[1].Text))
                return FileStatus.folder;
            return FileStatus.file;
        }

        protected override void OnCurrentDirectoryChange()
        {
            base.OnCurrentDirectoryChange();
            SetFolderId();
        }

        protected void SetFolderId()
        {
            if (RemoteSession is WatersConnectSession session && _currentDirectory is WatersConnectUrl currentDir)
            {
                if (!string.IsNullOrEmpty(currentDir.EncodedPath?.Trim()) && session.TryGetFolderByUrl(currentDir, out var folder))
                    _currentDirectory = currentDir.ChangeFolderOrSampleSetId(folder.Id);
            }
        }

        #region Test support

        public ImmutableList<ListViewItem> ListViewItems
        {
            get
            {
                return ImmutableList.ValueOf(listView.Items.OfType<ListViewItem>());
            }
        }

        public ListViewItem SelectedItem
        {
            get => listView.SelectedItems.Count > 0 ? listView.SelectedItems[0] : null;
        }

        public TextBox SourcePathTextBox => sourcePathTextBox;

        #endregion
    }
}