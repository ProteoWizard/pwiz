/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Load a small Waters results file with ion mobility data and check against curated results.
    /// 
    /// Run it three ways - 
    /// without IMS filter
    /// with measured drift time data from a spectral lib
    /// with collisional cross section data with a drift time predictor
    /// 
    /// Actually it's an mz5 file of the midsection in a larger Waters file
    /// but it still tests the ion mobility code.
    /// 
    /// </summary>
    [TestClass]
    public class WatersImsMseTest : AbstractUnitTestEx
    {
        private const string ZIP_FILE = @"TestData\Results\WatersImsMseTest.zip";

        private enum DriftFilterType { none, library }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTest()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none, IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power);
        }

        [TestMethod]
        public void WatersImsMseLibraryDriftTimesChromatogramTest()
        {
            WatersImsMseChromatogramTest(DriftFilterType.library, IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power);
            WatersImsMseChromatogramTest(DriftFilterType.library, IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.linear_range);
            WatersImsMseChromatogramTest(DriftFilterType.library, IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width);
        }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTestAsSmallMolecules()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none, IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power, RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTestAsSmallMoleculeMasses()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none, IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power, RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        [TestMethod]
        public void WatersImsMseLibraryDriftTimesChromatogramTestAsSmallMolecules()
        {
            WatersImsMseChromatogramTest(DriftFilterType.library, IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power, RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        private void WatersImsMseChromatogramTest(DriftFilterType mode,
            IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType driftWindowWidthCalcType,
            RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules = RefinementSettings.ConvertToSmallMoleculesMode.none)
        {
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none && SkipSmallMoleculeTestVersions())
            {
                return;
            }

            string subdir = (asSmallMolecules == RefinementSettings.ConvertToSmallMoleculesMode.none) ? null : asSmallMolecules.ToString();
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE, subdir);

            bool withDriftTimeFilter = (mode != DriftFilterType.none); // Perform drift time filtering from blib file?
            string docPath;
            SrmDocument document = InitWatersImsMseDocument(TestFilesDir, driftWindowWidthCalcType, withDriftTimeFilter, out docPath);
            AssertEx.IsDocumentState(document, null, 1, 1, 1, 8); // Drift time lib load bumps the doc version, so does small mol conversion
            var documentNoResults = document;
            var listChromatograms = new List<ChromatogramSet>();
            // A small subset of the QC_HDMSE_02_UCA168_3495_082213 data set (RT 21.5-22.5) from Will Thompson
            string mz5Path = "waters-mobility" + ExtensionTestContext.ExtMz5;
            var testModeStr = withDriftTimeFilter ? "with drift times from spectral library" : "without drift time filtering";

            listChromatograms.Add(AssertResult.FindChromatogramSet(document, new MsDataFilePath(mz5Path)) ??
                                    new ChromatogramSet(Path.GetFileName(mz5Path).Replace('.', '_'), new[] { mz5Path }));
            using (var docContainer = new ResultsTestDocumentContainer(document, docPath))
            {
                var doc = docContainer.Document;
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                AssertEx.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                document = docContainer.Document;
            }
            document = AsSmallMoleculeTestUtil.ConvertToSmallMolecules(document, ref docPath, new[] {mz5Path}, asSmallMolecules);
            using (var docContainer = new ResultsTestDocumentContainer(document, docPath))
            {
                float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                double maxHeight = 0;
                var results = document.Settings.MeasuredResults;
                AssertEx.AreEqual(1, document.MoleculePrecursorPairs.Count());
                foreach (var pair in document.MoleculePrecursorPairs)
                {
                    ChromatogramGroupInfo[] chromGroupInfo;
                    AssertEx.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                        tolerance, out chromGroupInfo));
                    AssertEx.AreEqual(1, chromGroupInfo.Length, testModeStr + " chromGroupInfo.Length");
                    var chromGroup = chromGroupInfo[0];
                    int expectedPeaks;
                    if (withDriftTimeFilter)
                        expectedPeaks = 3;
                    else if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)
                        expectedPeaks = 5;
                    else
                        expectedPeaks = 6; // No libraries
                    AssertEx.AreEqual(expectedPeaks, chromGroup.NumPeaks, testModeStr + " expectedPeaks"); // This will be higher if we don't filter on DT
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                    }
                }

                var expectedFilteredMaxHeight =
                    driftWindowWidthCalcType == IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width
                        ? 5814
                        : 5226;
                AssertEx.AreEqual(withDriftTimeFilter ? expectedFilteredMaxHeight : 20075, maxHeight, 1, testModeStr + " maxHeight");  // Without DT filtering, this will be much greater

                // now drill down for specific values
                int nPeptides = 0;
                foreach (var nodePep in document.Molecules.Where(nodePep => !nodePep.Results[0].IsEmpty))
                {
                    // expecting just one peptide result in this small data set
                    if (nodePep.Results[0].Sum(chromInfo => chromInfo.PeakCountRatio > 0 ? 1 : 0) > 0)
                    {
                        AssertEx.AreEqual(21.94865, (double)nodePep.GetMeasuredRetentionTime(0), .0001, testModeStr + " RT");
                        AssertEx.AreEqual(1.0, (double)nodePep.GetPeakCountRatio(0), 0.0001, testModeStr + "peak count ration");
                        nPeptides++;
                    }
                }
                AssertEx.AreEqual(1, nPeptides);

                if (withDriftTimeFilter)
                {
                    AssertPrecursorHighEnergyOffset(document, testModeStr);

                    // Verify that the .imsdb or .blib file goes out in the share zipfile
                    for (int complete = 0; complete <= 1; complete++)
                    {
                        var sharePath =
                            TestFilesDir.GetTestPath(complete == 1 ? "share_complete.zip" : "share_minimized.zip");
                        var share = new SrmDocumentSharing(document, docPath, sharePath,
                            new ShareType(complete == 1, SkylineVersion.CURRENT)); // Explicitly declaring version number forces a save before zip
                        share.Share(new SilentProgressMonitor());

                        var files = share.ListEntries().ToArray();
                        var imsdbFile = "waters-mobility.filtered-scaled.blib";
                        if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none)
                        {
                            var ext = "." + imsdbFile.Split('.').Last();
                            imsdbFile = imsdbFile.Replace(ext, BiblioSpecLiteSpec.DotConvertedToSmallMolecules + ext);
                        }
                        AssertEx.IsTrue(files.Contains(imsdbFile));
                        // And round trip it to make sure we haven't left out any new features in minimized imsdb or blib files
                        share.Extract(new SilentProgressMonitor());
                        using(var cmdline = new CommandLine())
                        {
                            AssertEx.IsTrue(cmdline.OpenSkyFile(share.DocumentPath)); // Handles any path shifts in database files, like our .imsdb file
                            var document2 = cmdline.Document;
                            AssertEx.IsNotNull(document2);

                            AssertEx.IsTrue(docContainer.SetDocument(document2, docContainer.Document, true));
                            docContainer.AssertComplete();

                            document2 = docContainer.Document;
                            var im = document2.Settings.GetIonMobilities(document2.MoleculeLibKeys.ToArray(), new MsDataFilePath(mz5Path));
                            var pep = document2.Molecules.First();
                            var expectedWidth =
                                driftWindowWidthCalcType == IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width
                                    ? 0.13799765403988132
                                    : 0.077224865797235934;
                            foreach (TransitionGroupDocNode nodeGroup in pep.Children)
                            {
                                var centerDriftTime = document2.Settings.GetIonMobilityFilter(
                                    pep, nodeGroup, null, im, null, driftTimeMax);
                                AssertEx.AreEqual(3.86124, centerDriftTime.IonMobilityAndCCS.IonMobility.Mobility.Value, .0001, testModeStr + " ccs");
                                AssertEx.AreEqual(expectedWidth, centerDriftTime.IonMobilityExtractionWindowWidth.Value, .0001, testModeStr + " dtWidth");
                            }
                        }
                    }
                }
            }

            if (withDriftTimeFilter && asSmallMolecules == RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                AssertObservedPeakValuesSurviveCacheRewrites(documentNoResults, mz5Path);
            }
        }

        /// <summary>
        /// The observed IM/CCS of each peak are stored in the peak itself, so every path that rewrites a
        /// cache must keep them: joining the partial caches of an import, removing a file, and minimizing -
        /// into the current format, and into one whose peaks are too small for them, which drops the values
        /// but must keep the peaks.
        /// </summary>
        private void AssertObservedPeakValuesSurviveCacheRewrites(SrmDocument documentNoResults, string mz5Path)
        {
            var dataPaths = new[] { TestFilesDir.GetTestPath(mz5Path), TestFilesDir.GetTestPath("waters-mobility-copy" + ExtensionTestContext.ExtMz5) };
            File.Copy(dataPaths[0], dataPaths[1]);
            var chromatogramSets = dataPaths.Select(path =>
                new ChromatogramSet(Path.GetFileNameWithoutExtension(path), new[] { new MsDataFilePath(path) })).ToList();
            using var docContainer = new ResultsTestDocumentContainer(documentNoResults, TestFilesDir.GetTestPath("observed_peak_values.sky"));
            var docResults = documentNoResults.ChangeMeasuredResults(new MeasuredResults(chromatogramSets));
            AssertEx.IsTrue(docContainer.SetDocument(docResults, documentNoResults, true));
            docContainer.AssertComplete();
            var document = docContainer.Document;

            // Joined from one partial cache per file
            var cache = document.Settings.MeasuredResults.CacheFinal;
            var expectedLines = GetPeakLines(cache, true);
            foreach (var dataPath in dataPaths)
            {
                var fileLines = expectedLines.Where(line => line.StartsWith(Path.GetFileName(dataPath))).ToList();
                AssertEx.IsTrue(fileLines.Any(line => !line.EndsWith(@"|-|-")), "no observed IM in " + dataPath);
            }

            // Removing a file rewrites the cache from the groups of the file kept
            var keepFilePath = cache.CachedFiles.First(file => Equals(file.FilePath.GetFileName(), Path.GetFileName(dataPaths[0]))).FilePath;
            var expectedKeptLines = expectedLines.Where(line => line.StartsWith(Path.GetFileName(dataPaths[0]))).ToList();
            string optimizedPath = TestFilesDir.GetTestPath("optimized" + ChromatogramCache.EXT);
            using (var optimizedCache = cache.OptimizeToPath(optimizedPath, new[] { keepFilePath }, FileStreamManager.Default, null))
            {
                AssertEx.AreEqual(TextUtil.LineSeparate(expectedKeptLines), TextUtil.LineSeparate(GetPeakLines(optimizedCache, true)));
            }
            using (var reloadedCache = LoadCache(optimizedPath, document))
            {
                AssertEx.AreEqual(TextUtil.LineSeparate(expectedKeptLines), TextUtil.LineSeparate(GetPeakLines(reloadedCache, true)));
            }

            using (var minimizedCache = MinimizeCache(document, CacheFormat.CURRENT, "minimized" + ChromatogramCache.EXT))
            {
                AssertEx.AreEqual(TextUtil.LineSeparate(expectedLines), TextUtil.LineSeparate(GetPeakLines(minimizedCache, true)));
            }
            using (var olderFormatCache = MinimizeCache(document, CacheFormat.FromVersion(CacheFormatVersion.Nineteen), "older" + ChromatogramCache.EXT))
            {
                AssertEx.AreEqual(TextUtil.LineSeparate(GetPeakLines(cache, false)), TextUtil.LineSeparate(GetPeakLines(olderFormatCache, true)));
            }
        }

        /// <summary>
        /// One line per peak in the cache: file, precursor, retention time, observed IM and observed CCS
        /// ("-" when not known, or when observed values are not requested).
        /// </summary>
        private static List<string> GetPeakLines(ChromatogramCache cache, bool withObservedValues)
        {
            var lines = new List<string>();
            foreach (var header in cache.ChromGroupHeaderInfos)
            {
                string fileName = cache.CachedFiles[header.FileIndex].FilePath.GetFileName();
                foreach (var peak in cache.ReadPeaks(header))
                {
                    lines.Add(string.Join(@"|", fileName,
                        header.Precursor.RawValue.ToString(@"R", CultureInfo.InvariantCulture),
                        peak.RetentionTime.ToString(@"R", CultureInfo.InvariantCulture),
                        FormatObservedValue(withObservedValues ? peak.ObservedIonMobility : null),
                        FormatObservedValue(withObservedValues ? peak.ObservedCcs : null)));
                }
            }
            return lines;
        }

        private static string FormatObservedValue(float? value)
        {
            return value?.ToString(@"R", CultureInfo.InvariantCulture) ?? @"-";
        }

        private ChromatogramCache MinimizeCache(SrmDocument document, CacheFormat cacheFormat, string fileName)
        {
            string skydPath = TestFilesDir.GetTestPath(fileName);
            var minimizer = document.Settings.MeasuredResults.GetChromCacheMinimizer(document);
            using (var fs = new FileSaver(skydPath))
            using (var fsScans = new FileSaver(skydPath + ChromatogramCache.SCANS_EXT, true))
            using (var fsPeaks = new FileSaver(skydPath + ChromatogramCache.PEAKS_EXT, true))
            using (var fsScores = new FileSaver(skydPath + ChromatogramCache.SCORES_EXT, true))
            {
                fs.Stream = File.OpenWrite(fs.SafeName);
                minimizer.Minimize(new ChromCacheMinimizer.Settings().ChangeCacheFormat(cacheFormat), null, fs.Stream,
                    fsScans.FileStream, fsPeaks.FileStream, fsScores.FileStream);
                fs.Commit();
            }
            return LoadCache(skydPath, document);
        }

        private static ChromatogramCache LoadCache(string skydPath, SrmDocument document)
        {
            return ChromatogramCache.Load(skydPath, new ProgressStatus(),
                new DefaultFileLoadMonitor(new SilentProgressMonitor()), document);
        }

        private static double driftTimeMax = 13.799765403988133; // Known max drift time for this file - use to mimic resolving power logic for test purposes

        /// <summary>
        /// The high-energy IM offset reports use to put MS2 observed IM back in the precursor frame
        /// must be the one extraction applied, in every window mode - including linear range, whose
        /// width math needs the file's maximum IM, which reports do not have.
        /// </summary>
        private static void AssertPrecursorHighEnergyOffset(SrmDocument document, string testModeStr)
        {
            var filePath = document.Settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].FilePath;
            var libraryIonMobilities = document.Settings.GetIonMobilities(document.MoleculeLibKeys.ToArray(), filePath);
            foreach (var pair in document.MoleculePrecursorPairs)
            {
                double expected = document.Settings.GetIonMobilityFilter(pair.NodePep, pair.NodeGroup, null,
                    libraryIonMobilities, null, driftTimeMax).HighEnergyIonMobilityOffset ?? 0;
                AssertEx.AreEqual(expected, ObservedIonMobilityCalculator.GetPrecursorHighEnergyOffset(
                    document.Settings, pair.NodePep, pair.NodeGroup, filePath), testModeStr + " high-energy offset");
            }
        }

        private static SrmDocument InitWatersImsMseDocument(TestFilesDir testFilesDir,
            IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType driftWindowWidthCalcType,
            bool withDriftTimeFilter, 
            out string docPath)
        {
            var skyFile =  "single_no_driftinfo.sky";
            docPath = testFilesDir.GetTestPath(skyFile);
            var cmdline = new CommandLine();
            Assert.IsTrue(cmdline.OpenSkyFile(docPath)); // Handles any path shifts in database files
            SrmDocument doc = cmdline.Document;
            // Cause library load and subsequent document update
            using (var docContainer = new ResultsTestDocumentContainer(null, docPath))
            {
                docContainer.SetDocument(doc, null, true);
                docContainer.AssertComplete();
                doc = docContainer.Document;

                double resolvingPower = 100; // Test was originally written with resolving power 100
                double widthAtDtMax = 2 * driftTimeMax / resolvingPower;
                double fixedWidth = widthAtDtMax / 2;
                var driftTimeWindowWidthCalculator = new IonMobilityWindowWidthCalculator(driftWindowWidthCalcType, resolvingPower, 0, widthAtDtMax, fixedWidth);

                if (withDriftTimeFilter)
                {
                    // Use the bare drift times in the spectral library
                    var librarySpec = new BiblioSpecLiteSpec("drift test",
                        testFilesDir.GetTestPath("waters-mobility.filtered-scaled.blib"));
                    doc = doc.ChangeSettings(
                        doc.Settings.ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] { librarySpec })).
                            ChangeTransitionIonMobilityFiltering(p => p.ChangeFilterWindowWidthCalculator(driftTimeWindowWidthCalculator)).
                            ChangeTransitionIonMobilityFiltering(p => p.ChangeUseSpectralLibraryIonMobilityValues(true))
                    );
                }
                else
                {
                    doc = doc.ChangeSettings(
                        doc.Settings.ChangeTransitionIonMobilityFiltering(im => im.ChangeFilterWindowWidthCalculator(driftTimeWindowWidthCalculator)));
                }
            }
            return doc;
        }
    }
}