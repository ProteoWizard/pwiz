/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that every transition in a particular Skyline document is properly 
    /// matched up to a chromatogram in the SRM result files.
    /// </summary>
    [TestClass]
    public class SrmSmMolChromTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSrmSmallMoleculeChromatograms()
        {
            TestFilesZip = @"TestFunctional\SrmSmMolChromTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SrmSmMolChromTest.sky")));
            ImportResultsFiles(new[]
            {
                new MsDataFilePath(TestFilesDir.GetTestPath("ID36310_01_WAA263_3771_030118" + ExtensionTestContext.ExtMz5)),
                new MsDataFilePath(TestFilesDir.GetTestPath("ID36311_01_WAA263_3771_030118" + ExtensionTestContext.ExtMz5))
            });
            VerifyAllTransitionsHaveChromatograms(SkylineWindow.Document);
        }

        private void VerifyAllTransitionsHaveChromatograms(SrmDocument doc)
        {
            var tolerance = (float) doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var molecule in doc.Molecules)
            {
                foreach (var precursor in molecule.TransitionGroups)
                {
                    foreach (var chromatogramSet in doc.MeasuredResults.Chromatograms)
                    {
                        ChromatogramGroupInfo[] chromatogramGroups;
                        Assert.IsTrue(doc.MeasuredResults.TryLoadChromatogram(chromatogramSet, molecule, precursor, tolerance, true, out chromatogramGroups));
                        Assert.AreEqual(1, chromatogramGroups.Length);
                        foreach (var transition in precursor.Transitions)
                        {
                            var chromatogram = chromatogramGroups[0].GetTransitionInfo(transition, tolerance, chromatogramSet.OptimizationFunction);
                            Assert.IsNotNull(chromatogram);
                        }
                    }
                }
            }
            
        }
    }
}
