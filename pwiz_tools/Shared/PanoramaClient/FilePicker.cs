using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;


namespace pwiz.PanoramaClient
{
    public partial class FilePicker : Form
    {
        private static string _peptideInfoQuery;
        private bool _restoring;
        private List<string> _mostRecent = new List<string>();
        private const string EXT = ".sky";
        private const string RECENT_VER = "Most recent";
        private const string ALL_VER = "All";
        private JToken FileJson;

        public FilePicker(List<PanoramaServer> servers, bool showCheckbox, string stateString, bool showingSky)
        {
            _servers = servers;
            IsLoaded = false;
            InitializeComponent();
            TreeState = stateString;
            _restoring = true;
            _showingSky = showingSky;
            showSkyCheckBox.Checked = _showingSky;
            versionOptions.Text = ALL_VER;
            _restoring = false;
            showSkyCheckBox.Visible = showCheckbox;
        }

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
        public FolderBrowser FolderBrowser;
        public TreeNodeCollection _nodesState;
        public List<TreeView> _tree = new List<TreeView>();
        public List<PanoramaServer> _servers;
        public string _fileUrl;
        public string _fileName;
        public string _folder;
        public string _downloadName;
        public bool _showingSky;
        public PanoramaServer _activeServer;
        public bool FormClosed { get; private set; }
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
        }

