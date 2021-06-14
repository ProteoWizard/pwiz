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
    [XmlRoot("main_settings")]
    public class MainSettings
    {

        // IMMUTABLE - all fields are readonly strings/objects

        public MainSettings(SkylineTemplate skylineTemplate, string analysisFolderPath, string dataFolderPath,
            DataServerInfo server, string annotationsFilePath, PanoramaFile annotationsDownload, string replicateNamingPattern)
        {
            Template = skylineTemplate;
            AnalysisFolderPath = analysisFolderPath;
            DataFolderPath = dataFolderPath;
            Server = server;
            AnnotationsFilePath = annotationsFilePath ?? string.Empty;
            AnnotationsDownload = annotationsDownload;
            ReplicateNamingPattern = replicateNamingPattern ?? string.Empty;
        }


        public readonly SkylineTemplate Template;

        public readonly string AnalysisFolderPath;

        public readonly string DataFolderPath;

        public readonly DataServerInfo Server;

        public readonly string AnnotationsFilePath;

        public readonly PanoramaFile AnnotationsDownload;

        public readonly string ReplicateNamingPattern;

        public bool WillDownloadData => Template.PanoramaFile != null || Server != null;

        public void AddDownloadingFiles(ServerFilesManager serverFiles)
        {
            if (Template.PanoramaFile != null)
                Template.PanoramaFile.AddDownloadingFile(serverFiles);
            if (Server != null)
                Server.AddDownloadingFiles(serverFiles, DataFolderPath);
        }

        public string GetResultsFilePath()
        {
            return Path.Combine(AnalysisFolderPath, Template.FileName());
        }

        public MainSettings WithoutDependency()
        {
            var independentTemplate = SkylineTemplate.ExistingTemplate(Template.FilePath);
            return new MainSettings(independentTemplate, AnalysisFolderPath, DataFolderPath, Server,
                AnnotationsFilePath, AnnotationsDownload, ReplicateNamingPattern);
        }

        public MainSettings UpdateDependent(string newName, string newFilePath)
        {
            if (string.IsNullOrEmpty(newFilePath)) return WithoutDependency();
            var newTemplate = SkylineTemplate.DependentTemplate(newFilePath, newName);
            return new MainSettings(newTemplate, AnalysisFolderPath, DataFolderPath, Server,
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
            ValidateAnnotationsFile(AnnotationsFilePath);
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
            if (!hasServer && !Directory.Exists(dataFolder))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateDataFolder_The_data_folder__0__does_not_exist_, dataFolder) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_);
            }

            if (hasServer)
            {
                var directoryExists = false;
                try
                {
                    directoryExists = Directory.Exists(Path.GetDirectoryName(dataFolder));
                }
                catch (Exception)
                {
                }
                if (!directoryExists)
                    throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnalysisFolder_The__parent_directory_of_the_data_folder__0__does_not_exist_1, dataFolder));
            }
            FileUtil.ValidateNotInDownloads(dataFolder, Resources.MainSettings_ValidateDataFolder_data_folder);
            if (!hasServer && !Directory.GetFiles(dataFolder).Any())
                throw new ArgumentException(Resources.MainSettings_ValidateAllButServer_The_data_folder_cannot_be_empty__Please_choose_a_folder_with_at_least_one_data_file_);
        }

        public static void ValidateAnnotationsFile(string annotationsFilePath)
        {
            if (!string.IsNullOrWhiteSpace(annotationsFilePath))
            {
                if (!File.Exists(annotationsFilePath))
                    throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnnotationsFolder_The_annotations_file__0__does_not_exist_, annotationsFilePath) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnnotationsFolder_Please_enter_a_valid_file_path__or_no_text_if_you_do_not_wish_to_include_annotations_);
                FileUtil.ValidateNotInDownloads(annotationsFilePath, Resources.MainSettings_ValidateAnnotationsFile_annotations_file);
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
            var annotationsReplaced =
                TextUtil.SuccessfulReplace(ValidateAnnotationsFile, oldRoot, newRoot, AnnotationsFilePath, preferReplace, out string replacedAnnotationsPath);
            var annotationsDownload = AnnotationsDownload != null && !string.IsNullOrEmpty(replacedAnnotationsPath) ? new PanoramaFile(AnnotationsDownload, Path.GetDirectoryName(replacedAnnotationsPath), AnnotationsDownload.FileName) : null;

            pathReplacedMainSettings = new MainSettings(replacedTemplate, replacedAnalysisPath, replacedDataPath,
                Server, replacedAnnotationsPath, annotationsDownload, ReplicateNamingPattern);

            return templateReplaced || analysisReplaced || dataReplaced || annotationsReplaced;
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
            var templateFilePath = reader.GetAttribute(Attr.TemplateFilePath);
            var dependentConfigName = reader.GetAttribute(Attr.DependentConfigName);
            var oldTemplate = templateFilePath != null
                ? SkylineTemplate.FromUi(templateFilePath, dependentConfigName, null)
                : null;
            var analysisFolderPath = reader.GetAttribute(Attr.AnalysisFolderPath);
            var dataFolderPath = reader.GetAttribute(Attr.DataFolderPath);
            var annotationsFilePath = reader.GetAttribute(Attr.AnnotationsFilePath);
            var replicateNamingPattern = reader.GetAttribute(Attr.ReplicateNamingPattern);
            var oldServer = DataServerInfo.ReadOldXml(reader, dataFolderPath);
            var template = oldTemplate ?? SkylineTemplate.ReadXml(reader);
            var server = oldServer ?? DataServerInfo.ReadXml(reader, dataFolderPath);
            var annotationsDownload = PanoramaFile.ReadXml(reader);
            return new MainSettings(template, analysisFolderPath, dataFolderPath, server,
                annotationsFilePath, annotationsDownload, replicateNamingPattern);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("main_settings");
            writer.WriteAttributeIfString(Attr.AnalysisFolderPath, AnalysisFolderPath);
            writer.WriteAttributeIfString(Attr.DataFolderPath, DataFolderPath);
            
            writer.WriteAttributeIfString(Attr.AnnotationsFilePath, AnnotationsFilePath);
            writer.WriteAttributeIfString(Attr.ReplicateNamingPattern, ReplicateNamingPattern);
            Template.WriteXml(writer);
            if (Server != null) Server.WriteXml(writer);
            if (AnnotationsDownload != null) AnnotationsDownload.WriteXml(writer);
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