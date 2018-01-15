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
using Newtonsoft.Json.Linq;
using System.Net.Http;
using IdentityModel.Client;

namespace MSConvertGUI
{
    public partial class UnifiBrowserForm : Form
    {
        private const string IdentityServerBaseAddress = "https://unifiapi.waters.com:50333/identity";
        private const string AuthorizeEndpoint = IdentityServerBaseAddress + "/connect/authorize";
        private const string LogoutEndpoint = IdentityServerBaseAddress + "/connect/endsession";
        private const string TokenEndpoint = IdentityServerBaseAddress + "/connect/token";
        private const string UserInfoEndpoint = IdentityServerBaseAddress + "/connect/userinfo";
        private const string IdentityTokenValidationEndpoint = IdentityServerBaseAddress + "/connect/identitytokenvalidation";
        private const string TokenRevocationEndpoint = IdentityServerBaseAddress + "/connect/revocation";

        //private const string Host = "unifiapi.waters.com:50034";
        private const string BasePath = "/unifi/v1";

        private string _accessToken;
        private string _username, _password;
        private ImageList _nodeImages;
        private HttpClient _httpClient;
        private Dictionary<string, TreeNode> _nodeById;

        public IEnumerable<string> SelectedSampleResults;

        public UnifiBrowserForm()
        {
            InitializeComponent();

            connectButton.Click += connectButton_Click;
            openButton.Click += openButton_Click;
            cancelButton.Click += cancelButton_Click;

            _nodeImages = treeViewImageList;
            FileTree.ImageList = _nodeImages;

            FolderViewList.LargeImageList = FolderViewList.SmallImageList = treeViewImageList;
            FolderViewList.Columns.Remove(SourceSize);

            _httpClient = new HttpClient();

            openButton.Enabled = false;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            serverLocationTextBox.Text = "unifiapi.waters.com:50034";
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
                    _nodeById[id].Tag = "folder";
                }
            }
        }

        void connectButton_Click(object sender, EventArgs e)
        {
            try
            {
                while (true)
                {
                    var loginForm = new LoginForm() { StartPosition = FormStartPosition.CenterParent };
                    if (loginForm.ShowDialog(this) == DialogResult.Cancel)
                        return;
                    _username = loginForm.usernameTextBox.Text;
                    _password = loginForm.passwordTextBox.Text;

                    TokenClient client = new TokenClient(TokenEndpoint, "resourceownerclient", "secret");
                    TokenResponse response = client.RequestResourceOwnerPasswordAsync(_username, _password, "unifi").Result;
                    if (response.IsError)
                    {
                        if (MessageBox.Show(this, "Username or password are not correct!", "Error", MessageBoxButtons.RetryCancel) == DialogResult.Cancel)
                            return;
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(this, "Server is not available", "Login Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            string url = serverLocationTextBox.Text;
            if (!url.StartsWith("http"))
                url = "https://" + url;
            if (!url.EndsWith(BasePath))
                url += BasePath;
            serverLocationTextBox.Text = url;
            serverLocationTextBox.ReadOnly = true;

            FileTree.Nodes.Clear();
            FileTree.Nodes.Add("host", new Uri(url).Host, 0);
            GetFolders();
            FileTree.ExpandAll();
        }

        void openButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;

            SelectedSampleResults = FolderViewList.SelectedItems.Cast<ListViewItem>().Select(o => serverLocationTextBox.Text.Replace("://", String.Format("://{0}:{1}@", _username, _password)) + (o.Tag as string));
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

                    FolderViewList.Items.Add(new ListViewItem(new string[] { name, type, created }, 2) { Tag = String.Format("/sampleresults({0})", id) });
                }
            }
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
    }
}
