/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests of changing <see cref="SrmSettings"/> on a <see cref="SrmDocument"/>.
    /// </summary>
    [TestClass]
    public class SrmSettingsChangeTest : AbstractUnitTest
    {
        /// <summary>
        /// Test of changing settings that should have no impact on the document
        /// node tree.
        /// </summary>
        [TestMethod]
        public void SettingsChangeNotDoc()
        {
            SrmDocument docFasta = CreateMixedDoc();
            SrmSettings settings = docFasta.Settings;

            // Change declustering potential, collision energy, and retention time
            var regressions = new DeclusterPotentialList();
            regressions.AddDefaults();
            var dpRegress = regressions["ABI"];
            var collisions = new CollisionEnergyList();
            collisions.AddDefaults();
            var ceRegress = collisions["ABI 4000 QTrap"];
            var calc = Settings.Default.RTScoreCalculatorList.GetDefaults().First();
            var rtRegress = new RetentionTimeRegression("Test", calc, 3.5, 10.4, 12.8,
                new MeasuredRetentionTime[0]);

            SrmSettings settings2 = settings.ChangePeptidePrediction(p => p.ChangeRetentionTime(rtRegress)).
                ChangeTransitionPrediction(p => p.ChangeCollisionEnergy(ceRegress).ChangeDeclusteringPotential(dpRegress));

            SrmDocument docFasta2 = docFasta.ChangeSettings(settings2);
            AssertEx.IsDocumentState(docFasta2, docFasta.RevisionIndex + 1, 3, 111, 352);
            Assert.AreSame(docFasta.Children, docFasta2.Children);
            Assert.AreNotEqual(docFasta.Settings, docFasta2.Settings);

            // Change auto-select toggles
            SrmSettings settings3 = settings.ChangePeptideFilter(f => f.ChangeAutoSelect(false)).
                ChangeTransitionFilter(f => f.ChangeAutoSelect(false));

            SrmDocument docFasta3 = docFasta.ChangeSettings(settings3);
            AssertEx.IsDocumentState(docFasta3, docFasta.RevisionIndex + 1, 3, 111, 352);
            Assert.AreSame(docFasta.Children, docFasta3.Children);
            Assert.AreNotEqual(docFasta.Settings, docFasta3.Settings);
        }

        /// <summary>
        /// Tests of settings that should impact the peptide list for a document.
        /// </summary>
        [TestMethod]
        public void SettingsChangePeptides()
        {
            SrmDocument docFasta = CreateMixedDoc();
            const int posList = 0;  // Peptide list is first peptide group.
            SrmSettings settings = docFasta.Settings;

            // Change enzymes, and verify expected peptide changes
            var enzymes = new EnzymeList();
            enzymes.AddDefaults();
            SrmDocument docCnbr = docFasta.ChangeSettings(settings.ChangePeptideSettings(
                p => p.ChangeEnzyme(enzymes["CNBr [M | P]"])));
            foreach (PeptideDocNode nodePeptide in docCnbr.Peptides)
            {
                if (nodePeptide.Peptide.FastaSequence == null)
                    continue;
                Peptide peptide = nodePeptide.Peptide;
                char prev = peptide.PrevAA;
                if (prev != 'M')
                    Assert.Fail("Unexpected preceding cleavage at {0}", prev);
                string seq = peptide.Sequence;
                char last = seq[seq.Length - 1];
                if (last != 'M' && peptide.NextAA != '-')
                    Assert.Fail("Unexpected cleavage at {0}", last);
            }
            Assert.IsTrue(docCnbr.PeptideCount < docFasta.PeptideCount);
            // Peptide list should not have changed.
            Assert.AreSame(docFasta.Children[posList], docCnbr.Children[posList]);

            // Change back to original enzyme, and make sure peptides are restored
            SrmDocument docFasta2 = docCnbr.ChangeSettings(settings);
            Assert.AreEqual(docFasta.RevisionIndex + 2, docFasta2.RevisionIndex);
            Assert.AreEqual(docFasta.PeptideCount, docFasta2.PeptideCount);
            Assert.AreEqual(docFasta.PeptideTransitionCount, docFasta2.PeptideTransitionCount);

            // Allow missed cleavages, and verify changes
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideSettings(
                p => p.ChangeDigestSettings(new DigestSettings(1, false))));
            Assert.IsTrue(docFasta.PeptideCount < docFasta2.PeptideCount);
// TODO: Make minimum transition count work immediately
//            Assert.IsTrue((docFasta2.PeptideCount - docFasta.PeptideCount) * 3 <
//                docFasta2.TransitionCount - docFasta.TransitionCount);
            int missedCleavageCount = 0;
            var dictOrig = docFasta.Peptides.ToDictionary(node => node.Peptide);
            foreach (PeptideDocNode nodePeptide in docFasta2.Peptides)
            {
                // Make sure all zero-cleavage peptides are the same as the old document
                int missed = nodePeptide.Peptide.MissedCleavages;
                if (missed == 0)
                    Assert.AreEqual(nodePeptide, dictOrig[nodePeptide.Peptide]);

                // Count the number of new missed cleavages
                missedCleavageCount += nodePeptide.Peptide.MissedCleavages;
            }
            Assert.AreEqual(docFasta2.PeptideCount - docFasta.PeptideCount, missedCleavageCount);
            // Peptide list should not have changed.
            Assert.AreSame(docFasta.Children[posList], docFasta2.Children[posList]);

            // Increase minimum peptide length
            const int minNew = 12;
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(f => f.ChangeMinPeptideLength(minNew)));
            CheckPeptides(docFasta, docFasta2, node => node.Peptide.Length >= minNew);
            Assert.AreSame(docFasta.Children[posList], docFasta2.Children[posList]);

            // Decrease maximum peptide length
            const int maxNew = 18;
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(f => f.ChangeMaxPeptideLength(maxNew)));
            CheckPeptides(docFasta, docFasta2, node => node.Peptide.Length <= maxNew);
            Assert.AreSame(docFasta.Children[posList], docFasta2.Children[posList]);

            // Increase n-term AA exclustion
            const int ntermStart = 50;
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeExcludeNTermAAs(ntermStart)));
            CheckPeptides(docFasta, docFasta2, node =>
                node.Peptide.Begin.HasValue && node.Peptide.Begin.Value >= ntermStart);
            Assert.AreSame(docFasta.Children[posList], docFasta2.Children[posList]);

            // Use ragged end exclusion
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideSettings(
                p => p.ChangeDigestSettings(new DigestSettings(0, true))));
            CheckPeptides(docFasta, docFasta2, IsNotRagged);
            Assert.AreSame(docFasta.Children[posList], docFasta2.Children[posList]);

            // Check custom exclusions
            var exclusions = new PeptideExcludeList();
            exclusions.AddDefaults();

            // Exclude Cys
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeExclusions(new[] { exclusions["Cys"] })));
            CheckPeptides(docFasta, docFasta2, node => node.Peptide.Sequence.IndexOf('C') == -1);
            Assert.AreSame(docFasta.Children[posList], docFasta2.Children[posList]);
            
            // Exclude Met
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeExclusions(new[] { exclusions["Met"] })));
            CheckPeptides(docFasta, docFasta2, node => node.Peptide.Sequence.IndexOf('M') == -1);

            // Exclude Hys
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeExclusions(new[] { exclusions["His"] })));
            CheckPeptides(docFasta, docFasta2, node => node.Peptide.Sequence.IndexOf('H') == -1);

            // Exclude NXS/NXT
            Regex regexNx = new Regex("N.[ST]");
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeExclusions(new[] { exclusions["NXT/NXS"] })));
            CheckPeptides(docFasta, docFasta2, node => !regexNx.Match(node.Peptide.Sequence).Success);

            // Exclude RP/KP
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeExclusions(new[] { exclusions["RP/KP"] })));
            CheckPeptides(docFasta, docFasta2, node => node.Peptide.Sequence.IndexOf("RP", StringComparison.Ordinal) == -1 &&
                                                       node.Peptide.Sequence.IndexOf("KP", StringComparison.Ordinal) == -1);

            // Custom exclude ^Q*K$
            var excludeCustom = new PeptideExcludeRegex("Custom", "^Q.*K$");
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeExclusions(new[] { excludeCustom })));
            CheckPeptides(docFasta, docFasta2, node =>
                (!node.Peptide.Sequence.StartsWith("Q") || !node.Peptide.Sequence.EndsWith("K")));

            // Auto-picking off should keep any changes from occurring
            docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeAutoSelect(false).ChangeExclusions(new[] { exclusions["Cys"], exclusions["Met"], excludeCustom })));
            Assert.AreSame(docFasta.Children, docFasta2.Children);

            // Removing restriction with auto-picking off should change anything
            settings = docFasta2.Settings;
            SrmDocument docFasta3 = docFasta2.ChangeSettings(settings.ChangePeptideFilter(
                f => f.ChangeExclusions(new PeptideExcludeRegex[0])));
            Assert.AreSame(docFasta2.Children, docFasta3.Children);
        }

        private static bool IsNotRagged(PeptideDocNode nodePeptide)
        {
            Peptide peptide = nodePeptide.Peptide;
            FastaSequence fastaSeq = peptide.FastaSequence;
            if (fastaSeq == null)
                return true;

            int begin = peptide.Begin ?? 0;
            return ((begin < 2 || "KR".IndexOf(fastaSeq.Sequence[begin - 2]) == -1) &&
                    "KR".IndexOf(peptide.NextAA) == -1);
        }

        private static void CheckPeptides(SrmDocument docOriginal, SrmDocument docNew, Func<PeptideDocNode, bool> accept)
        {
            // The peptide count must decrease, of this check will not work.
            Assert.IsTrue(docOriginal.PeptideCount > docNew.PeptideCount);

            // Make a list of all the peptides in the original document that meet the new criteria
            var listNodes = new List<PeptideDocNode>();
            foreach (PeptideDocNode nodePeptide in docOriginal.Peptides)
            {
                // All peptide list peptides are accepted
                if (nodePeptide.Peptide.FastaSequence == null || accept(nodePeptide))
                    listNodes.Add(nodePeptide);
            }

            // Make sure the new document has only nodes that are reference equal to those
            // that met the criteria in the previous set.
            var existingNodes = docNew.Peptides.ToArray();
            for (int i = 0; i < Math.Min(listNodes.Count, existingNodes.Length); i++)
            {
                if (!ReferenceEquals(listNodes[i], existingNodes[i]))
                    Assert.Fail("The peptide {0} does not match {1}.", listNodes[i], existingNodes[i]);
            }
            // This should result in the same number as the new document.
            Assert.AreEqual(docNew.PeptideCount, listNodes.Count);
        }

        /// <summary>
        /// Tests of settings that should impact the transition group list for a document.
        /// </summary>
        [TestMethod]
        public void SettingsChangeTranGroups()
        {
            SrmDocument docFasta = CreateMixedDoc();
            SrmSettings settings = docFasta.Settings;

            // Add heavy mod
            SrmDocument docFasta2 = docFasta.ChangeSettings(settings.ChangePeptideModifications(
                m => m.ChangeHeavyModifications( new[] { new StaticMod("N-Terminal K", "K", ModTerminus.C, "H7", LabelAtoms.None, null, null) })));
            CheckNTerminalKGroups(docFasta2);
            Assert.AreEqual(docFasta.PeptideCount, docFasta2.PeptideCount);

            // Add multiple charges with heavy mod
            var newCharges = new[] {2, 3, 4};
            settings = docFasta2.Settings;
            SrmDocument docFasta3 = docFasta2.ChangeSettings(settings.ChangeTransitionFilter(
                f => f.ChangePrecursorCharges(newCharges)));
            CheckNTerminalKGroups(docFasta3);
            Assert.AreEqual(docFasta.PeptideCount, docFasta3.PeptideCount);

            // Use charge that will cause filtering on instrument maximum m/z
            docFasta2 = docFasta.ChangeSettings(settings.ChangeTransitionFilter(
                f => f.ChangePrecursorCharges(new[] {1})));
            Assert.IsTrue(docFasta.PeptideTransitionGroupCount < docFasta2.PeptideTransitionGroupCount);
            Assert.AreEqual(docFasta.PeptideCount, docFasta2.PeptideCount);
        }

        private static void CheckNTerminalKGroups(SrmDocument document)
        {
            var newCharges = document.Settings.TransitionSettings.Filter.PrecursorCharges;
            foreach (PeptideDocNode nodePep in document.Peptides)
            {
                if (nodePep.Peptide.Sequence.Last() != 'K')
                {
                    Assert.AreEqual(newCharges.Count, nodePep.TransitionGroupCount);
                }
                else
                {
                    Assert.AreEqual(newCharges.Count * 2, nodePep.TransitionGroupCount);
                    for (int i = 0; i < newCharges.Count; i++)
                    {
                        // Check for expected heavy group
                        TransitionGroupDocNode nodeGroup1 = (TransitionGroupDocNode)nodePep.Children[i * 2];
                        TransitionGroupDocNode nodeGroup2 = (TransitionGroupDocNode)nodePep.Children[i * 2 + 1];
                        Assert.AreEqual(IsotopeLabelType.light, nodeGroup1.TransitionGroup.LabelType);
                        Assert.AreEqual(IsotopeLabelType.heavy, nodeGroup2.TransitionGroup.LabelType);
                        int chargeExpect = newCharges[i];
                        Assert.AreEqual(chargeExpect, nodeGroup1.TransitionGroup.PrecursorCharge);
                        Assert.AreEqual(chargeExpect, nodeGroup2.TransitionGroup.PrecursorCharge);
                        // Make sure the expected heavy group is heavier
                        Assert.IsTrue(nodeGroup1.PrecursorMz + 7.0 / chargeExpect < nodeGroup2.PrecursorMz);
                    }
                }
            }            
        }

        [TestMethod]
        public void SettingsChangeTrans()
        {
            SrmDocument docFasta = CreateMixedDoc();
            SrmSettings settings = docFasta.Settings;
            SrmDocument docFastaNoP = docFasta.ChangeSettings(settings.ChangeTransitionFilter(
                f => f.ChangeMeasuredIons(new MeasuredIon[0])));

            // Fixed start and end positions
            SrmDocument docFasta2 = CheckTranstions(docFastaNoP, "ion 1", "last ion", 1);
            CheckTranstions(docFasta2, "ion 2", "last ion - 1", 3);
            docFasta2 = CheckTranstions(docFastaNoP, "ion 3", "last ion - 2", 5);
            CheckTranstions(docFasta2, "ion 4", "last ion - 3", 7);

            // Check ion types including precursor
            var docPrec = docFasta2.ChangeSettings(docFasta2.Settings.ChangeTransitionFilter(f =>
                f.ChangeIonTypes(new[] { IonType.y, IonType.precursor })));
            Assert.AreEqual(docFasta2.PeptideTransitionCount + docFasta2.PeptideTransitionGroupCount, docPrec.PeptideTransitionCount);
            docPrec = docFasta2.ChangeSettings(docFasta2.Settings.ChangeTransitionFilter(f =>
                f.ChangeIonTypes(new[] { IonType.precursor })));
            Assert.AreEqual(docFasta2.PeptideTransitionGroupCount, docPrec.PeptideTransitionCount);
            AssertEx.Serializable(docPrec, AssertEx.DocumentCloned);

            // TODO: Finish this test
        }

        private static SrmDocument CheckTranstions(SrmDocument document, string startName, string endName, int ionDiff)
        {
            SrmSettings settings = document.Settings;
            SrmDocument docNew = document.ChangeSettings(settings.ChangeTransitionFilter(
                    f => f.ChangeFragmentRangeFirstName(startName). ChangeFragmentRangeLastName(endName)).
                ChangeTransitionInstrument(i => i.ChangeMaxMz(5000)));

            // length-n ions
            foreach (PeptideDocNode nodePeptide in docNew.Peptides)
            {
                Assert.AreEqual(Math.Max(0, nodePeptide.Peptide.Sequence.Length - ionDiff),
                    nodePeptide.TransitionCount);
            }

            return docNew;
        }

        /// <summary>
        /// Tests of settings that change precursor and fragment mass values.
        /// </summary>
        [TestMethod]
        public void SettingsChangeMassCalcProps()
        {
            SrmDocument docFasta = CreateMixedDoc();
            SrmSettings settings = docFasta.Settings;

            // Use average precursor masses
            SrmDocument docFasta2 = docFasta.ChangeSettings(settings.ChangeTransitionPrediction(
                p => p.ChangePrecursorMassType(MassType.Average)));
            // Average masses should be heavier that monoisotipic, and transitions should be unchanged
            CheckMasses(docFasta, docFasta2, (before, after) => Assert.IsTrue(before < after), Assert.AreEqual);

            // Use average fragment masses
            settings = docFasta2.Settings.ChangeTransitionInstrument(instrument => instrument.ChangeMaxMz(1501)); // Keep all the new heavy transitions
            SrmDocument docFasta3 = docFasta2.ChangeSettings(settings.ChangeTransitionPrediction(
                p => p.ChangeFragmentMassType(MassType.Average)));
            // Precursor masses should not have changed, and transitions should be heavier
            CheckMasses(docFasta2, docFasta3, Assert.AreEqual, (before, after) => Assert.IsTrue(before < after));

            // Change both back to all monoisotopic
            settings = docFasta3.Settings;
            SrmDocument docFasta4 = docFasta3.ChangeSettings(settings.ChangeTransitionPrediction(
                p => p.ChangePrecursorMassType(MassType.Monoisotopic).ChangeFragmentMassType(MassType.Monoisotopic)));
            // This should return the masses to their original values
            CheckMasses(docFasta, docFasta4, Assert.AreEqual, Assert.AreEqual);

            // TODO: Static modifications
        }

        private delegate void CompareMasses(double before, double after);

        private static void CheckMasses(SrmDocument docBefore, SrmDocument docAfter,
            CompareMasses precursorCompare, CompareMasses fragmentCompare)
        {
            // Check transition groups
            var tranGroupsBefore = docBefore.MoleculeTransitionGroups.ToArray();
            var tranGroupsAfter = docAfter.MoleculeTransitionGroups.ToArray();
            Assert.AreEqual(tranGroupsBefore.Length, tranGroupsAfter.Length);
            for (int i = 0; i < tranGroupsBefore.Length; i++)
                precursorCompare(tranGroupsBefore[i].PrecursorMz, tranGroupsAfter[i].PrecursorMz); // 60?

            // Check transitions
            var transBefore = docBefore.MoleculeTransitions.ToArray();
            var transAfter = docAfter.MoleculeTransitions.ToArray();
            Assert.AreEqual(transBefore.Length, transAfter.Length);
            for (int i = 0; i < transBefore.Length; i++)
                fragmentCompare(transBefore[i].Mz, transAfter[i].Mz);
        }

        private static SrmDocument CreateMixedDoc()
        {
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault0_6());
            IdentityPath path;
            // Add fasta sequences
            SrmDocument docFasta = document.ImportFasta(new StringReader(ExampleText.TEXT_FASTA_YEAST),
                false, IdentityPath.ROOT, out path);
            AssertEx.IsDocumentState(docFasta, 1, 2, 98, 311);
            // Insert peptide list at beginnning
            SrmDocument docMixed = docFasta.ImportFasta(new StringReader(SrmDocEditTest.TEXT_BOVINE_PEPTIDES1),
                true, docFasta.GetPathTo(0), out path);
            AssertEx.IsDocumentState(docMixed, 2, 3, 111, 352);
            return docMixed;            
        }
    }
}