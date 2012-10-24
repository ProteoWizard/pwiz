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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class BuildPeptideSearchLibraryControl : UserControl
    {
        private const string LAB_AUTHORITY = "proteome.gs.washington.edu";  // Not L10N

        public BuildPeptideSearchLibraryControl(SkylineWindow skylineWindow, LibraryManager libraryManager)
        {
            SkylineWindow = skylineWindow;
            LibraryManager = libraryManager;

            InitializeComponent();

            textCutoff.Text = Settings.Default.LibraryResultCutOff.ToString(CultureInfo.CurrentCulture);
        }

        public event EventHandler<InputFilesChangedEventArgs> InputFilesChanged;

        private void FireInputFilesChanged(InputFilesChangedEventArgs e)
        {
            if (InputFilesChanged != null)
            {
                InputFilesChanged(this, e);
            }
        }

        private SkylineWindow SkylineWindow { get; set; }
        private LibraryManager LibraryManager { get; set; }
        private Form WizardForm { get { return FormEx.GetParentForm(this); } }

        public Library DocLib { get; private set; }
        private LibrarySpec DocLibrarySpec { get; set; }

        private string[] _searchFileNames = new string[0];
        private string[] SearchFileNames
        {
            get { return _searchFileNames; }

            set
            {
                // Set new value
                _searchFileNames = value;

                // Always show sorted list of files
                Array.Sort(_searchFileNames);

                // Calculate the common root directory
                string dirInputRoot = PathEx.GetCommonRoot(_searchFileNames);

                // Populate the input files list
                listSearchFiles.BeginUpdate();
                listSearchFiles.Items.Clear();
                foreach (string fileName in _searchFileNames)
                {
                    listSearchFiles.Items.Add(fileName.Substring(dirInputRoot.Length));
                }
                listSearchFiles.EndUpdate();

                FireInputFilesChanged(new InputFilesChangedEventArgs(listSearchFiles.Items.Count));
            }
        }

        private void btnRemFile_Click(object sender, EventArgs e)
        {
            RemoveFiles();
        }

        public void RemoveFiles()
        {
            var selectedIndices = listSearchFiles.SelectedIndices;
            var listSearchFileNames = SearchFileNames.ToList();
            for (int i = selectedIndices.Count - 1; i >= 0; i--)
            {
                listSearchFiles.Items.RemoveAt(i);
                listSearchFileNames.RemoveAt(i);
            }

            _searchFileNames = listSearchFileNames.ToArray();

            FireInputFilesChanged(new InputFilesChangedEventArgs(listSearchFiles.Items.Count));
        }

        private void listSearchFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemFile.Enabled = listSearchFiles.SelectedItems.Count > 0;
        }

        private void btnAddFile_Click(object sender, EventArgs e)
        {
            string[] addFiles = BuildLibraryDlg.ShowAddFile(WizardForm);
            if (addFiles != null)
            {
                AddSearchFiles(addFiles);
            }
        }

        public void AddSearchFiles(IEnumerable<string> fileNames)
        {
            SearchFileNames = BuildLibraryDlg.AddInputFiles(WizardForm, SearchFileNames, fileNames);
        }

        public bool BuildPeptideSearchLibrary(CancelEventArgs e)
        {
            double cutOffScore;
            MessageBoxHelper helper = new MessageBoxHelper(WizardForm);
            if (!helper.ValidateDecimalTextBox(e, textCutoff, 0, 1.0, out cutOffScore))
                return false;

            string outputPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);

            // Check to see if the library is already there, and if it is, 
            // "Append" instead of "Create"
            var libraryBuildAction = File.Exists(outputPath) ? LibraryBuildAction.Append : LibraryBuildAction.Create;

            // TODO: I need to figure out the name based on the .sky filename
            string name = Path.GetFileNameWithoutExtension(SkylineWindow.DocumentFilePath);
            var builder = new BiblioSpecLiteBuilder(name, outputPath, SearchFileNames)
            {
                Action = libraryBuildAction,
                KeepRedundant = true,
                CutOffScore = cutOffScore,
                Authority = LAB_AUTHORITY,
                Id = Helpers.MakeId(name)
            };

            // TODO: Manage Peptide Searches user interface, like Manage Results form.  (After first commit)
            LongWaitDlg longWaitDlg = new LongWaitDlg
            {
                Text = Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_Peptide_Search_Library,
                Message = Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_document_library_for_peptide_search_,
            };

            // Disable the wizard, because the LongWaitDlg does not
            WizardForm.Enabled = false;
            var status = longWaitDlg.PerformWork(WizardForm, 800, monitor => builder.BuildLibrary(monitor));
            WizardForm.Enabled = true;
            if (status.IsError)
            {
                MessageDlg.Show(WizardForm, status.ErrorException.Message);
                return false;
            }

            DocLibrarySpec = builder.LibrarySpec.ChangeDocumentLocal(true);

            Settings.Default.SpectralLibraryList.Insert(0, DocLibrarySpec);

            // Go ahead and load the library - we'll need it for 
            // the modifications and chromatograms page.
            if (!LoadPeptideSearchLibrary())
            {
                return false;
            }

            SkylineWindow.ModifyDocument(Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Add_document_spectral_library, doc =>
                        doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(lib =>
            {
                var libSpecs = new List<LibrarySpec> {DocLibrarySpec};
                libSpecs.AddRange(lib.LibrarySpecs);
                var libs = new List<Library> {DocLib};
                libs.AddRange(lib.Libraries);
                return lib.ChangeDocumentLibrary(true).ChangeLibraries(libSpecs, libs);
            })));

            return true;
        }

        private bool LoadPeptideSearchLibrary()
        {
            if (null == DocLibrarySpec)
            {
                return false;
            }

            DocLib = LibraryManager.TryGetLibrary(DocLibrarySpec);
            if (null == DocLib)
            {
                var longWait = new LongWaitDlg { Text = Resources.BuildPeptideSearchLibraryControl_LoadPeptideSearchLibrary_Loading_Library };
                try
                {
                    var status = longWait.PerformWork(Parent, 800, monitor =>
                        DocLib = LibraryManager.LoadLibrary(DocLibrarySpec, () => new DefaultFileLoadMonitor(monitor)));
                    if (status.IsError)
                    {
                        MessageDlg.Show(WizardForm, status.ErrorException.Message);
                        return false;
                    }
                }
                catch (Exception x)
                {
                    MessageDlg.Show(WizardForm,
                                    TextUtil.LineSeparate(string.Format(Resources.BuildPeptideSearchLibraryControl_LoadPeptideSearchLibrary_An_error_occurred_attempting_to_import_the__0__library_,
                                                                        DocLibrarySpec.Name), x.Message));
                    return false;
                }
            }

            return true;
        }

        public void ClosePeptideSearchLibraryStreams()
        {
            if (null == DocLib)
                return;

            foreach (var stream in DocLib.ReadStreams)
                stream.CloseStream();
        }

        public bool VerifyRetentionTimes(List<string> resultsFiles)
        {
            foreach (var resultsFile in resultsFiles)
            {
                LibraryRetentionTimes retentionTimes;
                if (DocLib.TryGetRetentionTimes(resultsFile, out retentionTimes))
                {
                    if (retentionTimes.PeptideRetentionTimes.Any(t => t.RetentionTime <= 0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public class InputFilesChangedEventArgs : EventArgs
        {
            public InputFilesChangedEventArgs(int numInputFiles)
            {
                NumInputFiles = numInputFiles;
            }

            public int NumInputFiles { get; private set; }
        }    
    }
}
