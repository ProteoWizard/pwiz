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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Results;
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
        private const string REPORT_EXPECTED_ALL = "ReportExpectedAll.csv";
        private const string REPORT_ACTUAL = "ReportActual.csv";

        /// <summary>
        /// Set to true to regenerate the comparison files
        /// </summary>
        private bool IsSaveAll { get { return false; } }

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
            var save = TestSmallMolecules;
            TestSmallMolecules = false;  // Don't be adding nodes for this test
            ConfirmErrorOnOpen("ChromNoPeptides.sky", Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_targets_in_order_to_reintegrate_chromatograms_);
            TestSmallMolecules = save;
            // 3. Document with no trained model gives error message
            var documentNoModel = TestFilesDir.GetTestPath("ChromNoModel.sky");
            RunUI(() => SkylineWindow.OpenFile(documentNoModel));
            WaitForDocumentLoaded();
            var reintegrateDlgNoModel = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlgNoModel.ReintegrateAll = true);
            RunDlg<MessageDlg>(reintegrateDlgNoModel.OkDialog, messageBox =>
            {
                AssertEx.AreComparableStrings(Resources.ReintegrateDlg_OkDialog_You_must_train_and_select_a_model_in_order_to_reintegrate_peaks_, messageBox.Message);
                messageBox.OkDialog();
            });
            OkDialog(reintegrateDlgNoModel, reintegrateDlgNoModel.CancelDialog);

            // 4. No value for q cutoff gives error message
            var document = TestFilesDir.GetTestPath("MProphetGold-trained.sky");
            RunUI(() => SkylineWindow.OpenFile(document));
            WaitForDocumentLoaded();
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlg.ReintegrateAll = false);
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

            string docNewExpected = TestFilesDir.GetTestPathLocale(REPORT_EXPECTED);
            string docNewActual = TestFilesDir.GetTestPath(REPORT_ACTUAL);
            string docNewExpectedAll = TestFilesDir.GetTestPathLocale(REPORT_EXPECTED_ALL);
            var reportSpec = MakeReportSpec();
            // Test export gives same result as through non-UI
            RunUI(() =>
                {
                    reintegrateDlg.ReintegrateAll = false;
                    reintegrateDlg.AddAnnotation = false;
                    reintegrateDlg.Cutoff = 0.01;
                });
            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            RunUI(() =>
                {
                    ReportToCsv(reportSpec, SkylineWindow.DocumentUI, docNewActual, CultureInfo.CurrentCulture);
                    if (IsSaveAll)
                    {
                        // For regenerating expected files if things change
                        ReportToCsv(reportSpec, SkylineWindow.DocumentUI, TestFilesDir.GetTestPath(REPORT_EXPECTED), CultureInfo.GetCultureInfo("en-US"));
                        ReportToCsv(reportSpec, SkylineWindow.DocumentUI, TestFilesDir.GetTestPathIntl(REPORT_EXPECTED), CultureInfo.GetCultureInfo("fr-FR"));
                    }
                    AssertEx.FileEquals(docNewActual, docNewExpected);
                });
            // No annotations
            Assert.IsFalse(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Any());
            
            // Moving a peak, then reintegrating causes peak to return, but only if manual override is checked
            
            double? startNew = 41.0;
            double? endNew = 42.0;
            double? startOld, endOld;
            string nameSet = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[0].Name;
            IdentityPath groupPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
            var filePath = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].FilePath;
            CheckTimes(groupPath, 0, 0, out startOld, out endOld);
            RunUI(() => 
                SkylineWindow.ModifyDocument(null,
                    doc => doc.ChangePeak(groupPath, nameSet, filePath,
                        null, startNew, endNew, UserSet.TRUE, null, false)));
            var reintegrateDlgManual = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
            {
                reintegrateDlgManual.ReintegrateAll = true;
                reintegrateDlgManual.OverwriteManual = false;
            });
            OkDialog(reintegrateDlgManual, reintegrateDlgManual.OkDialog);
            // No annotations
            Assert.IsFalse(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Any());
            CheckRoundTrip(); // Verify that this all serializes properly

            // Peak Boundaries stay where they are when manual override is off
            double? startTime, endTime;
            CheckTimes(groupPath, 0, 0, out startTime, out endTime);
            AssertEx.AreEqualNullable(startNew, startTime, 1e-2);
            AssertEx.AreEqualNullable(endNew, endTime, 1e-2);

            var reintegrateDlgOverride = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
            {
                reintegrateDlgOverride.ReintegrateAll = true;
                reintegrateDlgOverride.OverwriteManual = true;
            });
            OkDialog(reintegrateDlgOverride, reintegrateDlgOverride.OkDialog);
            // No annotations
            Assert.IsFalse(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Any());
            CheckRoundTrip(); // Verify that this all serializes properly
            
            // Peak Boundaries move back when manual override is turned on
            CheckTimes(groupPath, 0, 0, out startTime, out endTime);
            AssertEx.AreEqualNullable(startOld, startTime, 1e-3);
            AssertEx.AreEqualNullable(endOld, endTime, 1e-3);

            // Checking "Reintegrate All" radio button has same effect as choosing q=1.1
            var reintegrateDlgAll = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
            {
                reintegrateDlgAll.ReintegrateAll = true;
                reintegrateDlgAll.OverwriteManual = true;
            });
            OkDialog(reintegrateDlgAll, reintegrateDlgAll.OkDialog);
            RunUI(() =>
            {
                ReportToCsv(reportSpec, SkylineWindow.DocumentUI, docNewActual, CultureInfo.CurrentCulture);
                if (IsSaveAll)
                {
                    // For regenerating expected files if things change
                    ReportToCsv(reportSpec, SkylineWindow.DocumentUI, TestFilesDir.GetTestPath(REPORT_EXPECTED_ALL), CultureInfo.GetCultureInfo("en-US"));
                    ReportToCsv(reportSpec, SkylineWindow.DocumentUI, TestFilesDir.GetTestPathIntl(REPORT_EXPECTED_ALL), CultureInfo.GetCultureInfo("fr-FR"));
                }
                AssertEx.FileEquals(docNewActual, docNewExpectedAll);
            });
            // No annotations
            Assert.IsFalse(SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Any());
            CheckRoundTrip(); // Verify that this all serializes properly
            var reintegrateDlgCutoff = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
            {
                reintegrateDlgCutoff.ReintegrateAll = false;
                reintegrateDlgCutoff.Cutoff = 1.0;
                reintegrateDlgCutoff.OverwriteManual = true;
                reintegrateDlgCutoff.OkDialog();
            });
            WaitForClosedForm(reintegrateDlgCutoff);
            RunUI(() =>
            {
                ReportToCsv(reportSpec, SkylineWindow.DocumentUI, docNewActual, CultureInfo.CurrentCulture);
                AssertEx.FileEquals(docNewActual, docNewExpectedAll);
            });
            CheckRoundTrip(); // Verify that this all serializes properly

            // This time annotations are added
            var reintegrateDlgAnnotations = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
            {
                reintegrateDlgAnnotations.ReintegrateAll = true;
                reintegrateDlgAnnotations.AddAnnotation = true;
            });
            OkDialog(reintegrateDlgAnnotations, reintegrateDlgAnnotations.OkDialog);
            CheckRoundTrip(); // Verify that this all serializes properly
            // Check annotation def is added
            var annotationDefs = SkylineWindow.Document.Settings.DataSettings.AnnotationDefs;
            Assert.AreEqual(2, annotationDefs.Count);
            Assert.AreEqual(1, annotationDefs[0].AnnotationTargets.Count);
            Assert.AreEqual(AnnotationDef.AnnotationTarget.precursor_result, annotationDefs[0].AnnotationTargets.First());
            Assert.AreEqual(AnnotationDef.AnnotationType.number, annotationDefs[0].Type);
            Assert.AreEqual(MProphetResultsHandler.AnnotationName, annotationDefs[0].Name);
            Assert.AreEqual(1, annotationDefs[1].AnnotationTargets.Count);
            Assert.AreEqual(AnnotationDef.AnnotationTarget.precursor_result, annotationDefs[1].AnnotationTargets.First());
            Assert.AreEqual(AnnotationDef.AnnotationType.number, annotationDefs[1].Type);
            Assert.AreEqual(MProphetResultsHandler.MAnnotationName, annotationDefs[1].Name);
            // Check annotations are added
            foreach (var nodeGroup in SkylineWindow.Document.MoleculeTransitionGroups)
            {
                foreach (var chromInfo in nodeGroup.ChromInfos)
                {
                    var annotations = chromInfo.Annotations.ListAnnotations();
                    if (nodeGroup.IsDecoy)
                    {
                        Assert.AreEqual(1, annotations.Length);
                        Assert.AreEqual(MProphetResultsHandler.MAnnotationName, annotations[0].Key);
                    }
                    else
                    {
                        Assert.AreEqual(2, annotations.Length);
                        Assert.AreEqual(MProphetResultsHandler.AnnotationName, annotations[0].Key);
                        Assert.AreEqual(MProphetResultsHandler.MAnnotationName, annotations[1].Key);
                    }

                }
            }
            // Clear annotations
            var reintegrateDlgAnnotationsRemove = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
            {
                reintegrateDlgAnnotationsRemove.ReintegrateAll = true;
                reintegrateDlgAnnotationsRemove.AddAnnotation = false;
            });
            OkDialog(reintegrateDlgAnnotationsRemove, reintegrateDlgAnnotationsRemove.OkDialog);
            var annotationDefsRemove = SkylineWindow.Document.Settings.DataSettings.AnnotationDefs;
            Assert.AreEqual(annotationDefsRemove.Count, 0);
            CheckRoundTrip(); // Verify that this all serializes properly
        }

        private void CheckRoundTrip()
        {
            // Verify that document roundtrips properly as small molecule
            var refine = new RefinementSettings();
            var doc = refine.ConvertToSmallMolecules(SkylineWindow.Document, ignoreDecoys: true); 
            AssertEx.RoundTrip(doc);
            // Verify that document roundtrips properly
            AssertEx.RoundTrip(SkylineWindow.Document);
        }

        private static void CheckTimes(IdentityPath groupPath, int file, int replicate, out double? startTime, out double? endTime)
        {
            var groupNode = (TransitionGroupDocNode)SkylineWindow.Document.FindNode(groupPath);
            var groupChromInfo = groupNode.Results[file][replicate];
            startTime = groupChromInfo.StartRetentionTime;
            endTime = groupChromInfo.EndRetentionTime;
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
