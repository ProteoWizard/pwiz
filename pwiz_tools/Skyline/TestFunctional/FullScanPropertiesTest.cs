/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FullScanPropertiesTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestFullScanProperties()
        {
            TestFilesZip = @"TestFunctional\FullScanPropertiesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("NeutralLoss.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("S_3.mzML"));
            RunUI(()=>SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 0));
            WaitForGraphs();
            RunUI(()=>
            {
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.SetTransformChrom(TransformChrom.interpolated);
                SkylineWindow.ShowChromatogramLegends(false);
            });
            WaitForGraphs();
            ClickChromatogram(31.1123047521535, 43338.2577592845, PaneKey.PRODUCTS);
            var graphFullScan = WaitForOpenForm<GraphFullScan>();
            RunUI(()=>
            {
                graphFullScan.ShowPropertiesSheet = true;
                graphFullScan.SetShowAnnotations(true);
                graphFullScan.SetShowAnnotations(false);
                graphFullScan.ShowPropertiesSheet = false;
            });
        }
    }
}
