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
            var downloadingFileName = HttpUtility.ParseQueryString(server.URI.Query)["fileName"];
            Uri webdavUri = null;
            var panoramaServerUri = new Uri(PanoramaUtil.ServerNameToUrl("https://panoramaweb.org/"));
            var webClient = new WebPanoramaClient(panoramaServerUri);
            var panoramaFolder = (Path.GetDirectoryName(server.URI.LocalPath) ?? string.Empty).Replace(@"\", "/");
            if (downloadingFileName == null) // this is not a zipped Skyline file 
            {
                downloadingFileName = Path.GetFileName(server.URI.LocalPath);
                webdavUri = server.URI;
            }
            else
            {
               
                var idQuery = PanoramaUtil.CallNewInterface(panoramaServerUri, "query", panoramaFolder,
                "selectRows", "schemaName=targetedms&query.queryName=runs&query.Deleted~eq=false&query.Status~isblank&query.columns=Id,FileName", true);
                var jsonAsString = webClient.DownloadStringAsync(idQuery, server.FileSource.Username, server.FileSource.Password, cancelToken);
                if (cancelToken.IsCancellationRequested) return null;

                var id = -1;
                var panoramaJsonObject = JsonConvert.DeserializeObject<PanoramaJsonObject>(jsonAsString);
                var rows = panoramaJsonObject.rows;
                foreach (var row in rows)
                {
                    if (row.FileName.Equals(downloadingFileName))
                        id = row.Id;
                }

                var downloadSkypUri =
                    PanoramaUtil.CallNewInterface(panoramaServerUri, "targetedms", panoramaFolder, "downloadDocument", $"id={id}&view=skyp");

                var tmpFile = Path.GetTempFileName();

                using (var wc = new UTF8WebClient())
                {
                    if (!string.IsNullOrEmpty(server.FileSource.Username) && !string.IsNullOrEmpty(server.FileSource.Password))
                    {
                        wc.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(server.FileSource.Username, server.FileSource.Password));
                    }

                    wc.DownloadFile(downloadSkypUri, tmpFile);
                }

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
            }

            var downloadServer = new Server(new RemoteFileSource(server.FileSource.Name + " TEST", webdavUri, server.FileSource.Username, server.FileSource.Password, server.FileSource.Encrypt), string.Empty);
            var fileName = downloadingFileName;
            var size = GetSize(downloadServer.URI, panoramaServerUri, webClient, server.FileSource.Username, server.FileSource.Password, cancelToken);
            return new ConnectedFileInfo(fileName, downloadServer, size, string.Empty);
        }

        public static long GetSize(Uri remoteUri, Uri panoramaServerUri, WebPanoramaClient webClient, string username, string password, CancellationToken cancelToken)
        {
            var folderUrl = Path.GetDirectoryName(remoteUri.LocalPath);
            var folderUri = new Uri(panoramaServerUri.AbsoluteUri + folderUrl);
            var filesJsonAsString = webClient.DownloadStringAsync(new Uri(folderUri, "?method=json"), username, password, cancelToken);
            dynamic jsonObject = JsonConvert.DeserializeObject(filesJsonAsString);
            long size = 0;
            try
            {
                dynamic files = jsonObject.files;
                foreach (var file in files)
                {
                    if (((string)(file.id)).Equals(remoteUri.LocalPath))
                    {
                        size = (long)file.size;
                        break;
                    }
                }

            }
            catch (Exception e)
            {
                throw new Exception("Could not parse json response: " + e.Message + Environment.NewLine + Environment.NewLine + filesJsonAsString);
            }
            return size;
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
