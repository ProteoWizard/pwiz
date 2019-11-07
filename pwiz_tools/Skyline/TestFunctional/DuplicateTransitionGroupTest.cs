using System;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the scenario where there are multiple transition groups with the same label type and charge state.
    /// Usually, the charge state and label type is sufficient to uniquely identify a transition group.
    /// If there is more than one transition group with the same label type and charge state, we want to make sure
    /// that the "Ratio to Standard" is only calculated using the first transition group of the set of conflicting ones.
    /// </summary>
    [TestClass]
    public class DuplicateTransitionGroupTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDuplicateTransitionGroup()
        {
            TestFilesZip = @"TestFunctional\DuplicateTransitionGroupTest.zip";
            RunFunctionalTest();
        }

        // TODO(nicksh): Optimizations disabled on this method to help track down intermittent test failure.
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DuplicateTransitionGroups.sky")));
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            RunUI(() => openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("DuplicateTransitionGroupTest.mzML")));
            OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            WaitForDocumentLoaded();
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.DataboundGridControl.ChooseView("RatioToStandards"));
            WaitForConditionUI(() => documentGrid.IsComplete && documentGrid.RowCount > 0);
            var expectedResults = new []
            {
                Tuple.Create("Normal", (double?) .6469),
                Tuple.Create("BothLight", (double?) null),
                Tuple.Create("BothHeavy", (double?) null),
                Tuple.Create("ExtraLight", (double?) .6469),
                Tuple.Create("ExtraHeavy", (double?) 1),
            };
            var dataGridView = documentGrid.DataGridView;
            RunUI(() =>
            {
                Assert.AreEqual(expectedResults.Length, dataGridView.RowCount);
                for (int iRow = 0; iRow < expectedResults.Length; iRow++)
                {
                    Assert.AreEqual(expectedResults[iRow].Item1, dataGridView.Rows[iRow].Cells[0].Value);
                    if (expectedResults[iRow].Item2 == null)
                    {
                        Assert.IsNull(dataGridView.Rows[iRow].Cells[1].Value);
                    }
                    else
                    {
                        Assert.IsInstanceOfType(dataGridView.Rows[iRow].Cells[1].Value, typeof(double));
                        Assert.AreEqual(expectedResults[iRow].Item2.Value,
                            (double) dataGridView.Rows[iRow].Cells[1].Value, .001);
                    }
                }
            });
        }
    }
}
