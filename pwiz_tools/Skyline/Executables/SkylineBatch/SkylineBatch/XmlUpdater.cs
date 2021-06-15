using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using SharedBatch;

namespace SkylineBatch
{
    class XmlUpdater
    {/*

        #region ConfigManager


        public delegate DialogResult ShowDownloadedFileForm(string filePath, out string copiedDestination);

        // gets the list of importing configs
        protected List<IConfig> ImportFrom(string filePath, ShowDownloadedFileForm showDownloadedFileForm)
        {
            var copiedDestination = string.Empty;
            var copiedConfigFile = string.Empty;
            // TODO (Ali) uncomment this when data and templates can be downloaded
            /*
            if (filePath.Contains(FileUtil.DOWNLOADS_FOLDER))
            {
                var dialogResult = showDownloadedFileForm(filePath, out copiedDestination);
                if (dialogResult != DialogResult.Yes)
                    return new List<IConfig>();
                copiedConfigFile = Path.Combine(copiedDestination, Path.GetFileName(filePath));
                var file = new FileInfo(filePath);
                if (!File.Exists(copiedConfigFile))
                    file.CopyTo(copiedConfigFile, false);
            }* /

            var readConfigs = new List<IConfig>();
            var addedConfigs = new List<IConfig>();
            var readXmlErrors = new List<string>();
            // read configs from file
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (!reader.Name.Equals("ConfigList"))
                            reader.Read();
                        var oldConfigFile = reader.GetAttribute(Attr.SavedConfigsFilePath);
                        var oldFolder = reader.GetAttribute(Attr.SavedPathRoot);
                        if (!string.IsNullOrEmpty(oldConfigFile) && string.IsNullOrEmpty(oldFolder))
                            oldFolder = Path.GetDirectoryName(oldConfigFile);
                        if (!string.IsNullOrEmpty(oldFolder))
                        {
                            var newFolder = string.IsNullOrEmpty(copiedDestination)
                                ? Path.GetDirectoryName(filePath)
                                : Path.GetDirectoryName(copiedConfigFile);
                            AddRootReplacement(oldFolder, newFolder, false, out _, out _);
                        }

                        while (!reader.Name.EndsWith("_config"))
                        {
                            if (reader.Name == "userSettings" && !reader.IsStartElement())
                                break; // there are no configurations in the file
                            reader.Read();
                        }

                        while (reader.IsStartElement())
                        {
                            if (reader.Name.EndsWith("_config"))
                            {
                                IConfig config = null;
                                try
                                {
                                    config = importer(reader);
                                }
                                catch (Exception ex)
                                {
                                    readXmlErrors.Add(ex.Message);
                                }

                                if (config != null)
                                    readConfigs.Add(config);
                            }

                            reader.Read();
                            reader.Read();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // possible xml format error
                DisplayError(
                    string.Format(
                        Resources.ConfigManager_Import_An_error_occurred_while_importing_configurations_from__0__,
                        filePath) + Environment.NewLine +
                    e.Message);
                return addedConfigs;
            }

            if (readConfigs.Count == 0 && readXmlErrors.Count == 0)
            {
                // warn if no configs found
                DisplayWarning(string.Format(Resources.ConfigManager_Import_No_configurations_were_found_in__0__,
                    filePath));
                return addedConfigs;
            }

            var duplicateConfigNames = new List<string>();
            lock (_lock)
            {
                foreach (IConfig config in readConfigs)
                {
                    // Make sure that the configuration name is unique
                    if (_configValidation.Keys.Contains(config.GetName()))
                        duplicateConfigNames.Add(config.GetName());
                }
            }

            var message = new StringBuilder();
            if (duplicateConfigNames.Count > 0)
            {
                var duplicateMessage =
                    new StringBuilder(Resources.ConfigManager_ImportFrom_The_following_configurations_already_exist_)
                        .Append(Environment.NewLine);
                foreach (var name in duplicateConfigNames)
                    duplicateMessage.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);

                message.Append(duplicateMessage).Append(Environment.NewLine);
                duplicateMessage.Append(Resources
                    .ConfigManager_ImportFrom_Do_you_want_to_overwrite_these_configurations_);
                if (DialogResult.Yes == DisplayQuestion(duplicateMessage.ToString()))
                {
                    message.Append(Resources.ConfigManager_ImportFrom_Overwriting_).Append(Environment.NewLine);
                    duplicateConfigNames.Clear();
                }
            }

            var numAdded = 0;

            foreach (IConfig config in readConfigs)
            {
                if (duplicateConfigNames.Contains(config.GetName())) continue;
                var addingConfig = RunRootReplacement(config);
                addedConfigs.Add(addingConfig);
                numAdded++;
            }

            message.Append(Resources.ConfigManager_Import_Number_of_configurations_imported_);
            message.Append(numAdded).Append(Environment.NewLine);

            if (readXmlErrors.Count > 0)
            {
                var errorMessage = new StringBuilder(Resources
                        .ConfigManager_Import_Number_of_configurations_with_errors_that_could_not_be_imported_)
                    .Append(Environment.NewLine);
                foreach (var error in readXmlErrors)
                {
                    errorMessage.Append(error).Append(Environment.NewLine);
                }

                message.Append(errorMessage);
                DisplayError(errorMessage.ToString());
            }

            ProgramLog.Info(message.ToString());
            return addedConfigs;
        }

        public void ExportConfigs(string filePath, int[] indiciesToSave)
        {
            var state = new ConfigManagerState(this);
            var directory = string.Empty;
            // Exception if no configurations are selected to export
            if (indiciesToSave.Length == 0)
            {
                throw new ArgumentException(Resources.ConfigManager_ExportConfigs_There_is_no_configuration_selected_ +
                                            Environment.NewLine +
                                            Resources
                                                .ConfigManager_ExportConfigs_Please_select_a_configuration_to_share_);
            }
            try
            {
                directory = Path.GetDirectoryName(filePath);
            }
            catch (ArgumentException)
            {
                // pass
            }
            // Exception if file folder does not exist
            if (!Directory.Exists(directory))
                throw new ArgumentException(Resources.ConfigManager_ExportConfigs_Could_not_save_configurations_to_ +
                                            Environment.NewLine +
                                            filePath + Environment.NewLine +
                                            Resources
                                                .ConfigManager_ExportConfigs_Please_provide_a_path_to_a_file_inside_an_existing_folder_);

            using (var file = File.Create(filePath))
            {
                using (var streamWriter = new StreamWriter(file))
                {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.NewLineChars = Environment.NewLine;
                    using (XmlWriter writer = XmlWriter.Create(streamWriter, settings))
                    {
                        writer.WriteStartElement("ConfigList");
                        writer.WriteAttributeString(Attr.SavedPathRoot, directory);
                        foreach (int index in indiciesToSave)
                            state.configList[index].WriteXml(writer);
                        writer.WriteEndElement();
                    }
                }
            }
        }

        enum Attr
        {
            SavedConfigsFilePath, // deprecated since SkylineBatch release 20.2.0.475
            SavedPathRoot
        }


        #endregion

        
        #region SkylineBatchConfig

        private enum Attr
        {
            Name,
            Enabled,
            Modified
        }

        public static SkylineBatchConfig ReadXml(XmlReader reader)
        {
            var name = reader.GetAttribute(Attr.Name);
            var enabled = reader.GetBoolAttribute(Attr.Enabled);
            DateTime modified;
            DateTime.TryParse(reader.GetAttribute(Attr.Modified), CultureInfo.InvariantCulture, DateTimeStyles.None, out modified);

            XmlUtil.ReadUntilElement(reader);
            MainSettings mainSettings = null;
            RefineSettings refineSettings = RefineSettings.Empty();
            FileSettings fileSettings = FileSettings.Empty();
            ReportSettings reportSettings = new ReportSettings(new List<ReportInfo>());
            SkylineSettings skylineSettings = null;
            string exceptionMessage = null;
            try
            {
                mainSettings = MainSettings.ReadXml(reader);
                if (XmlUtil.ReadNextElement(reader, "file_settings"))
                {
                    fileSettings = FileSettings.ReadXml(reader);
                }
                if (XmlUtil.ReadNextElement(reader, "refine_settings"))
                {
                    refineSettings = RefineSettings.ReadXml(reader);
                }
                if (XmlUtil.ReadNextElement(reader, "report_settings"))
                    reportSettings = ReportSettings.ReadXml(reader);
                if (!XmlUtil.ReadNextElement(reader, "config_skyline_settings")) throw new Exception("Configuration does not have Skyline settings");
                skylineSettings = SkylineSettings.ReadXml(reader);
            }
            catch (ArgumentException e)
            {
                exceptionMessage = string.Format("\"{0}\" ({1})", name, e.Message);
            }

            do
            {
                reader.Read();
            } while (!(reader.Name == "skylinebatch_config" && reader.NodeType == XmlNodeType.EndElement));

            if (exceptionMessage != null)
                throw new ArgumentException(exceptionMessage);

            return new SkylineBatchConfig(name, enabled, modified, mainSettings, fileSettings,
                refineSettings, reportSettings, skylineSettings);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("skylinebatch_config");
            writer.WriteAttribute(Attr.Name, Name);
            writer.WriteAttribute(Attr.Enabled, Enabled);
            writer.WriteAttributeIfString(Attr.Modified, Modified.ToString(CultureInfo.InvariantCulture));
            MainSettings.WriteXml(writer);
            FileSettings.WriteXml(writer);
            RefineSettings.WriteXml(writer);
            ReportSettings.WriteXml(writer);
            SkylineSettings.WriteXml(writer);
            writer.WriteEndElement();
        }



        #endregion







        #region MainSettings

        private enum Attr
        {
            TemplateFilePath,
            DependentConfigName,
            AnalysisFolderPath,
            DataFolderPath,
            AnnotationsFilePath,
            ReplicateNamingPattern,
        };

        public static MainSettings ReadXml(XmlReader reader)
        {

            var mainSettingsReader = reader.ReadSubtree();
            mainSettingsReader.Read();
            var templateFilePath = mainSettingsReader.GetAttribute(MainSettings.Attr.TemplateFilePath);
            var dependentConfigName = mainSettingsReader.GetAttribute(MainSettings.Attr.DependentConfigName);
            var oldTemplate = templateFilePath != null
                ? SkylineTemplate.FromUi(templateFilePath, dependentConfigName, null)
                : null;
            var analysisFolderPath = mainSettingsReader.GetAttribute(MainSettings.Attr.AnalysisFolderPath);
            var dataFolderPath = mainSettingsReader.GetAttribute(MainSettings.Attr.DataFolderPath);
            var annotationsFilePath = mainSettingsReader.GetAttribute(MainSettings.Attr.AnnotationsFilePath);
            var replicateNamingPattern = mainSettingsReader.GetAttribute(MainSettings.Attr.ReplicateNamingPattern);
            var oldServer = DataServerInfo.ReadOldXml(mainSettingsReader, dataFolderPath);
            var template = oldTemplate ?? SkylineTemplate.ReadXml(mainSettingsReader);
            var server = oldServer ?? DataServerInfo.ReadXml(mainSettingsReader, dataFolderPath);
            var annotationsDownload = PanoramaFile.ReadXml(mainSettingsReader);
            return new MainSettings(template, analysisFolderPath, dataFolderPath, server,
                annotationsFilePath, annotationsDownload, replicateNamingPattern);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("main_settings");
            writer.WriteAttributeIfString(MainSettings.Attr.AnalysisFolderPath, AnalysisFolderPath);
            writer.WriteAttributeIfString(MainSettings.Attr.DataFolderPath, DataFolderPath);

            writer.WriteAttributeIfString(MainSettings.Attr.AnnotationsFilePath, AnnotationsFilePath);
            writer.WriteAttributeIfString(MainSettings.Attr.ReplicateNamingPattern, ReplicateNamingPattern);
            Template.WriteXml(writer);
            if (Server != null) Server.WriteXml(writer);
            if (AnnotationsDownload != null) AnnotationsDownload.WriteXml(writer);
            writer.WriteEndElement();
        }

        #endregion

        #region FileSettings

        private enum Attr
        {
            MsOneResolvingPower,
            MsMsResolvingPower,
            RetentionTime,
            AddDecoys,
            ShuffleDecoys,
            TrainMProphet
        };

        public static FileSettings ReadXml(XmlReader reader)
        {
            var msOneResolvingPower = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(Attr.MsOneResolvingPower));
            var msMsResolvingPower = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(Attr.MsMsResolvingPower));
            var retentionTime = TextUtil.GetNullableIntFromInvariantString(reader.GetAttribute(Attr.RetentionTime));
            var addDecoys = reader.GetBoolAttribute(Attr.AddDecoys);
            var shuffleDecoys = reader.GetBoolAttribute(Attr.ShuffleDecoys);
            var trainMProphet = reader.GetBoolAttribute(Attr.TrainMProphet);
            return new FileSettings(msOneResolvingPower, msMsResolvingPower, retentionTime, addDecoys, shuffleDecoys, trainMProphet);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("file_settings");
            writer.WriteAttributeIfString(Attr.MsOneResolvingPower,
                TextUtil.ToInvariantCultureString(MsOneResolvingPower));
            writer.WriteAttributeIfString(Attr.MsMsResolvingPower,
                TextUtil.ToInvariantCultureString(MsMsResolvingPower));
            writer.WriteAttributeIfString(Attr.RetentionTime,
                TextUtil.ToInvariantCultureString(RetentionTime));
            writer.WriteAttribute(Attr.AddDecoys, AddDecoys);
            writer.WriteAttribute(Attr.ShuffleDecoys, ShuffleDecoys);
            writer.WriteAttribute(Attr.TrainMProphet, TrainMProphet);
            writer.WriteEndElement();
        }

        #endregion

        #region RefineSettings

        private enum Attr
        {
            RemoveDecoys,
            RemoveResults,
            OutputFilePath
        };

        public static RefineSettings ReadXml(XmlReader reader)
        {
            if (!reader.Name.Equals("refine_settings"))
            {
                // This is an old configuration with no refine settings
                return new RefineSettings(new RefineInputObject(), false, false,
                    string.Empty);
            }
            var removeDecoys = reader.GetBoolAttribute(Attr.RemoveDecoys);
            var removeResults = reader.GetBoolAttribute(Attr.RemoveResults);
            var outputFilePath = reader.GetAttribute(Attr.OutputFilePath);
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
            return new RefineSettings(commandValues, removeDecoys, removeResults, outputFilePath);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("refine_settings");
            writer.WriteAttribute(Attr.RemoveDecoys, RemoveDecoys);
            writer.WriteAttribute(Attr.RemoveResults, RemoveResults);
            writer.WriteAttributeIfString(Attr.OutputFilePath, OutputFilePath);
            var commandList = _commandValues.AsCommandList(CultureInfo.InvariantCulture);
            foreach (var commandValue in commandList)
                writer.WriteElementString("command_value", commandValue);
            writer.WriteEndElement();
        }

        #endregion

        #region ReportSettings

        public static ReportSettings ReadXml(XmlReader reader)
        {
            var reports = new List<ReportInfo>();
            while (reader.IsStartElement())
            {
                if (reader.Name == "report_info")
                {
                    var report = ReportInfo.ReadXml(reader);
                    reports.Add(report);
                }
                else if (reader.IsEmptyElement)
                {
                    break;
                }

                reader.Read();
            }
            return new ReportSettings(reports);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("report_settings");
            foreach (var report in Reports)
            {
                report.WriteXml(writer);
            }
            writer.WriteEndElement();
        }



        // reportInfo



        private enum Attr
        {
            Name,
            CultureSpecific,
            Path,
            UseRefineFile
        };

        public static ReportInfo ReadXml(XmlReader reader)
        {
            var name = reader.GetAttribute(ReportInfo.Attr.Name);
            var cultureSpecific = reader.GetBoolAttribute(ReportInfo.Attr.CultureSpecific);
            var reportPath = reader.GetAttribute(ReportInfo.Attr.Path);
            var resultsFile = reader.GetNullableBoolAttribute(ReportInfo.Attr.UseRefineFile);
            var rScripts = new List<Tuple<string, string>>();
            while (reader.IsStartElement() && !reader.IsEmptyElement)
            {
                if (reader.Name == "script_path")
                {
                    var tupleItems = reader.ReadElementContentAsString().Split(new[] { '(', ',', ')' }, StringSplitOptions.RemoveEmptyEntries);
                    rScripts.Add(new Tuple<string, string>(tupleItems[0].Trim(), tupleItems[1].Trim()));
                }
                else
                {
                    reader.Read();
                }
            }

            return new ReportInfo(name, cultureSpecific, reportPath, rScripts, resultsFile ?? false);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("report_info");
            writer.WriteAttribute(ReportInfo.Attr.CultureSpecific, CultureSpecific);
            writer.WriteAttributeIfString(ReportInfo.Attr.Name, Name);
            writer.WriteAttributeIfString(ReportInfo.Attr.Path, ReportPath);
            writer.WriteAttribute(ReportInfo.Attr.UseRefineFile, UseRefineFile);
            foreach (var script in RScripts)
            {
                writer.WriteElementString("script_path", script);
            }

            writer.WriteEndElement();
        }

        #endregion

        #region SkylineSettings

        private enum Attr
        {
            Type,
            CmdPath,
        }

        public static SkylineSettings ReadXml(XmlReader reader)
        {
            // always use local Skyline if it exists
            if (SkylineInstallations.HasLocalSkylineCmd)
                return new SkylineSettings(SkylineType.Local);
            var type = Enum.Parse(typeof(SkylineType), reader.GetAttribute(Attr.Type), false);
            var cmdPath = Path.GetDirectoryName(reader.GetAttribute(Attr.CmdPath));
            return new SkylineSettings((SkylineType)type, cmdPath);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("config_skyline_settings");
            writer.WriteAttributeIfString(Attr.Type, Type.ToString());
            writer.WriteAttributeIfString(Attr.CmdPath, CmdPath);
            writer.WriteEndElement();
        }

        #endregion
        
         #region Server
         
         
         
         public static Server ReadXml(XmlReader reader)
        {
            // Read tag attributes
            var username = reader.GetAttribute(ATTR.username) ?? string.Empty;
            string encryptedPassword = reader.GetAttribute(ATTR.password_encrypted);
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
                password = reader.GetAttribute(ATTR.password) ?? string.Empty;
            }
            string uriText = reader.GetAttribute(ATTR.uri);
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
            
            var server = new Server(uri, username, password);
            server.Validate();
            return server;
        }
        
         
         #endregion
         
         
         #region DataServerInfo
         
         public static DataServerInfo ReadOldXml(XmlReader reader, string folder)
        {
            var serverName = reader.GetAttribute(Attr.ServerUrl);
            if (serverName == null) return null;
            return ReadXmlFields(reader, folder);
        }
         
          public static DataServerInfo ReadXml(XmlReader reader, string folder)
        {
            if (XmlUtil.ReadNextElement(reader, "data_server"))
            {
                var server = ReadXmlFields(reader, folder);
                reader.Read();
                return server;
            }

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
        
         
         #endregion*/

    }
}
