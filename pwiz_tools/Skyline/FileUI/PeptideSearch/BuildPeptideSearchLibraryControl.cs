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
using pwiz.Skyline.Model.Results;
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

            textCutoff.Text = Settings.Default.LibraryResultCutOff.ToString(LocalizationHelper.CurrentCulture);

            if (SkylineWindow.Document.PeptideCount == 0)
                cbFilterForDocumentPeptides.Hide();
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

        public ImportPeptideSearchDlg.Workflow WorkflowType
        {
            get
            {
                if (radioPRM.Checked)
                    return ImportPeptideSearchDlg.Workflow.prm;
                if (radioDIA.Checked)
                    return ImportPeptideSearchDlg.Workflow.dia;
                return ImportPeptideSearchDlg.Workflow.dda;
            }
            set
            {
                switch (value)
                {
                    case ImportPeptideSearchDlg.Workflow.prm:
                        radioPRM.Checked = true;
                        break;
                    case ImportPeptideSearchDlg.Workflow.dia:
                        radioDIA.Checked = true;
                        break;
                    default:
                        radioDDA.Checked = true;
                        break;
                }
            }
        }

        public bool FilterForDocumentPeptides
        {
            get { return cbFilterForDocumentPeptides.Checked; }
            set { cbFilterForDocumentPeptides.Checked = value; }
        }

        private string[] _searchFileNames = new string[0];
        public string[] SearchFileNames
        {
            get { return _searchFileNames; }

            private set
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
            string[] addFiles = BuildLibraryDlg.ShowAddFile(WizardForm, Path.GetDirectoryName(SkylineWindow.DocumentFilePath));
            if (addFiles != null)
            {
                AddSearchFiles(addFiles);
            }
        }

        public void AddSearchFiles(IEnumerable<string> fileNames)
        {
            SearchFileNames = BuildLibraryDlg.AddInputFiles(WizardForm, SearchFileNames, fileNames);
        }

        public double CutOffScore
        {
            get { return double.Parse(textCutoff.Text); }
            set { textCutoff.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public bool BuildPeptideSearchLibrary(CancelEventArgs e)
        {
            // Nothing to build, if now search files were specified
            if (_searchFileNames.Length == 0)
            {
                var libraries = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                if (!libraries.HasLibraries)
                    return false;
                DocLibrarySpec = libraries.LibrarySpecs.FirstOrDefault(s => s.IsDocumentLibrary);
                if (DocLibrarySpec == null || !LoadPeptideSearchLibrary())
                    return false;
                return true;
            }

            double cutOffScore;
            MessageBoxHelper helper = new MessageBoxHelper(WizardForm);
            if (!helper.ValidateDecimalTextBox(textCutoff, 0, 1.0, out cutOffScore))
            {
                e.Cancel = true;
                return false;
            }

            string outputPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);

            // Check to see if the library is already there, and if it is, 
            // "Append" instead of "Create"
            bool libraryExists = File.Exists(outputPath);
            var libraryBuildAction = LibraryBuildAction.Create;
            if (libraryExists)
            {
                if (SkylineWindow.Document.Settings.HasDocumentLibrary)
                    libraryBuildAction = LibraryBuildAction.Append;
                else
                {
                    // If the document does not have a document library, then delete the one that we have found
                    try
                    {
                        // CONSIDER: it may be that user is trying to re-import, in which case this file is probably in use
                        FileEx.SafeDelete(outputPath);
                        FileEx.SafeDelete(Path.ChangeExtension(outputPath, BiblioSpecLiteSpec.EXT_REDUNDANT));
                    }
                    catch (FileEx.DeleteException de)
                    {
                        MessageDlg.Show(this, de.Message);
                        return false;
                    }
                }
            }

            string name = Path.GetFileNameWithoutExtension(SkylineWindow.DocumentFilePath);
            var builder = new BiblioSpecLiteBuilder(name, outputPath, SearchFileNames)
            {
                Action = libraryBuildAction,
                KeepRedundant = true,
                CutOffScore = cutOffScore,
                Authority = LAB_AUTHORITY,
                Id = Helpers.MakeId(name)
            };

            using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_Peptide_Search_Library,
                    Message = Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_document_library_for_peptide_search_,
                })
            {
                // Disable the wizard, because the LongWaitDlg does not
                try
                {
                    ClosePeptideSearchLibraryStreams();
                    var status = longWaitDlg.PerformWork(WizardForm, 800,
                        monitor => LibraryManager.BuildLibraryBackground(SkylineWindow, builder, monitor));
                    if (status.IsError)
                    {
                        MessageDlg.Show(WizardForm, status.ErrorException.Message);
                        return false;
                    }
                }
                catch (Exception x)
                {
                    MessageDlg.Show(WizardForm, TextUtil.LineSeparate(string.Format(Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Failed_to_build_the_library__0__,
                                                                                    Path.GetFileName(outputPath)), x.Message));
                    return false;
                }
            }

            DocLibrarySpec = builder.LibrarySpec.ChangeDocumentLibrary(true);

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
                int skipCount = lib.HasDocumentLibrary ? 1 : 0;
                var libSpecs = new List<LibrarySpec> {DocLibrarySpec};
                libSpecs.AddRange(lib.LibrarySpecs.Skip(skipCount));
                var libs = new List<Library> {DocLib};
                libs.AddRange(lib.Libraries.Skip(skipCount));
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
                using (var longWait = new LongWaitDlg
                        {
                            Text = Resources.BuildPeptideSearchLibraryControl_LoadPeptideSearchLibrary_Loading_Library
                        })
                {
                    try
                    {
                        var status = longWait.PerformWork(WizardForm, 800, monitor =>
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
            }

            return true;
        }

        public void ClosePeptideSearchLibraryStreams()
        {
            BiblioSpecLiteLibrary docLib;
            if (!SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.TryGetDocumentLibrary(out docLib))
                return;

            foreach (var stream in docLib.ReadStreams)
                stream.CloseStream();
        }

        public bool VerifyRetentionTimes(IEnumerable<string> resultsFiles)
        {
            foreach (var resultsFile in resultsFiles)
            {
                LibraryRetentionTimes retentionTimes;
                if (DocLib.TryGetRetentionTimes(MsDataFileUri.Parse(resultsFile), out retentionTimes))
                {
                    if (retentionTimes.PeptideRetentionTimes.Any(t => t.RetentionTime <= 0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void ForceWorkflow(ImportPeptideSearchDlg.Workflow workflowType)
        {
            WorkflowType = workflowType;
            grpWorkflow.Hide();
            int offset = grpWorkflow.Height + (cbFilterForDocumentPeptides.Top - listSearchFiles.Bottom);
            listSearchFiles.Height += offset;
            cbFilterForDocumentPeptides.Top += offset;
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
