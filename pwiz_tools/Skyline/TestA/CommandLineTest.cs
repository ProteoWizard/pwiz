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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
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
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //

        #endregion

        private const string ZIP_FILE = @"TestA\Results\FullScan.zip";
        private const string COMMAND_FILE = @"TestA\CommandLineTest.zip";

        [TestMethod]
        public void ConsoleReplicateOutTest()
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new StringWriter(consoleBuffer);
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Imported_single.sky");

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);

            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + rawPath,
                               "--import-replicate-name=Single",
                               "--out=" + outPath
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);

            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            AssertResult.IsDocumentResultsState(doc, "Single", 3, 3, 0, 21, 0);



            //Test --import-append
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            var dataFile2 = testFilesDir.GetTestPath("ah_20101029r_BSA_CID_FT_centroid_3uscan_3" +
                ExtensionTestContext.ExtThermoRaw);

            CommandLineRunner.RunCommand(new[]
                                   {
                                       "--in=" + outPath,
                                       "--import-file=" + dataFile2,
                                       "--import-replicate-name=Single",
                                       "--import-append",
                                       "--save"
                                    }, consoleOutput);

            doc = ResultsUtil.DeserializeDocument(outPath);

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            AssertResult.IsDocumentResultsState(doc, "Single", 6, 6, 0, 42, 0);

            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
        }

        [TestMethod]
        public void ConsoleReportExportTest()
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new StringWriter(consoleBuffer);

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Exported_test_report.csv");

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);
            const string replicate = "Single";

            //Before generating this report, check that it exists
            const string reportName = "Peptide Ratio Results";
            var defaultReportSpecs = Settings.Default.ReportSpecList.GetDefaults();
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
            doc = CommandLine.ImportResults(doc, docPath, "Single", rawPath);

            Database database = new Database(doc.Settings);
            database.AddSrmDocument(doc);
            ResultSet resultSet = report.Execute(database);

            ResultSet.WriteReportHelper(resultSet, TextUtil.GetCsvSeparator(CultureInfo.CurrentCulture), reportWriter,
                                              CultureInfo.CurrentCulture);

            reportWriter.Flush();

            reportWriter.Close();

            string programmaticReport = reportBuffer.ToString();
            
            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + rawPath,
                               "--import-replicate-name=" + replicate,                               
                               "--report-name=Peptide Ratio Results",
                               "--report-format=CSV",
                               "--report-file=" + outPath                               
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);

            string reportLines = File.ReadAllText(outPath);
            AssertEx.NoDiff(reportLines, programmaticReport);
        }

        [TestMethod]
        public void ConsoleMassListTest()
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new StringWriter(consoleBuffer);

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");

            var doc = ResultsUtil.DeserializeDocument(docPath);

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                                                      ExtensionTestContext.ExtThermoRaw);

            /////////////////////////
            // Thermo test
            string thermoPath = testFilesDir.GetTestPath("Thermo_test.csv");

            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + rawPath,
                               "--exp-translist-instrument=" + ExportInstrumentType.Thermo,
                               "--exp-file=" + thermoPath   
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);
            
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));
            Assert.IsTrue(File.Exists(thermoPath));
            Assert.AreEqual(doc.TransitionCount, File.ReadAllLines(thermoPath).Length);


            /////////////////////////
            // Agilent test
            string agilentPath = testFilesDir.GetTestPath("Agilent_test.csv");

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + rawPath,
                               "--exp-translist-instrument=" + ExportInstrumentType.Agilent,
                               "--exp-file=" + agilentPath,
                               "--exp-dwelltime=20"
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);

            //check for success
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));
            Assert.IsTrue(File.Exists(agilentPath));
            Assert.AreEqual(doc.TransitionCount + 1, File.ReadAllLines(agilentPath).Length);

            /////////////////////////
            // AB Sciex test
            string sciexPath = testFilesDir.GetTestPath("AB_Sciex_test.csv");

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + rawPath,
                               "--exp-translist-instrument=" + ExportInstrumentType.ABI,
                               "--exp-file=" + sciexPath,
                               "--exp-dwelltime=20"
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);

            //check for success
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));
            Assert.IsTrue(File.Exists(sciexPath));
            Assert.AreEqual(doc.TransitionCount, File.ReadAllLines(sciexPath).Length);

            /////////////////////////
            // Waters test
            string watersPath = testFilesDir.GetTestPath("Waters_test.csv");

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + rawPath,
                               "--exp-translist-instrument=" + ExportInstrumentType.Waters,
                               "--exp-file=" + watersPath,
                               "--exp-runlength=100"
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);

            //check for success
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));
            Assert.IsTrue(File.Exists(watersPath));
            Assert.AreEqual(doc.TransitionCount + 1, File.ReadAllLines(watersPath).Length);
        }

        [TestMethod]
        public void ConsoleMethodTest()
        {
            StringBuilder consoleBuffer; // = new StringBuilder();
            TextWriter consoleOutput; // = new StringWriter(consoleBuffer);

            //var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            //string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");

            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);

            string[] args;

            //Here I'll only test Agilent for now

            /////////////////////////
            // Thermo test
//            string thermoTemplate = methodFilesDir.GetTestPath("20100329_Protea_Peptide_targeted.meth");
//            string thermoOut = methodFilesDir.GetTestPath("Thermo_test.meth");
//            args = new[]
//                           {
//                               "--in=" + docPath,
//                               "--import-file=" + rawPath,
//                               "--exp-method-instrument=Thermo LTQ",
//                               "--exp-template=" + thermoTemplate,                        
//                               "--exp-file=" + thermoOut,
//                               "--exp-strategy=buckets",
//                               "--exp-max-trans=130",
//                               "--exp-optimizing=ce",
//                               "--exp-full-scans"                               
//                           };
//
//            CommandLineRunner.RunCommand(args, consoleOutput);
            //check for success
