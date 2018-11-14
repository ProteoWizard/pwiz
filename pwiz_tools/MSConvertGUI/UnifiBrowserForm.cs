//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2018 Matt Chambers - Nashville, TN 37221
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using IdentityModel.Client;
using pwiz.Common.SystemUtil;
using System.Threading;

namespace MSConvertGUI
{
    public partial class UnifiBrowserForm : Form
    {
        public class Credentials
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string IdentityServer { get; set; }
            public string ClientScope { get; set; }
            public string ClientSecret { get; set; }

            public string GetUrlWithAuthentication(string url)
            {
                return url.Replace("://", String.Format("://{0}:{1}@", Username, Password)) +
                       String.Format("?identity={0}&scope={1}&secret={2}", IdentityServer, ClientScope, ClientSecret) ;
            }

            public static Tuple<string, Credentials> ParseUrlWithAuthentication(string url)
            {
                var uri = new Uri(url);
                var credentials = new Credentials();
                if (uri.UserInfo.Contains(':'))
                {
                    credentials.Username = uri.UserInfo.Split(':')[0];
                    credentials.Password = uri.UserInfo.Split(':')[1];
                }
                credentials.IdentityServer = Regex.Match(uri.Query, "identity=([^&]+)").Groups[1].Value;
                credentials.ClientScope = Regex.Match(uri.Query, "scope=([^&]+)").Groups[1].Value;
                credentials.ClientSecret = Regex.Match(uri.Query, "secret=([^&]+)").Groups[1].Value;
                return new Tuple<string, Credentials>(uri.Authority, credentials);
            }
        }

        public class UnifiResultSorter : ListViewColumnSorter
        {
            UnifiBrowserForm p;
            public UnifiResultSorter(UnifiBrowserForm parent)
            {
                p = parent;
            }
            public override int Compare(object x, object y)
            {
                if (SortColumn < 0)
                {
                    ListViewItem lvX = x as ListViewItem;
                    ListViewItem lvY = y as ListViewItem;
                    int compareResult;
                    
                    compareResult = CompareSubItems(lvX, lvY, p.Analysis.Index);
                    if (compareResult == 0)
                    {
                        compareResult = CompareSubItems(lvX, lvY, p.WellPosition.Index);
                        if (compareResult == 0)
                        {
                            compareResult = CompareSubItems(lvX, lvY, p.SourceName.Index);
                            if (compareResult == 0)
                            {
                                compareResult = CompareSubItems(lvX, lvY, p.Replicate.Index);
                                return compareResult;
                            }
                            else
                                return compareResult;
                        }
                        else
                            return compareResult;
                    }
                    else
                        return compareResult;
                }
                else
                    return base.Compare(x, y);
            }
        }

        private string IdentityServerBasePath { get { return SelectedCredentials.IdentityServer + "/identity"; } }
        private string AuthorizeEndpoint { get { return IdentityServerBasePath + "/connect/authorize"; } }
        private string LogoutEndpoint { get { return IdentityServerBasePath + "/connect/endsession"; } }
        private string TokenEndpoint { get { return IdentityServerBasePath + "/connect/token"; } }
        private string UserInfoEndpoint { get { return IdentityServerBasePath + "/connect/userinfo"; } }
        private string IdentityTokenValidationEndpoint { get { return IdentityServerBasePath + "/connect/identitytokenvalidation"; } }
        private string TokenRevocationEndpoint { get { return IdentityServerBasePath + "/connect/revocation"; } }

        private const string DefaultUnifiPort = ":50034";
        private const string BasePath = "/unifi/v1";

        private string _accessToken;
        private ImageList _nodeImages;
        private HttpClient _httpClient;
        private Dictionary<string, TreeNode> _nodeById;
        private ListViewColumnSorter _sorter;
        private CancellationTokenSource cancellationTokenSource = null;

        public string SelectedHost { get { return serverLocationTextBox.Text; } }
        public Credentials SelectedCredentials { get; private set; }
        public IEnumerable<UnifiSampleResult> SelectedSampleResults;

