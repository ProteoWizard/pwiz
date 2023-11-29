/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FindNodeCancelTest : AbstractFunctionalTest
    {
        const string ALL_AMINO_ACIDS = "ACDEFGHIKLMNPQRSTVWY";
        /// <summary>
        /// List of three character combinations which are used as the Peptide Group names
        /// in the test document and also the first three amino acids of all of the peptides
        /// in that Peptide Group
        /// </summary>
        private List<string> _peptideGroupNames = ALL_AMINO_ACIDS.SelectMany(aa =>
            ALL_AMINO_ACIDS.SelectMany(aa2 => ALL_AMINO_ACIDS.Select(aa3 => "" + aa + aa2 + aa3))).ToList();

        [TestMethod]
        public void TestFindNodeCancel()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var emptyDocument = MakeEmptyDocument();
            SwitchDocument(AddProteinsToDocument(emptyDocument, 50));
            var findNodeDlg = ShowDialog<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg);
            Assert.IsNotNull(findNodeDlg);
            GoToFirstProtein();
            Assert.AreEqual(null, GetSelectedPeptideSequence());
            var firstPeptideToSearchFor = SkylineWindow.Document.MoleculeGroups.Last().Molecules.Last().Peptide.Sequence;
            RunUI(() =>
            {
                findNodeDlg.SearchString = firstPeptideToSearchFor;
                findNodeDlg.FindNext();
            });
            Assert.AreEqual(firstPeptideToSearchFor, GetSelectedPeptideSequence());
            while (true)
            {
                GoToFirstProtein();
                Assert.AreEqual(null, GetSelectedPeptideSequence());
                var lastPeptide = SkylineWindow.Document.MoleculeGroups.Last().Molecules.Last().Peptide.Sequence;
                RunUI(()=>findNodeDlg.SearchString = lastPeptide);
                using (new LongWaitDialogCanceler())
                {
                    var startTime = DateTime.UtcNow;
                    RunUI(() =>
                    {
                        findNodeDlg.FindNext();
                    });
                    var elapsedTime = DateTime.UtcNow - startTime;
                    Assert.IsTrue(elapsedTime.TotalSeconds < 60, "Searching for {0} took {1} which is longer than 60 seconds", lastPeptide, elapsedTime);
                    var selectedPeptideSequence = GetSelectedPeptideSequence();
                    if (null == selectedPeptideSequence)
                    {
                        break;
                    }
                    Assert.AreEqual(lastPeptide, selectedPeptideSequence);
                }

                var document = SkylineWindow.Document;
                int moleculeGroupCount = document.MoleculeGroupCount;
                if (moleculeGroupCount >= _peptideGroupNames.Count)
                {
                    Assert.Fail("Unable to cancel Find Dialog even though there are {0} proteins in the document", moleculeGroupCount);
                }

                document = AddProteinsToDocument(document, Math.Min(moleculeGroupCount * 4, _peptideGroupNames.Count));
                SwitchDocument(document);
            }
            OkDialog(findNodeDlg, findNodeDlg.Close);
            SwitchDocument(emptyDocument);
        }

        private void SwitchDocument(SrmDocument document)
        {
            RunUI(()=>
            {
                SkylineWindow.CollapseProteins();
                SkylineWindow.SwitchDocument(document, null);
            });
        }

        /// <summary>
        /// Add Peptide Groups to the document so that it has the <paramref name="newProteinCount"/>.
        /// The names of the Peptide Groups come from <see cref="_peptideGroupNames"/>.
        /// </summary>
        private SrmDocument AddProteinsToDocument(SrmDocument document, int newProteinCount)
        {
            var newProteins = new List<PeptideGroupDocNode>();
            for (int i = document.MoleculeGroupCount; i < newProteinCount; i++)
            {
                newProteins.Add(MakePeptideGroup(document.Settings, _peptideGroupNames[i]));
            }

            return (SrmDocument)document.ChangeChildren(document.Children.Concat(newProteins).ToArray());
        }

        /// <summary>
        /// Construct a Peptide Group whose name is <paramref name="prefix"/> and which has
        /// 400 Peptides where the peptide sequences are the prefix plus two amino acids.
        /// </summary>
        private PeptideGroupDocNode MakePeptideGroup(SrmSettings settings, string prefix)
        {
            var peptideDocNodes = new List<PeptideDocNode>();
            foreach (var firstAminoAcid in ALL_AMINO_ACIDS)
            {
                foreach (var secondAminoAcid in ALL_AMINO_ACIDS)
                {
                    var peptide = new Peptide(prefix + firstAminoAcid + secondAminoAcid);
                    var peptideDocNode = new PeptideDocNode(peptide, settings, ExplicitMods.EMPTY, null, null,
                        Array.Empty<TransitionGroupDocNode>(), true);
                    // PeptideGroupDocNode.GenerateColors is slow, so it's faster to just tell the peptide what
                    // color it should be
                    peptideDocNode = peptideDocNode.ChangeColor(Color.Black);
                    peptideDocNode = peptideDocNode.ChangeSettings(settings, SrmSettingsDiff.ALL);
                    peptideDocNodes.Add(peptideDocNode);
                }
            }
            var peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), prefix, null, peptideDocNodes.ToArray());
            peptideGroupDocNode =
                peptideGroupDocNode.ChangeProteinMetadata(peptideGroupDocNode.ProteinMetadata.SetWebSearchCompleted());
            return peptideGroupDocNode;
        }

        private SrmDocument MakeEmptyDocument()
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

        private string GetSelectedPeptideSequence()
        {
            string sequence = null;
            RunUI(() =>
            {
                var peptideTreeNode = SkylineWindow.SequenceTree.GetNodeOfType<PeptideTreeNode>();
                if (peptideTreeNode != null)
                {
                    sequence = peptideTreeNode.DocNode.Peptide.Sequence;
                }
            });
            return sequence;
        }

        private void GoToFirstProtein()
        {
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 0);
            });
        }
    }
}
