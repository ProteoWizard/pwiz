/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class MSstatsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod, NoLocalization]
        public void TestMSstatsTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            TestFilesZip = @"https://skyline.gs.washington.edu/tutorials/MSstatsTutorial.zip"; // Not L10
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            const string folderQuaser = "MSstatsTutorial";
            return TestFilesDir.GetTestPath(Path.Combine(folderQuaser, relativePath));
        }

        protected override void DoTest()
        {
            // open the file
            string documentFile = GetTestPath("Human_plasma.zip"); // Not L10N
            WaitForCondition(() => File.Exists(documentFile));
            RunUI(() => SkylineWindow.OpenSharedFile(documentFile));

            var document = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(document, null, 37, 82, 153, 459);

            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.SaveTools();
                });

            PauseForScreenShot("p. 2 - external tools"); // Not L10

            // R or the associated MS stats packages must be uninstalled for this screenshot to work
            const string installZipName = "MSstats-1_0.zip"; // Not L10N
            if (IsPauseForScreenShots)
            {
                var rInstaller =
                    ShowDialog<RInstaller>(() => configureToolsDlg.InstallZipTool(GetTestPath(installZipName))); // Not L10

                PauseForScreenShot("p. 3 - r Installer"); // Not L10

                // cancel as we don't actually want to install R
                OkDialog(rInstaller, rInstaller.CancelButton.PerformClick);
            }

            Settings.Default.ToolList.Clear();

            RunUI(() =>
            {
                // bypass the R installer dialogue
                configureToolsDlg.TestInstallProgram = (container, collection, script) => @"FakeDirectory\R.exe"; // Not L10N

                configureToolsDlg.InstallZipTool(GetTestPath(installZipName));
                AssertToolEquality(MSSTATS_QC, configureToolsDlg.ToolList[0]);
                AssertToolEquality(MSSTATS_GC, configureToolsDlg.ToolList[1]);
                AssertToolEquality(MSSTATS_DSS, configureToolsDlg.ToolList[2]);
            });

            PauseForScreenShot("p. 4 - External Tools (MSstats Installed)"); // Not L10

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

            PauseForScreenShot("p. 5 - Annotation Settings"); // Not L10

            OkDialog(annotationsDlg, annotationsDlg.OkDialog);

            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Contains(CONDITION));
                Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Contains(BIOREPLICATE));
                Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Contains(RUN));
            });

            RunUI(() => SkylineWindow.ShowResultsGrid(true));
            RunUI(() =>
            {
                SkylineWindow.SelectedPath =
                    SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 0);
            });
            WaitForDocumentLoaded();
            WaitForGraphs();
            DataGridView resultsGrid = null;
            DataGridViewColumn colBioreplicate = null, colCondition = null, colRunName = null;
            RunUI(() =>
            {
                resultsGrid = FindOpenForm<LiveResultsGrid>().DataGridView;
                colBioreplicate =
                    resultsGrid.Columns.Cast<DataGridViewColumn>().First(col => BIOREPLICATE.Name == col.HeaderText);
                colCondition =
                    resultsGrid.Columns.Cast<DataGridViewColumn>().First(col => CONDITION.Name == col.HeaderText);
                colRunName =
                    resultsGrid.Columns.Cast<DataGridViewColumn>().First(col => RUN.Name == col.HeaderText);
            });
            WaitForCondition(() => resultsGrid != null && colBioreplicate != null && colCondition != null && colRunName != null);

            PauseForScreenShot("p. 6,7 - Results Grid"); // Not L10

            RunUI(() => SkylineWindow.ShowResultsGrid(false));

            SelectNode(SrmDocument.Level.MoleculeGroups, 36);
            var popupPickList = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
                {
                    for (int i = 0; i < popupPickList.ItemNames.Count(); i++)
                    {
                        popupPickList.SetItemChecked(i, false);
                    }
                });

            PauseForScreenShot("p. 8 - pick list"); // Not L10
            OkDialog(popupPickList, popupPickList.OnOk);

            if (IsPauseForScreenShots)
            {
                RunArgsCollector(1, "GroupComparisonUi", "p. 9 - Group Comparison Ui"); // Not L10
                RunArgsCollector(2, "SampleSizeUi", "p. 10 - Sample Size Ui"); // Not L10
            }

        }

        private void RunArgsCollector(int index, string formName, string screenshotDescription)
        {
            int formCount = Application.OpenForms.Count;
            RunUI(() => SkylineWindow.RunTool(index));
            WaitForCondition(() => Application.OpenForms.Count == formCount + 1);
            Form argsCollector = Application.OpenForms[formName];
            Assert.IsNotNull(argsCollector);
            PauseForScreenShot(screenshotDescription);

            Action actCancel = () => argsCollector.CancelButton.PerformClick();
            argsCollector.BeginInvoke(actCancel);
            WaitForClosedForm(argsCollector);
        }

        private static void AssertToolEquality(ToolDescription expected, ToolDescription actual)
        {
            Assert.AreEqual(expected.Title, actual.Title);
            Assert.AreEqual(expected.Command, actual.Command);
            Assert.AreEqual(expected.Arguments, actual.Arguments);
            Assert.AreEqual(expected.InitialDirectory, actual.InitialDirectory);
            Assert.AreEqual(expected.ReportTitle, actual.ReportTitle);
            Assert.AreEqual(expected.OutputToImmediateWindow, actual.OutputToImmediateWindow);
        }

        private static readonly AnnotationDef CONDITION = new AnnotationDef("Condition", // Not L10N
                                                                            AnnotationDef.AnnotationTargetSet.Singleton(
                                                                                AnnotationDef.AnnotationTarget.replicate),
                                                                            AnnotationDef.AnnotationType.value_list,
                                                                            new[] { "Disease", "Healthy" }.ToList()); // Not L10

        private static readonly AnnotationDef BIOREPLICATE = new AnnotationDef("BioReplicate", // Not L10N
                                                                    AnnotationDef.AnnotationTargetSet.Singleton(
                                                                        AnnotationDef.AnnotationTarget.replicate),
                                                                    AnnotationDef.AnnotationType.text,
                                                                    new List<string>());

        private static readonly AnnotationDef RUN = new AnnotationDef("Run", // Not L10N
                                                                    AnnotationDef.AnnotationTargetSet.Singleton(
                                                                        AnnotationDef.AnnotationTarget.replicate),
                                                                    AnnotationDef.AnnotationType.text,
                                                                    new List<string>());
        
        private static readonly ToolDescription MSSTATS_QC = new ToolDescription("MSstats\\QC",                                                         // Not L10N
                                                                                 "$(ProgramPath(R,3.0.1))",                                             // Not L10N
                                                                                 "-f \"$(ToolDir)\\MSStatsQC.r\" --slave --args \"$(InputReportTempPath)\"",    // Not L10N
                                                                                 "$(DocumentDir)",                                                      // Not L10N
                                                                                 true,
                                                                                 "MSstats Input",                                                             // Not L10N
                                                                                 null,
                                                                                 null,
                                                                                 null,
                                                                                 new List<AnnotationDef> {BIOREPLICATE, CONDITION, RUN},
                                                                                 null,
                                                                                 null,
                                                                                 null);

        private static readonly ToolDescription MSSTATS_GC = new ToolDescription("MSstats\\Group Comparison",                                           // Not L10N
                                                                         "$(ProgramPath(R,3.0.1))",                                                     // Not L10N
                                                                         "-f \"$(ToolDir)\\MSStatsGC.r\" --slave --args \"$(InputReportTempPath)\"",            // Not L10N
                                                                         "$(DocumentDir)",                                                              // Not L10N
                                                                         true,
                                                                         "MSstats Input",                                                                     // Not L10N
                                                                         null,
                                                                         null,
                                                                         null,
                                                                         new List<AnnotationDef> { BIOREPLICATE, CONDITION, RUN },
                                                                         null,
                                                                         null,
                                                                         null);

        private static readonly ToolDescription MSSTATS_DSS = new ToolDescription("MSstats\\Design Sample Size",                                        // Not L10N
                                                                         "$(ProgramPath(R,3.0.1))",                                                     // Not L10N
                                                                         "-f \"$(ToolDir)\\MSStatsDSS.r\" --slave --args \"$(InputReportTempPath)\"",           // Not L10N
                                                                         "$(DocumentDir)",                                                              // Not L10N
                                                                         true,
                                                                         "MSstats Input",                                                                     // Not L10N
                                                                         null,
                                                                         null,
                                                                         null,
                                                                         new List<AnnotationDef> { BIOREPLICATE, CONDITION, RUN },
                                                                         null,
                                                                         null,
                                                                         null);
    }
}
