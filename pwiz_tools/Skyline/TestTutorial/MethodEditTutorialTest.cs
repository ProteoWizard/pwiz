/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Targeted Method Editing
    /// </summary>
    [TestClass]
    public class MethodEditTutorialTest : AbstractFunctionalTestEx
    {
        private const string YEAST_ATLAS = "Yeast (Atlas)"; // Not L10N
        private const string YEAST_GPM = "Yeast (GPM)"; // Not L10N

        [TestMethod,
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME)] // Don't leak test this - it takes a long time to run even once
        public void TestMethodEditTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsPauseForAuditLog = true;
//            IsCoverShotMode = true;
            CoverShotName = "MethodEdit";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/MethodEdit-22_2.pdf";
            
            TestFilesZipPaths = new[]
            {
                @"https://skyline.ms/tutorials/MethodEdit.zip",
                @"TestTutorial\MethodEditCSVs.zip",
                @"TestTutorial\MethodEditViews.zip"
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Creating a MS/MS Spectral Library, p. 1
            PeptideSettingsUI peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryPath = TestFilesDirs[0].GetTestPath(@"MethodEdit\Library\"); // Not L10N
                buildLibraryDlg.LibraryName = YEAST_ATLAS;
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddInputFiles(new[] { TestFilesDirs[0].GetTestPath(@"MethodEdit\Yeast_atlas\interact-prob.pep.xml") }); // Not L10N
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            RunUI(() => buildLibraryDlg.Grid.SetScoreThreshold(0.95));
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);

            PeptideSettingsUI peptideSettingsUI1 = peptideSettingsUI;
            WaitForConditionUI(() => peptideSettingsUI1.PickedLibraries.Contains(YEAST_ATLAS));
            RunUI(() =>
                {
                    peptideSettingsUI1.SelectedTab = PeptideSettingsUI.TABS.Library;
                    peptideSettingsUI1.PickedLibraries = new[] { YEAST_ATLAS };
                });
            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab"); // Not L10N

            RunUI(() => peptideSettingsUI1.SelectedTab = PeptideSettingsUI.TABS.Digest);
            WaitForOpenForm<PeptideSettingsUI>();   // To show Digestion tab for Forms testing

            // Creating a Background Proteome File, p. 3
            FileEx.SafeDelete(TestFilesDirs[0].GetTestPath(@"MethodEdit\FASTA\Yeast" + ProteomeDb.EXT_PROTDB)); // Not L10N
            var buildBackgroundProteomeDlg =
                ShowDialog<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
            {
                buildBackgroundProteomeDlg.BackgroundProteomeName = "Yeast"; // Not L10N
                buildBackgroundProteomeDlg.CreateDb(TestFilesDirs[0].GetTestPath(@"MethodEdit\FASTA\Yeast" + ProteomeDb.EXT_PROTDB)); // Not L10N
            });
            AddFastaToBackgroundProteome(buildBackgroundProteomeDlg, TestFilesDirs[0].GetTestPath(@"MethodEdit\FASTA\sgd_yeast.fasta"), 61);
            RunUI(buildBackgroundProteomeDlg.SelToEndBackgroundProteomePath);
            PauseForScreenShot<BuildBackgroundProteomeDlg>("Edit Background Proteome form"); // Not L10N

            OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);

            PauseForScreenShot<PeptideSettingsUI.DigestionTab>("Peptide Settings - Digestion tab"); // Not L10N

            var docB = SkylineWindow.Document;
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            
            WaitForDocumentChange(docB);

            if (!TryWaitForCondition(() =>
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0))
            {
                Assert.Fail("Timed out loading libraries: libCount={0}, NotLoadedExplained={1}",
                    SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count,
                    SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsNotLoadedExplained ?? "<null>");
            }

            // Wait a bit in case web access is turned on and backgroundProteome is actually resolving protein metadata
            int millis = (AllowInternetAccess ? 300 : 60) * 1000;
            WaitForConditionUI(millis, () =>
                SkylineWindow.DocumentUI.Settings.HasBackgroundProteome &&
                !SkylineWindow.DocumentUI.Settings.PeptideSettings.BackgroundProteome.NeedsProteinMetadataSearch,
                () => "backgroundProteome.NeedsProteinMetadataSearch");
            WaitForConditionUI(() => SkylineWindow.DocumentUI.RevisionIndex == 3);

            // FASTA paste will happen on the UI thread
            RunUI(() =>
            {
                // Really truly fully loaded?
                var allDescriptions = SkylineWindow.DocumentUI.NonLoadedStateDescriptionsFull.ToArray();
                if (allDescriptions.Length > 0)
                    Assert.Fail(TextUtil.LineSeparate("Document not fully loaded:", TextUtil.LineSeparate(allDescriptions)));

                // Should have been 3 changes: 1. peptide settings, 2. library load, 3. background proteome completion
                AssertEx.IsDocumentState(SkylineWindow.DocumentUI, 3, 0, 0, 0, 0);
            });

            // Pasting FASTA Sequences, p. 5
            RunUI(() => SetClipboardFileText(@"MethodEdit\FASTA\fasta.txt")); // Not L10N

            // New in v0.7 : Skyline asks about removing empty proteins.
            using (new CheckDocumentState(35, 25, 25, 75, null, true))
