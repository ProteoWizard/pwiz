/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for CommandLineTest
    /// </summary>
    [TestClass]
    public class CommandLineTest
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes

        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
         [TestCleanup]
         public void MyTestCleanup()
         {
             if (Settings.Default.ToolList.Count > 0)
             {
                 Settings.Default.ToolList.RemoveAll(t => true);
             }
         }
        

        #endregion

        private const string ZIP_FILE = @"TestA\Results\FullScan.zip";
        private const string COMMAND_FILE = @"TestA\CommandLineTest.zip";

        [TestMethod]
        public void ConsoleReplicateOutTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Imported_single.sky");

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);

            RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath,
                       "--import-replicate-name=Single",
                       "--out=" + outPath);

            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            AssertResult.IsDocumentResultsState(doc, "Single", 3, 3, 0, 21, 0);



            //Test --import-append
            var dataFile2 = testFilesDir.GetTestPath("ah_20101029r_BSA_CID_FT_centroid_3uscan_3" +
                ExtensionTestContext.ExtThermoRaw);

            RunCommand("--in=" + outPath,
                       "--import-file=" + dataFile2,
                       "--import-replicate-name=Single",
                       "--import-append",
                       "--save");

            doc = ResultsUtil.DeserializeDocument(outPath);

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            AssertResult.IsDocumentResultsState(doc, "Single", 6, 6, 0, 42, 0);

            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
        }

        [TestMethod]
        public void ConsoleReportExportTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Exported_test_report.csv");

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);
            const string replicate = "Single";

            //Before generating this report, check that it exists
            const string reportName = "Peptide Ratio Results";
            var defaultReportSpecs = Settings.Default.ReportSpecList.GetDefaults().ToArray();
            Assert.IsNotNull(defaultReportSpecs.FirstOrDefault(r => r.Name.Equals(reportName)));
            Settings.Default.ReportSpecList = new ReportSpecList();
            Settings.Default.ReportSpecList.AddRange(defaultReportSpecs);

            //First, programmatically generate the report
            StringBuilder reportBuffer = new StringBuilder();
            StringWriter reportWriter = new StringWriter(reportBuffer);

            ReportSpec reportSpec = Settings.Default.GetReportSpecByName(reportName);
            Report report = Report.Load(reportSpec);

            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);

            //Attach replicate
            ProgressStatus status;
            doc = CommandLine.ImportResults(doc, docPath, "Single", rawPath, out status);
            Assert.IsNull(status);

            using(Database database = new Database(doc.Settings))
            {
                database.AddSrmDocument(doc);
                ResultSet resultSet = report.Execute(database);

                ResultSet.WriteReportHelper(resultSet, TextUtil.GetCsvSeparator(CultureInfo.CurrentCulture), reportWriter,
                                                  CultureInfo.CurrentCulture);
            }

            reportWriter.Flush();

            reportWriter.Close();

            string programmaticReport = reportBuffer.ToString();

            RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath,
                       "--import-replicate-name=" + replicate,
                       "--report-name=Peptide Ratio Results",
                       "--report-format=CSV",
                       "--report-file=" + outPath);

            string reportLines = File.ReadAllText(outPath);
            AssertEx.NoDiff(reportLines, programmaticReport);
        }

        [TestMethod]
        public void ConsoleMassListTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");

            var doc = ResultsUtil.DeserializeDocument(docPath);

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                                                      ExtensionTestContext.ExtThermoRaw);

            /////////////////////////
            // Thermo test
            string thermoPath = testFilesDir.GetTestPath("Thermo_test.csv");

            string output = RunCommand("--in=" + docPath,
                                       "--import-file=" + rawPath,
                                       "--exp-translist-instrument=" + ExportInstrumentType.THERMO,
                                       "--exp-file=" + thermoPath);

            Assert.IsTrue(output.Contains("successfully."));
            Assert.IsTrue(File.Exists(thermoPath));
            Assert.AreEqual(doc.TransitionCount, File.ReadAllLines(thermoPath).Length);


            /////////////////////////
            // Agilent test
            string agilentPath = testFilesDir.GetTestPath("Agilent_test.csv");

            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.AGILENT,
                                "--exp-file=" + agilentPath,
                                "--exp-dwelltime=20");

            //check for success
            Assert.IsTrue(output.Contains("successfully."));
            Assert.IsTrue(File.Exists(agilentPath));
            Assert.AreEqual(doc.TransitionCount + 1, File.ReadAllLines(agilentPath).Length);

            /////////////////////////
            // AB Sciex test
            string sciexPath = testFilesDir.GetTestPath("AB_Sciex_test.csv");


            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.ABI,
                                "--exp-file=" + sciexPath,
                                "--exp-dwelltime=20");

            //check for success
            Assert.IsTrue(output.Contains("successfully."));
            Assert.IsTrue(File.Exists(sciexPath));
            Assert.AreEqual(doc.TransitionCount, File.ReadAllLines(sciexPath).Length);

            /////////////////////////
            // Waters test
            string watersPath = testFilesDir.GetTestPath("Waters_test.csv");

            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                                "--exp-file=" + watersPath,
                                "--exp-runlength=100");

            //check for success
            Assert.IsTrue(output.Contains("successfully."));
            Assert.IsTrue(File.Exists(watersPath));
            Assert.AreEqual(doc.TransitionCount + 1, File.ReadAllLines(watersPath).Length);
        }

        [TestMethod]
        public void ConsoleMethodTest()
        {
            //Here I'll only test Agilent for now

            /////////////////////////
            // Thermo test
//            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
//            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
//            string thermoTemplate = methodFilesDir.GetTestPath("20100329_Protea_Peptide_targeted.meth");
//            string thermoOut = methodFilesDir.GetTestPath("Thermo_test.meth");
//            output = RunCommand("--in=" + docPath,
//                               "--import-file=" + rawPath,
//                               "--exp-method-instrument=Thermo LTQ",
//                               "--exp-template=" + thermoTemplate,                        
//                               "--exp-file=" + thermoOut,
//                               "--exp-strategy=buckets",
//                               "--exp-max-trans=130",
//                               "--exp-optimizing=ce",
//                               "--exp-full-scans");
//
            // check for success
//            Assert.IsTrue(output.Contains("successfully."));

            
            /////////////////////////
            // Agilent test
            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            string docPath2 = commandFilesDir.GetTestPath("WormUnrefined.sky");
            string agilentTemplate = commandFilesDir.GetTestPath("43mm-40nL-30min-opt.m");
            string agilentOut = commandFilesDir.GetTestPath("Agilent_test.m");

            // Try this a few times, because Agilent method building seems to fail under stress
            // about 10% of the time.
            bool success = false;
            string output = "";
            for (int i = 0; !success && i < 3; i++)
            {
                output = RunCommand("--in=" + docPath2,
                                           "--exp-method-instrument=Agilent 6400 Series",
                                           "--exp-template=" + agilentTemplate,
                                           "--exp-file=" + agilentOut,
                                           "--exp-dwell-time=20",
                                           "--exp-strategy=buckets",
                                           "--exp-max-trans=75");

                //check for success
                success = output.Contains("successfully.");
            }

            if (!success)
            {
                Console.WriteLine("Failed to write Agilent method: {0}", output);
                Assert.IsTrue(success);
            }
        }

        [TestMethod]
        public void ConsolePathCoverage()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string bogusPath = testFilesDir.GetTestPath("bogus_file.sky");
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Output_file.sky");
            string tsvPath = testFilesDir.GetTestPath("Exported_test_report.csv");

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);


            //Error: file does not exist
            string output = RunCommand("--in=" + bogusPath);
            Assert.IsTrue(output.Contains("Error"));

            //Error: no raw file
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath + "x",
                                "--import-replicate-name=Single");
            Assert.IsTrue(output.ToLower().Contains("error"));

            //Error: no reportfile
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--import-replicate-name=Single",
                                "--out=" + outPath,
                                "--report-format=TSV",
                                "--report-name=" + "Peptide Ratio Results");
            Assert.IsTrue(output.Contains("Error"));


            //Error: no such report
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--report-file=" + tsvPath,
                                "--report-name=" + "Bogus Report");
            Assert.IsTrue(output.Contains("Error"));


            //Error: no --in specified with --import-file
            output = RunCommand("--import-file=" + rawPath,
                                "--save");
            Assert.IsTrue(output.Contains("Error"));


            //Error: no --in specified with --report
            output = RunCommand("--out=" + outPath,
                                "--report-file=" + tsvPath,
                                "--report-name=" + "Bogus Report");
            Assert.IsTrue(output.Contains("Error"));



            //check for success. This is merely to cover more paths
            string watersPath = testFilesDir.GetTestPath("Waters_test.csv");

            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=Waters",
                                "--exp-file=" + watersPath,
                                "--exp-method-type=scheduled",
                                "--exp-run-length=100",
                                "--exp-optimizing=ce",
                                "--exp-strategy=protein",
                                "--exp-max-trans=100",
                                "--exp-scheduling-replicate=LAST");
            Assert.IsTrue(output.Contains("successfully."));


            //check for success
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--import-replicate-name=Single",
                                "--exp-translist-instrument=Waters",
                                "--exp-file=" + watersPath,
                                "--exp-method-type=scheduled",
                                "--exp-run-length=100",
                                "--exp-optimizing=ce",
                                "--exp-strategy=buckets",
                                "--exp-max-trans=10000000",
                                "--exp-scheduling-replicate=Single");
            Assert.IsTrue(output.Contains("successfully."));


            //Check a bunch of warnings
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--import-replicate-name=Single",
                                "--report-format=BOGUS",
                                "--exp-translist-instrument=BOGUS",
                                "--exp-method-instrument=BOGUS",
                                "--exp-strategy=BOGUS",
                                "--exp-max-trans=BOGUS",
                                "--exp-optimizing=BOGUS",
                                "--exp-method-type=BOGUS",
                                "--exp-dwell-time=1000000000", //bogus
                                "--exp-dwell-time=BOGUS",
                                "--exp-run-length=1000000000",
                                "--exp-run-length=BOGUS",
                                "--exp-translist-instrument=Waters",
                                "--exp-method-instrument=Thermo LTQ");
                                //1 Error for using the above 2 parameters simultaneously

            Assert.IsFalse(output.Contains("successfully."));

            Assert.AreEqual(CountInstances("Warning", output), 11);
            Assert.AreEqual(CountInstances("Error", output), 1);


            //This test uses a broken Skyline file to test the InvalidDataException catch
            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            var brokenFile = commandFilesDir.GetTestPath("Broken_file.sky");

            output = RunCommand("--in=" + brokenFile);
            Assert.AreEqual(1, CountInstances("Error", output));
            AssertEx.Contains(output, new[] { "line", "column" });


            //This test uses a broken Skyline file to test the InvalidDataException catch
            var invalidFile = commandFilesDir.GetTestPath("InvalidFile.sky");
            output = RunCommand("--in=" + invalidFile);
            Assert.AreEqual(1, CountInstances("Error", output));
            AssertEx.Contains(output, new[] {"line", "column"});
        }

        private static string RunCommand(params string[] inputArgs)
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new StringWriter(consoleBuffer);
            CommandLineRunner.RunCommand(inputArgs, consoleOutput);
            return consoleBuffer.ToString();
        }

        // TODO: Test the case where the imported replicate has the wrong path without Lorenzo's data
        //[TestMethod]
        public void TestLorenzo()
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new StringWriter(consoleBuffer);

            var testFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);

            string docPath = testFilesDir.GetTestPath("VantageQCSkyline.sky");
            string tsvPath = testFilesDir.GetTestPath("Exported_test_report.csv");
            string dataPath = testFilesDir.GetTestPath("VantageQCSkyline.skyd");

            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + dataPath,
                               "--report-name=TestQCReport",
                               "--report-file=" + tsvPath,
                               "--report-format=TSV"
                           };

            //There are no tests. This is for debugging.
            CommandLineRunner.RunCommand(args, consoleOutput);
        }

        //[TestMethod]
        public void CountInstancesTest()
        {
            string s = "hello,hello,hello";
            Assert.AreEqual(3,CountInstances("hello",s));

            s += "hi";
            Assert.AreEqual(3,CountInstances("hello",s));

            Assert.AreEqual(0,CountInstances("",""));

            Assert.AreEqual(0,CountInstances("hi","howdy"));
        }

        public static int CountInstances(string search, string searchSpace)
        {
            if (searchSpace.Length == 0)
            {
                return 0;
            }

            int count = 0;
            int lastIndex = searchSpace.IndexOf(search, StringComparison.Ordinal);
            for (; !Equals(-1, lastIndex) && lastIndex + search.Length <= searchSpace.Length; count++)
            {
                lastIndex = searchSpace.IndexOf(search, StringComparison.Ordinal);
                searchSpace = searchSpace.Substring(lastIndex + 1);
                lastIndex = searchSpace.IndexOf(search, StringComparison.Ordinal);
            }

            return count;
        }
        
        [TestMethod]
        public void ConsoleBadRawFileImportTest()
        {
            // Run this test only if we can read Thermo's raw files
            if(ExtensionTestContext.CanImportThermoRaw &&
                ExtensionTestContext.CanImportWatersRaw)
            {
                const string testZipPath = @"TestA\ImportAllCmdLineTest.zip";

                var testFilesDir = new TestFilesDir(TestContext, testZipPath);

                // Contents:
                // ImportAllCmdLineTest
                //   -- REP01
                //       -- CE_Vantage_15mTorr_0001_REP1_01.raw|mzML
                //       -- CE_Vantage_15mTorr_0001_REP1_02.raw|mzML
                //   -- REP02
                //       -- CE_Vantage_15mTorr_0001_REP2_01.raw|mzML
                //       -- CE_Vantage_15mTorr_0001_REP2_02.raw|mzML
                //   -- 160109_Mix1_calcurve_070.mzML
                //   -- 160109_Mix1_calcurve_073.mzML
                //   -- 160109_Mix1_calcurve_071.raw (Waters .raw directory)
                //   -- 160109_Mix1_calcurve_074.raw (Waters .raw directory)
                //   -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
                //   -- bad_file_folder
                //       -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
                //   -- FullScan.RAW|mzML (should not be imported)
                //   -- FullScan_folder
                //       -- FullScan.RAW|mzML (should not be imported)

                var docPath = testFilesDir.GetTestPath("test.sky");

                var rawPath = testFilesDir.GetTestPath("bad_file.raw");

                var msg = RunCommand("--in=" + docPath,
                                     "--import-file=" + rawPath,
                                     "--save");

                Assert.IsTrue(msg.Contains("Warning: Cannot read file"));

                // the document should not have changed
                SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
                Assert.IsFalse(doc.Settings.HasResults);

                msg = RunCommand("--in=" + docPath,
                                 "--import-all=" + testFilesDir.FullPath,
                                 "--save");

                Assert.IsTrue(msg.Contains("Warning: Cannot read file"), msg);
                doc = ResultsUtil.DeserializeDocument(docPath);
                Assert.IsTrue(doc.Settings.HasResults);
                Assert.AreEqual(6, doc.Settings.MeasuredResults.Chromatograms.Count,
                    string.Format("Expected 6 replicates, found: {0}",
                                  string.Join(", ", doc.Settings.MeasuredResults.Chromatograms.Select(chromSet => chromSet.Name).ToArray())));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("REP01"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("REP02"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("160109_Mix1_calcurve_071"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("160109_Mix1_calcurve_074"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("160109_Mix1_calcurve_070"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("160109_Mix1_calcurve_073"));
                // We should not have a replicate named "bad_file"
                Assert.IsFalse(doc.Settings.MeasuredResults.ContainsChromatogram("bad_file"));
                // Or a replicate named "bad_file_folder"
                Assert.IsFalse(doc.Settings.MeasuredResults.ContainsChromatogram("bad_file_folder"));
            }
        }

        [TestMethod]
        public void ConsoleImportNonSRMFile()
        {
            bool useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;
            string extRaw = useRaw
                                ? ExtensionTestContext.ExtThermoRaw
                                : ".mzML";
            string testZipPath = useRaw
                                    ? @"TestA\ImportAllCmdLineTest.zip"
                                    : @"TestA\ImportAllCmdLineTestMzml.zip";
            var testFilesDir = new TestFilesDir(TestContext, testZipPath);

            // Contents:
            // ImportAllCmdLineTest
            //   -- REP01
            //       -- CE_Vantage_15mTorr_0001_REP1_01.raw|mzML
            //       -- CE_Vantage_15mTorr_0001_REP1_02.raw|mzML
            //   -- REP02
            //       -- CE_Vantage_15mTorr_0001_REP2_01.raw|mzML
            //       -- CE_Vantage_15mTorr_0001_REP2_02.raw|mzML
            //   -- 160109_Mix1_calcurve_070.mzML
            //   -- 160109_Mix1_calcurve_073.mzML
            //   -- 160109_Mix1_calcurve_071.raw (Waters .raw directory)
            //   -- 160109_Mix1_calcurve_074.raw (Waters .raw directory)
            //   -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
            //   -- bad_file_folder
            //       -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
            //   -- FullScan.RAW|mzML (should not be imported)
            //   -- FullScan_folder
            //       -- FullScan.RAW|mzML (should not be imported)

            
            var docPath = testFilesDir.GetTestPath("test.sky");
            var outPath = testFilesDir.GetTestPath("import_nonSRM_file.sky");

            var rawPath = testFilesDir.GetTestPath("FullScan" + extRaw);

            // Try to import FullScan.RAW|mzML
            var msg = RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath,
                       "--out=" + outPath);

            Assert.IsTrue(msg.Contains("Warning: Failed importing the results file"), msg);
            // Read the saved document. FullScan.RAW|mzML should not have been imported
            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(doc.Settings.HasResults);

            // Import all files in the directory. FullScan.RAW|mzML should not be imported
            msg = RunCommand("--in=" + outPath,
                             "--import-all=" + testFilesDir.FullPath,
                             "--save");
            Assert.IsTrue(msg.Contains("Warning: Failed importing the results file"), msg);

            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(6, doc.Settings.MeasuredResults.Chromatograms.Count,
                string.Format("Expected 6 replicates, found: {0}",
                              string.Join(", ", doc.Settings.MeasuredResults.Chromatograms.Select(chromSet => chromSet.Name).ToArray())));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("REP01"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("REP02"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("160109_Mix1_calcurve_071"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("160109_Mix1_calcurve_074"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("160109_Mix1_calcurve_070"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("160109_Mix1_calcurve_073"));
            // We should not have a replicate named "FullScan"
            Assert.IsFalse(doc.Settings.MeasuredResults.ContainsChromatogram("FullScan"));
            // Or a replicate named "FullScan_folder"
            Assert.IsFalse(doc.Settings.MeasuredResults.ContainsChromatogram("FullScan_folder"));
        }

        [TestMethod]
        public void ConsoleMultiReplicateImportTest()
        {
            bool useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;
            string testZipPath = useRaw
                                     ? @"TestA\ImportAllCmdLineTest.zip"
                                     : @"TestA\ImportAllCmdLineTestMzml.zip";
            string extRaw = useRaw
                                ? ".raw"
                                : ".mzML";

            var testFilesDir = new TestFilesDir(TestContext, testZipPath);


            // Contents:
            // ImportAllCmdLineTest
            //   -- REP01
            //       -- CE_Vantage_15mTorr_0001_REP1_01.raw|mzML
            //       -- CE_Vantage_15mTorr_0001_REP1_02.raw|mzML
            //   -- REP02
            //       -- CE_Vantage_15mTorr_0001_REP2_01.raw|mzML
            //       -- CE_Vantage_15mTorr_0001_REP2_02.raw|mzML
            //   -- 160109_Mix1_calcurve_070.mzML
            //   -- 160109_Mix1_calcurve_073.mzML
            //   -- 160109_Mix1_calcurve_071.raw (Waters .raw directory)
            //   -- 160109_Mix1_calcurve_074.raw (Waters .raw directory)
            //   -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
            //   -- bad_file_folder
            //       -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
            //   -- FullScan.RAW|mzML (should not be imported)
            //   -- FullScan_folder
            //       -- FullScan.RAW|mzML (should not be imported)



            var docPath = testFilesDir.GetTestPath("test.sky");
            var outPath1 = testFilesDir.GetTestPath("Imported_multiple1.sky");
            File.Delete(outPath1);
            var outPath2 = testFilesDir.GetTestPath("Imported_multiple2.sky");
            File.Delete(outPath2);
            var outPath3 = testFilesDir.GetTestPath("Imported_multiple3.sky");
            File.Delete(outPath3);

            var rawPath = testFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_01" + extRaw);
            
            // Test: Cannot use --import-file and --import-all options simultaneously
            var msg = RunCommand("--in=" + docPath,
                                 "--import-file=" + rawPath,
                                 "--import-replicate-name=Unscheduled01",
                                 "--import-all=" + testFilesDir.FullPath,
                                 "--out=" + outPath1);
            Assert.IsTrue(msg.Contains("Error:"), msg);
            // output file should not exist
            Assert.IsFalse(File.Exists(outPath1));



            // Test: Cannot use --import-replicate-name with --import-all
            msg = RunCommand("--in=" + docPath,
                             "--import-replicate-name=Unscheduled01",
                             "--import-all=" + testFilesDir.FullPath,
                             "--out=" + outPath1);
            Assert.IsTrue(msg.Contains("Error:"), msg);
            // output file should not exist
            Assert.IsFalse(File.Exists(outPath1));



            // Test: Cannot use --import-naming-pattern with --import-file
            msg = RunCommand("--in=" + docPath,
                                 "--import-file=" + rawPath,
                                 "--import-naming-pattern=prefix_(.*)",
                                 "--out=" + outPath1);
            Assert.IsTrue(msg.Contains("Error:"), msg);
            // output file should not exist
            Assert.IsFalse(File.Exists(outPath1));




            // Test: invalid regular expression (1)
            msg = RunCommand("--in=" + docPath,
                                 "--import-all=" + testFilesDir.FullPath,
                                 "--import-naming-pattern=",
                                 "--out=" + outPath1);
            // output file should not exist
            Assert.IsFalse(File.Exists(outPath1));
            Assert.IsTrue(msg.Contains("Error: Regular expression '' does not have any groups."), msg);



            // Test: invalid regular expression (2)
            msg = RunCommand("--in=" + docPath,
                      "--import-all=" + testFilesDir.FullPath,
                      "--import-naming-pattern=invalid",
                      "--out=" + outPath1);
            // output file should not exist
            Assert.IsTrue(!File.Exists(outPath1));
            Assert.IsTrue(msg.Contains("Error: Regular expression 'invalid' does not have any groups."), msg);




            // Test: Import files in the "REP01" directory; 
            // Use a naming pattern that will cause the replicate names of the two files to be the same
            msg = RunCommand("--in=" + docPath,
                             "--import-all=" + testFilesDir.GetTestPath("REP01"),
                             "--import-naming-pattern=.*_(REP[0-9]+)_(.+)",
                             "--out=" + outPath1);
            Assert.IsFalse(File.Exists(outPath1));
            Assert.IsTrue(msg.Contains("Error: Duplicate replicate name"), msg);




            // Test: Import files in the "REP01" directory; Use a naming pattern
            msg = RunCommand("--in=" + docPath,
                             "--import-all=" + testFilesDir.GetTestPath("REP01"),
                             "--import-naming-pattern=.*_([0-9]+)",
                             "--out=" + outPath1);
            Assert.IsTrue(File.Exists(outPath1), msg);
            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath1);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("01"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("02"));



            Assert.IsFalse(File.Exists(outPath2));

            // Test: Import a single file
            // Import REP01\CE_Vantage_15mTorr_0001_REP1_01.raw;
            // Use replicate name "REP01"
            msg = RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath,
                       "--import-replicate-name=REP01",
                       "--out=" + outPath2);
            Assert.IsTrue(File.Exists(outPath2), msg);
            doc = ResultsUtil.DeserializeDocument(outPath2);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            int initialFileCount = 0;
            foreach (var chromatogram in doc.Settings.MeasuredResults.Chromatograms)
            {
                initialFileCount += chromatogram.MSDataFilePaths.Count();
            }

            // Import another single file. 
            var rawPath2 = testFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML");
            msg = RunCommand("--in=" + outPath2,
                       "--import-file=" + rawPath2,
                       "--import-replicate-name=160109_Mix1_calcurve_070",
                       "--save");
            doc = ResultsUtil.DeserializeDocument(outPath2);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count, msg);
            ChromatogramSet chromatSet;
            int idx;
            doc.Settings.MeasuredResults.TryGetChromatogramSet("160109_Mix1_calcurve_070", out chromatSet, out idx);
            Assert.IsNotNull(chromatSet, msg);
            Assert.IsTrue(chromatSet.MSDataFilePaths.Contains(rawPath2));


            // Test: Import all files and sub-folders in test directory
            // The document should already contain a replicate named "REP01".
            // Only one more file should be added to the "REP01" replicate.
            // The document should also already contain replicate "160109_Mix1_calcurve_070".
            // There should be notes about ignoring the two files that are already in the document.
            msg = RunCommand("--in=" + outPath2,
                             "--import-all=" + testFilesDir.FullPath,
                             "--save");
            // ExtensionTestContext.ExtThermo raw uses different case from file on disk
            // which happens to make a good test case.
            string rawPathDisk = GetThermoDiskPath(rawPath);

            // These messages are due to files that were already in the document.
            Assert.IsTrue(msg.Contains(string.Format("REP01 -> {0}", rawPathDisk)), msg); 
            Assert.IsTrue(msg.Contains("Note: The file has already been imported. Ignoring..."), msg);
            Assert.IsTrue(msg.Contains(string.Format("160109_Mix1_calcurve_070 -> {0}",rawPath2)), msg); 

            doc = ResultsUtil.DeserializeDocument(outPath2);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(6, doc.Settings.MeasuredResults.Chromatograms.Count,
                string.Format("Expected 6 replicates, found: {0}",
                              string.Join(", ", doc.Settings.MeasuredResults.Chromatograms.Select(chromSet => chromSet.Name).ToArray())));
            // count the number of files imported into the document
            int totalImportedFiles = 0;
            foreach (var chromatogram in doc.Settings.MeasuredResults.Chromatograms)
            {
                totalImportedFiles += chromatogram.MSDataFilePaths.Count();
            }
            // We should have imported 7 more file
            Assert.AreEqual(initialFileCount + 7, totalImportedFiles);
            // In the "REP01" replicate we should have 2 files
            ChromatogramSet chromatogramSet;
            int index;
            doc.Settings.MeasuredResults.TryGetChromatogramSet("REP01", out chromatogramSet, out index);
            Assert.IsNotNull(chromatogramSet);
            Assert.IsTrue(chromatogramSet.MSDataFilePaths.Count() == 2);
            Assert.IsTrue(chromatogramSet.MSDataFilePaths.Contains(rawPath));
            Assert.IsTrue(chromatogramSet.MSDataFilePaths.Contains(
                testFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_01" +
                extRaw)));
            Assert.IsTrue(!useRaw || chromatogramSet.MSDataFilePaths.Contains(
                GetThermoDiskPath(testFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_02" +
                extRaw))));

           

            Assert.IsFalse(File.Exists(outPath3));
            // Test: Import a single file
            // Import 160109_Mix1_calcurve_074.raw;
            // Use replicate name "REP01"
            var rawPath3 = testFilesDir.GetTestPath("160109_Mix1_calcurve_074" + extRaw);
            msg = RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath3,
                       "--import-replicate-name=REP01",
                       "--out=" + outPath3);
            Assert.IsTrue(File.Exists(outPath3), msg);
            doc = ResultsUtil.DeserializeDocument(outPath3);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            // Now import all files and sub-folders in test directory.
            // This should return an error since the replicate "REP01" that already
            // exists in the document has an unexpected file: '160109_Mix1_calcurve_074.raw'.
            msg = RunCommand("--in=" + outPath3,
                             "--import-all=" + testFilesDir.FullPath,
                             "--save");
            Assert.IsTrue(
                msg.Contains(
                    string.Format(
                        "Error: Replicate REP01 in the document has an unexpected file {0}",
                        rawPath3)), msg);

        }

        [TestMethod]
        public void ConsoleAddToolTest()
        {

            // Get a unique tool title.
            string title = GetTitleHelper();
            const string command = @"C:\Windows\Notepad.exe";
            const string arguments = "$(DocumentDir) Other";
            const string initialDirectory = @"C:\";


            // Test adding a tool.
            RunCommand("--tool-add=" + title,
                     "--tool-command=" + command,
                     "--tool-arguments=" + arguments,
                     "--tool-initial-dir=" + initialDirectory);
            int index = Settings.Default.ToolList.Count -1;
            ToolDescription tool = Settings.Default.ToolList[index];
            Assert.AreEqual(title, tool.Title);
            Assert.AreEqual(command,tool.Command);
            Assert.AreEqual(arguments,tool.Arguments);
            Assert.AreEqual(initialDirectory,tool.InitialDirectory);
            // Remove that tool.
            Settings.Default.ToolList.RemoveAt(index);

            // Test a tool with no Initial Directory and no arguments
            RunCommand("--tool-add=" + title,
                     "--tool-command=" + command);
            int index1 = Settings.Default.ToolList.Count - 1;
            ToolDescription tool1 = Settings.Default.ToolList[index1];
            Assert.AreEqual(title, tool1.Title);
            Assert.AreEqual(command, tool1.Command);
            Assert.AreEqual("", tool1.Arguments);
            Assert.AreEqual("", tool1.InitialDirectory);
            // Remove that Tool.
            Settings.Default.ToolList.RemoveAt(index1);

            // Test failure to add tool
            string output = RunCommand("--tool-add=" + title);
            Assert.IsTrue(output.Contains("The tool was not imported"));

            string output2 = RunCommand("--tool-command=" + command);
            Assert.IsTrue(output2.Contains("The tool was not imported"));

            const string badCommand = "test";
            string output3 = RunCommand("--tool-add=" + title,"--tool-command=" + badCommand);
            Assert.IsTrue(output3.Contains("Supported Types are: *.exe; *.com; *.pif; *.cmd; *.bat"));
            Assert.IsTrue(output3.Contains("The tool was not imported"));

            // Now test conflicting titles.
            // Add the tool.
            RunCommand("--tool-add=" + title,
                     "--tool-command=" + command,
                     "--tool-arguments=" + arguments,
                     "--tool-initial-dir=" + initialDirectory);         
            ToolDescription tool2 = Settings.Default.ToolList[Settings.Default.ToolList.Count - 1];
            Assert.AreEqual(title, tool2.Title); // tool with title of title exists.
            // Add another tool with the same title.
            string output4 = RunCommand("--tool-add=" + title,
                     "--tool-command=" + command);
            Assert.IsTrue(output4.Contains(("Error:")));

            ToolDescription tool3 = Settings.Default.ToolList.Last();
            Assert.AreNotEqual("", tool3.Arguments);
            Assert.AreNotEqual("", tool3.InitialDirectory);
            // Specify overwrite
            string output5 = RunCommand("--tool-add=" + title,
                     "--tool-command=" + command,
                     "--resolve-tool-conflicts=overwrite");
            Assert.IsTrue((output5.Contains("Warning: overwriting")));
            // Check arguments and initialDir were written over.
            ToolDescription tool4 = Settings.Default.ToolList.Last();
            Assert.AreEqual(title,tool4.Title);
            Assert.AreEqual("", tool4.Arguments);
            Assert.AreEqual("", tool4.InitialDirectory);
            // Specify skip
            string output6 = RunCommand("--tool-add=" + title,
                     "--tool-command=" + command,
                     "--tool-arguments=thisIsATest",
                     "--resolve-tool-conflicts=skip");
            Assert.IsTrue((output6.Contains("Warning: skipping")));
            // Check Arguments
            ToolDescription tool5 = Settings.Default.ToolList.Last();
            Assert.AreEqual(title, tool5.Title);
            Assert.AreEqual("", tool5.Arguments); // unchanged.
            
            // It now complains in this case.
            string output7 = RunCommand( "--tool-arguments=" + arguments,
                     "--tool-initial-dir=" + initialDirectory);
            Assert.IsTrue(output7.Contains("Error"));            
            Assert.IsTrue(output7.Contains("Exiting..."));            
        }

        [TestMethod]
        public void ConsoleAddSkyrTest()
        {
            int initialNumber = Settings.Default.ReportSpecList.Count;
            // Assumes the title TextREportexam is a unique title. 
            // Add test.skyr which only has one report type named TextREportexam
            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            var skyrFile = commandFilesDir.GetTestPath("test.skyr");
            string output = RunCommand("--skyr-add=" + skyrFile);
            Assert.AreEqual(initialNumber+1, Settings.Default.ReportSpecList.Count);
            Assert.AreEqual("TextREportexam", Settings.Default.ReportSpecList.Last().GetKey());
            Assert.IsTrue(output.Contains("Success"));
            var skyrAdded = Settings.Default.ReportSpecList.Last();

            // Attempt to add the same skyr again.
            string output2 = RunCommand("--skyr-add=" + skyrFile);
            Assert.IsTrue(output2.Contains("Error"));
            // Do want to use == to show it is the same object, unchanged
            Assert.IsTrue(skyrAdded == Settings.Default.ReportSpecList.Last());

            // Specify skip
            string output4 = RunCommand("--skyr-add=" + skyrFile,
                "--resolve-skyr-conflicts=skip");
            Assert.IsTrue(output4.Contains("skipping"));
            // Do want to use == to show it is the same object, unchanged
            Assert.IsTrue(skyrAdded == Settings.Default.ReportSpecList.Last());


            // Specify overwrite
            string output3 = RunCommand("--skyr-add=" + skyrFile,
                "--resolve-skyr-conflicts=overwrite");
            Assert.IsTrue(output3.Contains("overwriting"));
            // Do want to use == to show it is not the same object, changed
            Assert.IsFalse(skyrAdded == Settings.Default.ReportSpecList.Last());

        }

        [TestMethod]
        public void ConsoleRunCommandsTest()
        {
            int toolListCount = Settings.Default.ToolList.Count;
            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            var commandsToRun = commandFilesDir.GetTestPath("ToolList2.txt");
            string output = RunCommand("--run-commands=" + commandsToRun);            
            Assert.IsTrue(output.Contains("NeWtOOl was added to the Tools Menu"));
            Assert.IsTrue(output.Contains("iHope was added to the Tools Menu"));
            Assert.IsTrue(output.Contains("thisWorks was added to the Tools Menu"));
            Assert.IsTrue(output.Contains("FirstTry was added to the Tools Menu"));
            Assert.IsTrue(Settings.Default.ToolList.Exists(t => t.Title == "NeWtOOl" && t.Command == @"C:\Windows\Notepad.exe" && t.Arguments == "$(DocumentDir)" && t.InitialDirectory == @"C:\"));
            Assert.IsTrue(Settings.Default.ToolList.Exists(t => t.Title == "iHope" && t.Command == @"C:\Windows\Notepad.exe"));
            Assert.IsTrue(Settings.Default.ToolList.Exists(t => t.Title == "thisWorks"));
            Assert.IsTrue(Settings.Default.ToolList.Exists(t => t.Title == "FirstTry"));
            Assert.AreEqual(toolListCount+4, Settings.Default.ToolList.Count);

            // run the same command again. this time each should be skipped.
            string output2 = RunCommand("--run-commands=" + commandsToRun);
            Assert.IsFalse(output2.Contains("NeWtOOl was added to the Tools Menu"));
            Assert.IsFalse(output2.Contains("iHope was added to the Tools Menu"));
            Assert.IsFalse(output2.Contains("thisWorks was added to the Tools Menu"));
            Assert.IsFalse(output2.Contains("FirstTry was added to the Tools Menu"));
            Assert.IsTrue(Settings.Default.ToolList.Exists(t => t.Title == "NeWtOOl" && t.Command == @"C:\Windows\Notepad.exe" && t.Arguments == "$(DocumentDir)" && t.InitialDirectory == @"C:\"));
            Assert.IsTrue(Settings.Default.ToolList.Exists(t => t.Title == "iHope" && t.Command == @"C:\Windows\Notepad.exe"));
            Assert.IsTrue(Settings.Default.ToolList.Exists(t => t.Title == "thisWorks"));
            Assert.IsTrue(Settings.Default.ToolList.Exists(t => t.Title == "FirstTry"));
            // the number of tools is unchanged.
            Assert.AreEqual(toolListCount + 4, Settings.Default.ToolList.Count);

        }

        [TestMethod]
        public void ConsoleParserTest()
        {            
            // Assert.AreEqual(new[] { "--test=foo bar", "--new" }, CommandLine.ParseInput("\"--test=foo bar\" --new"));
            // The above line of code would not pass so this other form works better.
            // Test case "--test=foo bar" --new
            string[] expected1 = new[] { "--test=foo bar", "--new" };
            string[] actual1 = CommandLine.ParseInput("\"--test=foo bar\" --new");
            Assert.AreEqual(expected1[0], actual1[0]);
            Assert.AreEqual(expected1[1], actual1[1]);
            // Or even better. A function that does the same assertion as above.
            Assert.IsTrue(ParserTestHelper(new[] { "--test=foo bar", "--new" }, CommandLine.ParseInput("\"--test=foo bar\" --new")));

           // Test case --test="foo bar" --new
            string[] expected2 = new[] {"--test=foo bar", "--new"};
            string[] actual2 = CommandLine.ParseInput("--test=\"foo bar\" --new");
            Assert.AreEqual(expected2[0],actual2[0]);
            Assert.AreEqual(expected2[1],actual2[1]);
            Assert.IsTrue(ParserTestHelper(new[] { "--test=foo bar", "--new" }, CommandLine.ParseInput("--test=\"foo bar\" --new")));


            // Test case --test="i said ""foo bar""" -new
            string[] expected3 = new[] { "--test=i said \"foo bar\"", "--new" };
            string[] actual3 = CommandLine.ParseInput("--test=\"i said \"\"foo bar\"\"\" --new");
            Assert.AreEqual(expected3[0], actual3[0]);
            Assert.AreEqual(expected3[1], actual3[1]);
            Assert.IsTrue(ParserTestHelper(new[] { "--test=i said \"foo bar\"", "--new" }, CommandLine.ParseInput("--test=\"i said \"\"foo bar\"\"\" --new")));

            // Test case "--test=foo --new --bar"
            Assert.IsTrue(ParserTestHelper(new[] { "--test=foo --new --bar" }, CommandLine.ParseInput("\"--test=foo --new --bar\"")));
            
            // Test case --test="" --new --bar
            Assert.IsTrue(ParserTestHelper(new[] { "--test=", "--new", "--bar" }, CommandLine.ParseInput("--test=\"\" --new --bar")));

            // Test case of all spaces
            string[] test = CommandLine.ParseInput("     ");
            Assert.IsTrue(ParserTestHelper(new string[] {}, test));
        }

        private string GetTitleHelper()
        {
            int i = 1;
            do
            {
                if (Settings.Default.ToolList.All(item => item.Title != (string.Format("TestTool{0}", i))))
                {
                    return string.Format("TestTool{0}", i);
                }
                i++;
            } while (true);
        }

        // Compare two string arrays. Check each actual string is equal to the expected one.
        private static bool ParserTestHelper (string[] actual, string[] expected )
        {
            if (actual.Length == expected.Length)
            {
                for (int i = 0; i < actual.Length; i++)
                {
                    if (!actual[i].Equals(expected[i]))
                    {
                        return false;
                    }
                }
            }
            return true;

        }

        private string GetThermoDiskPath(string pathToRaw)
        {
            return ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw
                ? Path.ChangeExtension(pathToRaw, "raw")
                : pathToRaw;
        }
    }
}