/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.CommonMsData.RemoteApi;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace TestPerf
{
    /// <summary>
    /// Runs the PRM With an Orbitrap Mass Spec tutorial
    /// </summary>
    [TestClass]
    public class OrbiPrmTutorialTest : AbstractFunctionalTestEx
    {
        private string ROOT_DIR;
        private string LIBRARY_DIR;
        private string DATA_DIR;
        private string SAMPLES_DIR;
        private string STANDARDS_DIR;

        private static string[] SAMPLE_NAMES = { "G1_rep1", "G1_rep2", "G1_rep3", "G2M_rep1", "G2M_rep2", "G2M_rep3", "S_rep1", "S_rep2", "S_rep3" };

        private void InitPaths()
        {
            ROOT_DIR ??= UseRawFiles ? "PRM-OrbiRaw" : "PRM-OrbiMzxml";
            LIBRARY_DIR = Path.Combine(ROOT_DIR, "Heavy Library");
            DATA_DIR = Path.Combine(ROOT_DIR, "PRM data");
            SAMPLES_DIR = Path.Combine(DATA_DIR, "Samples");
            STANDARDS_DIR = Path.Combine(DATA_DIR, "Standards");
        }

        private bool IncludePrbbMethodReports => !IsPauseForScreenShots;

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestOrbiPrmTutorial()
        {
            // Not yet translated
            if (IsTranslationRequired)
                return;

            InitPaths();

            //            IsPauseForScreenShots = true;
            //            RunPerfTests = true;
            //            IsCoverShotMode = true;
            CoverShotName = "PRMOrbitrap";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/PRMOrbitrap-22_2.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"http://skyline.ms/tutorials/PRM-OrbiRaw.zip"
                    : @"http://skyline.ms/tutorials/PRM-OrbiMzxml.zip",
                @"TestPerf\OrbiPrmViews.zip",
            };

            TestFilesPersistent = new[]
            {
                Path.Combine(LIBRARY_DIR, "heavy-01.mzXML"),
                Path.Combine(LIBRARY_DIR, "heavy-01.pep.xml"),
                Path.Combine(LIBRARY_DIR, "heavy-02.mzXML"),
                Path.Combine(LIBRARY_DIR, "heavy-02.pep.xml"),
                Path.Combine(STANDARDS_DIR, "heavy-PRM.raw"),
            }.Union(SAMPLE_NAMES.Select(s => Path.Combine(SAMPLES_DIR, s + ".raw"))).ToArray();

            RunFunctionalTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestOrbiPrmArdia()
        {
            if (!ArdiaTestUtil.EnableArdiaTests)
            {
                Console.Error.WriteLine($"NOTE: skipping {TestContext.TestName} because username/password for Ardia is not configured in environment variables");
                return;
            }

            IsArdiaTest = true;

            //            IsPauseForScreenShots = true;
            //            RunPerfTests = true;
            //            IsCoverShotMode = true;
            //            IsRecordMode = true;

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/PRMOrbitrap-22_2.pdf";

            ROOT_DIR = "PRM-OrbiRaw";

            InitPaths();

            TestFilesZipPaths = new[]
            {
                @"http://skyline.ms/tutorials/PRM-OrbiRaw.zip",
                @"TestPerf\OrbiPrmViews.zip",
            };

            TestFilesPersistent = new[]
            {
                Path.Combine(LIBRARY_DIR, "heavy-01.mzXML"),
                Path.Combine(LIBRARY_DIR, "heavy-01.pep.xml"),
                Path.Combine(LIBRARY_DIR, "heavy-02.mzXML"),
                Path.Combine(LIBRARY_DIR, "heavy-02.pep.xml"),
            };

            RunFunctionalTest();
        }

        private string DataPath => TestFilesDirs.Last().PersistentFilesDir;

        private string GetTestPath(string relativePath)
        {
            if (!relativePath.StartsWith(ROOT_DIR))
                relativePath = Path.Combine(ROOT_DIR, relativePath);
            return TestFilesDirs.First().GetTestPath(relativePath);
        }

        private const string QUANT_UNITS = "fmol";
        private const string HEAVY_LIBRARY = "heavy";
        private const string SHOTGUN_LIBRARY = "shotgun";

        private const string HEAVY_K = "Label:13C(6)15N(2) (C-term K)";
        private const string HEAVY_R = "Label:13C(6)15N(4) (C-term R)";

        private const string REPORT_METHOD = "PRM_precursor_list";
        private const string REPORT_SCHEDULED_METHOD = "PRM_precursor_list_scheduled";
        private const string REPORT_QUANT = "PRM-Quant";
        private const string INSTRUMENT_METHOD = "PRM_mass_list";

        private bool IsArdiaTest;

        protected override void DoTest()
        {
            PrepareTargets();
            ExportIsolationList();
            if (IncludePrbbMethodReports)
                ExportMethodReport();

            ExportScheduledMethodReport();

            ImportReplicates();
            if (IsCoverShotMode)
                return; // Done
            RefineTransitions();
            InternalSinglePointCalibration();
            AnnotateReplicates();
            GroupComparison();
            ExportResultsReport();
        }

        private void PrepareTargets()
        {
            // Have to open the start page from within Skyline to ensure audit logging starts up correctly
            var startPage = ShowDialog<StartPage>(SkylineWindow.OpenStartPage);

            // PauseForScreenShot<StartPage>("Import Peptide List icon");   // Named shared image

            var startPageSettings = ShowDialog<StartPageSettingsUI>(() =>
                startPage.ClickWizardAction(Resources.SkylineStartup_SkylineStartup_Import_Peptide_List));

            RunUI(() => startPageSettings.IsIntegrateAll = true);
            PauseForScreenShot<StartPageSettingsUI>("Settings form");

            RunDlg<MessageDlg>(startPageSettings.ResetDefaults, dlg => dlg.OkDialog());

            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(startPageSettings.ShowPeptideSettingsUI);

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsUI.MissedCleavages = 1;
                peptideSettingsUI.ComboPeptideUniquenessConstraintSelected =
                    PeptideFilter.PeptideUniquenessConstraint.protein;
            });

            // Creating a Background Proteome File, p. 2-3
            const string protdbName = "mouse-proteome";
            string protdbPath = GetTestPath(protdbName + ProteomeDb.EXT_PROTDB);
            FileEx.SafeDelete(protdbPath);
            var proteomeDlg = ShowDialog<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
            {
                proteomeDlg.BackgroundProteomeName = protdbName;
                proteomeDlg.CreateDb(protdbPath);
            });
            var messageRepeats =
                ShowDialog<MessageDlg>(() => proteomeDlg.AddFastaFile(GetTestPath("uniprot-mouse.fasta")));

            PauseForScreenShot<MessageDlg>("Repeats message");

            OkDialog(messageRepeats, messageRepeats.OkDialog);

            // RunUI(proteomeDlg.SelToEndBackgroundProteomePath);
            // PauseForScreenShot<BuildBackgroundProteomeDlg>("Edit Background Proteome form", 5);

            OkDialog(proteomeDlg, proteomeDlg.OkDialog);

            PauseForScreenShot<PeptideSettingsUI.DigestionTab>("Peptide Settings - Digestion tab");

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUI.TimeWindow = 5;
            });

            PauseForScreenShot<PeptideSettingsUI.PredictionTab>("Peptide Settings - Prediction tab");

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Filter;
                peptideSettingsUI.TextMinLength = 7;
                peptideSettingsUI.TextMaxLength = 26;
                peptideSettingsUI.TextExcludeAAs = 0;
            });

            PauseForScreenShot<PeptideSettingsUI.FilterTab>("Peptide Settings - Filter tab");

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library;
            });

            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryPath = GetTestPath(LIBRARY_DIR) + "\\";
                buildLibraryDlg.LibraryName = HEAVY_LIBRARY;
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddInputFiles(new []
                {
                    GetTestPath(Path.Combine(LIBRARY_DIR, "heavy-01.pep.xml")),
                    GetTestPath(Path.Combine(LIBRARY_DIR, "heavy-02.pep.xml")),
                });
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            RunUI(() =>
            {
                buildLibraryDlg.Grid.SetScoreThreshold(0.1);
                buildLibraryDlg.OkWizardPage();
            });
            WaitForConditionUI(() => peptideSettingsUI.PickedLibraries.Contains(HEAVY_LIBRARY));

            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            var addLibUI = ShowDialog<EditLibraryDlg>(editListUI.AddItem);
            RunUI(() => addLibUI.LibrarySpec =
                new BiblioSpecLibSpec(SHOTGUN_LIBRARY, GetTestPath(@"Shotgun Library\shotgun.blib")));
            OkDialog(addLibUI, addLibUI.OkDialog);
            RunUI(() => editListUI.MoveItemUp());   // Put shotgun at the top
            OkDialog(editListUI, editListUI.OkDialog);

            RunUI(() =>
            {
                peptideSettingsUI.PickedLibraries = new[] { SHOTGUN_LIBRARY, HEAVY_LIBRARY };
            });

            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab");

            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });

            var modHeavyK = new StaticMod(HEAVY_K, "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI/*, "Edit Isotope Modification form" */);
            var modHeavyR = new StaticMod(HEAVY_R, "R", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyR, peptideSettingsUI/*, "Edit Isotope Modification form" */);
            RunUI(() => peptideSettingsUI.PickedHeavyMods = new[] { HEAVY_K, HEAVY_R });

            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Modifications tab");


            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                peptideSettingsUI.QuantNormalizationMethod = NormalizationMethod.FromIsotopeLabelTypeName("heavy");
                peptideSettingsUI.QuantMsLevel = 2;
                peptideSettingsUI.QuantUnits = QUANT_UNITS;
            });

            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Quantification tab");

            using (new WaitDocumentChange(null, true))
            {
                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            }

            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(startPageSettings.ShowTransitionSettingsUI);

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Prediction;
            });

            PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab");

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUI.PrecursorCharges = "2, 3";
                transitionSettingsUI.ProductCharges = "1, 2";
                transitionSettingsUI.FragmentTypes = "y, b";
                transitionSettingsUI.RangeFrom = Resources.TransitionFilter_FragmentStartFinders_ion_1;
                transitionSettingsUI.RangeTo = Resources.TransitionFilter_FragmentEndFinders_last_ion;
                transitionSettingsUI.SpecialIons = Array.Empty<string>();
                transitionSettingsUI.ExclusionWindow = 5;
            });

            PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings - Filter tab");

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library;
                transitionSettingsUI.IonMatchTolerance = 0.05;
                transitionSettingsUI.IonCount = 10;
                transitionSettingsUI.MinIonCount = 3;
                transitionSettingsUI.Filtered = true;
            });

            PauseForScreenShot<TransitionSettingsUI.LibraryTab>("Transition Settings - Library tab");

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettingsUI.MinMz = 340;
                transitionSettingsUI.MaxMz = 1200;
            });

            PauseForScreenShot<TransitionSettingsUI.InstrumentTab>("Transition Settings - Instrument tab");

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                transitionSettingsUI.ProductMassAnalyzer = FullScanMassAnalyzerType.centroided;
                transitionSettingsUI.ProductRes = 10;
                transitionSettingsUI.RetentionTimeFilterType = RetentionTimeFilterType.none;
            });

            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan tab");

            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

            using (new CheckDocumentState(19, 31, 106, 882, null, true))
            {
                var pasteDlg = ShowDialog<PasteDlg>(startPageSettings.OkDialog);
                RunUI(() =>
                {
                    SetClipboardText(GetPeptideList());
                    pasteDlg.PastePeptides();
                });
                PauseForScreenShot<PasteDlg.ProteinListTab>("Insert Peptide List"); // Not L10N
                OkDialog(pasteDlg, pasteDlg.OkDialog);
            }

            RunUI(SkylineWindow.ExpandPrecursors);
            SelectNode(SrmDocument.Level.Molecules, 0);

            RestoreViewOnScreen(17);
            RunUI(() => SkylineWindow.Size = new Size(1100, 657));
            PauseForScreenShot<SkylineWindow>("Main window with peptide selected and Library Match view");

            SaveBackup("PRM_Proteome");
        }

        private void SaveBackup(string backupName)
        {
            string documentFile = GetTestPath(backupName + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentFile));
        }

        private string GetPeptideList()
        {
            var sb = new StringBuilder();
            // Skip the header line and then read all the modified sequences from the 3rd column (index 2)
            foreach (var line in File.ReadAllLines(GetTestPath("target_peptides.csv")).Skip(1))
                sb.AppendLine(line.ParseCsvFields()[2]);
            return sb.ToString();
        }

        private void ExportIsolationList()
        {
            var exportIsolationDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() => exportIsolationDlg.SetInstrument(ExportInstrumentType.THERMO_FUSION));
            PauseForScreenShot<ExportMethodDlg>("Export Isolation List form");
            string methodFilePath = GetTestPath(REPORT_METHOD + TextUtil.EXT_CSV);
            OkDialog(exportIsolationDlg, () => exportIsolationDlg.OkDialog(methodFilePath));
            WaitForCondition(() => File.Exists(methodFilePath));
            Assert.AreEqual(SkylineWindow.Document.PeptideTransitionGroupCount + 1, File.ReadAllLines(methodFilePath).Length);
            File.Delete(methodFilePath);    // Avoid leaving this where it will get overwritten
        }

        private void ExportMethodReport()
        {
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg = ShowDialog<ManageViewsForm>(exportReportDlg.EditList);
            var viewEditor = ShowDialog<ViewEditor>(editReportListDlg.AddView);
            RunUI(() => viewEditor.ViewName = REPORT_METHOD);
            RunUI(() =>
            {
                AddColumns(viewEditor,
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.ModifiedSequence"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Mz"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Charge"));

                Assert.AreEqual(3, viewEditor.ChooseColumnsTab.ColumnCount);
                // viewEditor.ChooseColumnsTab.ScrollTreeToTop();
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Edit Report form");

            var previewReportDlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
            WaitForConditionUI(() => previewReportDlg.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(106, previewReportDlg.RowCount);
                Assert.AreEqual(3, previewReportDlg.ColumnCount);
            });
            OkDialog(previewReportDlg, previewReportDlg.Close);
            OkDialog(viewEditor, viewEditor.OkDialog);
            OkDialog(editReportListDlg, editReportListDlg.OkDialog);
            OkDialog(exportReportDlg, () =>
            {
                exportReportDlg.ReportName = REPORT_METHOD;
                exportReportDlg.OkDialog(GetTestPath(REPORT_METHOD + TextUtil.EXT_CSV),
                    TextUtil.SEPARATOR_CSV);
            });
        }

        private static void AddColumns(ViewEditor viewEditor, params PropertyPath[] columnsToAdd)
        {
            // All nodes expected in the same parent. So, just expand the parent of the first node
            viewEditor.ChooseColumnsTab.ExpandPropertyPath(columnsToAdd[0].Parent, true);
            // Make the view editor bigger so that these expanded nodes can be seen in the next screenshot
            viewEditor.Height = Math.Min(viewEditor.Height, 434);
            foreach (var id in columnsToAdd)
            {
                Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(id), "Unable to select {0}", id);
                viewEditor.ChooseColumnsTab.AddSelectedColumn();
            }
        }

        private void OpenFile(OpenDataSourceDialog openDataSourceDialog, params string[] names)
        {
            WaitForConditionUI(() => names.All(n => openDataSourceDialog.ListItemNames.Contains(n)));
            RunUI(() =>
            {
                foreach (string name in names)
                    openDataSourceDialog.SelectFile(name);
                openDataSourceDialog.Open();
            });
        }

        private void ExportScheduledMethodReport()
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            PauseForScreenShot<ImportResultsDlg>("Import Results form");

            using (new WaitDocumentChange(null, true))
            {
                if (IsArdiaTest)
                {
                    var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
                    var editAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => openDataSourceDialog.CurrentDirectory = RemoteUrl.EMPTY);
                    RunUI(() => editAccountDlg.SetRemoteAccount(ArdiaTestUtil.GetTestAccount()));
                    OkDialog(editAccountDlg, editAccountDlg.OkDialog);
                    OpenFile(openDataSourceDialog, "Skyline");
                    OpenFile(openDataSourceDialog, "PRM data");
                    OpenFile(openDataSourceDialog, "Standards");
                    OpenFile(openDataSourceDialog, "heavy-PRM");
                }
                else
                {
                    RunUI(() => SetNamedPathSets(importResultsDlg, STANDARDS_DIR));
                    OkDialog(importResultsDlg, importResultsDlg.OkDialog);
                }
            }
            RunUI(SkylineWindow.RemoveMissingResults);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 19, 31, 62, 518);
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.Height = 790;

            });
            RestoreViewOnScreen(20);

            RunUI(() =>
            {
                var lightPrecursorNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0];
                string lightNodeText = lightPrecursorNode.Text;
                AssertEx.Contains(lightNodeText, "rdotp",
                    string.Format(Resources.TransitionGroupTreeNode_GetResultsText_total_ratio__0__, 0.0002));
                var transitionRatios = new[] { 0, 0, 0.0001, 0.0002, 0.0001, 0.0003, 0, 0 };
                Assert.AreEqual(transitionRatios.Length, lightPrecursorNode.Nodes.Count);
                for (int i = 0; i < transitionRatios.Length; i++)
                {
                    AssertEx.Contains(lightPrecursorNode.Nodes[i].Text,
                        string.Format(Resources.TransitionTreeNode_GetResultsText__0__ratio__1__,
                            string.Empty, transitionRatios[i]));
                }

                var heavyPrecursorNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[1];
                string heavyNodeText = heavyPrecursorNode.Text;
                Assert.IsFalse(heavyNodeText.Contains("rdotp"));
                foreach (TreeNode transitionTreeNode in heavyPrecursorNode.Nodes)
                {
                    Assert.IsFalse(transitionTreeNode.Text.Contains(
                        string.Format(Resources.TransitionTreeNode_GetResultsText__0__ratio__1__, string.Empty, string.Empty)));
                }
            });

            PauseForScreenShot<SkylineWindow>("Skyline main window");
            RunUI(SkylineWindow.CollapsePeptides);

            SaveBackup("PRM_Scheduled");

            RunUI(() =>
            {
                SkylineWindow.ShowRTSchedulingGraph();
            });
            var schedulingProps = ShowDialog<SchedulingGraphPropertyDlg>(() =>
                SkylineWindow.ShowRTPropertyDlg(SkylineWindow.GraphRetentionTime));
            RunUI(() => schedulingProps.TimeWindows = new double[]{1, 2, 5, 10});
            OkDialog(schedulingProps, schedulingProps.OkDialog);
            WaitForGraphs();
            RestoreViewOnScreen(21);
            // RunUI(() => SkylineWindow.ShowGraphRetentionTime(true, GraphTypeSummary.schedule));
            PauseForGraphScreenShot("Schedule graph metafile", SkylineWindow.GraphRetentionTime);

            ExportScheduledIsolationList();
            if (IncludePrbbMethodReports)
                ExportScheduledMethodPrbbReport();

            RunUI(() => SkylineWindow.ShowGraphRetentionTime(false, GraphTypeSummary.schedule));
            RunUI(() => SkylineWindow.SaveDocument());
            // Digression to show removing light precursors
            RunUI(() => Assert.AreEqual(62, SkylineWindow.DocumentUI.PeptideTransitionGroupCount));
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, dlg =>
            {
                dlg.RefineLabelType = IsotopeLabelType.light;
                dlg.OkDialog();
            });
            RunUI(() => Assert.AreEqual(31, SkylineWindow.DocumentUI.PeptideTransitionGroupCount));
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, dlg =>
            {
                dlg.RefineLabelType = IsotopeLabelType.light;
                dlg.AddLabelType = true;
                dlg.OkDialog();
            });
            RunUI(() => Assert.AreEqual(62, SkylineWindow.DocumentUI.PeptideTransitionGroupCount));
            // Revert with Unto to remove the audit log entries
            RunUI(SkylineWindow.Undo);
            RunUI(() => Assert.AreEqual(31, SkylineWindow.DocumentUI.PeptideTransitionGroupCount));
            RunUI(SkylineWindow.Undo);
            RunUI(() => Assert.AreEqual(62, SkylineWindow.DocumentUI.PeptideTransitionGroupCount));
        }

        private void ExportScheduledIsolationList()
        {
            var exportIsolationDlg = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() =>
            {
                exportIsolationDlg.SetInstrument(ExportInstrumentType.THERMO_FUSION);
                exportIsolationDlg.MethodType = ExportMethodType.Scheduled;
            });
            PauseForScreenShot<ExportMethodDlg>("Export Isolation List form scheduled");
            string methodFilePath = GetTestPath(REPORT_SCHEDULED_METHOD + TextUtil.EXT_CSV);
            OkDialog(exportIsolationDlg, () => exportIsolationDlg.OkDialog(methodFilePath));
            WaitForCondition(() => File.Exists(methodFilePath));
            Assert.AreEqual(SkylineWindow.Document.PeptideTransitionGroupCount + 1, File.ReadAllLines(methodFilePath).Length); // 62 precursors and header
            File.Delete(methodFilePath);    // Avoid leaving this where it will get overwritten
        }

        private void ExportScheduledMethodPrbbReport()
        {
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg = ShowDialog<ManageViewsForm>(exportReportDlg.EditList);
            RunUI(() => editReportListDlg.SelectView(REPORT_METHOD));
            var viewEditor = ShowDialog<ViewEditor>(editReportListDlg.EditView);
            RunUI(() =>
            {
                viewEditor.ViewName = REPORT_SCHEDULED_METHOD;

                AddColumns(viewEditor,
                    PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.PeptideRetentionTime"));

                Assert.AreEqual(4, viewEditor.ChooseColumnsTab.ColumnCount);
            });
            var previewReportDlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
            WaitForConditionUI(() => previewReportDlg.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(62, previewReportDlg.RowCount);
                Assert.AreEqual(4, previewReportDlg.ColumnCount);
            });
            OkDialog(previewReportDlg, previewReportDlg.Close);
            OkDialog(viewEditor, viewEditor.OkDialog);
            OkDialog(editReportListDlg, editReportListDlg.OkDialog);
            string methodScheduledCsv = GetTestPath(REPORT_SCHEDULED_METHOD + TextUtil.EXT_CSV);
            OkDialog(exportReportDlg, () =>
            {
                exportReportDlg.ReportName = REPORT_SCHEDULED_METHOD;
                exportReportDlg.OkDialog(methodScheduledCsv, TextUtil.SEPARATOR_CSV);
            });

            WaitForCondition(() => File.Exists(methodScheduledCsv));
            var lines = File.ReadAllLines(methodScheduledCsv);
            Assert.AreEqual(63, lines.Length);  // Rows plus header line

            // Write PRM_mass_list.csv with instrument format headers
            var linesMethod = new List<string> { "Compound,Formula,Adduct,m/z,z,t start(min),t stop(min)" };
            // Write all lines skipping the exported headers
            foreach (var line in lines.Skip(1))
            {
                var parts = line.ParseCsvFields();
                double time = double.Parse(parts[3]);
                linesMethod.Add(string.Format("{0},,,{1},{2},{3},{4}", parts[0], parts[1], parts[2], time - 2.5, time + 2.5));
            }
            File.WriteAllLines(GetTestPath(INSTRUMENT_METHOD + TextUtil.EXT_CSV), linesMethod);
        }

        private void ImportReplicates()
        {
            using (new WaitDocumentChange())
            {
                RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
                {
                    dlg.RemoveAllReplicates();
                    dlg.OkDialog();
                });
            }
            Assert.IsNull(SkylineWindow.Document.MeasuredResults);
            RunUI(()=>SkylineWindow.SaveDocument());
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            using (new WaitDocumentChange(null, true))
            {
                if (IsArdiaTest)
                {
                    var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
                    RunUI(() => openDataSourceDialog.CurrentDirectory = ArdiaTestUtil.GetTestAccount().GetRootUrl());
                    OpenFile(openDataSourceDialog, "Skyline");
                    OpenFile(openDataSourceDialog, "PRM data");
                    OpenFile(openDataSourceDialog, "Samples");
                    OpenFile(openDataSourceDialog, SAMPLE_NAMES);
                }
                else
                {
                    RunUI(() => SetNamedPathSets(importResultsDlg, SAMPLES_DIR));
                }
                OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            }
            Assert.IsNotNull(SkylineWindow.Document.MeasuredResults);
            Assert.AreEqual(9, SkylineWindow.Document.MeasuredResults.Chromatograms.Count);
            WaitForGraphs();
            RunUI(SkylineWindow.ArrangeGraphsTiled);
            WaitForGraphs();
            RunDlg<ArrangeGraphsGroupedDlg>(SkylineWindow.ArrangeGraphsGrouped, dlg =>
            {
                dlg.Groups = 3;
                dlg.GroupType = GroupGraphsType.distributed;
                dlg.DisplayType = DisplayGraphsType.Row;
                dlg.GroupOrder = GroupGraphsOrder.Document;
                dlg.OkDialog();
            });

            RunUI(() => SkylineWindow.Size = new Size(1276, 840));
            RestoreViewOnScreen(24);
            RunUI(SkylineWindow.SequenceTree.ScrollLeft);
            PauseForScreenShot<SkylineWindow>("Skyline main window");

            RunUI(() =>
            {
                SkylineWindow.ShowChromatogramLegends(false);
                SkylineWindow.ShowPeakAreaLegend(false);
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.AutoZoomBestPeak();
            });

            PauseForScreenShot<SkylineWindow>("Skyline main window - split graph");
            if (IsCoverShotMode)
            {
                TakeCoverShot();
                return;
            }

            RunUI(() =>
            {
                SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.TOTAL);
                SkylineWindow.ExpandPeptides();
            });

            SelectNode(SrmDocument.Level.TransitionGroups, 0);
            RunUI(SkylineWindow.SequenceTree.ScrollLeft);

            PauseForScreenShot<SkylineWindow>("Skyline main window - normalized");
        }

        private void SetNamedPathSets(ImportResultsDlg importResultsDlg, string folderName)
        {
            var filePaths = TestFilesPersistent
                .Where(f => f.Contains(folderName))
                .Select(GetTestPath)
                .Select(MsDataFileUri.Parse);
            importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFileReplicates(filePaths);
        }

        private void RefineTransitions()
        {
            int expectedTransitionCount = SkylineWindow.Document.MoleculeTransitionCount;
            var pickList = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                Assert.AreEqual(8, pickList.ItemNames.Count());
                pickList.SetItemChecked(0, false);
                pickList.SetItemChecked(1, false);
                pickList.SetItemChecked(7, false);
                pickList.AutoManageChildren = false; // TODO: Because calling SetItemChecked does not do this
            });
            PauseForScreenShot<PopupPickList>("Transitions picklist");
            RunUI(pickList.OnOk);
            expectedTransitionCount -= 6;
            RunUI(() => Assert.AreEqual(expectedTransitionCount, SkylineWindow.DocumentUI.MoleculeTransitionCount));

            expectedTransitionCount = 379;
            using (new CheckDocumentState(19, 31, 62, expectedTransitionCount, 1, true))
            {
                RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, dlg =>
                {
                    dlg.MinPeakFoundRatio = 0.5;
                    dlg.OkDialog();
                });
            }

            SaveBackup("PRM_Picked");

            FindNode("EAGNINQSLLTLGR");
            var tranIdentityPath = IdentityPath.ROOT;
            RunUI(() =>
            {
                var selectedNode = SkylineWindow.SequenceTree.SelectedNode;
                selectedNode.Nodes[0].Expand();
                tranIdentityPath = ((SrmTreeNode)selectedNode.Nodes[0].Nodes[6]).Path;
                SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.FromIsotopeLabelType(IsotopeLabelType.heavy));
            });
            RestoreViewOnScreen(25);
            PauseForGraphScreenShot("Peak Areas - Replicate Comparison graph showing interference", SkylineWindow.GraphPeakArea);
            RunUI(() =>
            {
                SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.NONE);
                SkylineWindow.SequenceTree.SelectedPath = tranIdentityPath;
            });
            RunUI(SkylineWindow.EditDelete);
            expectedTransitionCount -= 2;
            RunUI(() => Assert.AreEqual(expectedTransitionCount, SkylineWindow.DocumentUI.MoleculeTransitionCount));

            SaveBackup("PRM_Refined");
        }

        private void InternalSinglePointCalibration()
        {
            var documentGridForm = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() =>
            {
                var gridFloatingWindow = documentGridForm.Parent.Parent;
                gridFloatingWindow.Size = new Size(874, 352);
            });
            RunUI(() => documentGridForm.ChooseView(Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results));
            WaitForConditionUI(() =>
            {
                return documentGridForm.IsComplete && documentGridForm.DataGridView.RowCount == 279 &&
                       documentGridForm.DataGridView.ColumnCount == 7;
            });
            var quantPath = PropertyPath.Root.Property(nameof(Peptide.Results)).DictionaryValues().Property(nameof(PeptideResult.Quantification));
            RunUI(() =>
            {
                var quantColumn = documentGridForm.FindColumn(quantPath);
                documentGridForm.DataGridView.AutoResizeColumn(quantColumn.Index);
                string expectedPrefix = string.Format(QuantificationStrings.QuantificationResult_ToString_Normalized_Area___0_, string.Empty);

                foreach (DataGridViewRow row in documentGridForm.DataGridView.Rows)
                    AssertEx.Contains(row.Cells[quantColumn.Index].Value.ToString(), expectedPrefix);
            });
            PauseForScreenShot<DocumentGridForm>("Document Grid - Peptide Ratio Results view");

            RunUI(() => documentGridForm.ChooseView(Resources.Resources_ReportSpecList_GetDefaults_Peptide_Quantification));
            WaitForConditionUI(() => documentGridForm.IsComplete && documentGridForm.DataGridView.RowCount == 31 && documentGridForm.DataGridView.ColumnCount == 9);
            int[] appendixConcentrations =
            {
                10, 10, 10, 20, 10, 10, 10, 2, 2, 2, 100, 20, 10, 20, 10, 20, 20, 10, 10, 10, 2, 20, 20, 100, 20, 10, 10, 20, 10, 100, 10,
            };
            var concentrationColumn = documentGridForm.FindColumn(PropertyPath.Root.Property(nameof(Peptide.InternalStandardConcentration)));
            ClipboardEx.SetText(TextUtil.LineSeparate(appendixConcentrations.Select(v => v.ToString())));
            RunUI(() =>
            {
                documentGridForm.DataGridView.CurrentCell =
                    documentGridForm.DataGridView.Rows[0].Cells[concentrationColumn.Index];
                documentGridForm.DataGridView.SendPaste();
            });
            WaitForConditionUI(() => documentGridForm.IsComplete);
            RunUI(() =>
            {
                for (int iRow = 0; iRow < appendixConcentrations.Length; iRow++)
                    Assert.AreEqual(appendixConcentrations[iRow], (double)documentGridForm.DataGridView.Rows[iRow].Cells[concentrationColumn.Index].Value);
            });

            PauseForScreenShot<DocumentGridForm>("Document Grid - Peptide Quantification view - filled");

            RunUI(() => documentGridForm.ChooseView(Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results));
            WaitForConditionUI(() => documentGridForm.IsComplete && documentGridForm.DataGridView.RowCount == 279 && documentGridForm.DataGridView.ColumnCount == 7);
            RunUI(() =>
            {
                var quantColumn = documentGridForm.FindColumn(quantPath);

                foreach (DataGridViewRow row in documentGridForm.DataGridView.Rows)
                    AssertEx.Contains(row.Cells[quantColumn.Index].Value.ToString(), QUANT_UNITS);

                documentGridForm.DataGridView.CurrentCell = documentGridForm.DataGridView.Rows[0].Cells[0];
            });

            PauseForScreenShot<DocumentGridForm>("Document Grid - Peptide Ratio Results view - calculated");
            OkDialog(documentGridForm, () => SkylineWindow.ShowDocumentGrid(false));
        }

        private void AnnotateReplicates()
        {
            var conditionNames = new[] { "G1", "G2M", "S" };

            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);

            AddReplicateAnnotation(documentSettingsDlg, "Condition", AnnotationDef.AnnotationType.value_list,
                conditionNames, true);

            AddReplicateAnnotation(documentSettingsDlg, "BioReplicate", AnnotationDef.AnnotationType.number,
                null, true);

            RunUI(() =>
            {
                documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(0, true);
                documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(1, true);
            });

            PauseForScreenShot<DocumentSettingsDlg>("Annotation Settings form with MSstats annotations");

            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);

            var documentGridForm = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() =>
            {
                var gridFloatingWindow = documentGridForm.Parent.Parent;
                gridFloatingWindow.Size = new Size(570, 308);
            });
            RunUI(() => documentGridForm.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            int rowCount = 9, cellCount = 5;
            WaitForConditionUI(() =>
                documentGridForm.IsComplete && documentGridForm.DataGridView.RowCount == rowCount &&
                documentGridForm.DataGridView.ColumnCount == cellCount);
            var bioRepPath = PropertyPath.Root.Property("annotation_BioReplicate");
            var conditionPath = PropertyPath.Root.Property("annotation_Condition");
            for (int i = 0; i < conditionNames.Length * 3; i++)
            {
                RunUI(() =>
                {
                    var bioRepCol = documentGridForm.FindColumn(bioRepPath);
                    documentGridForm.DataGridView.Rows[i].Cells[bioRepCol.Index].Value = i;
                });
                WaitForConditionUI(() => documentGridForm.IsComplete);
                RunUI(() =>
                {
                    var conditionCol = documentGridForm.FindColumn(conditionPath);
                    documentGridForm.DataGridView.Rows[i].Cells[conditionCol.Index].Value = conditionNames[i / 3];
                });
                WaitForConditionUI(() => documentGridForm.IsComplete);
            }
            RunUI(() => documentGridForm.DataGridView.CurrentCell =
                documentGridForm.DataGridView.Rows[rowCount - 1].Cells[cellCount - 1]);

            PauseForScreenShot<DocumentGridForm>("Document Grid with replicate annotations");
            OkDialog(documentGridForm, () => SkylineWindow.ShowDocumentGrid(false));

            RunUI(() =>
            {
                SkylineWindow.GroupByReplicateAnnotation("Condition");
                SkylineWindow.ShowSplitChromatogramGraph(false);
            });
            SelectNode(SrmDocument.Level.Molecules, 0);

            RestoreViewOnScreen(24);
            var skylineSize = Size.Empty;
            RunUIForScreenShot(() =>
            {
                skylineSize = SkylineWindow.Size;
                SkylineWindow.Size = new Size(1399, 883);
                SkylineWindow.ForceOnScreen();
            });
            PauseForGraphScreenShot("Peak Areas grouped", SkylineWindow.GraphPeakArea);
            PauseForGraphScreenShot("Retention Times grouped", SkylineWindow.GraphRetentionTime);
            RunUIForScreenShot(() => SkylineWindow.Size = skylineSize);

            SaveBackup("PRM_Annotated");
        }

        protected override bool IsRecordMode => false;

        private float[] _g2mVsG1ExpectedValues =
        {
            1.39317822f, 4.608368f, 4.943386f, 22.3320484f, 15.2787361f, 9.21476f, 6.45845842f, 2.899078f, 1.79695535f,
            2.031291f, 3.80708241f, 6.77203751f, 6.023097f, 4.32763624f, 1.62340987f, 1.63163662f, 6.36888456f,
            2.99105954f, 2.07597923f
        };

        private float[] _sVsG1ExpectedValues =
        {
            1.0682888f, 1.59095418f, 2.26486182f, 8.523212f, 4.90076971f, 3.23356247f, 2.37735367f, 1.53841f,
            1.01328349f, 1.41797793f, 1.648648f, 2.85256171f, 2.864974f, 1.62072313f, 0.7086786f, 0.9771881f,
            1.9611131f, 1.810476f, 1.18064868f
        };

        private void GroupComparison()
        {
            var docBeforeComparison = SkylineWindow.Document;
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            RunUI(() => documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.group_comparisons));
            const string controlAnnotation = "Condition";
            const string controlValue = "G1";
            const string identityAnnotation = "BioReplicate";
            const string comparisonName1 = "G2M-vs-G1";
            const string caseValue1 = "G2M";
            AddGroupComparison(documentSettingsDlg, comparisonName1, controlAnnotation, controlValue, caseValue1, identityAnnotation);
            const string comparisonName2 = "S-vs-G1";
            const string caseValue2 = "S";
            AddGroupComparison(documentSettingsDlg, comparisonName2, controlAnnotation, controlValue, caseValue2, identityAnnotation);
            RunUI(() => documentSettingsDlg.Height = 310);
            PauseForScreenShot<DocumentSettingsDlg>("Document Settings");
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            var docAfterComparison = WaitForDocumentChange(docBeforeComparison);
            var groupComparisonDefs = docAfterComparison.Settings.DataSettings.GroupComparisonDefs;
            Assert.AreEqual(2, groupComparisonDefs.Count);
            ValidateGroupComparison(comparisonName1, groupComparisonDefs[0], controlAnnotation, controlValue, caseValue1, identityAnnotation);
            ValidateGroupComparison(comparisonName2, groupComparisonDefs[1], controlAnnotation, controlValue, caseValue2, identityAnnotation);

            var foldChangeGrid1 = ShowDialog<FoldChangeGrid>(() => SkylineWindow.ShowGroupComparisonWindow(comparisonName1));
            WaitForConditionUI(() => 19 == foldChangeGrid1.DataboundGridControl.RowCount);
            VerifyFoldChangeValues(foldChangeGrid1, _g2mVsG1ExpectedValues, nameof(_g2mVsG1ExpectedValues));
            RunUI(() => foldChangeGrid1.Parent.Parent.Width = 383);
            PauseForScreenShot<FoldChangeGrid>(comparisonName1 + ":Grid");
            OkDialog(foldChangeGrid1, () => foldChangeGrid1.Close());

            var foldChangeGrid2 = ShowDialog<FoldChangeGrid>(() => SkylineWindow.ShowGroupComparisonWindow(comparisonName2));
            WaitForConditionUI(() => 19 == foldChangeGrid2.DataboundGridControl.RowCount);
            VerifyFoldChangeValues(foldChangeGrid2, _sVsG1ExpectedValues, nameof(_sVsG1ExpectedValues));
            RunUI(() => foldChangeGrid2.Parent.Parent.Width = 383);
            PauseForScreenShot<FoldChangeGrid>(comparisonName2 + ":Grid");
            OkDialog(foldChangeGrid2, () => foldChangeGrid2.Close());

            var foldChangeGridWithGraph = ShowDialog<FoldChangeGrid>(() => SkylineWindow.ShowGroupComparisonWindow(comparisonName1));
            WaitForConditionUI(() => foldChangeGridWithGraph.DataboundGridControl.IsComplete);
            RunUI(() => foldChangeGridWithGraph.ShowGraph());
            RestoreViewOnScreen(38);
            var foldChangeGraph = WaitForOpenForm<FoldChangeBarGraph>();
            WaitForConditionUI(() => (foldChangeGraph.ZedGraphControl.GraphPane.CurveList.FirstOrDefault()?.Points.Count ?? 0) ==
                                     foldChangeGridWithGraph.DataboundGridControl.RowCount);
            RunUI(() =>
            {
                Assert.AreEqual(foldChangeGridWithGraph.DataboundGridControl.RowCount,
                    foldChangeGraph.ZedGraphControl.GraphPane.CurveList.First().Points.Count);
            });
            PauseForGraphScreenShot<FoldChangeBarGraph>(comparisonName1 + ":Graph metafile");

            RestoreViewOnScreen(39);
            foldChangeGraph = WaitForOpenForm<FoldChangeBarGraph>();
            foldChangeGridWithGraph = WaitForOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => foldChangeGridWithGraph.IsComplete);
            RunUI(() =>
            {
                var foldChangeResultColumn =
                    foldChangeGridWithGraph.DataboundGridControl.FindColumn(PropertyPath.Root.Property("FoldChangeResult"));
                Assert.IsNotNull(foldChangeResultColumn, "Could not find FoldChangeResultColumn");
                foldChangeGridWithGraph.DataboundGridControl.DataGridView.Sort(foldChangeResultColumn, ListSortDirection.Ascending);
            });
            PauseForScreenShot(foldChangeGridWithGraph.Parent.Parent, comparisonName1 + ":Grid and Graph window");

            OkDialog(foldChangeGridWithGraph, () => foldChangeGridWithGraph.Close());
            OkDialog(foldChangeGraph, () => foldChangeGraph.Close());

            SaveBackup("PRM_ttest");
        }

        private static void ValidateGroupComparison(string comparisonName1, GroupComparisonDef groupComparison,
            string controlAnnotation, string controlValue, string caseValue1, string identityAnnotation)
        {
            Assert.AreEqual(comparisonName1, groupComparison.Name);
            Assert.AreEqual(controlAnnotation, groupComparison.ControlAnnotation);
            Assert.AreEqual(controlValue, groupComparison.ControlValue);
            Assert.AreEqual(caseValue1, groupComparison.CaseValue);
            Assert.AreEqual(identityAnnotation, groupComparison.IdentityAnnotation);
        }

        private void AddGroupComparison(DocumentSettingsDlg documentSettingsDlg, string comparisonName,
            string controlAnnotation, string controlValue, string caseValue, string identityAnnotation)
        {
            var editGroupComparisonDlg = ShowDialog<EditGroupComparisonDlg>(documentSettingsDlg.AddGroupComparison);
            RunUI(() =>
            {
                editGroupComparisonDlg.TextBoxName.Text = comparisonName;
                editGroupComparisonDlg.ControlAnnotation = controlAnnotation;
            });
            WaitForConditionUI(2000, () => editGroupComparisonDlg.ControlValueOptions.Contains(controlValue));
            RunUI(() =>
            {
                editGroupComparisonDlg.ControlValue = controlValue;
                editGroupComparisonDlg.CaseValue= caseValue;
                editGroupComparisonDlg.IdentityAnnotation = identityAnnotation;
                editGroupComparisonDlg.NormalizeOption =
                    NormalizeOption.FromNormalizationMethod(NormalizationMethod.FromIsotopeLabelTypeName("heavy"));
                editGroupComparisonDlg.TextBoxConfidenceLevel.Text = 95.ToString(CultureInfo.CurrentCulture);
                editGroupComparisonDlg.RadioScopePerProtein.Checked = true;
                // Original tutorial had this, but didn't explain it well, and it seemed to add
                // unnecessary complexity.
                // editGroupComparisonDlg.ShowAdvanced(true);
                // editGroupComparisonDlg.ComboSummaryMethod.SelectedItem = SummarizationMethod.MEDIANPOLISH;
            });
            PauseForScreenShot<EditGroupComparisonDlg>("Edit Group Comparison");
            OkDialog(editGroupComparisonDlg, editGroupComparisonDlg.OkDialog);
        }

        private void ExportResultsReport()
        {
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg = ShowDialog<ManageViewsForm>(exportReportDlg.EditList);
            var viewEditor = ShowDialog<ViewEditor>(editReportListDlg.AddView);
            RunUI(() => viewEditor.ViewName = REPORT_QUANT);
            RunUI(() =>
            {
                AddColumns(viewEditor,
                    PropertyPath.Parse("Proteins!*.Name"));
                AddColumns(viewEditor,
                    PropertyPath.Parse("Proteins!*.Peptides!*.ModifiedSequence"));
                AddColumns(viewEditor,
                    PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.RatioToStandard"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.Quantification.CalculatedConcentration"));

                Assert.AreEqual(4, viewEditor.ChooseColumnsTab.ColumnCount);
                viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>().First().SetPivotReplicate(true);
                // viewEditor.ChooseColumnsTab.ScrollTreeToTop();
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Edit Report form");

            var previewReportDlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
            WaitForConditionUI(() => previewReportDlg.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(31, previewReportDlg.RowCount);
                Assert.AreEqual(20, previewReportDlg.ColumnCount);
            });
            OkDialog(previewReportDlg, previewReportDlg.Close);
            OkDialog(viewEditor, viewEditor.OkDialog);
            OkDialog(editReportListDlg, editReportListDlg.OkDialog);

            OkDialog(exportReportDlg, () =>
            {
                exportReportDlg.ReportName = REPORT_QUANT;
                exportReportDlg.OkDialog(GetTestPath(REPORT_QUANT + TextUtil.EXT_CSV),
                    TextUtil.SEPARATOR_CSV); // Not L10N
            });

            using (new WaitDocumentChange())
            {
                var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
                RunUI(() =>
                {
                    documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.reports);
                    documentSettingsDlg.ChooseViewsControl.CheckedViews = new[] { PersistedViews.MainGroup.Id.ViewName(REPORT_QUANT) };
                });
                OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            }
            Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.ViewSpecList.ViewSpecs
                .Contains(s => Equals(REPORT_QUANT, s.Name)));
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void VerifyFoldChangeValues(FoldChangeGrid foldChangeGrid, float[] expectedValues, string variableName)
        {
            RunUI(() =>
            {
                var foldChangeRows = foldChangeGrid.FoldChangeBindingSource.GetBindingListSource().Cast<RowItem>()
                    .Select(rowItem => (FoldChangeRow)rowItem.Value).ToList();
                float[] actualValues = foldChangeRows.Select(foldChangeResult => (float) foldChangeResult.FoldChangeResult.FoldChange).ToArray();
                if (IsRecordMode)
                {
                    var commaSeparatedValues = string.Join(",", actualValues.Select(FloatToString));
                    Console.Out.WriteLine("private float[] {0} = {{ {1} }};", variableName, commaSeparatedValues);
                }
                else
                {
                    CollectionAssert.AreEqual(expectedValues, actualValues);
                }
            });
        }

        private static string FloatToString(float value)
        {
            if (Equals(value, float.NaN))
            {
                return "float.NaN";
            }

            if (Equals(value, float.PositiveInfinity))
            {
                return "float.PositiveInfinity";
            }

            if (Equals(value, float.NegativeInfinity))
            {
                return "float.NegativeInfinity";
            }

            return value.ToString("R", CultureInfo.InvariantCulture) + 'f';
        }
    }
}
