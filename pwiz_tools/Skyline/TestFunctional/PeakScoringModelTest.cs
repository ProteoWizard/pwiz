/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakScoringModelTest : AbstractFunctionalTest
    {
        private string _format;
        private string _percentFormat;
        private IList<IPeakFeatureCalculator> _defaultMProphetCalcs;
        private IList<string[]> _cellValuesOriginal;
        
        [TestMethod]
        public void TestPeakScoringModel()
        {
            TestFilesZip = @"TestFunctional\PeakScoringModelTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Change to true to write coefficient arrays
        /// </summary>
        private bool IsRecordMode { get { return false; } }

        protected override void DoTest()
        {
            Settings.Default.PeakScoringModelList.Clear();

            var documentFile = TestFilesDir.GetTestPath("MProphetGold-rescore2.sky");
            RunUI(() => SkylineWindow.OpenFile(documentFile));
            WaitForDocumentLoaded();
            _defaultMProphetCalcs = (new MProphetPeakScoringModel("dummy")).PeakFeatureCalculators.ToArray();

            TestDialog();
            TestModelChangesAndSave();
            TestBackwardCompatibility();
            TestIncompatibleDataSet();

            var documentMissingScores = TestFilesDir.GetTestPath("SRMCourse_DosR-hDP__20130501.sky");
            RunUI(() => SkylineWindow.OpenFile(documentMissingScores));
            WaitForDocumentLoaded();
            TestFindMissingScores();
        }

        protected void TestDialog()
        {

            // Display integration tab.
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);

            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel);

                // Check default values.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.PeakScoringModelName, string.Empty);
                        var rows = editDlg.PeakCalculatorsGrid.RowCount;
                        Assert.AreEqual(_defaultMProphetCalcs.Count, rows, "Unexpected count of peak calculators"); // Not L10N
                    });

                // Test empty name.
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                    {
                        AssertEx.AreComparableStrings(
                            Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, messageDlg.Message, 1);
                        messageDlg.OkDialog();
                    });

                RunUI(() => editDlg.PeakScoringModelName = "test1"); // Not L10N

                // Create a model with default values.
                RunUI(() =>
                    {
                        editDlg.TrainModelClick();
                        for (int i = 0; i < editDlg.PeakCalculatorsGrid.RowCount; i++)
                        {
                            editDlg.PeakCalculatorsGrid.SelectRow(i);
                        }
                        VerifyBias(editDlg, -15.7061453);
                    });
                OkDialog(editDlg, editDlg.OkDialog);
            }

            var editList =
                ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlg.EditPeakScoringModel);

            RunUI(() => editList.SelectItem("test1")); // Not L10N

            {
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.EditItem);
                _format = editDlg.PeakCalculatorWeightFormat;
                _percentFormat = editDlg.PeakCalculatorPercentContributionFormat;
                var gridGen = new GridDataGenerator(_defaultMProphetCalcs, _format, _percentFormat);
                gridGen.AddRow(true, 0.382942517316037, 0.128627076210894);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(true, 0.827278215324237, 0.0109975203145636);
                gridGen.AddRow(true, 2.86041559389025, 0.133794233084609);
                gridGen.AddRow(true, -0.251912499420841, 0.14392065404677);
                gridGen.AddRow(true, 0.495149585576337, 0.0805515759397253);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(true, 4.13092927777122, 0.10907872496981);
                gridGen.AddRow(true, 9.60141282291844, 0.438116812109939);
                gridGen.AddRow(true, 0.222046587545859, -0.157529702072592);
                gridGen.AddRow(true, 0.149909944118646, 0.0373834253398596);
                gridGen.AddRow(true, 0.203812425696076, 0.0640621597418578);
                gridGen.AddRow(true, 0.827278215324159, 0.0109975203145625);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);
                gridGen.AddRow(false, double.NaN, double.NaN);

                _cellValuesOriginal = gridGen.Rows;
                var gridGenNew = new GridDataGenerator(_defaultMProphetCalcs, _format, _percentFormat);
                gridGenNew.AddRow(true, 0.723415661140142, 0.25434998082333);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(true, 2.43812176463771, 0.116170660271784);
                gridGenNew.AddRow(true, -0.347073056933346, 0.182607609708661);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(true, 5.11121516738511, 0.125972787659046);
                gridGenNew.AddRow(true, 9.9052012947631, 0.442873218397545);
                gridGenNew.AddRow(true, 0.296007686142129, -0.204175906231327);
                gridGenNew.AddRow(true, 0.310798888317742, 0.0616122783032736);
                gridGenNew.AddRow(true, -0.0103131517907828, -0.00330867142031841);
                gridGenNew.AddRow(true, 1.91363507932622, 0.0238980424880057);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                gridGenNew.AddRow(false, double.NaN, double.NaN);
                var cellValuesNew = gridGenNew.Rows;
                // Verify weights, change name.
                RunUI(() =>
                    {
                        Assert.AreEqual(editDlg.PeakScoringModelName, "test1"); // Not L10N
                        VerifyCellValues(editDlg, _cellValuesOriginal);
                        VerifyBias(editDlg, -15.706145);
                        // Manually uncheck two of the scores
                        editDlg.SetChecked(3, false);
                        editDlg.SetChecked(6, false);
                        editDlg.TrainModelClick();
                        VerifyCellValues(editDlg, cellValuesNew);
                        VerifyBias(editDlg, -16.324392);
                        // Re-check the scores, show that model goes back to normal
                        editDlg.SetChecked(3, true);
                        editDlg.SetChecked(6, true);
                        editDlg.TrainModelClick();
                        VerifyCellValues(editDlg, _cellValuesOriginal);
                        VerifyBias(editDlg, -15.706145);
                        editDlg.PeakScoringModelName = "test2"; // Not L10N
                    });
               OkDialog(editDlg, editDlg.OkDialog);
            }
            {
                // Add conflicting name.
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(editList.AddItem);
                RunUI(() =>
                {
                    Assert.AreEqual(editDlg.PeakScoringModelName, "");
                    editDlg.PeakScoringModelName = "test2"; // Not L10N
                });
                RunDlg<MessageDlg>(editDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.EditPeakScoringModelDlg_OkDialog_The_peak_scoring_model__0__already_exists, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });
                OkDialog(editDlg, editDlg.CancelDialog);
                OkDialog(editList, editList.OkDialog);
                RunUI(() => reintegrateDlg.ComboPeakScoringModelSelected = "test2");
                OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            }
        }


        /// <summary>
        /// Trains a legacy model, checks that it saves correctly in the list and the document, 
        /// modifies it without changing its name and checks that the list and document models update correctly
        /// </summary>
        protected void TestModelChangesAndSave()
        {
            LegacyScoringModel peakScoringModelBase = null;
            
            // Test legacy model
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            var editDlg = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel);

            var calculatorsLegacy = (new LegacyScoringModel("dummy")).PeakFeatureCalculators.ToArray();
            var gridGen = new GridDataGenerator(calculatorsLegacy, _format, _percentFormat);
            gridGen.AddRow(true, 0.983430800716846, 0.748079593893591);
            gridGen.AddRow(true, 0.983430800716846, 0.156881253584786);
            gridGen.AddRow(false, double.NaN, double.NaN);
            gridGen.AddRow(true, 2.95029240215054, 0.0950391525216232);
            gridGen.AddRow(false, double.NaN, double.NaN);
            gridGen.AddRow(false, double.NaN, double.NaN);
            gridGen.AddRow(false, double.NaN, double.NaN);
            var cellValuesLegacy = gridGen.Rows;
            var gridGenNew = new GridDataGenerator(calculatorsLegacy, _format, _percentFormat);
            gridGenNew.AddRow(true, 1.11441930900552, 0.799146499207338);
            gridGenNew.AddRow(true, 1.11441930900552, 0.184817989140233);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(true, 3.34325792701657, 0.0160355116524288);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            var cellValuesLegacyNew = gridGenNew.Rows;
            RunUI(() =>
            {
                Assert.AreEqual(editDlg.PeakScoringModelName, "");
                editDlg.PeakScoringModelName = "legacy1"; // Not L10N
                editDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                Assert.AreEqual(editDlg.PeakScoringModelName, "legacy1");
                editDlg.TrainModelClick();
                VerifyBias(editDlg, -9.379362);
                VerifyCellValues(editDlg, cellValuesLegacy);
                editDlg.UsesSecondBest = true;
                editDlg.UsesDecoys = false;
                editDlg.TrainModelClick();
                VerifyBias(editDlg, -11.638443);
                VerifyCellValues(editDlg, cellValuesLegacyNew);
                editDlg.UsesSecondBest = false;
                peakScoringModelBase = editDlg.PeakScoringModel as LegacyScoringModel;
            });

            //  Unchecking decoys and second best leads to error on training
            RunDlg<MessageDlg>(editDlg.TrainModelClick, messageDlg =>
            {
                Assert.AreEqual(string.Format(Resources.EditPeakScoringModelDlg_btnTrainModel_Click_Cannot_train_model_without_either_decoys_or_second_best_peaks_included_),
                                messageDlg.Message);
                messageDlg.OkDialog();
            });
            OkDialog(editDlg, editDlg.OkDialog);
            RunUI(() => reintegrateDlg.ComboPeakScoringModelSelected = "legacy1");
            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            RunUI(() =>
            {
                // Test modification of legacy scoring model
                SkylineWindow.SaveDocument();
                var peakScoringModel = SkylineWindow.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel as LegacyScoringModel;
                var listModels = Settings.Default.PeakScoringModelList;
                Assert.AreEqual(listModels.Count, 4);
                var peakScoringModelList = listModels[3] as LegacyScoringModel;
                // Check that model in EditList, document model, and model from the dialog are all the same
                AssertEqualNotNull(new List<LegacyScoringModel> { peakScoringModel, peakScoringModelList, peakScoringModelBase });
                // Check document model is the last model we trained
                // ReSharper disable PossibleNullReferenceException
                Assert.AreEqual(peakScoringModel.Name, "legacy1");
                // ReSharper restore PossibleNullReferenceException
            });
            LegacyScoringModel peakScoringModelBaseNew = null;
            RunEditPeakScoringDlg("legacy1", editDlgTemp =>
            {
                editDlgTemp.UsesDecoys = true;
                editDlgTemp.TrainModelClick();
                peakScoringModelBaseNew = editDlgTemp.PeakScoringModel as LegacyScoringModel;
                editDlgTemp.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                var peakScoringModelNew = SkylineWindow.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel as LegacyScoringModel;
                var listModels = Settings.Default.PeakScoringModelList;
                var peakScoringModelListNew = listModels[3] as LegacyScoringModel;
                // Check that model in EditList, document model, and model from the dialog are all the same
                AssertEqualNotNull(new List<LegacyScoringModel> { peakScoringModelNew, peakScoringModelListNew, peakScoringModelBaseNew });
                // Check document model has changed
                // ReSharper disable PossibleNullReferenceException
                Assert.IsTrue(peakScoringModelNew.UsesDecoys);
                // ReSharper restore PossibleNullReferenceException
            });

            // Test changing legacy to mProphet model without changing name
            MProphetPeakScoringModel peakScoringModelMProphetBase = null;
            RunEditPeakScoringDlg("legacy1", editDlgTemp =>
            {
                // Switch to mProphet model
                editDlgTemp.SelectedModelItem = "mProphet";
                editDlgTemp.TrainModelClick();
                peakScoringModelMProphetBase = editDlgTemp.PeakScoringModel as MProphetPeakScoringModel;
                editDlgTemp.OkDialog();
            }
            );
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                var peakScoringModelMProphet = SkylineWindow.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel as MProphetPeakScoringModel;
                var listModels = Settings.Default.PeakScoringModelList;
                var peakScoringModelMProphetList = listModels[3] as MProphetPeakScoringModel;
                // Check that model in EditList, document model, and model from the dialog are all the same
                AssertEqualNotNull(new List<MProphetPeakScoringModel> { peakScoringModelMProphet, 
                                                                        peakScoringModelMProphetList, 
                                                                        peakScoringModelMProphetBase });
                // Check document model has changed
                Assert.AreNotEqual(peakScoringModelBaseNew, peakScoringModelMProphet);
                // ReSharper disable PossibleNullReferenceException
                Assert.IsTrue(peakScoringModelMProphet.UsesDecoys);
                // ReSharper restore PossibleNullReferenceException
                Assert.AreEqual(peakScoringModelMProphet.PeakFeatureCalculators.Count, 23);
            });
        }

        // Opens up a model that was trained on an earlier (fictitious) version of skyline with half of the values in the default list missing,
        // and also two calculators which are no longer in the default list
        protected void TestBackwardCompatibility()
        {
            var oldCalcs = new List<IPeakFeatureCalculator>
                {
                    new MQuestIntensityCalc(),
                    new MQuestIntensityCorrelationCalc(),
                    new MQuestWeightedShapeCalc(),
                    new NextGenSignalNoiseCalc(),
                    new NextGenProductMassErrorCalc(),
                    new LegacyIdentifiedCountCalc(),
                    new MQuestWeightedReferenceShapeCalc(),
                    new MQuestWeightedReferenceCoElutionCalc(),
                    new MQuestShapeCalc(),
                    new MQuestCoElutionCalc()
                };
            var gridGen = new GridDataGenerator(oldCalcs, _format, _percentFormat);
            gridGen.AddRow(true, 0.863270635260084, 0.318581790432795);
            gridGen.AddRow(true, 2.01768291591259, 0.030648912491513);
            gridGen.AddRow(true, 6.21701653129739, 0.272271568046956);
            gridGen.AddRow(false, double.NaN, double.NaN);
            gridGen.AddRow(false, double.NaN, double.NaN);
            gridGen.AddRow(false, double.NaN, double.NaN);
            gridGen.AddRow(true, 7.53518445380051, 0.319096853160788);
            gridGen.AddRow(true, -0.127723820682657, 0.0594008758679496);
            gridGen.AddRow(false, double.NaN, double.NaN);
            gridGen.AddRow(false, double.NaN, double.NaN);
            var cellValuesOld = gridGen.Rows;
            var gridGenNew = new GridDataGenerator(_defaultMProphetCalcs, _format, _percentFormat);
            gridGenNew.AddRow(true, 0.412294733862261, 0.146358520954345);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(true, 1.05591699541179, 0.0134338629118288);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(true, -0.338653641260181, 0.198945542278336);
            gridGenNew.AddRow(true, 0.385267383364279, 0.0812438848987492);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(true, 5.280590346473, 0.135473165064042);
            gridGenNew.AddRow(true, 11.1943150894438, 0.539485707382467);
            gridGenNew.AddRow(true, 0.286419098955052, -0.219723442922616);
            gridGenNew.AddRow(true, 0.339000738822918, 0.0703244960450021);
            gridGenNew.AddRow(true, 0.0643044070046071, 0.0210244004760243);
            gridGenNew.AddRow(true, 1.05591699541111, 0.0134338629118201);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            gridGenNew.AddRow(false, double.NaN, double.NaN);
            var cellValuesNew = gridGenNew.Rows;       

            RunEditPeakScoringDlg("backward_compatibility_test", editDlg =>
                {
                    VerifyCellValues(editDlg, cellValuesOld);
                    // Unchecking a calculator which is common to both models carries over
                    editDlg.SetChecked(2, false);
                    editDlg.TrainModelClick();
                    VerifyCellValues(editDlg, cellValuesNew);
                    // Check for behind-by-1 errors
                    editDlg.TrainModelClick();
                    VerifyCellValues(editDlg, cellValuesNew);
                    editDlg.OkDialog();
                });
        }

        // Test that the dialog behaves correctly when opening a model 
        // that is incompatible with the dataset (some or all composite scores are NaN's)
        protected void TestIncompatibleDataSet()
        {
            // Define an incompatible model
            var weights = new[] {0.5322, -1.0352, double.NaN, double.NaN, 1.4744, 0.0430, 0.0477, -0.2740, double.NaN, 
                                 2.0096, 7.7726, -0.0566, 0.4751, 0.5, 0.5, double.NaN, double.NaN, 
                                 double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN};
            var parameters = new LinearModelParams(weights, -2.5);
            var incompatibleModel = new MProphetPeakScoringModel("incompatible", parameters, null, true);
            Settings.Default.PeakScoringModelList.Add(incompatibleModel);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsDlg =>
                {
                    peptideSettingsDlg.ComboPeakScoringModelSelected = "incompatible";
                    peptideSettingsDlg.OkDialog();
                });
            var gridGen = new GridDataGenerator(_defaultMProphetCalcs, _format, _percentFormat);
            int i = 0;
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(true, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            gridGen.AddRow(false, weights[i++], double.NaN);
            var cellValuesIncompatible = gridGen.Rows;

            var reintegrateDlgIncompatible = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);

            var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlgIncompatible.EditPeakScoringModel);
            RunUI(() => editList.SelectItem("incompatible")); // Not L10N           
            
            RunDlg<EditPeakScoringModelDlg>(editList.EditItem, editDlgTemp =>
            {
                // All of the percentage fields should be null
                VerifyCellValues(editDlgTemp, cellValuesIncompatible, 0.0);
                editDlgTemp.TrainModelClick();
                // Cell values go back to the standard trained model after we train and enable calculators, 
                // despite having been loaded with weird values
                editDlgTemp.SetChecked(3, true);
                editDlgTemp.TrainModelClick();
                VerifyCellValues(editDlgTemp, _cellValuesOriginal);
                editDlgTemp.CancelDialog();
            });
            OkDialog(editList, editList.OkDialog);
            // Trying to reintegrate gives an error because the model is incompatible
            RunDlg<MessageDlg>(reintegrateDlgIncompatible.OkDialog, messageDlg =>
            {
                Assert.AreEqual(TextUtil.LineSeparate(string.Format(Resources.ReintegrateDlg_OkDialog_Failed_attempting_to_reintegrate_peaks_),
                                                      Resources.ReintegrateDlg_OkDialog_The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document___Please_train_a_new_model_),
                                messageDlg.Message);
                messageDlg.OkDialog();
            });
            OkDialog(reintegrateDlgIncompatible, reintegrateDlgIncompatible.CancelDialog);
        }

        /// <summary>
        /// Tests the missing scores finder for finding peptides that lack a particular score
        /// </summary>
        protected void TestFindMissingScores()
        {
            RunEditPeakScoringDlg(null, editDlgTemp =>
            {
                // Find missing scores
                editDlgTemp.PeakScoringModelName = "missing_scores";
                editDlgTemp.TrainModelClick();
                editDlgTemp.PeakCalculatorsGrid.SelectRow(3);
                editDlgTemp.FindMissingValues(3);
                editDlgTemp.OkDialog();
            });
            var missingPeptides = new List<string> { "LGGNEQVTR", "IPVDSIYSPVLK", "YFNDGDIVEGTIVK", 
                                                     "DFDSLGTLR", "GGYAGMLVGSVGETVAQLAR", "GGYAGMLVGSVGETVAQLAR"};
            var isDecoys = new List<bool> { false, false, false, false, false, true };
            RunUI(() =>
                {
                    var findResultsForm = Application.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
                    Assert.IsNotNull(findResultsForm);
                    // There are 6 peptides for which scores are missing
                    Assert.AreEqual(findResultsForm.ItemCount, 6);
                    for (int i = 0; i < 6; ++i)
                    {
                        findResultsForm.ActivateItem(i);
                        Assert.AreEqual(SkylineWindow.SelectedPeptideSequence, missingPeptides[i]);
                    }
                });
            for (int i = 0; i < 6; ++i)
            {
                RemovePeptide(missingPeptides[i], isDecoys[i]);
            }
            RunEditPeakScoringDlg("missing_scores", editDlgTemp =>
            {
                // No missing values for these scores any more
                Assert.IsTrue(editDlgTemp.IsActiveCell(3, 0));
                Assert.IsTrue(editDlgTemp.IsActiveCell(9, 0));
                Assert.IsTrue(editDlgTemp.IsActiveCell(10, 0));
                Assert.IsTrue(editDlgTemp.IsActiveCell(11, 0));
               
                // But they aren't automatically enabled
                Assert.IsFalse(editDlgTemp.PeakCalculatorsGrid.Items[3].IsEnabled);
                Assert.IsFalse(editDlgTemp.PeakCalculatorsGrid.Items[9].IsEnabled);
                Assert.IsFalse(editDlgTemp.PeakCalculatorsGrid.Items[10].IsEnabled);
                Assert.IsFalse(editDlgTemp.PeakCalculatorsGrid.Items[11].IsEnabled);
                editDlgTemp.SetChecked(10, true);
                editDlgTemp.TrainModelClick();
                editDlgTemp.OkDialog();
            });
        }

        // Check that the items in the list are all equal and not null
        protected void AssertEqualNotNull(IList list)
        {
            if (list.Count == 0)
                return;
            var firstItem = list[0];
            foreach (var item in list)
            {
                Assert.IsNotNull(item);
                Assert.AreEqual(firstItem, item);
            }
        }

        // Conveniently opens/closes all the intermediate dialogs to open and run a EditPeakScoringModelDlg 
        protected static void RunEditPeakScoringDlg(string editName, Action<EditPeakScoringModelDlg> act)
        {
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);

            if (editName != null)
            {
                var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlg.EditPeakScoringModel);
                RunUI(() => editList.SelectItem(editName)); // Not L10N
                RunDlg(editList.EditItem, act);
                OkDialog(editList, editList.OkDialog);
            }
            else
            {
                RunDlg(reintegrateDlg.AddPeakScoringModel, act);
            }
            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
        }

        private class GridDataGenerator
        {
            private readonly IList<IPeakFeatureCalculator> _calculatorList;
            public IList<string[]> Rows { get; private set; }
            private readonly string _format;
            private readonly string _percentFormat;

            public GridDataGenerator(IList<IPeakFeatureCalculator> calculatorList, string format, string percentFormat)
            {
                _calculatorList = calculatorList;
                _format = format;
                _percentFormat = percentFormat;
                Rows = new List<string[]>();
            }

            public void AddRow(bool enabled, double weights, double percentages)
            {
                int i = Rows.Count;
                Rows.Add(new[]
                    {
                        enabled.ToString(),
                        _calculatorList[i].Name,
                        double.IsNaN(weights) ? null : weights.ToString(_format),
                        double.IsNaN(percentages) ? null : percentages.ToString(_percentFormat)
                    });
            }

            public void Clear()
            {
                Rows.Clear();
            }
        }

        private void VerifyCellValues(EditPeakScoringModelDlg editDlg, IList<string[]> expectedValues, double sumWeights = 1.0)
        {
            // Verify expected number of rows.
            Assert.AreEqual(editDlg.PeakCalculatorsGrid.RowCount, expectedValues.Count);
            // Verify normalized weights add to 1
            double sumNormWeights = 0;
            for (int row = 0; row < expectedValues.Count; row++)
            {
                var readModeSb = new StringBuilder();
                // Verify expected number of columns.
                Assert.AreEqual(4, expectedValues[row].Length);

                for (int col = 0; col < expectedValues[row].Length; col++)
                {
                    var expectedValue = expectedValues[row][col];

                    // Verify cell value.
                    var actualValue = editDlg.PeakCalculatorsGrid.GetCellValue(col, row);
                    if (IsRecordMode)
                    {
                        if (col == 0)
                        {
                            readModeSb.Append("gridGen.AddRow(" + (actualValue == null ? "" : actualValue.ToLower()));
                        }
                        if (col == 2)
                        {
                            readModeSb.Append("," + (string.IsNullOrEmpty(actualValue) ? "double.NaN" : actualValue));
                        }
                        if (col == 3)
                        {
                            readModeSb.Append("," + (string.IsNullOrEmpty(actualValue) ? "double.NaN" : actualValue) + ");");
                            Console.WriteLine(readModeSb.ToString());
                        }
                    }
                    if (col == 2  && !string.IsNullOrEmpty(actualValue))
                        actualValue = double.Parse(actualValue).ToString(editDlg.PeakCalculatorWeightFormat);
                    if (col == 3 && !string.IsNullOrEmpty(actualValue))
                    {
                        sumNormWeights += double.Parse(actualValue);
                        actualValue = double.Parse(actualValue).ToString(editDlg.PeakCalculatorPercentContributionFormat);
                    }
                    if(!IsRecordMode)
                    {
                        if (expectedValue == null)
                        {
                            Assert.IsTrue(string.IsNullOrEmpty(actualValue));
                        }
                        else
                        {
                            Assert.AreEqual(expectedValue, actualValue);
                        }
                    }
                }
            }
            Assert.AreEqual(sumNormWeights, sumWeights, 0.005);
        }

        private void VerifyBias(EditPeakScoringModelDlg editDlg, double bias)
        {
            if (IsRecordMode)
            {
                Console.WriteLine(editDlg.PeakScoringModel.Parameters.Bias.ToString(CultureInfo.CurrentCulture)); 
            }
            else
            {
                Assert.AreEqual(editDlg.PeakScoringModel.Parameters.Bias, bias, 1e-5);
            }
        }
    }
}
