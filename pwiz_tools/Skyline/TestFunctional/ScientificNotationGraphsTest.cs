/*
 * Original author: Alex MacLean <>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using ZedGraph;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for SplitGraphTest
    /// </summary>
    [TestClass]
    public class ScientificNotationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestScientificNotationGraph()
        {
            TestFilesZip = @"TestFunctional\SplitGraphTest.zip";
            RunFunctionalTest();
        }
        private ToolOptionsUI ToolOptionsDlg { get; set; }
        private ToolOptionsUI ToolOptionsDlg2 { get; set; }
       

        protected override void DoTest()
        {   //import some data
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SplitGraphUnitTest.sky"))); 
            WaitForDocumentLoaded();

            //make sure the check box can be turned on
            Settings.Default.UsePowerOfTen = false;
            ToolOptionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI());
            RunUI(() =>
            { 
                Assert.IsFalse(ToolOptionsDlg.PowerOfTenCheckBox);            
                ToolOptionsDlg.PowerOfTenCheckBox = true;
            });
            OkDialog(ToolOptionsDlg, ToolOptionsDlg.OkDialog);
            Assert.IsTrue(Settings.Default.UsePowerOfTen);
            //import some data
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SplitGraphUnitTest.sky")));
            WaitForDocumentLoaded();
            //make sure the graph is in scientific notation
            GraphChromatogram graph = FindOpenForm<GraphChromatogram>(); 
            AssertScientificNotation(graph.GraphItem);
           
            //make sure the check box can be turned off
            ToolOptionsDlg2 = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI()); 
            RunUI(() =>
            {
                Assert.IsTrue(ToolOptionsDlg2.PowerOfTenCheckBox);
                ToolOptionsDlg2.PowerOfTenCheckBox = false;
            });
            OkDialog(ToolOptionsDlg2, ToolOptionsDlg2.OkDialog);
            Assert.IsFalse(Settings.Default.UsePowerOfTen);
 
        }

        private static void AssertScientificNotation(GraphPane zGraph)
        {
            Assert.AreEqual(zGraph.YAxis.Scale.Mag, 0);
            Assert.AreEqual(zGraph.YAxis.Scale.Format, GraphHelper.scientificNotationFormatString);

        }

    }
}
