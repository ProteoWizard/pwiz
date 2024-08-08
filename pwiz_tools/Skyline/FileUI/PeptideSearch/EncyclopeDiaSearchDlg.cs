/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class EncyclopeDiaSearchDlg : FormEx, IModifyDocumentContainer, IAuditLogModifier<EncyclopeDiaSearchDlg.EncyclopeDiaSettings>, IMultipleViewProvider
    {
        public SkylineWindow SkylineWindow { get; set; }
        public EncyclopeDiaSearchControl SearchControl { get; private set; }

        public enum Pages
        {
            fasta_page,
            koina_page,
            narrow_window_page,
            wide_window_page,
            search_settings,
            run_page
        }

        public class FastaPage : IFormView { }
        public class KoinaPage : IFormView { }
        public class NarrowWindowPage : IFormView { }
        public class WideWindowPage : IFormView { }
        public class SearchSettingsPage : IFormView { }
        public class RunPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new FastaPage(), new KoinaPage(), new NarrowWindowPage(), new WideWindowPage(), new SearchSettingsPage(), new RunPage()
        };

        public EncyclopeDiaSearchDlg(SkylineWindow skylineWindow, LibraryManager libraryManager)
        {
            SkylineWindow = skylineWindow;
            _libraryManager = libraryManager;
            _documents = new Stack<SrmDocument>();
            SetDocument(skylineWindow.Document, null);

            InitializeComponent();

            Icon = SkylineWindow.Icon;

            ImportFastaControl = new ImportFastaControl(this, skylineWindow.SequenceTree, false);
            ImportFastaControl.IsDDASearch = true;
            AddPageControl(ImportFastaControl, fastaPage, 2, 60);

            NarrowWindowResults = new ImportResultsDIAControl(this);
            AddPageControl(NarrowWindowResults, narrowWindowPage, 2, 60);

            WideWindowResults = new ImportResultsDIAControl(this);
            AddPageControl(WideWindowResults, wideWindowPage, 2, 60);

            SearchControl = new EncyclopeDiaSearchControl(this);
            AddPageControl(SearchControl, runSearchPage, 2, 0);
            SearchControl.SearchFinished += SearchControlSearchFinished;

            ceCombo.Items.AddRange(Enumerable.Range(EncyclopeDiaHelpers.FastaToKoinaInputCsvConfig.MinNCE,
                EncyclopeDiaHelpers.FastaToKoinaInputCsvConfig.MaxNCE).Cast<object>().ToArray());
            ceCombo.SelectedIndex = ceCombo.Items.IndexOf(33);

            EncyclopeDiaAdditionalSettings = new EncyclopeDiaHelpers.EncyclopeDiaConfig();

            LoadMassUnitEntries();

            // set default tolerances from EncyclopeDia
            var defaultEncyclopeDiaConfig = EncyclopeDiaHelpers.EncyclopeDiaConfig.DEFAULT;
            EncyclopeDiaPrecursorTolerance = new MzTolerance(defaultEncyclopeDiaConfig.Ptol.Value,
                defaultEncyclopeDiaConfig.PtolUnits == EncyclopeDiaHelpers.EncyclopeDiaConfig.MassErrorType.PPM
                    ? MzTolerance.Units.ppm
                    : MzTolerance.Units.mz);
            EncyclopeDiaFragmentTolerance = new MzTolerance(defaultEncyclopeDiaConfig.Ftol.Value,
                defaultEncyclopeDiaConfig.FtolUnits == EncyclopeDiaHelpers.EncyclopeDiaConfig.MassErrorType.PPM
                    ? MzTolerance.Units.ppm
                    : MzTolerance.Units.mz);
        }

        public ImportFastaControl ImportFastaControl { get; set; }
        public ImportResultsDIAControl NarrowWindowResults { get; set; }
        public ImportResultsDIAControl WideWindowResults { get; set; }

        public EncyclopeDiaHelpers.FastaToKoinaInputCsvConfig KoinaSettings =>
            new EncyclopeDiaHelpers.FastaToKoinaInputCsvConfig
            {
                DefaultCharge = DefaultCharge,
                DefaultNCE = DefaultNCE,
                MinCharge = MinCharge,
                MaxCharge = MaxCharge,
                MinMz = MinMz,
                MaxMz = MaxMz,
                MaxMissedCleavage = ImportFastaControl.MaxMissedCleavages
            };

        public int DefaultCharge
        {
            get => Convert.ToInt32(defaultChargeUpDown.Value);
            set => defaultChargeUpDown.Value = value;
        }

        public int DefaultNCE
        {
            get => Convert.ToInt32(ceCombo.SelectedItem);
            set => ceCombo.SelectedItem = value;
        }

        public int MinCharge
        {
            get => Convert.ToInt32(minChargeUpDown.Value);
            set => minChargeUpDown.Value = value;
        }

        public int MaxCharge
        {
            get => Convert.ToInt32(maxChargeUpDown.Value);
            set => maxChargeUpDown.Value = value;
        }

        public int MaxMissedCleavage
        {
            get => Convert.ToInt32(defaultChargeUpDown.Value);
            set => ImportFastaControl.MaxMissedCleavages = value;
        }

        public double? MinMz
        {
            get => minMzCombo.Text.IsNullOrEmpty() ? null : (double?) Convert.ToDouble(minMzCombo.Text);
            set => minMzCombo.Text = value?.ToString(LocalizationHelper.CurrentCulture) ?? string.Empty;
        }

        public double? MaxMz
        {
            get => maxMzCombo.Text.IsNullOrEmpty() ? null : (double?) Convert.ToDouble(maxMzCombo.Text);
            set => maxMzCombo.Text = value?.ToString(LocalizationHelper.CurrentCulture) ?? string.Empty;
        }


        public EncyclopeDiaHelpers.EncyclopeDiaConfig EncyclopeDiaConfig
        {
            get
            {
                var result = EncyclopeDiaAdditionalSettings;

                result.Ptol = EncyclopeDiaPrecursorTolerance.Value;
                result.PtolUnits = EncyclopeDiaPrecursorTolerance.Unit == MzTolerance.Units.ppm
                    ? EncyclopeDiaHelpers.EncyclopeDiaConfig.MassErrorType.PPM
                    : EncyclopeDiaHelpers.EncyclopeDiaConfig.MassErrorType.AMU;
                result.Ftol = EncyclopeDiaFragmentTolerance.Value;
                result.FtolUnits = EncyclopeDiaFragmentTolerance.Unit == MzTolerance.Units.ppm
                    ? EncyclopeDiaHelpers.EncyclopeDiaConfig.MassErrorType.PPM
                    : EncyclopeDiaHelpers.EncyclopeDiaConfig.MassErrorType.AMU;
                return result;
            }
        }

        public MzTolerance EncyclopeDiaPrecursorTolerance
        {
            get { return new MzTolerance(double.Parse(txtMS1Tolerance.Text), (MzTolerance.Units) cbMS1TolUnit.SelectedIndex); }

            set
            {
                txtMS1Tolerance.Text = value.Value.ToString(LocalizationHelper.CurrentCulture);
                cbMS1TolUnit.SelectedIndex = (int)value.Unit;
            }
        }

        public MzTolerance EncyclopeDiaFragmentTolerance
        {
            get { return new MzTolerance(double.Parse(txtMS2Tolerance.Text), (MzTolerance.Units) cbMS2TolUnit.SelectedIndex); }

            set
            {
                txtMS2Tolerance.Text = value.Value.ToString(LocalizationHelper.CurrentCulture);
                cbMS2TolUnit.SelectedIndex = (int)value.Unit;
            }
        }
        public void SetAdditionalSetting(string name, string value)
        {
            EncyclopeDiaAdditionalSettings.Parameters[name].Value = value;
        }

        public EncyclopeDiaSettings FormSettings
        {
            get =>
                new EncyclopeDiaSettings(ImportFastaControl.ImportSettings,
                    KoinaSettings,
                    NarrowWindowResults.FoundResultsFiles.Select(f => new MsDataFilePath(f.Path)),
                    WideWindowResults.FoundResultsFiles.Select(f => new MsDataFilePath(f.Path)),
                    EncyclopeDiaConfig,
                    SearchControl.EncyclopeDiaChromLibraryPath,
                    SearchControl.EncyclopeDiaQuantLibraryPath);
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

        private readonly LibraryManager _libraryManager;
        private readonly Stack<SrmDocument> _documents;
        public SrmDocument Document
        {
            get
            {
                if (_documents.Count == 0)
                    return null;
                return _documents.Peek();
            }
        }

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

        #region IModifyDocumentContainer
        public void Listen(EventHandler<DocumentChangedEventArgs> listener)
        {
            throw new NotImplementedException();
        }

        public void Unlisten(EventHandler<DocumentChangedEventArgs> listener)
        {
            throw new NotImplementedException();
        }

        public bool IsClosing { get { throw new NotImplementedException(); } }
        public IEnumerable<BackgroundLoader> BackgroundLoaders { get { throw new NotImplementedException(); } }
        public void AddBackgroundLoader(BackgroundLoader loader)
        {
            throw new NotImplementedException();
        }

        public void RemoveBackgroundLoader(BackgroundLoader loader)
        {
            throw new NotImplementedException();
        }

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

        public Pages CurrentPage => (Pages) wizardPages.SelectedIndex;

        public void NextPage()
        {
            if (!File.Exists(ImportFastaControl.FastaFile))
            {
                MessageDlg.Show(this, PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_A_FASTA_file_is_required_for_an_EncyclopeDia_Koina_search_);
                return;
            }

            btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;

            if (CurrentPage == Pages.run_page)
            {
                ImportEncyclopediaLibrary();
                return;
            }

            if (wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1)
                wizardPages.SelectTab(wizardPages.SelectedIndex + 1);

            switch (CurrentPage)
            {
                case Pages.fasta_page:
                    break;

                case Pages.koina_page:
                    break;

                case Pages.narrow_window_page:
                    break;

                case Pages.wide_window_page:
                    break;

                case Pages.search_settings:
                    btnNext.Text = PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_Run;
                    break;

                case Pages.run_page:
                    btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                    SearchControl.Settings = FormSettings;
                    btnNext.Enabled = false;
                    btnCancel.Enabled = false;
                    btnBack.Enabled = false;
                    ControlBox = false;
                    SearchControl.RunSearch();
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            btnBack.Enabled = wizardPages.SelectedIndex > 0;
            btnNext.Enabled = wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1;
        }

        private void ImportEncyclopediaLibrary()
        {
            var libraryPaths = new[]
            {
                SearchControl.EncyclopeDiaQuantLibraryPath,
                SearchControl.EncyclopeDiaChromLibraryPath
            };

            var libraries = new List<Library>();
            var librarySpecs = new List<LibrarySpec>();
            using (var longWait = new LongWaitDlg())
            {
                longWait.Text = Resources.ViewLibraryDlg_LoadLibrary_Loading_Library;
                string libraryName = string.Empty;

                try
                {
                    var status = longWait.PerformWork(this, 800, monitor =>
                    {
                        foreach (var l in libraryPaths)
                        {
                            libraryName = l;
                            libraries.Add(EncyclopeDiaLibrary.Load(new EncyclopeDiaSpec(l, l), new DefaultFileLoadMonitor(monitor)));
                            librarySpecs.Add(libraries.Last().CreateSpec(l));
                        }

                    });
                    if (status.IsError)
                    {
                        MessageDlg.ShowException(this, status.ErrorException);
                        return;
                    }
                    else if (!string.IsNullOrEmpty(status.WarningMessage))
                    {
                        MessageDlg.Show(this, status.WarningMessage);
                    }
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(
                            Resources.ViewLibraryDlg_LoadLibrary_An_error_occurred_attempting_to_import_the__0__library,
                            libraryName),
                        x.Message);
                    MessageDlg.Show(this, message);
                }
            }

            // add audit log entry for EncyclopeDIA search
            SkylineWindow.ModifyDocument(PeptideSearchResources.EncyclopeDiaSearchDlg_ImportEncyclopediaLibrary_Ran_EncyclopeDIA_Search,
                doc => doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangeLibraries(
                        doc.Settings.PeptideSettings.Libraries.ChangeLibrarySpecs(librarySpecs).ChangeLibraries(libraries)))),
                FormSettings.EntryCreator.Create);

            using var importPeptideSearchDlg = new ImportPeptideSearchDlg(SkylineWindow, _libraryManager, ImportPeptideSearchDlg.Workflow.dia,
                WideWindowResults.FoundResultsFiles, ImportFastaControl.ImportSettings, libraryPaths);

            if (importPeptideSearchDlg.ShowDialog(this) == DialogResult.OK)
                DialogResult = DialogResult.OK;
            else
                foreach (var stream in libraries.SelectMany(library => library.ReadStreams))
                    stream.CloseStream();
        }

        public void PreviousPage()
        {
            if (wizardPages.SelectedIndex == 0)
                return;

            btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;

            switch (CurrentPage)
            {
                case Pages.koina_page:
                    break;

                case Pages.narrow_window_page:
                    break;

                case Pages.wide_window_page:
                    break;

                case Pages.search_settings:
                    break;

                case Pages.run_page:
                    btnNext.Text = PeptideSearchResources.EncyclopeDiaSearchDlg_NextPage_Run;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            wizardPages.SelectTab(wizardPages.SelectedIndex - 1);

            btnBack.Enabled = wizardPages.SelectedIndex > 0;
            btnNext.Enabled = wizardPages.SelectedIndex < wizardPages.TabPages.Count - 1;
        }

        private EncyclopeDiaHelpers.EncyclopeDiaConfig EncyclopeDiaAdditionalSettings { get; }

        private void SearchControlSearchFinished(bool success)
        {
            btnCancel.Enabled = true;
            btnBack.Enabled = true;
            ControlBox = true;
            if (success)
                btnNext.Enabled = true;
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            NextPage();
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            PreviousPage();
        }

        public class EncyclopeDiaSettings : AuditLogOperationSettings<EncyclopeDiaSettings>, IAuditLogComparable
        {
            [TrackChildren]
            public ImportFastaControl.ImportFastaSettings FastaSettings { get; }
            [TrackChildren]
            public EncyclopeDiaHelpers.FastaToKoinaInputCsvConfig KoinaSettings { get; }
            [Track]
            public List<AuditLogPath> NarrowWindowResults { get; }
            [Track]
            public List<AuditLogPath> WideWindowResults { get; }
            [Track]
            public EncyclopeDiaHelpers.EncyclopeDiaConfig EncyclopeDiaConfig { get; }
            [Track]
            public AuditLogPath EncyclopeDiaChromLibrary { get; }
            [Track]
            public AuditLogPath EncyclopeDiaQuantLibrary { get; }

            public EncyclopeDiaSettings(ImportFastaControl.ImportFastaSettings fastaSettings,
                EncyclopeDiaHelpers.FastaToKoinaInputCsvConfig koinaSettings,
                IEnumerable<MsDataFileUri> narrowWindowResults,
                IEnumerable<MsDataFileUri> wideWindowResults,
                EncyclopeDiaHelpers.EncyclopeDiaConfig encyclopeDiaConfig,
                string encyclopeDiaChromLibrary,
                string encyclopeDiaQuantLibrary)
            {
                FastaSettings = fastaSettings;
                KoinaSettings = koinaSettings;
                NarrowWindowResultUris = narrowWindowResults;
                WideWindowResultUris = wideWindowResults;
                NarrowWindowResults = NarrowWindowResultUris?.Select(r => AuditLogPath.Create(r.GetFilePath())).ToList();
                WideWindowResults = WideWindowResultUris?.Select(r => AuditLogPath.Create(r.GetFilePath())).ToList();
                EncyclopeDiaConfig = encyclopeDiaConfig;
                EncyclopeDiaChromLibrary = AuditLogPath.Create(encyclopeDiaChromLibrary);
                EncyclopeDiaQuantLibrary = AuditLogPath.Create(encyclopeDiaQuantLibrary);
            }

            public IEnumerable<MsDataFileUri> NarrowWindowResultUris { get; }
            public IEnumerable<MsDataFileUri> WideWindowResultUris { get; }

            public override MessageInfo MessageInfo => new MessageInfo(MessageType.added_spectral_library,
                SrmDocument.DOCUMENT_TYPE.proteomic,
                string.Join(@", ", EncyclopeDiaChromLibrary, EncyclopeDiaQuantLibrary));

            public object GetDefaultObject(ObjectInfo<object> info)
            {

                var doc = info.OldRootObject as SrmDocument;
                if (doc == null)
                    return null;

                return new EncyclopeDiaSettings(
                    ImportFastaControl.ImportFastaSettings.GetDefault(doc.Settings.PeptideSettings),
                    new EncyclopeDiaHelpers.FastaToKoinaInputCsvConfig(),
                    null,
                    null,
                    new EncyclopeDiaHelpers.EncyclopeDiaConfig(),
                    null,
                    null);
            }
        }

        private void btnAdditionalSettings_Click(object sender, EventArgs e)
        {
            KeyValueGridDlg.Show(PeptideSearchResources.SearchSettingsControl_Additional_Settings,
                EncyclopeDiaConfig.Parameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues);
        }

        private void LoadMassUnitEntries()
        {
            cbMS1TolUnit.Items.Clear();
            cbMS2TolUnit.Items.Clear();

            string[] entries = { @"Da", @"ppm" };
            cbMS1TolUnit.Items.AddRange(entries);
            cbMS2TolUnit.Items.AddRange(entries);
        }

        private bool ValidateCombobox(ComboBox comboBox, out string selectedElement)
        {
            selectedElement = "";
            if (comboBox.SelectedItem == null)
                return false;
            selectedElement = comboBox.SelectedItem.ToString();
            return true;
        }

        private void txtMS1Tolerance_LostFocus(object sender, EventArgs e)
        {
            if (cbMS1TolUnit.SelectedItem != null)
                return;

            if (double.TryParse(txtMS1Tolerance.Text, out double tmp))
                cbMS1TolUnit.SelectedIndex = tmp <= 3 ? 0 : 1;
        }

        private void txtMS2Tolerance_LostFocus(object sender, EventArgs e)
        {
            if (cbMS2TolUnit.SelectedItem != null)
                return;

            if (double.TryParse(txtMS2Tolerance.Text, out double tmp))
                cbMS2TolUnit.SelectedIndex = tmp <= 3 ? 0 : 1;
        }

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = wizardPages.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }
    }


    public class EncyclopeDiaSearchControl : SearchControl
    {
        public EncyclopeDiaSearchDlg HostDialog { get; }

        public EncyclopeDiaSearchControl(EncyclopeDiaSearchDlg hostControl)
        {
            Parent = HostDialog = hostControl;
        }

        public EncyclopeDiaSearchDlg.EncyclopeDiaSettings Settings { get; set; }
        public string EncyclopeDiaChromLibraryPath { get; private set; }
        public string EncyclopeDiaQuantLibraryPath { get; private set; }
        
        private bool Search(EncyclopeDiaSearchDlg.EncyclopeDiaSettings settings, CancellationTokenSource token, IProgressStatus status)
        {
            ParallelRunnerProgressControl multiProgressControl = null;
            try
            {
                if (!EnsureRequiredFilesDownloaded(EncyclopeDiaHelpers.FilesToDownload, this))
                    throw new InvalidOperationException(Resources.EncyclopeDiaHelpers_ConvertFastaToKoinaInputCsv_could_not_find_EncyclopeDia);

                status = status.ChangeSegments(0, 7);

                string fastaFilepath = Settings.FastaSettings.FastaFile.Path;
                string fastaBasename = Path.Combine(Path.GetDirectoryName(fastaFilepath) ?? "",
                    Path.GetFileNameWithoutExtension(fastaFilepath));
                string koinaBasename = fastaBasename + string.Format(@"-z{0}_nce{1}",
                    settings.KoinaSettings.DefaultCharge, settings.KoinaSettings.DefaultNCE);
                string dlibFilepath = koinaBasename + @"-koina.dlib";

                string koinaCsvFilepath = koinaBasename + @"-koina.csv";
                EncyclopeDiaHelpers.ConvertFastaToKoinaInputCsv(fastaFilepath, koinaCsvFilepath, this, ref status, Settings.KoinaSettings);
                status = status.NextSegment();

                // hash Koina CSV input to generate blib filename (so if blib file already exists there's no need to go to Koina)
                var hasher = new BlockHash(new MD5CryptoServiceProvider());
                var hashBytes = hasher.HashFile(koinaCsvFilepath);
                var hashString = string.Join("", hashBytes.Select(b => b.ToString(@"X")));
                string modelSuffix = Properties.Settings.Default.KoinaIntensityModel + @"-" +
                                     Properties.Settings.Default.KoinaRetentionTimeModel;
                string blibFilepath = koinaBasename + @"-koina-" + modelSuffix + @"-" + hashString + @".blib";

                if (!File.Exists(blibFilepath))
                {
                    var koinaMs2Spectra = KoinaHelpers.PredictBatchesFromKoinaCsv(koinaCsvFilepath, this, ref status, token.Token);
                    status = status.NextSegment();
                    KoinaHelpers.ExportKoinaSpectraToBlib(koinaMs2Spectra, blibFilepath, this, ref status);
                }
                else
                {
                    UpdateProgress(status.ChangeMessage(string.Format(
                        PeptideSearchResources.EncyclopeDiaSearchControl_Search_Reusing_Koina_predictions_from___0_,
                        blibFilepath)));
                    status = status.NextSegment(); // after generating koina input rows
                    status = status.NextSegment(); // after intensity model
                    status = status.NextSegment(); // after PredictBatchesFromKoinaCsv
                }

                status = status.NextSegment();

                EncyclopeDiaHelpers.ConvertKoinaOutputToDlib(blibFilepath, fastaFilepath, dlibFilepath, this, ref status);
                status = status.NextSegment();

                Invoke(new MethodInvoker(() =>
                {
                    multiProgressControl = new ParallelRunnerProgressControl(this);
                    multiProgressControl.Dock = DockStyle.Fill;
                    progressSplitContainer.Panel1.Controls.Add(multiProgressControl);
                }));

                EncyclopeDiaChromLibraryPath = koinaBasename + @".elib";
                EncyclopeDiaQuantLibraryPath = koinaBasename + @"-quant.elib";
                EncyclopeDiaHelpers.Generate(dlibFilepath, EncyclopeDiaChromLibraryPath,
                    EncyclopeDiaQuantLibraryPath, fastaFilepath, settings.EncyclopeDiaConfig,
                    settings.NarrowWindowResultUris, settings.WideWindowResultUris,
                    this, multiProgressControl, token.Token, status);
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
            finally
            {
                Invoke(new MethodInvoker(() =>
                {
                    progressSplitContainer.Panel1Collapsed = true;
                    progressSplitContainer.Panel1.Controls.Clear();
                    multiProgressControl?.Dispose();
                }));
            }

            return !token.IsCancellationRequested;
        }

        public override void RunSearch()
        {
            txtSearchProgress.Text = string.Empty;
            _progressTextItems.Clear();
            btnCancel.Enabled = progressBar.Visible = true;

            _cancelToken = new CancellationTokenSource();

            ActionUtil.RunAsync(RunSearchAsync, @"EncyclopeDIA Search thread");
        }

        public void RunSearchAsync()
        {
            IProgressStatus status = new ProgressStatus();
            bool success = true;

            if (!_cancelToken.IsCancellationRequested)
            {
                Invoke(new MethodInvoker(() => UpdateSearchEngineProgress(status.ChangeMessage(PeptideSearchResources.DDASearchControl_SearchProgress_Starting_search))));

                success = Search(Settings, _cancelToken, status);

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

        private bool EnsureRequiredFilesDownloaded(IEnumerable<FileDownloadInfo> requiredFiles, IProgressMonitor progressMonitor)
        {
            var requiredFilesList = requiredFiles.ToList();
            var filesNotAlreadyDownloaded = SimpleFileDownloader.FilesNotAlreadyDownloaded(requiredFilesList).ToList();
            if (!filesNotAlreadyDownloaded.Any())
                return true;

            Invoke(new Action(() =>
            {
                try
                {
                    SimpleFileDownloaderDlg.Show(null,
                        string.Format(PeptideSearchResources.SearchSettingsControl_EnsureRequiredFilesDownloaded_Download__0_,
                            @"EncyclopeDia"), filesNotAlreadyDownloaded);
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(Parent, x.Message, x);
                }
            }));

            return !SimpleFileDownloader.FilesNotAlreadyDownloaded(filesNotAlreadyDownloaded).Any();
        }
    }
}
