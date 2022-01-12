/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public class CommandLinePeakBoundaryTest : AbstractUnitTestEx
    {
        const PeakIdentification FALSE = PeakIdentification.FALSE;
        const PeakIdentification TRUE = PeakIdentification.TRUE;

        const string TEST_ZIP_PATH = @"Test\PeakBoundaryTest.zip";

        private readonly double?[] _tsvMinTime1 = { 34.13, 26.17, null, 53.3, null, 51.66 };
        private readonly double?[] _tsvMaxTime1 = { 34.72, 26.92, null, 54.06, null, 52.45 };
        private readonly PeakIdentification[] _tsvIdentified1 = { FALSE, FALSE, FALSE, FALSE, FALSE, FALSE };
        private readonly double?[] _tsvAreas1 = { 6.682e10, 2.421e10, null, 1.012e11, null, 4.677e10 };

        private readonly double?[] _tsvMinTime2 = { 33.88, 25.89, null, 53.09, null, 51.46 };
        private readonly double?[] _tsvMaxTime2 = { 34.49, 26.68, null, 53.84, null, 52.25 };
        private readonly PeakIdentification[] _tsvIdentified2 = { FALSE, FALSE, FALSE, FALSE, FALSE, FALSE };
        private readonly double?[] _tsvAreas2 = { 1.990e11, 8.593e10, null, 3.209e11, null, 1.332e11 };

        private readonly double?[] _csvMinTime1 = { 35.13, 27.17, null, 54.3, null, 52.66 };
        private readonly double?[] _csvMaxTime1 = { 35.92, 28.12, null, 55.26, null, 53.65 };
        private readonly PeakIdentification[] _csvIdentified1 = { FALSE, FALSE, FALSE, FALSE, FALSE, FALSE };
        private readonly double?[] _csvAreas1 = { 1.044e9, 4.711e8, null, 3.330e8, null, 5.523e9 };

        private readonly double?[] _csvMinTime2 = { 34.88, 26.89, null, 54.09, null, 52.46 };
        private readonly double?[] _csvMaxTime2 = { 35.69, 27.88, null, 55.04, null, 53.45 };
        private readonly PeakIdentification[] _csvIdentified2 = { FALSE, FALSE, FALSE, FALSE, FALSE, FALSE };
        private readonly double?[] _csvAreas2 = { 1.586e9, 6.603e8, null, 9.978e8, null, 1.853e10 };

        private readonly string[] _precursorMzsUs =
        {
            "533.294964", "623.29589", "415.866352", "692.868631",
            "462.248179", "634.355888"
        };

        private readonly string[] _precursorMzsIntl =
        {
            "533,294964", "623,29589", "415,866352", "692,868631",
            "462,248179", "634,355888"
        };
        private const string annote = "PrecursorMz";

        private readonly double?[] _idMinTime1 = { 32.25, 33.02, 37.68, 21.19, 35.93, 33.85, 29.29, 31.55, 37.21, 27.02, 25.41, 29.55, 29.60, 25.07, 23.14, 29.11 };
        private readonly double?[] _idMaxTime1 = { 33.53, 33.90, 38.90, 22.23, 37.43, 35.35, 30.86, 31.66, 37.81, 28.68, 26.20, 31.13, 30.32, 26.08, 24.92, 29.58 };

        private readonly PeakIdentification[] _idIdentified1 =
        {
            FALSE, FALSE, FALSE, TRUE, TRUE,
            FALSE, TRUE, FALSE, FALSE, TRUE,
            FALSE, TRUE, TRUE, TRUE, TRUE,
            TRUE
        };

        private readonly double?[] _idAreas1 =
        {
            4060, 1927, 16954, 16314, 4188, 23038, 28701, 354,
            2063, 21784, 8775, 18019, 19579, 11013, 58116, 26262
        };

        private readonly string[] _peptides = { "VLVLDTDYK", "TPEVDDEALEK", "FFVAPFPEVFGK", "YLGYLEQLLR" };

        private readonly string[] _peptidesId =
        {
            "LGGLRPESPESLTSVSR", "ALVEFESNPEETREPGSPPSVQR", "YGPADVEDTTGSGATDSKDDDDIDLFGSDDEEESEEAKR",
            "ESEDKPEIEDVGSDEEEEKKDGDK", "GVVDSEDLPLNISR", "DMESPTKLDVTLAK",
            "VGSLDNVGHLPAGGAVK", "KTGSYGALAEITASK", "TGSYGALAEITASK", "IVRGDQPAASGDSDDDEPPPLPR",
            "LLKEGEEPTVYSDEEEPKDESAR", "KQITMEELVR", "SSSVGSSSSYPISPAVSR",
            "EKTPELPEPSVK", "VPKPEPIPEPKEPSPEK", "KETESEAEDNLDDLEK"
        };

        private readonly int[] _precursorCharge = { 2, 2, 3, 2, 3, 2 };
        private readonly int[] _precursorChargeId = { 3, 3, 4, 4, 2, 2, 3, 3, 2, 3, 4, 2, 2, 2, 3, 3 };
        private const double RT_TOLERANCE = 0.02;

        /// <summary>
        /// Tests using the "--import-peak-boundaries" command line argument
        /// </summary>
        [TestMethod]
        public void TestCommandLineImportPeakBoundary()
        {
            // Load the SRM document and relevant files            
            var testFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            bool isIntl = (TextUtil.CsvSeparator != TextUtil.SEPARATOR_CSV);
            var precursorMzs = isIntl ? _precursorMzsIntl : _precursorMzsUs;
            var peakBoundaryFileTsv =
                testFilesDir.GetTestPath(isIntl ? "PeakBoundaryTsvIntl.tsv" : "PeakBoundaryTsv.tsv");
            var peakBoundaryFileCsv = testFilesDir.GetTestPath(isIntl ? "PeakBoundaryIntl.csv" : "PeakBoundaryUS.csv");
            var originalDocumentPath = testFilesDir.GetTestPath("Chrom05.sky");
            var cult = LocalizationHelper.CurrentCulture;
            var cultI = CultureInfo.InvariantCulture;
            string csvSep = TextUtil.CsvSeparator.ToString(cultI);
            string spaceSep = TextUtil.SEPARATOR_SPACE.ToString(cultI);

            // Test Tsv import, looking at first .raw file
            DoFileImportTests(originalDocumentPath, peakBoundaryFileTsv, _precursorCharge,
                _tsvMinTime1, _tsvMaxTime1, _tsvIdentified1, _tsvAreas1, _peptides, 0, precursorMzs);
            // Test Tsv import, looking at second .raw file
            DoFileImportTests(originalDocumentPath, peakBoundaryFileTsv, _precursorCharge,
                _tsvMinTime2, _tsvMaxTime2, _tsvIdentified2, _tsvAreas2, _peptides, 1, precursorMzs);

            // Test Csv import for local format
            DoFileImportTests(originalDocumentPath, peakBoundaryFileCsv, _precursorCharge,
                _csvMinTime1, _csvMaxTime1, _csvIdentified1, _csvAreas1, _peptides, 0, precursorMzs);
            DoFileImportTests(originalDocumentPath, peakBoundaryFileCsv, _precursorCharge,
                _csvMinTime2, _csvMaxTime2, _csvIdentified2, _csvAreas2, _peptides, 1, precursorMzs);

            // Test that importing same file twice leads to no change to document the second time
            var afterImportPath = testFilesDir.GetTestPath("AfterImport.sky");
            var docNew = ImportFileToDocAndSaveAs(originalDocumentPath, peakBoundaryFileTsv, afterImportPath);
            var docNewSame = ImportFileToDoc(afterImportPath, peakBoundaryFileTsv);
            AssertEx.DocumentCloned(docNew, docNewSame);

            // Test that exporting peak boundaries and then importing them leads to no change
            string peakBoundaryExport = testFilesDir.GetTestPath("TestRoundTrip.csv");
            ReportSpec reportSpec = MakeReportSpec();
            ReportToCsv(reportSpec, docNew, peakBoundaryExport);
            var docRoundTrip = ImportFileToDoc(afterImportPath, peakBoundaryExport);
            AssertEx.DocumentCloned(docNew, docRoundTrip);

            // 1. Empty file - 
            ImportThrowsException(originalDocumentPath, string.Empty,
                Resources.PeakBoundaryImporter_Import_Failed_to_read_the_first_line_of_the_file);
            // 2. No separator in first line
            ImportThrowsException(originalDocumentPath, "No-valid-separators",
                TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV
                    ? Resources
                        .PeakBoundaryImporter_DetermineCorrectSeparator_The_first_line_does_not_contain_any_of_the_possible_separators_comma__tab_or_space_
                    : Resources
                        .PeakBoundaryImporter_DetermineCorrectSeparator_The_first_line_does_not_contain_any_of_the_possible_separators_semicolon__tab_or_space_);

            // 3. Missing field names
            ImportThrowsException(originalDocumentPath, string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(3).ToArray()),
                Resources.PeakBoundaryImporter_Import_Failed_to_find_the_necessary_headers__0__in_the_first_line);

            string[] values =
            {
                "TPEVDDEALEK", "Q_2012_0918_RJ_13.raw", (4.0).ToString(cult), (3.5).ToString(cult),
                (4.5).ToString(cult), 2.ToString(cult), 0.ToString(cult)
            };
            string headerRow = string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(values.Length));
            string headerRowSpaced = string.Join(spaceSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(values.Length));

            // 4. Mismatched field count
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRow, string.Join(spaceSep, values)),
                Resources.PeakBoundaryImporter_Import_Line__0__field_count__1__differs_from_the_first_line__which_has__2_);

            // 5. Invalid charge state
            string[] valuesBadCharge = new List<string>(values).ToArray();
            valuesBadCharge[(int)PeakBoundaryImporter.Field.charge] = (3.5).ToString(cult);
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadCharge)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_charge_state_);
            valuesBadCharge[(int)PeakBoundaryImporter.Field.charge] = TextUtil.EXCEL_NA;
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadCharge)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_charge_state_);

            // 6. Invalid start time
            string[] valuesBadTime = new List<string>(values).ToArray();
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] = "bad";
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_);
            valuesBadTime[(int)PeakBoundaryImporter.Field.end_time] = "bad";
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_);

            // Still not okay when not changing peaks, because we now store start and end times as annotations
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_);

            // 7. Invalid end time
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] =
                values[(int)PeakBoundaryImporter.Field.start_time];
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_end_time_);

            // Still not okay when not changing peaks, because we now store start and end times as annotations
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_end_time_);

            // #N/A in times ok
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] =
                valuesBadTime[(int)PeakBoundaryImporter.Field.end_time] = TextUtil.EXCEL_NA;
            ImportNoException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)));
            // If only start time #N/A throws exception
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] = (3.5).ToString(cult);
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_Missing_end_time_on_line__0_);
            // If only end time #N/A throws exception
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] = TextUtil.EXCEL_NA;
            valuesBadTime[(int)PeakBoundaryImporter.Field.end_time] = (3.5).ToString(cult);
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_Missing_start_time_on_line__0_);
            // Empty times throws exception
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] =
                valuesBadTime[(int)PeakBoundaryImporter.Field.end_time] = string.Empty;
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_);

            // 8. Not imported file gets skipped
            string[] valuesBadFile = new List<string>(values).ToArray();
            valuesBadFile[(int)PeakBoundaryImporter.Field.filename] = "Q_2012_0918_RJ_15.raw";
            ImportNoException(originalDocumentPath, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadFile)));

            // 9. Unknown modification state gets skipped
            string[] valuesBadSequence = new List<string>(values).ToArray();
            valuesBadSequence[(int)PeakBoundaryImporter.Field.modified_peptide] = "T[+80]PEVDDEALEK";
            ImportNoException(originalDocumentPath, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadSequence)));

            // 10. Unknown peptide sequence gets skipped
            valuesBadSequence[(int)PeakBoundaryImporter.Field.modified_peptide] = "PEPTIDER";
            ImportNoException(originalDocumentPath, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadSequence)));

            // 11. Bad value in decoy field
            string[] valuesBadDecoys = new List<string>(values).ToArray();
            valuesBadDecoys[(int)PeakBoundaryImporter.Field.is_decoy] = 3.ToString(cult);
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadDecoys)),
                Resources.PeakBoundaryImporter_Import_The_decoy_value__0__on_line__1__is_invalid__must_be_0_or_1_);

            // 12. Import with bad sample throws exception
            string[] valuesSample =
            {
                "TPEVDDEALEK", "Q_2012_0918_RJ_13.raw", (4.0).ToString(cult), (3.5).ToString(cult),
                (4.5).ToString(cult), 2.ToString(cult), 0.ToString(cult), "badSample"
            };
            string headerRowSample = string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(valuesSample.Length));
            ImportThrowsException(originalDocumentPath, TextUtil.LineSeparate(headerRowSample, string.Join(csvSep, valuesSample)),
                Resources.PeakBoundaryImporter_Import_Sample__0__on_line__1__does_not_match_the_file__2__);

            // 13. Decoys, charge state, and sample missing ok
            var valuesFourFields = valuesSample.Take(5).ToArray();
            string headerFourFields = string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(valuesFourFields.Length));
            ImportNoException(originalDocumentPath, TextUtil.LineSeparate(headerFourFields, string.Join(csvSep, valuesFourFields)));

            // 14. Valid (charge state, fileName, peptide) combo that is not in document gets skipped
            string[] valuesBadCombo = new List<string>(values).ToArray();
            valuesBadCombo[(int)PeakBoundaryImporter.Field.charge] = (5).ToString(cult);
            ImportNoException(originalDocumentPath, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadCombo)));

            // Note: Importing with all 7 columns is tested as part of MProphetResultsHandlerTest

            // Now check a file that has peptide ID's, and see that they're properly ported
            var peptideIdPath = testFilesDir.GetTestPath("Template_MS1Filtering_1118_2011_3-2min.sky");
            var peakBoundaryFileId = testFilesDir.GetTestPath(isIntl
                ? "Template_MS1Filtering_1118_2011_3-2min_new_intl.tsv"
                : "Template_MS1Filtering_1118_2011_3-2min_new.tsv");
            DoFileImportTests(peptideIdPath, peakBoundaryFileId, _precursorChargeId,
                _idMinTime1, _idMaxTime1, _idIdentified1, _idAreas1, _peptidesId, 0);

            // 15. Decimal import format ok
            var valuesUnimod = new[]
            {
                "LGGLRPES[+" + string.Format("{0:F01}", 80.0) + "]PESLTSVSR", "100803_0005b_MCF7_TiTip3.wiff",
                (80.5).ToString(cult), (82.0).ToString(cult)
            };
            var headerUnimod = string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Where((s, i) =>
                i != (int)PeakBoundaryImporter.Field.apex_time).Take(valuesUnimod.Length));
            ImportNoException(peptideIdPath, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));

            // 16. Integer import format ok
            valuesUnimod[0] = "LGGLRPES[+80]PESLTSVSR";
            ImportNoException(peptideIdPath, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));

            // 17. Unimod import format ok
            valuesUnimod[0] = "LGGLRPES(UniMod:21)PESLTSVSR";
            ImportNoException(peptideIdPath, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));

            // 18. Strange capitalization OK
            valuesUnimod[0] = "LGGLRPES(uniMoD:21)PESLTSVSR";
            ImportNoException(peptideIdPath, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));

            // 18. Unimod with brackets OK
            valuesUnimod[0] = "LGGLRPES[uniMoD:21]PESLTSVSR";
            ImportNoException(peptideIdPath, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));
            // 19. Peak boundaries file does not exist
            TestPeakBoundariesNotFound(originalDocumentPath);
        }

        private static ReportSpec MakeReportSpec()
        {
            var specList = new ReportSpecList();
            var defaults = specList.GetDefaults();
            return defaults.First(spec => spec.Name == Resources.ReportSpecList_GetDefaults_Peak_Boundaries);
        }

        private static void ImportThrowsException(string documentPath, string importText, string message)
        {
            string output = ImportPeakBoundariesText(documentPath, importText);
            var errorLines = FindErrorLines(output).ToList();
            Assert.AreNotEqual(0, errorLines.Count);
            var lastLine = SplitLines(output).LastOrDefault();
            Assert.IsNotNull(lastLine);
            AssertEx.AreComparableStrings(message, lastLine);
        }

        private void TestPeakBoundariesNotFound(string documentPath)
        {
            var documentFolder = Path.GetDirectoryName(documentPath);
            Assert.IsNotNull(documentFolder);
            var peakBoundariesFile = Path.Combine(documentFolder, "FileDoesNotExist.txt");
            var outputFile = Path.Combine(documentFolder, "TestPeakBoundariesNotFound.sky");
            var output = ImportPeakBoundariesAndSaveAs(documentPath, peakBoundariesFile, outputFile);
            var errorLines = FindErrorLines(output).ToList();
            Assert.AreNotEqual(0, errorLines.Count);
            var lastLine = SplitLines(output).LastOrDefault();
            StringAssert.Contains(lastLine, peakBoundariesFile);
        }

        private static string ImportPeakBoundariesText(string documentPath, string importText)
        {
            var documentFolder = Path.GetDirectoryName(documentPath);
            Assert.IsNotNull(documentFolder);
            var peakBoundariesFile = Path.Combine(documentFolder, "PeakBoundaries.txt");
            File.WriteAllText(peakBoundariesFile, importText);
            var outputFile = Path.Combine(documentFolder, "ImportPeakBoundariesText.sky");
            return ImportPeakBoundariesAndSaveAs(documentPath, peakBoundariesFile, outputFile);
        }

        private static IEnumerable<string> SplitLines(string output)
        {
            var reader = new StringReader(output);
            string line;
            while (null != (line = reader.ReadLine()))
            {
                yield return line;
            }
        }

        private static IEnumerable<string> FindErrorLines(string output)
        {
            var prefixTuples = new[]
            {
                Tuple.Create(CommandStatusWriter.ERROR_MESSAGE_HINT, StringComparison.InvariantCulture),
                Tuple.Create(Resources.CommandStatusWriter_WriteLine_Error_, StringComparison.CurrentCulture)
            };
            foreach (var line in SplitLines(output))
            {
                foreach (var prefixTuple in prefixTuples)
                {
                    if (line.StartsWith(prefixTuple.Item1, prefixTuple.Item2))
                    {
                        yield return line.Substring(prefixTuple.Item1.Length).TrimStart();
                    }
                }
            }
        }

        private static void ImportNoException(string documentFilePath, string importText, bool isMinutes = true, bool removeMissing = false, bool changePeaks = true)
        {
            var output = ImportPeakBoundariesText(documentFilePath, importText);
            var errorLines = FindErrorLines(output).ToList();
            Assert.AreEqual(0, errorLines.Count);
        }

        private static SrmDocument ImportFileToDoc(string documentPath, string importFile)
        {
            var documentFolder = Path.GetDirectoryName(documentPath);
            Assert.IsNotNull(documentFolder);
            string outputFile = Path.Combine(documentFolder, "ImportFileToDoc.sky");
            return ImportFileToDocAndSaveAs(documentPath, importFile, outputFile);
        }

        private static SrmDocument ImportFileToDocAndSaveAs(string documentPath, string importFile, string outputFile)
        {
            ImportPeakBoundariesAndSaveAs(documentPath, importFile, outputFile);
            var serializer = new XmlSerializer(typeof(SrmDocument));
            using (var stream = File.OpenRead(outputFile))
            {
                return (SrmDocument)serializer.Deserialize(stream);
            }
        }

        private static string ImportPeakBoundariesAndSaveAs(string inputDocumentPath, string peakBoundariesFile,
            string outputDocumentPath)
        {
            return RunCommand("--in=" + inputDocumentPath, "--import-peak-boundaries=" + peakBoundariesFile, "--out=" + outputDocumentPath);
        }

        public void ReportToCsv(ReportSpec reportSpec, SrmDocument doc, string fileName)
        {
            CheckReportCompatibility.ReportToCsv(reportSpec, doc, fileName, LocalizationHelper.CurrentCulture);
        }

        private void DoFileImportTests(
            string documentPath, string importFile, int[] chargeList, double?[] minTime, double?[] maxTime, 
            PeakIdentification[] identified, double?[] peakAreas, string[] peptides, int fileId, string[] precursorMzs = null) 
        {
            SrmDocument docNew = ImportFileToDoc(documentPath, importFile);
            int i = 0;
            // Check peptide nodes are correct
            foreach (PeptideDocNode peptideNode in docNew.Peptides)
            {
                Assert.AreEqual(peptideNode.Peptide.Sequence, peptides[i]);
                ++i;
            }
            int j = 0;
            foreach (TransitionGroupDocNode groupNode in docNew.PeptideTransitionGroups)
            {
                var groupChromInfo = groupNode.ChromInfos.ToList()[fileId];
                // Make sure charge on each transition group is correct
                Assert.AreEqual(groupNode.TransitionGroup.PrecursorAdduct.AdductCharge, chargeList[j]);
                // Make sure imported retention time boundaries, including nulls, are correct
                AssertEx.AreEqualNullable(groupChromInfo.StartRetentionTime, minTime[j], RT_TOLERANCE);
                AssertEx.AreEqualNullable(groupChromInfo.EndRetentionTime, maxTime[j], RT_TOLERANCE);
                // Check that peak areas are updated correctly
                double peakArea = peakAreas[j] ?? 0;
                AssertEx.AreEqualNullable(groupChromInfo.Area, peakAreas[j], RT_TOLERANCE * peakArea);
                // Check that identified values are preserved/updated appropriately
                Assert.IsTrue(groupChromInfo.Identified == identified[j],
                    string.Format("No identification match for {0}  ({1})", groupNode.TransitionGroup.Peptide, j));
                var annotations = groupChromInfo.Annotations;
                if (precursorMzs != null)
                {
                    Assert.AreEqual(annotations.ListAnnotations().Length, 1);
                    Assert.AreEqual(annotations.GetAnnotation(annote), precursorMzs[j]);
                }
                else
                    Assert.AreEqual(annotations.ListAnnotations().Length, 0);
                ++j;
            }
        }
    }
}
