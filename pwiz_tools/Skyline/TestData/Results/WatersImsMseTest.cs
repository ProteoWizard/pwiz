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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
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

        private enum DriftFilterType { none, predictor, library }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTest()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power);
        }

        [TestMethod]
        public void WatersImsMsePredictedDriftTimesChromatogramTest()
        {
            WatersImsMseChromatogramTest(DriftFilterType.predictor, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power);
            WatersImsMseChromatogramTest(DriftFilterType.predictor, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.linear_range);
        }

        [TestMethod]
        public void WatersImsMseLibraryDriftTimesChromatogramTest()
        {
            WatersImsMseChromatogramTest(DriftFilterType.library, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power);
            WatersImsMseChromatogramTest(DriftFilterType.library, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.linear_range);
        }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTestAsSmallMolecules()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power, RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTestAsSmallMoleculeMasses()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power, RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        [TestMethod]
        public void WatersImsMsePredictedDriftTimesChromatogramTestAsSmallMolecules()
        {
            WatersImsMseChromatogramTest(DriftFilterType.predictor, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power, RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void WatersImsMseLibraryDriftTimesChromatogramTestAsSmallMolecules()
        {
            WatersImsMseChromatogramTest(DriftFilterType.library, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power, RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        private void WatersImsMseChromatogramTest(DriftFilterType mode,
            IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType driftPeakWidthCalcType,
            RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules = RefinementSettings.ConvertToSmallMoleculesMode.none)
        {
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none && !RunSmallMoleculeTestVersions)
            {
                Console.Write(MSG_SKIPPING_SMALLMOLECULE_TEST_VERSION);
                return;
            }

            string subdir = (asSmallMolecules == RefinementSettings.ConvertToSmallMoleculesMode.none) ? null : asSmallMolecules.ToString();
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE, subdir);

            bool withDriftTimePredictor = (mode == DriftFilterType.predictor); // Load the doc that has a drift time predictor?
            bool withDriftTimeFilter = (mode != DriftFilterType.none); // Perform drift time filtering?  (either with predictor, or with bare times in blib file)
            string docPath;
            SrmDocument document = InitWatersImsMseDocument(testFilesDir, driftPeakWidthCalcType, withDriftTimeFilter, withDriftTimePredictor, out docPath);
            AssertEx.IsDocumentState(document, null, 1, 1, 1, 8); // Drift time lib load bumps the doc version, so does small mol conversion
            var listChromatograms = new List<ChromatogramSet>();
            // A small subset of the QC_HDMSE_02_UCA168_3495_082213 data set (RT 21.5-22.5) from Will Thompson
            string mz5Path = "waters-mobility" + ExtensionTestContext.ExtMz5;
            string testModeStr = withDriftTimePredictor ? "with drift time predictor" : "without drift time info";
            if (withDriftTimeFilter && !withDriftTimePredictor)
            {
                testModeStr = "with drift times from spectral library";
            }

            listChromatograms.Add(AssertResult.FindChromatogramSet(document, new MsDataFilePath(mz5Path)) ??
                                    new ChromatogramSet(Path.GetFileName(mz5Path).Replace('.', '_'), new[] { mz5Path }));
            using (var docContainer = new ResultsTestDocumentContainer(document, docPath))
            {
                var doc = docContainer.Document;
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                Assume.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                document = docContainer.Document;
            }
            document = ConvertToSmallMolecules(document, ref docPath, new[] {mz5Path}, asSmallMolecules);
            using (var docContainer = new ResultsTestDocumentContainer(document, docPath))
            {
                float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                double maxHeight = 0;
                var results = document.Settings.MeasuredResults;
                Assume.AreEqual(1, document.MoleculePrecursorPairs.Count());
                foreach (var pair in document.MoleculePrecursorPairs)
                {
                    ChromatogramGroupInfo[] chromGroupInfo;
                    Assume.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                        tolerance, true, out chromGroupInfo));
                    Assume.AreEqual(1, chromGroupInfo.Length, testModeStr);
                    var chromGroup = chromGroupInfo[0];
                    int expectedPeaks;
                    if (withDriftTimeFilter)
                        expectedPeaks = 3;
                    else if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)
                        expectedPeaks = 5;
                    else
                        expectedPeaks = 6; // No libraries
                    Assume.AreEqual(expectedPeaks, chromGroup.NumPeaks, testModeStr); // This will be higher if we don't filter on DT
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                    }
                }
                Assume.AreEqual(withDriftTimeFilter ? 5226 : 20075, maxHeight, 1, testModeStr);  // Without DT filtering, this will be much greater

                // now drill down for specific values
                int nPeptides = 0;
                foreach (var nodePep in document.Molecules.Where(nodePep => !nodePep.Results[0].IsEmpty))
                {
                    // expecting just one peptide result in this small data set
                    if (nodePep.Results[0].Sum(chromInfo => chromInfo.PeakCountRatio > 0 ? 1 : 0) > 0)
                    {
                        Assume.AreEqual(21.94865, (double)nodePep.GetMeasuredRetentionTime(0), .0001, testModeStr);
                        Assume.AreEqual(1.0, (double)nodePep.GetPeakCountRatio(0), 0.0001, testModeStr);
                        nPeptides++;
                    }
                }
                Assume.AreEqual(1, nPeptides);

                if (withDriftTimePredictor || withDriftTimeFilter)
                {
                    // Verify that the .imdb pr .blib file goes out in the share zipfile
                    for (int complete = 0; complete <= 1; complete++)
                    {
                        var sharePath =
                            testFilesDir.GetTestPath(complete == 1 ? "share_complete.zip" : "share_minimized.zip");
                        var share = new SrmDocumentSharing(document, docPath, sharePath,
                            new ShareType(complete == 1, SkylineVersion.CURRENT)); // Explicitly declaring version number forces a save before zip
                        share.Share(new SilentProgressMonitor());

                        var files = share.ListEntries().ToArray();
                        var imdbFile = withDriftTimePredictor ? "scaled.imdb" : "waters-mobility.filtered-scaled.blib";
                        if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none)
                        {
                            var ext = "." + imdbFile.Split('.').Last();
                            imdbFile = imdbFile.Replace(ext, BiblioSpecLiteSpec.DotConvertedToSmallMolecules + ext);
                        }
                        Assume.IsTrue(files.Contains(imdbFile));
                        // And round trip it to make sure we haven't left out any new features in minimized imdb or blib files
                        share.Extract(new SilentProgressMonitor());
                        using(var cmdline = new CommandLine())
                        {
                            Assume.IsTrue(cmdline.OpenSkyFile(share.DocumentPath)); // Handles any path shifts in database files, like our .imdb file
                            var document2 = cmdline.Document;
                            Assume.IsNotNull(document2);

                            Assume.IsTrue(docContainer.SetDocument(document2, docContainer.Document, true));
                            docContainer.AssertComplete();

                            document2 = docContainer.Document;
                            var im = document2.Settings.GetIonMobilities(document2.MoleculeLibKeys.ToArray(), new MsDataFilePath(mz5Path));
                            var pep = document2.Molecules.First();
                            foreach (TransitionGroupDocNode nodeGroup in pep.Children)
                            {
                                double windowDT;
                                var centerDriftTime = document2.Settings.GetIonMobility(
                                    pep, nodeGroup, null, im, null, driftTimeMax, out windowDT);
                                Assume.AreEqual(3.86124, centerDriftTime.IonMobility.Mobility.Value, .0001, testModeStr);
                                Assume.AreEqual(0.077224865797235934, windowDT, .0001, testModeStr);
                            }
                        }
                    }
                }
            }
        }

        private static double driftTimeMax = 13.799765403988133; // Known max drift time for this file - use to mimic resolving power logic for test purposes

        private static SrmDocument InitWatersImsMseDocument(TestFilesDir testFilesDir,
            IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType driftPeakWidthCalcType,
            bool withDriftTimeFilter, bool withDriftTimePredictor, 
            out string docPath)
        {
            var skyFile = withDriftTimePredictor ? "single_with_driftinfo.sky" : "single_no_driftinfo.sky";
            docPath = testFilesDir.GetTestPath(skyFile);
            var cmdline = new CommandLine();
            Assert.IsTrue(cmdline.OpenSkyFile(docPath)); // Handles any path shifts in database files, like our .imdb file
            SrmDocument doc = cmdline.Document;
            // Cause library load and subsequent document update
            using (var docContainer = new ResultsTestDocumentContainer(null, docPath))
            {
                docContainer.SetDocument(doc, null, true);
                docContainer.AssertComplete();
                doc = docContainer.Document;

                double resolvingPower = 100; // Test was originally written with resolving power 100
                double widthAtDtMax = 2 * driftTimeMax / resolvingPower;
                var driftTimeWindowWidthCalculator = new IonMobilityWindowWidthCalculator(driftPeakWidthCalcType, resolvingPower, 0, widthAtDtMax);

                if (withDriftTimeFilter && !withDriftTimePredictor)
                {
                    // Use the bare drift times in the spectral library
                    var librarySpec = new BiblioSpecLiteSpec("drift test",
                        testFilesDir.GetTestPath("waters-mobility.filtered-scaled.blib"));
                    doc = doc.ChangeSettings(
                        doc.Settings.ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] { librarySpec })).
                            ChangePeptidePrediction(p => p.ChangeLibraryDriftTimesWindowWidthCalculator(driftTimeWindowWidthCalculator)).
                            ChangePeptidePrediction(p => p.ChangeUseLibraryIonMobilityValues(true))
                    );
                }
                else if (withDriftTimeFilter)
                {
                    doc = doc.ChangeSettings(
                        doc.Settings.ChangePeptideSettings(ps => ps.ChangePrediction(
                            ps.Prediction.ChangeDriftTimePredictor(ps.Prediction.IonMobilityPredictor.ChangeDriftTimeWindowWidthCalculator(driftTimeWindowWidthCalculator)))));
                }
            }
            return doc;
        }
    }
}