        public UnifiBrowserForm(string defaultUrl = null, Credentials defaultCredentials = null)
        {
            InitializeComponent();

            connectButton.Click += connectButton_Click;
            openButton.Click += openButton_Click;
            cancelButton.Click += cancelButton_Click;

            _nodeImages = treeViewImageList;
            FileTree.ImageList = _nodeImages;

            var method = typeof(Control).GetMethod("SetStyle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method.Invoke(FolderViewList, new object[] { ControlStyles.OptimizedDoubleBuffer, true });

            FolderViewList.LargeImageList = FolderViewList.SmallImageList = treeViewImageList;
            FolderViewList.Columns.Remove(SourceSize);
            FolderViewList.Columns.Remove(SourceType);

            FolderViewList.ListViewItemSorter = _sorter = new UnifiResultSorter(this);
            _sorter.Order = SortOrder.Ascending;
            _sorter.SortColumn = -1; // default to multi-column sort

            _httpClient = new HttpClient();

            openButton.Enabled = false;

            serverLocationTextBox.Text = defaultUrl ?? "unifiapi.waters.com:50034";
            SelectedCredentials = defaultCredentials;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (serverLocationTextBox.Text.Length > 0)
                connectButton.PerformClick();
        }

        async Task<JObject> GetJsonFromEndpoint(string endpoint, CancellationToken cancellationToken)
        {
            string url = serverLocationTextBox.Text + endpoint;

            //execute web api call
            HttpResponseMessage responseMessage = await _httpClient.GetAsync(url, cancellationToken);
            if (!responseMessage.IsSuccessStatusCode)
            {
                Console.WriteLine(responseMessage.ToString());
                throw new Exception("error getting response from UNIFI server: " + responseMessage.ToString());
            }

            string responseBody = responseMessage.Content.ReadAsStringAsync().Result;

            return JObject.Parse(responseBody);
        }

        async void GetFolders()
        {
            string host = FileTree.Nodes[0].Text;

            if (cancellationTokenSource != null)
                cancellationTokenSource.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            try
            {
                FileTree.Nodes[0].Text += " (loading...)";
                UseWaitCursor = true;

                JObject jobject = await GetJsonFromEndpoint("/folders", cancellationToken);

                JArray folders = jobject["value"] as JArray;
                _nodeById = new Dictionary<string, TreeNode>();
                foreach (JObject folder in folders)
                {
                    var path = folder.Property("path").Value.ToString();
                    var id = folder.Property("id").Value.ToString();
                    JToken parentIdProperty;
                    if (!folder.TryGetValue("parentId", out parentIdProperty) || (parentIdProperty as JValue).Value == null)
                    {
                        _nodeById[id] = FileTree.TopNode.Nodes.Add(id, System.IO.Path.GetFileName(path), 1);
                    }
                    else
                    {
                        var parentId = (parentIdProperty as JValue).Value.ToString();
                        var parentNode = _nodeById[parentId];
                        _nodeById[id] = parentNode.Nodes.Add(id, System.IO.Path.GetFileName(path), 1);
                    }
                    _nodeById[id].Tag = "folder";
                }

                FileTree.Nodes[0].Text = host;
                FileTree.ExpandAll();
            }
            catch (TaskCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                    Program.HandleException(new TimeoutException("UNIFI API call timed out"));
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                FileTree.Nodes[0].Text = host + " (error)";
                Program.HandleException(ex);
                disconnect();
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        public bool UrlSupportsSSL(string urlWithoutScheme)
        {
            try
            {
                using (var response = _httpClient.GetAsync("https://" + urlWithoutScheme).Result)
                {
                    // some sites perform a service side redirect to the http site before the browser/request can throw an errror.
                    return response.RequestMessage.RequestUri.Scheme == "https";
                }
            }
            catch (Exception e)
            {
                while (e.InnerException != null) e = e.InnerException;
                if (e is System.IO.IOException && e.HResult == -2146232800) // The handshake failed due to an unexpected packet format.
                    return false;
                throw e;
            }
        }

        void connect()
        {
            string url = serverLocationTextBox.Text;
            if (!url.Any())
                return;

            // add default unifi port if no port given
            var url2 = new Uri(!url.StartsWith("http") ? "http://" + url : url);
            if (url2.IsDefaultPort)
                url = url.Replace(url2.Host, url2.Host + DefaultUnifiPort);

            // guess scheme if no scheme given
            try
            {
                if (!url.StartsWith("http"))
                    url = (UrlSupportsSSL(url) ? "https://" : "http://") + url;
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "Error");
                return;
            }

            // add API path if missing
            if (!url.EndsWith(BasePath))
                url += BasePath;

            string host = url2.Host;

            while (true)
            {
                try
                {
                    if (SelectedCredentials != null && SelectedCredentials.Username?.Any() == true && SelectedCredentials.Password?.Any() == true)
                    {
                        TokenClient client = new TokenClient(TokenEndpoint, "resourceownerclient", SelectedCredentials.ClientSecret, null, AuthenticationStyle.BasicAuthentication);
                        TokenResponse response = client.RequestResourceOwnerPasswordAsync(SelectedCredentials.Username, SelectedCredentials.Password, SelectedCredentials.ClientScope).Result;
                        if (response.IsError)
                        {
                            if (response.ErrorDescription?.Contains("InvalidLogin") == true)
                            {
                                using (new CenterWinDialog(this))
                                {
                                    if (MessageBox.Show(this, "Username or password are not correct!", "Error", MessageBoxButtons.RetryCancel) == DialogResult.Cancel)
                                        return;
                                }
                            }
                            else if (response.Exception != null)
                                throw response.Exception;
                            else
                                throw new InvalidOperationException(response.Error + ": " + response.ErrorDescription);
                        }
                        else
                        {
                            _accessToken = response.AccessToken;
                            _httpClient.SetBearerToken(_accessToken);
                            _httpClient.DefaultRequestHeaders.Remove("Accept");
                            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json;odata.metadata=minimal");
                            break;
                        }
                    }

                    var loginForm = new LoginForm() { StartPosition = FormStartPosition.CenterParent };
                    if (SelectedCredentials != null && SelectedCredentials.Username?.Any() == true && SelectedCredentials.Password?.Any() == true)
                    {
                        loginForm.usernameTextBox.Text = SelectedCredentials.Username;
                        loginForm.passwordTextBox.Text = SelectedCredentials.Password;
                        loginForm.identityServerTextBox.Text = SelectedCredentials.IdentityServer.Replace(host, "<HostURL>");
                        loginForm.clientScopeTextBox.Text = SelectedCredentials.ClientScope;
                        loginForm.clientSecretTextBox.Text = SelectedCredentials.ClientSecret;
                    }
                    if (loginForm.ShowDialog(this) == DialogResult.Cancel)
                        return;

                    SelectedCredentials = new Credentials
                    {
                        Username = loginForm.usernameTextBox.Text,
                        Password = loginForm.passwordTextBox.Text,
                        IdentityServer = loginForm.identityServerTextBox.Text.Replace("<HostURL>", host),
                        ClientScope = loginForm.clientScopeTextBox.Text,
                        ClientSecret = loginForm.clientSecretTextBox.Text
                    };
                    if (!SelectedCredentials.IdentityServer.StartsWith("http"))
                        SelectedCredentials.IdentityServer = (url.StartsWith("http://") ? "http://" : "https://") + SelectedCredentials.IdentityServer;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null)
                        ex = ex.InnerException; // bypass AggregateException
                    if (MessageBox.Show(this, "Failed to connect to identity server (" + SelectedCredentials.IdentityServer + "):\r\n" + ex.Message, "Error", MessageBoxButtons.RetryCancel) == DialogResult.Cancel)
                        return;
                    SelectedCredentials = null;
                }
            }

            serverLocationTextBox.Text = url;

            FileTree.Nodes.Clear();
            FileTree.Nodes.Add("host", host, 0);
            GetFolders();
            serverLocationTextBox.Text = url;
            serverLocationTextBox.ReadOnly = true;
            connectButton.Text = "Disconnect";
        }

        void disconnect()
        {
            if (cancellationTokenSource != null)
                cancellationTokenSource.Cancel();

            FileTree.Nodes.Clear();
            FolderViewList.Items.Clear();
            serverLocationTextBox.ReadOnly = false;
            SelectedCredentials = null;
            connectButton.Text = "Connect";
        }

        void connectButton_Click(object sender, EventArgs e)
        {
            if (serverLocationTextBox.ReadOnly)
                disconnect();
            else
                connect();
        }

        void openButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;

            SelectedSampleResults = FolderViewList.SelectedItems.Cast<ListViewItem>().Select(o => o.Tag as UnifiSampleResult);
        }

