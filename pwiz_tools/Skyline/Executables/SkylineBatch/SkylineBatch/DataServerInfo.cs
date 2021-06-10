﻿using System;
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
        
        public static DataServerInfo ServerFromUi(string url, string userName, string password, string namingPattern, string folder)
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
            ValidateNamingPattern(namingPattern);

            return new DataServerInfo(uri, userName, password, namingPattern, folder);
        }

        private DataServerInfo(Uri server, string userName, string password, string namingPattern, string folder) : base(server, userName, password)
        {
            //_server = new Server(server, userName, password);
            DataNamingPattern = namingPattern ?? string.Empty;
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
            return new DataServerInfo(URI, Username, Password, DataNamingPattern, Folder);
        }

        public static void ValidateNamingPattern(string dataNamingPattern)
        {
            if (string.IsNullOrEmpty(dataNamingPattern))
                throw new ArgumentException(Resources.DataServerInfo_ValidateNamingPattern_A_data_naming_pattern_is_required_for_downloaded_data__Please_add_a_data_naming_pattern_);
        }

        private enum Attr
        {
            ServerUri,
            ServerUrl, // deprecated
            ServerFolder, // deprecated
            ServerUserName,
            ServerPassword,
            DataNamingPattern
        };

        public new void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("data_server");
            writer.WriteAttributeIfString(Attr.ServerUri, URI.AbsoluteUri);
            writer.WriteAttributeIfString(Attr.ServerUserName, Username);
            writer.WriteAttributeIfString(Attr.ServerPassword, Password);
            writer.WriteAttributeIfString(Attr.DataNamingPattern, DataNamingPattern);
            writer.WriteEndElement();
        }

        public static DataServerInfo ReadOldXml(XmlReader reader, string folder)
        {
            var serverName = reader.GetAttribute(Attr.ServerUrl);
            if (serverName == null) return null;
            return ReadXmlFields(reader, folder);
        }

        public static DataServerInfo ReadXml(XmlReader reader, string folder)
        {
            if (XmlUtil.ReadNextElement(reader, "data_server"))
                return ReadXmlFields(reader, folder);
            return null;
        }

        private static DataServerInfo ReadXmlFields(XmlReader reader, string folder)
        {
            var serverName = reader.GetAttribute(Attr.ServerUrl);
            var uriString = reader.GetAttribute(Attr.ServerUri);
            if (string.IsNullOrEmpty(serverName) && string.IsNullOrEmpty(uriString))
                return null;
            var serverFolder = reader.GetAttribute(Attr.ServerFolder);
            var uri = !string.IsNullOrEmpty(uriString) ? new Uri(uriString) : new Uri($@"ftp://{serverName}/{serverFolder}");
            var username = reader.GetAttribute(Attr.ServerUserName);
            var password = reader.GetAttribute(Attr.ServerPassword);
            var dataNamingPattern = reader.GetAttribute(Attr.DataNamingPattern);
            return new DataServerInfo(uri, username, password, dataNamingPattern, folder);
        }

        protected bool Equals(DataServerInfo other)
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
            return DataNamingPattern.GetHashCode();
        }

    }
}
