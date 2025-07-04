﻿/*
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
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.PanoramaClient;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineRunner;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Summary description for CommandLineTest
    /// </summary>
    [TestClass]
    public class CommandLineTest : AbstractUnitTestEx
    {
        protected override void Initialize()
        {
            Settings.Default.ToolList.Clear();
        }

        protected override void Cleanup()
        {
            Settings.Default.ToolList.Clear();
        }        

        private const string ZIP_FILE = @"TestData\Results\FullScan.zip";
        private const string COMMAND_FILE = @"TestData\CommandLineTest.zip";
        private const string PROTDB_FILE = @"TestFunctional\AssociateProteinsTest.zip";

        private new static string RunCommand(params string[] args)
        {
            if (args.Contains(a => a.StartsWith("--out=")))
                args = args.Append("--overwrite").ToArray();
            return AbstractUnitTestEx.RunCommand(args);
        }

        [TestMethod]
        public void ConsoleReplicateOutTest()
        {
            DoConsoleReplicateOutTest(false);
        }

        [TestMethod]
        public void ConsoleReplicateOutTestWithAuditLogging()
        {
            DoConsoleReplicateOutTest(true);
        }

        [TestMethod]
        private void DoConsoleReplicateOutTest(bool auditLogging)
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            if (auditLogging)
            {
                EnableAuditLogging(docPath);
            }
            string outPath = TestFilesDir.GetTestPath("Imported_single.sky");

            // Import the first RAW file (or mzML for international)
            string rawPath = TestFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);

            RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath,
                       "--import-replicate-name=Single",
                       "--out=" + outPath);

            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            AssertResult.IsDocumentResultsState(doc, "Single", 3, 3, 0, 21, 0);

            if (auditLogging)
            {
                var docWithAuditLog = DeserializeWithAuditLog(outPath);
                AssertLastEntry(docWithAuditLog.AuditLog, MessageType.imported_result);
            }

            //Test --import-append
            var dataFile2 = TestFilesDir.GetTestPath("ah_20101029r_BSA_CID_FT_centroid_3uscan_3" +
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

            if (auditLogging)
            {
                var docWithAuditLog = DeserializeWithAuditLog(outPath);
                AssertLastEntry(docWithAuditLog.AuditLog, MessageType.imported_result);
            }
        }

        [TestMethod]
        public void ConsoleShareZipTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky.zip");

            RunCommand("--in=" + docPath,
                       "--share-zip=" + outPath);

            AssertEx.FileExists(outPath);

            var outFilesDir = new TestFilesDir(TestContext, outPath);
            AssertEx.FileExists(outFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky"));
            // Let base class handle cleanup
            TestFilesDirs = new[] { testFilesDir, outFilesDir };
        }

        [TestMethod]
        public void ConsoleRemoveResultsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("Remove_Test.sky");
            string outPath = TestFilesDir.GetTestPath("Remove_Test_Out.sky");
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

        [TestMethod]
        public void ConsoleSetLibraryTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = TestFilesDir.GetTestPath("SetLib_Out.sky");
            string libPath = TestFilesDir.GetTestPath("sample.blib");
            string libPath2 = TestFilesDir.GetTestPath("sample2.blib");
            const string libName = "namedlib";
            string fakePath = docPath + ".fake";
            string libPathRedundant = TestFilesDir.GetTestPath("sample.redundant.blib");

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
            Assert.AreEqual(Path.GetFileNameWithoutExtension(libPath), doc.Settings.PeptideSettings.Libraries.Libraries[0].Name);
            Assert.AreEqual(Path.GetFileName(libPath), doc.Settings.PeptideSettings.Libraries.Libraries[0].FileNameHint);

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
            Assert.AreEqual(Path.GetFileNameWithoutExtension(libPath), doc.Settings.PeptideSettings.Libraries.Libraries[0].Name);
            Assert.AreEqual(Path.GetFileName(libPath), doc.Settings.PeptideSettings.Libraries.Libraries[0].FileNameHint);
            Assert.AreEqual(libName, doc.Settings.PeptideSettings.Libraries.Libraries[1].Name);
            Assert.AreEqual(Path.GetFileName(libPath2), doc.Settings.PeptideSettings.Libraries.Libraries[1].FileNameHint);

            // Test error (library with conflicting name)
            output = RunCommand("--in=" + outPath,
                                "--add-library-path=" + libPath,
                                "--out=" + outPath);
            CheckRunCommandOutputContains(Resources.CommandLine_SetLibrary_Error__The_library_you_are_trying_to_add_conflicts_with_a_library_already_in_the_file_, output);
        }

        [TestMethod]
        public void ConsoleAddFastaTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = TestFilesDir.GetTestPath("AddFasta_Out.sky");
            string fastaPath = TestFilesDir.GetTestPath("sample.fasta");


            string output = RunCommand("--in=" + docPath,
                                       "--import-fasta=" + fastaPath,
                                       "--keep-empty-proteins",
                                       "--out=" + outPath);

            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);
            AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Error);
            AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Warning);

            // Before import, there are 2 peptides. 3 peptides after
            AssertEx.IsDocumentState(doc, 0, 3, 7, 7, 49);

            // Test without keep empty proteins
            output = RunCommand("--in=" + docPath,
                                "--import-fasta=" + fastaPath,
                                "--out=" + outPath);

            doc = ResultsUtil.DeserializeDocument(outPath);
            AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Error);
            AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Warning);

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
        }

        /// <summary>
        /// Run command that should cause an error and validate the output contains the expected output
        /// </summary>
        private void RunCommandAndValidateError(string[] extraSettings, string expectedOutput, bool printErrors = false)
        {
            FileEx.SafeDelete("testError.sky");
            var output = RunCommand(new[] { "--new=testError.sky" }.Concat(extraSettings).ToArray());
            StringAssert.Contains(output, expectedOutput);
            if (printErrors)
                Console.WriteLine(expectedOutput);
        }

        [TestMethod]
        public void ConsoleNewDocumentTest()
        {
            TestFilesDirs = new []
            {
                new TestFilesDir(TestContext, ZIP_FILE),
                new TestFilesDir(TestContext, PROTDB_FILE)
            };

            string existingDocPath = TestFilesDirs[0].GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string docPath = TestFilesDirs[0].GetTestPath("ConsoleNewDocumentTest.sky");
            string fastaPath = TestFilesDirs[0].GetTestPath("sample.fasta");
            string protdbPath = TestFilesDirs[1].GetTestPath("AssociateProteinMatches.protdb");

            // arguments that would normally be quoted on the command-line shouldn't be quoted here
            var settings = new[]
            {
                "--new=" + docPath,
                "--full-scan-precursor-isotopes=Count",
                "--full-scan-precursor-analyzer=centroided",
                "--full-scan-precursor-res=5",
                "--full-scan-acquisition-method=DIA",
                "--full-scan-isolation-scheme=All Ions",
                "--full-scan-product-analyzer=centroided",
                "--full-scan-product-res=5",
                "--full-scan-rt-filter=scheduling_windows",
                "--full-scan-rt-filter-tolerance=5",
                "--tran-precursor-ion-charges=2,3,4",
                "--tran-product-ion-charges=1,2",
                "--tran-product-start-ion=" + TransitionFilter.StartFragmentFinder.ION_1.Label,
                "--tran-product-end-ion=" + TransitionFilter.EndFragmentFinder.LAST_ION_MINUS_1.Label,
                "--tran-product-clear-special-ions",
                "--tran-use-dia-window-exclusion",
                "--pep-digest-enzyme=Chymotrypsin",
                "--pep-max-missed-cleavages=9",
                "--pep-unique-by=Protein",
                "--pep-min-length=4",
                "--pep-max-length=42",
                "--pep-exclude-nterminal-aas=2",
                "--pep-exclude-potential-ragged-ends",
                "--background-proteome-file=" + protdbPath,
                "--save-settings", // save the protdb to Settings.Default so we can test --background-proteome-name later
                "--library-product-ions=6",
                "--library-min-product-ions=6",
                "--library-match-tolerance=" + 0.05 + "mz",
                "--library-pick-product-ions=filter",
                "--instrument-min-mz=42",
                "--instrument-max-mz=2000",
                "--instrument-min-time=" + 0.42,
                "--instrument-max-time=" + 4.2,
                "--instrument-dynamic-min-mz",
                "--instrument-method-mz-tolerance=" + 0.42,
                "--instrument-triggered-chromatograms",
                "--integrate-all"
            };

            string output = RunCommand(settings);
            AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Error);
            AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Warning);

            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(FullScanPrecursorIsotopes.Count, doc.Settings.TransitionSettings.FullScan.PrecursorIsotopes);
            Assert.AreEqual(FullScanAcquisitionMethod.DIA, doc.Settings.TransitionSettings.FullScan.AcquisitionMethod);
            Assert.AreEqual("All Ions", doc.Settings.TransitionSettings.FullScan.IsolationScheme.Name);
            Assert.AreEqual(FullScanMassAnalyzerType.centroided, doc.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);
            Assert.AreEqual(FullScanMassAnalyzerType.centroided, doc.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            Assert.AreEqual(5, doc.Settings.TransitionSettings.FullScan.PrecursorRes);
            Assert.AreEqual(5, doc.Settings.TransitionSettings.FullScan.ProductRes);
            Assert.AreEqual(RetentionTimeFilterType.scheduling_windows, doc.Settings.TransitionSettings.FullScan.RetentionTimeFilterType);
            Assert.AreEqual(5, doc.Settings.TransitionSettings.FullScan.RetentionTimeFilterLength);
            Assert.AreEqual("2, 3, 4", doc.Settings.TransitionSettings.Filter.PeptidePrecursorChargesString);
            Assert.AreEqual(TransitionFilter.StartFragmentFinder.ION_1.Label, doc.Settings.TransitionSettings.Filter.StartFragmentFinderLabel.Label);
            Assert.AreEqual(TransitionFilter.EndFragmentFinder.LAST_ION_MINUS_1.Label, doc.Settings.TransitionSettings.Filter.EndFragmentFinderLabel.Label);
            Assert.AreEqual(0, doc.Settings.TransitionSettings.Filter.MeasuredIons.Count);
            Assert.AreEqual(9, doc.Settings.PeptideSettings.DigestSettings.MaxMissedCleavages);
            Assert.AreEqual("Chymotrypsin", doc.Settings.PeptideSettings.Enzyme.Name);
            Assert.AreEqual(PeptideFilter.PeptideUniquenessConstraint.protein, doc.Settings.PeptideSettings.Filter.PeptideUniqueness);
            Assert.AreEqual(true, doc.Settings.HasBackgroundProteome);
            Assert.AreEqual(protdbPath, doc.Settings.PeptideSettings.BackgroundProteome.DatabasePath);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(protdbPath), doc.Settings.PeptideSettings.BackgroundProteome.Name);
            Assert.AreEqual(true, doc.Settings.TransitionSettings.Filter.ExclusionUseDIAWindow);
            Assert.AreEqual(4, doc.Settings.PeptideSettings.Filter.MinPeptideLength);
            Assert.AreEqual(42, doc.Settings.PeptideSettings.Filter.MaxPeptideLength);
            Assert.AreEqual(2, doc.Settings.PeptideSettings.Filter.ExcludeNTermAAs);
            Assert.AreEqual(true, doc.Settings.PeptideSettings.DigestSettings.ExcludeRaggedEnds);
            Assert.AreEqual(6, doc.Settings.TransitionSettings.Libraries.IonCount);
            Assert.AreEqual(6, doc.Settings.TransitionSettings.Libraries.MinIonCount);
            Assert.AreEqual(new MzTolerance(0.05), doc.Settings.TransitionSettings.Libraries.IonMatchMzTolerance);
            Assert.AreEqual(TransitionLibraryPick.filter, doc.Settings.TransitionSettings.Libraries.Pick);
            Assert.AreEqual(42, doc.Settings.TransitionSettings.Instrument.MinMz);
            Assert.AreEqual(2000, doc.Settings.TransitionSettings.Instrument.MaxMz);
            Assert.AreEqual(0, doc.Settings.TransitionSettings.Instrument.MinTime);
            Assert.AreEqual(5, doc.Settings.TransitionSettings.Instrument.MaxTime);
            Assert.AreEqual(true, doc.Settings.TransitionSettings.Instrument.IsDynamicMin);
            Assert.AreEqual(0.42, doc.Settings.TransitionSettings.Instrument.MzMatchTolerance);
            Assert.AreEqual(true, doc.Settings.TransitionSettings.Instrument.TriggeredAcquisition);
            Assert.AreEqual(true, doc.Settings.TransitionSettings.Integration.IsIntegrateAll);

            // test trying to associate proteins without a FASTA set
            Settings.Default.LastProteinAssociationFastaFilepath = null;
            settings = new[]
            {
                "--in=" + docPath,
                "--associate-proteins-group-proteins",
            };
            output = RunCommand(settings);
            StringAssert.Contains(output, Resources.CommandLine_AssociateProteins_Failed_to_associate_proteins);
            StringAssert.Contains(output, Resources.CommandLine_AssociateProteins_a_FASTA_file_must_be_imported_before_associating_proteins);

            // test associating proteins with the dedicated argument for specifying the FASTA (rather than --import-fasta=)
            Settings.Default.LastProteinAssociationFastaFilepath = null;
            settings = new[]
            {
                "--in=" + existingDocPath,
                "--associate-proteins-fasta=" + fastaPath,
            };
            output = RunCommand(settings);
            StringAssert.Contains(output, 
                string.Format(Resources.CommandLine_AssociateProteins_Associating_peptides_with_proteins_from_FASTA_file__0_, Path.GetFileName(fastaPath)));

            // test importing FASTA and associating proteins and adding special ions
            settings = new[]
            {
                "--in=" + docPath,
                "--save",
                "--import-fasta=" + fastaPath,
                "--associate-proteins-group-proteins",
                "--associate-proteins-shared-peptides=AssignedToBestProtein",
                "--associate-proteins-minimal-protein-list",
                "--associate-proteins-remove-subsets",
                "--associate-proteins-min-peptides=2",
                "--tran-product-add-special-ion=TMT-127L",
                "--tran-product-add-special-ion=TMT-127H",
                "--integrate-all=false" // test lower case bool
            };
            output = RunCommand(settings);
            doc = ResultsUtil.DeserializeDocument(docPath);
            StringAssert.Contains(output, 
                string.Format(Resources.CommandLine_AssociateProteins_Associating_peptides_with_proteins_from_FASTA_file__0_, Path.GetFileName(fastaPath)));
            Assert.AreEqual(true, doc.Settings.PeptideSettings.ProteinAssociationSettings.GroupProteins);
            Assert.AreEqual(ProteinAssociation.SharedPeptides.AssignedToBestProtein, doc.Settings.PeptideSettings.ProteinAssociationSettings.SharedPeptides);
            Assert.AreEqual(true, doc.Settings.PeptideSettings.ProteinAssociationSettings.FindMinimalProteinList);
            Assert.AreEqual(true, doc.Settings.PeptideSettings.ProteinAssociationSettings.RemoveSubsetProteins);
            Assert.AreEqual(2, doc.Settings.PeptideSettings.ProteinAssociationSettings.MinPeptidesPerProtein);
            Assert.AreEqual(2, doc.Settings.TransitionSettings.Filter.MeasuredIons.Count);
            Assert.AreEqual(false, doc.Settings.TransitionSettings.Integration.IsIntegrateAll);

            // test associating proteins in a file with a previously imported FASTA
            settings = new[]
            {
                "--in=" + docPath,
                "--save",
                "--associate-proteins-shared-peptides=Removed",
                "--associate-proteins-remove-subsets",
                "--associate-proteins-min-peptides=1",
            };

            output = RunCommand(settings);
            doc = ResultsUtil.DeserializeDocument(docPath);
            StringAssert.Contains(output, 
                string.Format(Resources.CommandLine_AssociateProteins_Associating_peptides_with_proteins_from_FASTA_file__0_, Path.GetFileName(fastaPath)));
            Assert.AreEqual(false, doc.Settings.PeptideSettings.ProteinAssociationSettings.GroupProteins);
            Assert.AreEqual(ProteinAssociation.SharedPeptides.Removed, doc.Settings.PeptideSettings.ProteinAssociationSettings.SharedPeptides);
            Assert.AreEqual(false, doc.Settings.PeptideSettings.ProteinAssociationSettings.FindMinimalProteinList);
            Assert.AreEqual(true, doc.Settings.PeptideSettings.ProteinAssociationSettings.RemoveSubsetProteins);
            Assert.AreEqual(1, doc.Settings.PeptideSettings.ProteinAssociationSettings.MinPeptidesPerProtein);

            // test changing parameter order and adding special ions after clearing them
            settings = new[]
            {
                "--new=" + docPath,
                "--overwrite",
                "--full-scan-precursor-res=5",
                "--full-scan-precursor-analyzer=centroided",
                "--full-scan-precursor-isotopes=Count",
                "--tran-product-clear-special-ions",
                "--tran-product-add-special-ion=TMT-127L",
                "--tran-product-add-special-ion=TMT-127H"
            };

            output = RunCommand(settings);
            StringAssert.Contains(output, string.Format(Resources.CommandLine_NewSkyFile_Deleting_existing_file___0__, docPath));
            doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(FullScanPrecursorIsotopes.Count, doc.Settings.TransitionSettings.FullScan.PrecursorIsotopes);
            Assert.AreEqual(FullScanMassAnalyzerType.centroided, doc.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            Assert.AreEqual(5, doc.Settings.TransitionSettings.FullScan.PrecursorRes);
            Assert.AreEqual(2, doc.Settings.TransitionSettings.Filter.MeasuredIons.Count);

            // test case insensitive enum parsing
            settings = new[]
            {
                "--new=" + docPath,
                "--overwrite",
                "--pep-digest-enzyme=chymotrypsin",
                "--pep-unique-by=proTEiN",
                "--library-pick-product-ions=FilTER"
            };

            RunCommand(settings);
            doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual("Chymotrypsin", doc.Settings.PeptideSettings.Enzyme.Name);
            Assert.AreEqual(PeptideFilter.PeptideUniquenessConstraint.protein, doc.Settings.PeptideSettings.Filter.PeptideUniqueness);
            Assert.AreEqual(TransitionLibraryPick.filter, doc.Settings.TransitionSettings.Libraries.Pick);

            // test using existing background proteome name
            settings = new[]
            {
                "--new=" + docPath,
                "--overwrite",
                "--background-proteome-name=" + Path.GetFileNameWithoutExtension(protdbPath)
            };

            RunCommand(settings);
            doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(true, doc.Settings.HasBackgroundProteome);
            Assert.AreEqual(protdbPath, doc.Settings.PeptideSettings.BackgroundProteome.DatabasePath);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(protdbPath), doc.Settings.PeptideSettings.BackgroundProteome.Name);

            // test new background proteome with explicit name
            settings = new[]
            {
                "--new=" + docPath,
                "--overwrite",
                "--background-proteome-file=" + protdbPath,
                "--background-proteome-name=protdb"
            };

            RunCommand(settings);
            doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(true, doc.Settings.HasBackgroundProteome);
            Assert.AreEqual(protdbPath, doc.Settings.PeptideSettings.BackgroundProteome.DatabasePath);
            Assert.AreEqual("protdb", doc.Settings.PeptideSettings.BackgroundProteome.Name);

            File.Delete(docPath);
        }

        [TestMethod]
        public void ConsoleArgumentValidationTest()
        {
            // parameter validation: analyzer specified with isotopes=none
            var settings = new[]
            {
                "--full-scan-precursor-isotopes=None",
                "--full-scan-precursor-analyzer=centroided",
            };

            RunCommandAndValidateError(settings, string.Format(
                Resources.CommandArgs_WarnArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_,
                CommandArgs.ARG_FULL_SCAN_PRECURSOR_ANALYZER.ArgumentText,
                CommandArgs.ARG_FULL_SCAN_PRECURSOR_RES.ArgumentText));

            // parameter validation: DDA method with isolation scheme
            settings = new[]
            {
                "--full-scan-acquisition-method=DDA",
                "--full-scan-isolation-scheme=All Ions",
            };

            RunCommandAndValidateError(settings, string.Format(
                Resources.TransitionFullScan_DoValidate_An_isolation_window_width_value_is_not_allowed_in__0___mode,
                FullScanAcquisitionMethod.DDA.Label));

            // parameter validation: DIA method without isolation scheme
            settings = new[] { "--full-scan-acquisition-method=DIA" };

            RunCommandAndValidateError(settings, Resources.TransitionFullScan_DoValidate_An_isolation_window_width_value_is_required_in_DIA_mode);

            // parameter validation: int min
            settings = new[] { "--pep-min-length=" + (PeptideFilter.MIN_MIN_LENGTH - 1) };

            RunCommandAndValidateError(settings, string.Format(
                Resources.ValueOutOfRangeDoubleException_ValueOutOfRangeException_The_value___0___for_the_argument__1__must_be_between__2__and__3__,
                PeptideFilter.MIN_MIN_LENGTH - 1, CommandArgs.ARG_PEPTIDE_MIN_LENGTH.ArgumentText, PeptideFilter.MIN_MIN_LENGTH, PeptideFilter.MAX_MIN_LENGTH));

            // parameter validation: int max
            settings = new[] { "--pep-max-length=" + (PeptideFilter.MAX_MAX_LENGTH + 1) };

            RunCommandAndValidateError(settings, string.Format(
                Resources.ValueOutOfRangeDoubleException_ValueOutOfRangeException_The_value___0___for_the_argument__1__must_be_between__2__and__3__,
                PeptideFilter.MAX_MAX_LENGTH + 1, CommandArgs.ARG_PEPTIDE_MAX_LENGTH.ArgumentText, PeptideFilter.MIN_MAX_LENGTH, PeptideFilter.MAX_MAX_LENGTH));

            // parameter validation: bad bool
            settings = new[] { "--pep-exclude-potential-ragged-ends=maybe" };

            RunCommandAndValidateError(settings, string.Format(
                Resources.ValueUnexpectedException_ValueUnexpectedException_The_argument__0__should_not_have_a_value_specified,
                CommandArgs.ARG_PEPTIDE_EXCLUDE_POTENTIAL_RAGGED_ENDS.ArgumentText));

            // parameter validation: bad enzyme
            settings = new[] { "--pep-digest-enzyme=nope" };

            RunCommandAndValidateError(settings, string.Format(
                CommandArgUsage.ValueInvalidException_ValueInvalidException_The_value___0___is_not_valid_for_the_argument__1___Use_one_of__2_,
                "nope", CommandArgs.ARG_PEPTIDE_ENZYME_NAME.ArgumentText, string.Join(", ", Settings.Default.EnzymeList.Select(e => e.Name))));

            // parameter validation: unknown background proteome name
            settings = new[] { "--background-proteome-name=alien" };

            RunCommandAndValidateError(settings, string.Format(
                Resources.CommandArgs_ParseArgsInternal_Error____0___is_not_a_valid_value_for__1___It_must_be_one_of_the_following___2_,
                "alien", CommandArgs.ARG_BGPROTEOME_NAME.ArgumentText, string.Join(", ", Settings.Default.BackgroundProteomeList.Select(e => e.Name))));

            // parameter validation: bad background proteome path
            settings = new[] { "--background-proteome-file=missing" };

            RunCommandAndValidateError(settings, string.Format(
                Resources.CommandLine_SetPeptideDigestSettings_Error__Could_not_find_background_proteome_file__0_, "missing"));
        }

        [TestMethod]
        public void ConsoleOverwriteDocumentTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");

            // test --new
            {
                var settings = new[]
                {
                    "--new=" + docPath,
                    "--full-scan-precursor-isotopes=Count",
                };

                string output = AbstractUnitTestEx.RunCommand(settings);
                StringAssert.Contains(output, string.Format(Resources.CommandLine_NewSkyFile_FileAlreadyExists, docPath));

                output = AbstractUnitTestEx.RunCommand(settings.Append("--overwrite").ToArray());
                StringAssert.Contains(output, string.Format(Resources.CommandLine_NewSkyFile_Deleting_existing_file___0__, docPath));
                AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Error);
                AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Warning);

                SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
                Assert.AreEqual(FullScanPrecursorIsotopes.Count, doc.Settings.TransitionSettings.FullScan.PrecursorIsotopes);
            }

            // test --in/--out
            {
                string docPath2 = Path.ChangeExtension(docPath, ".2.sky");
                File.Copy(docPath, docPath2);

                var settings = new[]
                {
                    "--in=" + docPath,
                    "--out=" + docPath2,
                    "--full-scan-precursor-isotopes=Percent",
                };
                string output = AbstractUnitTestEx.RunCommand(settings);
                StringAssert.Contains(output, string.Format(Resources.CommandLine_NewSkyFile_FileAlreadyExists, docPath2));

                output = AbstractUnitTestEx.RunCommand(settings.Append("--overwrite").ToArray());
                AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Error);
                AssertEx.DoesNotContain(output, Resources.CommandLineTest_ConsoleAddFastaTest_Warning);

                SrmDocument doc = ResultsUtil.DeserializeDocument(docPath2);
                Assert.AreEqual(FullScanPrecursorIsotopes.Percent, doc.Settings.TransitionSettings.FullScan.PrecursorIsotopes);
            }
        }

        [TestMethod]
        public void ConsoleModsTest()
        {

            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            
            // test --pep-add-mod, --pep-add-unimod, --pep-add-mod-aa, and --pep-add-mod-term
            {
                var mods = new Dictionary<object, StaticMod>
                {
                    // either long name or tuple with short/unimod name, AA, and terminus
                    {"Acetyl (N-term)", null},
                    {"Phospho (ST)", null},
                    {"Acetyl:13C(2) (K)", null},
                    {"Water Loss (D, E, S, T)", null},
                    {(4, "C", ""), UniMod.GetModification("Carbamidomethyl (C)", out _)},
                    {("Oxi", "M", ""), UniMod.GetModification("Oxidation (M)", out _)},
                    {(258, "", "C"), UniMod.GetModification("Label:18O(1) (C-term)", out _)},
                    {("Ach", "", ""), UniMod.GetModification("Archaeol (C)", out _)},
                    {(949, "", ""), UniMod.GetModification("3-deoxyglucosone (R)", out _)},
                    
                };

                var settings = new[]
                {
                    "--in=" + docPath,
                    "--save"
                };

                string[] AddModArgs(string[] args, object nameOrId, string aas, string terminus)
                {
                    var modSettings = nameOrId switch
                    {
                        string name => settings.Append("--pep-add-mod=" + name),
                        int unimodId => settings.Append("--pep-add-unimod=" + unimodId),
                        _ => throw new ArgumentException()
                    };
                    if (!aas.IsNullOrEmpty())
                        modSettings = modSettings.Append("--pep-add-unimod-aa=" + aas);
                    if (!terminus.IsNullOrEmpty())
                        modSettings = modSettings.Append("--pep-add-unimod-term=" + terminus);
                    return modSettings.ToArray();
                }

                foreach (var mod in mods)
                {
                    if (mod.Key is string longName)
                        settings = settings.AppendToNew("--pep-add-mod=" + longName);
                    else if (mod.Key is ValueTuple<string, string, string> shortNameInfo)
                        settings = AddModArgs(settings, shortNameInfo.Item1, shortNameInfo.Item2, shortNameInfo.Item3);
                    else if (mod.Key is ValueTuple<int, string, string> unimodInfo)
                        settings = AddModArgs(settings, unimodInfo.Item1, unimodInfo.Item2, unimodInfo.Item3);
                }

                string output = AbstractUnitTestEx.RunCommand(settings);
                var doc = ResultsUtil.DeserializeDocument(docPath);

                foreach (var mod in mods)
                {
                    var expectedMod = mod.Value ?? UniMod.GetModification(mod.Key as string, out _);

                    // Settings > Peptide Settings -- Modifications > Structural modifications : "Oxidation (M)" was added
                    var isotopeType = IsotopeLabelType.light;
                    var modSection = PropertyNames.PeptideModifications_StaticModifications;
                    if (!UniMod.IsStructuralModification(expectedMod.Name))
                    {
                        // Settings > Peptide Settings -- Modifications > Isotope modifications > "heavy" : "Label:18O(1) (C-term)" was added
                        isotopeType = IsotopeLabelType.heavy;
                        modSection = isotopeType.ToString().Quote();
                    }

                    // test the mod was added in the command output
                    StringAssert.Contains(output, string.Format(AuditLogStrings.added_to, modSection, expectedMod.Name.Quote()));

                    // test the mod is set in the document
                    doc.Settings.PeptideSettings.Modifications.GetModifications(isotopeType).Contains(m => m.EquivalentAll(expectedMod));
                }
            }

            // test --pep-clear-mods
            {
                var settings = new[]
                {
                    "--in=" + docPath,
                    "--save",
                    "--pep-clear-mods"
                };

                string output = AbstractUnitTestEx.RunCommand(settings);
                var doc = ResultsUtil.DeserializeDocument(docPath);
                
                StringAssert.Contains(output, string.Format(AuditLogStrings.removed_all, PropertyNames.PeptideModifications_StaticModifications));
                StringAssert.Contains(output, string.Format(AuditLogStrings.removed_all, IsotopeLabelType.heavy.ToString().Quote()));
                Assert.AreEqual(0, doc.Settings.PeptideSettings.Modifications.StaticModifications.Count);
            }

            // test invalid values for mod parameters
            {
                // ReSharper disable RedundantArgumentDefaultValue
                const bool printErrors = false; // set to true for easy viewing of what the error messages actually look like

                RunCommandAndValidateError(new[] { "--pep-add-mod=Foo" },
                    string.Format(CommandArgUsage.ValueInvalidModException_ValueInvalidModException_Unable_to_add_peptide_modification___0_____1_,
                        "Foo", ModelResources.ModificationMatcher_GetStaticMod_no_UniMod_match), printErrors);

                RunCommandAndValidateError(new[] { "--pep-add-mod=Foo", "--pep-add-unimod-term=N" },
                    string.Format(CommandArgUsage.ValueInvalidModException_ValueInvalidModException_Unable_to_add_peptide_modification___0_____1_,
                        "Foo", ModelResources.ModificationMatcher_GetStaticMod_no_UniMod_match), printErrors);

                RunCommandAndValidateError(new[] { "--pep-add-mod=Oxidation" },
                    string.Format(CommandArgUsage.ValueInvalidModException_ValueInvalidModException_Unable_to_add_peptide_modification___0_____1_,
                        "Oxidation", ModelResources.ModificationMatcher_GetStaticMod_no_UniMod_match), printErrors);

                RunCommandAndValidateError(new [] { "--pep-add-mod=Oxi" },
                    string.Format(CommandArgUsage.ValueInvalidModException_ValueInvalidModException_Unable_to_add_peptide_modification___0_____1_,
                        "Oxi", ModelResources.ModificationMatcher_GetStaticMod_found_more_than_one_UniMod_match__add_terminus_and_or_amino_acid_specificity_to_choose_a_single_match));

                RunCommandAndValidateError(new[] { "--pep-add-mod=Oxi", "--pep-add-unimod-term=N" },
                    string.Format(CommandArgUsage.ValueInvalidModException_ValueInvalidModException_Unable_to_add_peptide_modification___0_____1_,
                        "Oxi", string.Format(ModelResources.ModificationMatcher_GetStaticMod_found_more_than_one_UniMod_match_but_the_given_specificity___0___does_not_match_any_of_them_,
                            TextUtil.ColonSeparate(PropertyNames.StaticMod_Terminus, "N"))), printErrors);
                
                RunCommandAndValidateError(new[] { "--pep-add-unimod-term=N" },
                    Resources.PeptideMod_SetTerminus_A_peptide_modification_must_be_added_before_giving_it_a_terminal_or_amino_acid_specificity_, printErrors);

                RunCommandAndValidateError(new[] { "--pep-add-mod-variable=True" },
                    Resources.PeptideMod_SetVariable_A_peptide_modification_must_be_added_before_assigning_its_variable_status_, printErrors);

                RunCommandAndValidateError(new[] { "--pep-add-mod=Oxi", "--pep-add-mod-variable=X" },
                    new CommandArgs.ValueInvalidBoolException(CommandArgs.ARG_PEPTIDE_ADD_MOD_VARIABLE, "X").Message, printErrors);

                // Variable failure on loss-only modification
                RunCommandAndValidateError(new[] { "--pep-add-mod=Water Loss (D, E, S, T)", "--pep-add-mod-variable=true" },
                    DocSettingsResources.StaticMod_Validate_Loss_only_modifications_may_not_be_variable, printErrors);

                // Variable failure for amino acid labeling modification
                RunCommandAndValidateError(new[] { "--pep-add-mod=Label:13C(6)15N(2) (K)", "--pep-add-mod-variable=true" },
                    DocSettingsResources.StaticMod_DoValidate_Isotope_modifications_may_not_be_variable_, printErrors);

                // Variable failure for formulaic isotope labeling modification
                RunCommandAndValidateError(new[] { "--pep-add-mod=Label:18O(1) (C-term)", "--pep-add-mod-variable=true" },
                    DocSettingsResources.StaticMod_DoValidate_Isotope_modifications_may_not_be_variable_, printErrors);

                RunCommandAndValidateError(new[] { "--pep-add-mod=Oxi", "--pep-add-unimod-term=Z" },
                    new CommandArgs.ValueInvalidModTerminusException(CommandArgs.ARG_PEPTIDE_ADD_MOD_TERM, "Z").Message, printErrors);

                RunCommandAndValidateError(new[] { "--pep-add-unimod=35", "--pep-add-unimod-aa=1" },
                    new CommandArgs.ValueInvalidAminoAcidException(CommandArgs.ARG_PEPTIDE_ADD_MOD_AA, "1").Message, printErrors);

                Assert.IsFalse(printErrors, "Set printErrors to false before committing.");
                // ReSharper restore RedundantArgumentDefaultValue
            }
        }

        [TestMethod]
        public void ConsoleReportExportTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = TestFilesDir.GetTestPath("Exported_test_report.csv");

            // Import the first RAW file (or mzML for international)
            string rawPath = TestFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
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
            var commandLine = new CommandLine();
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                commandLine.ImportResults(docContainer, replicate, MsDataFileUri.Parse(rawPath), null);
                docContainer.WaitForComplete();
                docContainer.AssertComplete();  // No errors
                doc = docContainer.Document;
            }

            MemoryDocumentContainer memoryDocumentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(memoryDocumentContainer.SetDocument(doc, memoryDocumentContainer.Document));
            SkylineDataSchema skylineDataSchema = new SkylineDataSchema(memoryDocumentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            DocumentGridViewContext viewContext = new DocumentGridViewContext(skylineDataSchema);
            ViewInfo viewInfo = viewContext.GetViewInfo(PersistedViews.MainGroup.Id.ViewName(reportName));
            StringWriter writer = new StringWriter();
            IProgressStatus status = new ProgressStatus("Exporting report");
            viewContext.Export(CancellationToken.None, null, ref status, viewInfo, writer, TextUtil.GetCsvSeparator(CultureInfo.CurrentCulture));
            var programmaticReport = writer.ToString();

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
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = TestFilesDir.GetTestPath("Exported_chromatograms.csv");

            // Import the first RAW file (or mzML for international)
            string rawFile = "ah_20101011y_BSA_MS-MS_only_5-2" + ExtensionTestContext.ExtThermoRaw;
            string rawPath = TestFilesDir.GetTestPath(rawFile);
            const string replicate = "Single";

            //Attach replicate
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            var commandLine = new CommandLine();
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                commandLine.ImportResults(docContainer, replicate, MsDataFileUri.Parse(rawPath), null);
                docContainer.WaitForComplete();
                docContainer.AssertComplete();  // No errors
                doc = docContainer.Document;
            }

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
            CollectionUtil.ForEach(doc.Settings.MeasuredResults.ReadStreams, s => s.CloseStream());
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
        public void ConsoleExportSpecLibTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleExportSpecLibTest.zip");
            // A document with no results. Attempting to export a spectral library should
            // provoke an error
            var docWithNoResultsPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            // A document with results that should be able to export a spectral library
            var docWithResults = TestFilesDir.GetTestPath("msstatstest.sky");
            var exportPath = TestFilesDir.GetTestPath("out_lib.blib"); // filepath to export library to
            var newDocumentPath = TestFilesDir.GetTestPath("new.sky");
            // Test error (no peptide precursors)
            var output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--exp-speclib-file=" + exportPath // Export a spectral library
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportSpecLib_Error__The_document_must_contain_at_least_one_precursor_to_export_a_spectral_library_), output);
            // Test error (no results)
            output = RunCommand("--in=" + docWithNoResultsPath, // Load a document with no results
                "--exp-speclib-file=" + exportPath // Export a spectral library
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportSpecLib_Error__The_document_must_contain_results_to_export_a_spectral_library_), output);
            // Test export
            output = RunCommand("--in=" + docWithResults, // Load a document with results
                "--exp-speclib-file=" + exportPath // Export a spectral library
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportSpecLib_Spectral_library_file__0__exported_successfully_, exportPath), output);
            Assert.IsTrue(File.Exists(exportPath)); // Check that the exported file exists
            var refSpectra = SpectralLibraryTestUtil.GetRefSpectraFromPath(exportPath);
            CheckRefSpectraAll(refSpectra); // Check the spectra in the exported file

        }

        [TestMethod]
        public void ConsoleExportMProphetTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleExportMProphetTest.zip");
            // Path to export mProphet file to
            var exportPath = TestFilesDir.GetTestPath("out.csv");
            // A document with no results. Attempting to export mProphet features should
            // provoke an error
            var docWithNoResultsPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            // A document with results that should be able to export mProphet features
            var docWithResults = TestFilesDir.GetTestPath("MProphetGold-trained-reduced.sky");
            // The expected .csv export
            var expectedExport = TestFilesDir.GetTestPathLocale("MProphet_expected.csv");
            // The expected export with targets only and best scoring peaks only options
            var expectedExportTargetsBestPeaks = TestFilesDir.GetTestPathLocale("MProphet_expected_targets_only_best_peaks_only.csv");
            // The expected export when excluding the "Intensity" and "Standard signal to noise" features.
            var expectedExportExcludeFeatures = TestFilesDir.GetTestPathLocale("MProphet_expected_exclude_features.csv");
            var newDocumentPath = TestFilesDir.GetTestPath("new.sky");
            // A string that is not a feature name or a mProphet file header
            const string invalidFeatureName = "-la";

            // Test error (no targets)
            var output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--exp-mprophet-features=" + exportPath // Export mProphet features
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportMProphetFeatures_Error__The_document_must_contain_targets_for_which_to_export_mProphet_features_), output);
            // Test error (no results)
            output = RunCommand("--in=" + docWithNoResultsPath, // Load a document with no results
                "--exp-mprophet-features=" + exportPath // Export mProphet features
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportMProphetFeatures_Error__The_document_must_contain_results_to_export_mProphet_features_), output);
            // Test error (invalid feature name)
            output = RunCommand("--in=" + docWithResults, // Load a document with no results
                "--exp-mprophet-features=" + exportPath, // Export mProphet features
                "--exp-mprophet-exclude-feature=" + invalidFeatureName
            );
            CheckRunCommandOutputContains(string.Format(Resources
                .CommandArgs_ParseArgsInternal_Error__Attempting_to_exclude_an_unknown_feature_name___0____Try_one_of_the_following_, invalidFeatureName), output);
            // Test export
            output = RunCommand("--in=" + docWithResults, // Load a document with results
                "--exp-mprophet-features=" + exportPath // Export mProphet features
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportMProphetFeatures_mProphet_features_file__0__exported_successfully_, exportPath), output);
            AssertEx.FileEquals(expectedExport, exportPath);
            // Test export with target peptides only and best scoring peaks only
            output = RunCommand("--in=" + docWithResults, // Load a document with results
                "--exp-mprophet-features=" + exportPath, // Export mProphet features
                "--exp-mprophet-targets-only", // Export should not include decoys peptides
                "--exp-mprophet-best-peaks-only" // Export should contain best scoring peaks
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportMProphetFeatures_mProphet_features_file__0__exported_successfully_, exportPath), output);
            AssertEx.FileEquals(expectedExportTargetsBestPeaks, exportPath);
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportMProphetFeatures_mProphet_features_file__0__exported_successfully_, exportPath), output);
            // Test export with some scores excluded
            output = RunCommand("--in=" + docWithResults, // Load a document with results
                "--exp-mprophet-features=" + exportPath, // Export mProphet features
                "--exp-mprophet-exclude-feature=" + "Intensity", // Export should not contain an "Intensity" column
                "--exp-mprophet-exclude-feature=" + "Standard signal to noise" // Export should not contain a "Standard signal to noise" column
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportMProphetFeatures_mProphet_features_file__0__exported_successfully_, exportPath), output);
            AssertEx.FileEquals(expectedExportExcludeFeatures, exportPath);
        }

        [TestMethod]
        public void ConsoleExportAnnotationsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleExportAnnotationsTest.zip");
            var exportPath = TestFilesDir.GetTestPath("out.csv");
            var documentWithAnnotations = TestFilesDir.GetTestPath("Study9_1_Site56C_Final_CURVE_Annotated_reduced.sky");
            var expectedIncludeProperties = TestFilesDir.GetTestPath("expected_annotations_include_properties.csv");
            var expectedIncludeObjects = TestFilesDir.GetTestPath("expected_annotations_include_objects.csv");
            var expectedAnnotationsNoBlankRows = TestFilesDir.GetTestPath("expected_annotations_no_blank_rows.csv");
            var newDocumentPath = TestFilesDir.GetTestPath("new.sky");
            const string invalidName = "-la";

            // Test error (invalid include-object name)
            var output = RunCommand("--new=" + newDocumentPath, // Create a document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--exp-annotations=" + exportPath, // Export annotations
                "--exp-annotations-include-object=" + invalidName // Test specifying an invalid object name
            );
            CheckRunCommandOutputContains(string.Format(CommandArgUsage.ValueInvalidException_ValueInvalidException_The_value___0___is_not_valid_for_the_argument__1___Use_one_of__2_,
                invalidName,
                "--exp-annotations-include-object", string.Join(", ", CommandArgs.GetAllHandlerNames())), output);
            // Test error (no annotations and not including properties)
            output = RunCommand("--new=" + newDocumentPath, // Create a document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--exp-annotations=" + exportPath // Export annotations
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportAnnotations_Error__The_document_must_contain_annotations_in_order_to_export_annotations_), output);
            // Test export with some object types included (everything else excluded) 
            output = RunCommand("--in=" + documentWithAnnotations, // Load a document that already contains annotations
                "--exp-annotations=" + exportPath, // Export annotations
                "--exp-annotations-include-object=" + "PrecursorResult", // Include "PrecursorResult" object type
                "--exp-annotations-include-object=" + "TransitionResult" // Include "TransitionResult" object type
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportAnnotations_Annotations_file__0__exported_successfully_, exportPath), output);
            AssertEx.FileEquals(expectedIncludeObjects, exportPath);
            // Test export with properties included
            output = RunCommand("--in=" + documentWithAnnotations, // Load a document that already contains annotations
                "--exp-annotations=" + exportPath, // Export annotations
                "--exp-annotations-include-properties" // Include all properties
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportAnnotations_Annotations_file__0__exported_successfully_, exportPath), output);
            AssertEx.FileEquals(expectedIncludeProperties, exportPath);
            // Test export with blank rows excluded
            output = RunCommand("--in=" + documentWithAnnotations, // Load a document that already contains annotations
                "--exp-annotations=" + exportPath, // Export annotations
                "--exp-annotations-remove-blank-rows" // Remove blank rows
            );
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportAnnotations_Annotations_file__0__exported_successfully_, exportPath), output);
            AssertEx.FileEquals(expectedAnnotationsNoBlankRows, exportPath);
        }

        [TestMethod]
        public void ConsoleAnnotationsExportToImportTest()
        {
            DoConsoleAnnotationsExportToImportTest(false);
        }

        [TestMethod]
        public void ConsoleAnnotationsExportToImportTestWithAuditLogging()
        {
            DoConsoleAnnotationsExportToImportTest(true);
        }

        [TestMethod]
        private void DoConsoleAnnotationsExportToImportTest(bool auditLogging)
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleAnnotationsExportToImportTest.zip");
            var documentWithAnnotations = TestFilesDir.GetTestPath("Study 7ii (site 52)_heavily_annotated.sky");
            var documentWithoutAnnotations = TestFilesDir.GetTestPath("Study 7ii (site 52)_no_annotations.sky");
            if (auditLogging)
            {
                EnableAuditLogging(documentWithoutAnnotations);
            }

            var documentWithImportedAnnotations = TestFilesDir.GetTestPath("Study 7ii (site 52)_imported_annotations.sky");
            var annotationsPath = TestFilesDir.GetTestPath("original_annotations.csv");
            var newAnnotationsPath = TestFilesDir.GetTestPath("annotations_from_new_document.csv");
            // Load a document that already contains annotations and export annotations
            RunCommand(
                "--in=" + documentWithAnnotations, 
                "--exp-annotations=" + annotationsPath 
            );
            // Load the same document with empty annotations and import the annotations exported in the last step
            // Then save the annotations and the document for comparison
            RunCommand(
                "--in=" + documentWithoutAnnotations,
                "--import-annotations=" + annotationsPath,
                "--exp-annotations=" + newAnnotationsPath, 
                "--out=" + documentWithImportedAnnotations
            );
            // Assert that annotations exported from the document with imported annotations match annotations
            // exported from the original document
            AssertEx.FileEquals(annotationsPath, newAnnotationsPath);
            // Assert that the chromatograms (and their annotations) are identical
            var originalDocument = ResultsUtil.DeserializeDocument(documentWithAnnotations);
            var outputDocument = ResultsUtil.DeserializeDocument(documentWithImportedAnnotations);
            Assert.AreEqual(originalDocument.Settings.MeasuredResults.Chromatograms, 
                outputDocument.MeasuredResults.Chromatograms);

            if (auditLogging)
            {
                var outputDocumentWithAuditLog =
                    DeserializeWithAuditLog(documentWithImportedAnnotations);
                Assert.IsTrue(outputDocumentWithAuditLog.Settings.DataSettings.IsAuditLoggingEnabled);
                AssertLastEntry(outputDocumentWithAuditLog.AuditLog, MessageType.imported_annotations);
            }
        }

        private static void CheckRefSpectraAll(IList<DbRefSpectra> refSpectra)
        {

            SpectralLibraryTestUtil.CheckRefSpectra(refSpectra, "APVPTGEVYFADSFDR", "APVPTGEVYFADSFDR", 2, 885.920, 4, 24.366);
            SpectralLibraryTestUtil.CheckRefSpectra(refSpectra, "APVPTGEVYFADSFDR", "APVPTGEVYFADSFDR[+10.00827]", 2, 890.924, 4, 24.532);
            SpectralLibraryTestUtil.CheckRefSpectra(refSpectra, "AVTELNEPLSNEDR", "AVTELNEPLSNEDR", 2, 793.886, 4, 17.095);
            SpectralLibraryTestUtil.CheckRefSpectra(refSpectra, "AVTELNEPLSNEDR", "AVTELNEPLSNEDR[+10.00827]", 2, 798.891, 4, 17.095);
            SpectralLibraryTestUtil.CheckRefSpectra(refSpectra, "DQGGELLSLR", "DQGGELLSLR", 2, 544.291, 4, 20.355);
            SpectralLibraryTestUtil.CheckRefSpectra(refSpectra, "DQGGELLSLR", "DQGGELLSLR[+10.00827]", 2, 549.295, 4, 20.311);
            SpectralLibraryTestUtil.CheckRefSpectra(refSpectra, "ELLTTMGDR", "ELLTTMGDR", 2, 518.261, 4, 16.904);
            SpectralLibraryTestUtil.CheckRefSpectra(refSpectra, "ELLTTMGDR", "ELLTTMGDR[+10.00827]", 2, 523.265, 4, 16.904); 
            Assert.IsTrue(!refSpectra.Any());
        }

        [TestMethod]
        public void ConsoleAddDecoysTest()
        {
            DoConsoleAddDecoysTest(false);
        }

        [TestMethod]
        public void ConsoleAddDecoysTestWithAuditLogging()
        {
            DoConsoleAddDecoysTest(true);
        }

        public void DoConsoleAddDecoysTest(bool auditLogging)
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            if (auditLogging)
            {
                EnableAuditLogging(docPath);
            }
            string outPath = TestFilesDir.GetTestPath("DecoysAdded.sky");
            string output = RunCommand("--in=" + docPath,
                                       "--decoys-add",
                                       "--out=" + outPath);
            const int expectedPeptides = 7;
            AssertEx.Contains(output, string.Format(Resources.CommandLine_AddDecoys_Added__0__decoy_peptides_using___1___method,
                expectedPeptides, DecoyGeneration.REVERSE_SEQUENCE));
            if (auditLogging)
            {
                var outputDocument =
                    DeserializeWithAuditLog(outPath);
                AssertLastEntry(outputDocument.AuditLog, MessageType.added_peptide_decoys);
            }

            output = RunCommand("--in=" + docPath,
                                       "--decoys-add=" + CommandArgs.ARG_VALUE_DECOYS_ADD_REVERSE);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_AddDecoys_Added__0__decoy_peptides_using___1___method,
                expectedPeptides, DecoyGeneration.REVERSE_SEQUENCE));
            if (auditLogging)
            {
                var outputDocument =
                    DeserializeWithAuditLog(outPath);
                AssertLastEntry(outputDocument.AuditLog, MessageType.added_peptide_decoys);
            }

            output = RunCommand("--in=" + docPath,
                                       "--decoys-add=" + CommandArgs.ARG_VALUE_DECOYS_ADD_SHUFFLE);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_AddDecoys_Added__0__decoy_peptides_using___1___method,
                expectedPeptides, DecoyGeneration.SHUFFLE_SEQUENCE));
            if (auditLogging)
            {
                var outputDocument =
                    DeserializeWithAuditLog(outPath);
                AssertLastEntry(outputDocument.AuditLog, MessageType.added_peptide_decoys);
            }

            const string badDecoyMethod = "shift";
            output = RunCommand("--in=" + docPath,
                                       "--decoys-add=" + badDecoyMethod);
            var arg = CommandArgs.ARG_DECOYS_ADD;
            AssertEx.Contains(output, new CommandArgs.ValueInvalidException(arg, badDecoyMethod, arg.Values).Message);

            output = RunCommand("--in=" + outPath,
                                       "--decoys-add");
            AssertEx.Contains(output, Resources.CommandLine_AddDecoys_Error__Attempting_to_add_decoys_to_document_with_decoys_);

            string discardedDecoysPath = TestFilesDir.GetTestPath("DecoysDiscarded.sky");
            output = RunCommand("--in=" + outPath, "--decoys-discard", "--out=" + discardedDecoysPath);
            AssertEx.Contains(output, Resources.CommandLine_AddDecoys_Decoys_discarded);
            if (auditLogging)
            {
                var outputDocument = DeserializeWithAuditLog(discardedDecoysPath);
                AssertLastEntry(outputDocument.AuditLog, MessageType.deleted_target);
            }

            output = RunCommand("--in=" + outPath, "--decoys-add", "--decoys-discard");
            AssertEx.Contains(output, Resources.CommandLine_AddDecoys_Decoys_discarded);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_AddDecoys_Added__0__decoy_peptides_using___1___method,
                expectedPeptides, DecoyGeneration.REVERSE_SEQUENCE));

            int tooManyPeptides = expectedPeptides + 1;
            output = RunCommand("--in=" + docPath,
                                       "--decoys-add",
                                       "--decoys-add-count=" + tooManyPeptides);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_AddDecoys_Error_The_number_of_peptides,
                    tooManyPeptides, 7, CommandArgs.ARG_DECOYS_ADD.ArgumentText, CommandArgs.ARG_VALUE_DECOYS_ADD_SHUFFLE));

            const int expectFewerPeptides = 4;
            output = RunCommand("--in=" + docPath,
                                       "--decoys-add",
                                       "--decoys-add-count=" + expectFewerPeptides);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_AddDecoys_Added__0__decoy_peptides_using___1___method,
                expectFewerPeptides, DecoyGeneration.REVERSE_SEQUENCE));
        }

        [TestMethod]
        public void ConsoleMassListTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);

            // Import the first RAW file (or mzML for international)
            string rawPath = TestFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                                                      ExtensionTestContext.ExtThermoRaw);

            /////////////////////////
            // Thermo test
            string thermoPath = TestFilesDir.GetTestPath("Thermo_test.csv");

            string output = RunCommand("--in=" + docPath,
                                       "--import-file=" + rawPath,
                                       "--exp-translist-instrument=" + ExportInstrumentType.THERMO,
                                       "--exp-file=" + thermoPath);

            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "Thermo_test.csv"), output);
            AssertEx.FileExists(thermoPath);
            Assert.AreEqual(doc.MoleculeTransitionCount, File.ReadAllLines(thermoPath).Length);


            /////////////////////////
            // Agilent test
            string agilentPath = TestFilesDir.GetTestPath("Agilent_test.csv");

            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.AGILENT,
                                "--exp-file=" + agilentPath,
                                "--exp-dwell-time=20");

            //check for success
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "Agilent_test.csv"), output);
            AssertEx.FileExists(agilentPath);
            Assert.AreEqual(doc.MoleculeTransitionCount + 1, File.ReadAllLines(agilentPath).Length);

            /////////////////////////
            // AB Sciex test
            string sciexPath = TestFilesDir.GetTestPath("AB_Sciex_test.csv");


            output = RunCommand("--in=" + docPath,
                                "--import-file=" + rawPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.ABI,
                                "--exp-file=" + sciexPath,
                                "--exp-dwell-time=20");

            //check for success
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "AB_Sciex_test.csv"), output);
            AssertEx.FileExists(sciexPath);
            Assert.AreEqual(doc.MoleculeTransitionCount, File.ReadAllLines(sciexPath).Length);

            /////////////////////////
            // Waters test
            string watersPath = TestFilesDir.GetTestPath("Waters_test.csv");
            var cmd = new[] {
                "--in=" + docPath,
                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                "--exp-file=" + watersPath,
                "--exp-run-length=100"
                };
            output = RunCommand(cmd);

            //check for success
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "Waters_test.csv"), output);
            AssertEx.FileExists(watersPath);
            Assert.AreEqual(doc.MoleculeTransitionCount + 1, File.ReadAllLines(watersPath).Length);

            // Run it again as a mixed polarity document
            MixedPolarityTest(doc, TestFilesDir, docPath, watersPath, cmd, false, false);
        }

        private static void MixedPolarityTest(SrmDocument doc, TestFilesDir testFilesDir, string inPath, string outPath, string[]cmds, 
            bool precursorsOnly, bool isMethod)
        {
            var refine = new RefinementSettings();
            var docMixed = refine.ConvertToSmallMolecules(doc, testFilesDir.FullPath, RefinementSettings.ConvertToSmallMoleculesMode.formulas,
                RefinementSettings.ConvertToSmallMoleculesChargesMode.invert_some); // Convert every other transition group to negative charge
            var xml = string.Empty;
            AssertEx.RoundTrip(docMixed, ref xml);
            var skyExt = Path.GetExtension(inPath) ?? string.Empty;
            inPath = PathEx.SafePath(inPath);
            var docPathMixed = inPath.Replace(skyExt, "_mixed_polarity"+skyExt);
            File.WriteAllText(docPathMixed, xml);
            var ext =  Path.GetExtension(PathEx.SafePath(outPath))??string.Empty;
            foreach (var polarityFilter in Helpers.GetEnumValues<ExportPolarity>().Reverse())
            {
                var outname = "polarity_test_" + polarityFilter + ext;
                var outPathMixed = testFilesDir.GetTestPath(outname);
                var args = new List<string>(cmds.Select(c => c.Replace(inPath, docPathMixed).Replace(outPath, outPathMixed))) { "--exp-polarity=" + polarityFilter };
                var output = RunCommand(args.ToArray());
                if (polarityFilter == ExportPolarity.separate && !isMethod)
                {
                    outname = outname.Replace(ext, "*" + ext); // Will create multiple files 
                }
                CheckRunCommandOutputContains(
                    string.Format(isMethod ? 
                    Resources.CommandLine_ExportInstrumentFile_Method__0__exported_successfully_ : 
                    Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, outname), output);
                if (polarityFilter == ExportPolarity.separate)
                {
                    PolarityFilterCheck(docMixed, outPathMixed, ExportPolarity.negative, ExportPolarity.separate, precursorsOnly, isMethod);
                    PolarityFilterCheck(docMixed, outPathMixed, ExportPolarity.positive, ExportPolarity.separate, precursorsOnly, isMethod);
                }
                else
                {
                    PolarityFilterCheck(docMixed, outPathMixed, polarityFilter, polarityFilter, precursorsOnly, isMethod);
                }
            }
        }

        private static void PolarityFilterCheck(SrmDocument docMixed, string path, ExportPolarity polarityFilter, ExportPolarity mode, bool precursorsOnly, bool isMethod)
        {
            var expected = 0;
            var nPositive = precursorsOnly
                ? docMixed.MoleculeTransitionGroups.Count(t => t.TransitionGroup.PrecursorCharge > 0)
                : docMixed.MoleculeTransitions.Count(t => t.Transition.Charge > 0);
            var nNegative = precursorsOnly
                ? docMixed.MoleculeTransitionGroups.Count(t => t.TransitionGroup.PrecursorCharge < 0)
                : docMixed.MoleculeTransitions.Count(t => t.Transition.Charge < 0);
            if (polarityFilter != ExportPolarity.positive)
            {
                expected += nNegative;
            }
            if (polarityFilter != ExportPolarity.negative)
            {
                expected += nPositive;
            }

            path = PathEx.SafePath(path) ?? string.Empty;
            var ext = Path.GetExtension(path);
            if (mode == ExportPolarity.separate)
            {
                // Expect a pair of files
                path = path.Replace(ext, string.Format("_{0}_{1:0000}{2}", ExportPolarity.negative, 1, ext));
                if (isMethod)
                {
                    Assert.IsTrue(Directory.Exists(path));
                }
                else
                {
                    AssertEx.FileExists(path);
                    Assert.AreEqual(nNegative + 1, File.ReadAllLines(path).Length, polarityFilter.ToString());
                }
                path = path.Replace(ExportPolarity.negative.ToString(), ExportPolarity.positive.ToString());
                expected = nPositive;
            }
            else if (isMethod)
            {
                path = path.Replace(ext, "_0001"+ext);
            }
            if (isMethod)
            {
                Assert.IsTrue(Directory.Exists(path));
            }
            else
            {
                AssertEx.FileExists(path);
                Assert.AreEqual(expected + 1, File.ReadAllLines(path).Count(l => !string.IsNullOrEmpty(l)), polarityFilter.ToString());
            }
        }

        [TestMethod]
        public void ConsoleMethodTest()
        {
            //Here I'll only test Agilent for now
            TestFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);

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
            string docPath2 = TestFilesDir.GetTestPath("WormUnrefined.sky");
            string agilentTemplate = TestFilesDir.GetTestPath("43mm-40nL-30min-opt.m");
            string agilentOut = TestFilesDir.GetTestPath("Agilent_test.m");

            // Try this a few times, because Agilent method building seems to fail under stress
            // about 10% of the time.
            bool success = false;
            string output = "";
            for (int i = 0; !success && i < 3; i++)
            {
                var cmd = new[] {"--in=" + docPath2,
                                 "--exp-method-instrument=Agilent 6400 Series",
                                 "--exp-template=" + agilentTemplate,
                                 "--exp-file=" + agilentOut,
                                 "--exp-dwell-time=20",
                                 "--exp-strategy=buckets",
                                 "--exp-max-trans=75",
                                 "--import-warn-on-failure"
                };
                output = RunCommand(cmd);

                //check for success
                success = output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_Method__0__exported_successfully_, "Agilent_test.m"));

                // Relax a bit if things aren't going well.
                if (!success)
                    Thread.Sleep(5000);
                else
                {
                    try
                    {
                        // Run it again as a mixed polarity document
                        var doc = ResultsUtil.DeserializeDocument(docPath2);
                        MixedPolarityTest(doc, TestFilesDir, docPath2, agilentOut, cmd, false, true);
                    }
                    catch (Exception)
                    {
                        success = false; // Allow for retries
                    }
                }
            }

            if (!success)
            {
                Assert.Fail("Failed to write Agilent method: {0}", output);
            }

            // Test order by m/z
            var mzOrderOut = TestFilesDir.GetTestPath("export-order-by-mz.txt");
            var cmd2 = new[] {"--in=" + docPath2,
                "--exp-translist-instrument=Thermo",
                "--exp-order-by-mz",
                "--exp-file=" + mzOrderOut,
                "--import-warn-on-failure"
            };
            output = RunCommand(cmd2);

            //check for success
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "export-order-by-mz.txt")));
            using (var reader = new StreamReader(mzOrderOut))
            {
                double prevPrecursor = 0;
                double prevProduct = 0;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    Assert.IsNotNull(line);
                    var values = line.Split(',');
                    Assert.IsTrue(values.Length >= 2);
                    Assert.IsTrue(double.TryParse(values[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var precursor));
                    Assert.IsTrue(double.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var product));
                    Assert.IsTrue(prevPrecursor <= precursor);
                    if (prevPrecursor != precursor)
                    {
                        prevProduct = 0;
                    }
                    Assert.IsTrue(prevProduct <= product);
                    prevPrecursor = precursor;
                    prevProduct = product;
                }
            }
        }

        [TestMethod]
        public void ConsoleExportTrigger()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string failurePath = testFilesDir.GetTestPath("Failure_test.csv");

            var args = new[]
            {
                "--in=" + docPath,
                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                "--exp-file=" + failurePath,
                "--exp-strategy=single",
                "--exp-method-type=triggered",
                "--exp-primary-count=x"
            };
            string output = RunCommand(args);

            AssertEx.Contains(output, new CommandArgs.ValueInvalidIntException(CommandArgs.ARG_EXP_PRIMARY_COUNT, "x").Message);
            args[args.Length - 1] = "--exp-primary-count=1";
            output = RunCommand(args);

            //check for warning and error
            Assert.AreEqual(1, CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Warning, output));  // exp-primary-count and CE not Waters
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
            new CommandLine().SaveDocument(doc, triggerPath, new StringWriter());

            output = RunCommand("--in=" + triggerPath,
                                "--exp-translist-instrument=" + ExportInstrumentType.AGILENT,
                                "--exp-file=" + agilentTriggeredPath,
                                "--exp-strategy=single",
                                "--exp-method-type=triggered");
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "AgilentTriggered.csv")));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Warning));
            AssertEx.FileExists(agilentTriggeredPath);
            Assert.AreEqual(doc.PeptideTransitionCount + 1, File.ReadAllLines(agilentTriggeredPath).Length);

            // Isolation list export
            string agilentIsolationPath = testFilesDir.GetTestPath("AgilentIsolationList.csv");
            var cmd = new[]
            {
                "--in=" + docPath,
                "--exp-isolationlist-instrument=" + ExportInstrumentType.AGILENT_TOF,
                "--exp-strategy=single",
                "--exp-file=" + agilentIsolationPath
            };
            output = RunCommand(cmd);
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_, "AgilentIsolationList.csv")));
            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsoleAddFastaTest_Error));
            AssertEx.FileExists(agilentIsolationPath);
            doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(doc.PeptideTransitionGroupCount + 1, File.ReadAllLines(agilentIsolationPath).Length);

            // Run it again as a mixed polarity document
            MixedPolarityTest(doc, testFilesDir, docPath, agilentIsolationPath, cmd, true, false);

            // Let base class handle cleanup
            TestFilesDirs = new[] { testFilesDir, commandFilesDir };
        }

        private static void AssertErrorCount(int expectedErrorsInOutput, string output, string failureMessage)
        {
            // Include not-yet-localized messages in the error count
            var countErrorsLocalized = Resources.CommandLineTest_ConsoleAddFastaTest_Error.Contains("Error") ? 0 : CountInstances(Resources.CommandLineTest_ConsoleAddFastaTest_Error, output);
            var countErrorsEnglish = CountInstances("Error", output);
            Assert.AreEqual(expectedErrorsInOutput, countErrorsLocalized + countErrorsEnglish, failureMessage);
        }

        [TestMethod]
        public void ConsolePathCoverage()
        {
            TestFilesDirs = new[]
            {
                new TestFilesDir(TestContext, ZIP_FILE),
                new TestFilesDir(TestContext, COMMAND_FILE)
            };
            var testFilesDir = TestFilesDirs[0];
            string bogusPath = testFilesDir.GetTestPath("bogus_file.sky");
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            string outPath = testFilesDir.GetTestPath("Output_file.sky");
            string tsvPath = testFilesDir.GetTestPath("Exported_test_report.csv");

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);

            //Error: file does not exist
            var output = RunCommand("--in=" + bogusPath);
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_OpenSkyFile_Error__The_Skyline_file__0__does_not_exist_, bogusPath)));

            //Error: raw file does not exist
            var pathNotExists = rawPath + "x";
            output = RunCommand("--in=" + docPath,
                                "--import-file=" + pathNotExists,
                                "--import-replicate-name=Single");
            Assert.IsTrue(output.Contains(string.Format(Resources.ChromCacheBuilder_BuildNextFileInner_The_file__0__does_not_exist, pathNotExists)));

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
            var commandFilesDir = TestFilesDirs[1];
            string thermoTemplate = commandFilesDir.GetTestPath("20100329_Protea_Peptide_targeted.meth");
            output = RunCommand("--in=" + docPath,
                                "--exp-method-instrument=" + ExportInstrumentType.THERMO_LTQ,
                                "--exp-method-type=scheduled",
                                "--exp-strategy=single",
                                "--exp-file=" + testFilesDir.GetTestPath("Bogus.meth"),
                                "--exp-template=" + thermoTemplate);
            Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ExportInstrumentFile_Error__the_specified_instrument__0__is_not_compatible_with_scheduled_methods_, ExportInstrumentType.THERMO_LTQ)));
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
            new CommandLine().SaveDocument(doc, schedulePath, new StringWriter());
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
            var args = new[]
            {
                "--in=" + docPath,
                "--import-file=" + rawPath,
                "--import-replicate-name=Single",
                "--report-format=tsv",  // placeholder for replacement below
                "--exp-translist-instrument=" + ExportInstrumentType.WATERS,
                "--exp-method-instrument=" + ExportInstrumentType.THERMO_LTQ
            };
            output = RunCommand(args);
                                //1 Error for using the above 2 parameters simultaneously

            Assert.IsFalse(output.Contains(Resources.CommandLineTest_ConsolePathCoverage_successfully_));

            Assert.AreEqual(1, CountErrors(output));

            //Test value lists for failing values
            const string bogusValue = "BOGUS";
            CommandArgs.Argument[] valueListArgs = 
            {
                CommandArgs.ARG_REPORT_FORMAT,
                CommandArgs.ARG_EXP_STRATEGY,
                CommandArgs.ARG_EXP_METHOD_TYPE,
                CommandArgs.ARG_EXP_OPTIMIZING,
                CommandArgs.ARG_EXP_POLARITY,
            };
            foreach (var valueListArg in valueListArgs)
            {
                args[3] = valueListArg.ArgumentText + "=" + bogusValue;
                output = RunCommand(args);
                AssertEx.Contains(output, new CommandArgs.ValueInvalidException(valueListArg, bogusValue, valueListArg.Values).Message);
            }

            // Transition list, isolation list, and method export
            string bogusInstrument = string.Format(
                SkylineResources.CommandArgs_ParseArgsInternal_Warning__The_instrument_type__0__is_not_valid__Please_choose_from_,
                bogusValue);
            args[3] = CommandArgs.ARG_EXP_TRANSITION_LIST_INSTRUMENT.ArgumentText + "=" + bogusValue;
            output = RunCommand(args);
            AssertEx.Contains(output,
                bogusInstrument,
                TextUtil.LineSeparate(ExportInstrumentType.TRANSITION_LIST_TYPES),
                SkylineResources.CommandArgs_ParseArgsInternal_No_transition_list_will_be_exported_);
            args[3] = CommandArgs.ARG_EXP_ISOLATION_LIST_INSTRUMENT.ArgumentText + "=" + bogusValue;
            output = RunCommand(args);
            AssertEx.Contains(output,
                bogusInstrument,
                TextUtil.LineSeparate(ExportInstrumentType.ISOLATION_LIST_TYPES),
                SkylineResources.CommandArgs_ParseArgsInternal_No_isolation_list_will_be_exported_);
            args[3] = CommandArgs.ARG_EXP_METHOD_INSTRUMENT.ArgumentText + "=" + bogusValue;
            output = RunCommand(args);
            AssertEx.Contains(output,
                bogusInstrument,
                TextUtil.LineSeparate(ExportInstrumentType.METHOD_TYPES),
                SkylineResources.CommandArgs_ParseArgsInternal_No_method_will_be_exported_);

            CommandArgs.Argument[] valueIntArguments =
            {
                CommandArgs.ARG_EXP_MAX_TRANS,
                CommandArgs.ARG_EXP_DWELL_TIME
            };
            foreach (var valueIntArg in valueIntArguments)
            {
                args[3] = valueIntArg.ArgumentText + "=" + bogusValue;
                output = RunCommand(args);
                AssertEx.Contains(output, new CommandArgs.ValueInvalidIntException(valueIntArg, bogusValue).Message);
            }

            CommandArgs.Argument[] valueDoubleArguments =
            {
                CommandArgs.ARG_EXP_RUN_LENGTH,
                CommandArgs.ARG_IMPORT_LOCKMASS_POSITIVE,
                CommandArgs.ARG_IMPORT_LOCKMASS_NEGATIVE,
                CommandArgs.ARG_IMPORT_LOCKMASS_TOLERANCE
            };
            foreach (var valueDoubleArg in valueDoubleArguments)
            {
                args[3] = valueDoubleArg.ArgumentText + "=" + bogusValue;
                output = RunCommand(args);
                AssertEx.Contains(output, new CommandArgs.ValueInvalidDoubleException(valueDoubleArg, bogusValue).Message);
            }
            const int bigValue = 100000000;
            args[3] = "--exp-dwell-time=" + bigValue;
            output = RunCommand(args);
            AssertEx.Contains(output, new CommandArgs.ValueOutOfRangeIntException(CommandArgs.ARG_EXP_DWELL_TIME, bigValue,
                AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX).Message);
            args[3] = "--exp-run-length=" + bigValue;
            output = RunCommand(args);
            AssertEx.Contains(output, new CommandArgs.ValueOutOfRangeIntException(CommandArgs.ARG_EXP_RUN_LENGTH, bigValue,
                AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX).Message);


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
                                        "exp-polarity",
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
                                                "exp-polarity",
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

        private void TestNameValueFailures(IEnumerable<string> names, Func<string, string> getCommandLineForArg, bool allowUnlocalizedErrors = false)
        {
            foreach (var name in names)
            {
                string arg = string.Format("--{0}", name);
                string output = RunCommand(getCommandLineForArg(arg));
                Assert.AreEqual(1, CountErrors(output, allowUnlocalizedErrors), string.Format("No error for argument {0}", arg));
                Assert.AreEqual(1, CountInstances(arg, output), string.Format("Missing expected argument {0}", arg));
            }
        }

        // Historical: Test the case where the imported replicate has the wrong path without Lorenzo's data
        //[TestMethod]
        public void TestLorenzo()
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new CommandStatusWriter(new StringWriter(consoleBuffer));

            TestFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);

            string docPath = TestFilesDir.GetTestPath("VantageQCSkyline.sky");
            string tsvPath = TestFilesDir.GetTestPath("Exported_test_report.csv");
            string dataPath = TestFilesDir.GetTestPath("VantageQCSkyline.skyd");

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
            Assert.AreEqual(3, CountInstances("hello", s));

            s += "hi";
            Assert.AreEqual(3, CountInstances("hello", s));

            Assert.AreEqual(0, CountInstances("", ""));

            Assert.AreEqual(0, CountInstances("hi", "howdy"));
        }

        [TestMethod]
        public void ConsoleBadRawFileImportTest()
        {
            // Run this test only if we can read Thermo's raw files
            if(ExtensionTestContext.CanImportThermoRaw &&
                ExtensionTestContext.CanImportWatersRaw)
            {
                const string testZipPath = @"TestData\ImportAllCmdLineTest.zip";

                TestFilesDir = new TestFilesDir(TestContext, testZipPath);

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

                var docPath = TestFilesDir.GetTestPath("test.sky");

                var rawPath = TestFilesDir.GetTestPath("bad_file.raw");

                var msg = RunCommand("--in=" + docPath,
                                     "--import-file=" + rawPath,
                                     "--save");

                AssertEx.Contains(msg, string.Format(Resources.CommandLine_ImportResultsFile_Error__Failed_importing_the_results_file__0__, rawPath));

                // the document should not have changed
                SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
                Assert.IsFalse(doc.Settings.HasResults);

                msg = RunCommand("--in=" + docPath,
                                 "--import-all=" + TestFilesDir.FullPath,
                                 "--import-warn-on-failure",
                                 "--save");

                string expected = string.Format(Resources.CommandLine_ImportResultsFile_Warning__Failed_importing_the_results_file__0____Ignoring___, rawPath);
                AssertEx.Contains(msg, expected);
                doc = ResultsUtil.DeserializeDocument(docPath);
                Assert.IsTrue(doc.Settings.HasResults, TextUtil.LineSeparate("No results found.", "Output:", msg));
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
                                    ? @"TestData\ImportAllCmdLineTest.zip"
                                    : @"TestData\ImportAllCmdLineTestMzml.zip";
            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

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

            
            var docPath = TestFilesDir.GetTestPath("test.sky");
            var outPath = TestFilesDir.GetTestPath("import_nonSRM_file.sky");

            var rawPath = TestFilesDir.GetTestPath("FullScan" + extRaw);

            // Try to import FullScan.RAW|mzML
            var msg = RunCommand("--in=" + docPath,
                       "--import-file=" + rawPath,
                       "--import-warn-on-failure",
                       "--out=" + outPath);

             CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ImportResultsFile_Warning__Failed_importing_the_results_file__0____Ignoring___, rawPath), msg);
            // Read the saved document. FullScan.RAW|mzML should not have been imported
            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsFalse(doc.Settings.HasResults);

            // Import all files in the directory. FullScan.RAW|mzML should not be imported
            msg = RunCommand("--in=" + outPath,
                             "--import-all=" + TestFilesDir.FullPath,
                             "--import-warn-on-failure",
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

        private static string GetTestZipPath(out string extRaw)
        {
            bool useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;
            string testZipPath = useRaw
                ? @"TestData\ImportAllCmdLineTest.zip"
                : @"TestData\ImportAllCmdLineTestMzml.zip";
            extRaw = useRaw
                ? ".raw"
                : ".mzML";
            return testZipPath;
        }

        [TestMethod]
        public void ConsoleMultiReplicateImportTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, GetTestZipPath(out var extRaw), "ConsoleMultiReplicateImportTest");


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



            var docPath = TestFilesDir.GetTestPath("test.sky");
            var outPath0 = TestFilesDir.GetTestPath("Imported_multiple0.sky");
            FileEx.SafeDelete(outPath0);
            var outPath1 = TestFilesDir.GetTestPath("Imported_multiple1.sky");
            FileEx.SafeDelete(outPath1);
            var outPath4 = TestFilesDir.GetTestPath("Imported_multiple4.sky");
            FileEx.SafeDelete(outPath4);

            var rawPath = new MsDataFilePath(TestFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_01" + extRaw));
            
            // Test: Cannot use --import-file and --import-all options simultaneously
            var msg = RunCommand("--in=" + docPath,
                                 "--import-file=" + rawPath.FilePath,
                                 "--import-replicate-name=Unscheduled01",
                                 "--import-all=" + TestFilesDir.FullPath,
                                 "--out=" + outPath1);
            Assert.IsTrue(msg.Contains(CommandArgs.ErrorArgsExclusiveText(CommandArgs.ARG_IMPORT_FILE, CommandArgs.ARG_IMPORT_ALL)), msg);
            // output file should not exist
            AssertEx.FileNotExists(outPath1);



            // Test: Use --import-replicate-name with --import-all for single-replicate, multi-file import
            const string singleName = "Unscheduled01";
            msg = RunCommand("--in=" + docPath,
                             "--import-replicate-name=" + singleName,
                             "--import-all=" + TestFilesDir.GetTestPath("REP01"),
                             "--out=" + outPath0);
            // Used to give this error
//            Assert.IsTrue(msg.Contains(Resources.CommandArgs_ParseArgsInternal_Error____import_replicate_name_cannot_be_used_with_the___import_all_option_), msg);
//            // output file should not exist
            AssertEx.FileExists(outPath0, msg);            
            SrmDocument doc0 = ResultsUtil.DeserializeDocument(outPath0);
            Assert.AreEqual(1, doc0.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc0.Settings.MeasuredResults.ContainsChromatogram(singleName));
            Assert.AreEqual(2, doc0.Settings.MeasuredResults.Chromatograms[0].MSDataFileInfos.Count);



            // Test: Cannot use --import-naming-pattern with --import-file
            msg = RunCommand("--in=" + docPath,
                                 "--import-file=" + rawPath.FilePath,
                                 "--import-naming-pattern=prefix_(.*)",
                                 "--out=" + outPath1);
            Assert.IsTrue(msg.Contains(CommandArgs.ErrorArgsExclusiveText(CommandArgs.ARG_IMPORT_NAMING_PATTERN, CommandArgs.ARG_IMPORT_FILE)), msg);
            // output file should not exist
            AssertEx.FileNotExists(outPath1);




            // Test: invalid regular expression (1)
            msg = RunCommand("--in=" + docPath,
                                 "--import-all=" + TestFilesDir.FullPath,
                                 "--import-naming-pattern=A",
                                 "--out=" + outPath1);
            // output file should not exist
            AssertEx.FileNotExists(outPath1);
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression___0___does_not_have_any_groups___String, "A")), msg);



            // Test: invalid regular expression (2)
            msg = RunCommand("--in=" + docPath,
                      "--import-all=" + TestFilesDir.FullPath,
                      "--import-naming-pattern=invalid",
                      "--out=" + outPath1);
            // output file should not exist
            AssertEx.FileNotExists(outPath1);
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression___0___does_not_have_any_groups___String, "invalid")), msg);




            // Test: Import files in the "REP01" directory; 
            // Use a naming pattern that will cause the replicate names of the two files to be the same
            msg = RunCommand("--in=" + docPath,
                             "--import-all=" + TestFilesDir.GetTestPath("REP01"),
                             "--import-naming-pattern=.*_(REP[0-9]+)_(.+)",
                             "--out=" + outPath1);
            AssertEx.FileNotExists(outPath1);
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandLine_ApplyNamingPattern_Error__Duplicate_replicate_name___0___after_applying_regular_expression_,"REP1")), msg);




            // Test: Import files in the "REP01" directory; Use a naming pattern
            msg = RunCommand("--in=" + docPath,
                             "--import-all=" + TestFilesDir.GetTestPath("REP01"),
                             "--import-naming-pattern=.*_([0-9]+)",
                             "--out=" + outPath1);
            AssertEx.FileExists(outPath1, msg);
            SrmDocument doc = ResultsUtil.DeserializeDocument(outPath1);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("01"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("02"));



            // Test: Import non-recursive
            // Make sure only files directly in the folder get imported
            string badFilePath = TestFilesDir.GetTestPath("bad_file" + extRaw);
            string badFileMoved = badFilePath + ".save";
            if (File.Exists(badFilePath))
                File.Move(badFilePath, badFileMoved);
            string fullScanPath = TestFilesDir.GetTestPath("FullScan" + extRaw);
            string fullScanMoved = fullScanPath + ".save";
            File.Move(fullScanPath, fullScanMoved);

            msg = RunCommand("--in=" + docPath,
                "--import-all-files=" + TestFilesDir.FullPath,
                "--out=" + outPath4);

            AssertEx.FileExists(outPath4, msg);
            doc = ResultsUtil.DeserializeDocument(outPath4);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(4, doc.Settings.MeasuredResults.Chromatograms.Count,
                string.Format("Expected 4 replicates from files, found: {0}",
                    string.Join(", ", doc.Settings.MeasuredResults.Chromatograms.Select(chromSet => chromSet.Name).ToArray())));
            if (File.Exists(badFileMoved))
                File.Move(badFileMoved, badFilePath);
            File.Move(fullScanMoved, fullScanPath);
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] // Just a really tricky race condition using --warn-on-failure under parallel testing pressure
        public void ConsoleMultiWarnOnFailureImportTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, GetTestZipPath(out var extRaw), "ConsoleMultiWarnOnFailureImportTest");

            var docPath = TestFilesDir.GetTestPath("test.sky");
            var outPath2 = TestFilesDir.GetTestPath("Imported_multiple2.sky");
            FileEx.SafeDelete(outPath2);
            var rawPath = new MsDataFilePath(TestFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_01" + extRaw));

            AssertEx.FileNotExists(outPath2);

            // Test: Import a single file
            // Import REP01\CE_Vantage_15mTorr_0001_REP1_01.raw;
            // Use replicate name "REP01"
            var msg = RunCommand("--in=" + docPath,
                "--import-file=" + rawPath.FilePath,
                "--import-replicate-name=REP01",
                "--out=" + outPath2);
            AssertEx.FileExists(outPath2, msg);
            var doc = ResultsUtil.DeserializeDocument(outPath2);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            int initialFileCount = 0;
            foreach (var chromatogram in doc.Settings.MeasuredResults.Chromatograms)
            {
                initialFileCount += chromatogram.MSDataFilePaths.Count();
            }

            // Import another single file. 
            var rawPath2 = MsDataFileUri.Parse(TestFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML"));
            msg = RunCommand("--in=" + outPath2,
                "--import-file=" + rawPath2.GetFilePath(),
                "--import-replicate-name=160109_Mix1_calcurve_070",
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath2);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count, msg);
            ChromatogramSet chromatSet;
            doc.Settings.MeasuredResults.TryGetChromatogramSet("160109_Mix1_calcurve_070", out chromatSet, out _);
            Assert.IsNotNull(chromatSet, msg);
            Assert.IsTrue(chromatSet.MSDataFilePaths.Contains(rawPath2));

            // Test: Import all files and sub-folders in test directory
            // The document should already contain a replicate named "REP01".
            // A new replicate "REP012" should be added since "REP01" already exists.
            // The document should also already contain replicate "160109_Mix1_calcurve_070".
            // There should be notes about ignoring the two files that are already in the document.
            msg = RunCommand("--in=" + outPath2,
                             "--import-all=" + TestFilesDir.FullPath,
                             "--import-warn-on-failure",
                             "--save");

            Assert.IsFalse(msg.Contains(string.Format(Resources.Error___0_, string.Empty)), msg);

            // ExtensionTestContext.ExtThermo raw uses different case from file on disk
            // which happens to make a good test case.
            MsDataFilePath rawPathDisk = GetThermoDiskPath(rawPath);

            // These messages are due to files that were already in the document.
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___, "REP01", rawPathDisk)), msg);
            Assert.IsTrue(msg.Contains(string.Format(Resources.CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___, "160109_Mix1_calcurve_070", rawPath2)), msg);
            //            Assert.IsTrue(msg.Contains(string.Format("160109_Mix1_calcurve_070 -> {0}",rawPath2)), msg); 

            doc = ResultsUtil.DeserializeDocument(outPath2);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(7, doc.Settings.MeasuredResults.Chromatograms.Count,
                string.Format("Expected 7 replicates, found: {0}",
                              string.Join(", ", doc.Settings.MeasuredResults.Chromatograms.Select(chromSet => chromSet.Name).ToArray())));
            // count the number of files imported into the document
            int totalImportedFiles = 0;
            foreach (var chromatogram in doc.Settings.MeasuredResults.Chromatograms)
            {
                totalImportedFiles += chromatogram.MSDataFilePaths.Count();
            }
            // We should have imported 7 more file
            Assert.AreEqual(initialFileCount + 7, totalImportedFiles);
            // In the "REP01" replicate we should have 1 file
            ChromatogramSet chromatogramSet;
            doc.Settings.MeasuredResults.TryGetChromatogramSet("REP01", out chromatogramSet, out _);
            Assert.IsNotNull(chromatogramSet);
            Assert.IsTrue(chromatogramSet.MSDataFilePaths.Count() == 1);
            Assert.IsTrue(chromatogramSet.MSDataFilePaths.Contains(
                new MsDataFilePath(TestFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_01" +
                                                            extRaw))));
            // REP012 should have the file REP01\CE_Vantage_15mTorr_0001_REP1_02.raw|mzML
            doc.Settings.MeasuredResults.TryGetChromatogramSet("REP012", out chromatogramSet, out _);
            Assert.IsNotNull(chromatogramSet);
            Assert.IsTrue(chromatogramSet.MSDataFilePaths.Count() == 1);
            Assert.IsTrue(chromatogramSet.MSDataFilePaths.Contains(
                GetThermoDiskPath(new MsDataFilePath(TestFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_02" + extRaw)))));
        }

        [TestMethod]
        public void ConsoleFileNameRegexImportTest()
        {
            bool useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;
            string testZipPath = useRaw
                ? @"TestData\ImportAllCmdLineTest.zip"
                : @"TestData\ImportAllCmdLineTestMzml.zip";
            string extRaw = useRaw
                ? ".raw"
                : ".mzML";

            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

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
            //   -- 160109_Mix1_calcurve_071.raw (Waters .raw directory)|mzML
            //   -- 160109_Mix1_calcurve_074.raw (Waters .raw directory)|mzML
            //   -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
            //   -- bad_file_folder
            //       -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
            //   -- FullScan.RAW|mzML (should not be imported)
            //   -- FullScan_folder
            //       -- FullScan.RAW|mzML (should not be imported)

            var docPath = TestFilesDir.GetTestPath("test.sky");
            var outPath = TestFilesDir.GetTestPath("out.sky");
            FileEx.SafeDelete(outPath);

            var rawPath = MsDataFileUri.Parse(TestFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML"));
            // Test: invalid regex
            var msg = RunCommand("--in=" + docPath,
                "--import-file=" + rawPath.GetFilePath(),
                "--import-filename-pattern=*",
                "--out=" + outPath);
            CheckRunCommandOutputContains(
                string.Format(
                    Resources.CommandArgs_ParseRegexArgument_Error__Regular_expression___0___for__1__cannot_be_parsed_,
                    "*", "--import-filename-pattern"), msg);

            // Regex 1 - given raw file does not match the pattern
            // Call RunCommand instead of just testing the ApplyFileAndSampleNameRegex method so that we test 
            // that the error reporting and returned exit status are in sync.
            var pattern = "QC.*";
            msg = RunCommand("--in=" + docPath,
                "--import-file=" + rawPath.GetFilePath(),
                "--import-filename-pattern=" + pattern,
                "--out=" + outPath);
            CheckRunCommandOutputContains(
                string.Format(
                    Resources.CommandLine_ApplyFileNameRegex_File_name___0___does_not_match_the_pattern___1____Ignoring__2_,
                    rawPath.GetFileName(), pattern, rawPath), msg);
            CheckRunCommandOutputContains(
                string.Format(Resources.CommandLine_ApplyFileAndSampleNameRegex_Error__No_files_match_the_file_name_pattern___0___, pattern), msg);



            var log = new StringBuilder();
            var commandLine = new CommandLine(new CommandStatusWriter(new StringWriter(log)));

            IList<KeyValuePair<string, MsDataFileUri[]>> dataSourceList = DataSourceUtil.GetDataSources(TestFilesDir.FullPath).ToArray();
            IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>(dataSourceList);

            // Regex 2
            log.Clear();
            pattern = "\\d{6}_Mix\\d";
            listNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>(dataSourceList);
            commandLine.ApplyFileAndSampleNameRegex(new Regex(pattern), null, ref listNamedPaths);
            Assert.AreEqual(4, listNamedPaths.Count);
            var expected = new[]
            {
                "160109_Mix1_calcurve_070",
                "160109_Mix1_calcurve_073",
                "160109_Mix1_calcurve_071",
                "160109_Mix1_calcurve_074"
            }.ToList();
            expected.Sort();
            var actual = listNamedPaths.Select(p => p.Key).ToList();
            actual.Sort();
            AssertEx.AreEqualDeep(expected, actual);
            
            // Regex 3
            log.Clear();
            listNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>(dataSourceList);
            pattern = @"REP\d{1}_01";
            commandLine.ApplyFileAndSampleNameRegex(new Regex(pattern), null, ref listNamedPaths);
            Assert.AreEqual(2, listNamedPaths.Count);
            expected = new[]
            {
                "REP01",
                "REP02"
            }.ToList();
            expected.Sort();
            actual = listNamedPaths.Select(p => p.Key).ToList(); // Key is the replicate name; this will be directory name in this case
            actual.Sort();
            AssertEx.AreEqualDeep(expected, actual);
            expected = new[]
            {
                "CE_Vantage_15mTorr_0001_REP1_01" + extRaw,
                "CE_Vantage_15mTorr_0001_REP2_01" + extRaw
            }.ToList();
            expected.Sort();
            actual = listNamedPaths.Select(p => p.Value[0].GetFileName()).ToList(); // Filenames for the replicates
            actual.Sort();
            AssertEx.AreEqualDeep(expected, actual);


            // Apply a sample name regex.  Nothing should match since none of the files
            // in the test directory have sample names.  Only multi-injection .wiff files can have sample names. 
            log.Clear();
            listNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>(dataSourceList);
            commandLine.ApplyFileAndSampleNameRegex(null, new Regex(pattern), ref listNamedPaths);
            Assert.AreEqual(0, listNamedPaths.Count);
            CheckRunCommandOutputContains(
                string.Format(
                    Resources.CommandLine_ApplySampleNameRegex_File___0___does_not_have_a_sample__Cannot_apply_sample_name_pattern__Ignoring_,
                    rawPath), log.ToString());
            CheckRunCommandOutputContains(string.Format(Resources.CommandLine_ApplyFileAndSampleNameRegex_Error__No_files_match_the_sample_name_pattern___0___, pattern), log.ToString());
        }

        [TestMethod]
        public void ConsoleSampleNameRegexImportTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineWiffTest.zip");
            var docPath = TestFilesDir.GetTestPath("wiffcmdtest.sky");
            var outPath = TestFilesDir.GetTestPath("out.sky");
            FileEx.SafeDelete(outPath);
            var rawPath = MsDataFileUri.Parse(TestFilesDir.GetTestPath("051309_digestion.wiff"));
            // Make a copy of the wiff file
            var rawPath2 = MsDataFileUri.Parse(TestFilesDir.GetTestPath(rawPath.GetFileNameWithoutExtension() + "_copy.wiff"));
            File.Copy(rawPath.GetFilePath(), rawPath2.GetFilePath());
            AssertEx.FileExists(rawPath2.GetFilePath());
            
            var sampleNames = ImmutableList.ValueOf(new[] {"blank", "rfp9_after_h_1", "test", "rfp9_before_h_1"});
            var sampleFiles1 = DataSourceUtil.ListSubPaths(rawPath).ToArray();
            var sampleFiles2 = DataSourceUtil.ListSubPaths(rawPath2).ToArray();

            // Test: invalid regex
            var msg = RunCommand("--in=" + docPath,
                "--import-file=" + rawPath.GetFilePath(),
                "--import-samplename-pattern=*",
                "--out=" + outPath);
            CheckRunCommandOutputContains(
                string.Format(
                    Resources.CommandArgs_ParseRegexArgument_Error__Regular_expression___0___for__1__cannot_be_parsed_,
                    "*", "--import-samplename-pattern"), msg);

            // Test: No match found for given sample name regex.  This will also test that the error reporting and 
            // returned exit status are in sync.
            var pattern = "QC.*";
            msg = RunCommand("--in=" + docPath,
                "--import-file=" + rawPath.GetFilePath(),
                "--import-samplename-pattern=" + pattern,
                "--out=" + outPath);
            CheckRunCommandOutputContains(
                string.Format(
                    Resources
                        .CommandLine_ApplyFileAndSampleNameRegex_Error__No_files_match_the_sample_name_pattern___0___,
                    pattern), msg);


            var log = new StringBuilder();
            var commandLine = new CommandLine(new CommandStatusWriter(new StringWriter(log)));
            IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths = DataSourceUtil.GetDataSources(TestFilesDir.FullPath).ToArray();

            // Apply regex filters on file and sample names. There are two files in the folder (051309_digestion.wiff and 051309_digestion_copy.wiff)
            // Samples "blank" and "test" from only one of the files (051309_digestion_copy.wiff) should be selected
            var sampleRegex = "blank|test";
            var fileregex = ".*_copy";
            commandLine.ApplyFileAndSampleNameRegex(new Regex(fileregex), new Regex(sampleRegex), ref listNamedPaths);
            Assert.AreEqual(2, listNamedPaths.Count);
            var expected = new[]
            {
                sampleNames[0],
                sampleNames[2]
            }.ToList();
            expected.Sort();
            var actual = listNamedPaths.Select(p => p.Key).ToList();
            actual.Sort();
            AssertEx.AreEqualDeep(expected, actual);
            expected = new[]
            {
                sampleFiles2[0].ToString(),
                sampleFiles2[2].ToString()
            }.ToList();
            expected.Sort();
            actual = listNamedPaths.Select(p => p.Value[0].ToString()).ToList();
            actual.Sort();
            AssertEx.AreEqualDeep(expected, actual);
            foreach (var msDataFileUri in sampleFiles1)
            {
                // First file, 051309_digestion.wiff, should not have matched the file name regex 
                CheckRunCommandOutputContains(
                    string.Format(
                        Resources.CommandLine_ApplyFileNameRegex_File_name___0___does_not_match_the_pattern___1____Ignoring__2_,
                        msDataFileUri.GetFileName(), fileregex, msDataFileUri), log.ToString());
            }
        }
        
        [TestMethod]
        public void ConsoleImportDirsMakeUniqueReplicateTest()
        {
            bool useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;
            string testZipPath = useRaw
                ? @"TestData\ImportAllCmdLineTest.zip"
                : @"TestData\ImportAllCmdLineTestMzml.zip";
            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

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
            //   -- 160109_Mix1_calcurve_071.raw (Waters .raw directory)|mzML
            //   -- 160109_Mix1_calcurve_074.raw (Waters .raw directory)|mzML
            //   -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
            //   -- bad_file_folder
            //       -- bad_file.raw (Should not be imported. Only in ImportAllCmdLineTest.zip)
            //   -- FullScan.RAW|mzML (should not be imported)
            //   -- FullScan_folder
            //       -- FullScan.RAW|mzML (should not be imported)

            var docPath = TestFilesDir.GetTestPath("test.sky");
            var outPath = TestFilesDir.GetTestPath("out.sky");
            FileEx.SafeDelete(outPath);
            var rawPath = MsDataFileUri.Parse(TestFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML"));

            // Folder 1
            var folder1Path = TestFilesDir.GetTestPath(@"Folder1\Rep1");
            Directory.CreateDirectory(folder1Path);
            Assert.IsTrue(Directory.Exists(folder1Path));
            var rawPath1 = MsDataFileUri.Parse(Path.Combine(folder1Path, rawPath.GetFileName()));
            File.Copy(rawPath.GetFilePath(), rawPath1.GetFilePath());

            // Folder 2
            var folder2Path = TestFilesDir.GetTestPath(@"Folder2\Rep1");
            Directory.CreateDirectory(folder2Path);
            Assert.IsTrue(Directory.Exists(folder2Path));
            var rawPath2 = MsDataFileUri.Parse(Path.Combine(folder2Path, rawPath.GetFileName()));
            File.Copy(rawPath.GetFilePath(), rawPath2.GetFilePath());

            
            // Test: Import all in Folder 1
            RunCommand("--in=" + docPath,
                "--import-all=" + TestFilesDir.GetTestPath("Folder1"),
                "--save");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(1, doc.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.MeasuredResults.ContainsChromatogram("Rep1"));


            // Test: Import all in Folder2
            var msg = RunCommand("--in=" + docPath,
                "--import-all=" + TestFilesDir.GetTestPath("Folder2"),
                "--save");
            doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(2, doc.MeasuredResults.Chromatograms.Count);
            doc.MeasuredResults.TryGetChromatogramSet("Rep1", out var chromatogramSet1, out _);
            Assert.IsNotNull(chromatogramSet1);
            Assert.AreEqual(1, chromatogramSet1.MSDataFilePaths.Count());
            Assert.IsTrue(chromatogramSet1.MSDataFilePaths.Contains(rawPath1));

            doc.MeasuredResults.TryGetChromatogramSet("Rep12", out var chromatogramSet2, out _);
            Assert.IsNotNull(chromatogramSet2);
            Assert.AreEqual(1, chromatogramSet2.MSDataFilePaths.Count());
            Assert.IsTrue(chromatogramSet2.MSDataFilePaths.Contains(rawPath2));
            CheckRunCommandOutputContains(
                string.Format(
                    Resources
                        .CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                    "Rep1", "Rep12"), msg);
        }

        [TestMethod]
        public void ConsoleImportFileSameNameTest()
        {
            var useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;

            var testZipPath = @"TestData\ImportCommandLineSameName.zip";
            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

            // ImportCommandLineSameName.zip
            // Contents:
            //   -- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //   -- CE_Vantage_15mTorr_0001_REP1_01.raw
            //   -- Subdir1
            //        |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //        |-- A
            //            |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //   -- Subdir2
            //        |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //        |-- A
            //            |-- CE_Vantage_15mTorr_0001_REP1_01.mzML

            var docPath = TestFilesDir.GetTestPath(@"test.sky");

            var mzml1 = new MsDataFilePath(TestFilesDir.GetTestPath(@"CE_Vantage_15mTorr_0001_REP1_01.mzML"));
            var rawPath1 = new MsDataFilePath(TestFilesDir.GetTestPath(@"CE_Vantage_15mTorr_0001_REP1_01.raw"));
            var mzxml_subdir1 = new MsDataFilePath(TestFilesDir.GetTestPath(@"Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML"));
            var defaultReplicateName = mzml1.GetFileNameWithoutExtension();


            var outPath = TestFilesDir.GetTestPath("ImportFile.sky");
            FileEx.SafeDelete(outPath);

            // -------------------------------------------------------------------------// 
            // -------------------------- Import a single file ------------------------ //
            // -------------------------------------------------------------------------// 
            // 1. Import the file
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01 -> CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            var msg = RunCommand("--in=" + docPath,
                "--import-file=" + mzml1.FilePath,
                "--out=" + outPath);
            AssertEx.FileExists(outPath, msg);
            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));


            // ------------------------------------------------------------------------------------
            // 2. Import the same file again. It should be ignored.
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01 -> CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-file=" + mzml1.FilePath,
                "--save");doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources
                        .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                    defaultReplicateName, mzml1.FilePath)), msg);


            // ------------------------------------------------------------------------------------
            // 3. Import the same file again with --import-append. It should be ignored.
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01 -> CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-file=" + mzml1.FilePath,
                "--import-append",
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms[0].MSDataFileInfos.Count); // nothing got appended.
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources
                        .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                    defaultReplicateName, mzml1.FilePath)), msg);


            // ------------------------------------------------------------------------------------
            // 4. Import the same file but from a different path. The file will get imported.
            // Since the default replicate name exists in the document, the new replicate name
            // will have a '2' suffix appended - CE_Vantage_15mTorr_0001_REP1_012
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01  -> CE_Vantage_15mTorr_0001_REP1_01.mzML
            // CE_Vantage_15mTorr_0001_REP1_012 -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-file=" + mzxml_subdir1.FilePath,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "2"));
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                    defaultReplicateName, defaultReplicateName + "2")), msg);


            // ------------------------------------------------------------------------------------
            // 5. Import the file again from the second location.  It should be ignored.
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-file=" + mzxml_subdir1.FilePath,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources
                        .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                    defaultReplicateName + "2", mzxml_subdir1.FilePath)), msg);


            // ------------------------------------------------------------------------------------
            // 6. Import the file from the second location with --import-append.  A replicate exists
            // with the default replicate name but it has the file from the first path.
            // The file from the second path will get added to the replicate.
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01  -> CE_Vantage_15mTorr_0001_REP1_01.mzML
            //                                  -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // CE_Vantage_15mTorr_0001_REP1_012 -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-file=" + mzxml_subdir1,
                "--import-append",
                "--save");  
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            doc.Settings.MeasuredResults.TryGetChromatogramSet(defaultReplicateName, out ChromatogramSet chromatogram, out int indexChrom);
            Assert.IsNotNull(chromatogram);
            Assert.IsTrue(chromatogram.MSDataFilePaths.Contains(mzml1));
            Assert.IsTrue(chromatogram.MSDataFilePaths.Contains(mzxml_subdir1));


            // ------------------------------------------------------------------------------------
            // 7. Import the file with --import-replicate-name.  Even though this file has already 
            // been imported into the document it will be imported again since we are given a replicate name.
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01  -> CE_Vantage_15mTorr_0001_REP1_01.mzML
            //                                  -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // CE_Vantage_15mTorr_0001_REP1_012 -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // Replicate01                      -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            var replicateName = "Replicate01";
            msg = RunCommand("--in=" + outPath,
                "--import-file=" + mzml1.FilePath,
                "--import-replicate-name=" + replicateName,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(3, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "2"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(replicateName));

            // ------------------------------------------------------------------------------------
            // 8. Import again with same replicate name. File will be ignored
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-file=" + mzml1.FilePath,
                "--import-replicate-name=" + replicateName,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(3, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources.CommandLine_ImportDataFilesWithAppend_Error__The_replicate__0__already_exists_in_the_given_document_and_the___import_append_option_is_not_specified___The_replicate_will_not_be_added_to_the_document_,
                    replicateName)), msg);

            // ------------------------------------------------------------------------------------
            // 9. Import again with same replicate name and --import-append.  File will not be imported.
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-file=" + mzml1.FilePath,
                "--import-replicate-name=" + replicateName,
                "--import-append",
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(3, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources
                        .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                    replicateName, mzml1.FilePath)), msg);
            Assert.IsTrue(
                msg.Contains(Resources.CommandLine_ImportResults_Error__No_files_left_to_import_), msg);


            if (useRaw)
            {
                // 10. Import the .raw file (same file name as mzml1 but .raw extension. This should be imported into a new replicate.
                // Expected replicates in document after this command:
                // CE_Vantage_15mTorr_0001_REP1_01  -> CE_Vantage_15mTorr_0001_REP1_01.mzML
                //                                  -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // CE_Vantage_15mTorr_0001_REP1_012 -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // Replicate01                      -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // CE_Vantage_15mTorr_0001_REP1_013  -> CE_Vantage_15mTorr_0001_REP1_01.raw
                msg = RunCommand("--in=" + outPath,
                    "--import-file=" + rawPath1,
                    "--save");
                doc = ResultsUtil.DeserializeDocument(outPath);
                Assert.AreEqual(4, doc.Settings.MeasuredResults.Chromatograms.Count);
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "2"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(replicateName));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "3"));
                Assert.IsTrue(
                    msg.Contains(string.Format(
                        Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                        defaultReplicateName, defaultReplicateName + "3")), msg);
            }
        }

        [TestMethod]
        public void ConsoleImportAllFilesSameNameTest()
        {
            var useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;

            var testZipPath = @"TestData\ImportCommandLineSameName.zip";
            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

            // ImportCommandLineSameName.zip
            // Contents:
            //   -- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //   -- CE_Vantage_15mTorr_0001_REP1_01.raw
            //   -- Subdir1
            //        |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //        |-- A
            //            |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //   -- Subdir2
            //        |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //        |-- A
            //            |-- CE_Vantage_15mTorr_0001_REP1_01.mzML

            var docPath = TestFilesDir.GetTestPath(@"test.sky");

            var mzml1 = new MsDataFilePath(TestFilesDir.GetTestPath(@"CE_Vantage_15mTorr_0001_REP1_01.mzML"));
            var defaultReplicateName = mzml1.GetFileNameWithoutExtension();
            var replicateName = "Replicate01";
            var subDir1 = TestFilesDir.GetTestPath("Subdir1");
            var mzxml_subdir1 = new MsDataFilePath(TestFilesDir.GetTestPath(@"Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML"));
            var subDir2 = TestFilesDir.GetTestPath("Subdir2");
            var mzxml_subdir2 = new MsDataFilePath(TestFilesDir.GetTestPath(@"Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML"));

            // -------------------------------------------------------------------------// 
            // -------------------------- Import all files in a directory ------------- //
            // -------------------------- --import-all-files -------------------------- //
            // -------------------------------------------------------------------------// 
            var outPath = TestFilesDir.GetTestPath("ImportFilesInDir.sky");
            FileEx.SafeDelete(outPath);
            // ------------------------------------------------------------------------------------
            // 1. Import Subdir1 that has a single file CE_Vantage_15mTorr_0001_REP1_01.mzML
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01  -> CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            RunCommand("--in=" + docPath,
                "--import-all-files=" + subDir1,
                "--out=" + outPath);
            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));

            // ------------------------------------------------------------------------------------
            // 2. Import files in the directory again.  The file should be ignored.
            // ------------------------------------------------------------------------------------
            var msg = RunCommand("--in=" + outPath,
                "--import-all-files=" + subDir1,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources
                        .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                    defaultReplicateName, mzxml_subdir1.FilePath)), msg);


            // ------------------------------------------------------------------------------------
            // 2. Import the second subdirectory "Subdir2"
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01  -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // CE_Vantage_15mTorr_0001_REP1_012 -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-all-files=" + subDir2,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "2"));
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                    defaultReplicateName, defaultReplicateName + "2")), msg);

            // ------------------------------------------------------------------------------------
            // 3. Import with --import-replicate-name
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01  -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // CE_Vantage_15mTorr_0001_REP1_012 -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // Replicate01                      -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-all-files=" + subDir2,
                "--import-replicate-name=" + replicateName,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(3, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "2"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(replicateName));


            // ------------------------------------------------------------------------------------
            // 4. Import again with --import-replicate-name. Nothing should be added
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-all-files=" + subDir2,
                "--import-replicate-name=" + replicateName,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(3, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources
                        .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                    replicateName, mzxml_subdir2.FilePath)), msg);

            if (useRaw)
            {
                // 5. Import the root test directory containing both a .mzML and a .raw file with the same name.
                // Both files will get imported since the path is different.Two new replicates should get created for
                // CE_Vantage_15mTorr_0001_REP1_01.mzML AND
                // CE_Vantage_15mTorr_0001_REP1_01.raw
                // Expected replicates in document after this command:
                // CE_Vantage_15mTorr_0001_REP1_01  -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // CE_Vantage_15mTorr_0001_REP1_012 -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // Replicate01                      -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // CE_Vantage_15mTorr_0001_REP1_013  -> CE_Vantage_15mTorr_0001_REP1_01.mzML|.raw
                // CE_Vantage_15mTorr_0001_REP1_014  -> CE_Vantage_15mTorr_0001_REP1_01.mzML|.raw
                msg = RunCommand("--in=" + outPath,
                    "--import-all-files=" + TestFilesDir.FullPath,
                    "--save");
                doc = ResultsUtil.DeserializeDocument(outPath);
                Assert.AreEqual(5, doc.Settings.MeasuredResults.Chromatograms.Count);
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "2"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(replicateName));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "3"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "4"));
                Assert.IsTrue(
                    msg.Contains(string.Format(
                        Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                        defaultReplicateName, defaultReplicateName + "3")), msg);
                Assert.IsTrue(
                    msg.Contains(string.Format(
                        Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                        defaultReplicateName, defaultReplicateName + "4")), msg);
            }
        }

        [TestMethod]
        public void ConsoleImportAllSameNameTest()
        {
            var useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;

            var testZipPath = @"TestData\ImportCommandLineSameName.zip";
            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

            // ImportCommandLineSameName.zip
            // Contents:
            //   -- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //   -- CE_Vantage_15mTorr_0001_REP1_01.raw
            //   -- Subdir1
            //        |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //        |-- A
            //            |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //   -- Subdir2
            //        |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //        |-- A
            //            |-- CE_Vantage_15mTorr_0001_REP1_01.mzML

            var docPath = TestFilesDir.GetTestPath(@"test.sky");

            var mzml1 = new MsDataFilePath(TestFilesDir.GetTestPath(@"CE_Vantage_15mTorr_0001_REP1_01.mzML"));
            var defaultReplicateName = mzml1.GetFileNameWithoutExtension();
            var replicateName = "Replicate01";
            var subDir1 = TestFilesDir.GetTestPath("Subdir1");
            var subDir2 = TestFilesDir.GetTestPath("Subdir2");
            var mzxml_subdir1 = new MsDataFilePath(TestFilesDir.GetTestPath(@"Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML"));
            var mzxml_subdir1a = new MsDataFilePath(TestFilesDir.GetTestPath(@"Subdir1\A\CE_Vantage_15mTorr_0001_REP1_01.mzML"));
            var mzxml_subdir2 = new MsDataFilePath(TestFilesDir.GetTestPath(@"Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML"));
            var mzxml_subdir2a = new MsDataFilePath(TestFilesDir.GetTestPath(@"Subdir2\A\CE_Vantage_15mTorr_0001_REP1_01.mzML"));

            // -------------------------------------------------------------------------// 
            // -------------------------- Import all files and sub-directories -------- //
            // -------------------------- --import-all -------------------------------- //
            // -------------------------------------------------------------------------// 
            var outPath = TestFilesDir.GetTestPath("ImportFilesAndSubdirsInDir.sky");
            FileEx.SafeDelete(outPath);
            
            // ------------------------------------------------------------------------------------
            // 1. Import Subdir1. Expect two new replicates.  Files are the same but path is different.
            //    Subdir1
            //        |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //        |-- A
            //            |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01  -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // A                                -> Subdir1\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            RunCommand("--in=" + docPath,
                "--import-all=" + subDir1,
                "--out=" + outPath);
            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("A"));

            // ------------------------------------------------------------------------------------
            // 2. Import again.  Nothing should get imported.
            // ------------------------------------------------------------------------------------
            var msg = RunCommand("--in=" + outPath,
                "--import-all=" + subDir1,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources
                        .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                    defaultReplicateName, mzxml_subdir1.FilePath)), msg);
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources
                        .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                    "A", mzxml_subdir1a.FilePath)), msg);

            // ------------------------------------------------------------------------------------
            // 3. Import Subdir2.  Expect two new replicates.  Files are the same but path is different.
            //    Subdir2
            //        |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            //        |-- A
            //            |-- CE_Vantage_15mTorr_0001_REP1_01.mzML
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01   -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // A                                 -> Subdir1\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // CE_Vantage_15mTorr_0001_REP1_012  -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // A2                                -> Subdir2\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            msg = RunCommand("--in=" + outPath,
                "--import-all=" + subDir2,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(4, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "2"));
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("A2"));
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                    defaultReplicateName, defaultReplicateName + "2")), msg);
            Assert.IsTrue(
                msg.Contains(string.Format(
                    Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                    "A", "A2")), msg);

            // ------------------------------------------------------------------------------------
            // 4. Import Subdir2 with a replicate name.  All files in this folder and subfolders
            //    should get appended to the "Replicate01" replicate.
            // Expected replicates in document after this command:
            // CE_Vantage_15mTorr_0001_REP1_01   -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // A                                 -> Subdir1\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // CE_Vantage_15mTorr_0001_REP1_012  -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // A2                                -> Subdir2\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // Replicate01                       -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
            //                                   -> Subdir2\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
            // ------------------------------------------------------------------------------------
            RunCommand("--in=" + outPath,
                "--import-all=" + subDir2,
                "--import-replicate-name=" + replicateName,
                "--save");
            doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(5, doc.Settings.MeasuredResults.Chromatograms.Count);
            doc.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out var chromatogram, out _);
            Assert.IsNotNull(chromatogram);
            Assert.IsTrue(chromatogram.MSDataFilePaths.Contains(mzxml_subdir2));
            Assert.IsTrue(chromatogram.MSDataFilePaths.Contains(mzxml_subdir2a));

            if (useRaw)
            {
                // ------------------------------------------------------------------------------------
                // 5. Import the root test directory containing both a .mzML and a .raw file with the same name
                // Both files will get imported since the path is different. Two new replicates should get created for
                // CE_Vantage_15mTorr_0001_REP1_01.mzML AND
                // CE_Vantage_15mTorr_0001_REP1_01.raw
                // The .mzML files in Subdir1 and Subdir2 are
                // already imported and should be ignored. 
                // Expected replicates in document after this command:
                // CE_Vantage_15mTorr_0001_REP1_01   -> Subdir1\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // A                                 -> Subdir1\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // CE_Vantage_15mTorr_0001_REP1_012  -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // A2                                -> Subdir2\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // Replicate01                       -> Subdir2\CE_Vantage_15mTorr_0001_REP1_01.mzML
                //                                   -> Subdir2\A\CE_Vantage_15mTorr_0001_REP1_01.mzML
                // CE_Vantage_15mTorr_0001_REP1_013  -> CE_Vantage_15mTorr_0001_REP1_01.mzML|.raw
                // CE_Vantage_15mTorr_0001_REP1_014  -> CE_Vantage_15mTorr_0001_REP1_01.mzML|.raw
                // ------------------------------------------------------------------------------------
                msg = RunCommand("--in=" + outPath,
                    "--import-all=" + TestFilesDir.FullPath,
                    "--save");
                doc = ResultsUtil.DeserializeDocument(outPath);
                Assert.AreEqual(7, doc.Settings.MeasuredResults.Chromatograms.Count);
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "3"));
                Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram(defaultReplicateName + "4"));
                Assert.IsTrue(
                    msg.Contains(string.Format(
                        Resources
                            .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                        defaultReplicateName, mzxml_subdir1.FilePath)), msg);
                Assert.IsTrue(
                    msg.Contains(string.Format(
                        Resources
                            .CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___,
                        defaultReplicateName + "2", mzxml_subdir2.FilePath)), msg);
                Assert.IsTrue(
                    msg.Contains(string.Format(
                        Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                        defaultReplicateName, defaultReplicateName + "3")), msg);
                Assert.IsTrue(
                    msg.Contains(string.Format(
                        Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                        defaultReplicateName, defaultReplicateName + "4")), msg);
            }
        }


        // CONSIDER: Uncomment this test when it can clean up before/after itself on Panorama
        // [TestMethod]
        public void ConsolePanoramaImportTest()
        {
            bool useRaw = ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportWatersRaw;
            string testZipPath = useRaw
                                     ? @"TestData\ImportAllCmdLineTest.zip"
                                     : @"TestData\ImportAllCmdLineTestMzml.zip";
            string extRaw = useRaw
                                ? ".raw"
                                : ".mzML";

            TestFilesDir = new TestFilesDir(TestContext, testZipPath);

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

            var docPath = TestFilesDir.GetTestPath("test.sky");

            // Test: Import a file to an empty document and upload to the panorama server
            var rawPath = new MsDataFilePath(TestFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_01" + extRaw));
            var msg = RunCommand("--in=" + docPath,
                             "--import-file=" + rawPath.FilePath,
                //"--import-on-or-after=1/1/2014",
                             "--save",
                             "--panorama-server=https://panoramaweb.org",
                             "--panorama-folder=/MacCoss/SkylineUploadTest/",
                             "--panorama-username=skylinetest@proteinms.net",
                             "--panorama-password=skylinetest");

            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsFalse(msg.Contains("Skipping Panorama import."), msg);


            // Test: Import a second file and upload to the panorama server
            rawPath = new MsDataFilePath(TestFilesDir.GetTestPath(@"REP01\CE_Vantage_15mTorr_0001_REP1_02" + extRaw));
            msg = RunCommand("--in=" + docPath,
                             "--import-file=" + rawPath.FilePath,
                             "--save",
                             "--panorama-server=https://panoramaweb.org",
                             "--panorama-folder=/MacCoss/SkylineUploadTest/",
                             "--panorama-username=skylinetest@proteinms.net",
                             "--panorama-password=skylinetest");


            doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsFalse(msg.Contains("Skipping Panorama import."), msg);
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
                TestFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
                {
                    // Test bad input
                    const string badFileName = "BadFilePath";
                    AssertEx.FileNotExists(badFileName);
                    const string command = "--tool-add-zip=" + badFileName;
                    string output = RunCommand(command);
                    Assert.IsTrue(output.Contains(Resources.CommandLine_ImportToolsFromZip_Error__the_file_specified_with_the___tool_add_zip_command_does_not_exist__Please_verify_the_file_location_and_try_again_));
                }
                {
                    string notZip = TestFilesDir.GetTestPath("Broken_file.sky");
                    AssertEx.FileExists(notZip);
                    string command = "--tool-add-zip=" + notZip;
                    string output = RunCommand(command);
                    Assert.IsTrue(output.Contains(Resources.CommandLine_ImportToolsFromZip_Error__the_file_specified_with_the___tool_add_zip_command_is_not_a__zip_file__Please_specify_a_valid__zip_file_));
                }
                {
                    var uniqueReportZip = TestFilesDir.GetTestPath("UniqueReport.zip");
                    AssertEx.FileExists(uniqueReportZip);
                    string command = "--tool-add-zip=" + uniqueReportZip;
                    string output = RunCommand(command);

                    Assert.IsTrue(Settings.Default.ToolList.Count == 1);
                    ToolDescription newTool = Settings.Default.ToolList.Last();
                    Assert.AreEqual("HelloWorld", newTool.Title);
                    Assert.IsTrue(newTool.OutputToImmediateWindow);
                    Assert.AreEqual("UniqueReport", newTool.ReportTitle);
                    string path = newTool.ToolDirPath;
                    AssertEx.FileExists(Path.Combine(path, "HelloWorld.exe"));
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
                    AssertEx.FileExists(Path.Combine(path1, "HelloWorld.exe"));
                    //Cleanup.
                    Settings.Default.ToolList.Clear();
                    DirectoryEx.SafeDelete(ToolDescriptionHelpers.GetToolsDirectory());
                    Settings.Default.PersistedViews.RemoveView(PersistedViews.ExternalToolsGroup.Id, "UniqueReport");
                    Settings.Default.PersistedViews.RemoveView(PersistedViews.ExternalToolsGroup.Id, "UniqueReport1");
                }
                {
                    //Test working with packages and ProgramPath Macro.
                    var testCommandLine = TestFilesDir.GetTestPath("TestCommandLine.zip");
                    AssertEx.FileExists(testCommandLine);
                    string command = "--tool-add-zip=" + testCommandLine;
                    string output = RunCommand(command);
                    StringAssert.Contains(output, Resources.AddZipToolHelper_InstallProgram_Error__Package_installation_not_handled_in_SkylineRunner___If_you_have_already_handled_package_installation_use_the___tool_ignore_required_packages_flag);
                    string output1 = RunCommand(command, "--tool-ignore-required-packages");
                    StringAssert.Contains(output1, string.Format(
                        Resources.AddZipToolHelper_FindProgramPath_A_tool_requires_Program__0__Version__1__and_it_is_not_specified_with_the___tool_program_macro_and___tool_program_path_commands__Tool_Installation_Canceled_, 
                        "Bogus",
                        "2.15.2"));

                    string path = TestFilesDir.GetTestPath("NumberWriter.exe");
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
                    var testCommandLine = TestFilesDir.GetTestPath("TestAnnotations.zip");
                    AssertEx.FileExists(testCommandLine);
                    string command = "--tool-add-zip=" + testCommandLine;
                    string output = RunCommand(command);
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "AnnotationTest\\Tool1")));
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "AnnotationTest\\Tool2")));
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "AnnotationTest\\Tool3")));
                    Assert.IsTrue(output.Contains(string.Format(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, "AnnotationTest\\Tool4")));
                }
                {
                    var conflictingAnnotations = TestFilesDir.GetTestPath("ConflictAnnotations.zip");
                    AssertEx.FileExists(conflictingAnnotations);
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

        [TestMethod]
        public void ConsoleAddSkyrTest()
        {
            Settings.Default.PersistedViews.ResetDefaults();
            var existingReports = ReportSharing.GetExistingReports();
            int initialNumber = existingReports.Count;
            const string reportName = "TextREportexam";
            var viewName = new ViewName(PersistedViews.MainGroup.Id, "TextREportexam");
            // Assumes the title TextREportexam is a unique title. 
            Assert.IsFalse(existingReports.Keys.Contains(v => Equals(reportName, v.Name)));

            // Add test.skyr which only has one report type named TextREportexam
            TestFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            var skyrFile = TestFilesDir.GetTestPath("test.skyr");
            var overwriteFile = TestFilesDir.GetTestPath("overwrite.skyr");
            File.WriteAllText(overwriteFile, File.ReadAllText(skyrFile).Replace(">Description<", ">Name<"));
            string output = RunCommand("--report-add=" + skyrFile);
            existingReports = ReportSharing.GetExistingReports();
            Assert.AreEqual(initialNumber+1, existingReports.Count);
            Assert.IsTrue(existingReports.ContainsKey(viewName));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportSkyr_Success__Imported_Reports_from__0_, Path.GetFileName(skyrFile)));
            var skyrAdded = existingReports[viewName].ViewSpecLayout;
            Assert.IsNotNull(skyrAdded);

            // Attempt to add a skyr file that would change the report
            string output2 = RunCommand("--report-add=" + overwriteFile);
            AssertEx.Contains(output2, Resources.ImportSkyrHelper_ResolveImportConflicts_Use_command);
            existingReports = ReportSharing.GetExistingReports();
            Assert.AreEqual(skyrAdded, existingReports[viewName].ViewSpecLayout);

            // Specify skip
            string output4 = RunCommand("--report-add=" + overwriteFile,
                "--report-conflict-resolution=skip");
            AssertEx.Contains(output4, Resources.ImportSkyrHelper_ResolveImportConflicts_Resolving_conflicts_by_skipping_);
            existingReports = ReportSharing.GetExistingReports();
            Assert.AreEqual(skyrAdded, existingReports[viewName].ViewSpecLayout);

            // Specify overwrite
            string output3 = RunCommand("--report-add=" + overwriteFile,
                "--report-conflict-resolution=overwrite");
            AssertEx.Contains(output3, Resources.ImportSkyrHelper_ResolveImportConflicts_Resolving_conflicts_by_overwriting_);
            var existingOverwriteReports = ReportSharing.GetExistingReports();
            Assert.AreNotEqual(skyrAdded, existingOverwriteReports[viewName].ViewSpecLayout);
            Settings.Default.PersistedViews.ResetDefaults();
        }

        [TestMethod]
        public void ConsoleRunCommandsTest()
        {
            Settings.Default.ToolList.ResetDefaults();
            int toolListCount = Settings.Default.ToolList.Count;
            TestFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);
            var commandsToRun = TestFilesDir.GetTestPath("ToolList2.txt");
            string output = RunCommand(CommandArgs.ARG_BATCH.GetArgumentTextWithValue(commandsToRun));
            const string toolCommand = @"C:\Windows\Notepad.exe";
            const string toolArgs = "$(DocumentDir)";
            const string toolDir = @"C:\";
            ValidateToolAdded(output, "NeWtOOl", toolCommand, toolArgs, toolDir);
            ValidateToolAdded(output, "iHope", toolCommand);
            ValidateToolAdded(output, "thisWorks");
            ValidateToolAdded(output, "FirstTry");
            Assert.AreEqual(toolListCount + 4, Settings.Default.ToolList.Count);

            // run the same command again. this time each should be skipped.
            output = RunCommand(CommandArgs.ARG_BATCH.GetArgumentTextWithValue(commandsToRun));
            ValidateToolSkipped(output, "NeWtOOl", toolCommand, toolArgs, toolDir);
            ValidateToolSkipped(output, "iHope", toolCommand);
            ValidateToolSkipped(output, "thisWorks");
            ValidateToolSkipped(output, "FirstTry");
            // the number of tools is unchanged.
            Assert.AreEqual(toolListCount + 4, Settings.Default.ToolList.Count);
        }

        private static void ValidateToolAdded(string output, string toolName,
            string toolCommand = null, string toolArgs = null, string toolDir = null)
        {
            AssertEx.Contains(output,
                string.Format(Resources.CommandLine_ImportTool__0__was_added_to_the_Tools_Menu_, toolName));
            CheckToolExists(toolName, toolCommand, toolArgs, toolDir);
        }

        private static void ValidateToolSkipped(string output, string toolName,
            string toolCommand = null, string toolArgs = null, string toolDir = null)
        {
            AssertEx.Contains(output,
                string.Format(Resources.CommandLine_ImportTool_Warning__skipping_tool__0__due_to_a_name_conflict_, toolName));
            CheckToolExists(toolName, toolCommand, toolArgs, toolDir);
        }

        private static void CheckToolExists(string toolName, string toolCommand, string toolArgs, string toolDir)
        {
            Assert.IsTrue(Settings.Default.ToolList.Any(t => t.Title == toolName &&
                                                             (toolCommand == null || t.Command == toolCommand) &&
                                                             (toolArgs == null || t.Arguments == toolArgs) &&
                                                             (toolDir == null || t.InitialDirectory == toolDir)));
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
        public void CommandLineUsageTest()
        {
            CheckUsageOutput(RunCommand("--help"));
            CheckUsageOutput(RunCommand(string.Empty));
        }

        private static void CheckUsageOutput(string output)
        {
            foreach (CommandArgs.ArgumentGroup group in CommandArgs.UsageBlocks.Where(b => b is CommandArgs.ArgumentGroup))
            {
                if (group.IncludeInUsage)
                {
                    AssertEx.Contains(output, group.Title);
                    foreach (var arg in group.Args.Where(a => !a.InternalUse))
                        AssertEx.Contains(output, arg.ArgumentText);
                }
            }
        }

        [TestMethod]
        public void CommandLineUsageDescriptionsTest()
        {
            // All arguments that will appear in the usage text must have a description
            foreach (var arg in CommandArgs.UsageArguments)
            {
                Assert.IsFalse(string.IsNullOrEmpty(arg.Description),
                    string.Format("The argument {0} is missing a description. Add a non-empty string with the name {1} to CommandArgUsage.resx",
                        arg.ArgumentText, "_" + arg.Name.Replace('-', '_')));
            }
        }

        [TestMethod]
        public void ConsolePanoramaArgsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");

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
            var client = new TestPanoramaClient() { MyServerState = ServerStateEnum.unknown, ServerUri = serverUri };
            helper.ValidateServer(client);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(PanoramaClient.Properties.Resources.ServerState_GetErrorMessage_Unable_to_connect_to_the_server__0__,
                            serverUri.AbsoluteUri)));
            TestOutputHasErrorLine(buffer.ToString());
            buffer.Clear();


            // Error: Not a Panorama Server
            serverUri = PanoramaUtil.ServerNameToUri("www.google.com");
            client = new TestPanoramaClient() {MyUserState = UserStateEnum.unknown, ServerUri = serverUri};
            helper.ValidateServer(client);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(PanoramaClient.Properties.Resources.UserState_GetErrorMessage_There_was_an_error_authenticating_user_credentials_on_the_server__0__,
                            serverUri.AbsoluteUri)));
            TestOutputHasErrorLine(buffer.ToString());
            buffer.Clear();


            // Error: Invalid user
            serverUri = PanoramaUtil.ServerNameToUri(PanoramaUtil.PANORAMA_WEB);
            client = new TestPanoramaClient() { MyUserState = UserStateEnum.nonvalid, ServerUri = serverUri, Username = "invalid", Password = "user"};
            helper.ValidateServer(client);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        PanoramaClient.Properties.Resources
                            .UserState_GetErrorMessage_The_username_and_password_could_not_be_authenticated_with_the_panorama_server_));
            TestOutputHasErrorLine(buffer.ToString());
            buffer.Clear();


            // Error: unknown exception
            client = new TestPanoramaClientThrowsException();
            helper.ValidateServer(client);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(Resources.PanoramaHelper_ValidateServer_Exception_, "GetServerState threw an exception")));
            TestOutputHasErrorLine(buffer.ToString());
            buffer.Clear();

            
            // Error: folder does not exist
            client = new TestPanoramaClient() { MyFolderState = FolderState.notfound, ServerUri = serverUri, Username = "user", Password = "password"};
            helper.ValidateServer(client);
            var folder = "folder/not/found";
            helper.ValidateFolder(client, folder);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(
                            PanoramaClient.Properties.Resources.PanoramaUtil_VerifyFolder_Folder__0__does_not_exist_on_the_Panorama_server__1_,
                            folder, client.ServerUri)));
            TestOutputHasErrorLine(buffer.ToString());
            buffer.Clear();


            // Error: no permissions on folder
            client = new TestPanoramaClient() { MyFolderState = FolderState.nopermission, ServerUri = serverUri };
            folder = "no/permissions";
            helper.ValidateFolder(client, folder);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(
                        string.Format(
                            PanoramaClient.Properties.Resources.PanoramaUtil_VerifyFolder_User__0__does_not_have_permissions_to_upload_to_the_Panorama_folder__1_,
                            client.Username, folder)));
            TestOutputHasErrorLine(buffer.ToString());
            buffer.Clear();


            // Error: not a Panorama folder
            client = new TestPanoramaClient() { MyFolderState = FolderState.notpanorama, ServerUri = serverUri };
            folder = "not/panorama";
            helper.ValidateFolder(client, folder);
            Assert.IsTrue(
                buffer.ToString()
                    .Contains(string.Format(PanoramaClient.Properties.Resources.PanoramaUtil_VerifyFolder__0__is_not_a_Panorama_folder,
                        folder)));
            TestOutputHasErrorLine(buffer.ToString());
        }

        [TestMethod]
        public void SkylineRunnerErrorDetectionTest()
        {
            TestSkylineRunnerErrorDetection(null);
            TestSkylineRunnerErrorDetection(new CultureInfo("ja"));
            TestSkylineRunnerErrorDetection(new CultureInfo("zh-CHS"));
        }

        private void TestSkylineRunnerErrorDetection(CultureInfo ci)
        {
            TestDetectError(false, false, ci); // no timestamp or memstamp
            TestDetectError(true, false, ci);  // only timestamp
            TestDetectError(false, true, ci);  // only memstamp
            TestDetectError(true, true, ci);   // both timestamp and memstamp
        }

        /// <summary>
        /// Tests that "IsErrorLine" works when the commandline is invoked in a particular culture.
        /// Note that this code uses LocalizationHelper.CallWithCulture instead of the "--culture" commandline
        /// argument because the latter does not set the culture back to its original value.
        /// </summary>
        private void TestDetectError(bool timestamp, bool memstamp, CultureInfo cultureInfo)
        {
            Func<string> testFunc = () =>
            {
                // --timestamp, --memstamp and arguments have to be before the --in argument
                var command =
                    $"{(timestamp ? "--timestamp" : "")} " +
                    $"{(memstamp ? "--memstamp" : "")} " +
                    "--in";
                var argsArray = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return RunCommand(argsArray);
            };
            string output = cultureInfo == null
                ? testFunc()
                : LocalizationHelper.CallWithCulture(cultureInfo, testFunc);
            var errorLine = TestOutputHasErrorLine(output);

            // The error should be about the missing value for the --in argument
            var resourceErrString = cultureInfo == null
                ? Resources.ValueMissingException_ValueMissingException_ // Resource string for the culture that the test is running under
                : Resources.ResourceManager.GetString(@"ValueMissingException_ValueMissingException_", cultureInfo);

            Assert.IsNotNull(resourceErrString, "Expected to find a resources string for culture '{0}'.",
                (cultureInfo ?? CultureInfo.CurrentUICulture).Name);
            Assert.IsTrue(errorLine.Contains(string.Format(resourceErrString, "--in")));
        }

        private string TestOutputHasErrorLine(string output)
        {
            var outputLines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // The IsErrorLine method from ErrorChecker.cs in the SkylineRunner project should detect an error
            var errorLine = outputLines.FirstOrDefault(ErrorChecker.IsErrorLine);
            Assert.IsFalse(string.IsNullOrEmpty(errorLine),
                string.Format("Expected to find an error line in output: {0}", output));
            return errorLine;
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

        private class TestPanoramaClient : BaseTestPanoramaClient
        {
            public ServerStateEnum MyServerState { get; set; }
            public UserStateEnum MyUserState { get; set; }
            public FolderState MyFolderState { get; set; }

            public TestPanoramaClient()
            {
                MyServerState = ServerStateEnum.available;
                MyUserState = UserStateEnum.valid;
                MyFolderState = FolderState.valid;

                ServerUri = new Uri("https://panoramaweb.org");
                Username = "myuser";
                Password = "mypassword";
            }

            public override PanoramaServer ValidateServer()
            {
                if (ServerStateEnum.available != MyServerState)
                {
                    throw new PanoramaServerException(new ErrorMessageBuilder(MyServerState.Error(ServerUri)).ErrorDetail("Invalid server state").ToString());
                }
                if (UserStateEnum.valid != MyUserState)
                {
                    throw new PanoramaServerException(new ErrorMessageBuilder(MyUserState.Error(ServerUri)).ErrorDetail("Invalid user state").ToString());
                }

                return new PanoramaServer(ServerUri, Username, Password);
            }

            public override void ValidateFolder(string folderPath, PermissionSet permissionSet, bool checkTargetedMs = true)
            {
                if (MyFolderState != FolderState.valid)
                {
                    throw new PanoramaServerException(MyFolderState.Error(ServerUri, folderPath, Username));
                }
            }
        }

        private class TestPanoramaClientThrowsException : TestPanoramaClient
        {
            public override PanoramaServer ValidateServer()
            {
                throw new Exception("GetServerState threw an exception");
            }    
        }

    }
}