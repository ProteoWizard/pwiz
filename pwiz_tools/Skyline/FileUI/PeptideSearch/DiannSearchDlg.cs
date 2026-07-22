/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Parquet;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class DiannSearchDlg : FormEx, IModifyDocumentContainer, IMultipleViewProvider
    {
        public SkylineWindow SkylineWindow { get; set; }
        public DiannSearchControl SearchControl { get; private set; }

        public enum Pages
        {
            data_files_page,
            fasta_page,
            modifications_page,
            search_settings_page,
            run_page
        }

        public class DataFilesPage : IFormView { }
        public class FastaPage : IFormView { }
        public class ModificationsPage : IFormView { }
        public class SearchSettingsPage : IFormView { }
        public class RunPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new DataFilesPage(), new FastaPage(), new ModificationsPage(), new SearchSettingsPage(), new RunPage()
        };

        public DiannSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager)
        {
            SkylineWindow = skylineWindow;
            _libraryManager = libraryManager;
            _documents = new Stack<SrmDocument>();
            SetDocument(skylineWindow.Document, null);

            InitializeComponent();

            Icon = SkylineWindow.Icon;

            DataFileResults = new ImportResultsDIAControl(this);
            AddPageControl(DataFileResults, dataFilesPage, 2, 60);

            ImportFastaControl = new ImportFastaControl(this, skylineWindow.SequenceTree, false);
            ImportFastaControl.IsDDASearch = true;
            AddPageControl(ImportFastaControl, fastaPage, 2, 60);

            SearchControl = new DiannSearchControl(this);
            AddPageControl(SearchControl, runSearchPage, 2, 0);
            SearchControl.SearchFinished += SearchControlSearchFinished;

            // Set default config values
            numMs1Tolerance.Value = 0; // auto-detect
            numMs2Tolerance.Value = 0; // auto-detect
            numThreads.Maximum = MAX_THREAD_COUNT;
            numThreads.Value = Math.Min(Environment.ProcessorCount, MAX_THREAD_COUNT);

            // Populate modifications page with common defaults
            InitializeModifications();

            // Add a preset dropdown to the dialog footer next to the wizard buttons.
            InitSettingsPresetControls();
        }

        private ComboBox _cbSettingsPreset;
        private Button _btnSavePreset;
        private SettingsListComboDriver<SearchSettingsPreset> _settingsPresetDriver;
        private bool _suppressPresetEvent;

        private void InitSettingsPresetControls()
        {
            // Anchor the controls bottom-left of the dialog, in the same horizontal band
            // as the Back/Next/Cancel buttons.
            var lbl = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(12, btnBack.Top + 4),
                Text = PeptideSearchResources.DiannSearchDlg_SettingsPreset_Label
            };
            _cbSettingsPreset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Width = 220,
                Location = new Point(lbl.Right + 6, btnBack.Top)
            };
            _btnSavePreset = new Button
            {
                Text = PeptideSearchResources.DiannSearchDlg_SettingsPreset_Save,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                AutoSize = true,
                Location = new Point(_cbSettingsPreset.Right + 6, btnBack.Top - 1)
            };
            Controls.Add(lbl);
            Controls.Add(_cbSettingsPreset);
            Controls.Add(_btnSavePreset);

            _settingsPresetDriver = new SettingsListComboDriver<SearchSettingsPreset>(
                _cbSettingsPreset, Settings.Default.DiannSearchSettingsPresets, true);
            _suppressPresetEvent = true;
            try { _settingsPresetDriver.LoadList(null); }
            finally { _suppressPresetEvent = false; }

            _cbSettingsPreset.SelectedIndexChanged += (s, e) =>
            {
                if (_settingsPresetDriver.SelectedIndexChangedEvent(s, e))
                    return;
                var preset = _settingsPresetDriver.SelectedItem;
                if (preset != null && !_suppressPresetEvent && preset.SearchEngine == SearchEngine.DIANN)
                    ApplyPreset(preset);
            };
            _btnSavePreset.Click += (s, e) => SaveCurrentSettingsAsPreset();
        }

        private void SaveCurrentSettingsAsPreset()
        {
            var current = _settingsPresetDriver.SelectedItem;
            var suggested = current != null && current.SearchEngine == SearchEngine.DIANN
                ? current.Name
                : @"DIA-NN - ";
            string name;
            using (var dlg = new PresetNameDlg(suggested))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                name = dlg.PresetName;
            }
            if (string.IsNullOrWhiteSpace(name))
                return;

            var existing = Settings.Default.DiannSearchSettingsPresets.FirstOrDefault(p => p.Name == name);
            if (existing != null)
            {
                var result = MessageDlg.Show(this,
                    string.Format(PeptideSearchResources.SearchSettingsControl_OverwriteSettingsPreset_A_settings_preset_named__0__already_exists__Do_you_want_to_replace_it_, name),
                    false, MessageBoxButtons.YesNo);
                if (result != DialogResult.Yes)
                    return;
                Settings.Default.DiannSearchSettingsPresets.Remove(existing);
            }
            Settings.Default.DiannSearchSettingsPresets.Add(BuildPresetFromCurrentSettings(name));
            // Persist immediately so the preset survives even if Skyline isn't closed
            // cleanly. Settings.Default.Save() at SkylineWindow.OnClosing should pick it
            // up too, but the user-reported intermittent reset suggests we shouldn't rely
            // on that alone.
            try { Settings.Default.Save(); }
            catch { /* swallow — the in-memory list still has the preset for this session */ }
            _suppressPresetEvent = true;
            try { _settingsPresetDriver.LoadList(name); }
            finally { _suppressPresetEvent = false; }
        }

        private void InitializeModifications()
        {
            // Default fixed mod: Carbamidomethyl (C)
            var defaultFixed = UniMod.GetModification(@"Carbamidomethyl (C)", true);
            if (defaultFixed != null)
                listFixedMods.Items.Add(defaultFixed);

            // Default variable mod: Oxidation (M)
            var defaultVar = UniMod.GetModification(@"Oxidation (M)", true);
            if (defaultVar != null)
                listVariableMods.Items.Add(defaultVar);
        }

        public IEnumerable<StaticMod> FixedMods => listFixedMods.Items.Cast<StaticMod>();
        public IEnumerable<StaticMod> VariableMods => listVariableMods.Items.Cast<StaticMod>();

        private void btnAddFixedMod_Click(object sender, EventArgs e)
        {
            AddModification(listFixedMods);
        }

        private void btnAddVariableMod_Click(object sender, EventArgs e)
        {
            AddModification(listVariableMods);
        }

        private void btnRemoveFixedMod_Click(object sender, EventArgs e)
        {
            RemoveSelectedMod(listFixedMods);
        }

        private void btnRemoveVariableMod_Click(object sender, EventArgs e)
        {
            RemoveSelectedMod(listVariableMods);
        }

        private void AddModification(ListBox listBox)
        {
            var existing = new HashSet<string>(listFixedMods.Items.Cast<StaticMod>().Select(m => m.Name)
                .Concat(listVariableMods.Items.Cast<StaticMod>().Select(m => m.Name)));
            var newMod = Settings.Default.StaticModList.EditItem(this, null, Settings.Default.StaticModList, null);
            if (newMod != null && !existing.Contains(newMod.Name))
                listBox.Items.Add(newMod);
        }

        private static void RemoveSelectedMod(ListBox listBox)
        {
            if (listBox.SelectedIndex >= 0)
                listBox.Items.RemoveAt(listBox.SelectedIndex);
        }

        public ImportResultsDIAControl DataFileResults { get; set; }
        public ImportFastaControl ImportFastaControl { get; set; }

        public double Ms1Tolerance
        {
            get => (double)numMs1Tolerance.Value;
            set => numMs1Tolerance.Value = (decimal)value;
        }

        public double Ms2Tolerance
        {
            get => (double)numMs2Tolerance.Value;
            set => numMs2Tolerance.Value = (decimal)value;
        }

        public double QValueThreshold
        {
            get => double.TryParse(txtQValue.Text, out var v) ? v : 0.01;
            set => txtQValue.Text = value.ToString(@"G");
        }

        public const int MAX_THREAD_COUNT = 24;

        public int Threads
        {
            get => (int)numThreads.Value;
            set => numThreads.Value = Math.Min(value, MAX_THREAD_COUNT);
        }

        private readonly DiannConfig _diannConfig = new DiannConfig();

        public DiannConfig DiannSearchConfig
        {
            get
            {
                _diannConfig.Ms1Accuracy = Ms1Tolerance;
                _diannConfig.Ms2Accuracy = Ms2Tolerance;
                _diannConfig.QValue = QValueThreshold;
                _diannConfig.Threads = Threads;
                _diannConfig.MetExcision = cbMetExcision.Checked;
                _diannConfig.MaxMissedCleavages = (int)numMissedCleavages.Value;
                _diannConfig.ApplyAdditionalSettings();
                return _diannConfig;
            }
        }

        public void ShowAdditionalSettings()
        {
            KeyValueGridDlg.Show(this,
                PeptideSearchResources.SearchSettingsControl_Additional_Settings,
                _diannConfig.AdditionalSettings,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues);
        }

        /// <summary>
        /// Apply a preset's settings to this dialog's controls. Engine-specific values
        /// (peptide length, charge range) are pulled from the preset's AdditionalSettings
        /// XML and applied to <see cref="DiannConfig.AdditionalSettings"/>; tolerances,
        /// q-value, enzyme, FASTA, missed cleavages, and modifications map directly.
        /// Fields without a value in the preset are left at the user's current setting.
        /// </summary>
        public void ApplyPreset(SearchSettingsPreset preset)
        {
            if (preset == null)
                return;

            // Tolerances (preset value == 0 means "auto" in DIA-NN, which is what we want).
            // DIA-NN only accepts ppm tolerances; reject anything else rather than
            // silently feeding a Dalton magnitude through as ppm.
            if (preset.PrecursorToleranceUnit != MzTolerance.Units.ppm ||
                preset.FragmentToleranceUnit != MzTolerance.Units.ppm)
            {
                MessageDlg.Show(this, string.Format(
                    PeptideSearchResources.DiannSearchDlg_ApplyPreset_DiaNN_requires_ppm_tolerances_,
                    preset.Name));
                return;
            }
            Ms1Tolerance = preset.PrecursorToleranceValue;
            Ms2Tolerance = preset.FragmentToleranceValue;

            QValueThreshold = preset.CutoffScore > 0 ? preset.CutoffScore : 0.01;

            // FASTA / enzyme / missed cleavages — ImportFastaControl owns these.
            // Guard against a preset that references a FASTA the local filesystem doesn't
            // have (shared user.config, moved file): SetFastaContent throws from inside
            // SelectedIndexChanged, producing a modal popup in a re-entrant message loop.
            // Leave the field empty; the wizard's own validation flags it non-modally.
            if (!string.IsNullOrEmpty(preset.FastaFilePath) && File.Exists(preset.FastaFilePath))
                ImportFastaControl.SetFastaContent(preset.FastaFilePath, true);
            if (!string.IsNullOrEmpty(preset.EnzymeName))
            {
                // EnzymeList is keyed by Enzyme.GetKey() (e.g. "Trypsin KR | P"), NOT by
                // Name, and Settings.Default.GetEnzymeByName silently falls back to
                // EnzymeList[0] on a key miss. So match by Name explicitly to avoid
                // picking up whichever enzyme happens to be at the top of the list.
                var enzyme = Settings.Default.EnzymeList.FirstOrDefault(e => e.Name == preset.EnzymeName)
                             ?? new EnzymeList().GetDefaults(0).FirstOrDefault(e => e.Name == preset.EnzymeName);
                if (enzyme != null)
                {
                    if (Settings.Default.EnzymeList.All(e => e.Name != enzyme.Name))
                        Settings.Default.EnzymeList.Add(enzyme);
                    ImportFastaControl.Enzyme = enzyme;
                }
            }
            numMissedCleavages.Value = Math.Min(numMissedCleavages.Maximum,
                Math.Max(numMissedCleavages.Minimum, preset.MaxMissedCleavages));
            ImportFastaControl.MaxMissedCleavages = preset.MaxMissedCleavages;

            // Modifications — preset stores full StaticMod definitions, so we can rebuild
            // the lists directly. Anything in the preset's structural list with IsVariable
            // becomes a variable mod; the rest are fixed.
            if (preset.HasExplicitModifications)
            {
                listFixedMods.Items.Clear();
                listVariableMods.Items.Clear();
                foreach (var mod in preset.StructuralModifications ?? Enumerable.Empty<StaticMod>())
                {
                    if (mod.IsVariable)
                        listVariableMods.Items.Add(mod);
                    else
                        listFixedMods.Items.Add(mod);
                }
            }

            // First-class preset fields that don't have a UI control yet.
            _diannConfig.MaxVarMods = preset.MaxVariableMods;

            // Engine-specific settings (MinPepLen, charge range, etc.) — replay the XML bag
            // into the in-memory DiannConfig.
            ApplyAdditionalSettingsFromXml(preset.AdditionalSettingsXml);
        }

        private void ApplyAdditionalSettingsFromXml(string additionalSettingsXml)
        {
            if (string.IsNullOrEmpty(additionalSettingsXml))
                return;
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(additionalSettingsXml);
            var nodes = doc.SelectNodes(@"//Setting");
            if (nodes == null)
                return;
            foreach (System.Xml.XmlNode node in nodes)
            {
                var settingName = node.Attributes?[@"name"]?.Value;
                var settingValue = node.Attributes?[@"value"]?.Value;
                if (settingName != null &&
                    _diannConfig.AdditionalSettings.TryGetValue(settingName, out var setting))
                {
                    try { setting.Value = settingValue; }
                    catch { /* leave at default if the preset's value doesn't validate */ }
                }
            }
        }

        /// <summary>
        /// Snapshot the dialog's current state into a new preset so the user can save it.
        /// </summary>
        public SearchSettingsPreset BuildPresetFromCurrentSettings(string name)
        {
            var additional = new Dictionary<string, string>();
            foreach (var kvp in _diannConfig.AdditionalSettings)
            {
                if (!kvp.Value.IsDefault)
                    additional[kvp.Key] = kvp.Value.Value?.ToString() ?? string.Empty;
            }

            // Stamp IsVariable on each mod so ApplyPreset's `mod.IsVariable`-based
            // bucketing re-creates the right list. The dialog's FixedMods / VariableMods
            // lists are sourced from UniMod.GetModification(...), which leaves
            // StaticMod.IsVariable=false regardless of which list the user dropped
            // it in. Without ChangeVariable here, every variable mod silently round-trips
            // into the fixed-mod list.
            var stampedMods = FixedMods.Select(m => m.IsVariable ? m.ChangeVariable(false) : m)
                .Concat(VariableMods.Select(m => m.IsVariable ? m : m.ChangeVariable(true)));

            return new SearchSettingsPreset(
                name,
                SearchEngine.DIANN,
                new MzTolerance(Ms1Tolerance, MzTolerance.Units.ppm),
                new MzTolerance(Ms2Tolerance, MzTolerance.Units.ppm),
                maxVariableMods: _diannConfig.MaxVarMods,
                fragmentIons: null,
                ms2Analyzer: null,
                cutoffScore: QValueThreshold,
                additionalSettings: additional,
                fastaFilePath: ImportFastaControl?.FastaFile,
                enzymeName: ImportFastaControl?.Enzyme?.Name,
                maxMissedCleavages: (int)numMissedCleavages.Value,
                structuralModifications: stampedMods,
                workflowType: SearchWorkflowType.dia,
                hasExplicitModifications: true);
        }

        #region IMultipleViewProvider
        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = wizardPages.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }
        #endregion

        #region IModifyDocumentContainer
        private readonly LibraryManager _libraryManager;
        private readonly Stack<SrmDocument> _documents;

        public SrmDocument Document => _documents.Count == 0 ? null : _documents.Peek();

        public string DocumentFilePath
        {
            get => SkylineWindow.DocumentFilePath;
            set => SkylineWindow.DocumentFilePath = value;
        }

        public bool SetDocument(SrmDocument docNew, SrmDocument docOriginal)
        {
            if (!ReferenceEquals(Document, docOriginal))
                return false;

            _documents.Push(docNew);
            return true;
        }

        public void Listen(EventHandler<DocumentChangedEventArgs> listener) =>
            ((IDocumentContainer)SkylineWindow).Listen(listener);
        public void Unlisten(EventHandler<DocumentChangedEventArgs> listener) =>
            ((IDocumentContainer)SkylineWindow).Unlisten(listener);
        public bool IsClosing => ((IDocumentContainer)SkylineWindow).IsClosing;
        public IEnumerable<BackgroundLoader> BackgroundLoaders =>
            ((IDocumentContainer)SkylineWindow).BackgroundLoaders;
        public void AddBackgroundLoader(BackgroundLoader loader) =>
            ((IDocumentContainer)SkylineWindow).AddBackgroundLoader(loader);
        public void RemoveBackgroundLoader(BackgroundLoader loader) =>
            ((IDocumentContainer)SkylineWindow).RemoveBackgroundLoader(loader);

        public void ModifyDocumentNoUndo(Func<SrmDocument, SrmDocument> act)
        {
            ModifyDocument(null, act, null);
        }

        public void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act, Func<SrmDocumentPair, AuditLogEntry> logFunc)
        {
            var docNew = act(Document);
            if (ReferenceEquals(docNew, Document))
                return;

            SetDocument(docNew, Document);
        }
        #endregion

        public Pages CurrentPage => (Pages)wizardPages.SelectedIndex;

        public void NextPage()
        {
            btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;

            if (CurrentPage == Pages.run_page)
            {
                ImportDiannLibrary();
                return;
            }

            // Validate current page before advancing
            switch (CurrentPage)
            {
                case Pages.data_files_page:
                    if (!DataFileResults.FoundResultsFiles.Any())
                    {
                        MessageDlg.Show(this, PeptideSearchResources.DiannSearchDlg_NextPage_Please_add_at_least_one_DIA_data_file_);
                        return;
                    }
                    break;
                case Pages.fasta_page:
                    if (!File.Exists(ImportFastaControl.FastaFile))
                    {
                        MessageDlg.Show(this, PeptideSearchResources.DiannSearchDlg_NextPage_A_FASTA_file_is_required_for_a_DIA_NN_search_);
                        return;
                    }
                    break;
            }

            if (wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1)
                wizardPages.SelectTab(wizardPages.SelectedIndex + 1);

            switch (CurrentPage)
            {
                case Pages.search_settings_page:
                    btnNext.Text = PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_Run;
                    break;

                case Pages.run_page:
                    btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                    btnNext.Enabled = false;
                    btnCancel.Enabled = false;
                    btnBack.Enabled = false;
                    ControlBox = false;
                    SearchControl.DiannConfig = DiannSearchConfig;
                    SearchControl.FastaFilePath = ImportFastaControl.FastaFile;
                    SearchControl.DataFiles = DataFileResults.FoundResultsFiles.Select(f => f.Path).ToList();
                    SearchControl.FixedMods = FixedMods.ToList();
                    SearchControl.VariableMods = VariableMods.ToList();
                    SearchControl.RunSearch();
                    return;
            }

            btnBack.Enabled = wizardPages.SelectedIndex > 0;
            btnNext.Enabled = wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1;
        }

        private void ImportDiannLibrary()
        {
            string specLibPath = SearchControl.OutputSpecLibPath;

            if (string.IsNullOrEmpty(specLibPath) || !File.Exists(specLibPath))
            {
                MessageDlg.Show(this, PeptideSearchResources.DiannSearchDlg_ImportDiannLibrary_DIA_NN_output_spectral_library_not_found_);
                return;
            }

            // Gate on lib.parquet row count BEFORE handing off to BlibBuild. With zero
            // precursors, BlibBuild's abort_current_library path can't reliably delete
            // the empty .blib (unfinalized SQLite statements keep the handle open), and
            // the user sees a confusing "file in use" / STATUS_BREAKPOINT instead of the
            // real cause. Catch it here with a friendly message and leave the wizard on
            // run_page so the user can go back to settings and widen the q-value or
            // change parameters. A read failure (truncated/corrupt parquet) gets its
            // own message so the user isn't told to "widen the threshold" when the real
            // issue is that DIA-NN's output is unreadable.
            if (!TryCountDiannLibParquetRows(specLibPath, out long precursorRowCount))
            {
                MessageDlg.Show(this, string.Format(
                    PeptideSearchResources.DiannSearchDlg_ImportDiannLibrary_DiaNN_lib_unreadable_,
                    specLibPath));
                return;
            }
            if (precursorRowCount == 0)
            {
                MessageDlg.Show(this, string.Format(
                    PeptideSearchResources.DiannSearchDlg_ImportDiannLibrary_DiaNN_zero_precursors_,
                    _diannConfig.QValue));
                return;
            }

            // If the document already has a sibling .blib from a previous search, the wizard
            // would silently append to it. Ask the user instead so a re-run replaces the
            // stale library cleanly.
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
            if (File.Exists(docBlibPath))
            {
                var choice = MultiButtonMsgDlg.Show(this,
                    string.Format(PeptideSearchResources.DiannSearchDlg_ImportDiannLibrary_Existing_blib_prompt__0_, docBlibPath),
                    PeptideSearchResources.DiannSearchDlg_ImportDiannLibrary_Overwrite,
                    PeptideSearchResources.DiannSearchDlg_ImportDiannLibrary_Append,
                    true);
                if (choice == DialogResult.Cancel)
                    return;
                if (choice == DialogResult.Yes)
                {
                    // Close the doc's open read-streams and evict the cached Library
                    // object from the LibraryManager so SafeDelete can actually unlink
                    // the .blib. (LibraryManager.ReleaseLibraries now closes streams
                    // as part of eviction.) Without both, BlibBuild silently falls
                    // back to Append mode and the previous run's spectrum sources
                    // stick around as "missing" on the chromatograms page.
                    ImportPeptideSearch.ClosePeptideSearchLibraryStreams(SkylineWindow.Document);
                    var docLibSpecs = SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs
                        .Where(s => s != null && s.IsDocumentLibrary).ToArray();
                    if (docLibSpecs.Length > 0)
                        _libraryManager.ReleaseLibraries(docLibSpecs);
                    FileEx.SafeDelete(docBlibPath, true);
                    FileEx.SafeDelete(BiblioSpecLiteSpec.GetRedundantName(docBlibPath), true);
                }
            }

            // Launch Import Peptide Search dialog. The DIA-NN -lib.parquet is passed as a
            // search file so BiblioSpec builds a .blib from it via DiaNNSpecLibReader.
            using var importPeptideSearchDlg = new ImportPeptideSearchDlg(SkylineWindow, _libraryManager,
                false, ImportPeptideSearchDlg.Workflow.dia);
            importPeptideSearchDlg.BuildPepSearchLibControl.ForceWorkflow(ImportPeptideSearchDlg.Workflow.dia);
            importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new[] { specLibPath });
            importPeptideSearchDlg.ImportFastaControl.SetFastaContent(ImportFastaControl.ImportSettings.FastaFile.Path, true);
            importPeptideSearchDlg.ImportFastaControl.Enzyme = ImportFastaControl.ImportSettings.Enzyme;
            importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = ImportFastaControl.ImportSettings.MaxMissedCleavages;

            // Pre-populate the chromatograms page with the data files we just searched, so
            // the user doesn't have to re-browse for files we already know the paths of.
            // PrefillFoundResultsFiles uses the BlibBuild-compatible key + display name so
            // the doc-library scan in InitializeSpectrumSourceFiles doesn't create duplicates.
            importPeptideSearchDlg.PrefillFoundResultsFiles(DataFileResults.FoundResultsFiles
                .Select(f => new ImportPeptideSearch.FoundResultsFile(f.Name, f.Path)));

            if (importPeptideSearchDlg.ShowDialog(this) == DialogResult.OK)
                DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// Try to sum row counts across all row groups in the given parquet file.
        /// Returns true on a successful read (rows = 0 means the parquet exists but
        /// holds no precursors); returns false on any read failure (truncated file,
        /// I/O error, malformed parquet). The caller surfaces a distinct message for
        /// each case so the user can tell "no IDs" from "broken file" apart.
        /// </summary>
        /// <remarks>
        /// Runs on a worker thread via <see cref="ActionUtil.RunAsync"/> because
        /// ParquetReader.CreateAsync's continuation posts back to the calling
        /// SynchronizationContext — invoking it directly on the UI thread deadlocks,
        /// since the UI thread is the very thing the continuation is waiting for.
        /// </remarks>
        private static bool TryCountDiannLibParquetRows(string parquetPath, out long rowCount)
        {
            long result = 0;
            bool ok = false;
            var worker = ActionUtil.RunAsync(() =>
            {
                try
                {
                    using var stream = File.OpenRead(parquetPath);
                    using var reader = ParquetReader.CreateAsync(stream)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    result = reader.RowGroups.Sum(rg => rg.RowCount);
                    ok = true;
                }
                catch
                {
                    // Leave ok=false so the caller shows the "unreadable" message.
                }
            }, @"DiannParquetRowCount");
            worker.Join();
            rowCount = result;
            return ok;
        }

        public void PreviousPage()
        {
            if (wizardPages.SelectedIndex == 0)
                return;

            btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;

            if (CurrentPage == Pages.run_page)
                btnNext.Text = PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_Run;

            wizardPages.SelectTab(wizardPages.SelectedIndex - 1);

            btnBack.Enabled = wizardPages.SelectedIndex > 0;
            btnNext.Enabled = wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1;
        }

        private void SearchControlSearchFinished(bool success)
        {
            btnCancel.Enabled = true;
            btnBack.Enabled = true;
            ControlBox = true;
            if (success)
                btnNext.Enabled = true;
        }

        private void btnAdditionalSettings_Click(object sender, EventArgs e)
        {
            ShowAdditionalSettings();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            NextPage();
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            PreviousPage();
        }

        private static void AddPageControl<TControl>(TControl pageControl, TabPage tabPage, int border, int header)
            where TControl : UserControl
        {
            pageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pageControl.Location = new Point(border, header);
            pageControl.Width = tabPage.Width - border * 2;
            pageControl.Height = tabPage.Height - header - border;
            tabPage.Controls.Add(pageControl);
        }
    }

    public class DiannSearchControl : SearchControl
    {
        public DiannSearchDlg HostDialog { get; }

        public DiannSearchControl(DiannSearchDlg hostControl)
        {
            Parent = HostDialog = hostControl;
        }

        public DiannConfig DiannConfig { get; set; }
        public string FastaFilePath { get; set; }
        public List<string> DataFiles { get; set; }
        public List<StaticMod> FixedMods { get; set; }
        public List<StaticMod> VariableMods { get; set; }
        public string OutputSpecLibPath { get; private set; }

        private bool Search(CancellationTokenSource token, IProgressStatus status)
        {
            try
            {
                string outputDir = Path.GetDirectoryName(HostDialog.SkylineWindow.DocumentFilePath) ?? string.Empty;

                OutputSpecLibPath = DiannHelpers.RunSearch(
                    DataFiles, FastaFilePath, outputDir, DiannConfig,
                    this, ref status, token.Token,
                    FixedMods, VariableMods);

                if (OutputSpecLibPath == null)
                {
                    UpdateProgress(status.ChangeErrorException(
                        new IOException(PeptideSearchResources.DiannSearchControl_Search_DIA_NN_search_did_not_produce_a_spectral_library_output_)));
                    return false;
                }
            }
            catch (OperationCanceledException e)
            {
                UpdateProgress(status.ChangeWarningMessage(e.InnerException?.Message ?? e.Message));
                return false;
            }
            catch (Exception e)
            {
                UpdateProgress(status.ChangeErrorException(e));
                return false;
            }

            return !token.IsCancellationRequested;
        }

        public override void RunSearch()
        {
            txtSearchProgress.Text = string.Empty;
            _progressTextItems.Clear();
            btnCancel.Enabled = progressBar.Visible = true;

            _cancelToken = new CancellationTokenSource();

            ActionUtil.RunAsync(RunSearchAsync, @"DIA-NN Search thread");
        }

        public void RunSearchAsync()
        {
            IProgressStatus status = new ProgressStatus();
            bool success = true;

            if (!_cancelToken.IsCancellationRequested)
            {
                Invoke(new MethodInvoker(() => UpdateSearchEngineProgress(
                    status.ChangeMessage(PeptideSearchResources.DDASearchControl_SearchProgress_Starting_search))));

                success = Search(_cancelToken, status);

                Invoke(new MethodInvoker(() => UpdateSearchEngineProgressMilestone(status, success, status.SegmentCount,
                    Resources.DDASearchControl_SearchProgress_Search_canceled,
                    PeptideSearchResources.DDASearchControl_SearchProgress_Search_failed,
                    Resources.DDASearchControl_SearchProgress_Search_done)));
            }

            Invoke(new MethodInvoker(() =>
            {
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
                btnCancel.Enabled = false;
                OnSearchFinished(success);
            }));
        }
    }
}