        public void InitializeDialog()
        {
            FolderBrowser = new FolderBrowser(false, _showingSky, TreeState, _servers);
            FolderBrowser.AddFiles += AddFiles;
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

        public void InitializeTestDialog(Uri serverUri, string user, string pass, JToken folderJson, JToken fileJson)
        {
            FileJson = fileJson;
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
            var webClient = new WebClientWithCredentials(queryUri, _activeServer.Username, _activeServer.Password);
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
            _peptideInfoQuery = BuildQuery(_activeServer.URI.ToString(), path, @"TargetedMSRuns", @"Current",
                new[] { @"Name", @"Deleted", @"Container/Path", @"File/Proteins", @"File/Peptides", @"File/Precursors", @"File/Transitions", @"File/Replicates", @"Created", @"File/Versions", @"Replaced", @"ReplacedByRun", @"ReplacesRun", @"File/Id", @"RowId" }, string.Empty, string.Empty); 
            var query = _peptideInfoQuery;
            var json = GetJson(query);
            var rows = json[@"rows"];
            foreach (var row in rows)
            {
                var versions = row[@"File/Versions"].ToString();
                var rowId = row[@"RowId"].ToString();
                if (_mostRecent.Contains(rowId))
                {
                    _mostRecent.Remove(rowId);
                    result[2] = versions;
                    var name = row[@"Name"].ToString();
                    result[0] = name;
                    /*var size = (long)rowOne[@"DocumentSize"];
                    result[1] = new FileSize(size).ToString();*/
                    fileInfos[0] = (string)row[@"File/Proteins"];
                    fileInfos[1] = (string)row[@"File/Peptides"];
                    fileInfos[2] = (string)row[@"File/Precursors"];
                    fileInfos[3] = (string)row[@"File/Transitions"];
                    fileInfos[4] = (string)row[@"File/Replicates"];
                    result[4] = (string)row[@"Created"];
                    var fileNode = new ListViewItem(result, 1)
                    {
                        ToolTipText = $"Proteins: {fileInfos[0]}, Peptides: {fileInfos[1]}, Precursors: {fileInfos[2]}, Transitions: {fileInfos[3]}, Replicates: {fileInfos[4]}",
                        Name = (string)row[@"_labkeyurl_FileName"]
                    };
                    listView.Items.Add(fileNode);
                } else if (versions.Equals(1.ToString()))
                {
                    result[2] = versions;
                    var name = row[@"Name"].ToString();
                    result[0] = name;
                    /*var size = (long)rowOne[@"DocumentSize"];
                    result[1] = new FileSize(size).ToString();*/
                    fileInfos[0] = (string)row[@"File/Proteins"];
                    fileInfos[1] = (string)row[@"File/Peptides"];
                    fileInfos[2] = (string)row[@"File/Precursors"];
                    fileInfos[3] = (string)row[@"File/Transitions"];
                    fileInfos[4] = (string)row[@"File/Replicates"];
                    result[4] = (string)row[@"Created"];
                    var fileNode = new ListViewItem(result, 1)
                    {
                        ToolTipText = $"Proteins: {fileInfos[0]}, Peptides: {fileInfos[1]}, Precursors: {fileInfos[2]}, Transitions: {fileInfos[3]}, Replicates: {fileInfos[4]}",
                        Name = (string)row[@"_labkeyurl_FileName"]
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
            _peptideInfoQuery = BuildQuery(_activeServer.URI.ToString(), nodePath, @"TargetedMSRuns", @"Current",
                new[] { @"Name", @"Deleted", @"Container/Path", @"File/Proteins", @"File/Peptides", @"File/Precursors", @"File/Transitions", @"File/Replicates", @"Created", @"File/Versions", @"Replaced", @"ReplacedByRun" , @"ReplacesRun", @"File/Id", @"RowId" }, string.Empty, string.Empty);
            var sizeQuery = BuildQuery(_activeServer.URI.ToString(), nodePath, "Runs", "Current",
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


        public void AddFiles(object sender, EventArgs e)
        {
            //Use the correct server to show files
            try
            {
                if (FolderBrowser.Testing)
                {
                    _restoring = true;
                    versionOptions.Visible = false;
                    versionLabel.Visible = false;
                    versionOptions.Text = ALL_VER;
                    var path = FolderBrowser.Path;
                    listView.Items.Clear();
                    _activeServer = FolderBrowser.ActiveServer;
                    _restoring = false;
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (FolderBrowser.ShowSky)
                        {
                            TestAddQueryFiles(path, versionLabel, versionOptions, FileJson);
                        }
                        else
                        {
                            var uriString = string.Concat(_activeServer.URI.ToString(), @"_webdav/", path + @"/@files?method=json");
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
                    _activeServer = FolderBrowser.ActiveServer;
                    _restoring = false;
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (FolderBrowser.ShowSky)
                        {
                            AddQueryFiles(path, versionLabel, versionOptions);
                        }
                        else
                        {
                            var uriString = string.Concat(_activeServer.URI.ToString(), @"_webdav/", path + @"/@files?method=json");
                            var uri = new Uri(uriString);
                            AddChildFiles(uri);
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
               // throw new Exception(ex.Message);
            }
            
        }

        /// <summary>
        /// Resets the TreeView to display either all Panorama folders, or only Panorama folders containing .sky files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
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
        private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_restoring)
            {
                listView.Items.Clear();
                if (versionOptions.Text.Equals(RECENT_VER))
                {
                    GetLatestVersion((string)FolderBrowser.Clicked.Tag);
                }
                else
                {
                    _activeServer = FolderBrowser.ActiveServer;
                    AddQueryFiles((string)FolderBrowser.Clicked.Tag, versionLabel, versionOptions);
                }
            }
            
        }

        public void TestCancel()
        {
            Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

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
                _downloadName = downloadName;
                _fileUrl = _activeServer.URI.ToString() + downloadName;

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
            FormClosed = true;
            _fileName = listView.SelectedItems.Count != 0 ? listView.SelectedItems[0].Text : string.Empty;
            _showingSky = showSkyCheckBox.Checked;
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
            //back.PerformClick();
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
            //return forward.Enabled;
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



    /*public class UTF8WebClient : WebClient
    {
        public UTF8WebClient()
        {
            Encoding = Encoding.UTF8;
        }

        public Uri ServerUri { get; private set; }

        public JObject Get(Uri uri)
        {
            var response = DownloadString(uri);
            return JObject.Parse(response);
        }

        
    }*/

    /*public class WebClientWithCredentials : UTF8WebClient
    {
        private CookieContainer _cookies = new CookieContainer();
        private string _csrfToken;
        private Uri _serverUri;

        private static string LABKEY_CSRF = @"X-LABKEY-CSRF";

        public WebClientWithCredentials(Uri serverUri, string username, string password)
        {
            // Add the Authorization header
            Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
            _serverUri = serverUri;
        }


    }*/

    /*public sealed class Server
    {
        public Server(string uriText, string username, string password)
            : this(new Uri(uriText), username, password)
        {
        }

        public Server(Uri uri, string username, string password)
        {
            Username = username;
            Password = password;
            URI = uri;
        }

        internal string Username { get; set; }
        internal string Password { get; set; }
        internal Uri URI { get; set; }

        public string GetKey()
        {
            return URI.ToString();
        }

        internal string AuthHeader
        {
            get { return GetBasicAuthHeader(Username, Password); }
        }

        internal static string GetBasicAuthHeader(string username, string password)
        {
            byte[] authBytes = Encoding.UTF8.GetBytes(String.Format(@"{0}:{1}", username, password));
            var authHeader = @"Basic " + Convert.ToBase64String(authBytes);
            return authHeader;
        }
    }
    */





    /*public class TreeViewStateRestorer
    {
        private readonly TreeView _tree;

        public TreeViewStateRestorer(TreeView tree)
        {
            _tree = tree;
        }

        /// <summary>
        /// Generates a persistent string storing information about the expansion and selection
        /// of nodes as well as the vertical scrolling of the form, separated by pipes
        /// </summary>
        public string GetPersistentString()
        {
            StringBuilder result = new StringBuilder();
            result.Append(GenerateExpansionString(_tree.Nodes)).Append('|');

            var treeMS = _tree as TreeViewMS;
            if (treeMS != null)
                result.Append(GenerateSelectionString()).Append('|');
            else
                result.Append(GenerateSingleSelectionString()).Append('|');

            result.Append(GenerateScrollString());
            return result.ToString();
        }

        /// <summary>
        /// The expansion string stores the indices of expanded nodes when called in the format
        /// a(b(c)), where a is the top level node as an integer, b is a child of a, and c
        /// is a child of b, etc. Multiple nodes and their children are stored as a comma-separated
        /// string, e.g. 0(1(0,1),2(0)),3
        /// </summary>
        private static string GenerateExpansionString(IEnumerable nodes)
        {
            int index = 0;
            StringBuilder result = new StringBuilder();
            foreach (TreeNode parent in nodes)
            {
                if (parent.IsExpanded)
                {
                    if (result.Length > 0)
                        result.Append(',');
                    result.Append(index);
                    string children = GenerateExpansionString(parent.Nodes);
                    if (children.Length != 0)
                    {
                        result.Append('(').Append(children).Append(')');
                    }
                }
                index++;
            }
            return result.ToString();
        }

        /// <summary>
        /// Gets the index of the selected node in a single-select TreeView according to the
        /// visual order of the nodes in the tree
        /// </summary>
        private int GenerateSingleSelectionString()
        {
            int index = 0;
            foreach (TreeNode node in VisibleNodes)
            {
                if (node.IsSelected)
                    return index;
                index++;
            }
            return 0;
        }

        /// <summary>
        /// <para>The selection string stores which nodes are selected in the graph. The first element
        /// is a single integer representing which node "the" selected node of the underlying TreeView.
        /// The remaining comma-separated elements in the string represent the indices of nodes
        /// that are selected according to the visual order of the nodes</para> 
        ///
        /// <para>These selections can be a single element (e.g. 1), a range (e.g. 1-7) or a disjoint selection
        /// consisting of multiple single elements and/or ranges (e.g. 1,3-6,8)</para>
        /// </summary>
        private string GenerateSelectionString()
        {
            StringBuilder selectedRanges = new StringBuilder();

            int index = 0;
            int rangeStart = -1;
            int prevSelection = -1;
            int selectedIndex = -1;

            foreach (TreeNodeMS node in VisibleNodes)
            {
                if (node.IsInSelection)
                {
                    if (rangeStart == -1)
                    {
                        rangeStart = index;
                    }
                    else if (index != prevSelection + 1)
                    {
                        AppendRange(selectedRanges, rangeStart, prevSelection);
                        rangeStart = index;
                    }
                    prevSelection = index;
                }

                // insert the TreeView selected node at the front of the string
                if (node.IsSelected)
                    selectedIndex = index;
                index++;
            }

            // complete any selection(s) that occur at the end of the tree
            if (rangeStart != -1)
            {
                AppendRange(selectedRanges, rangeStart, prevSelection);
            }

            return selectedIndex + @"," + selectedRanges;
        }

        private static void AppendRange(StringBuilder selectedRanges, int rangeStart, int prevSelection)
        {
            if (selectedRanges.Length > 0)
                selectedRanges.Append(',');

            if (rangeStart == prevSelection)
                selectedRanges.Append(rangeStart);
            else
                selectedRanges.AppendFormat(@"{0}-{1}", rangeStart, prevSelection);
        }

        /// <summary>
        /// The scroll string stores the numerical index of the first visible node in the form.
        /// The index corresponds to the location in the visual order of nodes in the form
        /// </summary>
        /// <returns></returns>
        private int GenerateScrollString()
        {
            int index = 0;
            foreach (TreeNode node in VisibleNodes)
            {
                if (node.IsVisible)
                    return index;
                index++;
            }
            return 0;
        }

        /// <summary>
        /// Restores the expansion and selection of the tree, and sets the top node for scrolling
        /// to be updated after all resizing has occured
        /// </summary>
        public void RestoreExpansionAndSelection(string persistentString)
        {
            if (!string.IsNullOrEmpty(persistentString))
            {
                string[] stateStrings = persistentString.Split('|');

                // check that the .view file will have the necessary information to rebuild the tree
                if (stateStrings.Length > 2)
                {
                    TreeViewMS treeMS = null;
                    try
                    {
                        _tree.BeginUpdate();

                        treeMS = _tree as TreeViewMS;
                        if (treeMS != null)
                            treeMS.AutoExpandSingleNodes = false;

                        ExpandTreeFromString(stateStrings[0]);

                        if (treeMS != null)
                            treeMS.AutoExpandSingleNodes = true;

                        SelectTreeFromString(stateStrings[1]);
                        NextTopNode = GetTopNodeFromString(stateStrings[2]);

                        if (treeMS != null)
                            treeMS.RestoredFromPersistentString = true;
                    }
                    catch (FormatException)
                    {
                        // Ignore and give up
                    }
                    finally
                    {
                        _tree.EndUpdate();
                        if (treeMS != null)
                            treeMS.AutoExpandSingleNodes = true;
                    }
                }
            }
        }

        /// <summary>
        /// Expands the tree from the persistent string data
        /// </summary>
        private void ExpandTreeFromString(string persistentString)
        {
            IEnumerator<char> dataEnumerator = persistentString.GetEnumerator();
            ExpandTreeFromString(_tree.Nodes, dataEnumerator);
        }

        private static bool ExpandTreeFromString(TreeNodeCollection nodes, IEnumerator<char> data)
        {
            bool finishedEnumerating = !data.MoveNext();
            int currentNode = 0;
            while (!finishedEnumerating)
            {
                char value = data.Current;
                switch (value)
                {
                    case ',':
                        finishedEnumerating = !data.MoveNext();
                        break;
                    case '(':
                        finishedEnumerating = ExpandTreeFromString(nodes[currentNode].Nodes, data);
                        break;
                    case ')':
                        return !data.MoveNext();
                    default: // value must be an integer
                        StringBuilder dataIndex = new StringBuilder();
                        dataIndex.Append(value);
                        finishedEnumerating = !data.MoveNext();

                        // enumerate until the next element is not an integer
                        while (!finishedEnumerating && data.Current != ',' && data.Current != '(' && data.Current != ')')
                        {
                            dataIndex.Append(data.Current);
                            finishedEnumerating = !data.MoveNext();
                        }

                        currentNode = int.Parse(dataIndex.ToString());

                        // if invalid node in tree, return
                        if (currentNode >= nodes.Count)
                            return true;
                        nodes[currentNode].Expand();
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Reselects tree nodes from the persistent string data
        /// </summary>
        private void SelectTreeFromString(string persistentString)
        {
            IList<TreeNode> visualOrder = VisibleNodes.ToArray();
            int nodeCount = visualOrder.Count;
            string[] selections = persistentString.Split(',');

            // select first element separately, returning if it is not a valid node
            int selectedIndex = int.Parse(selections[0]);
            if (selectedIndex < 0 || selectedIndex >= nodeCount)
                return;
            _tree.SelectedNode = visualOrder[selectedIndex];

            var tree = _tree as TreeViewMS;

            // add remaining nodes to selection (if TreeViewMS)
            if (tree != null)
            {
                for (int i = 1; i < selections.Length; i++)
                {
                    string selection = selections[i];
                    if (selection.Contains(@"-")) // when true, the string represents a range and not a single element
                    {
                        string[] range = selection.Split('-');
                        int start = Math.Min(nodeCount - 1, Math.Max(0, int.Parse(range[0])));
                        int end = Math.Min(nodeCount - 1, Math.Max(0, int.Parse(range[1])));
                        for (int j = start; j <= end; j++)
                        {
                            tree.SelectNode((TreeNodeMS)visualOrder[j], true);
                        }
                    }
                    else // the string represents a single element
                    {
                        int index = int.Parse(selection);
                        if (0 > index || index >= nodeCount)
                            return;
                        tree.SelectNode((TreeNodeMS)visualOrder[index], true);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the top node (for scrolling) for update when the tree has finished resizing
        /// </summary>
        private TreeNode GetTopNodeFromString(string persistentString)
        {
            IList<TreeNode> nodes = VisibleNodes.ToArray();
            int index = int.Parse(persistentString);
            if (0 > index || index >= nodes.Count)
                return null;
            return nodes[index];
        }

        private TreeNode NextTopNode { get; set; }

        /// <summary>
        /// Updates the top node in order to establish the correct scrolling of the tree. This should
        /// not be called until all resizing of the tree has occured
        /// </summary>
        public void UpdateTopNode()
        {
            _tree.TopNode = NextTopNode ?? _tree.TopNode;
        }

        /// <summary>
        /// Generates the visual order of nodes as they appear in the tree
        /// </summary>
        private IEnumerable<TreeNode> VisibleNodes
        {
            get
            {
                for (TreeNode node = _tree.Nodes.Count > 0 ? _tree.Nodes[0] : null; node != null; node = node.NextVisibleNode)
                    yield return node;
            }
        }
    }*/
    

   

    /// <summary>
    /// A MultiSelect TreeView.
    /// <para>
    /// Inspired by the example at http://www.codeproject.com/KB/tree/treeviewms.aspx for details.</para>
    /// </summary>
    /*public abstract class TreeViewMS : TreeView
    {
        // Length of the horizontal dashed lines representing each branch of the tree
        protected internal const int HORZ_DASH_LENGTH = 11;
        // Text padding
        protected internal const int PADDING = 3;
        // Width of images associated with the tree
        protected internal const int IMG_WIDTH = 16;

        private TreeNodeMS _anchorNode;
        private bool _inRightClick;

        private const int DEFAULT_ITEM_HEIGHT = 16;
        private const float DEFAULT_FONT_SIZE = (float)8.25;

        public const double DEFAULT_TEXT_FACTOR = 1;
        public const double LRG_TEXT_FACTOR = 1.25;
        public const double XLRG_TEXT_FACTOR = 1.5;

        protected TreeViewMS()
        {
            UseKeysOverride = false;
            _inRightClick = false;

            SelectedNodes = new TreeNodeSelectionMS();

            SetStyle(ControlStyles.UserPaint, true);
            ItemHeight = DEFAULT_ITEM_HEIGHT;

            TreeStateRestorer = new TreeViewStateRestorer(this);
            AutoExpandSingleNodes = true;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public TextureBrush DashBrush { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ICollection<TreeNodeMS> SelectedNodes { get; private set; }

        // If true, disjoint select is enabled.
        private bool _allowDisjoint;

        /// <summary>
        /// For functional testing of multiple selection code.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Keys KeysOverride { get; set; }

        public bool UseKeysOverride { get; set; }

        private Keys ModifierKeysOverriden
        {
            // If the control key is overriden, we can assume disjoint select was intended. 
            get
            {
                if (KeysOverride == Keys.Control)
                    _allowDisjoint = true;
                return UseKeysOverride ? KeysOverride : ModifierKeys;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // It is necessary to make sure that just the CTRL key is pressed and no modifiers -
            // else, CTRL+C, CTRL+V, CTRL+Z .. etc can all cause incorrect selections.
            // The combination below represents just the CTRL key command key.
            // Modifiers to CTRL are sent as a seperate, following command key, which will disable disjoint select.
            if (!UseKeysOverride)
                _allowDisjoint = keyData == (Keys.Control | Keys.LButton | Keys.ShiftKey);
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected bool IsDisjointSelect
        {
            // Disjoint select only occurs when the control key is held, so check that first then check
            // allow disjoint to check for modifiers to the control key.
            get { return ModifierKeysOverriden == Keys.Control && _allowDisjoint; }
        }

        protected bool IsRangeSelect { get { return ModifierKeysOverriden == Keys.Shift; } }

        public void SelectNode(TreeNodeMS node, bool select)
        {
            if (!select)
                SelectedNodes.Remove(node);
            else if (!node.IsInSelection)
            {
                SelectedNodes.Add(node);
                // Make sure all ancestors of this node are expanded
                for (var parent = node.Parent; parent != null && !parent.IsExpanded; parent = parent.Parent)
                {
                    parent.Expand();
                }
            }
            node.IsInSelection = select;
        }

        public bool IsNodeSelected(TreeNode node)
        {
            return node is TreeNodeMS && ((TreeNodeMS)node).IsInSelection;
        }

        protected void UpdateSelection()
        {
            // Remove any nodes from the selection that may have been
            // removed from the tree.
            var selectedNodes = SelectedNodes.ToArray();
            foreach (var node in selectedNodes)
            {
                if (node.TreeView == null)
                    SelectedNodes.Remove(node);
            }

            // If any nodes were removed from the selection, reset the
            // anchor node to the selected node.
            if (selectedNodes.Length != SelectedNodes.Count)
                _anchorNode = (TreeNodeMS)SelectedNode;
        }

        [Browsable(true)]
        public bool AutoExpandSingleNodes { get; set; }

        protected void TreeViewMS_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (!IsInUpdate)
            {
                TreeNode nodeTree = e.Node;
                if (IsParentNode(nodeTree))
                {
                    // Save and restore top node to keep from scrolling
                    TreeNode nodeTop = TopNode;

                    int children = EnsureChildren(nodeTree);

                    // Do the Windows explorer thing of expanding single node children.
                    if (AutoExpandSingleNodes && children == 1)
                        nodeTree.Nodes[0].Expand();

                    TopNode = nodeTop;
                }
            }
        }

        protected abstract bool IsParentNode(TreeNode node);

        protected abstract int EnsureChildren(TreeNode node);

        public bool RestoredFromPersistentString { get; set; }
        private TreeViewStateRestorer TreeStateRestorer { get; set; }

        public string GetPersistentString()
        {
            return TreeStateRestorer.GetPersistentString();
        }

        public void RestoreExpansionAndSelection(string persistentString)
        {
            TreeStateRestorer.RestoreExpansionAndSelection(persistentString);
        }

        public void UpdateTopNode()
        {
            TreeStateRestorer.UpdateTopNode();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        public void ScrollLeft()
        {
            SetScrollPos(Handle, 0, 0, true);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _inRightClick = e.Button == MouseButtons.Right;
            if (_inRightClick)
            {
                TreeNodeMS node = (TreeNodeMS)GetNodeAt(0, e.Y);
                if (node != null && node.BoundsMS.Contains(e.Location))
                    SelectedNode = node;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_inRightClick)
            {
                TreeNodeMS node = (TreeNodeMS)GetNodeAt(0, e.Y);
                if (node != null && node.BoundsMS.Contains(e.Location))
                {
                    // If we are within the bounds of a node and that node is not the selected node,
                    // make it the selected node. Changing the font of the TreeView at runtime
                    // apparently does not update node bounds, so we need to do this check in case the
                    // user clicked outside of the original node bounds.
                    if (!ReferenceEquals(node, SelectedNode))
                        SelectedNode = node;
                    // Handle cases where clicking on the selected node should change
                    // the selection.
                    else
                    {
                        // Disjoint selection or the SelectedNode is not in the selection
                        if (IsDisjointSelect || !IsNodeSelected(node))
                            SelectedNode = null;
                        // More than a single node currently selected, and not performing
                        // range selection on an existing range selection.
                        else if (SelectedNodes.Count > 1 &&
                                    (!IsRangeSelect || ReferenceEquals(_anchorNode, SelectedNode)))
                            SelectedNode = null;
                    }
                }
            }
            base.OnMouseUp(e);
            _inRightClick = false;
        }

        protected override void OnBeforeSelect(TreeViewCancelEventArgs e)
        {
            base.OnBeforeSelect(e);

            // New selection is always the anchor for the next shift selection
            if (_anchorNode == null || !IsRangeSelect)
                _anchorNode = (TreeNodeMS)e.Node;
        }

        protected override void OnAfterSelect(TreeViewEventArgs e)
        {
            // Save old selection for invalidating
            var selectedNodesOld = SelectedNodes.ToArray();

            TreeNodeMS node = (TreeNodeMS)e.Node;

            // Don't change the selection if this is a right click and the node is in the
            // selection.
            if (node != null && !(_inRightClick && node.IsInSelection))
            {
                if (IsDisjointSelect)
                {
                    // Toggle selection on the node
                    SelectNode(node, !IsNodeSelected(e.Node));
                }
                else if (IsRangeSelect && !ReferenceEquals(_anchorNode, node))
                {
                    // Figure out top and bottom of the range to be selected
                    TreeNodeMS upperNode = _anchorNode;
                    TreeNodeMS bottomNode = node;
                    if (upperNode.BoundsMS.Top > bottomNode.BoundsMS.Top)
                        Swap(ref upperNode, ref bottomNode);

                    // Set new selection to contain all visible nodes between top and bottom
                    SelectedNodes.Clear();
                    while (upperNode != null && !ReferenceEquals(upperNode, bottomNode))
                    {
                        SelectNode(upperNode, true);
                        upperNode = (TreeNodeMS)upperNode.NextVisibleNode;
                    }
                    SelectNode(bottomNode, true);
                }
                else
                {
                    // Make this a single selection of the selected node.
                    SelectedNodes.Clear();
                    SelectNode(node, true);
                }

                // Invalidate the changed nodes
                var unchangedNodes = new HashSet<TreeNodeMS>(selectedNodesOld.Intersect(SelectedNodes));
                InvalidateChangedNodes(selectedNodesOld, unchangedNodes);
                InvalidateChangedNodes(SelectedNodes, unchangedNodes);
            }

            Invalidate();

            // Make sure selection is updated before after select event is fired
            base.OnAfterSelect(e);
        }

        public static void Swap<TItem>(ref TItem val1, ref TItem val2)
        {
            TItem tmp = val1;
            val1 = val2;
            val2 = tmp;
        }



        private void InvalidateChangedNodes(IEnumerable<TreeNodeMS> nodes, ICollection<TreeNodeMS> unchangedNodes)
        {
            if (IsInUpdate)
                return;

            foreach (var node in nodes)
            {
                if (!unchangedNodes.Contains(node))
                    InvalidateNode(node);
            }
        }

        protected void InvalidateNode(TreeNodeMS node)
        {
            Invalidate(new Rectangle(0, node.BoundsMS.Top, ClientRectangle.Width, node.BoundsMS.Height));
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {

        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // If we have nodes, then we have to draw them - and that means everything
            // about the node.
            using (var backColorBrush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(backColorBrush, ClientRectangle);
            }

            // No painting beyond the background while updating, since it can cause
            // unexpected exceptions.  This used to happen during a node removal that
            // caused removal of the control's scrollbar.
            if (IsInUpdate)
                return;

            // Draw all nodes exposed in the paint clipping rectangle.
            var drawRect = e.Graphics.ClipBounds;
            drawRect.Intersect(e.ClipRectangle);
            int bottom = (int)drawRect.Bottom;
            for (var node = TopNode;
                node != null && node.Bounds.Top <= bottom;
                node = node.NextVisibleNode)
            {
                ((TreeNodeMS)node).DrawNodeCustom(e.Graphics, ClientRectangle.Right);
            }
        }

        private int _updateLockCount;

        public bool IsInUpdate { get { return _updateLockCount > 0; } }

        public void BeginUpdateMS()
        {
            BeginUpdate();
            _updateLockCount++;
        }

        public void EndUpdateMS()
        {
            if (_updateLockCount == 0)
                return;
            if (--_updateLockCount == 0)
                UpdateSelection();
            EndUpdate();
        }

        private class TreeNodeSelectionMS : ICollection<TreeNodeMS>
        {
            private readonly List<TreeNodeMS> _nodes = new List<TreeNodeMS>();

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public IEnumerator<TreeNodeMS> GetEnumerator()
            {
                return _nodes.GetEnumerator();
            }

            public void Add(TreeNodeMS item)
            {
                if (item == null)
                {
                    throw new ArgumentNullException();
                }
                item.IsInSelection = true;
                _nodes.Add(item);
            }

            public void Clear()
            {
                _nodes.ForEach(node => node.IsInSelection = false);
                _nodes.Clear();
            }

            public bool Contains(TreeNodeMS item)
            {
                return _nodes.Contains(item);
            }

            public void CopyTo(TreeNodeMS[] array, int arrayIndex)
            {
                _nodes.CopyTo(array, arrayIndex);
            }

            public bool Remove(TreeNodeMS item)
            {
                if (item == null)
                {
                    return false;
                }
                if (_nodes.Remove(item))
                {
                    item.IsInSelection = false;
                    return true;
                }
                return false;
            }

            public int Count
            {
                get { return _nodes.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }
        }
    }*/

    /*public class TreeNodeMS : TreeNode
    {
        private const TextFormatFlags FORMAT_TEXT = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        public TreeNodeMS()
        {
        }

        public TreeNodeMS(string text) : base(text)
        {
        }

        /// <summary>
        /// Returns a typed reference to the owning <see cref="SequenceTree"/>.
        /// </summary>
        public TreeViewMS TreeViewMS { get { return (TreeViewMS)TreeView; } }

        public bool IsInSelection { get; protected internal set; }

        public Color ForeColorMS
        {
            get
            {
                if (!(IsSelected && IsInSelection) || !TreeViewMS.Focused)
                    return ForeColor;
                return SystemColors.HighlightText;
            }
        }




     

        protected double _textZoomFactor;
        protected string _widthText;
        protected int _widthCustom;
        protected IList<Color> _groupColors;

        protected virtual int WidthCustom
        {
            get { return _widthCustom > 0 ? _widthCustom : Bounds.Width; }
        }

  

        /// <summary>
        /// Because these nodes allow override of text drawing, this virtual
        /// is required to get the true bounds of the text that is drawn.
        /// </summary>
        public Rectangle BoundsMS
        {
            get
            {
                var bounds = Bounds;
                bounds.Width = WidthCustom;
                return bounds;
            }
        }

        public int XIndent
        {
            // Finds the X coordinate of the indent for this node, accounting for horizontal scrolling.
            get
            {
                int treeIndent = TreeViewMS.HORZ_DASH_LENGTH + TreeViewMS.PADDING;
                // Always indent for the node image, whether it has one or not
                treeIndent += TreeViewMS.IMG_WIDTH;
                // Only indent for the state image, if it has one
                if (StateImageIndex != -1)
                    treeIndent += TreeViewMS.IMG_WIDTH;
                return BoundsMS.X - treeIndent;
            }
        }

        public int HorizScrollDiff
        {
            get
            {
                return XIndent - (Level * TreeView.Indent + 11);
            }
        }

        public virtual void DrawNodeCustom(Graphics g, int rightEdge)
        {

            Rectangle bounds = BoundsMS;

            // Draw dashed lines
            var treeView = TreeViewMS;
            var dashBrush = treeView.DashBrush;
            // Horizontal line.
            dashBrush.TranslateTransform(Level % 2 + HorizScrollDiff, 0);
            g.FillRectangle(dashBrush, XIndent, bounds.Top + bounds.Height / 2,
                TreeViewMS.HORZ_DASH_LENGTH, 1);
            // Vertical lines corresponding to the horizontal level of this node.
            dashBrush.TranslateTransform(-Level % 2 - HorizScrollDiff, 0);
            // Check if this is the Root.
            if (ReferenceEquals(this, treeView.Nodes[0]))
            {
                if (treeView.Nodes.Count > 1)
                {
                    g.FillRectangle(dashBrush, XIndent, bounds.Top + bounds.Height / 2,
                        1, bounds.Height / 2);
                }
            }
            // Move up the levels of the tree, drawing the corresponding vertical lines.
            else
            {
                try
                {
                    TreeNodeMS curNode = this;
                    while (curNode != null)
                    {
                        dashBrush.TranslateTransform(0, curNode.Level % 2);
                        if (curNode.NextNode != null)
                            g.FillRectangle(dashBrush, curNode.XIndent, bounds.Top, 1, bounds.Height);
                        else if (curNode == this)
                            g.FillRectangle(dashBrush, curNode.XIndent, bounds.Top, 1, bounds.Height / 2);
                        dashBrush.TranslateTransform(0, -curNode.Level % 2);
                        curNode = curNode.Parent as TreeNodeMS;
                    }
                }
                catch (NullReferenceException)
                {
                    // Ignore a NullReferenceException in this code.  The case
                    // that once caused this has been fixed, but this safeguard is
                    // kept to avoid showing an unhandled exception to the user.

                    // If the node being painted is in the process of being removed
                    // from the tree, then curNode.NextNode will throw a NRE.
                }
            }


            // Draw images associated with the node.
            int imgLocX = XIndent + TreeViewMS.HORZ_DASH_LENGTH;
            const int imgWidth = TreeViewMS.IMG_WIDTH, imgHeight = TreeViewMS.IMG_WIDTH;
            if (StateImageIndex != -1)
            {
                Image stateImg = TreeView.StateImageList.Images[StateImageIndex];
                g.DrawImageUnscaled(stateImg, imgLocX, bounds.Top + (bounds.Height - imgHeight) / 2, imgWidth, imgHeight);
                imgLocX += imgWidth;
            }
            if (ImageIndex != -1)
            {
                Image nodeImg = TreeView.ImageList.Images[ImageIndex];
                g.DrawImageUnscaled(nodeImg, imgLocX, bounds.Top + (bounds.Height - imgHeight) / 2, imgWidth, imgHeight);
            }

               
        }

           

            
    }*/

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
