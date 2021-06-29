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
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedBatch.Properties;

namespace SharedBatch
{

    public enum FolderOperationStatus { OK, notpanorama, nopermission, notfound, alreadyexists, error }

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
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                throw new ArgumentException(Resources.PanoramaUtil_VerifyServerInformation_Username_and_password_cannot_be_null__Please_enter_a_username_and_password_);

            var uriServer = panoramaClient.ServerUri;

            var serverState = panoramaClient.GetServerState();
            if (!ServerState.available.Equals(serverState))
            {
                throw new PanoramaServerException(serverState, uriServer.AbsoluteUri);
            }

            var userState = panoramaClient.IsValidUser(username, password);
            if (!UserState.valid.Equals(userState))
            {
                throw new PanoramaServerException(userState);
            }

            var panoramaState = panoramaClient.IsPanorama();
            if (!PanoramaState.panorama.Equals(panoramaState))
            {
                throw new PanoramaServerException(panoramaState, uriServer.AbsoluteUri);
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
            var folderState = panoramaClient.IsValidFolder(panoramaFolder, server.Username, server.Password);
            if (!FolderState.valid.Equals(folderState))
            {
                throw new PanoramaServerException(folderState, server.Username, panoramaFolder, panoramaClient.ServerUri.AbsoluteUri);
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

        public static Uri CallNewInterface(Uri serverUri, string controller, string folderPath, string method,
            string query,
            bool isApi = false)
        {
            string apiString = isApi ? @"api" : @"view";
            string queryString = string.IsNullOrEmpty(query) ? "" : @"?" + query;
            string path = $@"{folderPath}/{controller}-{method}.{apiString}{queryString}";

            return new Uri(serverUri, path);
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

        internal string Username { get; }
        internal string Password { get; }
        internal Uri URI { get; }


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
                int hashCode = Username != null ? Username.GetHashCode() : 0;
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
        private Csrf _csrfToken;

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
                    if (_csrfToken == null)
                    {
                        // Look at CSRFUtil.validate() in the LabKey code.
                        // We need both a X-LABKEY-CSRF header and a cookie
                        _csrfToken = webClient.GetCsrfToken();
                    }
                    webClient.Post(uri, "", _csrfToken); // Try to reuse the CSRF token
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _csrfToken = null;  // Get a new CSRF token for the next request
                }
                throw;
            }
            return true;
        }

