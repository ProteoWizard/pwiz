/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SharedBatch;
using SkylineBatch.Properties;
using File = System.IO.File;

namespace SkylineBatch
{
    [XmlRoot("file_settings")]
    public class MainSettings
    {
        public const string XML_EL = "file_settings";
        public const string OLD_XML_EL = "main_settings";

        // IMMUTABLE - all fields are readonly strings/objects

        public MainSettings(SkylineTemplate skylineTemplate, string analysisFolderPath, bool useAnalysisFolderName, string dataFolderPath,
            DataServerInfo server, string annotationsFilePath, PanoramaFile annotationsDownload, string replicateNamingPattern)
        {
            Template = skylineTemplate;
            AnalysisFolderPath = analysisFolderPath;
            UseAnalysisFolderName = useAnalysisFolderName;
            DataFolderPath = dataFolderPath;
            Server = server;
            AnnotationsFilePath = annotationsFilePath ?? string.Empty;
            AnnotationsDownload = annotationsDownload;
            ReplicateNamingPattern = replicateNamingPattern ?? string.Empty;
        }


        public readonly SkylineTemplate Template;

        public readonly string AnalysisFolderPath;

        public readonly bool UseAnalysisFolderName;

        public readonly string DataFolderPath;

        public readonly DataServerInfo Server;

        public readonly string AnnotationsFilePath;

        public readonly PanoramaFile AnnotationsDownload;

        public readonly string ReplicateNamingPattern;

        public bool WillDownloadData => Template.PanoramaFile != null || Server != null || AnnotationsDownload != null;

        public void AddDownloadingFiles(ServerFilesManager serverFiles)
        {
            if (Template.PanoramaFile != null)
                Template.PanoramaFile.AddDownloadingFile(serverFiles);
            if (Server != null)
                Server.AddDownloadingFiles(serverFiles, DataFolderPath);
            if (AnnotationsDownload != null)
                AnnotationsDownload.AddDownloadingFile(serverFiles);
        }

        public string GetResultsFilePath()
        {
            if (UseAnalysisFolderName)
                return Path.Combine(AnalysisFolderPath, Path.GetFileName(AnalysisFolderPath) + TextUtil.EXT_SKY);
            else
                return Path.Combine(AnalysisFolderPath, Template.FileName());
        }

        public MainSettings WithoutDependency()
        {
            var independentTemplate = SkylineTemplate.ExistingTemplate(Template.FilePath);
            return new MainSettings(independentTemplate, AnalysisFolderPath, UseAnalysisFolderName, DataFolderPath, Server,
                AnnotationsFilePath, AnnotationsDownload, ReplicateNamingPattern);
        }

        public MainSettings UpdateDependent(string newName, string newFilePath)
        {
            if (string.IsNullOrEmpty(newFilePath)) return WithoutDependency();
            var newTemplate = SkylineTemplate.DependentTemplate(newFilePath, newName);
            return new MainSettings(newTemplate, AnalysisFolderPath, UseAnalysisFolderName, DataFolderPath, Server,
                 AnnotationsFilePath, AnnotationsDownload, ReplicateNamingPattern);
        }

        public void CreateAnalysisFolderIfNonexistent()
        {
            if(!Directory.Exists(AnalysisFolderPath)) Directory.CreateDirectory(AnalysisFolderPath);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Template file: ").AppendLine(Template.FilePath);
            sb.Append("Analysis folder: ").AppendLine(AnalysisFolderPath);
            sb.Append("Data folder: ").AppendLine(DataFolderPath);
            sb.Append("Replicate naming pattern: ").AppendLine(ReplicateNamingPattern);
            return sb.ToString();
        }

        public void Validate()
        {
            Template.Validate();
            ValidateDataFolder(DataFolderPath, Server != null);
            ValidateAnalysisFolder(AnalysisFolderPath);
            ValidateAnnotationsFile(AnnotationsFilePath, AnnotationsDownload != null);
        }

