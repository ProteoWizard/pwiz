/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// The Search Settings page of the Osprey Search wizard and of Build Library's fine-tune:
    /// the MS/MS resolution and fragment tolerance Osprey scores with, the FDR thresholds and the
    /// thread count, and behind Additional Settings... every other setting the installed Osprey
    /// declares (<see cref="OspreyAdditionalSettings"/>), in a grid, for the scopes of the run the
    /// host is configuring (<see cref="GetScopes"/>). The page's values save as a named preset
    /// (<see cref="OspreySettingsPreset"/>) and a preset chosen from the combo sets them again.
    /// </summary>
    public partial class OspreySearchSettingsControl : UserControl
    {
        private OspreyAdditionalSettings _additionalSettings;
        private IList<string> _refusedLastUsed;
        private readonly SettingsListComboDriver<OspreySettingsPreset> _settingsPresetDriver;
        private bool _suppressPresetApply;

        // Osprey's own defaults for each resolution: 10 ppm for high-resolution MS/MS, 0.5 m/z for
        // unit resolution.
        public const double DEFAULT_HRAM_FRAGMENT_TOLERANCE_PPM = 10;
        public const double DEFAULT_UNIT_FRAGMENT_TOLERANCE_MZ = 0.5;
        public const double DEFAULT_FDR = 0.01;
        public const int MAX_THREAD_COUNT = 128;

        public OspreySearchSettingsControl()
        {
            InitializeComponent();

            comboFragmentUnit.Items.AddRange(new object[]
            {
                PeptideSearchResources.OspreySearchDlg_FragmentUnit_ppm,
                PeptideSearchResources.OspreySearchDlg_FragmentUnit_m_z
            });
            comboResolution.Items.AddRange(new object[]
            {
                PeptideSearchResources.OspreySearchDlg_Resolution_High_resolution__Orbitrap__TOF_,
                PeptideSearchResources.OspreySearchDlg_Resolution_Unit_resolution__Stellar_
            });
            IsHighResolution = true; // Sets the tolerance and its unit to the high-resolution defaults
            numThreads.Maximum = MAX_THREAD_COUNT;
            numThreads.Value = Math.Min(Environment.ProcessorCount, MAX_THREAD_COUNT);
            // Set from a number, not resx text, so the decimal separator is the current culture's
            RunFdr = ExperimentFdr = ProteinFdr = DEFAULT_FDR;

            // The presets are listed with none chosen: the page starts at its defaults, not at a preset
            _settingsPresetDriver = new SettingsListComboDriver<OspreySettingsPreset>(comboSettingsPreset, Settings.Default.OspreySettingsPresets);
            _suppressPresetApply = true;
            try
            {
                _settingsPresetDriver.LoadList(null);
                comboSettingsPreset.SelectedIndex = -1;
            }
            finally
            {
                _suppressPresetApply = false;
            }
        }

        private Form WizardForm => FormEx.GetParentForm(this);

        /// <summary>
        /// The scopes of the run the host is configuring (<see cref="OspreyAdditionalSettings.ScopesFor"/>),
        /// asked when the grid opens, since the pages before this one decide them; every scope until the
        /// host says.
        /// </summary>
        public Func<ICollection<OspreyPropertyScope>> GetScopes { get; set; } = () => OspreyAdditionalSettings.ALL_SCOPES;

        /// <summary>
        /// The settings beyond the pages', as the installed Osprey declares them, with the values the
        /// user last ran with: read from Osprey when first needed, which throws when Osprey cannot be
        /// asked (<see cref="OspreyProperties.Load"/>); <see cref="TryGetAdditionalSettings"/> reports
        /// that instead. Null until then, so <see cref="ApplyTo"/> before <see cref="ValidatePage"/>
        /// carries none.
        /// </summary>
        public OspreyAdditionalSettings AdditionalSettings
        {
            get
            {
                if (_additionalSettings == null)
                {
                    var settings = new OspreyAdditionalSettings(OspreyProperties.Load(OspreyHelpers.OspreyBinary, LocalizationHelper.CurrentUICulture));
                    // A last-used value this Osprey refuses (a page's argument, a value outside its
                    // range now) is reported once, from the UI thread (TryGetAdditionalSettings)
                    _refusedLastUsed = settings.ApplyLastUsed();
                    _additionalSettings = settings;
                }
                return _additionalSettings;
            }
        }

        /// <summary>Loads <see cref="AdditionalSettings"/>, reporting an Osprey that cannot be asked for them.</summary>
        public bool TryGetAdditionalSettings(out OspreyAdditionalSettings additionalSettings)
        {
            try
            {
                additionalSettings = AdditionalSettings;
                if (_refusedLastUsed != null && _refusedLastUsed.Count > 0)
                {
                    MessageDlg.Show(WizardForm, string.Format(
                        PeptideSearchResources.OspreySearchSettingsControl_AdditionalSettings_The_additional_settings__0__are_set_on_the_pages_of_the_wizard_and_were_ignored_,
                        TextUtil.LineSeparate(_refusedLastUsed)));
                    _refusedLastUsed = null;
                }
                return true;
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(WizardForm, x.Message, x);
                additionalSettings = null;
                return false;
            }
        }

        /// <summary>
        /// Additional Settings...: the grid of the settings the pages do not show, for the run's
        /// scopes, in help order under their help sections' titles, each under its name with its
        /// usage text as the tooltip; a value the argument does not take is refused with the reason.
        /// </summary>
        public void ShowAdditionalSettings()
        {
            if (!TryGetAdditionalSettings(out var settings))
                return;
            KeyValueGridDlg.Show(WizardForm, PeptideSearchResources.SearchSettingsControl_Additional_Settings,
                settings.GetGridValues(GetScopes()),
                setting => setting.Value.ToString(),
                (value, setting) => settings.SetValue(setting, value),
                settings.Validate,
                setting => setting.ValidValues,
                settings.GetTooltip,
                settings.GetGroup);
        }

        private void btnAdditionalSettings_Click(object sender, EventArgs e)
        {
            ShowAdditionalSettings();
        }

        /// <summary>The user's presets of this page (<see cref="Settings.OspreySettingsPresets"/>), the ones the combo lists.</summary>
        public OspreySettingsPresetList SettingsPresets => Settings.Default.OspreySettingsPresets;

        /// <summary>
        /// The name of the preset chosen in the combo, or null when none is. Setting it chooses that
        /// preset and applies its values, the preset already chosen included, so a page changed since
        /// returns to it.
        /// </summary>
        public string SettingsPresetName
        {
            get => _settingsPresetDriver.SelectedItem?.Name;
            set
            {
                int index = IndexOfPreset(value);
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), $@"No Osprey settings preset is named {value}");
                if (index == comboSettingsPreset.SelectedIndex)
                    ApplyPreset(SettingsPresets[index]);
                else
                    comboSettingsPreset.SelectedIndex = index;
            }
        }

        /// <summary>
        /// Save...: keeps the page's values as a preset under a name the user gives, in place of one
        /// of that name with the user's consent.
        /// </summary>
        public void ShowSavePresetDlg()
        {
            string name;
            using (var dlg = new PresetNameDlg(SettingsPresetName ?? string.Empty))
            {
                if (dlg.ShowDialog(WizardForm) != DialogResult.OK)
                    return;
                name = dlg.PresetName;
            }
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (SettingsPresets.ContainsKey(name) && MessageDlg.Show(WizardForm,
                    string.Format(PeptideSearchResources.SearchSettingsControl_OverwriteSettingsPreset_A_settings_preset_named__0__already_exists__Do_you_want_to_replace_it_, name),
                    false, MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                return;
            }
            SaveSettingsPreset(name);
        }

        /// <summary>
        /// Keeps the page's values as the preset <paramref name="name"/>, in place of one of that
        /// name, chooses it, and writes the user's settings, so the preset survives however the
        /// session ends.
        /// </summary>
        public void SaveSettingsPreset(string name)
        {
            var presets = SettingsPresets;
            presets.RemoveKey(name);
            presets.Add(BuildPreset(name));
            if (!Program.NoSaveSettings)
                Settings.Default.SaveWithoutExceptions();
            // The preset just saved is the page's values, so choosing it changes nothing
            _suppressPresetApply = true;
            try
            {
                _settingsPresetDriver.LoadList(name);
            }
            finally
            {
                _suppressPresetApply = false;
            }
        }

        /// <summary>
        /// The page's values as a preset: its controls but the thread count, which is the machine's,
        /// and the additional settings that are not Osprey's defaults, read from Osprey if the grid
        /// was never opened, so the values last run with are kept too.
        /// </summary>
        public OspreySettingsPreset BuildPreset(string name)
        {
            string additionalSettingsXml = TryGetAdditionalSettings(out var settings) ? settings.ToXml() : null;
            return new OspreySettingsPreset(name, IsHighResolution, FragmentTolerance, IsFragmentTolerancePpm,
                RunFdr, ExperimentFdr, ProteinFdr, additionalSettingsXml);
        }

        /// <summary>
        /// Sets the page to a preset's values: its controls, and the additional settings to Osprey's
        /// defaults and then to the preset's. A setting the preset names that this Osprey refuses is
        /// left at its default: the preset was saved from this page with an Osprey that took it.
        /// </summary>
        public void ApplyPreset(OspreySettingsPreset preset)
        {
            IsHighResolution = preset.IsHighResolution; // Resets the tolerance to the resolution's default when it changes
            FragmentTolerance = preset.FragmentTolerance;
            IsFragmentTolerancePpm = preset.IsFragmentTolerancePpm;
            RunFdr = preset.RunFdr;
            ExperimentFdr = preset.ExperimentFdr;
            ProteinFdr = preset.ProteinFdr;
            if (TryGetAdditionalSettings(out var settings))
            {
                settings.ResetToDefaults();
                settings.ApplyXml(preset.AdditionalSettingsXml);
            }
        }

        private void comboSettingsPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            // The driver's own reload of the list (after Edit list...) changes the selection again
            // from inside this event: the outer call sees the result
            if (_settingsPresetDriver.IsInSelectedIndexChangedEvent)
                return;
            int indexBefore = _settingsPresetDriver.SelectedIndexLast;
            if (_settingsPresetDriver.SelectedIndexChangedEvent(sender, e))
            {
                // Edit list...: the driver chose the preset chosen before, or the first when none was
                if (indexBefore < 0)
                    comboSettingsPreset.SelectedIndex = -1;
                return;
            }
            var preset = _settingsPresetDriver.SelectedItem;
            if (preset != null && !_suppressPresetApply)
                ApplyPreset(preset);
        }

        private void btnSavePreset_Click(object sender, EventArgs e)
        {
            ShowSavePresetDlg();
        }

        private int IndexOfPreset(string name)
        {
            var presets = SettingsPresets;
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i].Name == name)
                    return i;
            }
            return -1;
        }

        public bool IsHighResolution
        {
            get => comboResolution.SelectedIndex == 0;
            set => comboResolution.SelectedIndex = value ? 0 : 1;
        }

        /// <summary>Choosing a resolution resets the fragment tolerance to Osprey's default for it.</summary>
        private void comboResolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (IsHighResolution)
            {
                FragmentTolerance = DEFAULT_HRAM_FRAGMENT_TOLERANCE_PPM;
                IsFragmentTolerancePpm = true;
            }
            else
            {
                FragmentTolerance = DEFAULT_UNIT_FRAGMENT_TOLERANCE_MZ;
                IsFragmentTolerancePpm = false;
            }
        }

        public double FragmentTolerance
        {
            get => (double)numFragmentTolerance.Value;
            set => numFragmentTolerance.Value = (decimal)value;
        }

        public bool IsFragmentTolerancePpm
        {
            get => comboFragmentUnit.SelectedIndex == 0;
            set => comboFragmentUnit.SelectedIndex = value ? 0 : 1;
        }

        public double RunFdr
        {
            get => double.TryParse(txtRunFdr.Text, out var v) ? v : DEFAULT_FDR;
            set => txtRunFdr.Text = value.ToString(CultureInfo.CurrentCulture);
        }

        public double ExperimentFdr
        {
            get => double.TryParse(txtExperimentFdr.Text, out var v) ? v : DEFAULT_FDR;
            set => txtExperimentFdr.Text = value.ToString(CultureInfo.CurrentCulture);
        }

        public double ProteinFdr
        {
            get => double.TryParse(txtProteinFdr.Text, out var v) ? v : DEFAULT_FDR;
            set => txtProteinFdr.Text = value.ToString(CultureInfo.CurrentCulture);
        }

        public int Threads
        {
            get => (int)numThreads.Value;
            set => numThreads.Value = Math.Min(value, MAX_THREAD_COUNT);
        }

        /// <summary>
        /// The page's settings, as Osprey's command line takes them: the one mapping both hosts use.
        /// The additional settings go with them once they have been read (<see cref="ValidatePage"/>).
        /// </summary>
        public void ApplyTo(OspreyConfig config)
        {
            config.Resolution = IsHighResolution ? OspreyHelpers.RESOLUTION_HRAM : OspreyHelpers.RESOLUTION_UNIT;
            config.FragmentTolerance = FragmentTolerance;
            config.FragmentUnit = IsFragmentTolerancePpm ? OspreyHelpers.FRAGMENT_UNIT_PPM : OspreyHelpers.FRAGMENT_UNIT_MZ;
            config.RunFdr = RunFdr;
            config.ExperimentFdr = ExperimentFdr;
            config.ProteinFdr = ProteinFdr;
            config.Threads = Threads;
            config.AdditionalSettings = _additionalSettings;
        }

        /// <summary>
        /// Next from the page, which runs: each threshold is a fraction in (0, 1]. A threshold of 0
        /// is a search that reports nothing, so the range excludes it. The additional settings are
        /// read from Osprey here if the grid was not opened, so the values last run with reach the
        /// command line, and the values this run has are kept as the last used. An Osprey that
        /// cannot report its settings (one from before the grid) is reported, and the run goes on
        /// without them, as it did before the grid.
        /// </summary>
        public bool ValidatePage()
        {
            var helper = new MessageBoxHelper(WizardForm);
            if (!helper.ValidateDecimalTextBox(txtRunFdr, 0, 1, out _, false) ||
                !helper.ValidateDecimalTextBox(txtExperimentFdr, 0, 1, out _, false) ||
                !helper.ValidateDecimalTextBox(txtProteinFdr, 0, 1, out _, false))
            {
                return false;
            }
            if (TryGetAdditionalSettings(out var additionalSettings))
                additionalSettings.SaveLastUsed();
            return true;
        }
    }
}
