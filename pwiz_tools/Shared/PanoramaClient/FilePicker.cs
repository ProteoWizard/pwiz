using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;


namespace pwiz.PanoramaClient
{
    public partial class FilePicker : Form
    {
        private static string _peptideInfoQuery;
        private bool _restoring;
        private List<string> _mostRecent = new List<string>();
        private JToken _fileJson;
        private const string EXT = ".sky";
        private const string RECENT_VER = "Most recent";
        private const string ALL_VER = "All";

        public FilePicker(List<PanoramaServer> servers, bool showCheckbox, string stateString, bool showingSky)
        {
            Servers = servers;
            IsLoaded = false;
            InitializeComponent();
            TreeState = stateString;
            _restoring = true;
            ShowingSky = showingSky;
            showSkyCheckBox.Checked = ShowingSky;
            versionOptions.Text = ALL_VER;
            _restoring = false;
            showSkyCheckBox.Visible = showCheckbox;
        }

        /// <summary>
        /// Used for testing purposes
        /// </summary>
        public FilePicker()
        {
            InitializeComponent();
            _restoring = true;
            IsLoaded = false;
            versionOptions.Text = ALL_VER;
            _restoring = false;
            //InitializeTestDialog(serverUri, user, pass, folderJson);
        }

        public string OkButtonText { get; set; }
        public string TreeState { get; set; }
        public bool IsLoaded { get; set; }
        public FolderBrowser FolderBrowser { get; private set; }
        public List<TreeView> Tree = new List<TreeView>();
        public List<PanoramaServer> Servers { get; private set; }
        public string FileUrl { get; private set; }
        public string FileName { get; private set; }
        public string Folder { get; private set; }
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
        }

