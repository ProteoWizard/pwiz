using System.Collections.Generic;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.ui.Properties {
    
    
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    internal sealed partial class Settings {
        
        public Settings() {
            // // To add event handlers for saving and changing settings, uncomment the lines below:
            //
            // this.SettingChanging += this.SettingChangingEventHandler;
            //
            // this.SettingsSaving += this.SettingsSavingEventHandler;
            //
        }
        
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
        public List<ViewSpecList> ViewSpecLists
        {
            get
            {
                if (this["ViewSpecLists"] == null)
                {
                    ViewSpecLists = new List<ViewSpecList>();
                }
                return (List<ViewSpecList>)this["ViewSpecLists"];
            }
            set
            {
                this["ViewSpecLists"] = value;
            }
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
    }
}
