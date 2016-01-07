/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.Results
{
    /// <summary>
    /// Tests for the <see cref="ChromCacheMinimizer"/> class.
    /// </summary>
    [TestClass]
    public class ChromCacheMinimizerTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestA\Results\FullScan.zip";

        /// <summary>
        /// Tests that calling <see cref="ChromCacheMinimizer.Minimize"/> statistics that are approximately correct.
        /// </summary>
        [TestMethod]
        public void TestCollectStatistics()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                                                      ExtensionTestContext.ExtThermoRaw);
            var measuredResults = new MeasuredResults(new[] {new ChromatogramSet("Single", new[] {MsDataFileUri.Parse(rawPath)})});

            SrmDocument docResults = docContainer.ChangeMeasuredResults(measuredResults, 3, 3, 21);

            ChromCacheMinimizer chromCacheMinimizer =
                docResults.Settings.MeasuredResults.GetChromCacheMinimizer(docResults);
            ChromCacheMinimizer.Settings settings =
                new ChromCacheMinimizer.Settings().SetDiscardUnmatchedChromatograms(true);
            ChromCacheMinimizer.MinStatistics minStatistics = null;
            chromCacheMinimizer.Minimize(settings, s => minStatistics = s, null);
            Assert.AreEqual(100, minStatistics.PercentComplete);
            Assert.AreEqual(1.0, minStatistics.MinimizedRatio);

            var docMissingFirstPeptide =
                (SrmDocument)
                docResults.ReplaceChild(
                    docResults.PeptideGroups.First().RemoveChild(docResults.PeptideGroups.First().Children[0]));
            var docWithOnlyFirstPeptide =
                (SrmDocument)
                docResults.ReplaceChild(
                    docResults.PeptideGroups.First().ChangeChildren(new[] {docResults.PeptideGroups.First().Children[0]}));

            ChromCacheMinimizer.MinStatistics statsMissingFirstProtein = null;
            ChromCacheMinimizer.MinStatistics statsWithOnlyFirstProtein = null;

            settings = settings.SetDiscardUnmatchedChromatograms(true);
            ChromCacheMinimizer minimizerMissingFirstProtein =
                docMissingFirstPeptide.Settings.MeasuredResults.GetChromCacheMinimizer(docMissingFirstPeptide);
            ChromCacheMinimizer minimizerWithOnlyFirstProtein =
                docWithOnlyFirstPeptide.Settings.MeasuredResults.GetChromCacheMinimizer(docWithOnlyFirstPeptide);
            minimizerMissingFirstProtein.Minimize(settings, s => statsMissingFirstProtein = s, null);
            minimizerWithOnlyFirstProtein.Minimize(settings, s => statsWithOnlyFirstProtein = s, null);
            Assert.AreEqual(100, statsMissingFirstProtein.PercentComplete);
            Assert.AreEqual(100, statsWithOnlyFirstProtein.PercentComplete);
            Assert.AreEqual(1.0, statsMissingFirstProtein.MinimizedRatio + statsWithOnlyFirstProtein.MinimizedRatio,
                            .00001);
            settings = settings.SetDiscardUnmatchedChromatograms(false);
            ChromCacheMinimizer.MinStatistics statsMissingFirstProteinKeepAll = null;
            ChromCacheMinimizer.MinStatistics statsWithOnlyFirstProteinKeepAll = null;
            minimizerMissingFirstProtein.Minimize(settings, s => statsMissingFirstProteinKeepAll = s, null);
            minimizerWithOnlyFirstProtein.Minimize(settings, s => statsWithOnlyFirstProteinKeepAll = s, null);
            Assert.AreEqual(100, statsMissingFirstProteinKeepAll.PercentComplete);
            Assert.AreEqual(1.0, statsMissingFirstProteinKeepAll.MinimizedRatio);
            Assert.AreEqual(100, statsWithOnlyFirstProteinKeepAll.PercentComplete);
            Assert.AreEqual(1.0, statsWithOnlyFirstProteinKeepAll.MinimizedRatio);
            docContainer.Release();
        }

        /// <summary>
        /// Tests that setting <see cref="ChromCacheMinimizer.Settings.NoiseTimeRange"/> results in cache files 
        /// that look correct.
        /// </summary>
        [TestMethod]
        public void TestNoiseTimeLimit()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                                                      ExtensionTestContext.ExtThermoRaw);
            var measuredResults = new MeasuredResults(new[] {new ChromatogramSet("Single", new[] {rawPath})});

            SrmDocument docResults = docContainer.ChangeMeasuredResults(measuredResults, 3, 3, 21);
            var tolerance = (float) docResults.Settings.TransitionSettings.Instrument.MzMatchTolerance;


            ChromCacheMinimizer.Settings settings = new ChromCacheMinimizer.Settings()
                .SetDiscardUnmatchedChromatograms(false)
                .SetNoiseTimeRange(1.0);
            string minimized1Path = testFilesDir.GetTestPath("NoiseTimeLimited1.sky");
            string minimized2Path = testFilesDir.GetTestPath("NoiseTimeLimited2.sky");
            ResultsTestDocumentContainer docContainerMinimized1Min = MinimizeCacheFile(docResults,
                                                                                       settings.SetNoiseTimeRange(1.0),
                                                                                       minimized1Path);
            ResultsTestDocumentContainer docContainerMinimized2Min = MinimizeCacheFile(docResults,
                                                                                       settings.SetNoiseTimeRange(2.0),
                                                                                       minimized2Path);
            SrmDocument docMinimized1Min = docContainerMinimized1Min.Document;
            SrmDocument docMinimized2Min = docContainerMinimized2Min.Document;
            ChromatogramSet chromSet1Min = docMinimized1Min.Settings.MeasuredResults.Chromatograms[0];
            ChromatogramSet chromSet2Min = docMinimized2Min.Settings.MeasuredResults.Chromatograms[0];
            ChromatogramSet chromSetOriginal = docResults.Settings.MeasuredResults.Chromatograms[0];
            foreach (var pair in docResults.PeptidePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupsOriginal;
                ChromatogramGroupInfo[] chromGroups1;
                ChromatogramGroupInfo[] chromGroups2;

                docMinimized1Min.Settings.MeasuredResults.TryLoadChromatogram(chromSet1Min,
                    pair.NodePep, pair.NodeGroup, tolerance, true, out chromGroups1);
                docMinimized2Min.Settings.MeasuredResults.TryLoadChromatogram(chromSet2Min,
                    pair.NodePep, pair.NodeGroup, tolerance, true, out chromGroups2);
                docResults.Settings.MeasuredResults.TryLoadChromatogram(chromSetOriginal,
                    pair.NodePep, pair.NodeGroup, tolerance, true, out chromGroupsOriginal);
                Assert.AreEqual(chromGroups1.Length, chromGroups2.Length);
                Assert.AreEqual(chromGroups1.Length, chromGroupsOriginal.Length);
                for (int iChromGroup = 0; iChromGroup < chromGroups1.Length; iChromGroup++)
                {
                    ChromatogramGroupInfo chromGroup1 = chromGroups1[iChromGroup];
                    ChromatogramGroupInfo chromGroup2 = chromGroups2[iChromGroup];
                    ChromatogramGroupInfo chromGroupOriginal = chromGroupsOriginal[iChromGroup];
                    var times = new[]
                                    {
                                        chromGroupOriginal.Times[0],
                                        chromGroup2.Times[0],
                                        chromGroup1.Times[0],
                                        chromGroup1.Times[chromGroup1.Times.Length - 1],
                                        chromGroup2.Times[chromGroup2.Times.Length - 1],
                                        chromGroupOriginal.Times[chromGroupOriginal.Times.Length - 1]
                                    };
                    // The two minute window around the peak might overlap with either the start or end of the original chromatogram,
                    // but will never overlap with both.
                    Assert.IsTrue(chromGroup2.Times[0] > chromGroupOriginal.Times[0]
                                  ||
                                  chromGroup2.Times[chromGroup2.Times.Length - 1] <
                                  chromGroupOriginal.Times[chromGroupOriginal.Times.Length - 1]);
                    // If the two minute window does not overlap with the start/end of the original chromatogram, then the difference
                    // in time between the one minute window and the two minute window will be approximately 1 minute.
                    if (chromGroup2.Times[0] > chromGroupOriginal.Times[0])
                    {
                        Assert.AreEqual(chromGroup2.Times[0], chromGroup1.Times[0] - 1, .1);
                    }
                    if (chromGroup2.Times[chromGroup2.Times.Length - 1] <
                        chromGroupOriginal.Times[chromGroupOriginal.Times.Length - 1])
                    {
                        Assert.AreEqual(chromGroup2.Times[chromGroup2.Times.Length - 1],
                                        chromGroup1.Times[chromGroup1.Times.Length - 1] + 1, .1);
                    }
                    float[] timesSorted = times.ToArray();
                    Array.Sort(timesSorted);
                    CollectionAssert.AreEqual(times, timesSorted);
                }
            }
            docContainer.Release();
            docContainerMinimized1Min.Release();
            docContainerMinimized2Min.Release();
        }

        private static ResultsTestDocumentContainer MinimizeCacheFile(SrmDocument document,
                ChromCacheMinimizer.Settings settings, string skyFilePath)
        {
            string skydFilePath = Path.ChangeExtension(skyFilePath, ChromatogramCache.EXT);
            ChromCacheMinimizer chromCacheMinimizer = document.Settings.MeasuredResults.GetChromCacheMinimizer(document);
            using (var fs = new FileSaver(skydFilePath))
            using (var fsScans = new FileSaver(skydFilePath + ChromatogramCache.SCANS_EXT, true))
            using (var fsPeaks = new FileSaver(skydFilePath + ChromatogramCache.PEAKS_EXT, true))
            using (var fsScores = new FileSaver(skydFilePath + ChromatogramCache.SCORES_EXT, true))
            {
                fs.Stream = File.OpenWrite(fs.SafeName);
                chromCacheMinimizer.Minimize(settings, null, fs.Stream,
                    fsScans.FileStream, fsPeaks.FileStream, fsScores.FileStream);
                fs.Commit();
            }
            using (var writer = new XmlTextWriter(skyFilePath, Encoding.UTF8) {Formatting = Formatting.Indented})
            {
                var ser = new XmlSerializer(typeof (SrmDocument));
                ser.Serialize(writer, document);

                writer.Flush();
                writer.Close();
            }
            var container = new ResultsTestDocumentContainer(document, skyFilePath, false);
            Assert.IsTrue(container.SetDocument(ResultsUtil.DeserializeDocument(skyFilePath), document, true));
            return container;
        }
    }
}