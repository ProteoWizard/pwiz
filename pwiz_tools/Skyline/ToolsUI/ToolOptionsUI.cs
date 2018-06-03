/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ToolOptionsUI : FormEx
    {
        private readonly SettingsListBoxDriver<Server> _driverServers;
        private readonly SettingsListBoxDriver<RemoteAccount> _driverChorusAccounts;
        private readonly SettingsListComboDriver<ColorScheme> _driverColorSchemes;

        public ToolOptionsUI()
        {
            InitializeComponent();
            checkBoxShowWizard.Checked = Settings.Default.ShowStartupForm;
            powerOfTenCheckBox.Checked = Settings.Default.UsePowerOfTen;
            Icon = Resources.Skyline;

            _driverServers = new SettingsListBoxDriver<Server>(listboxServers, Settings.Default.ServerList);
            _driverServers.LoadList();
            _driverChorusAccounts = new SettingsListBoxDriver<RemoteAccount>(listBoxRemoteAccounts, Settings.Default.RemoteAccountList);
            _driverChorusAccounts.LoadList();
            _driverColorSchemes = new SettingsListComboDriver<ColorScheme>(comboColorScheme, Settings.Default.ColorSchemes, true);
            _driverColorSchemes.LoadList(Settings.Default.CurrentColorScheme);

            // Hide ability to turn off live reports
            //tabControl.TabPages.Remove(tabMisc);

            // Populate the languages list with the languages that Skyline has been localized to
            string defaultDisplayName = string.Format(Resources.ToolOptionsUI_ToolOptionsUI_Default___0__,
                CultureUtil.GetDisplayLanguage(CultureInfo.InstalledUICulture).DisplayName);
            listBoxLanguages.Items.Add(new DisplayLanguageItem(string.Empty, defaultDisplayName));
            foreach (var culture in CultureUtil.AvailableDisplayLanguages())
            {
                listBoxLanguages.Items.Add(new DisplayLanguageItem(culture.Name, culture.DisplayName));
            }
            for (int i = 0; i < listBoxLanguages.Items.Count; i++)
            {
                var displayLanguageItem = (DisplayLanguageItem) listBoxLanguages.Items[i];
                if (Equals(displayLanguageItem.Key, Settings.Default.DisplayLanguage))
                {
                    listBoxLanguages.SelectedIndex = i;
                }
            }
            comboCompactFormatOption.Items.AddRange(CompactFormatOption.ALL_VALUES.ToArray());
            comboCompactFormatOption.SelectedItem = CompactFormatOption.FromSettings();
        }

        private void btnEditServers_Click(object sender, EventArgs e)
        {
            EditServers();
        }

        public void EditServers()
        {
            _driverServers.EditList();
        }

        private void btnEditChorusAccountList_Click(object sender, EventArgs e)
        {
            EditChorusAccounts();
        }

        public void EditChorusAccounts()
        {
            _driverChorusAccounts.EditList();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                var displayLanguageItem = listBoxLanguages.SelectedItem as DisplayLanguageItem;
                if (null != displayLanguageItem)
                {
                    Settings.Default.ShowStartupForm = checkBoxShowWizard.Checked;
                    Settings.Default.DisplayLanguage = displayLanguageItem.Key;
                    Settings.Default.UsePowerOfTen = powerOfTenCheckBox.Checked;
                    Program.MainWindow.UpdateGraphPanes();
                }
                CompactFormatOption compactFormatOption = comboCompactFormatOption.SelectedItem as CompactFormatOption;
                if (null != compactFormatOption)
                {
                    Settings.Default.CompactFormatOption = compactFormatOption.Name;
                }
                Settings.Default.CurrentColorScheme = comboColorScheme.SelectedItem as string;
            }
            base.OnClosed(e);
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private class DisplayLanguageItem
        {
            public DisplayLanguageItem(string key, string displayName)
            {
                Key = key;
                DisplayName = displayName;
            }
            public string DisplayName { get; private set; }
            public string Key { get; private set; }
            public override string ToString()
            {
                return DisplayName;
            }
        }

        // ReSharper disable InconsistentNaming
        public enum TABS { Panorama, Chorus, Language, Miscellaneous, Display }
        // ReSharper restore InconsistentNaming

        public class PanoramaTab : IFormView { }
        public class ChorusTab : IFormView { }
        public class LanguageTab : IFormView { }
        public class MiscellaneousTab : IFormView { }
        public class DisplayTab : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new PanoramaTab(), new ChorusTab(), new LanguageTab(), new MiscellaneousTab(), new DisplayTab(),
        };

        #region Functional testing support

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = tabControl.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }

        public TABS SelectedTab
        {
            get { return (TABS)tabControl.SelectedIndex; }
            set { tabControl.SelectedIndex = (int)value; }
        }

        public bool PowerOfTenCheckBox
        {
            get { return powerOfTenCheckBox.Checked; }
            set { powerOfTenCheckBox.Checked = value; }
        }

        #endregion

        private void comboColorScheme_SelectedIndexChanged(object sender, EventArgs e)
        {
            ColorScheme newColorScheme = _driverColorSchemes.SelectedItem;
            if (newColorScheme != null)
            {
                Settings.Default.CurrentColorScheme = newColorScheme.Name;
                Program.MainWindow.ChangeColorScheme();
            }
            _driverColorSchemes.SelectedIndexChangedEvent(sender, e);
        }

        public ComboBox getColorCB()
        {
            return comboColorScheme;
        }

        public SettingsListComboDriver<ColorScheme> getColorDrive()
        {
            return _driverColorSchemes;
        }

        private void btnResetSettings_Click(object sender, EventArgs e)
        {
            ResetAllSettings();
        }

        public void ResetAllSettings()
        {
            if (MultiButtonMsgDlg.Show(this,
                    string.Format(
                        Resources
                            .ToolOptionsUI_btnResetSettings_Click_Are_you_sure_you_want_to_clear_all_saved_settings__This_will_immediately_return__0__to_its_original_configuration_and_cannot_be_undone_,
                        Program.Name), MultiButtonMsgDlg.BUTTON_OK) == DialogResult.OK)
            {
                Settings.Default.Reset();
            }
        }
    }
}
