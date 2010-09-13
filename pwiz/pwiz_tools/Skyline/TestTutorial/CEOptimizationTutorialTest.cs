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

using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
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
        public void TestCEOptimizationTutorial()
        {
            TestFilesZip = ExtensionTestContext.CanImportThermoRaw ?  @"https://brendanx-uw1.gs.washington.edu/tutorials/OptimizeCE.zip"
                : @"https://brendanx-uw1.gs.washington.edu/tutorials/OptimizeCEMzml.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Skyline Collision Energy Optimization
            var folderOptimizeCE = ExtensionTestContext.CanImportThermoRaw ? "OptimizeCE" : "OptimizeCEMzml";
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(folderOptimizeCE + @"\CE_Vantage_15mTorr.sky")));

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

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                var path =
                    new[] {new KeyValuePair<string, string[]>("Unscheduled",
                        new[] { TestFilesDir.GetTestPath("CE_Vantage_15mTorr_unscheduled" + ExtensionTestContext.ExtThermoRaw)})};
                importResultsDlg.NamedPathSets = path;
                importResultsDlg.OkDialog();
            });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 7, 27, 30, 120);

            // Creating Optimization Methods, p. 5
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List), exportMethodDlg =>
            {
                exportMethodDlg.InstrumentType = ExportInstrumentType.Thermo;
                exportMethodDlg.ExportStrategy = ExportStrategy.Buckets;
                exportMethodDlg.MaxTransitions = 110;
                exportMethodDlg.IgnoreProteins = true;
                exportMethodDlg.OptimizeType = ExportOptimize.CE;
                exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                exportMethodDlg.OkDialog(TestFilesDir.GetTestPath(folderOptimizeCE + @"\CE_Vantage_15mTorr.csv"));
            });
            
            // Analyze Optimization Data, p. 7
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.OptimizationName = ExportOptimize.CE;
                importResultsDlg.NamedPathSets = ImportResultsDlg.GetDataSourcePathsDir(TestFilesDirs[0].FullPath).Take(5).ToArray();
                importResultsDlg.NamedPathSets[0] =
                     new KeyValuePair<string, string[]>("Optimize CE", importResultsDlg.NamedPathSets[0].Value);
                importResultsDlg.OkDialog();
            });
            RunUI(() => 
            {
                SkylineWindow.ShowSingleTransition();
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ExpandProteins();
                SkylineWindow.ExpandPeptides();
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                SkylineWindow.ArrangeGraphsTiled();

            });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            RunDlg<FindPeptideDlg>(SkylineWindow.ShowFindPeptideDlg, findPeptideDlg =>
            {
                findPeptideDlg.Sequence = "IDALNENK";
                findPeptideDlg.OkDialog();
            });
            RunUI(() => SkylineWindow.NormalizeAreaGraphToTotal(true));

            // Creating a New Equation for CE, p. 9
            var transitionSettingsUI1 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editCEDlg1 = ShowDialog<EditCEDlg>(transitionSettingsUI1.AddToCEList);
            RunUI(() =>
            {
                editCEDlg1.RegressionName = "Thermo Vantage Tutorial";
                editCEDlg1.UseCurrentData();
            });
            RunDlg<GraphRegression>(editCEDlg1.ShowGraph, graphRegression => graphRegression.CloseDialog());
            RunUI(editCEDlg1.OkDialog);
            WaitForClosedForm(editCEDlg1);
            RunUI(transitionSettingsUI1.OkDialog);
            WaitForClosedForm(transitionSettingsUI1);

            // Optimizing Each Transition, p. 10
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI2 =>
            {
                transitionSettingsUI2.UseOptimized = true;
                transitionSettingsUI2.OptimizeType = OptimizedMethodType.Transition.ToString();
                transitionSettingsUI2.OkDialog();
            });
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List), exportMethodDlg =>
            {
                exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                exportMethodDlg.OkDialog(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_optimized.csv"));
            });
        }
    }
}
