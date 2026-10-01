/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{

    [TestClass]
    public class AlphapeptdeepBuildLibraryTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// When true the test copies the libraries it predicts over the expected ones in the test files
        /// folder, for updating AlphapeptdeepBuildLibraryTest.zip.
        /// </summary>
        protected override bool IsRecordMode => false;

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestAlphaPeptDeepBuildLibrary()
        {
            TestFilesZip = "TestPerf/AlphapeptdeepBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        private string LibraryPathWithoutIrt =>
            TestFilesDir.GetTestPath("LibraryWithoutIrt.blib");

        private string LibraryPathWithIrt =>
            TestFilesDir.GetTestPath("LibraryWithIrt.blib");

        protected override void DoTest()
        {
            TestEmptyDocumentMessage();

            RunUI(() => OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky")));

            const string answerWithoutIrt = "without_iRT/predict_transformed.speclib.tsv";
            const string libraryWithoutIrt = "AlphaPeptDeepLibraryWithoutIrt";

            const string libraryWithIrt = "AlphaPeptDeepLibraryWithIrt";
            const string answerWithIrt = "with_iRT/predict_transformed.speclib.tsv";

            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            AlphapeptdeepBuildLibrary(peptideSettings, libraryWithIrt, LibraryPathWithIrt, answerWithIrt,
                IrtStandard.BIOGNOSYS_11);

            AlphapeptdeepBuildLibrary(peptideSettings, libraryWithoutIrt, LibraryPathWithoutIrt, answerWithoutIrt);

            OkDialog(peptideSettings, peptideSettings.OkDialog);

            var addRtStdDlg = WaitForOpenForm<AddIrtStandardsToDocumentDlg>();
            OkDialog(addRtStdDlg, addRtStdDlg.CancelDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithIrt);
            });

            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);

            var saveChangesDlg =
                ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.NewDocument(), WAIT_TIME);
            AssertEx.AreComparableStrings(SkylineResources.SkylineWindow_CheckSaveDocument_Do_you_want_to_save_changes,
                saveChangesDlg.Message);
            OkDialog(saveChangesDlg, saveChangesDlg.ClickNo);
            // The new document loads the libraries in the default settings in the background. Let that
            // finish, or CheckForFileLocks moves the test folder while a library is still being opened.
            WaitForDocumentLoaded();

            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);
        }

        private void TestEmptyDocumentMessage()
        {
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);

            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = "No peptides prediction";
                buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;

                // Focusing the radio button checks it, as clicking it does, and showing the AlphaPeptDeep
                // page must not take the focus from it
                var radioAlpha = buildLibraryDlg.Controls.Find(@"radioAlphaSource", true).Single();
                radioAlpha.Focus();
                Assert.IsTrue(buildLibraryDlg.AlphaPeptDeep);
                Assert.AreSame(radioAlpha, buildLibraryDlg.ActiveControl);
            });

            RunDlg<MessageDlg>(buildLibraryDlg.OkWizardPage, dlg =>
            {
                Assert.AreEqual(SettingsUIResources.BuildLibraryDlg_CreateAlphaBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_AlphaPeptDeep_predictions_,
                    dlg.Message);
                dlg.OkDialog();
            });

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);
        }

        /// <summary>
        /// Test goes through building of a Library by AlphaPeptDeep with or without iRT
        /// </summary>
        /// <param name="peptideSettings">Open PeptideSettingsUI Dialog object</param>
        /// <param name="libraryName">Name of the library to build</param>
        /// <param name="libraryPath">Path of the library to build</param>
        /// <param name="answerFile">Path to library answersheet</param>
        /// <param name="iRTtype">iRT standard type</param>
        private void AlphapeptdeepBuildLibrary(PeptideSettingsUI peptideSettings, string libraryName,
            string libraryPath, string answerFile, IrtStandard iRTtype = null)
        {
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.AlphaPeptDeep = true;
                if (iRTtype != null)
                    buildLibraryDlg.IrtStandard = iRTtype;
            });

            RunUI(buildLibraryDlg.OkWizardPage);

            WaitForCondition(() => buildLibraryDlg.Builder != null);

            var alphaPeptDeepBuilder = (AlphapeptdeepLibraryBuilder) buildLibraryDlg.Builder;
            Assert.IsNotNull(alphaPeptDeepBuilder);
            string builtLibraryPath = alphaPeptDeepBuilder.TransformedOutputSpectraLibFilepath;

            var limitedModelAlert = WaitForOpenForm<AlertDlg>();
            Assert.AreEqual(string.Format(ModelResources.Alphapeptdeep_Warn_limited_modification, @"Phospho (ST)".Indent(1)), limitedModelAlert.Message);
            OkDialog(limitedModelAlert, limitedModelAlert.OkDialog);
            if (iRTtype != null)
            {
                VerifyAddIrts(WaitForOpenForm<AddIrtPeptidesDlg>());
                var recalibrateIrtDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                StringAssert.StartsWith(recalibrateIrtDlg.Message,
                    Resources
                        .LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_);
                OkDialog(recalibrateIrtDlg, recalibrateIrtDlg.ClickNo);
                var addRtPredDlg = WaitForOpenForm<AddRetentionTimePredictorDlg>();
                OkDialog(addRtPredDlg, addRtPredDlg.OkDialog);
            }

            WaitForClosedForm<BuildLibraryDlg>();
            WaitForCondition(() => File.Exists(builtLibraryPath));

            string answerPath = TestFilesDir.GetTestPath(answerFile);
            if (IsRecordMode)
            {
                File.Copy(builtLibraryPath, answerPath, true);
                Console.WriteLine(@"Recorded {0}", answerPath);
            }
            TestResultingLibByValues(answerPath, builtLibraryPath);
        }

        private static void VerifyAddIrts(AddIrtPeptidesDlg dlg)
        {
            RunUI(() =>
            {
                Assert.AreEqual(6, dlg.PeptidesCount);
                Assert.AreEqual(1, dlg.RunsConvertedCount); // Libraries now convert through internal alignment to single RT scale
                Assert.AreEqual(0, dlg.RunsFailedCount);
            });

            VerifyRegression(dlg, 0, true, 11, 0, 0);

            OkDialog(dlg, dlg.OkDialog);
        }

        private static void VerifyRegression(AddIrtPeptidesDlg dlg, int index, bool converted, int numPoints,
            int numMissing, int numOutliers)
        {
            RunUI(() => Assert.AreEqual(converted, dlg.IsConverted(index)));
            var regression = ShowDialog<GraphRegression>(() => dlg.ShowRegression(index));
            RunUI(() =>
            {
                Assert.AreEqual(1, regression.RegressionGraphDatas.Count);
                var data = regression.RegressionGraphDatas.First();
                Assert.IsTrue(data.XValues.Length == data.YValues.Length);
                Assert.AreEqual(numPoints, data.XValues.Length);
                Assert.AreEqual(numMissing, data.MissingIndices.Count);
                Assert.AreEqual(numOutliers, data.OutlierIndices.Count);
            });
            OkDialog(regression, regression.CloseDialog);
        }

        private void TestResultingLibByValues(string answer, string product)
        {
            var sortFields = new List<(int FieldIndex, bool IsAscending)>
            {
                (0, true),   // ModifiedPeptideSequence
                (1, true),   // PrecursorCharge
                (6, true),   // FragmentType
                (9, true),   // FragmentCharge
                // Fragments are written in predicted-intensity order, and the predicted intensities
                // differ at the ~7th significant figure across machines and between the CPU and the
                // GPU, which flips the order of near-tied fragments. Sort by the fragment's identity
                // (series number + loss type) so the row order is deterministic and the field
                // comparison aligns the same fragment in both files; the tiny per-value drift is then
                // absorbed by the comparison tolerance.
                (10, true),  // FragmentSeriesNumber
                (11, true)   // FragmentLossType
            };
            var product_sorted = product + ".sorted";
            var answer_sorted = answer + ".sorted";

            DelimitedFileSorter.SortDelimitedFile(
                inputFilePath: product,
                outputFilePath: product_sorted,
                delimiter: TextUtil.SEPARATOR_TSV,
                sortFields: sortFields,
                hasHeader: true);

            DelimitedFileSorter.SortDelimitedFile(
                inputFilePath: answer,
                outputFilePath: answer_sorted,
                delimiter: TextUtil.SEPARATOR_TSV,
                sortFields: sortFields,
                hasHeader: true);

            using (var answerReader = new StreamReader(answer_sorted))
            using (var productReader = new StreamReader(product_sorted))
            {
                AssertEx.FieldsEqual(answerReader, productReader, 12, null, true, 0, 1);
            }
        }
    }
}

