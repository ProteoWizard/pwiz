using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
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

        public void Connect(OnPercentProgress doOnProgress, CancellationToken cancelToken, List<Server> servers = null)
        {
            if (servers == null)
                servers = _fileList.Keys.ToList();
            if (servers.Count == 0) return;

            var serverNum = 0.0;
            foreach (var server in servers)
            {
                doOnProgress((int)serverNum / servers.Count * 100,
                    (int)(serverNum + 1.0) / servers.Count * 100);
                if (cancelToken.IsCancellationRequested) break;
                try
                {
                    _fileList[server] = GetFileInfo(server, cancelToken);
                }
                catch (Exception e)
                {
                    _fileExceptions[server] = e;
                }

                serverNum++;
            }
        }

        public void Reconnect(List<Server> servers, OnPercentProgress doOnProgress, CancellationToken cancelToken)
        {
            foreach (var serverInfo in servers)
            {
                _fileList[serverInfo] = null;
                _fileExceptions[serverInfo] = null;
            }
            Connect(doOnProgress, cancelToken, servers);
        }

        public ConnectedFileInfo GetFile(Server server, string folder, out Exception connectionException)
        {
            connectionException = _fileExceptions[server];
            if (connectionException != null) return null;
            var fileInfo = _fileList[server];
            return new ConnectedFileInfo(fileInfo.FileName,fileInfo.ServerInfo, fileInfo.Size, folder);
        }


        public ConnectedFileInfo GetFileInfo(Server server, CancellationToken cancelToken)
        {
            var zipFileName = HttpUtility.ParseQueryString(server.URI.Query)["fileName"];
            var panoramaFolder = (Path.GetDirectoryName(server.URI.LocalPath) ?? string.Empty).Replace(@"\", "/");
            var panoramaServerUri =  new Uri(PanoramaUtil.ServerNameToUrl("https://panoramaweb.org/"));

            var idQuery = PanoramaUtil.CallNewInterface(panoramaServerUri, "query", panoramaFolder,
                "selectRows", "schemaName=targetedms&query.queryName=runs&query.Deleted~eq=false&query.Status~isblank&query.columns=Id,FileName", true);



            var webClient = new WebPanoramaClient(panoramaServerUri);
            var jsonAsString = webClient.DownloadStringAsync(idQuery, server.Username, server.Password, cancelToken);
            if (cancelToken.IsCancellationRequested) return null;

            var id = -1;
            var panoramaJsonObject = JsonConvert.DeserializeObject<PanoramaJsonObject>(jsonAsString);
            var rows = panoramaJsonObject.rows;
            foreach (var row in rows)
            {
                if (row.FileName.Equals(zipFileName))
                    id = row.Id;
            }

            var downloadSkypUri =
                PanoramaUtil.CallNewInterface(panoramaServerUri, "targetedms", panoramaFolder, "downloadDocument", $"id={id}&view=skyp");

            var tmpFile = Path.GetTempFileName();

            using (var wc = new UTF8WebClient())
            {
                if (!string.IsNullOrEmpty(server.Username) && !string.IsNullOrEmpty(server.Password))
                {
                    wc.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(server.Username, server.Password));
                }
                
                wc.DownloadFile(downloadSkypUri, tmpFile);
            }

            Uri webdavUri = null;
            using (var fileStream = new FileStream(tmpFile, FileMode.Open, FileAccess.Read))
            {
                using (var streamReader = new StreamReader(fileStream))
                {
                    while (!streamReader.EndOfStream)
                    {
                        var line = streamReader.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                            webdavUri = new Uri(line);
                    }
                }
            }
            File.Delete(tmpFile);

            if (webdavUri == null)
                throw new Exception("Could not parse skyp file.");
            var downloadServer = new Server(webdavUri, server.Username, server.Password);
            var fileName = zipFileName;
            var size = WebDownloadClient.GetSize(downloadServer.URI, server.Username, server.Password, cancelToken);
            return new ConnectedFileInfo(fileName, downloadServer, size, string.Empty);
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
