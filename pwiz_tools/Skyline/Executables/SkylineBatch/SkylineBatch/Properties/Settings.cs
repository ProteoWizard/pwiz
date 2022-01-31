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

 
using System.Collections.Generic;
using System.Configuration;

namespace SkylineBatch.Properties
{
    public sealed partial class Settings
    {

        
        
        [UserScopedSetting]
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

        public new void Reset()
        {
            base.Reset();
            SharedBatch.Properties.Settings.Default.Reset();
        }

        public new void Save()
        {
            base.Save();
            SharedBatch.Properties.Settings.Default.Save();
        }

        public new void Upgrade()
        {
            base.Upgrade();
            SharedBatch.Properties.Settings.Default.Upgrade();
        }

        public void UpdateIfNecessary()
        {
            SharedBatch.Properties.ConfigList.XmlVersion = Default.XmlVersion;
            SharedBatch.Properties.ConfigList.Importer = SkylineBatchConfig.ReadXml;
            Save();
        }

    }

}
