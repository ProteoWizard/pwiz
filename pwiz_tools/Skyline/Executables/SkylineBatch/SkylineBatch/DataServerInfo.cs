using System;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Xml;
using FluentFTP;
using SharedBatch;

namespace SkylineBatch
{
    public class DataServerInfo : Server, IEquatable<Object>
    {

        public static DataServerInfo ReplaceFolder(DataServerInfo other, string newFolder)
        {
            return new DataServerInfo(other.FileSource, other.RelativePath, other.DataNamingPattern, newFolder);
        }

        public DataServerInfo(RemoteFileSource remoteFileSource, string relativePath, string namingPattern, string folder) : base(remoteFileSource, relativePath)
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
            if (!string.IsNullOrEmpty(FileSource.Password))
                client.Credentials = new NetworkCredential(FileSource.Username, FileSource.Password);
            return client;
        }

        public void AddDownloadingFiles(ServerFilesManager serverFiles, string dataFolder)
        {
            serverFiles.AddServer(this);
        }


        public DataServerInfo Copy()
        {
            return new DataServerInfo(FileSource, RelativePath, DataNamingPattern, Folder);
        }

        public new DataServerInfo UpdateRemoteFileSet(ImmutableDictionary<string, RemoteFileSource> remoteFileSources, out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources)
        {
            newRemoteFileSources = remoteFileSources;
            var newFileSource = FileSource.UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources);
            return new DataServerInfo(newFileSource, RelativePath, DataNamingPattern, Folder);
        }

        public DataServerInfo ReplacedRemoteFileSource(RemoteFileSource existingSource, RemoteFileSource newSource, out bool replaced)
        {
            var newFileSource = FileSource.ReplacedRemoteFileSource(existingSource, newSource, out replaced);
            return new DataServerInfo(newFileSource, RelativePath, DataNamingPattern, Folder);
        }

        public new void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XMLElements.REMOTE_FILE_SET);
            if (!DataNamingPattern.Equals(".*"))
                writer.WriteAttributeIfString(XML_TAGS.data_naming_pattern, DataNamingPattern);
            base.WriteXml(writer);
            writer.WriteEndElement();
        }

        public static DataServerInfo ReadXml(XmlReader reader, string folder)
        {
            if (!reader.ReadToDescendant(XMLElements.REMOTE_FILE_SET))
                return null;
            var dataNamingPattern = reader.GetAttribute(XML_TAGS.data_naming_pattern);
            var server = Server.ReadXml(reader);
            return new DataServerInfo(server.FileSource, server.RelativePath, dataNamingPattern, folder);
        }

        public static DataServerInfo ReadXmlVersion_21_1(XmlReader reader, string folder)
        {
            if (!reader.ReadToDescendant(XMLElements.REMOTE_FILE_SET))
                return null;
            var dataNamingPattern = reader.GetAttribute(XML_TAGS.data_naming_pattern);
            var server = Server.ReadXmlVersion_21_1(reader);
            return new DataServerInfo(server.FileSource, server.RelativePath, dataNamingPattern, folder);
        }

        public static DataServerInfo ReadXmlVersion_20_2(XmlReader reader, string folder)
        {
            var serverName = reader.GetAttribute(OLD_XML_TAGS.ServerUrl);
            var uriString = reader.GetAttribute(OLD_XML_TAGS.ServerUri);
            if (string.IsNullOrEmpty(serverName) && string.IsNullOrEmpty(uriString))
                return null;
            var serverFolder = reader.GetAttribute(OLD_XML_TAGS.ServerFolder);
            var uri = !string.IsNullOrEmpty(uriString) ? new Uri(uriString) : new Uri($@"ftp://{serverName}/{serverFolder}");
            var username = reader.GetAttribute(OLD_XML_TAGS.ServerUserName);
            var password = reader.GetAttribute(OLD_XML_TAGS.ServerPassword);
            var dataNamingPattern = reader.GetAttribute(OLD_XML_TAGS.DataNamingPattern);
            var remoteFileSource = new RemoteFileSource(RemoteFileSource.CreateNameFromUrl(uri.AbsoluteUri), uri, username, password, false);
            return new DataServerInfo(remoteFileSource, string.Empty, dataNamingPattern, folder);
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
