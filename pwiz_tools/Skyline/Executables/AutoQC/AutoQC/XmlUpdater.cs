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
            SavedConfigsFilePath, // deprecated since SkylineBatch release 20.2.0.475
            SavedPathRoot
        }

        public static string GetUpdatedXml(string oldFile, string newVersion)
        {
            var guid = Guid.NewGuid();
            var uniqueFileName = guid + TextUtil.EXT_TMP;
            var filePath = Path.Combine(Path.GetDirectoryName(oldFile) ?? string.Empty, uniqueFileName);

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

            using (var streamWriter = new StreamWriter(File.Create(filePath)))
            {
                using (var writer = XmlWriter.Create(streamWriter, new XmlWriterSettings { Indent = true, NewLineChars = Environment.NewLine }))
                {
                    writer.WriteStartElement("config_list");
                    writer.WriteAttributeIfString(ConfigManager.Attr.saved_path_root, oldFolder);
                    writer.WriteAttributeString(ConfigManager.Attr.version, newVersion);

                    foreach (var config in configList)
                    {
                        config.WriteXml(writer);
                    }
                    writer.WriteEndElement();
                }
            }

            return filePath;
        }
    }
}
