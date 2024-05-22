using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;
using pwiz.Common.GUI;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient.Properties;


namespace pwiz.PanoramaClient
{
    public partial class PanoramaFilePicker : CommonFormEx
    {
        private static JToken _runsInfoJson;
        private static JToken _sizeInfoJson;
        private readonly Dictionary<long, long> _sizeDictionary = new Dictionary<long, long>(); // Stores the Id of a mass spec run and its size in a dictionary
        private readonly Dictionary<long, string> _nameDictionary = new Dictionary<long, string>(); // Stores the Id of a mass spec run and its name in a dictionary
        private readonly List<PanoramaServer> _servers;
        private string _treeState;
        private bool _restoring;
        private readonly bool _showWebDav;
        private int _sortColumn = -1;

        public static string RECENT_VER => Resources.PanoramaFilePicker_RECENT_VER_Most_recent;
        public static string ALL_VER => Resources.PanoramaFilePicker_ALL_VER_All;
        public const string EXT = ".sky";
        public const string ZIP_EXT = ".sky.zip";

        public string OkButtonText { get; set; }
        public bool IsLoaded { get; private set; }
        public PanoramaFolderBrowser FolderBrowser { get; private set; }
        public string FileUrl { get; private set; } 
        public string FileName { get; private set; } // Skyline documents can be renamed in Panorama and may have a different name than what is stored on the server
        public long FileSize { get; private set; }
        public JToken TestFileJson;
        public JToken TestSizeJson;
        public string SelectedPath { get; private set; }

        public bool ShowLatestVersion => versionOptions.Text.Equals(RECENT_VER);

        public PanoramaFilePicker(List<PanoramaServer> servers, string stateString, bool showWebDav = false, string selectedPath = null)
        {
            InitializeComponent();

            _servers = servers;
            _treeState = stateString;
            _showWebDav = showWebDav;
            _restoring = true;
            versionOptions.Text = RECENT_VER;
            _restoring = false;
            SelectedPath = selectedPath;
            noFiles.Visible = false;
        }

        public void InitializeDialog()
        {
            if (FolderBrowser == null)
            {
                if (_showWebDav)
                {
                    FolderBrowser = new WebDavBrowser(_servers.FirstOrDefault(), _treeState, SelectedPath);
                }
                else
                {
                    FolderBrowser = new LKContainerBrowser(_servers, _treeState, false, SelectedPath);
                }
            }

            FolderBrowser.Dock = DockStyle.Fill;
            FolderBrowser.AddFiles += AddFiles;
            FolderBrowser.NodeClick += FilePicker_MouseClick;
            browserSplitContainer.Panel1.Controls.Add(FolderBrowser);
            urlLink.Text = FolderBrowser.GetSelectedUri();
            // CONSIDER: Is this really necessary when UpdateButtonState will be called on Load
            if (string.IsNullOrEmpty(_treeState))
            {
                up.Enabled = false;
                back.Enabled = false;
                forward.Enabled = false;
            }
            else
            {
                up.Enabled = FolderBrowser.UpEnabled;
                back.Enabled = FolderBrowser.BackEnabled;
                forward.Enabled = FolderBrowser.ForwardEnabled;
            }
        }

