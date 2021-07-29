using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using SharedBatch;
using SharedBatch.Properties;

namespace SkylineBatch
{
    class XmlUpdater
    {

        private enum OLD_XML_TAGS
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
            decimal importingXmlVersion = -1;
            using (var stream = new FileStream(file, FileMode.Open))
            {
                using (var reader = XmlReader.Create(stream))
                {
                    while (reader.Read())
                    {
                        if (reader.Name.Equals("config_list") || reader.Name.Equals("ConfigList"))
                        {
                            importingXmlVersion = reader.GetAttribute(Attr.xml_version) != null ? Convert.ToDecimal(reader.GetAttribute(Attr.xml_version)) : -1;
                            var importingVersion = reader.GetAttribute(Attr.version);
                            if (importingXmlVersion < 0)
                                importingXmlVersion = importingVersion != null ? 21.1M : 20.2M;
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
                case 21.1M:
                    return UpdateVersion21_1(file, currentXmlVersion);
                case 20.2M:
                    return UpdateVersion20_2(file, currentXmlVersion);
                default:
                    return UpdateVersion20_2(file, currentXmlVersion);
            }

        }

        #region 21.1

        private static string UpdateVersion21_1(string oldFile, decimal currentXmlVersion)
        {
            var newFile = CreateTempFile(oldFile);
            string text = File.ReadAllText(oldFile);
            var versionRegex = new Regex("version=\"[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+\"");
            text = versionRegex.Replace(text, $"{Attr.xml_version}=\"{currentXmlVersion}\"");
            File.WriteAllText(newFile, text);
            return newFile;
        }

        #endregion

        #region 20.2

        private static string UpdateVersion20_2(string oldFile, decimal currentXmlVersion)
        {
            var stream = new FileStream(oldFile, FileMode.Open);
            var reader = XmlReader.Create(stream);
            var filePath = CreateTempFile(oldFile);
            var file = File.OpenWrite(filePath);
            var streamWriter = new StreamWriter(file);
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineChars = Environment.NewLine;
            XmlWriter writer = XmlWriter.Create(streamWriter, settings);

            var readConfigList = false;
            while (reader.Read())
            {
                if (reader.Name.Equals("ConfigList") || reader.Name.Equals("config_list"))
                {
                    var oldConfigFile = reader.GetAttribute(OLD_XML_TAGS.SavedConfigsFilePath);
                    var oldFolder = reader.GetAttribute(OLD_XML_TAGS.SavedPathRoot);
                    if (!string.IsNullOrEmpty(oldConfigFile) && string.IsNullOrEmpty(oldFolder))
                        oldFolder = Path.GetDirectoryName(oldConfigFile);

                    writer.WriteStartElement("config_list");
                    writer.WriteAttributeIfString(Attr.saved_path_root, oldFolder);
                    writer.WriteAttribute(Attr.xml_version, currentXmlVersion);


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
                            WriteNewConfig(reader, writer);
                        }

                        reader.Read();
                        reader.Read();
                    }
                    writer.WriteEndElement();
                    readConfigList = true;
                }
            }

            if (!readConfigList)
            {
                writer.WriteStartElement("config_list");
                writer.WriteAttribute(Attr.xml_version, currentXmlVersion);
                writer.WriteEndElement();
            }

            writer.Dispose();
            streamWriter.Dispose();
            file.Dispose();
            reader.Dispose();
            stream.Dispose();
            
            return filePath;
        }

        private static void WriteNewConfig(XmlReader reader, XmlWriter writer)
        {
            var name = reader.GetAttribute(OLD_XML_TAGS.Name);
            var enabled = reader.GetBoolAttribute(OLD_XML_TAGS.Enabled);
            var modified = reader.GetAttribute(OLD_XML_TAGS.Modified);

            writer.WriteStartElement(XMLElements.BATCH_CONFIG);
            writer.WriteAttribute(XML_TAGS.name, name);
            writer.WriteAttribute(XML_TAGS.enabled, enabled);
            writer.WriteAttributeIfString(XML_TAGS.modified, modified);
            WriteNewMainSettings(reader, writer);
            WriteNewFileSettings(reader, writer);
            WriteNewRefineSettings(reader, writer);
            WriteNewReportSettings(reader, writer);
            WriteNewSkylineSettings(reader, writer);
            writer.WriteEndElement();

        }

