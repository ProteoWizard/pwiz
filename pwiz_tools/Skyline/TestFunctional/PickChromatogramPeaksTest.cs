/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PickChromatogramPeaksTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPickChromatogramPeaks()
        {
            TestFilesZip = @"TestFunctional\PickChromatogramPeaksTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PickChromatogramPeaksTest.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("PickPeakTest.wiff"));
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findNodeDlg =>
            {
                findNodeDlg.SearchString = "GQLPSGSSQFPHGQK";
                findNodeDlg.FindNext();
                findNodeDlg.Close();
            });
            RunUI(()=>SkylineWindow.ShowCandidatePeaks());
            var candidatePeaks = FindOpenForm<CandidatePeakForm>();
            WaitForConditionUI(() => candidatePeaks.IsComplete);
            RunUI(() =>
            {
                var databoundGridControl = candidatePeaks.DataboundGridControl;
                Assert.AreEqual(4, databoundGridControl.RowCount);
                var colPeakGroupRetentionTime =
                    databoundGridControl.FindColumn(
                        PropertyPath.Root.Property(nameof(CandidatePeakGroup.PeakGroupRetentionTime)));
                Assert.IsNotNull(colPeakGroupRetentionTime);
                var retentionTimes = Enumerable.Range(0, databoundGridControl.RowCount).Select(iRow =>
                    Math.Round((double) databoundGridControl.DataGridView.Rows[iRow].Cells[colPeakGroupRetentionTime.Index].Value, 2)).ToList();
                Assert.AreEqual(7.2, retentionTimes[0]);
                Assert.AreEqual(7.73, retentionTimes[1]);
                Assert.AreEqual(8.44, retentionTimes[2]);
                Assert.AreEqual(8.98, retentionTimes[3]);
            });
        }
    }
}
