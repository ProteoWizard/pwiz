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
using SharedBatch;
using SkylineBatch.Properties;


namespace SkylineBatch
{
    public class ReportSettings
    {
        // IMMUTABLE
        // ReportSettings contains a list of reportInfos, each of which represents an individual report with R scripts to run on it.
        // An empty reportSettings is a valid instance of this class, as configurations don't require reports to run the batch commands.
        
        public ReportSettings(List<ReportInfo> reports)
        {
            Reports = ImmutableList.CreateRange(reports);
        }

        public readonly ImmutableList<ReportInfo> Reports;

        public void Validate()
        {
            foreach (var reportInfo in Reports)
            {
                reportInfo.Validate();
            }
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
        // IMMUTABLE
        // Represents a report and associated r scripts to run using that report.
        
        public ReportInfo(string name, string path, List<Tuple<string, string>> rScripts)
        {
            Name = name;
            ReportPath = path;
            RScripts = ImmutableList.Create<Tuple<string,string>>().AddRange(rScripts);

            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentException(Resources.ReportInfo_Validate_Report_must_have_name_);
            }
        }

        public readonly string Name;

        public readonly string ReportPath;

        public readonly ImmutableList<Tuple<string,string>> RScripts;

        public object[] AsObjectArray()
        {
            var scriptsString = string.Empty;
            foreach (var script in RScripts)
            {
                scriptsString += Path.GetFileName(script.Item1) + Environment.NewLine;
            }
            return new object[] {Name, ReportPath, scriptsString};
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
                ValidateRScriptPath(pathAndVersion.Item1);
                ValidateRVersion(pathAndVersion.Item2);
            }
        }

        public static void ValidateReportPath(string reportPath)
        {
            if (!File.Exists(reportPath))
                throw new ArgumentException(string.Format(Resources.ReportInfo_ValidateReportPath_Report_path__0__is_not_a_valid_path_, reportPath) + Environment.NewLine +
                                            Resources.ReportInfo_Validate_Please_enter_a_path_to_an_existing_file_);
        }

        public static void ValidateRScriptPath(string rScriptPath)
        {
            if (!File.Exists(rScriptPath))
                throw new ArgumentException(string.Format(Resources.ReportInfo_ValidateRScriptPath_R_script_path__0__is_not_a_valid_path_,
                                                rScriptPath) + Environment.NewLine +
                                            Resources.ReportInfo_Validate_Please_enter_a_path_to_an_existing_file_);
        }

        public static void ValidateRVersion(string rVersion)
        {
            if (!Settings.Default.RVersions.ContainsKey(rVersion))
                throw new ArgumentException(string.Format(Resources.ReportInfo_ValidateRVersion_R_version__0__is_not_installed_on_this_computer_, rVersion) + Environment.NewLine +
                                            Resources.ReportInfo_ValidateRVersion_Please_choose_a_different_version_of_R_);
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out ReportInfo pathReplacedReportInfo)
        {
            var reportReplaced = TextUtil.TryReplaceStart(oldRoot, newRoot, ReportPath, out string replacedReportPath);
            var replacedRScripts = new List<Tuple<string, string>>();
            var anyScriptReplaced = false;
            foreach (var rScriptAndVersion in RScripts)
            {
                anyScriptReplaced = TextUtil.TryReplaceStart(oldRoot, newRoot, rScriptAndVersion.Item1, out string replacedRScript) || anyScriptReplaced;
                replacedRScripts.Add(new Tuple<string, string>(replacedRScript, rScriptAndVersion.Item2));
            }
            pathReplacedReportInfo = new ReportInfo(Name, replacedReportPath, replacedRScripts);
            return reportReplaced || anyScriptReplaced;
        }
        
        private enum Attr
        {
            Name,
            Path,
        };

        public static ReportInfo ReadXml(XmlReader reader)
        {
            var name = reader.GetAttribute(Attr.Name);
            var reportPath = reader.GetAttribute(Attr.Path);
            var rScripts = new List<Tuple<string, string>>();
            while (reader.IsStartElement() && !reader.IsEmptyElement)
            {
                if (reader.Name == "script_path")
                {
                    var tupleItems = reader.ReadElementContentAsString().Split(new[]{ '(', ',', ')' }, StringSplitOptions.RemoveEmptyEntries);
                    rScripts.Add(new Tuple<string,string>(tupleItems[0].Trim(), tupleItems[1].Trim()));
                }
                else
                {
                    reader.Read();
                }
            }

            return new ReportInfo(name, reportPath, rScripts);
        }
        
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("report_info");
            writer.WriteAttributeIfString(Attr.Name, Name);
            writer.WriteAttributeIfString(Attr.Path, ReportPath);
            foreach (var script in RScripts)
            {
                writer.WriteElementString("script_path", script);
            }

            writer.WriteEndElement();
        }

        protected bool Equals(ReportInfo other)
        {
            if (other.Name != Name || other.ReportPath != ReportPath || other.RScripts.Count != RScripts.Count)
                return false;
            for (int i = 0; i < RScripts.Count; i++)
            {
                if (RScripts[i].Item1 != other.RScripts[i].Item1 || RScripts[i].Item2 != other.RScripts[i].Item2)
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
}
