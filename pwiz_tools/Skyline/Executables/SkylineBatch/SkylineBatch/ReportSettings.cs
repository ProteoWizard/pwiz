/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;
using System.Xml;
using System.IO;

namespace SkylineBatch
{
    public class ReportSettings
    {
        public List<ReportInfo> Reports { get; private set; }

        public ReportSettings(List<ReportInfo> reports = null)
        {
            Reports = reports == null ? new List<ReportInfo>() : reports;
        }

        public void Add(ReportInfo info) {
        
            Reports.Add(info);
        }

        public ReportSettings Copy()
        {
            var copyReports = new List<ReportInfo>();
            foreach (var report in Reports)
            {
                copyReports.Add(report.Copy());
            }

            return new ReportSettings(copyReports);
        }

        public void ValidateSettings()
        {
            foreach (var reportInfo in Reports)
            {
                reportInfo.ValidateSettings();
            }
        }

        public void ReadXml(XmlReader reader)
        {
            Reports.Clear();
            while (reader.IsStartElement())
            {
                if (reader.Name == "report_info")
                {
                    var report = new ReportInfo();
                    report.ReadXml(reader);
                    Reports.Add(report);
                } else if (reader.IsEmptyElement)
                {
                    break;
                }
                reader.Read();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("report_settings");
            foreach (var report in Reports)
            {
                report.WriteXML(writer);
            }
            writer.WriteEndElement();
        }

        protected bool Equals(ReportSettings other)
        {
            if (Reports.Count != other.Reports.Count) { return false; }
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
            return Equals((ReportSettings)obj);
        }
    }

    public class ReportInfo
    {
        public string Name { get; private set; }
        public string ReportPath { get; private set; }
        
        public List<string> rScripts { get; private set; }

        public ReportInfo(string name, string path, List<string> rScripts = null)
        {
            Name = name;
            ReportPath = path;
            this.rScripts = rScripts == null ? new List<string>() : rScripts;
        }

        public ReportInfo()
        {
            this.rScripts = new List<string>();
        }

        public object[] AsArray()
        {
            var scriptsString = "";
            foreach (var scriptPath in rScripts)
            {
                scriptsString += Path.GetFileName(scriptPath) + "\n";
            }
            return new object[] {Name, ReportPath, scriptsString};
        }

        public ReportInfo Copy()
        {
            var copyScripts = new string[rScripts.Count];
            rScripts.CopyTo(copyScripts);
            return new ReportInfo(Name, ReportPath, copyScripts.ToList());
        }

        public void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentException("Report must have name.");
            }

            if (!File.Exists(ReportPath))
            {
                throw new ArgumentException(string.Format("Report path {0} is not a valid path.", ReportPath));
            }
        }

        public void Set(string name, string path, List<string> scripts)
        {
            Name = name;
            ReportPath = path;
            rScripts = scripts;
        }

        public bool Empty()
        {
            return string.IsNullOrEmpty(Name);
        }

        private enum ATTR
        {
            name,
            path,
        };

        public void ReadXml(XmlReader reader)
        {
            Name = reader.GetAttribute(ATTR.name);
            ReportPath = reader.GetAttribute(ATTR.path);
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
        }
        
        
        public void WriteXML(XmlWriter writer)
        {
            writer.WriteStartElement("report_info");
            writer.WriteAttributeIfString(ATTR.name, Name);
            writer.WriteAttributeIfString(ATTR.path, ReportPath);
            foreach (var script in rScripts)
            {
                writer.WriteElementString("script_path", script);
            }
            writer.WriteEndElement();
        }

        protected bool Equals(ReportInfo other)
        {
            if (other.Name != Name || other.ReportPath != ReportPath || other.rScripts.Count != rScripts.Count)
                return false;
            for (int i = 0; i < rScripts.Count; i++)
            {
                if (rScripts[i] != other.rScripts[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ReportInfo)obj);
        }
    }


}
