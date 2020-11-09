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
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    [XmlRoot("main_settings")]
    public class MainSettings 
    {

        public string TemplateFilePath { get; set; }

        public string AnalysisFolderPath { get; set; }

        public string DataFolderPath { get; set; }

        public string ReplicateNamingPattern { get; set; }


        public DateTime LastAcquiredFileDate { get; set; } // Not saved to Properties.Settings
        public DateTime LastArchivalDate { get; set; }

        public static MainSettings GetDefault()
        {
            return new MainSettings(); //settings;
        }

        public MainSettings Clone()
        {
            return new MainSettings
            {
                //SkylineFilePath = SkylineFilePath,
                TemplateFilePath = TemplateFilePath,
                AnalysisFolderPath = AnalysisFolderPath,
                DataFolderPath = DataFolderPath,
                ReplicateNamingPattern = ReplicateNamingPattern
            };
        }

        public MainSettings MakeChild()
        {
            var childSettings = Clone();
            childSettings.AnalysisFolderPath = Path.GetDirectoryName(AnalysisFolderPath) + "\\";
            return childSettings;
        }

        public string GetNewTemplatePath()
        {
            return AnalysisFolderPath + "\\" + Path.GetFileName(TemplateFilePath);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            //sb.Append("Skyline file: ").AppendLine(SkylineFilePath);
            sb.Append("Template file: ").AppendLine(TemplateFilePath);
            sb.Append("Analysis folder: ").AppendLine(AnalysisFolderPath);
            sb.Append("Data folder: ").AppendLine(DataFolderPath);
            sb.Append("Replicate naming pattern: ").AppendLine(ReplicateNamingPattern);
            return sb.ToString();
        }

        public void ValidateSettings()
        {
            // TODO check skyline folder ?
            // Path to the Skyline file.

            CheckIfEmptyPath(TemplateFilePath, "Skyline file");
            if (!File.Exists(TemplateFilePath))
            {
                throw new ArgumentException(string.Format(Resources.Template_file_does_not_exist, TemplateFilePath));
            }

            CheckIfEmptyPath(AnalysisFolderPath, "analysis folder");
            var analysisFolderDirectory = Path.GetDirectoryName(AnalysisFolderPath);
            if (!Directory.Exists(analysisFolderDirectory))
            {
                throw new ArgumentException(string.Format(Resources.Analysis_folder_does_not_exist, analysisFolderDirectory));
            }

            CheckIfEmptyPath(DataFolderPath, "data folder");
            if (!Directory.Exists(DataFolderPath))
            {
                throw new ArgumentException(string.Format(Resources.Data_folder_does_not_exist, DataFolderPath));
            }

            // create analysis folder if doesn't exist
            if (!Directory.Exists(AnalysisFolderPath))
            {
                Directory.CreateDirectory(AnalysisFolderPath);
            }
        }

        public void CheckIfEmptyPath(string input, string name)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException(string.Format(Resources.Specify_path_to, name));
            }
        }

        

        


        #region Implementation of IXmlSerializable interface

        private enum ATTR
        {
            template_file_path,
            analysis_folder_path,
            data_folder_path,
            replicate_naming_pattern,
        };

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            TemplateFilePath = reader.GetAttribute(ATTR.template_file_path);
            AnalysisFolderPath = reader.GetAttribute(ATTR.analysis_folder_path);
            DataFolderPath = reader.GetAttribute(ATTR.data_folder_path);
            ReplicateNamingPattern = reader.GetAttribute(ATTR.replicate_naming_pattern);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("main_settings");
            writer.WriteAttributeIfString(ATTR.template_file_path, TemplateFilePath);
            writer.WriteAttributeIfString(ATTR.analysis_folder_path, AnalysisFolderPath);
            writer.WriteAttributeIfString(ATTR.data_folder_path, DataFolderPath);
            writer.WriteAttributeIfString(ATTR.replicate_naming_pattern, ReplicateNamingPattern);
            writer.WriteEndElement();
        }
        #endregion

        protected bool Equals(MainSettings other)
        {

            return (other.TemplateFilePath == TemplateFilePath &&
                    other.AnalysisFolderPath == AnalysisFolderPath &&
                    other.DataFolderPath == DataFolderPath &&
                    other.ReplicateNamingPattern == ReplicateNamingPattern &&
                    other.LastAcquiredFileDate == LastAcquiredFileDate &&
                    other.LastArchivalDate == LastArchivalDate);
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