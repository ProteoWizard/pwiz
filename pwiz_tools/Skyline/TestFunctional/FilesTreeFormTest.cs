/*
 * Copyright 2025 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Model.Results;

// TODO: Replicate => verify right-click menu includes Open Containing Folder
// TODO: Test Tooltips. See MethodEditTutorialTest.ShowNodeTip
namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreeFormTest : AbstractFunctionalTest
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)] // No parallel testing because SkylineException.txt is written to Skyline.exe directory by design - and that directory is shared by all workers
        public void TestFilesTreeForm()
        {
            TestFilesZip = @"TestFunctional\FilesTreeFormTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        { 
            // SCENARIO - empty document 
            RunUI(() =>
            {
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            Assert.AreEqual(ControlsResources.FilesTree_TreeNodeLabel_NewDocument, SkylineWindow.FilesTree.RootNodeText());
            Assert.AreEqual(1, SkylineWindow.FilesTree.Nodes.Count);
            Assert.AreEqual(1, SkylineWindow.FilesTree.Nodes[0].GetNodeCount(false));

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() =>
            {
                SkylineWindow.DestroyFilesTreeForm();
            });

            // SCENARIO
            var emptyDocument = SrmDocumentHelper.MakeEmptyDocument();
            SrmDocumentHelper.AddProteinsToDocument(emptyDocument, 50);

            RunUI(() =>
            {
                SkylineWindow.SwitchDocument(emptyDocument, null);
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            Assert.AreEqual(ControlsResources.FilesTree_TreeNodeLabel_NewDocument, SkylineWindow.FilesTree.RootNodeText());

            // FilesTree should only have one set of nodes after opening a new document
            Assert.AreEqual(1, SkylineWindow.FilesTree.Nodes.Count);
            Assert.AreEqual(1, SkylineWindow.FilesTree.Nodes[0].GetNodeCount(false));

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() =>
            {
                SkylineWindow.DestroyFilesTreeForm();
            });

            // SCENARIO - real .sky file
            var documentPath = TestFilesDir.GetTestPath("FilesTreeFormTest\\Rat_plasma.sky");
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentPath);
                SkylineWindow.ShowFilesTreeForm(true);
                SkylineWindow.FilesTree.Nodes[0].ExpandAll();
            });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            Assert.AreEqual("Rat_plasma.sky", SkylineWindow.FilesTree.RootNodeText());
            Assert.AreEqual(3, SkylineWindow.FilesTree.Nodes[0].Nodes.Count);

            //
            // Replicates / Chromatograms
            //

            var doc = SkylineWindow.Document;
            var chromatogramSets = doc.Settings.MeasuredResults.Chromatograms;
            var replicateTreeNodes = SkylineWindow.FilesTree.NodesForFileType(FileType.replicates)?.Nodes;

            Assert.AreEqual(42, chromatogramSets.Count);
            CheckEquivalence(chromatogramSets, replicateTreeNodes);

            // Rename replicate, asserting FilesTree's node gets new name in correct position
            doc = SkylineWindow.Document;

            const string newName = "Test this name";

            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Rename replicate in FilesTree test", srmDoc =>
                {
                    var chromSet = doc.MeasuredResults.Chromatograms[3];
                    var newChromSet = (ChromatogramSet)chromSet.ChangeName(newName);

                    var measuredResults = doc.MeasuredResults;
                    var chromatograms = measuredResults.Chromatograms.ToArray();

                    chromatograms[3] = newChromSet; // update with modified ChromatogramSet

                    measuredResults = measuredResults.ChangeChromatograms(chromatograms);
                    return doc.ChangeMeasuredResults(measuredResults); 
                });
            });

            doc = WaitForDocumentChange(doc);

            Assert.AreEqual(newName, doc.Settings.MeasuredResults.Chromatograms[3].Name);
            Assert.AreEqual(newName, SkylineWindow.FilesTree.NodesForFileType(FileType.replicates).Nodes[3].Name);

            chromatogramSets = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
            replicateTreeNodes = SkylineWindow.FilesTree.NodesForFileType(FileType.replicates).Nodes;

            Assert.AreEqual(42, chromatogramSets.Count);
            CheckEquivalence(chromatogramSets, replicateTreeNodes);

            // Selecting replicate should update selected index / graphs
            var replicateName = SkylineWindow.FilesTree.NodesForFileType(FileType.replicates).Nodes[4].Name;
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.ActivateReplicate(replicateName);
            });
            WaitForGraphs();
            RunUI(() => Assert.AreEqual(4, SkylineWindow.SelectedResultsIndex));

            //
            // Peptide Libraries
            //
            Assert.AreEqual(2, SkylineWindow.FilesTree.NodesForFileType(FileType.peptide_library)?.Nodes.Count);

            Assert.AreEqual("Rat (NIST) (Rat_plasma2) (Rat_plasma)", SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);
            Assert.AreEqual("Rat (GPM) (Rat_plasma2) (Rat_plasma)", SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[1].Name);

            var peptideLibraryName = SkylineWindow.FilesTree.NodesForFileType(FileType.peptide_library)?.Nodes[0].Name;
            Assert.AreEqual(SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name, peptideLibraryName);
            RunUI(() => SkylineWindow.FilesTreeForm.OpenLibraryExplorer(peptideLibraryName));
            var libraryDlg = WaitForOpenForm<ViewLibraryDlg>();
            RunUI(() =>
            {
                Assert.IsTrue(libraryDlg.HasSelectedLibrary);
                Assert.AreEqual(0, libraryDlg.SelectedIndex);
                libraryDlg.Close();
            });
            WaitForConditionUI(() => !libraryDlg.Visible);

            //
            // Project Files
            //

            // Audit Log
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.ShowAuditLog();
            });
            WaitForConditionUI(() => SkylineWindow.AuditLogForm.Visible);

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() =>
            {
                SkylineWindow.DestroyFilesTreeForm();
            });
        }

        private static void CheckEquivalence(IList<ChromatogramSet> docNodes, TreeNodeCollection treeNodes)
        {
            Assert.IsNotNull(docNodes);
            Assert.IsNotNull(treeNodes);
            Assert.AreEqual(docNodes.Count, treeNodes.Count);

            for (var i = 0; i < 42; i++)
            {
                Assert.AreEqual(docNodes[i].Id, ((FilesTreeNode)treeNodes[i]).Model.Id);
                Assert.AreEqual(docNodes[i].Name, treeNodes[i].Name);
            }
        }
    }

    // Borrowing for now from FindNodeCancelTest. Will consolidate if useful.
    internal static class SrmDocumentHelper
    {
        private const string ALL_AMINO_ACIDS = "ACDEFGHIKLMNPQRSTVWY";

        /// <summary>
        /// List of three character combinations which are used as the Peptide Group names
        /// in the test document and also the first three amino acids of all of the peptides
        /// in that Peptide Group
        /// </summary>
        private static readonly List<string> PEPTIDE_GROUP_NAMES = 
            ALL_AMINO_ACIDS.SelectMany(aa => ALL_AMINO_ACIDS.SelectMany(aa2 => ALL_AMINO_ACIDS.Select(aa3 => "" + aa + aa2 + aa3))).ToList();

        internal static SrmDocument MakeEmptyDocument()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionSettings = srmSettings.TransitionSettings;
            transitionSettings = transitionSettings
                .ChangeInstrument(transitionSettings.Instrument.ChangeMinMz(50))
                .ChangeFilter(transitionSettings.Filter
                    .ChangePeptidePrecursorCharges(new[] { Adduct.SINGLY_PROTONATED })
                    .ChangePeptideProductCharges(new[] { Adduct.SINGLY_PROTONATED })
                    .ChangePeptideIonTypes(new[] { IonType.precursor, IonType.b, IonType.y }));
            srmSettings = srmSettings.ChangeTransitionSettings(transitionSettings);
            return new SrmDocument(srmSettings);
        }

        /// <summary>
        /// Add Peptide Groups to the document so that it has the <paramref name="newProteinCount"/>.
        /// The names of the Peptide Groups come from <see cref="PEPTIDE_GROUP_NAMES"/>.
        /// </summary>
        internal static SrmDocument AddProteinsToDocument(SrmDocument document, int newProteinCount)
        {
            var newProteins = new List<PeptideGroupDocNode>();
            for (int i = document.MoleculeGroupCount; i < newProteinCount; i++)
            {
                newProteins.Add(MakePeptideGroup(document.Settings, PEPTIDE_GROUP_NAMES[i]));
            }

            return (SrmDocument)document.ChangeChildren(document.Children.Concat(newProteins).ToArray());
        }

        /// <summary>
        /// Construct a Peptide Group whose name is <paramref name="prefix"/> and which has
        /// 400 Peptides where the peptide sequences are the prefix plus two amino acids.
        /// </summary>
        static PeptideGroupDocNode MakePeptideGroup(SrmSettings settings, string prefix)
        {
            var peptideDocNodes = new List<PeptideDocNode>();
            foreach (var firstAminoAcid in ALL_AMINO_ACIDS)
            {
                foreach (var secondAminoAcid in ALL_AMINO_ACIDS)
                {
                    var peptide = new Peptide(prefix + firstAminoAcid + secondAminoAcid);
                    var peptideDocNode = new PeptideDocNode(peptide, settings, ExplicitMods.EMPTY, null, null,
                        System.Array.Empty<TransitionGroupDocNode>(), true);
                    // PeptideGroupDocNode.GenerateColors is slow, so it's faster to just tell the peptide what color it should be
                    peptideDocNode = peptideDocNode.ChangeColor(Color.Black);
                    peptideDocNode = peptideDocNode.ChangeSettings(settings, SrmSettingsDiff.ALL);
                    peptideDocNodes.Add(peptideDocNode);
                }
            }
            var peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), prefix, null, peptideDocNodes.ToArray());
            peptideGroupDocNode = peptideGroupDocNode.ChangeProteinMetadata(peptideGroupDocNode.ProteinMetadata.SetWebSearchCompleted());
            return peptideGroupDocNode;
        }
    }
}
