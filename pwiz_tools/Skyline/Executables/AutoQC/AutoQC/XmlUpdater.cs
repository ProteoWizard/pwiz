using System;
using System.IO;
using System.Xml;
using SharedBatch;
using SharedBatch.Properties;

namespace AutoQC
{
    public class XmlUpdater
    {

        private enum OLD_XML_TAGS
        {
            SavedConfigsFilePath, // deprecated since 20.2.0.475
            SavedPathRoot,
            version
        }

        public static string GetUpdatedXml(string file, decimal currentXmlVersion)
        {
            decimal importingXmlVersion = -1;
            using (var stream = new FileStream(file, FileMode.Open))
            {
                using (var reader = XmlReader.Create(stream))
                {
                    while (reader.Read())
                    {
                        if (reader.Name.Equals("config_list") || reader.Name.Equals("ConfigList"))
                        {
                            importingXmlVersion = reader.GetAttribute(ConfigManager.Attr.xml_version) != null ? Convert.ToDecimal(reader.GetAttribute(ConfigManager.Attr.xml_version)) : -1;
                            var importingVersion = reader.GetAttribute(OLD_XML_TAGS.version);
                            if (importingXmlVersion < 0)
                                importingXmlVersion = importingVersion != null ? 21.1M : 20.1M;
                            break;
                        }
                    }
                }
            }

            if (importingXmlVersion == currentXmlVersion)
                return file;
            if (importingXmlVersion > currentXmlVersion)
            {
                throw new ArgumentException(string.Format(
                    Resources
                        .ConfigManager_ImportFrom_The_version_of_the_file_to_import_from__0__is_newer_than_the_version_of_the_program__1___Please_update_the_program_to_import_configurations_from_this_file_,
                    importingXmlVersion, currentXmlVersion));
            }


            switch (importingXmlVersion)
            {
                case 20.1M:
                    return UpdateVersion20_1(file, currentXmlVersion);
                default:
                    return UpdateVersion20_1(file, currentXmlVersion);
            }
        }


        #region 20.1

        private static string UpdateVersion20_1(string oldFile, decimal currentXmlVersion)
        {
            var newFile = CreateTempFile(oldFile);
            var configList = new ConfigList();
            string oldFolder = null;
            var inConfigList = false;
            using (var stream = new FileStream(oldFile, FileMode.Open, FileAccess.Read))
            {
                using (var reader = XmlReader.Create(stream))
                {
                    while (reader.Read())
                    {
                        if (reader.IsEndElement("ConfigList"))
                        {
                            break; // We are done reading the ConfigList
                        }

                        if (reader.IsElement("ConfigList"))
                        {
                            inConfigList = true;
                            var oldConfigFile = reader.GetAttribute(OLD_XML_TAGS.SavedConfigsFilePath);
                            oldFolder = reader.GetAttribute(OLD_XML_TAGS.SavedPathRoot);
                            if (!string.IsNullOrEmpty(oldConfigFile) && string.IsNullOrEmpty(oldFolder))
                            {
                                oldFolder = Path.GetDirectoryName(oldConfigFile);
                            }

                            continue;
                        }

                        if (inConfigList && reader.IsStartElement("autoqc_config"))
                        {
                            var config = AutoQcConfig.ReadXml_v21_1_0_158(reader);
                            configList.Add(config);
                        }
                    }
                }
            }

            using (var streamWriter = new StreamWriter(File.Create(newFile)))
            {
                using (var writer = XmlWriter.Create(streamWriter, new XmlWriterSettings { Indent = true, NewLineChars = Environment.NewLine }))
                {
                    writer.WriteStartElement("config_list");
                    writer.WriteAttributeIfString(ConfigManager.Attr.saved_path_root, oldFolder);
                    writer.WriteAttribute(ConfigManager.Attr.xml_version, currentXmlVersion);

                    foreach (var config in configList)
                    {
                        config.WriteXml(writer);
                    }
                    writer.WriteEndElement();
                }
            }
            return newFile;
        }

        #endregion


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
