/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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


using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Document in this test has three groups - just peptide fragments, same fragments with reporters, reporters only
    /// We expect the first two groups to have the same RT since reporter ions should not factor into RT calculation
    /// The third group, being reporters only, has an RT base on just the reporter ions and that will be different
    /// </summary>
    [TestClass]
    public class NonScoringTransitionsPerfTest : AbstractFunctionalTest
    {

        [TestMethod]
        public void TestNonScoringTransitions()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfNonScoringTransitionsTest.zip");
            TestFilesPersistent = new[] { mzmlFile }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            RunFunctionalTest();
        }

        static string mzmlFile = "230815_P1_YeastTMTProAcc_ES904_Neo_60min_300nlmin_iw1p0_sr67_tar1e4_MS2HCD45_Large_0.mz5";

        protected override void DoTest()
        {
            Program.ExtraRawFileSearchFolder = TestFilesDir.PersistentFilesDir;
            TestSmallDocument();
            TestMediumDocument();
        }

        private void TestSmallDocument()
        {
            string skyFile = TestFilesDir.GetTestPath("NonScoringTransitionsTest.sky");
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            var document = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(document, null, 3, 3, 3, 44);
            ImportResultsFile(TestFilesDir.GetTestPath(mzmlFile));

            // Document has three groups - just peptide fragments, same fragments with reporters, reporters only
            // We expect the first two groups to have the same RT since reporter ions should not factor into RT calculation
            // The third group, being reporters only, should have no RT value at all

            var molecules = SkylineWindow.Document.PeptideGroups.Select(pg => pg.Molecules.First()).ToArray();
            var transitionChromInfo0 = molecules[0].Results[0].First();
            var transitionChromInfo1 = molecules[1].Results[0].First();
            var transitionChromInfo2 = molecules[2].Results[0].First();
            Assert.AreEqual(transitionChromInfo0.RetentionTime, transitionChromInfo1.RetentionTime);
            Assert.AreEqual(29.879, transitionChromInfo0.RetentionTime.Value, .01);
            Assert.IsNull(transitionChromInfo2.RetentionTime); // No RT - all transitions are non-scoring

            foreach (var group in SkylineWindow.Document.PeptideGroups)
            {
                var molecule = group.Molecules.First();
                foreach (var transitionDocNode in molecule.TransitionGroups.First().Transitions)
                {
                    var isReporter = transitionDocNode.Transition.FragmentIonName.Contains(@"TMT");
                    AssertEx.AreEqual(isReporter, !transitionDocNode.ParticipatesInScoring);
                }
            }
        }
        private void TestMediumDocument()
        {
            string skyFile = TestFilesDir.GetTestPath("medium.sky");
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            var document = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(document, null, 1, 2, 2, 43);
            ImportResultsFile(TestFilesDir.GetTestPath(mzmlFile));

            var molecules = SkylineWindow.Document.Molecules.ToArray();
            var transitionChromInfo0 = molecules[0].Results[0].First();
            var transitionChromInfo1 = molecules[1].Results[0].First();
            Assert.AreEqual(24.2548, transitionChromInfo0.RetentionTime.Value, .01);
            Assert.AreEqual(26.3618, transitionChromInfo1.RetentionTime.Value, .01);

            foreach (var molecule in molecules) 
            { 
                foreach (var transitionDocNode in molecule.TransitionGroups.First().Transitions)
                {
                    var isReporter = transitionDocNode.Transition.FragmentIonName.Contains(@"TMT");
                    AssertEx.AreEqual(isReporter, !transitionDocNode.ParticipatesInScoring);
                }
            }
        }
    }
}
