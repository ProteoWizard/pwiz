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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportResultsDlg : FormEx
    {
        private const string DEFAULT_NAME = "Chromatograms";

        // Number of transitions below which the user gets a warning about
        // multiple injection features.
        private const int MIN_MULTIPLE_TRANSITIONS = 60;

        private readonly string _documentSavedPath;
        private readonly bool _warnOnMultiInjection;

        public ImportResultsDlg(SrmDocument document, string savedPath)
        {
            _documentSavedPath = savedPath;
            _warnOnMultiInjection = (document.TransitionCount < MIN_MULTIPLE_TRANSITIONS);

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
        }

        private string DefaultNewName
        {
            get
            {
                string name = DEFAULT_NAME;
                if (ResultsExist(name))
                {
                    int i = 2;
                    do
                    {
                        name = DEFAULT_NAME + i++;
                    }
                    while (ResultsExist(name));
                }
                return name;
            }
        }

        private bool IsMultiple { get { return radioCreateMultiple.Checked || radioCreateMultipleMulti.Checked; } }

        private bool IsOptimizing { get { return comboOptimizing.SelectedIndex != -1; } }

        public KeyValuePair<string, string[]>[] NamedPathSets { get; set; }

        public string OptimizationName
        {
            get
            {
                return comboOptimizing.SelectedIndex != -1
                           ?
                               comboOptimizing.SelectedItem.ToString()
                           :
                               ExportOptimize.NONE;
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
                comboOptimizing.SelectedItem = value;
            }
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            if (NamedPathSets == null)
            {
                if (radioAddExisting.Checked)
                {
                    if (comboName.SelectedIndex == -1)
                    {
                        MessageBox.Show(this, "You must select an existing set of results to which to append new data.", Program.Name);
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
                    if (!helper.ValidateNameTextBox(e, textName, out name))
                        return;
                    if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                    {
                        helper.ShowTextBoxError(e, textName, "A result name may not contain any of the characters '{0}'.", Path.GetInvalidFileNameChars());
                        return;
                    }
                    if (ResultsExist(name))
                    {
                        helper.ShowTextBoxError(e, textName, "The specified name already exists for this document.");
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
                string prefix = GetCommonPrefix(Array.ConvertAll(NamedPathSets, ns => ns.Key));
                if (prefix.Length > 2)
                {
                    using (var dlgName = new ImportResultsNameDlg(prefix))
                    {
                        var result = dlgName.ShowDialog(this);
                        if (result == DialogResult.Cancel)
                        {
                            return;
                        }
                        if (result == DialogResult.Yes)
                        {
                            // Rename all the replicates to remove the specified prefix.
                            for (int i = 0; i < NamedPathSets.Length; i++)
                            {
                                var namedSet = NamedPathSets[i];
                                NamedPathSets[i] = new KeyValuePair<string, string[]>(
                                    namedSet.Key.Substring(dlgName.Prefix.Length), namedSet.Value);
                            }
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

        private bool CanCreateMultiInjectionMethods()
        {
            if (_warnOnMultiInjection && !IsOptimizing)
            {
                if (MessageBox.Show(this,
                                "The current document does not appear to have enough transitions to require multiple injections.\nAre you sure you want to continue?",
                                Program.Name, MessageBoxButtons.YesNo, MessageBoxIcon.None, MessageBoxDefaultButton.Button2) == DialogResult.No)
                    return false;
            }
            return true;
        }

        public KeyValuePair<string, string[]>[] GetDataSourcePathsFile(string name)
        {
            CheckDisposed();
            OpenDataSourceDialog dlgOpen = new OpenDataSourceDialog
                                               {
                                                   Text = "Import Results Files"
                                               };
            // The dialog expects null to mean no directory was supplied, so don't assign
            // an empty string.
            string initialDir = Path.GetDirectoryName(_documentSavedPath);
            dlgOpen.InitialDirectory = initialDir;
            // Use saved source type, if there is one.
            string sourceType = Settings.Default.SrmResultsSourceType;
            if (!string.IsNullOrEmpty(sourceType))
                dlgOpen.SourceTypeName = sourceType;

            if (dlgOpen.ShowDialog(this) != DialogResult.OK)
                return null;

            Settings.Default.SrmResultsDirectory = !Equals(_documentSavedPath, dlgOpen.CurrentDirectory)
                                                       ? dlgOpen.CurrentDirectory
                                                       : "";
            Settings.Default.SrmResultsSourceType = dlgOpen.SourceTypeName;

            string[] dataSources = dlgOpen.DataSources;

            if (dataSources == null || dataSources.Length == 0)
            {
                MessageBox.Show(this, "No results files chosen.", Program.Name);
                return null;
            }

            if (name != null)
                return GetDataSourcePathsFileSingle(name, dataSources);

            return GetDataSourcePathsFileReplicates(dataSources);
        }

        private KeyValuePair<string, string[]>[] GetDataSourcePathsFileSingle(string name, IEnumerable<string> dataSources)
        {
            var listPaths = new List<string>();
            foreach (string dataSource in dataSources)
            {
                // Only .wiff files currently support multiple samples per file.
                // Keep from doing the extra work on other types.
                if (DataSourceUtil.IsWiffFile(dataSource))
                {
                    string[] paths = GetWiffSubPaths(dataSource);
                    if (paths == null)
                        return null;    // An error or user cancelation occurred
                    listPaths.AddRange(paths);
                }
                else
                    listPaths.Add(dataSource);
            }
            return new[] { new KeyValuePair<string, string[]>(name, listPaths.ToArray()) };
        }

        private KeyValuePair<string, string[]>[] GetDataSourcePathsFileReplicates(IEnumerable<string> dataSources)
        {
            var listNamedPaths = new List<KeyValuePair<string, string[]>>();
            foreach (string dataSource in dataSources)
            {
                // Only .wiff files currently support multiple samples per file.
                // Keep from doing the extra work on other types.
                if (DataSourceUtil.IsWiffFile(dataSource))
                {
                    string[] paths = GetWiffSubPaths(dataSource);
                    if (paths == null)
                        return null;    // An error or user cancelation occurred
                    // Multiple paths then add as samples
                    if (paths.Length > 1 ||
                        // If just one, make sure it has a sample part.  Otherwise,
                        // drop through to add the entire file.
                        (paths.Length == 1 && SampleHelp.GetPathSampleNamePart(paths[0]) != null))
                    {
                        foreach (string path in paths)
                        {
                            listNamedPaths.Add(new KeyValuePair<string, string[]>(
                                                   SampleHelp.GetPathSampleNamePart(path), new[] { path }));                            
                        }
                        continue;
                    }
                }
                listNamedPaths.Add(new KeyValuePair<string, string[]>(
                                       Path.GetFileNameWithoutExtension(dataSource), new[] { dataSource }));
            }
            return listNamedPaths.ToArray();
        }

        private string[] GetWiffSubPaths(string filePath)
        {
            var longWaitDlg = new LongWaitDlg
            {
                Text = "Sample Names",
                Message = string.Format("Reading sample names from {0}", Path.GetFileName(filePath))
            };

            string[] dataIds = null;
            try
            {
                longWaitDlg.PerformWork(this, 800, () => dataIds = MsDataFileImpl.ReadIds(filePath));
            }
            catch (Exception x)
            {
                MessageDlg.Show(this, string.Format("An error occurred attempting to read sample information from the file {0}.\nThe file may be corrupted, missing, or the correct libraries may not be installed.\n{1}", filePath, x.Message));
            }

            return DataSourceUtil.GetWiffSubPaths(filePath, dataIds, ChooseSamples);
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

        public KeyValuePair<string, string[]>[] GetDataSourcePathsDir()
        {
            string initialDir = Path.GetDirectoryName(_documentSavedPath);
            FolderBrowserDialog dlg = new FolderBrowserDialog
            {
                Description = "Results Directory",
                ShowNewFolderButton = false,
                SelectedPath = initialDir
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return null;

            string dirRoot = dlg.SelectedPath;

            Settings.Default.SrmResultsDirectory = dirRoot;

            KeyValuePair<string, string[]>[] namedPaths = DataSourceUtil.GetDataSourcesInSubdirs(dirRoot).ToArray();
            if (namedPaths.Length == 0)
            {
                MessageBox.Show(this, string.Format("No results found in the folder {0}.", dirRoot), Program.Name);
                return null;
            }
            return namedPaths;
        }

        private static string GetCommonPrefix(IEnumerable<string> values)
        {
            string prefix = null;
            foreach (string value in values)
            {
                if (prefix == null)
                {
                    prefix = value;
                    continue;
                }
                if (prefix == "")
                {
                    break;
                }

                for (int i = 0; i < prefix.Length; i++)
                {
                    if (i >= value.Length || prefix[i] != value[i])
                    {
                        prefix = prefix.Substring(0, i);
                        break;
                    }
                }
            }
            return prefix ?? "";
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
            for (int i = 0; i < NamedPathSets.Length; i++)
            {
                var namedPathSet = NamedPathSets[i];
                string baseName = namedPathSet.Key;
                // Make sure the next name added is unique
                string name = (baseName.Length != 0 ? baseName : "1");
                for (int suffix = 2; setUsedNames.Contains(name); suffix++)
                    name = baseName + suffix;
                // If a change was made, update the named path sets
                if (!Equals(name, baseName))
                    NamedPathSets[i] = new KeyValuePair<string, string[]>(name, namedPathSet.Value);
                // Add this name to the used set
                setUsedNames.Add(name);
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
                UpdateRadioSelection();
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
                textName.Text = "";
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
                textName.Text = multiple ? "" : DefaultNewName;
            }

            if (radioCreateMultipleMulti.Checked)
            {
                if (comboOptimizing.Top > radioCreateNew.Top)
                {
                    int shiftHeight = radioAddExisting.Top - labelOptimizing.Top;
                    textName.Top += shiftHeight;
                    labelNameNew.Top += shiftHeight;
                    radioCreateNew.Top += shiftHeight;
                    shiftHeight = radioCreateNew.Top - labelOptimizing.Top - shiftHeight;
                    labelOptimizing.Top += shiftHeight;
                    comboOptimizing.Top += shiftHeight;
                    comboOptimizing.Enabled = labelOptimizing.Enabled = true;
                    comboOptimizing.SelectedIndex = 0;
                }
            }
            else
            {
                // Make sure optimizing combo is below radioCreateNew
                if (comboOptimizing.Top < radioCreateNew.Top)
                {
                    int shiftHeight = radioCreateNew.Top - labelOptimizing.Top;
                    radioCreateNew.Top -= shiftHeight;
                    labelNameNew.Top -= shiftHeight;
                    textName.Top -= shiftHeight;
                    shiftHeight = radioAddExisting.Top - labelOptimizing.Top - shiftHeight;
                    labelOptimizing.Top += shiftHeight;
                    comboOptimizing.Top += shiftHeight;
                }
                if (radioCreateNew.Checked)
                {
                    comboOptimizing.Enabled = labelOptimizing.Enabled = true;
                    comboOptimizing.SelectedIndex = 0;
                }
                else
                {
                    comboOptimizing.Enabled = labelOptimizing.Enabled = false;
                    comboOptimizing.SelectedIndex = -1;
                }
            }
        }

        public bool RadioAddNewChecked
        {
            get { return radioCreateNew.Checked;}
            set { radioCreateNew.Checked = value; }
        }

        public bool RadioAddExistingChecked
        {
            get { return radioAddExisting.Checked; }
            set { radioAddExisting.Checked = value; }
        }

        public bool RadioCreateMultipleMultiChecked
        {
            get { return radioCreateMultipleMulti.Checked; }
            set { radioCreateMultipleMulti.Checked = value; }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
