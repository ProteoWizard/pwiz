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

        private const string BasePath = "/unifi/v1";

        private string _accessToken;
        private ImageList _nodeImages;
        private HttpClient _httpClient;
        private Dictionary<string, TreeNode> _nodeById;
        private ListViewColumnSorter _sorter;

        public string SelectedHost { get { return serverLocationTextBox.Text; } }
        public Credentials SelectedCredentials { get; private set; }
        public IEnumerable<string> SelectedSampleResults;

        public UnifiBrowserForm(string defaultUrl = null, Credentials defaultCredentials = null)
        {
            InitializeComponent();

            connectButton.Click += connectButton_Click;
            openButton.Click += openButton_Click;
            cancelButton.Click += cancelButton_Click;

            _nodeImages = treeViewImageList;
            FileTree.ImageList = _nodeImages;

            DoubleBuffered = true;

            FolderViewList.LargeImageList = FolderViewList.SmallImageList = treeViewImageList;
            FolderViewList.Columns.Remove(SourceSize);

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

        JObject GetJsonFromEndpoint(string endpoint)
        {
            string url = serverLocationTextBox.Text + endpoint;

            //execute web api call
            HttpResponseMessage responseMessage = _httpClient.GetAsync(url).Result;
            if (!responseMessage.IsSuccessStatusCode)
            {
                Console.WriteLine(responseMessage.ToString());
                throw new Exception("error getting response from UNIFI server: " + responseMessage.ToString());
            }

            string responseBody = responseMessage.Content.ReadAsStringAsync().Result;

            return JObject.Parse(responseBody);
        }

        void GetFolders()
        {
            JObject jobject = GetJsonFromEndpoint("/folders");
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
        }

        void connect()
        {
            string url = serverLocationTextBox.Text;
            if (!url.Any())
                return;
            if (!url.StartsWith("http"))
                url = "https://" + url;
            if (!url.EndsWith(BasePath))
                url += BasePath;
            string host = new Uri(url).Host;

            while (true)
            {
                try
                {
                    if (SelectedCredentials != null && SelectedCredentials.Username.Any() && SelectedCredentials.Password.Any())
                    {
                        TokenClient client = new TokenClient(TokenEndpoint, "resourceownerclient", SelectedCredentials.ClientSecret);
                        TokenResponse response = client.RequestResourceOwnerPasswordAsync(SelectedCredentials.Username, SelectedCredentials.Password, SelectedCredentials.ClientScope).Result;
                        if (response.IsError)
                        {
                            if (response.ErrorDescription.Contains("InvalidLogin"))
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
                    if (SelectedCredentials != null)
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
                        SelectedCredentials.IdentityServer = "https://" + SelectedCredentials.IdentityServer;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null)
                        ex = ex.InnerException; // bypass AggregateException
                    if (MessageBox.Show(this, "Failed to connect to identity server:\r\n" + ex.Message, "Error", MessageBoxButtons.RetryCancel) == DialogResult.Cancel)
                        return;
                    SelectedCredentials = null;
                }
            }

            serverLocationTextBox.Text = url;

            FileTree.Nodes.Clear();
            FileTree.Nodes.Add("host", host, 0);

            try
            {
                GetFolders();
            }
            catch (Exception ex)
            {
                Program.HandleException(ex);
                disconnect();
                return;
            }

            FileTree.ExpandAll();

            serverLocationTextBox.Text = url;
            serverLocationTextBox.ReadOnly = true;
            connectButton.Text = "Disconnect";
        }

        void disconnect()
        {
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

            SelectedSampleResults = FolderViewList.SelectedItems.Cast<ListViewItem>().Select(o => serverLocationTextBox.Text + (o.Tag as string));
        }

        void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if(e.Node.Tag == null)
                return;

            if (e.Node.Tag.ToString() == "folder")
            {
                FolderViewList.Items.Clear();
                JObject jobject = GetJsonFromEndpoint(String.Format("/folders({0})/items", e.Node.Name));
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

                    var sampleResult = GetJsonFromEndpoint(String.Format("/sampleresults({0})", id));
                    var sample = sampleResult.Property("sample").Value as JObject;
                    var replicate = sample.Property("replicateNumber").Value.ToString();
                    var wellPosition = sample.Property("wellPosition").Value.ToString();
                    var acquisitionStartTime = sample.Property("acquisitionStartTime").Value.ToString();
                    name = sample.Property("name").Value.ToString();

                    JObject analysis = (GetJsonFromEndpoint(String.Format("/sampleresults({0})/analyses", id))["value"] as JArray).FirstOrDefault() as JObject;
                    string analysisName = "unknown";
                    if (analysis != null)
                        analysisName = analysis.Property("name").Value.ToString();

                    FolderViewList.Items.Add(new ListViewItem(new string[] { type, analysisName, wellPosition, replicate, name, acquisitionStartTime, created }, 2) { Tag = String.Format("/sampleresults({0})", id) });
                }
            }

            FolderViewList.Sort();
        }

        private void FolderViewList_SelectedIndexChanged(object sender, EventArgs e)
        {
            openButton.Enabled = true;

            sampleResultTextBox.Text = "\"" + String.Join("\" \"", FolderViewList.SelectedItems.Cast<ListViewItem>().Select(o => o.Tag as string)) + "\"";
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
