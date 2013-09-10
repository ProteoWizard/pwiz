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
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReintegrateDlgTest : AbstractFunctionalTest
    {
        private const string REPORT_EXPECTED = "ReportExpected.csv";
        private const string REPORT_ACTUAL = "ReportActual.csv";

        [TestMethod]
        public void TestReintegrateDlg()
        {
            TestFilesZip = @"TestFunctional\ReintegrateDlgTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // 1. Document with no imported results gives error message
            ConfirmErrorOnOpen("ChromNoFiles.sky", Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_imported_results_);
            // 2. Document with no peptides gives error message
            ConfirmErrorOnOpen("ChromNoPeptides.sky", Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_peptides_in_order_to_reintegrate_chromatograms_);
            // 3. Document with no trained model gives error message
            ConfirmErrorOnOpen("ChromNoModel.sky", Resources.SkylineWindow_ShowReintegrateDialog_Reintegration_of_results_requires_a_trained_peak_scoring_model_);

            // 4. No value for q cutoff gives error message
            var document = TestFilesDir.GetTestPath("MProphetGold-trained.sky");
            RunUI(() => SkylineWindow.OpenFile(document));
            WaitForDocumentLoaded();
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunDlg<MessageDlg>(reintegrateDlg.OkDialog, messageBox =>
            {
                AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, messageBox.Message);
                messageBox.OkDialog();
            });

            // 6. Negative value for q cutoff gives error message
            RunUI(() => reintegrateDlg.Cutoff = -1.0);
            RunDlg<MessageDlg>(reintegrateDlg.OkDialog, messageBox =>
            {
                AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, messageBox.Message);
                messageBox.OkDialog();
            });

            // Test export gives same result as through non-UI
            RunUI(() =>
                {
                    reintegrateDlg.Cutoff = 0.01;
                    reintegrateDlg.OkDialog();
                });
            WaitForClosedForm(reintegrateDlg);
            RunUI(() =>
                {
                    var reportSpec = MakeReportSpec();
                    string docNewActual = TestFilesDir.GetTestPath(REPORT_ACTUAL);
                    string docNewExpected = TestFilesDir.GetTestPathLocale(REPORT_EXPECTED);
                    ReportToCsv(reportSpec, SkylineWindow.DocumentUI, docNewActual, CultureInfo.CurrentCulture);
                    AssertEx.FileEquals(docNewActual, docNewExpected);
                });

        }

        private void ConfirmErrorOnOpen(string file, string message)
        {
            var document = TestFilesDir.GetTestPath(file);
            RunUI(() => SkylineWindow.OpenFile(document));
            WaitForDocumentLoaded();
            RunDlg<MessageDlg>(SkylineWindow.ShowReintegrateDialog, messageBox =>
            {
                Assert.AreEqual(message, messageBox.Message);
                messageBox.OkDialog();
            });
        }

        private ReportSpec MakeReportSpec()
        {
            Type tableTran = typeof(DbTransition);
            Type tableTranRes = typeof(DbTransitionResult);
            return new ReportSpec("PeakBoundaries", new QueryDef
            {
                Select = new[]
                        {
                            new ReportColumn(tableTran, "Precursor", "Charge"),
                            new ReportColumn(tableTranRes, "ResultFile", "FileName"),
                            new ReportColumn(tableTranRes, "PrecursorResult", "MinStartTime"),
                            new ReportColumn(tableTranRes, "PrecursorResult", "MaxEndTime"),
                            new ReportColumn(tableTran, "Precursor", "Peptide", "ModifiedSequence"),
                        }
            });

        }

        public void ReportToCsv(ReportSpec reportSpec, SrmDocument doc, string fileName, CultureInfo cultureInfo)
        {
            Report report = Report.Load(reportSpec);
            using (var saver = new FileSaver(fileName))
            using (var writer = new StreamWriter(saver.SafeName))
            using (var database = new Database(doc.Settings))
            {
                database.AddSrmDocument(doc);
                var resultSet = report.Execute(database);
                char separator = TextUtil.GetCsvSeparator(cultureInfo);
                ResultSet.WriteReportHelper(resultSet, separator, writer, cultureInfo);
                writer.Flush();
                writer.Close();
                saver.Commit();
            }
        }
    }
}
