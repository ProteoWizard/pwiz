using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using SharedBatch;

namespace SkylineBatch
{
    public class ServerConnector
    {
        private Dictionary<ServerInfo, List<FtpListItem>> _serverMap;
        private Dictionary<ServerInfo, Exception> _serverExceptions;
        private bool _started;
        private object _lock = new object();

        public ServerConnector()
        {
            lock (_lock)
            {
                _serverMap = new Dictionary<ServerInfo, List<FtpListItem>>();
                _serverExceptions = new Dictionary<ServerInfo, Exception>();
            }
        }


        public async Task GetFiles(ServerInfo serverInfo, OnPercentProgress doOnProgress, Action<List<FtpListItem>> successCallback, Action<Exception> errorCallback)
        {
            while (true)
            {
                lock (_lock)
                {
                    if (!_started)
                        Start(doOnProgress);
                    _started = true;
                    if (_serverMap[serverInfo] != null || _serverExceptions[serverInfo] != null)
                        break;
                }
                await Task.Delay(1000);
            }
            if (_serverMap[serverInfo] != null)
                successCallback(_serverMap[serverInfo]);
            if (_serverExceptions[serverInfo] != null)
                errorCallback(_serverExceptions[serverInfo]);
        }

        public void AddServer(ServerInfo serverInfo)
        {
            lock (_lock)
            {
                if (_started) throw new Exception("Cannot add servers after start.");
                if (!_serverMap.ContainsKey(serverInfo))
                {
                    _serverMap.Add(serverInfo, null);
                    _serverExceptions.Add(serverInfo, null);
                }
            }
        }

        private void Start(OnPercentProgress doOnProgress)
        {
            var serverCount = _serverMap.Count;
            var downloadFinished = 0;
            var servers = _serverMap.Keys.ToList();
            doOnProgress(0,
                (int)(1.0 / serverCount * 100));
            foreach (var serverInfo in servers)
            {
                var serverFiles = new List<FtpListItem>();
                var client = GetFtpClient(serverInfo);
                try
                {
                    client.Connect();
                    foreach (FtpListItem item in client.GetListing(serverInfo.Server.AbsolutePath))
                    {
                        if (item.Type == FtpFileSystemObjectType.File)
                        {
                            serverFiles.Add(item);
                        }
                    }
                }
                catch (Exception e)
                {
                    client.Disconnect();
                    lock (_lock)
                        _serverExceptions[serverInfo] = e;
                }
                client.Disconnect();
                lock (_lock)
                    _serverMap[serverInfo] = serverFiles;
                downloadFinished++;
                doOnProgress((int)((double)downloadFinished / serverCount * 100),
                    (int)((double)(downloadFinished + 1) / serverCount * 100));
            }
        }

        public FtpClient GetFtpClient(ServerInfo serverInfo)
        {
            var client = new FtpClient(serverInfo.Server.Host);

            if (!string.IsNullOrEmpty(serverInfo.Password))
            {
                if (!string.IsNullOrEmpty(serverInfo.UserName))
                    client.Credentials = new NetworkCredential(serverInfo.UserName, serverInfo.Password);
                else
                    client.Credentials = new NetworkCredential("anonymous", serverInfo.Password);
            }

            return client;
        }

    }
}
