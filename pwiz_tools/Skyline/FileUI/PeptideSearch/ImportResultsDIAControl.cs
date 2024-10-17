/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportResultsDIAControl : UserControl, IImportResultsControl
    {
        public ImportResultsDIAControl(IModifyDocumentContainer documentContainer, string browseResultsDialogText = null)
        {
            DocumentContainer = documentContainer;
            BrowseResultsDialogText = browseResultsDialogText ??
                                      PeptideSearchResources.ImportResultsDIAControl_btnBrowse_Click_Browse_for_Results_Files;

            InitializeComponent();

            _foundResultsFiles = new BindingList<ImportPeptideSearch.FoundResultsFile>();
            listResultsFiles.DataSource = _foundResultsFiles;
            listResultsFiles.DisplayMember = @"Name";
            SimultaneousFiles = Settings.Default.ImportResultsSimultaneousFiles;
            DoAutoRetry = Settings.Default.ImportResultsDoAutoRetry;
        }

        private BindingList<ImportPeptideSearch.FoundResultsFile> _foundResultsFiles;
        private IModifyDocumentContainer DocumentContainer { get; set; }
        private string BrowseResultsDialogText { get; }

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

        public string Prefix { get; set; }
        public string Suffix { get; set; }

        public IList<ImportPeptideSearch.FoundResultsFile> FoundResultsFiles
        {
            get { return _foundResultsFiles; }
            set
            {
                _foundResultsFiles = new BindingList<ImportPeptideSearch.FoundResultsFile>(ImportPeptideSearch.EnsureUniqueNames(value).ToList());
                listResultsFiles.DataSource = _foundResultsFiles;
            }
        }

        public IEnumerable<string> MissingResultsFiles { get { yield break; } }

        public bool ResultsFilesMissing { get { return !_foundResultsFiles.Any(); } }

        public ImportResultsSettings ImportSettings
        {
            get { return new ImportResultsSettings(false, this); }
        }

        public event EventHandler<ImportResultsControl.ResultsFilesEventArgs> ResultsFilesChanged;

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            Browse();
        }

        public void Browse(string path = null)
        {
            using (var dlgOpen = new OpenDataSourceDialog(Settings.Default.RemoteAccountList))
            {
                dlgOpen.Text = BrowseResultsDialogText;
                dlgOpen.RestoreState(DocumentContainer.DocumentFilePath, Settings.Default.OpenDataSourceState);
                // Passed in path overrides stored or document path
                if (path != null)
                    dlgOpen.InitialDirectory = new MsDataFilePath(path);
                if (dlgOpen.ShowDialog(this) != DialogResult.OK)
                    return;

                Settings.Default.OpenDataSourceState = dlgOpen.GetState(DocumentContainer.DocumentFilePath);

                var dataSources = dlgOpen.DataSources;

                if (dataSources == null || !dataSources.Any())
                {
                    MessageDlg.Show(this, Resources.ImportResultsDlg_GetDataSourcePathsFile_No_results_files_chosen);
                    return;
                }

                _foundResultsFiles.RaiseListChangedEvents = false;
                try
                {
                    foreach (var dataSource in dataSources.Where(
                                 d => !_foundResultsFiles.Select(f => f.Path).Contains(d.ToString())))
                    {
                        _foundResultsFiles.Add(new ImportPeptideSearch.FoundResultsFile(
                            dataSource.GetFileNameWithoutExtension(),
                            dataSource.ToString()));
                    }
                }
                finally
                {
                    _foundResultsFiles.RaiseListChangedEvents = true;
                    _foundResultsFiles.ResetBindings();
                }

                if (ResultsFilesChanged != null)
                {
                    ResultsFilesChanged(this, new ImportResultsControl.ResultsFilesEventArgs(_foundResultsFiles.Count));
                }
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (listResultsFiles.SelectedIndex != -1)
            {
                for (int i = listResultsFiles.SelectedItems.Count - 1; i >= 0; i--)
                {
                    _foundResultsFiles.RemoveAt(i);
                }
            }
        }
    }
}
