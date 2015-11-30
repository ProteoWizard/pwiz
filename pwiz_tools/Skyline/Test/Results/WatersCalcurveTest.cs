/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    /// <summary>
    /// Summary description for SmallWiffTest
    /// </summary>
    [TestClass]
    public class WatersCalcurveTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"Test\Results\WatersCalcurve.zip";

        [TestMethod]
        public void WatersFileTypeTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string extRaw = ExtensionTestContext.ExtWatersRaw;

            // Do file type checks
            using (var msData = new MsDataFileImpl(testFilesDir.GetTestPath("160109_Mix1_calcurve_075" + extRaw)))
            {
                Assert.IsTrue(msData.IsWatersFile);
                Assert.IsFalse(msData.IsWatersLockmassCorrectionCandidate);  // This file contains both scan and chromatogram data, we ignore the scan data
            }

            using (var msData = new MsDataFileImpl(testFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML")))
            {
                Assert.IsTrue(msData.IsWatersFile);
            }

            // TODO: Fix Proteowizard.
            // This mzXML is from the MassWolf converter.  
            // The source file types are recorded as "RAWData" and the parent file extension is .raw. 
            // This ends up getting interpreted as ""Thermo RAW file" when reading with proteowizard.
            // using (var msData = new MsDataFileImpl(testFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzXML")))
            // {
            //     Assert.IsFalse(msData.IsWatersFile);
            //     Assert.IsTrue(msData.IsThermoFile);
            // }

            using (var msData = new MsDataFileImpl(testFilesDir.GetTestPath("160109_Mix1_calcurve_073.mzML")))
            {
                Assert.IsTrue(msData.IsWatersFile);
            }

            using (var msData = new MsDataFileImpl(testFilesDir.GetTestPath("160109_Mix1_calcurve_078.mzML")))
            {
                Assert.IsTrue(msData.IsWatersFile);
            }
        }

        [TestMethod]
        public void WatersMultiReplicateTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath;
            SrmDocument docOriginal = InitWatersDocument(testFilesDir, out docPath);
            SrmDocument doc = docOriginal;
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);
            string extRaw = ExtensionTestContext.ExtWatersRaw;

            string[] replicatePaths =
            {
                testFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML"),
                testFilesDir.GetTestPath("160109_Mix1_calcurve_073.mzML"),
                testFilesDir.GetTestPath("160109_Mix1_calcurve_075" + extRaw),
                testFilesDir.GetTestPath("160109_Mix1_calcurve_078.mzML")
            };
            // Count peaks where higher concentration replicates show less area
            int outOfOrder = 0;
            foreach (string path in replicatePaths)
            {
                var listChromatograms = new List<ChromatogramSet>();
                if (doc.Settings.HasResults)
                    listChromatograms.AddRange(doc.Settings.MeasuredResults.Chromatograms);

                string name = Path.GetFileNameWithoutExtension(path);
                if (name != null)
                    name = name.Substring(name.Length - 12);
                listChromatograms.Add(new ChromatogramSet(name, new[] {MsDataFileUri.Parse(path)}));
                int len = listChromatograms.Count;

                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                // Adding unloaded results should add a new null result.
                foreach (var nodeTran in docResults.PeptideTransitions)
                {
                    Assert.IsTrue(nodeTran.HasResults);
                    Assert.AreEqual(listChromatograms.Count, nodeTran.Results.Count);
                    Assert.IsNull(nodeTran.Results[len - 1]);
                }

                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true),
                    string.Format("Failed adding results for {0}.", path));
                docContainer.AssertComplete();
                docResults = docContainer.Document;

                Assert.IsTrue(docResults.Settings.MeasuredResults.IsLoaded);

                var transOld = doc.PeptideTransitions.ToArray();
                var transNew = docResults.PeptideTransitions.ToArray();
                Assert.AreEqual(transOld.Length, transNew.Length);
                int countPeaks = 0;
                for (int i = 0; i < transNew.Length; i++)
                {
                    // Make sure new peak was added to each transition
                    var nodeTranNew = transNew[i];
                    Assert.IsTrue(nodeTranNew.HasResults);
                    Assert.AreEqual(len, nodeTranNew.Results.Count);
                    var chromInfo = nodeTranNew.Results[len - 1][0];
                    Assert.IsNotNull(chromInfo);

                    if (!chromInfo.IsEmpty)
                        countPeaks++;

                    // Make sure previously loaded peaks did not change
                    for (int j = 0; j < len - 1; j++)
                    {
                        var chromInfoPrevious = transOld[i].Results[j][0];
                        Assert.AreSame(chromInfoPrevious, nodeTranNew.Results[j][0]);
                        if ((chromInfo.IsEmpty && !chromInfoPrevious.IsEmpty) ||
                                (!chromInfo.IsEmpty && chromInfoPrevious.Area >= chromInfo.Area))
                            outOfOrder++;
                    }
                }
                // Allow 2 missed peaks
                Assert.IsTrue(countPeaks >= transNew.Length - (TestSmallMolecules ? 1 : 0) - 2);

                // Check results calculations for peptides and groups
                foreach (var nodePep in docResults.Peptides)
                {
                    Assert.AreEqual(len, nodePep.Results.Count);
                    Assert.IsTrue(nodePep.HasResults);
                    var chromInfo = nodePep.Results[len - 1][0];
                    Assert.AreEqual(1, nodePep.Children.Count);
                    var nodeGroup = (TransitionGroupDocNode) nodePep.Children[0];
                    Assert.IsTrue(nodeGroup.HasResults);
                    Assert.AreEqual(len, nodeGroup.Results.Count);
                    var chromInfoGroup = nodeGroup.Results[len - 1][0];
                    Assert.IsTrue(chromInfoGroup.PeakCountRatio >= 0.5);
                    Assert.IsTrue(chromInfoGroup.RetentionTime.HasValue);
                    Assert.IsTrue(chromInfoGroup.Area.HasValue && chromInfoGroup.Area > 290);

                    Assert.AreEqual(chromInfo.RetentionTime, chromInfoGroup.RetentionTime);
                    Assert.AreEqual(chromInfo.PeakCountRatio, chromInfoGroup.PeakCountRatio);
                }

                doc = docResults;
            }

            Assert.AreEqual(13, outOfOrder, 1);

            // Remove the original data
            foreach (string path in replicatePaths)
            {
                if (File.Exists(path))
                    FileEx.SafeDelete(path);
                else
                    DirectoryEx.SafeDelete(path);
            }
            FileEx.SafeDelete(docPath);

            // Save the document
            string xmlSaved = null;
            var docPersisted = AssertEx.RoundTrip(doc, ref xmlSaved);
            Assert.IsTrue(!docPersisted.Settings.MeasuredResults.IsLoaded);
            // Make sure the persisted document round-trips.
            // The orginal doesn't because of changing precision in the results info.
            AssertEx.Serializable(docPersisted, AssertEx.DocumentCloned);

            // Make sure the loaded document has reasonable results info
            // before the cache files are loaded
            for (int i = 0; i < doc.Children.Count; i++)
            {
                PeptideGroupDocNode nodePepGroup1 = (PeptideGroupDocNode) doc.Children[i];
                if (TestSmallMolecules && nodePepGroup1.Name.Equals(SrmDocument.TestingNonProteomicMoleculeGroupName))
                    continue;
                PeptideGroupDocNode nodePepGroup2 = (PeptideGroupDocNode) docPersisted.Children[i];
                Assert.AreNotSame(nodePepGroup1, nodePepGroup2);
                for (int j = 0; j < nodePepGroup1.Children.Count; j++)
                {
                    PeptideDocNode nodePep1 = (PeptideDocNode) nodePepGroup1.Children[j];
                    PeptideDocNode nodePep2 = (PeptideDocNode) nodePepGroup2.Children[j];
                    Assert.AreNotSame(nodePep1, nodePep2);
                    Assert.AreEqual(nodePep1.Results.Count, nodePep2.Results.Count);
                    for (int k = 0; k < nodePep1.Results.Count; k++)
                        Assert.AreEqual(nodePep1.Results[k][0].PeakCountRatio, nodePep2.Results[k][0].PeakCountRatio);
                    for (int k = 0; k < nodePep1.Children.Count; k++)
                    {
                        TransitionGroupDocNode nodeGroup1 = (TransitionGroupDocNode) nodePep1.Children[k];
                        TransitionGroupDocNode nodeGroup2 = (TransitionGroupDocNode) nodePep2.Children[k];
                        Assert.AreNotSame(nodeGroup1, nodeGroup2);
                        Assert.AreEqual(nodeGroup1.Results.Count, nodeGroup2.Results.Count);
                        for (int l = 0; l < nodeGroup1.Results.Count; l++)
                            Assert.AreEqual(nodeGroup1.Results[l][0].PeakCountRatio,
                                            nodeGroup2.Results[l][0].PeakCountRatio);
                        for (int l = 0; l < nodeGroup1.Children.Count; l++)
                        {
                            TransitionDocNode nodeTran1 = (TransitionDocNode) nodeGroup1.Children[l];
                            TransitionDocNode nodeTran2 = (TransitionDocNode) nodeGroup2.Children[l];
                            Assert.AreNotSame(nodeTran1, nodeTran2);
                            Assert.AreEqual(nodeTran1.Results.Count, nodeTran2.Results.Count);
                            for (int m = 0; m < nodeTran1.Results.Count; m++)
                            {
                                if (nodeTran1.Results[m] != null && nodeTran2.Results[m] != null)
                                    Assert.AreEqual(nodeTran1.Results[m][0].IsEmpty, nodeTran2.Results[m][0].IsEmpty);
                                else
                                    Assert.AreEqual(nodeTran1.Results[m], nodeTran2.Results[m]); // both null
                            }
                        }
                    }
                }
            }

            // Reload data from .skyd files
            Assert.IsTrue(docContainer.SetDocument(docPersisted, doc, true));
            docContainer.AssertComplete();
            doc = docContainer.Document;

            var results = doc.Settings.MeasuredResults;
            const float tolerance = (float) TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE;
            foreach (var pair in doc.PeptidePrecursorPairs)
            {
                foreach (var chromSet in results.Chromatograms)
                {
                    ChromatogramGroupInfo[] chromGroupInfo;
                    Assert.IsTrue(results.TryLoadChromatogram(chromSet, pair.NodePep, pair.NodeGroup,
                                                              tolerance, true, out chromGroupInfo));
                }
            }

            // The single final cache path should be open now
            var listCachePaths = new List<string>(doc.Settings.MeasuredResults.CachePaths);
            // Should only have one cache file at this point
            Assert.AreEqual(1, listCachePaths.Count);
            foreach (var cachePath in listCachePaths)
            {
                // Attempting to delete should throw
                string path = cachePath;
                AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(path));
            }

            // Release the .skyd file
            docContainer.Release();
            foreach (var cachePath in listCachePaths)
            {
                // Cache files should be closed now, and delete successfully.
                FileEx.SafeDelete(cachePath);
            }
            testFilesDir.Dispose();
        }

        [TestMethod]
        public void WatersMultiFileTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath;
            SrmDocument docOriginal = InitWatersDocument(testFilesDir, out docPath);
            SrmDocument doc = docOriginal;
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);
            string extRaw = ExtensionTestContext.ExtWatersRaw;

            var listChromatograms = new List<ChromatogramSet>
            {
                new ChromatogramSet("double", new[]
                    {
                        MsDataFileUri.Parse(testFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML")),
                        MsDataFileUri.Parse(testFilesDir.GetTestPath("160109_Mix1_calcurve_073.mzML"))
                    }),
                new ChromatogramSet("trouble", new[]
                    {
                        MsDataFileUri.Parse(testFilesDir.GetTestPath("160109_Mix1_calcurve_075" + extRaw)),
                        MsDataFileUri.Parse(testFilesDir.GetTestPath("160109_Mix1_calcurve_078.mzML"))
                    })
            };
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            docResults = docContainer.Document;
            Assert.IsTrue(docResults.Settings.HasResults);
            var measuredResults = docResults.Settings.MeasuredResults;
            var chromatograms = measuredResults.Chromatograms;
            Assert.AreEqual(2, chromatograms.Count);

            const float tolerance = (float)TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE;

            foreach (var pair in docResults.PeptidePrecursorPairs)
            {
                var nodePep = pair.NodePep;
                var nodeGroup = pair.NodeGroup;
                Assert.IsTrue(nodeGroup.HasResults);
                Assert.AreEqual(2, nodeGroup.Results.Count);
                foreach (var result in nodeGroup.Results)
                    Assert.AreEqual(2, result.Count);
                for (int i = 0; i < 2; i++)
                {
                    ChromatogramGroupInfo[] chromInfos;
                    Assert.IsTrue(measuredResults.TryLoadChromatogram(i, nodePep, nodeGroup, tolerance, true, out chromInfos));
                    Assert.AreEqual(2, chromInfos.Length);
                    double[] peakAreas = new double[2];
                    for (int j = 0; j < 2; j++)
                    {
                        var chromInfo = chromInfos[j];
                        Assert.IsTrue(chromInfo.BestPeakIndex != -1);
                        foreach (var tranInfo in chromInfo.TransitionPointSets)
                        {
                            var peakInfo = tranInfo.GetPeak(chromInfo.BestPeakIndex);
                            if (peakInfo.IsEmpty || peakInfo.IsForcedIntegration)
                                continue;

                            // Check times
                            var times = tranInfo.Times;
                            int iStart = Array.BinarySearch(times, peakInfo.StartTime);
                            Assert.IsTrue(iStart >= 0);
                            int iEnd = Array.BinarySearch(times, peakInfo.EndTime);
                            Assert.IsTrue(iEnd >= 0);
                            int iPeak = Array.BinarySearch(times, iStart, iEnd - iStart, peakInfo.RetentionTime);
                            // Check intensities at times
                            var intensities = tranInfo.Intensities;
                            Assert.IsTrue(intensities[iStart] < intensities[iPeak]);
                            Assert.IsTrue(intensities[iEnd] < intensities[iPeak]);
                            // Sum peak area
                            peakAreas[j] += peakInfo.Area;
                        }
                    }
                    Assert.IsTrue(peakAreas[0] < peakAreas[1]);
                }
            }

            // Release file handles
            docContainer.Release();
            testFilesDir.Dispose();
        }

        [TestMethod]
        public void WatersMzXmlTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath;
            SrmDocument doc = InitWatersDocument(testFilesDir, out docPath);
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);
            // Verify mzXML and mzML contained same results
            // TODO: Figure out why these don't match well enough to use strict compare
            AssertResult.MatchChromatograms(docContainer,
                                            testFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML"),
                                            testFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzXML"),
                                            -1, 0);
            // Release file handles
            docContainer.Release();
            testFilesDir.Dispose();
        }

        [TestMethod]
        public void WatersCacheTest()
        {
            // First test transition from per-replicate caching strategy to
            // single cache per document strategy.
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            // Open the replicate document, and let it reload the data from mzML
            // showing the document can find data files by name in its own directory,
            // since the document paths will not match those on disk.
            string docPath;
            var doc = InitWatersDocument(testFilesDir, out docPath);
            var docReload = InitWatersDocument(testFilesDir, "160109_Mix1_calcurve_rep.sky", out docPath);
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);
            var streamManager = docContainer.ChromatogramManager.StreamManager;
            Assert.IsTrue(docContainer.SetDocument(docReload, doc, true));
            docContainer.AssertComplete();
            docReload = docContainer.Document;
            // Release file handles to cache files created during load
            Assert.IsTrue(docContainer.SetDocument(doc, docReload));
            // Delete the cache
            string cachePath = Path.ChangeExtension(docPath, ".skyd");
            FileEx.SafeDelete(cachePath);

            // Then try using cached replicate files
            // Move per-replicate cache files into place
            var replicateCacheNames = new[]
                {
                    "160109_Mix1_calcurve_rep_calcurve_070.skyd",
                    "160109_Mix1_calcurve_rep_calcurve_073.skyd"
                };
            GetCacheFiles(testFilesDir, replicateCacheNames);
            // Delete the files these cache
            DeleteFiles(testFilesDir,
                new[]
                {
                    "160109_Mix1_calcurve_070.mzML",
                    "160109_Mix1_calcurve_073.mzML",
                });
            var docCached = InitWatersDocument(testFilesDir, "160109_Mix1_calcurve_rep.sky", out docPath);
            Assert.IsTrue(docContainer.SetDocument(docCached, doc, true));
            docContainer.AssertComplete();
            docCached = docContainer.Document;

            // The document with data from the .mzML files should be the same as
            // the one loaded from the .skyd files.
            // Unfortunately, this is to hard to maintain when cache changes are made.
            // AssertEx.Cloned(docCached, docReload);

            // The one cache should be present
            Assert.IsTrue(File.Exists(cachePath));
            // And the replicate cache files should have been removed
            foreach (var cacheName in replicateCacheNames)
                Assert.IsFalse(File.Exists(testFilesDir.GetTestPath(cacheName)));

            // Save the cache file time stamp
            var cacheInfo = new FileInfo(cachePath);
            long cacheSize = cacheInfo.Length;

            // Adding files already in the document should have no impact on the cache.
            string extRaw = ExtensionTestContext.ExtWatersRaw;
            var listChromatograms = new List<ChromatogramSet>(docCached.Settings.MeasuredResults.Chromatograms)
                {
                    new ChromatogramSet("extra1",
                                        new[] { MsDataFileUri.Parse(testFilesDir.GetTestPath("160109_Mix1_calcurve_075" + extRaw)) }),
                    new ChromatogramSet("extra2",
                                        new[] { MsDataFileUri.Parse(testFilesDir.GetTestPath("160109_Mix1_calcurve_078.mzML")) })
                };

            // Adding a new file should cause the cache to grow.
            var settings = docCached.Settings.MeasuredResults.ChangeChromatograms(listChromatograms);
            var docGrow = docCached.ChangeMeasuredResults(settings);
            Assert.IsTrue(docContainer.SetDocument(docGrow, docCached, true));
            docContainer.AssertComplete();
            docGrow = docContainer.Document;

            cacheInfo = new FileInfo(cachePath);
            Assert.IsTrue(cacheSize < cacheInfo.Length);

            cacheSize = cacheInfo.Length;
            var writeTime = cacheInfo.LastWriteTime;

            listChromatograms.Add(
                    new ChromatogramSet("double",
                        new[]
                            {
                                testFilesDir.GetTestPath("160109_Mix1_calcurve_075" + extRaw),
                                testFilesDir.GetTestPath("160109_Mix1_calcurve_078.mzML")
                            }));

            settings = docGrow.Settings.MeasuredResults.ChangeChromatograms(listChromatograms);
            var docNoCacheChange1 = docGrow.ChangeMeasuredResults(settings);
            Assert.IsTrue(docContainer.SetDocument(docNoCacheChange1, docGrow, true));
            docContainer.AssertComplete();
            docNoCacheChange1 = docContainer.Document;

            Assert.AreEqual(writeTime, File.GetLastWriteTime(cachePath));

            // Removing files should have no impact, until optimized
            listChromatograms.RemoveRange(listChromatograms.Count - 2, 2);
            listChromatograms.RemoveAt(1);

            settings = docNoCacheChange1.Settings.MeasuredResults.ChangeChromatograms(listChromatograms);
            var docNoCacheChange2 = docNoCacheChange1.ChangeMeasuredResults(settings);
            Assert.IsTrue(docContainer.SetDocument(docNoCacheChange2, docNoCacheChange1, true));
            docContainer.AssertComplete();
            docNoCacheChange2 = docContainer.Document;

            Assert.AreEqual(writeTime, File.GetLastWriteTime(cachePath));

            // Optimizing should shrink the cache
            var results = docNoCacheChange2.Settings.MeasuredResults.OptimizeCache(docPath, streamManager);
            var docOptimized = new SrmDocument(docNoCacheChange2,
                                               docNoCacheChange2.Settings.ChangeMeasuredResults(results),
                                               docNoCacheChange2.Children);
            // This should not cause a reload
            Assert.IsTrue(docContainer.SetDocument(docOptimized, docNoCacheChange2, false));

            cacheInfo = new FileInfo(cachePath);
            Assert.IsTrue(cacheSize > cacheInfo.Length);

            // Test file caches
            // First reload the files from .mzML
            docReload = InitWatersDocument(testFilesDir, "160109_Mix1_calcurve_file.sky", out docPath);
            // Change the path to use the right .skyd file
            docContainer.DocumentFilePath = docPath;
            Assert.IsTrue(docContainer.SetDocument(docReload, docOptimized, true));
            docContainer.AssertComplete();
            docReload = docContainer.Document;
            // Release file handles to cache files created during load
            Assert.IsTrue(docContainer.SetDocument(doc, docReload));
            // Delete the cache
            cachePath = Path.ChangeExtension(docPath, ".skyd");
            FileEx.SafeDelete(cachePath);

            // Then try using cached files
            // Move per-file cache files into place
            var fileCacheNames = new[]
                {
                    "160109_Mix1_calcurve_075.mzML.skyd",
                    "160109_Mix1_calcurve_078.mzML.skyd"
                };
            GetCacheFiles(testFilesDir, fileCacheNames);
            // Swap the mzML files, so the test will fail, if not reading from the cache
            // CONSIDER: Should this really work, since they have different time stamps?
            string file075 = testFilesDir.GetTestPath("160109_Mix1_calcurve_075.mzML");
            string file078 = testFilesDir.GetTestPath("160109_Mix1_calcurve_078.mzML");
            string fileTemp = file075 + ".tmp";
            File.Move(file075, fileTemp);
            File.Move(file078, file075);
            File.Move(fileTemp, file078);

            docCached = InitWatersDocument(testFilesDir, "160109_Mix1_calcurve_file.sky", out docPath);
            // Make sure cache files exactly match the names the loader will look for
            var listResultsFiles = new List<MsDataFileUri>();
            foreach (var chromatogram in docCached.Settings.MeasuredResults.Chromatograms)
                listResultsFiles.AddRange(chromatogram.MSDataFilePaths);
            for (int i = 0; i < fileCacheNames.Length; i++)
            {
                string partPath = ChromatogramCache.PartPathForName(docPath, listResultsFiles[i]);
                File.Move(testFilesDir.GetTestPath(fileCacheNames[i]), partPath);
            }

            Assert.IsTrue(docContainer.SetDocument(docCached, doc, true));
            docContainer.AssertComplete();
            // docCached = docContainer.Document;

            // The document with data from the .mzML files should be the same as
            // the one loaded from the .skyd files.
            // Unfortunately, this is to hard to maintain when cache changes are made.
            // AssertEx.Cloned(docCached, docReload);

            // The one cache should be present
            Assert.IsTrue(File.Exists(Path.ChangeExtension(docPath, ".skyd")));
            // And the replicate cache files should have been removed
            foreach (var cacheName in fileCacheNames)
                Assert.IsFalse(File.Exists(testFilesDir.GetTestPath(cacheName)));

            // Release file handles
            docContainer.Release();
            testFilesDir.Dispose();
        }

        private static void GetCacheFiles(TestFilesDir testFilesDir, IEnumerable<string> fileNames)
        {
            foreach (var fileName in fileNames)
            {
                File.Move(Path.Combine(testFilesDir.GetTestPath("cache"), fileName),
                    testFilesDir.GetTestPath(fileName));
            }            
        }

        private static void DeleteFiles(TestFilesDir testFilesDir, IEnumerable<string> fileNames)
        {
            foreach (var fileName in fileNames)
                FileEx.SafeDelete(testFilesDir.GetTestPath(fileName));
        }

        private static SrmDocument InitWatersDocument(TestFilesDir testFilesDir, out string docPath)
        {
            return InitWatersDocument(testFilesDir, "160109_Mix1_calcurve.sky", out docPath);
        }

        private static SrmDocument InitWatersDocument(TestFilesDir testFilesDir, string fileName, out string docPath)
        {
            docPath = testFilesDir.GetTestPath(fileName);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 1, 12, 12, 24);
            return doc;
        }
    }
}