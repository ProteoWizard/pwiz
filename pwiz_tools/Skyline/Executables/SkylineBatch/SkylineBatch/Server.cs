using SharedBatch;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using SkylineBatch.Properties;

namespace SkylineBatch
{

    public class WebDownloadClient
    {
        private Update ProgressHandler { get; }
        private CancellationToken CancelToken { get; set; }

        public WebDownloadClient(Update progressHandler, CancellationToken cancellationToken)
        {
            ProgressHandler = progressHandler;
            CancelToken = cancellationToken;
        }

        public void DownloadAsync(Uri remoteUri, string downloadPath, string username, string password,
            long expectedSize)
        {
            using (var wc = new WebClient())
            {
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    wc.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
                }

                var completed = false;
                Exception error = null;
                wc.DownloadFileAsync(remoteUri, downloadPath);
                wc.DownloadFileCompleted += ((sender, e) =>
                {
                    error = e.Error;
                    completed = true;
                });
                var progressChanged = new DownloadProgressChangedEventHandler((sender, e) => {
                    var percent = new FileInfo(downloadPath).Length * 100 / expectedSize;
                    ProgressHandler((int)percent, null);
                });
                wc.DownloadProgressChanged += progressChanged;
                while (!completed)
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        wc.CancelAsync();
                    }