        /// <summary>
        /// Changes the 'Open' button text if a custom string is passed in
        /// and docks the PanoramaFolderBrowser user control in a panel
        /// </summary>
        private void FilePicker_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(OkButtonText))
            {
                open.Text = OkButtonText;
            }
            IsLoaded = true;
            UpdateButtonState();
        }

        /// <summary>
        /// Builds a string that will be used as a URI to find all .sky folders
        /// </summary>
        private static Uri BuildQuery(string server, string folderPath, string queryName, string folderFilter, string[] columns, string sortParam)
        {
            var columnsQueryParam = columns != null ? "&query.columns=" + string.Join(",", columns) : string.Empty;
            var sortQueryParam = !string.IsNullOrEmpty(sortParam) ? "&query.sort={sortParam}" : string.Empty;
            var allQueryParams = $"schemaName=targetedms&query.queryName={queryName}&query.containerFilterName={folderFilter}{columnsQueryParam}{sortQueryParam}";
            var queryUri = PanoramaUtil.CallNewInterface(new Uri(server), @"query", folderPath, @"selectRows", allQueryParams);
            return queryUri;
        }

        /// <summary>
        /// Takes in a query string and returns the associated JSON
        /// </summary>
        private JToken GetJson(Uri queryUri)
        {
            using (var requestHelper = new PanoramaRequestHelper(new WebClientWithCredentials(queryUri, FolderBrowser.GetActiveServer().Username,
                       FolderBrowser.GetActiveServer().Password)))
            {
                JToken json = requestHelper.Get(queryUri);
                return json;
            }
        }

        /// <summary>
        /// Adds all files in a particular folder to
        /// a ListView
        /// </summary>
        private void AddAllFiles(Uri newUri)
        {
            var json = GetJson(newUri);
            ShowFiles((int)json[@"fileCount"] != 0);
            if ((int)json[@"fileCount"] != 0)
            {
                var files = json[@"files"];
                foreach (var file in files)
                {
                    var listItem = new string[5];
                    var fileName = (string)file[@"text"];
                    listItem[0] = fileName;
                    var isFile = (bool)file[@"leaf"];
                    if (isFile)
                    {
                        var canRead = (bool)file[@"canRead"];
                        if (!canRead)
                        {
                            continue;
                        }


                        var link = (string)file[@"href"];
                        link = link.Substring(1);
                        var size = (long)file[@"size"];
                        var sizeObj = new FileSizeFormatProvider();
                        var sizeString = sizeObj.Format(@"fs1", size, sizeObj);
                        listItem[1] = sizeString;
                        // The date format for webDav files is: Thu Jul 13 12:00:00 PDT 2023
                        // We need to use a custom date format and remove the time zone characters
                        // in order to apply an InvariantCulture
                        var date = (string)file[@"creationdate"];
                        date = date.Remove(20, 4);
                        var format = "ddd MMM dd HH:mm:ss yyyy";
                        DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var formattedDate);
                        listItem[4] = formattedDate.ToString(CultureInfo.CurrentCulture);
                        var fileNode = fileName.EndsWith(EXT) || fileName.EndsWith(ZIP_EXT) ? new ListViewItem(listItem, 1) : new ListViewItem(listItem, 0);
                        fileNode.Tag = size;
                        fileNode.Name = link;
                        listView.Items.Add(fileNode);
                    }
                }
            }
        }

        /// <summary>
        /// Populates one dictionary with the ID of a mass spec run and its
        /// name on the server, and populates another dictionary with the ID of
        /// a mass spec run and its size
        /// </summary>
        private void GetRunsDict()
        {
            _nameDictionary.Clear();
            _sizeDictionary.Clear();
            var rowSize = _sizeInfoJson[@"rows"];
            foreach (var curRow in rowSize)
            {
                var runName = (string)curRow[@"FileName"];
                var curId = (long)curRow[@"Id"];
                var size = curRow[@"DocumentSize"];
                _nameDictionary.Add(curId, runName);
                if (size.Type != JTokenType.Null)
                {
                    var lSize = size.ToObject<long>();
                    _sizeDictionary.Add(curId, lSize);
                }
            }
        }

        /// <summary>
        /// Returns true if a file has multiple versions, and false if not
        /// </summary>
        private bool HasVersions(JToken json)
        {
            var rows = json[@"rows"];
            return rows.Select(row => row[@"Replaced"].ToString()).Any(replaced => replaced.Equals("True"));
        }

        /// <summary>
        /// Given the ID of the mass spec run, find that run and
        /// return the number of versions associated with the run and its name
        /// </summary>
        private string[] GetVersionInfo(JToken json, string replacedBy)
        {
            var result = new string[2];
            var rows = json[@"rows"];
            foreach (var row in rows)
            {
                var rowId = row[@"RowId"].ToString();
                if (rowId.Equals(replacedBy))
                {
                    result[1] = (string)row[@"Name"];
                }
                var count = (string)row[@"File/Versions"];
                result[0] = count;
            }
            return result;
        }

        /// <summary>
        /// Adds Skyline files in a particular folder to the ListView
        /// If showLatestVersion is true, only the most recent version
        /// of all files will be shown, otherwise all versions will be shown
        /// </summary>
        private void AddSkyFiles(bool hasVersions, bool showLatestVersion)
        {
            ModifyListViewCols(hasVersions, showLatestVersion);
            var rows = _runsInfoJson[@"rows"];

            foreach (var row in rows)
            {
                var fileName = (string)row[@"Name"];
                var id = (long)row[@"File/Id"];
                var versions = row[@"File/Versions"].ToString();
                var rowReplaced = (string)row[@"ReplacedByRun"];
                var replaces = (string)row[@"ReplacesRun"];
                var listItem = new string[5];
                _nameDictionary.TryGetValue(id, out var serverName);
                listItem[0] = fileName;
                long size = 0;
                if (_sizeDictionary.TryGetValue(id, out var value))
                {
                    size = value;
                }

                if (size > 0)
                {
                    var sizeObj = new FileSizeFormatProvider();
                    var sizeString = sizeObj.Format(@"fs1", size, sizeObj);
                    listItem[1] = sizeString;
                }

                if (showLatestVersion)
                {
                    listItem[2] = versions;
                }
                else
                {
                    var numVersions = GetVersionInfo(_runsInfoJson, rowReplaced);
                    if (numVersions[0] != null)
                    {
                        listItem[2] = versions;
                    }
                    else
                    {
                        listItem[2] = 1.ToString();
                    }

                    if (numVersions[1] != null)
                    {
                        listItem[3] = numVersions[1];
                    }
                }

                DateTime.TryParse((string)row[@"Created"], CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var formattedDate);
                listItem[4] = formattedDate.ToString(CultureInfo.CurrentCulture);
                var fileNode = new ListViewItem(listItem, 1)
                {
                    Name = serverName,
                    ToolTipText =
                        $"Proteins: {row[@"File/Proteins"]}, Peptides: {row[@"File/Peptides"]}, Precursors: {row[@"File/Precursors"]}, Transitions: {row[@"File/Transitions"]}, Replicates: {row[@"File/Replicates"]}",
                    Tag = size
                };
                if (showLatestVersion)
                {
                    if ((!string.IsNullOrEmpty(replaces) && string.IsNullOrEmpty(rowReplaced)) ||
                        versions.Equals(1.ToString()))
                    {
                        listView.Items.Add(fileNode);
                    }
                }
                else
                {
                    listView.Items.Add(fileNode);
                }
            }
        }

        /// <summary>
        /// Checks if a given node has any Skyline files on the server
        /// and if it does, updates the ListView accordingly, otherwise, show a message
        /// saying there are no files
        /// </summary>
        public void AddFiles(object sender, EventArgs e)
        {
            try
            {
                _restoring = true;
                urlLink.Text = FolderBrowser.GetSelectedUri();
                ShowFiles(true);
                // versionOptions.Text = RECENT_VER;
                var path = FolderBrowser.GetFolderPath();
                listView.Items.Clear();
                _restoring = false;
                if (!string.IsNullOrEmpty(path))
                {
                    if (!_showWebDav)
                    {
                        if (FolderBrowser.GetNodeIsTargetedMS())
                        {
                            // This means we're running a test
                            if (TestFileJson != null)
                            {
                                _sizeInfoJson = TestSizeJson;
                                _runsInfoJson = TestFileJson;
                            } 
                            else
                            {
                                // Add File/DocumentSize column when it is linked up
                                var query = BuildQuery(FolderBrowser.GetActiveServer().URI.ToString(), path, @"TargetedMSRuns", @"Current",
                                    new[] { @"Name", @"Deleted", @"Container/Path", @"File/Proteins", @"File/Peptides", @"File/Precursors", @"File/Transitions", @"File/Replicates", @"Created", @"File/Versions", @"Replaced", @"ReplacedByRun", @"ReplacesRun", @"File/Id", @"RowId" }, string.Empty);
                                var sizeQuery = BuildQuery(FolderBrowser.GetActiveServer().URI.ToString(), path, "Runs", "Current",
                                    new[] { "DocumentSize", "Id", "FileName" }, string.Empty);
                                _sizeInfoJson = GetJson(sizeQuery);
                                _runsInfoJson = GetJson(query);
                            }

                            var versions = HasVersions(_runsInfoJson);
                            ModifyListViewCols(versions, ShowLatestVersion);
                            GetRunsDict();
                            AddSkyFiles(versions, ShowLatestVersion);
                        }
                    }
                    else
                    {
                        // For the WebDAV browser
                        ModifyListViewCols(false);
                        var uriString = string.Concat(FolderBrowser.GetActiveServer().URI.ToString(), PanoramaUtil.WEBDAV,
                            path + @"/?method=json");
                        var uri = new Uri(uriString);
                        AddAllFiles(uri);
                    }

                    ShowFiles(listView.Items.Count >= 1);
                }
            }
            catch (Exception ex)
            {
                if (ex is InvalidDataException
                    || ex is IOException
                    || ex is UnauthorizedAccessException)
                {
                    CommonAlertDlg.ShowException(FormUtil.FindTopLevelOwner(this), ex);
                }
            }
        }

        /// <summary>
        /// Displays either all versions of the files in a particular folder,
        /// or only the most recent versions
        /// </summary>
        private void VersionOptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_restoring)
            {
                listView.Items.Clear();
                AddSkyFiles(true, ShowLatestVersion);
            }
        }

        /// <summary>
        /// When a user clicks 'Open', information is stored about the selected file
        /// if there is one, otherwise the user is prompted to select a file
        /// </summary>
        private void Open_Click(object sender, EventArgs e)
        {
            ClickOpen();
        }

        public void ClickOpen()
        {
            if (listView.SelectedItems.Count != 0 && listView.SelectedItems[0] != null)
            {
                var downloadName = listView.SelectedItems[0].Name;
                if (listView.SelectedItems[0].SubItems[1] != null)
                {
                    FileSize = (long)listView.SelectedItems[0].Tag;
                }


                var folderPath = FolderBrowser.GetSelectedFolderPath();
                if (folderPath != null)
                {
                    if (!_showWebDav)
                    {
                        downloadName =
                            string.Concat(PanoramaUtil.WEBDAV, folderPath, PanoramaUtil.FILES_W_SLASH, @"/",
                                listView.SelectedItems[0].Name);
                    }

                    SelectedPath = folderPath;
                }

                FileUrl = FolderBrowser.GetActiveServer().URI + downloadName;
                DialogResult = DialogResult.Yes;
            }
            else
            {
                using var alert = new CommonAlertDlg(Resources.PanoramaFilePicker_Open_Click_You_must_select_a_file_first_, MessageBoxButtons.OK);
                alert.ShowDialog(FormUtil.FindTopLevelOwner(this));
            }
        }

        private void PanoramaFilePicker_FormClosing(object sender, FormClosingEventArgs e) 
        {
            FileName = listView.SelectedItems.Count != 0 ? listView.SelectedItems[0].Text : string.Empty;
            _treeState = FolderBrowser.GetClosingTreeState();
        }

        /// <summary>
        /// Given a boolean, display columns 2 and 3 if there are multiple
        /// versions of a file, otherwise hide columns 2 and 3 if there
        /// are not multiple versions of a file
        /// The optional second parameter is used in cases where we are viewing
        /// a folder with multiple versions, but we only want the latest versions
        /// </summary>
        private void ModifyListViewCols(bool versions, bool latestVersion = false)
        {
            versionLabel.Visible = versions;
            versionOptions.Visible = versions;
            // We extend the date column if there are no versions or if we are viewing the latest versions only
            // If there are no versions, or we are showing latest versions, we don't need the Replaced By column
            if (!versions || latestVersion)
            {
                listView.Columns[3].Width = 0;
                listView.Columns[4].Width = -2;
                listView.Columns[2].Width = !versions ? 0 : 60;
            }
            else 
            {
                listView.Columns[3].Width = -2;
                listView.Columns[2].Width = -2;
            }
        }

        /// <summary>
        /// Given a boolean value representing if there
        /// are files in a particular folder, display
        /// a message saying there are no files if the boolean
        /// is false, otherwise do not display this message if the
        /// boolean is true
        /// </summary>
        private void ShowFiles(bool files)
        {
            noFiles.Text = _showWebDav
                ? Resources.PanoramaFilePicker_ShowFiles_There_are_no_files_in_this_folder
                : Resources.PanoramaFilePicker_ShowFiles_There_are_no_Skyline_files_in_this_folder;
            if (files)
            {
                listView.HeaderStyle = ColumnHeaderStyle.Clickable;
                noFiles.Visible = false;
            }
            else
            {
                listView.HeaderStyle = ColumnHeaderStyle.None;
                noFiles.Visible = true;
            }
        }

        /// <summary>
        /// Navigates to the parent folder of the currently selected folder
        /// and displays its files 
        /// </summary>
        private void UpButton_Click(object sender, EventArgs e)
        {
            ClickUp();
        }

        public void ClickUp()
        {
            FolderBrowser.UpButtonClick();
            UpdateButtonState();
            forward.Enabled = false;
        }

        /// <summary>
        /// Navigates to the previous folder a user was looking at
        /// and displays its files
        /// </summary>
        private void Back_Click(object sender, EventArgs e)
        {
            ClickBack();
        }

        public void ClickBack()
        {
            back.Enabled = FolderBrowser.BackEnabled;
            FolderBrowser.BackButtonClick();
            UpdateButtonState();
        }

        /// <summary>
        /// Navigates to the next folder a user was looking at
        /// and displays its files
        /// </summary>
        private void Forward_Click(object sender, EventArgs e)
        {
            ClickForward();
        }

        public void ClickForward()
        {
            forward.Enabled = FolderBrowser.ForwardEnabled;
            FolderBrowser.ForwardButtonClick();
            UpdateButtonState();
        }

        private void FilePicker_MouseClick(object sender, EventArgs e)
        {
            up.Enabled = FolderBrowser.UpEnabled;
            forward.Enabled = false;
            back.Enabled = FolderBrowser.BackEnabled;
        }

        private void UpdateButtonState()
        {
            up.Enabled = FolderBrowser.UpEnabled;
            forward.Enabled = FolderBrowser.ForwardEnabled;
            back.Enabled = FolderBrowser.BackEnabled;
            urlLink.Text = FolderBrowser.GetSelectedUri();
        }

        /// <summary>
        /// Adapted from https://learn.microsoft.com/en-us/previous-versions/dotnet/articles/ms996467(v=msdn.10)?redirectedfrom=MSDN
        /// </summary>
        private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine whether the column is the same as the last column clicked.
            if (e.Column != _sortColumn)
            {
                // Set the sort column to the new column.
                _sortColumn = e.Column;
                // Set the sort order to ascending by default.
                listView.Sorting = SortOrder.Ascending;
            }
            else
            {
                // Determine what the last sort order was and change it.
                if (listView.Sorting == SortOrder.Ascending)
                    listView.Sorting = SortOrder.Descending;
                else
                    listView.Sorting = SortOrder.Ascending;
            }

            // Call the sort method to manually sort.
            listView.Sort();
            // Set the ListViewItemSorter property to a new ListViewItemComparer
            // object.
            listView.ListViewItemSorter = new ListViewItemComparer(e.Column,
                listView.Sorting);
        }

        private void ListView_DoubleClick(object sender, EventArgs e)
        {
            ClickOpen();
        }

        private void PanoramaFilePicker_SizeChanged(object sender, EventArgs e)
        {
            ResizeLabel(e);
        }

        private void ListView_SizeChanged(object sender, EventArgs e)
        {
            ResizeLabel(e);
        }

        private void ResizeLabel(EventArgs e)
        {
            noFiles.Location = new Point((listView.Location.Y + listView.Width - noFiles.Width) / 2,
                noFiles.Location.Y);
        }

        private void UrlLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Right:
                    contextMenuStrip.Show();
                    break;
                case MouseButtons.Left:
                    Process.Start(urlLink.Text);
                    break;
            }
        }

        private void CopyLinkAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(urlLink.Text);
        }

        /// <summary>
        /// Adapted from https://learn.microsoft.com/en-us/previous-versions/dotnet/articles/ms996467(v=msdn.10)?redirectedfrom=MSDN
        /// </summary>
        class ListViewItemComparer : IComparer
        {
            private int col;
            private SortOrder order;
            public ListViewItemComparer(int column, SortOrder order)
            {
                col = column;
                this.order = order;
            }
            public int Compare(object x, object y)
            {
                int returnVal;
                if (col == 1)
                {
                    returnVal = Comparer<long?>.Default.Compare(((ListViewItem)x)?.Tag as long?,
                        ((ListViewItem)y)?.Tag as long?);
                }
                else
                {
                    returnVal = String.CompareOrdinal(((ListViewItem)x)?.SubItems[col].Text,
                        ((ListViewItem)y)?.SubItems[col].Text);
                }
                
                // Determine whether the sort order is descending.
                if (order == SortOrder.Descending)
                    // Invert the value returned by String.Compare.
                    returnVal *= -1;
                return returnVal;
            }
        }

        #region Test Support

        public PanoramaFilePicker(Uri serverUri, string user, string pass,
            JToken folderJson, JToken fileJson, JToken sizeJson)
        {
            InitializeComponent();

            _restoring = true;
            versionOptions.Text = RECENT_VER;
            _restoring = false;

            TestFileJson = fileJson;
            TestSizeJson = sizeJson;

            var server = new PanoramaServer(serverUri, user, pass);
            _servers = new List<PanoramaServer> { server };

            FolderBrowser = new TestPanoramaFolderBrowser(server, folderJson);

            InitializeDialog();
        }

        public bool UpEnabled => up.Enabled;

        public bool BackEnabled => back.Enabled;

        public bool ForwardEnabled => FolderBrowser.ForwardEnabled;

        public bool VersionsVisible => versionOptions.Visible;

        public string GetItemValue(int index)
        {
            return listView.SelectedItems[0].SubItems[index].Text;
        }

        public string GetItemName(int index)
        {
            return listView.SelectedItems[0].SubItems[index].Name;
        }

        public void ClickVersions()
        {
            versionOptions.Text = versionOptions.Text.Equals(RECENT_VER) ? ALL_VER : RECENT_VER;
        }

        public bool ColumnVisible(int index)
        {
            return listView.Columns[index].Width > 0;
        }

        public int FileNumber => listView.Items.Count;

        public string VersionsOption => versionOptions.Text;

        public bool ClickFile(string name)
        {
            listView.SelectedItems.Clear();
            foreach (ListViewItem item in listView.Items)
            {
                var itemName = item.Text;
                if (name.Equals(itemName))
                {
                    item.Selected = true;
                    listView.Select();
                    ClickOpen();
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
