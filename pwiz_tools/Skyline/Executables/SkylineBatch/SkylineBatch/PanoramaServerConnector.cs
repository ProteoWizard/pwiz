using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SharedBatch;

namespace SkylineBatch
{
    public class PanoramaServerConnector
    {

        private Dictionary<Server, ConnectedFileInfo> _fileList;
        private Dictionary<Server, Exception> _fileExceptions;

        public PanoramaServerConnector()
        {
            _fileList = new Dictionary<Server, ConnectedFileInfo>();
            _fileExceptions = new Dictionary<Server, Exception>();
        }

        public void Add(Server server)
        {
            if (!_fileList.ContainsKey(server))
            {
                _fileList.Add(server, null);
                _fileExceptions.Add(server, null);
            }
        }

        public void Connect(OnPercentProgress doOnProgress, List<Server> servers = null)
        {
            if (servers == null)
                servers = _fileList.Keys.ToList();

            var serverNum = 0.0;
            doOnProgress(0,
                (int)(1.0 / servers.Count * 100));
            foreach (var server in servers)
            {
                try
                {
                    _fileList[server] = GetFileInfo(server);
                }
                catch (Exception e)
                {
                    _fileExceptions[server] = e;
                }

                serverNum++;
                doOnProgress((int)serverNum / servers.Count * 100,
                    (int)(serverNum + 1.0) / servers.Count * 100);
            }
        }

        public void Reconnect(List<Server> servers, OnPercentProgress doOnProgress)
        {
            foreach (var serverInfo in servers)
            {
                _fileList[serverInfo] = null;
                _fileExceptions[serverInfo] = null;
            }
            Connect(doOnProgress, servers);
        }

        public ConnectedFileInfo GetFile(Server server, string folder, out Exception connectionException)
        {
            connectionException = _fileExceptions[server];
            if (connectionException != null) return null;
            var fileInfo = _fileList[server];
            return new ConnectedFileInfo(fileInfo.FileName, server, fileInfo.Size, folder);
        }


        public static SkypFile DownloadSkyp(string filePath, Server server)
        {
            var skypDownloader = new WebDownloadClient((percent, error) => { }, new CancellationToken());
            skypDownloader.Download(server.URI, filePath, server.Username, server.Password);
            return SkypFile.Create(filePath, server);
        }

        public ConnectedFileInfo GetFileInfo(Server server)
        {
            var temporarySkypFile = Path.Combine(Path.GetTempPath(), FileUtil.GetSafeName(server.URI.AbsoluteUri));
            var skypFile = DownloadSkyp(temporarySkypFile, server);
            var fileName = skypFile.GetSkylineDocName().Replace(TextUtil.EXT_ZIP, string.Empty);
            var size = WebDownloadClient.GetSize(skypFile.SkylineDocUri, server.Username, server.Password);
            File.Delete(temporarySkypFile);
            return new ConnectedFileInfo(fileName, server, size, string.Empty);
        }

        public void Combine(PanoramaServerConnector other)
        {
            foreach (var server in other._fileList.Keys)
            {
                if (!_fileList.ContainsKey(server))
                {
                    _fileList.Add(server, null);
                    _fileExceptions.Add(server, null);
                }
                _fileList[server] = other._fileList[server];
                _fileExceptions[server] = other._fileExceptions[server];
            }
        }
    }
}
