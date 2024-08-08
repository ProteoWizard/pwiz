/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for RetentionTimeFilterTest
    /// </summary>
    [TestClass]
    public class RetentionTimeFilterTest : AbstractFunctionalTest
    {
        private readonly string extension = ExtensionTestContext.ExtMz5;

        [TestMethod]
        public void TestRetentionTimeFilter()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeFilterTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestUsePredictedTime();
        }

        protected void TestUsePredictedTime()
        {
            const double FILTER_LENGTH = 2.7;
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RetentionTimeFilterTest.sky")));
            var docStart = WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("TestUsePredictedTime.sky")));
            SetUiDocument(docStart.ChangeSettings(docStart.Settings.ChangeTransitionFullScan(f =>
                f.ChangeRetentionTimeFilter(RetentionTimeFilterType.scheduling_windows, FILTER_LENGTH))));
            Assert.IsNull(SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime);
            Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Prediction.UseMeasuredRTs);
            // When we try to import a file, we should get an error about not having a peptide prediction algorithm
            var messageDlg = ShowDialog<MessageDlg>(() => SkylineWindow.ImportResults());
            RunUI(() =>
            {
                Assert.AreEqual(Resources.SkylineWindow_CheckRetentionTimeFilter_NoPredictionAlgorithm, messageDlg.Message);
                messageDlg.Close();
            });

            var ssrCalcRegression = new RetentionTimeRegression("SSRCALC_FOR_RtFilterTest",
                new RetentionScoreCalculator(RetentionTimeRegression.SSRCALC_100_A), .63,
                5.8, 1.4, new MeasuredRetentionTime[0]);
            // Now give the document a prediction algorithm
            var docBeforeImport = SkylineWindow.Document;
            docBeforeImport = docBeforeImport.ChangeSettings(docBeforeImport.Settings.ChangePeptidePrediction(p =>
                p.ChangeRetentionTime(ssrCalcRegression)));
            SetUiDocument(docBeforeImport);
            // Now import two result files
            {
                var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
                RunUI(() =>
                {
                    openDataSourceDialog.SelectFile("200fmol" + extension);
                    openDataSourceDialog.SelectFile("20fmol" + extension);
                });
                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            }
            {
                var document = WaitForDocumentChangeLoaded(docBeforeImport);
                foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
                {
                    foreach (var tuple in LoadAllChromatograms(document, chromatogramSet))
                    {
                        var peptide = tuple.Item1;
                        if (!peptide.IsProteomic)
                            continue;
                        var transitionGroup = tuple.Item2;
                        var predictedRetentionTime = ssrCalcRegression.GetRetentionTime(
                            document.Settings.GetModifiedSequence(peptide.Peptide.Target,
                                transitionGroup.TransitionGroup.LabelType, peptide.ExplicitMods)).Value;
                        AssertChromatogramWindow(document, chromatogramSet, 
                            predictedRetentionTime - FILTER_LENGTH, 
                            predictedRetentionTime + FILTER_LENGTH, tuple.Item3);
                    }
                }
            }
            ChromatogramSet chromSetForScheduling = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[1];
            // Create a SrmDocument with just the one ChromatogramSet that we are going to use for scheduling, so that
            // we can assert later that the chromatogram windows are where this document says they should be.
            var docForScheduling = SkylineWindow.Document.ChangeMeasuredResults(
                    SkylineWindow.Document.Settings.MeasuredResults.ChangeChromatograms(new[] {chromSetForScheduling}));
            docForScheduling = docForScheduling.ChangeSettings(docForScheduling.Settings.ChangePeptidePrediction(p =>
                p.ChangeUseMeasuredRTs(true).ChangeRetentionTime(null)));
            SetUiDocument(docForScheduling);
            {
                var chooseSchedulingReplicatesDlg = ShowDialog<ChooseSchedulingReplicatesDlg>(SkylineWindow.ImportResults);
                // Choose a scheduling replicate (the one saved above)
                RunUI(() => Assert.IsTrue(chooseSchedulingReplicatesDlg.TrySetReplicateChecked(
                    chromSetForScheduling, true)));
                var importResultsDlg = ShowDialog<ImportResultsDlg>(chooseSchedulingReplicatesDlg.OkDialog);
                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
                RunUI(() => openDataSourceDialog.SelectFile("40fmol" + extension));
                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            }
            {
                WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Any(cs => cs.Name == "40fmol")); 
                var document = WaitForDocumentChangeLoaded(docForScheduling);
                var chromatogramSet = document.Settings.MeasuredResults.Chromatograms.First(cs => cs.Name == "40fmol");
                int countNull = 0;
                foreach (var tuple in LoadAllChromatograms(document, chromatogramSet))
                {
                    var prediction = new PeptidePrediction(null, true, 1);

                    var schedulingPeptide =
                        docForScheduling.Molecules.First(pep => ReferenceEquals(pep.Peptide, tuple.Item1.Peptide));
                    var schedulingTransitionGroup = (TransitionGroupDocNode) schedulingPeptide.FindNode(tuple.Item2.TransitionGroup);
                    double? predictedRt = prediction.PredictRetentionTime(docForScheduling, 
                        schedulingPeptide, 
                        schedulingTransitionGroup, 
                        null, ExportSchedulingAlgorithm.Average, true, out _);
                    if (!predictedRt.HasValue)
                    {
                        countNull++;
                        continue;
                    }
                    AssertChromatogramWindow(document, chromatogramSet, predictedRt.Value - FILTER_LENGTH, predictedRt.Value + FILTER_LENGTH, tuple.Item3);
                }
                Assert.AreEqual(0, countNull);
            }

            // Test using iRT with auto-calculated regression
            {
                var docBeforeSettingsChange = SkylineWindow.Document;
                const string calcName = "TestCalculator";
                const string regressionName = "TestCalculatorAutoCalcRegression";
                var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                var editIrtDlg = ShowDialog<EditIrtCalcDlg>(peptideSettingsDlg.AddCalculator);
                RunUI(() =>
                {
                    editIrtDlg.OpenDatabase(TestFilesDir.GetTestPath("RetentionTimeFilterTest.irtdb"));
                    editIrtDlg.CalcName = calcName;
                });

                var multiButtonMsgDlg = ShowDialog<MultiButtonMsgDlg>(editIrtDlg.OkDialog);
                OkDialog(multiButtonMsgDlg, multiButtonMsgDlg.ClickYes);
                var editRtDlg = ShowDialog<EditRTDlg>(peptideSettingsDlg.AddRTRegression);
                RunUI(() =>
                {
                    editRtDlg.ChooseCalculator(calcName);
                    editRtDlg.SetAutoCalcRegression(true);
                    editRtDlg.SetRegressionName(regressionName);
                    editRtDlg.SetTimeWindow(1.0);
                });
                OkDialog(editRtDlg, editRtDlg.OkDialog);
                RunUI(() =>
                {
                    peptideSettingsDlg.ChooseRegression(regressionName);
                    peptideSettingsDlg.UseMeasuredRT(false);
                });
                OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
                docBeforeImport = WaitForDocumentChange(docBeforeSettingsChange);
                AssertEx.AreEqual(calcName, SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime?.Calculator.Name);
                Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Prediction.UseMeasuredRTs);
                var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
                RunUI(() => openDataSourceDialog.SelectFile("8fmol" + extension));
                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
                AssertEx.AreEqual(calcName, SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime?.Calculator.Name);
                var document = WaitForDocumentChangeLoaded(docBeforeImport);
                AssertEx.AreEqual(calcName, SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime?.Calculator.Name);
                var chromatogramSet = document.Settings.MeasuredResults.Chromatograms.First(cs => cs.Name == "8fmol");
                
                var regressionLine =
                    document.Settings.PeptideSettings.Prediction.RetentionTime.GetConversion(
                        chromatogramSet.MSDataFileInfos.First().FileId);
                var calculator = document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
                double fullGradientStartTime;
                double fullGradientEndTime;
                using (var msDataFile = new MsDataFileImpl(TestFilesDir.GetTestPath("8fmol" + extension)))
                {
                    fullGradientStartTime = msDataFile.GetSpectrum(0).RetentionTime.Value;
                    fullGradientEndTime = msDataFile.GetSpectrum(msDataFile.SpectrumCount - 1).RetentionTime.Value;
                }

                foreach (var tuple in LoadAllChromatograms(document, chromatogramSet))
                {
                    if (tuple.Item1.GlobalStandardType != PeptideDocNode.STANDARD_TYPE_IRT)
                    {
                        double? score =
                            calculator.ScoreSequence(document.Settings.GetModifiedSequence(tuple.Item1.Peptide.Target,
                                tuple.Item2.TransitionGroup.LabelType, tuple.Item1.ExplicitMods));
                        if (score.HasValue)
                        {
                            double? predictedRt = regressionLine.GetY(score.Value);
                            AssertChromatogramWindow(document, chromatogramSet, predictedRt.Value - FILTER_LENGTH,
                                predictedRt.Value + FILTER_LENGTH, tuple.Item3);
                        }
                    }
                    else
                    {
                        // IRT Standards get extracted for the full gradient
                        AssertChromatogramWindow(document, chromatogramSet, fullGradientStartTime, fullGradientEndTime, tuple.Item3);
                    }
                }
            }
        }

        private void SetUiDocument(SrmDocument newDocument)
        {
            RunUI(() => Assert.IsTrue(SkylineWindow.SetDocument(newDocument, SkylineWindow.DocumentUI)));
        }

        private IEnumerable<Tuple<PeptideDocNode, TransitionGroupDocNode, ChromatogramGroupInfo[]>> 
            LoadAllChromatograms(SrmDocument document, ChromatogramSet chromatogramSet)
        {
            foreach (var peptide in document.Molecules)
            {
                foreach (var transitionGroup in peptide.TransitionGroups)
                {
                    ChromatogramGroupInfo[] infos;
                    document.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptide, transitionGroup,
                        (float) TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE, out infos);
                    yield return new Tuple<PeptideDocNode, TransitionGroupDocNode, ChromatogramGroupInfo[]>(peptide, transitionGroup, infos);
                }
            }
        }

        private void AssertChromatogramWindow(SrmDocument document, ChromatogramSet chromatogramSet,
            double expectedStartTime, double expectedEndTime, params ChromatogramGroupInfo[] chromGroupInfos)
        {
            ChromatogramGroupInfo[] ticChromatograms;
            Assert.IsTrue(document.Settings.MeasuredResults.TryLoadAllIonsChromatogram(chromatogramSet,
                ChromExtractor.summed, true,
                out ticChromatograms));
            if (ticChromatograms.Length > 0)
            {
                double runStartTime =
                    ticChromatograms.Min(chromGroup => chromGroup.TimeIntensitiesGroup.MinTime);
                double runEndTime =
                    ticChromatograms.Max(chromGroup => chromGroup.TimeIntensitiesGroup.MaxTime);
                expectedStartTime = Math.Max(runStartTime, expectedStartTime);
                expectedEndTime = Math.Min(runEndTime, expectedEndTime);
            }

            const double delta = .15;
            foreach (var chromGroupInfo in chromGroupInfos)
            {
                double startTime = chromGroupInfo.TimeIntensitiesGroup.MinTime;
                double endTime = chromGroupInfo.TimeIntensitiesGroup.MaxTime;
                if (Math.Abs(expectedStartTime - startTime) > delta)
                {
                    Assert.AreEqual(expectedStartTime, startTime, delta);
                }
                if (Math.Abs(expectedEndTime - endTime) > delta)
                {
                    Assert.AreEqual(expectedEndTime, endTime, delta);
                }
            }
        }
    }
}
