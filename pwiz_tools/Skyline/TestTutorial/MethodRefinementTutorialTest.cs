/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Targeted Method Refinement
    /// </summary>
    [TestClass]
    public class MethodRefinementTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMethodRefinementTutorial()
        {
            // Need to deal with this issue.
            TestFilesZip = @"https://brendanx-uw1.gs.washington.edu/tutorials/MethodRefineSupplement.zip";
            TestFilesZip = @"https://brendanx-uw1.gs.washington.edu/tutorials/MethodRefine.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
                  {
                      SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"MethodRefine\WormUnrefined.sky"));
                      SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                  });
            var exportDialog = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
            RunUI(() =>
                {
                    exportDialog.ExportStrategy = ExportStrategy.Buckets;
                    exportDialog.MethodType = ExportMethodType.Standard;
                    exportDialog.OptimizeType = ExportOptimize.NONE;
                    exportDialog.MaxTransitions = 59;
                });
            OkDialog(exportDialog, () => exportDialog.OkDialog(TestFilesDir.GetTestPath(@"MethodRefine\worm")));
            TestContext.ExtractTestFiles(TestContext.TestDir + @"\MethodRefineSupplement.zip");
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
                 {
                     manageResultsDlg.Remove();
                     manageResultsDlg.OkDialog();
                 });
            RunUI(() => SkylineWindow.SaveDocument());
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
                  {
                      importResultsDlg.RadioAddNewChecked = true;
                      importResultsDlg.NamedPathSets = ImportResultsDlg.GetDataSourcePathsDir(TestContext.TestDir).Take(15).ToArray();
                      importResultsDlg.OptimizationName = ExportOptimize.CE;
                      importResultsDlg.OkDialog();
                    });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            //RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            //{
            //    importResultsDlg.RadioAddExistingChecked = true;
            //    importResultsDlg.NamedPathSets = ImportResultsDlg.GetDataSourcePathsDir(TestContext.TestDir).Skip(15).ToArray();
            //    importResultsDlg.OptimizationName = ExportOptimize.CE;
            //    importResultsDlg.OkDialog();
            //});
            //WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            //RunUI(() =>
            //          {
            //              SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
            //              SkylineWindow.AutoZoomNone();
            //              SkylineWindow.AutoZoomBestPeak();
            //              SkylineWindow.EditDelete();
            //              SkylineWindow.ShowRTLinearRegressionGraph();
            //          });
            //RunDlg<ShowRTThresholdDlg>(SkylineWindow.ShowRTThresholdDlg, rtThresholdDlg =>
            //         {
            //             rtThresholdDlg.Threshold = 0.95;
            //             rtThresholdDlg.OkDialog();
            //         });
            //WaitForConditionUI(() => SkylineWindow.RTGraphController.RegressionRefined != null);
            //RunDlg<EditRTDlg>(SkylineWindow.CreateRegression, editRTDlg => editRTDlg.OkDialog());
            // WaitForConditionUI(() => false);
            
            // RunUI(() => SkylineWindow.RTGraphController.GraphSummary.GraphPane );
        }
    }
}
