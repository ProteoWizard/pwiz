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
using System.Collections.Immutable;
using System.Xml;
using System.IO;
using System.Linq;
using System.Text;
using SharedBatch;
using SkylineBatch.Properties;


namespace SkylineBatch
{
    public class ReportSettings
    {
        public const string XML_EL = "report_settings";

        // IMMUTABLE
        // ReportSettings contains a list of reportInfos, each of which represents an individual report with R scripts to run on it.
        // An empty reportSettings is a valid instance of this class, as configurations don't require reports to run the batch commands.
        
        public ReportSettings(List<ReportInfo> reports)
        {
            Reports = ImmutableList.CreateRange(reports);
        }

        public readonly ImmutableList<ReportInfo> Reports;

        public bool WillDownloadData => DownloadDataHelper();

        private bool DownloadDataHelper()
        {
            foreach (var report in Reports)
                if (report.WillDownloadData)
                    return true;
            return false;
        }

        public void AddDownloadingFiles(ServerFilesManager serverFiles)
        {
            foreach (var report in Reports)
                report.AddDownloadingFiles(serverFiles);
        }

        public void Validate()
        {
            foreach (var reportInfo in Reports)
            {
                reportInfo.Validate();
            }
        }

        public bool RunWillOverwrite(RunBatchOptions runOption, string configHeader, string analysisFolder, out StringBuilder message)
        {
            message = new StringBuilder();
            if (runOption < RunBatchOptions.FROM_REPORT_EXPORT)
                return false;
            var tab = "      ";
            var analysisFolderName = Path.GetFileName(analysisFolder);
            var csvFiles = FileUtil.GetFilesInFolder(analysisFolder, TextUtil.EXT_CSV);
            var existingReports = new List<string>();
            foreach (var report in Reports)
            {
                var reportPath = Path.Combine(analysisFolder, report.Name + TextUtil.EXT_CSV);
                if (csvFiles.Contains(reportPath))
                    existingReports.Add(reportPath);
            }


            if (runOption == RunBatchOptions.FROM_REPORT_EXPORT)
            {
                if (existingReports.Count > 0)
                {
                    foreach (var reportPath in existingReports)
                        message.Append(tab + tab).Append(Path.Combine(analysisFolderName, Path.GetFileName(reportPath))).AppendLine();
                }
                return existingReports.Count > 0;
            }
            else
            {
                var analysisFolderFiles = Directory.GetFiles(analysisFolder);
                foreach (var file in analysisFolderFiles)
                {
                    var extension = Path.GetExtension(file);
                    if (extension.StartsWith(TextUtil.EXT_SKY) || extension == TextUtil.EXT_LOG || existingReports.Contains(file))
                        continue;
                    message.Append(tab + tab).Append(analysisFolderName).AppendLine();
                    return true;
                }
                return false;
            }
        }

        public bool UsesRefinedFile()
        {
            foreach (var report in Reports)
            {
                if (report.UseRefineFile) return true;
            }
            return false;
        }

        public HashSet<string> RVersions()
        {
            var RVersions = new HashSet<string>();
            foreach (var report in Reports)
                RVersions.UnionWith(report.RVersions());
            return RVersions;
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out ReportSettings pathReplacedReportSettings)
        {
            var anyReplaced = false;
            var newReports = new List<ReportInfo>();
            foreach (var report in Reports)
            {
                anyReplaced = report.TryPathReplace(oldRoot, newRoot, out ReportInfo pathReplacedReportInfo) ||
                              anyReplaced;
                newReports.Add(pathReplacedReportInfo);
            }
            pathReplacedReportSettings = new ReportSettings(newReports);
            return anyReplaced;
        }

        public ReportSettings ForcePathReplace(string oldRoot, string newRoot)
        {
            var newReports = new List<ReportInfo>();
            foreach (var report in Reports)
                newReports.Add(report.ForcePathReplace(oldRoot, newRoot));
            return new ReportSettings(newReports);
        }

