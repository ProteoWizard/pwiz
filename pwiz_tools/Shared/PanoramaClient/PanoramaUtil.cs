using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient.Properties;

namespace pwiz.PanoramaClient
{
    public class PanoramaUtil
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

            var serverState = panoramaClient.GetServerState();
            if (!serverState.IsValid())
            {
                throw new PanoramaServerException(serverState.GetErrorMessage(uriServer));
            }

            var userState = panoramaClient.IsValidUser(username, password);
            if (!userState.IsValid())
            {
                throw new PanoramaServerException(userState.GetErrorMessage(uriServer));
            }
        }

        public static UserState ValidateServerAndUser(ref Uri serverUri, string username, string password)
        {
            var pServer = new PanoramaServer(serverUri, username, password);

            try
            {
                var userState = EnsureLogin(pServer);
                serverUri = pServer.URI;
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

                return new UserState(UserStateEnum.unknown, ex.Message, GetEnsureLoginUri(pServer));
            }
        }

        private static UserState EnsureLogin(PanoramaServer pServer)
        {
            var requestUri = GetEnsureLoginUri(pServer);
            var request = (HttpWebRequest)WebRequest.Create(requestUri);
            request.Headers.Add(HttpRequestHeader.Authorization,
                PanoramaServer.GetBasicAuthHeader(pServer.Username, pServer.Password));
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return new UserState(UserStateEnum.nonvalid,
                            string.Format("Could not authenticate user. Response received from server: {0} {1}",
                                response.StatusCode, response.StatusDescription),
                            requestUri);
                    }

                    JObject jsonResponse = null;
                    if (TryGetJsonResponse(response, ref jsonResponse) && IsValidEnsureLoginResponse(jsonResponse, pServer.Username))
                    {
                        return UserState.VALID;
                    }
                    else if (jsonResponse == null)
                    {
                        return new UserState(UserStateEnum.unknown,
                            string.Format("Server did not return a valid JSON response. {0} is not a Panorama server.", pServer.URI),
                            requestUri);
                    }
                    else
                    {
                        var jsonText = jsonResponse.ToString(Formatting.None);
                        jsonText = jsonText.Replace(@"{", @"{{"); // escape curly braces
                        return new UserState(UserStateEnum.unknown,
                            string.Format("Unexpected JSON response from the server: {0}", jsonText),
                            requestUri);
                    }
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

                    return new UserState(UserStateEnum.nonvalid, ex.Message, requestUri); // User cannot be authenticated
                }

                throw;
            }
        }

        private static bool TryGetJsonResponse(HttpWebResponse response, ref JObject jsonResponse)
        {
            using (var stream = response.GetResponseStream())
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var responseText = reader.ReadToEnd();
                        try
                        {
                            jsonResponse = JObject.Parse(responseText);
                            return true;
                        }
                        catch (JsonReaderException) { }
                    }
                }
            }

            return false;
        }

        private static bool IsValidEnsureLoginResponse(JObject jsonResponse, string expectedEmail)
        {
            // Example JSON response:
            /*
             * {
                  "currentUser" : {
                    "canUpdateOwn" : "false",
                    "canUpdate" : "false",
                    "canDeleteOwn" : "false",
                    "canInsert" : "false",
                    "displayName" : "test_user",
                    "canDelete" : "false",
                    "id" : 1166,
                    "isAdmin" : "false",
                    "email" : "test_user@uw.edu"
                  }
                }
             */
            jsonResponse.TryGetValue(@"currentUser", out JToken currentUser);
            if (currentUser != null)
            {
                var email = currentUser.Value<string>(@"email");
                return email != null && email.Equals(expectedEmail);
            }

            return false;
        }

        private static Uri GetEnsureLoginUri(PanoramaServer pServer)
        {
            return new Uri(pServer.URI, ENSURE_LOGIN_PATH);
        }

        private static UserState TryEnsureLogin(PanoramaServer pServer, ref Uri serverUri)
        {
            try
            {
                var userState = EnsureLogin(pServer);
                serverUri = pServer.URI;
                return userState;
            }
            catch (WebException e)
            {
                // Due to anything other than 401 (Unauthorized), which is handled in EnsureLogin.
                return new UserState(UserStateEnum.unknown, e.Message, GetEnsureLoginUri(pServer));
            }
        }

        public static void VerifyFolder(IPanoramaClient panoramaClient, PanoramaServer server, string panoramaFolder)
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

                var folderType = (string)folderJson[@"folderType"];
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
        {
            return new WebPanoramaClient(serverUri);
        }
    }

    public abstract class GenericState<T>
    {
        public T State { get; }
        public string Error { get; }
        public Uri Uri { get; }

        public abstract bool IsValid();

        protected string AppendErrorAndUri(string stateErrorMessage)
        {
            var message = stateErrorMessage;

            if (Error != null || Uri != null)
            {
                var sb = new StringBuilder();

                if (Error != null)
                {
                    sb.AppendLine(string.Format(Resources.GenericState_AppendErrorAndUri_Error___0_, Error));
                }

                if (Uri != null)
                {
                    sb.AppendLine(string.Format(Resources.GenericState_AppendErrorAndUri_URL___0_, Uri));
                }

                message = TextUtil.LineSeparate(message, string.Empty, sb.ToString());
            }


            return message;
        }

        public GenericState(T state, string error, Uri uri)
        {
            State = state;
            Error = error;
            Uri = uri;
        }
    }

    public class ServerState : GenericState<ServerStateEnum>
    {
        public static readonly ServerState VALID = new ServerState(ServerStateEnum.available, null, null);

        public ServerState(ServerStateEnum state, string error, Uri uri) : base(state, error, uri)
        {
        }

        public override bool IsValid()
        {
            return State == ServerStateEnum.available;
        }

        public string GetErrorMessage(Uri serverUri)
        {
            var stateError = string.Empty;
            switch (State)
            {
                case ServerStateEnum.missing:
                    stateError = string.Format(
                        Resources.ServerState_GetErrorMessage_The_server__0__does_not_exist_,
                        serverUri.AbsoluteUri);
                    break;
                case ServerStateEnum.unknown:
                    stateError = string.Format(
                        Resources.ServerState_GetErrorMessage_Unable_to_connect_to_the_server__0__,
                        serverUri.AbsoluteUri);
                    break;
            }

            return AppendErrorAndUri(stateError);
        }
    }

    public class UserState : GenericState<UserStateEnum>
    {
        public static readonly UserState VALID = new UserState(UserStateEnum.valid, null, null);

        public UserState(UserStateEnum state, string error, Uri uri) : base(state, error, uri)
        {
        }

        public override bool IsValid()
        {
            return State == UserStateEnum.valid;
        }

        public string GetErrorMessage(Uri serverUri)
        {
            var stateError = string.Empty;
            switch (State)
            {
                case UserStateEnum.nonvalid:
                    stateError = Resources.UserState_GetErrorMessage_The_username_and_password_could_not_be_authenticated_with_the_panorama_server_;
                    break;
                case UserStateEnum.unknown:
                    stateError = string.Format(
                        Resources.UserState_GetErrorMessage_There_was_an_error_authenticating_user_credentials_on_the_server__0__,
                        serverUri.AbsoluteUri);
                    break;
            }

            return AppendErrorAndUri(stateError);
        }
    }

    public enum ServerStateEnum { unknown, missing, available }
    public enum UserStateEnum { valid, nonvalid, unknown }
    public enum FolderState { valid, notpanorama, nopermission, notfound }
    public enum FolderOperationStatus { OK, notpanorama, nopermission, notfound, alreadyexists, error }

    public class PanoramaServer: Immutable
    {
        public Uri URI { get; protected set; }
        public string Username { get; protected set; }
        public string Password { get; protected set; }

        protected PanoramaServer()
        {
        }

        public PanoramaServer(Uri serverUri) : this(serverUri, null, null)
        {
        }

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

            URI = new UriBuilder(serverUri) { Path = path, Query = string.Empty, Fragment = string.Empty }.Uri;
        }

        public string AuthHeader => GetBasicAuthHeader(Username, Password);

        public PanoramaServer ChangeUri(Uri uri)
        {
            return ChangeProp(ImClone(this), im => im.URI = uri);
        }

        public bool HasUserCredentials()
        {
            return Username != null;
        }

        public bool RemoveContextPath()
        {
            if (!URI.AbsolutePath.Equals(@"/"))
            {
                URI = new UriBuilder(URI) { Path = @"/" }.Uri;
                return true;
            }
            return false;
        }

        public bool AddLabKeyContextPath()
        {
            if (URI.AbsolutePath.Equals(@"/"))
            {
                URI = new UriBuilder(URI) { Path = PanoramaUtil.LABKEY_CTX }.Uri;
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
                if (!URI.Host.Equals(newUri.Host))
                {
                    return false;
                }

                URI = newUri;
                return true;
            }
            return false;
        }

        public static string getFolderPath(PanoramaServer server, Uri serverPlusPath)
        {
            var path = serverPlusPath.AbsolutePath;
            var contextPath = server.URI.AbsolutePath;
            return path.StartsWith(contextPath) ? path.Remove(0, contextPath.Length) : path;
        }

        public static string GetBasicAuthHeader(string username, string password)
        {
            byte[] authBytes = Encoding.UTF8.GetBytes(String.Format(@"{0}:{1}", username, password));
            var authHeader = @"Basic " + Convert.ToBase64String(authBytes);
            return authHeader;
        }
    }

    public class PanoramaServerException : Exception
    {
        public PanoramaServerException(string message) : base(message)
        {
        }
    }

    public class FolderInformation
    {
        private readonly PanoramaServer _server;
        private readonly bool _hasWritePermission;

        public FolderInformation(PanoramaServer server, bool hasWritePermission)
        {
            _server = server;
            _hasWritePermission = hasWritePermission;
        }

        public PanoramaServer Server
        {
            get { return _server; }
        }

        public bool HasWritePermission
        {
            get { return _hasWritePermission; }
        }
    }
}
