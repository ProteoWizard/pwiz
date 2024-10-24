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

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
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
            AssertEx.IsDocumentState(doc, null, 5, 26, 78);
            Assert.IsTrue(doc.PeptideGroups.All(nodePepGroup => nodePepGroup.IsProtein));

            // Repeat this with a peptide list, expecting the same numbers but peptide lists
            // instead of proteins
            var pepListPath = TestFilesDir.GetTestPath("bov-5-peplist.txt");
            var outListPath = TestFilesDir.GetTestPath("import-search-list.sky");
            WritePeptideList(pepListPath, doc, true);
            var listArgs = args.ToList();
            listArgs[1] = "--out=" + outListPath;
            listArgs[listArgs.Count - 1] = "--import-pep-list=" + pepListPath;

            output = RunCommand(listArgs.ToArray());

            string lineLibrary = TextUtil.LineSeparate(Resources.CommandLine_ImportSearch_Creating_spectral_library_from_files_,
                Path.GetFileName(searchFilePath));
            string lineList = string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_lists_from_file__0____,
                Path.GetFileName(pepListPath));
            AssertEx.Contains(output, lineLibrary);
            AssertEx.Contains(output, lineList);
            Assert.IsTrue(output.IndexOf(lineLibrary, StringComparison.Ordinal) < output.IndexOf(lineList, StringComparison.Ordinal),
                TextUtil.LineSeparate("Library building appears after peptide list import in the output:", output));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportSearch_Adding__0__modifications_, 2));

            var docList = ResultsUtil.DeserializeDocument(outListPath);
            Assert.IsTrue(docList.Settings.HasResults);
            Assert.AreEqual(1, docList.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(docList.Settings.MeasuredResults.ContainsChromatogram("CAexample"));
            AssertEx.IsDocumentState(docList, null, 5, 26, 78);
            Assert.IsTrue(docList.PeptideGroups.All(nodePepGroup => nodePepGroup.IsPeptideList));
            Assert.IsTrue(docList.PeptideTransitionGroups.All(nodeGroup => nodeGroup.HasLibInfo));

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
            string externalSpectrumFileErrorPrefix = Regex.Replace(Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFileError, "\\{2\\}.*", "{2}",
                RegexOptions.Singleline);
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
            const int expectedProtCount = 5;
            const int expectedPepCount = 40;
            var listPath = TestFilesDir.GetTestPath("peplist.txt");
            var listPath3 = TestFilesDir.GetTestPath("peplist3.txt");
            var listsPath = TestFilesDir.GetTestPath("peplists.txt");

            // Create initial document for comparisons by importing a FASTA file
            var output = RunCommand(true, CommandArgs.ARG_IN + docPath,
                CommandArgs.ARG_OUT + outPath,
                CommandArgs.ARG_IMPORT_FASTA + fastaPath);
            var docFasta = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(expectedProtCount, docFasta.PeptideGroupCount);
            Assert.AreEqual(expectedPepCount, docFasta.PeptideCount);

            // Test simply importing the modified sequences to create charge precursors
            // in a single peptide list with a default name
            outPath = TestFilesDir.GetTestPath("import-peplist.sky");
            File.WriteAllLines(listPath, docFasta.Peptides.Select(p => p.ModifiedSequence));
            output = RunCommand(true, CommandArgs.ARG_IN + docPath,
                CommandArgs.ARG_OUT + outPath,
                CommandArgs.ARG_IMPORT_PEP_LIST + listPath);
            var listName = docFasta.GetPeptideGroupId(true);    // Strictly speaking this should be using the doc for docPath
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_list__0__from_file__1____,
                listName, Path.GetFileName(listPath)));
            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc.PeptideGroupCount);
            Assert.AreEqual(listName, doc.PeptideGroups.First().Name);
            Assert.AreEqual(expectedPepCount, doc.PeptideCount);
            foreach (var nodePep in doc.Peptides)
            {
                Assert.AreEqual(1, nodePep.Children.Count);
                Assert.AreEqual(2, nodePep.TransitionGroups.First().PrecursorCharge);
            }

            // Add charge 3 specifiers to all the peptide sequences to create charge 3 precursors
            outPath = TestFilesDir.GetTestPath("import-peplist3.sky");
            const string listName3 = "Peptide-precursors-charge3";
            File.WriteAllLines(listPath3, doc.Peptides.Select(p => p.ModifiedSequence + "+++"));
            output = RunCommand(true, CommandArgs.ARG_IN + docPath,
                CommandArgs.ARG_OUT + outPath,
                CommandArgs.ARG_IMPORT_PEP_LIST_NAME + listName3,
                CommandArgs.ARG_IMPORT_PEP_LIST + listPath3);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_list__0__from_file__1____,
                listName3, Path.GetFileName(listPath3)));
            var doc3 = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, doc3.PeptideGroupCount);
            Assert.AreEqual(listName3, doc3.PeptideGroups.First().Name);
            Assert.AreEqual(expectedPepCount, doc3.PeptideCount);
            foreach (var nodePep in doc3.Peptides)
            {
                Assert.AreEqual(1, nodePep.Children.Count);
                Assert.AreEqual(3, nodePep.TransitionGroups.First().PrecursorCharge);
            }

            // Test using --associate-proteins-fasta to reconstitute the same protein
            // structure as the original FASTA import
            output = RunCommand(true, CommandArgs.ARG_IN + outPath,
                CommandArgs.ARG_SAVE.ArgumentText,
                CommandArgs.ARG_AP_FASTA + fastaPath);
            AssertEx.Contains(output, 
                string.Format(Resources.CommandLine_AssociateProteins_Associating_peptides_with_proteins_from_FASTA_file__0_, Path.GetFileName(fastaPath)));
            var docAssoc = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(expectedProtCount, docAssoc.PeptideGroupCount);
            Assert.AreEqual(expectedPepCount, docAssoc.PeptideCount);
            ValidateMultiList(docAssoc, docFasta, true);

            // Write the original peptide group structure out as a set of lists
            // and test that this can be imported into a document
            WritePeptideList(listsPath, docFasta, false);

            outPath = TestFilesDir.GetTestPath("import-peplists.sky");
            output = RunCommand(true, CommandArgs.ARG_IN + docPath,
                CommandArgs.ARG_OUT + outPath,
                CommandArgs.ARG_IMPORT_PEP_LIST + listsPath);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_lists_from_file__0____,
                Path.GetFileName(listsPath)));
            AssertEx.DoesNotContain(output, Resources.CommandLine_ImportPeptideList_Warning__peptide_list_file_contains_lines_with_____Ignoring_provided_list_name_);
            var docMulti = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(expectedProtCount, docMulti.PeptideGroupCount);
            Assert.AreEqual(expectedPepCount, docMulti.PeptideCount);
            ValidateMultiList(docMulti, docFasta, false);

            // Test that attempting to specify a list name with a file that
            // has list names causes a warning message
            outPath = TestFilesDir.GetTestPath("import-peplists-warn.sky");
            output = RunCommand(true, CommandArgs.ARG_IN + docPath,
                CommandArgs.ARG_OUT + outPath,
                CommandArgs.ARG_IMPORT_PEP_LIST + listsPath,
                CommandArgs.ARG_IMPORT_PEP_LIST_NAME + "cause_warning");
            AssertEx.Contains(output, Resources.CommandLine_ImportPeptideList_Warning__peptide_list_file_contains_lines_with_____Ignoring_provided_list_name_);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportPeptideList_Importing_peptide_lists_from_file__0____,
                Path.GetFileName(listsPath)));
            var docMultiWarn = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(expectedProtCount, docMultiWarn.PeptideGroupCount);
            Assert.AreEqual(expectedPepCount, docMultiWarn.PeptideCount);

            // Create new document with variable modifications
            outPath = TestFilesDir.GetTestPath("import-fasta-mods.sky");
            var oxMod = UniMod.DictUniModIds[new UniMod.UniModIdKey { Id = 35, Aa = 'M' }];
            var phosMod = UniMod.DictUniModIds[new UniMod.UniModIdKey { Id = 21, Aa = 'S' }];
            output = RunCommand(true, CommandArgs.ARG_NEW.GetArgumentTextWithValue(outPath),
                CommandArgs.ARG_SAVE.ArgumentText,
                CommandArgs.ARG_PEPTIDE_ADD_MOD + oxMod.UnimodId.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_AA + oxMod.AAs,
                CommandArgs.ARG_PEPTIDE_ADD_MOD + phosMod.UnimodId.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_AA + phosMod.AAs.Remove(1, 2),  // Remove ", " from "S, T"
                CommandArgs.ARG_IMPORT_FASTA + fastaPath);
            Assert.AreEqual(7, output.Split(new[] {oxMod.Name}, StringSplitOptions.None).Length - 1);
            Assert.AreEqual(7, output.Split(new[] {phosMod.Name}, StringSplitOptions.None).Length - 1);
            var docFastaMods = ResultsUtil.DeserializeDocument(outPath);
            const int expectedVarModPepCount = 170;
            Assert.AreEqual(expectedProtCount, docFastaMods.PeptideGroupCount);
            Assert.AreEqual(expectedPepCount + expectedVarModPepCount, docFastaMods.PeptideCount);
            Assert.AreEqual(expectedVarModPepCount, docFastaMods.Peptides.Count(HasVarMod));

            // Try this again with mods flipped to make Carbamidomethyl (C) variable
            // and others static
            outPath = TestFilesDir.GetTestPath("import-fasta-flipped-mods.sky");
            var carbMod = UniMod.DictUniModIds[new UniMod.UniModIdKey { Id = 4, Aa = 'C' }];
            output = RunCommand(true, CommandArgs.ARG_NEW.GetArgumentTextWithValue(outPath),
                CommandArgs.ARG_SAVE.ArgumentText,
                CommandArgs.ARG_PEPTIDE_CLEAR_MODS.ArgumentText,
                CommandArgs.ARG_PEPTIDE_ADD_MOD + carbMod.Name,
                CommandArgs.ARG_PEPTIDE_ADD_MOD_VARIABLE + true.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD + oxMod.UnimodId.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_VARIABLE + false.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_AA + oxMod.AAs,
                CommandArgs.ARG_PEPTIDE_ADD_MOD + phosMod.UnimodId.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_VARIABLE + false.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_AA + phosMod.AAs.Remove(1, 2),  // Remove ", " from "S, T"
                CommandArgs.ARG_IMPORT_FASTA + fastaPath);
            Assert.AreEqual(6, output.Split(new[] { carbMod.Name }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual(6, output.Split(new[] { oxMod.Name }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual(6, output.Split(new[] { phosMod.Name }, StringSplitOptions.None).Length - 1);
            var docFastaFlippedMods = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(expectedProtCount, docFastaFlippedMods.PeptideGroupCount);
            Assert.AreEqual(47, docFastaFlippedMods.PeptideCount);
            Assert.AreEqual(35, docFastaFlippedMods.Peptides.Count(HasVarMod));

            // Import this as a set of peptide lists and test that ModificationMatcher
            // automatically creates the right modifications
            outPath = TestFilesDir.GetTestPath("import-list-mods.sky");
            listsPath = TestFilesDir.GetTestPath("peplists-mods.txt");
            WritePeptideList(listsPath, docFastaMods, true);
            output = RunCommand(true, CommandArgs.ARG_NEW.GetArgumentTextWithValue(outPath),
                CommandArgs.ARG_SAVE.ArgumentText,
                CommandArgs.ARG_IMPORT_PEP_LIST + listsPath);
            var docListMods = ResultsUtil.DeserializeDocument(outPath);
            // Settings should already contain these modifications by virtue of adding
            // them above.
            AssertEx.DoesNotContain(output, Resources.CommandLine_ImportPeptideList_Using_the_Unimod_definitions_for_the_following_modifications_);
            Assert.AreEqual(3, docListMods.Settings.PeptideSettings.Modifications.StaticModifications.Count, 
                "Incorrect modifications: {0}", string.Join(",", docListMods.Settings.PeptideSettings.Modifications.StaticModifications));
            Assert.AreEqual(expectedProtCount, docListMods.PeptideGroupCount);
            Assert.AreEqual(expectedPepCount + expectedVarModPepCount, docListMods.PeptideCount);
            Assert.AreEqual(expectedVarModPepCount, docListMods.Peptides.Count(HasVarMod));
            ValidateMultiList(docListMods, docFastaMods, false);

            // Clear settings and make sure the Unimod modifications still work
            Settings.Default.StaticModList.Clear();
            Settings.Default.StaticModList.AddDefaults();
            output = RunCommand(true, CommandArgs.ARG_NEW.GetArgumentTextWithValue(outPath),
                CommandArgs.ARG_OVERWRITE.ArgumentText,
                CommandArgs.ARG_SAVE.ArgumentText,
                CommandArgs.ARG_IMPORT_PEP_LIST + listsPath);
            docListMods = ResultsUtil.DeserializeDocument(outPath);
            AssertEx.Contains(output, Resources.CommandLine_ImportPeptideList_Using_the_Unimod_definitions_for_the_following_modifications_);
            Assert.AreEqual(1, output.Split(new[] { oxMod.Name }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual(1, output.Split(new[] { phosMod.Name }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual(3, docListMods.Settings.PeptideSettings.Modifications.StaticModifications.Count);
            Assert.AreEqual(expectedProtCount, docListMods.PeptideGroupCount);
            Assert.AreEqual(expectedPepCount + expectedVarModPepCount, docListMods.PeptideCount);
            Assert.AreEqual(expectedVarModPepCount, docListMods.Peptides.Count(HasVarMod));
            ValidateMultiList(docListMods, docFastaMods, false);

            // Add a single protein with a bare sequence and verify that it does not pick up modified variants
            // The modifications in the other proteins should tell Skyline that sequences are specified
            // without need of modification expansion.
            File.AppendAllLines(listsPath, new[]
            {
                PeptideGroupBuilder.PEPTIDE_LIST_PREFIX + "Unmodified",
                "MVNNGHSFNVEYDDSQDR"
            });
            output = RunCommand(true, CommandArgs.ARG_NEW.GetArgumentTextWithValue(outPath),
                CommandArgs.ARG_OVERWRITE.ArgumentText,
                CommandArgs.ARG_SAVE.ArgumentText,
                CommandArgs.ARG_PEPTIDE_ADD_MOD + oxMod.UnimodId.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_AA + oxMod.AAs,
                CommandArgs.ARG_PEPTIDE_ADD_MOD + phosMod.UnimodId.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_AA + phosMod.AAs.Remove(1, 2),  // Remove ", " from "S, T"
                CommandArgs.ARG_IMPORT_PEP_LIST + listsPath);
            docListMods = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(3, docListMods.Settings.PeptideSettings.Modifications.StaticModifications.Count);
            Assert.AreEqual(expectedProtCount + 1, docListMods.PeptideGroupCount);
            Assert.AreEqual(expectedPepCount + expectedVarModPepCount + 1, docListMods.PeptideCount);
            Assert.AreEqual(expectedVarModPepCount, docListMods.Peptides.Count(HasVarMod));
            Assert.IsFalse(docListMods.PeptideGroups.Any(g => g.AutoManageChildren),
                TextUtil.LineSeparate("The following lists have auto-manage children:",
                    TextUtil.LineSeparate(docListMods.PeptideGroups.Where(g => g.AutoManageChildren).Select(g => g.Name))));

            // And verify that a bare peptide sequence with no modified peptides gets expanded appropriately
            // CONSIDER: Adding a command-line argument to prevent this expansion might be desirable
            File.WriteAllLines(listPath, new []{ @"MVNNGHSFNVEYDDSQDR" });
            outPath = TestFilesDir.GetTestPath("import-list-expand-mods.sky");
            output = RunCommand(true, CommandArgs.ARG_NEW.GetArgumentTextWithValue(outPath),
                CommandArgs.ARG_SAVE.ArgumentText,
                CommandArgs.ARG_PEPTIDE_ADD_MOD + oxMod.UnimodId.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_AA + oxMod.AAs,
                CommandArgs.ARG_PEPTIDE_ADD_MOD + phosMod.UnimodId.ToString(),
                CommandArgs.ARG_PEPTIDE_ADD_MOD_AA + phosMod.AAs.Remove(1, 2),  // Remove ", " from "S, T"
                CommandArgs.ARG_IMPORT_PEP_LIST + listPath);
            var docListExpanded = ResultsUtil.DeserializeDocument(outPath);
            Assert.AreEqual(1, docListExpanded.PeptideGroupCount);
            Assert.AreEqual(8, docListExpanded.PeptideCount);

            // Test correct error reporting for unrecognized modifications
            var listPathBadMod = TestFilesDir.GetTestPath("peplist-bad-mod.txt");
            outPath = TestFilesDir.GetTestPath("import-bad-mod.sky");
            File.WriteAllLines(listPathBadMod, doc.Peptides.Select(MungeMod));
            output = RunCommand(false, CommandArgs.ARG_IN + docPath,
                CommandArgs.ARG_OUT + outPath,
                CommandArgs.ARG_IMPORT_PEP_LIST + listPathBadMod);
            AssertEx.Contains(output, ModelResources.AbstractModificationMatcher_UninterpretedMods_The_following_modifications_could_not_be_interpreted);
            var regexMod = new Regex(@"^.*(.\[[^\]]+]).*$");
            var mods = (from p in doc.Peptides
                where regexMod.IsMatch(p.ModifiedSequence)
                select regexMod.Match(MungeMod(p)).Groups[1].Value).Distinct().ToArray();
            Assert.AreEqual(1, mods.Length);
            AssertEx.Contains(output, mods[0]);
            Assert.IsFalse(File.Exists(outPath));
        }

        private string MungeMod(PeptideDocNode p)
        {
            return p.ModifiedSequence.Replace("[+", "[+1");
        }

        private static void ValidateMultiList(SrmDocument docMulti, SrmDocument docFasta, bool withAssoc)
        {
            var origProteins = docFasta.PeptideGroups.ToArray();
            var multiLists = docMulti.PeptideGroups.ToArray();
            for (int i = 0; i < docMulti.PeptideGroupCount; i++)
            {
                var origProt = origProteins[i];
                var multiGroup = multiLists[i];
                Assert.AreEqual(origProt.Name, multiGroup.Name);
                if (withAssoc)
                {
                    Assert.IsTrue(multiGroup.IsProtein);
                    Assert.AreEqual(origProt.Description, multiGroup.Description);
                }
                else
                {
                    Assert.IsFalse(multiGroup.IsProtein);
                    Assert.IsNull(multiGroup.Description);
                }

                var origPeps = origProt.Peptides.ToArray();
                var multiPeps = multiGroup.Peptides.ToArray();
                Assert.AreEqual(origPeps.Length, multiPeps.Length);
                for (int j = 0; j < multiPeps.Length; j++)
                {
                    Assert.AreEqual(origPeps[j].ModifiedSequence, multiPeps[j].ModifiedSequence);
                }
            }
        }

        private static void WritePeptideList(string listsPath, SrmDocument doc, bool withUnimodIds)
        {
            using var writerList = new StreamWriter(listsPath);

            foreach (var protein in doc.PeptideGroups)
            {
                writerList.WriteLine(PeptideGroupBuilder.PEPTIDE_LIST_PREFIX + protein.Name);
                foreach (var nodePep in protein.Peptides)
                {
                    var modifiedSeq =
                        ModifiedSequence.GetModifiedSequence(doc.Settings, nodePep, IsotopeLabelType.light);
                    string modifiedText = withUnimodIds ? modifiedSeq.UnimodIds : modifiedSeq.MonoisotopicMasses;
                    string chargeIndicator =
                        Transition.GetChargeIndicator(nodePep.TransitionGroups.First().PrecursorCharge);
                    writerList.WriteLine(modifiedText + chargeIndicator);
                }
            }
        }

        private bool HasVarMod(PeptideDocNode nodePep)
        {
            return nodePep.ModifiedSequence.Contains("M[") ||
                   nodePep.ModifiedSequence.Contains("S[") ||
                   nodePep.ModifiedSequence.Contains("T[");
        }
    }
}
