using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using SharedBatch;
using SharedBatch.Properties;

namespace SkylineBatch
{
    class XmlUpdater
    {

        public enum OLD_XML_TAGS
        {
            SavedConfigsFilePath, // deprecated since SkylineBatch release 20.2.0.475
            SavedPathRoot,

            Name,
            Enabled,
            Modified,

            TemplateFilePath,
            DependentConfigName,
            AnalysisFolderPath,
            DataFolderPath,
            AnnotationsFilePath,
            ReplicateNamingPattern,

            FilePath,
            ZipFilePath,

            DownloadFolder,
            FileName,

            MsOneResolvingPower,
            MsMsResolvingPower,
            RetentionTime,
            AddDecoys,
            ShuffleDecoys,
            TrainMProphet,

            RemoveDecoys,
            RemoveResults,
            OutputFilePath,

            //Name,
            CultureSpecific,
            Path,
            UseRefineFile,

            Type,
            CmdPath,

            ServerUrl,
            ServerUri,
            ServerUserName,
            ServerPassword,
            ServerFolder,
            DataNamingPattern,
            uri,
        }

        enum Attr
        {
            saved_path_root,
            version,
            xml_version
        }


        public static string GetUpdatedXml(string file, decimal currentXmlVersion)
        {
            // TODO: handle ArgumentException from this
            //decimal importingXmlVersion = -1;
            var readConfigs = new List<SkylineBatchConfig>();
            string oldFolder = null;
            using (var stream = new FileStream(file, FileMode.Open))
            {
                using (var reader = XmlReader.Create(stream))
                {
                    while (reader.Read())
                    {
                        if (reader.Name.Equals("config_list") || reader.Name.Equals("ConfigList"))
                        {
                            var importingXmlVersion = reader.GetAttribute(Attr.xml_version) != null ? Convert.ToDecimal(reader.GetAttribute(Attr.xml_version)) : -1;
                            var importingVersion = reader.GetAttribute(Attr.version);
                            if (importingXmlVersion < 0)
                                importingXmlVersion = importingVersion != null ? 21.1M : 20.2M;

                            if (importingXmlVersion == currentXmlVersion)
                                return file;
                            if (importingXmlVersion > currentXmlVersion)
                            {
                                throw new ArgumentException(string.Format(
                                    Resources
                                        .ConfigManager_ImportFrom_The_version_of_the_file_to_import_from__0__is_newer_than_the_version_of_the_program__1___Please_update_the_program_to_import_configurations_from_this_file_,
                                    importingXmlVersion, currentXmlVersion));
                            }

                            var oldConfigFile = reader.GetAttribute(OLD_XML_TAGS.SavedConfigsFilePath);
                            oldFolder = reader.GetAttribute(OLD_XML_TAGS.SavedPathRoot);
                            if (!string.IsNullOrEmpty(oldConfigFile) && string.IsNullOrEmpty(oldFolder))
                                oldFolder = Path.GetDirectoryName(oldConfigFile);

                            while (!reader.Name.EndsWith("_config") && !reader.EOF)
                            {
                                if (reader.Name == "userSettings" && !reader.IsStartElement())
                                    break; // there are no configurations in the file
                                reader.Read();
                            }

                            while (reader.IsStartElement())
                            {
                                if (reader.Name.EndsWith("_config"))
                                {
                                    readConfigs.Add(SkylineBatchConfig.ReadXml(reader, importingXmlVersion));
                                }

                                reader.Read();
                                reader.Read();
                            }
                        }
                    }
                }
            }

            

            var newFile = CreateTempFile(file);
            using (var streamWriter = new StreamWriter(newFile))
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.NewLineChars = Environment.NewLine;
                using (XmlWriter writer = XmlWriter.Create(streamWriter, settings))
                {
                    writer.WriteStartElement("config_list");
                    writer.WriteAttributeString(Attr.saved_path_root, oldFolder);
                    writer.WriteAttribute(Attr.xml_version, currentXmlVersion);
                    foreach (var config in readConfigs)
                        config.WriteXml(writer);
                    writer.WriteEndElement();
                }
            }
                
            

            return newFile;
        }

        private static string CreateTempFile(string oldFile)
        {
            Guid guid = Guid.NewGuid();
            string uniqueFileName = guid + TextUtil.EXT_TMP;
            var filePath = Path.Combine(Path.GetDirectoryName(oldFile) ?? string.Empty, uniqueFileName);
            using (File.Create(filePath))
            {
            }
            return filePath;
        }
    }
}
