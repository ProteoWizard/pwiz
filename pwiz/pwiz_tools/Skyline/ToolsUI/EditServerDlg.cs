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
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class EditServerDlg : FormEx
    {
        public const string PANORAMA_WEB = "https://panoramaweb.org/"; // Not L10N

        private Server _server;
        private readonly IEnumerable<Server> _existing;

        public IPanoramaClient PanoramaClient { get; set; }

        public EditServerDlg(IEnumerable<Server> existing)
        {
            _existing = existing;
            Icon = Resources.Skyline;
            InitializeComponent();
        }

        public void ShowInstructions()
        {
            InstructionPanel.Visible = true;
        }

        public Server Server
        {
            get { return _server; }
            set
            {
                _server = value;
                if (_server == null)
                {
                    textServerURL.Text = string.Empty;
                    textPassword.Text = string.Empty;
                    textUsername.Text = string.Empty;
                }
                else
                {
                    textServerURL.Text = _server.URI.ToString();
                    textPassword.Text = _server.Password;
                    textUsername.Text = _server.Username;
                    string labelText = lblProjectInfo.Text;
                    if (labelText.Contains(textServerURL.Text))
                        lblProjectInfo.Text = labelText.Substring(0, labelText.IndexOf(' ')) + ':';
                }
            }
        }

        public string URL { get { return textServerURL.Text; } set { textServerURL.Text = value; } }
        public string Username { get { return textUsername.Text; } set { textUsername.Text = value; } }
        public string Password { get { return textPassword.Text; } set { textPassword.Text = value; } }

        public void OkDialog()
        {
            MessageBoxHelper helper = new MessageBoxHelper(this);
            string serverName;
            if (!helper.ValidateNameTextBox(textServerURL, out serverName))
                return;

            Uri uriServer = ServerNameToUri(serverName);
            if (uriServer == null)
            {
                helper.ShowTextBoxError(textServerURL, Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_, serverName);
                return;
            }

            var panoramaClient = PanoramaClient ?? new WebPanoramaClient(uriServer);

            using (var waitDlg = new LongWaitDlg { Text = Resources.EditServerDlg_OkDialog_Verifying_server_information })
            {
                try
                {
                    waitDlg.PerformWork(this, 1000, () => VerifyServerInformation(helper, panoramaClient, uriServer, Username, Password));
                }
                catch (Exception x)
                {
                    helper.ShowTextBoxError(textServerURL, x.Message);
                    return;
                }
            }

            Uri updatedUri = panoramaClient.ServerUri ?? uriServer;

            if (_existing.Contains(server => !ReferenceEquals(_server, server) && Equals(updatedUri, server.URI)))
            {
                helper.ShowTextBoxError(textServerURL, Resources.EditServerDlg_OkDialog_The_server__0__already_exists_, uriServer.Host);
                return;
            }

            _server = new Server(updatedUri, Username, Password);
            DialogResult = DialogResult.OK;
        }

        public Uri ServerNameToUri(string serverName)
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

        public string ServerNameToUrl(string serverName)
        {
            const string https = "https://"; // Not L10N
            const string http = "http://"; // Not L10N
            int length = https.Length;

            var httpsIndex = serverName.IndexOf(https, StringComparison.Ordinal);
            var httpIndex = serverName.IndexOf(http, StringComparison.Ordinal);

            if (httpsIndex == -1 && httpIndex == -1)
            {
                serverName = serverName.Insert(0, https);
            }
            else if (httpsIndex == -1)
            {
                length = http.Length;
            }

            int pathIndex = serverName.IndexOf("/", length, StringComparison.Ordinal); // Not L10N

            if (pathIndex != -1)
                serverName = serverName.Remove(pathIndex);

            return serverName;
        }

        private void VerifyServerInformation(MessageBoxHelper helper, IPanoramaClient panoramaClient, Uri uriServer, string username, string password)
        {
            switch (panoramaClient.GetServerState())
            {
                case ServerState.missing:
                    throw new Exception(string.Format(Resources.EditServerDlg_VerifyServerInformation_The_server__0__does_not_exist, uriServer.Host));
                case ServerState.unknown:
                    throw new Exception(string.Format(Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__, uriServer.Host));
            }
            switch (panoramaClient.IsPanorama())
            {
                case PanoramaState.other:
                    throw new Exception(string.Format(Resources.EditServerDlg_OkDialog_The_server__0__is_not_a_Panorama_server, uriServer.Host));
                case PanoramaState.unknown:
                    throw new Exception(string.Format(Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__, uriServer.Host));
            }

            switch (panoramaClient.IsValidUser(username, password))
            {
                case UserState.nonvalid:
                    throw new Exception(Resources.EditServerDlg_OkDialog_The_username_and_password_could_not_be_authenticated_with_the_panorama_server);
                case UserState.unknown:
                    throw new Exception(string.Format(Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__, uriServer.Host));
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }

    [XmlRoot("server")]
    public sealed class Server : Immutable, IKeyContainer<string>, IXmlSerializable
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
            get
            {
                byte[] authBytes = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", Username, Password)); // Not L10N
                var authHeader = "Basic " + Convert.ToBase64String(authBytes); // Not L10N
                return authHeader;
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private Server()
        {
        }

        private enum ATTR
        {
            username,
            password,
            uri
        }

        public static Server Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new Server());
        }

        private void Validate()
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            Username = reader.GetAttribute(ATTR.username) ?? string.Empty;
            Password = reader.GetAttribute(ATTR.password) ?? string.Empty;
            string uriText = reader.GetAttribute(ATTR.uri);
            if (string.IsNullOrEmpty(uriText))
            {
                throw new InvalidDataException(Resources.Server_ReadXml_A_Panorama_server_must_be_specified);
            }
            try
            {
                URI = new Uri(uriText);
            }
            catch (UriFormatException)
            {
                throw new InvalidDataException(Resources.Server_ReadXml_Server_URL_is_corrupt);
            }
            // Consume tag
            reader.Read();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttributeString(ATTR.username, Username);
            writer.WriteAttributeString(ATTR.password, Password);
            writer.WriteAttribute(ATTR.uri, URI);
        }
        #endregion

        #region object overrides

        private bool Equals(Server other)
        {
            return string.Equals(Username, other.Username) &&
                string.Equals(Password, other.Password) &&
                Equals(URI, other.URI);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Server && Equals((Server) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Username != null ? Username.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Password != null ? Password.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (URI != null ? URI.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }

    public enum ServerState { unknown, missing, available }
    public enum PanoramaState { panorama, other, unknown }
    public enum UserState { valid, nonvalid, unknown }

    public interface IPanoramaClient
    {
        Uri ServerUri { get; }
        ServerState GetServerState();
        PanoramaState IsPanorama();
        UserState IsValidUser(string username, string password);
    }

    class WebPanoramaClient: IPanoramaClient
    {
        public Uri ServerUri { get;  private set;}

        public WebPanoramaClient(Uri server)
        {
            ServerUri = server;
        }

        public ServerState GetServerState()
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.DownloadString(ServerUri);
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
                    if (TryNewProtocol(() => GetServerState() == ServerState.available))
                        return ServerState.available;

                    return ServerState.unknown;
                }
            }
        }

        // This function must be true/false returning; no exceptions can be thrown
        private bool TryNewProtocol(Func<bool> testFunc)
        {
            Uri currentUri = ServerUri;

            // try again using https
            if (!ServerUri.AbsoluteUri.StartsWith("https")) // Not L10N
            {
                ServerUri = new Uri(currentUri.AbsoluteUri.Replace("http", "https")); // Not L10N
                return testFunc();
            }

            ServerUri = currentUri;
            return false;
        }

        public PanoramaState IsPanorama()
        {
            try
            {
                Uri uri = new Uri(ServerUri, "/labkey/project/home/getContainers.view"); // Not L10N
                using (var webClient = new WebClient())
                {
                    string response = webClient.UploadString(uri, "POST", string.Empty); // Not L10N
                    JObject jsonResponse = JObject.Parse(response);
                    string type = (string)jsonResponse["type"]; // Not L10N
                    if (string.Equals(type, "project")) // Not L10N
                    {
                        return PanoramaState.panorama;
                    }
                    else
                    {
                        return PanoramaState.other;
                    }
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                // Labkey container page should be part of all Panorama servers. 
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return PanoramaState.other;
                }
                else
                {
                    if (TryNewProtocol(() => IsPanorama() == PanoramaState.panorama))
                        return PanoramaState.panorama;

                    return PanoramaState.unknown;
                }
            }
            catch
            {
                return PanoramaState.unknown;
            }
        }

        public UserState IsValidUser(string username, string password)
        {
            try
            {
                byte[] authBytes = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", username, password)); // Not L10N
                var authHeader = "Basic " + Convert.ToBase64String(authBytes); // Not L10N

                Uri uri = new Uri(ServerUri, "/labkey/security/home/ensureLogin.view"); // Not L10N

                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers.Add(HttpRequestHeader.Authorization, authHeader);
                    // If credentials are not valid, will return a 401 error.
                    webClient.UploadString(uri, "POST", string.Empty); // Not L10N
                    return UserState.valid;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                // Labkey container page should be part of all Panorama servers. 
                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return UserState.nonvalid;
                }
                else
                {
                    return UserState.unknown;
                }
            }
        }
    }
}
