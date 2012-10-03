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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportResultsControl : UserControl
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

        public List<string> ResultsFilesFound
        {
            get
            {
                List<string> resultsFilesFound = new List<string>();
                foreach (string item in listResultsFilesFound.Items)
                {
                    resultsFilesFound.Add(item);
                }

                return resultsFilesFound;
            }
        }

        // TODO: Use PathEx.GetCommonRoot() to show less of the result file path.  Currently can get too long.
        public void InitializeChromatogramsPage(Library docLib)
        {
            if (null != docLib)
            {
                foreach (var dataFile in docLib.LibraryDetails.DataFiles)
                {
                    if (File.Exists(dataFile) && DataSourceUtil.IsDataSource(dataFile))
                    {
                        // We've found the dataFile in the exact location
                        // specified in the document library, so just add it
                        // to the "FOUND" list.
                        listResultsFilesFound.Items.Add(dataFile);
                    }
                    else
                    {
                        listResultsFilesMissing.Items.Add(dataFile);
                    }
                }

                docLib.ReadStream.CloseStream();
            }

            UpdateMissingResultsFiles(Path.GetDirectoryName(SkylineWindow.DocumentFilePath));
        }

        public void GetPeptideSearchChromatograms()
        {
            List<KeyValuePair<string, string[]>> namedResults = new List<KeyValuePair<string, string[]>>();
            foreach (string resultsFile in listResultsFilesFound.Items)
            {
                namedResults.Add(new KeyValuePair<string, string[]>(
                                       Path.GetFileNameWithoutExtension(resultsFile), new[] { resultsFile }));
            }

            SkylineWindow.ModifyDocument(Resources.ImportResultsControl_GetPeptideSearchChromatograms_Import_results,
               doc => SkylineWindow.ImportResults(doc, namedResults.ToArray(), ExportOptimize.NONE));
        }

        private void findResultsFilesButton_Click(object sender, EventArgs e)
        {
            // Ask the user for the directory to search
            string initialDir = Path.GetDirectoryName(Path.GetDirectoryName(SkylineWindow.DocumentFilePath));
            FolderBrowserDialog dlg = new FolderBrowserDialog
            {
                Description = Resources.ImportResultsControl_findResultsFilesButton_Click_Results_Directory,
                ShowNewFolderButton = false,
                SelectedPath = initialDir
            };
            if (dlg.ShowDialog(WizardForm) != DialogResult.OK)
                return;

            // See if we're still missing any files, and update UI accordingly
            if (!UpdateMissingResultsFiles(dlg.SelectedPath))
            {
                MessageDlg.Show(WizardForm, Resources.ImportResultsControl_findResultsFilesButton_Click_Could_not_find_all_the_missing_results_files_);
            }
        }

        public bool UpdateMissingResultsFiles(string dirPath)
        {
            // Create a map for the missing results files, where 
            // "missingFiles[key] = true" means the file "key" is missing
            Dictionary<string, bool> missingFiles = new Dictionary<string, bool>();
            foreach (var item in listResultsFilesMissing.Items)
            {
                missingFiles.Add(item.ToString(), true);
            }

            // Add files that were found to the "found results file" list box
            List<string> filesFound = FindResultsFiles(dirPath, missingFiles);
            if (null != filesFound)
            {
                foreach (var file in filesFound)
                {
                    listResultsFilesFound.Items.Add(file);
                }
            }

            // Remove files that were found from the "missing results file" list box
            foreach (var item in missingFiles.Keys.Where(item => !missingFiles[item]))
            {
                listResultsFilesMissing.Items.Remove(item);
            }

            bool allFound;
            if (listResultsFilesMissing.Items.Count == 0)
            {
                resultsSplitContainer.Panel2.Visible = false;
                resultsSplitContainer.Panel1.Dock = DockStyle.Fill;
                allFound = true;
            }
            else
            {
                resultsSplitContainer.Panel2.Visible = true;
                allFound = false;
            }

            FireResultsFilesChanged(new ResultsFilesEventArgs(listResultsFilesMissing.Items.Count));
            return allFound;
        }

        public List<string> FindResultsFiles(string dirSearch, Dictionary<string, bool> missingFiles)
        {
            List<string> filesFound = null;
            LongWaitDlg longWaitDlg = new LongWaitDlg
            {
                Text = Resources.ImportResultsControl_FindResultsFiles_Searching_for_Results_Files,
                Message = string.Format(Resources.ImportResultsControl_FindResultsFiles_Searching_for_matching_results_files_in__0__, dirSearch)
            };
            try
            {
                longWaitDlg.PerformWork(WizardForm, 1000, longWaitBroker =>
                       filesFound = FindMissingResultsFiles(longWaitBroker, dirSearch, missingFiles));
            }
            catch (Exception x)
            {
                MessageDlg.Show(WizardForm, TextUtil.LineSeparate(Resources.ImportResultsControl_FindResultsFiles_An_error_occurred_attempting_to_find_results_files_, x.Message));
            }

            return filesFound;
        }

        private List<string> FindMissingResultsFiles(ILongWaitBroker longWaitBroker, string dirSearch, Dictionary<string, bool> missingFiles)
        {
            List<string> listFilesFound = new List<string>();
            foreach (var item in missingFiles.Keys.ToArray())
            {
                if (longWaitBroker.IsCanceled)
                {
                    break;
                }

                longWaitBroker.Message = string.Format("Searching for {0}", item);
                string file = FindDataFile(longWaitBroker, item, dirSearch);
                if (null != file)
                {
                    listFilesFound.Add(file);

                    // "missingFiles[key] = false" means the file "key" is no longer missing
                    missingFiles[item] = false;
                }
            }

            return listFilesFound;
        }

        private string FindDataFile(ILongWaitBroker longWaitBroker, string dataFileName, string directory)
        {
            try
            {
                string file = SearchForDataFileInDirectory(longWaitBroker, dataFileName, directory);
                if (null != file)
                {
                    return file;
                }

                string[] dirs = Directory.GetDirectories(directory);
                foreach (string dir in dirs)
                {
                    if (longWaitBroker.IsCanceled)
                    {
                        break;
                    }

                    longWaitBroker.Message = String.Format("Searching for {0} in {1}", dataFileName, dir);
                    file = FindDataFile(longWaitBroker, dataFileName, dir);
                    if (null != file)
                    {
                        return file;
                    }
                }
            }
            catch (Exception x)
            {
                MessageDlg.Show(WizardForm, TextUtil.LineSeparate(string.Format("An error occurred attempting to find the file {0}.", dataFileName), x.Message));
            }

            return null;
        }

        private string SearchForDataFileInDirectory(ILongWaitBroker longWaitBroker, string dataFileName, string dir)
        {
            string[] files = Directory.GetFiles(dir);
            foreach (string file in files)
            {
                if (longWaitBroker.IsCanceled)
                {
                    break;
                }

                // TODO: Traverse data files and check data source only once for every file
                if (DataSourceUtil.IsDataSource(file))
                {
                    string fileName = Path.GetFileName(file);
                    if (Equals(dataFileName, fileName) ||
                        MeasuredResults.IsBaseNameMatch(Path.GetFileNameWithoutExtension(dataFileName),
                                                        Path.GetFileNameWithoutExtension(file)))
                    {
                        return file;
                    }
                }
            }

            return null;
        }

        public class ResultsFilesEventArgs : EventArgs
        {

            public ResultsFilesEventArgs(int numMissingFiles)
            {
                NumMissingFiles = numMissingFiles;
            }

            public int NumMissingFiles { get; private set; }
        }
    }
}
