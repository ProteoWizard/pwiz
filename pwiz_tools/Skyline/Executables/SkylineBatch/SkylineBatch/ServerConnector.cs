using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ServerConnector
    {
        private Dictionary<ServerInfo, List<FtpListItem>> _serverMap;
        private Dictionary<ServerInfo, Exception> _serverExceptions;
        private object _lock = new object();

        public ServerConnector(params ServerInfo[] serverInfos)
        {
            lock (_lock)
            {
                _serverMap = new Dictionary<ServerInfo, List<FtpListItem>>();
                _serverExceptions = new Dictionary<ServerInfo, Exception>();
                foreach (var serverInfo in serverInfos)
                {
                    if (!_serverMap.ContainsKey(serverInfo))
                    {
                        _serverMap.Add(serverInfo, null);
                        _serverExceptions.Add(serverInfo, null);
                    }
                }
            }
        }

        public List<FtpListItem> GetFiles(DataServerInfo serverInfo, out Exception connectionException)
        {
            if (!_serverMap.ContainsKey(serverInfo) )
                throw new Exception("ServerConnector was not initialized with this server. No information for the server.");
            if (_serverMap[serverInfo] == null && _serverExceptions[serverInfo] == null)
                throw new Exception("ServerConnector was never started. No information for the server.");
            connectionException = _serverExceptions[serverInfo];
            var matchingFiles = new List<FtpListItem>();
            if (connectionException == null)
            {
                var namingRegex = new Regex(serverInfo.DataNamingPattern);
                foreach (var ftpFile in _serverMap[serverInfo])
                {
                    if (namingRegex.IsMatch(ftpFile.Name))
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

        public void Connect(OnPercentProgress doOnProgress, List<ServerInfo> servers = null)
        {
            if (servers == null)
                servers = _serverMap.Keys.ToList();

            var serverCount = servers.Count;
            var downloadFinished = 0;
            doOnProgress(0,
                (int)(1.0 / serverCount * 100));
            foreach (var serverInfo in servers)
            {
                if (_serverMap[serverInfo] != null || _serverExceptions[serverInfo] != null)
                    continue;
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
                if (_serverExceptions[serverInfo] == null && serverFiles.Count == 0)
                {
                    _serverExceptions[serverInfo] = new ArgumentException(string.Format(
                        Resources
                            .DataServerInfo_Validate_There_were_no_files_found_at__0___Make_sure_the_URL__username__and_password_are_correct_and_try_again_,
                        serverInfo.Server.AbsoluteUri));
                }
                else
                {
                    lock (_lock)
                        _serverMap[serverInfo] = serverFiles;
                }
                client.Disconnect();
                downloadFinished++;
                doOnProgress((int)((double)downloadFinished / serverCount * 100),
                    (int)((double)(downloadFinished + 1) / serverCount * 100));
            }
        }

        public void Reconnect(List<ServerInfo> servers, OnPercentProgress doOnProgress)
        {
            foreach (var serverInfo in servers)
            {
                _serverMap[serverInfo] = null;
                _serverExceptions[serverInfo] = null;
            }
            Connect(doOnProgress, servers);
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
    }
}
