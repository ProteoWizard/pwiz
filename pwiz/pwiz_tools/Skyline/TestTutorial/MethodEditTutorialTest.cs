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

using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Targeted Method Editing
    /// </summary>
    [TestClass]
    public class MethodEditTutorialTest : AbstractFunctionalTest
    {
        private const string YEAST_ATLAS = "Yeast (Atlas)";
        private const string YEAST_GPM = "Yeast (GPM)";

        [TestMethod]
        public void TestMethodEditTutorial()
        {
            TestFilesZip = @"https://skyline.gs.washington.edu/tutorials/MethodEdit.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Creating a MS/MS Spectral Library, p. 1
            PeptideSettingsUI peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunDlg<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg, buildLibraryDlg =>
            {
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath(@"MethodEdit\Library\");
                buildLibraryDlg.LibraryName = YEAST_ATLAS;
                buildLibraryDlg.LibraryCutoff = 0.95;
                buildLibraryDlg.LibraryAuthority = "peptideatlas.org";
                buildLibraryDlg.OkWizardPage();
                IList<string> inputPaths = new List<string>
                 {
                     TestFilesDir.GetTestPath(@"MethodEdit\Yeast_atlas\interact-prob.pep.xml")
                 };
                buildLibraryDlg.AddInputFiles(inputPaths);
                buildLibraryDlg.OkWizardPage();
            });

            // Creating a Background Proteome File, p. 3
            File.Delete(TestFilesDir.GetTestPath(@"MethodEdit\FASTA\Yeast" + ProteomeDb.EXT_PROTDB));
            var buildBackgroundProteomeDlg =
                ShowDialog<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg);
            PeptideSettingsUI peptideSettingsUI1 = peptideSettingsUI;
            RunUI(() =>
            {
                peptideSettingsUI1.PickedLibraries = new[] { YEAST_ATLAS };
                buildBackgroundProteomeDlg.BuildNew = true;
                buildBackgroundProteomeDlg.BackgroundProteomePath =
                    TestFilesDir.GetTestPath(@"MethodEdit\FASTA\Yeast");
                buildBackgroundProteomeDlg.BackgroundProteomeName = "Yeast";
                buildBackgroundProteomeDlg.AddFastaFile(
                    TestFilesDir.GetTestPath(@"MethodEdit\FASTA\sgd_yeast.fasta"));
            });
            OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            Assert.IsTrue(WaitForCondition(() =>
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0));

            Assert.IsTrue(WaitForCondition(() =>
            {
                var peptideSettings = Program.ActiveDocument.Settings.PeptideSettings;
                var backgroundProteome = peptideSettings.BackgroundProteome;
                return (backgroundProteome.GetDigestion(peptideSettings) != null);
            }));

            // Pasting FASTA Sequences, p. 5
            RunUI(() => SetClipboardFileText(@"MethodEdit\FASTA\fasta.txt"));

            // New in v0.7 : Skyline asks about removing empty proteins.
            var emptyProteinsDlg = ShowDialog<EmptyProteinsDlg>(SkylineWindow.Paste);
            RunUI(() => emptyProteinsDlg.IsKeepEmptyProteins = true);
            OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);

            WaitForCondition(() => SkylineWindow.SequenceTree.Nodes.Count > 4);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 35, 25, 25, 75);
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[3].Nodes[0];
                Settings.Default.ShowBIons = true;
                SkylineWindow.SequenceTree.SelectedNode.Expand();
                SkylineWindow.SequenceTree.SelectedNode =
                    SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Nodes[1];
            });

            CheckTransitionCount("VDIIANDQGNR", 3);

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.PrecursorCharges = "2,3";
                transitionSettingsUI.ProductCharges = "1";
                transitionSettingsUI.FragmentTypes = "y,b";
                transitionSettingsUI.IonCount = 5;
                transitionSettingsUI.OkDialog();
            });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 35, 28, 31, 155);

            CheckTransitionCount("VDIIANDQGNR", 5);

            // Using a Public Spectral Library, p. 8
            peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            var addLibUI = ShowDialog<EditLibraryDlg>(editListUI.AddItem);
            RunUI(() => addLibUI.LibrarySpec = 
                new BiblioSpecLibSpec(YEAST_GPM, TestFilesDir.GetTestPath(@"MethodEdit\Library\yeast_cmp_20.hlf")));
            OkDialog(addLibUI, addLibUI.OkDialog);
            WaitForClosedForm(addLibUI);
            OkDialog(editListUI, editListUI.OkDialog);

            // Limiting Peptides per Protein, p. 10
            RunUI(() => peptideSettingsUI.PickedLibraries = new[] { YEAST_GPM });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            Assert.IsTrue(WaitForCondition(
                () =>
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded &&
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0));

            AssertEx.IsDocumentState(SkylineWindow.Document, null, 35, 180, 216, 1036);

            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUI2 =>
               {
                   peptideSettingsUI2.LimitPeptides = true;
                   peptideSettingsUI2.RankID = new PeptideRankId("Expect");
                   peptideSettingsUI2.PeptidesPerProtein = 3;
                   peptideSettingsUI2.OkDialog();
               });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 35, 47, 47, 223);
            RunUI(() =>
            {
                var refinementSettings = new RefinementSettings { MinPeptidesPerProtein = 1 };
                SkylineWindow.ModifyDocument("Remove empty proteins", refinementSettings.Refine);
            });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 19, 47, 47, 223);

            // Inserting a Protein List, p. 10
            PasteDlg pasteProteinsDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteProteinsDlg);
            RunUI(() =>
            {
                var node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                SkylineWindow.SequenceTree.SelectedNode = node;
                SetClipboardFileText(@"MethodEdit\FASTA\Protein list.txt");
                pasteProteinsDlg.SelectedPath = SkylineWindow.SequenceTree.SelectedPath;
                pasteProteinsDlg.PasteProteins();
            });
            OkDialog(pasteProteinsDlg, pasteProteinsDlg.OkDialog);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 36, 58, 58, 278);
            RunUI(() =>
            {
                var refinementSettings = new RefinementSettings { MinPeptidesPerProtein = 1 };
                SkylineWindow.ModifyDocument("Remove empty proteins", refinementSettings.Refine);
            });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 24, 58, 58, 278);

            // Inserting a Peptide List, p. 12
            RunUI(() =>
            {
                SetClipboardFileText(@"MethodEdit\FASTA\Peptide list.txt");
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                SkylineWindow.Paste();
                SkylineWindow.SequenceTree.Nodes[0].Text = "Primary Peptides";
            });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 25, 70, 70, 338);
            RunUI(() => SkylineWindow.Undo());
            PasteDlg pastePeptidesDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
            RunUI(pastePeptidesDlg.PastePeptides);
            OkDialog(pastePeptidesDlg, pastePeptidesDlg.OkDialog);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 35, 70, 70, 338);

            // Simple Refinement, p. 13
            var findPeptideDlg = ShowDialog<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg);
            RunUI(() => findPeptideDlg.SearchString = "IPEE");
            OkDialog(findPeptideDlg, () =>
                                         {
                                             findPeptideDlg.FindNext();
                                             findPeptideDlg.Close();
                                         });
            RefineDlg refineDlg = ShowDialog<RefineDlg>(SkylineWindow.ShowRefineDlg);
            RunUI(() => refineDlg.MinTransitions = 5);
            OkDialog(refineDlg, refineDlg.OkDialog);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 35, 64, 64, 320);

            // Checking Peptide Uniqueness, p. 14
            RunUI(() =>
            {
                var node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 2];
                SkylineWindow.SequenceTree.SelectedNode = node;
            });

            var uniquePeptidesDlg = ShowDialog<UniquePeptidesDlg>(SkylineWindow.ShowUniquePeptidesDlg);
            WaitForConditionUI(() => uniquePeptidesDlg.GetDataGridView().RowCount == 1);
            RunUI(() =>
               {
                   Assert.AreEqual(1, uniquePeptidesDlg.GetDataGridView().RowCount);
                   Assert.AreEqual(7, uniquePeptidesDlg.GetDataGridView().ColumnCount);
               });
            OkDialog(uniquePeptidesDlg, uniquePeptidesDlg.OkDialog);
            RunUI(() => SkylineWindow.EditDelete());
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 34, 63, 63, 315);

            // Protein Name Auto-Completion, p. 15
            TestAutoComplete("ybl087", 0);
            var peptideGroups = new List<PeptideGroupDocNode>(Program.ActiveDocument.PeptideGroups);
            Assert.AreEqual("YBL087C", peptideGroups[peptideGroups.Count - 1].Name);

            // Protein Description Auto-Completion, p. 16
            TestAutoComplete("eft2", 1);
            peptideGroups = new List<PeptideGroupDocNode>(Program.ActiveDocument.PeptideGroups);
            Assert.AreEqual("YDR385W", peptideGroups[peptideGroups.Count - 1].Name);

            // Peptide Sequence Auto-Completion, p. 16
            TestAutoComplete("IQGP", 0);
            var peptides = new List<PeptideDocNode>(Program.ActiveDocument.Peptides);
            Assert.AreEqual("K.AYLPVNESFGFTGELR.Q [769, 784]", peptides[peptides.Count - 1].Peptide.ToString());

            // Pop-up Pick-Lists, p. 17
            RunUI(() =>
            {
                var node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 3];
                SkylineWindow.SequenceTree.SelectedNode = node;
            });
            var pickList = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            var curDoc = SkylineWindow.Document;
            RunUI(() =>
            {
                pickList.ApplyFilter(false);
                pickList.ToggleFind();
                pickList.SearchString = "(rank 6)";
                pickList.SetItemChecked(0, true);
                pickList.OnOk();
            });
            WaitForDocumentChange(curDoc);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 36, 71, 71, 355);
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.Nodes[34].ExpandAll();
                var node =
                    SkylineWindow.SequenceTree.Nodes[34].Nodes[0].Nodes[0];
                SkylineWindow.SequenceTree.SelectedNode = node;
            });
            var pickList1 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            curDoc = SkylineWindow.Document;
            RunUI(() =>
            {
                pickList1.SearchString = "y";
                pickList1.SetItemChecked(0, false);
                pickList1.SetItemChecked(1, false);
                pickList1.ApplyFilter(false);
                pickList1.ToggleFind();
                pickList1.SearchString = "b ++";
                pickList1.SetItemChecked(5, true);
                pickList1.SetItemChecked(7, true);
                pickList1.OnOk();
            });
            WaitForDocumentChange(curDoc);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 36, 71, 71, 355);

            // Bigger Picture, p. 19. Drag and Drop, p. 19
            RunUI(() =>
            {
                ITipProvider nodeTip = SkylineWindow.SequenceTree.SelectedNode as ITipProvider;
                Assert.IsTrue(nodeTip != null && nodeTip.HasTip);
                var nodeName = SkylineWindow.SequenceTree.Nodes[1].Name;
                IdentityPath selectPath;
                SkylineWindow.ModifyDocument("Drag and drop",
                    doc => doc.MoveNode(SkylineWindow.Document.GetPathTo(0, 1), SkylineWindow.Document.GetPathTo(0, 0), out selectPath));
                Assert.IsTrue(SkylineWindow.SequenceTree.Nodes[0].Name == nodeName);
            });

            // Preparing to Measure, p. 20
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.RegressionCE = Settings.Default.GetCollisionEnergyByName("ABI 4000 QTrap");
                transitionSettingsUI.RegressionDP = Settings.Default.GetDeclusterPotentialByName("ABI");
                transitionSettingsUI.InstrumentMaxMz = 1800;
                transitionSettingsUI.OkDialog();
            });
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("MethodEdit Tutorial.sky")));
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
            OkDialog(exportDialog, () => exportDialog.OkDialog(TestFilesDir.GetTestPath("")));
        }

        private void SetClipboardFileText(string filepath)
        {
            SetClipboardTextUI(File.ReadAllText(TestFilesDir.GetTestPath(filepath)));
        }

        private static void TestAutoComplete(string text, int index)
        {
            RunUI(() =>
            {
                var node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                SkylineWindow.SequenceTree.SelectedNode = node;
                SkylineWindow.SequenceTree.BeginEdit(false);
                SkylineWindow.SequenceTree.StatementCompletionEditBox.TextBox.Text = text;
            });
            var statementCompletionForm = WaitForOpenForm<StatementCompletionForm>();
            Assert.IsNotNull(statementCompletionForm);
            RunUI(() => SkylineWindow.SequenceTree.StatementCompletionEditBox.OnSelectionMade(
                            (StatementCompletionItem)statementCompletionForm.ListView.Items[index].Tag));
        }

        private static void CheckTransitionCount(string peptideSequence, int count)
        {
            var doc = SkylineWindow.Document;
            var nodePeptide = doc.Peptides.FirstOrDefault(nodePep =>
                Equals(peptideSequence, nodePep.Peptide.Sequence));
            Assert.IsNotNull(nodePeptide);
            Assert.IsTrue(nodePeptide.TransitionCount == count);
        }
    }
}
