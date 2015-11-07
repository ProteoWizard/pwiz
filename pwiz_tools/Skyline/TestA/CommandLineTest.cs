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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NHibernate.Util;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for CommandLineTest
    /// </summary>
    [TestClass]
    public class CommandLineTest : AbstractUnitTest
    {
        protected override void Initialize()
        {
            Settings.Default.ToolList.Clear();
        }

        protected override void Cleanup()
        {
            Settings.Default.ToolList.Clear();
        }        

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
        public void ConsoleRemoveResultsTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("Remove_Test.sky");
            string outPath = testFilesDir.GetTestPath("Remove_Test_Out.sky");
            string[] allFiles =
            {
                "FT_2012_0311_RJ_01.raw",
                "FT_2012_0311_RJ_02.raw",
                "FT_2012_0311_RJ_07.raw",
                "FT_2012_0316_RJ_01_120316125013.raw",
                "FT_2012_0316_RJ_01_120316131853.raw",
                "FT_2012_0316_RJ_01_120316132340.raw",
                "FT_2012_0316_RJ_02.raw",
                "FT_2012_0316_RJ_09.raw",
                "FT_2012_0316_RJ_10.raw",
            };
            string[] removedFiles =
            {
                "FT_2012_0311_RJ_01.raw",
                "FT_2012_0311_RJ_02.raw",
                "FT_2012_0311_RJ_07.raw"
            };

            string output = RunCommand("--in=" + docPath,
                                       "--remove-before=" + DateTime.Parse("3/16/2012", CultureInfo.InvariantCulture),
                                       "--out=" + outPath);

            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));
            
            // check for removed filenames
//            Assert.AreEqual(removedFiles.Count(), Regex.Matches(output, "\nRemoved").Count);  L10N problem
            AssertEx.Contains(output, removedFiles);

            AssertEx.IsDocumentState(doc, 0, 1, 5, 5, 15);
            Assert.AreEqual(6, doc.Settings.MeasuredResults.Chromatograms.Count);

            // try to remove all
            output = RunCommand("--in=" + docPath,
                                "--remove-before=" + DateTime.Parse("3/16/2013", CultureInfo.InvariantCulture),
                                "--out=" + outPath);

            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));

//            Assert.AreEqual(allFiles.Count(), Regex.Matches(output, "\nRemoved").Count);  L10N problem
            AssertEx.Contains(output, allFiles);

            Assert.IsNull(doc.Settings.MeasuredResults);
        }

        // TODO: Enable this again once file locking issues have been resolved
        //[TestMethod]
        public void ConsoleSetLibraryTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("SetLib_Out.sky");
            string libPath = testFilesDir.GetTestPath("sample.blib");
            string libPath2 = testFilesDir.GetTestPath("sample2.blib");
            const string libName = "namedlib";
            string fakePath = docPath + ".fake";
            string libPathRedundant = testFilesDir.GetTestPath("sample.redundant.blib");

            // Test error (name without path)
            string output = RunCommand("--in=" + docPath,
                                "--add-library-name=" + libName,
                                "--out=" + outPath);
            CheckRunCommandOutputContains(Resources.CommandLine_SetLibrary_Error__Cannot_set_library_name_without_path_, output);

            // Test error (file does not exist)
            output = RunCommand("--in=" + docPath,
                                "--add-library-path=" + fakePath,
                                "--out=" + outPath);
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_SetLibrary_Error__The_file__0__does_not_exist_, fakePath), output);

            // Test error (file does not exist)
            output = RunCommand("--in=" + docPath,
                                "--add-library-path=" + libPathRedundant,
                                "--out=" + outPath);
            CheckRunCommandOutputContains(Resources.CommandLineTest_ConsoleAddFastaTest_Error, output);

            // Test error (unsupported library format)
            output = RunCommand("--in=" + docPath,
                                "--add-library-path=" + docPath,
                                "--out=" + outPath);
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_SetLibrary_Error__The_file__0__is_not_a_supported_spectral_library_file_format_,docPath), output);

            // Test add library without name
            output = RunCommand("--in=" + docPath,
                                "--add-library-path=" + libPath,
                                "--out=" + outPath);

            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            Assert.AreEqual(doc.Settings.PeptideSettings.Libraries.Libraries.Count,
                doc.Settings.PeptideSettings.Libraries.LibrarySpecs.Count);
            Assert.AreEqual(1, doc.Settings.PeptideSettings.Libraries.LibrarySpecs.Count);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(libPath), doc.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);
            Assert.AreEqual(libPath, doc.Settings.PeptideSettings.Libraries.LibrarySpecs[0].FilePath);

            // Add another library with name
            output = RunCommand("--in=" + outPath,
                                "--add-library-name=" + libName,
                                "--add-library-path=" + libPath2,
                                "--save");

            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            Assert.AreEqual(doc.Settings.PeptideSettings.Libraries.Libraries.Count,
                doc.Settings.PeptideSettings.Libraries.LibrarySpecs.Count);
            Assert.AreEqual(2, doc.Settings.PeptideSettings.Libraries.LibrarySpecs.Count);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(libPath), doc.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);
            Assert.AreEqual(libPath, doc.Settings.PeptideSettings.Libraries.LibrarySpecs[0].FilePath);
            Assert.AreEqual(libName, doc.Settings.PeptideSettings.Libraries.LibrarySpecs[1].Name);
            Assert.AreEqual(libPath2, doc.Settings.PeptideSettings.Libraries.LibrarySpecs[1].FilePath);

            // Test error (library with conflicting name)
            output = RunCommand("--in=" + outPath,
                                "--add-library-path=" + libPath,
                                "--out=" + outPath);
            CheckRunCommandOutputContains(Resources.CommandLine_SetLibrary_Error__The_library_you_are_trying_to_add_conflicts_with_a_library_already_in_the_file_, output);
        }

        [TestMethod]
        public void ConsoleAddFastaTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("AddFasta_Out.sky");
            string fastaPath = testFilesDir.GetTestPath("sample.fasta");


            string output = RunCommand("--in=" + docPath,
                                       "--import-fasta=" + fastaPath,
                                       "--keep-empty-proteins",
                                       "--out=" + outPath);

            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));

            // Before import, there are 2 peptides. 3 peptides after
            AssertEx.IsDocumentState(doc, 0, 3, 7, 7, 49);

            // Test without keep empty proteins
            output = RunCommand("--in=" + docPath,
                                "--import-fasta=" + fastaPath,
                                "--out=" + outPath);

            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
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
            string reportName = Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results;
            Settings.Default.PersistedViews.ResetDefaults();
            Assert.IsNotNull(Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id)
                .GetView(Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results));

            //First, programmatically generate the report
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);

            //Attach replicate
            ProgressStatus status;
            doc = CommandLine.ImportResults(doc, docPath, replicate, MsDataFileUri.Parse(rawPath), null, null, out status);
            Assert.IsNull(status);

            string programmaticReport;
            MemoryDocumentContainer memoryDocumentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(memoryDocumentContainer.SetDocument(doc, memoryDocumentContainer.Document));
            SkylineDataSchema skylineDataSchema = new SkylineDataSchema(memoryDocumentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            DocumentGridViewContext viewContext = new DocumentGridViewContext(skylineDataSchema);
            ViewInfo viewInfo = viewContext.GetViewInfo(PersistedViews.MainGroup.Id.ViewName(reportName));
            StringWriter writer = new StringWriter();
            status = new ProgressStatus("Exporting report");
            viewContext.Export(null, ref status, viewInfo, writer,
                new DsvWriter(CultureInfo.CurrentCulture, TextUtil.GetCsvSeparator(LocalizationHelper.CurrentCulture)));
            programmaticReport = writer.ToString();

            RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath,
                       "--import-replicate-name=" + replicate,
                       "--report-name=" + reportName,
                       "--report-format=CSV",
                       "--report-file=" + outPath);

            string reportLines = File.ReadAllText(outPath);
            AssertEx.NoDiff(reportLines, programmaticReport);
        }

        [TestMethod]
        public void ConsoleChromatogramExportTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Exported_chromatograms.csv");

            // Import the first RAW file (or mzML for international)
            string rawFile = "ah_20101011y_BSA_MS-MS_only_5-2" + ExtensionTestContext.ExtThermoRaw;
            string rawPath = testFilesDir.GetTestPath(rawFile);
            const string replicate = "Single";

            //Attach replicate
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            ProgressStatus status;
            doc = CommandLine.ImportResults(doc, docPath, replicate, MsDataFileUri.Parse(rawPath), null, null, out status);
            Assert.IsNull(status);

            //First, programmatically generate the report
            var chromFiles = new[] { rawFile };
            var chromExporter = new ChromatogramExporter(doc);
            var chromExtractors = new[] { ChromExtractor.summed, ChromExtractor.base_peak };
            var chromSources = new[] { ChromSource.ms1, ChromSource.fragment };
            var chromBuffer = new StringBuilder();
            using (var chromWriter = new StringWriter(chromBuffer))
            {
                chromExporter.Export(chromWriter, null, chromFiles, LocalizationHelper.CurrentCulture, chromExtractors,
                    chromSources);
            }
            doc.Settings.MeasuredResults.ReadStreams.ForEach(s => s.CloseStream());
            string programmaticReport = chromBuffer.ToString();

            RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath,
                       "--import-replicate-name=" + replicate,
                       "--chromatogram-file=" + outPath,
                       "--chromatogram-precursors",
                       "--chromatogram-products",
                       "--chromatogram-base-peaks",
                       "--chromatogram-tics");

            string chromLines = File.ReadAllText(outPath);
            AssertEx.NoDiff(chromLines, programmaticReport);
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

            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "Thermo_test.csv"), output);
            Assert.IsTrue(File.Exists(thermoPath));
            Assert.AreEqual(doc.MoleculeTransitionCount, File.ReadAllLines(thermoPath).Length);


            /////////////////////////
            // Agilent test
            string agilentPath = testFilesDir.GetTestPath("Agilent_test.csv");

            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.AGILENT,
                                "--exp-file=" + agilentPath,
                                "--exp-dwell-time=20");

            //check for success
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "Agilent_test.csv"), output);
            Assert.IsTrue(File.Exists(agilentPath));
            Assert.AreEqual(doc.MoleculeTransitionCount + 1, File.ReadAllLines(agilentPath).Length);

            /////////////////////////
            // AB Sciex test
            string sciexPath = testFilesDir.GetTestPath("AB_Sciex_test.csv");


            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.ABI,
                                "--exp-file=" + sciexPath,
                                "--exp-dwell-time=20");

            //check for success
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "AB_Sciex_test.csv"), output);
            Assert.IsTrue(File.Exists(sciexPath));
            Assert.AreEqual(doc.MoleculeTransitionCount, File.ReadAllLines(sciexPath).Length);

            /////////////////////////
            // Waters test
            string watersPath = testFilesDir.GetTestPath("Waters_test.csv");

            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                                "--exp-file=" + watersPath,
                                "--exp-run-length=100");

            //check for success
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "Waters_test.csv"), output);
            Assert.IsTrue(File.Exists(watersPath));
            Assert.AreEqual(doc.MoleculeTransitionCount + 1, File.ReadAllLines(watersPath).Length);
        }

        [TestMethod]
        public void ConsoleMethodTest()
        {
            //Here I'll only test Agilent for now
            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);

            /////////////////////////
            // Thermo test
