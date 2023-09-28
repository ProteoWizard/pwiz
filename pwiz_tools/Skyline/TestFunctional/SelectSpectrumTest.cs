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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests clicking on ID lines in a GraphChromatogram to select different spectra in the LibraryMatch window
    /// </summary>
    [TestClass]
    public class SelectSpectrumTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSelectSpectrum()
        {
            TestFilesZip = @"TestFunctional\SelectSpectrumTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SelectSpectrumTest.sky"));
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });
            WaitForDocumentLoaded();
            WaitForGraphs();
            // Click all of the ID lines on the chromatogram
            foreach (var chromatogramSet in SkylineWindow.Document.Settings.MeasuredResults.Chromatograms)
            {
                var graphChrom = SkylineWindow.GetGraphChrom(chromatogramSet.Name);
                foreach (var time in GetAnnotatedRetentionTimes(graphChrom))
                {
                    RunUI(()=>graphChrom.FirePickedSpectrum(time));
                    WaitForGraphs();

                }
            }
            // Click the same ID lines in reverse order
            foreach (var chromatogramSet in SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Reverse())
            {
                var graphChrom = SkylineWindow.GetGraphChrom(chromatogramSet.Name);
                foreach (var time in GetAnnotatedRetentionTimes(graphChrom).Reverse())
                {
                    RunUI(()=>graphChrom.FirePickedSpectrum(time));
                    WaitForGraphs();
                }
            }
        }

        protected IEnumerable<ScaledRetentionTime> GetAnnotatedRetentionTimes(GraphChromatogram graphChrom)
        {
            HashSet<ScaledRetentionTime> times = new HashSet<ScaledRetentionTime>();
            foreach (var graphPane in graphChrom.GraphControl.MasterPane.PaneList)
            {
                foreach (var graphObj in graphPane.GraphObjList)
                {
                    foreach (var graphItem in graphChrom.GetGraphItems(graphPane))
                    {
                        var rt = graphItem.FindSpectrumRetentionTime(graphObj);
                        if (!rt.IsZero)
                        {
                            times.Add(rt);
                        }
                    }
                }
            }

            return times.OrderBy(time => time.MeasuredTime);
        }
    }
}
