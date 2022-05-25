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
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using SkylineBatch.Properties;
using SharedBatch;

namespace SkylineBatch
{
    [XmlRoot("skylinebatch_config")]
    public class SkylineBatchConfig : IConfig
    {
        // IMMUTABLE - all fields are readonly, all variables are immutable
        // A configuration is a set of information about a skyline file, data, reports and scripts.
        // To be a valid configuration, it must contain enough of this information to run a batch 
        // script that will copy the skyline file, import data, export reports, and run r scripts.

        
        public SkylineBatchConfig(string name, bool enabled, bool logTestFormat, DateTime modified, MainSettings mainSettings, 
            FileSettings fileSettings, RefineSettings refineSettings, ReportSettings reportSettings, 
            SkylineSettings skylineSettings)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(string.Format(Resources.SkylineBatchConfig_SkylineBatchConfig___0___is_not_a_valid_name_for_the_configuration_, name) + Environment.NewLine +
                                            Resources.SkylineBatchConfig_SkylineBatchConfig_Please_enter_a_name_);
            }
            Name = name;
            Enabled = enabled;
            LogTestFormat = logTestFormat;
            Modified = modified;
            MainSettings = mainSettings;
            FileSettings = fileSettings;
            RefineSettings = refineSettings;
            ReportSettings = reportSettings;
            SkylineSettings = skylineSettings;
        }

        public readonly string Name;

        public readonly DateTime Modified;

        public readonly MainSettings MainSettings;

        public readonly RefineSettings RefineSettings;

        public readonly FileSettings FileSettings;

        public readonly ReportSettings ReportSettings;

        public readonly SkylineSettings SkylineSettings;

        public readonly bool Enabled;

        public bool LogTestFormat;

        public bool UsesSkyline => SkylineSettings.Type == SkylineType.Skyline;

        public bool UsesSkylineDaily => SkylineSettings.Type == SkylineType.SkylineDaily;

        public bool UsesCustomSkylinePath => SkylineSettings.Type == SkylineType.Custom;

        public bool WillDownloadData => MainSettings.WillDownloadData || ReportSettings.WillDownloadData;

        public string GetName() { return Name; }

        public DateTime GetModified()  { return Modified; }

        private string hoursWhitespace; // spaces added to the front of runtime strings to align the

        public ListViewItem AsListViewItem(IConfigRunner runner, Graphics graphics)
        {
            var configRunner = (ConfigRunner) runner;
            var lvi = new ListViewItem(Name);
            lvi.Checked = Enabled;
            lvi.SubItems.Add(Modified.ToShortDateString());
            lvi.SubItems.Add(configRunner.GetDisplayStatus());
            lvi.SubItems.Add(configRunner.StartTime != null
                ? ((DateTime)configRunner.StartTime).ToString("T")
                : string.Empty);

            // calculate space needed for hours in runtime column
            if (hoursWhitespace == null)
            {
                var hoursSize = graphics.MeasureString("00:", lvi.Font).Width;
                hoursWhitespace = "          .";
                while (hoursSize < graphics.MeasureString(hoursWhitespace, lvi.Font).Width)
                    hoursWhitespace = hoursWhitespace.Substring(1);
                hoursWhitespace = hoursWhitespace.Replace('.', ' ');
            }
            if (configRunner.RunTime != null)
            {
                TimeSpan runTime = (TimeSpan)configRunner.RunTime;
                var runTimeString = runTime.Hours > 0
                    ? runTime.ToString(@"hh\:mm\:ss")
                    : hoursWhitespace + runTime.ToString(@"mm\:ss");
                lvi.SubItems.Add(runTimeString);
            }
            else
                lvi.SubItems.Add(string.Empty);

            return lvi;
        }

        public SkylineBatchConfig WithoutDependency()
        {
            return new SkylineBatchConfig(Name, Enabled, LogTestFormat, DateTime.Now, MainSettings.WithoutDependency(),
                FileSettings, RefineSettings, ReportSettings, SkylineSettings);
        }

        public SkylineBatchConfig DependentChanged(string newName, string newTemplateFile)
        {
            return new SkylineBatchConfig(Name, Enabled, LogTestFormat, DateTime.Now, MainSettings.UpdateDependent(newName, newTemplateFile),
                FileSettings, RefineSettings, ReportSettings, SkylineSettings);
        }

        public IConfig UpdateRemoteFileSet(ImmutableDictionary<string, RemoteFileSource> remoteFileSources, out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources)
        {
            newRemoteFileSources = remoteFileSources;
            var newMainSettings = MainSettings.UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources);
            var newReportSettings = ReportSettings.UpdateRemoteFileSet(newRemoteFileSources, out newRemoteFileSources);
            return new SkylineBatchConfig(Name, Enabled, LogTestFormat, DateTime.Now, newMainSettings,
                FileSettings, RefineSettings, newReportSettings, SkylineSettings);
        }

        public IConfig ReplacedRemoteFileSource(RemoteFileSource existingSource, RemoteFileSource newSource, out bool replaced)
        {
            var newMainSettings = MainSettings.ReplacedRemoteFileSource(existingSource, newSource, out bool mainSettingsReplaced);
            var newReportSettings = ReportSettings.ReplacedRemoteFileSource(existingSource, newSource, out bool reportsReplaced);
            replaced = mainSettingsReplaced || reportsReplaced;
            return new SkylineBatchConfig(Name, Enabled, LogTestFormat, DateTime.Now, newMainSettings,
                FileSettings, RefineSettings, newReportSettings, SkylineSettings);
        }

        public IConfig ReplaceSkylineVersion(SkylineSettings newSettings)
        {
            return new SkylineBatchConfig(Name, Enabled, LogTestFormat, DateTime.Now, MainSettings,
                FileSettings, RefineSettings, ReportSettings, newSettings);
        }

        public void AddDownloadingFiles(ServerFilesManager serverFiles)
        {
            MainSettings.AddDownloadingFiles(serverFiles);
            ReportSettings.AddDownloadingFiles(serverFiles);
        }


        #region XML

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static SkylineBatchConfig ReadXml(XmlReader reader)
        {
            return ReadXml(reader, Settings.Default.XmlVersion);
        }

        public static SkylineBatchConfig ReadXml(XmlReader reader, decimal version)
        {
            string name;
            bool enabled;
            bool logTestFormat;
            DateTime modified;
            switch (version)
            {
                case 21.12M:
                    ReadConfigVariables(reader, out name, out enabled, out logTestFormat, out modified);
                    break;
                case 21.11M:
                    ReadConfigVariables(reader, out name, out enabled, out logTestFormat, out modified);
                    break;
                case 21.1M:
                    ReadConfigVariables(reader, out name, out enabled, out logTestFormat, out modified);
                    break;
                case 20.2M:
                    ReadConfigVariables_20_2(reader, out name, out enabled, out logTestFormat, out modified);
                    break;
                default:
                    throw new ArgumentException(string.Format(
                        Resources.XmlUpdater_GetUpdatedXml_The_version_of_the_imported_file__0__was_not_recognized__No_configurations_will_be_imported_,
                        version));
            }

            XmlUtil.ReadUntilElement(reader);
            MainSettings mainSettings = null;
            RefineSettings refineSettings = null;
            FileSettings fileSettings = null;
            ReportSettings reportSettings = null;
            SkylineSettings skylineSettings = null;
            string exceptionMessage = null;
            try
            {
                switch (version)
                {
                    case 21.12M:
                        mainSettings = MainSettings.ReadXml(reader);
                        fileSettings = FileSettings.ReadXml(reader);
                        refineSettings = RefineSettings.ReadXml(reader);
                        reportSettings = ReportSettings.ReadXml(reader);
                        skylineSettings = SkylineSettings.ReadXml(reader);
                        break;
                    case 21.11M:
                        mainSettings = MainSettings.ReadXml(reader);
                        fileSettings = FileSettings.ReadXml(reader);
                        refineSettings = RefineSettings.ReadXml(reader);
                        reportSettings = ReportSettings.ReadXml(reader);
                        skylineSettings = SkylineSettings.ReadXml(reader);
                        break;
                    case 21.1M:
                        mainSettings = MainSettings.ReadXmlVersion_21_1(reader);
                        fileSettings = FileSettings.ReadXml(reader);
                        refineSettings = RefineSettings.ReadXml(reader);
                        reportSettings = ReportSettings.ReadXmlVersion_21_1(reader);
                        skylineSettings = SkylineSettings.ReadXml(reader);
                        break;
                    default: // 20.2M
                        mainSettings = MainSettings.ReadXmlVersion_20_2(reader);
                        fileSettings = FileSettings.ReadXmlVersion_20_2(reader);
                        refineSettings = RefineSettings.ReadXmlVersion_20_2(reader);
                        reportSettings = ReportSettings.ReadXmlVersion_20_2(reader);
                        skylineSettings = SkylineSettings.ReadXmlVersion_20_2(reader);
                        break;
                }
            }
            catch (ArgumentException e)
            {
                exceptionMessage = string.Format("\"{0}\" ({1})", name, e.Message);
            }
            
            do
            {
                reader.Read();
            } while (!(reader.Name == XMLElements.BATCH_CONFIG && reader.NodeType == XmlNodeType.EndElement) && !reader.EOF);

            if (exceptionMessage != null)
                throw new ArgumentException(exceptionMessage);

            return new SkylineBatchConfig(name, enabled, logTestFormat, modified, mainSettings, fileSettings,
                refineSettings, reportSettings, skylineSettings);
        }

        private static void ReadConfigVariables(XmlReader reader, out string name, out bool enabled, out bool logTestFormat, out DateTime modified)
        {
            name = reader.GetAttribute(XML_TAGS.name);
            enabled = reader.GetBoolAttribute(XML_TAGS.enabled);
            logTestFormat = !SkylineInstallations.HasLocalSkylineCmd && reader.GetBoolAttribute(XML_TAGS.log_test_format);
            DateTime.TryParse(reader.GetAttribute(XML_TAGS.modified), CultureInfo.InvariantCulture, DateTimeStyles.None, out modified);
        }

        private static void ReadConfigVariables_20_2(XmlReader reader, out string name, out bool enabled, out bool logTestFormat, out DateTime modified)
        {
            name = reader.GetAttribute(OLD_XML_TAGS.Name);
            enabled = reader.GetBoolAttribute(OLD_XML_TAGS.Enabled);
            logTestFormat = false;
            DateTime.TryParse(reader.GetAttribute(OLD_XML_TAGS.Modified), CultureInfo.InvariantCulture, DateTimeStyles.None, out modified);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XMLElements.BATCH_CONFIG);
            writer.WriteAttribute(XML_TAGS.name, Name);
            writer.WriteAttribute(XML_TAGS.enabled, Enabled);
            writer.WriteAttribute(XML_TAGS.log_test_format, LogTestFormat);
            writer.WriteAttributeIfString(XML_TAGS.modified, Modified.ToString(CultureInfo.InvariantCulture));
            MainSettings.WriteXml(writer);
            FileSettings.WriteXml(writer);
            RefineSettings.WriteXml(writer);
            ReportSettings.WriteXml(writer);
            SkylineSettings.WriteXml(writer);
            writer.WriteEndElement();
        }
        
        #endregion
        
        public void Validate()
        {
            MainSettings.Validate();
            FileSettings.Validate();
            RefineSettings.Validate();
            ReportSettings.Validate();
            SkylineSettings.Validate();
        }

        public bool RunWillOverwrite(RunBatchOptions runOption, string configurationHeader, out StringBuilder message)
        {
            message = new StringBuilder();
            if (runOption == RunBatchOptions.DOWNLOAD_DATA) return false;
            
            return MainSettings.RunWillOverwrite(runOption, configurationHeader, out message)
                || RefineSettings.RunWillOverwrite(runOption, configurationHeader, out message)
                || ReportSettings.RunWillOverwrite(runOption, configurationHeader, MainSettings.AnalysisFolderPath, out message);
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out IConfig replacedPathConfig)
        {
            var mainSettingsReplaced = MainSettings.TryPathReplace(oldRoot, newRoot, out MainSettings pathReplacedMainSettings);
            var refineSettingsReplaced =
                RefineSettings.TryPathReplace(oldRoot, newRoot, out RefineSettings pathReplacedRefineSettings);
            var reportSettingsReplaced =
                ReportSettings.TryPathReplace(oldRoot, newRoot, out ReportSettings pathReplacedReportSettings);
            replacedPathConfig = new SkylineBatchConfig(Name, Enabled, LogTestFormat, DateTime.Now, pathReplacedMainSettings,
                FileSettings, pathReplacedRefineSettings, pathReplacedReportSettings, SkylineSettings);
            return mainSettingsReplaced || reportSettingsReplaced || refineSettingsReplaced;
        }

        public IConfig ForcePathReplace(string oldRoot, string newRoot)
        {
            var mainSettings = MainSettings.ForcePathReplace(oldRoot, newRoot);
            var refineSettings = RefineSettings.ForcePathReplace(oldRoot, newRoot);
            var reportSettings = ReportSettings.ForcePathReplace(oldRoot, newRoot);
            return new SkylineBatchConfig(Name, Enabled, LogTestFormat, DateTime.Now, mainSettings,
                FileSettings, refineSettings, reportSettings, SkylineSettings);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Name: ").AppendLine(Name);
            sb.Append("Enabled: ").Append(Enabled);
            sb.Append("Modified: ").Append(Modified.ToShortDateString()).AppendLine(Modified.ToShortTimeString());
            sb.AppendLine(string.Empty).AppendLine("Main Settings");
            sb.Append(MainSettings);
            return sb.ToString();
        }

        #region Batch Commands
        public static readonly string OPEN_SKYLINE_FILE_COMMAND = "--in=\"{0}\"";
        public static readonly string SAVE_AS_NEW_FILE_COMMAND = "--out=\"{0}\"";
        public static readonly string SAVE_COMMAND = "--save";
        public static readonly string SAVE_SETTINGS_COMMAND = "--save-settings";

        public void WriteSaveCommand(CommandWriter commandWriter) => commandWriter.Write(SAVE_COMMAND);
        public void WriteSaveSettingsCommand(CommandWriter commandWriter) => commandWriter.Write(SAVE_SETTINGS_COMMAND);

        public void WriteOpenSkylineTemplateCommand(CommandWriter commandWriter) => MainSettings.WriteOpenSkylineTemplateCommand(commandWriter);
        public void WriteOpenSkylineResultsCommand(CommandWriter commandWriter) => MainSettings.WriteOpenSkylineResultsCommand(commandWriter);
        public void WriteSaveToResultsFile(CommandWriter commandWriter) => MainSettings.WriteSaveToResultsFile(commandWriter);

        public void WriteImportDataCommand(CommandWriter commandWriter) => MainSettings.WriteImportDataCommand(commandWriter);
        public void WriteImportNamingPatternCommand(CommandWriter commandWriter) => MainSettings.WriteImportNamingPatternCommand(commandWriter);
        public void WriteImportAnnotationsCommand(CommandWriter commandWriter) => MainSettings.WriteImportAnnotationsCommand(commandWriter);
        
        public void WriteMsOneCommand(CommandWriter commandWriter) => FileSettings.WriteMsOneCommand(commandWriter);
        public void WriteMsMsCommand(CommandWriter commandWriter) => FileSettings.WriteMsMsCommand(commandWriter);
        public void WriteAddDecoysCommand(CommandWriter commandWriter) => FileSettings.WriteAddDecoysCommand(commandWriter);
        public void WriteRetentionTimeCommand(CommandWriter commandWriter) => FileSettings.WriteRetentionTimeCommand(commandWriter);
        public void WriteTrainMProphetCommand(CommandWriter commandWriter) => FileSettings.WriteTrainMProphetCommand(commandWriter, Name);

        public void WriteRefineCommands(CommandWriter commandWriter) => RefineSettings.WriteRefineCommands(commandWriter);
        public void WriteOpenRefineFileCommand(CommandWriter commandWriter) => RefineSettings.WriteOpenRefineFileCommand(commandWriter);

        public void WriteRefinedFileReportCommands(CommandWriter commandWriter) => ReportSettings.WriteReportCommands(commandWriter, MainSettings.AnalysisFolderPath, true);
        public void WriteResultsFileReportCommands(CommandWriter commandWriter) => ReportSettings.WriteReportCommands(commandWriter, MainSettings.AnalysisFolderPath, false);

        public List<Dictionary<RRunInfo, string>> GetScriptArguments() => ReportSettings.GetScriptArguments(MainSettings.AnalysisFolderPath);

        #endregion

        #region Equality members

        protected bool Equals(SkylineBatchConfig other)
        {
            return string.Equals(Name, other.Name)
                   && Equals(MainSettings, other.MainSettings)
                   && Equals(ReportSettings, other.ReportSettings)
                   && Equals(FileSettings, other.FileSettings)
                   && Equals(RefineSettings, other.RefineSettings)
                   && Equals(SkylineSettings, other.SkylineSettings);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SkylineBatchConfig) obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() + Modified.GetHashCode() +
                   MainSettings.GetHashCode() + ReportSettings.GetHashCode();
        }

        #endregion
    }
}
