/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.EditUI.OptimizeTransitions;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TransitionOptimizationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestTransitionOptimization()
        {
            TestFilesZip = @"TestFunctional\TransitionOptimizationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("TransitionOptimizationTest.sky"));
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });
            WaitForDocumentLoaded();
            var optimizeTransitionsForm =
                ShowDialog<OptimizeTransitionsForm>(SkylineWindow.ShowOptimizeTransitionsForm);
            WaitForCondition(() => optimizeTransitionsForm.IsComplete);
            RunUI(() =>
            {
                optimizeTransitionsForm.SelectOriginalTransitionRows();
                Assert.AreEqual(optimizeTransitionsForm.DataGridView.RowCount, optimizeTransitionsForm.DataGridView.SelectedRows.Count);
                optimizeTransitionsForm.SelectOptimizedTransitionRows();
                Assert.AreNotEqual(optimizeTransitionsForm.DataGridView.RowCount, optimizeTransitionsForm.DataGridView.SelectedRows.Count);
            });
            RunDlg<CalibrationCurveOptionsDlg>(
                optimizeTransitionsForm.CalibrationGraphControl.ShowCalibrationCurveOptions,
                dlg => dlg.OkDialog());
            RunUI(() => { optimizeTransitionsForm.ShowOptimizeDocumentTransitionsForm(); });
            var optimizeDocumentTransitionsForm = WaitForOpenForm<OptimizeDocumentTransitionsForm>();
            RunUI(()=>optimizeDocumentTransitionsForm.Preview());
            OkDialog(optimizeDocumentTransitionsForm, optimizeDocumentTransitionsForm.Close);
            OkDialog(optimizeTransitionsForm, optimizeTransitionsForm.Close);
        }
    }
}
