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
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var docPath = TestFilesDir.GetTestPath("blank.sky");
            var outPath = TestFilesDir.GetTestPath("import-search.sky");
            var searchFilePath = TestFilesDir.GetTestPath("CAexample.pep.xml");
            var fastaPath = TestFilesDir.GetTestPath("bov-5-prot.fasta");

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
            Assert.AreEqual(26, doc.PeptideTransitionGroupCount);
            Assert.AreEqual(78, doc.PeptideTransitionCount);

            // without mods
            var outPath2 = TestFilesDir.GetTestPath("import-search2.sky");
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
            Assert.AreEqual(23, doc.PeptideTransitionGroupCount);
            Assert.AreEqual(69, doc.PeptideTransitionCount);

            // test setting cutoff and accepting mods when not importing a search
            output = RunCommand(
                "--import-search-cutoff-score=" + 0.99,
                "--import-search-add-mods");

            AssertEx.Contains(output, CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_FILE));
            AssertEx.Contains(output, CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_MODS, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_FILE));


            // MaxQuant embedding error
            searchFilePath = TestFilesDir.GetTestPath("yeast-wiff-msms.txt");
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
            File.Copy(TestFilesDir.GetTestPath("cirts.mqpar.xml"), TestFilesDir.GetTestPath("mqpar.xml"), true);
            searchFilePath = TestFilesDir.GetTestPath("cirts.msms.txt");
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
            var libIrts = IrtDb.GetIrtDb(TestFilesDir.GetTestPath("blank.blib"), null).StandardPeptides.ToArray();
            AssertEx.AreEqual(10, libIrts.Length);
            foreach (var libIrt in libIrts)
                AssertEx.IsTrue(IrtStandard.CIRT.Contains(libIrt));
        }

        [TestMethod]
        public void ConsoleImportSmallMoleculesTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var docPath = TestFilesDir.GetTestPath("blank.sky");
            var smallmolPath = TestFilesDir.GetTestPath("smallmolecules.txt");
            var outPath = TestFilesDir.GetTestPath("import-smallmol.sky");
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
            docPath = TestFilesDir.GetTestPath("proteomics.sky");
            var ambiguousListPath = TestFilesDir.GetTestPath("DrugX.txt");

            // Import a transition list that cannot be read either way
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-transition-list=" + ambiguousListPath);

            // We should rely on the type of the document and categorize it as a proteomics transition list
            // so verify that we get a proteomics error message
            AssertEx.Contains(output, Resources.MassListImporter_Import_Failed_to_find_peptide_column);

            // Load a small molecule document
            docPath = TestFilesDir.GetTestPath("smallmolecule.sky");

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

        [TestMethod]
        public void ConsoleImportPeptideListTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var docPath = TestFilesDir.GetTestPath("blank.sky");
            var outPath = TestFilesDir.GetTestPath("import-fasta-file.sky");
            var fastaPath = TestFilesDir.GetTestPath("bov-5-prot.fasta");
            var listPath = TestFilesDir.GetTestPath("peplist.txt");
            var listPath3 = TestFilesDir.GetTestPath("peplist3.txt");
            var listsPath = TestFilesDir.GetTestPath("peplists.txt");

            // Create initial document for comparisons by importing a FASTA file
            var output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-fasta=" + fastaPath);
            var docFasta = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(5, docFasta.PeptideGroupCount);
            Assert.AreEqual(40, docFasta.PeptideCount);

            // Test simply importing the modified sequences to create charge precursors
            // in a single peptide list with a default name
            outPath = TestFilesDir.GetTestPath("import-peplist.sky");
            File.WriteAllLines(listPath, docFasta.Peptides.Select(p => p.ModifiedSequence));
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-pep-list=" + listPath);
            var listName = docFasta.GetPeptideGroupId(true);    // Strictly speaking this should be using the doc for docPath
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_list__0__from_file__1____,
                listName, Path.GetFileName(listPath)));
            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc.PeptideGroupCount);
            Assert.AreEqual(listName, doc.PeptideGroups.First().Name);
            Assert.AreEqual(40, doc.PeptideCount);
            foreach (var nodePep in doc.Peptides)
            {
                Assert.AreEqual(1, nodePep.Children.Count);
                Assert.AreEqual(2, nodePep.TransitionGroups.First().PrecursorCharge);
            }

            // Add charge 3 specifiers to all the peptide sequences to create charge 3 precursors
            outPath = TestFilesDir.GetTestPath("import-peplist3.sky");
            const string listName3 = "Peptide-precursors-charge3";
            File.WriteAllLines(listPath3, doc.Peptides.Select(p => p.ModifiedSequence + "+++"));
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-pep-list-name=" + listName3,
                "--import-pep-list=" + listPath3);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_list__0__from_file__1____,
                listName3, Path.GetFileName(listPath3)));
            var doc3 = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc3.PeptideGroupCount);
            Assert.AreEqual(listName3, doc3.PeptideGroups.First().Name);
            Assert.AreEqual(40, doc3.PeptideCount);
            foreach (var nodePep in doc3.Peptides)
            {
                Assert.AreEqual(1, nodePep.Children.Count);
                Assert.AreEqual(3, nodePep.TransitionGroups.First().PrecursorCharge);
            }

            // Test using --associate-proteins-fasta to reconstitute the same protein
            // structure as the original FASTA import
            output = RunCommand("--in=" + outPath,
                "--save",
                "--associate-proteins-fasta=" + fastaPath);
            AssertEx.Contains(output, 
                string.Format(Resources.CommandLine_AssociateProteins_Associating_peptides_with_proteins_from_FASTA_file__0_, Path.GetFileName(fastaPath)));
            var docAssoc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(5, docAssoc.PeptideGroupCount);
            Assert.AreEqual(40, docAssoc.PeptideCount);
            var origProteins = docFasta.PeptideGroups.ToArray();
            var assocProteins = docAssoc.PeptideGroups.ToArray();
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(assocProteins[i].IsProtein);
                Assert.AreEqual(origProteins[i].Name, assocProteins[i].Name);
                Assert.AreEqual(origProteins[i].Description, assocProteins[i].Description);
            }

            // Write the original peptide group structure out as a set of lists
            // and test that this can be imported into a document
            using (var writerList = new StreamWriter(listsPath))
            {
                foreach (var protein in origProteins)
                {
                    writerList.WriteLine(">>" + protein.Name);
                    foreach (var nodePep in protein.Peptides)
                    {
                        writerList.WriteLine(nodePep.ModifiedSequence +
                                             Transition.GetChargeIndicator(nodePep.TransitionGroups.First().PrecursorCharge));
                    }
                }
            }

            outPath = TestFilesDir.GetTestPath("import-peplists.sky");
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-pep-list=" + listsPath);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_lists_from_file__0____,
                Path.GetFileName(listsPath)));
            AssertEx.DoesNotContain(output, Resources.CommandLine_ImportPeptideList_Warning__peptide_list_file_contains_lines_with_____Ignoring_provided_list_name_);
            var docMulti = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(5, docMulti.PeptideGroupCount);
            Assert.AreEqual(40, docMulti.PeptideCount);
            var multiLists = docMulti.PeptideGroups.ToArray();
            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(multiLists[i].IsProtein);
                Assert.AreEqual(origProteins[i].Name, multiLists[i].Name);
                Assert.IsNull(multiLists[i].Description);
            }

            // Test that attempting to specify a list name with a file that
            // has list names causes a warning message
            outPath = TestFilesDir.GetTestPath("import-peplists-warn.sky");
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-pep-list=" + listsPath,
                "--import-pep-list-name=cause_warning");
            AssertEx.Contains(output, Resources.CommandLine_ImportPeptideList_Warning__peptide_list_file_contains_lines_with_____Ignoring_provided_list_name_);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_lists_from_file__0____,
                Path.GetFileName(listsPath)));
            var docMultiWarn = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(5, docMultiWarn.PeptideGroupCount);
            Assert.AreEqual(40, docMultiWarn.PeptideCount);
        }
    }
}
