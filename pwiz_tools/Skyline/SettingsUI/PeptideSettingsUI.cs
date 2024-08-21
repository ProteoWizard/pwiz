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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class PeptideSettingsUI : FormEx, IMultipleViewProvider
    {
        private const int BORDER_BOTTOM_HEIGHT = 16;

// ReSharper disable InconsistentNaming
        public enum TABS { Digest, Prediction, Filter, Library, Modifications, Labels, /* Integration, */ Quantification }
        // ReSharper restore InconsistentNaming

        public class TabWithPage : IFormView
        {
            public TabPage Page;
        }
        public class DigestionTab : TabWithPage { }
        public class PredictionTab : TabWithPage { }
        public class FilterTab : TabWithPage { }
        public class LibraryTab : TabWithPage { }
        public class ModificationsTab : TabWithPage { }
        public class LabelsTab : TabWithPage { }
        /* public class IntegrationTab : TabWithPage { } never visible */
        public class QuantificationTab : TabWithPage { }

        private readonly Dictionary<TABS, TabWithPage> _tabPages;

        private readonly SkylineWindow _parent;
        private readonly LibraryManager _libraryManager;
        private PeptideSettings _peptideSettings;
        private IEnumerable<LibrarySpec> _eventChosenLibraries;
        private PeptideRankId _lastRankId;
        private string _lastPeptideCount;

        private readonly SettingsListComboDriver<Enzyme> _driverEnzyme;
        private readonly SettingsListComboDriver<RetentionTimeRegression> _driverRT;
        private readonly SettingsListBoxDriver<PeptideExcludeRegex> _driverExclusion;
        private readonly SettingsListBoxDriver<LibrarySpec> _driverLibrary;
        private readonly SettingsListComboDriver<BackgroundProteomeSpec> _driverBackgroundProteome;
        private readonly SettingsListBoxDriver<StaticMod> _driverStaticMod;
        private readonly SettingsListBoxDriver<StaticMod> _driverHeavyMod;
        private readonly SettingsListComboDriver<PeakScoringModelSpec> _driverPeakScoringModel;
        private readonly LabelTypeComboDriver _driverLabelType;
        private static readonly IList<int?> _quantMsLevels = ImmutableList.ValueOf(new int?[] {null, 1, 2});
        private readonly LabelTypeComboDriver _driverSmallMolInternalStandardTypes;
        private string _staticModsOriginalTooltip;
        private string _heavyModsOriginalTooltip;
        private string _librariesOriginalTooltip;

        public PeptideSettingsUI(SkylineWindow parent, LibraryManager libraryManager, TABS? selectTab)
        {
            InitializeComponent();

            _tabPages = new Dictionary<TABS, TabWithPage>
            {
                {TABS.Digest, new DigestionTab { Page = tabDigestion }},
                {TABS.Prediction, new PredictionTab {Page = tabPrediction }},
                {TABS.Filter, new FilterTab {Page = tabFilter}},
                {TABS.Library, new LibraryTab {Page = tabLibrary}},
                {TABS.Modifications, new ModificationsTab {Page = tabModifications}},
                {TABS.Labels, new LabelsTab {Page = tabLabels}},
                // {TABS.Integration, new IntegrationTab {Page = tabIntegration}},
                {TABS.Quantification, new QuantificationTab {Page = tabQuantification}}
            };

            RestoreTabSel(selectTab);

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


            // Initialize filter settings
            _driverExclusion = new SettingsListBoxDriver<PeptideExcludeRegex>(listboxExclusions, Settings.Default.PeptideExcludeList);
            _driverExclusion.LoadList(null, Filter.Exclusions);

            textExcludeAAs.Text = Filter.ExcludeNTermAAs.ToString(LocalizationHelper.CurrentCulture);
            textMaxLength.Text = Filter.MaxPeptideLength.ToString(LocalizationHelper.CurrentCulture);
            textMinLength.Text = Filter.MinPeptideLength.ToString(LocalizationHelper.CurrentCulture);
            cbAutoSelect.Checked = Filter.AutoSelect;
            comboBoxPeptideUniquenessConstraint.SelectedItem =
                comboBoxPeptideUniquenessConstraint.Items[(int)_peptideSettings.Filter.PeptideUniqueness];

            // Initialize spectral library settings
            _driverLibrary = new SettingsListBoxDriver<LibrarySpec>(listLibraries, Settings.Default.SpectralLibraryList);
            IList<LibrarySpec> listLibrarySpecs = Libraries.LibrarySpecs;

            _driverLibrary.LoadList(null, listLibrarySpecs);
            _driverBackgroundProteome = new SettingsListComboDriver<BackgroundProteomeSpec>(comboBackgroundProteome, Settings.Default.BackgroundProteomeList);
            _driverBackgroundProteome.LoadList(_peptideSettings.BackgroundProteome.Name);
            UpdatePeptideUniquenessEnabled();
            FilterLibraryEnabled = _peptideSettings.Libraries.HasMidasLibrary && _parent.Document.Settings.HasResults;

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
            _driverLabelType = new LabelTypeComboDriver(LabelTypeComboDriver.UsageType.ModificationsPicker, comboLabelType, Modifications, _driverHeavyMod, 
                labelStandardType, comboStandardType, listStandardTypes);
            textMaxVariableMods.Text = Modifications.MaxVariableMods.ToString(LocalizationHelper.CurrentCulture);
            textMaxNeutralLosses.Text = Modifications.MaxNeutralLosses.ToString(LocalizationHelper.CurrentCulture);

            // Initialize small molecule label types.
            _driverSmallMolInternalStandardTypes = new LabelTypeComboDriver(LabelTypeComboDriver.UsageType.InternalStandardListMaintainer, comboLabelType, Modifications, null,
                labelSmallMolInternalStandardTypes, null, listBoxSmallMolInternalStandardTypes);

            // Initialize peak scoring settings.
            _driverPeakScoringModel = new SettingsListComboDriver<PeakScoringModelSpec>(comboPeakScoringModel, Settings.Default.PeakScoringModelList);
            var peakScoringModel = _peptideSettings.Integration.PeakScoringModel;
            _driverPeakScoringModel.LoadList(peakScoringModel != null ? peakScoringModel.Name : null);

            IsShowLibraryExplorer = false;
            FormUtil.RemoveTabPage(tabIntegration, helpTip);
            comboNormalizationMethod.SelectedItem = _peptideSettings.Quantification.NormalizationMethod;
            comboWeighting.Items.AddRange(RegressionWeighting.All.Cast<object>().ToArray());
            comboWeighting.SelectedItem = _peptideSettings.Quantification.RegressionWeighting;

            comboRegressionFit.Items.AddRange(RegressionFit.All.Cast<object>().ToArray());
            comboRegressionFit.SelectedItem = _peptideSettings.Quantification.RegressionFit;
            UpdateLodOptions(_peptideSettings.Quantification.LodCalculation ?? LodCalculation.NONE);

            comboQuantMsLevel.SelectedIndex = Math.Max(0, _quantMsLevels.IndexOf(_peptideSettings.Quantification.MsLevel));
            tbxQuantUnits.Text = _peptideSettings.Quantification.Units;
            tbxMaxLoqBias.Text = _peptideSettings.Quantification.MaxLoqBias.ToString();
            tbxMaxLoqCv.Text = _peptideSettings.Quantification.MaxLoqCv.ToString();
            tbxIonRatioThreshold.Text = _peptideSettings.Quantification.QualitativeIonRatioThreshold.ToString();
            cbxSimpleRatios.Checked = _peptideSettings.Quantification.SimpleRatios;
            UpdateComboNormalizationMethod();
        }

        /// <summary>
        /// Restore the selected tab, or use current UI mode's last-used tab if null request
        /// </summary>
        private void RestoreTabSel(TABS? selectTab)
        {
            if (selectTab != null)
            {
                TabControlSel = selectTab;
            }
            else
            { 
                // No active tab requested (ie we're not in a test), go with current UI mode's last-used
                switch (ModeUI)
                {
                    case SrmDocument.DOCUMENT_TYPE.proteomic:
                        TabControlSel = (TABS) Settings.Default.PeptideSettingsTab;
                        break;
                    case SrmDocument.DOCUMENT_TYPE.small_molecules:
                        TabControlSel = (TABS) Settings.Default.MoleculeSettingsTab;
                        break;
                    case SrmDocument.DOCUMENT_TYPE.mixed:
                        TabControlSel = (TABS) Settings.Default.MixedMoleculeSettingsTab;
                        break;
                }
            }
        }

        /// <summary>
        /// Remember which tab we were on for user convenience next time we are here
        /// </summary>
        private void SaveTabSel()
        {
            switch (ModeUI) 
            {
                case SrmDocument.DOCUMENT_TYPE.proteomic:
                    Settings.Default.PeptideSettingsTab = (int)SelectedTab;
                    break;
                case SrmDocument.DOCUMENT_TYPE.small_molecules:
                    Settings.Default.MoleculeSettingsTab = (int)SelectedTab;
                    break;
                case SrmDocument.DOCUMENT_TYPE.mixed:
                    Settings.Default.MixedMoleculeSettingsTab = (int)SelectedTab;
                    break;
            }
        }

        public DigestSettings Digest { get { return _peptideSettings.DigestSettings; } }
        public PeptidePrediction Prediction { get { return _peptideSettings.Prediction; } }
        public PeptideFilter Filter { get { return _peptideSettings.Filter; } }
        public PeptideLibraries Libraries { get { return _peptideSettings.Libraries; } }
        public PeptideModifications Modifications { get { return _peptideSettings.Modifications; } }
        public PeptideIntegration Integration { get { return _peptideSettings.Integration; } }
        public bool IsShowLibraryExplorer { get; set; }
        public TABS? TabControlSel { get; set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_parent != null)
            {
                _parent.DocumentUIChangedEvent += ParentOnDocumentChangedEvent;
            }
        }

        private void ParentOnDocumentChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            _peptideSettings = _parent.DocumentUI.Settings.PeptideSettings;
            var retentionTime = _peptideSettings.Prediction.RetentionTime;
            string retentionTimeName = retentionTime != null ? retentionTime.Name : null;
            _driverRT.LoadList(retentionTimeName);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_parent != null)
            {
                _parent.DocumentUIChangedEvent -= ParentOnDocumentChangedEvent;
            }
            base.OnHandleDestroyed(e);
        }

        public bool FilterLibraryEnabled
        {
            get { return btnFilter.Visible; }
            private set
            {
                btnFilter.Visible = value;
                if (value)
                {
                    btnExplore.Top = btnFilter.Bottom + 7;
                }
                else
                {
                    btnExplore.Location = btnFilter.Location;
                }
            }
        }
        // Adjusts indexing for tabs that may be hidden due to UI mode
        private int TabEnumToControlIndex(TABS tab)
        {
            int tabIndex = tabControl1.TabPages.IndexOf(_tabPages[tab].Page);
            if (tabIndex != -1)
                return tabIndex;
            return 0; // The tab is not visible default to the first tab
        }

        // Adjusts indexing for tabs that may be hidden due to UI mode
        private TABS ControlIndexToTabEnum(int controlIndex)
        {
            var control = tabControl1.TabPages[controlIndex];
            var kvp = _tabPages.FirstOrDefault(p => ReferenceEquals(p.Value.Page, control));
            return kvp.Key;
        }

        // Adjusts indexing for tabs that may be hidden due to UI mode
        private TabWithPage ControlIndexToTabPage(int controlIndex)
        {
            var tab = ControlIndexToTabEnum(controlIndex);
            return _tabPages[tab];
        }

        protected override void OnShown(EventArgs e)
        {
            if (TabControlSel.HasValue)
                tabControl1.SelectedIndex = TabEnumToControlIndex(TabControlSel.Value); 
            tabControl1.FocusFirstTabStop();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            SaveTabSel(); // Remember which tab we were on for user convenience next time we are here
        }
        private void UpdatePeptideUniquenessEnabled()
        {
            labelPeptideUniquenessConstraint.Enabled = comboBoxPeptideUniquenessConstraint.Enabled = !_driverBackgroundProteome.SelectedItem.IsNone;
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
                if (_peptideSettings.BackgroundProteome.EqualsSpec(backgroundProteomeSpec))
                    backgroundProteome = _peptideSettings.BackgroundProteome;
                else
                    backgroundProteome = new BackgroundProteome(backgroundProteomeSpec);
                if (backgroundProteome.DatabaseInvalid)
                {

                    var message = TextUtil.LineSeparate(string.Format(SettingsUIResources.PeptideSettingsUI_ValidateNewSettings_Failed_to_load_background_proteome__0__,
                                                                      backgroundProteomeSpec.Name),                                                        
                                                        string.Format(File.Exists(backgroundProteomeSpec.DatabasePath)
                                                                        ? SettingsUIResources.PeptideSettingsUI_ValidateNewSettings_The_file__0__may_not_be_a_valid_proteome_file
                                                                        : SettingsUIResources.PeptideSettingsUI_ValidateNewSettings_The_file__0__is_missing_,
                                                                      backgroundProteomeSpec.DatabasePath));
                    MessageDlg.Show(this, message);
                    tabControl1.SelectedIndex = 0;
                    _driverBackgroundProteome.Combo.Focus();
                    return null;
                }
            }
            Helpers.AssignIfEquals(ref backgroundProteome, _peptideSettings.BackgroundProteome);
            UpdatePeptideUniquenessEnabled();

            // Validate and hold prediction settings
            string nameRT = comboRetentionTime.SelectedItem?.ToString();
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
                if (!helper.ValidateDecimalTextBox(textMeasureRTWindow, minWindow, maxWindow, out measuredRTWindowOut))
                    return null;
                measuredRTWindow = measuredRTWindowOut;
            }

            var prediction = new PeptidePrediction(retentionTime, useMeasuredRT, measuredRTWindow);
            Helpers.AssignIfEquals(ref prediction, Prediction);

            // Validate and hold filter settings
            int excludeNTermAAs;
            if (!helper.ValidateNumberTextBox(textExcludeAAs,
                    PeptideFilter.MIN_EXCLUDE_NTERM_AA, PeptideFilter.MAX_EXCLUDE_NTERM_AA, out excludeNTermAAs))
                return null;
            int minPeptideLength;
            if (!helper.ValidateNumberTextBox(textMinLength,
                    PeptideFilter.MIN_MIN_LENGTH, PeptideFilter.MAX_MIN_LENGTH, out minPeptideLength))
                return null;
            int maxPeptideLength;
            if (!helper.ValidateNumberTextBox(textMaxLength,
                    Math.Max(PeptideFilter.MIN_MAX_LENGTH, minPeptideLength), PeptideFilter.MAX_MAX_LENGTH, out maxPeptideLength))
                return null;

            PeptideExcludeRegex[] exclusions = _driverExclusion.Chosen;

            var peptideUniquenessMode = ComboPeptideUniquenessConstraintSelected;

            bool autoSelect = cbAutoSelect.Checked;
            PeptideFilter filter;
            try
            {
                filter = new PeptideFilter(excludeNTermAAs,
                                           minPeptideLength,
                                           maxPeptideLength,
                                           exclusions,
                                           autoSelect,
                                           peptideUniquenessMode);
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
                        documentLibrary = Libraries.HasDocumentLibrary;
                    }
                    else
                    {
                        // Set to true only if one of the selected libraries is a document library.
                        documentLibrary = librarySpecs.Any(libSpec => libSpec != null && libSpec.IsDocumentLibrary);
                    }

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
            if (!helper.ValidateNumberTextBox(textMaxVariableMods,
                    PeptideModifications.MIN_MAX_VARIABLE_MODS, PeptideModifications.MAX_MAX_VARIABLE_MODS, out maxVariableMods))
                return null;
            int maxNeutralLosses;
            if (!helper.ValidateNumberTextBox(textMaxNeutralLosses,
                    PeptideModifications.MIN_MAX_NEUTRAL_LOSSES, PeptideModifications.MAX_MAX_NEUTRAL_LOSSES, out maxNeutralLosses))
                return null;

            var standardTypes = SmallMoleculeLabelsTabEnabled
                ? _driverSmallMolInternalStandardTypes.InternalStandardTypes
                : _driverLabelType.InternalStandardTypes;
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
                .ChangeUnits(tbxQuantUnits.Text)
                .ChangeLodCalculation(comboLodMethod.SelectedItem as LodCalculation);
            if (Equals(quantification.LodCalculation, LodCalculation.TURNING_POINT) &&
                !Equals(quantification.RegressionFit, RegressionFit.BILINEAR))
            {
                MessageDlg.Show(this, SettingsUIResources.PeptideSettingsUI_ValidateNewSettings_In_order_to_use_the__Bilinear_turning_point__method_of_LOD_calculation___Regression_fit__must_be_set_to__Bilinear__);
                comboLodMethod.Focus();
                return null;
            }
            if (!string.IsNullOrEmpty(tbxMaxLoqBias.Text.Trim()))
            {
                double maxLoqBias;
                if (!helper.ValidateDecimalTextBox(tbxMaxLoqBias, 0, null, out maxLoqBias))
                {
                    return null;
                }
                quantification = quantification.ChangeMaxLoqBias(maxLoqBias);
            }
            if (!string.IsNullOrEmpty(tbxMaxLoqCv.Text.Trim()))
            {
                double maxLoqCv;
                if (!helper.ValidateDecimalTextBox(tbxMaxLoqCv, 0, null, out maxLoqCv))
                {
                    return null;
                }
                quantification = quantification.ChangeMaxLoqCv(maxLoqCv);
            }

            if (!string.IsNullOrEmpty(tbxIonRatioThreshold.Text.Trim()))
            {
                double ionRatioThreshold;
                if (!helper.ValidateDecimalTextBox(tbxIonRatioThreshold, 0, null, out ionRatioThreshold))
                {
                    return null;
                }

                quantification = quantification.ChangeQualitativeIonRatioThreshold(ionRatioThreshold);
            }

            quantification = quantification.ChangeSimpleRatios(cbxSimpleRatios.Checked);

            return new PeptideSettings(enzyme, digest, prediction, filter, libraries, modifications, integration, backgroundProteome, _peptideSettings.ProteinAssociationSettings)
                    .ChangeAbsoluteQuantification(quantification);
        }

        public void OkDialog()
        {
            PeptideSettings settings = ValidateNewSettings(true);
            if (settings == null)
                return;

            // Only update, if anything changed
            if (!Equals(MakeDocIndependent(settings), MakeDocIndependent(_peptideSettings)))
            {
                if (!_parent.ChangeSettingsMonitored(this, SettingsUIResources.PeptideSettingsUI_OkDialog_Changing_peptide_settings,
                                                     s => s.ChangePeptideSettings(settings)))
                {
                    return;
                }
                _peptideSettings = settings;
            }
            DialogResult = DialogResult.OK;
        }

        private PeptideSettings MakeDocIndependent(PeptideSettings settings)
        {
            // TODO(nicksh): This is to handle the fact that we currently cache document information in PeptideModifications.HasHeavyModifications
            //               The cached value gets updated later. So, any PeptideSettings where it is true will not equal the one constructed by this form
            return settings.ChangeModifications(settings.Modifications.ChangeHasHeavyModifications(false));
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
            {
                list.SetValue(calcNew);
                // Automatically add new RT regression using this calculator
                var regressionName = Helpers.GetUniqueName(calcNew.Name, name => !_driverRT.List.Contains(r => Equals(r.Name, name)));
                var regression = new RetentionTimeRegression(regressionName, calcNew, null, null, ImportPeptideSearch.DEFAULT_RT_WINDOW, new List<MeasuredRetentionTime>());
                Settings.Default.RetentionTimeList.Add(regression);
                _driverRT.LoadList(regression.GetKey());
            }
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

        private void comboBackgroundProteome_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverBackgroundProteome.SelectedIndexChangedEvent(sender, e);
            UpdatePeptideUniquenessEnabled();
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

        public void SetIsotopeModifications(int index, bool check)
        {
            listHeavyMods.SetItemChecked(index, check);
        }

        private void btnBuildLibrary_Click(object sender, EventArgs e)
        {
            ShowBuildLibraryDlg();
        }

        public bool IsBuildingLibrary { get; private set; }
        public bool ReportLibraryBuildFailure { get; set; }

        public void ShowBuildLibraryDlg()
        {
            CheckDisposed();

            // Libraries built for full-scan filtering can have important retention time information,
            // and the redundant libraries are more likely to be desirable for showing spectra.
            using (var dlg = new BuildLibraryDlg(_parent))
            {
                dlg.LibraryKeepRedundant = _parent.DocumentUI.Settings.TransitionSettings.FullScan.IsEnabled;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (!string.IsNullOrEmpty(dlg.AddLibraryFile))
                    {
                        using (var editLibDlg = new EditLibraryDlg(Settings.Default.SpectralLibraryList))
                        {
                            editLibDlg.LibraryPath = dlg.AddLibraryFile;
                            if (editLibDlg.ShowDialog(this) == DialogResult.OK)
                            {
                                _driverLibrary.List.Add(editLibDlg.LibrarySpec);
                                _driverLibrary.LoadList(_driverLibrary.Chosen.Concat(new[] {editLibDlg.LibrarySpec}).ToArray());
                            }
                        }
                        return;
                    }

                    IsBuildingLibrary = true;

                    var builder = dlg.Builder;

                    // assume success and cleanup later
                    Settings.Default.SpectralLibraryList.Add(builder.LibrarySpec);
                    _driverLibrary.LoadList();
                    var libraryIndex = listLibraries.Items.IndexOf(builder.LibrarySpec.Name);
                    if (libraryIndex >= 0)
                        listLibraries.SetItemChecked(libraryIndex, true);

                    var currentForm = this;

                    _libraryManager.BuildLibrary(_parent, builder, (buildState, success) =>
                    {
                        _parent.LibraryBuildCompleteCallback(buildState, success);

                        if (!success)
                        {
                            if (ReportLibraryBuildFailure)
                                Console.WriteLine(@"Library {0} build failed", builder.LibrarySpec.Name);

                            _parent.Invoke(new Action(() =>
                            {
                                if (Settings.Default.SpectralLibraryList.Contains(builder.LibrarySpec))
                                    Settings.Default.SpectralLibraryList.Remove(builder.LibrarySpec);
                            }));

                            // TODO: handle the case of cleaning up a PeptideSettingsUI form other than the one that launched this library build
                            if (ReferenceEquals(currentForm, this) && !IsDisposed && !Disposing)
                                currentForm.Invoke(new Action(() =>
                                {
                                    _driverLibrary.LoadList();
                                    listLibraries.Items.Remove(builder.LibrarySpec.Name);
                                }));
                        }
                        IsBuildingLibrary = false;
                    });
                }
            }
        }

        private void btnFilter_Click(object sender, EventArgs e)
        {
            ShowFilterMidasDlg();
        }

        public void ShowFilterMidasDlg()
        {
            var midasLibSpecs = _driverLibrary.Chosen.OfType<MidasLibSpec>().ToArray();
            if (midasLibSpecs.Length > 1)
            {
                midasLibSpecs = midasLibSpecs.Where(lib => Equals(lib.Name, MidasLibSpec.GetName(_parent.DocumentFilePath))).ToArray();
                if (midasLibSpecs.Length != 1)
                {
                    MessageDlg.Show(this, SettingsUIResources.PeptideSettingsUI_ShowFilterMidasDlg_Multiple_MIDAS_libraries_in_document__Select_only_one_before_filtering_);
                    return;
                }
                midasLibSpecs = new[] {midasLibSpecs[0]};
            }
            var midasLibSpec = midasLibSpecs[0];

            using (var filterDlg = new FilterMidasLibraryDlg(_parent.DocumentFilePath, midasLibSpec, _driverLibrary.List))
            {
                if (filterDlg.ShowDialog(this) == DialogResult.OK)
                {
                    MidasLibrary midasLib = null;
                    using (var longWait = new LongWaitDlg())
                    {
                        longWait.Text = SettingsUIResources.PeptideSettingsUI_ShowFilterMidasDlg_Loading_MIDAS_Library;
                        longWait.Message = string.Format(SettingsUIResources.PeptideSettingsUI_ShowFilterMidasDlg_Loading__0_, Path.GetFileName(midasLibSpec.FilePath));
                        longWait.PerformWork(this, 800, monitor => midasLib =
                            _libraryManager.LoadLibrary(midasLibSpec, () => new DefaultFileLoadMonitor(monitor)) as MidasLibrary);
                    }

                    if (midasLib == null)
                    {
                        MessageDlg.Show(this, string.Format(
                            SettingsUIResources.PeptideSettingsUI_ShowFilterMidasDlg_Failed_loading_MIDAS_library__0__, Path.GetFileName(midasLibSpec.FilePath)));
                        return;
                    }

                    var builder = new MidasBlibBuilder(_parent.Document, midasLib, filterDlg.LibraryName, filterDlg.FileName);
                    builder.BuildLibrary(null);
                    Settings.Default.SpectralLibraryList.Add(builder.LibrarySpec);
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

            var isMidas = _driverLibrary.List[e.Index] is MidasLibSpec;
            if (e.NewValue == CheckState.Checked && !FilterLibraryEnabled && isMidas && _parent.Document.Settings.HasResults)
            {
                FilterLibraryEnabled = true;
            }
            else if (e.NewValue == CheckState.Unchecked && FilterLibraryEnabled && _driverLibrary.Chosen.OfType<MidasLibSpec>().Count() == 1 && isMidas)
            {
                FilterLibraryEnabled = false;
            }
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
                    var message = TextUtil.LineSeparate(string.Format(SettingsUIResources.PeptideSettingsUI_comboRank_SelectedIndexChanged_Not_all_libraries_chosen_support_the__0__ranking_for_peptides,
                                                                      rankId),
                                                        SettingsUIResources.PeptideSettingsUI_comboRank_SelectedIndexChanged_Do_you_want_to_uncheck_the_ones_that_do_not);
                    if (MultiButtonMsgDlg.Show(this, message, MessageBoxButtons.OKCancel) == DialogResult.OK)
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
                    SettingsUIResources.PeptideSettingsUI_ShowViewLibraryDlg_Peptide_settings_have_been_changed_Save_changes,
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
                viewLibraryDlg.Show(Owner);
            }
        }

        private void comboLabelType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Handle label type selection events, like <Edit list...>
            if (_driverLabelType != null && _driverLabelType.Usage == LabelTypeComboDriver.UsageType.ModificationsPicker)
                _driverLabelType.SelectedIndexChangedEvent();
        }

        public bool SmallMoleculeLabelsTabEnabled
        {
            get { return tabControl1.TabPages.ContainsKey(@"tabLabels"); }
        }

        public void EditLabelTypeList()
        {
            CheckDisposed();
            _driverLabelType.EditList();
        }

        private void btnEditSmallMoleculeInternalStandards_Click(object sender, EventArgs e)
        {
            EditSmallMoleculeInternalStandards();
        }

        public void EditSmallMoleculeInternalStandards()
        {
            _driverSmallMolInternalStandardTypes.EditList();
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

        /// <summary>
        /// Update the Items in comboNormalizationMethod to include not only the options that were available when this dialog
        /// first came up, but also those options that would be available if the user were to OK the dialog right now.
        /// </summary>
        private void UpdateComboNormalizationMethod()
        {
            var currentNormalizationMethod = 
                comboNormalizationMethod.SelectedItem as NormalizationMethod 
                ?? _parent.DocumentUI.Settings.PeptideSettings.Quantification.NormalizationMethod
                ?? NormalizationMethod.NONE;

            IEnumerable<NormalizationMethod> availableNormalizationMethods = NormalizationMethod.ListNormalizationMethods(_parent.DocumentUI);

            // If the user has checked any isotope modifications, then some new ratio to label options may be available
            if (_driverLabelType != null && _driverLabelType.GetHeavyModifications().Any(mods=>mods.Modifications.Count > 0))
            {
                IEnumerable<IsotopeLabelType> ratioInternalStandardTypes = SmallMoleculeLabelsTabEnabled
                    ? _driverSmallMolInternalStandardTypes.InternalStandardTypes
                    : _driverLabelType.InternalStandardTypes;
                if (!ratioInternalStandardTypes.Any())
                {
                    // Duplicate the logic of "PeptideModifications.RatioInternalStandardTypes": if none of the isotope label types are internal standards,
                    // then all heavy label types are available for normalization
                    ratioInternalStandardTypes = _driverLabelType.GetHeavyModifications().Select(mods => mods.LabelType);
                }

                availableNormalizationMethods = availableNormalizationMethods.Concat(
                    ratioInternalStandardTypes.Select(NormalizationMethod.GetNormalizationMethod));
            }

            var newComboItems = availableNormalizationMethods.Distinct().ToList();
            if (!newComboItems.Contains(currentNormalizationMethod))
            {
                newComboItems.Add(currentNormalizationMethod);
            }

            if (newComboItems.SequenceEqual(comboNormalizationMethod.Items.OfType<object>()))
            {
                return;
            }

            comboNormalizationMethod.Items.Clear();
            comboNormalizationMethod.Items.AddRange(newComboItems.ToArray());
            comboNormalizationMethod.SelectedItem = currentNormalizationMethod;
        }

        private void tabControl1_TabIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == tabQuantification)
            {
                UpdateComboNormalizationMethod();
            }
        }
        #region Functional testing support

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = tabControl1.SelectedIndex));
                return ControlIndexToTabPage(selectedIndex);
            }
        }

        public TABS SelectedTab
        {
            get { return ControlIndexToTabEnum(tabControl1.SelectedIndex); }
            set { tabControl1.SelectedIndex = TabEnumToControlIndex(value); }
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
            get { return _driverLibrary.Choices.Select(c => c.Name).ToArray(); }
        }

        public string[] PickedLibraries
        {
            get { return _driverLibrary.CheckedNames; }
            set { _driverLibrary.CheckedNames = value; }
        }

        public LibrarySpec[] PickedLibrarySpecs
        {
            get { return _driverLibrary.Chosen; }
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
            set { _driverBackgroundProteome.Combo.SelectedItem = value; }
        }

        public IEnumerable<string> ListBackgroundProteomes
        {
            get { return _driverBackgroundProteome.Combo.Items.Cast<object>().Select(item => item.ToString()); }
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

        public PeptideFilter.PeptideUniquenessConstraint ComboPeptideUniquenessConstraintSelected
        {
            get { return (PeptideFilter.PeptideUniquenessConstraint)comboBoxPeptideUniquenessConstraint.SelectedIndex; }
            set { comboBoxPeptideUniquenessConstraint.SelectedIndex = (int)value; }
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

        public double? QuantMaxLoqBias
        {
            get {
                if (tbxMaxLoqBias.Text.Trim().Length == 0)
                {
                    return null;
                }
                return double.Parse(tbxMaxLoqBias.Text.Trim());
            }
            set { tbxMaxLoqBias.Text = value.ToString(); }
        }

        public double? QuantMaxLoqCv
        {
            get
            {
                if (tbxMaxLoqCv.Text.Trim().Length == 0)
                {
                    return null;
                }
                return double.Parse(tbxMaxLoqCv.Text.Trim());
            }
            set { tbxMaxLoqCv.Text = value.ToString(); }
        }

        public double? IonRatioThreshold
        {
            get
            {
                var text = tbxIonRatioThreshold.Text.Trim();
                return string.IsNullOrEmpty(text) ? (double?) null : double.Parse(text);
            }
            set
            {
                tbxIonRatioThreshold.Text = value.ToString();
            }
        }

        public bool SimpleRatios
        {
            get
            {
                return cbxSimpleRatios.Checked;
            }
            set
            {
                cbxSimpleRatios.Checked = value;
            }
        }

        public LodCalculation QuantLodMethod
        {
            get { return comboLodMethod.SelectedItem as LodCalculation; }
            set { comboLodMethod.SelectedItem = value; }
        }

        #endregion

        public sealed class LabelTypeComboDriver
        {
            private readonly SettingsListBoxDriver<StaticMod> _driverHeavyMod;

            private int _selectedIndexLast;
            private int SafeSelectedIndexLast
            {
                get
                {
                    return _selectedIndexLast < 0 ? 0 :
                        _selectedIndexLast >= Combo.Items.Count ? Combo.Items.Count - 1 :
                        _selectedIndexLast;
                }
            }
            private bool _singleStandard;
            public UsageType Usage { get; }

            public enum UsageType { ModificationsPicker, InternalStandardPicker, InternalStandardListMaintainer }

            public LabelTypeComboDriver(UsageType usageType, ComboBox combo,
                                        PeptideModifications modifications,
                                        SettingsListBoxDriver<StaticMod> driverHeavyMod,
                                        Label labelIS,
                                        ComboBox comboIS,
                                        CheckedListBox listBoxIS)
            {
                Usage = usageType;
                _driverHeavyMod = driverHeavyMod;

                LabelIS = labelIS;
                Combo = combo;
                Combo.DisplayMember = SettingsUIResources.LabelTypeComboDriver_LabelTypeComboDriver_LabelType;
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
                    if (Usage == UsageType.InternalStandardPicker)
                        return null; // We're using this from the EditCustomMolecule dialog

                    if (_singleStandard && Usage == UsageType.ModificationsPicker)
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

                    if (Usage == UsageType.InternalStandardPicker) 
                    {
                        // Using this from the Edit Molecule dialog, we want to see Light in this list
                        Combo.Items.Add(new TypedModifications(IsotopeLabelType.light, new List<StaticMod>()));
                    }
                    else
                    {
                        _singleStandard = (heavyMods.Count <= 1);
                        if (_singleStandard && Usage == UsageType.ModificationsPicker && ComboIS != null)
                        {
                            LabelIS.Text = SettingsUIResources.LabelTypeComboDriver_LoadList_Internal_standard_type;
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
                            LabelIS.Text = SettingsUIResources.LabelTypeComboDriver_LoadList_Internal_standard_types;
                            ListBoxIS.Items.Clear();
                            ListBoxIS.Items.Add(IsotopeLabelType.light);
                            if (internalStandardTypes.Contains(IsotopeLabelType.light))
                                ListBoxIS.SetItemChecked(0, true);
                            ListBoxIS.Visible = true;
                            if (ComboIS != null)
                            {
                                ComboIS.Visible = false;
                                ListBoxIS.Parent.Parent.Parent.Height +=
                                    ListBoxIS.Bottom + BORDER_BOTTOM_HEIGHT - ComboIS.Parent.Height;
                            }
                        }
                    }
                    foreach (var typedMods in heavyMods)
                    {
                        string labelName = typedMods.LabelType.Name;

                        int i = Combo.Items.Add(typedMods);
                        if (Equals(typedMods.LabelType.Name, selectedItemLast))
                            Combo.SelectedIndex = i;

                        if (Usage != UsageType.InternalStandardPicker) // We may be using this from the Edit Molecule dialog, which is less complex
                        {
                            if (_singleStandard && Usage == UsageType.ModificationsPicker && ComboIS != null)
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

                    Combo.Items.Add(SettingsUIResources.LabelTypeComboDriver_LoadList_Edit_list);
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
                        if (Combo.Items[i] is TypedModifications && // Watch out for "Edit List"
                            Equals(value, ((TypedModifications)Combo.Items[i]).LabelType.Name))
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
                        if (ComboIS.Items[i] is IsotopeLabelType && // Watch out for "Edit List"
                            Equals(value, ((IsotopeLabelType)ComboIS.Items[i]).Name))
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
                get { return (TypedModifications)Combo.Items[SafeSelectedIndexLast]; }
            }

            public IEnumerable<TypedModifications> GetHeavyModifications()
            {
                UpdateLastSelected();
                // If we are using this in the Add Custom Molecule dialog, we will have added the "light" label to the pick list - ignore here
                return Combo.Items.OfType<TypedModifications>().Where(x => x.LabelType.Name != IsotopeLabelType.light.Name);
            }

            private bool EditListSelected()
            {
                return (SettingsUIResources.LabelTypeComboDriver_LoadList_Edit_list == Combo.SelectedItem.ToString());
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
                    Combo.Items[SafeSelectedIndexLast] =
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
                using (var dlg = new EditLabelTypeListDlg())
                {
                    dlg.LabelTypes = from typedMods in heavyMods
                        select typedMods.LabelType;
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
                        Combo.SelectedIndex = SafeSelectedIndexLast;
                    }
                }
            }
        }

        public PeptidePick PeptidePick
        {
            get { return (PeptidePick) comboMatching.SelectedIndex; }
            set { comboMatching.SelectedIndex = (int) value; }
        }
        private void ChangeTooltip(Control control, string newToolTip)
        {
            if (helpTip.GetToolTip(control) != newToolTip)
            {
                helpTip.SetToolTip(control, newToolTip);
            }
        }
        
        private void listStaticMods_MouseMove(object sender, MouseEventArgs e)
        {
            var itemIndex = listStaticMods.IndexFromPoint(e.Location);
            StaticMod staticMod = null;
            if (itemIndex >= 0 && itemIndex < _driverStaticMod.Choices.Length)
            {
                staticMod = _driverStaticMod.Choices[itemIndex];
            }
            // Remember the original tooltips which were set in the form designer.
            // The original tooltip is displayed when the mouse is not pointing at any item in the list
            if (string.IsNullOrEmpty(_staticModsOriginalTooltip))
            {
                _staticModsOriginalTooltip = helpTip.GetToolTip(listStaticMods);
            }
            ChangeTooltip(listStaticMods, staticMod?.ItemDescription.ToString() ?? _staticModsOriginalTooltip);
        }

        private void listHeavyMods_MouseMove(object sender, MouseEventArgs e)
        {
            var itemIndex = listHeavyMods.IndexFromPoint(e.Location);
            StaticMod heavyMod = null;
            if (itemIndex >= 0 && itemIndex < _driverHeavyMod.Choices.Length)
            {
                heavyMod = _driverHeavyMod.Choices[itemIndex];
            }
            // Remember the original tooltips which were set in the form designer.
            // The original tooltip is displayed when the mouse is not pointing at any item in the list
            if (string.IsNullOrEmpty(_heavyModsOriginalTooltip))
            {
                _heavyModsOriginalTooltip = helpTip.GetToolTip(listHeavyMods);
            }
            ChangeTooltip(listHeavyMods, heavyMod?.ItemDescription.ToString() ?? _heavyModsOriginalTooltip);
        }

        private void listLibraries_MouseMove(object sender, MouseEventArgs e)
        {
            var itemIndex = listLibraries.IndexFromPoint(e.Location);
            LibrarySpec librarySpec = null;
            if (itemIndex >= 0 && itemIndex < _driverLibrary.Choices.Length)
            {
                librarySpec = _driverLibrary.Choices[itemIndex];
            }
            // Remember the original tooltips which were set in the form designer.
            // The original tooltip is displayed when the mouse is not pointing at any item in the list
            if (string.IsNullOrEmpty(_librariesOriginalTooltip))
            {
                _librariesOriginalTooltip = helpTip.GetToolTip(listLibraries);
            }
            ChangeTooltip(listLibraries, librarySpec?.ItemDescription?.ToString() ?? _librariesOriginalTooltip);
        }

        private void comboLodMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            tbxMaxLoqBias.Enabled = comboLodMethod.SelectedItem != LodCalculation.TURNING_POINT_STDERR;
        }

        private void comboRegressionFit_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateLodOptions(comboLodMethod.SelectedItem as LodCalculation);

            // If the user chooses "Bilinear" for the regression method, then
            // change the LodCalculation and Max LOQ CV to the recommended values
            if (comboRegressionFit.SelectedItem == RegressionFit.BILINEAR &&
                _peptideSettings.Quantification.RegressionFit != RegressionFit.BILINEAR)
            {
                if (LodCalculation.NONE.Equals(comboLodMethod.SelectedItem))
                {
                    comboLodMethod.SelectedItem = LodCalculation.TURNING_POINT_STDERR;
                }
                if (!QuantMaxLoqCv.HasValue)
                {
                    QuantMaxLoqCv = 20;
                }
            }
        }

        private void UpdateLodOptions(LodCalculation current)
        {
            var  options = new List<object>();
            options.AddRange(LodCalculation.ForRegressionFit(comboRegressionFit.SelectedItem as RegressionFit));
            comboLodMethod.Items.Clear();
            comboLodMethod.Items.AddRange(options.ToArray());
            if (options.Contains(current))
            {
                comboLodMethod.SelectedItem = current;
            }
            else
            {
                comboLodMethod.SelectedItem = LodCalculation.NONE;
            }
            ComboHelper.AutoSizeDropDown(comboLodMethod);
        }
    }
}