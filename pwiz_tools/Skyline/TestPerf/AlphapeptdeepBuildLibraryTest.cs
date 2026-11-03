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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
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
            // Osprey predicts the library: a local build, or the latest TeamCity build downloaded
            OspreyTestUtil.RegisterOsprey(OspreyTestUtil.EnsureOsprey(TestContext));
            TestEmptyDocumentMessage();

            RunUI(() => OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky")));

            const string answerWithoutIrt = "without_iRT/LibraryWithoutIrt.blib";
            const string libraryWithoutIrt = "AlphaPeptDeepLibraryWithoutIrt";

            const string libraryWithIrt = "AlphaPeptDeepLibraryWithIrt";
            const string answerWithIrt = "with_iRT/LibraryWithIrt.blib";

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
            // The new document starts loading the libraries in the last-used settings, and the
            // library loader closes their files only after the loaded document is published
            WaitForDocumentLoaded();
            WaitForBackgroundLoaders();

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
                buildLibraryDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = null };

                // Focusing the radio button checks it, as clicking it does, and showing the AlphaPeptDeep
                // page must not take the focus from it
                var radioAlpha = buildLibraryDlg.Controls.Find(@"radioAlphaSource", true).Single();
                radioAlpha.Focus();
                Assert.IsTrue(buildLibraryDlg.Carafe);
                Assert.AreSame(radioAlpha, buildLibraryDlg.ActiveControl);
            });

            RunDlg<MessageDlg>(buildLibraryDlg.OkWizardPage, dlg =>
            {
                Assert.AreEqual(SettingsUIResources.BuildLibraryDlg_CreateCarafeBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_Carafe_predictions_,
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
                // Predict on the CPU, whatever GPU this computer has, so the library compares with the baselines
                buildLibraryDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = null };
                buildLibraryDlg.Carafe = true;
                if (iRTtype != null)
                    buildLibraryDlg.IrtStandard = iRTtype;
            });

            // The modification Carafe knows only in part is flagged as soon as the source is chosen,
            // and asked about before the form closes, so Cancel would return to it
            RunUI(() => Assert.AreEqual(
                string.Format(SettingsUIResources.BuildLibraryDlg_UpdateModificationWarning_Limited_or_no_support_for__0_, @"Phospho (ST)"),
                buildLibraryDlg.ModificationWarning));
            var limitedModelAlert = ShowDialog<AlertDlg>(buildLibraryDlg.OkWizardPage);
            Assert.AreEqual(TextUtil.LineSeparate(string.Format(ModelResources.Alphapeptdeep_Warn_limited_modification, @"Phospho (ST)".Indent(1)),
                Environment.NewLine, ModelResources.Alphapeptdeep_Warn_continue_building), limitedModelAlert.Message);
            OkDialog(limitedModelAlert, limitedModelAlert.OkDialog);

            WaitForCondition(() => buildLibraryDlg.Builder != null);

            var alphaPeptDeepBuilder = (CarafeLibraryBuilder) buildLibraryDlg.Builder;
            Assert.IsNotNull(alphaPeptDeepBuilder);
            // Osprey writes the library itself, moved over the final name only when it is complete
            string builtLibraryPath = alphaPeptDeepBuilder.LibrarySpec.FilePath;
            Assert.AreEqual(libraryPath, builtLibraryPath);
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
                // Every document peptide the models predicted, the phosphopeptide included: Chronologer,
                // Carafe's default RT model, encodes phosphorylation and was trained on it, so its RT is
                // kept, where the peptdeep TSV path zeroed it (the CCS, from the pretrained AlphaPeptDeep
                // model, is withheld for it - see CarafeSharp's ModelSupport)
                Assert.AreEqual(7, dlg.PeptidesCount);
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

        /// <summary>
        /// The built library against the recorded one, spectrum by spectrum: the same precursors,
        /// each with the same fragments (by m/z) at intensities within 1e-3 of each other, and
        /// retention times and CCS within tolerance. The predicted intensities differ at the ~7th
        /// significant figure across machines and between the CPU and the GPU, so the fragments
        /// are aligned by m/z, not by their intensity order in the library.
        /// </summary>
        private static void TestResultingLibByValues(string answer, string product)
        {
            var answerLibrary = LoadLibrary(answer);
            var productLibrary = LoadLibrary(product);
            try
            {
                var answerKeys = answerLibrary.Keys.ToList();
                CollectionAssert.AreEquivalent(answerKeys, productLibrary.Keys.ToList());
                foreach (var key in answerKeys)
                {
                    string label = key.ToString();
                    var expected = answerLibrary.GetSpectra(key, IsotopeLabelType.light, LibraryRedundancy.best).First();
                    var actual = productLibrary.GetSpectra(key, IsotopeLabelType.light, LibraryRedundancy.best).First();
                    Assert.AreEqual(expected.RetentionTime ?? 0, actual.RetentionTime ?? 0, 0.05, label);
                    Assert.AreEqual(expected.IonMobilityInfo?.CollisionalCrossSectionSqA ?? 0,
                        actual.IonMobilityInfo?.CollisionalCrossSectionSqA ?? 0, 1.0, label);
                    var expectedPeaks = expected.SpectrumPeaksInfo.Peaks.OrderBy(p => p.Mz).ToArray();
                    var actualPeaks = actual.SpectrumPeaksInfo.Peaks.OrderBy(p => p.Mz).ToArray();
                    Assert.AreEqual(expectedPeaks.Length, actualPeaks.Length, label);
                    for (int i = 0; i < expectedPeaks.Length; i++)
                    {
                        Assert.AreEqual(expectedPeaks[i].Mz, actualPeaks[i].Mz, 1e-4, label);
                        Assert.AreEqual(expectedPeaks[i].Intensity, actualPeaks[i].Intensity, 1e-3, label);
                    }
                }
            }
            finally
            {
                answerLibrary.ReadStream.CloseStream();
                productLibrary.ReadStream.CloseStream();
            }
        }

        private static Library LoadLibrary(string path)
        {
            var spec = new BiblioSpecLiteSpec(Path.GetFileNameWithoutExtension(path), path);
            var library = spec.LoadLibrary(new DefaultFileLoadMonitor(new SilentProgressMonitor()));
            Assert.IsTrue(library.IsLoaded, path);
            return library;
        }
    }
}
