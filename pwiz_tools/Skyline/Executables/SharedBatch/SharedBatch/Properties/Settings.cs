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

        public string OpenedVersion { get; private set; }

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

        public new void Upgrade()
        {
            var oldVersion = Default.ProgramVersion;
            base.Upgrade();
            Default.OpenedVersion = oldVersion ?? string.Empty;
        }
    }

    public class ConfigList : Collection<IConfig>, IXmlSerializable
    {
        #region IXmlSerializable Members

        public static Importer Importer;

        public static XmlUpdater GetUpdatedXml;

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (Importer == null || GetUpdatedXml == null)
            {
                throw new Exception("Must specify Importer and XML Updater before configurations are loaded.");
            }

            bool updateRequired = Settings.Default.OpenedVersion != null &&
                                  !Equals(Settings.Default.OpenedVersion, Settings.Default.ProgramVersion);
            if (updateRequired)
            {
                reader.Dispose();
                var stream = new FileStream(GetUpdatedXml(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath), FileMode.Open);
                reader = XmlReader.Create(stream);
                while (!Equals(reader.Name, "config_list") && !reader.EOF)
                    reader.Read();
            }

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
                try
                {
                    list.Add(Importer(reader));
                }
                catch (ArgumentException e)
                {
                    message.Append(e.Message + Environment.NewLine);
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

            if (updateRequired)
            {
                Settings.Default.ConfigList = this;
                Settings.Default.Save();
            }

        }

        enum Attr
        {
            version
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString(Attr.version, Settings.Default.ProgramVersion);
            foreach (var config in this)
            {
                config.WriteXml(writer);
            }
        }

        #endregion // IXmlSerializable Members  
    }
}