        public ReportSettings UpdateRemoteFileSet(ImmutableDictionary<string, RemoteFileSource> remoteFileSources, out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources)
        {
            newRemoteFileSources = remoteFileSources;
            var newReports = new List<ReportInfo>();
            foreach (var report in Reports)
                newReports.Add(report.UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources));
            return new ReportSettings(newReports);
        }

        public ReportSettings ReplacedRemoteFileSource(RemoteFileSource existingSource, RemoteFileSource newSource, out bool replaced)
        {
            var newReports = new List<ReportInfo>();
            replaced = false;
            foreach (var report in Reports)
            {
                newReports.Add(report.ReplacedRemoteFileSource(existingSource, newSource, out bool reportReplaced));
                replaced = replaced || reportReplaced;
            }
            return new ReportSettings(newReports);
        }

        public static ReportSettings ReadXml(XmlReader reader)
        {
            return ReadXmlWithFunction(reader, ReportInfo.ReadXml);
        }

        public static ReportSettings ReadXmlVersion_21_1(XmlReader reader)
        {
            return ReadXmlWithFunction(reader, ReportInfo.ReadXmlVersion_21_1);
        }

        public static ReportSettings ReadXmlVersion_20_2(XmlReader reader)
        {
            return ReadXmlWithFunction(reader, ReportInfo.ReadXmlVersion_20_2);
        }

