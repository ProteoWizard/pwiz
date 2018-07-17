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
using System.IO;
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
        public ImportResultsDIAControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;

            InitializeComponent();

            _foundResultsFiles = new BindingList<ImportPeptideSearch.FoundResultsFile>();
            listResultsFiles.DataSource = _foundResultsFiles;
            listResultsFiles.DisplayMember = "Name"; // Not L10N
            SimultaneousFiles = Settings.Default.ImportResultsSimultaneousFiles;
            DoAutoRetry = Settings.Default.ImportResultsDoAutoRetry;
        }

        private BindingList<ImportPeptideSearch.FoundResultsFile> _foundResultsFiles;
        private SkylineWindow SkylineWindow { get; set; }

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

        public List<ImportPeptideSearch.FoundResultsFile> FoundResultsFiles
        {
            get { return _foundResultsFiles.ToList(); }
            set
            {
                var files = ImportResultsControl.EnsureUniqueNames(value); // May change names to ensure uniqueness
                _foundResultsFiles = new BindingList<ImportPeptideSearch.FoundResultsFile>(files);
                listResultsFiles.DataSource = _foundResultsFiles;
            }
        }

        public bool ResultsFilesMissing { get { return !_foundResultsFiles.Any(); } }

        public event EventHandler<ImportResultsControl.ResultsFilesEventArgs> ResultsFilesChanged;

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlgOpen = new OpenDataSourceDialog(Settings.Default.RemoteAccountList)
            {
                Text = Resources.ImportResultsDIAControl_btnBrowse_Click_Browse_for_Results_Files
            })
            {
                // The dialog expects null to mean no directory was supplied, so don't assign an empty string.
                string initialDir = Path.GetDirectoryName(SkylineWindow.DocumentFilePath);
                dlgOpen.InitialDirectory = new MsDataFilePath(initialDir);

                // Use saved source type, if there is one.
                string sourceType = Settings.Default.SrmResultsSourceType;
                if (!string.IsNullOrEmpty(sourceType))
                    dlgOpen.SourceTypeName = sourceType;

                if (dlgOpen.ShowDialog(this) != DialogResult.OK)
                    return;

                var dataSources = dlgOpen.DataSources;

                if (dataSources == null || !dataSources.Any())
                {
                    MessageDlg.Show(this, Resources.ImportResultsDlg_GetDataSourcePathsFile_No_results_files_chosen);
                    return;
                }

                foreach (var dataSource in dataSources.Where(d => !_foundResultsFiles.Select(f => f.Path).Contains(d.ToString())))
                {
                    _foundResultsFiles.Add(new ImportPeptideSearch.FoundResultsFile(dataSource.GetFileNameWithoutExtension(), dataSource.ToString()));
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
