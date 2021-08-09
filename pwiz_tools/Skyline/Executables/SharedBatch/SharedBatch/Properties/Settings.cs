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
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SharedBatch.Properties
{
    public sealed partial class Settings
    {
        
        [UserScopedSetting]
        public ConfigList ConfigList
        {
            get
            {
                var list = (ConfigList) this["ConfigList"]; // Not L10N
                if (list == null)
                {
                    list = new ConfigList();
                    ConfigList = list;
                }
                return list;
            }
            set => this["ConfigList"] = value; // Not L10N
        }

        public void SetConfigList(List<IConfig> configs)
        {
            var settingsConfigList = new ConfigList();
            foreach (var config in configs)
                settingsConfigList.Add(config);
            Default.ConfigList = settingsConfigList;
        }

        [UserScopedSetting]
        public string InstallationId
        {
            get
            {
                var cid = (string)this["InstallationId"];
                if (cid == null)
                {
                    cid = Guid.NewGuid().ToString();
                    InstallationId = cid;
                    Save();
                }
                return cid;
            }
            private set => this["InstallationId"] = value;
        }

        [ApplicationScopedSetting]
        public Dictionary<string,string> RVersions
        {
            get
            {
                var dict = (Dictionary<string, string>)this["RVersions"]; // Not L10N
                if (dict == null)
                {
                    dict = new Dictionary<string,string>();
                    RVersions = dict;
                }
                return dict;
            }
            set => this["RVersions"] = value; // Not L10N
        }

        // Upgrades the settings from the previous version and rewrites the XML to the current version
        public void Update(string oldXmlPath, string newVersion, string appName, XmlUpdater updater)
        {
            Upgrade();
            var newXmlPath = string.Empty;
            try
            {
                newXmlPath = updater(oldXmlPath, newVersion);
                if (newXmlPath == null)
                    return;
                var configList = new ConfigList();
                using (var stream = new FileStream(newXmlPath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsStartElement("config_list")) continue;
                            configList.ReadXml(reader);
                            break;
                        }
                    }
                }
                Default.ConfigList = configList;
                Default.Save();
            }
            catch (Exception e)
            {
                ProgramLog.Error(e.Message, e);
                var folderToCopy = Path.GetDirectoryName(ProgramLog.GetProgramLogFilePath()) ?? string.Empty;
                var newFileName = Path.Combine(folderToCopy, "error-user.config");
                var message = string.Format(
                    Resources
                        .Program_Main_There_was_an_error_reading_the_saved_configurations_from_an_earlier_version_of__0___,
                    appName);
                File.Copy(oldXmlPath, newFileName, true);
                File.Delete(oldXmlPath);
                message += Environment.NewLine + Environment.NewLine +
                           string.Format(
                               Resources
                                   .Program_Main_To_help_improve__0__in_future_versions__please_post_the_configuration_file_to_the_Skyline_Support_board_,
                               appName) +
                           Environment.NewLine +
                           newFileName;

                MessageBox.Show(message);
            }
            finally
            {
                if (newXmlPath != null && File.Exists(newXmlPath))
                    File.Delete(newXmlPath);
            }
        }
    }

    public class ConfigList : Collection<IConfig>, IXmlSerializable
    {
        #region IXmlSerializable Members

        public static Importer Importer;

        public static string Version;
        public const string DUMMY_VER = "0.0.0.0";

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            var isEmpty = reader.IsEmptyElement;

            // Read past the property element
            reader.Read();
            
            // For an empty list in Settings.Default
            if (isEmpty)
            {
                return;
            }

            // Read list items
            var list = new List<IConfig>();
            var message = new StringBuilder();
            while (reader.IsStartElement())
            {
                if (Importer != null)
                {
                    try
                    {
                        list.Add(Importer(reader));
                    }
                    catch (ArgumentException e)
                    {
                        message.Append(e.Message + Environment.NewLine);
                    }
                }
                else
                {
                    // this should never happen
                    throw new Exception("Must specify Importer before configurations are loaded.");
                }

                reader.Read();
            }

            Clear();
            foreach (var config in list)
            {
                Add(config);
            }

            if (message.Length > 0)
                MessageBox.Show(message.ToString(), Resources.ConfigList_ReadXml_Load_Configurations_Error, MessageBoxButtons.OK);

        }

        enum Attr
        {
            version
        }

        public void WriteXml(XmlWriter writer)
        {
            // if (string.IsNullOrEmpty(Version))
            // {
            //     // this should never happen
            //     throw new Exception("Must specify version before configurations are saved.");
            // }
            // The version should never be blank but if it is write a dummy version "0.0.0.0". An exception thrown here while
            // running the program will not be caught, and any existing configurations will not be written to user.config. 
            // As a result, the user will not see any saved configurations next time they start the application.
            writer.WriteAttributeString(Attr.version, string.IsNullOrEmpty(Version) ? DUMMY_VER : Version);
            foreach (var config in this)
            {
                config.WriteXml(writer);
            }
        }

        #endregion // IXmlSerializable Members  
    }
}
