using SharedBatch;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using SkylineBatch.Properties;
using Path = System.IO.Path;
using Timer = System.Windows.Forms.Timer;

namespace SkylineBatch
{
    public class SkypFile
    {
        public string SkypPath { get; private set; }
        public Uri SkylineDocUri { get; private set; }
        public Server Server { get; private set; }
        public string DownloadPath { get; private set; }

        public static SkypFile Create(string skypPath, Server server)
        {
            if (string.IsNullOrEmpty(skypPath))
            {
                return null;
            }
            var skyp = new SkypFile
            {
                SkypPath = skypPath,
                SkylineDocUri = GetSkyFileUrl(skypPath),
            };
            skyp.Server = server;

            var downloadDir = Path.GetDirectoryName(skyp.SkypPath) ?? string.Empty;
            skyp.DownloadPath = Path.Combine(downloadDir, skyp.GetSkylineDocName());

            return skyp;
        }

        private static Uri GetSkyFileUrl(string skypPath)
        {
            using (var reader = new StreamReader(skypPath))
            {
                return GetSkyFileUrl(reader);
            }
        }

        public static Uri GetSkyFileUrl(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    var urlString = line;
                    if (!urlString.EndsWith(TextUtil.EXT_ZIP))
                    {
                        // This is not a shared Skyline zip.
                        throw new InvalidDataException(string.Format(
                            Resources.SkypFile_GetSkyFileUrl_Expected_the_URL_of_a_shared_Skyline_document_archive__0__in_the_skyp_file__Found__1__instead_,
                            TextUtil.EXT_SKY_ZIP, urlString));
                    }

                    var validUrl = Uri.TryCreate(urlString, UriKind.Absolute, out var skyFileUri)
                                   && (skyFileUri.Scheme == Uri.UriSchemeHttp || skyFileUri.Scheme == Uri.UriSchemeHttps);
                    if (!validUrl)
                    {
                        throw new InvalidDataException(string.Format(
                            Resources.SkypFile_GetSkyFileUrl__0__is_not_a_valid_URL_on_a_Panorama_server_, urlString));
                    }

                    return skyFileUri;
                }
            }

