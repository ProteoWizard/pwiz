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
using System.Text;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
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
    public class WatersImsMseTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"Test\Results\WatersImsMseTest.zip";

        private enum DriftFilterType { none, predictor, library }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTest()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none);
        }

        [TestMethod]
        public void WatersImsMsePredictedDriftTimesChromatogramTest()
        {
            WatersImsMseChromatogramTest(DriftFilterType.predictor);
        }

        [TestMethod]
        public void WatersImsMseLibraryDriftTimesChromatogramTest()
        {
            WatersImsMseChromatogramTest(DriftFilterType.library);
        }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTestAsSmallMolecules()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none, RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void WatersImsMseNoDriftTimesChromatogramTestAsSmallMoleculeMasses()
        {
            WatersImsMseChromatogramTest(DriftFilterType.none, RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        /* TODO bspratt drift time libs for small molecules

        [TestMethod]
        public void WatersImsMsePredictedDriftTimesChromatogramTestAsSmallMolecules()
        {
            WatersImsMseChromatogramTest(DriftFilterType.predictor, true);
        }

        [TestMethod]
        public void WatersImsMseLibraryDriftTimesChromatogramTestAsSmallMolecules()
        {
            WatersImsMseChromatogramTest(DriftFilterType.library, true);
        }
          
         */

        private void WatersImsMseChromatogramTest(DriftFilterType mode,
            RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules = RefinementSettings.ConvertToSmallMoleculesMode.none)
        {
            string subdir = (asSmallMolecules == RefinementSettings.ConvertToSmallMoleculesMode.none) ? null : asSmallMolecules.ToString();
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE, subdir);
            TestSmallMolecules = false; // Don't need that extra magic node

            bool withDriftTimePredictor = (mode == DriftFilterType.predictor); // Load the doc that has a drift time predictor?
            bool withDriftTimeFilter = (mode != DriftFilterType.none); // Perform drift time filtering?  (either with predictor, or with bare times in blib file)
            string docPath;
            SrmDocument document = InitWatersImsMseDocument(testFilesDir, withDriftTimePredictor ? "single_with_driftinfo.sky" : "single_no_driftinfo.sky", asSmallMolecules, out docPath);
            AssertEx.IsDocumentState(document, (withDriftTimePredictor || (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none)) ? 1 : 0, 1, 1, 1, 8); // Drift time lib load bumps the doc version
            var docContainer = new ResultsTestDocumentContainer(document, docPath);
            var doc = docContainer.Document;
            var docOriginal = doc;

            string testModeStr = withDriftTimePredictor ? "with drift time predictor" : "without drift time info";

            if (withDriftTimeFilter && !withDriftTimePredictor)
            {
                // Use the bare drift times in the spectral library
                var librarySpec = new BiblioSpecLiteSpec("drift test",
                                                    testFilesDir.GetTestPath("mse-mobility.filtered-scaled.blib"));
                doc = doc.ChangeSettings(
                    doc.Settings.ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] { librarySpec })).
                    ChangePeptidePrediction(p => p.ChangeLibraryDriftTimesResolvingPower(100)).
                    ChangePeptidePrediction(p => p.ChangeUseLibraryDriftTimes(true))
                    );
                testModeStr = "with drift times from spectral library";
            }

            var listChromatograms = new List<ChromatogramSet>();
            // A small subset of the QC_HDMSE_02_UCA168_3495_082213 data set (RT 21.5-22.5) from Will Thompson
            const string path = @"waters-mobility.mz5";
            listChromatograms.Add(AssertResult.FindChromatogramSet(doc, new MsDataFilePath(path)) ??
                                    new ChromatogramSet(Path.GetFileName(path).Replace('.', '_'), new[] { path }));
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(docContainer.SetDocument(docResults, docOriginal, true));
            docContainer.AssertComplete();
            document = docContainer.Document;

            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = document.Settings.MeasuredResults;
            Assert.AreEqual(1, document.MoleculePrecursorPairs.Count());
            foreach (var pair in document.MoleculePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out chromGroupInfo));
                Assert.AreEqual(1, chromGroupInfo.Length, testModeStr);
                var chromGroup = chromGroupInfo[0];
                var expectedPeaks = ((asSmallMolecules == RefinementSettings.ConvertToSmallMoleculesMode.masses_only) ? 6 : 5);
                Assert.AreEqual(withDriftTimeFilter ? 3 : expectedPeaks, chromGroup.NumPeaks, testModeStr); // This will be higher if we don't filter on DT
                foreach (var tranInfo in chromGroup.TransitionPointSets)
                {
                    maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                }
            }
            Assert.AreEqual(withDriftTimeFilter? 5226 : 20075 , maxHeight, 1, testModeStr);  // Without DT filtering, this will be much greater

            // now drill down for specific values
            int nPeptides = 0;
            foreach (var nodePep in document.Molecules.Where(nodePep => nodePep.Results[0] != null))
            {
                // expecting just one peptide result in this small data set
                if (nodePep.Results[0].Sum(chromInfo => chromInfo.PeakCountRatio > 0 ? 1 : 0) > 0)
                {
                    Assert.AreEqual(21.94865, (double)nodePep.GetMeasuredRetentionTime(0), .0001, testModeStr);
                    Assert.AreEqual(1.0, (double)nodePep.GetPeakCountRatio(0), 0.0001, testModeStr);
                    nPeptides++;
                }
            }
            Assert.AreEqual(1, nPeptides);

            if (withDriftTimePredictor || withDriftTimeFilter)
            {
                // Verify that the .imdb pr .blib file goes out in the share zipfile
                for (int complete = 0; complete <= 1; complete++)
                {
                    var sharePath = testFilesDir.GetTestPath(complete==1?"share_complete.zip":"share_minimized.zip");
                    var share = new SrmDocumentSharing(document, docPath, sharePath, complete==1);
                    using (var longWaitDlg = new LongWaitDlg
                    { 
                        // ReSharper disable once LocalizableElement
                        Text = "unit test WatersImsTest -- sharing document",
                    })
                    {
                        longWaitDlg.PerformWork(null, 1000, share.Share);
                        Assert.IsFalse(longWaitDlg.IsCanceled);
                    } 

                    var files = share.ListEntries().ToArray();
                    Assert.IsTrue(files.Contains(withDriftTimePredictor ? "scaled.imdb" : "mse-mobility.filtered-scaled.blib"));
                    // And round trip it to make sure we haven't left out any new features in minimized imdb or blib files
                    using (var longWaitDlg = new LongWaitDlg
                    {
                        // ReSharper disable once LocalizableElement
                        Text = "unit test WatersImsTest",
                    })
                    {
                        longWaitDlg.PerformWork(null, 1000, share.Extract);
                        Assert.IsFalse(longWaitDlg.IsCanceled);
                    }
                    using (TextReader reader = new StreamReader(share.DocumentPath))
                    {
                        XmlSerializer documentSerializer = new XmlSerializer(typeof(SrmDocument));
                        var document2 = (SrmDocument) documentSerializer.Deserialize(reader);
                        Assert.IsNotNull(document2);
                        var im = document.Settings.GetIonMobilities(new MsDataFilePath(path));
                        var pep = document2.Molecules.First();
                        foreach (TransitionGroupDocNode nodeGroup in pep.Children)
                        {
                            double windowDT;
                            var centerDriftTime = document.Settings.PeptideSettings.Prediction.GetDriftTime(
                                                       pep, nodeGroup, im, out windowDT);
                            Assert.AreEqual(3.86124, centerDriftTime.DriftTimeMsec(false) ?? 0, .0001, testModeStr);
                            Assert.AreEqual(0.077224865797235934, windowDT, .0001, testModeStr);
                        }
                    }
                }
            }

            // Release file handles
            docContainer.Release();
            testFilesDir.Dispose();
            string cachePath = ChromatogramCache.FinalPathForName(docPath, null);
            FileEx.SafeDelete(cachePath);
        }

        private static SrmDocument InitWatersImsMseDocument(TestFilesDir testFilesDir, string skyFile, 
            RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules, 
            out string docPath)
        {
            docPath = testFilesDir.GetTestPath(skyFile);
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new CommandStatusWriter(new StringWriter(consoleBuffer));
            var cmdline = new CommandLine(consoleOutput);
            Assert.IsTrue(cmdline.OpenSkyFile(docPath)); // Handles any path shifts in database files, like our .imdb file
            SrmDocument doc = cmdline.Document;
            var refine = new RefinementSettings();
            doc = refine.ConvertToSmallMolecules(doc, asSmallMolecules);
            return doc;
        }
    }
}