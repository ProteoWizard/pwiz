/*
 * Original author: Viktoria Dorfer <viktoria.dorfer .at. fh-hagenberg.at>,
 *                  Bioinformatics Research Group, University of Applied Sciences Upper Austria
 *
 * Copyright 2020 University of Applied Sciences Upper Austria
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ConverterSettingsControl : UserControl
    {
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private FullScanSettingsControl _fullScanSettingsControl;
        private IDictionary<string, AbstractDdaSearchEngine.Setting> _diaUmpireAdditionalSettings;

        public ConverterSettingsControl(IModifyDocumentContainer documentContainer, ImportPeptideSearch importPeptideSearch, FullScanSettingsControl fullScanSettingsControl)
        {
            InitializeComponent();
            ImportPeptideSearch = importPeptideSearch;
            _fullScanSettingsControl = fullScanSettingsControl;
            converterTabControl.SelectedTab = null;

            LoadComboboxEntries();
        }

        public enum Protocol
        {
            none,
            msconvert,
            dia_umpire
        }

        public Protocol CurrentProtocol
        {
            get
            {
                if (converterTabControl.SelectedTab == diaUmpireTabPage)
                    return Protocol.dia_umpire;
                else if (converterTabControl.SelectedTab == msconvertTabPage)
                    return Protocol.msconvert;
                return Protocol.none;
            }

            set
            {
                InitializeProtocol(value);
            }
        }

        public IDictionary<string, AbstractDdaSearchEngine.Setting> AdditionalSettings
        {
            get
            {
                if (converterTabControl.SelectedTab == diaUmpireTabPage)
                    return _diaUmpireAdditionalSettings;
                //else if (converterTabControl.SelectedTab == msconvertTabPage)
                //  return _msconvertAdditionalSettings;
                return null;
            }

            set
            {
                if (converterTabControl.SelectedTab == diaUmpireTabPage)
                    _diaUmpireAdditionalSettings = value;
                //else if (converterTabControl.SelectedTab == msconvertTabPage)
                //  _msconvertAdditionalSettings = value;
            }
        }

        public void InitializeProtocol(Protocol protocol)
        {
            converterTabControl.TabPages.Clear();

            switch (protocol)
            {
                case Protocol.none:
                    break;

                case Protocol.msconvert:
                    converterTabControl.TabPages.Add(msconvertTabPage);
                    break;

                case Protocol.dia_umpire:
                    converterTabControl.TabPages.Add(diaUmpireTabPage);
                    break;
            }

            converterTabControl.SelectedIndex = 0;
            cbInstrumentPreset.SelectedIndex = 0;
        }

        public DdaConverterSettings ConverterSettings
        {
            get { return CurrentProtocol != Protocol.none ? new DdaConverterSettings(this) : null; }
        }

        public class DdaConverterSettings
        {
            public static DdaConverterSettings GetDefault()
            {
                return new DdaConverterSettings();
            }

            public DdaConverterSettings()
            {
            }

            public DdaConverterSettings(ConverterSettingsControl control)
            {
                Protocol = control.CurrentProtocol;
                if (Protocol == Protocol.dia_umpire)
                {
                    InstrumentPreset = control.InstrumentPreset;
                    NonDefaultAdditionalSettings = control.AdditionalSettings?.Values;
                }
            }

            [Track]
            public Protocol Protocol { get; }
            [Track]
            public DiaUmpire.Config.InstrumentPreset InstrumentPreset { get; }
            [Track]
            public IEnumerable<AbstractDdaSearchEngine.Setting> NonDefaultAdditionalSettings { get; }
        }

        private void LoadComboboxEntries()
        {
            LoadInstrumentPresetEntries();
        }

        private void LoadInstrumentPresetEntries()
        {
            foreach (var name in Enum.GetNames(typeof(DiaUmpire.Config.InstrumentPreset)))
                cbInstrumentPreset.Items.Add(name);
            ComboHelper.AutoSizeDropDown(cbInstrumentPreset);
        }
    
        private Form WizardForm
        {
            get { return FormEx.GetParentForm(this); }
        }
    
        private bool ValidateCombobox(ComboBox comboBox, out string selectedElement)
        {
            selectedElement = "";
            if (comboBox.SelectedItem == null)
                return false;
            selectedElement = comboBox.SelectedItem.ToString();
            return true;
        }
   
        public bool SaveAllSettings()
        {
            bool valid = ValidateEntries();
            if (!valid)
                return false;

            return true;
        }

        private bool ValidateEntries()
        {
            /*string fragmentIons;
            if (!ValidateCombobox(cbInstrumentPreset, out fragmentIons))
            {
                helper.ShowTextBoxError(cbInstrumentPreset, 
                    Resources.DdaSearch_SearchSettingsControl_Fragment_ions_must_be_selected);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIons(fragmentIons);*/
            return true;
        }

        private void btnDiaUmpireAdditionalSettings_Click(object sender, EventArgs e)
        {
            var defaultDiaUmpireSettings = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            var allDiaUmpireSettings = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            var diaUmpireConfig = DiaUmpire.Config.GetDefaultsForInstrument(InstrumentPreset);
            foreach (var param in diaUmpireConfig.Parameters)
            {
                switch (param.Value)
                {
                    case bool b:
                        defaultDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, b);
                        allDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, b);
                        break;
                    case string s:
                        defaultDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, s);
                        allDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, s);
                        break;
                    case int i32:
                        defaultDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, i32, int.MinValue, int.MaxValue);
                        allDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, i32, int.MinValue, int.MaxValue);
                        break;
                    case float r32:
                        defaultDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, r32, double.MinValue, double.MaxValue);
                        allDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, r32, double.MinValue, double.MaxValue);
                        break;
                    case double r64:
                        defaultDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, r64, double.MinValue, double.MaxValue);
                        allDiaUmpireSettings[param.Key] = new AbstractDdaSearchEngine.Setting(param.Key, r64, double.MinValue, double.MaxValue);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (_diaUmpireAdditionalSettings != null)
                foreach (var param in _diaUmpireAdditionalSettings)
                    allDiaUmpireSettings[param.Key].Value = param.Value.Value;

            // only non-default settings are kept in _diaUmpireAdditionalSettings
            _diaUmpireAdditionalSettings = new Dictionary<string, AbstractDdaSearchEngine.Setting>();

            Func<AbstractDdaSearchEngine.Setting, string> valueToString = (setting) =>
            {
                if (setting.Value is double)
                    return Math.Round((double) setting.Value, 4).ToString(@"F");
                return setting.Value.ToString();
            };

            Action<string, AbstractDdaSearchEngine.Setting> stringToValueIfNonDefault = (value, setting) =>
            {
                if (value.ToString() == valueToString(defaultDiaUmpireSettings[setting.Name]))
                    return;
                _diaUmpireAdditionalSettings[setting.Name] = setting;
                setting.Value = value;
            };

            KeyValueGridDlg.Show(Resources.SearchSettingsControl_Additional_Settings,
                allDiaUmpireSettings, valueToString, stringToValueIfNonDefault,
                (value, setting) => setting.Validate(value));
        }

        public DiaUmpire.Config.InstrumentPreset InstrumentPreset
        {
            get { return (DiaUmpire.Config.InstrumentPreset) cbInstrumentPreset.SelectedIndex; }
            set { cbInstrumentPreset.SelectedIndex = (int) value; }
        }

        public AbstractDdaConverter GetDiaUmpireConverter()
        {
            var diaUmpireConfig = DiaUmpire.Config.GetDefaultsForInstrument(InstrumentPreset);

            if (_diaUmpireAdditionalSettings != null)
                foreach (var kvp in _diaUmpireAdditionalSettings)
                    diaUmpireConfig.Parameters[kvp.Key] = kvp.Value.Value;

            return new DiaUmpireDdaConverter(ImportPeptideSearch.SearchEngine, _fullScanSettingsControl.IsolationScheme, diaUmpireConfig);
        }
    }
}
