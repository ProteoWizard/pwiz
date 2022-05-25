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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    public static class PanoramaUtil
    {
        public const string PANORAMA_WEB = "https://panoramaweb.org/";
        public const string FORM_POST = "POST";
        public const string LABKEY_CTX = "/labkey/";
        public const string ENSURE_LOGIN_PATH = "security/home/ensureLogin.view";

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
            const string https = "https://";
            const string http = "http://";

            var httpsIndex = serverName.IndexOf(https, StringComparison.Ordinal);
            var httpIndex = serverName.IndexOf(http, StringComparison.Ordinal);

            if (httpsIndex == -1 && httpIndex == -1)
            {
                serverName = serverName.Insert(0, https);
            }

            return serverName;
        }

        public static void VerifyServerInformation(IPanoramaClient panoramaClient, string username, string password)
        {
            var uriServer = panoramaClient.ServerUri;

            switch (panoramaClient.GetServerState())
            {
                case ServerState.missing:
                    throw new PanoramaServerException(string.Format(
                        Resources.EditServerDlg_VerifyServerInformation_The_server__0__does_not_exist,
                        uriServer.AbsoluteUri));
                case ServerState.unknown:
                    throw new PanoramaServerException(string.Format(
                        Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__,
                        uriServer.AbsoluteUri));
            }

            switch (panoramaClient.IsValidUser(username, password))
            {
                case UserState.nonvalid:
                    throw new PanoramaServerException(Resources
                        .EditServerDlg_OkDialog_The_username_and_password_could_not_be_authenticated_with_the_panorama_server);
                case UserState.unknown:
                    throw new PanoramaServerException(string.Format(
                        Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__,
                        uriServer.AbsoluteUri));
            }

            switch (panoramaClient.IsPanorama())
            {
                case PanoramaState.other:
                    throw new PanoramaServerException(string.Format(
                        Resources.EditServerDlg_OkDialog_The_server__0__is_not_a_Panorama_server,
                        uriServer.AbsoluteUri));
                case PanoramaState.unknown:
                    throw new PanoramaServerException(string.Format(
                        Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__,
                        uriServer.AbsoluteUri));
            }
        }

        public static UserState ValidateServerAndUser(ref Uri serverUri, string username, string password)
        {
            var pServer = new PanoramaServer(serverUri, username, password);

            try
            {
                var userState = EnsureLogin(pServer);
                serverUri = pServer.ServerUri;
                return userState;
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.NotFound) // 404
                {
                    if (pServer.AddLabKeyContextPath())
                    {
                        // e.g. Given server URL is https://panoramaweb.org but LabKey Server is not deployed as the root webapp.
                        // Try again with '/labkey' context path
                        return TryEnsureLogin(pServer, ref serverUri);
                    }
                    else if (pServer.RemoveContextPath())
                    {
                        // e.g. User entered the home page of the LabKey Server, running as the root webapp: 
                        // https://panoramaweb.org/project/home/begin.view OR https://panoramaweb.org/home/project-begin.view
                        // We will first try https://panoramaweb.org/project/ OR https://panoramaweb.org/home/ as the server URL. 
                        // And that will fail.  Remove the assumed context path and try again.
                        return TryEnsureLogin(pServer, ref serverUri);
                    }
                }

                return UserState.unknown;
            }
        }

        private static UserState EnsureLogin(PanoramaServer pServer)
        {
            var requestUri = new Uri(pServer.ServerUri, ENSURE_LOGIN_PATH);
            var request = (HttpWebRequest) WebRequest.Create(requestUri);
            request.Headers.Add(HttpRequestHeader.Authorization,
                Server.GetBasicAuthHeader(pServer.Username, pServer.Password));
            try
            {
                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK ? UserState.valid : UserState.unknown;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized) // 401
                {
                    var responseUri = response.ResponseUri;
                    if (!requestUri.Equals(responseUri))
                    {
                        // This means we were redirected.  Authorization headers are not persisted across redirects. Try again
                        // with the responseUri.
                        if (pServer.Redirect(responseUri.AbsoluteUri, ENSURE_LOGIN_PATH))
                        {
                            return EnsureLogin(pServer);
                        }
                    }

                    return UserState.nonvalid; // User cannot be authenticated
                }

                throw;
            }
        }

        private static UserState TryEnsureLogin(PanoramaServer pServer, ref Uri serverUri)
        {
            try
            {
                var userState = EnsureLogin(pServer);
                serverUri = pServer.ServerUri;
                return userState;
            }
            catch (WebException)
            {
                // Due to anything other than 401 (Unauthorized), which is handled in EnsureLogin.
                return UserState.unknown;
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
                        Resources
                            .PanoramaUtil_VerifyFolder_User__0__does_not_have_permissions_to_upload_to_the_Panorama_folder__1_,
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

                var folderType = (string) folderJson[@"folderType"];
                var modules = folderJson[@"activeModules"];
                return modules != null && ContainsTargetedMSModule(modules) &&
                       Equals(@"Targeted MS", folderType);
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
                var userPermissions = folderJson.Value<int?>(@"userPermissions");
                return userPermissions != null && Equals(userPermissions & 2, 2);
            }

            return false;
        }

        private static bool ContainsTargetedMSModule(IEnumerable<JToken> modules)
        {
            foreach (var module in modules)
            {
                if (string.Equals(module.ToString(), @"TargetedMS"))
                    return true;
            }

            return false;
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, bool isApi = false)
        {
            return Call(serverUri, controller, folderPath, method, null, isApi);
        }

        public static Uri Call(Uri serverUri, string controller, string folderPath, string method, string query,
            bool isApi = false)
        {
            string path = controller + @"/" + (folderPath ?? string.Empty) + @"/" +
                          method + (isApi ? @".api" : @".view");

            if (!string.IsNullOrEmpty(query))
            {
                path = path + @"?" + query;
            }

            return new Uri(serverUri, path);
        }

        public static Uri CallNewInterface(Uri serverUri, string controller, string folderPath, string method,
            string query,
            bool isApi = false)
        {
            string apiString = isApi ? @"api" : @"view";
            string queryString = string.IsNullOrEmpty(query) ? "" : @"?" + query;
            string path = $@"{folderPath}/{controller}-{method}.{apiString}{queryString}";

            return new Uri(serverUri, path);
        }

        public static Uri GetContainersUri(Uri serverUri, string folder, bool includeSubfolders)
        {
            var queryString = string.Format(@"includeSubfolders={0}&moduleProperties=TargetedMS",
                includeSubfolders ? @"true" : @"false");
            return Call(serverUri, @"project", folder, @"getContainers", queryString);
        }

        public static IPanoramaClient CreatePanoramaClient(Uri serverUri)
        { return new WebPanoramaClient(serverUri);}
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
            byte[] authBytes = Encoding.UTF8.GetBytes(String.Format(@"{0}:{1}", username, password));
            var authHeader = @"Basic " + Convert.ToBase64String(authBytes);
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
            password_encrypted,
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
            string encryptedPassword = reader.GetAttribute(ATTR.password_encrypted);
            if (encryptedPassword != null)
            {
                try
                {
                    Password = TextUtil.DecryptString(encryptedPassword);
                }
                catch (Exception)
                {
                    Password = string.Empty;
                }
            }
            else
            {
                Password = reader.GetAttribute(ATTR.password) ?? string.Empty;
            }
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
            if (!string.IsNullOrEmpty(Password))
            {
                writer.WriteAttributeString(ATTR.password_encrypted, TextUtil.EncryptString(Password));
            }
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
    public enum FolderOperationStatus{OK, notpanorama, nopermission, notfound, alreadyexists, error}

    public interface IPanoramaClient
    {
        Uri ServerUri { get; }
        ServerState GetServerState();
        PanoramaState IsPanorama();
        UserState IsValidUser(string username, string password);
        FolderState IsValidFolder(string folderPath, string username, string password);

        /**
         * Returns FolderOperationStatus.OK if created successfully, otherwise returns the reason
         * why the folder was not created.
         */
        FolderOperationStatus CreateFolder(string parentPath, string folderName, string username, string password);
        /**
         * Returns FolderOperationStatus.OK if the folder was successfully deleted, otherwise returns the reason
         * why the folder was not deleted.
         */
        FolderOperationStatus DeleteFolder(string folderPath, string username, string password);
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
            return TryGetServerState();
        }

        private ServerState TryGetServerState(bool tryNewProtocol = true)
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
                else if (tryNewProtocol)
                {
                    if (TryNewProtocol(() => TryGetServerState(false) == ServerState.available))
                        return ServerState.available;

                    return ServerState.unknown;
                }
            }
            return ServerState.unknown;
        }

        // This function must be true/false returning; no exceptions can be thrown
        private bool TryNewProtocol(Func<bool> testFunc)
        {
            Uri currentUri = ServerUri;
            
            // try again using https
            if (ServerUri.Scheme.Equals(@"http"))
            {
                ServerUri = new Uri(currentUri.AbsoluteUri.Replace(@"http", @"https"));
                return testFunc();
            }
            // We assume "https" (PanoramaUtil.ServerNameToUrl) if there is no scheme in the user provided URL.
            // Try http. LabKey Server may not be running under SSL. 
            else if (ServerUri.Scheme.Equals(@"https"))
            {
                ServerUri = new Uri(currentUri.AbsoluteUri.Replace(@"https", @"http"));
                return testFunc();
            }

            ServerUri = currentUri;
            return false;
        }

        public PanoramaState IsPanorama()
        {
            return TryIsPanorama();
        }

        private PanoramaState TryIsPanorama(bool tryNewProtocol = true)
        {
            try
            {
                // Use the LabKey AdminController.HealthCheckAction instead of ProjectController.GetContainersAction which does not return the expected
                // JSON key if the "Home" container on the LabKey Server is not public.
                // (https://www.labkey.org/home/Developer/issues/Secure/issues-details.view?issueId=20686)
                Uri uri = new Uri(ServerUri, @"admin/home/healthCheck.view");
                using (var webClient = new UTF8WebClient())
                {
                    JObject jsonResponse = webClient.Get(uri);
                    var panoramaState = jsonResponse.ContainsKey(@"healthy")
                        ? PanoramaState.panorama
                        : PanoramaState.other;
                    return panoramaState;
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
                else if(tryNewProtocol)
                {
                    if (TryNewProtocol(() => TryIsPanorama(false) == PanoramaState.panorama))
                        return PanoramaState.panorama;
                }
            }
            catch
            {
                return PanoramaState.unknown;
            }

            return PanoramaState.unknown;  
        }

        public UserState IsValidUser(string username, string password)
        {
            var refServerUri = ServerUri;
            var userState = PanoramaUtil.ValidateServerAndUser(ref refServerUri, username, password);
            if (userState == UserState.valid)
            {
                ServerUri = refServerUri;
            }
            return userState;
        }

        public FolderState IsValidFolder(string folderPath, string username, string password)
        {
           try
            {
                var uri = PanoramaUtil.GetContainersUri(ServerUri, folderPath, false);

                using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
                {
                    JToken response = webClient.Get(uri);

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

        public FolderOperationStatus CreateFolder(string folderPath, string folderName, string username, string password)
        {

            if (IsValidFolder($@"{folderPath}/{folderName}", username, password) == FolderState.valid)
                return FolderOperationStatus.alreadyexists;        //cannot create a folder with the same name
            var parentFolderStatus = IsValidFolder(folderPath, username, password);
            switch (parentFolderStatus)
            {
                case FolderState.nopermission:
                    return FolderOperationStatus.nopermission;
                case FolderState.notfound:
                    return FolderOperationStatus.notfound;
                case FolderState.notpanorama:
                    return FolderOperationStatus.notpanorama;
            }

            //Create JSON body for the request
            Dictionary<string, string> requestData = new Dictionary<string, string>();
            requestData[@"name"] = folderName;
            requestData[@"title"] = folderName;
            requestData[@"description"] = folderName;
            requestData[@"type"] = @"normal";
            requestData[@"folderType"] = @"Targeted MS";
            string createRequest = JsonConvert.SerializeObject(requestData);

            try { 
                using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
                {
                    Uri requestUri = PanoramaUtil.CallNewInterface(ServerUri, @"core", folderPath, @"createContainer", "", true);
                    JObject result = webClient.Post(requestUri, createRequest);
                    return FolderOperationStatus.OK;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode != HttpStatusCode.OK)
                {
                    return FolderOperationStatus.error;
                }
                else throw;
            }
        }

        public FolderOperationStatus DeleteFolder(string folderPath, string username, string password)
        {
            var parentFolderStatus = IsValidFolder(folderPath, username, password);
            switch (parentFolderStatus)
            {
                case FolderState.nopermission:
                    return FolderOperationStatus.nopermission;
                case FolderState.notfound:
                    return FolderOperationStatus.notfound;
                case FolderState.notpanorama:
                    return FolderOperationStatus.notpanorama;
            }

            try
            {
                using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
                {
                    Uri requestUri = PanoramaUtil.CallNewInterface(ServerUri, @"core", folderPath, @"deleteContainer", "", true);
                    JObject result = webClient.Post(requestUri, "");
                    return FolderOperationStatus.OK;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode != HttpStatusCode.OK)
                {
                    return FolderOperationStatus.error;
                }
                else throw;
            }
        }

    }

    public interface IPanoramaPublishClient
    {
        JToken GetInfoForFolders(Server server, string folder);
        Uri SendZipFile(Server server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor);
        JObject SupportedVersionsJson(Server server);
        void UploadSharedZipFile(Control parent, Server server, string zipFilePath, string folderPath);
        ShareType DecideShareTypeVersion(FolderInformation folderInfo, SrmDocument document, ShareType shareType);
        ShareType GetShareType(FolderInformation folderInfo, SrmDocument document, DocumentFormat? fileFormatOnDisk,
            Control parent, ref bool cancelled);

        Uri UploadedDocumentUri { get; }
    }

    public abstract class AbstractPanoramaPublishClient : IPanoramaPublishClient
    {
        public abstract JToken GetInfoForFolders(Server server, string folder);
        public abstract Uri SendZipFile(Server server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor);
        public abstract JObject SupportedVersionsJson(Server server);

        private Uri _uploadedDocumentUri;

        public Uri UploadedDocumentUri
        {
            get { return _uploadedDocumentUri; }
        }

        public CacheFormatVersion GetSupportedSkydVersion(FolderInformation folderInfo)
        {
            var serverVersionsJson = SupportedVersionsJson(folderInfo.Server);
            if (serverVersionsJson == null)
            {
                // There was an error getting the server-supported skyd version for some reason.
                // Perhaps this is an older server that did not understand the request, or
                // the returned JSON was malformed. Let the document upload continue.
                return CacheFormatVersion.CURRENT;
            }

            JToken serverSkydVersion;
            if (serverVersionsJson.TryGetValue(@"SKYD_version", out serverSkydVersion))
            {
                int version;
                if (int.TryParse(serverSkydVersion.Value<string>(), out version))
                {
                    return (CacheFormatVersion) version;
                }
            }

            return CacheFormatVersion.CURRENT;
        }

        public ShareType DecideShareTypeVersion(FolderInformation folderInfo, SrmDocument document, ShareType shareType)
        {
            var cacheVersion = GetDocumentCacheVersion(document);

            if (!cacheVersion.HasValue)
            {
                // The document may not have any chromatogram data.
                return shareType;
            }

            var supportedSkylineVersion = GetSupportedVersionForCacheFormat(folderInfo, cacheVersion);
            CacheFormatVersion supportedVersion = supportedSkylineVersion.CacheFormatVersion;
            if (supportedVersion >= cacheVersion.Value)
            {
                return shareType;
            }
            
            return shareType.ChangeSkylineVersion(supportedSkylineVersion);
        }


        private SkylineVersion GetSupportedVersionForCacheFormat(FolderInformation folderInfo, CacheFormatVersion? cacheVersion)
        {
            var skydVersion = GetSupportedSkydVersion(folderInfo);
            SkylineVersion skylineVersion;
            if (!cacheVersion.HasValue || skydVersion >= cacheVersion)
            {
                // Either the document does not have any chromatograms or the server supports the document's cache version. 
                // Since the cache version does not change when the document is shared, it can be shared as the latest Skyline
                // version even if the cache version associated with that version is higher than what the server supports. 
                // Example scenario:
                // Document cache version is 14; max version supported by server is 14; current Skyline version is associated
                // with cache version 15. In this case the document can be shared as the current Skyline version even though
                // the cache version associated with the current version is higher than what the server supports. When the document
                // is shared the cache format of the document will remain at 14. Only the document format (.sky XML) will change.
                skylineVersion = SkylineVersion.SupportedForSharing().First();
            }
            else
            {
                // The server does not support the document's cache version.
                // Find the highest Skyline version consistent with the cache version supported by the server.
                skylineVersion = SkylineVersion.SupportedForSharing().FirstOrDefault(ver => ver.CacheFormatVersion <= skydVersion);
                if (skylineVersion == null)
                {
                    throw new PanoramaServerException(string.Format(
                        Resources.PublishDocumentDlg_ServerSupportsSkydVersion_, (int)cacheVersion.Value));
                }
            }

            return skylineVersion;
        }

        private static CacheFormatVersion? GetDocumentCacheVersion(SrmDocument document)
        {
            var settings = document.Settings;
            Assume.IsTrue(document.IsLoaded);
            return settings.HasResults ? settings.MeasuredResults.CacheVersion : null;
        }

        public ShareType GetShareType(FolderInformation folderInfo, SrmDocument document, DocumentFormat? fileFormatOnDisk, Control parent, ref bool cancelled)
        {
            var cacheVersion = GetDocumentCacheVersion(document);
            var supportedSkylineVersion = GetSupportedVersionForCacheFormat(folderInfo, cacheVersion);
            
            using (var dlgType = new ShareTypeDlg(document, fileFormatOnDisk, supportedSkylineVersion))
            {
                if (dlgType.ShowDialog(parent) == DialogResult.Cancel)
                {
                    cancelled = true;
                    return null;
                }
                else
                {
                    return dlgType.ShareType;
                }
            }
        }

        public void UploadSharedZipFile(Control parent, Server server, string zipFilePath, string folderPath)
        {
            Uri result = null;

            if (server == null)
                return;
            try
            {
                var isCanceled = false;
                using (var waitDlg = new LongWaitDlg { Text = Resources.PublishDocumentDlg_UploadSharedZipFile_Uploading_File })
                {
                    waitDlg.PerformWork(parent, 1000, longWaitBroker =>
                    {
                        result = SendZipFile(server, folderPath,
                            zipFilePath, longWaitBroker);
                        if (longWaitBroker.IsCanceled)
                            isCanceled = true;
                    });
                }
                if (!isCanceled) // if user not canceled 
                {
                    _uploadedDocumentUri = result;
                    String message = Resources.AbstractPanoramaPublishClient_UploadSharedZipFile_Upload_succeeded__would_you_like_to_view_the_file_in_Panorama_;
                    if (MultiButtonMsgDlg.Show(parent, message, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false)
                        == DialogResult.Yes)
                        Process.Start(result.ToString());
                }
            }
            catch (Exception x)
            {
                var panoramaEx = x.InnerException as PanoramaImportErrorException;
                if (panoramaEx != null)
                {
                    var message = panoramaEx.JobCancelled
                        ? Resources.AbstractPanoramaPublishClient_UploadSharedZipFile_Document_import_was_cancelled_on_the_server__Would_you_like_to_go_to_Panorama_
                        : Resources
                            .AbstractPanoramaPublishClient_UploadSharedZipFile_An_error_occured_while_uploading_to_Panorama__would_you_like_to_go_to_Panorama_;

                    if (MultiButtonMsgDlg.Show(parent, message, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false)
                        == DialogResult.Yes)
                        Process.Start(panoramaEx.JobUrl.ToString());
                }
                else
                {
                    MessageDlg.ShowException(parent, x);
                }
            }

            // Change PanoramaUrl setting to the successful url used
            var uriString = server.GetKey() + folderPath;
            uriString = Uri.EscapeUriString(uriString);
            var window = parent as SkylineWindow;
            if (window != null && Uri.IsWellFormedUriString(uriString, UriKind.Absolute)) // cant do Uri.isWellFormed because of port and ip
            {
                window.ChangeDocPanoramaUri(new Uri(uriString));
            }
        }
    }

    class WebPanoramaPublishClient : AbstractPanoramaPublishClient
    {
        private WebClientWithCredentials _webClient;
        private IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;

        private readonly Regex _runningStatusRegex = new Regex(@"RUNNING, (\d+)%");
        private int _waitTime = 1;

        public void EnsureLogin(Server server)
        {
            var refServerUri = server.URI;
            UserState userState = PanoramaUtil.ValidateServerAndUser(ref refServerUri, server.Username, server.Password);
            if (userState == UserState.valid)
            {
                server.URI = refServerUri;
                return;
            }

            switch (userState)
            {
                case UserState.nonvalid:
                    throw new PanoramaServerException(Resources.EditServerDlg_OkDialog_The_username_and_password_could_not_be_authenticated_with_the_panorama_server);
                case UserState.unknown:
                    throw new PanoramaServerException(string.Format(Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__, refServerUri.AbsoluteUri));
            } 
        }

        public override JToken GetInfoForFolders(Server server, string folder)
        {
            EnsureLogin(server);

            // Retrieve folders from server.
            Uri uri = PanoramaUtil.GetContainersUri(server.URI, folder, true);

            using (var webClient = new WebClientWithCredentials(server.URI, server.Username, server.Password))
            {
                return webClient.Get(uri);
            }
        }

        public override Uri  SendZipFile(Server server, string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
        {
            _progressMonitor = progressMonitor;
            _progressStatus = new ProgressStatus(String.Empty);
            var zipFileName = Path.GetFileName(zipFilePath) ?? string.Empty;

            // Upload zip file to pipeline folder.
            using (_webClient = new NonStreamBufferingWebClient(server.URI, server.Username, server.Password))
            {
                _webClient.UploadProgressChanged += webClient_UploadProgressChanged;
                _webClient.UploadFileCompleted += webClient_UploadFileCompleted;

                var webDav = PanoramaUtil.Call(server.URI, @"pipeline", folderPath, @"getPipelineContainer", true);
                JObject jsonWebDavInfo = _webClient.Get(webDav);

                string webDavUrl = (string)jsonWebDavInfo[@"webDavURL"];

                // Upload Url minus the name of the zip file.
                var baseUploadUri = new Uri(server.URI, webDavUrl); // webDavUrl will start with the context path (e.g. /labkey/...). We will get the correct URL even if serverUri has a context path.
                // Use Uri.EscapeDataString instead of Uri.EscapleUriString.  
                // The latter will not escape characters such as '+' or '#' 
                var escapedZipFileName = Uri.EscapeDataString(zipFileName);
                var tmpUploadUri = new Uri(baseUploadUri, escapedZipFileName + @".part");
                var uploadUri = new Uri(baseUploadUri, escapedZipFileName);

                lock (this)
                {
                    // Write to a temp file first. This will be renamed after a successful upload or deleted if the upload is canceled.
                    // Add a "Temporary" header so that LabKey marks this as a temporary file.
                    // https://www.labkey.org/issues/home/Developer/issues/details.view?issueId=19220
                    _webClient.Headers.Add(@"Temporary", @"T");
                    _webClient.UploadFileAsync(tmpUploadUri, @"PUT", PathEx.SafePath(zipFilePath));

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
                    return null;
                }
                // Remove the "Temporary" header added while uploading the file
                _webClient.Headers.Remove(@"Temporary");

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
                Uri importUrl = PanoramaUtil.Call(server.URI, @"targetedms", folderPath, @"skylineDocUploadApi");

                // Need to tell server which uploaded file to import.
                var dataImportInformation = new NameValueCollection
                                                {
                                                    // For now, we only have one root that user can upload to
                                                    {@"path", @"./"},
                                                    {@"file", zipFileName}
                                                };
                
                JToken importResponse = _webClient.Post(importUrl, dataImportInformation);

                // ID to check import status.
                var details = importResponse[@"UploadedJobDetails"];
                int rowId = (int)details[0][@"RowId"];
                Uri statusUri = PanoramaUtil.Call(server.URI, @"query", folderPath, @"selectRows",
                    @"query.queryName=job&schemaName=pipeline&query.rowId~eq=" + rowId);
                // Wait for import to finish before returning.
                var startTime = DateTime.Now;
                while (true)
                {
                    if (progressMonitor.IsCanceled)
                        return null;

                    JToken jStatusResponse = _webClient.Get(statusUri);
                    JToken rows = jStatusResponse[@"rows"];
                    var row = rows.FirstOrDefault(r => (int)r[@"RowId"] == rowId);
                    if (row == null)
                        continue;

                    var status = new ImportStatus((string) row[@"Status"]);
                    if (status.IsComplete)
                    {
                        progressMonitor.UpdateProgress(_progressStatus.Complete());
                        return new Uri(server.URI, (string)row[@"_labkeyurl_Description"]);
                    }
                  
                    else if (status.IsError || status.IsCancelled)
                    {
                        var jobUrl = new Uri(server.URI, (string) row[@"_labkeyurl_RowId"]);
                        var e = new PanoramaImportErrorException(server.URI, jobUrl, status.IsCancelled);
                        progressMonitor.UpdateProgress(
                            _progressStatus = _progressStatus.ChangeErrorException(e));
                        throw e;
                    }

                    updateProgressAndWait(status, progressMonitor, _progressStatus, startTime);
                }
            }
        }

        private class ImportStatus
        {
            public string StatusString { get; }
            public bool IsComplete => string.Equals(@"COMPLETE", StatusString);
            public bool IsRunning => StatusString.Contains(@"RUNNING"); // "IMPORT RUNNING" pre LK19.3, RUNNING, x% in LK19.3
            public bool IsError => string.Equals(@"ERROR", StatusString);
            public bool IsCancelled => string.Equals(@"CANCELLED", StatusString);

            public ImportStatus(string status)
            {
                StatusString = status;
            }
        }

        private void updateProgressAndWait(ImportStatus jobStatus, IProgressMonitor progressMonitor, IProgressStatus status, DateTime startTime)
        {
            var match = _runningStatusRegex.Match(jobStatus.StatusString);
            if (match.Success)
            {
                var currentProgress = _progressStatus.PercentComplete;

                if (int.TryParse(match.Groups[1].Value, out var progress))
                {
                    progress = Math.Max(progress, currentProgress);
                    _progressStatus = _progressStatus.ChangeMessage(string.Format(Resources.WebPanoramaPublishClient_updateProgressAndWait_Importing_data___0___complete_, progress));
                    _progressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(progress));

                    var delta = progress - currentProgress;
                    if (delta > 1)
                    {
                        // If progress is > 1% half the wait time
                        _waitTime = Math.Max(1, _waitTime / 2);
                    }
                    else if (delta < 1)
                    {
                        // If progress is < 1% double the wait time, up to a max of 10 seconds.
                        _waitTime = Math.Min(10, _waitTime * 2);
                    }

                    Thread.Sleep(_waitTime * 1000);
                    return;
                }
            }

            if (!jobStatus.IsRunning)
            {
                // Display the status since we don't recognize it.  This could be, for example, an "Import Waiting" status if another 
                // Skyline document is currently being imported on the server. 
                _progressMonitor.UpdateProgress(_progressStatus = _progressStatus =
                    _progressStatus.ChangeMessage(string.Format(Resources.WebPanoramaPublishClient_SendZipFile_Status_on_server_is___0_, jobStatus.StatusString)));
            }

            else if (!_progressStatus.Message.Equals(Resources
                .WebPanoramaPublishClient_SendZipFile_Waiting_for_data_import_completion___))
            {
                // Import is running now. Reset the progress message in case it had been set to something else (e.g. "Import Waiting") in a previous iteration.  
                progressMonitor.UpdateProgress(_progressStatus =
                    _progressStatus.ChangeMessage(Resources
                        .WebPanoramaPublishClient_SendZipFile_Waiting_for_data_import_completion___));
            }

            // This is probably an older server (pre LK19.3) that does not include the progress percent in the status.
            // Wait between 1 and 5 seconds before checking status again.
            var elapsed = (DateTime.Now - startTime).TotalMinutes;
            var sleepTime = elapsed > 5 ? 5 * 1000 : (int)(Math.Max(1, elapsed % 5) * 1000);
            Thread.Sleep(sleepTime);
        }

        public override JObject SupportedVersionsJson(Server server)
        {
            var uri = PanoramaUtil.Call(server.URI, @"targetedms", null, @"getMaxSupportedVersions");

            string supportedVersionsJson;

            using (var webClient = new WebClientWithCredentials(server.URI, server.Username, server.Password))
            {
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

        private class NonStreamBufferingWebClient : WebClientWithCredentials
        {
            public NonStreamBufferingWebClient(Uri serverUri, string username, string password)
                : base(serverUri, username, password)
            {
            }

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
            request.Headers.Add(@"Destination", destUri.AbsoluteUri);

            // If a file already exists at the destination URI, it will not be overwritten.  
            // The server would return a 412 Precondition Failed status code.
            request.Headers.Add(@"Overwrite", @"F");

            DoRequest(request,
                @"MOVE",
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
                    msg += @". Inner Exception: " + x.InnerException.Message;
                }
                throw new Exception(
                    TextUtil.LineSeparate(errorMessage, msg), x);
            }
        }

        private void DeleteTempZipFile(Uri sourceUri, string authHeader)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri.ToString());

            DoRequest(request,
                @"DELETE",
                authHeader,
                Resources.WebPanoramaPublishClient_DeleteTempZipFile_Error_deleting_temporary_zip_file__);
        }

        private void ConfirmFileOnServer(Uri sourceUri, string authHeader)
        {
            var request = (HttpWebRequest)WebRequest.Create(sourceUri);

            // Do a HEAD request to check if the file exists on the server.
            DoRequest(request,
                @"HEAD",
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
        public PanoramaImportErrorException(Uri serverUrl, Uri jobUrl, bool jobCancelled = false)
        {
            ServerUrl = serverUrl;
            JobUrl = jobUrl;
            JobCancelled = jobCancelled;
        }

        public Uri ServerUrl { get; private set; }
        public Uri JobUrl { get; private set; }
        public bool JobCancelled { get; private set; }
    }

    public class PanoramaServerException : Exception
    {
        public PanoramaServerException(string message) : base(message)
        {
        }
    }

    public class FolderInformation
    {
        private readonly Server _server;
        private readonly bool _hasWritePermission;

        public FolderInformation(Server server, bool hasWritePermission)
        {
            _server = server;
            _hasWritePermission = hasWritePermission;
        }

        public Server Server
        {
            get { return _server; }
        }

        public bool HasWritePermission
        {
            get { return _hasWritePermission; }
        }
    }

    public class UTF8WebClient : WebClient
    {
        public UTF8WebClient()
        {
            Encoding = Encoding.UTF8;
        }

        public JObject Get(Uri uri)
        {
            var response = DownloadString(uri);
            return JObject.Parse(response);
        }
    }

    public class WebClientWithCredentials : UTF8WebClient
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
        
        public JObject Post(Uri uri, NameValueCollection postData)
        {
            if (string.IsNullOrEmpty(_csrfToken))
            {
                // After this the client should have the X-LABKEY-CSRF token 
                DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
            }
            if (postData == null)
            {
                postData = new NameValueCollection();
            }
            var responseBytes = UploadValues(uri, PanoramaUtil.FORM_POST, postData);
            var response = Encoding.UTF8.GetString(responseBytes);
            return JObject.Parse(response);
        }

        public JObject Post(Uri uri, string postData)
        {
            if (string.IsNullOrEmpty(_csrfToken))
            {
                // After this the client should have the X-LABKEY-CSRF token 
                DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
            }
            Headers.Add(HttpRequestHeader.ContentType, "application/json");
            var response = UploadString(uri, PanoramaUtil.FORM_POST, postData);
            return JObject.Parse(response);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);

            var httpWebRequest = request as HttpWebRequest;
            if (httpWebRequest != null)
            {
                httpWebRequest.CookieContainer = _cookies;

                if (request.Method == PanoramaUtil.FORM_POST)
                {
                    if (!string.IsNullOrEmpty(_csrfToken))
                    {
                        // All POST requests to LabKey Server will be checked for a CSRF token
                        request.Headers.Add(LABKEY_CSRF, _csrfToken);
                    }
                }
            }
            return request;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var response = base.GetWebResponse(request);
            var httpResponse = response as HttpWebResponse;
            if (httpResponse != null)
            {
                GetCsrfToken(httpResponse);
            }
            return response;
        }

        private void GetCsrfToken(HttpWebResponse response)
        {
            if (!string.IsNullOrEmpty(_csrfToken))
            {
                return;
            }

            var csrf = response.Cookies[LABKEY_CSRF];
            if (csrf != null)
            {
                // The server set a cookie called X-LABKEY-CSRF, get its value
                _csrfToken = csrf.Value;
            }
        }
    }

    public class PanoramaServer
    {
        public Uri ServerUri { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        public PanoramaServer(Uri serverUri, string username, string password)
        {
            Username = username;
            Password = password;

            var path = serverUri.AbsolutePath;
            
            if (path.Length > 1)
            {
                // Get the context path (e.g. /labkey) from the path
                var idx = path.IndexOf(@"/", 1, StringComparison.Ordinal);
                if (idx != -1 && path.Length > idx + 1)
                {
                    path = path.Substring(0, idx + 1);
                }
            }

            // Need trailing '/' for correct URIs with new Uri(baseUri, relativeUri) method
            // With no trailing '/', new Uri("https://panoramaweb.org/labkey", "project/getContainers.view") will
            // return https://panoramaweb.org/project/getContainers.view (no labkey)
            // ReSharper disable LocalizableElement
            path = path + (path.EndsWith("/") ? "" : "/");
            // ReSharper restore LocalizableElement

            ServerUri = new UriBuilder(serverUri) { Path = path, Query = string.Empty, Fragment = string.Empty }.Uri;
        }

        public bool RemoveContextPath()
        {
            if (!ServerUri.AbsolutePath.Equals(@"/"))
            {
                ServerUri = new UriBuilder(ServerUri){Path=@"/"}.Uri;
                return true;
            }
            return false;
        }

        public bool AddLabKeyContextPath()
        {
            if (ServerUri.AbsolutePath.Equals(@"/"))
            {
                ServerUri = new UriBuilder(ServerUri) { Path = PanoramaUtil.LABKEY_CTX }.Uri;
                return true;
            }
            return false;
        }

        public bool Redirect(string redirectUri, string panoramaActionPath)
        {
            if (!Uri.IsWellFormedUriString(redirectUri, UriKind.Absolute))
            {
                return false;
            }

            var idx = redirectUri.IndexOf(panoramaActionPath, StringComparison.Ordinal);
            if (idx != -1)
            {
                var newUri = new Uri(redirectUri.Remove(idx));
                if (!ServerUri.Host.Equals(newUri.Host))
                {
                    return false;
                }

                ServerUri = newUri;
                return true;
            }
            return false;
        }

        public static string getFolderPath(Server server, Uri serverPlusPath)
        {
            var path = serverPlusPath.AbsolutePath;
            var contextPath = server.URI.AbsolutePath;
            return path.StartsWith(contextPath) ? path.Remove(0, contextPath.Length) : path;
        }
    }
}
