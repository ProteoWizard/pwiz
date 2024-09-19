/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Skyline;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CommandLineAssayImportTest : AbstractUnitTestEx
    {
        private const string ZIP_FILE = @"Test\CommandLineAssayImportTest.zip";

        [TestMethod]
        public void ConsoleAssayLibraryImportTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            TestAssayImportGeneral();
        }

        [TestMethod]
        public void ConsoleAssayLibraryImportTestAsSmallMolecules()
        {
            if (SkipSmallMoleculeTestVersions())
            {
                return;
            }
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            AsSmallMoleculeTestUtil.TranslateFilesToSmallMolecules(TestFilesDir.FullPath, true);
            TestAssayImportGeneral(true);
        }

        private string GetTemplateDoc(string docname, bool asSmallMolecules)
        {
            var path = TestFilesDir.GetTestPath(docname);
            if (asSmallMolecules)
            {
                AsSmallMoleculeTestUtil.TranslateSkylineDocumentToSmallMolecules(path);
            }
            return path;
        }

        protected void TestAssayImportGeneral(bool asSmallMolecules = false)
        {
            var documentExisting = GetTemplateDoc("AQUA4_Human_Existing_Calc.sky", asSmallMolecules);
            var documentUpdatedNoPath = "AQUA4_Human_Existing_Calc2.sky";
            var documentUpdated = TestFilesDir.GetTestPath(documentUpdatedNoPath);
            // 1. Import mass list with iRT's into document, then cancel
            string textNoError = TestFilesDir.GetTestPath("OpenSWATH_SM4_NoError.csv");

            string output = RunCommand("--in=" + documentExisting,
                "--import-assay-library=" + textNoError,
                "--out=" + documentUpdated);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Importing_transiton_list__0____, Path.GetFileName(textNoError)));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_SaveFile_File__0__saved_, documentUpdatedNoPath)); // Output file name appears in success message
            var docAfter = ResultsUtil.DeserializeDocument(documentUpdated);
            AssertEx.IsDocumentState(docAfter, null, 24, 294, 1170);
            ValidateIrtAndLibrary(docAfter);

            // 2. Repeat causes error due to existing library
            output = RunCommand("--in=" + documentExisting,
                "--import-assay-library=" + textNoError,
                "--out=" + documentUpdated);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Error__There_is_an_existing_library_with_the_same_name__0__as_the_document_library_to_be_created_,
                        Path.GetFileNameWithoutExtension(documentUpdated) + BiblioSpecLiteSpec.ASSAY_NAME));

            // 3. Peptide iRT in document conflicts with peptide iRT in database, respond by canceling whole operation
            documentUpdated = TestFilesDir.GetTestPath("AQUA4_Human_Existing_Calc3.sky");
            string textConflict = TestFilesDir.GetTestPath("OpenSWATH_SM4_Overwrite.csv");
            output = RunCommand("--in=" + documentExisting,
                "--import-assay-library=" + textConflict,
                "--out=" + documentUpdated);

            var smDecorate = asSmallMolecules ? RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator : string.Empty;
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Warning__The_iRT_calculator_already_contains__0__with_the_value__1___Ignoring__2_,
                smDecorate+"YVPIHTIDDGYSVIK", 76, 49.8));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Importing_transiton_list__0____, Path.GetFileName(textConflict)));
            Assert.IsTrue(File.Exists(documentUpdated));

            // 4. If mass list contains a peptide that is already an iRT standard, throw exception
            documentUpdated = TestFilesDir.GetTestPath("AQUA4_Human_Existing_Calc4.sky");
            string textStandard = TestFilesDir.GetTestPath("OpenSWATH_SM4_StandardsConflict.csv");
            output = RunCommand("--in=" + documentExisting,
                "--import-assay-library=" + textStandard,
                "--out=" + documentUpdated);

            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Warning__The_iRT_calculator_already_contains__0__with_the_value__1___Ignoring__2_,
                smDecorate + "YVPIHTIDDGYSVIK", 76, 49.8));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Warning__The_iRT_calculator_already_contains__0__with_the_value__1___Ignoring__2_,
                smDecorate + "GTFIIDPGGVIR", 71.3819, -10));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_Run_Error__Failed_importing_the_file__0____1_, textStandard,
                string.Format(Resources.SkylineWindow_AddIrtPeptides_Imported_peptide__0__with_iRT_library_value_is_already_being_used_as_an_iRT_standard_,
                    smDecorate + "GTFIIDPGGVIR")));
            Assert.IsFalse(File.Exists(documentUpdated));

            // 5. Mass list contains different iRT times on same peptide
            string textIrtConflict = TestFilesDir.GetTestPath("OpenSWATH_SM4_InconsistentIrt.csv");
            output = RunCommand("--in=" + documentExisting,
                "--import-assay-library=" + textIrtConflict,
                "--out=" + documentUpdated);
            AssertEx.Contains(output, string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                smDecorate + "YVPIHTIDDGYSVIK", asSmallMolecules ? 860.4512 : 864.458, 49.8, 50.2));
            Assert.IsFalse(File.Exists(documentUpdated));

            // 6. Mass list contains different iRT values on two non-contiguous lines of the same transition group
            string textIrtGroupConflict = TestFilesDir.GetTestPath("InterleavedInconsistentIrt.csv");
            output = RunCommand("--in=" + documentExisting,
                "--import-assay-library=" + textIrtGroupConflict,
                "--out=" + documentUpdated);

            string expectedErrors = "{0}";
            for (int i = 0; i < (asSmallMolecules ? 2 : 59); i++) // Peptide version has modifications trouble, small mol does not
            {
                expectedErrors += Resources.CommandLine_ImportTransitionList_Error___line__0___column__1____2_;
            }
            AssertEx.AreComparableStrings(expectedErrors, output);
            Assert.IsFalse(File.Exists(documentUpdated));

            // 7. Now remove the modified column which is bogus and causing errors
            RemoveColumn(textIrtGroupConflict, 22);
            output = RunCommand("--in=" + documentExisting,
                "--import-assay-library=" + textIrtGroupConflict,
                "--out=" + documentUpdated);
            expectedErrors = "{0}";
            for (int i = 0; i < 2; i++)
            {
                expectedErrors += Resources.CommandLine_ImportTransitionList_Error___line__0___column__1____2_;
            }
            AssertEx.AreComparableStrings(expectedErrors, output);
            Assert.IsFalse(File.Exists(documentUpdated));

            // 8. Try again, this time accepting errors
            output = RunCommand("--in=" + documentExisting,
                "--import-assay-library=" + textIrtGroupConflict,
                "--ignore-transition-errors",
                "--out=" + documentUpdated);
            string expectedWarnings = "{0}";
            for (int i = 0; i < 2; i++)
            {
                expectedWarnings += Resources.CommandLine_ImportTransitionList_Warning___line__0___column__1____2_; // Two transitions of the same precursor have different iRT
            }
            expectedWarnings += "{3}"; // Various success messages
            AssertEx.AreComparableStrings(expectedWarnings, output);
            var docIgnore = ResultsUtil.DeserializeDocument(documentUpdated);
            Assert.AreEqual(docIgnore.MoleculeTransitionCount, 109);
            Assert.AreEqual(docIgnore.MoleculeTransitionGroupCount, 22);
            ValidateIrtAndLibrary(docIgnore);

            // 9. Argument requirements warnings
            var documentBlank = TestFilesDir.GetTestPath("AQUA4_Human_Blank.sky");
            output = RunCommand("--in=" + documentBlank,
                CommandArgs.ARG_IGNORE_TRANSITION_ERRORS.ArgumentText,
                CommandArgs.ARG_IRT_STANDARDS_GROUP_NAME.ArgumentText + "=iRT-peps",
                CommandArgs.ARG_IRT_STANDARDS_FILE.ArgumentText + "=C:\\test.csv");
            AssertEx.Contains(output, 
                CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IGNORE_TRANSITION_ERRORS, CommandArgs.ARG_IMPORT_TRANSITION_LIST),
                CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IRT_STANDARDS_GROUP_NAME, CommandArgs.ARG_IMPORT_ASSAY_LIBRARY),
                CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IRT_STANDARDS_FILE, CommandArgs.ARG_IMPORT_ASSAY_LIBRARY));

            // 10. Irt database errors
            documentUpdated = TestFilesDir.GetTestPath("AQUA4_Human_Blank2.sky");
            string irtDatabasePath = Path.ChangeExtension(documentUpdated, IrtDb.EXT) ?? IrtDb.EXT; // Not null for ReSharper
            string textIrt = TestFilesDir.GetTestPath("OpenSWATH_SM4_iRT.csv");
            File.WriteAllText(irtDatabasePath, irtDatabasePath); // Dummy file containing its own path
            string allIonsArgument = asSmallMolecules ? string.Empty : "--tran-product-ion-types=" + string.Join(",", Transition.PEPTIDE_ION_TYPES);  // For when more than y- and b-ions are used (a2 in OpenSWATH_SM4_iRT.csv)
            output = RunCommand("--in=" + documentBlank,
                "--import-assay-library=" + textNoError,
                allIonsArgument,
                CommandArgs.ARG_IRT_STANDARDS_FILE.ArgumentText + "=" + textIrt,
                "--out=" + documentUpdated);
            AssertEx.Contains(output,
                string.Format(Resources.CommandLine_CreateIrtDatabase_Error__Importing_an_assay_library_to_a_document_without_an_iRT_calculator_cannot_create__0___because_it_exists_,
                              irtDatabasePath),
                string.Format(Resources.CommandLine_CreateIrtDatabase_Use_the__0__argument_to_specify_a_file_to_create_,
                              CommandArgs.ARG_IRT_DATABASE_PATH.ArgumentText));
            Assert.IsFalse(File.Exists(documentUpdated));
            FileEx.SafeDelete(irtDatabasePath);

            string fakePath = TestFilesDir.GetTestPath("Fake.irtdb");
            output = RunCommand("--in=" + documentBlank,
                "--import-assay-library=" + textNoError,
                CommandArgs.ARG_IRT_DATABASE_PATH.ArgumentText + "=" + fakePath,
                "--out=" + documentUpdated);
            AssertEx.Contains(output,
                string.Format(Resources.CommandLine_ImportTransitionList_Error__To_create_the_iRT_database___0___for_this_assay_library__you_must_specify_the_iRT_standards_using_either_of_the_arguments__1__or__2_,
                            fakePath, CommandArgs.ARG_IRT_STANDARDS_GROUP_NAME.ArgumentText, CommandArgs.ARG_IRT_STANDARDS_FILE.ArgumentText));
            Assert.IsFalse(File.Exists(documentUpdated));
            Assert.IsFalse(File.Exists(irtDatabasePath));

            // 11. Blank textIrt file shows error message
            string blankPath = TestFilesDir.GetTestPath("blank_file.txt");
            output = RunCommand("--in=" + documentBlank,
                "--import-assay-library=" + textNoError,
                CommandArgs.ARG_IRT_STANDARDS_FILE.ArgumentText + "=" + blankPath,
                "--out=" + documentUpdated);
            AssertEx.Contains(output,
                string.Format(Resources.CommandLine_Run_Error__Failed_importing_the_file__0____1_, blankPath, Resources.SkylineWindow_importMassListMenuItem_Click_Data_columns_not_found_in_first_line));
            Assert.IsFalse(File.Exists(documentUpdated));
            Assert.IsFalse(File.Exists(irtDatabasePath));

            // 12. Provide iRT protein group name not in the assay library
            const string dummyName = "DummyName";
            output = RunCommand("--in=" + documentBlank,
                "--import-assay-library=" + textNoError,
                CommandArgs.ARG_IRT_STANDARDS_GROUP_NAME.ArgumentText + "=" + dummyName,
                "--out=" + documentUpdated);
            AssertEx.Contains(output,
                string.Format(Resources.CommandLine_ImportTransitionList_Error__The_name__0__specified_with__1__was_not_found_in_the_imported_assay_library_,
                                dummyName, CommandArgs.ARG_IRT_STANDARDS_GROUP_NAME.ArgumentText));
            Assert.IsFalse(File.Exists(documentUpdated));
            Assert.IsFalse(File.Exists(irtDatabasePath));

            // Test creation of iRT calculator from protein embedded in the imported transition list

            // 13. Successful import and successful creation of iRT database and library
            // Document starts empty with no transitions and no iRT calculator
            // 355 transitions, libraries, and iRT times are imported, including libraries for the iRT times
            string newIrtDatabasePath = TestFilesDir.GetTestPath("irtNew.irtdb");
            output = RunCommand("--in=" + documentBlank,
                "--import-assay-library=" + textNoError,
                allIonsArgument,
                CommandArgs.ARG_IRT_DATABASE_PATH.ArgumentText + "=" + newIrtDatabasePath,
                CommandArgs.ARG_IRT_STANDARDS_FILE.ArgumentText + "=" + textIrt,
                "--out=" + documentUpdated);
            int expectedCount = 294;
            ValidateIrtAndLibraryOutput(output, documentUpdated, expectedCount);
            Assert.IsTrue(File.Exists(documentUpdated));
            Assert.IsTrue(File.Exists(newIrtDatabasePath));
            var docSuccess = ResultsUtil.DeserializeDocument(documentUpdated);
            Assert.AreEqual(expectedCount, docSuccess.MoleculeTransitionGroupCount);
            Assert.AreEqual(1170, docSuccess.MoleculeTransitionCount);
            ValidateIrtAndLibrary(docSuccess);

            // 14. Successful import and succesful load of existing database, with keeping of iRT's, plus successful library import with ion mobility values
            documentUpdated = TestFilesDir.GetTestPath("AQUA4_Human_Blank3.sky"); 
            var irtOriginal = TestFilesDir.GetTestPath("irtOriginal.irtdb");
            output = RunCommand("--in=" + documentBlank,
                "--import-assay-library=" + TestFilesDir.GetTestPath("OpenSWATH_SM4_NoError_with_ion_mobility.csv"),
                CommandArgs.ARG_IRT_DATABASE_PATH.ArgumentText + "=" + irtOriginal,
                "--out=" + documentUpdated);
            expectedCount = 284;
            ValidateIrtAndLibraryOutput(output, documentUpdated, expectedCount);
            var docSuccess2 = ResultsUtil.DeserializeDocument(documentUpdated);
            Assert.AreEqual(expectedCount, docSuccess2.MoleculeTransitionGroupCount);
            Assert.AreEqual(1119, docSuccess2.MoleculeTransitionCount);
            // Can't validate, because the document does not contain the iRT standard peptides
            AssertEx.Contains(output, Resources.CommandLine_ImportTransitionList_Warning__The_document_is_missing_iRT_standards);
            // The data has fake ion mobility values set as 1+(mz/2), this should appear in document and in library
            var mobilities = new HashSet<double>();
            var imTolerance = asSmallMolecules ? (10.1/4) : 0.001; // Original peptide calculation considered modification to arrive at mz
            foreach (var tg in docSuccess2.MoleculeTransitionGroups.Where(n => n.ExplicitValues.IonMobility.HasValue))
            {
                var imExpected = 1 + (0.5 * tg.PrecursorMz);
                AssertEx.IsTrue(Math.Abs(imExpected - tg.ExplicitValues.IonMobility.Value) < imTolerance);
                mobilities.Add(tg.ExplicitValues.IonMobility.Value);
            }

            var currentLibrary = docSuccess2.Settings.PeptideSettings.Libraries.Libraries[0];
            foreach (var key in currentLibrary.Keys)
            {
                var spec = currentLibrary.GetSpectra(key, IsotopeLabelType.light, LibraryRedundancy.all).First();
                AssertEx.IsTrue(mobilities.Any(im => Math.Abs(im - spec.IonMobilityInfo.IonMobility.Mobility.Value) < 0.001));
            }
        }

        private void ValidateIrtAndLibraryOutput(string output, string path, int expectedCount)
        {
            string basename = Path.GetFileNameWithoutExtension(path);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Importing__0__iRT_values_into_the_iRT_calculator__1_, expectedCount, basename),
                string.Format(Resources.CommandLine_ImportTransitionList_Adding__0__spectra_to_the_library__1_, expectedCount, basename + BiblioSpecLiteSpec.ASSAY_NAME));
        }

        private static void ValidateIrtAndLibrary(SrmDocument docAfter)
        {
            int irtStandardCount = 0;
            foreach (var nodePep in docAfter.Molecules)
            {
                if (nodePep.GlobalStandardType == PeptideDocNode.STANDARD_TYPE_IRT)
                    irtStandardCount++;
                else
                {
                    Assert.IsTrue(nodePep.HasLibInfo);
                    foreach (var nodeTran in nodePep.TransitionGroups.SelectMany(g => g.Transitions))
                    {
                        if (!nodeTran.HasLibInfo)
                            Assert.Fail("Missing library info from {0} - {1}", nodePep, nodeTran.GetDisplayText(new DisplaySettings(
                                new NormalizedValueCalculator(docAfter), 
                                nodePep, false, 0, NormalizeOption.RatioToFirstStandard(docAfter.Settings))));
                    }
                }
            }
            Assert.AreEqual(10, irtStandardCount);
        }

        private static void RemoveColumn(string textIrtGroupConflict, int columnIndex)
        {
            // Now Skyline will choose the modified column which causes different errors. So we remove it.
            string[] lines = File.ReadAllLines(textIrtGroupConflict);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] =
                    string.Join(TextUtil.SEPARATOR_TSV.ToString(),
                        lines[i].ParseDsvFields(TextUtil.SEPARATOR_TSV)
                            .Where((s, j) => j != columnIndex).ToArray());
            }
            File.WriteAllLines(textIrtGroupConflict, lines);
        }
    }
}

