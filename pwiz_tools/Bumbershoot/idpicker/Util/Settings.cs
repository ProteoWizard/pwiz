//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2018 Matt Chambers
//

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace IDPicker.Properties {
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    public sealed partial class Settings {
        public override void Upgrade()
        {
            base.Upgrade();

            var previousQonverterSettings = GetPreviousVersion("QonverterSettings") as StringCollection;
            if (previousQonverterSettings != null)
            {
                var userQonverterSettings = new HashSet<string>(previousQonverterSettings.OfType<string>().ToList());
                var newDefaultQonverterSettings = new HashSet<string>(DefaultQonverterSettings.OfType<string>().ToList());

                var userQonverterSettingsByName = userQonverterSettings.ToDictionary(o => o.Split(';').First());
                var newDefaultQonverterSettingsByName = newDefaultQonverterSettings.ToDictionary(o => o.Split(';').First());

                foreach (var nameSettingsPair in newDefaultQonverterSettingsByName)
                {
                    if (!userQonverterSettingsByName.ContainsKey(nameSettingsPair.Key))
                    {
                        QonverterSettings.Add(nameSettingsPair.Value);
                    }
                }
            }
        }
    }
}