        public FolderOperationStatus CreateFolder(string parentPath, string folderName, string username, string password)
        {
            if (IsValidFolder($@"{parentPath}/{folderName}", username, password) == FolderState.valid)
                return FolderOperationStatus.alreadyexists;        //cannot create a folder with the same name
            var parentFolderStatus = IsValidFolder(parentPath, username, password);
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

            try
            {
                using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
                {
                    Uri requestUri = PanoramaUtil.CallNewInterface(ServerUri, @"core", parentPath, @"createContainer", "", true);
                    webClient.Post(requestUri, createRequest);
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


        public string DownloadString(Uri queryUri, string username, string password)
        {
            string data;

            using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
            {
                data = webClient.DownloadString(queryUri);
            }
            return data;
        }

        public string DownloadStringAsync(Uri queryUri, string username, string password, CancellationToken cancelToken)
        {
            string data = null;
            Exception error = null;
            using (var webClient = new WebClientWithCredentials(ServerUri, username, password))
            {
                bool finishedDownloading = false;
                webClient.DownloadStringAsync(queryUri);
                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    error = e.Error;
                    if (error == null)
                        data = e.Result;
                    finishedDownloading = true;
                };
                while (!finishedDownloading)
                {
                    if (cancelToken.IsCancellationRequested)
                        webClient.CancelAsync();
                }
            }
            if (error != null)
                throw error;
            return data;
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
                    webClient.Post(requestUri, "");
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

    public class PanoramaServerException : ArgumentException
    {
        public PanoramaServerException(string message) : base(message)
        {
        }

        public PanoramaServerException(ServerState serverState, string serverUrl) : base(ServerError(serverState, serverUrl))
        {
        }

        private static string ServerError(ServerState serverState, string serverUrl)
        {
            switch (serverState)
            {
                case ServerState.missing:
                    return string.Format(Resources.PanoramaUtil_VerifyServerInformation_The_server__0__does_not_exist_, serverUrl);
                case ServerState.unknown:
                    return string.Format(Resources.PanoramaUtil_VerifyServerInformation_Unknown_error_connecting_to_the_server__0__, serverUrl);
                case ServerState.available:
                    return Resources.PanoramaServerException_ServerError_Server_validation_passed_without_errors_; // Should not be throwing an exception if validation was successful
                default:
                    return string.Format(Resources.PanoramaServerException_ServerError_Encountered_unknown_server_validation_state__0__, serverState);
            }
        }

        public PanoramaServerException(UserState userState) : base(UserValidationError(userState))
        {
        }

        private static string UserValidationError(UserState userState)
        {
            switch (userState)
            {
                case UserState.nonvalid:
                    return Resources.PanoramaServerException_UserValidationError_The_username_and_password_could_not_be_authenticated_with_the_Panorama_server_;
                case UserState.unknown:
                    return Resources.PanoramaServerException_UserValidationError_Unknown_error_validating_user_permissions_on_the_Panorama_server_;
                case UserState.valid:
                    return Resources.PanoramaServerException_UserValidationError_User_validation_passed_without_errors_; // Should not be throwing an exception if validation was successful
                default:
                    return string.Format(Resources.PanoramaServerException_UserValidationError_Encountered_unknown_user_validation_state__0__, userState);
            }
        }

        public PanoramaServerException(PanoramaState panoramaState, string serverUrl) : base(PanoramaStateError(panoramaState, serverUrl))
        {
        }

        private static string PanoramaStateError(PanoramaState panoramaState, string serverUrl)
        {
            switch (panoramaState)
            {
                case PanoramaState.other:
                    return string.Format(Resources.PanoramaUtil_VerifyServerInformation_The_server__0__is_not_a_Panorama_server_, serverUrl);
                case PanoramaState.unknown:
                    return string.Format(Resources.PanoramaUtil_VerifyServerInformation_Unknown_error_while_checking_if_server__0__is_a_Panorama_server_, serverUrl);
                case PanoramaState.panorama:
                    return Resources.PanoramaServerException_PanoramaStateError_Panorama_server_validation_passed_without_errors_; // Should not be throwing an exception if validation was successful
                default:
                    return string.Format(Resources.PanoramaServerException_PanoramaStateError_Encountered_unknown_Panorama_server_validation_state__0__, panoramaState);
            }
        }

        public PanoramaServerException(FolderState folderState, string user, string folder, string serverUrl) : base(FolderValidationError(folderState, user, folder, serverUrl))
        {
        }

        private static string FolderValidationError(FolderState folderState, string user, string folder, string serverUrl)
        {
            switch (folderState)
            {
                case FolderState.notfound:
                    return string.Format(Resources.PanoramaServerException_FolderValidationError_Folder___0___does_not_exist_on_the_Panorama_server__1__, folder, serverUrl);
                case FolderState.nopermission:
                    return string.Format(Resources.PanoramaServerException_FolderValidationError_User__0__does_not_have_permissions_to_upload_to_the_Panorama_folder___1___, user, folder);
                case FolderState.notpanorama:
                    return string.Format(Resources.PanoramaServerException_FolderValidationError___0___is_not_a_Panorama_folder_, folder);
                case FolderState.valid:
                    return Resources.PanoramaServerException_FolderValidationError_Folder_validation_passed_without_errors_; // Should not be throwing an exception if validation was successful
                default:
                    return string.Format(Resources.PanoramaServerException_FolderValidationError_Encountered_unknown_folder_validation_state__0__, folderState);
            }
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

    internal class Csrf
    {
        public Csrf(string csrfToken, CookieContainer cookies)
        {
            CsrfToken = csrfToken;
            Cookies = cookies;
        }

        public string CsrfToken { get; }

        public CookieContainer Cookies { get; }
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

        public JObject Post(Uri uri, string postData, Csrf csrfToken)
        {
            _csrfToken = csrfToken.CsrfToken;
            _cookies = csrfToken.Cookies;
            return Post(uri, postData);
        }

        public JObject Post(Uri uri, string postData)
        {
            if (string.IsNullOrEmpty(_csrfToken))
            {
                GetCsrfToken();
            }

            return DoPost(uri, postData);
        }

        public Csrf GetCsrfToken()
        {
            // After this the client should have the X-LABKEY-CSRF token 
            DownloadString(new Uri(_serverUri, PanoramaUtil.ENSURE_LOGIN_PATH));
            return new Csrf(_csrfToken, _cookies);
        }

        private JObject DoPost(Uri uri, string postData)
        {
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