//            using (new ImportFastaDocChangeLogger()) // Log any unexpected document changes (i.e. changes not due to import fasta)
            {
                var emptyProteinsDlg = ShowDialog<EmptyProteinsDlg>(SkylineWindow.Paste);
                RunUI(() => emptyProteinsDlg.IsKeepEmptyProteins = true);
                OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);
                WaitForCondition(millis, () => SkylineWindow.SequenceTree.Nodes.Count > 4);
            }

            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[3].Nodes[0];
                SkylineWindow.Size = new Size(1035, 511);
            });
            RestoreViewOnScreen(07);
            PauseForScreenShot("Main window"); // Not L10N

            RunUI(() =>
            {
                Settings.Default.ShowBIons = true;
                SkylineWindow.SequenceTree.SelectedNode.Expand();
                SkylineWindow.SequenceTree.SelectedNode =
                    SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Nodes[1];
            });
            PauseForScreenShot("Main window showing effect of selection on Library Match graph"); // Not L10N

            CheckTransitionCount("VDIIANDQGNR", 3); // Not L10N

            using (new CheckDocumentState(35, 28, 31, 155, null, true))
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                    {
                        transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                        transitionSettingsUI.PrecursorCharges = "2, 3"; // Not L10N
                        transitionSettingsUI.ProductCharges = "1"; // Not L10N
                        transitionSettingsUI.FragmentTypes = "y, b"; // Not L10N
                    });
                WaitForOpenForm<TransitionSettingsUI>();   // To show Filter tab for Forms testing
                PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings - Filter tab"); // Not L10N
                RunUI(() =>
                    {
                        transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library;
                        transitionSettingsUI.IonCount = 5;
                    });
                PauseForScreenShot<TransitionSettingsUI.LibraryTab>("Transition Settings - Library tab"); // Not L10N
                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }
            RunUI(() => SkylineWindow.ShowFilesTreeForm(false));
            PauseForScreenShot<SequenceTreeForm>("Targets tree clipped from main window", null,
                bmp => ClipTargets(bmp)); // Not L10N

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.SpectrumFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                });
                RestoreCoverViewOnScreen(false);
                RunUI(() => SkylineWindow.SequenceTree.TopNode = SkylineWindow.SelectedNode.Parent.Parent.Parent);
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.PrevNode);
                WaitForGraphs();
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.NextNode);
                TakeCoverShot();
                return;
            }

            CheckTransitionCount("VDIIANDQGNR", 5); // Not L10N

            // Using a Public Spectral Library, p. 9
            peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            var addLibUI = ShowDialog<EditLibraryDlg>(editListUI.AddItem);
            RunUI(() => addLibUI.LibrarySpec =
                new BiblioSpecLibSpec(YEAST_GPM, TestFilesDirs[0].GetTestPath(@"MethodEdit\Library\yeast_cmp_20.hlf"))); // Not L10N
            OkDialog(addLibUI, addLibUI.OkDialog);
            WaitForClosedForm(addLibUI);
            OkDialog(editListUI, editListUI.OkDialog);

            // Limiting Peptides per Protein, p. 11
            using (new CheckDocumentState(35, 182, 219, 1058, null, true))
            {
                RunUI(() =>
                    {
                        peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library;
                        peptideSettingsUI.PickedLibraries = new[] {YEAST_ATLAS, YEAST_GPM};
                    });
                PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab"); // Not L10N
                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                Assert.IsTrue(WaitForCondition(
                    () =>
                        SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded &&
                            SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0));
            }
            // The tutorial tells the reader they can see the library name in the spectrum graph title
            VerifyPrecursorLibrary(12, YEAST_GPM, 125);
            VerifyPrecursorLibrary(13, YEAST_ATLAS, 5.23156E+07);

            using (new CheckDocumentState(35, 47, 47, 223, 2, true))    // Wait for change loaded, and expect 2 document revisions.
            {
                RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUI2 =>
                    {
                        peptideSettingsUI2.PickedLibraries = new[] {YEAST_GPM};
                        peptideSettingsUI2.LimitPeptides = true;
                        peptideSettingsUI2.RankID = XHunterLibSpec.PEP_RANK_EXPECT;
                        peptideSettingsUI2.PeptidesPerProtein = 3;
                        peptideSettingsUI2.OkDialog();
                    });
            }

            using (new CheckDocumentState(19, 47, 47, 223, null, true))
            {
                RunUI(() =>
                    {
                        var refinementSettings = new RefinementSettings {MinPeptidesPerProtein = 1};
                        SkylineWindow.ModifyDocument("Remove empty proteins", refinementSettings.Refine); // Not L10N
                    });
            }

            // Inserting a Protein List, p. 11
            using (new CheckDocumentState(36, 58, 58, 278, null, true))
            {
                PasteDlg pasteProteinsDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteProteinsDlg);
                RunUI(() =>
                    {
                        var node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                        SkylineWindow.SequenceTree.SelectedNode = node;
                        SetClipboardFileText(@"MethodEdit\FASTA\Protein list.txt"); // Not L10N
                        pasteProteinsDlg.SelectedPath = SkylineWindow.SequenceTree.SelectedPath;
                        pasteProteinsDlg.PasteProteins();
                    });
                RunUI(() =>
                {
                    pasteProteinsDlg.SelectCell(17, 0);
                    pasteProteinsDlg.SetColumnWidths(-1, 220, 500, 0, 0, 0, 0);
                });
                PauseForScreenShot<PasteDlg.ProteinListTab>("Insert Protein List - For Screenshot, select last (empty) item in list"); // Not L10N
                OkDialog(pasteProteinsDlg, pasteProteinsDlg.OkDialog);
            }

            using (new CheckDocumentState(24, 58, 58, 278, null, true))
            {
                RunUI(() =>
                    {
                        var refinementSettings = new RefinementSettings {MinPeptidesPerProtein = 1};
                        SkylineWindow.ModifyDocument("Remove empty proteins", refinementSettings.Refine); // Not L10N
                    });
            }

            // Wait for protein metadata again to avoid changes during paste
            WaitForProteinMetadataBackgroundLoaderCompleted(millis);
            
            // Inserting a Peptide List, p. 13
            using (new CheckDocumentState(25, 70, 70, 338, null, true))
