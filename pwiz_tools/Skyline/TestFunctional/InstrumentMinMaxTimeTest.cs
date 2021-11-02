/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that the Instrument settings "Min Time" and "Max Time" properly restrict the length of chromatogram
    /// extraction.
    /// Verifies that this works when none, one or both of the values are set.
    /// Verifies that the instrument min and max time work in conjunction with retention time filtering around predicted retention times.
    /// </summary>
    [TestClass]
    public class InstrumentMinMaxTimeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestInstrumentMinMax()
        {
            TestFilesZip = @"TestFunctional\InstrumentMinMaxTimeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("InstrumentMinMaxTimeTest.sky")));
            WaitForDocumentLoaded();
            // The document is set to do filtering around predicted retention times
            Assert.AreEqual(RetentionTimeFilterType.scheduling_windows, SkylineWindow.Document.Settings.TransitionSettings.FullScan.RetentionTimeFilterType);
            // The document is using an iRT database which has entries for some but not all of the peptides.
            // The values in the iRT database have already been calibrated
            Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.AutoCalcRegression);

            Assert.IsNull(SkylineWindow.Document.Settings.TransitionSettings.Instrument.MinTime);
            Assert.IsNull(SkylineWindow.Document.Settings.TransitionSettings.Instrument.MaxTime);

            ImportResultsFile(TestFilesDir.GetTestPath("200fmol.mzML"));
            VerifyChromatogramLengths();
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.MinTime = 20;
                transitionSettingsUi.OkDialog();
            });
            Reimport();
            VerifyChromatogramLengths();
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.MaxTime = 35;
                transitionSettingsUi.OkDialog();
            });
            Reimport();
            VerifyChromatogramLengths();
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.MinTime = null;
                transitionSettingsUi.OkDialog();
            });
            Reimport();
            VerifyChromatogramLengths();
        }

        private void Reimport()
        {
            var doc = SkylineWindow.Document;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms;
                manageResultsDlg.ReimportResults();
                manageResultsDlg.OkDialog();
            });
            WaitForDocumentChangeLoaded(doc);
        }

        /// <summary>
        ///  Verifies that the retention time range over which chromatograms have been collected the correct range
        /// based on the predicted retention time of the peptide and the Instrument Min Time and Max Time values.
        /// </summary>
        private void VerifyChromatogramLengths()
        {
            var document = SkylineWindow.Document;
            var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[0];
            // The data file that we are using has retention times from 15 to 45 minutes.
            double runStartTime = document.Settings.TransitionSettings.Instrument.MinTime ?? 15;
            double runEndTime = document.Settings.TransitionSettings.Instrument.MaxTime ?? 45;

            double retentionTimeFilterLength = document.Settings.TransitionSettings.FullScan.RetentionTimeFilterLength;

            foreach (var peptide in document.Molecules)
            {
                double expectedStartTime = runStartTime;
                double expectedEndTime = runEndTime;
                double? predictedTime = document.Settings.PeptideSettings.Prediction.RetentionTime
                    .GetRetentionTime(peptide.ModifiedTarget);
                if (predictedTime.HasValue)
                {
                    expectedStartTime = Math.Max(predictedTime.Value - retentionTimeFilterLength, expectedStartTime);
                    expectedEndTime = Math.Min(predictedTime.Value + retentionTimeFilterLength, expectedEndTime);
                }
                foreach (var transitionGroup in peptide.TransitionGroups)
                {
                    ChromatogramGroupInfo[] infos;
                    document.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptide, transitionGroup,
                        (float)TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE, out infos);
                    foreach (var chromGroupInfo in infos)
                    {
                        var startTime = chromGroupInfo.TimeIntensitiesGroup.MinTime;
                        var endTime = chromGroupInfo.TimeIntensitiesGroup.MaxTime;
                        const double tolerance = 0.15;
                        AssertEx.AreEqual(expectedStartTime, startTime, tolerance);
                        AssertEx.AreEqual(expectedEndTime, endTime, tolerance);
                    }
                }
            }
        }
    }
}