        /// <summary>
        /// Loads FolderBrowser and sets the state of navigation arrows
        /// </summary>
        public void InitializeDialog()
        {
            FolderBrowser = new FolderBrowser(false, ShowingSky, TreeState, Servers);
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
        /// Used for testing purposes
        /// </summary>
        /// <param name="serverUri"></param>
        /// <param name="user"></param>
        /// <param name="pass"></param>
        /// <param name="folderJson"></param>
        /// <param name="fileJson"></param>
        public void InitializeTestDialog(Uri serverUri, string user, string pass, JToken folderJson, JToken fileJson)
        {
            _fileJson = fileJson;
            var server = new PanoramaServer(serverUri, user, pass);
            FolderBrowser = new FolderBrowser(server, folderJson);
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
        private static string BuildQuery(string server, string folderPath, string queryName, string folderFilter, string[] columns, string sortParam, string equalityParam)
        {
            var query =
                $@"{server}{folderPath}/query-selectRows.view?schemaName=targetedms&query.queryName={queryName}&query.containerFilterName={folderFilter}";
            if (columns != null)
            {
                query = $@"{query}&query.columns=";
                var allCols = columns.Aggregate(string.Empty, (current, col) => $@"{col},{current}");

                query = $@"{query}{allCols}";
            }

            if (!string.IsNullOrEmpty(sortParam))
            {
                query = $@"{query}&query.sort={sortParam}";
            }

            if (!string.IsNullOrEmpty(equalityParam))
            {
                query = $@"{query}&query.{equalityParam}~eq=";
            }
            return query;
        }

        /// <summary>
        /// Takes in a query string and returns the associated JSON
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private JToken GetJson(string query)
        {
            var queryUri = new Uri(query);
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
            var json = GetJson(newUri.ToString());
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
        }

        /// <summary>
        /// Displays the latest versions of all files in a particular folder
        /// </summary>
        /// <param name="path"></param>
        private void GetLatestVersion(string path)
        {
            listView.Items.Clear();
            var result = new string[5];
            var fileInfos = new string[5];
            _peptideInfoQuery = BuildQuery(ActiveServer.URI.ToString(), path, @"TargetedMSRuns", @"Current",
                new[] { @"Name", @"Deleted", @"Container/Path", @"File/Proteins", @"File/Peptides", @"File/Precursors", @"File/Transitions", @"File/Replicates", @"Created", @"File/Versions", @"Replaced", @"ReplacedByRun", @"ReplacesRun", @"File/Id", @"RowId" }, string.Empty, string.Empty); 
            var query = _peptideInfoQuery;
            var json = GetJson(query);
            var sizeQuery = BuildQuery(ActiveServer.URI.ToString(),path, "Runs", "Current",
                new[] { "DocumentSize", "Id" }, string.Empty, string.Empty);
            var sizeJson = GetJson(sizeQuery);
            var rowSize = sizeJson[@"rows"];
            var rows = json[@"rows"];
            foreach (var row in rows)
            {
                var versions = row[@"File/Versions"].ToString();
                var rowId = row[@"RowId"].ToString();

                if (_mostRecent.Contains(rowId) || versions.Equals(1.ToString()))
                {
                    if (_mostRecent.Contains(rowId))
                    {
                        _mostRecent.Remove(rowId);
                    }
                    long size = 0;
                    try
                    {
                        var id = (long)row[@"File/Id"];
                        foreach (var curRow in rowSize)
                        {
                            var curId = (long)curRow[@"Id"];
                            if (curId == id)
                            {
                                size = (long)curRow[@"DocumentSize"];
                            }
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
                    result[4] = (string)row[@"Created"];
                    var fileNode = new ListViewItem(result, 1)
                    {
                        ToolTipText = $"Proteins: {fileInfos[0]}, Peptides: {fileInfos[1]}, Precursors: {fileInfos[2]}, Transitions: {fileInfos[3]}, Replicates: {fileInfos[4]}",
                        Name = (string)row[@"_labkeyurl_FileName"],
                        Tag = size
                    };
                    listView.Items.Add(fileNode);
                }
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
                var name = (string)row[@"Name"];
                var rowReplaced = (string)row[@"ReplacedByRun"];
                var replaces = (string)row[@"ReplacesRun"];
                if (!string.IsNullOrEmpty(replaces) && string.IsNullOrEmpty(rowReplaced))
                {
                    _mostRecent.Add(rowId);
                }
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
            //Use this one query once Vagisha can link up the file size column 
            //https://panoramaweb-dr.gs.washington.edu/00Developer/Sophie/Versions/query-selectRows.api?schemaName=targetedms&query.queryName=TargetedMSRuns&query.columns=File%2FId%2CRowId%2CCreated%2CFile%2FProteins%2CFile%2FPeptides%2CFile%2FPrecursors%2CFile%2FTransitions%2CFile%2FReplicates%2CReplacedByRun%2CReplacesRun,File%2FVersions,Container%2FPath,Name
            _peptideInfoQuery = BuildQuery(ActiveServer.URI.ToString(), nodePath, @"TargetedMSRuns", @"Current",
                new[] { @"Name", @"Deleted", @"Container/Path", @"File/Proteins", @"File/Peptides", @"File/Precursors", @"File/Transitions", @"File/Replicates", @"Created", @"File/Versions", @"Replaced", @"ReplacedByRun" , @"ReplacesRun", @"File/Id", @"RowId" }, string.Empty, string.Empty);
            var sizeQuery = BuildQuery(ActiveServer.URI.ToString(), nodePath, "Runs", "Current",
                new[] { "DocumentSize", "Id" }, string.Empty, string.Empty);
            var sizeJson = GetJson(sizeQuery);
            var rowSize = sizeJson[@"rows"];
            var query = _peptideInfoQuery;
            var json = GetJson(query);
            var rows = json[@"rows"];
            var rowCount = json[@"rowCount"];
            if ((int)rowCount > 0)
            {
                var versions = HasVersions(json);
                foreach (var row in rows)
                {
                    var fileName = (string)row[@"Name"];
                    var filePath = (string)row[@"Container/Path"];
                    if (filePath.Equals(nodePath))
                    {
                        var listItem = new string[5];
                        var numVersions = new string[2];
                        var replacedBy = row[@"ReplacedByRun"].ToString();
                        if (versions)
                        {
                            listView.Columns[3].Width = 100;
                            listView.Columns[2].Width = 60;
                            l.Visible = true;
                            options.Visible = true;
                            numVersions = GetVersionInfo(json, replacedBy);
                        }
                        else
                        {
                            listView.Columns[3].Width = 0;
                            listView.Columns[2].Width = 0;
                            l.Visible = false;
                            options.Visible = false;
                        }
                        listItem[0] = fileName;
                        long size = 0;
                        try
                        {
                            var id = (long)row[@"File/Id"];
                            foreach (var curRow in rowSize)
                            {
                                var curId = (long)curRow[@"Id"];
                                if (curId == id)
                                {
                                    size = (long)curRow[@"DocumentSize"];
                                }
                            }

                            if (size > 0)
                            {
                                var sizeObj = new FileSize(size);
                                listItem[1] = sizeObj.ToString();
                            }
                        }
                        catch (Exception e)
                        {
                            throw new Exception(e.Message);
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
                            listItem[3] = numVersions[1].ToString();
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
                //Show a message saying there are no Skyline files in this folder
            }
        }

        /// <summary>
        /// Used for testing 
        /// </summary>
        /// <param name="nodePath"></param>
        /// <param name="l"></param>
        /// <param name="options"></param>
        /// <param name="json"></param>
        public void TestAddQueryFiles(string nodePath, Control l, Control options, JToken json)
        {
            var rows = json[@"rows"];
            var rowCount = json[@"rowCount"];
            if ((int)rowCount > 0)
            {
                var versions = HasVersions(json);
                foreach (var row in rows)
                {
                    var fileName = (string)row[@"Name"];
                    var filePath = (string)row[@"Container/Path"];
                    if (filePath.Equals(nodePath))
                    {
                        var listItem = new string[5];
                        var numVersions = new string[2];
                        var replacedBy = row[@"ReplacedByRun"].ToString();
                        if (versions)
                        {
                            listView.Columns[3].Width = 100;
                            listView.Columns[2].Width = 60;
                            l.Visible = true;
                            options.Visible = true;
                            numVersions = GetVersionInfo(json, replacedBy);
                        }
                        else
                        {
                            listView.Columns[3].Width = 0;
                            listView.Columns[2].Width = 0;
                            l.Visible = false;
                            options.Visible = false;
                        }
                        listItem[0] = fileName;


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
                            listItem[3] = numVersions[1].ToString();
                        }

                        listItem[4] = (string)row[@"Created"];
                        var fileNode = new ListViewItem(listItem, 1)
                        {
                            Name = (string)row[@"_labkeyurl_FileName"],
                            ToolTipText = $"Proteins: {row[@"File/Proteins"]}, Peptides: {row[@"File/Peptides"]}, Precursors: {row[@"File/Precursors"]}, Transitions: {row[@"File/Transitions"]}, Replicates: {row[@"File/Replicates"]}"
                        };
                        listView.Items.Add(fileNode);
                    }
                }
            }
            else
            {
                //Show a message saying there are no Skyline files in this folder
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
                Cursor = Cursors.WaitCursor;
                if (FolderBrowser.Testing)
                {
                    _restoring = true;
                    versionOptions.Visible = false;
                    versionLabel.Visible = false;
                    versionOptions.Text = ALL_VER;
                    var path = FolderBrowser.Path;
                    listView.Items.Clear();
                    ActiveServer = FolderBrowser.ActiveServer;
                    _restoring = false;
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (FolderBrowser.ShowSky)
                        {
                            TestAddQueryFiles(path, versionLabel, versionOptions, _fileJson);
                        }
                        else
                        {
                            var uriString = string.Concat(ActiveServer.URI.ToString(), @"_webdav/",
                                path + @"/@files?method=json");
                            var uri = new Uri(uriString);
                            AddChildFiles(uri);
                        }
                    }
                }
                else
                {
                    _restoring = true;
                    versionOptions.Visible = false;
                    versionLabel.Visible = false;
                    versionOptions.Text = ALL_VER;
                    var path = FolderBrowser.Path;
                    listView.Items.Clear();
                    ActiveServer = FolderBrowser.ActiveServer;
                    _restoring = false;
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (FolderBrowser.ShowSky)
                        {
                            AddQueryFiles(path, versionLabel, versionOptions);
                        }
                        else
                        {
                            var uriString = string.Concat(ActiveServer.URI.ToString(), @"_webdav/",
                                path + @"/@files?method=json");
                            var uri = new Uri(uriString);
                            AddChildFiles(uri);
                        }
                    }
                }
            }
            catch (SystemException)
            {
                //Ignored
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            Cursor = Cursors.Default;
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
                Cursor = Cursors.WaitCursor;
                listView.Items.Clear();
                if (versionOptions.Text.Equals(RECENT_VER))
                {
                    GetLatestVersion((string)FolderBrowser.Clicked.Tag);
                }
                else
                {
                    ActiveServer = FolderBrowser.ActiveServer;
                    AddQueryFiles((string)FolderBrowser.Clicked.Tag, versionLabel, versionOptions);
                }

                Cursor = Cursors.Default;
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
                FileUrl = ActiveServer.URI.ToString() + downloadName;

                DialogResult = DialogResult.Yes;
                Close();
            }
            else
            {
                MessageBox.Show(@"You must select a file first!");
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

        public string VersionsOption()
        {
            return versionOptions.Text;
        }

        public bool CheckBoxVisible()
        {
            return showSkyCheckBox.Visible;
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
                precision = @"2";
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