        void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (cancellationTokenSource != null)
                cancellationTokenSource.Cancel();

            base.OnFormClosed(e);
        }

        private async void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if(e.Node.Tag == null)
                return;

            if (cancellationTokenSource != null)
                cancellationTokenSource.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            try
            {
                if (e.Node.Tag.ToString() == "folder")
                {
                    FolderViewList.Items.Clear();
                    JObject jobject = await GetJsonFromEndpoint(String.Format("/folders({0})/items", e.Node.Name), cancellationToken);
                    JArray items = jobject["value"] as JArray;

                    foreach (JObject item in items)
                    {
                        var type = item.Property("type").Value.ToString();

                        // only display sample results
                        if (type != "SampleResult")
                            continue;

                        var name = item.Property("name").Value.ToString();
                        var id = item.Property("id").Value.ToString();
                        var created = item.Property("createdAt").Value.ToString();
                        var sampleResult = await GetJsonFromEndpoint(String.Format("/sampleresults({0})", id), cancellationToken);
                        var sample = sampleResult.Property("sample").Value as JObject;
                        var replicate = sample.Property("replicateNumber").Value.ToString();
                        var wellPosition = sample.Property("wellPosition").Value.ToString();
                        var acquisitionStartTime = sample.Property("acquisitionStartTime").Value.ToString();
                        //name = sample.Property("name").Value.ToString();

                        var analyses = await GetJsonFromEndpoint(String.Format("/sampleresults({0})/analyses", id), cancellationToken);
                        JObject analysis = (analyses["value"] as JArray).FirstOrDefault() as JObject;
                        string analysisName = "unknown";
                        if (analysis != null)
                            analysisName = analysis.Property("name").Value.ToString();

                        if (name.Length == 0)
                            name = analysisName;

                        var updateView = new MethodInvoker(() => { FolderViewList.Items.Add(new ListViewItem(new string[] { analysisName, wellPosition, replicate, name, acquisitionStartTime, created }, 2) { Tag = new UnifiSampleResult(serverLocationTextBox.Text, id, name, replicate, wellPosition) }); });
                        FolderViewList.Invoke(updateView);
                    }

                    cancellationTokenSource = null;
                }
            }
            catch (TaskCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                    Program.HandleException(new TimeoutException("UNIFI API call timed out"));
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                Program.HandleException(ex);
                disconnect();
            }

            FolderViewList.Sort();
        }

        private void FolderViewList_SelectedIndexChanged(object sender, EventArgs e)
        {
            openButton.Enabled = true;

            sampleResultTextBox.Text = "\"" + String.Join("\" \"", FolderViewList.SelectedItems.Cast<ListViewItem>().Select(o => o.Tag as UnifiSampleResult)) + "\"";
        }

        private void FolderViewList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            openButton_Click(sender, e);
        }

        private SortOrder toggleSort(SortOrder o) { return o == SortOrder.Descending ? SortOrder.Ascending : SortOrder.Descending; }

        private void FolderViewList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sorter.SortColumn)
                _sorter.Order = toggleSort(_sorter.Order);
            else
                _sorter.SortColumn = e.Column;
            FolderViewList.Sort();
        }
    }
}
