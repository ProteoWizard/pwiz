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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    public static class PanoramaUtil
    {
        public const string PANORAMA_WEB = "https://panoramaweb.org/"; // Not L10N
        public const string FORM_POST = "POST"; // Not L10N

        public static Uri ServerNameToUri(string serverName)
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

        private static string ServerNameToUrl(string serverName)
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

        public static void VerifyServerInformation(IPanoramaClient panoramaClient, string username, string password)
        {
            var uriServer = panoramaClient.ServerUri;

            switch (panoramaClient.GetServerState())
            {
                case ServerState.missing:
                    throw new PanoramaServerException(string.Format(Resources.EditServerDlg_VerifyServerInformation_The_server__0__does_not_exist, uriServer.Host));
                case ServerState.unknown:
                    throw new PanoramaServerException(string.Format(Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__, uriServer.Host));
            }
            switch (panoramaClient.IsPanorama())
            {
                case PanoramaState.other:
                    throw new PanoramaServerException(string.Format(Resources.EditServerDlg_OkDialog_The_server__0__is_not_a_Panorama_server, uriServer.Host));
                case PanoramaState.unknown:
                    throw new PanoramaServerException(string.Format(Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__, uriServer.Host));
            }

            switch (panoramaClient.IsValidUser(username, password))
            {
                case UserState.nonvalid:
                    throw new PanoramaServerException(Resources.EditServerDlg_OkDialog_The_username_and_password_could_not_be_authenticated_with_the_panorama_server);
                case UserState.unknown:
                    throw new PanoramaServerException(string.Format(Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__, uriServer.Host));
            }
        }

        public static void VerifyFolder(IPanoramaClient panoramaClient, Server server, string panoramaFolder)
        {
            switch (panoramaClient.IsValidFolder(panoramaFolder, server.Username, server.Password))
            {
                case FolderState.notfound:
                    throw new PanoramaServerException(
                        string.Format(
                            Resources.PanoramaUtil_VerifyFolder_Folder__0__does_not_exist_on_the_Panorama_server__1_,
                            panoramaFolder, panoramaClient.ServerUri));
                case FolderState.nopermission:
                    throw new PanoramaServerException(string.Format(
                        Resources.PanoramaUtil_VerifyFolder_User__0__does_not_have_permissions_to_upload_to_the_Panorama_folder__1_,
                        server.Username, panoramaFolder));
                case FolderState.notpanorama:
                    throw new PanoramaServerException(string.Format(
                        Resources.PanoramaUtil_VerifyFolder__0__is_not_a_Panorama_folder,
                        panoramaFolder));
            }
        }

        /// <summary>
        /// Parses the JSON returned from the getContainers LabKey API to look for the folder type and active modules in a container.
        /// </summary>
        /// <param name="folderJson"></param>
        /// <returns>True if the folder is a Targeted MS folder.</returns>
        public static bool CheckFolderType(JToken folderJson)
        {
            if (folderJson != null)
            {

                var folderType = (string)folderJson["folderType"]; // Not L10N
                var modules = folderJson["activeModules"]; // Not L10N
                return modules != null && ContainsTargetedMSModule(modules) &&
                       Equals("Targeted MS", folderType); // Not L10N
            }
            return false;
        }

        /// <summary>
        /// Parses the JSON returned from the getContainers LabKey API to look user permissions in the container.
        /// </summary>
        /// <param name="folderJson"></param>
        /// <returns>True if the user has insert permissions.</returns>
        public static bool CheckFolderPermissions(JToken folderJson)
        {
            if (folderJson != null)
            {
                var userPermissions = folderJson.Value<int?>("userPermissions"); // Not L10N
                return userPermissions != null && Equals(userPermissions & 2, 2);
            }
            return false;
        }

        private static bool ContainsTargetedMSModule(IEnumerable<JToken> modules)
        {
            foreach (var module in modules)
            {
                if (string.Equals(module.ToString(), "TargetedMS")) // Not L10N
                    return true;
            }
            return false;
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, bool isApi = false)
        {
            return Call(serverUri, controller, folderPath, method, null, isApi);
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, string query, bool isApi = false)
        {
            string path = "labkey/" + controller + "/" + (folderPath ?? string.Empty) + "/" + // Not L10N
                method + (isApi ? ".api" : ".view"); // Not L10N

            if (string.IsNullOrEmpty(query))
            {
                return new UriBuilder(serverUri.Scheme, serverUri.Host, serverUri.Port, path).Uri;
            }
            else
            {
                return new UriBuilder(serverUri.Scheme, serverUri.Host, serverUri.Port, path, "?" + query).Uri; // Not L10N   
            }
        }

        public static Uri GetContainersUri(Uri serverUri, string folder, bool includeSubfolders)
        {
            var queryString = string.Format("includeSubfolders={0}&moduleProperties=TargetedMS", includeSubfolders ? "true" : "false"); // Not L10N
            return Call(serverUri, "project", folder, "getContainers", queryString); // Not L10N
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
                return GetBasicAuthHeader(Username, Password);
            }
        }

        internal static string GetBasicAuthHeader(string username, string password)
        {
            byte[] authBytes = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", username, password)); // Not L10N
            var authHeader = "Basic " + Convert.ToBase64String(authBytes); // Not L10N
            return authHeader;   
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
            return obj is Server && Equals((Server)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Username != null ? Username.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Password != null ? Password.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (URI != null ? URI.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }

    public enum ServerState { unknown, missing, available }
    public enum PanoramaState { panorama, other, unknown }
    public enum UserState { valid, nonvalid, unknown }
    public enum FolderState { valid, notpanorama, nopermission, notfound }

    public interface IPanoramaClient
    {
        Uri ServerUri { get; }
        ServerState GetServerState();
        PanoramaState IsPanorama();
        UserState IsValidUser(string username, string password);
        FolderState IsValidFolder(string folderPath, string username, string password);
    }

    class WebPanoramaClient : IPanoramaClient
    {
        public Uri ServerUri { get; private set; }

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
                    string response = webClient.UploadString(uri, PanoramaUtil.FORM_POST, string.Empty); // Not L10N
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
                Uri uri = new Uri(ServerUri, "/labkey/security/home/ensureLogin.view"); // Not L10N

                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
                    // If credentials are not valid, will return a 401 error.
                    webClient.UploadString(uri, PanoramaUtil.FORM_POST, string.Empty); // Not L10N
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

        public FolderState IsValidFolder(string folderPath, string username, string password)
        {
           try
            {
                var uri = PanoramaUtil.GetContainersUri(ServerUri, folderPath, false);

                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
                    var folderInfo = webClient.UploadString(uri, PanoramaUtil.FORM_POST, string.Empty); // Not L10N
                    JToken response = JObject.Parse(folderInfo);

                    // User needs write permissions to publish to the folder
                    if (!PanoramaUtil.CheckFolderPermissions(response))
                    {
                        return FolderState.nopermission;
                    }

                    // User can only upload to a TargetedMS folder type.
                    if (!PanoramaUtil.CheckFolderType(response))
                    {
                        return FolderState.notpanorama;
                    }
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return FolderState.notfound;
                }
                else throw;
            }
           return FolderState.valid;
        }
    }

    public interface IPanoramaPublishClient
    {
        JToken GetInfoForFolders(Server server);
        void SendZipFile(Server server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor);
        JObject SupportedVersionsJson(Server server);
    }

    class WebPanoramaPublishClient : IPanoramaPublishClient
    {
        private WebClient _webClient;
        private IProgressMonitor _progressMonitor;
        private ProgressStatus _progressStatus;

       
        public JToken GetInfoForFolders(Server server)
        {
            // Retrieve folders from server.
            Uri uri = PanoramaUtil.GetContainersUri(server.URI, null, true); // Not L10N

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add(HttpRequestHeader.Authorization, server.AuthHeader);
                string folderInfo = webClient.UploadString(uri, PanoramaUtil.FORM_POST, string.Empty);
                return JObject.Parse(folderInfo);
            }
        }

        public void SendZipFile(Server server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
        {
            _progressMonitor = progressMonitor;
            _progressStatus = new ProgressStatus(String.Empty);
            var zipFileName = Path.GetFileName(zipFilePath) ?? string.Empty;

            // Upload zip file to pipeline folder.
            using (_webClient = new NonStreamBufferingWebClient())
            {
                _webClient.UploadProgressChanged += webClient_UploadProgressChanged;
                _webClient.UploadFileCompleted += webClient_UploadFileCompleted;

                _webClient.Headers.Add(HttpRequestHeader.Authorization, server.AuthHeader);
                var webDav = PanoramaUtil.Call(server.URI, "pipeline", folderPath, "getPipelineContainer", true); // Not L10N
                var webDavInfo = _webClient.UploadString(webDav, PanoramaUtil.FORM_POST, string.Empty);
                JObject jsonWebDavInfo = JObject.Parse(webDavInfo);

                string webDavUrl = (string)jsonWebDavInfo["webDavURL"]; // Not L10N

                // Upload Url minus the name of the zip file.
                var baseUploadUri = new Uri(server.URI, webDavUrl);
                // Use Uri.EscapeDataString instead of Uri.EscapleUriString.  
                // The latter will not escape characters such as '+' or '#' 
                var escapedZipFileName = Uri.EscapeDataString(zipFileName);
                var tmpUploadUri = new Uri(baseUploadUri, escapedZipFileName + ".part"); // Not L10N
                var uploadUri = new Uri(baseUploadUri, escapedZipFileName);

                lock (this)
                {
                    // Write to a temp file first. This will be renamed after a successful upload or deleted if the upload is canceled.
                    // Add a "Temporary" header so that LabKey marks this as a temporary file.
                    // https://www.labkey.org/issues/home/Developer/issues/details.view?issueId=19220
                    _webClient.Headers.Add("Temporary", "T"); // Not L10N
                    _webClient.UploadFileAsync(tmpUploadUri, "PUT", zipFilePath); // Not L10N

                    // Wait for the upload to complete
                    Monitor.Wait(this);
                }

                if (progressMonitor.IsCanceled)
                {
                    // Delete the temporary file on the server
                    progressMonitor.UpdateProgress(
                        _progressStatus =
                            _progressStatus.ChangeMessage(
                                Resources.WebPanoramaPublishClient_SendZipFile_Deleting_temporary_file_on_server));
                    DeleteTempZipFile(tmpUploadUri, server.AuthHeader);
                    return;
                }
                // Make sure the temporary file was uploaded to the server
                ConfirmFileOnServer(tmpUploadUri, server.AuthHeader);

                // Rename the temporary file
                _progressStatus = _progressStatus.ChangeMessage(
                    Resources.WebPanoramaPublishClient_SendZipFile_Renaming_temporary_file_on_server);
                progressMonitor.UpdateProgress(_progressStatus);

                RenameTempZipFile(tmpUploadUri, uploadUri, server.AuthHeader);

                _progressStatus = _progressStatus.ChangeMessage(Resources.WebPanoramaPublishClient_SendZipFile_Waiting_for_data_import_completion___);
                progressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(-1));

                // Data must be completely uploaded before we can import.
                Uri importUrl = PanoramaUtil.Call(server.URI, "targetedms", folderPath, "skylineDocUploadApi"); // Not L10N

                // Need to tell server which uploaded file to import.
                var dataImportInformation = new NameValueCollection
                                                {
                                                    // For now, we only have one root that user can upload to
                                                    {"path", "./"}, // Not L10N 
                                                    {"file", zipFileName} // Not L10N
                                                };
                byte[] responseBytes = _webClient.UploadValues(importUrl, PanoramaUtil.FORM_POST, dataImportInformation); // Not L10N
                string response = Encoding.UTF8.GetString(responseBytes);
                JToken importResponse = JObject.Parse(response);

                // ID to check import status.
                var details = importResponse["UploadedJobDetails"]; // Not L10N
                int rowId = (int)details[0]["RowId"]; // Not L10N
                Uri statusUri = PanoramaUtil.Call(server.URI, "query", folderPath, "selectRows", // Not L10N
                                     "query.queryName=job&schemaName=pipeline&query.rowId~eq=" + rowId); // Not L10N
                bool complete = false;
                // Wait for import to finish before returning.
                while (!complete)
                {
                    if (progressMonitor.IsCanceled)
                        return;

                    string statusResponse = _webClient.UploadString(statusUri, PanoramaUtil.FORM_POST, string.Empty);
                    JToken jStatusResponse = JObject.Parse(statusResponse);
                    JToken rows = jStatusResponse["rows"]; // Not L10N
                    var row = rows.FirstOrDefault(r => (int)r["RowId"] == rowId); // Not L10N
                    if (row == null)
                        continue;

                    string status = (string)row["Status"]; // Not L10N
                    if (string.Equals(status, "ERROR")) // Not L10N
                    {
                        throw new PanoramaImportErrorException(server.URI, (string)row["_labkeyurl_RowId"]); // Not L10N
                    }

                    complete = string.Equals(status, "COMPLETE"); // Not L10N
                }

                progressMonitor.UpdateProgress(_progressStatus.Complete());
            }
        }

        public JObject SupportedVersionsJson(Server server)
        {
            var uri = PanoramaUtil.Call(server.URI, "targetedms", null, "getMaxSupportedVersions"); // Not L10N

            string supportedVersionsJson;

            using (var webClient = new WebClient())
            {
                webClient.Headers.Add(HttpRequestHeader.Authorization, server.AuthHeader);

                try
                {
                    supportedVersionsJson = webClient.DownloadString(uri);
                }
                catch (WebException)
                {
                    // An exception will be thrown if the response code is not 200 (Success).
                    // We may be communicating with an older server that does not understand the request.
                    return null;
                }
            }
            try
            {
                return JObject.Parse(supportedVersionsJson);
            }
            catch (Exception)
            {
                // If there was an error in parsing the JSON.
                return null;
            }
        }

        private class NonStreamBufferingWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);

                var httpWebRequest = request as HttpWebRequest;
                if (httpWebRequest != null)
                {
                    httpWebRequest.Timeout = Timeout.Infinite;
                    httpWebRequest.AllowWriteStreamBuffering = false;
                }
                return request;
            }
        }


        private void RenameTempZipFile(Uri sourceUri, Uri destUri, string authHeader)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri);

            // Destination URI.  
            // NOTE: Do not use Uri.ToString since it does not return the escaped version.
            request.Headers.Add("Destination", destUri.AbsoluteUri); // Not L10N

            // If a file already exists at the destination URI, it will not be overwritten.  
            // The server would return a 412 Precondition Failed status code.
            request.Headers.Add("Overwrite", "F"); // Not L10N

            DoRequest(request,
                "MOVE", // Not L10N
                authHeader,
                Resources.WebPanoramaPublishClient_RenameTempZipFile_Error_renaming_temporary_zip_file__,
                5 // Try up to 5 times.
                );
        }

        private static void DoRequest(HttpWebRequest request, string method, string authHeader, string errorMessage, int maxTryCount = 1)
        {
            request.Method = method;
            request.Headers.Add(HttpRequestHeader.Authorization, authHeader);

            try
            {
                using (request.GetResponse())
                {
                }
            }
            // An exception will be thrown if the response code is not 200 (Success).
            catch (WebException x)
            {
                if (--maxTryCount > 0)
                {
                    DoRequest(request, method, authHeader, errorMessage, maxTryCount);
                }

                var msg = x.Message;
                if (x.InnerException != null)
                {
                    msg += ". Inner Exception: " + x.InnerException.Message; // Not L10N
                }
                throw new Exception(
                    TextUtil.LineSeparate(errorMessage, msg), x);
            }
        }

        private void DeleteTempZipFile(Uri sourceUri, string authHeader)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri.ToString());

            DoRequest(request,
                "DELETE", // Not L10N
                authHeader,
                Resources.WebPanoramaPublishClient_DeleteTempZipFile_Error_deleting_temporary_zip_file__);
        }

        private void ConfirmFileOnServer(Uri sourceUri, string authHeader)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri);

            // Do a HEAD request to check if the file exists on the server.
            DoRequest(request,
                "HEAD", // Not L10N
                authHeader,
                Resources.WebPanoramaPublishClient_ConfirmFileOnServer_File_was_not_uploaded_to_the_server__Please_try_again__or_if_the_problem_persists__please_contact_your_Panorama_server_administrator_
                );
        }

        public void webClient_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)
        {
            var message = string.Format(FileSize.FormatProvider,
                Resources.WebPanoramaPublishClient_webClient_UploadProgressChanged_Uploaded__0_fs__of__1_fs_,
                e.BytesSent, e.TotalBytesToSend);
            _progressStatus = _progressStatus.ChangeMessage(message).ChangePercentComplete(e.ProgressPercentage);
            _progressMonitor.UpdateProgress(_progressStatus);
            if (_progressMonitor.IsCanceled)
                _webClient.CancelAsync();
        }

        private void webClient_UploadFileCompleted(object sender, UploadFileCompletedEventArgs e)
        {
            lock (this)
            {
                Monitor.PulseAll(this);
            }
        }
    }

    public class PanoramaImportErrorException : Exception
    {
        public PanoramaImportErrorException(Uri serverUrl, string jobUrlPart)
        {
            ServerUrl = serverUrl;
            JobUrlPart = jobUrlPart;
        }

        public Uri ServerUrl { get; private set; }
        public string JobUrlPart { get; private set; }
    }

    public class PanoramaServerException : Exception
    {
        public PanoramaServerException(string message) : base(message)
        {
        }
    }
}
