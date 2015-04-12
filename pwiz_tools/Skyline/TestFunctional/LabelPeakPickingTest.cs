/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LabelPeakPickingTest : AbstractFunctionalTest
    {
        private static readonly Type[] RT_TYPES = 
        { 
            typeof(MQuestRetentionTimePredictionCalc), 
            typeof(MQuestRetentionTimeSquaredPredictionCalc)
        };

        private static readonly Type[] ANALYTE_TYPES = 
        { 
            typeof(MQuestIntensityCalc), 
            typeof(MQuestIntensityCorrelationCalc),
            typeof(MQuestWeightedShapeCalc),
            typeof(MQuestWeightedCoElutionCalc),
            typeof(LegacyUnforcedCountScoreCalc),
            typeof(NextGenSignalNoiseCalc)
        };

        private static readonly Type[] STANDARD_TYPES =
        {
            typeof(MQuestStandardIntensityCalc), 
            typeof(MQuestStandardIntensityCorrelationCalc), 
            typeof(MQuestStandardWeightedShapeCalc), 
            typeof(MQuestStandardWeightedCoElutionCalc),
            typeof(LegacyUnforcedCountScoreStandardCalc),
            typeof(NextGenStandardSignalNoiseCalc)
        };

        private static readonly Type[] DEFAULT_TYPES = 
        { 
            typeof(MQuestDefaultIntensityCalc), 
            typeof(MQuestDefaultIntensityCorrelationCalc), 
            typeof(MQuestDefaultWeightedShapeCalc), 
            typeof(MQuestDefaultWeightedCoElutionCalc),
            typeof(LegacyUnforcedCountScoreDefaultCalc)
        };
        
        private static readonly Type[] REFERENCE_TYPES =
        {
            typeof(MQuestWeightedReferenceShapeCalc),
            typeof(MQuestWeightedReferenceCoElutionCalc),
            typeof(MQuestReferenceCorrelationCalc)
        };

        private static readonly Type[] MS1_TYPES = 
        { 
            typeof(NextGenCrossWeightedShapeCalc), 
            typeof(NextGenPrecursorMassErrorCalc), 
            typeof(NextGenIsotopeDotProductCalc) 
        };

        [TestMethod]
        public void TestLabelPeakPicking()
        {
            TestFilesZip = @"TestFunctional\LabelPeakPickingTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestHeavyLight();
            TestMs1Ms2();
        }

        protected void TestMs1Ms2()
        {
            // TODO: test of peak scoring when both MS1 and MS2 transitions are present
        }

        protected void TestHeavyLight()
        {
            var documentHeavyLight = TestFilesDir.GetTestPath("Olga_srm_course_heavy_light.sky");
            
            // Use heavy as internal standards
            LoadDocument(documentHeavyLight);
            SetStandardType(IsotopeLabelType.light.Name);
            ImportFiles();
            RunEditPeakScoringDlg(null, editDlg =>
            {
                editDlg.SelectedModelItem = MProphetPeakScoringModel.NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, RT_TYPES);
                VerifyScores(editDlg, true, ANALYTE_TYPES);
                VerifyScores(editDlg, true, STANDARD_TYPES);
                VerifyScores(editDlg, true, REFERENCE_TYPES);
                VerifyScores(editDlg, false, MS1_TYPES);
                var analyteHistograms = ANALYTE_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                var standardHistograms = STANDARD_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                editDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, DEFAULT_TYPES);
                var defaultHistograms = DEFAULT_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                // Default scores match standard scores for all peptides 
                Assert.IsTrue(ArrayUtil.EqualsDeep(standardHistograms.Take(5).ToArray(), defaultHistograms));
                Assert.IsFalse(ArrayUtil.EqualsDeep(analyteHistograms.Take(5).ToArray(), defaultHistograms));
                editDlg.CancelDialog();
            });

            // Use light as internal standards
            RemoveImportedResults();
            SetStandardType(IsotopeLabelType.light.Name);
            ImportFiles();
            RunEditPeakScoringDlg(null, editDlg =>
            {
                editDlg.SelectedModelItem = MProphetPeakScoringModel.NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, RT_TYPES);
                VerifyScores(editDlg, true, ANALYTE_TYPES);
                VerifyScores(editDlg, true, STANDARD_TYPES);
                VerifyScores(editDlg, true, REFERENCE_TYPES);
                VerifyScores(editDlg, false, MS1_TYPES);
                var analyteHistograms = ANALYTE_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                var standardHistograms = STANDARD_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                editDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, DEFAULT_TYPES);
                var defaultHistograms = DEFAULT_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                // Default scores match standard scores for all peptides 
                Assert.IsTrue(ArrayUtil.EqualsDeep(standardHistograms.Take(5).ToArray(), defaultHistograms));
                Assert.IsFalse(ArrayUtil.EqualsDeep(analyteHistograms.Take(5).ToArray(), defaultHistograms));
                editDlg.CancelDialog();
            });

            // Use no internal standards
            RemoveImportedResults();
            SetStandardType(Resources.LabelTypeComboDriver_LoadList_none);
            ImportFiles();
            RunEditPeakScoringDlg(null, editDlg =>
            {
                editDlg.SelectedModelItem = MProphetPeakScoringModel.NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, RT_TYPES);
                VerifyScores(editDlg, true, ANALYTE_TYPES);
                VerifyScores(editDlg, false, STANDARD_TYPES);
                VerifyScores(editDlg, false, REFERENCE_TYPES);
                VerifyScores(editDlg, false, MS1_TYPES);
                var analyteHistograms = ANALYTE_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                var standardHistograms = STANDARD_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                editDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, DEFAULT_TYPES);
                var defaultHistograms = DEFAULT_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                // Default scores match analyte scores for all peptides, since there are no standards 
                Assert.IsFalse(ArrayUtil.EqualsDeep(standardHistograms.Take(5).ToArray(), defaultHistograms));
                Assert.IsTrue(ArrayUtil.EqualsDeep(analyteHistograms.Take(5).ToArray(), defaultHistograms));
                editDlg.CancelDialog();
            });

            // Repeat the whole process, but this time with ONLY the heavy peptides present
            // Show that scoring handles everything correctly in this case too
            RemoveImportedResults();
            RemoveHeavyPeptides();
            SetStandardType(IsotopeLabelType.heavy.Name);
            ImportFiles();
            EditPeakScoringModelDlg.HistogramGroup[] analyteHeavyHistogramsOnly = null;
            RunEditPeakScoringDlg(null, editDlg =>
            {
                editDlg.SelectedModelItem = MProphetPeakScoringModel.NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, RT_TYPES);
                VerifyScores(editDlg, true, ANALYTE_TYPES);
                VerifyScores(editDlg, false, STANDARD_TYPES);
                VerifyScores(editDlg, false, REFERENCE_TYPES);
                VerifyScores(editDlg, false, MS1_TYPES);
                analyteHeavyHistogramsOnly = ANALYTE_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                var standardHistograms = STANDARD_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                editDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, DEFAULT_TYPES);
                var defaultHistograms = DEFAULT_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                // Default scores match analyte scores
                Assert.IsFalse(ArrayUtil.EqualsDeep(standardHistograms.Take(5).ToArray(), defaultHistograms));
                Assert.IsTrue(ArrayUtil.EqualsDeep(analyteHeavyHistogramsOnly.Take(5).ToArray(), defaultHistograms));
                editDlg.CancelDialog();
            });

            RemoveImportedResults();
            SetStandardType(IsotopeLabelType.light.Name);
            ImportFiles();
            EditPeakScoringModelDlg.HistogramGroup[] standardLightHistogramsOnly = null;
            RunEditPeakScoringDlg(null, editDlg =>
            {
                editDlg.SelectedModelItem = MProphetPeakScoringModel.NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, RT_TYPES);
                VerifyScores(editDlg, false, ANALYTE_TYPES);
                VerifyScores(editDlg, true, STANDARD_TYPES);
                VerifyScores(editDlg, false, REFERENCE_TYPES);
                VerifyScores(editDlg, false, MS1_TYPES);
                var analyteHistograms = ANALYTE_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                standardLightHistogramsOnly = STANDARD_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                editDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, DEFAULT_TYPES);
                var defaultHistograms = DEFAULT_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                // Default scores match standard scores
                Assert.IsTrue(ArrayUtil.EqualsDeep(standardLightHistogramsOnly.Take(5).ToArray(), defaultHistograms));
                Assert.IsFalse(ArrayUtil.EqualsDeep(analyteHistograms.Take(5).ToArray(), defaultHistograms));
                editDlg.CancelDialog();
            });

            RemoveImportedResults();
            SetStandardType(Resources.LabelTypeComboDriver_LoadList_none);
            ImportFiles();
            EditPeakScoringModelDlg.HistogramGroup[] analyteNoneHistogramsOnly = null;
            RunEditPeakScoringDlg(null, editDlg =>
            {
                editDlg.SelectedModelItem = MProphetPeakScoringModel.NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, RT_TYPES);
                VerifyScores(editDlg, true, ANALYTE_TYPES);
                VerifyScores(editDlg, false, STANDARD_TYPES);
                VerifyScores(editDlg, false, REFERENCE_TYPES);
                VerifyScores(editDlg, false, MS1_TYPES);
                analyteNoneHistogramsOnly = ANALYTE_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                var standardHistograms = STANDARD_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                editDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editDlg.TrainModelClick();
                VerifyScores(editDlg, true, DEFAULT_TYPES);
                var defaultHistograms = DEFAULT_TYPES.Select(type => GetHistogramForScore(editDlg, type)).ToArray();
                // Default scores match analyte scores
                Assert.IsFalse(ArrayUtil.EqualsDeep(standardHistograms.Take(5).ToArray(), defaultHistograms));
                Assert.IsTrue(ArrayUtil.EqualsDeep(analyteNoneHistogramsOnly.Take(5).ToArray(), defaultHistograms));
                editDlg.CancelDialog();
            });

            // Test some scores that should be exactly equal
            Assert.IsTrue(ArrayUtil.EqualsDeep(analyteHeavyHistogramsOnly, standardLightHistogramsOnly));
            Assert.IsTrue(ArrayUtil.EqualsDeep(analyteHeavyHistogramsOnly, analyteNoneHistogramsOnly));
        }

        public SrmDocument LoadDocument(string document)
        {
            RunUI(() => SkylineWindow.OpenFile(document));
            return WaitForDocumentLoaded();
        }

        public void RemoveHeavyPeptides()
        {
            var refineDlg = ShowDialog<RefineDlg>(SkylineWindow.ShowRefineDlg);
            RunUI(() =>
            {
                refineDlg.RefineLabelType = IsotopeLabelType.heavy;
                refineDlg.OkDialog();
            });
            WaitForDocumentLoaded();
        }

        public void RemoveImportedResults()
        {
            var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(manageResults.RemoveAllReplicates);
            OkDialog(manageResults, manageResults.OkDialog);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        public void SetStandardType(string standardType)
        {
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlg.SelectedInternalStandardTypeName = standardType);
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
        }

        protected void ImportFiles()
        {
            ImportFile(TestFilesDir.GetTestPath("olgas_S130501_009_StC-DosR_B4.wiff"));
            ImportFile(TestFilesDir.GetTestPath("olgas_S130501_010_StC-DosR_C4.wiff"));
            WaitForCondition(2 * 60 * 1000, () => SkylineWindow.Document.IsLoaded);    // 2 minutes
        }

        protected void ImportFile(string fileName)
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                var path = new KeyValuePair<string, MsDataFileUri[]>[1];
                path[0] = new KeyValuePair<string, MsDataFileUri[]>(Path.GetFileNameWithoutExtension(fileName),
                                            new[] { MsDataFileUri.Parse(fileName) });
                importResultsDlg.NamedPathSets = path;
            });
            OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            WaitForCondition(2 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 2 minutes
        }

        protected EditPeakScoringModelDlg.HistogramGroup GetHistogramForScore(EditPeakScoringModelDlg editDlg, Type scoreType)
        {
            int index = GetIndex(editDlg, scoreType);
            return GetHistogramForScoreIndex(editDlg, index);
        }

        protected EditPeakScoringModelDlg.HistogramGroup GetHistogramForScoreIndex(EditPeakScoringModelDlg editDlg, int scoreIndex)
        {
            EditPeakScoringModelDlg.HistogramGroup scoreHistograms;
            EditPeakScoringModelDlg.HistogramGroup pValueHistograms;
            EditPeakScoringModelDlg.HistogramGroup qValueHistograms;
            PointPairList piZeroLine;
            editDlg.GetPoints(
                scoreIndex,
                out scoreHistograms,
                out pValueHistograms,
                out qValueHistograms,
                out piZeroLine);
            return scoreHistograms;
        }

        protected void VerifyScores(EditPeakScoringModelDlg editDlg,
                                    bool isPresent,
                                    Type[] scoreTypes)
        {
            List<double?> scores;
            VerifyScores(editDlg, isPresent, scoreTypes, out scores);
        }

        protected void VerifyScores(EditPeakScoringModelDlg editDlg, 
                                    bool isPresent,
                                    Type[] scoreTypes,
                                    out List<double?> scores)
        {
            scores = new List<double?>();
            foreach (var scoreType in scoreTypes)
            {
                Assert.AreEqual(IsActiveCalculator(editDlg, scoreType), isPresent);
                scores.Add(ValueCalculator(editDlg, scoreType));
            }
        }

        protected bool IsActiveCalculator(EditPeakScoringModelDlg editDlg, Type calcType)
        {
            var index = GetIndex(editDlg, calcType);
            return editDlg.PeakCalculatorsGrid.Items[index].IsEnabled;
        }

        protected double? ValueCalculator(EditPeakScoringModelDlg editDlg, Type calcType)
        {
            var index = GetIndex(editDlg, calcType);
            return editDlg.PeakCalculatorsGrid.Items[index].Weight;
        }

        protected int GetIndex(EditPeakScoringModelDlg editDlg, Type calcType)
        {
            var calculators = editDlg.PeakScoringModel.PeakFeatureCalculators;
            var calculator = calculators.FirstOrDefault(calc => calc.GetType() == calcType);
            Assert.IsNotNull(calculator);
            return calculators.IndexOf(calculator); 
        }

        // Conveniently opens/closes all the intermediate dialogs to open and run a EditPeakScoringModelDlg 
        protected static void RunEditPeakScoringDlg(string editName, Action<EditPeakScoringModelDlg> act)
        {
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);

            if (editName != null)
            {
                var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlg.EditPeakScoringModel);
                RunUI(() => editList.SelectItem(editName));
                RunDlg(editList.EditItem, act);
                OkDialog(editList, editList.OkDialog);
            }
            else
            {
                RunDlg(reintegrateDlg.AddPeakScoringModel, act);
            }
            OkDialog(reintegrateDlg, reintegrateDlg.CancelDialog);
        }
    }
}
