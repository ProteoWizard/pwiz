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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests importing results where one of the SRM chromatograms has zero points in it
    /// </summary>
    [TestClass]
    public class ZeroLengthSrmChromatogramTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestZeroLengthSrmChromatogram()
        {
            TestFilesZip = @"TestFunctional\ZeroLengthSrmChromatogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ZeroLengthSrmChromatogramTest.sky"));
            });
            ImportResultsFile(TestFilesDir.GetTestPath("ZeroLengthSrmChromatogram.mzML"));

            var document = SkylineWindow.Document;
            float tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var measuredResults = document.Settings.MeasuredResults;
            Assert.IsNotNull(measuredResults);
            var peptideDocNode = document.Molecules.Single();
            var transitionGroupDocNode = peptideDocNode.TransitionGroups.Single();
            var chromatogramSet = measuredResults.Chromatograms.Single();
            Assert.IsTrue(measuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, transitionGroupDocNode,
                tolerance, out var chromatogramGroupInfos));
            Assert.IsNotNull(chromatogramGroupInfos);
            Assert.AreEqual(1, chromatogramGroupInfos.Length);
            var chromatogramGroupInfo = chromatogramGroupInfos[0];
            
            // Verify that all of the transitions in the document have a chromatogram
            for (int iTransition = 0; iTransition < transitionGroupDocNode.TransitionCount; iTransition++)
            {
                var transitionDocNode = transitionGroupDocNode.Transitions.ElementAt(iTransition);
                var chromatogramInfo = chromatogramGroupInfo.GetTransitionInfo(transitionDocNode, tolerance);
                Assert.IsNotNull(chromatogramInfo);
                Assert.AreNotEqual(0, chromatogramInfo.TimeIntensities.NumPoints);
                Assert.IsNotNull(chromatogramInfo.RawTimes);

                if (iTransition == transitionGroupDocNode.TransitionCount - 1)
                {
                    // In this data, the last transition's chromatogram is the one that has zero points in it
                    Assert.AreEqual(0, chromatogramInfo.RawTimes.Count);
                }
                else
                {
                    Assert.AreNotEqual(0, chromatogramInfo.TimeIntensities.NumPoints);
                }
            }
        }
    }
}