        private static void WriteNewMainSettings(XmlReader reader, XmlWriter writer)
        {
            XmlUtil.ReadUntilElement(reader);
            // get values from old xml
            var mainSettingsReader = reader.ReadSubtree();
            mainSettingsReader.Read();
            var templateFilePath = mainSettingsReader.GetAttribute(OLD_XML_TAGS.TemplateFilePath);
            string zippedFilePath = null;
            var dependentConfigName = mainSettingsReader.GetAttribute(OLD_XML_TAGS.DependentConfigName);
            PanoramaFile templatePanoramaFile = null;
            var analysisFolderPath = mainSettingsReader.GetAttribute(OLD_XML_TAGS.AnalysisFolderPath);
            var dataFolderPath = mainSettingsReader.GetAttribute(OLD_XML_TAGS.DataFolderPath);
            var annotationsFilePath = mainSettingsReader.GetAttribute(OLD_XML_TAGS.AnnotationsFilePath);
            var replicateNamingPattern = mainSettingsReader.GetAttribute(OLD_XML_TAGS.ReplicateNamingPattern);

            ReadDataServerXmlFields(mainSettingsReader, out Server dataServer, out string dataNamingPattern);

            if (templateFilePath == null)
            {
                XmlUtil.ReadNextElement(mainSettingsReader, "template_file");
                templateFilePath = mainSettingsReader.GetAttribute(OLD_XML_TAGS.FilePath);
                zippedFilePath = mainSettingsReader.GetAttribute(OLD_XML_TAGS.ZipFilePath);
                dependentConfigName = mainSettingsReader.GetAttribute(OLD_XML_TAGS.DependentConfigName);
                templatePanoramaFile = ReadOldPanoramaFile(mainSettingsReader);
            }
            if (XmlUtil.ReadNextElement(mainSettingsReader, "data_server"))
            {
                ReadDataServerXmlFields(mainSettingsReader, out dataServer, out dataNamingPattern);
            }
            

            // write main settings
            writer.WriteStartElement(XMLElements.MAIN_SETTINGS);
            writer.WriteAttributeIfString(XML_TAGS.analysis_folder_path, analysisFolderPath);
            writer.WriteAttributeIfString(XML_TAGS.replicate_naming_pattern, replicateNamingPattern);
            // write template file
            writer.WriteStartElement(XMLElements.TEMPLATE_FILE);
            writer.WriteAttributeIfString(XML_TAGS.path, templateFilePath);
            writer.WriteAttributeIfString(XML_TAGS.zip_path, zippedFilePath);
            writer.WriteAttributeIfString(XML_TAGS.dependent_configuration, dependentConfigName);
            if (templatePanoramaFile != null) templatePanoramaFile.WriteXml(writer);
            writer.WriteEndElement();
            // write data folder
            writer.WriteStartElement(XMLElements.DATA_FOLDER);
            writer.WriteAttributeIfString(XML_TAGS.path, dataFolderPath);
            if (dataServer != null)
            {
                writer.WriteStartElement(XMLElements.REMOTE_FILE_SET);
                dataServer.WriteXml(writer);
                if (dataNamingPattern != null && !dataNamingPattern.Equals(".*"))
                    writer.WriteAttributeIfString(XML_TAGS.data_naming_pattern, dataNamingPattern);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            // write annotations file
            writer.WriteStartElement(XMLElements.ANNOTATIONS_FILE);
            writer.WriteAttributeIfString(XML_TAGS.path, annotationsFilePath);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        private static void ReadDataServerXmlFields(XmlReader reader, out Server server, out string dataNamingPattern)
        {
            server = null;
            dataNamingPattern = null;

            var serverName = reader.GetAttribute(OLD_XML_TAGS.ServerUrl);
            var uriString = reader.GetAttribute(OLD_XML_TAGS.ServerUri);
            if (string.IsNullOrEmpty(serverName) && string.IsNullOrEmpty(uriString))
                return;
            var serverFolder = reader.GetAttribute(OLD_XML_TAGS.ServerFolder);
            var uri = !string.IsNullOrEmpty(uriString) ? new Uri(uriString) : new Uri($@"ftp://{serverName}/{serverFolder}");
            var username = reader.GetAttribute(OLD_XML_TAGS.ServerUserName);
            var password = reader.GetAttribute(OLD_XML_TAGS.ServerPassword);
            server = new Server(uri, username, password, false);
            dataNamingPattern = reader.GetAttribute(OLD_XML_TAGS.DataNamingPattern);
        }

        public static PanoramaFile ReadOldPanoramaFile(XmlReader reader)
        {
            if (!reader.ReadToDescendant("panorama_file")) return null;
            var downloadFolder = reader.GetAttribute(OLD_XML_TAGS.DownloadFolder);
            var fileName = reader.GetAttribute(OLD_XML_TAGS.FileName);
            var server = ReadOldServer(reader);

            return new PanoramaFile(server, downloadFolder, fileName);
        }

        public static Server ReadOldServer(XmlReader reader)
        {
            var username = reader.GetAttribute(XML_TAGS.username) ?? string.Empty;
            var password = reader.GetAttribute(XML_TAGS.password);
            var url = reader.GetAttribute(OLD_XML_TAGS.uri);
            return new Server(url, username, password, false);
        }

        private static void WriteNewFileSettings(XmlReader reader, XmlWriter writer)
        {
            var fileSettings = new FileSettings(null, null, null, false, false, false);
            if (XmlUtil.ReadNextElement(reader, "file_settings"))
            {
                var msOneResolvingPower = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(OLD_XML_TAGS.MsOneResolvingPower));
                var msMsResolvingPower = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(OLD_XML_TAGS.MsMsResolvingPower));
                var retentionTime = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(OLD_XML_TAGS.RetentionTime));
                var addDecoys = reader.GetBoolAttribute(OLD_XML_TAGS.AddDecoys);
                var shuffleDecoys = reader.GetBoolAttribute(OLD_XML_TAGS.ShuffleDecoys);
                var trainMProphet = reader.GetBoolAttribute(OLD_XML_TAGS.TrainMProphet);

                fileSettings = new FileSettings(msOneResolvingPower, msMsResolvingPower, retentionTime, addDecoys, shuffleDecoys, trainMProphet);
            }
            fileSettings.WriteXml(writer);
        }

