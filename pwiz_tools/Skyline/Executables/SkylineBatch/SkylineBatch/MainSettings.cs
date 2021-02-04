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
using SkylineBatch.Properties;

namespace SkylineBatch
{
    [XmlRoot("main_settings")]
    public class MainSettings
    {

        // IMMUTABLE - all fields are readonly strings
        // Holds file locations and naming pattern to use when running the configuration


        public MainSettings(string templateFilePath, string analysisFolderPath, string dataFolderPath,
            string replicateNamingPattern)
        {
            TemplateFilePath = templateFilePath;
            AnalysisFolderPath = analysisFolderPath;
            DataFolderPath = dataFolderPath;
            ReplicateNamingPattern = replicateNamingPattern ?? string.Empty;
        }

        public readonly string TemplateFilePath;

        public readonly string AnalysisFolderPath;

        public readonly string DataFolderPath;

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

        public void Validate()
        {
            ValidateSkylineFile(TemplateFilePath);
            ValidateDataFolder(DataFolderPath);
            ValidateAnalysisFolder(AnalysisFolderPath);
        }

        public static void ValidateSkylineFile(string skylineFile, string name = "")
        {
            CheckIfEmptyPath(skylineFile, "Skyline file");
            if (!File.Exists(skylineFile))
            {
                throw new ArgumentException(string.Format("The skyline template file {0} does not exist.", skylineFile) + Environment.NewLine +
                                            "Please provide a valid file.");
            }
        }

        public static void ValidateAnalysisFolder(string analysisFolder, string name = "")
        {
            CheckIfEmptyPath(analysisFolder, "analysis folder");
            var analysisFolderDirectory = Path.GetDirectoryName(analysisFolder);
            if (!Directory.Exists(analysisFolderDirectory))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateAnalysisFolder_The_analysis_folder__0__does_not_exist_, analysisFolderDirectory) + Environment.NewLine +
                                            "Please provide a valid folder.");
            }
        }

        public static void ValidateDataFolder(string dataFolder, string name = "")
        {
            CheckIfEmptyPath(dataFolder, "data folder");
            if (!Directory.Exists(dataFolder))
            {
                throw new ArgumentException(string.Format("The data folder {0} does not exist.", dataFolder) + Environment.NewLine +
                                            "Please provide a valid folder.");
            }
        }

        public static void CheckIfEmptyPath(string input, string name)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException(string.Format("Please specify a path to {0}", name));
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
            pathReplacedMainSettings = new MainSettings(replacedTemplatePath, replacedAnalysisPath, replacedDataPath, ReplicateNamingPattern);
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
            ReplicateNamingPattern,
        };

        public static MainSettings ReadXml(XmlReader reader)
        {
            var templateFilePath = reader.GetAttribute(Attr.TemplateFilePath);
            var analysisFolderPath = reader.GetAttribute(Attr.AnalysisFolderPath);
            var dataFolderPath = reader.GetAttribute(Attr.DataFolderPath);
            var replicateNamingPattern = reader.GetAttribute(Attr.ReplicateNamingPattern);
            return new MainSettings(templateFilePath, analysisFolderPath, dataFolderPath, replicateNamingPattern);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("main_settings");
            writer.WriteAttributeIfString(Attr.TemplateFilePath, TemplateFilePath);
            writer.WriteAttributeIfString(Attr.AnalysisFolderPath, AnalysisFolderPath);
            writer.WriteAttributeIfString(Attr.DataFolderPath, DataFolderPath);
            writer.WriteAttributeIfString(Attr.ReplicateNamingPattern, ReplicateNamingPattern);
            writer.WriteEndElement();
        }
        #endregion

        protected bool Equals(MainSettings other)
        {

            return (other.TemplateFilePath == TemplateFilePath &&
                    other.AnalysisFolderPath == AnalysisFolderPath &&
                    other.DataFolderPath == DataFolderPath &&
                    other.ReplicateNamingPattern == ReplicateNamingPattern);
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