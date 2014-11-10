using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RenameProteinsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRenameProteins()
        {
            TestFilesZip = @"TestFunctional\RenameProteinsTest.zip";
            RunFunctionalTest();
        }

        private const string FIRST_RENAMED_PROTEIN = "Test";
        private const string SECOND_RENAMED_PROTEIN = "Test1";
        private const string NON_FASTA_PROTEIN = "protein";
        private const string NON_FASTA_PROTEIN_RENAMED = "protein1";

        protected override void DoTest()
        {
            RunUI(() => SetClipboardFileText(@"RenameProteinsTest\Fasta.txt")); // Not L10N

            RunUI(() => SkylineWindow.Paste());

            WaitForCondition(() => SkylineWindow.SequenceTree.Nodes.Count > 4);

            var docOrig = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            Assert.AreNotEqual(FIRST_RENAMED_PROTEIN, docOrig.PeptideGroups.ToArray()[1].Name);
            RunUI(() =>
                      {
                          SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[1];
                          SkylineWindow.SequenceTree.BeginEdit(false);
                          SkylineWindow.SequenceTree.StatementCompletionEditBox.TextBox.Text = FIRST_RENAMED_PROTEIN;
                          SkylineWindow.SequenceTree.CommitEditBox(false);
                      });
            var docEdited = WaitForDocumentChange(docOrig);
            Assert.AreEqual(FIRST_RENAMED_PROTEIN, docEdited.PeptideGroups.ToArray()[1].Name);
            RunUI(() =>
                      {
                          SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                          SkylineWindow.SequenceTree.BeginEdit(false);
                          SkylineWindow.SequenceTree.StatementCompletionEditBox.TextBox.Text = NON_FASTA_PROTEIN;
                          SkylineWindow.SequenceTree.CommitEditBox(false);
                      });


            // Choose valid proteins to rename
            {
                ClipboardEx.SetText(string.Format("YAL001C\n{0}\nYAL003W\t{1}\nYAL035W\n{2}\t{3}\n", FIRST_RENAMED_PROTEIN,
                                                  SECOND_RENAMED_PROTEIN, NON_FASTA_PROTEIN, NON_FASTA_PROTEIN_RENAMED));
                RunDlg<RenameProteinsDlg>(SkylineWindow.ShowRenameProteinsDlg,
                    renameProteinsDlg =>
                    {
                        renameProteinsDlg.Paste();
                        Assert.IsTrue(Equals(renameProteinsDlg.NameCount, 5));
                        renameProteinsDlg.OkDialog();
                    });

                RunUI(() =>
                {
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[2];
                    Assert.IsTrue(Equals(SkylineWindow.SequenceTree.SelectedNode.Text, SECOND_RENAMED_PROTEIN));
                    SkylineWindow.SequenceTree.SelectedNode =
                        SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - (TestSmallMolecules ? 3 : 2)];
                    Assert.IsTrue(Equals(SkylineWindow.SequenceTree.SelectedNode.Text, NON_FASTA_PROTEIN_RENAMED));
                });
            }

            // Choose invalid proteins to rename
            {
                ClipboardEx.SetText(string.Format("Random\tHello\n{0}\n{1}\nYAL035W\n", FIRST_RENAMED_PROTEIN, SECOND_RENAMED_PROTEIN));
                var renameProteinsDlg = ShowDialog<RenameProteinsDlg>(SkylineWindow.ShowRenameProteinsDlg);
                RunUI(renameProteinsDlg.Paste);
                RunDlg<MessageDlg>(renameProteinsDlg.OkDialog,
                    messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.RenameProteinsDlg_OkDialog__0__is_not_a_current_protein, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });
                RunUI(renameProteinsDlg.Clear);

                // Try to rename a protein twice
                ClipboardEx.SetText(string.Format("YAL001C\tTest2\n{0}\n{1}\nYAL001C\tTest3\n", FIRST_RENAMED_PROTEIN, SECOND_RENAMED_PROTEIN));
                RunUI(renameProteinsDlg.Paste);
                RunDlg<MessageDlg>(renameProteinsDlg.OkDialog,
                                                 messageDlg =>
                                                 {
                                                     AssertEx.AreComparableStrings(
                                                         Resources.
                                                             RenameProteinsDlg_OkDialog_Cannot_rename__0__more_than_once__Please_remove_either__1__or__2__,
                                                         messageDlg.Message, 3);
                                                     messageDlg.OkDialog();
                                                 });

                // Populate grid test
                RunUI(() =>
                        {
                            renameProteinsDlg.PopulateGrid();
                            Assert.IsTrue(Equals(renameProteinsDlg.NameCount, SkylineWindow.Document.MoleculeGroupCount));
                            renameProteinsDlg.CancelButton.PerformClick();
                        });

                WaitForClosedForm(renameProteinsDlg);
            }

            // Use FASTA File
            {
                Assert.AreNotSame(docOrig, SkylineWindow.Document);
                RunUI(() =>
                          {
                              SkylineWindow.Undo(); // Rename
                              SkylineWindow.Undo(); // Add nonFASTA protein
                              SkylineWindow.Undo(); // Direct edit
                          });
                WaitForCondition(() => !Equals(SkylineWindow.Document.PeptideGroups.ToArray()[1].Name, FIRST_RENAMED_PROTEIN));
                WaitForProteinMetadataBackgroundLoaderCompletedUI();
                Assert.AreSame(docOrig, SkylineWindow.Document);
                Assert.AreNotEqual(FIRST_RENAMED_PROTEIN, docOrig.PeptideGroups.ToArray()[1].Name);
                WaitForCondition(() => !Equals(SkylineWindow.SequenceTree.Nodes[2].Text, SECOND_RENAMED_PROTEIN));
                WaitForProteinMetadataBackgroundLoaderCompletedUI();
                RunUI(() =>
                {
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[2];
                    Assert.IsTrue(!Equals(SkylineWindow.SequenceTree.SelectedNode.Text, SECOND_RENAMED_PROTEIN)); 
                });


                // Use FASTA file where only two names differ
                RunDlg<RenameProteinsDlg>(SkylineWindow.ShowRenameProteinsDlg,
                                          renameProteinsDlg =>
                                              {
                                                  renameProteinsDlg.UseFastaFile(TestFilesDir.GetTestPath(@"RenameProteinsTest\FastaPartialRenamed.txt"));
                                                  Assert.IsTrue(Equals(renameProteinsDlg.NameCount, 2));
                                                  renameProteinsDlg.CancelButton.PerformClick();
                                              });

                // Use FASTA file where all names differ
                RunDlg<RenameProteinsDlg>(SkylineWindow.ShowRenameProteinsDlg,
                    renameProteinsDlg =>
                    {
                        renameProteinsDlg.UseFastaFile(TestFilesDir.GetTestPath(@"RenameProteinsTest\FastaRenamed.txt"));
                        Assert.IsTrue(Equals(renameProteinsDlg.NameCount, SkylineWindow.Document.PeptideGroupCount));
                        renameProteinsDlg.OkDialog();
                    });
                var docFastaRename = WaitForDocumentChange(docOrig);
                var peptideGroups = docFastaRename.PeptideGroups.ToArray();
                for (int i = 0; i < peptideGroups.Length; i++)
                {
                    Assert.AreEqual(string.Format("Test{0}", i + 1), peptideGroups[i].Name);
                }
            }

            // Reopen file
            {
                RunUI(() =>
                          {
                              SkylineWindow.SaveDocument(TestContext.GetTestPath("test.sky"));
                              SkylineWindow.NewDocument();
                              SkylineWindow.OpenFile(TestContext.GetTestPath("test.sky"));
                          });
                var docFastaRename = WaitForDocumentChange(docOrig);
                var peptideGroups = docFastaRename.PeptideGroups.ToArray();
                for (int i = 0; i < peptideGroups.Length; i++)
                {
                    Assert.AreEqual(string.Format("Test{0}", i + 1), peptideGroups[i].Name);
                }
            }

            // Name repetition
            {
                RunUI(() =>
                {
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                    SkylineWindow.SequenceTree.BeginEdit(false);
                    SkylineWindow.SequenceTree.StatementCompletionEditBox.TextBox.Text = FIRST_RENAMED_PROTEIN;
                    SkylineWindow.SequenceTree.CommitEditBox(false);
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[1];
                    SkylineWindow.SequenceTree.BeginEdit(false);
                    SkylineWindow.SequenceTree.StatementCompletionEditBox.TextBox.Text = FIRST_RENAMED_PROTEIN;
                    SkylineWindow.SequenceTree.CommitEditBox(false);
                });
                var renameProteinsDlg = ShowDialog<RenameProteinsDlg>(SkylineWindow.ShowRenameProteinsDlg);
                RunDlg<MessageDlg>(() => renameProteinsDlg.UseFastaFile(TestFilesDir.GetTestPath(@"RenameProteinsTest\Fasta.txt")),
                    messageDlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.RenameProteinsDlg_UseFastaFile_The_document_contains_a_naming_conflict_The_name__0__is_currently_used_by_multiple_protein_sequences, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });
                RunUI(() => renameProteinsDlg.CancelButton.PerformClick());
                WaitForClosedForm(renameProteinsDlg);
            }
        }

        private void SetClipboardFileText(string filepath)
        {
            SetClipboardTextUI(File.ReadAllText(TestFilesDir.GetTestPath(filepath)));
        }
    }
}