        public static void ValidateAnalysisFolder(string analysisFolder)
        {
            FileUtil.ValidateNotEmptyPath(analysisFolder, Resources.MainSettings_ValidateAnalysisFolder_analysis_folder);
            var analysisFolderDirectory = Path.GetDirectoryName(analysisFolder);
            if (!Directory.Exists(analysisFolderDirectory))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnalysisFolder_The__parent_directory_of_the_analysis_folder__0__does_not_exist_, analysisFolderDirectory) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_);
            }
            FileUtil.ValidateNotInDownloads(analysisFolder, Resources.MainSettings_ValidateAnalysisFolder_analysis_folder);
        }

        public static void ValidateDataFolderWithoutServer(string dataFolder)
        {
            ValidateDataFolder(dataFolder, false);
        }

        public static void ValidateDataFolderWithServer(string dataFolder)
        {
            ValidateDataFolder(dataFolder, true);
        }

        private static void ValidateDataFolder(string dataFolder, bool hasServer)
        {
            FileUtil.ValidateNotEmptyPath(dataFolder, Resources.MainSettings_ValidateDataFolder_data_folder);
            if (!hasServer && (!Directory.Exists(dataFolder) || !FileUtil.PathHasDriveName(dataFolder)))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateDataFolder_The_data_folder__0__does_not_exist_, dataFolder) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_);
            }

            if (hasServer)
            {
                var directoryExists = false;
                try
                {
                    directoryExists = Directory.Exists(Path.GetDirectoryName(dataFolder)) && FileUtil.PathHasDriveName(dataFolder);
                }
                catch (Exception)
                {
                    // pass - throws exception if directory was not found
                }
                if (!directoryExists)
                    throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnalysisFolder_The__parent_directory_of_the_data_folder__0__does_not_exist_1, dataFolder));
            }
            FileUtil.ValidateNotInDownloads(dataFolder, Resources.MainSettings_ValidateDataFolder_data_folder);
            if (!hasServer && !Directory.EnumerateFileSystemEntries(dataFolder).Any())
                throw new ArgumentException(Resources.MainSettings_ValidateAllButServer_The_data_folder_cannot_be_empty__Please_choose_a_folder_with_at_least_one_data_file_);
        }

        public static void ValidateAnnotationsWithoutServer(string annotationsFilePath)
        {
            ValidateAnnotationsFile(annotationsFilePath, false);
        }

        public static void ValidateAnnotationsWithServer(string annotationsFilePath)
        {
            ValidateAnnotationsFile(annotationsFilePath, true);
        }

        public static void ValidateAnnotationsFile(string annotationsFilePath, bool downloading)
        {
            if (!string.IsNullOrWhiteSpace(annotationsFilePath))
            {
                if (!downloading && !File.Exists(annotationsFilePath))
                    throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnnotationsFolder_The_annotations_file__0__does_not_exist_, annotationsFilePath) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnnotationsFolder_Please_enter_a_valid_file_path__or_no_text_if_you_do_not_wish_to_include_annotations_);
                var folderName = FileUtil.GetDirectorySafe(annotationsFilePath);
                if (downloading && (!Directory.Exists(folderName) || !FileUtil.PathHasDriveName(folderName)))
                    throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnnotationsFile_The_download_folder_for_the_annotations_file__0__does_not_exist_, annotationsFilePath) + Environment.NewLine +
                                                Resources.MainSettings_ValidateAnnotationsFile_Please_anter_a_path_to_an_existing_folder_);
                FileUtil.ValidateNotInDownloads(annotationsFilePath, Resources.MainSettings_ValidateAnnotationsFile_annotations_file);
            } else if (downloading)
            {
                throw new ArgumentException(Resources.MainSettings_ValidateAnnotationsFile_A_file_path_for_a_downloaded_annotations_file_is_required__Please_add_an_annotations_file_path_);
            }
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out MainSettings pathReplacedMainSettings)
        {
            var preferReplace = Program.FunctionalTest;
            var templateReplaced = Template.TryPathReplace(oldRoot, newRoot, out SkylineTemplate replacedTemplate);
            var analysisReplaced =
                TextUtil.SuccessfulReplace(ValidateAnalysisFolder, oldRoot, newRoot, AnalysisFolderPath, preferReplace, out string replacedAnalysisPath);
            var dataValidator = Server != null
                ? ValidateDataFolderWithServer
                : (Validator) ValidateDataFolderWithoutServer;
            var dataReplaced =
                TextUtil.SuccessfulReplace(dataValidator, oldRoot, newRoot, DataFolderPath,
                    Server != null || preferReplace, out string replacedDataPath);
            var dataServer = Server != null ? new DataServerInfo(Server.FileSource, Server.RelativePath, Server.DataNamingPattern, replacedDataPath) : null;
            var annotationsValidator = AnnotationsDownload != null
                ? ValidateAnnotationsWithServer
                : (Validator)ValidateAnnotationsWithoutServer;
            var annotationsReplaced =
                TextUtil.SuccessfulReplace(annotationsValidator, oldRoot, newRoot, AnnotationsFilePath, preferReplace, out string replacedAnnotationsPath);
            var annotationsDownload = AnnotationsDownload != null && !string.IsNullOrEmpty(replacedAnnotationsPath) ? AnnotationsDownload.ReplaceFolder(Path.GetDirectoryName(replacedAnnotationsPath)) : null;

            pathReplacedMainSettings = new MainSettings(replacedTemplate, replacedAnalysisPath, UseAnalysisFolderName, replacedDataPath,
                dataServer, replacedAnnotationsPath, annotationsDownload, ReplicateNamingPattern);
            return templateReplaced || analysisReplaced || dataReplaced || annotationsReplaced;
        }

        public MainSettings ForcePathReplace(string oldRoot, string newRoot)
        {
            var skylineTemplate = Template.ForcePathReplace(oldRoot, newRoot);
            var analysisFolderPath = FileUtil.ForceReplaceRoot(oldRoot, newRoot, AnalysisFolderPath);
            var dataPath = FileUtil.ForceReplaceRoot(oldRoot, newRoot, DataFolderPath);
            var dataServer = new DataServerInfo(Server.FileSource, Server.RelativePath, Server.DataNamingPattern, dataPath);
            var annotationsPath = !string.IsNullOrEmpty(AnnotationsFilePath) ? FileUtil.ForceReplaceRoot(oldRoot, newRoot, AnnotationsFilePath) : string.Empty;

           var annotationsDownload = AnnotationsDownload != null
               ? AnnotationsDownload.ReplaceFolder(Path.GetDirectoryName(annotationsPath)) : null;

            return new MainSettings(skylineTemplate, analysisFolderPath, UseAnalysisFolderName, dataPath,
                dataServer, annotationsPath, annotationsDownload, ReplicateNamingPattern);
        }

        public MainSettings UpdateRemoteFileSet(ImmutableDictionary<string, RemoteFileSource> remoteFileSources, out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources)
        {
            newRemoteFileSources = remoteFileSources;
            var newTemplate = Template.UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources);
            var newDataServer = Server != null ? Server.UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources) : null;
            var newAnnotationsDownload = AnnotationsDownload != null ? AnnotationsDownload.UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources) : null;
            return new MainSettings(newTemplate, AnalysisFolderPath, UseAnalysisFolderName, DataFolderPath,
                newDataServer, AnnotationsFilePath, newAnnotationsDownload, ReplicateNamingPattern);
        }

        public MainSettings ReplacedRemoteFileSource(RemoteFileSource existingSource, RemoteFileSource newSource, out bool replaced)
        {
            var newTemplate = Template.ReplacedRemoteFileSource(existingSource, newSource, out bool templateReplaced);
            var dataReplaced = false;
            var newDataServer = Server != null ? Server.ReplacedRemoteFileSource(existingSource, newSource, out dataReplaced) : null;
            var annotationsReplaced = false;
            var newAnnotationsDownload = AnnotationsDownload != null ? AnnotationsDownload.ReplacedRemoteFileSource(existingSource, newSource, out annotationsReplaced) : null;
            replaced = templateReplaced || dataReplaced || annotationsReplaced;
            return new MainSettings(newTemplate, AnalysisFolderPath, UseAnalysisFolderName, DataFolderPath,
                newDataServer, AnnotationsFilePath, newAnnotationsDownload, ReplicateNamingPattern);
        }

        public bool RunWillOverwrite(RunBatchOptions runOption, string configHeader, out StringBuilder message)
        {
            var tab = "      ";
            message = new StringBuilder(configHeader);
            CreateAnalysisFolderIfNonexistent();
            var analysisFolderName = Path.GetFileName(AnalysisFolderPath);

            if (runOption == RunBatchOptions.ALL || runOption == RunBatchOptions.FROM_TEMPLATE_COPY)
            {
                var overwriteFiles = FileUtil.GetFilesInFolder(AnalysisFolderPath, TextUtil.EXT_SKY);
                overwriteFiles.AddRange(FileUtil.GetFilesInFolder(AnalysisFolderPath, TextUtil.EXT_SKYD));
                if (overwriteFiles.Count == 0)
                    return false;
                foreach (var file in overwriteFiles)
                {
                    message.Append(tab + tab)
                        .Append(string.Format(Path.Combine(analysisFolderName, Path.GetFileName(file))))
                        .AppendLine();
                }

                return true;
            }
            return false;
        }

        #region Read/Write XML

        public static MainSettings ReadXml(XmlReader reader)
        {
            var analysisFolderPath = reader.GetAttribute(XML_TAGS.analysis_folder_path);
            var useAnalysisFolderName = reader.GetBoolAttribute(XML_TAGS.use_analysis_folder_name);
            var replicateNamingPattern = reader.GetAttribute(XML_TAGS.replicate_naming_pattern);
            reader.ReadToDescendant(XMLElements.TEMPLATE_FILE);
            var template = SkylineTemplate.ReadXml(reader);
            reader.ReadToFollowing(XMLElements.DATA_FOLDER);
            var dataFolderPath = reader.GetAttribute(XML_TAGS.path);
            var server = DataServerInfo.ReadXml(reader, dataFolderPath);
            reader.ReadToFollowing(XMLElements.ANNOTATIONS_FILE);
            var annotationsFilePath = reader.GetAttribute(XML_TAGS.path);
            var annotationsDownload = PanoramaFile.ReadXml(reader, annotationsFilePath);
            return new MainSettings(template, analysisFolderPath, useAnalysisFolderName, dataFolderPath, server,
                annotationsFilePath, annotationsDownload, replicateNamingPattern);
        }

        public static MainSettings ReadXmlVersion_21_1(XmlReader reader)
        {
            var analysisFolderPath = reader.GetAttribute(XML_TAGS.analysis_folder_path);
            var replicateNamingPattern = reader.GetAttribute(XML_TAGS.replicate_naming_pattern);
            reader.ReadToDescendant(XMLElements.TEMPLATE_FILE);
            var template = SkylineTemplate.ReadXmlVersion_21_1(reader);
            reader.ReadToFollowing(XMLElements.DATA_FOLDER);
            var dataFolderPath = reader.GetAttribute(XML_TAGS.path);
            var server = DataServerInfo.ReadXmlVersion_21_1(reader, dataFolderPath);
            reader.ReadToFollowing(XMLElements.ANNOTATIONS_FILE);
            var annotationsFilePath = reader.GetAttribute(XML_TAGS.path);
            var annotationsDownload = PanoramaFile.ReadXmlVersion_21_1(reader, annotationsFilePath);
            return new MainSettings(template, analysisFolderPath, false, dataFolderPath, server,
                annotationsFilePath, annotationsDownload, replicateNamingPattern);
        }

        public static MainSettings ReadXmlVersion_20_2(XmlReader reader)
        {
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

            var server = DataServerInfo.ReadXmlVersion_20_2(mainSettingsReader, dataFolderPath);
            //ReadDataServerXmlFields(mainSettingsReader, out Server dataServer, out string dataNamingPattern);

            if (templateFilePath == null)
            {
                XmlUtil.ReadNextElement(mainSettingsReader, "template_file");
                templateFilePath = mainSettingsReader.GetAttribute(OLD_XML_TAGS.FilePath);
                zippedFilePath = mainSettingsReader.GetAttribute(OLD_XML_TAGS.ZipFilePath);
                dependentConfigName = mainSettingsReader.GetAttribute(OLD_XML_TAGS.DependentConfigName);
                templatePanoramaFile = PanoramaFile.ReadXmlVersion_20_2(mainSettingsReader); //ReadOldPanoramaFile(mainSettingsReader);
            }
            if (XmlUtil.ReadNextElement(mainSettingsReader, "data_server"))
            {
                server = DataServerInfo.ReadXmlVersion_20_2(mainSettingsReader, dataFolderPath);
                //ReadDataServerXmlFields(mainSettingsReader, out dataServer, out dataNamingPattern);
            }
            var template = new SkylineTemplate(templateFilePath, zippedFilePath, dependentConfigName, templatePanoramaFile);
            return new MainSettings(template, analysisFolderPath, false, dataFolderPath, server,
                annotationsFilePath, null, replicateNamingPattern);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XML_EL);
            writer.WriteAttributeIfString(XML_TAGS.analysis_folder_path, AnalysisFolderPath);
            writer.WriteAttribute(XML_TAGS.use_analysis_folder_name, UseAnalysisFolderName);
            writer.WriteAttributeIfString(XML_TAGS.replicate_naming_pattern, ReplicateNamingPattern);
            Template.WriteXml(writer);

            writer.WriteStartElement(XMLElements.DATA_FOLDER);
            writer.WriteAttributeIfString(XML_TAGS.path, DataFolderPath);
            if (Server != null) Server.WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteStartElement(XMLElements.ANNOTATIONS_FILE);
            writer.WriteAttributeIfString(XML_TAGS.path, AnnotationsFilePath);
            if (AnnotationsDownload != null) AnnotationsDownload.WriteXml(writer);
            writer.WriteEndElement();
            
            writer.WriteEndElement();
        }
        #endregion

        #region Batch Commands

        public const string IMPORT_DATA_COMMAND = "--import-all=\"{0}\"";
        public const string IMPORT_NAMING_PATTERN_COMMAND = "--import-naming-pattern=\"{0}\"";
        public const string IMPORT_ANNOTATIONS_COMMAND = "--import-annotations=\"{0}\"";

        public void WriteOpenSkylineTemplateCommand(CommandWriter commandWriter)
        {
            commandWriter.Write(SkylineBatchConfig.OPEN_SKYLINE_FILE_COMMAND, Template.FilePath);
        }

        public void WriteSaveToResultsFile(CommandWriter commandWriter)
        {
            commandWriter.Write(SkylineBatchConfig.SAVE_AS_NEW_FILE_COMMAND, GetResultsFilePath());
        }

        public void WriteOpenSkylineResultsCommand(CommandWriter commandWriter)
        {
            commandWriter.Write(SkylineBatchConfig.OPEN_SKYLINE_FILE_COMMAND, GetResultsFilePath());
        }

        public void WriteImportDataCommand(CommandWriter commandWriter)
        {
            commandWriter.Write(IMPORT_DATA_COMMAND, DataFolderPath);
        }

        public void WriteImportNamingPatternCommand(CommandWriter commandWriter)
        {
            if (!string.IsNullOrEmpty(ReplicateNamingPattern))
                commandWriter.Write(IMPORT_NAMING_PATTERN_COMMAND, ReplicateNamingPattern);
        }

        public void WriteImportAnnotationsCommand(CommandWriter commandWriter)
        {
            if (!string.IsNullOrEmpty(AnnotationsFilePath))
                commandWriter.Write(IMPORT_ANNOTATIONS_COMMAND, AnnotationsFilePath);
        }

        #endregion

        protected bool Equals(MainSettings other)
        {
            return Equals(Template, other.Template)
                   && Equals(AnalysisFolderPath, other.AnalysisFolderPath)
                   && Equals(UseAnalysisFolderName, other.UseAnalysisFolderName)
                   && Equals(DataFolderPath, other.DataFolderPath)
                   && Equals(ReplicateNamingPattern, other.ReplicateNamingPattern)
                   && Equals(AnnotationsFilePath, other.AnnotationsFilePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MainSettings)obj);
        }

        public override int GetHashCode()
        {
            return Template.GetHashCode() +
                   AnalysisFolderPath.GetHashCode() +
                   DataFolderPath.GetHashCode() +
                   ReplicateNamingPattern.GetHashCode();
        }
    }
}