using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace AutoQC
{
    public class PanoramaUtils
    {
        private const string FORM_POST = "POST"; // Not L10N

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

        public static void VerifyServerInformation(IPanoramaClient panoramaClient, Uri uriServer, string username, string password)
        {
            switch (panoramaClient.GetServerState())
            {
                case ServerState.missing:
                    throw new Exception(string.Format("The server {0} does not exist", uriServer.Host));
                case ServerState.unknown:
                    throw new Exception(string.Format("Unknown error connecting to the server {0}", uriServer.Host));
            }
            switch (panoramaClient.IsPanorama())
            {
                case PanoramaState.other:
                    throw new Exception(string.Format("The server {0} is not a Panorama server", uriServer.Host));
                case PanoramaState.unknown:
                    throw new Exception(string.Format("Unknown error connecting to the server {0}", uriServer.Host));
            }

            switch (panoramaClient.IsValidUser(username, password))
            {
                case UserState.nonvalid:
                    throw new Exception("The username and password could not be authenticated with the panorama server");
                case UserState.unknown:
                    throw new Exception(string.Format("Unknown error connecting to the server {0}", uriServer.Host));
            }
        }

        private Uri Call(Uri serverUri, string controller, string folderPath, string method, bool isApi = false)
        {
            return Call(serverUri, controller, folderPath, method, null, isApi);
        }

        private static Uri Call(Uri serverUri, string controller, string folderPath, string method, string query, bool isApi = false)
        {
            string path = "labkey/" + controller + "/" + (folderPath ?? string.Empty) + // Not L10N
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

 
        public static bool verifyFolder(Server server, string folderPath)
        {
            // Retrieve folders from server.
            Uri uri = Call(server.URI, "project", folderPath, "getContainers", "includeSubfolders=false&moduleProperties=TargetedMS"); // Not L10N

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add(HttpRequestHeader.Authorization, server.AuthHeader);
                // TODO: Exception will be thrown here is something is not righ
                string folderInfo = webClient.UploadString(uri, FORM_POST, string.Empty);
                JToken folder = JObject.Parse(folderInfo);

                int userPermissions = (int)folder["userPermissions"]; // Not L10N

                // User can only upload to folders where TargetedMS is an active module.
                JToken modules = folder["activeModules"]; // Not L10N
                string folderType = (string)folder["folderType"]; // Not L10N
                bool canUpload = ContainsTargetedMSModule(modules) &&
                                 Equals(folderType, "Targeted MS") && // Not L10N
                                 Equals(userPermissions & 2, 2);

                return canUpload;
            }     
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
                byte[] authBytes = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", Username, Password)); // Not L10N
                var authHeader = "Basic " + Convert.ToBase64String(authBytes); // Not L10N
                return authHeader;
            }
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

    public class WebPanoramaClient : IPanoramaClient
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
