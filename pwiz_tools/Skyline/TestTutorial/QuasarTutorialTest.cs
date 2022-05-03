/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class QuasarTutorialTest : AbstractFunctionalTest
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        const int WM_KEYDOWN    = 0x100;
        private const int WM_KEYUP = 0x101;

        [TestMethod, NoLocalization]
        public void TestQuasarTutorialLegacy()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            TestFilesZip = @"http://skyline.gs.washington.edu/tutorials/QuaSARTutorial.zip";
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            const string folderQuaser = "QuaSARTutorial";
            return TestFilesDir.GetTestPath(Path.Combine(folderQuaser, relativePath));
        }

        protected override void DoTest()
        {
            // p. 1 open the file
            string documentFile = GetTestPath(@"QuaSAR_Tutorial.sky"); // Not L10N
            WaitForCondition(() => File.Exists(documentFile));
            RunUI(() => SkylineWindow.OpenFile(documentFile));

            var document = SkylineWindow.Document;
            AssertEx.IsDocumentState(document, null, 34, 125, 250, 750);

            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                });

            PauseForScreenShot("p. 2 - External Tools");

            const string installZipName = "QuaSAR-1_0.zip"; // Not L10N
            if (IsPauseForScreenShots)
            {    
                var rInstaller = ShowDialog<RInstaller>(() =>
                    {
                        configureToolsDlg.RemoveAllTools();
                        configureToolsDlg.SaveTools();
                        configureToolsDlg.InstallZipTool(GetTestPath(installZipName));
                    });

                PauseForScreenShot("p. 3 - R Installer");

                // cancel as we don't actually want to install R / Packages
                OkDialog(rInstaller, rInstaller.CancelButton.PerformClick);
            }

            RunUI(() =>
                {
                    // bypass the R installer dialogue
                    configureToolsDlg.TestInstallProgram = (container, collection, script) => @"FakeDirectory\R.exe"; // Not L10N

                    configureToolsDlg.InstallZipTool(GetTestPath(installZipName));
                    var installedQuaSAR = configureToolsDlg.ToolList[0];
                    Assert.AreEqual(QUASAR.Title, installedQuaSAR.Title);
                    Assert.AreEqual(QUASAR.Command, installedQuaSAR.Command);
                    Assert.AreEqual(QUASAR.Arguments, installedQuaSAR.Arguments);
                    Assert.AreEqual(QUASAR.InitialDirectory, installedQuaSAR.InitialDirectory);
                    Assert.AreEqual(QUASAR.ReportTitle, installedQuaSAR.ReportTitle);
                    Assert.AreEqual(QUASAR.OutputToImmediateWindow, installedQuaSAR.OutputToImmediateWindow);
                });

            PauseForScreenShot("p. 4 - External Tools (QuaSAR Installed)");

            OkDialog(configureToolsDlg, configureToolsDlg.OkDialog);
            RunUI(() => SkylineWindow.PopulateToolsMenu());

            var annotationsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            RunUI(() =>
                {
                    var checkedListBox = annotationsDlg.AnnotationsCheckedListBox;
                    for (int i = 0; i < checkedListBox.Items.Count; i++)
                    {
                        checkedListBox.SetItemChecked(i, true);
                    }
                });

            PauseForScreenShot("p. 5 - Annotation Settings");

            OkDialog(annotationsDlg, annotationsDlg.OkDialog);

            RunUI(() =>
                {
                    Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Contains(SAMPLEGROUP));
                    Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Contains(IS_SPIKE));
                    Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Contains(CONCENTRATION));
                });

            RunUI(() => SkylineWindow.ShowResultsGrid(true));
            RunUI(() =>
            {
                SkylineWindow.SelectedPath =
                    SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 0);
            });
            WaitForGraphs();
            DataGridViewColumn colSampleId = null, colConcentration = null, colIsConc = null;
            var resultsGrid = FindOpenForm<LiveResultsGrid>().DataGridView;
            WaitForConditionUI(() => 0 != resultsGrid.ColumnCount);
            RunUI(() =>
                {
                    colSampleId =
                        resultsGrid.Columns.Cast<DataGridViewColumn>()
                            .First(col => SAMPLEGROUP.Name == col.HeaderText);
                    colConcentration =
                        resultsGrid.Columns.Cast<DataGridViewColumn>()
                            .First(col => CONCENTRATION.Name == col.HeaderText);
                    colIsConc =
                        resultsGrid.Columns.Cast<DataGridViewColumn>().First(col => IS_SPIKE.Name == col.HeaderText);
                });
            WaitForCondition(() => resultsGrid != null && colSampleId != null && colConcentration != null && colIsConc != null);

            float[] concentrations = { 0f, .001f, .004f, .018f, .075f, .316f, 1.33f, 5.62f, 23.71f, 100 };
            string[] sampleIds = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" }; // Not L10N

            var docBeforePaste = SkylineWindow.Document;
            RunUI(() =>
            {
                Assert.AreEqual(colSampleId.DisplayIndex + 1, colConcentration.DisplayIndex);
                Assert.AreEqual(colSampleId.DisplayIndex + 1, colConcentration.DisplayIndex);
                StringBuilder clipboardText = new StringBuilder();
                for (int i = 0; i < concentrations.Length; i++)
                {
                    for (int j = i*4; j < (i + 1)*4; j++)
                    {
                        if (clipboardText.Length > 0)
                        {
                            clipboardText.Append('\n');
                        }
                        clipboardText.Append(sampleIds[i]);
                        clipboardText.Append('\t');
                        clipboardText.Append(concentrations[i]);
                        clipboardText.Append('\t');
                        clipboardText.Append(10);
                    }
                }
                resultsGrid.CurrentCell = resultsGrid.Rows[0].Cells[colSampleId.Index];
                ClipboardEx.SetText(clipboardText.ToString());
                resultsGrid.SendPaste();
            });
            WaitForDocumentChange(docBeforePaste);
            WaitForGraphs();
            PauseForScreenShot("p. 7 - Results Grid");

            RunUI(() => SkylineWindow.ShowResultsGrid(false));

            if (IsPauseForScreenShots)
            {
                int formCount = Application.OpenForms.Count;
                RunUI(() => SkylineWindow.RunTool(0));
                WaitForCondition(() => Application.OpenForms.Count == formCount + 1);
                Form argsCollector = Application.OpenForms["QuaSARUI"]; // Not L10N
                Assert.IsNotNull(argsCollector);
                PauseForScreenShot("p. 8 - Args Collector");

                Action actCancel = () => argsCollector.CancelButton.PerformClick();
                argsCollector.BeginInvoke(actCancel);
                WaitForClosedForm(argsCollector);
            }

            // Try to prevent the occasional "The process cannot access the file 'QuaSAR_Tutorial.skyd' because it is being
            // used by another process" error we sometimes see on exit, especially under code coverage on TeamCity
            WaitForDocumentLoaded();
        }

        private static readonly AnnotationDef SAMPLEGROUP = new AnnotationDef("SampleGroup", // Not L10N
                                     AnnotationDef.AnnotationTargetSet.Singleton(
                                         AnnotationDef.AnnotationTarget.replicate),
                                      AnnotationDef.AnnotationType.text,
                                     new List<string>());

        private static readonly AnnotationDef IS_SPIKE = new AnnotationDef("IS Spike", // Not L10N
                                           AnnotationDef.AnnotationTargetSet.Singleton(
                                               AnnotationDef.AnnotationTarget.replicate),
                                           AnnotationDef.AnnotationType.text,
                                           new List<string>());

        private static readonly AnnotationDef CONCENTRATION = new AnnotationDef("Concentration", // Not L10N
                                                         AnnotationDef.AnnotationTargetSet.Singleton(
                                                             AnnotationDef.AnnotationTarget.replicate),
                                                         AnnotationDef.AnnotationType.text,
                                                         new List<string>());

        private static readonly ToolDescription QUASAR = new ToolDescription("QuaSAR", // Title Not L10N
                                                                             "$(ProgramPath(R,3.0.1))", // Command Not L10N
                                                                             "-f \"$(ToolDir)\\QuaSAR-GP.R\" --slave --no-save --args \"$(ToolDir)\\QuaSAR.R\" \"$(ToolDir)\\common.R\" \"$(InputReportTempPath)\" $(CollectedArgs)", // Arguments Not L10N
                                                                             "$(DocumentDir)", // Initial Directory Not L10N
                                                                             true, // Output to Immediate Window
                                                                             "QuaSAR Input", // Input Report Name Not L10N
                                                                             null, // Args Collector dll Path
                                                                             null, // Args Collector class name
                                                                             null, // Tool Directory path
                                                                             new List<AnnotationDef> {SAMPLEGROUP, IS_SPIKE, CONCENTRATION}, // Annotations
                                                                             null, // Package Version
                                                                             null, // Package Identifier
                                                                             null); // Package Name
        
/*
        private void SetCellValue(DataGridView dataGridView, int rowIndex, int columnIndex, object value)
        {
            dataGridView.CurrentCell = dataGridView.Rows[rowIndex].Cells[columnIndex];
            dataGridView.BeginEdit(true);
            dataGridView.CurrentCell.Value = value;
            dataGridView.EndEdit();
        }
*/
    }
}