//            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));

            
            /////////////////////////
            // Agilent test
            string docPath2 = commandFilesDir.GetTestPath("WormUnrefined.sky");
            string agilentTemplate = commandFilesDir.GetTestPath("43mm-40nL-30min-opt.m");
            string agilentOut = commandFilesDir.GetTestPath("Agilent_test.m");

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath2,
                               "--exp-method-instrument=Agilent 6400 Series",
                               "--exp-template=" + agilentTemplate,                               
                               "--exp-file=" + agilentOut,
                               "--exp-dwell-time=20",
                               "--exp-strategy=buckets",                               
                               "--exp-max-trans=75",
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);

            //check for success
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));
            
        }

        [TestMethod]
        public void ConsolePathCoverage()
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new StringWriter(consoleBuffer);

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string bogusPath = testFilesDir.GetTestPath("bogus_file.sky");
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Output_file.sky");
            string tsvPath = testFilesDir.GetTestPath("Exported_test_report.csv");

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);


            //Error: file does not exist
            CommandLineRunner.RunCommand(new[]
                                   {
                                       "--in=" + bogusPath
                                   },
                                   consoleOutput);
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));


            //Error: no reportfile
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            CommandLineRunner.RunCommand(new[]
                                   {
                                       "--in=" + docPath,
                                       "--import-file=" + rawPath,
                                       "--import-replicate-name=Single",                                       
                                       "--out=" + outPath,
                                       "--report-format=TSV",
                                       "--report-name=" + "Peptide Ratio Results"
                                    }, consoleOutput);
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));


            //Error: no such report
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            CommandLineRunner.RunCommand(new[]
                                   {
                                       "--in=" + docPath,
                                       "--import-file=" + rawPath,
                                       "--report-file=" + tsvPath,                                       
                                       "--report-name=" + "Bogus Report"
                                    }, consoleOutput);
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));


            //Error: no --in specified with --import-file
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            CommandLineRunner.RunCommand(new[]
                                   {
                                       "--import-file=" + rawPath,
                                       "--save"
                                    }, consoleOutput);
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));


            //Error: no --in specified with --report
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            CommandLineRunner.RunCommand(new[]
                                   {
                                       "--out=" + outPath,
                                       "--report-file=" + tsvPath,                                       
                                       "--report-name=" + "Bogus Report"
                                    }, consoleOutput);
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));



            //check for success. This is merely to cover more paths
            string watersPath = testFilesDir.GetTestPath("Waters_test.csv");
            
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + rawPath,
                               "--exp-translist-instrument=Waters",
                               "--exp-file=" + watersPath,
                               "--exp-method-type=scheduled",                               
                               "--exp-run-length=100",
                               "--exp-optimizing=ce",
                               "--exp-strategy=protein",
                               "--exp-max-trans=100",
                               "--exp-scheduling-replicate=LAST"                               
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));


            //check for success
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath,
                               "--import-file=" + rawPath,
                               "--import-replicate-name=Single",                               
                               "--exp-translist-instrument=Waters",
                               "--exp-file=" + watersPath,
                               "--exp-method-type=scheduled",
                               "--exp-run-length=100",
                               "--exp-optimizing=ce",
                               "--exp-strategy=buckets",
                               "--exp-max-trans=10000000",
                               "--exp-scheduling-replicate=Single"                               
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));


            //Check a bunch of warnings
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath,
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
                               "--exp-method-instrument=Thermo LTQ",
                               //1 Error for using the above 2 parameters simultaneously
                           };

            CommandLineRunner.RunCommand(args, consoleOutput);

            string buf = consoleBuffer.ToString();
            Assert.IsFalse(buf.Contains("successfully."));

            Assert.AreEqual(CountInstances("Warning", buf), 11);
            Assert.AreEqual(CountInstances("Error", buf), 1);


            //This test uses a broken Skyline file to test the InvalidDataException catch
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            var brokenFile = commandFilesDir.GetTestPath("Broken_file.sky");

            CommandLineRunner.RunCommand(new[]
                                   {
                                       "--in=" + brokenFile
                                    }, consoleOutput);
            Assert.AreEqual(1, CountInstances("Error",consoleBuffer.ToString()));
            AssertEx.Contains(consoleBuffer.ToString(), new[] { "line", "column" });


            //This test uses a broken Skyline file to test the InvalidDataException catch
            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            var invalidFile = commandFilesDir.GetTestPath("InvalidFile.sky");

            CommandLineRunner.RunCommand(new[]
                                   {
                                       "--in=" + invalidFile
                                    }, consoleOutput);
            Assert.AreEqual(1, CountInstances("Error", consoleBuffer.ToString()));
            AssertEx.Contains(consoleBuffer.ToString(), new[] {"line", "column"});

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
            int lastIndex = searchSpace.IndexOf(search);
            for (; !Equals(-1, lastIndex) && lastIndex + search.Length <= searchSpace.Length; count++)
            {
                lastIndex = searchSpace.IndexOf(search);
                searchSpace = searchSpace.Substring(lastIndex + 1);
                lastIndex = searchSpace.IndexOf(search);
            }

            return count;
        }
    }
}