/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Collision Energy Optimization
    /// </summary>
    [TestClass]
    public class CEOptimizationTutorialTest : AbstractFunctionalTest
    {
        
        [TestMethod]
        public void TestMethod1()
        {
            TestFilesZip = @"https://brendanx-uw1.gs.washington.edu/tutorials/OptimizeCE.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Skyline Collision Energy Optimization

            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"OptimizeCE\CE_Vantage_15mTorr.sky")));

            // Deriving a New Linear Equation, p. 2
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editList = 
                ShowDialog<EditListDlg<SettingsListBase<CollisionEnergyRegression>, CollisionEnergyRegression>>
                (transitionSettingsUI.EditCEList);
            RunUI(() => editList.SelectItem("Thermo"));
            RunDlg<EditCEDlg>(editList.EditItem, editCEDlg => editCEDlg.DialogResult = DialogResult.Cancel);
            RunUI(() =>
            {
                editList.DialogResult = DialogResult.Cancel;
                transitionSettingsUI.DialogResult = DialogResult.Cancel;
            });
            WaitForClosedForm(transitionSettingsUI);

            // Measuring Retention Times for Method Scheduling, p. 3
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List), exportMethodDlg =>
            {
                exportMethodDlg.InstrumentType = ExportInstrumentType.Thermo;
                exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                exportMethodDlg.OptimizeType = ExportOptimize.NONE;
                exportMethodDlg.MethodType = ExportMethodType.Standard;
                exportMethodDlg.OkDialog(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_unscheduled.csv"));
            });

            //RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            //{
            //    importResultsDlg.RadioAddNewChecked = true;
            //    importResultsDlg.GetDataSourcePathsFile(
            //        TestFilesDir.GetTestPath(@"OptimizeCE\CE_Vantage_15mTorr_unscheduled.raw"));
            //    importResultsDlg.NamedPathSets[0] =
            //        new KeyValuePair<string, string[]>("Unscheduled", importResultsDlg.NamedPathSets[0].Value);
            //    importResultsDlg.OkDialog();
            //});
            //AssertEx.IsDocumentState(SkylineWindow.Document, null, 7, 27, 120);


        }
    }
}
