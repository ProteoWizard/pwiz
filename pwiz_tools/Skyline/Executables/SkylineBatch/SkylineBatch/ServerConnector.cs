using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using FluentFTP;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ServerConnector
    {
        private Dictionary<Server, List<ConnectedFileInfo>> _serverMap;
        private Dictionary<Server, Exception> _serverExceptions;

        public ServerConnector(params Server[] serverInfos)
        {
            _serverMap = new Dictionary<Server, List<ConnectedFileInfo>>();
            _serverExceptions = new Dictionary<Server, Exception>();
            foreach (var serverInfo in serverInfos)
            {
                if (!_serverMap.ContainsKey(serverInfo))
                {
                    _serverMap.Add(serverInfo, null);
                    _serverExceptions.Add(serverInfo, null);
                }
            }
        }

        public List<ConnectedFileInfo> GetFiles(DataServerInfo serverInfo, out Exception connectionException)
        {
            if (!_serverMap.ContainsKey(serverInfo) )
                throw new Exception("ServerConnector was not initialized with this server. No information for the server.");

            if (_serverMap[serverInfo] == null && _serverExceptions[serverInfo] == null)
                throw new Exception("ServerConnector was never started. No information for the server.");
            connectionException = _serverExceptions[serverInfo];
            var matchingFiles = new List<ConnectedFileInfo>();
            if (connectionException == null)
            {
                var namingRegex = new Regex(serverInfo.DataNamingPattern);
                foreach (var ftpFile in _serverMap[serverInfo])
                {
                    if (namingRegex.IsMatch(ftpFile.FileName))
                        matchingFiles.Add(ftpFile);
                }
                if (matchingFiles.Count == 0)
                {
                    connectionException = new ArgumentException(
                        string.Format(
                            Resources
                                .DataServerInfo_Validate_None_of_the_file_names_on_the_server_matched_the_regular_expression___0_,
                            serverInfo.DataNamingPattern) + Environment.NewLine +
                        Resources.DataServerInfo_Validate_Please_make_sure_your_regular_expression_is_correct_);
                }
            }

            if (connectionException != null)
                return null;
            return matchingFiles;
        }

        public void Add(Server server)
        {
            if (!_serverMap.ContainsKey(server))
            {
                _serverMap.Add(server, null);
                _serverExceptions.Add(server, null);
            }
        }

        public void Connect(OnPercentProgress doOnProgress, CancellationToken cancelToken, List<Server> servers = null)
        {
            if (servers == null)
                servers = _serverMap.Keys.ToList();
            if (servers.Count == 0) return;

            var serverCount = servers.Count;
            var downloadFinished = 0;
            var percentScale = 1.0 / serverCount * 100;
            foreach (var server in servers)
            {
                var percentDone = (int)(downloadFinished * percentScale);
                if (cancelToken.IsCancellationRequested)
                    break;
                if (_serverMap[server] != null || _serverExceptions[server] != null)
                    continue;
                var folder = ((DataServerInfo) server).Folder;
                var uri = server.FileSource.URI;
                var scheme = uri.Scheme.ToLowerInvariant();
                if (Equals(scheme, "http") || Equals(scheme, "https"))  // Assume Panorama
                {
                    var fileInfos = new List<ConnectedFileInfo>();
                    try
                    {
                        string userName = server.FileSource.Username, password = server.FileSource.Password;
                        if (!string.IsNullOrEmpty(userName) || !string.IsNullOrEmpty(password))
                        {
                            var validatedPanoramaServer = new WebPanoramaClient(uri, userName, password).ValidateServer();
                            uri = validatedPanoramaServer.URI;
                        }

                        var chosenServer = Uri.UnescapeDataString(uri.GetLeftPart(UriPartial.Authority));
                        JToken files;
                        if (GetPanoramaFolder(server).StartsWith("/_webdav/"))
                        {
                            files = GetFilesJson(server, null, cancelToken);
                        }
                        else
                        {
                            files = GetFilesJson(server, "/RawFiles", cancelToken) ??
                                    GetFilesJson(server, "/", cancelToken);
                        }

                        if (cancelToken.IsCancellationRequested)
                            break;

                        var count = (double) files.AsEnumerable().Count();
                        int i = 0;
                        foreach (dynamic file in files)
                        {
                            doOnProgress((int) (i / count * percentScale) + percentDone,
                                (int) ((i + 1) / count * percentScale) + percentDone);
                            if (file.collection == false && file.leaf == true)
                            {
                                var pathOnServer = (string)file["id"];
                                var downloadUri = new Uri(chosenServer + pathOnServer);
                                var panoramaServerUri = new Uri(chosenServer);
                                var size = PanoramaServerConnector.GetSize(downloadUri, panoramaServerUri, new WebPanoramaClient(panoramaServerUri, server.FileSource.Username, server.FileSource.Password),
                                     cancelToken);
                                fileInfos.Add(new ConnectedFileInfo(Path.GetFileName(pathOnServer),
                                    new Server(new RemoteFileSource(server.FileSource.Name + " TEST2", downloadUri, server.FileSource.Username, server.FileSource.Password, server.FileSource.Encrypt), string.Empty), size,
                                    folder));
                            }
                            i++;
                        }
                    }
                    catch (Exception e)
                    {
                        _serverExceptions[server] = e;
                    }

                    if (_serverExceptions[server] == null)
                        _serverMap[server] = fileInfos;
                }
                else if (Equals(scheme, "ftp"))
                {
                    doOnProgress(percentDone,
                        percentDone + (int)percentScale);
                    var serverFiles = new List<ConnectedFileInfo>();
                    var client = GetFtpClient(server);
                    var connectThread = new Thread(() =>
                    {
                        try
                        {
                            client.Connect();
                            foreach (FtpListItem item in client.GetListing(server.URI.LocalPath))
                            {
                                if (item.Type == FtpFileSystemObjectType.File)
                                {
                                    serverFiles.Add(new ConnectedFileInfo(item.Name, server, item.Size, folder));
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _serverExceptions[server] = e;
                        }

                    });
                    connectThread.Start();
                    while (connectThread.IsAlive)
                    {
                        if (cancelToken.IsCancellationRequested)
                            connectThread.Abort();
                    }
                    client.Disconnect();

                    if (_serverExceptions[server] == null && serverFiles.Count == 0)
                    {
                        _serverExceptions[server] = new ArgumentException(string.Format(
                            Resources
                                .DataServerInfo_Validate_There_were_no_files_found_at__0___Make_sure_the_URL__username__and_password_are_correct_and_try_again_,
                            server.URI));
                    }
                    else
                    {
                        _serverMap[server] = serverFiles;
                    }
                }
                // CONSIDER: Error if scheme is not recognized
                downloadFinished++;
            }
            doOnProgress(100, 100);
        }

        private JToken GetFilesJson(Server server, string relativePath, CancellationToken cancelToken)
        {
            if (cancelToken.IsCancellationRequested)
                return null;
            var uri = GetFilesJsonUri(server, relativePath);
            var webClient = new WebPanoramaClient(new Uri(Uri.UnescapeDataString(uri.GetLeftPart(UriPartial.Authority))), server.FileSource.Username, server.FileSource.Password);
            var jsonAsString =
                webClient.DownloadStringAsync(uri, cancelToken);
            if (cancelToken.IsCancellationRequested)
                return null;
            var panoramaJsonObject = JsonConvert.DeserializeObject<JObject>(jsonAsString);
            return panoramaJsonObject["files"];
        }

        private Uri GetFilesJsonUri(Server server, string relativePath)
        {
            string serverRoot = Uri.UnescapeDataString(server.URI.GetLeftPart(UriPartial.Authority));
            string folderRoot = serverRoot + GetPanoramaFolder(server);
            if (relativePath != null)
                folderRoot = folderRoot + "/%40files" + relativePath;
            return new Uri(folderRoot + "?method=json");
        }

        private string GetPanoramaFolder(Server server)
        {
            return (Path.GetDirectoryName(server.URI.LocalPath) ?? string.Empty).Replace(@"\", "/");
        }

        public void Reconnect(List<Server> servers, OnPercentProgress doOnProgress, CancellationToken cancelToken)
        {
            foreach (var serverInfo in servers)
            {
                _serverMap[serverInfo] = null;
                _serverExceptions[serverInfo] = null;
            }
            Connect(doOnProgress, cancelToken, servers);
        }

        public FtpClient GetFtpClient(Server serverInfo)
        {
            var client = new FtpClient(serverInfo.URI.Host);

            if (!string.IsNullOrEmpty(serverInfo.FileSource.Password))
                client.Credentials = new NetworkCredential(serverInfo.FileSource.Username, serverInfo.FileSource.Password);

            return client;
        }

        public void Combine(ServerConnector other)
        {
            foreach (var server in other._serverMap.Keys)
            {
                if (!_serverMap.ContainsKey(server))
                {
                    _serverMap.Add(server, null);
                    _serverExceptions.Add(server, null);
                }
                _serverMap[server] = other._serverMap[server];
                _serverExceptions[server] = other._serverExceptions[server];
            }
        }

        public bool Contains(Server server)
        {
            return _serverMap.ContainsKey(server);
        }
    }
}
