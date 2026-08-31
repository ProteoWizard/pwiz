/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Summary description for PwizFileInfoTest
    /// </summary>
    [TestClass]
    public class PwizFileInfoTest : AbstractUnitTest
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)]
        public void TestInstrumentInfo()
        {
            if (SkipWiff2TestInTestExplorer(nameof(TestInstrumentInfo)))
                return;
            const string testZipPath = @"TestData\PwizFileInfoTest.zip";

            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

            // Waters file (.raw directory) and mz5 equivalent
            foreach (
                var ext in
                    new[]
                    {ExtensionTestContext.ExtWatersRaw, ExtensionTestContext.ExtMz5})
            {
                VerifyInstrumentInfo(TestFilesDir.GetTestPath("160109_Mix1_calcurve_075" + ext),
                    "Waters instrument model", "", "", "");
            }

            // ABI .wiff file
            VerifyInstrumentInfo(TestFilesDir.GetTestPath("051309_digestion" + ExtensionTestContext.ExtAbWiff),
                "4000 QTRAP", "electrospray ionization", "quadrupole/quadrupole/axial ejection linear ion trap", "electron multiplier");

            // Mobilion .mbi file
            VerifyInstrumentInfo(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Mobilion, "2024-02-16-16.02.20-CCS Calibration_02" + ExtensionTestContext.ExtMobilionRaw),
                "Agilent 6546", "electrospray ionization", "quadrupole/quadrupole/time-of-flight", "microchannel plate detector");

            // Sciex .wiff2 file
            string wiff2Ext = ExtensionTestContext.CanImportAbWiff2 ? ".wiff2" : "-sample-centroid.mzML";
            VerifyInstrumentInfo(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.ABI, "swath.api" + wiff2Ext),
                "X500R QTOF", "electrospray ionization", "quadrupole/quadrupole/time-of-flight", "electron multiplier");

            // MzWiff generated mzXML files
            VerifyInstrumentInfo(TestFilesDir.GetTestPath("051309_digestion-s3.mzXML"),
                "4000 Q Trap", "electrospray ionization", "TOFMS", "");

            // Agilent file (.d directory)
            VerifyInstrumentInfo(TestFilesDir.GetTestPath("081809_100fmol-MichromMix-05" + ExtensionTestContext.ExtAgilentRaw),
                "Agilent instrument model", "nanoelectrospray", "quadrupole/quadrupole/quadrupole", "electron multiplier");

            // Shimadzu TOF file (.lcd file)
            VerifyInstrumentInfo(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Shimadzu, "10nmol_Negative_MS_ID_ON_055" + ExtensionTestContext.ExtShimadzuRaw),
                "Shimadzu Scientific Instruments instrument model", "electrospray ionization", "quadrupole/quadrupole/time-of-flight", "microchannel plate detector");

            // Thermo .raw|mzML file
            foreach (
                var ext in
                    new[]
                    {ExtensionTestContext.ExtThermoRaw, ExtensionTestContext.ExtMzml})
            {
                VerifyInstrumentInfo(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_0001_REP1_01" + ext),
                    "TSQ Vantage", "nanoelectrospray", "quadrupole/quadrupole/quadrupole", "electron multiplier");
            }
        }

        // PressureTrace1.wiff, below, is read out of the shared vendor data directory through the
        // SAME Sciex reader the two tests above are excluded for - the legacy .wiff path rather than
        // .wiff2, which is why a sweep looking for "wiff2" missed it and why this test killed a
        // worker within the hour of that sweep reporting nothing else exposed.
        // The other vendors here are not the problem: Thermo shared data survives six concurrent
        // containers at 100 runs each.
        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)]
        public void TestTicChromatogram()
        {
            if (Skyline.Program.NoVendorReaders)
                return;

            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.ABI, "PressureTrace1.wiff"), 0, 0);
            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Agilent, "ImsSynthAllIons.d"), 49, 369032);
            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Agilent, "GFb_4Scan_TimeSegs_1530_100ng.d"), 63, 56163792);
            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Bruker, "Hela_QC_PASEF_Slot1-first-6-frames.d"), 1, 23340182);
            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Mobilion, "ExampleTuneMix_binned5" + ExtensionTestContext.ExtMobilionRaw), 29, 60899367);
            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Thermo, "090701-LTQVelos-unittest-01.raw"), 30, 32992246);
            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Waters, "MSe_Short.raw"), 2, 3286253);
            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Waters, "HDMRM_Short_noLM.raw"), 0, 0);
            VerifyTicChromatogram(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Waters, "HDDDA_Short_noLM.raw"), 1, 3692876);
        }

        /// <summary>
        /// Ascending m/z is nowhere required by the mzML specification, but it is what every consumer
        /// assumes, and extraction binary searches the m/z axis - so on a file presented in any other
        /// order the search lands nowhere useful and every chromatogram comes out empty, with no
        /// error. Spectra must therefore arrive m/z sorted whatever order the writer used.
        /// pwiz orders ordinary spectra on read, and this verifies that guarantee survives the trip
        /// out through pwiz_data_cli. It does not cover combined ion mobility spectra, which pwiz
        /// deliberately leaves alone because their m/z ascends only within each mobility bin -
        /// Skyline sorts those itself, in SpectraChromDataProvider.EnsureSortedMzs and
        /// ScanProvider, and those are not redundant with this.
        /// The fixture here is ordered by ascending intensity, one shape this has taken in practice.
        /// Its first spectrum is short and does happen to ascend, which is the case a probe of the
        /// first spectrum alone gets wrong: three peaks in order say nothing about the writer, and
        /// an early scan really can precede the sample and carry almost no peaks.
        /// </summary>
        [TestMethod]
        public void TestUnsortedMzArrays()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\UnsortedMzArraysMzml.zip");

            var path = TestFilesDir.GetTestPath("DataConvert5_unsorted_mz.mzML");

            // Pin the shape of the fixture, both halves of it. Every assertion below is satisfied by
            // an already-sorted file, so without this the fixture could be regenerated in m/z order
            // - by an msconvert round trip, say - and the test would stay green covering nothing.
            // The leading spectrum being the ordered one is just as essential: reorder the file and
            // the test stops covering the too-few-peaks-to-mean-anything rule that keeps a short
            // ordered spectrum from settling the question for the whole file.
            AssertEx.IsTrue(IsMzArrayAscendingInFile(path, 0), path);
            AssertEx.IsFalse(IsMzArrayAscendingInFile(path, 1), path);
            AssertEx.IsFalse(IsMzArrayAscendingInFile(path, 2), path);

            using var msDataFile = new MsDataFileImpl(path);
            AssertEx.AreEqual(3, msDataFile.SpectrumCount);

            for (var i = 0; i < msDataFile.SpectrumCount; i++)
            {
                var spectrum = msDataFile.GetSpectrum(i);
                CollectionAssert.AreEqual(ExpectedMzs(i), spectrum.Mzs, @"spectrum " + i);

                // The intensity paired with each m/z has to travel with it. Sorting the m/z array on
                // its own would leave every value plausible and every pairing wrong, which is worse
                // than the defect being fixed.
                for (var j = 0; j < spectrum.Mzs.Length; j++)
                {
                    AssertEx.AreEqual(ExpectedIntensity(i, spectrum.Mzs[j]), spectrum.Intensities[j],
                        string.Format(@"spectrum {0} m/z {1}", i, spectrum.Mzs[j]));
                }
            }
        }

        /// <summary>
        /// Read one m/z array straight out of the mzML text and report whether it ascends.
        /// Deliberately does not go through <see cref="MsDataFileImpl"/>, which hands back what pwiz
        /// already corrected - the point is to see the order the file is actually stored in. The
        /// fixture is written
        /// uncompressed 64-bit precisely so this stays a few lines.
        /// </summary>
        private static bool IsMzArrayAscendingInFile(string path, int spectrumIndex)
        {
            var text = File.ReadAllText(path);
            var arrayAt = -1;
            for (var i = 0; i <= spectrumIndex; i++)
            {
                arrayAt = text.IndexOf(@"name=""m/z array""", arrayAt + 1, StringComparison.Ordinal);
                AssertEx.IsTrue(arrayAt > 0, path);
            }
            // These two must be found inside this binaryDataArray element, not just anywhere before
            // it - searching back from the m/z array term would otherwise be satisfied by a
            // preceding array's params, and the decode below would read the wrong format silently.
            var elementAt = text.LastIndexOf(@"<binaryDataArray", arrayAt, StringComparison.Ordinal);
            AssertEx.IsTrue(elementAt > 0, path);
            AssertEx.IsTrue(text.LastIndexOf(@"name=""no compression""", arrayAt, StringComparison.Ordinal) > elementAt, path);
            AssertEx.IsTrue(text.LastIndexOf(@"name=""64-bit float""", arrayAt, StringComparison.Ordinal) > elementAt, path);

            var open = text.IndexOf(@"<binary>", arrayAt, StringComparison.Ordinal) + @"<binary>".Length;
            var close = text.IndexOf(@"</binary>", open, StringComparison.Ordinal);
            var bytes = Convert.FromBase64String(text.Substring(open, close - open));
            AssertEx.IsTrue(bytes.Length % sizeof(double) == 0, path);

            var previous = double.NegativeInfinity;
            for (var i = 0; i < bytes.Length; i += sizeof(double))
            {
                var mz = BitConverter.ToDouble(bytes, i);
                if (mz < previous)
                    return false;
                previous = mz;
            }
            return true;
        }

        /// <summary>
        /// The m/z values the fixture holds, in the order they have to come back in. The short
        /// leading spectrum is a set of its own; the two after it share one.
        /// </summary>
        private static double[] ExpectedMzs(int spectrumIndex)
        {
            return spectrumIndex == 0
                ? new[] { 150.1, 250.2, 350.3 }
                : new[] { 200.2, 300.3, 400.4, 500.5, 600.6, 700.7 };
        }

        /// <summary>
        /// The intensities the fixture pairs with each m/z, independent of the order they are stored in.
        /// </summary>
        private static double ExpectedIntensity(int spectrumIndex, double mz)
        {
            Dictionary<double, double> byMz;
            switch (spectrumIndex)
            {
                case 0:
                    byMz = new Dictionary<double, double> { { 150.1, 5 }, { 250.2, 7 }, { 350.3, 9 } };
                    break;
                case 1:
                    byMz = new Dictionary<double, double> { { 500.5, 10 }, { 300.3, 20 }, { 700.7, 30 }, { 200.2, 40 }, { 600.6, 50 }, { 400.4, 60 } };
                    break;
                default:
                    byMz = new Dictionary<double, double> { { 400.4, 15 }, { 700.7, 25 }, { 200.2, 35 }, { 600.6, 45 }, { 300.3, 55 }, { 500.5, 65 } };
                    break;
            }
            return byMz[mz];
        }

        /// <summary>
        /// The open dialogs match source types by string equality, so the type names in
        /// <see cref="DataSourceUtil"/> have to agree with the reader's own names wherever it
        /// does not translate them. Agilent is such a case - the reader answers "Agilent
        /// MassHunter" and nothing maps it - so a rename on either side would silently drop
        /// Agilent data from the dialogs, which is what happened to Bruker FID in issue #4510.
        /// </summary>
        [TestMethod]
        public void TestAgilentSourceType()
        {
            if (Skyline.Program.NoVendorReaders)
                return;

            var agilentPath = TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Agilent, "ImsSynthAllIons.d");
            Assert.AreEqual(DataSourceUtil.TYPE_AGILENT, MsDataFileImpl.IdentifyReaderType(agilentPath));
            Assert.AreEqual(DataSourceUtil.TYPE_AGILENT, DataSourceUtil.GetSourceType(agilentPath));
            Assert.IsTrue(DataSourceUtil.IsDataSource(agilentPath));
        }

        [TestMethod]
        public void TestScanDescription()
        {
            if (Skyline.Program.NoVendorReaders)
                return;

            string path = TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Thermo, "IT-HCD-SPS.raw");

            using (var msDataFile = new MsDataFileImpl(path))
            {
                Assert.AreEqual("sps", msDataFile.GetScanDescription(0));
            }
        }

        [TestMethod]
        public void TestQcTraces()
        {
            const string testZipPath = @"TestData\PressureTracesTest.zip";

            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

            using (var msDataFile = new MsDataFileImpl(TestFilesDir.GetTestPath("PressureTrace1" + ExtensionTestContext.ExtAbWiff)))
            {
                var pressureTraces = msDataFile.GetQcTraces();

                VerifyQcTrace(pressureTraces[0], "Column Pressure (channel 1)", 1148, 0, 9.558333, 1470, 210, MsDataFileImpl.QcTraceQuality.Pressure, MsDataFileImpl.QcTraceUnits.Pascal);
                VerifyQcTrace(pressureTraces[1], "Pump A Flowrate (channel 2)", 1148, 0, 9.558333, 91590, 89120, MsDataFileImpl.QcTraceQuality.FlowRate, MsDataFileImpl.QcTraceUnits.MicrolitersPerMinute);
                VerifyQcTrace(pressureTraces[2], "Pump B Flowrate (channel 3)", 1148, 0, 9.558333, 0, 840, MsDataFileImpl.QcTraceQuality.FlowRate, MsDataFileImpl.QcTraceUnits.MicrolitersPerMinute);
                VerifyQcTrace(pressureTraces[3], "Column Pressure (channel 4)", 3508, 0, 29.225, 1396, 1322, MsDataFileImpl.QcTraceQuality.Pressure, MsDataFileImpl.QcTraceUnits.Pascal);
                VerifyQcTrace(pressureTraces[4], "Pump A Flowrate (channel 5)", 3508, 0, 29.225, 7038, 7833, MsDataFileImpl.QcTraceQuality.FlowRate, MsDataFileImpl.QcTraceUnits.MicrolitersPerMinute);
                VerifyQcTrace(pressureTraces[5], "Pump B Flowrate (channel 6)", 3508, 0, 29.225, 680, 151, MsDataFileImpl.QcTraceQuality.FlowRate, MsDataFileImpl.QcTraceUnits.MicrolitersPerMinute);

                string docPath = TestFilesDir.GetTestPath("PressureTrace1.sky");
                SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
                AssertEx.IsDocumentState(doc, 0, 1, 3, 9);

                using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
                {
                    const string replicateName = "PressureTrace1";
                    string extRaw = ExtensionTestContext.ExtAbWiff;
                    var chromSets = new[]
                    {
                        new ChromatogramSet(replicateName, new[]
                            { new MsDataFilePath(TestFilesDir.GetTestPath("PressureTrace1" + extRaw)),  }),
                    };
                    var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                    Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                    docContainer.AssertComplete();
                    docResults = docContainer.Document;

                    var chromCache = docResults.Settings.MeasuredResults.GetChromCacheMinimizer(docResults).ChromatogramCache;
                    var tic = chromCache.LoadAllIonsChromatogramInfo(ChromExtractor.summed, chromSets[0]);
                    var bpc = chromCache.LoadAllIonsChromatogramInfo(ChromExtractor.base_peak, chromSets[0]);
                    var qc = chromCache.LoadAllIonsChromatogramInfo(ChromExtractor.qc, chromSets[0]);

                    Assert.AreEqual(0, tic.Count());    // No longer expect these in SRM data
                    Assert.AreEqual(0, bpc.Count());    // No longer expect these in SRM data
                    var qcNames = qc.Select(o => o.QcTraceName).ToArray();
                    Assert.AreEqual(6, qcNames.Length);
                    CollectionAssert.IsSubsetOf(new [] {"Column Pressure (channel 1)",
                                                        "Pump A Flowrate (channel 2)",
                                                        "Pump B Flowrate (channel 3)",
                                                        "Column Pressure (channel 4)",
                                                        "Pump A Flowrate (channel 5)",
                                                        "Pump B Flowrate (channel 6)" },
                                                qcNames);
                }
            }
        }

        // Reads swath.api.wiff2 out of the shared vendor data directory, the same file
        // TestInstrumentInfo above is excluded for. On net8 that goes through the Sciex SDK
        // shimmed into a side-by-side load context, and a second process opening the file while
        // another holds it does not fail - it takes the process down, with no managed exception
        // and a container exit code of -1. Five workers were lost to this test in two nightly
        // runs, on four different languages, while the run reported no failures.
        // Only across the Docker volume mount: four processes on local NTFS share the file
        // 40 times each without trouble, so nothing a Skyline user does is affected.
        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)]
        public void TestInstrumentSerialNumbers()
        {
            if (SkipWiff2TestInTestExplorer(nameof(TestInstrumentSerialNumbers)))
                return;
            if (Skyline.Program.NoVendorReaders)
                return;

            TestFilesDirs = new []
            {
                new TestFilesDir(TestContext, @"TestData\PwizFileInfoTest.zip"),
                new TestFilesDir(TestContext, @"TestData\Results\ThermoQuant.zip")
            };
            var testFilesDir = TestFilesDirs[0];

            if (ExtensionTestContext.CanImportAbWiff2)
                VerifySerialNumber(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.ABI, "swath.api.wiff2"), "CI231606PT"); // WIFF2 file with empty serial number

            if (ExtensionTestContext.CanImportAbWiff)
                VerifySerialNumber(testFilesDir.GetTestPath("051309_digestion.wiff"), "U016050603");

            if (ExtensionTestContext.CanImportAgilentRaw)
                VerifySerialNumber(testFilesDir.GetTestPath("081809_100fmol-MichromMix-05.d"), "50331873");

            if (ExtensionTestContext.CanImportShimadzuRaw)
                VerifySerialNumber(testFilesDir.GetTestPath("10nmol_Negative_MS_ID_ON_055.lcd"), null); // Shimadzu does not provide serial number

            if (ExtensionTestContext.CanImportWatersRaw)
                VerifySerialNumber(testFilesDir.GetTestPath("160109_Mix1_calcurve_075.raw"), null); // Waters does not provide serial number

            if (ExtensionTestContext.CanImportThermoRaw)
            {
                VerifySerialNumber(testFilesDir.GetTestPath("CE_Vantage_15mTorr_0001_REP1_01.raw"), null); // Thermo RAW file with empty serial number

                var testFilesDir2 = TestFilesDirs[1];
                VerifySerialNumber(testFilesDir2.GetTestPath("Site20_STUDY9P_PHASEII_QC_03.raw"), "TQU00490");
            }
        }

        private static void VerifyInstrumentInfo(string path, string model, string ionization, string analyzer, string detector)
        {
            using (var msDataFile = new MsDataFileImpl(path))
            {
                var instrumentInfoList = msDataFile.GetInstrumentConfigInfoList().ToList();
                Assert.AreEqual(1, instrumentInfoList.Count);
                var instrument = instrumentInfoList[0];
                Assert.IsFalse(instrument.IsEmpty);
                Assert.AreEqual(model, instrument.Model);
                Assert.AreEqual(ionization, instrument.Ionization);
                Assert.AreEqual(analyzer, instrument.Analyzer);
                Assert.AreEqual(detector, instrument.Detector);
            }
        }

        private static void VerifyTicChromatogram(string path, int count, double maxIntensity, int sampleIndex = 0)
        {
            if (!MsDataFileImpl.SupportsMultipleSamples(path))
                sampleIndex = 0;

            using (var msDataFile = new MsDataFileImpl(path, sampleIndex))
            {
                var tic = msDataFile.GetTotalIonCurrent();
                Assert.AreEqual(count, tic.Length);
                Assert.AreEqual(maxIntensity, tic.Length > 0 ? tic.Max() : 0, maxIntensity * 1e-7);
            }
        }

        private static void VerifyQcTrace(MsDataFileImpl.QcTrace qcTrace,
                                          string name, int count,
                                          double firstTime, double lastTime,
                                          double firstIntensity, double lastIntensity,
                                          string measuredQuality, string intensityUnits)
        {
            Assert.AreEqual(name, qcTrace.Name);
            Assert.AreEqual(measuredQuality, qcTrace.MeasuredQuality);
            Assert.AreEqual(intensityUnits, qcTrace.IntensityUnits);
            Assert.AreEqual(count, qcTrace.Times.Length);
            Assert.AreEqual(qcTrace.Times.Length, qcTrace.Intensities.Length);
            Assert.AreEqual(firstTime, qcTrace.Times.First());
            Assert.AreEqual(firstIntensity, qcTrace.Intensities.First());
            Assert.AreEqual(lastTime, qcTrace.Times.Last(), 1e-6);
            Assert.AreEqual(lastIntensity, qcTrace.Intensities.Last());
        }

        private static void VerifySerialNumber(string path, string serialNumber, int sampleIndex = 0)
        {
            if (!MsDataFileImpl.SupportsMultipleSamples(path))
                sampleIndex = 0;

            using (var msDataFile = new MsDataFileImpl(path, sampleIndex))
            {
                Assert.AreEqual(serialNumber, msDataFile.GetInstrumentSerialNumber());
            }
        }

        [TestMethod]
        public void VerifyUnicodePathHandling()
        {
            //
            // Verify that the logic for converting Unicode paths to 8.3 short format
            // works the same in PathEx and it does in the pwiz CLI
            //

            // Create a temporary file with Unicode characters in its name
            var tempFilePath = PathEx.GetTempFileNameWithExtension("test测试文件.txt");
            File.WriteAllText(tempFilePath, @"This is a test file with Unicode characters in its name.");

            // Use PathEx.GetNonUnicodePath() to get the non-Unicode path
            var nonUnicodePath = PathEx.GetNonUnicodePath(tempFilePath);

            // Use the CLI implementation to get the non-Unicode path
            var cliEquivalentPath = MsDataFileImpl.GetNonUnicodePath(tempFilePath);

            File.Delete(tempFilePath); // No longer needed

            // Assert that the two (possibly 8.3 converted) paths are equivalent
            Assert.AreEqual(cliEquivalentPath, nonUnicodePath,
                @$"The 8.3 converted path for {tempFilePath} does not match the pwiz CLI implementation.");
        }
    }
}
