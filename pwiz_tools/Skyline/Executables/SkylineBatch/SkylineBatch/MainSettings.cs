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
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using FluentFTP;
using IWshRuntimeLibrary;
using SharedBatch;
using SkylineBatch.Properties;
using File = System.IO.File;

namespace SkylineBatch
{
    [XmlRoot("main_settings")]
    public class MainSettings
    {

        // IMMUTABLE - all fields are readonly strings
        // Holds file locations and naming pattern to use when running the configuration

        private string _fileNamesResponse;

        public MainSettings(string templateFilePath, string analysisFolderPath, string dataFolderPath,
            DataServerInfo server, string annotationsFilePath, string replicateNamingPattern, string dependentConfigName)
        {
            TemplateFilePath = templateFilePath;
            DependentConfigName = !string.IsNullOrEmpty(dependentConfigName) ? dependentConfigName : null;
            AnalysisFolderPath = analysisFolderPath;
            DataFolderPath = dataFolderPath;
            Server = server;
            AnnotationsFilePath = annotationsFilePath ?? string.Empty;
            ReplicateNamingPattern = replicateNamingPattern ?? string.Empty;
            
            if (Server != null)
            {
                try
                {
                    if (Directory.Exists(Path.GetDirectoryName(DataFolderPath)))
                        Directory.CreateDirectory(DataFolderPath);
                }
                catch (Exception)
                {
                    // pass - will be invalidated later
                }

            }
        }


        public readonly string TemplateFilePath;

        public readonly string DependentConfigName;

        public readonly string AnalysisFolderPath;

        public readonly string DataFolderPath;

        public readonly DataServerInfo Server;

        public readonly string AnnotationsFilePath;

        public readonly string ReplicateNamingPattern;

        public bool WillDownloadData => Server != null;

        public string GetResultsFilePath()
        {
            return Path.Combine(AnalysisFolderPath, Path.GetFileName(TemplateFilePath));
        }

        public MainSettings WithoutDependency()
        {
            return new MainSettings(TemplateFilePath, AnalysisFolderPath, DataFolderPath, Server, 
                AnnotationsFilePath, ReplicateNamingPattern, string.Empty);
        }

        public MainSettings UpdateDependent(string newName, string newTemplate)
        {
            if (string.IsNullOrEmpty(newTemplate)) return WithoutDependency();
            return new MainSettings(newTemplate, AnalysisFolderPath, DataFolderPath, Server, 
                 AnnotationsFilePath, ReplicateNamingPattern, newName);
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

        public void Validate(List<string> generatedSkylineFiles = null)
        {
            if (DependentConfigName == null)
                ValidateTemplateFile(TemplateFilePath);
            ValidateDataFolder(DataFolderPath);
            if (Server == null && !Directory.GetFiles(DataFolderPath).Any())
                throw new ArgumentException("The data folder cannot be empty. Please choose a folder with at least one data file.");
            ValidateAnalysisFolder(AnalysisFolderPath);
            ValidateAnnotationsFile(AnnotationsFilePath);
            if (Server != null) DataServerInfo.ValidateNamingPattern(Server.DataNamingPattern);
        }

        public static void ValidateTemplateFile(string templateFile)
        {

            FileUtil.ValidateNotEmptyPath(templateFile, Resources.MainSettings_ValidateSkylineFile_Skyline_file);
            if (!File.Exists(templateFile))
            
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateSkylineFile_The_Skyline_template_file__0__does_not_exist_, templateFile) + Environment.NewLine +
                                            Resources.MainSettings_ValidateSkylineFile_Please_provide_a_valid_file_);
            FileUtil.ValidateNotInDownloads(templateFile, Resources.MainSettings_ValidateSkylineFile_Skyline_file);
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

