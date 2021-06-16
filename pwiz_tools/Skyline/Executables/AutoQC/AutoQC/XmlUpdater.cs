using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using SharedBatch;

namespace AutoQC
{
    public class XmlUpdater
    {

        private enum OLD_XML_TAGS
        {
            SavedConfigsFilePath, // deprecated since SkylineBatch release 20.2.0.475
            SavedPathRoot,
            Type,
            CmdPath
        }
        



        public static string GetUpdatedXml(string oldFile)
        {
            Guid guid = Guid.NewGuid();
            string uniqueFileName = guid + TextUtil.EXT_TMP;
            var filePath = Path.Combine(Path.GetDirectoryName(oldFile) ?? string.Empty, uniqueFileName);

            var stream = new FileStream(oldFile, FileMode.Open, FileAccess.Read);
            var reader = XmlReader.Create(stream);
            var file = File.Create(filePath);
            var streamWriter = new StreamWriter(file);
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineChars = Environment.NewLine;
            XmlWriter writer = XmlWriter.Create(streamWriter, settings);

            while (!reader.Name.Equals("ConfigList"))
                reader.Read();
            var oldConfigFile = reader.GetAttribute(OLD_XML_TAGS.SavedConfigsFilePath);
            var oldFolder = reader.GetAttribute(OLD_XML_TAGS.SavedPathRoot);
            if (!string.IsNullOrEmpty(oldConfigFile) && string.IsNullOrEmpty(oldFolder))
                oldFolder = Path.GetDirectoryName(oldConfigFile);

            writer.WriteStartElement("config_list");
            writer.WriteAttributeIfString(Attr.saved_path_root, oldFolder);
            writer.WriteAttributeString(Attr.version, SharedBatch.Properties.Settings.Default.ProgramVersion);


            while (reader.IsStartElement())
            {
                if (reader.Name.EndsWith("_config"))
                {
                    var name = reader.GetAttribute(Attr.name);

                    var isEnabled = reader.GetBoolAttribute(Attr.is_enabled);
                    DateTime dateTime;
                    DateTime.TryParse(reader.GetAttribute(Attr.created), out dateTime);
                    var created = dateTime;
                    DateTime.TryParse(reader.GetAttribute(Attr.modified), out dateTime);
                    var modified = dateTime;

                    SharedBatch.XmlUtil.ReadUntilElement(reader);

                    MainSettings mainSettings = null;
                    PanoramaSettings panoramaSettings = null;
                    SkylineSettings skylineSettings = null;
                    mainSettings = MainSettings.ReadXml(reader);
                    do
                    {
                        reader.Read();
                    } while (reader.NodeType != XmlNodeType.Element);
                    panoramaSettings = PanoramaSettings.ReadXml(reader);
                    do
                    {
                        reader.Read();

                        if (reader.Name.Equals(AutoQcConfig.AUTOQC_CONFIG)) // handles old configurations without skyline settings
                        {
                            skylineSettings = new SkylineSettings(SkylineType.Skyline);
                            break;
                        }
                    } while (reader.NodeType != XmlNodeType.Element);
                    skylineSettings = skylineSettings ?? ReadOldSkylineSettings(reader);
                    new AutoQcConfig(name, isEnabled, created, modified, mainSettings, panoramaSettings, skylineSettings).WriteXml(writer);
                }

                reader.Read();
                reader.Read();
            }
            writer.WriteEndElement();


            reader.Dispose();
            stream.Dispose();
            writer.Dispose();
            streamWriter.Dispose();
            file.Dispose();


            return filePath;
        }



        private static SkylineSettings ReadOldSkylineSettings(XmlReader reader)
        {
            if (!SharedBatch.XmlUtil.ReadNextElement(reader, "config_skyline_settings"))
                throw new Exception("The bcfg file is from an earlier version of Skyline Batch and could not be loaded.");
            var type = (SkylineType)Enum.Parse(typeof(SkylineType), reader.GetAttribute(OLD_XML_TAGS.Type), false);
            var cmdPath = Path.GetDirectoryName(reader.GetAttribute(OLD_XML_TAGS.CmdPath));
            reader.Read();
            return new SkylineSettings(type, cmdPath);
        }

        enum Attr
        {
            saved_path_root,
            version,

            name,
            is_enabled,
            created,
            modified,

        }




    }
}
