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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Koina;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using File = System.IO.File;
using pwiz.Skyline.Model.Lib.Carafe;
using pwiz.Skyline.EditUI;
using System.Xml.Serialization;
using pwiz.CommonMsData;

[assembly: InternalsVisibleTo("TestFunctional")]

[assembly: InternalsVisibleTo("TestPerf")]
namespace pwiz.Skyline.SettingsUI
{
    public partial class BuildLibraryDlg : FormEx, IMultipleViewProvider
    {
        public BuildLibraryGridView Grid { get; }
        public static string[] RESULTS_EXTS =>
            Program.ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules ? RESULTS_EXTS_SMALL_MOL : RESULTS_EXTS_PEPTIDES;

        public static readonly string[] RESULTS_EXTS_PEPTIDES =
        {
            BiblioSpecLiteBuilder.EXT_DAT,
            BiblioSpecLiteBuilder.EXT_PEP_XML,
            BiblioSpecLiteBuilder.EXT_PEP_XML_ONE_DOT,
            BiblioSpecLiteBuilder.EXT_MZID,
            BiblioSpecLiteBuilder.EXT_MZID_GZ,
            BiblioSpecLiteBuilder.EXT_XTAN_XML,
            BiblioSpecLiteBuilder.EXT_PROTEOME_DISC,
            BiblioSpecLiteBuilder.EXT_PROTEOME_DISC_FILTERED,
            BiblioSpecLiteBuilder.EXT_PILOT,
            BiblioSpecLiteBuilder.EXT_PILOT_XML,
            BiblioSpecLiteBuilder.EXT_PRIDE_XML,
            BiblioSpecLiteBuilder.EXT_IDP_XML,
            BiblioSpecLiteBuilder.EXT_SQT,
            BiblioSpecLiteBuilder.EXT_SSL,
            BiblioSpecLiteBuilder.EXT_PERCOLATOR,
            BiblioSpecLiteBuilder.EXT_PERCOLATOR_XML,
            BiblioSpecLiteBuilder.EXT_MAX_QUANT,
            BiblioSpecLiteBuilder.EXT_WATERS_MSE,
            BiblioSpecLiteBuilder.EXT_PROXL_XML,
            BiblioSpecLiteBuilder.EXT_TSV,
            BiblioSpecLiteBuilder.EXT_MZTAB,
            BiblioSpecLiteBuilder.EXT_MZTAB_TXT,
            BiblioSpecLiteBuilder.EXT_OPEN_SWATH,
            BiblioSpecLiteBuilder.EXT_SPECLIB,
        };

        public static readonly string[] RESULTS_EXTS_SMALL_MOL =
        {
            BiblioSpecLiteBuilder.EXT_SSL,
        };

        public void ResetBuilder() => Builder = null;

        public MultiButtonMsgDlg PythonDlg { get; private set; }
        
        public enum Pages { properties, files, alphapeptdeepOptions, carafeOptions, learning }

        public class PropertiesPage : IFormView { }
        public class FilesPage : IFormView { }
        public class AlphapeptdeepOptionsPage : IFormView { }
        public class CarafeOptionsPage : IFormView { }
        public class LearningPage : IFormView { }

        private const string PYTHON = @"Python";
        public const string ALPHAPEPTDEEP_PYTHON_VERSION = @"3.9.13";
        private const string ALPHAPEPTDEEP = @"AlphaPeptDeep";
        private const string ALPHAPEPTDEEP_DIA = @"alphapeptdeep_dia";
        internal const string CARAFE_PYTHON_VERSION = @"3.9.13";
        private const string CARAFE = @"Carafe";
        private const string WORKSPACES = @"workspaces";
        private const string PEPTDEEP = @"PeptDeep";

        private static readonly IFormView[] TAB_PAGES =
        {
            new PropertiesPage(), new FilesPage(), new AlphapeptdeepOptionsPage(), new CarafeOptionsPage(), new LearningPage(),
        };
        public enum DataSourcePages { files, alpha, carafe, koina }
        public enum BuildLibraryTargetOptions { fastaFile, currentSkylineDocument }
        public enum LearningOptions { another_doc, this_doc, diann_report }
        private bool IsAlphaEnabled => true;
        private bool IsCarafeEnabled => true;
        private string AlphapeptdeepPythonVirtualEnvironmentDir =>
            PythonInstallerUtil.GetPythonVirtualEnvironmentScriptsDir(ALPHAPEPTDEEP_PYTHON_VERSION, ALPHAPEPTDEEP);
        private string CarafePythonVirtualEnvironmentDir =>
            PythonInstallerUtil.GetPythonVirtualEnvironmentScriptsDir(CARAFE_PYTHON_VERSION, CARAFE);
        private string UserDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        //        private string AlphapeptdeepDiaRepo => Path.Combine(UserDir, WORKSPACES, ALPHAPEPTDEEP_DIA);
        private string AlphapeptdeepDiaRepo => @"https://codeload.github.com/wenbostar/alphapeptdeep_dia/zip/refs/tags/v1.0";
        private string ProteinDatabaseFilePath => Path.Combine(UserDir, @"Downloads", @"UP000005640_9606.fasta");
        private string ExperimentDataFilePath => Path.Combine(UserDir, @"Downloads", @"LFQ_Orbitrap_AIF_Human_01.mzML");
        private string ExperimentDataSearchResultFilePath => Path.Combine(UserDir, @"Downloads", @"report.tsv");

        private string _productPath;

        public string ProductPath { get => _productPath; private set => _productPath = value; }

        private readonly MessageBoxHelper _helper;
        private readonly IDocumentUIContainer _documentUiContainer;
        private readonly SkylineWindow _skylineWindow;

        private readonly SettingsListComboDriver<IrtStandard> _driverStandards;
        private SettingsListBoxDriver<LibrarySpec> _driverLibrary;
        private LearningOptions _currentLearningOption;

        private string _lastUpdatedFileName;
        private string _lastUpdatedLibName;

        private void TestAndEnableFinish()
        {
            if ((textBoxProteinDatabase.Text != "" || comboBuildLibraryTarget.SelectedIndex == (int)BuildLibraryTargetOptions.currentSkylineDocument) &&
                textBoxMsMsData.Text != "" && (textBoxTrainingDoc.Text != "" || comboLearnFrom.SelectedIndex == (int)LearningOptions.this_doc))
                btnNext.Enabled = true;
            else if (btnNext.Text == Resources.BuildLibraryDlg_OkWizardPage_Finish)
                btnNext.Enabled = false;
        }
        public BuildLibraryDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            EmbedAlphaPeptDeepUserSettings();
            EmbedCarafeDataSettings();
            EmbedCarafeModelSettings();
            EmbedCarafeLibrarySettings();

