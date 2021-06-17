using SharedBatch;
using System;
using System.IO;
using System.Net;
using System.Reflection.Emit;
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
                var progressChanged = new DownloadProgressChangedEventHandler((sender, e) => { ProgressHandler(e.ProgressPercentage, null); });
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
        public Server(string uriText, string username, string password, bool encrypt)
            : this(new Uri(uriText), username, password, encrypt)
        {
        }

        public Server(Uri uri, string username, string password, bool encrypt)
        {
            Username = username;
            Password = password;
            URI = uri;
            Encrypt = encrypt;
        }

        internal string Username { get; set; }
        internal string Password { get; set; }
        internal Uri URI { get; set; }

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

        public static Server ReadXml(XmlReader reader)
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
            var encrypt = encryptedPassword != null || string.IsNullOrEmpty(password);
            var server = new Server(uri, username, password, encrypt);
            server.Validate();
            return server;
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(XML_TAGS.url, URI);
            writer.WriteAttributeIfString(XML_TAGS.username, Username);
            if (Encrypt && !string.IsNullOrEmpty(Password))
                writer.WriteAttributeIfString(XML_TAGS.encrypted_password, TextUtil.EncryptPassword(Password));
            else
                writer.WriteAttributeIfString(XML_TAGS.password, Password);
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