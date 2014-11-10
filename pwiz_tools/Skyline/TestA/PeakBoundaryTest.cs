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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class PeakBoundaryTest : AbstractUnitTest
    {
        const PeakIdentification FALSE = PeakIdentification.FALSE;
        const PeakIdentification TRUE = PeakIdentification.TRUE;
        
        const string TEST_ZIP_PATH = @"TestA\PeakBoundaryTest.zip";
        
        private readonly double?[] _tsvMinTime1 = { 34.13, 26.17, null, 53.3, null, 51.66 };
        private readonly double?[] _tsvMaxTime1 = { 34.72, 26.92, null, 54.06, null, 52.45 };
        private readonly PeakIdentification[] _tsvIdentified1 = {FALSE, FALSE, FALSE, FALSE, FALSE, FALSE};
        private readonly double?[] _tsvAreas1 = { 6.682e10,2.421e10,null,1.012e11,null,4.677e10 };

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

        private readonly string[] _precursorMzsUs = { "533.294964", "623.29589", "415.866352", "692.868631",
                                                  "462.248179", "634.355888"};
        private readonly string[] _precursorMzsIntl = { "533,294964", "623,29589", "415,866352", "692,868631",
                                                  "462,248179", "634,355888"};
        private const string annote = "PrecursorMz";

        private readonly double?[] _idMinTime1 = { 32.25,33.02,37.68,21.19,35.93, 33.85,29.29,31.55,37.21,27.02, 25.41,29.55,29.60,25.07,23.14, 29.11 };
        private readonly double?[] _idMaxTime1 = { 33.53,33.90,38.90,22.23,37.43, 35.35,30.86,31.66,37.81,28.68, 26.20,31.13,30.32,26.08,24.92, 29.58 };
        private readonly PeakIdentification[] _idIdentified1 = {
                                                                   FALSE, FALSE, FALSE, TRUE,  TRUE,
                                                                   FALSE, TRUE,  FALSE,  FALSE, TRUE,
                                                                   FALSE,  TRUE,  TRUE,  TRUE,  TRUE,
                                                                   TRUE
                                                               };

        private readonly double?[] _idAreas1 = {4060, 1927, 16954, 16314, 4188, 23038, 28701, 354,
                                               2063, 21784, 8775, 18019, 19579, 11013, 58116, 26262};

        private readonly string[] _peptides = { "VLVLDTDYK", "TPEVDDEALEK", "FFVAPFPEVFGK", "YLGYLEQLLR" };
        private readonly string[] _peptidesId ={ "LGGLRPESPESLTSVSR", "ALVEFESNPEETREPGSPPSVQR", "YGPADVEDTTGSGATDSKDDDDIDLFGSDDEEESEEAKR",
                                                    "ESEDKPEIEDVGSDEEEEKKDGDK", "GVVDSEDLPLNISR", "DMESPTKLDVTLAK",
                                                    "VGSLDNVGHLPAGGAVK","KTGSYGALAEITASK","TGSYGALAEITASK","IVRGDQPAASGDSDDDEPPPLPR",
                                                    "LLKEGEEPTVYSDEEEPKDESAR","KQITMEELVR","SSSVGSSSSYPISPAVSR",
                                                    "EKTPELPEPSVK", "VPKPEPIPEPKEPSPEK", "KETESEAEDNLDDLEK"};

        private readonly int[] _precursorCharge = { 2, 2, 3, 2, 3, 2 };
        private readonly int[] _precursorChargeId = {3,3,4,4,2,2,3,3,2,3,4,2,2,2,3,3};
        private const double RT_TOLERANCE = 0.02;

        /// <summary>
        /// Tests File > Import > Peak Boundaries support
        /// </summary>
        [TestMethod]
        public void TestImportPeakBoundary()
        {
            // Load the SRM document and relevant files            
            var testFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            bool isIntl = (TextUtil.CsvSeparator != TextUtil.SEPARATOR_CSV);
            var precursorMzs = isIntl ? _precursorMzsIntl : _precursorMzsUs;
            var peakBoundaryFileTsv = testFilesDir.GetTestPath(isIntl
                                                                   ? "PeakBoundaryTsvIntl.tsv"
                                                                   : "PeakBoundaryTsv.tsv");
            var peakBoundaryFileCsv = testFilesDir.GetTestPath(isIntl
                                                                   ? "PeakBoundaryIntl.csv"
                                                                   : "PeakBoundaryUS.csv");
            var peakBoundaryDoc = testFilesDir.GetTestPath("Chrom05.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(peakBoundaryDoc);

            // Load an empty doc, so that we can make a change and 
            // cause the .skyd to be loaded
            var docContainer = new ResultsTestDocumentContainer(null, peakBoundaryDoc);
            docContainer.SetDocument(doc, null, true);
            docContainer.AssertComplete();
            SrmDocument docResults = docContainer.Document;
            // Test Tsv import, looking at first .raw file
            DoFileImportTests(docResults, peakBoundaryFileTsv, _precursorCharge,
                _tsvMinTime1, _tsvMaxTime1, _tsvIdentified1, _tsvAreas1, _peptides, 0, precursorMzs, annote);
            // Test Tsv import, looking at second .raw file
            DoFileImportTests(docResults, peakBoundaryFileTsv, _precursorCharge,
                _tsvMinTime2, _tsvMaxTime2, _tsvIdentified2, _tsvAreas2, _peptides, 1, precursorMzs, annote);

            // Test Csv import for local format
            DoFileImportTests(docResults, peakBoundaryFileCsv, _precursorCharge,
                _csvMinTime1, _csvMaxTime1, _csvIdentified1, _csvAreas1, _peptides, 0, precursorMzs, annote);
            DoFileImportTests(docResults, peakBoundaryFileCsv, _precursorCharge,
                _csvMinTime2, _csvMaxTime2, _csvIdentified2, _csvAreas2, _peptides, 1, precursorMzs, annote);

            // Test that importing same file twice leads to no change to document the second time
            var docNew = ImportFileToDoc(docResults, peakBoundaryFileTsv);
            var docNewSame = ImportFileToDoc(docNew, peakBoundaryFileTsv);
            Assert.AreSame(docNew, docNewSame);
            Assert.AreNotSame(docNew, docResults);

            // Test that exporting peak boundaries and then importing them leads to no change
            string peakBoundaryExport = testFilesDir.GetTestPath("TestRoundTrip.csv");
            ReportSpec reportSpec = MakeReportSpec();
            ReportToCsv(reportSpec, docNew, peakBoundaryExport);
            var docRoundTrip = ImportFileToDoc(docNew, peakBoundaryExport);
            Assert.AreSame(docNew, docRoundTrip);


            var cult = LocalizationHelper.CurrentCulture;
            var cultI = CultureInfo.InvariantCulture;
            // 1. Empty file - 
            ImportThrowsException(docResults, string.Empty,
                Resources.PeakBoundaryImporter_Import_Failed_to_read_the_first_line_of_the_file);

            // 2. No separator in first line
            ImportThrowsException(docResults, "No-valid-separators",
                TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV
                    ? Resources.PeakBoundaryImporter_DetermineCorrectSeparator_The_first_line_does_not_contain_any_of_the_possible_separators_comma__tab_or_space_
                    : Resources.PeakBoundaryImporter_DetermineCorrectSeparator_The_first_line_does_not_contain_any_of_the_possible_separators_semicolon__tab_or_space_);
            
            // 3. Missing field names
            string csvSep = TextUtil.CsvSeparator.ToString(cultI);
            string spaceSep = TextUtil.SEPARATOR_SPACE.ToString(cultI);
            ImportThrowsException(docResults, string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(3).ToArray()),
                Resources.PeakBoundaryImporter_Import_Failed_to_find_the_necessary_headers__0__in_the_first_line);

            string headerRow = string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(6));
            string headerRowSpaced = string.Join(spaceSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(6));
            string[] values =
            {
                "TPEVDDEALEK", "Q_2012_0918_RJ_13.raw", (3.5).ToString(cult), (4.5).ToString(cult), 2.ToString(cult), 0.ToString(cult)
            };
            
            // 4. Mismatched field count
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRow, string.Join(spaceSep, values)),
                Resources.PeakBoundaryImporter_Import_Line__0__field_count__1__differs_from_the_first_line__which_has__2_);
            
            // 5. Invalid charge state
            string[] valuesBadCharge = new List<string>(values).ToArray();
            valuesBadCharge[(int) PeakBoundaryImporter.Field.charge] = (3.5).ToString(cult);
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadCharge)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_charge_state_);
            valuesBadCharge[(int) PeakBoundaryImporter.Field.charge] = TextUtil.EXCEL_NA;
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadCharge)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_charge_state_);

            // 6. Invalid start time
            string[] valuesBadTime = new List<string>(values).ToArray();
            valuesBadTime[(int) PeakBoundaryImporter.Field.start_time] = "bad";
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_);
            valuesBadTime[(int)PeakBoundaryImporter.Field.end_time] = "bad";
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_);

            // But ok if not adjusting peaks
            ImportNoException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)), true, false, false);

            // 7. Invalid end time
            valuesBadTime[(int) PeakBoundaryImporter.Field.start_time] =
                values[(int) PeakBoundaryImporter.Field.start_time];
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_end_time_);

            // But ok if not adjusting peaks
            ImportNoException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)), true, false, false);

            // #N/A in times ok
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] =
                valuesBadTime[(int)PeakBoundaryImporter.Field.end_time] = TextUtil.EXCEL_NA;
            ImportNoException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)));
            // If only start time #N/A throws exception
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] = (3.5).ToString(cult);
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_Missing_end_time_on_line__0_);
            // If only end time #N/A throws exception
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] = TextUtil.EXCEL_NA;
            valuesBadTime[(int)PeakBoundaryImporter.Field.end_time] = (3.5).ToString(cult);
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_Missing_start_time_on_line__0_);
            // Empty times throws exception
            valuesBadTime[(int)PeakBoundaryImporter.Field.start_time] =
                valuesBadTime[(int)PeakBoundaryImporter.Field.end_time] = string.Empty;
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadTime)),
                Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_);
            
            // 8. Not imported file gets skipped
            string[] valuesBadFile = new List<string>(values).ToArray();
            valuesBadFile[(int) PeakBoundaryImporter.Field.filename] = "Q_2012_0918_RJ_15.raw";
            ImportNoException(docResults, TextUtil.LineSeparate(headerRowSpaced, string.Join(spaceSep, valuesBadFile)));

            // 9. Unknown modification state gets skipped
            string[] valuesBadSequence = new List<string>(values).ToArray();
            valuesBadSequence[(int)PeakBoundaryImporter.Field.modified_peptide] = "T[+80]PEVDDEALEK";
            ImportNoException(docResults, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadSequence)));

            // 10. Unknown peptide sequence gets skipped
            valuesBadSequence[(int)PeakBoundaryImporter.Field.modified_peptide] = "PEPTIDER";
            ImportNoException(docResults, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadSequence)));

            // 11. Bad value in decoy field
            string[] valuesBadDecoys = new List<string>(values).ToArray();
            valuesBadDecoys[(int)PeakBoundaryImporter.Field.is_decoy] = 3.ToString(cult);
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadDecoys)),
                Resources.PeakBoundaryImporter_Import_The_decoy_value__0__on_line__1__is_invalid__must_be_0_or_1_);

            // 12. Import with bad sample throws exception
            string[] valuesSample =
            {
                "TPEVDDEALEK", "Q_2012_0918_RJ_13.raw", (3.5).ToString(cult), (4.5).ToString(cult), 2.ToString(cult), 0.ToString(cult), "badSample"
            };
            string headerRowSample = string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES);
            ImportThrowsException(docResults, TextUtil.LineSeparate(headerRowSample, string.Join(csvSep, valuesSample)),
                Resources.PeakBoundaryImporter_Import_Sample__0__on_line__1__does_not_match_the_file__2__);

            // 13. Decoys, charge state, and sample missing ok
            var valuesFourFields = valuesSample.Take(4);
            string headerFourFields = string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(4));
            ImportNoException(docResults, TextUtil.LineSeparate(headerFourFields, string.Join(csvSep, valuesFourFields)));

            // 14. Valid (charge state, fileName, peptide) combo that is not in document gets skipped
            string[] valuesBadCombo = new List<string>(values).ToArray();
            valuesBadCombo[(int) PeakBoundaryImporter.Field.charge] = (5).ToString(cult);
            ImportNoException(docResults, TextUtil.LineSeparate(headerRow, string.Join(csvSep, valuesBadCombo)));

            // Note: Importing with all 7 columns is tested as part of MProphetResultsHandlerTest

            // Release open streams
            docContainer.Release();

            // Now check a file that has peptide ID's, and see that they're properly ported
            var peptideIdPath = testFilesDir.GetTestPath("Template_MS1Filtering_1118_2011_3-2min.sky");
            SrmDocument docId = ResultsUtil.DeserializeDocument(peptideIdPath);
            docId = docId.ChangeSettings(docId.Settings.ChangePeptideLibraries(libraries =>
                {
                    var lib = libraries.Libraries[0];
                    return libraries.ChangeLibrarySpecs(new LibrarySpec[]
                        {
                            new BiblioSpecLiteSpec(lib.Name, testFilesDir.GetTestPath(lib.FileNameHint))
                        });
                }));

            var docContainerId = new ResultsTestDocumentContainer(null, peptideIdPath);
            docContainerId.SetDocument(docId, null, true);
            docContainerId.AssertComplete();
            SrmDocument docResultsId = docContainerId.Document;
            var peakBoundaryFileId = testFilesDir.GetTestPath(isIntl
                                                                  ? "Template_MS1Filtering_1118_2011_3-2min_new_intl.tsv"
                                                                  : "Template_MS1Filtering_1118_2011_3-2min_new.tsv");
            DoFileImportTests(docResultsId, peakBoundaryFileId, _precursorChargeId,
                _idMinTime1, _idMaxTime1, _idIdentified1, _idAreas1, _peptidesId, 0);

            // 15. Decminal import format ok
            var headerUnimod = string.Join(csvSep, PeakBoundaryImporter.STANDARD_FIELD_NAMES.Take(4));
            var valuesUnimod =  new []
            {
                "LGGLRPES[+" + string.Format("{0:F01}", 80.0) + "]PESLTSVSR", "100803_0005b_MCF7_TiTip3.wiff", (80.5).ToString(cult), (82.0).ToString(cult)
            };
            ImportNoException(docResultsId, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));
            
            // 16. Integer import format ok
            valuesUnimod[0] = "LGGLRPES[+80]PESLTSVSR";
            ImportNoException(docResultsId, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));
            
            // 17. Unimod import format ok
            valuesUnimod[0] = "LGGLRPES(UniMod:21)PESLTSVSR";
            ImportNoException(docResultsId, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));

            // 18. Strange capitalizations OK
            valuesUnimod[0] = "LGGLRPES(uniMoD:21)PESLTSVSR";
            ImportNoException(docResultsId, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));

            // 18. Unimod with brackets OK
            valuesUnimod[0] = "LGGLRPES[uniMoD:21]PESLTSVSR";
            ImportNoException(docResultsId, TextUtil.LineSeparate(headerUnimod, string.Join(csvSep, valuesUnimod)));
            
            // Release open streams
            docContainerId.Release();
        }

        private static ReportSpec MakeReportSpec()
        {
            var specList = new ReportSpecList();
            var defaults = specList.GetDefaults();
            return defaults.First(spec => spec.Name == Resources.ReportSpecList_GetDefaults_Peak_Boundaries);
        }

        private static void ImportThrowsException(SrmDocument docResults, string importText, string message, bool isMinutes = true, bool removeMissing = false, bool changePeaks = true)
        {
            var peakBoundaryImporter = new PeakBoundaryImporter(docResults);
            using (var readerPeakBoundaries = new StringReader(importText))
            {
                long lineCount = Helpers.CountLinesInString(importText);
                AssertEx.ThrowsException<IOException>(() => peakBoundaryImporter.Import(readerPeakBoundaries, null, lineCount, isMinutes, removeMissing, changePeaks), message);
            }
        }

        private static void ImportNoException(SrmDocument docResults, string importText, bool isMinutes = true, bool removeMissing = false, bool changePeaks = true)
        {
            var peakBoundaryImporter = new PeakBoundaryImporter(docResults);
            using (var readerPeakBoundaries = new StringReader(importText))
            {
                long lineCount = Helpers.CountLinesInString(importText);
                AssertEx.NoExceptionThrown<Exception>(() => peakBoundaryImporter.Import(readerPeakBoundaries, null, lineCount, isMinutes, removeMissing, changePeaks));
            }
        }

        private static SrmDocument ImportFileToDoc(SrmDocument docOld, string importFile)
        {
            var peakBoundaryImporter = new PeakBoundaryImporter(docOld);
            long lineCount = Helpers.CountLinesInFile(importFile);
            SrmDocument docNew = peakBoundaryImporter.Import(importFile, null, lineCount);
            return docNew;
        }

        public void ReportToCsv(ReportSpec reportSpec, SrmDocument doc, string fileName)
        {
            Report report = Report.Load(reportSpec);
            using (var saver = new FileSaver(fileName))
            using (var writer = new StreamWriter(saver.SafeName))
            using (var database = new Database(doc.Settings))
            {
                database.AddSrmDocument(doc);
                var resultSet = report.Execute(database);
                char separator = TextUtil.CsvSeparator;
                ResultSet.WriteReportHelper(resultSet, separator, writer, LocalizationHelper.CurrentCulture);
                writer.Flush();
                writer.Close();
                saver.Commit();
            }
        }

        private void DoFileImportTests(SrmDocument docResults,
                                       string importFile,
                                       int[] chargeList,
                                       double?[] minTime,
                                       double?[] maxTime,
                                       PeakIdentification[] identified,
                                       double?[] peakAreas,
                                       string[] peptides,
                                       int fileId,
                                       string[] precursorMzs = null,
                                       string annotationName = null)
        {
            SrmDocument docNew = ImportFileToDoc(docResults, importFile);
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
                Assert.AreEqual(groupNode.TransitionGroup.PrecursorCharge, chargeList[j]);
                // Make sure imported retention time boundaries, including nulls, are correct
                Assert.IsTrue(ApproxEqualNullable(groupChromInfo.StartRetentionTime, minTime[j], RT_TOLERANCE));
                Assert.IsTrue(ApproxEqualNullable(groupChromInfo.EndRetentionTime, maxTime[j], RT_TOLERANCE));
                // Check that peak areas are updated correctly
                double peakArea = peakAreas[j] ?? 0;
                Assert.IsTrue(ApproxEqualNullable(groupChromInfo.Area, peakAreas[j], RT_TOLERANCE*peakArea));
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

        private bool ApproxEqualNullable(double? a, double? b, double tol)
        {
            return a == b || (a != null && b != null && Math.Abs((double)a - (double)b) < tol);
        }
    }
}
