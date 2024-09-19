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
        }

        private static double driftTimeMax = 13.799765403988133; // Known max drift time for this file - use to mimic resolving power logic for test purposes

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