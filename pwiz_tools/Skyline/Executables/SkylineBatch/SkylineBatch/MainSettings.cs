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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    [XmlRoot("main_settings")]
    public class MainSettings
    {

        // IMMUTABLE - all fields are readonly strings
        // Holds file locations and naming pattern to use when running the configuration


        public MainSettings(string templateFilePath, string analysisFolderPath, string dataFolderPath,
            string annotationsFilePath, string replicateNamingPattern)
        {
            TemplateFilePath = templateFilePath;
            AnalysisFolderPath = analysisFolderPath;
            DataFolderPath = dataFolderPath;
            AnnotationsFilePath = annotationsFilePath ?? string.Empty;
            ReplicateNamingPattern = replicateNamingPattern ?? string.Empty;
        }

        public readonly string TemplateFilePath;

        public readonly string AnalysisFolderPath;

        public readonly string DataFolderPath;

        public readonly string AnnotationsFilePath;

        public readonly string ReplicateNamingPattern;

        public string GetResultsFilePath()
        {
            return Path.Combine(AnalysisFolderPath, Path.GetFileName(TemplateFilePath));
        }

        public void CreateAnalysisFolderIfNonexistent()
        {
            if(!Directory.Exists(AnalysisFolderPath)) Directory.CreateDirectory(AnalysisFolderPath);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Template file: ").AppendLine(TemplateFilePath);
            sb.Append("Analysis folder: ").AppendLine(AnalysisFolderPath);
            sb.Append("Data folder: ").AppendLine(DataFolderPath);
            sb.Append("Replicate naming pattern: ").AppendLine(ReplicateNamingPattern);
            return sb.ToString();
        }

        public List<string> Validate(List<string> generatedSkylineFiles = null)
        {
            var questions = new List<string>();
            questions.Add(ValidateTemplateFile(TemplateFilePath));

            ValidateDataFolder(DataFolderPath);
            ValidateAnalysisFolder(AnalysisFolderPath);
            ValidateAnnotationsFile(AnnotationsFilePath);
            questions.RemoveAll(item => item == null);
            return questions;
        }

        public static void ValidateSkylineFile(string skylineFile)
        {
            // Used by InvalidConfigSetupForm, validates the template file regardless of questions
            ValidateTemplateFile(skylineFile);
        }

        private static string ValidateTemplateFile(string templateFile)
        {
            CheckIfEmptyPath(templateFile, Resources.MainSettings_ValidateSkylineFile_Skyline_file);

            if (!File.Exists(templateFile))
            {
                string directory = null;
                string directoryParent = null;
                try
                {
                    directory = Path.GetDirectoryName(templateFile);
                    directoryParent = Path.GetDirectoryName(directory);

                }
                catch (Exception)
                {
                    // pass, will throw ArgumentException if directories are null
                }

                if (Directory.Exists(directory) || Directory.Exists(directoryParent))
                {
                    return "The Skyline template file for this configuration does not exist." +
                                 Environment.NewLine +
                                 string.Format("Will another configuration generate {0} before this configuration runs?",
                                     Path.GetFileName(templateFile));
                }
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateSkylineFile_The_Skyline_template_file__0__does_not_exist_, templateFile) + Environment.NewLine +
                                            Resources.MainSettings_ValidateSkylineFile_Please_provide_a_valid_file_);
            }
            return null;
        }

        public static void ValidateAnalysisFolder(string analysisFolder)
        {
            CheckIfEmptyPath(analysisFolder, Resources.MainSettings_ValidateAnalysisFolder_analysis_folder);
            var analysisFolderDirectory = Path.GetDirectoryName(analysisFolder);
            if (!Directory.Exists(analysisFolderDirectory))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnalysisFolder_The_analysis_folder__0__does_not_exist_, analysisFolderDirectory) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_);
            }
        }

        public static void ValidateDataFolder(string dataFolder)
        {
            CheckIfEmptyPath(dataFolder, Resources.MainSettings_ValidateDataFolder_data_folder);
            if (!Directory.Exists(dataFolder))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateDataFolder_The_data_folder__0__does_not_exist_, dataFolder) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_);
            }
        }

        public static void ValidateAnnotationsFile(string annotationsFilePath)
        {
            if (!string.IsNullOrWhiteSpace(annotationsFilePath) && !File.Exists(annotationsFilePath))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnnotationsFolder_The_annotations_file__0__does_not_exist_, annotationsFilePath) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnnotationsFolder_Please_enter_a_valid_file_path__or_no_text_if_you_do_not_wish_to_include_annotations_);
            }
        }

        public static void CheckIfEmptyPath(string input, string name)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_CheckIfEmptyPath_Please_specify_a_path_to__0_, name));
            }
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out MainSettings pathReplacedMainSettings)
        {
            var templateReplaced =
                TextUtil.TryReplaceStart(oldRoot, newRoot, TemplateFilePath, out string replacedTemplatePath);
            var analysisReplaced =
                TextUtil.TryReplaceStart(oldRoot, newRoot, AnalysisFolderPath, out string replacedAnalysisPath);
            var dataReplaced =
                TextUtil.TryReplaceStart(oldRoot, newRoot, DataFolderPath, out string replacedDataPath);
            // annotations replaced
            TextUtil.TryReplaceStart(oldRoot, newRoot, AnnotationsFilePath, out string replacedAnnotationsPath);

            pathReplacedMainSettings = new MainSettings(replacedTemplatePath, replacedAnalysisPath, replacedDataPath, replacedAnnotationsPath, ReplicateNamingPattern);
            return templateReplaced || analysisReplaced || dataReplaced;
        }

        public bool RunWillOverwrite(int startStep, string configHeader, out StringBuilder message)
        {
            var tab = "      ";
            message = new StringBuilder(configHeader);
            var analysisFolderName = Path.GetFileName(AnalysisFolderPath);
            switch (startStep)
            {
                case 1:
                    var resultsFile = GetResultsFilePath();
                    var resultsFileIdentifyer = Path.Combine(analysisFolderName, Path.GetFileName(resultsFile));
                    if (File.Exists(resultsFile) && new FileInfo(TemplateFilePath).Length != new FileInfo(resultsFile).Length)
                    {
                        message.Append(tab + tab)
                            .Append(resultsFileIdentifyer)
                            .AppendLine();
                        return true;
                    }
                    break;
                case 2:
                    var templateSkyds = GetFilesInFolder(Path.GetDirectoryName(TemplateFilePath), TextUtil.EXT_SKYD);
                    var resultsSkyds = GetFilesInFolder(AnalysisFolderPath, TextUtil.EXT_SKYD);
                    var templateSkydSize = templateSkyds.Count == 0 ? 0 : new FileInfo(templateSkyds[0]).Length;
                    var resultsSkydSize = resultsSkyds.Count == 0 ? 0 : new FileInfo(resultsSkyds[0]).Length;
                    if (templateSkydSize < resultsSkydSize)
                    {
                        message.Append(tab + tab)
                            .Append(string.Format(Path.Combine(analysisFolderName, Path.GetFileName(resultsSkyds[0]))))
                            .AppendLine();
                        return true;
                    }
                    break;
                case 3:
                    var reportFiles = GetFilesInFolder(AnalysisFolderPath, TextUtil.EXT_CSV);
                    if (reportFiles.Count > 0)
                    {
                        foreach (var reportCsv in reportFiles)
                        {
                            message.Append(tab + tab).Append(Path.GetFileName(reportCsv)).AppendLine();
                        }
                        return true;
                    }
                    break;
                case 4:
                    // pass
                    break;
                default:
                    throw new Exception(startStep + " is not a valid start step.");
            }
            return false;
        }


        private List<string> GetFilesInFolder(string folder, string fileType)
        {
            var filesWithType = new List<string>();
            var allFiles = new DirectoryInfo(folder).GetFiles();
            foreach (var file in allFiles)
            {
                if (file.Name.EndsWith(fileType))
                    filesWithType.Add(file.FullName);
            }

            return filesWithType;
        }

        #region Read/Write XML

        private enum Attr
        {
            TemplateFilePath,
            AnalysisFolderPath,
            DataFolderPath,
            AnnotationsFilePath,
            ReplicateNamingPattern,
        };

        public static MainSettings ReadXml(XmlReader reader)
        {
            var templateFilePath = reader.GetAttribute(Attr.TemplateFilePath);
            var analysisFolderPath = reader.GetAttribute(Attr.AnalysisFolderPath);
            var dataFolderPath = reader.GetAttribute(Attr.DataFolderPath);
            var annotationsFilePath = reader.GetAttribute(Attr.AnnotationsFilePath);
            var replicateNamingPattern = reader.GetAttribute(Attr.ReplicateNamingPattern);
            return new MainSettings(templateFilePath, analysisFolderPath, dataFolderPath, annotationsFilePath, replicateNamingPattern);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("main_settings");
            writer.WriteAttributeIfString(Attr.TemplateFilePath, TemplateFilePath);
            writer.WriteAttributeIfString(Attr.AnalysisFolderPath, AnalysisFolderPath);
            writer.WriteAttributeIfString(Attr.DataFolderPath, DataFolderPath);
            writer.WriteAttributeIfString(Attr.AnnotationsFilePath, AnnotationsFilePath);
            writer.WriteAttributeIfString(Attr.ReplicateNamingPattern, ReplicateNamingPattern);
            writer.WriteEndElement();
        }
        #endregion

        #region Batch Commands

        public const string IMPORT_DATA_COMMAND = "--import-all=\"{0}\"";
        public const string IMPORT_NAMING_PATTERN_COMMAND = "--import-naming-pattern=\"{0}\"";
        public const string IMPORT_ANNOTATIONS_COMMAND = "--import-annotations=\"{0}\"";

        public void WriteOpenSkylineTemplateCommand(CommandWriter commandWriter)
        {
            commandWriter.Write(SkylineBatchConfig.OPEN_SKYLINE_FILE_COMMAND, TemplateFilePath);
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
            // checks if annotation paths are both empty or equal
            if (!(string.IsNullOrWhiteSpace(AnnotationsFilePath) && string.IsNullOrWhiteSpace(other.AnnotationsFilePath)))
            {
                if (!other.AnnotationsFilePath.Equals(AnnotationsFilePath)) return false;
            }

            return (other.TemplateFilePath.Equals(TemplateFilePath) &&
                    other.AnalysisFolderPath.Equals(AnalysisFolderPath) &&
                    other.DataFolderPath.Equals(DataFolderPath) &&
                    other.ReplicateNamingPattern.Equals(ReplicateNamingPattern));
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
            return TemplateFilePath.GetHashCode() +
                   AnalysisFolderPath.GetHashCode() +
                   DataFolderPath.GetHashCode() +
                   ReplicateNamingPattern.GetHashCode();
        }
    }
}