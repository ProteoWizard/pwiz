/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportResultsControl : UserControl, IImportResultsControl
    {
        public ImportResultsControl(ImportPeptideSearch importPeptideSearch, string documentPath)
        {
            ImportPeptideSearch = importPeptideSearch;
            DocumentDirectory = Path.GetDirectoryName(documentPath);

            InitializeComponent();

            SimultaneousFiles = Settings.Default.ImportResultsSimultaneousFiles;
            DoAutoRetry = Settings.Default.ImportResultsDoAutoRetry;
        }

        public event EventHandler<ResultsFilesEventArgs> ResultsFilesChanged;
        private Form WizardForm { get { return FormEx.GetParentForm(this); } }

        public int SimultaneousFiles
        {
            get { return comboSimultaneousFiles.SelectedIndex; }
            set { comboSimultaneousFiles.SelectedIndex = value; }
        }

        public bool DoAutoRetry
        {
            get { return cbAutoRetry.Checked; }
            set { cbAutoRetry.Checked = value; }
        }

        public static List<ImportPeptideSearch.FoundResultsFile> EnsureUniqueNames(List<ImportPeptideSearch.FoundResultsFile> files)
        {
            var result = new List<ImportPeptideSearch.FoundResultsFile>();
            // Enforce uniqueness in names (might be constructed from list of files a.raw, a.mzML)
            var names = ImportResultsDlg.EnsureUniqueNames(files.Select(f => f.Name).ToList());
            for (var i = 0; i < files.Count; i++)
            {
                result.Add(new ImportPeptideSearch.FoundResultsFile(names[i], files[i].Path));
            }
            return result;
        }

        public List<ImportPeptideSearch.FoundResultsFile> FoundResultsFiles
        {
            get { return ImportPeptideSearch.GetFoundResultsFiles(ExcludeSpectrumSourceFiles).ToList(); }
            set
            {
                var files = EnsureUniqueNames(value); // May change names to ensure uniqueness
                ImportPeptideSearch.SpectrumSourceFiles = files.ToDictionary(v => v.Name,
                    v => new ImportPeptideSearch.FoundResultsFilePossibilities(v.Name, v.Path));
            }
        }

        public IEnumerable<string> MissingResultsFiles
        {
            get { return ImportPeptideSearch.GetMissingResultsFiles(ExcludeSpectrumSourceFiles); }
        }

        public bool ResultsFilesMissing { get { return MissingResultsFiles.Any(); } }

        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private string DocumentDirectory { get; set; }
        public bool ExcluedSpectrumSourceFilesVisible { get { return cbExcludeSourceFiles.Visible; } }
        public bool ExcludeSpectrumSourceFiles
        {
            get { return cbExcludeSourceFiles.Checked; }
            set { cbExcludeSourceFiles.Checked = value; }
        }

        public void InitializeChromatogramsPage(SrmDocument document)
        {
            ImportPeptideSearch.InitializeSpectrumSourceFiles(document);
            UpdateResultsFiles(ImportPeptideSearch.GetDirsToSearch(DocumentDirectory), false);
        }

        private void browseToResultsFileButton_Click(object sender, EventArgs e)
        {
            MsDataFileUri[] dataSources;
            using (var dlg = new OpenDataSourceDialog(Settings.Default.RemoteAccountList)
            {
                Text = Resources.ImportResultsControl_browseToResultsFileButton_Click_Import_Peptide_Search,
                InitialDirectory = new MsDataFilePath(DocumentDirectory)
            })
            {
                // Use saved source type, if there is one.
                string sourceType = Settings.Default.SrmResultsSourceType;
                if (!string.IsNullOrEmpty(sourceType))
                    dlg.SourceTypeName = sourceType;

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                dataSources = dlg.DataSources;
            }

            if (dataSources == null || dataSources.Length == 0)
            {
                MessageDlg.Show(this, Resources.ImportResultsDlg_GetDataSourcePathsFile_No_results_files_chosen);
                return;
            }

            Array.ForEach(dataSources, d => ImportPeptideSearch.TryMatch(d.ToString(), true));
            UpdateResultsFilesUI();
        }

        private void findResultsFilesButton_Click(object sender, EventArgs e)
        {
            // Ask the user for the directory to search
            using (var dlg = new FolderBrowserDialog
            {
                Description = Resources.ImportResultsControl_findResultsFilesButton_Click_Results_Directory,
                ShowNewFolderButton = false,
                SelectedPath = Path.GetDirectoryName(DocumentDirectory)
            })
            {
                if (dlg.ShowDialog(WizardForm) != DialogResult.OK)
                    return;

                // See if we're still missing any files, and update UI accordingly
                var dirsToSearch = new List<string> {dlg.SelectedPath};
                dirsToSearch.AddRange(ImportPeptideSearch.GetSubdirectories(dlg.SelectedPath));
                if (!UpdateResultsFiles(dirsToSearch, true))
                {
                    MessageDlg.Show(WizardForm, Resources.ImportResultsControl_findResultsFilesButton_Click_Could_not_find_all_the_missing_results_files_);
                }
            }
        }

        public bool UpdateResultsFiles(IEnumerable<string> dirPaths, bool overwrite)
        {
            using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.ImportResultsControl_FindResultsFiles_Searching_for_Results_Files
                })
            {
                try
                {
                    longWaitDlg.PerformWork(WizardForm, 1000, longWaitBroker =>
                        ImportPeptideSearch.UpdateSpectrumSourceFilesFromDirs(dirPaths, overwrite, longWaitBroker));
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(WizardForm, TextUtil.LineSeparate(
                        Resources.ImportResultsControl_FindResultsFiles_An_error_occurred_attempting_to_find_results_files_,
                        x.Message), x);
                }
            }

            UpdateResultsFilesUI();

            return !MissingResultsFiles.Any();
        }

        private void UpdateResultsFilesUI()
        {
            // Update the results files list boxes
            UpdateResultsFilesList(FoundResultsFiles.Select(kvp => kvp.Path), listResultsFilesFound);
            UpdateResultsFilesList(MissingResultsFiles, listResultsFilesMissing);

            bool allFilesFound = !MissingResultsFiles.Any();
            resultsSplitContainer.Panel1.Dock = allFilesFound ? DockStyle.Fill : DockStyle.None;
            resultsSplitContainer.Panel2.Visible = !allFilesFound;

            // If any match has an exact match, the "Exclude spectrum source files" checkbox should be visible
            cbExcludeSourceFiles.Visible = ImportPeptideSearch.SpectrumSourceFiles.Values.Any(s => s.HasExactMatch);

            // Fire ResultsFilesChanged, if it has been set
            if (ResultsFilesChanged != null)
            {
                ResultsFilesChanged(this, new ResultsFilesEventArgs(FoundResultsFiles.Count));
            }
        }

        private static void UpdateResultsFilesList(IEnumerable<string> resultsFiles, ListBox resultsFilesList)
        {
            var fileNames = resultsFiles.Where(f => !string.IsNullOrEmpty(f)).ToArray();
            string dirInputRoot = PathEx.GetCommonRoot(fileNames);
            resultsFilesList.Items.Clear();
            foreach (string fileSuffix in fileNames.Select(fileName => fileName.StartsWith(dirInputRoot)
                ? fileName.Substring(dirInputRoot.Length)
                : fileName))
            {
                resultsFilesList.Items.Add(fileSuffix);
            }
        }

        private void cbExcludeSourceFiles_CheckedChanged(object sender, EventArgs e)
        {
            UpdateResultsFilesUI();
        }

        public class ResultsFilesEventArgs : EventArgs
        {

            public ResultsFilesEventArgs(int numFoundFiles)
            {
                NumFoundFiles = numFoundFiles;
            }

            public int NumFoundFiles { get; private set; }
        }
    }
}
