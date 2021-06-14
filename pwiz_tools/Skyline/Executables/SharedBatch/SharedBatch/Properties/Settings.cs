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
    }

    public class ConfigList : Collection<IConfig>, IXmlSerializable
    {
        #region IXmlSerializable Members

        public static Importer Importer;

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (Importer == null)
            {
                throw new Exception("Must specify Importer before configurations are loaded.");
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
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var config in this)
            {
                config.WriteXml(writer);
            }
        }

        #endregion // IXmlSerializable Members  
    }
}