        public static void ValidateDataFolder(string dataFolder)
        {
            FileUtil.ValidateNotEmptyPath(dataFolder, Resources.MainSettings_ValidateDataFolder_data_folder);
            if (!Directory.Exists(dataFolder))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateDataFolder_The_data_folder__0__does_not_exist_, dataFolder) + Environment.NewLine +
                                            Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_);
            }
            FileUtil.ValidateNotInDownloads(dataFolder, Resources.MainSettings_ValidateDataFolder_data_folder);
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
            var templateReplaced = false;
            var replacedTemplatePath = TemplateFilePath;
            if (DependentConfigName == null)
                templateReplaced = TextUtil.SuccessfulReplace(ValidateTemplateFile, oldRoot, newRoot, TemplateFilePath, out replacedTemplatePath);
            var analysisReplaced =
                TextUtil.SuccessfulReplace(ValidateAnalysisFolder, oldRoot, newRoot, AnalysisFolderPath, out string replacedAnalysisPath);
            var dataReplaced =
                TextUtil.SuccessfulReplace(ValidateDataFolder, oldRoot, newRoot, DataFolderPath, out string replacedDataPath);
            var annotationsReplaced =
                TextUtil.SuccessfulReplace(ValidateAnnotationsFile, oldRoot, newRoot, AnnotationsFilePath, out string replacedAnnotationsPath);

            pathReplacedMainSettings = new MainSettings(replacedTemplatePath, replacedAnalysisPath, replacedDataPath,
                Server, replacedAnnotationsPath, ReplicateNamingPattern, DependentConfigName);

            return templateReplaced || analysisReplaced || dataReplaced || annotationsReplaced;
        }

        public bool RunWillOverwrite(int startStep, string configHeader, out StringBuilder message)
        {
            var tab = "      ";
            message = new StringBuilder(configHeader);
            CreateAnalysisFolderIfNonexistent();
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
                case 4:
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
                case 5:
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
            DependentConfigName,
            AnalysisFolderPath,
            DataFolderPath,
            AnnotationsFilePath,
            ReplicateNamingPattern,
        };

        public static MainSettings ReadXml(XmlReader reader)
        {
            var templateFilePath = GetPath(reader.GetAttribute(Attr.TemplateFilePath));
            var dependentConfigName = reader.GetAttribute(Attr.DependentConfigName);
            var analysisFolderPath = GetPath(reader.GetAttribute(Attr.AnalysisFolderPath));
            var dataFolderPath = GetPath(reader.GetAttribute(Attr.DataFolderPath));
            var annotationsFilePath = GetPath(reader.GetAttribute(Attr.AnnotationsFilePath));
            var replicateNamingPattern = reader.GetAttribute(Attr.ReplicateNamingPattern);
            var server = DataServerInfo.ReadXml(reader);
            return new MainSettings(templateFilePath, analysisFolderPath, dataFolderPath, server, 
                annotationsFilePath, replicateNamingPattern, dependentConfigName);
        }

        private static string GetPath(string path) =>
            FileUtil.GetTestPath(Program.FunctionalTest, Program.TestDirectory, path);

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("main_settings");
            writer.WriteAttributeIfString(Attr.TemplateFilePath, TemplateFilePath);
            writer.WriteAttributeIfString(Attr.DependentConfigName, DependentConfigName);
            writer.WriteAttributeIfString(Attr.AnalysisFolderPath, AnalysisFolderPath);
            writer.WriteAttributeIfString(Attr.DataFolderPath, DataFolderPath);
            
            writer.WriteAttributeIfString(Attr.AnnotationsFilePath, AnnotationsFilePath);
            writer.WriteAttributeIfString(Attr.ReplicateNamingPattern, ReplicateNamingPattern);
            if (Server != null) Server.WriteXml(writer);
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

    public class DataServerInfo
    {
        private Dictionary<string, FtpListItem> _serverFiles;
        
        public static DataServerInfo ServerFromUi(string url, string userName, string password, string namingPattern)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("The URL cannot be empty. Please enter a URL.");
            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (Exception)
            {
                throw new ArgumentException("Error parsing the URL. Please correct the URL and try again.");
            }
            return new DataServerInfo(uri, userName, password, namingPattern);
        }

        private DataServerInfo(Uri server, string userName, string password, string namingPattern)
        {
            Server = server;
            UserName = userName ?? string.Empty;
            Password = password ?? string.Empty;
            DataNamingPattern = namingPattern ?? string.Empty;
        }

        public bool Validated { get; private set; }
        public readonly Uri Server;
        public readonly string UserName;
        public readonly string Password;
        public readonly string DataNamingPattern;

        public string GetUrl() => Server.AbsoluteUri;
        

        public string FilePath(string fileName) =>
            string.IsNullOrEmpty(Server.AbsolutePath) ? fileName : Path.Combine(Server.AbsolutePath, fileName);

        public Dictionary<string, FtpListItem> GetServerFiles => new Dictionary<string, FtpListItem>(_serverFiles);

        public FtpClient GetFtpClient()
        {
            var client = new FtpClient(Server.Host);

            if (!string.IsNullOrEmpty(Password))
            {
                if (!string.IsNullOrEmpty(UserName))
                    client.Credentials = new NetworkCredential(UserName, Password);
                else
                    client.Credentials = new NetworkCredential("anonymous", Password);
            }

            return client;
        }


        public void Validate()
        {
            ValidateNamingPattern(DataNamingPattern);
            _serverFiles = new Dictionary<string, FtpListItem>();
            var client = GetFtpClient();
            try
            {
                client.Connect();
                foreach (FtpListItem item in client.GetListing(Server.AbsolutePath))
                {
                    if (item.Type == FtpFileSystemObjectType.File) _serverFiles.Add(item.Name, item);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException(
                    string.Format(Resources.DataServerInfo_Validate_There_was_an_error_connecting_to__0____1_, GetUrl(), e.Message));
            }
            client.Disconnect();
            if (_serverFiles.Count == 0)
                throw new ArgumentException(string.Format(
                    Resources.DataServerInfo_Validate_There_were_no_files_found_at__0___Make_sure_the_URL__username__and_password_are_correct_and_try_again_,
                    GetUrl()));
            Validated = true;
        }

        public static void ValidateNamingPattern(string dataNamingPattern)
        {
            if (string.IsNullOrEmpty(dataNamingPattern))
                throw new ArgumentException(Resources.DataServerInfo_ValidateNamingPattern_A_data_naming_pattern_is_required_for_downloaded_data__Please_add_a_data_naming_pattern_);
        }

        private enum Attr
        {
            ServerUri,
            ServerUrl, // deprecated
            ServerFolder, // deprecated
            ServerUserName,
            ServerPassword,
            DataNamingPattern
        };

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeIfString(Attr.ServerUri, Server.AbsoluteUri);
            writer.WriteAttributeIfString(Attr.ServerUserName, UserName);
            writer.WriteAttributeIfString(Attr.ServerPassword, Password);
            writer.WriteAttributeIfString(Attr.DataNamingPattern, DataNamingPattern);
        }

        public static DataServerInfo ReadXml(XmlReader reader)
        {
            var serverName = reader.GetAttribute(Attr.ServerUrl);
            var uriString = reader.GetAttribute(Attr.ServerUri);
            if (string.IsNullOrEmpty(serverName) && string.IsNullOrEmpty(uriString))
                return null;
            var folder = reader.GetAttribute(Attr.ServerFolder);
            var uri = !string.IsNullOrEmpty(uriString) ? new Uri(uriString) : new Uri($@"ftp://{serverName}/{folder}");
            var username = reader.GetAttribute(Attr.ServerUserName);
            var password = reader.GetAttribute(Attr.ServerPassword);
            var dataNamingPattern = reader.GetAttribute(Attr.DataNamingPattern);
            return new DataServerInfo(uri, username, password, dataNamingPattern);
        }

    }



}