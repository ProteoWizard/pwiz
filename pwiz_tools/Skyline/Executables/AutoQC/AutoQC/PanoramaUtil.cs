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
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace AutoQC
{
    public class PanoramaUtil
    {
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
                    throw new PanoramaServerException(string.Format("The server {0} does not exist", uriServer.Host));
                case ServerState.unknown:
                    throw new PanoramaServerException(string.Format("Unknown error connecting to the server {0}", uriServer.Host));
            }
            switch (panoramaClient.IsPanorama())
            {
                case PanoramaState.other:
                    throw new PanoramaServerException(string.Format("The server {0} is not a Panorama server", uriServer.Host));
                case PanoramaState.unknown:
                    throw new PanoramaServerException(string.Format("Unknown error connecting to the server {0}", uriServer.Host));
            }

            switch (panoramaClient.IsValidUser(username, password))
            {
                case UserState.nonvalid:
                    throw new PanoramaServerException("The username and password could not be authenticated with the panorama server");
                case UserState.unknown:
                    throw new PanoramaServerException(string.Format("Unknown error connecting to the server {0}", uriServer.Host));
            }
        }

        public static Uri GetContainersUri(Uri serverUri, string folder, bool includeSubfolders)
        {
            var queryString = string.Format("includeSubfolders={0}&moduleProperties=TargetedMS", includeSubfolders ? "true" : "false"); // Not L10N
            return Call(serverUri, "project", folder, "getContainers", queryString); // Not L10N
        }

        private static Uri Call(Uri serverUri, string controller, string folderPath, string method, string query, bool isApi = false)
        {
            string path = "labkey/" + controller + "/" + (folderPath ?? string.Empty) + // Not L10N
                          "/" + method + (isApi ? ".api" : ".view"); // Not L10N

            if (string.IsNullOrEmpty(query))
            {
                return new UriBuilder(serverUri.Scheme, serverUri.Host, serverUri.Port, path).Uri;
            }
            else
            {
                return new UriBuilder(serverUri.Scheme, serverUri.Host, serverUri.Port, path, "?" + query).Uri; // Not L10N   
            }
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
    public enum FolderState { valid, notpanorama, nopermission, notfound }

    public interface IPanoramaClient
    {
        Uri ServerUri { get; }
        ServerState GetServerState();
        PanoramaState IsPanorama();
        UserState IsValidUser(string username, string password);
        FolderState IsValidFolder(string folderPath, string username, string password);
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
    public class PanoramaServerException : Exception
    {
        public PanoramaServerException(string message)
            : base(message)
        {
        }
    }

}
