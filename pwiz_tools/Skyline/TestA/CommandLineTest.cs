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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
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
            const string replicateName = "Single";

            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import=" + rawPath,
                               "--replicate=" + replicateName,
                               "--out=" + outPath
                           };

            Program.RunCommand(args, consoleOutput);

            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);
            //var docContainer = new ResultsTestDocumentContainer(doc, outPath, true);

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            AssertResult.IsDocumentResultsState(doc, replicateName, 3, 3, 0, 21, 0);

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

            //Before generating this report, check that it exists
            const string reportName = "Peptide Ratio Results";
            var defaultReportSpecs = Settings.Default.ReportSpecList.GetDefaults();
            Assert.IsNotNull(defaultReportSpecs.FirstOrDefault(r => r.Name.Equals(reportName)));
            Settings.Default.ReportSpecList = new ReportSpecList();
            Settings.Default.ReportSpecList.AddRange(defaultReportSpecs);

            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import=" + rawPath,
                               "--report=" + reportName,
                               "--separator=,",
                               "--reportfile=" + outPath                               
                           };

            string[] properReport = new[]
                                        {
                                            "PeptideSequence,ProteinName,ReplicateName,PeptidePeakFoundRatio,PeptideRetentionTime,RatioToStandard"
                                            ,
                                            "LVNELTEFAK,sp|P02769|ALBU_BOVIN,ah_20101011y_BSA_MS-MS_only_5-2,1,46.66,#N/A"
                                            ,
                                            "HLVDEPQNLIK,sp|P02769|ALBU_BOVIN,ah_20101011y_BSA_MS-MS_only_5-2,1,42.37,#N/A"
                                            ,
                                            "KVPQVSTPTLVEVSR,sp|P02769|ALBU_BOVIN,ah_20101011y_BSA_MS-MS_only_5-2,1,44.21,#N/A"
                                        };

            Program.RunCommand(args, consoleOutput);

            string consoleText = consoleBuffer.ToString();
            Assert.IsFalse(consoleText.ToLower().Contains("error"), consoleText);

            try
            {
                using (var fileStream = new FileStream(outPath, FileMode.Open))
                using (var stream = new StreamReader(fileStream))
                {
                    int i = 0;
                    string line;
                    while (!String.IsNullOrEmpty(line = stream.ReadLine()))
                    {
                        Assert.AreEqual(line, properReport[i]);
                        i++;
                    }
                    Assert.AreEqual(i, 4);
                }
            }
            catch(FileNotFoundException)
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void ConsoleMassListTest()
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new StringWriter(consoleBuffer);

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                                                      ExtensionTestContext.ExtThermoRaw);

            /////////////////////////
            // Thermo test
            string thermoPath = testFilesDir.GetTestPath("Thermo_test.csv");

            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import=" + rawPath,
                               "--exp-translist-instrument=Thermo",
                               "--exp-translist-out=" + thermoPath   
                           };

            Program.RunCommand(args, consoleOutput);

            //check for success
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));


            /////////////////////////
            // Agilent test
            string agilentPath = testFilesDir.GetTestPath("Agilent_test.csv");

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath,
                               "--import=" + rawPath,
                               "--exp-translist-instrument=Agilent",
                               "--exp-translist-out=" + agilentPath,
                               "--exp-dwelltime=20"
                           };

            Program.RunCommand(args, consoleOutput);

            //check for success
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));

            /////////////////////////
            // AB Sciex test
            string sciexPath = testFilesDir.GetTestPath("AB_Sciex_test.csv");

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath,
                               "--import=" + rawPath,
                               "--exp-translist-instrument=AB SCIEX",
                               "--exp-translist-out=" + sciexPath,
                               "--exp-dwelltime=20"
                           };

            Program.RunCommand(args, consoleOutput);

            //check for success
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));

            /////////////////////////
            // Waters test
            string watersPath = testFilesDir.GetTestPath("Waters_test.csv");

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            args = new[]
                           {
                               "--in=" + docPath,
                               "--import=" + rawPath,
                               "--exp-translist-instrument=Waters",
                               "--exp-translist-out=" + watersPath,
                               "--exp-runlength=100"
                           };

            Program.RunCommand(args, consoleOutput);

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

            Program.RunCommand(new[]
                                   {
                                       "--in=" + bogusPath
                                   },
                                   consoleOutput);
            //Error: file does not exist
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            Program.RunCommand(new[]
                                   {
                                       "--in=" + docPath,
                                       "--import=" + rawPath,
                                       "--save",
                                       "--out=" + outPath,
                                       "--separator=TAB",
                                       "--report=" + "Peptide Ratio Results"
                                    }, consoleOutput);
            //Error: no reportfile
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            Program.RunCommand(new[]
                                   {
                                       "--in=" + docPath,
                                       "--import=" + rawPath,
                                       "--save",
                                       "--out=" + outPath,
                                       "--reportfile=" + tsvPath,                                       
                                       "--report=" + "Peptide Ratio Results"
                                    }, consoleOutput);


            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            Program.RunCommand(new[]
                                   {
                                       "--in=" + docPath,
                                       "--import=" + rawPath,
                                       "--save",
                                       "--out=" + outPath,
                                       "--reportfile=" + tsvPath,                                       
                                       "--report=" + "Bogus Report"
                                    }, consoleOutput);
            //Error: no such report
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            Program.RunCommand(new[]
                                   {
                                       "--import=" + rawPath,
                                       "--save"
                                    }, consoleOutput);
            //Error: no --in specified with --import
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            Program.RunCommand(new[]
                                   {
                                       "--out=" + outPath,
                                       "--reportfile=" + tsvPath,                                       
                                       "--report=" + "Bogus Report"
                                    }, consoleOutput);
            //Error: no --in specified with --report
            Assert.IsTrue(consoleBuffer.ToString().Contains("Error"));



            //check for success. This is merely to cover more paths

            string watersPath = testFilesDir.GetTestPath("Waters_test.csv");

            consoleBuffer = new StringBuilder();
            consoleOutput = new StringWriter(consoleBuffer);

            var args = new[]
                           {
                               "--in=" + docPath,
                               "--import=" + rawPath,
                               "--exp-translist-instrument=Waters",
                               "--exp-translist-out=" + watersPath,
                               "--exp-runlength=100",
                               "--exp-optimizing=ce",
                               "--exp-strategy=protein",
                               "--exp-max-trans=100",
                               "--exp-scheduling-algorithm=single",
                               "--exp-scheduling-replicate-index=1"                               
                           };

            Program.RunCommand(args, consoleOutput);

            //check for success
            Assert.IsTrue(consoleBuffer.ToString().Contains("successfully."));

        }
    }
}