public class DelimitedFileSorter
{
    public static void SortDelimitedFile(
        string inputFilePath,
        string outputFilePath,
        char delimiter,
        List<(int FieldIndex, bool IsAscending)> sortFields,
        bool hasHeader = true)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(inputFilePath) || !File.Exists(inputFilePath))
                throw new ArgumentException("Input file does not exist or is invalid.");
            if (string.IsNullOrEmpty(outputFilePath))
                throw new ArgumentException("Output file path is invalid.");
            if (sortFields == null || !sortFields.Any())
                throw new ArgumentException("At least one sort field must be specified.");
            if (sortFields.Any(sf => sf.FieldIndex < 0))
                throw new ArgumentException("Field indices must be non-negative.");

            // Read all lines
            var lines = File.ReadAllLines(inputFilePath);
            if (lines.Length == 0)
                throw new InvalidOperationException("Input file is empty.");

            // Store header if present
            string header = hasHeader ? lines[0] : null;
            var dataLines = hasHeader ? lines.Skip(1).ToList() : lines.ToList();

            // Sort data
            var sortedLines = Enumerable.OrderBy(
                dataLines.Select(line => line.Split(delimiter)),
                fields => fields, // Use fields as the key
                new FieldComparer(sortFields)).Select(fields => string.Join(delimiter.ToString(), fields));

            // Write to output file
            using (var writer = new StreamWriter(outputFilePath))
            {
                if (hasHeader)
                    writer.WriteLine(header);
                foreach (var line in sortedLines)
                    writer.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sorting file: {ex}");
            throw;
        }
    }

    private class FieldComparer : IComparer<string[]>
    {
        private readonly List<(int FieldIndex, bool IsAscending)> _sortFields;

        public FieldComparer(List<(int FieldIndex, bool IsAscending)> sortFields)
        {
            _sortFields = sortFields;
        }

        public int Compare(string[] x, string[] y)
        {
            foreach ((int fieldIndex, bool isAscending) in _sortFields)
            {
                string xValue = x != null && fieldIndex < x.Length ? x[fieldIndex] : string.Empty;
                string yValue = y != null && fieldIndex < y.Length ? y[fieldIndex] : string.Empty;

                if (double.TryParse(xValue, out double xNum) && double.TryParse(yValue, out double yNum))
                {
                    int comparison = xNum.CompareTo(yNum);
                    if (comparison != 0)
                        return isAscending ? comparison : -comparison;
                }
                else
                {
                    int comparison = string.Compare(xValue, yValue, StringComparison.OrdinalIgnoreCase);
                    if (comparison != 0)
                        return isAscending ? comparison : -comparison;
                }
            }
            return 0;
        }
    }
}