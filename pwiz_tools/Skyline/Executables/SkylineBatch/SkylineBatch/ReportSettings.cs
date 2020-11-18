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


namespace SkylineBatch
{
    public class ReportSettings
    {
    // IMMUTABLE
    //
    // ReportSettings contains a list of reportInfos, each of which represents an individual report with R scripts to run on it.
    // An empty reportSettings is a valid instance of this class, as configurations don't require reports to run the batch commands.

    

    public ReportSettings(List<ReportInfo> reports)
    {
        Reports = ImmutableList.CreateRange(reports);
        Validate();
    }

    public readonly ImmutableList<ReportInfo> Reports;

    public void Validate()
        {
            foreach (var reportInfo in Reports)
            {
                reportInfo.Validate();
            }
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
        

        public ReportInfo(string name, string path, List<string> rScripts)
        {
            Name = name;
            ReportPath = path;
            RScripts = ImmutableList.CreateRange(rScripts);

            Validate();
        }

        public readonly string Name;

        public readonly string ReportPath;

        public readonly ImmutableList<string> RScripts;

    public object[] AsArray()
        {
            var scriptsString = "";
            foreach (var scriptPath in RScripts)
            {
                scriptsString += Path.GetFileName(scriptPath) + "\n";
            }

            return new object[] {Name, ReportPath, scriptsString};
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentException("Report must have name.");
            }

            if (!File.Exists(ReportPath))
            {
                throw new ArgumentException(string.Format("Report path {0} is not a valid path.", ReportPath));
            }

            foreach (var script in RScripts)
            {
                if (!File.Exists(script))
                {
                    throw new ArgumentException(string.Format("R script path {0} is not a valid path.", script));
                }
            }
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
            var rScripts = new List<string>();
            while (reader.IsStartElement() && !reader.IsEmptyElement)
            {
                if (reader.Name == "script_path")
                {

                    rScripts.Add(reader.ReadElementContentAsString());
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
                if (RScripts[i] != other.RScripts[i])
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
