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

namespace pwiz.SkylineTestA
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
            TestSmallMolecules = false; // No concept of neutral loss for small molecules

            var phosphoLossMod = new StaticMod("Phospho Loss", "S, T, Y", null, false, "HPO3",
                LabelAtoms.None, RelativeRT.Matching, null, null, new[] { new FragmentLoss("H3PO4"), });

            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault().ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(new List<StaticMod>(mods.StaticModifications) { phosphoLossMod })));
            IdentityPath path = IdentityPath.ROOT;
            SrmDocument docFasta = document.ImportFasta(new StringReader(TEXT_FASTA_YEAST_7), false, path, out path);

            Assert.AreEqual(0, GetLossCount(docFasta, 1));

            // Insert losses into the first transition group
            var pathPeptide = docFasta.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            var nodePep = (PeptideDocNode) docFasta.FindNode(pathPeptide);
            var nodeGroup = (TransitionGroupDocNode) nodePep.Children[0];
            var listChildren = new List<DocNode>(nodeGroup.Children);
            foreach (var nodeTran in nodeGroup.GetTransitions(docFasta.Settings, 
                nodePep.ExplicitMods, nodeGroup.PrecursorMz, null, null, null, false))
            {
                if (!nodeTran.HasLoss)
                    continue;

                var tran = nodeTran.Transition;
                int matchIndex = listChildren.IndexOf(node =>
                    Equals(tran, ((TransitionDocNode)node).Transition));
                if (matchIndex == -1)
                    continue;

                while (matchIndex < listChildren.Count &&
                    Equals(tran, ((TransitionDocNode)listChildren[matchIndex]).Transition))
                {
                    matchIndex++;
                }
                listChildren.Insert(matchIndex, nodeTran);
            }

            var docLosses = (SrmDocument) docFasta.ReplaceChild(pathPeptide,
                nodeGroup.ChangeChildren(listChildren));

            int lossCount = GetLossCount(docLosses, 1);
            Assert.IsTrue(lossCount > 0);
            var docRoundTripped = AssertEx.RoundTripTransitionList(new ThermoMassListExporter(docLosses));
            Assert.AreEqual(lossCount, GetLossCount(docRoundTripped, 1));
            docRoundTripped = AssertEx.RoundTripTransitionList(new AgilentMassListExporter(docLosses));
            Assert.AreEqual(lossCount, GetLossCount(docRoundTripped, 1));
        }

        private static int GetLossCount(SrmDocument document, int minLosses)
        {
            return document.PeptideTransitions.Count(nodeTran => nodeTran.HasLoss && nodeTran.Losses.Losses.Count >= minLosses);
        }
    }
}