        private static ReportSettings ReadXmlWithFunction(XmlReader reader, Func<XmlReader, ReportInfo> ReadXmlFunction)
        {
            var reports = new List<ReportInfo>();
            while (reader.Read())
            {
                if (reader.IsElement(ReportInfo.XML_EL))
                {
                    reports.Add(ReadXmlFunction(reader));
                }
                if (reader.IsElement(SkylineSettings.XML_EL))
                {
                    break;
                }
            }
            return new ReportSettings(reports);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XML_EL);
            foreach (var report in Reports)
            {
                report.WriteXml(writer);
            }
            writer.WriteEndElement();
        }

        #region Run Commands

        public void WriteReportCommands(CommandWriter commandWriter, string analysisFolder, bool useRefineFile)
        {
            foreach(var report in Reports)
            {
                if (useRefineFile == report.UseRefineFile)
                    report.WriteAddExportReportCommand(commandWriter, analysisFolder);
            }
        }

        public List<Dictionary<RRunInfo, string>> GetScriptArguments(string analysisFolder)
        {
            var rRunInformation = new List<Dictionary<RRunInfo, string>>();
            foreach (var report in Reports)
                rRunInformation.AddRange(report.GetScriptArguments(analysisFolder));
            return rRunInformation;
        }

        #endregion

        protected bool Equals(ReportSettings other)
        {
            if (Reports.Count != other.Reports.Count)
            {
                return false;
            }

            for (int i = 0; i < Reports.Count; i++)
            {
                if (!Reports[i].Equals(other.Reports[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ReportSettings) obj);
        }

        public override int GetHashCode()
        {
            var hashCode = 367;
            foreach (var report in Reports)
            {
                hashCode += report.GetHashCode();
            }

            return hashCode;
        }
    }


    public class ReportInfo
    {
        public const string XML_EL = "report_info";

        // IMMUTABLE
        // Represents a report and associated r scripts to run using that report.
        
        public ReportInfo(string name, bool cultureSpecific, string path, List<Tuple<string, string>> rScripts, Dictionary<string, PanoramaFile> rScriptServers, bool useRefineFile)
        {
            Name = name;
            CultureSpecific = cultureSpecific;
            ReportPath = path ?? string.Empty;
            RScripts = ImmutableList.Create<Tuple<string,string>>().AddRange(rScripts);
            RScriptServers = ImmutableDictionary<string, PanoramaFile>.Empty.AddRange(rScriptServers); // TODO (Ali): use argument for this
            UseRefineFile = useRefineFile;

            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentException(Resources.ReportInfo_Validate_Report_must_have_name_);
            }
        }

        public readonly string Name;

        public readonly bool CultureSpecific;

        public readonly string ReportPath;

        public readonly ImmutableList<Tuple<string,string>> RScripts;

        public readonly ImmutableDictionary<string, PanoramaFile> RScriptServers;

        public readonly bool UseRefineFile;

        public bool WillDownloadData => RScriptServers.Count > 0;

        public object[] AsObjectArray()
        {
            var scriptsString = string.Empty;
            foreach (var script in RScripts)
            {
                scriptsString += Path.GetFileName(script.Item1) + Environment.NewLine;
            }
            var fileString = !UseRefineFile ? Resources.ReportInfo_AsObjectArray_Results : Resources.ReportInfo_AsObjectArray_Refined;
            return new object[] { Name, scriptsString, fileString };

        }

        public HashSet<string> RVersions()
        {
            var RVersions = new HashSet<string>();
            foreach (var rScript in RScripts)
                RVersions.Add(rScript.Item2);
            return RVersions;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentException(Resources.ReportInfo_Validate_Report_must_have_name_ + Environment.NewLine +
                                            Resources.ReportInfo_Validate_Please_enter_a_name_for_this_report_);
            }
            
            ValidateReportPath(ReportPath);
            foreach (var pathAndVersion in RScripts)
            {
                ValidateRScript(pathAndVersion.Item1, RScriptServers.ContainsKey(pathAndVersion.Item1));
                ValidateRVersion(pathAndVersion.Item2);
            }
        }

        public static void ValidateReportPath(string reportPath)
        {
            if (!string.IsNullOrEmpty(reportPath))
            {
                if (!File.Exists(reportPath))
                    throw new ArgumentException(string.Format(Resources.ReportInfo_ValidateReportPath_Report_path__0__is_not_a_valid_path_, reportPath) + Environment.NewLine +
                                                Resources.ReportInfo_Validate_Please_enter_a_path_to_an_existing_file_);
                FileUtil.ValidateNotInDownloads(reportPath, Resources.ReportInfo_ValidateReportPath_report_path);
            }
        }

        public static void ValidateRScriptWithoutServer(string rScriptPath)
        {
            ValidateRScript(rScriptPath, false);
        }

        public static void ValidateRScriptWithServer(string rScriptPath)
        {
            ValidateRScript(rScriptPath, true);
        }

        public static void ValidateRScript(string rScriptPath, bool hasServer)
        {
            FileUtil.ValidateNotEmptyPath(rScriptPath, Resources.MainSettings_ValidateDataFolder_data_folder);
            if (!hasServer && !File.Exists(rScriptPath))
            {
                throw new ArgumentException(string.Format(Resources.ReportInfo_ValidateRScriptPath_R_script_path__0__is_not_a_valid_path_,
                                                rScriptPath) + Environment.NewLine +
                                            Resources.ReportInfo_Validate_Please_enter_a_path_to_an_existing_file_);
            }

            if (hasServer)
            {
                var directoryExists = false;
                try
                {
                    var directory = Path.GetDirectoryName(rScriptPath);
                    directoryExists = Directory.Exists(directory) && FileUtil.PathHasDriveName(directory);
                }
                catch (Exception)
                {
                    // pass
                }
                if (!directoryExists)
                    throw new ArgumentException(string.Format(Resources.ReportInfo_ValidateRScript_The_folder_location_for_the_downloaded_R_script__0__does_not_exist_, rScriptPath));
            }
            FileUtil.ValidateNotInDownloads(rScriptPath, Resources.ReportInfo_ValidateRScriptPath_R_script);
        }

        public static void ValidateRVersion(string rVersion)
        {
            if (string.IsNullOrEmpty(rVersion))
                throw new ArgumentException(Resources.ReportInfo_ValidateRVersion_An_R_version_is_required_to_use_this_R_script__Please_select_an_R_version_);
            if (!Settings.Default.RVersions.ContainsKey(rVersion))
                throw new ArgumentException(string.Format(Resources.ReportInfo_ValidateRVersion_R_version__0__is_not_installed_on_this_computer_, rVersion) + Environment.NewLine +
                                            Resources.ReportInfo_ValidateRVersion_Please_choose_a_different_version_of_R_);
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out ReportInfo pathReplacedReportInfo)
        {
            var preferReplace = Program.FunctionalTest;
            var reportReplaced = TextUtil.SuccessfulReplace(ValidateReportPath, oldRoot, newRoot, ReportPath, preferReplace, out string replacedReportPath);
            var replacedRScripts = new List<Tuple<string, string>>();
            var anyScriptReplaced = false;
            var rScriptServers = new Dictionary<string, PanoramaFile>();
            foreach (var rScriptAndVersion in RScripts)
            {
                var rScriptValidator = RScriptServers.ContainsKey(rScriptAndVersion.Item1)
                    ? ValidateRScriptWithServer
                    : (Validator)ValidateRScriptWithoutServer;
                anyScriptReplaced = TextUtil.SuccessfulReplace(rScriptValidator, oldRoot, newRoot, rScriptAndVersion.Item1, preferReplace, out string replacedRScript) || anyScriptReplaced;
                replacedRScripts.Add(new Tuple<string, string>(replacedRScript, rScriptAndVersion.Item2));
                if (RScriptServers.ContainsKey(rScriptAndVersion.Item1))
                    rScriptServers.Add(replacedRScript, RScriptServers[rScriptAndVersion.Item1].ReplaceFolder(Path.GetDirectoryName(replacedRScript)));
            }
            pathReplacedReportInfo = new ReportInfo(Name, CultureSpecific, replacedReportPath, replacedRScripts, rScriptServers, UseRefineFile);
            return reportReplaced || anyScriptReplaced;
        }

        public ReportInfo ForcePathReplace(string oldRoot, string newRoot)
        {
            var reportPath = !string.IsNullOrEmpty(ReportPath)
                ? FileUtil.ForceReplaceRoot(oldRoot, newRoot, ReportPath)
                : string.Empty;
            var rScripts = new List<Tuple<string, string>>();
            var rScriptServers = new Dictionary<string, PanoramaFile>();
            foreach (var rScriptAndVersion in RScripts)
            {
                var scriptPath = FileUtil.ForceReplaceRoot(oldRoot, newRoot, rScriptAndVersion.Item1);
                rScripts.Add(new Tuple<string, string>(scriptPath, rScriptAndVersion.Item2));
                if (RScriptServers.ContainsKey(rScriptAndVersion.Item1))
                    rScriptServers.Add(scriptPath, RScriptServers[rScriptAndVersion.Item1].ReplaceFolder(Path.GetDirectoryName(scriptPath)));
            }
            return new ReportInfo(Name, CultureSpecific, reportPath, rScripts, rScriptServers, UseRefineFile);
        }

        public void AddDownloadingFiles(ServerFilesManager serverFiles)
        {
            foreach (var server in RScriptServers.Values)
                serverFiles.AddServer(server);
        }

        public ReportInfo UpdateRemoteFileSet(ImmutableDictionary<string, RemoteFileSource> remoteFileSources, out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources)
        {
            newRemoteFileSources = remoteFileSources;
            var newRScriptServers = new Dictionary<string, PanoramaFile>();
            foreach (var rScript in RScriptServers.Keys)
                newRScriptServers.Add(rScript, RScriptServers[rScript].UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources));
            return new ReportInfo(Name, CultureSpecific, ReportPath, RScripts.ToList(), newRScriptServers, UseRefineFile);
        }

        public ReportInfo ReplacedRemoteFileSource(RemoteFileSource existingSource, RemoteFileSource newSource, out bool replaced)
        {
            var newRScriptServers = new Dictionary<string, PanoramaFile>();
            replaced = false;
            foreach (var rScript in RScriptServers.Keys)
            {
                newRScriptServers.Add(rScript,
                    RScriptServers[rScript]
                        .ReplacedRemoteFileSource(existingSource, newSource, out bool scriptReplaced));
                replaced = replaced || scriptReplaced;
            }
            return new ReportInfo(Name, CultureSpecific, ReportPath, RScripts.ToList(), newRScriptServers, UseRefineFile);
        }

        public static ReportInfo ReadXml(XmlReader reader)
        {
            var name = reader.GetAttribute(XML_TAGS.name);
            var cultureSpecific = reader.GetBoolAttribute(XML_TAGS.culture_specific);
            var reportPath = reader.GetAttribute(XML_TAGS.path);
            var resultsFile = reader.GetNullableBoolAttribute(XML_TAGS.use_refined_file);
            var rScripts = new List<Tuple<string, string>>();
            var rScriptServers = new Dictionary<string, PanoramaFile>();
            if (reader.ReadToDescendant(XMLElements.R_SCRIPT))
            {
                do
                {
                    var path = reader.GetAttribute(XML_TAGS.path);
                    var version = reader.GetAttribute(XML_TAGS.version);
                    rScripts.Add(new Tuple<string, string>(path, version));
                    var remoteFile = PanoramaFile.ReadXml(reader, path);
                    if (remoteFile != null)
                        rScriptServers.Add(path, remoteFile);
                } while (reader.ReadToNextSibling(XMLElements.R_SCRIPT));
            }
            
            return new ReportInfo(name, cultureSpecific, reportPath, rScripts, rScriptServers, resultsFile?? false);
        }

        public static ReportInfo ReadXmlVersion_21_1(XmlReader reader)
        {
            var name = reader.GetAttribute(XML_TAGS.name);
            var cultureSpecific = reader.GetBoolAttribute(XML_TAGS.culture_specific);
            var reportPath = reader.GetAttribute(XML_TAGS.path);
            var resultsFile = reader.GetNullableBoolAttribute(XML_TAGS.use_refined_file);
            var rScripts = new List<Tuple<string, string>>();
            var rScriptServers = new Dictionary<string, PanoramaFile>();
            if (reader.ReadToDescendant(XMLElements.R_SCRIPT))
            {
                do
                {
                    var path = reader.GetAttribute(XML_TAGS.path);
                    var version = reader.GetAttribute(XML_TAGS.version);
                    rScripts.Add(new Tuple<string, string>(path, version));
                    var remoteFile = PanoramaFile.ReadXmlVersion_21_1(reader, path);
                    if (remoteFile != null)
                        rScriptServers.Add(path, remoteFile);
                } while (reader.ReadToNextSibling(XMLElements.R_SCRIPT));
            }

            return new ReportInfo(name, cultureSpecific, reportPath, rScripts, rScriptServers, resultsFile ?? false);
        }

        public static ReportInfo ReadXmlVersion_20_2(XmlReader reader)
        {
            var name = reader.GetAttribute(OLD_XML_TAGS.Name);
            var cultureSpecific = reader.GetBoolAttribute(OLD_XML_TAGS.CultureSpecific);
            var reportPath = reader.GetAttribute(OLD_XML_TAGS.Path);
            var resultsFile = reader.GetNullableBoolAttribute(OLD_XML_TAGS.UseRefineFile);
            var rScripts = new List<Tuple<string, string>>();
            while (reader.IsStartElement() && !reader.IsEmptyElement)
            {
                if (Equals(reader.Name, OLD_XML_TAGS.script_path.ToString()))
                {
                    var tupleItems = reader.ReadElementContentAsString().Split(new[] { '(', ',', ')' },
                        StringSplitOptions.RemoveEmptyEntries);
                    rScripts.Add(new Tuple<string, string>(tupleItems[0].Trim(), tupleItems[1].Trim()));
                }
                else
                {
                    reader.Read();
                }
            }

            return new ReportInfo(name, cultureSpecific, reportPath, rScripts, new Dictionary<string, PanoramaFile>(), resultsFile ?? false);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XML_EL);
            writer.WriteAttributeIfString(XML_TAGS.name, Name);
            writer.WriteAttributeIfString(XML_TAGS.path, ReportPath);
            writer.WriteAttribute(XML_TAGS.use_refined_file, UseRefineFile);
            writer.WriteAttribute(XML_TAGS.culture_specific, CultureSpecific);
            foreach (var script in RScripts)
            {
                writer.WriteStartElement(XMLElements.R_SCRIPT);
                writer.WriteAttributeIfString(XML_TAGS.path, script.Item1);
                writer.WriteAttributeIfString(XML_TAGS.version, script.Item2);
                if (RScriptServers.ContainsKey(script.Item1))
                {
                    RScriptServers[script.Item1].WriteXml(writer);
                }
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        #region Run Commands

        public const string ADD_REPORT_OVERWRITE_COMMAND =
            "--report-add=\"{0}\" --report-conflict-resolution=overwrite";
        public const string EXPORT_REPORT_COMMAND = "--report-name=\"{0}\" --report-file=\"{1}\"";
        public const string REPORT_INVARIANT_COMMAND = "--report-invariant";
        public const string REPORT_TSV_COMMAND = "--report-format=tsv";
        public const string SAVE_SETTINGS_COMMAND = "--save-settings";
        public const string RUN_R_ARGUMENT = "\"{0}\" \"{1}\"";

        public void WriteAddExportReportCommand(CommandWriter commandWriter, string analysisFolder)
        {
            if (!string.IsNullOrEmpty(ReportPath))
            {
                commandWriter.Write(ADD_REPORT_OVERWRITE_COMMAND, ReportPath);
                commandWriter.Write(SAVE_SETTINGS_COMMAND);
            }
            commandWriter.Write(EXPORT_REPORT_COMMAND, Name, Path.Combine(analysisFolder, Name + TextUtil.EXT_CSV));
            if (!CultureSpecific)
            {
                commandWriter.Write(REPORT_INVARIANT_COMMAND);
                if (!commandWriter.ExportsInvariantReport)
                    commandWriter.Write(REPORT_TSV_COMMAND);
            }
            commandWriter.EndCommandGroup();
        }

        public List<Dictionary<RRunInfo, string>> GetScriptArguments(string analysisFolder)
        {
            var rRunInformation = new List<Dictionary<RRunInfo, string>>();
            var newReportPath = Path.Combine(analysisFolder, Name + TextUtil.EXT_CSV);
            foreach (var scriptAndVersion in RScripts)
            {
                var rExeAndArguments = new Dictionary<RRunInfo, string>();
                rExeAndArguments.Add(RRunInfo.ExePath, Settings.Default.RVersions[scriptAndVersion.Item2]);
                rExeAndArguments.Add(RRunInfo.Arguments, string.Format(RUN_R_ARGUMENT, scriptAndVersion.Item1, newReportPath));
                rRunInformation.Add(rExeAndArguments);
            }
            return rRunInformation;
        }
        
        #endregion

        protected bool Equals(ReportInfo other)
        {
            if (!other.Name.Equals(Name) || !other.ReportPath.Equals(ReportPath) || other.RScripts.Count != RScripts.Count)
                return false;
            for (int i = 0; i < RScripts.Count; i++)
            {
                if (!RScripts[i].Item1.Equals(other.RScripts[i].Item1) || !RScripts[i].Item2.Equals(other.RScripts[i].Item2))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ReportInfo) obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() + ReportPath.GetHashCode();
        }
    }

    public enum RRunInfo
    {
        ExePath,
        Arguments
    }
}
