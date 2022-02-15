using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SharedBatch;
using SkylineBatch.Properties;
using File = System.IO.File;

namespace SkylineBatch
{
    public class ServerFilesManager
    {
        private readonly HashSet<Server> _servers;
        private readonly Dictionary<Server, Exception> _connectionExceptions;
        private readonly Dictionary<Server, List<ConnectedFileInfo>> _connectedServerFiles;

        private readonly Dictionary<Server, List<Server>> _serverReferences;


        private readonly ServerConnector _serverConnector;

        private readonly PanoramaServerConnector _panoramaServerConnecter;


        public ServerFilesManager()
        {
            _servers = new HashSet<Server>();
            _connectionExceptions = new Dictionary<Server, Exception>();
            _connectedServerFiles = new Dictionary<Server, List<ConnectedFileInfo>>();
            _serverReferences = new Dictionary<Server, List<Server>>();

            _serverConnector = new ServerConnector();
            _panoramaServerConnecter = new PanoramaServerConnector();
        }

        public bool HadConnectionExceptions => _connectionExceptions.Count > 0;


        public void AddServer(Server server)
        {
            _servers.Add(server);
            if (!_serverReferences.ContainsKey(server)) _serverReferences.Add(server, new List<Server>());
            _serverReferences[server].Add(server);
            if (!_connectedServerFiles.ContainsKey(server)) _connectedServerFiles.Add(server, new List<ConnectedFileInfo>());
            if (server is DataServerInfo)
                _serverConnector.Add(server);
            else if (server is PanoramaFile)
                _panoramaServerConnecter.Add(server);
            else
                throw new Exception("Unrecognized server type");
        }

        public Exception ConnectionException(Server serverInfo)
        {
            if (_connectionExceptions.ContainsKey(serverInfo))
                return _connectionExceptions[serverInfo];
            return null;
        }

        public void Replace(Server oldServer, Server newServer,
            ServerConnector newConnectedServer, PanoramaServerConnector newPanoramaConnectedServer)
        {
            if (_serverReferences[oldServer].Count == 1)
            {
                _servers.Remove(oldServer);
                _connectedServerFiles.Remove(oldServer);
                _serverReferences.Remove(oldServer);
            }
            else
            {
                if (oldServer is DataServerInfo)
                    _serverReferences.Remove((DataServerInfo) oldServer);
                else
                    _serverReferences.Remove((PanoramaFile)oldServer);
            }

            AddServer(newServer);
            _serverConnector.Combine(newConnectedServer);
            _panoramaServerConnecter.Combine(newPanoramaConnectedServer);
        }


        public void Connect(OnPercentProgress doOnProgress, CancellationToken cancelToken, HashSet<Server> servers = null)
        {
            if (servers == null)
                servers = _servers;

            var dataServers = new List<Server>();
            var panoramaServers = new List<Server>();
            foreach (var server in servers)
            {
                if (server is DataServerInfo) dataServers.Add(server);
                else panoramaServers.Add(server);
            }
            _panoramaServerConnecter.Reconnect(panoramaServers, doOnProgress, cancelToken);
            _serverConnector.Reconnect(dataServers, doOnProgress, cancelToken);
            if (cancelToken.IsCancellationRequested)
                return;
            foreach (var server in servers)
            {
                if (server is DataServerInfo)
                {
                    var files = _serverConnector.GetFiles((DataServerInfo)server, out Exception connectionException);
                    if (connectionException != null)
                    {
                        _connectionExceptions.Add(server, connectionException);
                        continue;
                    }
                    foreach (var file in files)
                        AddConnectedFtpFile(file, (DataServerInfo)server);
                }
                else if (server is PanoramaFile)
                {
                    var file = _panoramaServerConnecter.GetFile(server, ((PanoramaFile)server).DownloadFolder,
                        out Exception connectionException);
                    if (connectionException != null)
                    {
                        _connectionExceptions.Add(server, connectionException);
                        continue;
                    }
                    _connectedServerFiles[server].Add(file);
                }
                else
                    throw new Exception("Unrecognized server type");
            }
        }