            throw new InvalidDataException(string.Format(
                Resources.SkypFile_GetSkyFileUrl_File_does_not_contain_the_URL_of_a_shared_Skyline_archive_file__0__on_a_Panorama_server_,
                TextUtil.EXT_SKY_ZIP));
        }

        public string GetSkylineDocName()
        {
            return SkylineDocUri != null ? Path.GetFileName(SkylineDocUri.AbsolutePath) : null;
        }

        public static string GetNonExistentPath(string dir, string sharedSkyFile)
        {
            if (string.IsNullOrEmpty(sharedSkyFile))
            {
                throw new ArgumentException(Resources.SkypFile_GetNonExistentPath_Name_of_shared_Skyline_archive_cannot_be_null_or_empty_);
            }

            var count = 1;
            var isSkyZip = FileUtil.HasExtension(sharedSkyFile, TextUtil.EXT_SKY_ZIP);
            var ext = isSkyZip ? TextUtil.EXT_SKY_ZIP : TextUtil.EXT_ZIP;
            var fileNameNoExt = isSkyZip
                ? sharedSkyFile.Substring(0, sharedSkyFile.Length - TextUtil.EXT_SKY_ZIP.Length)
                : Path.GetFileNameWithoutExtension(sharedSkyFile);

            var path = Path.Combine(dir, sharedSkyFile);
            while (File.Exists(path) || Directory.Exists(Path.Combine(dir, FileUtil.ExtractDir(path))))
            {
                // Add a suffix if a file with the given name already exists OR
                // a directory that would be created when opening this file exists.
                sharedSkyFile = fileNameNoExt + @"(" + count + @")" + ext;
                path = Path.Combine(dir, sharedSkyFile);
                count++;
            }
            return Path.Combine(dir, sharedSkyFile);
        }
    }

    public class SkypSupport
    {

        public DownloadClientCreator DownloadClientCreator { private get; set; }

        public const string ERROR401 = "(401) Unauthorized";
        public const string ERROR403 = "(403) Forbidden";

        public SkypSupport()
        {
            DownloadClientCreator = new DownloadClientCreator();
        }

        public string Open(string skypPath, Server server, Update progressHandler, CancellationToken cancellationToken, long size)
        {
            SkypFile skyp;
            try
            {
                skyp = SkypFile.Create(skypPath, server);
                Download(skyp, progressHandler, cancellationToken, size);
                return Path.Combine(Path.GetDirectoryName(skyp.DownloadPath) ?? string.Empty, skyp.GetSkylineDocName());
            }
            catch (Exception e)
            {
                progressHandler(-1, e);
                return string.Empty;
            }
        }
        
        private void Download(SkypFile skyp, Update progressHandler, CancellationToken cancellationToken, long size)
        {
            var downloadClient = DownloadClientCreator.Create(progressHandler, cancellationToken);

            downloadClient.Download(skyp.SkylineDocUri, skyp.DownloadPath, skyp.Server?.Username, skyp.Server?.Password, size);

            if (cancellationToken.IsCancellationRequested || downloadClient.IsError)
            {
                File.Delete(skyp.DownloadPath);
            }
            if (downloadClient.IsError)
            {
                var message =
                    string.Format(
                        Resources.SkypSupport_Download_There_was_an_error_downloading_the_Skyline_document_specified_in_the_skyp_file__0__,
                        skyp.SkylineDocUri);

                if (downloadClient.Error != null)
                {
                    var exceptionMsg = downloadClient.Error.Message;
                    message = TextUtil.LineSeparate(message, exceptionMsg);

                    if (exceptionMsg.Contains(ERROR401))
                    {
                        message = TextUtil.LineSeparate(message, AddPanoramaServerMessage(skyp));
                    }
                    else if (exceptionMsg.Contains(ERROR403))
                    {
                        message = TextUtil.LineSeparate(message,
                            string.Format(
                                Resources.SkypSupport_Download_You_do_not_have_permissions_to_download_this_file_from__0__,
                                skyp.SkylineDocUri.Host));
                    }
                }

                throw new Exception(message, downloadClient.Error);
            }
        }

        private static string AddPanoramaServerMessage(SkypFile skyp)
        {
            return string.Format(
                Resources.SkypSupport_AddPanoramaServerMessage_You_may_have_to_add__0__as_a_Panorama_server_from_the_Tools___Options_menu_in_Skyline_,
                skyp.SkylineDocUri.Host);
        }
    }

    public class WebDownloadClient : IDownloadClient
    {
        private Update ProgressHandler { get; }
        private CancellationToken CancelToken { get; set; }

        public bool IsCancelled => CancelToken.IsCancellationRequested;
        public bool IsError => false;
        public Exception Error => null;

        public WebDownloadClient(Update progressHandler, CancellationToken cancellationToken)
        {
            ProgressHandler = progressHandler;
            CancelToken = cancellationToken;
        }

        public void Download(Uri remoteUri, string downloadPath, string username, string password, long? expectedSize = null)
        {
            using (var wc = new UTF8WebClient())
            {
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    wc.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
                }
                wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                wc.DownloadDataCompleted += wc_DownloadFileCompleted;
                wc.DownloadFileCompleted += wc_DownloadFileCompleted;
                wc.DownloadStringCompleted += wc_DownloadFileCompleted;

                if (expectedSize != null)
                {
                    new Thread(() =>
                    {
                        long fileSize = 0;
                        while ((fileSize < expectedSize))
                        {
                            ProgressHandler((int)((double)fileSize / expectedSize * 100),
                                null);
                            fileSize = new FileInfo(downloadPath).Length;
                            Thread.Sleep(1000);
                        }
                    }).Start();
                }
                wc.DownloadFileAsync(remoteUri, downloadPath, new CancellationTokenSource().Token);

            }
        }


        public void DownloadAsync(Uri remoteUri, string downloadPath, string username, string password, long expectedSize)
        {
            using (var wc = new UTF8WebClient())
            {
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    wc.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
                }
                wc.DownloadFileAsync(remoteUri, downloadPath, CancelToken);
                
                
                long fileSize = 0;
                long lastFileSize = -1;
                while (fileSize < expectedSize)
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        wc.CancelAsync();
                        break;
                    }
                    ProgressHandler((int)((double)fileSize / expectedSize * 100),
                        lastFileSize == fileSize ? new Exception(Resources.WebDownloadClient_DownloadAsync_Operation_timed_out_) : null);
                    lastFileSize = fileSize;
                    fileSize = new FileInfo(downloadPath).Length;
                    Thread.Sleep(2000);
                }

            }
        }

        public static long GetSize(Uri remoteUri, string username, string password)
        {
            long result;
            using (var wc = new UTF8WebClient())
            {
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    wc.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
                }
                wc.OpenRead(remoteUri);
                result = Convert.ToInt64(wc.ResponseHeaders["Content-Length"]);
                wc.Dispose();

            }

            return result;
        }

        private void wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null && !CancelToken.IsCancellationRequested)
            {
                ProgressHandler(-1, e.Error);
            }
        }

        private void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            ProgressHandler(e.ProgressPercentage, null);
        }
    }

    public interface IDownloadClient
    {
        void Download(Uri remoteUri, string downloadPath, string username, string password, long? expectedSize = null);

        bool IsCancelled { get; }
        bool IsError { get; }
        Exception Error { get; }
    }

    public class DownloadClientCreator
    {
        public virtual IDownloadClient Create(Update progressHandler, CancellationToken cancellationToken)
        {
            return new WebDownloadClient(progressHandler, cancellationToken);
        }
    }

    [XmlRoot("server")]
    public class Server
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

        public string GetKey()
        {
            return URI.ToString();
        }

        internal string AuthHeader
        {
            get
            {
                return GetBasicAuthHeader(Username, Password);
            }
        }

        internal static string GetBasicAuthHeader(string username, string password)
        {
            byte[] authBytes = Encoding.UTF8.GetBytes(String.Format(@"{0}:{1}", username, password));
            var authHeader = @"Basic " + Convert.ToBase64String(authBytes);
            return authHeader;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private Server()
        {
        }

        private enum ATTR
        {
            username,
            password,
            password_encrypted,
            uri
        }

        private void Validate()
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static Server ReadXml(XmlReader reader)
        {
            // Read tag attributes
            var username = reader.GetAttribute(ATTR.username) ?? string.Empty;
            string encryptedPassword = reader.GetAttribute(ATTR.password_encrypted);
            string password;
            if (encryptedPassword != null)
            {
                try
                {
                    password = TextUtil.DecryptPassword(encryptedPassword);
                }
                catch (Exception)
                {
                    password = string.Empty;
                }
            }
            else
            {
                password = reader.GetAttribute(ATTR.password) ?? string.Empty;
            }
            string uriText = reader.GetAttribute(ATTR.uri);
            if (string.IsNullOrEmpty(uriText))
            {
                throw new InvalidDataException(Resources.Server_ReadXml_A_Panorama_server_must_be_specified_);
            }

            Uri uri;
            try
            {
                uri = new Uri(uriText);
            }
            catch (UriFormatException)
            {
                throw new InvalidDataException(Resources.Server_ReadXml_Server_URL_is_corrupt_);
            }
            // Consume tag
            reader.Read();
            
            var server = new Server(uri, username, password);
            server.Validate();
            return server;
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttributeString(ATTR.username, Username);
            if (!string.IsNullOrEmpty(Password))
            {
                writer.WriteAttributeString(ATTR.password_encrypted, TextUtil.EncryptPassword(Password));
            }
            writer.WriteAttribute(ATTR.uri, URI);
        }
        #endregion

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
            return obj is Server && Equals((Server)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Username != null ? Username.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Password != null ? Password.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (URI != null ? URI.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }

    public class UTF8WebClient : WebClient
    {
        public UTF8WebClient()
        {
            Encoding = Encoding.UTF8;
        }

        public JObject Get(Uri uri)
        {
            var response = DownloadString(uri);
            return JObject.Parse(response);
        }
    }
}