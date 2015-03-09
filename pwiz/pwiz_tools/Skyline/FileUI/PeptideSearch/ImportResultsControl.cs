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

        private void FireResultsFilesChanged(ResultsFilesEventArgs e)
        {
            if (ResultsFilesChanged != null)
            {
                ResultsFilesChanged(this, e);
            }
        }

        private SkylineWindow SkylineWindow { get; set; }
        private Form WizardForm { get { return FormEx.GetParentForm(this); } }

        public List<FoundResultsFile> FoundResultsFiles { get; set; }
        public bool ResultsFilesMissing { get { return MissingResultsFiles.Any(); } }
        private List<string> MissingResultsFiles { get; set; }

        public void InitializeChromatogramsPage(Library docLib)
        {
            FoundResultsFiles = new List<FoundResultsFile>();
            MissingResultsFiles = new List<string>();
            if (null != docLib)
            {
                var measuredResults = SkylineWindow.DocumentUI.Settings.MeasuredResults;

                foreach (var dataFile in docLib.LibraryDetails.DataFiles)
                {
                    // If a matching file is already in the document, then don't include
                    // this library spectrum source in the set of files to find.
                    if (measuredResults != null && measuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(dataFile)) != null)
                        continue;

                    if (File.Exists(dataFile) && DataSourceUtil.IsDataSource(dataFile))
                    {
                        // We've found the dataFile in the exact location
                        // specified in the document library, so just add it
                        // to the "FOUND" list.
                        AddFoundResultsFile(new MsDataFilePath(dataFile));
                    }
                    else
                    {
                        MissingResultsFiles.Add(dataFile);
                    }
                }

                docLib.ReadStream.CloseStream();
            }

            UpdateResultsFiles(Path.GetDirectoryName(SkylineWindow.DocumentFilePath));
        }

        private void browseToResultsFileButton_Click(object sender, EventArgs e)
        {
            OpenDataSourceDialog dlgOpen = new OpenDataSourceDialog(Settings.Default.ChorusAccountList)
                                               {
                                                   Text =
                                                       Resources.ImportResultsControl_browseToResultsFileButton_Click_Import_Peptide_Search
                                               };

            // The dialog expects null to mean no directory was supplied, so don't assign
            // an empty string.
            string initialDir = Path.GetDirectoryName(SkylineWindow.DocumentFilePath);
            dlgOpen.InitialDirectory = new MsDataFilePath(initialDir);

            // Use saved source type, if there is one.
            string sourceType = Settings.Default.SrmResultsSourceType;
            if (!string.IsNullOrEmpty(sourceType))
                dlgOpen.SourceTypeName = sourceType;

            if (dlgOpen.ShowDialog(this) != DialogResult.OK)
                return;

            var dataSources = dlgOpen.DataSources;

            if (dataSources == null || dataSources.Length == 0)
            {
                MessageDlg.Show(this, Resources.ImportResultsDlg_GetDataSourcePathsFile_No_results_files_chosen,
                                Program.Name);
                return;
            }

            foreach (var dataSource in dataSources)
            {
                string dataSourceFileName = dataSource.GetFileName();
                foreach (var item in MissingResultsFiles)
                {
                    if (null != item)
                    {
                        if (Equals(item, dataSource.ToString()) ||
                            MeasuredResults.IsBaseNameMatch(Path.GetFileNameWithoutExtension(item),
                                                            Path.GetFileNameWithoutExtension(dataSourceFileName)))
                        {
                            AddFoundResultsFile(dataSource);
                            MissingResultsFiles.Remove(item);
                            break;
                        }
                    }
                }

                if (MissingResultsFiles.Count == 0)
                {
                    break;
                }
            }

            UpdateResultsFilesUI(MissingResultsFiles.Count == 0);
            FireResultsFilesChanged(new ResultsFilesEventArgs(FoundResultsFiles.Count));
        }

        private void findResultsFilesButton_Click(object sender, EventArgs e)
        {
            // Ask the user for the directory to search
            string initialDir = Path.GetDirectoryName(Path.GetDirectoryName(SkylineWindow.DocumentFilePath));
            using (var dlg = new FolderBrowserDialog
                {
                    Description = Resources.ImportResultsControl_findResultsFilesButton_Click_Results_Directory,
                    ShowNewFolderButton = false,
                    SelectedPath = initialDir
                })
            {
                if (dlg.ShowDialog(WizardForm) != DialogResult.OK)
                    return;

                // See if we're still missing any files, and update UI accordingly
                if (!UpdateResultsFiles(dlg.SelectedPath))
                {
                    MessageDlg.Show(WizardForm, Resources.ImportResultsControl_findResultsFilesButton_Click_Could_not_find_all_the_missing_results_files_);
                }
            }
        }

        public bool UpdateResultsFiles(string dirPath)
        {
            // Create a map for the missing results files, where 
            // "missingFiles[key].Found = false" means the file "key" is missing
            Dictionary<string, ResultsFileFindInfo> missingFiles = new Dictionary<string, ResultsFileFindInfo>();
            foreach (var item in MissingResultsFiles)
            {
                missingFiles.Add(item, new ResultsFileFindInfo(null, false));
            }

            // Add files that were found to the "found results files" list,
            // and remove it from the "missing results files" list.
            bool allFilesFound = FindResultsFiles(dirPath, missingFiles);
            foreach (var item in missingFiles.Keys.Where(item => missingFiles[item].Found))
            {
                AddFoundResultsFile(missingFiles[item].Path);
                MissingResultsFiles.Remove(item);
            }

            UpdateResultsFilesUI(allFilesFound);

            FireResultsFilesChanged(new ResultsFilesEventArgs(listResultsFilesFound.Items.Count));
            return allFilesFound;
        }

        public bool FindResultsFiles(string dirSearch, Dictionary<string, ResultsFileFindInfo> missingFiles)
        {
            int numMissingFiles = missingFiles.Count;
            LongWaitDlg longWaitDlg = new LongWaitDlg
            {
                Text = Resources.ImportResultsControl_FindResultsFiles_Searching_for_Results_Files,
                Message = string.Format(Resources.ImportResultsControl_FindResultsFiles_Searching_for_matching_results_files_in__0__, dirSearch)
            };
            try
            {
                longWaitDlg.PerformWork(WizardForm, 1000, longWaitBroker =>
                    FindDataFilesStartingWith(longWaitBroker, missingFiles, dirSearch, ref numMissingFiles)
                );
            }
            catch (Exception x)
            {
                MessageDlg.Show(WizardForm, TextUtil.LineSeparate(Resources.ImportResultsControl_FindResultsFiles_An_error_occurred_attempting_to_find_results_files_, x.Message));
            }

            return numMissingFiles == 0;
        }

        private void FindDataFilesStartingWith(ILongWaitBroker longWaitBroker,
            Dictionary<string, ResultsFileFindInfo> missingFiles, string initialDirectory, ref int numMissingFiles)
        {
            var dirs = new List<string> { initialDirectory };
            // Also handle the case where the file being searched against is in a completely different directory
            foreach (var missingFile in missingFiles)
            {
                var dirname = Path.GetDirectoryName(missingFile.Key);
                if (dirname != null && !dirs.Contains(dirname))
                {
                    dirs.Add(dirname);
                }
            }
            foreach (var directory in dirs)
            {
                FindDataFiles(longWaitBroker, missingFiles, directory, ref numMissingFiles);
            }
        }

        private void FindDataFiles(ILongWaitBroker longWaitBroker, Dictionary<string, ResultsFileFindInfo> missingFiles, string directory, ref int numMissingFiles)
        {
            SearchForDataFilesInDirectory(longWaitBroker, missingFiles, directory, ref numMissingFiles);
            if (numMissingFiles == 0)
            {
                return;
            }

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(directory);
            }
            catch (Exception)
            {
                return;
            }

            foreach (string dir in dirs)
            {
                if (longWaitBroker.IsCanceled)
                {
                    break;
                }

                // Don't look inside directories which are actually data sources,
                // but do see if such directories have a basename match
                if (DataSourceUtil.IsDataSource(dir))
                {
                    CheckBasenameMatch(dir, missingFiles, ref numMissingFiles);
                    if (numMissingFiles == 0)
                    {
                        return;
                    }
                    continue;
                }

                longWaitBroker.Message = String.Format(Resources.ImportResultsControl_FindDataFiles_Searching_for_missing_result_files_in__0__, dir);
                FindDataFiles(longWaitBroker, missingFiles, dir, ref numMissingFiles);
                if (numMissingFiles == 0)
                {
                    return;
                }
            }
        }

        private bool CheckBasenameMatch(string filePath, Dictionary<string, ResultsFileFindInfo> missingFiles, ref int numMissingFiles)
        {
            var fileName = Path.GetFileName(filePath);
            foreach (var item in missingFiles.Keys.Where(item => !missingFiles[item].Found))
            {
                if (null == item) // For ReSharper
                    continue;

                if (Equals(item, fileName) ||
                    MeasuredResults.IsBaseNameMatch(Path.GetFileNameWithoutExtension(item),
                        Path.GetFileNameWithoutExtension(fileName)))
                {
                    missingFiles[item].Found = true;
                    missingFiles[item].Path = new MsDataFilePath(filePath);
                    numMissingFiles--;
                    return true;
                }
            }
            return false;
        }

        private void SearchForDataFilesInDirectory(ILongWaitBroker longWaitBroker, Dictionary<string, ResultsFileFindInfo> missingFiles, string dir, ref int numMissingFiles)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch (Exception)
            {
                // No permissions on folder
                return;
            }
            foreach (string file in files)
            {
                if (longWaitBroker.IsCanceled)
                {
                    break;
                }

                if (!DataSourceUtil.IsDataSource(file))
                    continue;

                // Check to see if the file matches any of the missing files
                if (CheckBasenameMatch(file, missingFiles, ref numMissingFiles))
                {
                    if (numMissingFiles == 0)
                    {
                        return;
                    }
                }
            }
        }

        private void UpdateResultsFilesUI(bool allFilesFound)
        {
            // Update the results files list boxes
            UpdateResultsFilesList(FoundResultsFiles.Select(kvp => kvp.Path).ToList(), listResultsFilesFound);
            UpdateResultsFilesList(MissingResultsFiles, listResultsFilesMissing);

            if (allFilesFound)
            {
                resultsSplitContainer.Panel2.Visible = false;
                resultsSplitContainer.Panel1.Dock = DockStyle.Fill;
            }
            else
            {
                resultsSplitContainer.Panel2.Visible = true;
            }            
        }

        private void UpdateResultsFilesList(List<string> resultsFiles, ListBox resultsFilesList)
        {
            string dirInputRoot = PathEx.GetCommonRoot(resultsFiles);
            resultsFilesList.Items.Clear();
            foreach (var fileName in resultsFiles)
            {
                string fileSuffix = fileName;
                if (dirInputRoot.Length > 0)
                    fileSuffix = fileName.Substring(dirInputRoot.Length);
                resultsFilesList.Items.Add(fileSuffix);
            }
        }

        private void AddFoundResultsFile(MsDataFileUri path)
        {
            FoundResultsFiles.Add(new FoundResultsFile(path.GetFileNameWithoutExtension(), path.ToString()));
        }

        public class ResultsFileFindInfo
        {
            public MsDataFileUri Path { get; set; }
            public bool Found { get; set; }

            public ResultsFileFindInfo(MsDataFileUri path, bool found)
            {
                Path = path;
                Found = found;
            }
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