            Icon = Resources.Skyline;
            
            _skylineWindow = skylineWindow;
            _currentLearningOption = LearningOptions.another_doc;
            _documentUiContainer = skylineWindow;

            textName.Focus();
            textPath.Text = Settings.Default.LibraryDirectory;
            comboAction.SelectedItem = LibraryBuildAction.Create.GetLocalizedString();

            if (_documentUiContainer.Document.PeptideCount == 0)
                cbFilter.Hide();
            else
                cbFilter.Checked = Settings.Default.LibraryFilterDocumentPeptides;

            cbKeepRedundant.Checked = Settings.Default.LibraryKeepRedundant;

            ceCombo.Items.AddRange(
                Enumerable.Range(KoinaConstants.MIN_NCE, KoinaConstants.MAX_NCE - KoinaConstants.MIN_NCE + 1).Select(c => (object)c)
                    .ToArray());
            ceCombo.SelectedItem = Settings.Default.KoinaNCE;
            
            comboLearnFrom.SelectedIndex = (int)DataSourcePages.files;

            _helper = new MessageBoxHelper(this);

            _driverStandards = new SettingsListComboDriver<IrtStandard>(comboStandards, Settings.Default.IrtStandardList);
            _driverStandards.LoadList(IrtStandard.EMPTY.GetKey());

            if (_documentUiContainer.DocumentFilePath != null && _documentUiContainer.Document.HasPeptides)
                comboBuildLibraryTarget.SelectedIndex = (int)BuildLibraryTargetOptions.currentSkylineDocument;
            else
                comboBuildLibraryTarget.SelectedIndex = (int)BuildLibraryTargetOptions.fastaFile;

            Grid = gridInputFiles;
            Grid.FilesChanged += (sender, e) =>
            {
                btnNext.Enabled = tabControlMain.SelectedIndex == (int)Pages.files || Grid.IsReady;
            };

            // If we're not using dataSourceGroupBox (because we're in small molecule mode) shift other controls over where it was
            if (modeUIHandler.ComponentsDisabledForModeUI(dataSourceGroupBox))
            {
                tabControlDataSource.Left = dataSourceGroupBox.Left;
                Height -= tabControlDataSource.Bottom - dataSourceGroupBox.Bottom;
            }
            else
            {
                int heightDiffGroupBox = 0;
                if (!IsAlphaEnabled)
                {
                    int yShift = radioCarafeSource.Top - radioAlphaSource.Top;
                    radioCarafeSource.Top -= yShift;
                    radioKoinaSource.Top -= yShift;
                    koinaInfoSettingsBtn.Top -= yShift;
                    radioAlphaSource.Visible = false;
                    heightDiffGroupBox += yShift;
                }

                if (!IsCarafeEnabled)
                {
                    int yShift = radioKoinaSource.Top - radioCarafeSource.Top;
                    radioKoinaSource.Top -= yShift;
                    koinaInfoSettingsBtn.Top -= yShift;
                    radioCarafeSource.Visible = false;
                    heightDiffGroupBox += yShift;
                }

                dataSourceGroupBox.Height -= heightDiffGroupBox;
                iRTPeptidesLabel.Top -= heightDiffGroupBox;
                comboStandards.Top -= heightDiffGroupBox;
                Height -= heightDiffGroupBox;
            }
        }

