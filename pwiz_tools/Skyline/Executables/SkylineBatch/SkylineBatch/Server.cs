using SharedBatch;
using System;
using System.IO;
using System.Net;
using System.Text;
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

                    ProgressHandler((int) ((double) fileSize / expectedSize * 100),
                        lastFileSize == fileSize
                            ? new Exception(Resources.WebDownloadClient_DownloadAsync_Operation_timed_out_)
                            : null);
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