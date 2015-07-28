/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.ProteomeDatabase.API;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional tests for the UI elements added with protein metadata capability.
    /// </summary>
    [TestClass]
    public class ProteinMetadataFunctionalTest : AbstractFunctionalTest
    {

        [TestMethod]
        public void ProteinMetadataFunctionalTests()
        {
            TestFilesZip = @"TestFunctional\ProteinMetadataFunctionalTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Formerly this little .sky file would not update its (unsearchable) protein metdata on load
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Mutant Peptides  with Braf AG A00Y - Cut Down.sky")));
            var doc = WaitForDocumentLoaded();
            var nodeProt = doc.MoleculeGroups.First();
            var metadata = nodeProt.ProteinMetadata;
            Assert.IsFalse(metadata.NeedsSearch());

            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ProteinMetadataFunctionalTests.sky")));

            doc = WaitForDocumentLoaded();
            nodeProt = doc.MoleculeGroups.First();
            metadata = nodeProt.ProteinMetadata;

            // Examine the various View | Targets | By* modes
            foreach (ProteinDisplayMode mode in Enum.GetValues(typeof(ProteinDisplayMode)))
            {
                ProteinDisplayMode arg = mode;
                RunUI(() => SkylineWindow.UpdateTargetsDisplayMode(arg)); // This should alter the text displayed by the node in the sequence tree view
                WaitForConditionUI(() =>
                {
                    var displayText = SkylineWindow.SequenceTree.GetSequenceNodes().First().Text;
                    if (arg != ProteinDisplayMode.ByName &&
                        Equals(displayText, GetDisplayText(ProteinDisplayMode.ByName, metadata)))
                        return false;
                    return Equals(displayText, GetDisplayText(arg, metadata));
                });
            }
            
            // Examine the various Edit | Refine | Sort Proteins | By* modes
            Assert.AreEqual("YIL075C", nodeProt.Name); // unsorted
            foreach (ProteinDisplayMode mode in Enum.GetValues(typeof (ProteinDisplayMode)))
            {
                string expectedTopName = null;
                switch (mode)
                {
                    case ProteinDisplayMode.ByName:
                        expectedTopName = "YAL003W";
                        RunUI(() => SkylineWindow.sortProteinsByNameToolStripMenuItem_Click(null, null));
                        break;
                    case ProteinDisplayMode.ByAccession:
                        expectedTopName = TestSmallMolecules ? "ZZZTESTINGNONPROTEOMICMOLECULEGROUP" : "YFL038C";
                        RunUI(() => SkylineWindow.sortProteinsByAccessionToolStripMenuItem_Click(null, null));
                        break;
                    case ProteinDisplayMode.ByPreferredName:
                        RunUI(() => SkylineWindow.sortProteinsByPreferredNameToolStripMenuItem_Click(null, null));
                        expectedTopName = TestSmallMolecules ? "ZZZTESTINGNONPROTEOMICMOLECULEGROUP" : "YAL016W";
                        break;
                    case ProteinDisplayMode.ByGene:
                        RunUI(() => SkylineWindow.sortProteinsByGeneToolStripMenuItem_Click(null, null));
                        expectedTopName = TestSmallMolecules ? "ZZZTESTINGNONPROTEOMICMOLECULEGROUP" : "YGL234W";
                        break;
                }
                var actualTopName = WaitForDocumentLoaded().MoleculeGroups.First().Name.ToUpperInvariant();
                Assert.AreEqual(expectedTopName, actualTopName);
            }

            // Now paste in our fake fasta test data, and handle it with our fake webaccess handler
            var protdbLoader = SkylineWindow.BackgroundProteomeManager;
            protdbLoader.FastaImporter = new WebEnabledFastaImporter(new CommonTest.FastaImporterTest.PlaybackProvider());
            var treeLoader = SkylineWindow.ProteinMetadataManager;
            treeLoader.FastaImporter = new WebEnabledFastaImporter(new CommonTest.FastaImporterTest.PlaybackProvider());

            const int maxEntries = 5;
            var fastaText = CommonTest.FastaImporterTest.GetFastaTestText(maxEntries);  // Just get the first few
            SetClipboardTextUI(fastaText);
            var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteFastaDlg); // Show the paste dialog

            RunDlg<EmptyProteinsDlg>(() => // Anticpate the EmptyProteinsDialog as a side effect
                {
                    pasteDlg.PasteFasta();  // Doing this in pastDlg...
                    pasteDlg.OkDialog();
                },
                dlg =>
                {
                    Assert.AreEqual(maxEntries, dlg.EmptyProteins);
                    dlg.KeepEmptyProteins(); 
                    dlg.OkDialog(); // ... will cause EmptyProteinsDlg to pop up, so OK it.
                });

            // Check that IPI:IPI00197700.1 got an accession number P04638
            WaitForCondition(() => SkylineWindow.Document.MoleculeGroups.Any(pg => Equals("IPI:IPI00197700.1", pg.Name)));
            doc = WaitForDocumentLoaded();
            nodeProt = doc.MoleculeGroups.First(pg => Equals("IPI:IPI00197700.1", pg.Name));
            Assert.AreEqual("P04638", nodeProt.ProteinMetadata.Accession);

            // Now make our fake fasta into a protdb file, and check statement completion against that
            const string basename = "fake.fasta";
            var protdbPath = TestFilesDir.GetTestPath(basename + ProteomeDb.EXT_PROTDB);
            var fastapath = TestFilesDir.GetTestPath(basename);
            using (StreamWriter outfile = new StreamWriter(fastapath))
            {
                outfile.Write(CommonTest.FastaImporterTest.GetFastaTestText()); // write them all
            } 
            BackgroundProteomeTest.CreateBackgroundProteome(protdbPath, basename, fastapath);
            doc = WaitForDocumentChange(doc);

            // Test for getting accession info from protdb
            RunUI(() =>
            {
                SequenceTree sequenceTree = SkylineWindow.SequenceTree;
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1]; // Select the creation node
                sequenceTree.BeginEdit(false);
                // ReSharper disable LocalizableElement
                sequenceTree.StatementCompletionEditBox.TextBox.Text = "NP_313205";
                // ReSharper restore LocalizableElement
                sequenceTree.CommitEditBox(false);
            });
            doc = WaitForDocumentChange(doc);
            nodeProt = doc.MoleculeGroups.First(pg => Equals("NP_313205", pg.Name));
            Assert.AreEqual("P0A7T9", nodeProt.ProteinMetadata.Accession);

            var snapshot = SkylineWindow.Document;

            // Test for getting protein from protdb by preferredName
            const string uniRef100A5Di11 = "UniRef100_A5DI11";
            const string a5Di11 = "A5DI11";
            RunUI(() =>
            {
                SequenceTree sequenceTree = SkylineWindow.SequenceTree;
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1]; // Select the creation node
                sequenceTree.BeginEdit(false);
                // ReSharper disable LocalizableElement
                sequenceTree.StatementCompletionEditBox.TextBox.Text = "EF2_PICGU"; // PreferredName for UniRef100_A5DI11
                // ReSharper restore LocalizableElement
                sequenceTree.CommitEditBox(false);
            });
            WaitForCondition(() => SkylineWindow.Document.MoleculeGroups.Any(pg => Equals(uniRef100A5Di11, pg.Name)));
            doc = WaitForDocumentChange(doc);
            nodeProt = doc.MoleculeGroups.First(pg => Equals(uniRef100A5Di11, pg.Name));
            Assert.AreEqual(a5Di11, nodeProt.ProteinMetadata.Accession);

            // Paste in some junk and make sure we handle the View Targets By* gracefully
            const string badname = "badname";
            RunUI(() =>
            {
                SequenceTree sequenceTree = SkylineWindow.SequenceTree;
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1]; // Select the creation node
                sequenceTree.BeginEdit(false);
                sequenceTree.StatementCompletionEditBox.TextBox.Text = badname;
                sequenceTree.CommitEditBox(false);
            });
            doc = WaitForDocumentChange(doc);
            var nodeText = SkylineWindow.SequenceTree.GetSequenceNodes().Last(n => !SrmDocument.IsSpecialNonProteomicTestDocNode(n.DocNode)).Text;
            var failsafe = String.Format(Resources.PeptideGroupTreeNode_ProteinModalDisplayText__name___0__, badname);  // As in PeptideGroupTreeNode.cs
            Assert.AreEqual(failsafe, nodeText);


            // Revert those changes, so we can insert another way
            Assert.IsTrue(SkylineWindow.SetDocument(snapshot, doc));
            WaitForCondition(() => !SkylineWindow.Document.MoleculeGroups.Any(pg => Equals(uniRef100A5Di11, pg.Name)));
            doc = SkylineWindow.Document;

            // Test for pasting accession number in protein paste dialog, and having it populate with correct name
            SetClipboardTextUI(a5Di11);
            PasteDlg pasteProteinsDlgA = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteProteinsDlg);
            RunUI(() =>
            {
                var selectedNode = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                SkylineWindow.SequenceTree.SelectedNode = selectedNode;
                pasteProteinsDlgA.SelectedPath = SkylineWindow.SequenceTree.SelectedPath;
                pasteProteinsDlgA.PasteProteins();
            });
            OkDialog(pasteProteinsDlgA, pasteProteinsDlgA.OkDialog);
            WaitForCondition(() => SkylineWindow.Document.MoleculeGroups.Any(pg => Equals(uniRef100A5Di11, pg.Name)));
            doc = WaitForDocumentChange(doc);
            nodeProt = doc.MoleculeGroups.First(pg => Equals(uniRef100A5Di11, pg.Name));
            Assert.AreEqual(a5Di11, nodeProt.ProteinMetadata.Accession);

            // See what happens when you paste in a gene name shared by a couple of proteins
            const string dupeGene = "Apoa2";
            const string ipi00197700 = "IPI:IPI00197700.1";
            SetClipboardTextUI(dupeGene);
            PasteDlg pasteProteinsDlgB = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteProteinsDlg);
            RunUI(() =>
            {
                var selectedNode = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                SkylineWindow.SequenceTree.SelectedNode = selectedNode;
                pasteProteinsDlgB.SelectedPath = SkylineWindow.SequenceTree.SelectedPath;
                pasteProteinsDlgB.PasteProteins();
            });
            OkDialog(pasteProteinsDlgB, pasteProteinsDlgB.OkDialog);
            WaitForCondition(() => SkylineWindow.Document.MoleculeGroups.Any(pg => Equals(ipi00197700, pg.Name)));
            doc = WaitForDocumentChange(doc);
            nodeProt = doc.MoleculeGroups.First(pg => Equals(ipi00197700, pg.Name));
            Assert.AreEqual(dupeGene, nodeProt.ProteinMetadata.Gene);

            // Test for pasting in protein PasteDlg with sequence and metadata - metadata values are same as DocumentGrid column names
            const string pasteProteinName = "Protein";
            const string pasteProteinDescription = "Description";
            const string pasteProteinAccession = "Accession";
            const string pasteProteinPreferredName = "PreferredName";
            const string pasteProteinGene = "Gene";
            const string pasteProteinSpecies = "Species";
            var pasteProteinText = String.Join("\t", pasteProteinName, pasteProteinDescription,
                "MFEQFDLDSELLASINK   IGYTKPTSIQELVIPQAMV", pasteProteinAccession, pasteProteinPreferredName,  // Note the whitespace embedded in the sequence - UI should deal with that
                pasteProteinGene, pasteProteinSpecies);
            SetClipboardTextUI(pasteProteinText);
            PasteDlg pasteProteinsDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteProteinsDlg);
            RunUI(() =>
            {
                var selectedNode = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                SkylineWindow.SequenceTree.SelectedNode = selectedNode;
                pasteProteinsDlg.SelectedPath = SkylineWindow.SequenceTree.SelectedPath;
                pasteProteinsDlg.PasteProteins();
            });
            OkDialog(pasteProteinsDlg, pasteProteinsDlg.OkDialog);
            WaitForCondition(() => SkylineWindow.Document.MoleculeGroups.Any(pg => Equals(pasteProteinName, pg.Name)));
            doc = WaitForDocumentChange(doc);
            nodeProt = doc.MoleculeGroups.First(pg => Equals(pasteProteinName, pg.Name));
            Assert.AreEqual(pasteProteinAccession, nodeProt.ProteinMetadata.Accession);

            // Verify DocumentGrid's use of protein metadata - last line should agree with var pasteProteinText
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            DocumentGridForm documentGrid = WaitForOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins));
            WaitForCondition(() => (documentGrid.RowCount > 0));  // Let it initialize
            foreach (var colName in new[]
            {
                pasteProteinAccession,
                pasteProteinPreferredName,
                pasteProteinGene,
                pasteProteinSpecies
            })
            {   // Column content should match column name, for pasteProteinText as input
                string value = null;
                string name = colName;
                RunUI(() =>
                {
                    var col = documentGrid.FindColumn(PropertyPath.Parse(name));
                    value =
                        documentGrid.DataGridView.Rows[documentGrid.RowCount - 1].Cells[col.Index].Value.ToString();
                });
                Assert.AreEqual(colName, value);
            }

        }

        private static string GetDisplayText(ProteinDisplayMode arg, ProteinMetadata proteinMetadata)
        {
            string val;
            switch (arg)
            {
                case ProteinDisplayMode.ByAccession:
                    val = proteinMetadata.Accession;
                    break;
                case ProteinDisplayMode.ByGene:
                    val = proteinMetadata.Gene;
                    break;
                case ProteinDisplayMode.ByPreferredName:
                    val = proteinMetadata.PreferredName;
                    break;
                default:
                    val = proteinMetadata.Name;
                    break;
            }
            Assert.IsFalse(String.IsNullOrEmpty(val));
            if (arg != ProteinDisplayMode.ByName)
                Assert.AreNotEqual(val, proteinMetadata.Name);
            return val;
        }
    }
}