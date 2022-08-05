/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class CommandLineImportTest : AbstractUnitTestEx
    {
        private const string ZIP_FILE = @"TestData\CommandLineImportTest.zip";

        [TestMethod]
        public void ConsoleImportPeptideSearchTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var docPath = testFilesDir.GetTestPath("blank.sky");
            var outPath = testFilesDir.GetTestPath("import-search.sky");
            var searchFilePath = testFilesDir.GetTestPath("CAexample.pep.xml");
            var fastaPath = testFilesDir.GetTestPath("bov-5-prot.fasta");

            // with mods and invalid cutoff score
            const double badCutoff = 1.1;
            var args = new[]
            {
                "--in=" + docPath,
                "--out=" + outPath,
                "--import-search-file=" + searchFilePath,
                "--import-search-cutoff-score=" + badCutoff,
                "--import-search-add-mods",
                "--import-fasta=" + fastaPath
            };
            var output = RunCommand(args);

            AssertEx.Contains(output, new CommandArgs.ValueOutOfRangeDoubleException(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF, badCutoff, 0, 1).Message);

            args[3] = "--import-search-cutoff-score=" + Settings.Default.LibraryResultCutOff;
            output = RunCommand(args);

            AssertEx.Contains(output, TextUtil.LineSeparate(Resources.CommandLine_ImportSearch_Creating_spectral_library_from_files_,
                Path.GetFileName(searchFilePath)));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportSearch_Adding__0__modifications_, 2));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____,
                Path.GetFileName(fastaPath)));

            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("CAexample"));
            Assert.AreEqual(5, doc.PeptideGroupCount);
            Assert.AreEqual(26, doc.PeptideCount);
            Assert.AreEqual(26, doc.MoleculeTransitionGroupCountIgnoringSpecialTestNodes);
            Assert.AreEqual(78, doc.MoleculeTransitionCountIgnoringSpecialTestNodes);

            // without mods
            var outPath2 = testFilesDir.GetTestPath("import-search2.sky");
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath2,
                "--import-search-file=" + searchFilePath,
                "--import-fasta=" + fastaPath);

            AssertEx.Contains(output, TextUtil.LineSeparate(Resources.CommandLine_ImportSearch_Creating_spectral_library_from_files_,
                Path.GetFileName(searchFilePath)));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____,
                Path.GetFileName(fastaPath)));

            doc = ResultsUtil.DeserializeDocument(outPath2);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("CAexample"));
            Assert.AreEqual(5, doc.PeptideGroupCount);
            Assert.AreEqual(23, doc.PeptideCount);
            Assert.AreEqual(23, doc.MoleculeTransitionGroupCountIgnoringSpecialTestNodes);
            Assert.AreEqual(69, doc.MoleculeTransitionCountIgnoringSpecialTestNodes);

            // test setting cutoff and accepting mods when not importing a search
            output = RunCommand(
                "--import-search-cutoff-score=" + 0.99,
                "--import-search-add-mods");

            AssertEx.Contains(output, CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_FILE));
            AssertEx.Contains(output, CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_MODS, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_FILE));


            // MaxQuant embedding error
            searchFilePath = testFilesDir.GetTestPath("yeast-wiff-msms.txt");
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath2,
                "--import-search-file=" + searchFilePath,
                "--import-fasta=" + fastaPath);

            // only check the error message up to and including "In any of the following directories:"
            string externalSpectrumFileErrorPrefix = System.Text.RegularExpressions.Regex.Replace(Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFileError, "\\{2\\}.*", "{2}",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            AssertEx.Contains(output, TextUtil.LineSeparate(Resources.CommandLine_ImportSearch_Creating_spectral_library_from_files_,
                Path.GetFileName(searchFilePath)));
            AssertEx.Contains(output, string.Format(externalSpectrumFileErrorPrefix, searchFilePath, "wine yeast sampleA_2", "", BiblioSpecLiteBuilder.BiblioSpecSupportedFileExtensions));
            AssertEx.Contains(output,Resources.CommandLine_ShowLibraryMissingExternalSpectraError_Description);

            output = RunCommand("--in=" + docPath,
                "--out=" + outPath2,
                "--import-search-file=" + searchFilePath,
                "--import-fasta=" + fastaPath,
                "--import-search-prefer-embedded-spectra");

            AssertEx.Contains(output, TextUtil.LineSeparate(Resources.CommandLine_ImportSearch_Creating_spectral_library_from_files_,
                Path.GetFileName(searchFilePath)));
            Assert.IsTrue(!output.Contains(string.Format(externalSpectrumFileErrorPrefix, searchFilePath, "wine yeast sampleA_2", "", BiblioSpecLiteBuilder.BiblioSpecSupportedFileExtensions)));
            Assert.IsTrue(!output.Contains(Resources.CommandLine_ShowLibraryMissingExternalSpectraError_Description));

            // iRTs
            File.Copy(testFilesDir.GetTestPath("cirts.mqpar.xml"), testFilesDir.GetTestPath("mqpar.xml"), true);
            searchFilePath = testFilesDir.GetTestPath("cirts.msms.txt");
            // test setting num cirts and recalibrate when no irts
            output = RunCommand("--in=" + docPath,
                "--import-search-file=" + searchFilePath,
                "--import-search-num-cirts=10",
                "--import-search-recalibrate-irts");
            AssertEx.Contains(output,
                CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_NUM_CIRTS, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_IRTS),
                CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_RECALIBRATE_IRTS, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_IRTS));
            // test cirt without num cirts set
            output = RunCommand("--in=" + docPath,
                "--import-search-file=" + searchFilePath,
                "--import-search-irts=CiRT (iRT-C18)");
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportSearchInternal_Error___0__must_be_set_when_using_CiRT_peptides_, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_NUM_CIRTS.Name));
            // test with irts
            output = RunCommand("--in=" + docPath,
                "--import-search-file=" + searchFilePath,
                "--import-search-irts=CiRT (iRT-C18)",
                "--import-search-num-cirts=10");
            var libIrts = IrtDb.GetIrtDb(testFilesDir.GetTestPath("blank.blib"), null).StandardPeptides.ToArray();
            AssertEx.AreEqual(10, libIrts.Length);
            foreach (var libIrt in libIrts)
                AssertEx.IsTrue(IrtStandard.CIRT.Contains(libIrt));
        }

        [TestMethod]
        public void ConsoleImportSmallMoleculesTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var docPath = testFilesDir.GetTestPath("blank.sky");
            var smallmolPath = testFilesDir.GetTestPath("smallmolecules.txt");
            var outPath = testFilesDir.GetTestPath("import-smallmol.sky");
            var output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-transition-list=" + smallmolPath);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Importing_transiton_list__0____,
                Path.GetFileName(smallmolPath))); 
            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(2, doc.MoleculeGroupCount);
            Assert.AreEqual(4, doc.MoleculeCount);

            // Test how we categorize transition lists as small molecule or peptide in command line context
            // when those transition lists do not contain defining features such as a peptide sequence column or small molecule headers
            // Load a proteomic document
            docPath = testFilesDir.GetTestPath("proteomics.sky");
            var ambiguousListPath = testFilesDir.GetTestPath("DrugX.txt");

            // Import a transition list that cannot be read either way
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-transition-list=" + ambiguousListPath);

            // We should rely on the type of the document and categorize it as a proteomics transition list
            // so verify that we get a proteomics error message
            AssertEx.Contains(output, Resources.MassListImporter_Import_Failed_to_find_peptide_column);

            // Load a small molecule document
            docPath = testFilesDir.GetTestPath("smallmolecule.sky");

            // Import the same transition list that cannot be read either way
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-transition-list=" + ambiguousListPath);

            // We should rely on the type of the document and categorize it as a small molecule transition list
            // so verify that we get a small molecule error message
            var smallMoleculeErrorMessage = string.Format(Resources.SmallMoleculeTransitionListReader_SmallMoleculeTransitionListReader_,
                TextUtil.LineSeparate(new[] { "DrugX","Drug","light","283.04","1","129.96","1","26","16","2.7", string.Empty
                }),
                TextUtil.LineSeparate(SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms.Keys));
            AssertEx.Contains(output, smallMoleculeErrorMessage);
        }
    }
}