//            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
//            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
//            string thermoTemplate = commandFilesDir.GetTestPath("20100329_Protea_Peptide_targeted.meth");
//            string thermoOut = commandFilesDir.GetTestPath("Thermo_test.meth");
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
//            CheckRunCommandOutputContains("successfully.", output);

            
            /////////////////////////
            // Agilent test
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
                success = output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_Method__0__exported_successfully_, "Agilent_test.m"));

                // Relax a bit if things aren't going well.
                if (!success)
                    Thread.Sleep(5000);
            }

            if (!success)
            {
// ReSharper disable LocalizableElement
                Console.WriteLine("Failed to write Agilent method: {0}", output);   // Not L10N
// ReSharper restore LocalizableElement
                Assert.IsTrue(success);
            }
        }

        [TestMethod]
        public void ConsoleExportTrigger()
        {
            // The special mode for exercising non-proteomic molecules just doesn't make sense with this test
            TestSmallMolecules = false; 

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string failurePath = testFilesDir.GetTestPath("Failure_test.csv");

            string output = RunCommand("--in=" + docPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                                "--exp-file=" + failurePath,
                                "--exp-strategy=single",
                                "--exp-method-type=triggered",
                                "--exp-primary-count=x");

            //check for warning and error
            Assert.AreEqual(2, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Warning, output));  // exp-primary-count and CE not Waters
            CheckRunCommandOutputContains(Resources.CommandLineTest_ConsoleAddFastaTest_Error, output);    // Waters
            Assert.AreEqual(2, CountInstances(ExportInstrumentType.WATERS, output));

            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            string thermoTemplate = commandFilesDir.GetTestPath("20100329_Protea_Peptide_targeted.meth");
            output = RunCommand("--in=" + docPath,
                                "--exp-method-instrument=" + ExportInstrumentType.THERMO_TSQ,
                                "--exp-template=" + thermoTemplate,                        
                                "--exp-file=" + failurePath,
                                "--exp-strategy=single",
                                "--exp-method-type=triggered");
            Assert.IsTrue(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));    // Thermo TSQ method
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));
            Assert.AreEqual(2, CountInstances(ExportInstrumentType.THERMO, output));    // Thermo and Thermo TSQ
            Assert.AreEqual(1, CountInstances(ExportInstrumentType.THERMO_TSQ, output));

            output = RunCommand("--in=" + docPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.AGILENT,
                                "--exp-file=" + failurePath,
                                "--exp-strategy=single",
                                "--exp-method-type=triggered");
            Assert.AreEqual(1, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Warning, output));  // exp-primary-count and CE not Agilent
            Assert.AreEqual(1, CountInstances(ExportInstrumentType.AGILENT, output));   // CE not Agilent
            Assert.IsTrue(output.Contains(Resources.CommandLine_ExportInstrumentFile_Error__triggered_acquistion_requires_a_spectral_library_or_imported_results_in_order_to_rank_transitions_));    // No library and no data

            // Successful export to Agilent transtion list
            string triggerPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi_triggered.sky");
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);
            const string replicate = "Single";
            string agilentTriggeredPath = testFilesDir.GetTestPath("AgilentTriggered.csv");

            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--import-replicate-name=" + replicate,
                                "--out=" + triggerPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.AGILENT,
                                "--exp-file=" + agilentTriggeredPath,
                                "--exp-strategy=single",
                                "--exp-method-type=triggered");
            Assert.AreEqual(1, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Warning, output));  // exp-primary-count and CE not Agilent
            Assert.AreEqual(1, CountInstances(ExportInstrumentType.AGILENT, output));   // CE not Agilent
            Assert.IsTrue(output.Contains(Resources.CommandLine_ExportInstrumentFile_Error__The_current_document_contains_peptides_without_enough_information_to_rank_transitions_for_triggered_acquisition_)); // peptides without enough information

            //check for success
            var doc = ResultsUtil.DeserializeDocument(triggerPath);
            var ceRegression = new CollisionEnergyRegression("Agilent", new[] {new ChargeRegressionLine(2, 2, 10)});
            doc = doc.ChangeSettings(doc.Settings.ChangeTransitionPrediction(
                p => p.ChangeCollisionEnergy(ceRegression)));
            doc = (SrmDocument) doc.RemoveChild(doc.Children[1]);
            CommandLine.SaveDocument(doc, triggerPath, Console.Out);

            output = RunCommand("--in=" + triggerPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.AGILENT,
                                "--exp-file=" + agilentTriggeredPath,
                                "--exp-strategy=single",
                                "--exp-method-type=triggered");
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "AgilentTriggered.csv")));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));
            Assert.IsTrue(File.Exists(agilentTriggeredPath));
            Assert.AreEqual(doc.PeptideTransitionCount + 1, File.ReadAllLines(agilentTriggeredPath).Length);
        }

        [TestMethod]
        public void ConsolePathCoverage()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string bogusPath = testFilesDir.GetTestPath("bogus_file.sky");
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Output_file.sky");
            string tsvPath = testFilesDir.GetTestPath("Exported_test_report.csv");

            // The special mode for exercising non-proteomic molecules just doesn't make sense with this test
            TestSmallMolecules = false; 

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);


            //Error: file does not exist
            string output = RunCommand("--in=" + bogusPath);
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_OpenSkyFile_Error__The_Skyline_file__0__does_not_exist_, bogusPath)));

            //Error: no raw file
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath + "x",
                                "--import-replicate-name=Single");
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_CanReadFile_Error__File_does_not_exist___0__,rawPath+"x")));

            //Error: no reportfile
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--import-replicate-name=Single",
                                "--out=" + outPath,
                                "--report-format=TSV",
                                "--report-name=" + "Peptide Ratio Results");
            Assert.IsTrue(output.Contains(Resources.CommandLine_ExportReport_));


            //Error: no such report
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--report-file=" + tsvPath,
                                "--report-name=" + "Bogus Report");
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportReport_Error__The_report__0__does_not_exist__If_it_has_spaces_in_its_name__use__double_quotes__around_the_entire_list_of_command_parameters_,"Bogus Report")));


            //Error: no --in specified with --import-file
            output = RunCommand("--import-file=" + rawPath,
                                "--save");
            Assert.IsTrue(output.Contains(Resources.CommandArgs_ParseArgsInternal_Error__Use___in_to_specify_a_Skyline_document_to_open_));


            //Error: no --in specified with --report
            output = RunCommand("--out=" + outPath,
                                "--report-file=" + tsvPath,
                                "--report-name=" + "Bogus Report");
            Assert.IsTrue(output.Contains(Resources.CommandArgs_ParseArgsInternal_Error__Use___in_to_specify_a_Skyline_document_to_open_));

            //Error: no template
            output = RunCommand("--in=" + docPath,
                                "--exp-method-instrument=" + ExportInstrumentType.THERMO_LTQ,
                                "--exp-method-type=scheduled",
                                "--exp-strategy=single",
                                "--exp-file=" + testFilesDir.GetTestPath("Bogus.meth"));
            Assert.IsTrue(output.Contains(Resources.CommandLine_ExportInstrumentFile_Error__A_template_file_is_required_to_export_a_method_));
            Assert.IsFalse(output.Contains(Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_));

            //Error: template does not exist
            output = RunCommand("--in=" + docPath,
                                "--exp-method-instrument=" + ExportInstrumentType.THERMO_LTQ,
                                "--exp-method-type=scheduled",
                                "--exp-strategy=single",
                                "--exp-file=" + testFilesDir.GetTestPath("Bogus.meth"),
                                "--exp-template=" + testFilesDir.GetTestPath("Bogus_template.meth"));
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_Error__The_template_file__0__does_not_exist_, testFilesDir.GetTestPath("Bogus_template.meth"))));
            Assert.IsFalse(output.Contains(Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_));

            //Error: can't schedule instrument type
            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            string thermoTemplate = commandFilesDir.GetTestPath("20100329_Protea_Peptide_targeted.meth");
            output = RunCommand("--in=" + docPath,
                                "--exp-method-instrument=" + ExportInstrumentType.THERMO_LTQ,
                                "--exp-method-type=scheduled",
                                "--exp-strategy=single",
                                "--exp-file=" + testFilesDir.GetTestPath("Bogus.meth"),
                                "--exp-template=" + thermoTemplate);
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_Error__the_specified_instrument__0__is_not_compatible_with_scheduled_methods_,"Thermo LTQ")));
            Assert.IsTrue(output.Contains(Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_));

            //Error: not all peptides have RT info
            const string watersFilename = "Waters_test.csv";
            string watersPath = testFilesDir.GetTestPath(watersFilename);
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                                "--exp-file=" + watersPath,
                                "--exp-method-type=scheduled",
                                "--exp-run-length=100",
                                "--exp-optimizing=ce",
                                "--exp-strategy=protein",
                                "--exp-max-trans=100",
                                "--exp-scheduling-replicate=LAST");
            Assert.IsTrue(output.Contains(Resources.CommandLine_ExportInstrumentFile_Error__to_export_a_scheduled_method__you_must_first_choose_a_retention_time_predictor_in_Peptide_Settings___Prediction__or_import_results_for_all_peptides_in_the_document_));
            Assert.IsTrue(output.Contains(Resources.CommandLine_ExportInstrumentFile_No_list_will_be_exported_));

            //check for success. This is merely to cover more paths
            string schedulePath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi_scheduled.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            doc = (SrmDocument)doc.RemoveChild(doc.Children[1]);
            CommandLine.SaveDocument(doc, schedulePath, Console.Out);
            docPath = schedulePath;

            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                                "--exp-file=" + watersPath,
                                "--exp-method-type=scheduled",
                                "--exp-run-length=100",
                                "--exp-optimizing=ce",
                                "--exp-strategy=protein",
                                "--exp-max-trans=100",
                                "--exp-scheduling-replicate=LAST");
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, watersFilename)));


            //check for success
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--import-replicate-name=Single",
                                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                                "--exp-file=" + watersPath,
                                "--exp-method-type=scheduled",
                                "--exp-run-length=100",
                                "--exp-optimizing=ce",
                                "--exp-strategy=buckets",
                                "--exp-max-trans=10000000",
                                "--exp-scheduling-replicate=Single");
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "Waters_test.csv")));


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
                                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                                "--exp-method-instrument=" + ExportInstrumentType.THERMO_LTQ);
                                //1 Error for using the above 2 parameters simultaneously

            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsolePathCoverage_successfully_));

            Assert.AreEqual(11, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Warning, output));
            Assert.AreEqual(1, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Error, output));


            //This test uses a broken Skyline file to test the InvalidDataException catch
            var brokenFile = commandFilesDir.GetTestPath("Broken_file.sky");

            output = RunCommand("--in=" + brokenFile);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_OpenSkyFile_Error__There_was_an_error_opening_the_file__0_, brokenFile));
            AssertEx.Contains(output, string.Format(Resources.XmlUtil_GetInvalidDataMessage_The_file_contains_an_error_on_line__0__at_column__1__, 2, 7));


            //This test uses a broken Skyline file to test the InvalidDataException catch
            var invalidFile = commandFilesDir.GetTestPath("InvalidFile.sky");
            output = RunCommand("--in=" + invalidFile);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_OpenSkyFile_Error__There_was_an_error_opening_the_file__0_, invalidFile));
            AssertEx.Contains(output, string.Format(Resources.XmlUtil_GetInvalidDataMessage_The_file_contains_an_error_on_line__0__at_column__1__, 7, 8));
            AssertEx.Contains(output, string.Format(Resources.DigestSettings_ValidateIntRange_The_value__1__for__0__must_be_between__2__and__3__, Resources.DigestSettings_Validate_maximum_missed_cleavages, 10, 0, 9));

            //Test unexpected parameter formats
            //CONSIDER: Maybe some more automatic way to keep these lists up to date.
            TestMissingValueFailures(new[]
                                    {
                                        "in",
                                        "out",
                                        "import-file",
                                        "import-replicate-name",
                                        "import-all",
                                        "import-naming-pattern",
                                        "report-name",
                                        "report-file",
                                        "report-format",
//                                        "exp-translist-format",
                                        "exp-dwell-time",
                                        "exp-run-length",
                                        "exp-method-instrument",
                                        "exp-template",
                                        "exp-file",
                                        "exp-strategy",
                                        "exp-method-type",
                                        "exp-max-trans",
                                        "exp-optimizing",
                                        "exp-scheduling-replicate",
                                        "tool-add",
                                        "tool-command",
                                        "tool-arguments",
                                        "tool-initial-dir",
                                        "tool-conflict-resolution",
                                        "tool-report",
                                        "report-add",
                                        "report-conflict-resolution",
                                        "batch-commands",
                                    });
            TestUnexpectedValueFailures(new[]
                                            {
                                                "save",
                                                "import-append",
                                                "exp-ignore-proteins",
                                                "exp-add-energy-ramp",
//                                                "exp-full-scans",
                                                "tool-output-to-immediate-window",
                                            });
        }

        private void TestMissingValueFailures(string[] names)
        {
            TestNameValueFailures(names, arg => arg);
            TestNameValueFailures(names, arg => string.Format("{0}=", arg));
        }

        private void TestUnexpectedValueFailures(IEnumerable<string> names)
        {
            TestNameValueFailures(names, arg => string.Format("{0}=true", arg));
        }

        private void TestNameValueFailures(IEnumerable<string> names, Func<string, string> getCommandLineForArg)
        {
            foreach (var name in names)
            {
                string arg = string.Format("--{0}", name);
                string output = RunCommand(getCommandLineForArg(arg));
                Assert.AreEqual(1, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Error, output), string.Format("No error for argument {0}", arg));
                Assert.AreEqual(1, CountInstances(arg, output), string.Format("Missing expected argument {0}", arg));
            }
        }

        private static string RunCommand(params string[] inputArgs)
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new CommandStatusWriter(new StringWriter(consoleBuffer));
            CommandLineRunner.RunCommand(inputArgs, consoleOutput);
            return consoleBuffer.ToString();
        }

        // TODO: Test the case where the imported replicate has the wrong path without Lorenzo's data
        //[TestMethod]
        public void TestLorenzo()
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new CommandStatusWriter(new StringWriter(consoleBuffer));

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

                Assert.IsTrue(msg.Contains(string.Format(Resources.CommandLine_ImportResultsFile_Warning__Cannot_read_file__0____Ignoring___, rawPath)));

                // the document should not have changed
                SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
                Assert.IsFalse(doc.Settings.HasResults);

                msg = RunCommand("--in=" + docPath,
                                 "--import-all=" + testFilesDir.FullPath,
                                 "--save");

                Assert.IsTrue(msg.Contains(string.Format(Resources.CommandLine_ImportResultsFile_Warning__Cannot_read_file__0____Ignoring___, rawPath)), msg);
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

             CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ImportResultsFile_Warning__Failed_importing_the_results_file__0____Ignoring___, rawPath), msg);
            // Read the saved document. FullScan.RAW|mzML should not have been imported
            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(doc.Settings.HasResults);

            // Import all files in the directory. FullScan.RAW|mzML should not be imported
            msg = RunCommand("--in=" + outPath,
                             "--import-all=" + testFilesDir.FullPath,
                             "--save");
             CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ImportResultsFile_Warning__Failed_importing_the_results_file__0____Ignoring___, rawPath), msg);


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
            FileEx.SafeDelete(outPath1);
            var outPath2 = testFilesDir.GetTestPath("Imported_multiple2.sky");
            FileEx.SafeDelete(outPath2);
            var outPath3 = testFilesDir.GetTestPath("Imported_multiple3.sky");
            FileEx.SafeDelete(outPath3);

            var rawPath = new MsDataFilePath(testFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_01" + extRaw));
            
            // Test: Cannot use --import-file and --import-all options simultaneously
            var msg = RunCommand("--in=" + docPath,
                                 "--import-file=" + rawPath.FilePath,
                                 "--import-replicate-name=Unscheduled01",
                                 "--import-all=" + testFilesDir.FullPath,
                                 "--out=" + outPath1);
            Assert.IsTrue(msg.Contains(Resources.CommandArgs_ParseArgsInternal_Error____import_file_and___import_all_options_cannot_be_used_simultaneously_), msg);
            // output file should not exist
            Assert.IsFalse(File.Exists(outPath1));



            // Test: Cannot use --import-replicate-name with --import-all
            msg = RunCommand("--in=" + docPath,
                             "--import-replicate-name=Unscheduled01",
                             "--import-all=" + testFilesDir.FullPath,
                             "--out=" + outPath1);
            Assert.IsTrue(msg.Contains(Resources.CommandArgs_ParseArgsInternal_Error____import_replicate_name_cannot_be_used_with_the___import_all_option_), msg);
            // output file should not exist
            Assert.IsFalse(File.Exists(outPath1));



            // Test: Cannot use --import-naming-pattern with --import-file
            msg = RunCommand("--in=" + docPath,
                                 "--import-file=" + rawPath.FilePath,
                                 "--import-naming-pattern=prefix_(.*)",
                                 "--out=" + outPath1);
            Assert.IsTrue(msg.Contains(Resources.CommandArgs_ParseArgsInternal_Error____import_naming_pattern_cannot_be_used_with_the___import_file_option_), msg);
            // output file should not exist
            Assert.IsFalse(File.Exists(outPath1));




            // Test: invalid regular expression (1)
            msg = RunCommand("--in=" + docPath,
                                 "--import-all=" + testFilesDir.FullPath,
                                 "--import-naming-pattern=A",
                                 "--out=" + outPath1);
            // output file should not exist
            Assert.IsFalse(File.Exists(outPath1));
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression___0___does_not_have_any_groups___String, "A")), msg);



            // Test: invalid regular expression (2)
            msg = RunCommand("--in=" + docPath,
                      "--import-all=" + testFilesDir.FullPath,
                      "--import-naming-pattern=invalid",
                      "--out=" + outPath1);
            // output file should not exist
            Assert.IsTrue(!File.Exists(outPath1));
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression___0___does_not_have_any_groups___String, "invalid")), msg);




            // Test: Import files in the "REP01" directory; 
            // Use a naming pattern that will cause the replicate names of the two files to be the same
            msg = RunCommand("--in=" + docPath,
                             "--import-all=" + testFilesDir.GetTestPath("REP01"),
                             "--import-naming-pattern=.*_(REP[0-9]+)_(.+)",
                             "--out=" + outPath1);
            Assert.IsFalse(File.Exists(outPath1));
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandLine_ApplyNamingPattern_Error__Duplicate_replicate_name___0___after_applying_regular_expression_,"REP1")), msg);




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
                       "--import-file=" + rawPath.FilePath,
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
            var rawPath2 = MsDataFileUri.Parse(testFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML"));
            msg = RunCommand("--in=" + outPath2,
                       "--import-file=" + rawPath2.GetFilePath(),
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
            MsDataFilePath rawPathDisk = GetThermoDiskPath(rawPath);

            // These messages are due to files that were already in the document.
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___, "REP01", rawPathDisk)), msg);
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___, "160109_Mix1_calcurve_070", rawPath2)), msg);
//            Assert.IsTrue(msg.Contains(string.Format("160109_Mix1_calcurve_070 -> {0}",rawPath2)), msg); 

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
                new MsDataFilePath(testFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_01" +
                extRaw))));
            Assert.IsTrue(!useRaw || chromatogramSet.MSDataFilePaths.Contains(
                GetThermoDiskPath(new MsDataFilePath(testFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_02" + extRaw)))));

           

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
                        Resources.CommandLine_CheckReplicateFiles_Error__Replicate__0__in_the_document_has_an_unexpected_file__1__,"REP01",
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

            Settings.Default.ToolList.Clear(); // in case any previous run had trouble

            // Test adding a tool.
            RunCommand("--tool-add=" + title,
                     "--tool-command=" + command,
                     "--tool-arguments=" + arguments,
                     "--tool-initial-dir=" + initialDirectory);
            Assert.IsTrue(Settings.Default.ToolList.Count > 0, "The expected tool was not added to the list.");
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
            Assert.IsTrue(output.Contains(Resources.CommandLine_ImportTool_The_tool_was_not_imported___));

            string output2 = RunCommand("--tool-command=" + command);
            Assert.IsTrue(output2.Contains(Resources.CommandLine_ImportTool_The_tool_was_not_imported___));

            const string badCommand = "test";
            string output3 = RunCommand("--tool-add=" + title,"--tool-command=" + badCommand);
            Assert.IsTrue(output3.Contains(string.Format(Resources.CommandLine_ImportTool_Error__the_provided_command_for_the_tool__0__is_not_of_a_supported_type___Supported_Types_are___1_, title, "*.exe; *.com; *.pif; *.cmd; *.bat")));
            Assert.IsTrue(output3.Contains(Resources.CommandLine_ImportTool_The_tool_was_not_imported___));

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
            Assert.IsTrue(output4.Contains((string.Format(Resources.CommandLine_ImportTool_, "TestTool1"))));

            ToolDescription tool3 = Settings.Default.ToolList.Last();
            Assert.AreNotEqual("", tool3.Arguments);
            Assert.AreNotEqual("", tool3.InitialDirectory);
            // Specify overwrite
            string output5 = RunCommand("--tool-add=" + title,
                     "--tool-command=" + command,
                     "--tool-conflict-resolution=overwrite");
            Assert.IsTrue((output5.Contains(string.Format(Resources.CommandLine_ImportTool_Warning__the_tool__0__was_overwritten,"TestTool1"))));
            // Check arguments and initialDir were written over.
            ToolDescription tool4 = Settings.Default.ToolList.Last();
            Assert.AreEqual(title,tool4.Title);
            Assert.AreEqual("", tool4.Arguments);
            Assert.AreEqual("", tool4.InitialDirectory);
            // Specify skip
            string output6 = RunCommand("--tool-add=" + title,
                     "--tool-command=" + command,
                     "--tool-arguments=thisIsATest",
                     "--tool-conflict-resolution=skip");
            Assert.IsTrue((output6.Contains(string.Format(Resources.CommandLine_ImportTool_Warning__skipping_tool__0__due_to_a_name_conflict_,"TestTool1"))));
            // Check Arguments
            ToolDescription tool5 = Settings.Default.ToolList.Last();
            Assert.AreEqual(title, tool5.Title);
            Assert.AreEqual("", tool5.Arguments); // unchanged.
            
            // It now complains in this case.
            string output7 = RunCommand( "--tool-arguments=" + arguments,
                     "--tool-initial-dir=" + initialDirectory);
            Assert.IsTrue(output7.Contains(Resources.CommandLine_ImportTool_Error__to_import_a_tool_it_must_have_a_name_and_a_command___Use___tool_add_to_specify_a_name_and_use___tool_command_to_specify_a_command___The_tool_was_not_imported___));

            // Test adding a tool.
            const string newToolTitle = "TestTitle";
            const string reportTitle = "\"Transition Results\"";
            RunCommand("--tool-add=" + newToolTitle,
                     "--tool-command=" + command,
                     "--tool-arguments=" + arguments,
                     "--tool-initial-dir=" + initialDirectory,
                     "--tool-output-to-immediate-window",
                     "--tool-report=" + reportTitle);
            int index3 = Settings.Default.ToolList.Count - 1;
            ToolDescription tool6 = Settings.Default.ToolList[index3];
            Assert.AreEqual(newToolTitle, tool6.Title);
            Assert.AreEqual(command, tool6.Command);
            Assert.AreEqual(arguments, tool6.Arguments);
            Assert.AreEqual(initialDirectory, tool6.InitialDirectory);
            Assert.IsTrue(tool6.OutputToImmediateWindow);
            Assert.AreEqual(reportTitle, tool6.ReportTitle);
            // Remove that tool.
            Settings.Default.ToolList.RemoveAt(index3);

            const string importReportArgument = ToolMacros.INPUT_REPORT_TEMP_PATH;
            string output8 = RunCommand("--tool-add=" + newToolTitle,
                     "--tool-command=" + command,
                     "--tool-arguments=" + importReportArgument,
                     "--tool-initial-dir=" + initialDirectory,
                     "--tool-output-to-immediate-window");
            Assert.IsTrue(output8.Contains(string.Format(Resources.CommandLine_ImportTool_Error__If__0__is_and_argument_the_tool_must_have_a_Report_Title__Use_the___tool_report_parameter_to_specify_a_report_, "$(InputReportTempPath)")));

            const string reportTitle3 = "fakeReport";
            string output9 = RunCommand("--tool-add=" + newToolTitle,
                     "--tool-command=" + command,
                     "--tool-arguments=" + importReportArgument,
                     "--tool-initial-dir=" + initialDirectory,
                     "--tool-output-to-immediate-window",
                     "--tool-report=" + reportTitle3);
            Assert.IsTrue(output9.Contains(string.Format(Resources.CommandLine_ImportTool_Error__Please_import_the_report_format_for__0____Use_the___report_add_parameter_to_add_the_missing_custom_report_, reportTitle3)));
            Assert.IsTrue(output9.Contains(Resources.CommandLine_ImportTool_The_tool_was_not_imported___));
        }

        [TestMethod]
        public void TestInstallFromZip()
        {
            // Using clause here overwrites failure exception when it fails
            var movedDir = new MovedDirectory(ToolDescriptionHelpers.GetToolsDirectory(), Program.StressTest);
            try
            {
                Settings.Default.ToolList.Clear();
                var testFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
                {
                    // Test bad input
                    const string badFileName = "BadFilePath";
                    Assert.IsFalse(File.Exists(badFileName));
                    const string command = "--tool-add-zip=" + badFileName;
                    string output = RunCommand(command);
                    Assert.IsTrue(output.Contains(Resources.CommandLine_ImportToolsFromZip_Error__the_file_specified_with_the___tool_add_zip_command_does_not_exist__Please_verify_the_file_location_and_try_again_));
                }
                {
                    string notZip = testFilesDir.GetTestPath("Broken_file.sky");
                    Assert.IsTrue(File.Exists(notZip));
                    string command = "--tool-add-zip=" + notZip;
                    string output = RunCommand(command);
                    Assert.IsTrue(output.Contains(Resources.CommandLine_ImportToolsFromZip_Error__the_file_specified_with_the___tool_add_zip_command_is_not_a__zip_file__Please_specify_a_valid__zip_file_));
                }
                {
                    var uniqueReportZip = testFilesDir.GetTestPath("UniqueReport.zip");
                    Assert.IsTrue(File.Exists(uniqueReportZip));
                    string command = "--tool-add-zip=" + uniqueReportZip;
                    string output = RunCommand(command);

                    Assert.IsTrue(Settings.Default.ToolList.Count == 1);
                    ToolDescription newTool = Settings.Default.ToolList.Last();
                    Assert.AreEqual("HelloWorld", newTool.Title);
                    Assert.IsTrue(newTool.OutputToImmediateWindow);
                    Assert.AreEqual("UniqueReport", newTool.ReportTitle);
                    string path = newTool.ToolDirPath;
                    Assert.IsTrue(File.Exists(Path.Combine(path, "HelloWorld.exe")));
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_,"HelloWorld")));
                    //Try to add the same tool again. Get conflicting report and tool with no overwrite specified.
                    string output1 = RunCommand(command);
                    Assert.IsTrue(output1.Contains(string.Format(Resources.AddZipToolHelper_ShouldOverwrite_Error__There_is_a_conflicting_tool + Resources.AddZipToolHelper_ShouldOverwrite__in_the_file__0_, "UniqueReport.zip")));
                    Assert.IsTrue(
                        output1.Contains(
                            Resources.AddZipToolHelper_ShouldOverwrite_Please_specify__overwrite__or__parallel__with_the___tool_zip_conflict_resolution_command_));
                    //Now run with overwrite specified.
                    string output2 = RunCommand(command, "--tool-zip-conflict-resolution=overwrite");
                    Assert.IsTrue(output2.Contains(string.Format(Resources.AddZipToolHelper_ShouldOverwrite_Overwriting_tool___0_,"HelloWorld")));
                    //Now install in parallel.
                    string output3 = RunCommand(command, "--tool-zip-conflict-resolution=parallel");
                    Assert.IsTrue(output3.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "HelloWorld1")));
                    ToolDescription newTool1 = Settings.Default.ToolList.Last();
                    Assert.AreEqual("HelloWorld1", newTool1.Title);
                    Assert.IsTrue(newTool1.OutputToImmediateWindow);
                    Assert.AreEqual("UniqueReport", newTool1.ReportTitle);
                    string path1 = newTool1.ToolDirPath;
                    Assert.IsTrue(File.Exists(Path.Combine(path1, "HelloWorld.exe")));
                    //Cleanup.
                    Settings.Default.ToolList.Clear();
                    DirectoryEx.SafeDelete(ToolDescriptionHelpers.GetToolsDirectory());
                    Settings.Default.PersistedViews.RemoveView(PersistedViews.ExternalToolsGroup.Id, "UniqueReport");
                    Settings.Default.PersistedViews.RemoveView(PersistedViews.ExternalToolsGroup.Id, "UniqueReport1");
                }
                {
                    //Test working with packages and ProgramPath Macro.
                    var testCommandLine = testFilesDir.GetTestPath("TestCommandLine.zip");
                    Assert.IsTrue(File.Exists(testCommandLine));
                    string command = "--tool-add-zip=" + testCommandLine;
                    string output = RunCommand(command);
                    StringAssert.Contains(output, Resources.AddZipToolHelper_InstallProgram_Error__Package_installation_not_handled_in_SkylineRunner___If_you_have_already_handled_package_installation_use_the___tool_ignore_required_packages_flag);
                    string output1 = RunCommand(command, "--tool-ignore-required-packages");
                    StringAssert.Contains(output1, string.Format(
                        Resources.AddZipToolHelper_FindProgramPath_A_tool_requires_Program__0__Version__1__and_it_is_not_specified_with_the___tool_program_macro_and___tool_program_path_commands__Tool_Installation_Canceled_, 
                        "Bogus",
                        "2.15.2"));

                    string path = testFilesDir.GetTestPath("NumberWriter.exe");
                    string output2 = RunCommand(command, "--tool-ignore-required-packages",
                                                "--tool-program-macro=Bogus,2.15.2",
                                                "--tool-program-path=" + path);

                    StringAssert.Contains(output2, string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "TestCommandline"));
                    ToolDescription newTool = Settings.Default.ToolList.Last();
                    Assert.AreEqual("TestCommandline", newTool.Title);
                    Assert.AreEqual("$(ProgramPath(Bogus,2.15.2))", newTool.Command);
                    Assert.AreEqual("100 12", newTool.Arguments);
                    ProgramPathContainer ppc = new ProgramPathContainer("Bogus", "2.15.2");
                    Assert.IsTrue(Settings.Default.ToolFilePaths.ContainsKey(ppc));
                    Assert.AreEqual(path, Settings.Default.ToolFilePaths[ppc]);
                    Settings.Default.ToolFilePaths.Remove(ppc);
                    Settings.Default.ToolList.Clear();
                    DirectoryEx.SafeDelete(ToolDescriptionHelpers.GetToolsDirectory());
                }
                {
                    //Test working with annotations.
                    var testCommandLine = testFilesDir.GetTestPath("TestAnnotations.zip");
                    Assert.IsTrue(File.Exists(testCommandLine));
                    string command = "--tool-add-zip=" + testCommandLine;
                    string output = RunCommand(command);
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "AnnotationTest\\Tool1")));
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "AnnotationTest\\Tool2")));
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "AnnotationTest\\Tool3")));
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "AnnotationTest\\Tool4")));
                }
                {
                    var conflictingAnnotations = testFilesDir.GetTestPath("ConflictAnnotations.zip");
                    Assert.IsTrue(File.Exists(conflictingAnnotations));
                    string command = "--tool-add-zip=" + conflictingAnnotations;
                    string output = RunCommand(command);
                    Assert.IsTrue(
                        output.Contains(string.Format(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_annotations_with_conflicting_names__Please_use_the___tool_zip_overwrite_annotations_command_)));
                    output = RunCommand(command, "--tool-zip-overwrite-annotations=false");
                    Assert.IsTrue(output.Contains(string.Format(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_conflicting_annotations__Keeping_existing_)));
                    Assert.IsTrue(
                        output.Contains(
                            string.Format(
                                Resources.AddZipToolHelper_ShouldOverwriteAnnotations_Warning__the_annotation__0__may_not_be_what_your_tool_requires_,
                                "SampleID")));

                    output = RunCommand(command, "--tool-zip-overwrite-annotations=true");
                    Assert.IsTrue(output.Contains(string.Format(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_conflicting_annotations__Overwriting_)));
                    Assert.IsTrue(output.Contains(string.Format(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_Warning__the_annotation__0__is_being_overwritten,"SampleID")));

                    Settings.Default.AnnotationDefList = new AnnotationDefList();
                    Settings.Default.ToolList.Clear();
                    DirectoryEx.SafeDelete(ToolDescriptionHelpers.GetToolsDirectory());
                }
            }
            finally
            {
                try { movedDir.Dispose(); }
// ReSharper disable once EmptyGeneralCatchClause
                catch (Exception) {}                
            }
        }

        // TODO: Don removed this test because it was failing in multiple runs under TestRunner
        //[TestMethod]
        public void ConsoleAddSkyrTest()
        {
            int initialNumber = Settings.Default.ReportSpecList.Count;
            // Assumes the title TextREportexam is a unique title. 
            // Add test.skyr which only has one report type named TextREportexam
            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            var skyrFile = commandFilesDir.GetTestPath("test.skyr");
            string output = RunCommand("--report-add=" + skyrFile);
            Assert.AreEqual(initialNumber+1, Settings.Default.ReportSpecList.Count);
            Assert.AreEqual("TextREportexam", Settings.Default.ReportSpecList.Last().GetKey());
            Assert.IsTrue(output.Contains("Success"));
            var skyrAdded = Settings.Default.ReportSpecList.Last();

            // Attempt to add the same skyr again.
            string output2 = RunCommand("--report-add=" + skyrFile);
            Assert.IsTrue(output2.Contains("Error"));
            // Do want to use == to show it is the same object, unchanged
            Assert.IsTrue(ReferenceEquals(skyrAdded, Settings.Default.ReportSpecList.Last()));

            // Specify skip
            string output4 = RunCommand("--report-add=" + skyrFile,
                "--report-conflict-resolution=skip");
            Assert.IsTrue(output4.Contains("skipping"));
            // Do want to use == to show it is the same object, unchanged
            Assert.IsTrue(ReferenceEquals(skyrAdded, Settings.Default.ReportSpecList.Last()));


            // Specify overwrite
            string output3 = RunCommand("--report-add=" + skyrFile,
                "--report-conflict-resolution=overwrite");
            Assert.IsTrue(output3.Contains("overwriting"));
            // Do want to use == to show it is not the same object, changed
            Assert.IsFalse(ReferenceEquals(skyrAdded, Settings.Default.ReportSpecList.Last()));

        }

        // TODO: Don removed this test because it was failing in multiple runs under TestRunner
        //[TestMethod]
        public void ConsoleRunCommandsTest()
        {
            int toolListCount = Settings.Default.ToolList.Count;
            var commandFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            var commandsToRun = commandFilesDir.GetTestPath("ToolList2.txt");
            string output = RunCommand("--batch-commands=" + commandsToRun);            
            Assert.IsTrue(output.Contains("NeWtOOl was added to the Tools Menu"));
            Assert.IsTrue(output.Contains("iHope was added to the Tools Menu"));
            Assert.IsTrue(output.Contains("thisWorks was added to the Tools Menu"));
            Assert.IsTrue(output.Contains("FirstTry was added to the Tools Menu"));
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == "NeWtOOl" && t.Command == @"C:\Windows\Notepad.exe" && t.Arguments == "$(DocumentDir)" && t.InitialDirectory == @"C:\"));
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == "iHope" && t.Command == @"C:\Windows\Notepad.exe"));
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == "thisWorks"));
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == "FirstTry"));
            Assert.AreEqual(toolListCount+4, Settings.Default.ToolList.Count);

            // run the same command again. this time each should be skipped.
            string output2 = RunCommand("--batch-commands=" + commandsToRun);
            Assert.IsFalse(output2.Contains("NeWtOOl was added to the Tools Menu"));
            Assert.IsFalse(output2.Contains("iHope was added to the Tools Menu"));
            Assert.IsFalse(output2.Contains("thisWorks was added to the Tools Menu"));
            Assert.IsFalse(output2.Contains("FirstTry was added to the Tools Menu"));
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == "NeWtOOl" && t.Command == @"C:\Windows\Notepad.exe" && t.Arguments == "$(DocumentDir)" && t.InitialDirectory == @"C:\"));
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == "iHope" && t.Command == @"C:\Windows\Notepad.exe"));
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == "thisWorks"));
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == "FirstTry"));
            // the number of tools is unchanged.
            Assert.AreEqual(toolListCount + 4, Settings.Default.ToolList.Count);

        }

        [TestMethod]
        public void ConsoleExportToolsTest()
        {
            Settings.Default.ToolList.Clear();

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

            // Test adding a tool.
            const string newToolTitle = "TestTitle";
            const string reportTitle = "\"Transition Results\"";
            RunCommand("--tool-add=" + newToolTitle,
                     "--tool-command=" + command,
                     "--tool-arguments=" + arguments,
                     "--tool-initial-dir=" + initialDirectory,
                     "--tool-output-to-immediate-window",
                     "--tool-report=" + reportTitle);

            string filePath = Path.GetTempFileName();
            RunCommand("--tool-list-export=" + filePath);

            using (StreamReader sr = new StreamReader(filePath))
            {
                string line1 = sr.ReadLine();
                Assert.IsTrue(line1!=null);
                Assert.IsTrue(line1.Contains(string.Format("--tool-add=\"{0}\"",title)));
                Assert.IsTrue(line1.Contains(string.Format("--tool-command=\"{0}\"",command)));
                Assert.IsTrue(line1.Contains(string.Format("--tool-arguments=\"{0}\"", arguments)));
                Assert.IsTrue(line1.Contains(string.Format("--tool-initial-dir=\"{0}\"", initialDirectory)));
                Assert.IsTrue(line1.Contains("--tool-conflict-resolution=skip"));
                Assert.IsTrue(line1.Contains("--tool-report=\"\""));

                string line2 = sr.ReadLine();
                Assert.IsTrue(line2 != null);
                Assert.IsTrue(line2.Contains(string.Format("--tool-add=\"{0}\"", newToolTitle)));
                Assert.IsTrue(line2.Contains(string.Format("--tool-command=\"{0}\"", command)));
                Assert.IsTrue(line2.Contains(string.Format("--tool-arguments=\"{0}\"", arguments)));
                Assert.IsTrue(line2.Contains(string.Format("--tool-initial-dir=\"{0}\"", initialDirectory)));
                Assert.IsTrue(line2.Contains("--tool-conflict-resolution=skip"));
                Assert.IsTrue(line2.Contains(string.Format("--tool-report=\"{0}\"",reportTitle)));
                Assert.IsTrue(line2.Contains("--tool-output-to-immediate-window"));
            }
            FileEx.SafeDelete(filePath);   
        }        

        [TestMethod]
        public void ConsoleParserTest()
        {            
            // Assert.AreEqual(new[] { "--test=foo bar", "--new" }, CommandLine.ParseArgs("\"--test=foo bar\" --new"));
            // The above line of code would not pass so this other form works better.
            // Test case "--test=foo bar" --new
            string[] expected1 = { "--test=foo bar", "--new" };
            string[] actual1 = CommandLine.ParseArgs("\"--test=foo bar\" --new");
            Assert.AreEqual(expected1[0], actual1[0]);
            Assert.AreEqual(expected1[1], actual1[1]);
            // Or even better. A function that does the same assertion as above.
            Assert.IsTrue(ParserTestHelper(new[] { "--test=foo bar", "--new" }, CommandLine.ParseArgs("\"--test=foo bar\" --new")));

           // Test case --test="foo bar" --new
            string[] expected2 = {"--test=foo bar", "--new"};
            string[] actual2 = CommandLine.ParseArgs("--test=\"foo bar\" --new");
            Assert.AreEqual(expected2[0],actual2[0]);
            Assert.AreEqual(expected2[1],actual2[1]);
            Assert.IsTrue(ParserTestHelper(new[] { "--test=foo bar", "--new" }, CommandLine.ParseArgs("--test=\"foo bar\" --new")));


            // Test case --test="i said ""foo bar""" -new
            string[] expected3 = { "--test=i said \"foo bar\"", "--new" };
            string[] actual3 = CommandLine.ParseArgs("--test=\"i said \"\"foo bar\"\"\" --new");
            Assert.AreEqual(expected3[0], actual3[0]);
            Assert.AreEqual(expected3[1], actual3[1]);
            Assert.IsTrue(ParserTestHelper(new[] { "--test=i said \"foo bar\"", "--new" }, CommandLine.ParseArgs("--test=\"i said \"\"foo bar\"\"\" --new")));

            // Test case "--test=foo --new --bar"
            Assert.IsTrue(ParserTestHelper(new[] { "--test=foo --new --bar" }, CommandLine.ParseArgs("\"--test=foo --new --bar\"")));
            
            // Test case --test="" --new --bar
            Assert.IsTrue(ParserTestHelper(new[] { "--test=", "--new", "--bar" }, CommandLine.ParseArgs("--test=\"\" --new --bar")));

            // Test case of all spaces
            string[] test = CommandLine.ParseArgs("     ");
            Assert.IsTrue(ParserTestHelper(new string[] {}, test));
        }

        [TestMethod]
        public void CommandLineArrayParserTest()
        {
            // Test case [] = "" - an empty array
            Assert.AreEqual(string.Empty, CommandLine.JoinArgs(new string[0]));
            
            // Test case [a,b,c] = "a b c" - a simple array with no spaces
            Assert.AreEqual("a b c", CommandLine.JoinArgs(new [] {"a", "b", "c"}));

            // Test case [a b, c, d] = ""a b" c d" - multiword string at beginning of array
            Assert.AreEqual("\"a b\" c d", CommandLine.JoinArgs(new [] {"a b", "c", "d"}));

            // Test case [a, b, c d] = "a b "c d"" - multiword string at end of array
            Assert.AreEqual("a b \"c d\"", CommandLine.JoinArgs(new [] { "a", "b", "c d" }));

            // Test case [a, b c d, e] = " a "b c d" e" - multiword string at middle of array
            Assert.AreEqual("a \"b c d\" e", CommandLine.JoinArgs(new [] { "a", "b c d", "e" }));

            // Test case [a, b c, d e f, g, h i] = "a "b c" "d e f" g "h i"" - multiple multiword strings
            Assert.AreEqual("a \"b c\" \"d e f\" g \"h i\"", CommandLine.JoinArgs(new [] { "a", "b c", "d e f", "g" , "h i" }));

            // Test case [a "b" c] = "a "b" c" - nested quotes
            Assert.AreEqual("\"a \"b\" c\"", CommandLine.JoinArgs(new [] {"a \"b\" c"}));

            // Test case [a   bc] = "a   bc" - tabbed whitespace only
            Assert.AreEqual("\"a\tbc\"", CommandLine.JoinArgs(new [] {"a\tbc"}));

            // Test case [a,,c] = "a "" c" - empty string
            Assert.AreEqual("a \"\" c", CommandLine.JoinArgs(new [] {"a", string.Empty, "c"}));
        }

        [TestMethod]
        public void ConsolePanoramaArgsTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");

            // Error: missing panorama args
            var output = RunCommand("--in=" + docPath,
                "--panorama-server=https://panoramaweb.org");

            Assert.IsTrue(
                output.Contains(string.Format(Resources.CommandArgs_PanoramaArgsComplete_plural_,
                    TextUtil.LineSeparate("--panorama-username", "--panorama-password", "--panorama-folder"))));

            output = RunCommand("--in=" + docPath,
                "--panorama-server=https://panoramaweb.org",
                "--panorama-username=user",
                "--panorama-password=passwd");

            Assert.IsTrue(
                output.Contains(string.Format(Resources.CommandArgs_PanoramaArgsComplete_, "--panorama-folder")));


            // Error: invalid server URL
            const string badServer = "bad server url";
            output = RunCommand("--in=" + docPath,
                "--panorama-server=" + badServer,
                "--panorama-username=user",
                "--panorama-password=passwd",
                "--panorama-folder=folder");

            Assert.IsTrue(output.Contains(string.Format(
                Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_, badServer
                )));


            var buffer = new StringBuilder();
            var helper = new CommandArgs.PanoramaHelper(new StringWriter(buffer));

            // Error: Unknown server
            var serverUri = PanoramaUtil.ServerNameToUri("unknown.server-state.com");
            var client = new TestPanoramaClient() { MyServerState = ServerState.unknown, ServerUri = serverUri };
            helper.ValidateServer(client, null, null);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(Resources.EditServerDlg_OkDialog_Unknown_error_connecting_to_the_server__0__,
                            serverUri.Host)));
            buffer.Clear();


            // Error: Not a Panorama Server
            serverUri = PanoramaUtil.ServerNameToUri("www.google.com");
            client = new TestPanoramaClient() {MyPanoramaState = PanoramaState.other, ServerUri = serverUri};
            helper.ValidateServer(client, null, null);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(Resources.EditServerDlg_OkDialog_The_server__0__is_not_a_Panorama_server,
                            serverUri.Host)));
            buffer.Clear();


            // Error: Invalid user
            serverUri = PanoramaUtil.ServerNameToUri(PanoramaUtil.PANORAMA_WEB);
            client = new TestPanoramaClient() { MyUserState = UserState.nonvalid, ServerUri = serverUri };
            helper.ValidateServer(client, "invalid", "user");
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        Resources
                            .EditServerDlg_OkDialog_The_username_and_password_could_not_be_authenticated_with_the_panorama_server));
            buffer.Clear();


            // Error: unknown exception
            client = new TestPanoramaClientThrowsException();
            helper.ValidateServer(client, null, null);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(Resources.PanoramaHelper_ValidateServer_, "GetServerState threw an exception")));
            buffer.Clear();

            
            // Error: folder does not exist
            client = new TestPanoramaClient() { MyFolderState = FolderState.notfound, ServerUri = serverUri };
            var server = helper.ValidateServer(client, "user", "password");
            var folder = "folder/not/found";
            helper.ValidateFolder(client, server, folder);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(
                            Resources.PanoramaUtil_VerifyFolder_Folder__0__does_not_exist_on_the_Panorama_server__1_,
                            folder, client.ServerUri)));
            buffer.Clear();


            // Error: no permissions on folder
            client = new TestPanoramaClient() { MyFolderState = FolderState.nopermission, ServerUri = serverUri };
            folder = "no/permissions";
            helper.ValidateFolder(client, server, folder);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(
                            Resources.PanoramaUtil_VerifyFolder_User__0__does_not_have_permissions_to_upload_to_the_Panorama_folder__1_,
                            "user", folder)));
            buffer.Clear();


            // Error: not a Panorama folder
            client = new TestPanoramaClient() { MyFolderState = FolderState.notpanorama, ServerUri = serverUri };
            folder = "not/panorama";
            helper.ValidateFolder(client, server, folder);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(string.Format(Resources.PanoramaUtil_VerifyFolder__0__is_not_a_Panorama_folder,
                        folder)));


        }

        private static string GetTitleHelper()
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

        private static MsDataFilePath GetThermoDiskPath(MsDataFilePath pathToRaw)
        {
            return ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw
                ? pathToRaw.SetFilePath(Path.ChangeExtension(pathToRaw.FilePath, "raw"))
                : pathToRaw;
        }

        private static void CheckRunCommandOutputContains(string expectedMessage, string actualMessage)
        {
            Assert.IsTrue(actualMessage.Contains(expectedMessage),
                string.Format("Expected RunCommand result message containing \n\"{0}\",\ngot\n\"{1}\"\ninstead.", expectedMessage, actualMessage));
        }

        private class TestPanoramaClient : IPanoramaClient
        {
            public Uri ServerUri { get; set; }

            public ServerState MyServerState { get; set; }
            public PanoramaState MyPanoramaState { get; set; }
            public UserState MyUserState { get; set; }
            public FolderState MyFolderState { get; set; }

            public TestPanoramaClient()
            {
                MyServerState = ServerState.available;
                MyPanoramaState = PanoramaState.panorama;
                MyUserState = UserState.valid;
                MyFolderState = FolderState.valid;
            }

            public virtual ServerState GetServerState()
            {
                return MyServerState;
            }

            public PanoramaState IsPanorama()
            {
                return MyPanoramaState;
            }

            public UserState IsValidUser(string username, string password)
            {
                return MyUserState;
            }

            public FolderState IsValidFolder(string folderPath, string username, string password)
            {
                return MyFolderState;
            }
        }

        private class TestPanoramaClientThrowsException : TestPanoramaClient
        {
            public override ServerState GetServerState()
            {
                throw new Exception("GetServerState threw an exception");
            }    
        }
    }
}