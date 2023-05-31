using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;


namespace pwiz.PanoramaClient
{
    public partial class PanoramaFilePicker : CommonFormEx
    {
        private static JToken _runsInfoJson;
        private static JToken _sizeInfoJson;
        private static Dictionary<long, long> _sizeDictionary = new Dictionary<long, long>();
        private bool _restoring;
        private JToken _fileJson;
        private JToken _sizeJson;
        private int _sortColumn = -1;
        private const string EXT = ".sky";
        private const string RECENT_VER = "Most recent";
        private const string ALL_VER = "All";

        public PanoramaFilePicker(List<PanoramaServer> servers, bool showCheckbox, string stateString, bool showingSky)
        {
            InitializeComponent();
            Servers = servers;
            IsLoaded = false;
            TreeState = stateString;
            _restoring = true;
            ShowingSky = showingSky;
            showSkyCheckBox.Checked = ShowingSky;
            versionOptions.Text = RECENT_VER;
            showSkyCheckBox.Visible = showCheckbox;
            noFiles.Visible = false;
            _restoring = false;
        }

        /// <summary>
        /// Used for testing purposes
        /// </summary>
        public PanoramaFilePicker()
        {
            InitializeComponent();
            _restoring = true;
            IsLoaded = false;
            versionOptions.Text = RECENT_VER;
            ShowingSky = true;
            showSkyCheckBox.Checked = ShowingSky;
            _restoring = false;
        }

        public string OkButtonText { get; set; }
        public string TreeState { get; set; }
        public bool IsLoaded { get; set; }
        public PanoramaFolderBrowser FolderBrowser { get; private set; }
        public List<PanoramaServer> Servers { get; }
        public string FileUrl { get; private set; }
        public string FileName { get; private set; }
        public string DownloadName { get; private set; }
        public bool ShowingSky { get; private set; }
        public PanoramaServer ActiveServer { get; private set; }
        public bool FormHasClosed { get; private set; }
        public long FileSize { get; private set; }