        private static void WriteNewRefineSettings(XmlReader reader, XmlWriter writer)
        {
            RefineSettings refineSettings;
            if (!XmlUtil.ReadNextElement(reader, "refine_settings"))
            {
                // This is a very old configuration with no refine settings
                refineSettings = new RefineSettings(new RefineInputObject(), false, false,
                    string.Empty);
            }
            else
            {
                var removeDecoys = reader.GetBoolAttribute(OLD_XML_TAGS.RemoveDecoys);
                var removeResults = reader.GetBoolAttribute(OLD_XML_TAGS.RemoveResults);
                var outputFilePath = reader.GetAttribute(OLD_XML_TAGS.OutputFilePath);
                var commandList = new List<Tuple<RefineVariable, string>>();
                while (reader.IsStartElement() && !reader.IsEmptyElement)
                {
                    if (reader.Name == "command_value")
                    {
                        var tupleItems = reader.ReadElementContentAsString().Split(new[] { '(', ',', ')' }, StringSplitOptions.RemoveEmptyEntries);
                        var variable = (RefineVariable)Enum.Parse(typeof(RefineVariable), tupleItems[0].Trim());
                        var value = tupleItems[1].Trim();
                        commandList.Add(new Tuple<RefineVariable, string>(variable, value));
                    }
                    else
                    {
                        reader.Read();
                    }
                }
                var commandValues = RefineInputObject.FromInvariantCommandList(commandList);
                refineSettings = new RefineSettings(commandValues, removeDecoys, removeResults, outputFilePath);
            }
            refineSettings.WriteXml(writer);
        }

        private static void WriteNewReportSettings(XmlReader reader, XmlWriter writer)
        {
            var reports = new List<ReportInfo>();
            if (XmlUtil.ReadNextElement(reader, "report_settings"))
            {
                while (reader.IsStartElement())
                {
                    if (XmlUtil.ReadNextElement(reader, "report_info"))
                    {
                        var name = reader.GetAttribute(OLD_XML_TAGS.Name);
                        var cultureSpecific = reader.GetBoolAttribute(OLD_XML_TAGS.CultureSpecific);
                        var reportPath = reader.GetAttribute(OLD_XML_TAGS.Path);
                        var resultsFile = reader.GetNullableBoolAttribute(OLD_XML_TAGS.UseRefineFile);
                        var rScripts = new List<Tuple<string, string>>();
                        while (reader.IsStartElement() && !reader.IsEmptyElement)
                        {
                            if (reader.Name == "script_path")
                            {
                                var tupleItems = reader.ReadElementContentAsString().Split(new[] {'(', ',', ')'},
                                    StringSplitOptions.RemoveEmptyEntries);
                                rScripts.Add(new Tuple<string, string>(tupleItems[0].Trim(), tupleItems[1].Trim()));
                            }
                            else
                            {
                                reader.Read();
                            }
                        }

                        reports.Add(new ReportInfo(name, cultureSpecific, reportPath, rScripts, new Dictionary<string, PanoramaFile>(), resultsFile ?? false));
                    }
                    else
                    {
                        break;
                    }

                    reader.Read();
                }
            }

            new ReportSettings(reports).WriteXml(writer);
        }


        private static void WriteNewSkylineSettings(XmlReader reader, XmlWriter writer)
        {
            if (!XmlUtil.ReadNextElement(reader, "config_skyline_settings"))
                throw new Exception("The bcfg file is from an earlier version of Skyline Batch and could not be loaded.");
            var type = reader.GetAttribute(OLD_XML_TAGS.Type);
            var cmdPath = reader.GetAttribute(OLD_XML_TAGS.CmdPath);
            reader.Read();
            writer.WriteStartElement("config_skyline_settings");
            writer.WriteAttributeIfString(XML_TAGS.type, type);
            if (type.Equals("Custom"))
                writer.WriteAttributeIfString(XML_TAGS.path, cmdPath);
            writer.WriteEndElement();
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
