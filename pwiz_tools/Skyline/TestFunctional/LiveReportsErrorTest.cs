/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LiveReportsErrorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLiveReportsError()
        {
            TestFilesZip = @"TestFunctional\LiveReportsErrorTest.sky.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("LiveReportsErrorTest.sky"));
            });
            TestDocumentGridErrors();
            // TODO(nicksh): Make this I18N safe
            if (CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == ".")
                TestFoldChangeGridErrors();
        }

        private void TestDocumentGridErrors()
        {
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(false);
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => { documentGrid.ChooseView("PeptidesNoError"); });
            WaitForConditionUI(() => documentGrid.IsComplete);
            Assert.IsNull(FindOpenForm<AlertDlg>());
            RunUI(() => { documentGrid.ChooseView("PeptidesError"); });
            var alertDlg = WaitForOpenForm<AlertDlg>();
            OkDialog(alertDlg, alertDlg.ClickYes);
            RunUI(()=>documentGrid.DataGridView.Sort(documentGrid.FindColumn(PropertyPath.Root), ListSortDirection.Ascending));
            alertDlg = WaitForOpenForm<AlertDlg>();
            OkDialog(alertDlg, alertDlg.ClickNo);
            RunUI(()=>documentGrid.DataGridView.Sort(documentGrid.FindColumn(PropertyPath.Root), ListSortDirection.Descending));
            Assert.IsNull(FindOpenForm<AlertDlg>());
            OkDialog(documentGrid, documentGrid.Close);
        }

        private void TestFoldChangeGridErrors()
        {
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow("TestGroupComparison"));
            var foldChangeGrid = FindOpenForm<FoldChangeGrid>();
            RunUI(()=> { foldChangeGrid.DataboundGridControl.ChooseView("NoErrorGroupComparisonRpt"); });
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.IsComplete);
            Assert.IsNull(FindOpenForm<AlertDlg>());
            RunUI(()=> { foldChangeGrid.DataboundGridControl.ChooseView("ErrorGroupComparisonRpt"); });
            var alertDlg = WaitForOpenForm<AlertDlg>();
            OkDialog(alertDlg, alertDlg.ClickYes);
            RunUI(() => foldChangeGrid.DataboundGridControl.DataGridView.Sort(foldChangeGrid.DataboundGridControl.DataGridView.Columns[0], ListSortDirection.Ascending));
            alertDlg = WaitForOpenForm<AlertDlg>();
            OkDialog(alertDlg, alertDlg.ClickNo);
            RunUI(() => foldChangeGrid.DataboundGridControl.DataGridView.Sort(foldChangeGrid.DataboundGridControl.DataGridView.Columns[0], ListSortDirection.Descending));
            Assert.IsNull(FindOpenForm<AlertDlg>());
            OkDialog(foldChangeGrid, foldChangeGrid.Close);
        }
    }
}