        /// <summary>
        /// Sets a username and password and changes the 'Open' button text if a custom string is passed in
        /// </summary>
        private void FilePicker_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(OkButtonText))
            {
                open.Text = OkButtonText;
            }
            FolderBrowser.AddFiles += AddFiles;
            FolderBrowser.Dock = DockStyle.Fill;
            splitContainer1.Panel1.Controls.Add(FolderBrowser);
            FolderBrowser.NodeClick += FilePicker_MouseClick;
            IsLoaded = true;
            CheckEnabled();
        }

        /// <summary>
        /// Loads FolderBrowser and sets the state of navigation arrows
        /// </summary>
        public void InitializeDialog()
        {
            FolderBrowser = new PanoramaFolderBrowser(false, ShowingSky, TreeState, Servers);
            if (string.IsNullOrEmpty(TreeState))
            {
                up.Enabled = false;
                back.Enabled = false;
                forward.Enabled = false;
            }
            //IsLoaded = true;
        }

        /// <summary>
        /// Used for testing purposes
        /// </summary>
        /// <param name="serverUri"></param>
        /// <param name="user"></param>
        /// <param name="pass"></param>
        /// <param name="folderJson"></param>
        /// <param name="fileJson"></param>
        /// <param name="sizeJson"></param>
        public void InitializeTestDialog(Uri serverUri, string user, string pass, JToken folderJson, JToken fileJson, JToken sizeJson)
        {
            _fileJson = fileJson;
            _sizeJson = sizeJson;
            var server = new PanoramaServer(serverUri, user, pass);
            FolderBrowser = new PanoramaFolderBrowser(server, folderJson);
            FolderBrowser.Dock = DockStyle.Fill;
            splitContainer1.Panel1.Controls.Add(FolderBrowser);
            FolderBrowser.NodeClick += FilePicker_MouseClick;
            if (string.IsNullOrEmpty(TreeState))
            {
                up.Enabled = false;
                back.Enabled = false;
                forward.Enabled = false;
            }
            else
            {
                up.Enabled = FolderBrowser.UpEnabled();
                back.Enabled = FolderBrowser.BackEnabled();
                forward.Enabled = FolderBrowser.ForwardEnabled();
            }
            IsLoaded = true;
        }


        /// <summary>
        /// Builds a string that will be used as a URI to find all .sky folders
        /// </summary>
        /// <returns></returns>
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
        /// <param name="queryUri"></param>
        /// <returns></returns>
        private JToken GetJson(Uri queryUri)
        {
            var webClient = new WebClientWithCredentials(queryUri, ActiveServer.Username, ActiveServer.Password);
            JToken json = webClient.Get(queryUri);
            return json;
        }


        /// <summary>
        /// Adds all files in a particular folder 
        /// </summary>
        /// <param name="newUri"></param>
        public void AddChildFiles(Uri newUri)
        {
            var json = GetJson(newUri);
            if ((int)json[@"fileCount"] != 0)
            {
                var files = json[@"files"];
                foreach (dynamic file in files)
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
                        var size = (long)file[@"size"];
                        var sizeObj = new FileSize(size);
                        listItem[1] = sizeObj.ToString();
                        listItem[4] = (string)file[@"creationdate"];
                        var fileNode = fileName.EndsWith(EXT) ? new ListViewItem(listItem, 1) : new ListViewItem(listItem, 0);
                        fileNode.Tag = (string)file[@"id"];
                        fileNode.Name = (string)file[@"href"];
                        listView.Items.Add(fileNode);
                    }
                }
            }
            else
            {
                listView.HeaderStyle = ColumnHeaderStyle.None;
                noFiles.Visible = true;
            }
        }

        private Dictionary<long, long> GetSizeDict()
        {
            _sizeDictionary.Clear();
            var rowSize = _sizeInfoJson[@"rows"];
            foreach (var curRow in rowSize)
            {
                var curId = (long)curRow[@"Id"];
                var size = curRow[@"DocumentSize"];
                if (size.Type != JTokenType.Null)
                {
                    var lSize = size.ToObject<long>();
                    _sizeDictionary.Add(curId, lSize);
                }
            }
            return _sizeDictionary;
        }

        /// <summary>
        /// Gets the latest versions of all files in a folder and adds
        /// them to ListView
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void GetLatestVersion()
        {
            var rowCount = _runsInfoJson[@"rowCount"];
            if ((int)rowCount > 0)
            {
                listView.HeaderStyle = ColumnHeaderStyle.Clickable;
                noFiles.Visible = false;
                listView.Columns[3].Width = 0;
                listView.Items.Clear();
                var result = new string[5];
                var fileInfos = new string[5];
                var rows = _runsInfoJson[@"rows"];
                var sizeDict = GetSizeDict();

                foreach (var row in rows)
                {
                    var versions = row[@"File/Versions"].ToString();
                    var rowReplaced = (string)row[@"ReplacedByRun"];
                    var replaces = (string)row[@"ReplacesRun"];
                    if ((!string.IsNullOrEmpty(replaces) && string.IsNullOrEmpty(rowReplaced)) ||
                        versions.Equals(1.ToString()))
                    {
                        long size = 0;
                        try
                        {
                            var id = (long)row[@"File/Id"];
                            if (sizeDict.TryGetValue(id, out var value))
                            {
                                size = value;
                            }

                            if (size > 0)
                            {
                                var sizeObj = new FileSize(size);
                                result[1] = sizeObj.ToString();
                            }
                        }
                        catch (Exception e)
                        {
                            throw new Exception(e.Message);
                        }

                        result[2] = versions;
                        var name = row[@"Name"].ToString();
                        result[0] = name;
                        fileInfos[0] = (string)row[@"File/Proteins"];
                        fileInfos[1] = (string)row[@"File/Peptides"];
                        fileInfos[2] = (string)row[@"File/Precursors"];
                        fileInfos[3] = (string)row[@"File/Transitions"];
                        fileInfos[4] = (string)row[@"File/Replicates"];
                        var date = (string)row[@"Created"];
                        result[4] = DateTime.Parse(date, CultureInfo.InvariantCulture).ToString();
                        var fileNode = new ListViewItem(result, 1)
                        {
                            ToolTipText =
                                $"Proteins: {fileInfos[0]}, Peptides: {fileInfos[1]}, Precursors: {fileInfos[2]}, Transitions: {fileInfos[3]}, Replicates: {fileInfos[4]}",
                            Name = (string)row[@"_labkeyurl_FileName"],
                            Tag = size
                        };
                        listView.Items.Add(fileNode);
                    }
                }
                listView.Columns[4].Width = -2;
            }
            else
            {
                listView.HeaderStyle = ColumnHeaderStyle.None;
                noFiles.Visible = true;
            }
        }

        /// <summary>
        /// Returns true if a file has multiple versions, and false if not
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        private bool HasVersions(JToken json)
        {
            var rows = json[@"rows"];
            foreach (var row in rows)
            {
                var replaced = row[@"Replaced"].ToString();
                if (replaced.Equals("True"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Find number of rows which gives version, and latest version is the only version where replaced is false
        /// </summary>
        /// <param name="json"></param>
        /// <param name="replacedBy"></param>
        /// <returns></returns>
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
        /// Given a folder path, try and add all .sky files inside that folder to this ListView
        /// </summary>
        /// <param name="nodePath"></param>
        /// <param name="l"></param>
        /// <param name="options"></param>
        private void AddQueryFiles(string nodePath, Control l, Control options)
        {
            var rows = _runsInfoJson[@"rows"];
            var rowCount = _runsInfoJson[@"rowCount"];
            if ((int)rowCount > 0)
            {
                listView.HeaderStyle = ColumnHeaderStyle.Clickable;
                noFiles.Visible = false;
                foreach (var row in rows)
                {
                    var fileName = (string)row[@"Name"];
                    var filePath = (string)row[@"Container/Path"];
                    if (filePath.Equals(nodePath))
                    {
                        var listItem = new string[5];
                        var replacedBy = row[@"ReplacedByRun"].ToString();

                        listView.Columns[3].Width = 100;
                        listView.Columns[2].Width = 60;
                        l.Visible = true;
                        options.Visible = true;
                        var numVersions = GetVersionInfo(_runsInfoJson, replacedBy);
                        listItem[0] = fileName;
                        long size = 0;
                        var id = (long)row[@"File/Id"];
                        if (_sizeDictionary.TryGetValue(id, out var value))
                        {
                            size = value;
                        }
                        if (size > 0)
                        {
                            var sizeObj = new FileSize(size);
                            listItem[1] = sizeObj.ToString();
                        }

                        if (numVersions[0] != null)
                        {
                            listItem[2] = row[@"File/Versions"].ToString(); 
                        }
                        else
                        {
                            listItem[2] = 1.ToString();
                        }

                        if (numVersions[1] != null)
                        {
                            listItem[3] = numVersions[1];
                        }

                        listItem[4] = (string)row[@"Created"];
                        var fileNode = new ListViewItem(listItem, 1)
                        {
                            Name = (string)row[@"_labkeyurl_FileName"],
                            ToolTipText = $"Proteins: {row[@"File/Proteins"]}, Peptides: {row[@"File/Peptides"]}, Precursors: {row[@"File/Precursors"]}, Transitions: {row[@"File/Transitions"]}, Replicates: {row[@"File/Replicates"]}", Tag = size
                        };
                        listView.Items.Add(fileNode);
                    }
                }
            }
            else
            {
                listView.HeaderStyle = ColumnHeaderStyle.None;
                noFiles.Visible = true;
            }
        }

        /// <summary>
        /// Check if a given node has any skyline files on the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="Exception"></exception>
        public void AddFiles(object sender, EventArgs e)
        {
            try
            {
                if (FolderBrowser.Testing)
                {
                    _restoring = true;
                    versionOptions.Visible = false;
                    versionLabel.Visible = false;
                    versionOptions.Text = RECENT_VER;
                    var path = FolderBrowser.Path;
                    listView.Items.Clear();
                    ActiveServer = FolderBrowser.ActiveServer;
                    _restoring = false;
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (FolderBrowser.ShowSky)
                        {
                            if (FolderBrowser.CurNodeIsTargetedMS.Equals(@"True"))
                            {
                                _runsInfoJson = _fileJson;
                                _sizeInfoJson = _sizeJson;
                                var versions = HasVersions(_runsInfoJson);
                                if (versions)
                                {

                                    listView.Columns[3].Width = 100;
                                    listView.Columns[2].Width = 60;
                                    versionLabel.Visible = true;
                                    versionOptions.Visible = true;
                                }
                                else
                                {
                                    listView.Columns[3].Width = 0;
                                    listView.Columns[2].Width = 0;
                                    versionLabel.Visible = false;
                                    versionOptions.Visible = false;
                                }
                                GetLatestVersion();
                            }
                        }
                    }
                }
                else
                {
                    _restoring = true;
                    versionOptions.Visible = false;
                    versionLabel.Visible = false;
                    versionOptions.Text = RECENT_VER;
                    var path = FolderBrowser.Path;
                    listView.Items.Clear();
                    listView.HeaderStyle = ColumnHeaderStyle.Clickable;
                    noFiles.Visible = false;
                    ActiveServer = FolderBrowser.ActiveServer;
                    _restoring = false;
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (FolderBrowser.CurNodeIsTargetedMS.Equals(@"True"))
                        {
                            
                            if (FolderBrowser.ShowSky)
                            {
                                //Add File/DocumentSize column when it is linked up
                                var query = BuildQuery(ActiveServer.URI.ToString(), path, @"TargetedMSRuns", @"Current",
                                    new[] { @"Name", @"Deleted", @"Container/Path", @"File/Proteins", @"File/Peptides", @"File/Precursors", @"File/Transitions", @"File/Replicates", @"Created", @"File/Versions", @"Replaced", @"ReplacedByRun", @"ReplacesRun", @"File/Id", @"RowId" }, string.Empty);
                                var sizeQuery = BuildQuery(ActiveServer.URI.ToString(), path, "Runs", "Current",
                                    new[] { "DocumentSize", "Id" }, string.Empty);
                                _sizeInfoJson = GetJson(sizeQuery);
                                _runsInfoJson = GetJson(query);
                                var versions = HasVersions(_runsInfoJson);
                                if (versions)
                                {

                                    listView.Columns[3].Width = 100;
                                    listView.Columns[2].Width = 60;
                                    versionLabel.Visible = true;
                                    versionOptions.Visible = true;
                                }
                                else
                                {
                                    listView.Columns[3].Width = 0;
                                    listView.Columns[2].Width = 0;
                                    versionLabel.Visible = false;
                                    versionOptions.Visible = false;
                                }
                                GetLatestVersion();
                            }
                            else
                            {
                                var uriString = string.Concat(ActiveServer.URI.ToString(), @"_webdav/",
                                    path + @"/@files?method=json");
                                var uri = new Uri(uriString);
                                AddChildFiles(uri);
                            }
                        }
                        else
                        {
                            listView.HeaderStyle = ColumnHeaderStyle.None;
                            noFiles.Visible = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var alert = new AlertDlg(ex.Message, MessageBoxButtons.OK);
                alert.ShowDialog();
            }
        }

        /// <summary>
        /// Resets the TreeView to display either all Panorama folders, or only Panorama folders containing .sky files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowSkyCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!_restoring)
            {
                listView.Items.Clear();
                versionOptions.Visible = false;
                versionLabel.Visible = false;
                var type = showSkyCheckBox.Checked;
                FolderBrowser.SwitchFolderType(type);
                up.Enabled = false;
                back.Enabled = false;
                forward.Enabled = false;
            }
        }


        /// <summary>
        /// Displays either all versions of a Skyline file, or only the most recent version
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VersionOptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_restoring)
            {
                listView.Items.Clear();
                if (versionOptions.Text.Equals(RECENT_VER))
                {
                    GetLatestVersion();
                }
                else
                {
                    ActiveServer = FolderBrowser.ActiveServer;
                    AddQueryFiles((string)FolderBrowser.Clicked.Tag, versionLabel, versionOptions);
                }

            }
            
        }

        /// <summary>
        /// Used for testing purposes
        /// </summary>
        public void TestCancel()
        {
            Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// When a user clicks 'Open', information is stored about the selected file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Open_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count != 0 && listView.SelectedItems[0] != null)
            {
                var downloadName = listView.SelectedItems[0].Name;
                if (listView.SelectedItems[0].SubItems[1] != null)
                {
                    FileSize =(long) listView.SelectedItems[0].Tag;
                }
                if (FolderBrowser.ShowSky)
                {
                    downloadName =
                        string.Concat(@"/_webdav", FolderBrowser.Clicked.Tag, @"/@files/", listView.SelectedItems[0]
                            .Text); 
                }
                DownloadName = downloadName;
                FileUrl = ActiveServer.URI + downloadName;

                DialogResult = DialogResult.Yes;
                Close();
            }
            else
            {
                var alert = new AlertDlg(@"You must select a file first!", MessageBoxButtons.OK);
                alert.ShowDialog();
            }
        }



        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            FormHasClosed = true;
            FileName = listView.SelectedItems.Count != 0 ? listView.SelectedItems[0].Text : string.Empty;
            ShowingSky = showSkyCheckBox.Checked;
            TreeState = FolderBrowser.ClosingState();
        }


        /// <summary>
        /// Navigates to the parent folder of the currently selected folder
        /// and displays it's files 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpButton_Click(object sender, EventArgs e)
        {
            FolderBrowser.UpClick();
            CheckEnabled();
            forward.Enabled = false;
        }

        /// <summary>
        /// Navigates to the previous folder a user was looking at
        /// and displays it's files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Back_Click(object sender, EventArgs e)
        {
            back.Enabled = FolderBrowser.BackEnabled();
            FolderBrowser.BackClick();
            CheckEnabled();
        }

        /// <summary>
        /// Navigates to the next folder a user was looking at
        /// and displays it's files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Forward_Click(object sender, EventArgs e)
        {
            forward.Enabled = FolderBrowser.ForwardEnabled();
            FolderBrowser.ForwardClick();
            CheckEnabled();
        }

        public void ClickBack()
        {
            FolderBrowser.BackClick();
            CheckEnabled();
        }

        public void ClickForward()
        {
            FolderBrowser.ForwardClick();
            CheckEnabled();
        }

        public void ClickUp()
        {
            FolderBrowser.UpClick();
            CheckEnabled();
        }

        public void ClickOpen()
        {
            Open_Click(this, EventArgs.Empty);
        }


        public void FilePicker_MouseClick(object sender, EventArgs e)
        {
            up.Enabled = FolderBrowser.UpEnabled();
            forward.Enabled = false;
            back.Enabled = FolderBrowser.BackEnabled();
        }

        private void CheckEnabled()
        {
            up.Enabled = FolderBrowser.UpEnabled();
            forward.Enabled = FolderBrowser.ForwardEnabled();
            back.Enabled = FolderBrowser.BackEnabled();
        }

        public bool UpEnabled()
        {
            return up.Enabled;
        }

        public bool BackEnabled()
        {
            return back.Enabled;
        }

        public bool ForwardEnabled()
        {
            return FolderBrowser.ForwardEnabled();
        }

        public bool VersionsVisible()
        {
            return versionOptions.Visible;
        }

        public string GetItemValue(int index)
        {
            return listView.SelectedItems[0].SubItems[index].Text;
        }

        public void ClickVersions()
        {
            if (versionOptions.Text.Equals(RECENT_VER))
            {
                versionOptions.Text = ALL_VER;
            }
            else
            {
                versionOptions.Text = RECENT_VER;
            }
        }

        public bool ColumnVisible(int index)
        {
            return listView.Columns[index].Width > 0;
        }

        public int FileNumber()
        {
            return listView.Items.Count;
        }

        public string VersionsOption()
        {
            return versionOptions.Text;
        }

        public void ClickCheckBox()
        {
            showSkyCheckBox.Checked = !showSkyCheckBox.Checked;
            ShowingSky = showSkyCheckBox.Checked;
        }

        public bool CheckBoxVisible()
        {
            return showSkyCheckBox.Visible;
        }

        private void listView_ColumnClick(object sender, ColumnClickEventArgs e)
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
            this.listView.ListViewItemSorter = new ListViewItemComparer(e.Column,
                listView.Sorting);
        }

        public void ClickFile(string name)
        {
            foreach (ListViewItem item in listView.Items)
            {
                var itemName = item.Text;
                if (name.Equals(itemName))
                {
                    item.Selected = true;
                    listView.Select();
                    DialogResult = DialogResult.Yes;
                    open.PerformClick();
                }
            }
        }

        private void PanoramaFilePicker_SizeChanged(object sender, EventArgs e)
        {
            noFiles.Location = new Point((listView.Location.Y + listView.Width - noFiles.Width) / 2,
                noFiles.Location.Y);
        }


        private void listView_SizeChanged(object sender, EventArgs e)
        {
            noFiles.Location = new Point((listView.Location.Y + listView.Width - noFiles.Width) / 2,
                noFiles.Location.Y);
        }

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
                int returnVal = -1;
                returnVal = String.CompareOrdinal(((ListViewItem)x)?.SubItems[col].Text,
                    ((ListViewItem)y)?.SubItems[col].Text);
                // Determine whether the sort order is descending.
                if (order == SortOrder.Descending)
                    // Invert the value returned by String.Compare.
                    returnVal *= -1;
                return returnVal;
            }
        }
    }


    class FileSizeFormatProvider : IFormatProvider, ICustomFormatter
    {
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter)) return this;
            return null;
        }

        private const string FILE_SIZE_FORMAT = "fs";
        private const Decimal ONE_KILO_BYTE = 1024M;
        private const Decimal ONE_MEGA_BYTE = ONE_KILO_BYTE * 1024M;
        private const Decimal ONE_GIGA_BYTE = ONE_MEGA_BYTE * 1024M;

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (format == null || !format.StartsWith(FILE_SIZE_FORMAT))
            {
                return DefaultFormat(format, arg, formatProvider);
            }

            if (arg is string)
            {
                return DefaultFormat(format, arg, formatProvider);
            }

            Decimal size;

            try
            {
                size = Convert.ToDecimal(arg);
            }
            catch (InvalidCastException)
            {
                return DefaultFormat(format, arg, formatProvider);
            }

            string suffix;

            if (size > ONE_GIGA_BYTE)
            {
                size /= ONE_GIGA_BYTE;
                suffix = @" GB";
            }
            else if (size > ONE_MEGA_BYTE)
            {
                size /= ONE_MEGA_BYTE;
                suffix = @" MB";
            }
            else if (size > ONE_KILO_BYTE)
            {
                size /= ONE_KILO_BYTE;
                suffix = @" KB";
            }
            else
            {
                suffix = @" B";
            }

            string precision = format.Substring(2);
            if (String.IsNullOrEmpty(precision))
                precision = @"1";
            string formatString = @"{0:N" + precision + @"}{1}";  // Avoid ReSharper analysis
            return String.Format(formatString, size, suffix);
        }

        private static string DefaultFormat(string format, object arg, IFormatProvider formatProvider)
        {
            IFormattable formattableArg = arg as IFormattable;
            if (formattableArg != null)
            {
                return formattableArg.ToString(format, formatProvider);
            }
            return arg.ToString();
        }
    }

    struct FileSize : IComparable
    {
        private static readonly FileSizeFormatProvider FORMAT_PROVIDER = new FileSizeFormatProvider();
        public static FileSizeFormatProvider FormatProvider
        {
            get { return FORMAT_PROVIDER; }
        }
        public FileSize(long byteCount) : this()
        {
            ByteCount = byteCount;
        }

        public long ByteCount { get; private set; }
        public override string ToString()
        {
            return String.Format(FORMAT_PROVIDER, @"{0:fs}", ByteCount);
        }

        public int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            if (!(obj is FileSize))
            {
                throw new ArgumentException(@"Must be FileSize");
            }
            return ByteCount.CompareTo(((FileSize)obj).ByteCount);
        }
    }


}
