/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
    /// Summary description for NeutralLossTest
    /// </summary>
    [TestClass]
    public class LossesTest : AbstractUnitTest
    {
        private const string TEXT_FASTA_YEAST_7 =
            ">YAL007C ERP2 SGDID:S000000005, Chr I from 138347-137700, reverse complement, Verified ORF, \"Protein that forms a heterotrimeric complex with Erp1p, Emp24p, and Erv25p; member, along with Emp24p and Erv25p, of the p24 family involved in ER to Golgi transport and localized to COPII-coated vesicles\"\n" +
            "MIKSTIALPSFFIVLILALVNSVAASSSYAPVAISLPAFSKECLYYDMVTEDDSLAVGYQ\n" +
            "VLTGGNFEIDFDITAPDGSVITSEKQKKYSDFLLKSFGVGKYTFCFSNNYGTALKKVEIT\n" +
            "LEKEKTLTDEHEADVNNDDIIANNAVEEIDRNLNKITKTLNYLRAREWRNMSTVNSTESR\n" +
            "LTWLSILIIIIIAVISIAQVLLIQFLFTGRQKNYV*\n";

        [TestMethod]
        public void NeutralLossListTest()
        {
            var phosphoLossMod = new StaticMod("Phospho Loss", "S, T, Y", null, false, "HPO3",
                LabelAtoms.None, RelativeRT.Matching, null, null, new[] { new FragmentLoss("H3PO4"), });

            ValidateNeutralLossTransitions(phosphoLossMod, 2, 11, true);
        }

        /// <summary>
        /// Test case of multiple losses with the same m/z to make sure they all get represented
        /// </summary>
        [TestMethod]
        public void ChargedLossListTest()
        {
            var neutralLoss = new FragmentLoss("H3PO4");
            var phosphoMultiLossMod = new StaticMod("Phospho Loss", "S, T, Y", null, false, "HPO3",
                LabelAtoms.None, RelativeRT.Matching, null, null,
                new[] { neutralLoss, neutralLoss.ChangeCharge(2), neutralLoss.ChangeCharge(1) });

            // Only 1 extra precursor (charge 1) is expected from the neutral loss only case
            ValidateNeutralLossTransitions(phosphoMultiLossMod, 2, 12, false);
            // Now 2 extra precursors since both charged losses apply to a charge 3 precursor
            ValidateNeutralLossTransitions(phosphoMultiLossMod, 3, 13, false);

            // Repeat with the losses reversed which should not matter
            phosphoMultiLossMod = phosphoMultiLossMod.ChangeLosses(phosphoMultiLossMod.Losses.Reverse().ToArray());
            ValidateNeutralLossTransitions(phosphoMultiLossMod, 2, 12, false);
            ValidateNeutralLossTransitions(phosphoMultiLossMod, 3, 13, false);

            // Also try charged losses only
            phosphoMultiLossMod = phosphoMultiLossMod.ChangeLosses(
                new[] { neutralLoss.ChangeCharge(1), neutralLoss.ChangeCharge(2) });
            ValidateNeutralLossTransitions(phosphoMultiLossMod, 2, 11, false);
            ValidateNeutralLossTransitions(phosphoMultiLossMod, 3, 12, false);
        }

        private static void ValidateNeutralLossTransitions(StaticMod lossMod, int precursorCharge, int expectedLosses, bool roundtrip)
        {
            var allowedIonTypes = new[] { IonType.y, IonType.precursor };
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault()
                .ChangePeptideModifications(mods =>
                    mods.ChangeStaticModifications(new List<StaticMod>(mods.StaticModifications) { lossMod }))
                .ChangeTransitionFilter(tf =>
                    tf.ChangeFragmentRangeAll()
                        .ChangePeptideIonTypes(allowedIonTypes)
                        .ChangePeptidePrecursorCharges(new[] {Adduct.FromChargeProtonated(precursorCharge)}))
                .ChangeTransitionInstrument(ti => ti.ChangeMaxMz(2000)));   // Allow precursor charge loss
            IdentityPath path = IdentityPath.ROOT;
            SrmDocument docFasta = document.ImportFasta(new StringReader(TEXT_FASTA_YEAST_7), false, path, out path);

            Assert.AreEqual(0, GetLossCount(docFasta, 1));
            const int expectedTransitions = 25;
            AssertEx.IsDocumentState(docFasta, null, 1, 2, expectedTransitions);

            // Insert losses into the first transition group
            var pathPeptide = docFasta.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            var nodePep = (PeptideDocNode)docFasta.FindNode(pathPeptide);
            var nodeGroup = (TransitionGroupDocNode)nodePep.Children[0];
            var listChildren = new List<DocNode>(nodeGroup.Children);
            foreach (var nodeTran in nodeGroup.GetTransitions(docFasta.Settings,
                         nodePep.ExplicitMods, nodeGroup.PrecursorMz, null, null, null, false))
            {
                // Make sure all of the available transitions preserve proteomics adducts
                var adduct = nodeTran.Transition.Adduct;
                Assert.IsTrue(adduct.IsProteomic, "Non-proteomics adduct found " + adduct);

                // Only interested in adding loss ions
                if (!nodeTran.HasLoss)
                    continue;
                // Only interested in ion types and charges allowed by the settings
                if (!allowedIonTypes.Contains(nodeTran.Transition.IonType) ||
                    // The precursors may change charge state based on charged losses
                    (nodeTran.Transition.IonType == IonType.y && nodeTran.Transition.Charge != 1))
                    continue;

                var tran = nodeTran.Transition;
                int matchIndex = listChildren.IndexOf(node => IsMatch(tran, node));
                Assert.IsFalse(matchIndex == -1, "No match found for " + tran);

                while (matchIndex < listChildren.Count && IsMatch(tran, listChildren[matchIndex]))
                {
                    matchIndex++;
                }

                listChildren.Insert(matchIndex, nodeTran);
            }

            var docLosses = (SrmDocument)docFasta.ReplaceChild(pathPeptide,
                nodeGroup.ChangeChildren(listChildren));
            AssertEx.IsDocumentState(docLosses, null, 1, 2, expectedTransitions + expectedLosses);

            int lossCount = GetLossCount(docLosses, 1);
            Assert.AreEqual(expectedLosses, lossCount);
            if (roundtrip)
            {
                var docRoundTripped = AssertEx.RoundTripTransitionList(new ThermoMassListExporter(docLosses));
                Assert.AreEqual(lossCount, GetLossCount(docRoundTripped, 1));
                docRoundTripped = AssertEx.RoundTripTransitionList(new AgilentMassListExporter(docLosses));
                Assert.AreEqual(lossCount, GetLossCount(docRoundTripped, 1));
            }
        }

        private static bool IsMatch(Transition tran, DocNode node)
        {
            // Allow charge state changes in precursors to allow for charged losses
            var existingTran = ((TransitionDocNode)node).Transition;
            if (tran.IonType != existingTran.IonType || tran.Ordinal != existingTran.Ordinal)
                return false;
            if (tran.IonType != IonType.precursor && tran.Charge != existingTran.Charge)
                return false;
            return true;
        }

        private static int GetLossCount(SrmDocument document, int minLosses)
        {
            return document.PeptideTransitions.Count(nodeTran => nodeTran.HasLoss && nodeTran.Losses.Losses.Count >= minLosses);
        }
    }
}