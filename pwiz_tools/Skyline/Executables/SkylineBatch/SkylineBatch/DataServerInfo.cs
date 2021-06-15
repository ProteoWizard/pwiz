using System;
using System.IO;
using System.Net;
using System.Xml;
using FluentFTP;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class DataServerInfo : Server, IEquatable<Object>
    {
        
        public static DataServerInfo ServerFromUi(string url, string userName, string password, bool encrypt, string namingPattern, string folder)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException(Resources.DataServerInfo_ServerFromUi_The_URL_cannot_be_empty__Please_enter_a_URL_);
            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (Exception)
            {
                throw new ArgumentException(Resources.DataServerInfo_ServerFromUi_Error_parsing_the_URL__Please_correct_the_URL_and_try_again_);
            }
            ValidateUsernamePassword(userName, password);

            return new DataServerInfo(uri, userName, password, encrypt, namingPattern, folder);
        }

        public static DataServerInfo ReplaceFolder(DataServerInfo other, string newFolder)
        {
            return new DataServerInfo(other.URI, other.Username, other.Password, other.Encrypt, other.DataNamingPattern, newFolder);
        }

        public DataServerInfo(Uri server, string userName, string password, bool encrypt, string namingPattern, string folder) : base(server, userName, password, encrypt)
        {
            DataNamingPattern = namingPattern ?? ".*";
            Folder = folder;
        }

        public readonly string DataNamingPattern;
        public readonly string Folder;

        public string GetUrl() => URI.AbsoluteUri;
        

        public string FilePath(string fileName) =>
            string.IsNullOrEmpty(URI.AbsolutePath) ? fileName : Path.Combine(URI.AbsolutePath, fileName);

        public FtpClient GetFtpClient()
        {
            var client = new FtpClient(URI.Host);

            if (!string.IsNullOrEmpty(Password))
            {
                if (!string.IsNullOrEmpty(Username))
                    client.Credentials = new NetworkCredential(Username, Password);
                else
                    client.Credentials = new NetworkCredential("anonymous", Password);
            }

            return client;
        }

        public void AddDownloadingFiles(ServerFilesManager serverFiles, string dataFolder)
        {
            serverFiles.AddServer(this);
        }


        public DataServerInfo Copy()
        {
            return new DataServerInfo(URI, Username, Password, Encrypt, DataNamingPattern, Folder);
        }

        public static void ValidateUsernamePassword(string username, string password)
        {
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
                return;
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException(Resources.DataServerInfo_ValidateUsernamePassword_Username_cannot_be_empty_if_the_server_has_a_password__Please_enter_a_username_);
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException(Resources.DataServerInfo_ValidateUsernamePassword_Password_cannot_be_empty_if_the_server_has_a_username__Please_enter_a_password_);
        }

        public new void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XMLElements.REMOTE_FILE_SET);
            base.WriteXml(writer);
            if (!DataNamingPattern.Equals(".*"))
                writer.WriteAttributeIfString(XML_TAGS.data_naming_pattern, DataNamingPattern);
            writer.WriteEndElement();
        }

        public static DataServerInfo ReadXml(XmlReader reader, string folder)
        {
            if (!reader.ReadToDescendant(XMLElements.REMOTE_FILE_SET))
                return null;
            var dataNamingPattern = reader.GetAttribute(XML_TAGS.data_naming_pattern);
            var server = Server.ReadXml(reader);
            return new DataServerInfo(server.URI, server.Username, server.Password, server.Encrypt, dataNamingPattern, folder);
        }

        /*protected bool Equals(DataServerInfo other)
        {
            return string.Equals(Username, other.Username) &&
                   string.Equals(Password, other.Password) &&
                   Equals(URI, other.URI) &&
                   string.Equals(other.DataNamingPattern, DataNamingPattern);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DataServerInfo)obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }*/

    }
}