        private void BuildLibraryDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Settings.Default.IrtStandardList.Contains(IrtStandard.AUTO))
            {
                Settings.Default.IrtStandardList.Insert(0, IrtStandard.AUTO);
            }
        }

        public ILibraryBuilder Builder { get; internal set; }

        public IEnumerable<string> InputFileNames
        {
            get => Grid.FilePaths;
            set => Grid.FilePaths = value;
        }

        public string AddLibraryFile { get; private set; }


        internal SrmDocument DocumentUI
        {
            get => _documentUiContainer.DocumentUI;
        }

        internal SrmDocument _trainingDocument;

        internal bool ValidateBuilder(bool validateInputFiles)
        {
            string name;
            if (!_helper.ValidateNameTextBox(textName, out name))
                return false;

            string outputPath = textPath.Text;

            if (string.IsNullOrEmpty(outputPath))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path, outputPath);
                return false;
            }
            if (Directory.Exists(outputPath))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_The_output_path__0__is_a_directory_You_must_specify_a_file_path, outputPath);
                return false;
            }
            string outputDir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDir))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path, outputPath);
                return false;
            }
            if (!Directory.Exists(outputDir))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_The_directory__0__does_not_exist, outputDir);
                return false;
            }
            if (!outputPath.EndsWith(BiblioSpecLiteSpec.EXT))
                outputPath += BiblioSpecLiteSpec.EXT;
            try
            {
                using (var sfLib = new FileSaver(outputPath))
                {
                    if (!sfLib.CanSave(this))
                    {
                        textPath.Focus();
                        textPath.SelectAll();
                        return false;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _helper.ShowTextBoxError(textPath, TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Access_violation_attempting_to_write_to__0__,
                    SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Please_check_that_you_have_write_access_to_this_folder_), outputDir);
                return false;
            }
            catch (IOException)
            {
                _helper.ShowTextBoxError(textPath, TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Failure_attempting_to_create_a_file_in__0__,
                    SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Please_check_that_you_have_write_access_to_this_folder_), outputDir);
                return false;
            }

            if (validateInputFiles)
            {
                if (radioKoinaSource.Checked)
                {
                    if (!CreateKoinaBuilder(name, outputPath, NCE))
                        return false;
                }
                else if (radioAlphaSource.Checked)
                {
                    if (!CreateAlphaBuilder(name, outputPath))
                        return false;

                }
                else if (radioCarafeSource.Checked)
                {
                    if (Builder == null && !CreateCarafeBuilder(name, outputPath))
                        return false;
                }
                else
                {
                    if (!Grid.Validate(this, null, true, out var thresholdsByFile))
                        return false;

                    List<Target> targetPeptidesChosen = null;
                    if (cbFilter.Checked)
                    {
                        targetPeptidesChosen = new List<Target>();
                        var doc = _documentUiContainer.Document;
                        foreach (PeptideDocNode nodePep in doc.Peptides)
                        {
                            // Add light modified sequences
                            targetPeptidesChosen.Add(nodePep.ModifiedTarget);
                            // Add heavy modified sequences
                            foreach (var nodeGroup in nodePep.TransitionGroups)
                            {
                                if (nodeGroup.TransitionGroup.LabelType.IsLight)
                                    continue;
                                targetPeptidesChosen.Add(doc.Settings.GetModifiedSequence(nodePep.Peptide.Target,
                                    nodeGroup.TransitionGroup.LabelType,
                                    nodePep.ExplicitMods,
                                    SequenceModFormatType.lib_precision));
                            }
                        }
                    }

                    Builder = new BiblioSpecLiteBuilder(name, outputPath, InputFileNames.ToArray(), targetPeptidesChosen)
                    {
                        Action = LibraryBuildAction,
                        IncludeAmbiguousMatches = cbIncludeAmbiguousMatches.Checked,
                        KeepRedundant = LibraryKeepRedundant,
                        ScoreThresholdsByFile = thresholdsByFile,
                        Id = Helpers.MakeId(textName.Text),
                        IrtStandard = _driverStandards.SelectedItem,
                        PreferEmbeddedSpectra = PreferEmbeddedSpectra
                    };
                }
            }
            return true;
        }


        private string _libFilepath;

        public string BuilderLibFilepath
        {
            get => _libFilepath;
            set => _libFilepath = value;
        }

        private string _testLibFilepath;
        public string TestLibFilepath
        {
            get => _testLibFilepath;
            set => _testLibFilepath = value;
        }

        private bool CreateCarafeBuilder(string name, string outputPath)
        {
            if (tabControlMain.SelectedIndex == (int)Pages.learning &&
           (textBoxMsMsData.Text == "" || textBoxTrainingDoc.Text == "" ||
            ((BuildLibraryTargetOptions)comboBuildLibraryTarget.SelectedIndex ==
                BuildLibraryTargetOptions.fastaFile && textBoxProteinDatabase.Text == "")))
            {
                MessageDlg.Show(this, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_fill_out_this_form_to_continue);
                return false;
            }


            if (!SetupPythonEnvironmentForCarafe())
            {
                return false;
            }


            string msMsDataFilePath = textBoxMsMsData.Text;
            if (!File.Exists(msMsDataFilePath))
            {
                _helper.ShowTextBoxError(textBoxMsMsData, @$"{msMsDataFilePath} does not exist.");
                return false;
            }
            Builder = new CarafeLibraryBuilder(name, outputPath, CARAFE, CarafePythonVirtualEnvironmentDir,
                msMsDataFilePath, textBoxTrainingDoc.Text, textBoxProteinDatabase.Text, DocumentUI, _trainingDocument,
                labelDoc.Text == string.Format(SettingsUIResources.BuildLibraryDlg_DIANN_report_document), IrtStandard, out _testLibFilepath, out _libFilepath);

            btnNext.Enabled = true;

            return true;
        }
        private bool CreateAlphaBuilder(string name, string outputPath)
        {
            var doc = _documentUiContainer.DocumentUI;
            if (!doc.HasPeptides)
            {
                MessageDlg.Show(this,
                    SettingsUIResources.BuildLibraryDlg_CreateAlphaBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_AlphaPeptDeep_predictions_);
                return false;
            }

            if (!SetupPythonEnvironmentForAlpha())
            {
                return false;
            }

            Builder = new AlphapeptdeepLibraryBuilder(name, outputPath, doc, IrtStandard);

            return true;
        }

        private bool CreateKoinaBuilder(string name, string outputPath, int nce = 27)
        {
            // TODO: Need to figure out a better way to do this, use KoinaPeptidePrecursorPair?
            var doc = _documentUiContainer.DocumentUI;
            var peptides = doc.Peptides.Where(pep => !pep.IsDecoy).ToArray();
            var precursorCount = peptides.Sum(pep => pep.TransitionGroupCount);
            var peptidesPerPrecursor = new PeptideDocNode[precursorCount];
            var precursors = new TransitionGroupDocNode[precursorCount];
            int index = 0;

            for (var i = 0; i < peptides.Length; ++i)
            {
                var groups = peptides[i].TransitionGroups.ToArray();
                Array.Copy(Enumerable.Repeat(peptides[i], groups.Length).ToArray(), 0, peptidesPerPrecursor, index,
                    groups.Length);
                Array.Copy(groups, 0, precursors, index, groups.Length);
                index += groups.Length;
            }

            if (index == 0)
            {
                MessageDlg.Show(this, Resources.BuildLibraryDlg_ValidateBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_Koina_predictions_);
                return false;
            }

            try
            {
                KoinaUIHelpers.CheckKoinaSettings(this, _skylineWindow);

                // Still construct the library builder, otherwise a user might configure Koina
                // incorrectly, causing the build to silently fail
                if (Builder != null)
                {
                    ResetBuilder();
                }

                Builder = new KoinaLibraryBuilder(doc, name, outputPath, () => true, IrtStandard,
                    peptidesPerPrecursor, precursors, nce);

            }
            catch (Exception ex)
            {
                MessageDlg.ShowWithException(this, ex.Message, ex);
                return false;
            }

            return true;
        }

        private bool SetupPythonEnvironmentForCarafe()
        {
            var pythonInstaller = CarafeLibraryBuilder.CreatePythonInstaller(new TextBoxStreamWriterHelper());

            btnNext.Enabled = false;
            bool setupSuccess = false;
            try
            {
                setupSuccess = SetupPythonEnvironmentInternal(pythonInstaller, CarafeLibraryBuilder.PythonVersion, CarafeLibraryBuilder.CARAFE);
            }
            finally
            {
                // If not a successful installation, try to clean-up before leaving
                if (!setupSuccess)
                    pythonInstaller.CleanUpPythonEnvironment(CarafeLibraryBuilder.CARAFE);

                btnNext.Enabled = true;
            }

            return setupSuccess;


        }

        private bool SetupPythonEnvironmentForAlpha()
        {
            var pythonInstaller = AlphapeptdeepLibraryBuilder.CreatePythonInstaller(new TextBoxStreamWriterHelper());

            btnNext.Enabled = false;

            bool setupSuccess = false;
            try
            {
                setupSuccess = SetupPythonEnvironmentInternal(pythonInstaller, AlphapeptdeepLibraryBuilder.PythonVersion, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP);
            }
            finally
            {
                // If not a successful installation, try to clean-up before leaving
                if (!setupSuccess)
                    pythonInstaller.CleanUpPythonEnvironment(AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP);

                btnNext.Enabled = true;
            }

            return setupSuccess;
        }

        private bool SetupPythonEnvironmentInternal(PythonInstaller pythonInstaller, string version, string environment)
        {
            if (pythonInstaller.IsPythonVirtualEnvironmentReady() && pythonInstaller.IsNvidiaEnvironmentReady())
            {
                return true;
            }

            if (!pythonInstaller.IsPythonVirtualEnvironmentReady())
            {
                using var pythonDlg = new MultiButtonMsgDlg(
                    string.Format(ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required, version, environment),
                    Resources.OK);

                if (pythonDlg.ShowDialog(this) == DialogResult.Cancel)
                {
                    return false;
                }
                if (!PythonInstallerUI.InstallPythonVirtualEnvironment(this, pythonInstaller))
                {
                    return false;
                }
            }
            else if (!pythonInstaller.IsNvidiaEnvironmentReady())
            {
                if (!PythonInstallerUI.InstallPythonVirtualEnvironment(this, pythonInstaller))
                {
                    return false;
                }
            }
            return true;
        }

        private void textName_TextChanged(object sender, EventArgs e)
        {
            string name = textName.Text;
            string outputPath = textPath.Text;
            if (outputPath.Length > 0 && !Directory.Exists(outputPath))
            {
                try
                {
                    // ReSharper disable once ConstantNullCoalescingCondition
                    outputPath = Path.GetDirectoryName(outputPath) ?? string.Empty;                
                }
                catch (Exception)
                {
                    outputPath = string.Empty;
                }
            }
            string id = (name.Length == 0 ? string.Empty : Helpers.MakeId(textName.Text));
        
            if (_lastUpdatedFileName.IsNullOrEmpty() || _lastUpdatedFileName == _lastUpdatedLibName)
            {
                textPath.Text = id.Length == 0 ? outputPath 
                    : Path.Combine(outputPath, id + BiblioSpecLiteSpec.EXT);
                _lastUpdatedFileName = id;
                _lastUpdatedLibName = id;

            }

        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            string fileName;
            try
            {
                fileName = Path.GetFileName(textPath.Text);
            }
            catch (Exception)
            {
                fileName = string.Empty;
            }

            using (var dlg = new SaveFileDialog())
            {
                dlg.InitialDirectory = Settings.Default.LibraryDirectory;
                dlg.FileName = fileName;
                dlg.OverwritePrompt = true;
                dlg.DefaultExt = BiblioSpecLiteSpec.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(BiblioSpecLiteSpec.FILTER_BLIB);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.LibraryDirectory = Path.GetDirectoryName(dlg.FileName);
                    textPath.Text = dlg.FileName;
                    _lastUpdatedFileName = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            if (this.ActiveControl is ComboBox)
            {
                this.DialogResult = DialogResult.None;
                this.ActiveControl = null;
                AcceptButton = null;
            }
            OkWizardPage();
        }

        public bool PythonRequirementMet()
        {
            if (radioCarafeSource.Checked || radioAlphaSource.Checked || radioKoinaSource.Checked)
            {
                return ValidateBuilder(true);
            }

            return ValidateBuilder(false);

        }

        private bool SetParameters(IDictionary<string, AbstractDdaSearchEngine.Setting> gridValues, Dictionary<string, Control> keyValueParams)
        {
            bool isValid = true;
            this.ActiveControl = null;
            foreach (var kvp in keyValueParams)
            {
                if (!gridValues[kvp.Key].IsValid)
                {
                    isValid = false;
                    this.DialogResult = DialogResult.None;
                }

                if (kvp.Value is TextBox tb)
                    gridValues[kvp.Key].Value = tb.Text;
                else if (kvp.Value is CheckBox cb)
                    gridValues[kvp.Key].Value = cb.Checked.ToString();
                else if (kvp.Value is ComboBox cmb)
                {
                    if (cmb.SelectedItem != null)
                    {
                        gridValues[kvp.Key].Value = cmb.SelectedItem.ToString();
                    }
                }
                else
                    throw new InvalidOperationException();
            }

            return isValid;
        }
        public Control.ControlCollection AlphaPeptDeepTabControl
        {
            get => tabAlphaOptionsSubHolder.Controls;
        }
        public Control.ControlCollection CarafeDataTabControl
        {
            get => tabCarafeData.Controls;
        }
        public Control.ControlCollection CarafeModelTabControl
        {
            get => tabCarafeModel.Controls;
        }
        public Control.ControlCollection CarafeLibraryTabControl
        {
            get => tabCarafeLibrary.Controls;
        }

        private Dictionary<string, Control> _alphaPeptDeepParams;
        private Dictionary<string, Control> _carafeDataParams;
        private Dictionary<string, Control> _carafeModelParams;
        private Dictionary<string, Control> _carafeLibraryParams;
        private void EmbedAlphaPeptDeepUserSettings()
        {
            if (AlphapeptdeepLibraryBuilder.UserParameters == null)
                AlphapeptdeepLibraryBuilder.AlphaPeptDeepDefaultSettings();

            _alphaPeptDeepParams = KeyValueGridDlg.Show(null, ModelResources.AlphaPeptDeep_Settings,
                AlphapeptdeepLibraryBuilder.UserParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description, AlphaPeptDeepTabControl);
        }
        private void EmbedCarafeDataSettings()
        {
            if (CarafeLibraryBuilder.DataParameters == null)
                CarafeLibraryBuilder.CarafeDefaultDataSettings();

            _carafeDataParams = KeyValueGridDlg.Show(null, ModelResources.CarafeTraining_Settings,
                CarafeLibraryBuilder.DataParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description, CarafeDataTabControl);
        }

        private void EmbedCarafeModelSettings()
        {
            if (CarafeLibraryBuilder.ModelParameters == null)
                CarafeLibraryBuilder.CarafeDefaultModelSettings();

            _carafeModelParams = KeyValueGridDlg.Show(null, ModelResources.CarafeModel_Settings,
                CarafeLibraryBuilder.ModelParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description, CarafeModelTabControl);
        }

        private void EmbedCarafeLibrarySettings()
        {
            if (CarafeLibraryBuilder.LibraryParameters == null)
                CarafeLibraryBuilder.CarafeDefaultLibrarySettings();

            _carafeLibraryParams = KeyValueGridDlg.Show(null, ModelResources.CarafeLibrary_Settings,
                CarafeLibraryBuilder.LibraryParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description, CarafeLibraryTabControl);
        }
        public void OkWizardPage()
        {
            Cursor.Current = Cursors.WaitCursor;
            if (tabControlMain.SelectedIndex == (int)Pages.learning && radioCarafeSource.Checked)
            {
                if (ValidateBuilder(true))
                {
                    Settings.Default.LibraryFilterDocumentPeptides = LibraryFilterPeptides;
                    Settings.Default.LibraryKeepRedundant = LibraryKeepRedundant;
                    DialogResult = DialogResult.OK;
                    btnNext.Enabled = false;

                }
                else
                {
                    btnNext.Enabled = true;
                }

            }
            else if (tabControlMain.SelectedIndex == (int)Pages.properties && radioAlphaSource.Checked)
            {
                if (ValidateBuilder(false)) {
                    btnPrevious.Enabled = true;
                    tabControlMain.SelectedIndex = (int)Pages.alphapeptdeepOptions;
                    this.DialogResult = DialogResult.None;
                    this.ActiveControl = null;
                    btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                }
            }
            else if (tabControlMain.SelectedIndex == (int)Pages.alphapeptdeepOptions && radioAlphaSource.Checked)
            {
                if (SetParameters(AlphapeptdeepLibraryBuilder.UserParameters, _alphaPeptDeepParams)) 
                {
                    btnPrevious.Enabled = true;
                    this.DialogResult = DialogResult.None;
                    this.ActiveControl = null;
                    if (ValidateBuilder(true))
                    {
                        Settings.Default.LibraryFilterDocumentPeptides = LibraryFilterPeptides;
                        Settings.Default.LibraryKeepRedundant = LibraryKeepRedundant;
                        DialogResult = DialogResult.OK;
                    }
                }
            }
            else if (tabControlMain.SelectedIndex == (int)Pages.properties && radioCarafeSource.Checked)
            {
                if (ValidateBuilder(false))
                {
                    btnPrevious.Enabled = true;
                    tabControlMain.SelectedIndex = (int)Pages.carafeOptions;
                    AcceptButton = btnNext;
                }
            }
            else if (tabControlMain.SelectedIndex == (int)Pages.carafeOptions)
            {
                if (SetParameters(CarafeLibraryBuilder.DataParameters, _carafeDataParams) &&
                    SetParameters(CarafeLibraryBuilder.ModelParameters, _carafeModelParams) &&
                    SetParameters(CarafeLibraryBuilder.LibraryParameters, _carafeLibraryParams) && 
                    AcceptButton == btnNext)
                {
                    Settings.Default.LibraryDirectory = Path.GetDirectoryName(LibraryPath);
                    tabControlMain.SelectedIndex = (int)Pages.learning;
                    btnPrevious.Enabled = true;
                    btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                    AcceptButton = null;
                    btnNext.Enabled = (textBoxProteinDatabase.Text != "" || 
                                       comboBuildLibraryTarget.SelectedIndex == (int)BuildLibraryTargetOptions.currentSkylineDocument) &&
                                      textBoxMsMsData.Text != "" &&
                                      (textBoxTrainingDoc.Text != "" ||
                                       comboLearnFrom.SelectedIndex == (int)LearningOptions.this_doc);
                }

                AcceptButton = btnNext;
            }
            else if (tabControlMain.SelectedIndex != (int)Pages.properties || radioAlphaSource.Checked || radioKoinaSource.Checked)
            {
                if (ValidateBuilder(true))
                {
                    Settings.Default.LibraryFilterDocumentPeptides = LibraryFilterPeptides;
                    Settings.Default.LibraryKeepRedundant = LibraryKeepRedundant;
                    DialogResult = DialogResult.OK;
                }
            }
            else if (ValidateBuilder(false))
            {
                Settings.Default.LibraryDirectory = Path.GetDirectoryName(LibraryPath);

                tabControlMain.SelectedIndex = (int)(radioFilesSource.Checked
                    ? Pages.files
                    : Pages.learning);  // Carafe
                btnPrevious.Enabled = true;
                btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                AcceptButton = btnNext;
                btnNext.Enabled = false;
                if ((textBoxProteinDatabase.Text != "" || comboBuildLibraryTarget.SelectedIndex == (int)BuildLibraryTargetOptions.currentSkylineDocument) &&
                    textBoxMsMsData.Text != "" &&
                    (textBoxTrainingDoc.Text != "" || comboLearnFrom.SelectedIndex == (int)LearningOptions.this_doc))
                    btnNext.Enabled = true;
            }
        }
        private void btnPrevious_Click(object sender, EventArgs e)
        {
            if (tabControlMain.SelectedIndex == (int)Pages.learning)
            {
                tabControlMain.SelectedIndex = (int)Pages.carafeOptions;
                btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;
                btnNext.Enabled = true;
                btnNext.DialogResult = DialogResult.None;
                AcceptButton = null;
            }
            else if (tabControlMain.SelectedIndex == (int)Pages.carafeOptions ||
                     tabControlMain.SelectedIndex == (int)Pages.alphapeptdeepOptions ||
                     tabControlMain.SelectedIndex == (int)Pages.files)
            {
                tabControlMain.SelectedIndex = (int)Pages.properties;
                btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;
                btnNext.Enabled = true;
                btnNext.DialogResult = DialogResult.None;
                AcceptButton = null;
                btnPrevious.Enabled = false;
            }
        }

        private void btnAddFile_Click(object sender, EventArgs e)
        {
            string[] addFiles = ShowAddFile(this, Settings.Default.LibraryResultsDirectory);
            if (addFiles != null)
            {
                AddInputFiles(addFiles);
            }
        }

        public static string[] ShowAddFile(Form parent, String initialDirectory)
        {
            var wildExts = new string[RESULTS_EXTS.Length];
            for (int i = 0; i < wildExts.Length; i++)
                wildExts[i] = @"*" + RESULTS_EXTS[i];

            // Adjust the button text for small molecule UI
            var buttonText = parent is FormEx formEx ?
                formEx.GetModeUIHelper().Translate(SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Matched_Peptides) :
                SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Matched_Peptides;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Add_Input_Files;
                dlg.InitialDirectory = initialDirectory;
                dlg.CheckPathExists = true;
                dlg.SupportMultiDottedExtensions = true;
                dlg.Multiselect = true;
                dlg.DefaultExt = BiblioSpecLibSpec.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(
                    buttonText + string.Join(@",", wildExts) + @")|" +
                    string.Join(@";", wildExts),
                    BiblioSpecLiteSpec.FILTER_BLIB);
                if (dlg.ShowDialog(parent) == DialogResult.OK)
                {
                    Settings.Default.LibraryResultsDirectory = Path.GetDirectoryName(dlg.FileName);

                    return dlg.FileNames;
                }
                return null;
            }
        }

        private void btnAddDirectory_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = SettingsUIResources.BuildLibraryDlg_btnAddDirectory_Click_Add_Input_Directory;
                dlg.ShowNewFolderButton = false;
                dlg.SelectedPath = Settings.Default.LibraryResultsDirectory;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.LibraryResultsDirectory = dlg.SelectedPath;

                    AddDirectory(dlg.SelectedPath);
                }
            }
        }

        public void AddDirectory(string dirPath)
        {
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = SettingsUIResources.BuildLibraryDlg_AddDirectory_Find_Input_Files;
                try
                {
                    var inputFiles = new List<string>();
                    longWaitDlg.PerformWork(this, 800, broker => FindInputFiles(dirPath, inputFiles, broker));
                    AddInputFiles(inputFiles);
                }
                catch (Exception x)
                {
                    var message = 
                        TextUtil.LineSeparate(string.Format(SettingsUIResources.BuildLibraryDlg_AddDirectory_An_error_occurred_reading_files_in_the_directory__0__, dirPath), x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                }
            }
        }

        private void btnAddPaths_Click(object sender, EventArgs e)
        {
            ShowAddPathsDlg();
        }

        public void ShowAddPathsDlg()
        {
            CheckDisposed();

            using (var dlg = new AddPathsDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    AddInputFiles(dlg.FileNames);
                }
            }
        }

        private static void FindInputFiles(string dir, ICollection<string> inputFiles, ILongWaitBroker broker)
        {
            broker.ProgressValue = 0;
            FindInputFiles(dir, inputFiles, broker, 0, 100);
        }

        private static void FindInputFiles(string dir, ICollection<string> inputFiles,
            ILongWaitBroker broker, double start, double stop)
        {
            broker.Message = TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_FindInputFiles_Finding_library_input_files_in,
                PathEx.ShortenPathForDisplay(dir));

            string[] fileNames = Directory.GetFiles(dir);
            Array.Sort(fileNames);
            string[] dirs = Directory.GetDirectories(dir);
            Array.Sort(dirs);

            double startSub = start;
            double increment = (stop - start) / (dirs.Length + 1);

            const string extPep = BiblioSpecLiteBuilder.EXT_PEP_XML_ONE_DOT;
            const string extIdp = BiblioSpecLiteBuilder.EXT_IDP_XML;            
            bool hasIdp = fileNames.Contains(f => PathEx.HasExtension(f, extIdp));

            foreach (string fileName in fileNames)
            {
                if (IsValidInputFile(fileName))
                {
                    // If the directory has any .idpXML files, then do not add the
                    // supporting .pepXML files.
                    if (!hasIdp || !PathEx.HasExtension(fileName, extPep))
                        inputFiles.Add(fileName);                    
                }
                if (broker.IsCanceled)
                    return;
            }

            startSub += increment;
            broker.ProgressValue = (int) startSub; 

            foreach (string dirSub in dirs)
            {
                FindInputFiles(dirSub, inputFiles, broker, startSub, startSub + increment);
                if (broker.IsCanceled)
                    return;
                startSub += increment;
                broker.ProgressValue = (int)startSub;
            }
        }

        public void AddInputFiles(IEnumerable<string> fileNames)
        {
            InputFileNames = AddInputFiles(this, InputFileNames, fileNames);
        }

        public static void CheckInputFiles(IEnumerable<string> inputFileNames, IEnumerable<string> fileNames, bool performDDASearch, out List<string> filesNew, out List<string> filesError)
        {
            filesNew = new List<string>(inputFileNames);
            filesError = new List<string>();
            foreach (var fileName in fileNames)
            {
                if (IsValidInputFile(fileName, performDDASearch))
                {
                    if (!filesNew.Contains(fileName))
                        filesNew.Add(fileName);
                }
                else
                {
                    if (!filesError.Contains(fileName))
                        filesError.Add(fileName);
                }
            }
        }

        private string[] AddInputFiles(Form parent, IEnumerable<string> inputFileNames, IEnumerable<string> fileNames)
        {
            CheckInputFiles(inputFileNames, fileNames, false, out var filesNew, out var filesError);

            if (filesError.Count > 0)
            {
                var filesLib = filesError.Where(IsLibraryFile).ToArray();
                if (filesError.Count == filesLib.Length)
                {
                    // All files are library files (e.g. msp, sptxt, etc)
                    if (filesLib.Length == 1)
                    {
                        using (var dlg = new MultiButtonMsgDlg(
                                   string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_a_library_file_and_does_not_need_to_be_built__Would_you_like_to_add_this_library_to_the_document_,
                                       filesLib[0]), MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                        {
                            if (dlg.ShowDialog(parent) == DialogResult.Yes)
                            {
                                AddLibraryFile = filesLib[0];
                                DialogResult = DialogResult.OK;
                            }
                        }
                    }
                    else
                    {
                        MessageDlg.Show(parent, SettingsUIResources.BuildLibraryDlg_AddInputFiles_These_files_are_library_files_and_do_not_need_to_be_built__Edit_the_list_of_libraries_to_add_them_directly_);
                    }
                }
                else if (filesError.Count == 1)
                {
                    MessageDlg.Show(parent, string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_not_a_valid_library_input_file, filesError[0]));
                }
                else
                {
                    var message = TextUtil.SpaceSeparate(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_following_files_are_not_valid_library_input_files,
                        string.Empty,
                        // ReSharper disable LocalizableElement
                        "\t" + string.Join("\n\t", filesError.ToArray()));
                                  // ReSharper restore LocalizableElement
                    MessageDlg.Show(parent, message);
                }
            }

            return filesNew.ToArray();
        }

        public static string[] AddInputFiles(Form parent, IEnumerable<string> inputFileNames, IEnumerable<string> fileNames, bool performDDASearch = false)
        {
            CheckInputFiles(inputFileNames, fileNames, performDDASearch, out var filesNew, out var filesError);

            if (filesError.Count == 1)
            {
                MessageDlg.Show(parent, string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_not_a_valid_library_input_file, filesError[0]));
            }
            else if (filesError.Count > 1)
            {
                var message = TextUtil.SpaceSeparate(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_following_files_are_not_valid_library_input_files,
                              string.Empty,
                              // ReSharper disable LocalizableElement
                              "\t" + string.Join("\n\t", filesError.ToArray()));
                              // ReSharper restore LocalizableElement
                MessageDlg.Show(parent, message);
            }

            return filesNew.ToArray();
        }

        private static bool IsValidInputFile(string fileName, bool performDDASearch = false)
        {
            if (performDDASearch)
                return true; // these are validated in OpenFileDialog
            else
            {
                foreach (string extResult in RESULTS_EXTS)
                {
                    if (PathEx.HasExtension(fileName, extResult))
                        return true;
                }
            }
            return fileName.EndsWith(BiblioSpecLiteSpec.EXT);
        }

        private static bool IsLibraryFile(string fileName)
        {
            return LibrarySpec.CreateFromPath(@"__internal__", fileName) != null;
        }

        private void textPath_TextChanged(object sender, EventArgs e)
        {
            bool existsRedundant = false;
            string path = textPath.Text;
            if (!string.IsNullOrEmpty(path) && path.EndsWith(BiblioSpecLiteSpec.EXT))
            {
                try
                {
                    string baseName = Path.GetFileNameWithoutExtension(textPath.Text);
                    string redundantName = baseName + BiblioSpecLiteSpec.EXT_REDUNDANT;
                    existsRedundant = File.Exists(Path.Combine(Path.GetDirectoryName(textPath.Text) ?? string.Empty, redundantName));
                }
                catch (IOException)
                {
                    // May happen if path is too long.
                }
            }

            if (existsRedundant)
            {
                if (!comboAction.Enabled)
                    comboAction.Enabled = true;
            }
            else
            {
                if (comboAction.Enabled)
                {
                    comboAction.SelectedItem = LibraryBuildAction.Create.GetLocalizedString();
                    comboAction.Enabled = false;
                }
            }
        }

        public string LibraryName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public string LibraryPath
        {
            get { return textPath.Text; }
            set { textPath.Text = value; }
        }

        public void LibraryPathSelectEnd()
        {
            textPath.Select(textPath.Text.Length, 0);
        }

        public bool Koina
        {
            get { return radioKoinaSource.Checked; }
            set { radioKoinaSource.Checked = value; }
        }
        public bool AlphaPeptDeep
        {
            get { return radioAlphaSource.Checked; }
            set { radioAlphaSource.Checked = value; }
        }
        public bool Carafe
        {
            get { return radioCarafeSource.Checked; }
            set { radioCarafeSource.Checked = value; }
        }

        public int NCE
        {
            get { return (int)ceCombo.SelectedItem; }
            set { ceCombo.SelectedItem = value; }
        }

        public bool LibraryKeepRedundant
        {
            get { return cbKeepRedundant.Checked; }
            set { cbKeepRedundant.Checked = value; }
        }

        public bool IncludeAmbiguousMatches
        {
            get { return cbIncludeAmbiguousMatches.Checked; }
            set { cbIncludeAmbiguousMatches.Checked = value; }
        }

        public bool LibraryFilterPeptides
        {
            get { return cbFilter.Checked; }
            set { cbFilter.Checked = value; }
        }

        public LibraryBuildAction LibraryBuildAction
        {
            get
            {
                return (comboAction.SelectedIndex == 0
                            ? LibraryBuildAction.Create
                            : LibraryBuildAction.Append);
            }

            set
            {
                comboAction.SelectedIndex = (value == LibraryBuildAction.Create ? 0 : 1);
            }
        }

        public IrtStandard IrtStandard
        {
            get { return _driverStandards.SelectedItem; }
            set
            {
                var index = 0;
                if (value != null)
                {
                    for (var i = 0; i < comboStandards.Items.Count; i++)
                    {
                        if (comboStandards.Items[i].ToString().Equals(value.GetKey()))
                        {
                            index = i;
                            break;
                        }
                    }
                }
                comboStandards.SelectedIndex = index;
                _driverStandards.SelectedIndexChangedEvent(null, null);
            }
        }

        public bool? PreferEmbeddedSpectra { get; set; }

        private void comboStandards_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverStandards.SelectedIndexChangedEvent(sender, e);
        }

        private void dataSourceRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            // Only respond to the checking event, or this will happen
            // twice for every change.
            var radioSender = (RadioButton)sender;
            
            if (!radioSender.Checked)
                return;

            var selectedStandard = _driverStandards.SelectedItem;
            string nextText = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;
            if (radioFilesSource.Checked)
            {
                tabControlDataSource.SelectedIndex = (int)DataSourcePages.files;
                if (!Settings.Default.IrtStandardList.Contains(IrtStandard.AUTO))
                {
                    Settings.Default.IrtStandardList.Insert(1, IrtStandard.AUTO);
                }
                comboStandards.Enabled = true;

            }
            else
            {
                Settings.Default.IrtStandardList.Remove(IrtStandard.AUTO);

                if (radioCarafeSource.Checked)
                {
                    tabControlDataSource.SelectedIndex = (int)DataSourcePages.carafe;
                    comboStandards.Enabled = true;
                }
                else if (radioAlphaSource.Checked)
                {
                    tabControlDataSource.SelectedIndex = (int)DataSourcePages.alpha;
                    //nextText = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                    comboStandards.Enabled = true;
                }
                else // must be Koina
                {
                    tabControlDataSource.SelectedIndex = (int)DataSourcePages.koina;
                    KoinaUIHelpers.CheckKoinaSettings(this, _skylineWindow);
                    nextText = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                    comboStandards.Enabled = true;
                }
            }
            _driverStandards.LoadList(selectedStandard.GetKey());

            if (!Equals(btnNext.Text, nextText))
                btnNext.Text = nextText;
        }

        private void koinaInfoSettingsBtn_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _skylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Koina);
        }

        public IFormView ShowingFormView
        {
            get
            {
                return TAB_PAGES[tabControlMain.SelectedIndex];
            }
        }

        private void comboLearnFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            var learningOption = (LearningOptions)comboLearnFrom.SelectedIndex;
            comboLearnFrom_Update(learningOption);
        }

        private void comboLearnFrom_Update(LearningOptions learningOption)
        {

            if (_currentLearningOption != learningOption)
                textBoxTrainingDoc.Text = "";
            else
                return;
            comboLearnFrom.SelectedIndex = (int)learningOption;
            switch (learningOption)
            {
                case LearningOptions.another_doc:
                    labelDoc.Enabled = true;
                    buttonTrainingDoc.Enabled = true;
                    textBoxTrainingDoc.Enabled = true;
                    labelDoc.Text = string.Format(SettingsUIResources.BuildLibraryDlg_Skyline_tuning_document);
                    //PopulateLibraries();
                    break;

                case LearningOptions.this_doc:
                    if (_documentUiContainer.DocumentFilePath != null)
                    {
                        //labelDoc.Text = string.Format(SettingsUIResources.BuildLibraryDlg_Skyline_tuning_document);
                        comboLearnFrom.SelectedIndex = (int)_currentLearningOption;
                        if (!_documentUiContainer.Document.HasPeptides)
                        {
                            _helper.ShowTextBoxError(tabControlLearning, SettingsUIResources.BuildLibraryDlg_Current_Skyline_document_is_missing_peptides);
                        }
                        else
                        {
                            _helper.ShowTextBoxError(tabControlLearning,
                                SettingsUIResources
                                    .BuildLibraryDlg_Cannot_predict_library_for_and_tune_from_the_same_document);
                        }

                        tabPage1.BackColor = tabPage1.Parent.BackColor;
                    }
                    else
                    {
                        _helper.ShowTextBoxError(tabControlLearning,
                            SettingsUIResources.BuildLibraryDlg_Current_Skyline_document_is_missing_peptides); // SettingsUIResources.BuildLibraryDlg_No_Skyline_document_is_currently_loaded);
                        comboLearnFrom.SelectedIndex = (int)_currentLearningOption;
                    }
                    break;

                case LearningOptions.diann_report:
                    labelDoc.Enabled = true;
                    buttonTrainingDoc.Enabled = true;
                    textBoxTrainingDoc.Enabled = true;
                    labelDoc.Text = string.Format(SettingsUIResources.BuildLibraryDlg_DIANN_report_document);
                    break;
            }

            _currentLearningOption = (LearningOptions)comboLearnFrom.SelectedIndex;
        }
        private void comboBuildLibraryTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            tabControlBuildLibraryTarget.SelectedIndex = comboBuildLibraryTarget.SelectedIndex;
            comboBuildLibraryTarget_Update((BuildLibraryTargetOptions)tabControlBuildLibraryTarget.SelectedIndex);
        }
        private void comboBuildLibraryTarget_Update(BuildLibraryTargetOptions targetOption)
        {
            comboBuildLibraryTarget.SelectedIndex = (int)targetOption;
            TestAndEnableFinish();

            switch (targetOption)
            {
                case BuildLibraryTargetOptions.currentSkylineDocument:
                    if (!_documentUiContainer.Document.HasPeptides)
                    {
                        _helper.ShowTextBoxError(tabControlLearning, SettingsUIResources.BuildLibraryDlg_Current_Skyline_document_is_missing_peptides);
                        comboBuildLibraryTarget.SelectedIndex = (int)BuildLibraryTargetOptions.fastaFile;
                    }
                    else if (comboLearnFrom.SelectedIndex == (int)LearningOptions.this_doc)
                    {
                        comboLearnFrom.SelectedIndex = (int)LearningOptions.another_doc;
                        labelDoc.Enabled = true;
                        buttonTrainingDoc.Enabled = true;
                        textBoxTrainingDoc.Enabled = true;
                        labelDoc.Text = string.Format(SettingsUIResources.BuildLibraryDlg_Skyline_tuning_document);
                        tabControlLearning.SelectedIndex = (int)LearningOptions.another_doc;
                    }
                    break;

                case BuildLibraryTargetOptions.fastaFile:
                    if (_documentUiContainer.DocumentFilePath != null && _documentUiContainer.Document.HasPeptides)
                    {
                        tabControlLearning.SelectedIndex = (int)LearningOptions.another_doc;
                        _helper.ShowTextBoxError(tabControlLearning, SettingsUIResources.BuildLibraryDlg_Cannot_predict_library_for_FASTA_file_when_Skyline_document_is_loaded);
                        comboBuildLibraryTarget.SelectedIndex = (int)BuildLibraryTargetOptions.currentSkylineDocument;
                    }
                    break;
            }
        }
        private void PopulateLibraries()
        {
            if (_driverLibrary == null)
            {
                _driverLibrary = new SettingsListBoxDriver<LibrarySpec>(listLibraries, Settings.Default.SpectralLibraryList);
                _driverLibrary.LoadList(null, Array.Empty<LibrarySpec>());
            }
        }

     
        private void buttonProteinDatabase_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog();
            dlg.Title = @"Select Protein Database File";
            dlg.InitialDirectory = Settings.Default.ActiveDirectory;
            dlg.CheckPathExists = true;
            dlg.Multiselect = false;
            dlg.SupportMultiDottedExtensions = true;
            dlg.DefaultExt = DataSourceUtil.EXT_FASTA[0];
            dlg.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(EditUIResources.OpenFileDialog_FASTA_files, DataSourceUtil.EXT_FASTA));
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                textBoxProteinDatabase.Text = dlg.FileName;
            }
            
            TestAndEnableFinish();
        }

        private void buttonMsMsData_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog();
            dlg.Title = @"Select Ms/Ms Data File";
            dlg.InitialDirectory = Settings.Default.ActiveDirectory;
            dlg.CheckPathExists = true;
            dlg.Multiselect = false;
            dlg.SupportMultiDottedExtensions = true;
            dlg.DefaultExt = DataSourceUtil.EXT_MZML;
            dlg.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(@"mzML files", DataSourceUtil.EXT_MZML));
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                textBoxMsMsData.Text = dlg.FileName;
            }
            TestAndEnableFinish();
        }
        private void buttonTrainingDoc_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog();

            if (this.labelDoc.Text == SettingsUIResources.BuildLibraryDlg_Skyline_tuning_document)
            {
                dlg.Title = SettingsUIResources.BuildLibraryDlg_Select_Skyline_document_file;
                dlg.Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC);
            }
            else
            {
                dlg.Title = SettingsUIResources.BuildLibraryDlg_Select_DIANN_report_document;
                dlg.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FILTER_TSV);
            }

            dlg.InitialDirectory = Settings.Default.ActiveDirectory;
            dlg.CheckPathExists = true;
            dlg.Multiselect = false;
            dlg.SupportMultiDottedExtensions = true;
            dlg.DefaultExt = DataSourceUtil.EXT_MZML;

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                textBoxTrainingDoc.Text = dlg.FileName;

                if (this.labelDoc.Text == SettingsUIResources.BuildLibraryDlg_Skyline_tuning_document)
                {
                    LoadTrainingDocument(dlg.FileName);
                    _trainingDocument = new SrmDocument(SrmSettingsList.GetDefault());

                    using (var reader = new StreamReader(PathEx.SafePath(dlg.FileName)))
                    {
                        XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                        _trainingDocument = (SrmDocument)ser.Deserialize(reader);
                    }
                }
            }
            
            TestAndEnableFinish();
        }


        internal void UnloadTrainingDocument()
        {
            _trainingDocument = null;
        }
        internal void LoadTrainingDocument(string fileName)
        {
            btnNext.Enabled = false;
            _trainingDocument = new SrmDocument(SrmSettingsList.GetDefault());

            using (var reader = new StreamReader(PathEx.SafePath(fileName)))
            {
                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                _trainingDocument = (SrmDocument)ser.Deserialize(reader);
            }
            TestAndEnableFinish();
        }

        private void carafeSettings_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
          //  _skylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Carafe);
        }

        private void textBoxTrainingDoc_TextChanged(object sender, EventArgs e)
        {
            TestAndEnableFinish();
        }

        private void textBoxMsMsData_TextChanged(object sender, EventArgs e)
        {
            TestAndEnableFinish();
        }

        private void textBoxProteinDatabase_TextChanged(object sender, EventArgs e)
        {
            TestAndEnableFinish();
        }

        private void alphaPeptDeepSettings_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
           // _skylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.AlphaPeptDeep);
        }
        private void tabCarafeOptions_Click(object sender, EventArgs e)
        {

        }
    }
}
