/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace AutoQC
{
    public class PanoramaUtil
    {
        public const string PANORAMA_WEB = "https://panoramaweb.org/"; // Not L10N
        public const string FORM_POST = "POST"; // Not L10N
        public const string LABKEY_CTX = "/labkey/"; // Not L10N
        public const string ENSURE_LOGIN_PATH = "security/home/ensureLogin.view"; // Not L10N

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

        public static string ServerNameToUrl(string serverName)
        {
            const string https = "https://"; // Not L10N
            const string http = "http://"; // Not L10N

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
                    throw new PanoramaServerException(string.Format("The server {0} does not exist", uriServer.AbsoluteUri));
                case ServerState.unknown:
                    throw new PanoramaServerException(string.Format("Unknown error connecting to the server {0}", uriServer.AbsoluteUri));
            }

            switch (panoramaClient.IsValidUser(username, password))
            {
                case UserState.nonvalid:
                    throw new PanoramaServerException("The username and password could not be authenticated with the panorama server");
                case UserState.unknown:
                    throw new PanoramaServerException(string.Format("Unknown error validating user on server {0}", uriServer.AbsoluteUri));
            }

            switch (panoramaClient.IsPanorama())
            {
                case PanoramaState.other:
                    throw new PanoramaServerException(string.Format("The server {0} is not a Panorama server", uriServer.AbsoluteUri));
                case PanoramaState.unknown:
                    throw new PanoramaServerException(string.Format("Unknown error while checking if server {0} is a Panorama server", uriServer.AbsoluteUri));
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
                throw;
            }
        }

        private static UserState EnsureLogin(PanoramaServer pServer)
        {
            var requestUri = new Uri(pServer.ServerUri, ENSURE_LOGIN_PATH);
            var request = (HttpWebRequest)WebRequest.Create(requestUri);
            request.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(pServer.Username, pServer.Password));
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
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

        public static Uri GetContainersUri(Uri serverUri, string folder, bool includeSubfolders)
        {
            var queryString = string.Format("includeSubfolders={0}&moduleProperties=TargetedMS", includeSubfolders ? "true" : "false"); // Not L10N
            return Call(serverUri, "project", folder, "getContainers", queryString); // Not L10N
        }

        internal static Uri Call(Uri serverUri, string controller, string folderPath, string method, string query, bool isApi = false)
        {
            string path = controller + "/" + (folderPath ?? string.Empty) + "/" + // Not L10N
                          method + (isApi ? ".api" : ".view"); // Not L10N

            if (!string.IsNullOrEmpty(query))
            {
                path = path + "?" + query;  // Not L10N
            }

            return new Uri(serverUri, path);
        }


        public static void VerifyFolder(IPanoramaClient panoramaClient, Server server, string panoramaFolder)
        {
            switch (panoramaClient.IsValidFolder(panoramaFolder, server.Username, server.Password))
            {
                case FolderState.notfound:
                    throw new PanoramaServerException(
                        string.Format(
                            "Folder {0} does not exist on the Panorama server {1}",
                            panoramaFolder, panoramaClient.ServerUri));
                case FolderState.nopermission:
                    throw new PanoramaServerException(string.Format(
                        "User {0} does not have permissions to upload to the Panorama folder {1}",
                        server.Username, panoramaFolder));
                case FolderState.notpanorama:
                    throw new PanoramaServerException(string.Format(
                        "{0} is not a Panorama folder",
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

    }

    public sealed class Server
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
        bool PingPanorama(string folderPath, string username, string password);
    }

    public class WebPanoramaClient : IPanoramaClient
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

                    throw;
                }
            }
            return ServerState.unknown;
        }

        // This function must be true/false returning; no exceptions can be thrown
        private bool TryNewProtocol(Func<bool> testFunc)
        {
            Uri currentUri = ServerUri;

            // try again using https
            if (ServerUri.Scheme.Equals("http")) // Not L10N
            {
                ServerUri = new Uri(currentUri.AbsoluteUri.Replace("http", "https")); // Not L10N
                return testFunc();
            }
            // We assume "https" (PanoramaUtil.ServerNameToUrl) if there is no scheme in the user provided URL.
            // Try http. LabKey Server may not be running under SSL. 
            else if (ServerUri.Scheme.Equals("https")) // Not L10N
            {
                ServerUri = new Uri(currentUri.AbsoluteUri.Replace("https", "http")); // Not L10N
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
                Uri uri = new Uri(ServerUri, "project/home/getContainers.view"); // Not L10N
                using (var webClient = new UTF8WebClient())
                {
                    JObject jsonResponse = webClient.Get(uri);
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
                else if (tryNewProtocol)
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

        public bool PingPanorama(string folderPath, string username, string password)
        {
            try
            {
                var uri = PanoramaUtil.Call(ServerUri, "targetedms", folderPath, "autoQCPing", "", true);

                using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
                {
                    webClient.Post(uri, null);
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }
                else throw;
            }
            return true;
        }
    }

    public class PanoramaServerException : Exception
    {
        public PanoramaServerException(string message)
            : base(message)
        {
        }
    }

    class UTF8WebClient : WebClient
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

    internal class WebClientWithCredentials : UTF8WebClient
    {
        private CookieContainer _cookies = new CookieContainer();
        private string _csrfToken;
        private Uri _serverUri;

        private static string LABKEY_CSRF = "X-LABKEY-CSRF"; // Not L10N

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
                var idx = path.IndexOf("/", 1, StringComparison.Ordinal); // Not L10N
                if (idx != -1 && path.Length > idx + 1)
                {
                    path = path.Substring(0, idx + 1);
                }
            }

            // Need trailing '/' for correct URIs with new Uri(baseUri, relativeUri) method
            // With no trailing '/', new Uri("https://panoramaweb.org/labkey", "project/getContainers.view") will
            // return https://panoramaweb.org/project/getContainers.view (no labkey)
            path = path + (path.EndsWith("/") ? "" : "/");  // Not L10N 

            ServerUri = new UriBuilder(serverUri) { Path = path, Query = string.Empty, Fragment = string.Empty }.Uri;
        }

        public bool RemoveContextPath()
        {
            if (!ServerUri.AbsolutePath.Equals("/"))  // Not L10N
            {
                ServerUri = new UriBuilder(ServerUri) { Path = "/" }.Uri;  // Not L10N
                return true;
            }
            return false;
        }

        public bool AddLabKeyContextPath()
        {
            if (ServerUri.AbsolutePath.Equals("/"))  // Not L10N
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