                    if (error != null)
                        ProgressHandler(-1, error);
                    Thread.Sleep(100);
                }
                wc.DownloadProgressChanged -= progressChanged;
            }
        }

        public static long GetSize(Uri remoteUri, string username, string password, CancellationToken cancelToken)
        {
            long result = -1;
            using (var wc = new UTF8WebClient())
            {
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    wc.Headers.Add(HttpRequestHeader.Authorization, Server.GetBasicAuthHeader(username, password));
                }

                Stream stream = null;
                Exception serverException = null;
                var sizeThread = new Thread(() =>
                {
                    try
                    {

                        // ReSharper disable once AccessToDisposedClosure - must dispose wc after cancellation or close
                        stream = wc.OpenRead(remoteUri);

                        // TODO: figure out why this stopped working
                        // ReSharper disable once AccessToDisposedClosure
                        result = Convert.ToInt64(wc.ResponseHeaders["Content-Length"]);
                    }
                    catch (Exception e)
                    {
                        serverException = e;
                    }
                });
                sizeThread.Start();
                while (sizeThread.IsAlive)
                {
                    if (cancelToken.IsCancellationRequested)
                        sizeThread.Abort();
                }
                if (stream != null) stream.Dispose();
                wc.Dispose();
                if (serverException != null)
                    throw serverException;
            }
            return result;
        }
    }

    [XmlRoot("server")]
    public class Server
    {

        public Server(RemoteFileSource remoteDataSource, string relativePath)
        {
            FileSource = remoteDataSource;
            RelativePath = relativePath;
        }

        internal RemoteFileSource FileSource { get; set; }
        internal string RelativePath { get; set; }

        public Uri URI => new Uri(FileSource.URI.AbsoluteUri + RelativePath);

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

        private void Validate()
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static void ValidateInputs(RemoteFileSource remoteFileSource, string relativeUrl)
        {
            if (remoteFileSource == null)
                throw new ArgumentException(Resources.Server_ValidateInputs_No_remote_file_source_was_specified__Please_choose_a_remote_file_source_);
            try
            {
                _ = new Uri(remoteFileSource.URI.AbsoluteUri + relativeUrl);
            }
            catch (Exception)
            {
                throw new ArgumentException(Resources.DataServerInfo_ServerFromUi_Error_parsing_the_URL__Please_correct_the_URL_and_try_again_);
            }
        }

        public Server UpdateRemoteFileSet(ImmutableDictionary<string, RemoteFileSource> remoteFileSources, out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources)
        {
            newRemoteFileSources = remoteFileSources;
            var newFileSource = FileSource.UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources);
            return new Server(newFileSource, RelativePath);
        }

        public static Server ReadXml(XmlReader reader)
        {
            //reader.ReadToDescendant("remote_file");
            var relativePath = reader.GetAttribute(XML_TAGS.relative_path) ?? string.Empty;
            var remoteDataSource = RemoteFileSource.ReadXml(reader);
            return new Server(remoteDataSource, relativePath);
        }

        public static Server ReadXmlVersion_21_1(XmlReader reader)
        {
            var remoteDataSource = RemoteFileSource.ReadXmlVersion_21_1(reader);
            return new Server(remoteDataSource, string.Empty);
        }

        public static Server ReadXmlVersion_20_2(XmlReader reader)
        {
            var remoteDataSource = RemoteFileSource.ReadXmlVersion_20_2(reader);
            return new Server(remoteDataSource, string.Empty);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeIfString(XML_TAGS.relative_path, RelativePath);
            FileSource.WriteXml(writer);
        }
        #endregion

        #region object overrides

        private bool Equals(Server other)
        {
            return string.Equals(RelativePath, other.RelativePath) &&
                   Equals(FileSource, other.FileSource);
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
                int hashCode = RelativePath != null ? RelativePath.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (FileSource != null ? FileSource.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }

    [XmlRoot("remote_file_source")]
    public class RemoteFileSource
    {
        public const string XML_EL = "remote_file_source";

        public static RemoteFileSource RemoteSourceFromUi(string name, string url, string username, string password, bool encrypt)
        {
            ValidateInputs(url, username, password, out Uri uri);
            return new RemoteFileSource(name, uri, username, password, encrypt);
        }

        public RemoteFileSource(string name, string uriText, string username, string password, bool encrypt)
            : this(name, new Uri(uriText), username, password, encrypt)
        {
        }

        public RemoteFileSource(string name, Uri uri, string username, string password, bool encrypt)
        {
            Name = name;
            Username = username;
            Password = password;
            URI = uri;
            Encrypt = encrypt;
            FtpSource = uri.AbsoluteUri.StartsWith("ftp://");
        }

        internal string Name { get; set; }
        internal string Username { get; set; }
        internal string Password { get; set; }
        internal Uri URI { get; set; }
        internal bool FtpSource { get; set; }

        internal bool Encrypt { get; set; }

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
        private RemoteFileSource()
        {
        }


        public XmlSchema GetSchema()
        {
            return null;
        }

        public static void ValidateInputs(string url, string username, string password, out Uri uri)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException(Resources.DataServerInfo_ServerFromUi_The_URL_cannot_be_empty__Please_enter_a_URL_);
            try
            {
                uri = new Uri(url);
            }
            catch (Exception)
            {
                throw new ArgumentException(Resources.DataServerInfo_ServerFromUi_Error_parsing_the_URL__Please_correct_the_URL_and_try_again_);
            }
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
                return;
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException(Resources.DataServerInfo_ValidateUsernamePassword_Username_cannot_be_empty_if_the_server_has_a_password__Please_enter_a_username_);
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException(Resources.DataServerInfo_ValidateUsernamePassword_Password_cannot_be_empty_if_the_server_has_a_username__Please_enter_a_password_);
        }

        public RemoteFileSource UpdateRemoteFileSet(ImmutableDictionary<string, RemoteFileSource> remoteFileSources, out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources)
        {
            newRemoteFileSources = remoteFileSources;
            if (newRemoteFileSources.ContainsKey(Name))
            {
                if (Equals(newRemoteFileSources[Name]))
                    return newRemoteFileSources[Name];
                var duplicateIndexRegex = new Regex("\\(([1-9][0-9]*)\\)$");
                var regexMatches = duplicateIndexRegex.Match(Name).Groups;
                string newName;
                try
                {
                    var lastIndex = int.Parse(regexMatches[0].Value);
                    newName = duplicateIndexRegex.Replace(Name, $"({lastIndex + 1})");
                } catch (FormatException)
                {
                    newName = Name + "(2)";
                }
                var newFileSource = new RemoteFileSource(newName, URI, Username, Password, Encrypt);
                newRemoteFileSources = newRemoteFileSources.Add(newFileSource.Name, newFileSource);
                return newFileSource;
            }
            newRemoteFileSources = newRemoteFileSources.Add(Name, this);
            return this;
        }

        public RemoteFileSource ReplacedRemoteFileSource(RemoteFileSource replacingSource, RemoteFileSource newSource, out bool replaced)
        {
            replaced = Equals(replacingSource);
            return replaced ? newSource : this;
        }


        public static string CreateNameFromUrl(string url)
        {
            string host = null;
            string pathFileName = null;
            string queryFileName = null;
            try
            {
                var uri = new Uri(url);
                host = uri.Host;
                var localPath = uri.LocalPath.EndsWith("/") || uri.LocalPath.EndsWith("\\")
                    ? uri.LocalPath.Substring(0, uri.LocalPath.Length - 1)
                    : uri.LocalPath;
                pathFileName = Path.GetFileName(localPath);
                var queryDictionary = System.Web.HttpUtility.ParseQueryString(uri.Query);
                if (queryDictionary.AllKeys.Contains("fileName"))
                    queryFileName = queryDictionary["fileName"];
            }
            catch (Exception)
            {
                // pass
            }
            if (host != null && queryFileName != null)
                return host + " " + queryFileName;
            if (host != null && !string.IsNullOrEmpty(pathFileName))
                return host + " " + pathFileName;
            return url;
        }

        public static RemoteFileSource ReadXml(XmlReader reader)
        {
            reader.ReadToDescendant("remote_file_source");
            // Read tag attributes
            var name = reader.GetAttribute(XML_TAGS.name);
            var username = reader.GetAttribute(XML_TAGS.username) ?? string.Empty;
            string encryptedPassword = reader.GetAttribute(XML_TAGS.encrypted_password);
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
                password = reader.GetAttribute(XML_TAGS.password) ?? string.Empty;
            }
            string uriText = reader.GetAttribute(XML_TAGS.url);

            // Consume tag
            reader.Read();
            var encrypt = encryptedPassword != null || string.IsNullOrEmpty(password);
            return CreateFileSourceFromInputs(name, uriText, username, password, encrypt);
        }

        public static RemoteFileSource ReadXmlVersion_21_1(XmlReader reader)
        {
            // Read tag attributes
            var username = reader.GetAttribute(XML_TAGS.username) ?? string.Empty;
            string encryptedPassword = reader.GetAttribute(XML_TAGS.encrypted_password);
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
                password = reader.GetAttribute(XML_TAGS.password) ?? string.Empty;
            }
            string uriText = reader.GetAttribute(XML_TAGS.url);

            // Consume tag
            reader.Read();
            var encrypt = encryptedPassword != null || string.IsNullOrEmpty(password);
            return CreateFileSourceFromInputs(CreateNameFromUrl(uriText), uriText, username, password, encrypt);
        }

        public static RemoteFileSource ReadXmlVersion_20_2(XmlReader reader)
        {
            var username = reader.GetAttribute(XML_TAGS.username) ?? string.Empty;
            var password = reader.GetAttribute(XML_TAGS.password) ?? string.Empty;
            var url = reader.GetAttribute(OLD_XML_TAGS.uri);
            return CreateFileSourceFromInputs(CreateNameFromUrl(url), url, username, password, false);
        }

        private static RemoteFileSource CreateFileSourceFromInputs(string name, string uriText, string username, string password, bool encrypt)
        {
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
            var remoteFileSource = new RemoteFileSource(name, uri, username, password, encrypt);
            return remoteFileSource;
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XML_EL);
            writer.WriteAttributeIfString(XML_TAGS.name, Name);
            writer.WriteAttribute(XML_TAGS.url, URI);
            writer.WriteAttributeIfString(XML_TAGS.username, Username);
            if (Encrypt && !string.IsNullOrEmpty(Password))
                writer.WriteAttributeIfString(XML_TAGS.encrypted_password, TextUtil.EncryptPassword(Password));
            else
                writer.WriteAttributeIfString(XML_TAGS.password, Password);
            writer.WriteEndElement();
        }
        #endregion

        public bool Equivalent(RemoteFileSource other)
        {
            return Equals(new RemoteFileSource(Name, other.URI, other.Username, other.Password, other.Encrypt));
        }

        #region object overrides

        private bool Equals(RemoteFileSource other)
        {
            return string.Equals(Name, other.Name) && 
                   string.Equals(Username, other.Username) &&
                   string.Equals(Password, other.Password) &&
                   Equals(URI, other.URI) &&
                   Equals(Encrypt, other.Encrypt) &&
                   Equals(FtpSource, other.FtpSource);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is RemoteFileSource && Equals((RemoteFileSource)obj);
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