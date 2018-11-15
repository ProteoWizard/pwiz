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

        protected override void DoTest()
        {
            // Init scientific notation to off
            RunUI(() => Settings.Default.UsePowerOfTen = false);
            // Open document with chromatograms
            var doc = SkylineWindow.Document;
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SplitGraphUnitTest.sky"))); 
            WaitForDocumentChangeLoaded(doc);
            WaitForGraphs();

            // Make sure the check box can be turned on
            TestScientificNotationSetting(true);

            // Make sure the check box can be turned off
            TestScientificNotationSetting(false);
        }

        private static void TestScientificNotationSetting(bool scientificNotationOn)
        {
            // Make sure the Tools > Options dialog box can be used to set the setting
            var toolOptionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI());
            RunUI(() =>
            {
                Assert.IsFalse(toolOptionsDlg.PowerOfTenCheckBox == scientificNotationOn);
                toolOptionsDlg.PowerOfTenCheckBox = scientificNotationOn;
            });
            OkDialog(toolOptionsDlg, toolOptionsDlg.OkDialog);
            WaitForConditionUI(() => Settings.Default.UsePowerOfTen == scientificNotationOn);
            WaitForGraphs();

            // Make sure the graph is in scientific notation or not depending on scientificNotationOn parameter
            var graph = FindOpenForm<GraphChromatogram>();
            RunUI(() =>
            {
                Assert.AreEqual(graph.GraphItem.YAxis.Scale.Mag, 0);
                if (scientificNotationOn)
                    Assert.AreEqual(graph.GraphItem.YAxis.Scale.Format, GraphHelper.SCIENTIFIC_NOTATION_FORMAT_STRING);
                else
                    Assert.AreNotEqual(graph.GraphItem.YAxis.Scale.Format, GraphHelper.SCIENTIFIC_NOTATION_FORMAT_STRING);
            });
        }
    }
}