//            using (new ImportFastaDocChangeLogger()) // Log any unexpected document changes (i.e. changes not due to import fasta)
            {
                RunUI(() =>
                    {
                        SetClipboardFileText(@"MethodEdit\FASTA\Peptide list.txt"); // Not L10N
                        SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                        SkylineWindow.Paste();
                    });
            }

            RunUI(() => SkylineWindow.SequenceTree.Nodes[0].Text = @"Primary Peptides");
            FindNode("TLTAQSMQNSTQSAPNK"); // Not L10N
            PauseForScreenShot("Main window"); // Not L10N

            using (new CheckDocumentState(35, 70, 70, 338, null, true))
            {
                RunUI(() => SkylineWindow.Undo());
                PasteDlg pastePeptidesDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
                RunUI(pastePeptidesDlg.PastePeptides);
                RunUI(() =>
                {
                    pastePeptidesDlg.SelectCell(12, 0);
                    pastePeptidesDlg.Height = 437;
                });
                PauseForScreenShot<PasteDlg.PeptideListTab>("Insert Peptide List -  For screenshot, select last (empty) line in list"); // Not L10N
                OkDialog(pastePeptidesDlg, pastePeptidesDlg.OkDialog);
            }

            // Simple Refinement, p. 16
            var findPeptideDlg = ShowDialog<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg);
            RunUI(() => findPeptideDlg.SearchString = "IPEE"); // Not L10N
            OkDialog(findPeptideDlg, () =>
                                         {
                                             findPeptideDlg.FindNext();
                                             findPeptideDlg.Close();
                                         });
            PauseForGraphScreenShot("Library Match graph metafile", SkylineWindow.GraphSpectrum); // Not L10N

            using (new CheckDocumentState(35, 64, 64, 320, null, true))
            {
                RefineDlg refineDlg = ShowDialog<RefineDlg>(SkylineWindow.ShowRefineDlg);
                PauseForForm(typeof(RefineDlg.DocumentTab));
                RunUI(() => refineDlg.MinTransitions = 5);
                OkDialog(refineDlg, refineDlg.OkDialog);
                PauseForScreenShot("29/35 prot 50/64 pep 50/64 prec 246/320 tran", null,
                    ClipSelectionStatus); // Not L10N
            }

            // Checking Peptide Uniqueness, p. 18
            RunUI(() =>
            {
                var node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 2];
                SkylineWindow.SequenceTree.SelectedNode = node;
            });

            using (new CheckDocumentState(34, 63, 63, 315, null, true))
            {
                var uniquePeptidesDlg = ShowDialog<UniquePeptidesDlg>(SkylineWindow.ShowUniquePeptidesDlg);
                WaitForConditionUI(() => uniquePeptidesDlg.GetDataGridView().RowCount == 1);
                RunUI(() =>
                    {
                        Assert.AreEqual(1, uniquePeptidesDlg.GetDataGridView().RowCount);
                        Assert.AreEqual(7, uniquePeptidesDlg.GetDataGridView().ColumnCount);

                        uniquePeptidesDlg.SplitHeight = 58;
                        uniquePeptidesDlg.Height = 292;
                    });
                PauseForScreenShot<UniquePeptidesDlg>("Unique Peptides form"); // Not L10N
                var oldDoc = SkylineWindow.Document;
                OkDialog(uniquePeptidesDlg, uniquePeptidesDlg.OkDialog);
                RunUI(() => Assert.AreSame(oldDoc, SkylineWindow.DocumentUI));
                RunUI(() => SkylineWindow.EditDelete());
            }

            // Protein Name Auto-Completion
            TestAutoComplete("ybl087", 0, true, 51); // Not L10N
            var peptideGroups = new List<PeptideGroupDocNode>(Program.ActiveDocument.PeptideGroups);
            Assert.AreEqual("YBL087C", peptideGroups[peptideGroups.Count - 1].Name); // Not L10N

            // Protein Description Auto-Completion
            TestAutoComplete("eft2", 0, true, 83); // Sorting logic puts this at the 0th entry in the list - Not L10N
            peptideGroups = new List<PeptideGroupDocNode>(Program.ActiveDocument.PeptideGroups);
            Assert.AreEqual("YDR385W", peptideGroups[peptideGroups.Count - 1].Name); // Not L10N

            // Peptide Sequence Auto-Completion, p. 21
            TestAutoComplete("IQGP", 0); // Not L10N
            var peptides = new List<PeptideDocNode>(Program.ActiveDocument.Peptides);
            Assert.AreEqual("K.AYLPVNESFGFTGELR.Q [770, 785]", peptides[peptides.Count - 1].Peptide.ToString()); // Not L10N
            RestoreViewOnScreen(21);
            RunUI(() => SkylineWindow.ShowFilesTreeForm(false));
            PauseForScreenShot<SequenceTreeForm>("(fig. 1) - Added targets", null,
                bmp => ClipTargets(bmp, 10, true, true)); // Not L10N

            // Pop-up Pick-Lists, p. 21
            using (new CheckDocumentState(36, 71, 71, 355, null, true))
            {
                RunUI(() =>
                    {
                        var node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 3];
                        SkylineWindow.SequenceTree.SelectedNode = node;
                    });
                var pickList = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
                RunUI(() =>
                    {
                        pickList.ApplyFilter(false);
                        pickList.SetItemChecked(8, true, true);
                        pickList.AutoManageChildren = false; // TODO: Because calling SetItemChecked does not do this
                    });
                PauseForScreenShot<PopupPickList>("(fig. 2) - YBL087C Peptides picklist"); // Not L10N
                RunUI(pickList.OnOk);
            }

            using (new CheckDocumentState(36, 71, 71, 355))
            {
                RunUI(() =>
                    {
                        SkylineWindow.SequenceTree.Nodes[34].ExpandAll();
                        var node =
                            SkylineWindow.SequenceTree.Nodes[34].Nodes[0].Nodes[0];
                        SkylineWindow.SequenceTree.SelectedNode = node;
                    });
                var pickList1 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
                RunUI(() =>
                    {
                        pickList1.SearchString = "y"; // Not L10N
                        pickList1.SetItemChecked(0, false);
                        pickList1.SetItemChecked(1, false);
                        pickList1.ApplyFilter(false);
                        pickList1.ToggleFind();
                        pickList1.SearchString = "b ++"; // Not L10N
                        pickList1.SetItemChecked(4, true);
                        pickList1.SetItemChecked(6, true, true);
                    });
                PauseForScreenShot<PopupPickList>("b ++ filtered picklist"); // Not L10N
                RunUI(pickList1.OnOk);
            }

            // Bigger Picture, p. 22. Drag and Drop, p. 23
            RunUI(() =>
            {
                ITipProvider nodeTip = SkylineWindow.SequenceTree.SelectedNode as ITipProvider;
                Assert.IsTrue(nodeTip != null && nodeTip.HasTip);
                var nodeName = SkylineWindow.SequenceTree.Nodes[1].Name;
                SkylineWindow.ModifyDocument("Drag and drop", // Not L10N
                    doc => doc.MoveNode(SkylineWindow.Document.GetPathTo(0, 1), SkylineWindow.Document.GetPathTo(0, 0), out _));
                Assert.IsTrue(SkylineWindow.SequenceTree.Nodes[0].Name == nodeName);
            });

            FindNode(string.Format("L [b5] - {0:F04}+", 484.3130)); // Not L10N - may be localized " (rank 3)"
            ShowNodeTip("YBL087C", true);
            ShowNodeTip(string.Format("{0:F04}+++", 672.6716), true);
            ShowNodeTip(null);

            // Preparing to Measure, p. 25
            RunDlg<TransitionSettingsUI>(() => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.Prediction), transitionSettingsUI =>
            {
                transitionSettingsUI.RegressionCE = Settings.Default.GetCollisionEnergyByName("SCIEX"); // Not L10N
                transitionSettingsUI.RegressionDP = Settings.Default.GetDeclusterPotentialByName("SCIEX"); // Not L10N
                transitionSettingsUI.InstrumentMaxMz = 1800;
                transitionSettingsUI.OkDialog();
            });
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDirs[0].GetTestPath("MethodEdit Tutorial.sky"))); // Not L10N
            var exportDialog = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
            RunUI(() =>
            {
                exportDialog.ExportStrategy = ExportStrategy.Buckets;
                exportDialog.MethodType = ExportMethodType.Standard;
                exportDialog.OptimizeType = ExportOptimize.NONE;
                exportDialog.IgnoreProteins = true;
                exportDialog.MaxTransitions = 75;
            });
            PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form"); // Not L10N
            
            const string basename = "Yeast_list"; //  Not L10N
            OkDialog(exportDialog, () => exportDialog.OkDialog(TestFilesDirs[0].GetTestPath(basename)));  // write Yeast_list_000n.csv

            // check the output files
            for (int n = 0; n++ < 5;)
            {
                var csvname = String.Format("{0}_{1}.csv", basename, n.ToString("D4")); // Not L10N

                // AssertEx.FieldsEqual is hard-coded with CultureInfo.InvariantCulture, but so is transition list CSV export, so OK
                using (TextReader actual = new StreamReader(TestFilesDirs[0].GetTestPath(csvname)))
                using (TextReader target = new StreamReader(TestFilesDirs[1].GetTestPath(csvname)))
                {
                    AssertEx.FieldsEqual(target, actual, 6, null, true);
                }
            }
        }

        private void VerifyPrecursorLibrary(int indexPrecursor, string libraryName, double maxIntensity)
        {
            SelectNode(SrmDocument.Level.TransitionGroups, indexPrecursor);
            WaitForGraphs();
            RunUI(() =>
            {
                var graphSpec = SkylineWindow.GraphSpectrum;
                Assert.IsTrue(graphSpec.GraphTitle.StartsWith(libraryName),
                    string.Format("Graph title '{0}' does not start with {1}", graphSpec.GraphTitle, libraryName));
                Assert.AreEqual(maxIntensity, graphSpec.IntensityScale.Max);
            });
        }

        private void ShowNodeTip(string nodeText, bool pause = false)
        {
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.MoveMouse(new Point(-1, -1));
                Assert.IsFalse(SkylineWindow.SequenceTree.IsTipVisible);
            });
            if (string.IsNullOrEmpty(nodeText))
                return;
            SkylineWindow.SequenceTree.IgnoreFocus = true;
            RunUI(() =>
            {
                var node = FindSequenceTreeNode(nodeText);
                Assert.IsNotNull(node, "Missing tree node: {0}", nodeText);
                var rect = node.Bounds;
                var pt = new Point((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
                SkylineWindow.SequenceTree.MoveMouse(pt);
            });
            WaitForConditionUI(NodeTip.TipDelayMs * 10, () => SkylineWindow.SequenceTree.IsTipVisible);

            if (pause)
            {
                PauseForScreenShot<ScreenForm>("Tip for " + nodeText, null,
                    bmp =>
                    {
                        var cropRect = SkylineWindow.SequenceTree.TipRect;
                        // Remove lower-right shadow
                        cropRect.Width -= 4;
                        cropRect.Height -= 4;
                        return ClipBitmap(bmp, cropRect);
                    });
            }

            SkylineWindow.SequenceTree.IgnoreFocus = false;
            // If someone is watching let them at least see the tips, if not take screenshots of them
            int delayMultiplier = IsPauseForScreenShots ? 4 : 1;
            Thread.Sleep(NodeTip.TipDelayMs * delayMultiplier);
        }

        private static SrmTreeNode FindSequenceTreeNode(string nodeText)
        {
            return FindSequenceTreeNode(nodeText, SkylineWindow.SequenceTree.Nodes);
        }

        private static SrmTreeNode FindSequenceTreeNode(string nodeText, TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Text == nodeText)
                    return (SrmTreeNode) node;
                var childNode = FindSequenceTreeNode(nodeText, node.Nodes);
                if (childNode != null)
                    return childNode;
            }
            return null;
        }

        private void SetClipboardFileText(string filepath)
        {
            SetClipboardTextUI(File.ReadAllText(TestFilesDirs[0].GetTestPath(filepath)));
        }

        private void TestAutoComplete(string text, int index, bool pause = false, int aboveAutoComplete = 0)
        {
            var doc = WaitForDocumentLoaded();
            RunUI(() =>
            {
                var node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                SkylineWindow.SequenceTree.SelectedNode = node;
                SkylineWindow.SequenceTree.BeginEdit(false);
                SkylineWindow.SequenceTree.StatementCompletionEditBox.TextBox.Text = text;
            });
            var statementCompletionForm = WaitForOpenForm<StatementCompletionForm>();
            Assert.IsNotNull(statementCompletionForm);
            if (pause)
            {
                RunUI(() => SkylineWindow.SequenceTree.StatementCompletionEditBox.SelectWithoutChoosing(0));
                PauseForScreenShot<ScreenForm>("Auto-complete " + text, null,
                    bmp =>
                    {
                        var completeRect = statementCompletionForm.Bounds;
                        var skylineRect = ScreenshotManager.GetFramedWindowBounds(SkylineWindow);
                        bmp = bmp.CleanupBorder(skylineRect, ScreenshotProcessingExtensions.CornerForm, completeRect);

                        int top = completeRect.Top - aboveAutoComplete;
                        int bottom = Math.Max(skylineRect.Bottom, completeRect.Bottom);
                        var cropRect = new Rectangle(skylineRect.Left, top, 735,  bottom - top);
                        if (skylineRect.Bottom < bottom)
                        {
                            var bmpClipped = ClipRegionAndEraseBackground(bmp,
                                new Control[] { statementCompletionForm, SkylineWindow },
                                Array.Empty<ToolStripDropDown>(),
                                Color.White);

                            // The final clipping expects a full screen bitmap
                            var bmpNew = new Bitmap(bmp.Width, bmp.Height);
                            using var g = Graphics.FromImage(bmpNew);
                            g.DrawImageUnscaled(bmpClipped, skylineRect.X, skylineRect.Y);
                            bmp = bmpNew;
                        }
                        return ClipBitmap(bmp, cropRect);
                    });
            }

            RunUI(() => SkylineWindow.SequenceTree.StatementCompletionEditBox.OnSelectionMade(
                (StatementCompletionItem)statementCompletionForm.ListView.Items[index].Tag));
            WaitForDocumentChangeLoaded(doc);
        }

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private static void CheckTransitionCount(string peptideSequence, int count)
        {
            var doc = SkylineWindow.Document;
            var nodePeptide = doc.Molecules.FirstOrDefault(nodePep =>
                Equals(peptideSequence, nodePep.Peptide.TextId));
            Assert.IsNotNull(nodePeptide);
            Assert.IsTrue(nodePeptide.TransitionCount == count);
        }
    }
}
