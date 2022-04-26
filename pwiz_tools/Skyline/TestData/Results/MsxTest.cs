/*
 * Original author: Jarrett Egertson <jegertso .at .u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    [TestClass]
    public class MsxTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\MsxTest.zip";

        private const int TEST_SPECTRUM = 105;
        private const int TEST_SPECTRUM_OVERLAP = 67;

        [TestMethod]
        public void TestDemux()
        {
            DoTestDemux(false);
        }

        [TestMethod]
        public void TestDemuxAsSmallMolecules()
        {
            if (SkipSmallMoleculeTestVersions())
            {
                return;
            }

            DoTestDemux(true);
        }

        public void DoTestDemux(bool asSmallMolecules)
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPathMsx = testFilesDir.GetTestPath("MsxTest.sky");
            string dataPathMsx = testFilesDir.GetTestPath("MsxTest.mzML");
            string cachePathMsx = ChromatogramCache.FinalPathForName(docPathMsx, null);
            FileEx.SafeDelete(cachePathMsx);
            SrmDocument docMsx = ResultsUtil.DeserializeDocument(docPathMsx);
            if (asSmallMolecules)
            {
                var refine = new RefinementSettings();
                docMsx = refine.ConvertToSmallMolecules(docMsx, testFilesDir.FullPath);
            }
            var fullScanInitialMsx = docMsx.Settings.TransitionSettings.FullScan;
            Assert.IsTrue(fullScanInitialMsx.IsEnabledMsMs);
            
            TestMsx(docMsx, dataPathMsx);

            string docPathOverlap = testFilesDir.GetTestPath("OverlapTest.sky");
            string dataPathOverlap = testFilesDir.GetTestPath("OverlapTest.mzML");
            string cachePathOverlap = ChromatogramCache.FinalPathForName(docPathOverlap, null);
            FileEx.SafeDelete(cachePathOverlap);
            SrmDocument docOverlap = ResultsUtil.DeserializeDocument(docPathOverlap);
            if (asSmallMolecules)
            {
                var refine = new RefinementSettings();
                docOverlap = refine.ConvertToSmallMolecules(docOverlap, testFilesDir.FullPath);
            }
            var fullScanInitialOverlap = docMsx.Settings.TransitionSettings.FullScan;
            Assert.IsTrue(fullScanInitialOverlap.IsEnabledMsMs);
            TestOverlap(docOverlap,dataPathOverlap);
        }

        public void TestOverlap(SrmDocument doc, string dataPath)
        {
            // Load the file and check MsDataFileImpl
            using (var file = new MsDataFileImpl(dataPath))
            {
                var filter = new SpectrumFilter(doc, null, null);
                Assert.IsTrue(filter.EnabledMsMs);
                var demultiplexer = new OverlapDemultiplexer(file, filter);
                demultiplexer.ForceInitializeFile();

                // Check that the demultiplexer found the correct multiplexing parameters
                Assert.AreEqual(1, demultiplexer.IsoWindowsPerScan);
                Assert.AreEqual(40, demultiplexer.NumIsoWindows);
                Assert.AreEqual(41, demultiplexer.NumDeconvRegions);

                var isoMapper = demultiplexer.IsoMapperTest;

                // Basic checks of IsolationWindowMapper
                TestIsolationWindowMapper(isoMapper, file, 20.009);
                // Checks of overlap-specific functionality in IsolationWindowMapper
                TestOverlapIsolationWindowMapper(isoMapper, file);
                var transBinner = new TransitionBinner(filter, isoMapper);

                // Test creation of a transition binner from a spectrum filter
                TestTransitionBinnerFromFilter(transBinner, isoMapper);
                var testSpectrum = file.GetSpectrum(TEST_SPECTRUM_OVERLAP);
                // Generate a transition binner containing a lot of tough cases with
                // overlapping transitions and test
                double[] binnedExpected =
                    {
                        0.0, 0.0, 0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0
                    };
                var transBinnerOverlap = TestTransitionBinnerOverlap(testSpectrum, isoMapper, binnedExpected);

                TestSpectrumProcessor(transBinnerOverlap, isoMapper, file, TEST_SPECTRUM_OVERLAP);
                int[] intensityIndices = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 290, 291, 292, 293, 294, 295, 296};
                double[] intensityValues =
                    {
                        0.0, 0.0, 0.0, 0.0, 4545.85, 15660.49,
                        35050.01, 56321.66, 62715.75, 43598.31, 23179.42,
                        2745.94, 3870.54, 4060.16, 3148.17, 1656.38,
                        0.0, 0.0
                    };
                // Test demultiplexing of a real spectrum
                TestSpectrumDemultiplex(demultiplexer, TEST_SPECTRUM_OVERLAP, testSpectrum, intensityIndices,
                                        intensityValues, 0);
            }
        }

        public void TestMsx(SrmDocument doc, string dataPath)
        {
            // Load the file and check MsDataFileImpl
            using (var file = new MsDataFileImpl(dataPath))
            {
                var filter = new SpectrumFilter(doc, null, null);
                Assert.IsTrue(filter.EnabledMsMs);
                var demultiplexer = new MsxDemultiplexer(file, filter);
                demultiplexer.ForceInitializeFile();

                // Check that the demultiplexer found the correct multiplexing parameters
                Assert.AreEqual(5, demultiplexer.IsoWindowsPerScan);
                Assert.AreEqual(100, demultiplexer.NumIsoWindows);
                Assert.AreEqual(20, demultiplexer.DutyCycleLength);

                var isoMapper = demultiplexer.IsoMapperTest;
                Assert.AreEqual(100, isoMapper.NumWindows);

                // Check the isolation window mapper responsible for mapping each unique isolation
                // window detected in the file to a unique index
                TestIsolationWindowMapper(isoMapper, file, 4.00182);

                var transBinner = new TransitionBinner(filter, isoMapper);

                // Test creation of a transition binner from a spectrum filter
                TestTransitionBinnerFromFilter(transBinner, isoMapper);
                var testSpectrum = file.GetSpectrum(TEST_SPECTRUM);
                // Generate a transition binner containing a lot of tough cases with
                // overlapping transitions and test    
                double[] binnedExpected =
                {
                    0.0, 25160.11261, 18254.06375, 18254.06375, 18254.06375,
                    11090.00577, 19780.18628, 19780.18628
                };
                var transBinnerOverlap = TestTransitionBinnerOverlap(testSpectrum, isoMapper, binnedExpected);

                TestSpectrumProcessor(transBinnerOverlap, isoMapper, file, TEST_SPECTRUM);

                TestPeakIntensityCorrection(testSpectrum, isoMapper,
                                            demultiplexer);
                int[] intensityIndices = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 290, 291, 292, 293, 294, 295, 296 };
                double[] intensityValues =
                {
                    0.0, 0.0, 0.0, 0.0, 142.95, 349.75, 
                    542.87, 511.77, 248.4, 0.0, 49.28,
                    1033.65, 278.56, 0.0, 0.0, 0.0,
                    0.0, 0.0
                };
                // Test demultiplexing of a real spectrum
                TestSpectrumDemultiplex(demultiplexer, TEST_SPECTRUM, testSpectrum, intensityIndices, intensityValues, 0);
            }
        }

        private static void TestOverlapIsolationWindowMapper(AbstractIsoWindowMapper isoMapper, MsDataFileImpl file)
        {
            // ReSharper disable once CollectionNeverQueried.Local
            var isoWinList = new List<IsoWin>();  // Useful for debugging
            Assert.AreEqual(40,isoMapper.NumWindows);
            Assert.AreEqual(41,isoMapper.NumDeconvRegions);
            // Check that each isolation window has two overlap regions with mz values that are contained in it
            for (int isoIndex = 0; isoIndex < isoMapper.NumWindows; ++isoIndex)
            {
                IsoWin isoWin = isoMapper.GetIsolationWindow(isoIndex);
                isoWinList.Add(isoWin);
                Assert.AreEqual(2, isoWin.DeconvRegions.Count);
                for (int i = 0; i < isoWin.DeconvRegions.Count; ++i)
                {
                    // Check self-consistency of each deconvRegion in the isolation window
                    var deconvRegion = isoWin.DeconvRegions[i];
                    var deconvIndex = deconvRegion.Id;
                    Assert.AreEqual(deconvRegion.Start, isoMapper.GetDeconvRegion(deconvIndex).Start);
                    Assert.AreEqual(deconvRegion.Stop, isoMapper.GetDeconvRegion(deconvIndex).Stop);
                    Assert.AreEqual(deconvRegion.Id, isoMapper.GetDeconvRegion(deconvIndex).Id);
                    Assert.IsTrue(isoWin.Contains(deconvRegion));
                    Assert.IsTrue(isoWin.Start <= deconvRegion.Start && isoWin.Stop >= deconvRegion.Stop);
                    double deconvRegionCenter = (deconvRegion.StartMz + deconvRegion.StopMz) / 2.0;
                    int newDeconvIndex;
                    isoMapper.TryGetDeconvFromMz(deconvRegionCenter, out newDeconvIndex);
                    Assert.AreEqual(deconvIndex, newDeconvIndex);
                    int[] isosForDeconv;
                    isoMapper.TryGetIsosForDeconv(deconvIndex, out isosForDeconv);
                    // 2 isolation windows cover this deconv region, unless it's at beginning or end
                    int deconvInIsos = deconvRegion.Id == 0 || deconvRegion.Id == isoMapper.NumDeconvRegions - 1 ? 1 : 2;
                    Assert.IsTrue(isosForDeconv.Length == deconvInIsos);
                    foreach (var isoForDeconv in isosForDeconv)
                    {
                        IsoWin isoWindow = isoMapper.GetIsolationWindow(isoForDeconv);
                        Assert.IsTrue(isoWindow.Contains(deconvRegion));
                    }
                }
            }

        }

        private static void TestIsolationWindowMapper(AbstractIsoWindowMapper isoMapper, MsDataFileImpl file, double width)
        {
            // Check the first five spectra in the file
            for (int checkIndex = 0 ; checkIndex < 5; ++checkIndex)
            {
                var spectrum = file.GetSpectrum(checkIndex);
                var precursors = spectrum.Precursors;
                List<int> isoIndices = new List<int>();
                // Make sure that each precursor read from the file is included in the isolation
                // window mapper
                foreach (var precursor in precursors)
                {
                    int index;
                    Assert.IsTrue(precursor.IsolationMz.HasValue);
                    Assert.IsTrue(isoMapper.TryGetWindowIndex(precursor.IsolationMz.Value, out index));
                    isoIndices.Add(index);
                }
                int[] isoIndicesFromMapper;
                int[] overlapIndicesFromMapper;
                double[] mask = new double[isoMapper.NumWindows];
                // Make sure the isolation windows called from GetWindowMask match those
                // extracted from the spectrum manually above using TryGetWindowIndex
                isoMapper.GetWindowMask(spectrum, out isoIndicesFromMapper, out overlapIndicesFromMapper, ref mask);
                Assert.AreEqual(isoIndices.Count,isoIndicesFromMapper.Length);
                foreach (int index in isoIndicesFromMapper)
                {
                    Assert.IsTrue(isoIndices.Contains(index));
                }
                // Make sure the overlapIndicesFromMapper matches the mask
                for (int i = 0; i < mask.Length; ++i)
                {
                    if (mask[i] < 0.5)
                        Assert.AreEqual(0.0, mask[i]);
                    else
                    {
                        Assert.AreEqual(1.0, mask[i]);
                        Assert.IsTrue(overlapIndicesFromMapper.Contains(i));
                    }
                }
            }

            var testMzs = new[] {553.49, 623.7, 859, 658.55, 768.7, 621.52};
            // For each test m/z, get the matching isolation window from isoMapper and
            // make sure the m/z does indeed fall within the window
            foreach (var checkMz in testMzs)
            {
                int windowIndex;
                Assert.IsTrue(isoMapper.TryGetWindowFromMz(checkMz, out windowIndex));
                var matchWindowCenterMz = isoMapper.GetPrecursor(windowIndex);
                Assert.IsTrue(matchWindowCenterMz.IsolationMz.HasValue);
                // Check that mz is within window (width/2 )
                Assert.IsTrue(Math.Abs(matchWindowCenterMz.IsolationMz.Value - checkMz) < width/2);
            }

            // Test out of range precursors
            int dummyIndex;
            Assert.IsFalse(isoMapper.TryGetDeconvFromMz(315.25, out dummyIndex));
            Assert.AreEqual(-1, dummyIndex);
            Assert.IsFalse(isoMapper.TryGetDeconvFromMz(1005.2, out dummyIndex));
            Assert.AreEqual(-1, dummyIndex);
        }

        private static void TestTransitionBinnerFromFilter(TransitionBinner transBinner, 
            AbstractIsoWindowMapper isoMapper)
        {
            // Test that the transition binner interpreted the spectrum filter correctly
            // here are the expected precursor -> transition pairs (precursors[0] has
            // transitions in transitions[0])
            double[] precursors = {586.8479, 602.6211};
            double[][] transitions =
            {
                new[]
                {
                    1002.5830, 889.4989, 790.4305, 733.4090,
                    676.3876, 589.3556, 488.3079, 375.2238
                },
                new[]
                {
                    1465.6376, 1305.6070, 1176.5644, 1047.5218, 861.4425, 
                    748.3584, 633.3315, 520.2474, 433.2154, 346.1833
                }
            };

            // Test the mapping of precursor -> transitions
            for (int testIndex = 0; testIndex < precursors.Length; ++testIndex)
            {
                var precursor = precursors[testIndex];
                int deconvIndex;
                // Make sure the isolation mapper has this precursor
                Assert.IsTrue(isoMapper.TryGetDeconvFromMz(precursor, out deconvIndex));
                Assert.AreNotEqual(-1, deconvIndex);
                var expectedTransitions = transitions[testIndex];
                // TransitionBinsFromBinner is a list of indices for transitions stored by transBinner
                // that map to the precursor
                var transitionBinsFromBinner = transBinner.BinsForDeconvWindows(new[] { deconvIndex });
                Assert.AreEqual(expectedTransitions.Length, transitionBinsFromBinner.Count);
                // This will be populated with bins for the transitions we know should be in this
                // precursor as explicitly defined above in precursors[] and transitions[][]
                var expectedTransitionBins = new HashSet<int>();
                var binIndicesSet = new HashSet<int>(transitionBinsFromBinner);
                // For each transitions center m/z that's supposed to map to this precursor
                foreach (var transition in expectedTransitions)
                {
                    // What bin does this transition fall into?
                    var bins = transBinner.BinsFromValues(new[]{transition}, true).ToArray();

                    // Each transition maps to a single transition bin in this test
                    // m/z values mapping to multiple (overlapping) transitions is tested in
                    // TestTransitionBinnerOverlap
                    Assert.AreEqual(1,bins.Length);
                    var bin = bins[0].Value;
                    // Does this transition actually fall into the returned bin?
                    Assert.IsTrue(transBinner.TransInfoFromBin(bin).ContainsMz(transition));
                    // Was this bin correctly mapped to the precursor?
                    Assert.IsTrue(transitionBinsFromBinner.Contains(bin));
                    // This transition falls into this bin, and we expected this bin to map to the precursor
                    // being tested so add it to expectedTransitionBins
                    expectedTransitionBins.Add(bin);
                }
                var setComparer = HashSet<int>.CreateSetComparer();
                Assert.IsTrue(setComparer.Equals(expectedTransitionBins, binIndicesSet));
            }

            // Check BinInPrecursor
            // Positive control
            // Bin for 1465.6376 is in precursor 602.6211
            var binsFromValue = transBinner.BinsFromValues(new[] { 1465.6376 }, true).ToArray();
            Assert.AreEqual(binsFromValue.Length, 1);
            var binTwo = binsFromValue[0].Value;
            int deconvIndexTwo;
            Assert.IsTrue(isoMapper.TryGetDeconvFromMz(602.6211, out deconvIndexTwo));
            Assert.IsTrue(transBinner.BinInDeconvWindow(binTwo, deconvIndexTwo));

            // Negative control: and is not in precursor 586.8479
            Assert.IsTrue(isoMapper.TryGetDeconvFromMz(586.8479, out deconvIndexTwo));
            Assert.IsFalse(transBinner.BinInDeconvWindow(binTwo, deconvIndexTwo));
        }

        private static TransitionBinner TestTransitionBinnerOverlap(MsDataSpectrum spectrum, 
            AbstractIsoWindowMapper isoMapper, double[] binnedIntensities)
        {
            // Multiple transitions overlapping the same peak from the same precursor
            var precursorList = new List<double>();
            var transitionList = new List<KeyValuePair<double, double>>();
            precursorList.Add(710.5);                                 //transition: start - stop     index
            transitionList.Add(new KeyValuePair<double, double>(380.19,1.0));   // 379.69-380.69     Bin3
            precursorList.Add(710.5);
            transitionList.Add(new KeyValuePair<double, double>(380.44,1.0));   // 379.94-380.94     Bin4
            precursorList.Add(710.5);
            transitionList.Add(new KeyValuePair<double, double>(380.49,0.6));   // 380.19-380.79     Bin5
            // And a non-overlapping transition for good measure
            precursorList.Add(710.5);
            transitionList.Add(new KeyValuePair<double, double>(389.19,0.3));   // 389.04 - 389.34   Bin7 
            // And a transition that won't have any peaks in the spectrum
            precursorList.Add(710.5);
            transitionList.Add(new KeyValuePair<double, double>(377.0,0.7));    // 376.65-377.35     Bin0

            // And now another precursor with some of the transitions overlapping the first
            precursorList.Add(595.0);
            transitionList.Add(new KeyValuePair<double, double>(380.0,1.0));    // 379.5 - 380.5     Bin2
            precursorList.Add(595.0);
            transitionList.Add(new KeyValuePair<double, double>(379.55,1.0));   // 379.05 - 380.05   Bin1
            precursorList.Add(595.0);
            transitionList.Add(new KeyValuePair<double, double>(389.15, 0.4));  // 388.95 - 389.35   Bin6

            var transitionBinner = new TransitionBinner(precursorList, transitionList, isoMapper);

            double[] binnedData = new double[transitionList.Count];
            transitionBinner.BinData(spectrum.Mzs, spectrum.Intensities, ref binnedData);

            // Test that m/z, intensity pairs were binned correctly into their transitions
            for (int i = 0; i< binnedData.Length; ++i)
            {
                Assert.AreEqual(binnedIntensities[i], binnedData[i], 0.001);
            }

            // Get the precursor index for this precursor
            // make sure the precursor -> transition mapping is working for the first precursor
            int windowIndexOne;
            Assert.IsTrue(isoMapper.TryGetDeconvFromMz(710.5, out windowIndexOne));
            // Use the precursor index to get the bins mapping to this precursor
            var precursorBins = new HashSet<int>(transitionBinner.BinsForDeconvWindows(new[] {windowIndexOne}));
            var expectedPrecursorBins = new HashSet<int>(new[]{0,3,4,5,7});
            var setComparer = HashSet<int>.CreateSetComparer();
            Assert.IsTrue(setComparer.Equals(expectedPrecursorBins, precursorBins));

            // Make sure the precursor -> transition mapping is working for the second precursor
            int windowIndexTwo;
            Assert.IsTrue(isoMapper.TryGetDeconvFromMz(595.0, out windowIndexTwo));
            precursorBins = new HashSet<int>(transitionBinner.BinsForDeconvWindows(new[]{windowIndexOne, windowIndexTwo}));
            expectedPrecursorBins = new HashSet<int>(new[]{0,1,2,3,4,5,6,7});
            Assert.IsTrue(setComparer.Equals(expectedPrecursorBins, precursorBins));

            // m/z values to test mapping of m/z value -> multiplex matching transition bins
            var queryVals = new[] {252.0, 377.0, 379.92, 379.95};
            var bins = transitionBinner.BinsFromValues(queryVals, true).ToList();

            var expectedBins = new List<KeyValuePair<int, int>>
                                   {
                                       new KeyValuePair<int, int>(1, 0),
                                       new KeyValuePair<int, int>(2, 1),
                                       new KeyValuePair<int, int>(2, 2),
                                       new KeyValuePair<int, int>(2, 3),
                                       new KeyValuePair<int, int>(3, 1),
                                       new KeyValuePair<int, int>(3, 2),
                                       new KeyValuePair<int, int>(3, 3),
                                       new KeyValuePair<int, int>(3, 4)
                                   };
            AssertEx.AreEqualDeep(expectedBins, bins);
            Assert.AreEqual(376.65, transitionBinner.LowerValueFromBin(0),0.0001);
            Assert.AreEqual(377.35, transitionBinner.UpperValueFromBin(0),0.0001);
            Assert.AreEqual(377.0, transitionBinner.CenterValueFromBin(0),0.0001);
            return transitionBinner;
        }

        private static void TestSpectrumProcessor(TransitionBinner transBinner,AbstractIsoWindowMapper isoMapper, 
            MsDataFileImpl file, int testSpectrumNum)
        {
            int lastSpectrum = testSpectrumNum + 60;
            int firstSpectrum = testSpectrumNum - 60;
            var spectrumProcessor = new SpectrumProcessor(lastSpectrum + 1, isoMapper, transBinner);

            // Make a new spectrum processor using the testing constructor that takes in
            // a TransitionBinner, which will be the one used for overlap
            // Add the spectra to the cache +/- 60 from the spectrum of interest
            for (int i = firstSpectrum; i <= lastSpectrum; ++i)
            {
                var spectrum = file.GetSpectrum(i);
                spectrumProcessor.AddSpectrum(i, spectrum);
            }

            // Check that the caching worked correctly
            for (int spectrumIndex = firstSpectrum; spectrumIndex <= lastSpectrum; ++spectrumIndex)
            {
                ScanCached specData;
                Assert.IsTrue(spectrumProcessor.TryGetSpectrum(spectrumIndex, out specData));

                // Check that the cached processing results match the output of
                // redoing the processing manually by extracting the spectrum from
                // the file again and using transBinner to bin the data
                var testSpectrum = file.GetSpectrum(spectrumIndex);
                double[] expectedBinnedData = new double[transBinner.NumBins];
                transBinner.BinData(testSpectrum.Mzs, testSpectrum.Intensities, ref expectedBinnedData);
                ScanCached testSpecData;
                spectrumProcessor.TryGetSpectrum(spectrumIndex, out testSpecData);
                var seenBinnedData = testSpecData.Data;
                Assert.AreEqual(expectedBinnedData.Length, seenBinnedData.Length);
                for (int i = 0; i < expectedBinnedData.Length; ++i)
                {
                    Assert.AreEqual(expectedBinnedData[i], seenBinnedData[i], 0.0001);
                }
            }
        }


        /// <summary>
        /// generates a fake deconvolution solution for the given test spectrum
        /// in order to make sure that the deconvolution results are being applied
        /// correctly to scale peaks in the test spectrum
        /// </summary>
        private static void TestPeakIntensityCorrection(MsDataSpectrum testSpectrum,
            AbstractIsoWindowMapper isoMapper,
            AbstractDemultiplexer demultiplexer)
        {
            // Store a reference to the old spectrum processor
            var oldProcessor = demultiplexer.SpectrumProcessor;
            MsxDeconvSolverHandler db = new MsxDeconvSolverHandler(100, 120, 35);
            int[] isoIndices;
            int[] deconvIndices;
            double[] mask = new double[100];
            isoMapper.GetWindowMask(testSpectrum, out isoIndices, out deconvIndices, ref mask);
            db.SetDeconvIndices(deconvIndices);
            Assert.IsTrue(deconvIndices.Contains(50));
            Assert.IsTrue(deconvIndices.Contains(47));
            Assert.IsTrue(deconvIndices.Contains(15));
            Assert.IsTrue(deconvIndices.Contains(17));
            Assert.IsTrue(deconvIndices.Contains(74));
            Assert.AreEqual(5, deconvIndices.Length);

            // Prepare the transition binner with a precursor containing transitions overlapping
            // peaks in the test spectrum
            List<double> precursors = new List<double>();
            List<KeyValuePair<double, double>> transitions = new List<KeyValuePair<double, double>>();
            var precVals = new[] {710.57};
            var transMzs = new[] {372.18, 373.1588, 373.1794, 375.2, 379.23, 387.19};
            var transWidths = new[] {0.3, 0.04, 0.02, 1.0, 1.0, 1.0};
            foreach (var precMz in precVals)
            {
                for (int i = 0; i < isoIndices.Length; ++i)
                {
                    var transMz = transMzs[i];
                    var transWidth = transWidths[i];
                    precursors.Add(precMz);
                    transitions.Add(new KeyValuePair<double, double>(transMz, transWidth));
                }
            }

            var transBinner = new TransitionBinner(precursors, transitions, isoMapper);
            var binIndicesSet = new HashSet<int>(transBinner.BinsForDeconvWindows(deconvIndices));

            double[][] deconvIntensities = new double[deconvIndices.Length][];
            double[][] deconvMzs = new double[deconvIndices.Length][];

            for (int i = 0; i < deconvIndices.Length; ++i)
            {
                deconvIntensities[i] = new double[testSpectrum.Mzs.Length];
                deconvMzs[i] = new double[testSpectrum.Mzs.Length];
            }

            List<int> binIndicesList = binIndicesSet.ToList();
            binIndicesList.Sort();

            // Use each transition bin index to test a different possible
            // type of demultiplexed solution
            int numDeMultiplexedTrans = binIndicesSet.Count;
            double[] peakSums = new double[numDeMultiplexedTrans];
            // Initialize the deconvBlock
            db.Solution.Resize(100, binIndicesList.Count);
            db.Solution.Clear();
            // Transition index 0, each row is a precursor
            // the values for each precursor are the relative contribution
            // of that precursor to the observed (convolved) intensity of the peak
            db.Solution.Matrix[2, 0] = 0.0;
            db.Solution.Matrix[1, 0] = 0.0;
            db.Solution.Matrix[0, 0] = 0.0;
            db.Solution.Matrix[3, 0] = 0.0;
            db.Solution.Matrix[4, 0] = 0.0;
            peakSums[0] = 0.0;
            // Transition index 1, each row is a precursor
            db.Solution.Matrix[2, 1] = 1.0;
            db.Solution.Matrix[1, 1] = 1.0;
            db.Solution.Matrix[0, 1] = 2.0;
            db.Solution.Matrix[3, 1] = 1.0;
            db.Solution.Matrix[4, 1] = 1.0;
            peakSums[1] = 6.0;
            // Transition index 2, each row is a precursor
            db.Solution.Matrix[2, 2] = 2.0;
            db.Solution.Matrix[1, 2] = 1.0;
            db.Solution.Matrix[0, 2] = 3.0;
            db.Solution.Matrix[3, 2] = 3.0;
            db.Solution.Matrix[4, 2] = 1.0;
            peakSums[2] = 10.0;
            // Transition index 3, each row is a precursor
            db.Solution.Matrix[2, 3] = 0.0;
            db.Solution.Matrix[1, 3] = 0.0;
            db.Solution.Matrix[0, 3] = 1.0;
            db.Solution.Matrix[3, 3] = 0.0;
            db.Solution.Matrix[4, 3] = 0.0;
            peakSums[3] = 1.0;
            // Transition index 4, each row is a precursor
            db.Solution.Matrix[2, 4] = 1.0;
            db.Solution.Matrix[1, 4] = 1.0;
            db.Solution.Matrix[0, 4] = 1.0;
            db.Solution.Matrix[3, 4] = 1.0;
            db.Solution.Matrix[4, 4] = 1.0;
            peakSums[4] = 5.0;

            // Transition bin index (in transBinner) -> solution transition index (0-4 above), needed for
            // CorrectPeakIntensities call
            Dictionary<int, int> binToDeconvIndex = new Dictionary<int, int>(numDeMultiplexedTrans);
            int numBinIndices = binIndicesList.Count;
            for (int i = 0; i < numBinIndices; ++i)
                binToDeconvIndex[binIndicesList[i]] = i;
            var queryBinEnumerator = transBinner.BinsFromValues(testSpectrum.Mzs, true);
            demultiplexer.SpectrumProcessor = new SpectrumProcessor(165, isoMapper, transBinner);
            // Apply the peak intensity correction
            demultiplexer.CorrectPeakIntensitiesTest(testSpectrum, binIndicesSet, peakSums,
                                                     queryBinEnumerator, db, ref deconvIntensities,
                                                     ref deconvMzs);

            // Check that the five solutions defined above were applied correctly
            // by CorrectPeakIntensities

            // If all precursors contribute 0 intensity, the peak should be zero for every
            // deconvolved spectrum
            for (int i = 0; i < deconvIndices.Length; ++i)
            {
                Assert.AreEqual(deconvIntensities[i][binToDeconvIndex[0]], 0.0);
            }

            // Find the index of the demultiplexed spectrum corresponding to the precursor
            // at 710.57 m/z for which we generated the dummy demultiplexing solution for
            int windowIndex;
            isoMapper.TryGetDeconvFromMz(710.57, out windowIndex);
            Assert.AreEqual(50, windowIndex);
            int isoIndex = deconvIndices.IndexOf(i => i == windowIndex);
            Assert.AreNotEqual(-1 , isoIndex);

            // This peak falls in one transition bin (transition index 1)
            // 47 is the index of a peak in the test spectrum
            var originalIntensity = testSpectrum.Intensities[47];
            var expectedIntensity = originalIntensity*(2.0/6.0);
            Assert.AreEqual(deconvIntensities[isoIndex][47], expectedIntensity, 0.00001);

            // This peak falls in one transitions bin (transition index 2)
            originalIntensity = testSpectrum.Intensities[50];
            expectedIntensity = originalIntensity*(3.0/10.0);
            Assert.AreEqual(deconvIntensities[isoIndex][50], expectedIntensity, 0.00001);

            // This peak falls in both transition indices 1 and 2, the expected adjusted
            // intensity should be the average of the solution from each transition bin
            originalIntensity = testSpectrum.Intensities[49];
            expectedIntensity = originalIntensity*(19.0/60.0);
            Assert.AreEqual(deconvIntensities[isoIndex][49], expectedIntensity, 0.00001);

            // This peak falls in transition index 3, and the demultiplexing spectra
            // indicates that all of the intensity of teh original peak comes from
            // the precursor window at isoIndex that's being queried.  The peak intensity
            // should not change
            originalIntensity = testSpectrum.Intensities[108];
            expectedIntensity = originalIntensity;
            Assert.AreEqual(deconvIntensities[isoIndex][108], expectedIntensity, 0.00001);

            // This peak falls in one transition bin (transition index 5)
            originalIntensity = testSpectrum.Intensities[149];
            expectedIntensity = originalIntensity * (1.0/5.0);
            Assert.AreEqual(deconvIntensities[isoIndex][149], expectedIntensity, 0.00001);

            // Undo changes to the spectrum processor in case this demultiplexer object is
            // reused
            demultiplexer.SpectrumProcessor = oldProcessor;
        }

        /// <summary> 
        /// Demultiplexes a real .mxML file in a case where the answer is simple, and checks the answer is correct.
        /// </summary>
        private static void TestSpectrumDemultiplex(AbstractDemultiplexer demultiplexer, 
                                                int spectrumIndex,
                                                MsDataSpectrum originalSpectrum,
                                                int[] intensityIndices,
                                                double[] intensityValues,
                                                int deconvIndex)
        {
            var deconvSpectra = demultiplexer.GetDeconvolvedSpectra(spectrumIndex, originalSpectrum);
            int numberMzPoints = originalSpectrum.Intensities.Length;
            double[] peakSums = new double[numberMzPoints];
            for (int i = 0; i < numberMzPoints; ++i)
            {
                peakSums[i] = 0.0;
            }
            foreach (var deconvSpectrum in deconvSpectra)
            {
                Assert.AreEqual(deconvSpectrum.Intensities.Length, originalSpectrum.Intensities.Length);
                for (int i = 0; i < numberMzPoints; ++i)
                {
                    peakSums[i] += deconvSpectrum.Intensities[i];
                }
            }
            for (int i = 0; i < numberMzPoints; ++i)
            {
                Assert.AreEqual(peakSums[i], originalSpectrum.Intensities[i], 0.01);
            }
            for (int i = 0; i < intensityIndices.Length ; ++i)
            {
                Assert.AreEqual(deconvSpectra[deconvIndex].Intensities[intensityIndices[i]], intensityValues[i], 0.1);
            }
        }
   }
}