        public void Reconnect(List<Server> servers, OnPercentProgress doOnProgress, CancellationToken cancelToken)
        {
            var serverSet = new HashSet<Server>(servers);
            foreach (var server in serverSet)
            {
                _connectedServerFiles[server] = new List<ConnectedFileInfo>();
                _connectionExceptions.Remove(server);
            }
            Connect(doOnProgress, cancelToken, serverSet);
        }

        private void AddConnectedFtpFile(ConnectedFileInfo file, DataServerInfo serverInfo)
        {
            foreach (var server in _serverReferences[serverInfo])
            {
                if (!_connectedServerFiles.ContainsKey(server))
                    _connectedServerFiles.Add(server, new List<ConnectedFileInfo>());
                _connectedServerFiles[server].Add(file);
            }
        }

        public List<ConnectedFileInfo> GetFiles(DataServerInfo serverInfo)
        {
            return _serverConnector.GetFiles(serverInfo, out _);
        }

        public ConnectedFileInfo GetFile(PanoramaFile serverInfo)
        {
            return _connectedServerFiles[serverInfo][0];
        }

        public List<ConnectedFileInfo> GetDataFilesToDownload(DataServerInfo serverInfo, string folder)
        {
            var files = GetFiles(serverInfo);
            var downloadingFiles = new List<ConnectedFileInfo>();
            foreach (var file in files)
            {
                var filePath = Path.Combine(folder, file.FileName);
                if (!File.Exists(filePath) || new FileInfo(filePath).Length < file.Size)
                    downloadingFiles.Add(file);
            }

            return downloadingFiles;
        }

        public List<ConnectedFileInfo> GetPanoramaFilesToDownload(PanoramaFile serverInfo)
        {
            var file = _connectedServerFiles[serverInfo][0];
            var filePath = Path.Combine(file.DownloadFolder, file.FileName);
            var files = new List<ConnectedFileInfo>();
            if (File.Exists(filePath) && new FileInfo(filePath).Length == file.Size)
                return files;
            files.Add(file);
            return files;
        }


        public Dictionary<string, long> GetSize()
        {
            var fileSizes = new Dictionary<string, long>();
            foreach (var fileList in _connectedServerFiles.Values)
            {
                foreach (var fileInfo in fileList)
                {
                    var filePath = Path.Combine(fileInfo.DownloadFolder, fileInfo.FileName);
                    if (!fileSizes.ContainsKey(filePath))
                        fileSizes.Add(filePath, fileInfo.Size);
                    else if (fileSizes[filePath] != fileInfo.Size)
                        throw new Exception(string.Format(Resources.ServerFilesManager_GetSize_Two_different_files_are_both_being_saved_to__0_, filePath));
                }
            }
            
            var driveSpaceNeeded = new Dictionary<string, long>();
            foreach (var file in fileSizes.Keys)
            {
                if (!File.Exists(file) || new FileInfo(file).Length < fileSizes[file])
                {
                    var driveName = file.Substring(0, 3);
                    if (!driveSpaceNeeded.ContainsKey(driveName))
                        driveSpaceNeeded.Add(driveName, 0);
                    driveSpaceNeeded[driveName] += fileSizes[file];
                }
            }
            return driveSpaceNeeded;
        }
        
    }

    public class ConnectedFileInfo
    {

        public readonly long Size;
        public readonly string FileName;
        public readonly Server ServerInfo;
        public readonly string DownloadFolder;


        public ConnectedFileInfo(string fileName, Server serverInfo, long size, string downloadFolder)
        {
            FileName = fileName;
            ServerInfo = serverInfo;
            Size = size;
            DownloadFolder = downloadFolder;
        }

        public bool Downloaded()
        {
            var downloadedFullPath = Path.Combine(DownloadFolder, FileName);
            return File.Exists(downloadedFullPath) && new FileInfo(downloadedFullPath).Length == Size;
        }

    }
}
