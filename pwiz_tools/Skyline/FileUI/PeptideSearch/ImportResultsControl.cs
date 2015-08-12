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
using NHibernate.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportResultsControl : UserControl, IImportResultsControl
    {
        public ImportResultsControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;

            InitializeComponent();
        }

        public event EventHandler<ResultsFilesEventArgs> ResultsFilesChanged;
        private SkylineWindow SkylineWindow { get; set; }
        private Form WizardForm { get { return FormEx.GetParentForm(this); } }

        public List<FoundResultsFile> FoundResultsFiles
        {
            get
            {
                return !ExcludeSpectrumSourceFiles
                    ? SpectrumSourceFiles.Values.Where(s => s.HasMatch).Select(s => new FoundResultsFile(s.Name, s.ExactMatch ?? s.AlternateMatch)).ToList()
                    : SpectrumSourceFiles.Values.Where(s => s.HasAlternateMatch).Select(s => new FoundResultsFile(s.Name, s.AlternateMatch)).ToList();
            }
            set
            {
                SpectrumSourceFiles = value.ToDictionary(v => v.Name, v => new FoundResultsFilePossibilities(v.Name, v.Path));
            }
        }

        public IEnumerable<string> MissingResultsFiles
        {
            get
            {
                return !ExcludeSpectrumSourceFiles
                    ? SpectrumSourceFiles.Where(s => !s.Value.HasMatch).Select(s => s.Key)
                    : SpectrumSourceFiles.Where(s => !s.Value.HasAlternateMatch).Select(s => s.Key);
            }
        }

        public bool ResultsFilesMissing { get { return MissingResultsFiles.Any(); } }

        private Dictionary<string, FoundResultsFilePossibilities> SpectrumSourceFiles { get; set; }

        private string DocumentDirectory { get { return Path.GetDirectoryName(SkylineWindow.DocumentFilePath); } }
        public bool ExcluedSpectrumSourceFilesVisible { get { return cbExcludeSourceFiles.Visible; } }
        public bool ExcludeSpectrumSourceFiles
        {
            get { return cbExcludeSourceFiles.Checked; }
            set { cbExcludeSourceFiles.Checked = value; }
        }

        public void InitializeChromatogramsPage(Library docLib, string[] searchFileNames)
        {
            SpectrumSourceFiles = new Dictionary<string, FoundResultsFilePossibilities>();

            if (null != docLib)
            {
                var measuredResults = SkylineWindow.DocumentUI.Settings.MeasuredResults;

                foreach (var dataFile in docLib.LibraryDetails.DataFiles)
                {
                    var msDataFilePath = new MsDataFilePath(dataFile);
                    SpectrumSourceFiles[dataFile] = new FoundResultsFilePossibilities(msDataFilePath.GetFileNameWithoutExtension());

                    // If a matching file is already in the document, then don't include
                    // this library spectrum source in the set of files to find.
                    if (measuredResults != null && measuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(dataFile)) != null)
                        continue;

                    if (File.Exists(dataFile) && DataSourceUtil.IsDataSource(dataFile))
                    {
                        // We've found the dataFile in the exact location
                        // specified in the document library, so just add it
                        // to the "FOUND" list.
                        SpectrumSourceFiles[dataFile].ExactMatch = msDataFilePath.ToString();
                    }
                }

                docLib.ReadStream.CloseStream();
            }

            // Search the directory of the document and its parent
            var dirsToSearch = new List<string> {DocumentDirectory};
            DirectoryInfo parentDir = Directory.GetParent(DocumentDirectory);
            if (parentDir != null)
            {
                dirsToSearch.Add(parentDir.ToString());
            }
            if (searchFileNames != null)
            {
                // Search the directories of the search files
                dirsToSearch.AddRange(searchFileNames.Select(Path.GetDirectoryName));
            }
            // Search each subdirectory of the document directory
            dirsToSearch.AddRange(GetSubdirectories(DocumentDirectory));

            UpdateResultsFiles(dirsToSearch, false);
        }

        private void browseToResultsFileButton_Click(object sender, EventArgs e)
        {
            MsDataFileUri[] dataSources;
            using (var dlg = new OpenDataSourceDialog(Settings.Default.ChorusAccountList)
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

            Array.ForEach(dataSources, d => CheckMatch(d.ToString(), true));
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
                dirsToSearch.AddRange(GetSubdirectories(dlg.SelectedPath));
                if (!UpdateResultsFiles(dirsToSearch, true))
                {
                    MessageDlg.Show(WizardForm, Resources.ImportResultsControl_findResultsFilesButton_Click_Could_not_find_all_the_missing_results_files_);
                }
            }
        }

        private static IEnumerable<string> GetSubdirectories(string dir)
        {
            try
            {
                return Directory.EnumerateDirectories(dir);
            }
            catch (Exception)
            {
                // No permissions on folder
                return new string[0];
            }
        }

        public bool UpdateResultsFiles(IEnumerable<string> dirPaths, bool overwrite)
        {
            if (overwrite || !SpectrumSourceFiles.Values.All(s => s.HasMatches))
            {
                var longWaitDlg = new LongWaitDlg {Text = Resources.ImportResultsControl_FindResultsFiles_Searching_for_Results_Files};
                try
                {
                    longWaitDlg.PerformWork(WizardForm, 1000, longWaitBroker => Array.ForEach(dirPaths.Distinct().ToArray(), dir => FindDataFiles(longWaitBroker, dir, overwrite)));
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(WizardForm,
                        TextUtil.LineSeparate(Resources.ImportResultsControl_FindResultsFiles_An_error_occurred_attempting_to_find_results_files_, x.Message), x);
                }
            }

            UpdateResultsFilesUI();

            return !MissingResultsFiles.Any();
        }

        private void FindDataFiles(ILongWaitBroker longWaitBroker, string directory, bool overwrite)
        {
            // Don't search if every spectrum source file has an exact match and an alternate match
            if (directory == null || !Directory.Exists(directory) || (!overwrite && SpectrumSourceFiles.Values.All(s => s.HasMatches)))
                return;

            longWaitBroker.Message = string.Format(Resources.ImportResultsControl_FindResultsFiles_Searching_for_matching_results_files_in__0__, directory);

            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (longWaitBroker.IsCanceled)
                        return;

                    if (entry != null && DataSourceUtil.IsDataSource(entry))
                        CheckMatch(entry, overwrite);
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
                // No permissions on folder
            }
        }

        private void CheckMatch(string potentialMatch, bool overwrite)
        {
            if (string.IsNullOrEmpty(potentialMatch))
                return;

            foreach (var spectrumSourceFile in SpectrumSourceFiles.Keys)
            {
                if (spectrumSourceFile == null || (!overwrite && SpectrumSourceFiles[spectrumSourceFile].HasMatches))
                    continue;

                if (Path.GetFileName(spectrumSourceFile).Equals(Path.GetFileName(potentialMatch)))
                {
                    if (overwrite || !SpectrumSourceFiles[spectrumSourceFile].HasExactMatch)
                    {
                        SpectrumSourceFiles[spectrumSourceFile].ExactMatch = potentialMatch;
                    }
                }
                else if (MeasuredResults.IsBaseNameMatch(Path.GetFileNameWithoutExtension(spectrumSourceFile),
                    Path.GetFileNameWithoutExtension(potentialMatch)) &&
                    (overwrite || !SpectrumSourceFiles[spectrumSourceFile].HasAlternateMatch))
                {
                    SpectrumSourceFiles[spectrumSourceFile].AlternateMatch = potentialMatch;
                }
            }
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
            cbExcludeSourceFiles.Visible = SpectrumSourceFiles.Values.Any(s => s.HasExactMatch);

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

        /// <summary>
        /// Stores possible matches for spectrum source files.
        /// </summary>
        private class FoundResultsFilePossibilities
        {
            /// <summary>
            /// The name of the spectrum source file without extension.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// The path to a match for the spectrum source file where the filename matches exactly.
            /// </summary>
            public string ExactMatch
            {
                get { return _exactMatch; }
                set { _exactMatch = value != null ? Path.GetFullPath(value) : null; }
            }
            private string _exactMatch;

            /// <summary>
            /// The path to a match for the spectrum source file where the filestem matches, but the extension doesn't.
            /// </summary>
            public string AlternateMatch
            {
                get { return _alternateMatch; }
                set { _alternateMatch = value != null ? Path.GetFullPath(value) : null; }
            }
            private string _alternateMatch;

            public FoundResultsFilePossibilities(string name)
            {
                Name = name;
                ExactMatch = null;
                AlternateMatch = null;
            }

            public FoundResultsFilePossibilities(string name, string path)
            {
                Name = name;
                ExactMatch = path;
                AlternateMatch = path;
            }

            public bool HasMatch { get { return HasExactMatch || HasAlternateMatch; } }
            public bool HasMatches { get { return HasExactMatch && HasAlternateMatch; } }
            public bool HasExactMatch { get { return ExactMatch != null; } }
            public bool HasAlternateMatch { get { return AlternateMatch != null; } }
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
