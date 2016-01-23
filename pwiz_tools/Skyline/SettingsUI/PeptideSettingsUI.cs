/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class PeptideSettingsUI : FormEx, IMultipleViewProvider
    {
        private const int BORDER_BOTTOM_HEIGHT = 16;

// ReSharper disable InconsistentNaming
        public enum TABS { Digest, Prediction, Filter, Library, Modifications, /* Integration, */ Quantification }
// ReSharper restore InconsistentNaming

        public class DigestionTab : IFormView { }
        public class PredictionTab : IFormView { }
        public class FilterTab : IFormView { }
        public class LibraryTab : IFormView { }
        public class ModificationsTab : IFormView { }
//        public class IntegrationTab : IFormView { }    - not yet visible ever
        public class QuantificationTab : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new DigestionTab(), new PredictionTab(), new FilterTab(), new LibraryTab(), new ModificationsTab(), /* new IntegrationTab(), */ new QuantificationTab(), 
        };

        private readonly SkylineWindow _parent;
        private readonly LibraryManager _libraryManager;
        private PeptideSettings _peptideSettings;
        private IEnumerable<LibrarySpec> _eventChosenLibraries;
        private PeptideRankId _lastRankId;
        private string _lastPeptideCount;

        private readonly SettingsListComboDriver<Enzyme> _driverEnzyme;
        private readonly SettingsListComboDriver<RetentionTimeRegression> _driverRT;
        private readonly SettingsListComboDriver<DriftTimePredictor> _driverDT;
        private readonly SettingsListBoxDriver<PeptideExcludeRegex> _driverExclusion;
        private readonly SettingsListBoxDriver<LibrarySpec> _driverLibrary;
        private readonly SettingsListComboDriver<BackgroundProteomeSpec> _driverBackgroundProteome;
        private readonly SettingsListBoxDriver<StaticMod> _driverStaticMod;
        private readonly SettingsListBoxDriver<StaticMod> _driverHeavyMod;
        private readonly SettingsListComboDriver<PeakScoringModelSpec> _driverPeakScoringModel;
        private readonly LabelTypeComboDriver _driverLabelType;
        private static readonly IList<int?> _quantMsLevels = ImmutableList.ValueOf(new int?[] {null, 1, 2});

        public PeptideSettingsUI(SkylineWindow parent, LibraryManager libraryManager)
        {
            InitializeComponent();

            btnUpdateIonMobilityLibraries.Visible = false; // TODO: ion mobility libraries are more complex than initially thought - put this off until after summer 2014 release

            _parent = parent;
            _libraryManager = libraryManager;
            _peptideSettings = parent.DocumentUI.Settings.PeptideSettings;

            // Initialize digestion settings
            _driverEnzyme = new SettingsListComboDriver<Enzyme>(comboEnzyme, Settings.Default.EnzymeList);
            _driverEnzyme.LoadList(_peptideSettings.Enzyme.GetKey());
            for (int i = DigestSettings.MIN_MISSED_CLEAVAGES; i <= DigestSettings.MAX_MISSED_CLEAVAGES; i++)
                comboMissedCleavages.Items.Add(i.ToString(CultureInfo.InvariantCulture));
            comboMissedCleavages.SelectedItem = Digest.MaxMissedCleavages.ToString(LocalizationHelper.CurrentCulture);
            if (comboMissedCleavages.SelectedIndex < 0)
                comboMissedCleavages.SelectedIndex = 0;
            cbRaggedEnds.Checked = Digest.ExcludeRaggedEnds;

            // Initialize prediction settings
            _driverRT = new SettingsListComboDriver<RetentionTimeRegression>(comboRetentionTime, Settings.Default.RetentionTimeList);
            string sel = (Prediction.RetentionTime == null ? null : Prediction.RetentionTime.Name);
            _driverRT.LoadList(sel);
            cbUseMeasuredRT.Checked = textMeasureRTWindow.Enabled = Prediction.UseMeasuredRTs;
            if (Prediction.MeasuredRTWindow.HasValue)
                textMeasureRTWindow.Text = Prediction.MeasuredRTWindow.Value.ToString(LocalizationHelper.CurrentCulture);

            _driverDT = new SettingsListComboDriver<DriftTimePredictor>(comboDriftTimePredictor, Settings.Default.DriftTimePredictorList);
            string selDT = (Prediction.DriftTimePredictor == null ? null : Prediction.DriftTimePredictor.Name);
            _driverDT.LoadList(selDT);
            cbUseSpectralLibraryDriftTimes.Checked = textSpectralLibraryDriftTimesResolvingPower.Enabled = Prediction.UseLibraryDriftTimes;
            if (Prediction.LibraryDriftTimesResolvingPower.HasValue)
                textSpectralLibraryDriftTimesResolvingPower.Text = Prediction.LibraryDriftTimesResolvingPower.Value.ToString(LocalizationHelper.CurrentCulture);

            // Initialize filter settings
            _driverExclusion = new SettingsListBoxDriver<PeptideExcludeRegex>(listboxExclusions, Settings.Default.PeptideExcludeList);
            _driverExclusion.LoadList(null, Filter.Exclusions);

            textExcludeAAs.Text = Filter.ExcludeNTermAAs.ToString(LocalizationHelper.CurrentCulture);
            textMaxLength.Text = Filter.MaxPeptideLength.ToString(LocalizationHelper.CurrentCulture);
            textMinLength.Text = Filter.MinPeptideLength.ToString(LocalizationHelper.CurrentCulture);
            cbAutoSelect.Checked = Filter.AutoSelect;

            // Initialize spectral library settings
            _driverLibrary = new SettingsListBoxDriver<LibrarySpec>(listLibraries, Settings.Default.SpectralLibraryList);
            IList<LibrarySpec> listLibrarySpecs = Libraries.LibrarySpecs;

            _driverLibrary.LoadList(null, listLibrarySpecs);
            _driverBackgroundProteome = new SettingsListComboDriver<BackgroundProteomeSpec>(comboBackgroundProteome, Settings.Default.BackgroundProteomeList);
            _driverBackgroundProteome.LoadList(_peptideSettings.BackgroundProteome.Name);

            panelPick.Visible = listLibrarySpecs.Count > 0;
            btnExplore.Enabled = listLibraries.Items.Count > 0;

            comboMatching.SelectedIndex = (int) Libraries.Pick;

            _lastRankId = Libraries.RankId;
            _lastPeptideCount = Libraries.PeptideCount.HasValue
                                    ? Libraries.PeptideCount.Value.ToString(LocalizationHelper.CurrentCulture)
                                    : null;

            UpdateRanks(null);

            // Initialize modification settings
            _driverStaticMod = new SettingsListBoxDriver<StaticMod>(listStaticMods, Settings.Default.StaticModList);
            _driverStaticMod.LoadList(null, Modifications.StaticModifications);
            _driverHeavyMod = new SettingsListBoxDriver<StaticMod>(listHeavyMods, Settings.Default.HeavyModList);
            _driverLabelType = new LabelTypeComboDriver(comboLabelType, Modifications, _driverHeavyMod, 
                labelStandardType, comboStandardType, listStandardTypes);
            textMaxVariableMods.Text = Modifications.MaxVariableMods.ToString(LocalizationHelper.CurrentCulture);
            textMaxNeutralLosses.Text = Modifications.MaxNeutralLosses.ToString(LocalizationHelper.CurrentCulture);

            // Initialize peak scoring settings.
            _driverPeakScoringModel = new SettingsListComboDriver<PeakScoringModelSpec>(comboPeakScoringModel, Settings.Default.PeakScoringModelList);
            var peakScoringModel = _peptideSettings.Integration.PeakScoringModel;
            _driverPeakScoringModel.LoadList(peakScoringModel != null ? peakScoringModel.Name : null);

            IsShowLibraryExplorer = false;
            tabControl1.TabPages.Remove(tabIntegration);
            comboNormalizationMethod.Items.AddRange(
                NormalizationMethod.ListNormalizationMethods(parent.DocumentUI).ToArray());
            comboNormalizationMethod.SelectedItem = _peptideSettings.Quantification.NormalizationMethod;
            comboWeighting.Items.AddRange(RegressionWeighting.All.Cast<object>().ToArray());
            comboWeighting.SelectedItem = _peptideSettings.Quantification.RegressionWeighting;
            comboRegressionFit.Items.AddRange(RegressionFit.All.Cast<object>().ToArray());
            comboRegressionFit.SelectedItem = _peptideSettings.Quantification.RegressionFit;
            comboQuantMsLevel.SelectedIndex = Math.Max(0, _quantMsLevels.IndexOf(_peptideSettings.Quantification.MsLevel));
            tbxQuantUnits.Text = _peptideSettings.Quantification.Units;
        }

        public DigestSettings Digest { get { return _peptideSettings.DigestSettings; } }
        public PeptidePrediction Prediction { get { return _peptideSettings.Prediction; } }
        public PeptideFilter Filter { get { return _peptideSettings.Filter; } }
        public PeptideLibraries Libraries { get { return _peptideSettings.Libraries; } }
        public PeptideModifications Modifications { get { return _peptideSettings.Modifications; } }
        public PeptideIntegration Integration { get { return _peptideSettings.Integration; } }
        public bool IsShowLibraryExplorer { get; set; }
        public TABS? TabControlSel { get; set; }

        protected override void OnShown(EventArgs e)
        {
            if (TabControlSel != null) 
                tabControl1.SelectedIndex = (int) TabControlSel; 
            tabControl1.FocusFirstTabStop();
        }

        private PeptideSettings ValidateNewSettings(bool showMessages)
        {
            var helper = new MessageBoxHelper(this, showMessages);

            // Validate and hold digestion settings
            Enzyme enzyme = Settings.Default.GetEnzymeByName(comboEnzyme.SelectedItem.ToString());
            Helpers.AssignIfEquals(ref enzyme, _peptideSettings.Enzyme);

            int maxMissedCleavages =
                int.Parse(comboMissedCleavages.SelectedItem.ToString());
            bool excludeRaggedEnds = cbRaggedEnds.Checked;
            DigestSettings digest = new DigestSettings(maxMissedCleavages, excludeRaggedEnds);
            Helpers.AssignIfEquals(ref digest, Digest);

            var backgroundProteomeSpec = _driverBackgroundProteome.SelectedItem;
            BackgroundProteome backgroundProteome = BackgroundProteome.NONE;
            if (!backgroundProteomeSpec.IsNone)
            {
                backgroundProteome = new BackgroundProteome(backgroundProteomeSpec, true);
                if (backgroundProteome.DatabaseInvalid)
                {

                    var message = TextUtil.LineSeparate(string.Format(Resources.PeptideSettingsUI_ValidateNewSettings_Failed_to_load_background_proteome__0__,
                                                                      backgroundProteomeSpec.Name),
                                                        string.Format(Resources.PeptideSettingsUI_ValidateNewSettings_The_file__0__may_not_be_a_valid_proteome_file,
                                                                      backgroundProteomeSpec.DatabasePath));
                    MessageDlg.Show(this, message);
                    tabControl1.SelectedIndex = 0;
                    _driverBackgroundProteome.Combo.Focus();
                    return null;
                }
            }
            Helpers.AssignIfEquals(ref backgroundProteome, _peptideSettings.BackgroundProteome);

            // Validate and hold prediction settings
            string nameRT = comboRetentionTime.SelectedItem.ToString();
            RetentionTimeRegression retentionTime =
                Settings.Default.GetRetentionTimeByName(nameRT);
            if (retentionTime != null && retentionTime.Calculator != null)
            {
                RetentionScoreCalculatorSpec retentionCalc =
                    Settings.Default.GetCalculatorByName(retentionTime.Calculator.Name);
                // Just in case the calculator in use in the current documet got removed,
                // never set the calculator to null.  Just keep using the one we have.
                if (retentionCalc != null && !ReferenceEquals(retentionCalc, retentionTime.Calculator))
                    retentionTime = retentionTime.ChangeCalculator(retentionCalc);
            }
            bool useMeasuredRT = cbUseMeasuredRT.Checked;
            double? measuredRTWindow = null;
            if (!string.IsNullOrEmpty(textMeasureRTWindow.Text))
            {
                double measuredRTWindowOut;
                const double minWindow = PeptidePrediction.MIN_MEASURED_RT_WINDOW;
                const double maxWindow = PeptidePrediction.MAX_MEASURED_RT_WINDOW;
                if (!helper.ValidateDecimalTextBox(tabControl1, (int) TABS.Prediction,
                        textMeasureRTWindow, minWindow, maxWindow, out measuredRTWindowOut))
                    return null;
                measuredRTWindow = measuredRTWindowOut;
            }

            string nameDt = comboDriftTimePredictor.SelectedItem.ToString();
            DriftTimePredictor driftTimePredictor =
                Settings.Default.GetDriftTimePredictorByName(nameDt);
            if (driftTimePredictor != null && driftTimePredictor.IonMobilityLibrary != null)
            {
                IonMobilityLibrarySpec ionMobilityLibrary =
                    Settings.Default.GetIonMobilityLibraryByName(driftTimePredictor.IonMobilityLibrary.Name);
                // Just in case the library in use in the current documet got removed,
                // never set the library to null.  Just keep using the one we have.
                if (ionMobilityLibrary != null && !ReferenceEquals(ionMobilityLibrary, driftTimePredictor.IonMobilityLibrary))
                    driftTimePredictor = driftTimePredictor.ChangeLibrary(ionMobilityLibrary);
            }
            bool useLibraryDriftTime = cbUseSpectralLibraryDriftTimes.Checked;
            double? libraryDTResolvingPower = null;
            if (useLibraryDriftTime || !string.IsNullOrEmpty(textSpectralLibraryDriftTimesResolvingPower.Text))
            {
                double libraryDTWindowOut;
                if (!helper.ValidateDecimalTextBox(tabControl1, (int)TABS.Prediction,
                        textSpectralLibraryDriftTimesResolvingPower, null, null, out libraryDTWindowOut))
                    return null;
                string errmsg = EditDriftTimePredictorDlg.ValidateResolvingPower(libraryDTWindowOut);
                if (errmsg != null)
                {
                    helper.ShowTextBoxError(tabControl1, (int)TABS.Prediction, textSpectralLibraryDriftTimesResolvingPower, errmsg);
                    return null;
                }
                libraryDTResolvingPower = libraryDTWindowOut;
            }

            var prediction = new PeptidePrediction(retentionTime, driftTimePredictor, useMeasuredRT, measuredRTWindow, useLibraryDriftTime, libraryDTResolvingPower);
            Helpers.AssignIfEquals(ref prediction, Prediction);

            // Validate and hold filter settings
            int excludeNTermAAs;
            if (!helper.ValidateNumberTextBox(tabControl1, (int) TABS.Filter, textExcludeAAs,
                    PeptideFilter.MIN_EXCLUDE_NTERM_AA, PeptideFilter.MAX_EXCLUDE_NTERM_AA, out excludeNTermAAs))
                return null;
            int minPeptideLength;
            if (!helper.ValidateNumberTextBox(tabControl1, (int) TABS.Filter, textMinLength,
                    PeptideFilter.MIN_MIN_LENGTH, PeptideFilter.MAX_MIN_LENGTH, out minPeptideLength))
                return null;
            int maxPeptideLength;
            if (!helper.ValidateNumberTextBox(tabControl1, (int)TABS.Filter, textMaxLength,
                    Math.Max(PeptideFilter.MIN_MAX_LENGTH, minPeptideLength), PeptideFilter.MAX_MAX_LENGTH, out maxPeptideLength))
                return null;

            PeptideExcludeRegex[] exclusions = _driverExclusion.Chosen;

            bool autoSelect = cbAutoSelect.Checked;
            PeptideFilter filter;
            try
            {
                filter = new PeptideFilter(excludeNTermAAs,
                                           minPeptideLength,
                                           maxPeptideLength,
                                           exclusions,
                                           autoSelect);
            }
            catch (InvalidDataException x)
            {
                if (showMessages)
                    MessageDlg.ShowException(this, x);
                return null;
            }

            Helpers.AssignIfEquals(ref filter, Filter);

            // Validate and hold libraries
            PeptideLibraries libraries;
            IList<LibrarySpec> librarySpecs = _driverLibrary.Chosen;
            if (librarySpecs.Count == 0)
                libraries = new PeptideLibraries(PeptidePick.library, null, null, false, librarySpecs, new Library[0]);
            else
            {
                int? peptideCount = null;
                if (cbLimitPeptides.Checked)
                {
                    int peptideCountVal;
                    if (!helper.ValidateNumberTextBox(textPeptideCount, PeptideLibraries.MIN_PEPTIDE_COUNT,
                            PeptideLibraries.MAX_PEPTIDE_COUNT, out peptideCountVal))
                        return null;
                    peptideCount = peptideCountVal;
                }
                PeptidePick pick = (PeptidePick) comboMatching.SelectedIndex;

                IList<Library> librariesLoaded = new Library[librarySpecs.Count];
                bool documentLibrary = false;
                if (Libraries != null)
                {
                    // Use existing library spec's, if nothing was changed.
                    // Avoid changing the libraries, just because the the picking
                    // algorithm changed.
                    if (ArrayUtil.EqualsDeep(librarySpecs, Libraries.LibrarySpecs))
                    {
                        librarySpecs = Libraries.LibrarySpecs;
                        librariesLoaded = Libraries.Libraries;
                    }

                    documentLibrary = Libraries.HasDocumentLibrary;
                    // Otherwise, leave the list of loaded libraries empty,
                    // and let the LibraryManager refill it.  This ensures a
                    // clean save of library specs only in the user config, rather
                    // than a mix of library specs and libraries.
                }

                PeptideRankId rankId = (PeptideRankId) comboRank.SelectedItem;
                if (comboRank.SelectedIndex == 0)
                    rankId = null;

                libraries = new PeptideLibraries(pick, rankId, peptideCount, documentLibrary, librarySpecs, librariesLoaded);
            }
            Helpers.AssignIfEquals(ref libraries, Libraries);

            // Validate and hold modifications
            int maxVariableMods;
            if (!helper.ValidateNumberTextBox(tabControl1, (int)TABS.Modifications, textMaxVariableMods,
                    PeptideModifications.MIN_MAX_VARIABLE_MODS, PeptideModifications.MAX_MAX_VARIABLE_MODS, out maxVariableMods))
                return null;
            int maxNeutralLosses;
            if (!helper.ValidateNumberTextBox(tabControl1, (int)TABS.Modifications, textMaxNeutralLosses,
                    PeptideModifications.MIN_MAX_NEUTRAL_LOSSES, PeptideModifications.MAX_MAX_NEUTRAL_LOSSES, out maxNeutralLosses))
                return null;

            var standardTypes = _driverLabelType.InternalStandardTypes;
            PeptideModifications modifications = new PeptideModifications(
                _driverStaticMod.Chosen, maxVariableMods, maxNeutralLosses,
                _driverLabelType.GetHeavyModifications(), standardTypes);
            // Should not be possible to change explicit modifications in the background,
            // so this should be safe.  CONSIDER: Document structure because of a library load?
            modifications = modifications.DeclareExplicitMods(_parent.DocumentUI,
                Settings.Default.StaticModList, Settings.Default.HeavyModList);
            Helpers.AssignIfEquals(ref modifications, _peptideSettings.Modifications);

            PeptideIntegration integration = new PeptideIntegration(_driverPeakScoringModel.SelectedItem);
            Helpers.AssignIfEquals(ref integration, Integration);

            QuantificationSettings quantification = QuantificationSettings.DEFAULT
                .ChangeNormalizationMethod(comboNormalizationMethod.SelectedItem as NormalizationMethod ?? NormalizationMethod.NONE) 
                .ChangeRegressionWeighting(comboWeighting.SelectedItem as RegressionWeighting)
                .ChangeRegressionFit(comboRegressionFit.SelectedItem as RegressionFit)
                .ChangeMsLevel(_quantMsLevels[comboQuantMsLevel.SelectedIndex])
                .ChangeUnits(tbxQuantUnits.Text);

            return new PeptideSettings(enzyme, digest, prediction, filter, libraries, modifications, integration, backgroundProteome)
                    .ChangeAbsoluteQuantification(quantification);
        }

        public void OkDialog()
        {
            PeptideSettings settings = ValidateNewSettings(true);
            if (settings == null)
                return;

            // Only update, if anything changed
            if (!Equals(settings, _peptideSettings))
            {
                if (!_parent.ChangeSettingsMonitored(this, Resources.PeptideSettingsUI_OkDialog_Changing_peptide_settings,
                                                     s => s.ChangePeptideSettings(settings)))
                {
                    return;
                }
                _peptideSettings = settings;
            }
            DialogResult = DialogResult.OK;
        }

        private void enzyme_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverEnzyme.SelectedIndexChangedEvent(sender, e);
        }

        private void comboRetentionTime_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverRT.SelectedIndexChangedEvent(sender, e);
        }

        private void btnUpdateCalculator_Click(object sender, EventArgs e)
        {
            // Enable Update Calculator button based on whether the selected calculator
            // supports editing.
            var regressionRT = _driverRT.SelectedItem;
            editCalculatorCurrentContextMenuItem.Visible = regressionRT != null &&
                Settings.Default.RTScoreCalculatorList.CanEditItem(regressionRT.Calculator);

            contextMenuCalculator.Show(btnUpdateCalculator.Parent,
                btnUpdateCalculator.Left, btnUpdateCalculator.Bottom + 1);
        }

        private void addCalculatorContextMenuItem_Click(object sender, EventArgs e)
        {
            AddCalculator();
        }

        public void AddCalculator()
        {
            CheckDisposed();
            var list = Settings.Default.RTScoreCalculatorList;
            var calcNew = list.EditItem(this, null, list, null);
            if (calcNew != null)
                list.SetValue(calcNew);
        }

        private void editCalculatorCurrentContextMenuItem_Click(object sender, EventArgs e)
        {
            EditCalculator();
        }

        public void EditCalculator()
        {
            var list = Settings.Default.RTScoreCalculatorList;
            var calcNew = list.EditItem(this, _driverRT.SelectedItem.Calculator, list, null);
            if (calcNew != null)
                list.SetValue(calcNew);
        }

        private void editCalculatorListContextMenuItem_Click(object sender, EventArgs e)
        {
            EditCalculatorList();
        }

        public void EditCalculatorList()
        {
            var list = Settings.Default.RTScoreCalculatorList;
            var listNew = list.EditList(this, null);
            if (listNew != null)
            {
                list.Clear();
                list.AddRange(listNew);
            }
        }

        private void comboDriftTime_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverDT.SelectedIndexChangedEvent(sender, e);
        }

        public void AddDriftTimePredictor()
        {
            CheckDisposed();
            _driverDT.AddItem();
        }

        public void EditDriftTimePredictor()
        {
            CheckDisposed();
            _driverDT.EditCurrent();
        }

        private void btnUpdateIonMobilityLibrary_Click(object sender, EventArgs e)
        {
            // Enable Update Ion Mobility Library button based on whether the selected predictor
            // supports editing.
            var driftTimePredictor = _driverDT.SelectedItem;
            editIonMobilityLibraryCurrentContextMenuItem.Visible = driftTimePredictor != null &&
                Settings.Default.IonMobilityLibraryList.CanEditItem(driftTimePredictor.IonMobilityLibrary);

            contextMenuIonMobilityLibraries.Show(btnUpdateIonMobilityLibraries.Parent,
                btnUpdateIonMobilityLibraries.Left, btnUpdateIonMobilityLibraries.Bottom + 1);
        }

        private void addIonMobilityLibraryContextMenuItem_Click(object sender, EventArgs e)
        {
            AddIonMobilityLibrary();
        }

        public void AddIonMobilityLibrary()
        {
            CheckDisposed();
            var list = Settings.Default.IonMobilityLibraryList;
            var libNew = list.EditItem(this, null, list, null);
            if (libNew != null)
                list.SetValue(libNew);
        }

        private void editIonMobilityLibraryCurrentContextMenuItem_Click(object sender, EventArgs e)
        {
            EditIonMobilityLibrary();
        }

        public void EditIonMobilityLibrary()
        {
            var list = Settings.Default.IonMobilityLibraryList;
            var calcNew = list.EditItem(this, _driverDT.SelectedItem.IonMobilityLibrary, list, null);
            if (calcNew != null)
                list.SetValue(calcNew);
        }

        private void editIonMobilityLibraryListContextMenuItem_Click(object sender, EventArgs e)
        {
            EditIonMobilityLibraryList();
        }

        public void EditIonMobilityLibraryList()
        {
            var dtllist = Settings.Default.IonMobilityLibraryList;
            var dtllistNew = dtllist.EditList(this, null);
            if (dtllistNew != null)
            {
                dtllist.Clear();
                dtllist.AddRange(dtllistNew);
            }
        }

        private void comboBackgroundProteome_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverBackgroundProteome.SelectedIndexChangedEvent(sender, e);
        }

        private void cbUseMeasuredRT_CheckedChanged(object sender, EventArgs e)
        {
            bool enable = cbUseMeasuredRT.Checked;
            textMeasureRTWindow.Enabled = enable;
            // If disabling the text box, and it has content, make sure it is
            // valid content.  Otherwise, clear the current content, which
            // is always valid, if the measured RT values will not be used.
            if (!enable && !string.IsNullOrEmpty(textMeasureRTWindow.Text))
            {
                double measuredRTWindow;
                if (!double.TryParse(textMeasureRTWindow.Text, out measuredRTWindow) ||
                        PeptidePrediction.MIN_MEASURED_RT_WINDOW > measuredRTWindow ||
                        measuredRTWindow > PeptidePrediction.MAX_MEASURED_RT_WINDOW)
                {
                    textMeasureRTWindow.Text = string.Empty;
                }
            }
        }

        private void cbUseSpectralLibraryDriftTimes_CheckChanged(object sender, EventArgs e)
        {
            bool enable = cbUseSpectralLibraryDriftTimes.Checked;
            textSpectralLibraryDriftTimesResolvingPower.Enabled = enable;
            // If disabling the text box, and it has content, make sure it is
            // valid content.  Otherwise, clear the current content, which
            // is always valid, if the measured drift time values will not be used.
            if (!enable && !string.IsNullOrEmpty(textSpectralLibraryDriftTimesResolvingPower.Text))
            {
                double resolvingPower;
                if (!double.TryParse(textSpectralLibraryDriftTimesResolvingPower.Text, out resolvingPower) ||
                        resolvingPower <= 0)
                {
                    textSpectralLibraryDriftTimesResolvingPower.Text = string.Empty;
                }
            }
        }

        private void btnEditExlusions_Click(object sender, EventArgs e)
        {
            _driverExclusion.EditList();
        }

        private void editLibraries_Click(object sender, EventArgs e)
        {
            EditLibraryList();
        }

        public void EditLibraryList()
        {
            CheckDisposed();
            _driverLibrary.EditList();
            _driverRT.LoadList(_driverRT.SelectedItem.ToString());

            panelPick.Visible = listLibraries.CheckedIndices.Count > 0;
            btnExplore.Enabled = listLibraries.Items.Count > 0;
        }

        public SettingsListBoxDriver<LibrarySpec> LibraryDriver
        {
            get { return _driverLibrary; }
        }

        public void SetIsotopeModifications(int index, bool check)
        {
            listHeavyMods.SetItemChecked(index, check);
        }

        private void btnBuildLibrary_Click(object sender, EventArgs e)
        {
            ShowBuildLibraryDlg();
        }

        public void ShowBuildLibraryDlg()
        {
            CheckDisposed();

            // Libraries built for full-scan filtering can have important retention time information,
            // and the redundant libraries are more likely to be desirable for showing spectra.
            bool isFullScanEnabled = _parent.DocumentUI.Settings.TransitionSettings.FullScan.IsEnabled;
            using (var dlg = new BuildLibraryDlg(_parent) {LibraryKeepRedundant = isFullScanEnabled})
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _libraryManager.BuildLibrary(_parent, dlg.Builder, _parent.LibraryBuildCompleteCallback);

                    Settings.Default.SpectralLibraryList.Add(dlg.Builder.LibrarySpec);
                    _driverLibrary.LoadList();
                }
            }
        }

        private void listLibraries_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Checked && listLibraries.CheckedItems.Count == 0)
                panelPick.Visible = true;
            else if (e.NewValue == CheckState.Unchecked && listLibraries.CheckedItems.Count == 1)
                panelPick.Visible = false;

            // Only update ranks, if they are enabled
            int match = comboMatching.SelectedIndex;
            if (match == (int) PeptidePick.library || match == (int) PeptidePick.both)
                UpdateRanks(e);
        }

        private void comboMatching_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboMatching.SelectedIndex)
            {
                case (int)PeptidePick.library:
                case (int)PeptidePick.both:
                    UpdateRanks(null);
                    if (IsValidRankId(_lastRankId, _driverLibrary.Chosen))
                        comboRank.SelectedItem = _lastRankId;
                    comboRank.Enabled = true;
                    break;

                case (int)PeptidePick.filter:
                case (int)PeptidePick.either:
                    if (comboRank.SelectedIndex != -1)
                        _lastRankId = (PeptideRankId)comboRank.SelectedItem;
                    comboRank.SelectedIndex = -1;
                    comboRank.Enabled = false;
                    break;
            }
        }

        private void UpdateRanks(ItemCheckEventArgs e)
        {
            // Store selection, if there is one.
            if (comboRank.SelectedIndex != -1)
                _lastRankId = (PeptideRankId) comboRank.SelectedItem;

            PeptideRankId rankId = _lastRankId;

            _eventChosenLibraries = _driverLibrary.GetChosen(e);
            try
            {
                // Recalculate possible ranks from selected libraries
                comboRank.Items.Clear();
                comboRank.Items.Add(PeptideRankId.PEPTIDE_RANK_NONE);

                HashSet<PeptideRankId> rankIdSet = new HashSet<PeptideRankId>();
                foreach (LibrarySpec spec in _eventChosenLibraries)
                {
                    // If not all libraries contain the most recently selected
                    // rank ID, then leave the default of no rank ID.
                    if (rankId != null && !spec.PeptideRankIds.Contains(rankId))
                        rankId = null;

                    rankIdSet.UnionWith(spec.PeptideRankIds);
                }
                PeptideRankId[] rankIds = rankIdSet.ToArray();
                Array.Sort(rankIds, (id1, id2) => Comparer<string>.Default.Compare(id1.Label, id2.Label));
                comboRank.Items.AddRange(rankIds.Cast<object>().ToArray());

                // Restore selection
                if (rankId != null)
                    comboRank.SelectedItem = rankId;
                comboRank_SelectedIndexChanged(this, new EventArgs());
            }
            finally
            {
                _eventChosenLibraries = null;
            }
        }

        private void comboRank_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboRank.SelectedIndex < 1)
            {
                cbLimitPeptides.Enabled = false;
                if (cbLimitPeptides.Checked)
                    cbLimitPeptides.Checked = false;
                else
                    cbLimitPeptides_CheckedChanged(this, new EventArgs());
            }
            else
            {
                // Make sure all libraries have this rank ID.
                PeptideRankId rankId = (PeptideRankId)comboRank.SelectedItem;
                IEnumerable<LibrarySpec> chosen = _eventChosenLibraries ?? _driverLibrary.Chosen;
                if (!IsValidRankId(rankId, chosen))
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.PeptideSettingsUI_comboRank_SelectedIndexChanged_Not_all_libraries_chosen_support_the__0__ranking_for_peptides,
                                                                      rankId),
                                                        Resources.PeptideSettingsUI_comboRank_SelectedIndexChanged_Do_you_want_to_uncheck_the_ones_that_do_not);
                    if (MessageBox.Show(this, message, Program.Name, MessageBoxButtons.OKCancel) == DialogResult.OK)
                    {
                        foreach (int i in listLibraries.CheckedIndices)
                        {
                            if (!_driverLibrary.List[i].PeptideRankIds.Contains(rankId))
                                listLibraries.SetItemChecked(i, false);
                        }
                    }
                    else
                    {
                        comboRank.SelectedIndex = -1;
                        return;
                    }
                }
                cbLimitPeptides.Enabled = true;
                if (!string.IsNullOrEmpty(_lastPeptideCount))
                    cbLimitPeptides.Checked = true;
            }
        }

        private static bool IsValidRankId(PeptideRankId rankId, IEnumerable<LibrarySpec> chosen)
        {
            foreach (LibrarySpec spec in chosen)
            {
                if (!spec.PeptideRankIds.Contains(rankId))
                    return false;
            }
            return true;            
        }

        private void cbLimitPeptides_CheckedChanged(object sender, EventArgs e)
        {
            if (cbLimitPeptides.Checked)
            {
                if (!string.IsNullOrEmpty(_lastPeptideCount))
                    textPeptideCount.Text = _lastPeptideCount;

                textPeptideCount.Enabled = true;
                labelPeptides.Enabled = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(textPeptideCount.Text))
                    _lastPeptideCount = textPeptideCount.Text;

                labelPeptides.Enabled = false;
                textPeptideCount.Enabled = false;
                textPeptideCount.Text = string.Empty;
            }
        }

        private void textPeptideCount_TextChanged(object sender, EventArgs e)
        {
            // If the control is enabled, then the user is changing this value,
            // so the saved value is cleared.
            if (textPeptideCount.Enabled)
                _lastPeptideCount = null;
        }

        private void btnExplore_Click(object sender, EventArgs e)
        {
            ShowViewLibraryDlg();
        }
        
        public bool IsSettingsChanged
        {
            get { return !Equals(_peptideSettings, ValidateNewSettings(false)); }
        }

        public void ShowViewLibraryDlg(string libName = null)
        {
            CheckDisposed();

            // Validate new settings without showing message boxes
            PeptideSettings settings = ValidateNewSettings(false);
            // Only update, if anything changed
            if (!Equals(settings, _peptideSettings))
            {
                var result = MultiButtonMsgDlg.Show(
                    this,
                    Resources.PeptideSettingsUI_ShowViewLibraryDlg_Peptide_settings_have_been_changed_Save_changes,
                    MultiButtonMsgDlg.BUTTON_YES,
                    MultiButtonMsgDlg.BUTTON_NO,
                    true);
                switch (result)
                {
                    case DialogResult.Yes:
                        // If settings are null, then validation failed the first time
                        if (settings == null)
                        {
                            // Show the error this time
                            ValidateNewSettings(true);
                            return;
                        }
                        SrmSettings newSettings = _parent.DocumentUI.Settings.ChangePeptideSettings(settings);
                        if (_parent.ChangeSettings(newSettings, true))
                        {
                            _peptideSettings = settings;
                        }
                        break;
                    case DialogResult.No:
                        break;
                    case DialogResult.Cancel:
                        return;
                }
            }
            IsShowLibraryExplorer = true;
            DialogResult = DialogResult.OK;
            var index = _parent.OwnedForms.IndexOf(form => form is ViewLibraryDlg);
            if (index == -1)
            {
                // Selected library name should be the ListBox selected item if possible, else the first checked item, 
                // else the empty string. 
                if (libName == null)
                {
                    libName = _driverLibrary.ListBox.SelectedItem != null
                                  ? _driverLibrary.ListBox.SelectedItem.ToString()
                                  : (_driverLibrary.CheckedNames.Any() ? _driverLibrary.CheckedNames[0] : string.Empty);
                }
                var viewLibraryDlg = new ViewLibraryDlg(_libraryManager, libName, _parent) { Owner = Owner };
                viewLibraryDlg.Show();
            }
        }

        private void comboLabelType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Handle label type selection events, like <Edit list...>
            if (_driverLabelType != null)
                _driverLabelType.SelectedIndexChangedEvent();
        }

        public void EditLabelTypeList ()
        {
            CheckDisposed();
            _driverLabelType.EditList();
        }

        private void btnEditStaticMods_Click(object sender, EventArgs e)
        {
            EditStaticMods();
        }

        public void EditStaticMods()
        {
            _driverStaticMod.EditList();
        }

        private void btnEditHeavyMods_Click(object sender, EventArgs e)
        {
            EditHeavyMods();
        }

        public void EditHeavyMods()
        {
            _driverHeavyMod.EditList();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void comboPeakScoringModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ((_driverPeakScoringModel.EditCurrentSelected() || _driverPeakScoringModel.AddItemSelected()) &&
                _parent.DocumentUI.Settings.MeasuredResults == null)
            {
                MessageDlg.Show(this, Resources.PeptideSettingsUI_comboPeakScoringModel_SelectedIndexChanged_The_document_must_have_imported_results_in_order_to_train_a_model_);
                return;
            }
            _driverPeakScoringModel.SelectedIndexChangedEvent(sender, e);
        }

        #region Functional testing support

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = tabControl1.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }

        public TABS SelectedTab
        {
            get { return (TABS)tabControl1.SelectedIndex; }
            set { tabControl1.SelectedIndex = (int)value; }
        }

        public void ChooseRegression(string name)
        {
            comboRetentionTime.SelectedItem = name;
        }

        public void UseMeasuredRT(bool use)
        {
            cbUseMeasuredRT.Checked = use;
        }

        public bool IsUseMeasuredRT
        {
            get { return cbUseMeasuredRT.Checked; }
            set { cbUseMeasuredRT.Checked = value; }
        }

        public int TimeWindow
        {
            get { return Convert.ToInt32(textMeasureRTWindow.Text); }
            set { textMeasureRTWindow.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public bool IsUseSpectralLibraryDriftTimes
        {
            get { return cbUseSpectralLibraryDriftTimes.Checked; }
            set { cbUseSpectralLibraryDriftTimes.Checked = value; }
        }

        public double? SpectralLibraryDriftTimeResolvingPower
        {
            get
            {

                if (string.IsNullOrEmpty(textSpectralLibraryDriftTimesResolvingPower.Text))
                    return null;
                return Convert.ToDouble(textSpectralLibraryDriftTimesResolvingPower.Text);
            }
            set { textSpectralLibraryDriftTimesResolvingPower.Text = value.HasValue ? value.ToString() : string.Empty; }
        }

        public void ShowBuildBackgroundProteomeDlg()
        {
            CheckDisposed();
            _driverBackgroundProteome.AddItem();
        }

        public void AddRTRegression()
        {
            CheckDisposed();
            _driverRT.AddItem();
        }
        
        public void EditRegression()
        {
            var list = Settings.Default.RetentionTimeList;
            var regNew = list.EditItem(this, _driverRT.SelectedItem, list, null);
            if (regNew != null)
                list.SetValue(regNew);
        }

        public void EditRegressionList()
        {
            CheckDisposed();
            _driverRT.EditList();
        }

        public void EditExclusionList()
        {
            CheckDisposed();
            _driverExclusion.EditList();
        }

        public int MissedCleavages
        { 
            get { return int.Parse(comboMissedCleavages.SelectedItem.ToString()); }
            set { comboMissedCleavages.SelectedItem = value.ToString(CultureInfo.CurrentCulture); }
        }

        public int TextMinLength
        {
            get { return int.Parse(textMinLength.Text); }
            set { textMinLength.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public int TextMaxLength
        {
            get { return int.Parse(textMaxLength.Text); }
            set { textMaxLength.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public int TextExcludeAAs
        {
            get { return int.Parse(textExcludeAAs.Text); }
            set { textExcludeAAs.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public void SetLibraryChecked(int index, bool value)
        {
            listLibraries.SetItemChecked(index,value);
        }

        public bool AutoSelectMatchingPeptides
        {
            get { return cbAutoSelect.Checked; }
            set { cbAutoSelect.Checked = value; }
        }

        public string[] AvailableLibraries
        {
            get
            {
                var availableLibraries = new List<string>();
                foreach (object item in listLibraries.Items)
                    availableLibraries.Add(item.ToString());
                return availableLibraries.ToArray();
            }
        }

        public string[] PickedLibraries
        {
            get { return _driverLibrary.CheckedNames; }
            set { _driverLibrary.CheckedNames = value; }
        }

        public string[] PickedStaticMods
        {
            get { return _driverStaticMod.CheckedNames; }
            set { _driverStaticMod.CheckedNames = value;}
        }

        public string[] PickedHeavyMods
        {
            get { return _driverHeavyMod.CheckedNames; }
            set { _driverHeavyMod.CheckedNames = value; }
        }

        public string SelectedBackgroundProteome
        {
            get { return _driverBackgroundProteome.Combo.SelectedItem.ToString(); }
            set { _driverBackgroundProteome.Combo.SelectedItem = value;  }
        }

        public string SelectedRTPredictor
        {
            get { return _driverRT.Combo.SelectedItem.ToString(); }
            set { _driverRT.Combo.SelectedItem = value; }
        }

        public string SelectedLabelTypeName
        {
            get { return _driverLabelType.SelectedName; }
            set { _driverLabelType.SelectedName = value; }
        }

        public string SelectedInternalStandardTypeName
        {
            get { return _driverLabelType.SelectedInternalStandardName; }
            set { _driverLabelType.SelectedInternalStandardName = value; }
        }

        public int MaxVariableMods
        {
            get { return Convert.ToInt32(textMaxVariableMods.Text); }
            set { textMaxVariableMods.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public int MaxNeutralLosses
        {
            get { return Convert.ToInt32(textMaxNeutralLosses.Text); }
            set { textMaxNeutralLosses.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public PeptideRankId RankID
        {
            get { return (PeptideRankId) comboRank.SelectedItem;  }
            set { comboRank.SelectedIndex = comboRank.FindString(value.Label); }
        }

        public bool LimitPeptides
        {
            get { return cbLimitPeptides.Checked; }
            set { cbLimitPeptides.Checked = value; }
        }

        public int PeptidesPerProtein
        {
            get { return Convert.ToInt32(textPeptideCount.Text); }
            set { textPeptideCount.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public IEnumerable<IsotopeLabelType> LabelTypes
        {
            get
            {
                return from mod in _driverLabelType.GetHeavyModifications()
                       select mod.LabelType;
            }
        }

        public void AddPeakScoringModel()
        {
            _driverPeakScoringModel.AddItem();
        }

        public void EditPeakScoringModel()
        {
            _driverPeakScoringModel.EditList();
        }

        public string ComboPeakScoringModelSelected 
        {
            get { return comboPeakScoringModel.SelectedItem.ToString(); }
            set
            {
                foreach (var item in comboPeakScoringModel.Items)
                {
                    if (item.ToString() == value)
                    {
                        comboPeakScoringModel.SelectedItem = item;
                        return;
                    }
                }
                throw new InvalidDataException(Resources.EditPeakScoringModelDlg_SelectedModelItem_Invalid_Model_Selection);
            }
        }

        public string ComboEnzymeSelected
        {
            get { return comboEnzyme.SelectedItem.ToString(); }
            set
            {
                int i = 0;
                foreach (var item in comboEnzyme.Items)
                {
                    if (item.ToString().Equals(value))
                    {
                        comboEnzyme.SelectedIndex = i;
                        return;
                    }
                    i ++;
                }
                throw new ArgumentException();
            }
        }

        public int MaxMissedCleavages
        {
            get { return int.Parse(comboMissedCleavages.SelectedItem.ToString()); }
            set
            {
                foreach (var item in comboMissedCleavages.Items)
                {
                    if (int.Parse(item.ToString()) == value)
                    {
                        comboMissedCleavages.SelectedItem = value.ToString(LocalizationHelper.CurrentCulture);
                        return;
                    }
                }
                throw new ArgumentException();
            }
        }

        public void AddBackgroundProteome()
        {
            _driverBackgroundProteome.AddItem();    
        }

        public NormalizationMethod QuantNormalizationMethod
        {
            get { return comboNormalizationMethod.SelectedItem as NormalizationMethod; }
            set { comboNormalizationMethod.SelectedItem = value; }
        }

        public RegressionFit QuantRegressionFit
        {
            get { return comboRegressionFit.SelectedItem as RegressionFit; }
            set { comboRegressionFit.SelectedItem = value; }
        }

        public RegressionWeighting QuantRegressionWeighting
        {
            get { return comboWeighting.SelectedItem as RegressionWeighting; }
            set { comboWeighting.SelectedItem = value; }
        }

        public int? QuantMsLevel
        {
            get { return _quantMsLevels[comboQuantMsLevel.SelectedIndex]; }
            set { comboQuantMsLevel.SelectedIndex = _quantMsLevels.IndexOf(value); }
        }

        public string QuantUnits
        {
            get { return tbxQuantUnits.Text; }
            set { tbxQuantUnits.Text = value; }
        }

        #endregion

        public sealed class LabelTypeComboDriver
        {
            private readonly SettingsListBoxDriver<StaticMod> _driverHeavyMod;

            private int _selectedIndexLast;
            private bool _singleStandard;

            public LabelTypeComboDriver(ComboBox combo,
                                        PeptideModifications modifications,
                                        SettingsListBoxDriver<StaticMod> driverHeavyMod,
                                        Label labelIS,
                                        ComboBox comboIS,
                                        CheckedListBox listBoxIS)
            {
                _driverHeavyMod = driverHeavyMod;

                LabelIS = labelIS;
                Combo = combo;
                Combo.DisplayMember = Resources.LabelTypeComboDriver_LabelTypeComboDriver_LabelType;
                ComboIS = comboIS;
                ListBoxIS = listBoxIS;
                LoadList(null, modifications.InternalStandardTypes,
                    modifications.GetHeavyModifications().ToArray());
                ShowModifications();
            }

            private ComboBox Combo { get; set; }
            private ComboBox ComboIS { get; set; }
            private CheckedListBox ListBoxIS { get; set; }
            private Label LabelIS { get; set; }

            public IList<IsotopeLabelType> InternalStandardTypes
            {
                get
                {
                    if (ComboIS == null)
                        return null; // We're using this from the EditCustomMolecule dialog

                    if (_singleStandard)
                        return Equals(ComboIS.SelectedItem, Resources.LabelTypeComboDriver_LoadList_none) ? new IsotopeLabelType[0] : new[] { (IsotopeLabelType)ComboIS.SelectedItem };
                    
                    var listStandardTypes = new List<IsotopeLabelType>();
                    foreach (IsotopeLabelType labelType in ListBoxIS.CheckedItems)
                        listStandardTypes.Add(labelType);
                    return listStandardTypes.ToArray();
                }
            }

            private void LoadList(string selectedItemLast, ICollection<IsotopeLabelType> internalStandardTypes,
                IList<TypedModifications> heavyMods)
            {
                try
                {
                    Combo.BeginUpdate();
                    if (ComboIS != null)
                        ComboIS.BeginUpdate();
                    Combo.Items.Clear();

                    if (ComboIS == null) 
                    {
                        // Using this from the Edit Molecule dialog, we want to see Light in this list
                        Combo.Items.Add(new TypedModifications(IsotopeLabelType.light, new List<StaticMod>()));
                    }
                    else
                    {
                        _singleStandard = (heavyMods.Count() <= 1);
                        if (_singleStandard)
                        {
                            LabelIS.Text = Resources.LabelTypeComboDriver_LoadList_Internal_standard_type;
                            ComboIS.Items.Clear();
                            ComboIS.Items.Add(Resources.LabelTypeComboDriver_LoadList_none);
                            ComboIS.Items.Add(IsotopeLabelType.light);
                            if (!internalStandardTypes.Any())
                                ComboIS.SelectedIndex = 0;
                            if (internalStandardTypes.Contains(IsotopeLabelType.light))
                                ComboIS.SelectedIndex = 1;
                            ComboIS.Visible = true;
                            ListBoxIS.Visible = false;
                            ComboIS.Parent.Parent.Parent.Height +=
                                ComboIS.Bottom + BORDER_BOTTOM_HEIGHT - ComboIS.Parent.Height;
                        }
                        else
                        {
                            LabelIS.Text = Resources.LabelTypeComboDriver_LoadList_Internal_standard_types;
                            ListBoxIS.Items.Clear();
                            ListBoxIS.Items.Add(IsotopeLabelType.light);
                            if (internalStandardTypes.Contains(IsotopeLabelType.light))
                                ListBoxIS.SetItemChecked(0, true);
                            ComboIS.Visible = false;
                            ListBoxIS.Visible = true;
                            ListBoxIS.Parent.Parent.Parent.Height +=
                                ListBoxIS.Bottom + BORDER_BOTTOM_HEIGHT - ComboIS.Parent.Height;
                        }
                    }
                    foreach (var typedMods in heavyMods)
                    {
                        string labelName = typedMods.LabelType.Name;

                        int i = Combo.Items.Add(typedMods);
                        if (Equals(typedMods.LabelType.Name, selectedItemLast))
                            Combo.SelectedIndex = i;

                        if (ComboIS != null) // We may be using this from the Edit Molecule dialog, which is less complex
                        {
                            if (_singleStandard)
                            {
                                i = ComboIS.Items.Add(typedMods.LabelType);
                                if (internalStandardTypes.Contains(lt => Equals(lt.Name, labelName)))
                                    ComboIS.SelectedIndex = i;
                            }
                            else
                            {
                                i = ListBoxIS.Items.Add(typedMods.LabelType);
                                if (internalStandardTypes.Contains(lt => Equals(lt.Name, labelName)))
                                    ListBoxIS.SetItemChecked(i, true);
                            }
                        }
                    }

                    Combo.Items.Add(Resources.LabelTypeComboDriver_LoadList_Edit_list);
                    if (Combo.SelectedIndex < 0)
                        Combo.SelectedIndex = 0;
                    // If no internal standard selected yet, use the first heavy mod type
                    if (ComboIS != null &&_singleStandard && ComboIS.SelectedIndex == -1)
                        ComboIS.SelectedIndex = (ComboIS.Items.Count > 2 ? 2 : 1);
                }
                finally
                {
                    if (ComboIS != null)
                        ComboIS.EndUpdate();
                    Combo.EndUpdate();
                }                
            }

            public string SelectedName
            {
                get { return SelectedMods.LabelType.Name; }
                set
                {
                    for (int i = 0; i < Combo.Items.Count; i++)
                    {
                        if (Equals(value, ((TypedModifications)Combo.Items[i]).LabelType.Name))
                        {
                            Combo.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            public string SelectedInternalStandardName
            {
                get { return SelectedInternalStandardType == null ? Resources.LabelTypeComboDriver_LoadList_none : SelectedInternalStandardType.Name; }
                set
                {
                    if (Equals(value, Resources.LabelTypeComboDriver_LoadList_none))
                    {
                        ComboIS.SelectedItem = Resources.LabelTypeComboDriver_LoadList_none;
                    }
                    for (int i = 1; i < ComboIS.Items.Count; i++)
                    {
                        if (Equals(value, ((IsotopeLabelType)ComboIS.Items[i]).Name))
                        {
                            ComboIS.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            public TypedModifications SelectedMods
            {
                get { return (TypedModifications) Combo.SelectedItem; }
            }

            private IsotopeLabelType SelectedInternalStandardType
            {
                get { return ComboIS.SelectedItem as IsotopeLabelType; }
            }

            private TypedModifications SelectedModsLast
            {
                get { return (TypedModifications)Combo.Items[_selectedIndexLast]; }
            }

            public IEnumerable<TypedModifications> GetHeavyModifications()
            {
                UpdateLastSelected();
                // If we are using this in the Add Custom Molecule dialog, we will have added the "light" label to the pick list - ignore here
                return Combo.Items.OfType<TypedModifications>().Where(x => x.LabelType.Name != IsotopeLabelType.light.Name);
            }

            private bool EditListSelected()
            {
                return (Resources.LabelTypeComboDriver_LoadList_Edit_list == Combo.SelectedItem.ToString());
            }

            public void SelectedIndexChangedEvent()
            {
                if (_selectedIndexLast != -1)
                {
                    UpdateLastSelected();

                    if (EditListSelected())
                    {
                        EditList();
                    }

                    ShowModifications();
                }

                _selectedIndexLast = Combo.SelectedIndex;
            }

            private void UpdateLastSelected()
            {
                if (_driverHeavyMod == null) // We may be using this from the less-complicated EditCustomMolecule dialog
                    return;
                var lastSelectedMods = SelectedModsLast;
                var currentMods = _driverHeavyMod.Chosen;
                if (!ArrayUtil.EqualsDeep(currentMods, lastSelectedMods.Modifications))
                {
                    Combo.Items[_selectedIndexLast] =
                        new TypedModifications(lastSelectedMods.LabelType, currentMods);
                }
            }

            private void ShowModifications()
            {
                if (_driverHeavyMod == null) // We may be using this from the less-complicated EditCustomMolecule dialog
                    return;
                // Update the heavy modifications check-list to show the new selection
                _driverHeavyMod.LoadList(SelectedMods.Modifications);
            }

            public void EditList()
            {
                var heavyMods = GetHeavyModifications().ToArray();
                using (var dlg = new EditLabelTypeListDlg
                              {
                                  LabelTypes = from typedMods in heavyMods
                                               select typedMods.LabelType
                              })
                {
                    if (dlg.ShowDialog(Combo.TopLevelControl) == DialogResult.OK)
                    {
                        // Store existing values in dictionary by lowercase name.
                        string selectedItemLast = SelectedModsLast.LabelType.Name;
                        var internalStandardTypes = InternalStandardTypes;
                        var dictHeavyMods = new Dictionary<string, TypedModifications>();
                        foreach (var typedMods in heavyMods)
                            dictHeavyMods.Add(typedMods.LabelType.Name.ToLower(), typedMods);

                        Combo.Items.Clear();

                        // Add new values based on the content of the dialog.
                        // Names that already existed keep same modifications, and
                        // names that have not changed order stay reference-equal.
                        var listHeavyMods = new List<TypedModifications>();
                        foreach (var labelType in dlg.LabelTypes)
                        {
                            TypedModifications typedMods;
                            if (!dictHeavyMods.TryGetValue(labelType.Name.ToLower(), out typedMods))
                                typedMods = new TypedModifications(labelType, new StaticMod[0]);
                            else if (!Equals(labelType, typedMods.LabelType))
                                typedMods = new TypedModifications(labelType, typedMods.Modifications);
                            listHeavyMods.Add(typedMods);
                        }

                        _selectedIndexLast = -1;
                        LoadList(selectedItemLast, internalStandardTypes, listHeavyMods);
                    }
                    else
                    {
                        // Reset the selected index before edit was chosen.
                        Combo.SelectedIndex = _selectedIndexLast;
                    }
                }
            }
        }
    }
}