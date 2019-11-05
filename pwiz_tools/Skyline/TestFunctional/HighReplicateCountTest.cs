/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;
using System.IO;
using System.Linq;
using pwiz.Skyline.Properties;


namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test for dealing with high replicate counts, which may cause us to hit windows handle limit.
    /// Skyline limits the number of chromatogram windows that can be open at once in order to avoid that system limit.
    /// Note that in this test we artificially lower our chromatogram window limit for speed.
    /// </summary>
    [TestClass]
    public class HighReplicateCountTest : AbstractFunctionalTestEx
    {
        private const string ZIP_FILE = @"TestFunctional\HighReplicateCountTest.zip";

        [TestMethod]
        public void TestHighReplicateCount()
        {
            TestFilesZip = ZIP_FILE;
            int maxGraphChromOld = Skyline.SkylineWindow.MAX_GRAPH_CHROM;
            try
            {
                // Actually need about 2000 to make Skyline hit the 10,000 window handle limit if it tries to open all chromatogram views at once, and creates a progress bar for each in the loading window
                Skyline.SkylineWindow.MAX_GRAPH_CHROM = 3; // Normally 100, reduce for quicker test
                RunFunctionalTest();
            }
            finally
            {
                Skyline.SkylineWindow.MAX_GRAPH_CHROM = maxGraphChromOld;
            }
        }

        protected override void DoTest()
        {
            var testFilesDir = TestFilesDir;

            var skyFile = "test_b.sky";
            var basename = "289_97";
            var sourceData = TestFilesDir.GetTestPath(basename + ExtensionTestContext.ExtMzml);
            string docPath;
            var doc = InitHighReplicateCountDocument(testFilesDir, skyFile, out docPath);
            Settings.Default.ImportResultsSimultaneousFiles =
                (int) MultiFileLoader.ImportResultsSimultaneousFileOptions
                    .many; // use maximum threads for multiple file import

            var listChromatograms = new List<ChromatogramSet>();
            var filenames = new List<string>();
            var TOO_MANY_FILES = Skyline.SkylineWindow.MAX_GRAPH_CHROM * 2;
            int count;
            for (count = 0; count < TOO_MANY_FILES; count++)
            {
                var fname = TestFilesDir.GetTestPath(GetReplicateNameFromIndex(count));
                filenames.Add(fname);
                if (count != Skyline.SkylineWindow.MAX_GRAPH_CHROM)
                    File.Copy(sourceData, fname);
                var path = MsDataFileUri.Parse(fname);
                listChromatograms.Add(AssertResult.FindChromatogramSet(doc, path) ??
                                      new ChromatogramSet(path.GetFileName().Replace('.', '_'), new[] {path}));
            }

            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(SkylineWindow.SetDocument(docResults, doc));
            var document = WaitForDocumentLoaded();

            float tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var pair in document.MoleculePrecursorPairs)
            {
                for (var f = 0; f < filenames.Count; f++)
                {
                    ChromatogramGroupInfo[] chromGroupInfo;
                    Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(f, pair.NodePep, pair.NodeGroup,
                        tolerance,
                        true, out chromGroupInfo));
                }
            }

            WaitForClosedAllChromatogramsGraph();

            // We expect 0th display to be R0, then as we move on to high undisplayed chromatograms we expect r0 to be replaced with R100, R1 with R101 etc
            var graphChromatograms = SkylineWindow.GraphChromatograms.ToList();
            Assert.IsTrue(graphChromatograms.Any(g => g.NameSet.Equals("R0_mzML")));
            var oldest = graphChromatograms[0].NameSet;
            var newest = "distinct_mzML"; // This replicate should have a different peak
            Assert.IsFalse(graphChromatograms.Any(g => g.NameSet.Equals(newest)));
            // Now select a graph not currently displayed
            RunUI(() => SkylineWindow.SelectedResultsIndex = Skyline.SkylineWindow.MAX_GRAPH_CHROM);
            WaitForGraphs();
            graphChromatograms = SkylineWindow.GraphChromatograms.ToList();
            Assert.IsFalse(graphChromatograms.Any(g => g.NameSet.Equals(oldest)));
            Assert.IsTrue(graphChromatograms.Any(g => g.NameSet.Equals(newest)));
            RunUI(() => SkylineWindow.ArrangeGraphsTiled()); // Arrange so that all are fully visible, so we can use .Visible in test below

            // Now close all, then open them back up again with ctrl-up/ctrl-down hotkeys, and verify that we see the ones we expect
            RunUI(() => SkylineWindow.CloseAllChromatograms());
            WaitForClosedAllChromatogramsGraph();
            // Now run through the replicates
            for (count = 0; count < TOO_MANY_FILES; count++)
            {
                RunUI(() => SkylineWindow.SelectedResultsIndex = count);
                var name = GetReplicateNameFromIndex(count).Replace(@".", @"_");
                WaitForConditionUI(() => SkylineWindow.GraphChromatograms.Any(g => g.NameSet.Equals(name) && g.Visible));
                graphChromatograms = SkylineWindow.GraphChromatograms.ToList();
                for (var index = 0; index < TOO_MANY_FILES; index++)
                {
                    name = GetReplicateNameFromIndex(index).Replace(@".",@"_");
                    var visible = graphChromatograms.Any(g => g.NameSet.Equals(name) && g.Visible);
                    var shouldBeVisible = (index > count - Skyline.SkylineWindow.MAX_GRAPH_CHROM) && (index <= count);
                    Assert.IsTrue(visible == shouldBeVisible);
                    if (shouldBeVisible)
                    {
                        // Make sure it's showing the right data
                        Assert.IsTrue(graphChromatograms.First(g => g.NameSet.Equals(name)).FilePath.ToString().Contains(GetReplicateNameFromIndex(index)));
                    }
                }
            }
        }

        private static string GetReplicateNameFromIndex(int count)
        {
            return (count==Skyline.SkylineWindow.MAX_GRAPH_CHROM ? "distinct" : "R" + count) + ExtensionTestContext.ExtMzml;
        }

        private SrmDocument InitHighReplicateCountDocument(TestFilesDir testFilesDir, string fileName, out string docPath)
        {
            docPath = testFilesDir.GetTestPath(fileName);

            var documentFile = TestFilesDir.GetTestPath(docPath);
            WaitForCondition(() => File.Exists(documentFile));
            
            OpenDocument(docPath);
            return SkylineWindow.Document;
        }

    }
}