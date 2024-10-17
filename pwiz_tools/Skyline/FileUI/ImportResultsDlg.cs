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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportResultsDlg : FormEx, IAuditLogModifier<ImportResultsDlg.ImportResultsSettings>
    {
        public const int MIN_COMMON_PREFIX_LENGTH = 3;

        // Number of transitions below which the user gets a warning about
        // multiple injection features.
        private const int MIN_MULTIPLE_TRANSITIONS = 60;

        private readonly string _documentSavedPath;
        private readonly bool _warnOnMultiInjection;

        public ImportResultsDlg(SrmDocument document, string savedPath)
        {
            _documentSavedPath = savedPath;
            _warnOnMultiInjection = (document.MoleculeTransitionCount < MIN_MULTIPLE_TRANSITIONS);

            InitializeComponent();

            var results = document.Settings.MeasuredResults;
            if (results == null || results.Chromatograms.Count == 0)
            {
                // Disable append button, if nothing to append to
                radioAddExisting.Enabled = false;
            }
            else
            {
                // Populate name combo box with existing names
                foreach (var set in results.Chromatograms)
                    comboName.Items.Add(set.Name);
            }

            // Add optimizable regressions
            comboOptimizing.Items.Add(ExportOptimize.NONE);
            comboOptimizing.Items.Add(ExportOptimize.CE);
            if (document.Settings.TransitionSettings.Prediction.DeclusteringPotential != null)
                comboOptimizing.Items.Add(ExportOptimize.DP);
            if (document.Settings.TransitionSettings.Prediction.CompensationVoltage != null)
            {
                comboOptimizing.Items.Add(ExportOptimize.COV);

                comboTuning.Items.Add(ExportOptimize.COV_ROUGH);
                comboTuning.SelectedIndex = 0;
                if (!document.GetMissingCompensationVoltages(CompensationVoltageParameters.Tuning.rough).Any())
                {
                    // We can only import optimizing CoV medium tune if there are no missing CoV rough tune values
                    comboTuning.Items.Add(ExportOptimize.COV_MEDIUM);
                    if (!document.GetMissingCompensationVoltages(CompensationVoltageParameters.Tuning.medium).Any())
                    {
                        // We can only import optimizing CoV fine tune if there are no missing CoV medium tune values
                        comboTuning.Items.Add(ExportOptimize.COV_FINE);
                    }
                }
            }
            comboOptimizing.SelectedIndex = 0;

            comboSimultaneousFiles.SelectedIndex = Settings.Default.ImportResultsSimultaneousFiles;
            cbShowAllChromatograms.Checked = Settings.Default.AutoShowAllChromatogramsGraph;
            cbAutoRetry.Checked = Settings.Default.ImportResultsDoAutoRetry;
        }

        private string DefaultNewName
        {
            get
            {
                string name = Resources.ImportResultsDlg_DefaultNewName_Default_Name;
                if (ResultsExist(name))
                {
                    int i = 2;
                    do
                    {
                        name = Resources.ImportResultsDlg_DefaultNewName_Default_Name +i++;
                    }
                    while (ResultsExist(name));
                }
                return name;
            }
        }

        private bool IsMultiple { get { return radioCreateMultiple.Checked || radioCreateMultipleMulti.Checked; } }

        private bool IsOptimizing { get { return comboOptimizing.SelectedIndex != -1; } }

        public KeyValuePair<string, MsDataFileUri[]>[] NamedPathSets { get; set; }

        public string OptimizationName
        {
            get
            {
                int selectedIndex = comboOptimizing.SelectedIndex;
                int covIndex = comboOptimizing.Items.IndexOf(ExportOptimize.COV);
                if (covIndex != -1 && selectedIndex == covIndex)
                {
                    return comboTuning.SelectedItem.ToString();
                }
                return selectedIndex != -1
                           ? comboOptimizing.SelectedItem.ToString()
                           : ExportOptimize.NONE;
            }

            set
            {
                // If optimizing, make sure one of the options that allows
                // optimizing is checked.
                if (!Equals(ExportOptimize.NONE, value))
                {
                    if (NamedPathSets != null && NamedPathSets.Length > 1)
                        radioCreateMultipleMulti.Checked = true;
                    else
                        radioCreateNew.Checked = true;
                }

                if (value.Equals(ExportOptimize.COV_FINE) || value.Equals(ExportOptimize.COV_MEDIUM) ||
                    value.Equals(ExportOptimize.COV_ROUGH))
                {
                    comboOptimizing.SelectedItem = ExportOptimize.COV;
                    comboTuning.SelectedItem = value;
                }
                else
                {
                    comboOptimizing.SelectedItem = value;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Settings.Default.ImportResultsSimultaneousFiles = comboSimultaneousFiles.SelectedIndex;
            Settings.Default.ImportResultsDoAutoRetry = cbAutoRetry.Checked;
            base.OnClosed(e);
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            if (NamedPathSets == null)
            {
                if (radioAddExisting.Checked)
                {
                    if (comboName.SelectedIndex == -1)
                    {
                        MessageDlg.Show(this, FileUIResources.ImportResultsDlg_OkDialog_You_must_select_an_existing_set_of_results_to_which_to_append_new_data);
                        comboName.Focus();
                        return;
                    }

                    if (!CanCreateMultiInjectionMethods())
                        return;

                    NamedPathSets = GetDataSourcePathsFile(comboName.SelectedItem.ToString());
                }
                else if (radioCreateMultiple.Checked)
                {
                    NamedPathSets = GetDataSourcePathsFile(null);
                }
                else if (radioCreateMultipleMulti.Checked)
                {
                    if (!CanCreateMultiInjectionMethods())
                        return;
                    NamedPathSets = GetDataSourcePathsDir();
                }
                else
                {
                    string name;
                    if (!helper.ValidateNameTextBox(textName, out name))
                        return;
                    if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                    {
                        helper.ShowTextBoxError(textName, FileUIResources.ImportResultsDlg_OkDialog_A_result_name_may_not_contain_any_of_the_characters___0___, Path.GetInvalidFileNameChars());
                        return;
                    }
                    if (ResultsExist(name))
                    {
                        helper.ShowTextBoxError(textName, FileUIResources.ImportResultsDlg_OkDialog_The_specified_name_already_exists_for_this_document);
                        return;
                    }

                    NamedPathSets = GetDataSourcePathsFile(name);

                    if (NamedPathSets == null)
                        return;

                    foreach (var namedPathSet in NamedPathSets)
                    {
                        // Look for a multiple injection replicate
                        if (namedPathSet.Value.Length > 1)
                        {
                            // Make sure they are allowed
                            if (!CanCreateMultiInjectionMethods())
                                return;
                            // If so, then no need to check any others
                            break;
                        }
                    }
                }
            }

            if (NamedPathSets == null)
                return;

            if (NamedPathSets.Length > 1)
            {
                var resultNames = NamedPathSets.Select(ns => ns.Key).ToArray();
                string prefix = GetCommonPrefix(resultNames);
                string suffix = GetCommonSuffix(resultNames);
                if (!String.IsNullOrEmpty(prefix) || !String.IsNullOrEmpty(suffix))
                {
                    using (var dlgName = new ImportResultsNameDlg(prefix, suffix, resultNames))
                    {
                        var result = dlgName.ShowDialog(this);
                        if (result == DialogResult.Cancel)
                        {
                            return;
                        }
                        if (dlgName.IsRemove)
                        {
                            dlgName.ApplyNameChange(NamedPathSets);

                            Prefix = string.IsNullOrEmpty(prefix) ? null : prefix;
                            Suffix = string.IsNullOrEmpty(suffix) ? null : suffix;
                        }
                    }
                }
            }

            // Always make sure multiple replicates have unique names.  For single
            // replicate, the user will get an error.
            if (IsMultiple)
                EnsureUniqueNames();
            
            DialogResult = DialogResult.OK;
        }

        public string Prefix { get; private set; }
        public string Suffix { get; private set; }

        public static string GetCommonPrefix(IEnumerable<string> names)
        {
            return names.GetCommonPrefix(MIN_COMMON_PREFIX_LENGTH);
        }

        private static readonly string[] SUFFIX_COMMON_CONCENTRATIONS = 
        {
            @"amol", @"fmol", @"pmol", @"nmol", @"umol", @"mol"
        };

        public static string GetCommonSuffix(IEnumerable<string> names)
        {
            string suffix = names.GetCommonSuffix(MIN_COMMON_PREFIX_LENGTH);
            // Ignore common concentration suffixes
            string prefixOfSuffix = SUFFIX_COMMON_CONCENTRATIONS.FirstOrDefault(s => suffix.Contains(s));
            if (prefixOfSuffix != null)
            {
                int start = suffix.IndexOf(prefixOfSuffix, StringComparison.Ordinal);
                if (IsNumericOrSeperator(suffix.Substring(0, start)))
                    suffix = suffix.Substring(start + prefixOfSuffix.Length);
            }
            return suffix;
        }

        private static bool IsNumericOrSeperator(string s)
        {
            const string allowedChars = "0123456789.-_";
            return s.All(c => allowedChars.Contains(c));
        }

        private bool CanCreateMultiInjectionMethods()
        {
            if (_warnOnMultiInjection && !IsOptimizing)
            {
                if (MultiButtonMsgDlg.Show(this,
                                FileUIResources.ImportResultsDlg_CanCreateMultiInjectionMethods_The_current_document_does_not_appear_to_have_enough_transitions_to_require_multiple_injections,
                                 MessageBoxButtons.YesNo, DialogResult.No) == DialogResult.No)
                    return false;
            }
            return true;
        }

        public KeyValuePair<string, MsDataFileUri[]>[] GetDataSourcePathsFile(string name)
        {
            CheckDisposed();

            var dataSources = GetDataSourcePaths(this, _documentSavedPath);
            if (dataSources == null || dataSources.Length == 0)
            {
                // Above call will have shown a message if necessary, or not if canceled
                return null;
            }

            if (name != null)
                return GetDataSourcePathsFileSingle(name, dataSources);

            return GetDataSourcePathsFileReplicates(dataSources);
        }

        public static MsDataFileUri[] GetDataSourcePaths(Control parent, string documentSavedPath)
        {
            using (var dlgOpen = new OpenDataSourceDialog(Settings.Default.RemoteAccountList))
            {
                dlgOpen.Text = FileUIResources.ImportResultsDlg_GetDataSourcePathsFile_Import_Results_Files;
                dlgOpen.RestoreState(documentSavedPath, Settings.Default.OpenDataSourceState);
                if (dlgOpen.ShowDialog(parent) != DialogResult.OK)
                    return null;

                Settings.Default.OpenDataSourceState = dlgOpen.GetState(documentSavedPath);

                var dataSources = dlgOpen.DataSources;

                if (dataSources == null || dataSources.Length == 0)
                {
                    MessageDlg.Show(parent, Resources.ImportResultsDlg_GetDataSourcePathsFile_No_results_files_chosen);
                    return null;
                }

                return dataSources;
            }
        }

        public KeyValuePair<string, MsDataFileUri[]>[] GetDataSourcePathsFileSingle(string name, IEnumerable<MsDataFileUri> dataSources)
        {
            var listPaths = new List<MsDataFileUri>();
            foreach (var dataSource in dataSources)
            {
                MsDataFilePath msDataFilePath = dataSource as MsDataFilePath;
                // Only .wiff files currently support multiple samples per file.
                // Keep from doing the extra work on other types.
                if (null != msDataFilePath && DataSourceUtil.IsWiffFile(msDataFilePath.FilePath))
                {
                    var paths = GetWiffSubPaths(msDataFilePath.FilePath);
                    if (paths == null)
                        return null;    // An error or user cancellation occurred
                    listPaths.AddRange(paths);
                }
                else
                    listPaths.Add(dataSource);
            }
            return new[] { new KeyValuePair<string, MsDataFileUri[]>(name, listPaths.ToArray()) };
        }

        public KeyValuePair<string, MsDataFileUri[]>[] GetDataSourcePathsFileReplicates(IEnumerable<MsDataFileUri> dataSources)
        {
            var listNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();
            foreach (var dataSource in dataSources)
            {
                MsDataFilePath msDataFilePath = dataSource as MsDataFilePath;
                // Only .wiff files currently support multiple samples per file.
                // Keep from doing the extra work on other types.
                if (null != msDataFilePath && DataSourceUtil.IsWiffFile(msDataFilePath.FilePath))
                {
                    var paths = GetWiffSubPaths(msDataFilePath.FilePath);
                    if (paths == null)
                        return null;    // An error or user cancelation occurred
                    // Multiple paths then add as samples
                    if (paths.Length > 1 ||
                        // If just one, make sure it has a sample part.  Otherwise,
                        // drop through to add the entire file.
                        (paths.Length == 1 && paths[0].SampleName != null))
                    {
                        foreach (var path in paths)
                        {
                            listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(
                                                   path.SampleName, new[]{ path }));
                        }
                        continue;
                    }
                }
                listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(
                                       dataSource.GetFileNameWithoutExtension(), new[] { dataSource }));
            }
            return listNamedPaths.ToArray();
        }

        private MsDataFilePath[] GetWiffSubPaths(string filePath)
        {
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = FileUIResources.ImportResultsDlg_GetWiffSubPaths_Sample_Names;
                longWaitDlg.Message = string.Format(FileUIResources.ImportResultsDlg_GetWiffSubPaths_Reading_sample_names_from__0__, 
                    Path.GetFileName(filePath));
                string[] dataIds = null;
                try
                {
                    longWaitDlg.PerformWork(this, 800, () => dataIds = MsDataFileImpl.ReadIds(filePath));
                }
                catch (Exception x)
                {
                    string message = TextUtil.LineSeparate(
                        string.Format(FileUIResources.ImportResultsDlg_GetWiffSubPaths_An_error_occurred_attempting_to_read_sample_information_from_the_file__0__, filePath),
                        FileUIResources.ImportResultsDlg_GetWiffSubPaths_The_file_may_be_corrupted_missing_or_the_correct_libraries_may_not_be_installed,
                        x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                }

                return DataSourceUtil.GetWiffSubPaths(filePath, dataIds, ChooseSamples);
            }
        }

        private IEnumerable<int> ChooseSamples(string dataSource, IEnumerable<string> sampleNames)
        {
            using (var dlg = new ImportResultsSamplesDlg(dataSource, sampleNames))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    return dlg.SampleIndices;
                return null;
            }
        }

        public KeyValuePair<string, MsDataFileUri[]>[] GetDataSourcePathsDir()
        {
            string initialDir = Path.GetDirectoryName(_documentSavedPath);
            // A saved results folder for this document path supersedes the document directory
            var dlgState = Settings.Default.OpenDataSourceState;
            if (dlgState != null && Equals(dlgState.DocumentPath, _documentSavedPath))
                initialDir = dlgState.InitialDirectory;

            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = FileUIResources.ImportResultsDlg_GetDataSourcePathsDir_Results_Directory;
                dlg.ShowNewFolderButton = false;
                dlg.SelectedPath = initialDir;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return null;

                string dirRoot = dlg.SelectedPath;

                if (dlgState != null && Equals(dlgState.DocumentPath, _documentSavedPath))
                    dlgState.InitialDirectory = dirRoot;

                KeyValuePair<string, MsDataFileUri[]>[] namedPaths = DataSourceUtil.GetDataSourcesInSubdirs(dirRoot).ToArray();
                if (namedPaths.Length == 0)
                {
                    MessageDlg.Show(this,
                        string.Format(FileUIResources.ImportResultsDlg_GetDataSourcePathsDir_No_results_found_in_the_folder__0__, dirRoot));
                    return null;
                }
                return namedPaths;
            }
        }

        private bool ResultsExist(string name)
        {
            foreach (var item in comboName.Items)
            {
                if (Equals(name, item.ToString()))
                    return true;
            }
            return false;
        }

        private void EnsureUniqueNames()
        {
            var setUsedNames = new HashSet<string>();
            foreach (var item in comboName.Items)
                setUsedNames.Add(item.ToString());
            var names = Helpers.EnsureUniqueNames(NamedPathSets.Select(n => n.Key).ToList(), setUsedNames);
            for (int i = 0; i < NamedPathSets.Length; i++)
            {
                var namedPathSet = NamedPathSets[i];
                string baseName = namedPathSet.Key;
                string name = names[i];
                if (!Equals(name, baseName))
                    NamedPathSets[i] = new KeyValuePair<string, MsDataFileUri[]>(name, namedPathSet.Value);
            }
        }

        private void radioCreateMultiple_CheckedChanged(object sender, EventArgs e)
        {
            if (radioCreateMultiple.Checked)
                UpdateRadioSelection();
        }

        private void radioCreateMultipleMulti_CheckedChanged(object sender, EventArgs e)
        {
            if (radioCreateMultipleMulti.Checked)
                UpdateRadioSelection();
        }

        private void radioCreateNew_CheckedChanged(object sender, EventArgs e)
        {
            if (radioCreateNew.Checked)
            {
                UpdateRadioSelection();
                textName.Focus();
                textName.SelectAll();
            }
        }

        private void radioAddExisting_CheckedChanged(object sender, EventArgs e)
        {
            if (radioAddExisting.Checked)
                UpdateRadioSelection();
        }

        private void UpdateRadioSelection()
        {
            if (radioAddExisting.Checked)
            {
                textName.Text = string.Empty;
                textName.Enabled = labelNameNew.Enabled = false;
                comboName.Enabled = labelNameAdd.Enabled = true;
                // If there is only one option, then select it.
                if (comboName.Items.Count == 1)
                    comboName.SelectedIndex = 0;
            }
            else
            {
                comboName.SelectedIndex = -1;
                comboName.Enabled = labelNameAdd.Enabled = false;
                bool multiple = IsMultiple;
                textName.Enabled = labelNameNew.Enabled = !multiple;
                textName.Text = multiple ? string.Empty : DefaultNewName;
            }

            // If comboOptimizing is not supposed to be below radioCreateMulti and it is
            if (!radioCreateMultiple.Checked && !radioAddExisting.Checked  && comboOptimizing.Top < radioCreateMultipleMulti.Top)
            {
                // Move it to below radioCreateMultipleMulti
                radioCreateMultipleMulti.Top = radioCreateMultiple.Top + (radioCreateNew.Top - radioCreateMultipleMulti.Top);
                int shiftHeight = radioCreateMultipleMulti.Top - radioCreateMultiple.Top;
                labelOptimizing.Top += shiftHeight;
                comboOptimizing.Top += shiftHeight;
                labelTuning.Top = labelOptimizing.Top;
                comboTuning.Top = comboOptimizing.Top;
            }
            // If comboOptimizing is not supposed to be below radioCreateNew and it is
            if (!radioCreateNew.Checked && comboOptimizing.Top > radioCreateNew.Top)
            {
                // Move it to below radioCreateMultipleMulti
                int shiftHeight = radioAddExisting.Top - labelOptimizing.Top;
                textName.Top += shiftHeight;
                labelNameNew.Top += shiftHeight;
                radioCreateNew.Top += shiftHeight;
                shiftHeight = radioCreateNew.Top - labelOptimizing.Top - shiftHeight;
                labelOptimizing.Top += shiftHeight;
                comboOptimizing.Top += shiftHeight;
                labelTuning.Top = labelOptimizing.Top;
                comboTuning.Top = comboOptimizing.Top;
            }
            // If comboOptimizing is supposed to be below radioCreateNew, but it is not
            if (radioCreateNew.Checked && comboOptimizing.Top < radioCreateNew.Top)
            {
                // Move it to below radioCreateNew, starting from being below radioCreateMultipleMulti
                int shiftHeight = radioCreateNew.Top - labelOptimizing.Top;
                radioCreateNew.Top -= shiftHeight;
                labelNameNew.Top -= shiftHeight;
                textName.Top -= shiftHeight;
                shiftHeight = radioAddExisting.Top - labelOptimizing.Top - shiftHeight;
                labelOptimizing.Top += shiftHeight;
                comboOptimizing.Top += shiftHeight;
                labelTuning.Top = labelOptimizing.Top;
                comboTuning.Top = comboOptimizing.Top;
            }
            // If comboOptimizing is supposed to be below radioCreateMultiple, but it is not
            if ((radioCreateMultiple.Checked || radioAddExisting.Checked) &&
                comboOptimizing.Top > radioCreateMultipleMulti.Top)
            {
                // Move it to below radioCreateMultiple, starting from being below radioCreateMultipleMulti
                int shiftHeight = radioCreateMultipleMulti.Top - labelOptimizing.Top;
                labelOptimizing.Top += shiftHeight;
                comboOptimizing.Top += shiftHeight;
                labelTuning.Top = labelOptimizing.Top;
                comboTuning.Top = comboOptimizing.Top;
                radioCreateMultipleMulti.Top = radioCreateNew.Top - (radioCreateMultipleMulti.Top - radioCreateMultiple.Top);
            }

            if (radioAddExisting.Checked)
            {
                comboOptimizing.Enabled = labelOptimizing.Enabled = comboTuning.Enabled = labelTuning.Enabled = false;
                comboOptimizing.SelectedIndex = comboTuning.SelectedIndex = -1;
            }
            else
            {
                comboOptimizing.Enabled = labelOptimizing.Enabled = comboTuning.Enabled = labelTuning.Enabled = true;
                if (comboTuning.Items.Count > 0)
                {
                    comboOptimizing.SelectedIndex = comboTuning.SelectedIndex = 0;
                }
            }
        }

        public int ImportSimultaneousIndex
        {
            get { return comboSimultaneousFiles.SelectedIndex;}
            set { comboSimultaneousFiles.SelectedIndex = value;}
        }

        public class ImportResultsSettings : AuditLogOperationSettings<ImportResultsSettings>, IAuditLogComparable
        {
            public static ImportResultsSettings EMPTY = new ImportResultsSettings(null, false, false, false, string.Empty, false,
                () => ExportOptimize.NONE, MultiFileLoader.ImportResultsSimultaneousFileOptions.one_at_a_time, null, null);

            private readonly Func<string> _optimizeFunc;

            public ImportResultsSettings(List<string> fileNames, bool singleInjectionReplicates, bool multiInjectionReplicates,
                bool addNewReplicate, string replicateName, bool addToExistingReplicate, Func<string> optimizeFunc,
                MultiFileLoader.ImportResultsSimultaneousFileOptions fileImportOption, string prefix, string suffix)
            {
                FileNames = fileNames == null
                    ? new List<AuditLogPath>()
                    : fileNames.Select(AuditLogPath.Create).ToList();
                SingleInjectionReplicates = singleInjectionReplicates;
                MultiInjectionReplicates = multiInjectionReplicates;
                AddNewReplicate = addNewReplicate;
                ReplicateName = replicateName;
                AddToExistingReplicate = addToExistingReplicate;
                _optimizeFunc = optimizeFunc;
                FileImportOption = fileImportOption;
                Prefix = prefix;
                Suffix = suffix;
            }

            protected override AuditLogEntry CreateEntry(SrmDocumentPair docPair)
            {
                var entry = AuditLogEntry.CreateCountChangeEntry(MessageType.imported_result,
                    MessageType.imported_results, docPair.NewDocumentType, FileNames, MessageArgs.DefaultSingular, null);

                return entry.Merge(base.CreateEntry(docPair), false);
            }

            [Track]
            public List<AuditLogPath> FileNames { get; private set; }
            [Track]
            public bool SingleInjectionReplicates { get; private set; }
            [Track]
            public bool MultiInjectionReplicates { get; private set; }
            [Track]
            public bool AddNewReplicate { get; private set; }
            [Track]
            public string ReplicateName { get; private set; }
            [Track]
            public bool AddToExistingReplicate { get; private set; }
            [Track]
            public string Optimization { get {return _optimizeFunc();} }
            [Track(ignoreDefaultParent:true)]
            public MultiFileLoader.ImportResultsSimultaneousFileOptions FileImportOption { get; private set; }
            [Track]
            public string Prefix { get; private set; }
            [Track]
            public string Suffix { get; private set; }

            public object GetDefaultObject(ObjectInfo<object> info)
            {
                return EMPTY;
            }
        }

        public ImportResultsSettings FormSettings
        {
            get
            {
                var name = ImportResultsSettings.EMPTY.ReplicateName;
                if ((RadioAddNewChecked || RadioAddExistingChecked) && NamedPathSets.Length == 1)
                    name = NamedPathSets[0].Key;

                return new ImportResultsSettings(NamedPathSets.SelectMany(pair => pair.Value.Select(fileUri => fileUri.GetFilePath())).ToList(), RadioCreateMultipleChecked, RadioCreateMultipleMultiChecked,
                    RadioAddNewChecked, name, RadioAddExistingChecked, () => OptimizationName,
                    (MultiFileLoader.ImportResultsSimultaneousFileOptions) ImportSimultaneousIndex,
                    Prefix, Suffix);
            }
        }

        public bool RadioAddNewChecked
        {
            get { return radioCreateNew.Checked; }
            set { radioCreateNew.Checked = value; }
        }

        public bool RadioAddExistingChecked
        {
            get { return radioAddExisting.Checked; }
            set { radioAddExisting.Checked = value; }
        }

        public bool RadioCreateMultipleChecked
        {
            get { return radioCreateMultiple.Checked; }
            set { radioCreateMultiple.Checked = value; }
        }

        public bool RadioCreateMultipleMultiChecked
        {
            get { return radioCreateMultipleMulti.Checked; }
            set { radioCreateMultipleMulti.Checked = value; }
        }

        public string ReplicateName
        {
            get { return textName.Text; }
            set { textName.Text = value ?? string.Empty; }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void cbShowAllChromatograms_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.AutoShowAllChromatogramsGraph = cbShowAllChromatograms.Checked;
        }

        private void comboOptimizing_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selected = comboOptimizing.SelectedItem;
            labelTuning.Visible = comboTuning.Visible = selected != null && selected.ToString().Equals(ExportOptimize.COV);
        }

        public bool CanExportCov { get { return comboOptimizing.Items.Contains(ExportOptimize.COV); } }
        public bool CanOptimizeFine { get { return CanExportCov && comboTuning.Items.Contains(ExportOptimize.COV_FINE); } }
        public bool CanOptimizeMedium { get { return CanExportCov && comboTuning.Items.Contains(ExportOptimize.COV_MEDIUM); } }
    }
}
