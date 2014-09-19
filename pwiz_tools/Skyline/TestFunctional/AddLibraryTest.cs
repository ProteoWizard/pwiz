/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AddLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAddLibrary()
        {
            TestFilesZip = @"TestFunctional\AddLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestAddChromatogramLibrary();
        }

        protected void TestAddChromatogramLibrary()
        {
            const string libName = "Human Chromatograms";
            const string predictorName = "Human Chromatograms Predictor";
            const double regressionWindow = 5.0;

            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = libName;
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("Human-iRT_rev1.clib");
            });
            var addPredictorDlg = ShowDialog<AddRetentionTimePredictorDlg>(addLibDlg.OkDialog);
            RunUI(() =>
            {
                addPredictorDlg.PredictorName = predictorName;
                addPredictorDlg.PredictorWindow = regressionWindow;
            });

            OkDialog(addPredictorDlg, addPredictorDlg.OkDialog);
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            OkDialog(libListDlg, libListDlg.OkDialog);

            // Check retention time predictor
            Assert.AreEqual(predictorName, Settings.Default.RetentionTimeList.Last().Name);

            // Check iRT calculator
            Assert.AreEqual(libName, Settings.Default.RTScoreCalculatorList.Last().Name);

            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.SelectedRTPredictor = predictorName;
            });

            var editRtDlg = ShowDialog<EditRTDlg>(peptideSettingsUi.EditRegression);

            Assert.AreEqual(regressionWindow, editRtDlg.Regression.TimeWindow);
            Assert.AreEqual(libName, editRtDlg.Regression.Calculator.Name);

            OkDialog(editRtDlg, editRtDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
        }
    }
}
