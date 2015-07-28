/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.DataBinding;

namespace pwiz.Topograph.ui.Properties {
    
    
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    internal sealed partial class Settings {
        
        // ReSharper disable EmptyConstructor
        public Settings() {
            // // To add event handlers for saving and changing settings, uncomment the lines below:
            //
            // this.SettingChanging += this.SettingChangingEventHandler;
            //
            // this.SettingsSaving += this.SettingsSavingEventHandler;
            //
        }
        // ReSharper restore EmptyConstructor
        
        private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e) {
            // Add code to handle the SettingChangingEvent event here.
        }
        
        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e) {
            // Add code to handle the SettingsSaving event here.
        }

        [System.Configuration.UserScopedSettingAttribute]
        public List<string> MruList
        {
            get
            {
                if (this["MruList"] == null)
                    MruList = new List<string>();
                return (List<string>)(this["MruList"]);
            }
            set
            {
                this["MruList"] = value;
            }
        }
        [System.Configuration.UserScopedSettingAttribute]
        public ViewSpecList ViewSpecList
        {
            get
            {
                if (this["ViewSpecList"] == null)
                {
                    ViewSpecList = ViewSpecList.EMPTY;
                }
                return (ViewSpecList)this["ViewSpecList"];
            }
            set
            {
                this["ViewSpecList"] = value;
            }
        }

        [System.Configuration.UserScopedSettingAttribute]
        public TopographViewGroups TopographViewGroups
        {
            get
            {
                if (this["TopographViewGroups"] == null)
                {
                    TopographViewGroups = new TopographViewGroups(ViewSpecList);
                }
                return (TopographViewGroups) this["TopographViewGroups"];
            }
            set { this["TopographViewGroups"] = value; }
        }
        [System.Configuration.UserScopedSettingAttribute]
        public HalfLifeSettings HalfLifeSettings
        {
            get
            {
                if (this["HalfLifeSettings"] == null)
                {
                    HalfLifeSettings = HalfLifeSettings.Default;
                }
                return (HalfLifeSettings)this["HalfLifeSettings"];
            }
            set { this["HalfLifeSettings"] = value; }
        }
        [System.Configuration.UserScopedSettingAttribute]
        public ConnectionSettings ConnectionSettings
        {
            get
            {
                if (this["ConnectionSettings"] == null)
                {
                    ConnectionSettings = new ConnectionSettings();
                }
                return (ConnectionSettings) this["ConnectionSettings"];
            }
            set { this["ConnectionSettings"] = value; }
        }
        [System.Configuration.UserScopedSettingAttribute] 
        public bool ShowStatusBar
        {
            get
            {
                if (this["ShowStatusBar"] == null)
                {
                    ShowStatusBar = true;
                }
                return (bool) this["ShowStatusBar"];
            }
            set { this["ShowStatusBar"] = value; }
        }
    }
}
