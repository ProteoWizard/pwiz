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
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// The Search Settings page of the Osprey Search wizard and of Build Library's fine-tune:
    /// the MS/MS resolution and fragment tolerance Osprey scores with, the FDR thresholds and the
    /// thread count, and behind Additional Settings... every other setting the installed Osprey
    /// declares (<see cref="OspreyAdditionalSettings"/>), in a grid, for the scopes of the run the
    /// host is configuring (<see cref="GetScopes"/>).
    /// </summary>
    public partial class OspreySearchSettingsControl : UserControl
    {
        private OspreyAdditionalSettings _additionalSettings;

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
                    var refused = settings.ApplyLastUsed();
                    if (refused.Count > 0)
                    {
                        MessageDlg.Show(WizardForm, string.Format(
                            PeptideSearchResources.OspreySearchSettingsControl_AdditionalSettings_The_additional_settings__0__are_set_on_the_pages_of_the_wizard_and_were_ignored_,
                            TextUtil.LineSeparate(refused)));
                    }
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
        /// scopes, each under its name with its usage text as the tooltip; a value the argument does
        /// not take is refused with the reason.
        /// </summary>
        public void ShowAdditionalSettings()
        {
            if (!TryGetAdditionalSettings(out var settings))
                return;
            KeyValueGridDlg.Show(WizardForm, PeptideSearchResources.SearchSettingsControl_Additional_Settings,
                settings.GetGridValues(GetScopes()),
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                settings.Validate,
                setting => setting.ValidValues,
                settings.GetTooltip);
        }

        private void btnAdditionalSettings_Click(object sender, EventArgs e)
        {
            ShowAdditionalSettings();
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
        /// The additional settings go with them once they have been read (<see cref="ValidatePage"/>),
        /// and are kept as the last used.
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
            _additionalSettings?.SaveLastUsed();
        }

        /// <summary>
        /// Next from the page: each threshold is a fraction in (0, 1]. A threshold of 0 is a search
        /// that reports nothing, so the range excludes it. The additional settings are read from
        /// Osprey here if the grid was not opened, so the values last run with reach the command line.
        /// </summary>
        public bool ValidatePage()
        {
            var helper = new MessageBoxHelper(WizardForm);
            return helper.ValidateDecimalTextBox(txtRunFdr, 0, 1, out _, false) &&
                   helper.ValidateDecimalTextBox(txtExperimentFdr, 0, 1, out _, false) &&
                   helper.ValidateDecimalTextBox(txtProteinFdr, 0, 1, out _, false) &&
                   TryGetAdditionalSettings(out _);
        }
    }
}
