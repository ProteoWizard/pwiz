/*
 * Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
 *
 * Copyright 2009 Vanderbilt University - Nashville, TN 37232
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.CommonMsData.RemoteApi;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;


namespace pwiz.Skyline.FileUI
{
    public partial class BaseFileDialogNE : CommonFormEx
    {
        private readonly ListViewColumnSorter _listViewColumnSorter = new ListViewColumnSorter();
        private readonly Stack<MsDataFileUri> _previousDirectories = new Stack<MsDataFileUri>();
        private readonly int _myComputerIndex;
        private readonly int _remoteIndex;
        private RemoteSession _remoteSession;
        private readonly IList<RemoteAccount> _remoteAccounts;
        private bool _waitingForData;
        private readonly IList<string> _specificDataSourceFilter; // Specific data sources to look for
        protected bool IsRemote { get; private set; }

        /// <summary>
        /// File picker which is aware of mass spec "files" that are really directories
        /// </summary>
        /// <param name="sourceTypes"></param>
        /// <param name="remoteAccounts">For UNIFI</param>
        /// <param name="specificDataSourceFilter">Optional list of specific files the user needs to located, ignoring the rest</param>
        /// <param name="isRemote">Indicates that this dialog is for remote accounts only and local drives and folders should be hidden</param>
        public BaseFileDialogNE(string[] sourceTypes, IList<RemoteAccount> remoteAccounts, IList<string> specificDataSourceFilter = null, bool isRemote = false)
        {
            InitializeComponent();
            _remoteAccounts = new List<RemoteAccount>(remoteAccounts);
            IsRemote = isRemote;

            listView.ListViewItemSorter = _listViewColumnSorter;

            DialogResult = DialogResult.Cancel;

            if (sourceTypes == null)
            {
                sourceTypeComboBox.Visible = false;
                label1.Visible = false;
            }
            else
            {
                sourceTypeComboBox.Items.AddRange(sourceTypes.Cast<object>().ToArray());
                sourceTypeComboBox.SelectedIndex = 0;
            }

            // Create a new image list for the list view that is the default size (16x16)
            ImageList imageList = new ImageList{ColorDepth = ColorDepth.Depth32Bit};
            imageList.Images.AddRange(lookInImageList.Images.Cast<Image>().ToArray());
            listView.SmallImageList = imageList;
            listView.LargeImageList = imageList;

            TreeView tv = new TreeView { Indent = 8 };
            _remoteIndex = lookInComboBox.Items.Count;
            TreeNode remoteNode = tv.Nodes.Add(@"Remote",
                FileUIResources.OpenDataSourceDialog_OpenDataSourceDialog_Remote_Accounts, (int) ImageIndex.MyNetworkPlaces,
                (int) ImageIndex.MyNetworkPlaces);
            remoteNode.Tag = RemoteUrl.EMPTY;
            lookInComboBox.Items.Add(remoteNode);
            lookInComboBox.SelectedIndex = 0;
            remoteAccountsButton.Visible = true;
            recentDocumentsButton.Visible = false;

            if (!isRemote)
            {
                TreeNode desktopNode = tv.Nodes.Add(@"Desktop",
                    FileUIResources.OpenDataSourceDialog_OpenDataSourceDialog_Desktop, (int)ImageIndex.Desktop, (int)ImageIndex.Desktop);
                desktopNode.Tag = new MsDataFilePath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                lookInComboBox.Items.Add(desktopNode);
                TreeNode lookInNode = desktopNode.Nodes.Add(@"My Documents",
                    FileUIResources.OpenDataSourceDialog_OpenDataSourceDialog_My_Documents, (int)ImageIndex.MyDocuments, (int)ImageIndex.MyDocuments);
                lookInNode.Tag = new MsDataFilePath(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
                lookInComboBox.Items.Add(lookInNode);
                _myComputerIndex = lookInComboBox.Items.Count;
                TreeNode myComputerNode = desktopNode.Nodes.Add(@"My Computer",
                    FileUIResources.OpenDataSourceDialog_OpenDataSourceDialog_My_Computer, (int)ImageIndex.MyComputer, (int)ImageIndex.MyComputer);
                myComputerNode.Tag = new MsDataFilePath(Environment.GetFolderPath(Environment.SpecialFolder.MyComputer));
                lookInComboBox.Items.Add(myComputerNode);
                lookInComboBox.SelectedIndex = 1;
            }
            else
            {
                desktopButton.Enabled = myComputerButton.Enabled = myDocumentsButton.Enabled = recentDocumentsButton.Enabled = false;
            }

            lookInComboBox.IntegralHeight = false;
            lookInComboBox.DropDownHeight = lookInComboBox.Items.Count * lookInComboBox.ItemHeight + 2;

            _specificDataSourceFilter = specificDataSourceFilter;
        }

        // private void ExportImages(ImageList imageList, string suffix)
        // {
        //     try
        //     {
        //         string saveFolder = @"C:\proj\Scratch\pwiz_tools\Skyline\Resources\ImageList";
        //         Directory.CreateDirectory(saveFolder);
        //
        //         for (int i = 0; i < imageList.Images.Count; i++)
        //         {
        //             Image image = imageList.Images[i];
        //             string filePath = Path.Combine(saveFolder, $"{(ImageIndex)i}{suffix}.png");
        //             image.Save(filePath, ImageFormat.Png);
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         Console.WriteLine(e);
        //     }
        // }

        public void RestoreState(string documentPath, OpenDataSourceState state)
        {
            if (state != null)
                Size = state.WindowSize;

            string initialDir = state?.InitialDirectory;
            // If the saved initial directory is not for the same document, then start
            // in the document folder. Always starting in the document folder is painful
            // to watch, if the user is adding a single file at a time from a different
            // directory.
            if (string.IsNullOrEmpty(initialDir) || !Equals(state.DocumentPath, documentPath))
            {
                string docDir = Path.GetDirectoryName(documentPath);
                if (!string.IsNullOrEmpty(docDir))
                    initialDir = docDir;
            }
            // The dialog expects null to mean no directory was supplied, so don't assign
            // an empty string.
            if (string.IsNullOrEmpty(initialDir))
                initialDir = null;
            InitialDirectory = MsDataFileUri.Parse(initialDir);
            if (state != null)
            {
                if (SourceTypeName != null)
                    SourceTypeName = state.SourceTypeName;
                ListView = state.ListView;
                SetListViewSort(state.ListSortColumnIndex, state.ListSortOrder);
            }
        }

        public OpenDataSourceState GetState(string documentPath)
        {
            return new OpenDataSourceState
            {
                DocumentPath = documentPath,
                InitialDirectory = CurrentDirectory.ToString(),
                SourceTypeName = SourceTypeName,
                ListView = ListView,
                ListSortColumnIndex = ListSortColumnIndex,
                ListSortOrder = ListSortOrder,
                WindowSize = Size
            };
        }

        public new DialogResult ShowDialog(IWin32Window owner)
        {
            CurrentDirectory = InitialDirectory ?? new MsDataFilePath(Environment.CurrentDirectory);
            return base.ShowDialog(owner);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            RemoteSession = null;
        }

        protected virtual void OnCurrentDirectoryChange()
        {
        }

        protected MsDataFileUri _currentDirectory;
        public MsDataFileUri CurrentDirectory
        {
            get { return _currentDirectory; }
            set
            {
                if (Equals(value, RemoteUrl.EMPTY))
                {
                    EnsureRemoteAccount();
                    if (!_remoteAccounts.Any())
                    {
                        return;
                    }
                    if (_remoteAccounts.Count == 1)
                    {
                        // If there is exactly one account, then skip the level that
                        // lists the accounts to choose from.
                        value = GetRootUrl(_remoteAccounts.First());
                    }
                }
                if (value != null)
                {
                    _currentDirectory = value;
                    OnCurrentDirectoryChange();
                    populateListViewFromDirectory(_currentDirectory);
                    populateComboBoxFromDirectory(_currentDirectory);
                }
            }
        }

        protected virtual RemoteUrl GetRootUrl(RemoteAccount account)
        {
            return account.GetRootUrl();
        }
        public RemoteSession RemoteSession
        {
            get { return _remoteSession; }
            set
            {
                if (ReferenceEquals(RemoteSession, value))
                {
                    return;
                }
                if (null != RemoteSession)
                {
                    RemoteSession.Dispose();
                }
                _remoteSession = value;
                if (null != RemoteSession)
                {
                    AttachContentsAvailable(RemoteSession);
                }
            }
        }

        private void AttachContentsAvailable(RemoteSession remoteSession)
        {
            remoteSession.ContentsAvailable += () =>
            {
                if (!ReferenceEquals(remoteSession, RemoteSession))
                {
                    return;
                }
                RemoteContentsAvailable();
            };
        }

        public MsDataFileUri InitialDirectory { get; set; }

        public MsDataFileUri FileName
        {
            get { return FileNames[0]; }
        }

        public MsDataFileUri[] FileNames { get; protected set; }

        public void SelectAllFileType(string extension, Func<string, bool> accept = null)
        {
            foreach(ListViewItem item in listView.Items)
            {
                if (PathEx.HasExtension(item.Text, extension) &&
                    (accept == null || accept(item.Text)))
                {
                    listView.SelectedIndices.Add(item.Index);
                }
            }
        }

        public void SelectFile(string fileName)
        {
            foreach (ListViewItem item in listView.Items)
            {
                if (Equals(item.Text, fileName))
                    listView.SelectedIndices.Add(item.Index);
            }
            // ReSharper disable LocalizableElement
            if (0 == listView.SelectedIndices.Count && fileName.Contains("\\"))
                // ReSharper restore LocalizableElement
            {
                // mimic the action of user pasting an entire path into the textbox
                sourcePathTextBox.Text = fileName;
            }
        }

        public IEnumerable<string> SelectedFiles
        {
            get
            {
                foreach (int index in listView.SelectedIndices)
                    yield return listView.Items[index].Text;
            }
        }

        public void EnsureListViewItemVisible(int item)
        {
            listView.EnsureVisible(item);
        }

        public View ListView
        {
            get { return listView.View; }
            set { SetListView(value); }
        }

        public int ListSortColumnIndex
        {
            get { return _listViewColumnSorter.SortColumn; }
        }

        public SortOrder ListSortOrder
        {
            get { return _listViewColumnSorter.Order; }
        }


        private string _sourceTypeName;
        public string SourceTypeName
        {
            get { return _sourceTypeName; }
            set
            {
                _sourceTypeName = value;
                sourceTypeComboBox.SelectedItem = _sourceTypeName;
            }
        }

        private SourceInfo getSourceInfo( DirectoryInfo dirInfo )
        {
            string type = DataSourceUtil.GetSourceType(dirInfo);
            SourceInfo sourceInfo = new SourceInfo(new MsDataFilePath(dirInfo.FullName))
            {
                type = type,
                imageIndex = (DataSourceUtil.IsFolderType(type) ? ImageIndex.Folder : ImageIndex.MassSpecFile),
                name = dirInfo.Name,
                dateModified = GetSafeDateModified(dirInfo)
            };

            if (listView.View != View.Details ||
                (sourceTypeComboBox.SelectedIndex > 0 &&
                 sourceTypeComboBox.SelectedItem.ToString() != sourceInfo.type))
                return sourceInfo;

            if (sourceInfo.isFolder)
            {
                return sourceInfo;
            }

            if (!sourceInfo.isUnknown)
            {
                sourceInfo.size = 0;
                foreach( FileInfo fileInfo in dirInfo.GetFiles() )
                    sourceInfo.size += (UInt64) fileInfo.Length;
                return sourceInfo;
            }
            return null;
        }

        private SourceInfo getSourceInfo(FileInfo fileInfo)
        {
            string type = DataSourceUtil.GetSourceType(fileInfo);
            SourceInfo sourceInfo = new SourceInfo(new MsDataFilePath(fileInfo.FullName))
            {
                type = type,
                imageIndex = (DataSourceUtil.IsUnknownType(type) ? ImageIndex.UnknownFile : ImageIndex.MassSpecFile),
                name = fileInfo.Name
            };
            if( !sourceInfo.isUnknown )
            {
                if (listView.View != View.Details ||
                    (sourceTypeComboBox.SelectedIndex > 0 &&
                     sourceTypeComboBox.SelectedItem.ToString() != sourceInfo.type))
                    return sourceInfo;
                sourceInfo.size = (UInt64) fileInfo.Length;
                sourceInfo.dateModified = GetSafeDateModified(fileInfo);
                return sourceInfo;
            }
            return null;
        }

        protected bool _abortPopulateList;

        // Overload this method to add custom post-processing after populating the list view
        protected virtual void ListViewPostprocessing()
        {
        }

        private void populateListViewFromDirectory(MsDataFileUri directory)
        {
            _abortPopulateList = false;
            listView.Cursor = Cursors.Default;
            _waitingForData = false;
            listView.Items.Clear();

            var listSourceInfo = new List<SourceInfo>();
            if (null == directory || directory is MsDataFilePath && string.IsNullOrEmpty(((MsDataFilePath) directory).FilePath))
            {
                foreach (DriveInfo driveInfo in DriveInfo.GetDrives())
                {
                    string label = string.Empty;
                    string sublabel = driveInfo.Name;
                    ImageIndex imageIndex = ImageIndex.Folder;
                    _driveReadiness[sublabel] = false;
                    try
                    {
                        switch (driveInfo.DriveType)
                        {
                            case DriveType.Fixed:
                                imageIndex = ImageIndex.LocalDrive;
                                label = FileUIResources.OpenDataSourceDialog_populateListViewFromDirectory_Local_Drive;
                                if (driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.CDRom:
                                imageIndex = ImageIndex.OpticalDrive;
                                label = FileUIResources.OpenDataSourceDialog_populateListViewFromDirectory_Optical_Drive;
                                if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.Removable:
                                imageIndex = ImageIndex.OpticalDrive;
                                label = FileUIResources.OpenDataSourceDialog_populateListViewFromDirectory_Removable_Drive;
                                if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.Network:
                                label = FileUIResources.OpenDataSourceDialog_populateListViewFromDirectory_Network_Share;
                                break;
                        }
                        _driveReadiness[sublabel] = IsDriveReady(driveInfo);
                    }
                    catch (Exception)
                    {
                        label += string.Format(@" ({0})", FileUIResources.OpenDataSourceDialog_populateListViewFromDirectory_access_failure);
                    }

                    string name = driveInfo.Name;
                    if (label != string.Empty)
                        name = string.Format(@"{0} ({1})", label, name);

                    listSourceInfo.Add(new SourceInfo(new MsDataFilePath(driveInfo.RootDirectory.FullName))
                    {
                        type = DataSourceUtil.FOLDER_TYPE,
                        imageIndex = imageIndex,
                        name = name,
                        dateModified = GetDriveModifiedTime(driveInfo)
                    });
                }
            }
            else if (directory is RemoteUrl)
            {
                RemoteUrl remoteUrl = directory as RemoteUrl;
                if (string.IsNullOrEmpty(remoteUrl.ServerUrl))
                {
                    listSourceInfo.AddRange(
                        _remoteAccounts.Select(
                            remoteAccount => new SourceInfo(GetRootUrl(remoteAccount))
                            {
                                name = remoteAccount.AccountAlias, type = DataSourceUtil.FOLDER_TYPE, imageIndex = ImageIndex.MyNetworkPlaces,
                            }));
                    listSourceInfo.Add(new SourceInfo(null)  // Add the item to edit accounts
                    {
                        name = FileUIResources.BaseFileDialogNE_populateListViewFromDirectory_Add_Edit_Account___,
                        type = DataSourceUtil.EDIT_ACCOUNT,
                        imageIndex = ImageIndex.BlankImage,
                    });
                }
                else
                {
                    RemoteAccount remoteAccount = GetRemoteAccount(remoteUrl);
                    if (RemoteSession == null || !Equals(remoteAccount, RemoteSession.Account))
                    {
                        CreateNewRemoteSession(remoteAccount);
                    }
                    RemoteServerException exception;
                    var isComplete = _remoteSession.AsyncFetchContents(remoteUrl, out exception);
                    foreach (var item in _remoteSession.ListContents(remoteUrl))
                    {
                        var imageIndex = GetRemoteItemImageIndex(item);
 
                        listSourceInfo.Add(new SourceInfo(item.MsDataFileUri)
                        {
                            name = item.Label,
                            type = item.Type,
                            imageIndex = imageIndex,
                            dateModified = item.LastModified,
                            size = item.FileSize
                        });
                    }
                    if (null != exception)
                    {
                        if (MultiButtonMsgDlg.Show(this, exception.Message, FileUIResources.OpenDataSourceDialog_populateListViewFromDirectory_Retry) != DialogResult.Cancel)
                        {
                            RemoteSession?.RetryFetchContents(remoteUrl);
                            isComplete = false;
                        }
                    }
                    if (!isComplete)
                    {
                        listView.Cursor = Cursors.WaitCursor;
                        _waitingForData = true;
                    }
                }
            }
            else if (directory is MsDataFilePath)
            {
                MsDataFilePath msDataFilePath = (MsDataFilePath) directory;
                DirectoryInfo dirInfo = new DirectoryInfo(msDataFilePath.FilePath);

                try
                {
                    // subitems: Name, Type, Spectra, Size, Date Modified
                    var arraySubDirInfo = dirInfo.GetDirectories();
                    Array.Sort(arraySubDirInfo, (d1, d2) => string.Compare(d1.Name, d2.Name, StringComparison.CurrentCultureIgnoreCase));
                    var arrayFileInfo = dirInfo.GetFiles();
                    Array.Sort(arrayFileInfo, (f1, f2) => string.Compare(f1.Name, f2.Name, StringComparison.CurrentCultureIgnoreCase));

                    // Calculate information about the files, allowing the user to cancel
                    foreach (var info in arraySubDirInfo)
                    {
                        listSourceInfo.Add(getSourceInfo(info));
                        Application.DoEvents();
                        if (_abortPopulateList)
                        {
                            //MessageDlg.Show( "abort" );
                            break;
                        }
                    }

                    if (!_abortPopulateList)
                    {
                        foreach (var info in arrayFileInfo)
                        {
                            listSourceInfo.Add(getSourceInfo(info));
                            Application.DoEvents();
                            if (_abortPopulateList)
                            {
                                //MessageDlg.Show( "abort" );
                                break;
                            }
                        }
                    }
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(
                        FileUIResources.OpenDataSourceDialog_populateListViewFromDirectory_An_error_occurred_attempting_to_retrieve_the_contents_of_this_directory,
                        x.Message);
                    // Might throw access violation.
                    MessageDlg.ShowWithException(this, message, x);
                    return;
                }
            }

            listSourceInfo = listSourceInfo.Where(l => l != null).ToList(); // Ignore null entries in order to not confuse sort

            // Sorts files and folders in natural order rather than lexicographically with folders being prioritized (sorted above files)
            // e.g. (a1.txt, b10.folder, b2.folder, c6.txt --> b2.folder, b10.folder, a1.txt, c6.txt)
            listSourceInfo.Sort((x, y) =>
            {
                // Check to see if one element is a folder. If so regardless of name it will always prioritized over a file.
                if (x.isFolder != y.isFolder)
                {
                    return x.isFolder ? -1 : 1; 
                }
                return NaturalFilenameComparer.Compare(x.name, y.name); // Use normal compare if both elements are of the same type (folder vs folder, file vs file)
            }); // Sorts by natural sort order for easier more natural readability e.g. (A1.raw, A22.raw, A5.raw --> A1.raw, A5.raw, A22.raw)


            // Populate the list
            var items = new List<ListViewItem>();
            foreach (var sourceInfo in listSourceInfo)
            {
                if (sourceTypeComboBox == null ||
                     sourceTypeComboBox.SelectedItem == null || // null if no sourceTypes passed in
                     (sourceTypeComboBox != null &&
                        sourceTypeComboBox.SelectedItem != null &&
                        (sourceTypeComboBox.SelectedIndex == 0 ||
                         sourceTypeComboBox.SelectedItem.ToString() == sourceInfo.type)) ||
                     // Always show folders
                     sourceInfo.isFolder)
                {
                    // Filter for specifically named data sources (as when called from Skyline File>Share)
                    if (_specificDataSourceFilter != null && !sourceInfo.isFolder)
                    {
                        var name = sourceInfo.MsDataFileUri.GetFileName();
                        if (!_specificDataSourceFilter.Any(specificDataSource => specificDataSource.Equals(name,
                                StringComparison.CurrentCultureIgnoreCase)))
                        {
                            continue;
                        }
                    }

                    ListViewItem item = new ListViewItem(sourceInfo.ToArray(), (int) sourceInfo.imageIndex)
                    {
                        Tag = sourceInfo
                    };
                    item.SubItems[2].Tag = sourceInfo.size; // CONSIDER: file size is always 0 for method files
                    item.SubItems[3].Tag = sourceInfo.dateModified;
                        
                    items.Add(item);
                }
            }
            listView.Items.AddRange(items.ToArray());
            ListViewPostprocessing();
        }

        protected virtual ImageIndex GetRemoteItemImageIndex(RemoteItem item)
        {
            return item.Type switch
            {
                DataSourceUtil.FOLDER_TYPE => ImageIndex.Folder,
                DataSourceUtil.TYPE_WATERS_ACQUISITION_METHOD => ImageIndex.MethodFile,
                DataSourceUtil.SAMPLE_SET_TYPE => ImageIndex.SampleSet,
                _ => ImageIndex.MassSpecFile
            };
        }

        private void RemoteContentsAvailable()
        {
            // ReSharper disable EmptyGeneralCatchClause
            try
            {
                while (!IsHandleCreated)    // Cannot call BeginInvoke until the handle is created
                {
                    // Wait for the handle to be created
                    System.Threading.Thread.Sleep(100);
                }
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (CurrentDirectory is RemoteUrl && _waitingForData)
                        {
                            populateListViewFromDirectory(CurrentDirectory);
                        }
                    }
                    catch
                    {
                    }
                }));
            }
            catch
            {
            }
            // ReSharper restore EmptyGeneralCatchClause
        }

        private RemoteAccount GetRemoteAccount(RemoteUrl remoteUrl)
        {
            return
                _remoteAccounts.FirstOrDefault(
                    remoteAccount =>
                        Equals(remoteAccount.ServerUrl, remoteUrl.ServerUrl) &&
                        Equals(remoteAccount.Username, remoteUrl.Username));
        }

        private static DateTime? GetSafeDateModified(FileSystemInfo dirInfo)
        {
            try
            {
                return dirInfo.LastWriteTime;
            }
            catch (IOException) {}
            catch (UnauthorizedAccessException) {}
            return null;
        }

        private void populateComboBoxFromDirectory( MsDataFileUri directory )
        {
            if (IsRemote && !(directory is RemoteUrl))
                throw new ArgumentException(@"Cannot show a local path when remote mode is requested.");
            lookInComboBox.SuspendLayout();

            // remove old items except My Documents, Desktop, My Computer, and the Remote root
            for (int i = lookInComboBox.Items.Count - 1; i >= 0; i--)
            {
                if (lookInComboBox.Items[i] is TreeNode node)
                {
                    if (node.Tag is RemoteUrl remoteUrl && !ReferenceEquals(remoteUrl, RemoteUrl.EMPTY))
                    {
                        lookInComboBox.Items.RemoveAt(i);
                    }
                    else if (node.Tag is MsDataFilePath path)
                    {
                        if (node.ImageIndex == (int)ImageIndex.MyDocuments ||
                            node.ImageIndex == (int)ImageIndex.Desktop ||
                            node.ImageIndex == (int)ImageIndex.MyComputer)
                            continue;
                        lookInComboBox.Items.RemoveAt(i);
                    }
                }
            }

            DirectoryInfo dirInfo = null;
            int driveCount = 0;

            if (directory is MsDataFilePath)
            {
                MsDataFilePath msDataFilePath = (MsDataFilePath)directory;
                if (!string.IsNullOrEmpty(msDataFilePath.FilePath))
                {
                    dirInfo = new DirectoryInfo(msDataFilePath.FilePath);
                }
                else
                {
                    lookInComboBox.SelectedIndex = _myComputerIndex;
                }
            }
            else if (directory is RemoteUrl remoteUrl)
            {
                if (string.IsNullOrEmpty(remoteUrl.EncodedPath))
                {
                    lookInComboBox.SelectedIndex = _remoteIndex;
                }

                if (!ReferenceEquals(remoteUrl, RemoteUrl.EMPTY))
                {
                    upOneLevelButton.Enabled = true;
                    TreeNode remoteNode = (TreeNode)lookInComboBox.Items[_remoteIndex];

                    ++driveCount;
                    var remoteAccount = remoteUrl.FindMatchingAccount();
                    TreeNode serverNode = remoteNode.Nodes.Add(remoteUrl.ServerUrl,
                        remoteAccount?.AccountAlias ?? remoteUrl.ServerUrl,
                        (int)ImageIndex.MyNetworkPlaces,
                        (int)ImageIndex.MyNetworkPlaces);
                    // ReSharper disable once PossibleUnintendedReferenceComparison
                    serverNode.Tag = (remoteUrl != RemoteUrl.EMPTY) ? remoteUrl.ChangePathParts(null) : remoteUrl;
                    lookInComboBox.Items.Insert(_remoteIndex + driveCount, serverNode);

                    var branches = remoteUrl.GetPathParts().ToList();
                    TreeNode pathNode = serverNode;
                    if (!string.IsNullOrEmpty(remoteUrl.EncodedPath))
                    {
                        for (int i = 0; i < branches.Count; ++i)
                        {
                            ++driveCount;
                            int imageIndex = (int)ImageIndex.Folder;
                            if (i + 1 == branches.Count && DataSourceUtil.IsSampleSetType(remoteUrl.SourceType))
                                imageIndex = (int)ImageIndex.SampleSet; // only last branch can be a sample set
                            pathNode = pathNode.Nodes.Add(branches[i], branches[i], imageIndex, imageIndex);
                            pathNode.Tag = remoteUrl.ChangePathParts(branches.GetRange(0, i + 1));
                            lookInComboBox.Items.Insert(_remoteIndex + driveCount, pathNode);
                        }
                    }
                    lookInComboBox.SelectedIndex = _remoteIndex + driveCount;
                }
                else
                {
                    upOneLevelButton.Enabled = false;
                }
            }

            if (!IsRemote)
            {
                TreeNode myComputerNode = (TreeNode)lookInComboBox.Items[_myComputerIndex];

                foreach (DriveInfo driveInfo in DriveInfo.GetDrives())
                {
                    string label = string.Empty;
                    string sublabel = driveInfo.Name;
                    ImageIndex imageIndex = ImageIndex.Folder;
                    ++driveCount;
                    _driveReadiness[sublabel] = false;
                    try
                    {
                        switch (driveInfo.DriveType)
                        {
                            case DriveType.Fixed:
                                imageIndex = ImageIndex.LocalDrive;
                                label = FileUIResources.OpenDataSourceDialog_populateComboBoxFromDirectory_Local_Drive;
                                if (driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.CDRom:
                                imageIndex = ImageIndex.OpticalDrive;
                                label = FileUIResources
                                    .OpenDataSourceDialog_populateComboBoxFromDirectory_Optical_Drive;
                                if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.Removable:
                                imageIndex = ImageIndex.OpticalDrive;
                                label = FileUIResources
                                    .OpenDataSourceDialog_populateComboBoxFromDirectory_Removable_Drive;
                                if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.Network:
                                label = FileUIResources
                                    .OpenDataSourceDialog_populateComboBoxFromDirectory_Network_Share;
                                break;
                        }
                        _driveReadiness[sublabel] = IsDriveReady(driveInfo);
                    }
                    catch (Exception)
                    {
                        label += string.Format(@" ({0})",
                            FileUIResources.OpenDataSourceDialog_populateComboBoxFromDirectory_access_failure);
                    }
                    TreeNode driveNode = myComputerNode.Nodes.Add(sublabel,
                        label.Length > 0
                            ? String.Format(@"{0} ({1})", label, sublabel)
                            : sublabel,
                        (int)imageIndex,
                        (int)imageIndex);
                    driveNode.Tag = new MsDataFilePath(sublabel);
                    lookInComboBox.Items.Insert(_myComputerIndex + driveCount, driveNode);

                    if (dirInfo != null && sublabel == dirInfo.Root.Name)
                    {
                        List<string> branches = new List<string>(((MsDataFilePath)directory).FilePath.Split(new[]
                            {
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar
                            },
                            StringSplitOptions.RemoveEmptyEntries));
                        TreeNode pathNode = driveNode;
                        for (int i = 1; i < branches.Count; ++i)
                        {
                            ++driveCount;
                            pathNode = pathNode.Nodes.Add(branches[i], branches[i], (int)ImageIndex.Folder, (int)ImageIndex.Folder);
                            pathNode.Tag = new MsDataFilePath(String.Join(
                                Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture),
                                branches.GetRange(0, i + 1).ToArray()));
                            lookInComboBox.Items.Insert(_myComputerIndex + driveCount, pathNode);
                        }
                        lookInComboBox.SelectedIndex = _myComputerIndex + driveCount;
                        upOneLevelButton.Enabled = (branches.Count > 1);
                    }
                }
            }

            lookInComboBox.DropDownHeight = lookInComboBox.Items.Count * 18 + 2;

            lookInComboBox.ResumeLayout();
        }

        /// <summary>
        /// Allows override in subclasses
        /// </summary>
        /// <param name="remoteAccount"></param>
        protected virtual void CreateNewRemoteSession(RemoteAccount remoteAccount)
        {
            RemoteSession = RemoteSession.CreateSession(remoteAccount);
        }

        private void sourcePathTextBox_KeyUp( object sender, KeyEventArgs e )
        {
            switch( e.KeyCode )
            {
                case Keys.F5:
                    _abortPopulateList = true;
                    populateListViewFromDirectory( _currentDirectory ); // refresh
                    break;
            }
        }

        protected virtual void OnFileNameTyped() {}

        private void sourcePathTextBox_TextChanged(object sender, EventArgs e)
        {
            OnFileNameTyped();
        }
        private void sourcePathTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            listView.ItemSelectionChanged -= listView_ItemSelectionChanged;
            listView.SelectedItems.Clear();
            listView.ItemSelectionChanged += listView_ItemSelectionChanged;
        }
        protected static bool TreatAsFolder(string itemText)
        {
            return DataSourceUtil.IsFolderType(itemText) || DataSourceUtil.IsSampleSetType(itemText);
        }

        /// <summary>
        /// Event handler invoked when selecting an item in the File Dialog. May
        /// cause a folder to open or close. Opening a folder may ask a remote
        /// server to provide information about its file / folder contents.
        /// </summary>
        private void listView_ItemActivate( object sender, EventArgs e )
        {
            if (!EditAccount())
                SelectItem();
        }

        protected virtual void SelectItem()
        {
            var selected = listView.SelectedItems.OfType<ListViewItem>().ToList();
            if (selected.Count == 0)
                return;

            ListViewItem item = selected[0];
            if (TreatAsFolder(item.SubItems[1].Text))
            {
                OpenFolderItem(item);
            }
            else
            {
                FileNames = new[] { ((SourceInfo) item.Tag).MsDataFileUri,  };
                DoMainAction();
            }
        }

        private bool EditAccount()
        {
            var selected = listView.SelectedItems.OfType<ListViewItem>().ToList();
            if (selected.Count == 0)
                return false;

            ListViewItem selectedItem = selected[0];
            if (DataSourceUtil.IsEditAccount(selectedItem.SubItems[1].Text))
            {
                var list = Settings.Default.RemoteAccountList;
                var listNew = list.EditList(this, null);
                if (listNew != null)
                {
                    list.Clear();
                    list.AddRange(listNew);
                    SetRemoteAccounts(Settings.Default.RemoteAccountList);
                }
                // clear listView contents if all remote accounts have been removed
                if (!list.Any())
                    listView.Clear();
                CurrentDirectory = RemoteUrl.EMPTY;
                return true;
            }
            return false;
        }

        protected virtual void SetRemoteAccounts(IEnumerable<RemoteAccount> accounts)
        {
            _remoteAccounts.Clear();
            _remoteAccounts.AddRange(accounts);
        }

        protected void OpenFolderItem(ListViewItem listViewItem)
        {
            OpenFolder(((SourceInfo) listViewItem.Tag).MsDataFileUri);
        }

        protected void OpenFolder(MsDataFileUri uri)
        {
            if (_currentDirectory != null)
                _previousDirectories.Push(_currentDirectory);
            CurrentDirectory = uri;
            _abortPopulateList = true;
            sourcePathTextBox.Clear();
        }

        protected bool OpenFolderFromTextBox()
        {
            var fileOrDirName = sourcePathTextBox.Text;
            bool exists;
            bool triedAddingDirectory = false;
            while (!(exists = ((File.Exists(fileOrDirName) || Directory.Exists(fileOrDirName)))))
            {
                if (triedAddingDirectory)
                    break;
                MsDataFilePath currentDirectoryPath = CurrentDirectory as MsDataFilePath;
                if (null == currentDirectoryPath)
                    break;
                fileOrDirName = Path.Combine(currentDirectoryPath.FilePath, fileOrDirName);
                triedAddingDirectory = true;
            }
            if (exists)
            {
                if (DataSourceUtil.IsDataSource(fileOrDirName))
                {
                    FileNames = new[] { MsDataFileUri.Parse(fileOrDirName) };
                    DialogResult = DialogResult.OK;
                }
                else if (Directory.Exists(fileOrDirName))
                    OpenFolder(new MsDataFilePath(fileOrDirName));
                return true;
            }
            return false;
        }

        private void listView_ColumnClick( object sender, ColumnClickEventArgs e )
        {
            ToggleListViewSort(e.Column);
        }

        private void ToggleListViewSort(int columnIndex)
        {
            var order = SortOrder.Ascending;
            if (columnIndex == _listViewColumnSorter.SortColumn && order == _listViewColumnSorter.Order)
                order = SortOrder.Descending;
            SetListViewSort(columnIndex, order);
        }

        public void SetListViewSort(int columnIndex, SortOrder order)
        {
            _listViewColumnSorter.SortColumn = columnIndex;
            _listViewColumnSorter.Order = order;

            if (listView.IsHandleCreated)
                listView.Sort();
        }

        private void openButton_Click( object sender, EventArgs e )
        {
            if (!EditAccount())
                DoMainAction();
        }

        // TODO: rename OkDialog for consistency with other Skyline dialogs, especially useful for test readability. Do this after PR3170 merged.
        protected virtual void DoMainAction()
        {
            throw new Exception(@"method DoMainAction() MUST be overridden");
        }

        private void cancelButton_Click( object sender, EventArgs e )
        {
            _abortPopulateList = true;
            DialogResult = DialogResult.Cancel;
            Close();
            Application.DoEvents();
        }

        private void tilesToolStripMenuItem_Click( object sender, EventArgs e )
        {
            SetListView(View.Tile);
        }

        private void listToolStripMenuItem_Click( object sender, EventArgs e )
        {
            SetListView(View.List);
        }

        private void detailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetListView(View.Details);
        }

        private void SetListView(View view)
        {
            if (listView.View != view)
            {
                int menuIndex = GetMenuIndex(view);
                var items = viewsDropDownButton.DropDownItems;
                for (int i = 0; i < items.Count; i++)
                    ((ToolStripMenuItem)items[i]).Checked = (i == menuIndex);

                listView.BeginUpdate();
                listView.View = view;
                if (view == View.Details)
                {
                    populateListViewFromDirectory(_currentDirectory);
                    listView.Columns[0].Width = 200;
                }
                else
                {
                    listView.Columns[0].Width = -1;
                }
                listView.EndUpdate();
            }
        }

        private int GetMenuIndex(View view)
        {
            switch (view)
            {
                case View.Details:
                    return 2;
                case View.Tile:
                    return 0;
                default:
                    return 1;
            }
        }

        private void upOneLevelButton_Click( object sender, EventArgs e )
        {
            MsDataFileUri parent = null;
            var dataFilePath = _currentDirectory as MsDataFilePath;
            if (dataFilePath != null && !string.IsNullOrEmpty(dataFilePath.FilePath))
            {
                DirectoryInfo parentDirectory = Directory.GetParent(dataFilePath.FilePath);
                if (parentDirectory != null)
                {
                    parent = new MsDataFilePath(parentDirectory.FullName);
                }
            }
            else if (_currentDirectory is RemoteUrl remoteUrl)
            {
                var pathParts = remoteUrl.GetPathParts().ToList();
                if (!string.IsNullOrEmpty(remoteUrl.EncodedPath))
                    parent = remoteUrl.ChangePathParts(remoteUrl.GetPathParts().Take(pathParts.Count - 1));
                else if (!RemoteUrl.EMPTY.Equals(remoteUrl))
                {
                    parent = RemoteUrl.EMPTY;
                }
            }
            else
            {
                if (_previousDirectories.Any())
                {
                    CurrentDirectory = _previousDirectories.Pop();
                    return;
                }
            }
            if (null != parent && !Equals(parent, _currentDirectory))
            {
                _previousDirectories.Push(_currentDirectory);
                CurrentDirectory = parent;
            }
        }

        private void backButton_Click( object sender, EventArgs e )
        {
            if( _previousDirectories.Count > 0 )
                CurrentDirectory = _previousDirectories.Pop();
        }

        private void listView_ItemSelectionChanged( object sender, ListViewItemSelectionChangedEventArgs e )
        {
            var selected = listView.SelectedItems.OfType<ListViewItem>().ToList();

            if (selected.Count > 1 )
            {
                List<string> dataSourceList = new List<string>();
                foreach( ListViewItem item in selected)
                {
                    if( !TreatAsFolder(item.SubItems[1].Text) && !DataSourceUtil.IsEditAccount(item.SubItems[1].Text) )
                        // ReSharper disable LocalizableElement
                        dataSourceList.Add(string.Format("\"{0}\"", GetItemPath(item)));
                        // ReSharper restore LocalizableElement
                }
                sourcePathTextBox.Text = string.Join(@" ", dataSourceList.ToArray());
            }
            else if (listView.SelectedItems.Count > 0 && !DataSourceUtil.IsEditAccount(listView.SelectedItems[0].SubItems[1].Text))
            {
                sourcePathTextBox.Text = GetItemPath(listView.SelectedItems[0]);
            }
            else
            {
                sourcePathTextBox.Text = string.Empty;
            }
        }

        // ReSharper disable LocalizableElement
        private static readonly Regex REGEX_DRIVE = new Regex("\\(([A-Z]:\\\\)\\)");
        // ReSharper restore LocalizableElement

        private static string GetItemPath(ListViewItem item)
        {
            string path = item.SubItems[0].Text;
            Match match = REGEX_DRIVE.Match(path);
            if (match.Success)
                path = match.Groups[1].Value;
            return path;
        }

        private void sourceTypeComboBox_SelectionChangeCommitted( object sender, EventArgs e )
        {
            _sourceTypeName = (string) sourceTypeComboBox.SelectedItem;
            populateListViewFromDirectory( _currentDirectory );
            _abortPopulateList = true;
        }

        private void listView_KeyDown( object sender, KeyEventArgs e )
        {
            switch( e.KeyCode )
            {
                case Keys.F5:
                    populateListViewFromDirectory( _currentDirectory ); // refresh
                    _abortPopulateList = true;
                    break;
                case Keys.A:
                    if (e.Control)
                    {
                        foreach (ListViewItem item in listView.Items)
                            item.Selected = true;
                    }
                    break;
            }
        }

        private void this_KeyDown(object sender, KeyEventArgs e)
        {
            KeyPressHandler(e.KeyCode);
        }

        public void KeyPressHandler(Keys key)
        {
            if (key == Keys.Enter)
                DoMainAction();
        }

        private void remoteAccountsButton_Click( object sender, EventArgs e )
        {
            if (Equals(CurrentDirectory, RemoteUrl.EMPTY) ||
                CurrentDirectory is RemoteUrl && _remoteAccounts.Count == 1)
            {
                var list = Settings.Default.RemoteAccountList;
                var listNew = list.EditList(this, null);
                if (listNew != null)
                {
                    list.Clear();
                    list.AddRange(listNew);
                }
                // clear listView contents if all remote accounts have been removed
                if (!list.Any())
                    listView.Clear();
            }
            CurrentDirectory = RemoteUrl.EMPTY;
        }

        private void desktopButton_Click( object sender, EventArgs e )
        {
            CurrentDirectory = new MsDataFilePath(Environment.GetFolderPath( Environment.SpecialFolder.DesktopDirectory ));
        }

        private void myDocumentsButton_Click( object sender, EventArgs e )
        {
            CurrentDirectory = new MsDataFilePath(Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ));
        }

        private void myComputerButton_Click( object sender, EventArgs e )
        {
            CurrentDirectory = new MsDataFilePath(Environment.GetFolderPath(Environment.SpecialFolder.MyComputer));
        }

//        private void myNetworkPlacesButton_Click( object sender, EventArgs e )
//        {
//
//        }

        private readonly Dictionary<string, bool> _driveReadiness = new Dictionary<string, bool>();
        private void lookInComboBox_DropDown( object sender, EventArgs e )
        {
            
        }

        private void lookInComboBox_DrawItem( object sender, DrawItemEventArgs e )
        {
            if( e.Index < 0 || e.Index >= lookInComboBox.Items.Count)
                return;

            var node = (TreeNode) lookInComboBox.Items[e.Index];
            var textSize = TextRenderer.MeasureText(node.Text, lookInComboBox.Font);

            int x, indent;
            int y = e.Bounds.Y + (e.Bounds.Height - 16) / 2;
            if ( ( e.State & DrawItemState.ComboBoxEdit ) == DrawItemState.ComboBoxEdit )
            {
                x = 3;  // Any less and the image gets clipped
                indent = 0;
            }
            else
            {
                e.DrawBackground();
                e.DrawFocusRectangle();

                x = node.TreeView.Indent / 2;
                indent = node.TreeView.Indent * node.Level;
            }

            var image = lookInImageListSmall.Images[node.ImageIndex];
            e.Graphics.DrawImage( image, x + indent, y, 16, 16 );
            int textY = e.Bounds.Y + (e.Bounds.Height - textSize.Height) / 2;   // Vertically center the text
            e.Graphics.DrawString( node.Text, lookInComboBox.Font, new SolidBrush(lookInComboBox.ForeColor), x + indent + 16, textY );
        }

        private void lookInComboBox_MeasureItem( object sender, MeasureItemEventArgs e )
        {
            if( e.Index < 0 )
                return;

            TreeNode node = (TreeNode) lookInComboBox.Items[e.Index];
            int x = node.TreeView.Indent / 2;
            int indent = node.TreeView.Indent * node.Level;
            e.ItemHeight = 16;
            e.ItemWidth = x + indent + 16 + (int) e.Graphics.MeasureString( node.Text, lookInComboBox.Font ).Width;
        }

        private void lookInComboBox_SelectionChangeCommitted( object sender, EventArgs e )
        {
            if( lookInComboBox.SelectedIndex < 0 )
                lookInComboBox.SelectedIndex = 0;

            var selectedItem = (TreeNode) lookInComboBox.SelectedItem;
            var prevDirectory = CurrentDirectory;
            MsDataFileUri msDataFileUri = (MsDataFileUri) selectedItem.Tag;
            if (msDataFileUri is MsDataFilePath)
            {
                bool isReady = false;
                string location = ((MsDataFilePath) msDataFileUri).FilePath;
                foreach (var drivePair in _driveReadiness)
                {
                    if (location.StartsWith(drivePair.Key))
                    {
                        // If it is ready switch to it
                        if (drivePair.Value)
                        {
                            isReady = true;
                        }
                        break;
                    }
                }
                if (!isReady)
                {
                    return;
                }
            }
            CurrentDirectory = msDataFileUri;
            if(!Equals(prevDirectory, CurrentDirectory))
                _previousDirectories.Push(prevDirectory);
        }
        protected class SourceInfo
        {
            public SourceInfo(MsDataFileUri msDataFileUri)
            {
                MsDataFileUri = msDataFileUri;
            }
            // ReSharper disable InconsistentNaming
            public MsDataFileUri MsDataFileUri { get; private set; }
            public string name;
            public string type;
            public ImageIndex imageIndex;
            public UInt64 size;
            public DateTime? dateModified;
            // ReSharper restore InconsistentNaming

            public bool isFolder
            {
                get { return DataSourceUtil.IsFolderType(type); }
            }

            public bool isUnknown
            {
                get { return DataSourceUtil.IsUnknownType(type); }
            }

            public string[] ToArray()
            {
                return new[]
            {
                name,
                type,
                SizeLabel,
                DateModifiedLabel
            };
            }

            private string DateModifiedLabel
            {
                get
                {
                    return dateModified.HasValue
                        ? TextUtil.SpaceSeparate(dateModified.Value.ToShortDateString(),
                            dateModified.Value.ToShortTimeString())
                        : String.Empty;
                }
            }

            private string SizeLabel
            {
                get
                {
                    return type != DataSourceUtil.FOLDER_TYPE
                        ? String.Format(new FileSizeFormatProvider(), @"{0:fs}", size)
                        : String.Empty;
                }
            }
        }

        public enum ImageIndex
        {
            RecentDocuments,
            Desktop,
            MyDocuments,
            MyComputer,
            MyNetworkPlaces,
            LocalDrive,
            OpticalDrive,
            NetworkDrive,
            Folder,
            MassSpecFile,
            UnknownFile,
            MethodFile,
            NoAccessFolder,
            ReadOnlyFolder,
            ReadWriteFolder,
            BlankImage,
            SampleSet
        }

        private void EnsureRemoteAccount()
        {
            if (_remoteAccounts.Any())
            {
                return;
            }
            var newAccount = Settings.Default.RemoteAccountList.NewItem(this, Settings.Default.RemoteAccountList, null);
            if (null != newAccount)
            {
                Settings.Default.RemoteAccountList.Add(newAccount);
                _remoteAccounts.Add(newAccount);
            }
        }

        private void recentDocumentsButton_Click(object sender, EventArgs e)
        {
            CurrentDirectory = new MsDataFilePath(Environment.GetFolderPath(Environment.SpecialFolder.Recent));
        }

        public bool WaitingForData { get { return _waitingForData; } }

        private static bool DriveMightBeSlow(DriveInfo driveInfo)
        {
            return driveInfo.DriveType == DriveType.Network;
        }

        private static bool IsDriveReady(DriveInfo driveInfo)
        {
            return DriveMightBeSlow(driveInfo) || driveInfo.IsReady;
        }

        private static DateTime? GetDriveModifiedTime(DriveInfo driveInfo)
        {
            if (DriveMightBeSlow(driveInfo))
            {
                return null;
            }
            return GetSafeDateModified(driveInfo.RootDirectory);
        }

        public IEnumerable<string> ListItemNames { get { return listView.Items.OfType<ListViewItem>().Select(item=>item.Text); } }
    }
}
