/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditServerDlg : FormEx
    {
        private Server _server;
        private readonly IEnumerable<Server> _existing;

        public IPanoramaClient PanoramaClient { get; set; }

        public EditServerDlg(IEnumerable<Server> existing)
        {
            _existing = existing;
            InitializeComponent();
        }

        public Server Server
        {
            get { return _server; }
            set
            {
                _server = value;
                if (_server == null)
                {
                    textServerName.Text = "";
                    textPassword.Text = "";
                    textUsername.Text = "";
                }
                else
                {
                    textServerName.Text = _server.Name;
                    textPassword.Text = _server.Password;
                    textUsername.Text = _server.Username;
                }
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            string serverName;
            if (!helper.ValidateNameTextBox(new CancelEventArgs(), textServerName, out serverName))
                return;

            var uriServer = ServerNameToUri(serverName);
            if (uriServer == null)
            {
                helper.ShowTextBoxError(textServerName, "The text '{0}' is not a valid server name.", serverName);
                return;
            }

            if ((_server == null || !Equals(uriServer, _server.Name)) && _existing.Any(e => 
                Equals(e.Name, uriServer.Host)))
            {
                helper.ShowTextBoxError(textServerName, "The server '{0}' already exists.", uriServer.Host);
                return;
            }

            
            var panoramaClient = PanoramaClient;
            if (panoramaClient == null)
                panoramaClient = new WebPanoramaClient(uriServer);

            switch (panoramaClient.GetServerState())
            {
                case ServerState.missing:
                    helper.ShowTextBoxError(textServerName, "The server {0} does not exist.", uriServer.Host);
                    return;
                case ServerState.unknown:
                    helper.ShowTextBoxError(textServerName, "Unknown error connecting to the server {0}.", uriServer.Host);
                    return;
            }
            if (!panoramaClient.IsPanorama())
            {
                helper.ShowTextBoxError(textServerName, "The server {0} is not a Panorama server.", uriServer.Host);
                return;
            }
            string username = textUsername.Text;
            string password = textPassword.Text;
            if (!panoramaClient.IsValidUser(username, password))
            {
                helper.ShowTextBoxError(textUsername, "The username and password could not be authenticated with the panorama server.");
                return;
            }

            _server = new Server(uriServer.Host, textUsername.Text, textPassword.Text);
            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private Uri ServerNameToUri(string serverName)
        {
            try
            {
                return new Uri(ServerNameToUrl(serverName));
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        private string ServerNameToUrl(string serverName)
        {
            const string https = "https://";
            const string http = "http://";
            int length = http.Length;

            var httpsIndex = serverName.IndexOf(https, StringComparison.Ordinal);
            var httpIndex = serverName.IndexOf(http, StringComparison.Ordinal);

            if (httpsIndex == -1 && httpIndex == -1)
            {
                serverName = serverName.Insert(0, http);
            }
            else if (httpIndex == -1)
            {
                length = https.Length;
            }

            int pathIndex = serverName.IndexOf("/", length, StringComparison.Ordinal);

            if (pathIndex != -1)
                serverName = serverName.Remove(pathIndex);

            return serverName;
        }
    }

    [XmlRoot("server")]
    public sealed class Server : XmlNamedElement
    {
        public Server(string name, string username, string password)
            : base(name)
        {
            Username = username;
            Password = password;
        }

        internal string Username { get; set; }
        internal string Password { get; set; }
    }

    public enum ServerState { unknown, missing, available }

    public interface IPanoramaClient
    {
        ServerState GetServerState();
        bool IsPanorama();
        bool IsValidUser(string username, string password);
    }

    class WebPanoramaClient: IPanoramaClient
    {
        private readonly Uri _server;

        public WebPanoramaClient(Uri server)
        {
            _server = server;
        }

        public ServerState GetServerState()
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.DownloadString(_server);
                    return ServerState.available;
                }
            }
            catch (WebException ex)
            {
                // Invalid URL
                if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    return ServerState.missing;
                }
                else
                {
                    return ServerState.unknown;
                }
            }
        }

        public bool IsPanorama()
        {
            try
            {
                Uri uri = new Uri(_server, "/labkey/project/home/getContainers.view");
                using (var webClient = new WebClient())
                {
                    string response = webClient.UploadString(uri, "POST", "");
                    JObject jsonResponse = JObject.Parse(response);
                    string type = (string) jsonResponse["type"];
                    return String.Equals(type, "project");
                }
            }
            catch (WebException)
            { 
                return false;
            }
        }

        public bool IsValidUser(string username, string password)
        {
            try
            {
                byte[] authBytes = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", username, password));
                var authHeader = "Basic " + Convert.ToBase64String(authBytes);

                Uri uri = new Uri(_server, "/labkey/security/home/ensureLogin.view");

                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers.Add(HttpRequestHeader.Authorization, authHeader);
                    // If credentials are not valid, will return a 401 error.
                    webClient.UploadString(uri, "POST", "");
                    return true;
                }
            }
            catch (WebException)
            {
                return false;
            }
        }
    }

    
}
