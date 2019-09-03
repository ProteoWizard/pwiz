/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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

namespace TestPerf
{
    [TestClass]
    public class PerfMs3ChromatogramTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMs3Chromatograms()
        {
            TestFilesZipPaths = new[]
            {
                "https://skyline.gs.washington.edu/perftests/PerfMs3ChromatogramTest_v1.zip",
                @"TestPerf\PerfMs3ChromatogramTest.zip"
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDirs[1].GetTestPath("Ms3ChromatogramTest.sky")));
            ImportResultsFile(TestFilesDirs[0].GetTestPath("180417_prtcInHela_100pep_2min_bcidrcid_10fmol_1.raw"));
            WaitForDocumentLoaded();
            var peptide = SkylineWindow.Document.Molecules.First();
            var precursor = peptide.TransitionGroups.First();
            var transitionGroupChromInfo = precursor.Results[0].First();
            Assert.IsTrue(transitionGroupChromInfo.Area > 0);
        }
    }
}
