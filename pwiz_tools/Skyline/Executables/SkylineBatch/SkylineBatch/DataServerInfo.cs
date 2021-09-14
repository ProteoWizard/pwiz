﻿using System;
using System.IO;
using System.Net;
using System.Xml;
using FluentFTP;
using SharedBatch;

namespace SkylineBatch
{
    public class DataServerInfo : Server, IEquatable<Object>
    {
        
        public static DataServerInfo ServerFromUi(string url, string userName, string password, bool encrypt, string namingPattern, string folder)
        {
            ValidateInputs(url, userName, password, out Uri uri);

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
            return new DataServerInfo(uri, username, password, false, dataNamingPattern, folder);
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
