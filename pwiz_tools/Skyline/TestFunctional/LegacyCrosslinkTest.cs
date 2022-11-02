/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LegacyCrosslinkTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLegacyCrosslinks()
        {
            TestFilesZip = @"TestFunctional\LegacyCrosslinkTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            foreach (var filename in new[]
            {
                "CrosslinkNeutralLossTest.sky",
                "CrosslinkNeutralLossTest_compact.sky"
            })
            {
                RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath(filename)));
                var transitionGroups = SkylineWindow.Document.MoleculeTransitionGroups.ToList();
                Assert.AreEqual(2, transitionGroups.Count, filename);
                foreach (var transitionGroup in transitionGroups)
                {
                    Assert.AreEqual(921.051471, transitionGroup.PrecursorMz, 1E-6, filename);
                    Assert.AreEqual(921.051471, transitionGroup.Transitions.First().Mz, 1E-6, filename);
                }

                var expectedTuples = new List<Tuple<IonChain, int, double, double>>
                {
                    Tuple.Create(IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.B(11)), 2, 64.0, 1156.9502)
                };
                foreach (var tuple in expectedTuples)
                {
                    VerifyTransitionMz(SkylineWindow.Document, tuple);
                }
            }

            foreach (var filename in new[]
            {
                "CrosslinkChromatogramTest.sky",
                "CrosslinkChromatogramTest_compact.sky"
            })
            {
                RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath(filename)));
                var expectedMzs = new[]
                {
                    1099.61261, 650.285333, 673.852852, 681.850309, 950.964056, 1067.051717, 578.329673, 728.364972,
                    613.312208, 855.459738, 878.970667, 1292.583632, 469.2531, 780.917038, 1049.942175, 714.799328,
                    569.76182, 1250.686504, 828.920643, 501.758746, 894.467289, 541.279846, 748.879932, 756.87739,
                    506.774731, 487.281857, 1222.140055, 791.412257, 1127.081143, 569.27476, 994.490378, 812.424499
                };
                var actualMzs = SkylineWindow.Document.MoleculeTransitions.Select(t => t.Mz.RawValue).ToList();
                Assert.AreEqual(expectedMzs.Length, actualMzs.Count);
                for (int i = 0; i < expectedMzs.Length; i++)
                {
                    Assert.AreEqual(expectedMzs[i], actualMzs[i], 1e-4, filename);
                }
            }
        }

        private void VerifyTransitionMz(SrmDocument document, Tuple<IonChain, int, double, double> tuple)
        {
            var ionChain = tuple.Item1;
            var charge = tuple.Item2;
            var neutralLoss = tuple.Item3;
            var expectedMz = tuple.Item4;
            var transitions = document.MoleculeTransitions.ToList();
            for (int iTransition = 0; iTransition < transitions.Count; iTransition++)
            {
                var transitionDocNode = transitions[iTransition];
                if (!Adduct.FromChargeProtonated(charge).Equals(transitionDocNode.Transition.Adduct))
                {
                    continue;
                }
                if (!Equals(transitionDocNode.ComplexFragmentIon.NeutralFragmentIon.IonChain, ionChain))
                {
                    continue;
                }

                if (Math.Abs(neutralLoss - transitionDocNode.LostMass) > 1)
                {
                    continue;
                }

                Assert.AreEqual(expectedMz, transitionDocNode.Mz, 1e-4, "{0}", tuple);
                return;
            }
            Assert.Fail("Unable to find transition {0}", tuple);
        }
    }
}
