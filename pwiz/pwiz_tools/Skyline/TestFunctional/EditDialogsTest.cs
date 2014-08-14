/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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

using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EditDialogsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEditDialogs()
        {
            TestFilesZip = @"TestFunctional\EditNoteTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunEditDialogs(); // no data needed to test these

            // Open a .sky file
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini.sky")));
            RunOtherDialogs();
        }

        private void RunEditDialogs()
        {
            // Display transition settings, prediction tab.
            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => { transitionSettings.SelectedTab = TransitionSettingsUI.TABS.Prediction; });

            RunDlg<EditDPDlg>(transitionSettings.AddToDPList);

            // Display full scan tab.
            RunUI(() =>
            {
                transitionSettings.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettings.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
            });

            RunDlg<EditIsotopeEnrichmentDlg>(transitionSettings.AddToEnrichmentsList);

            // Display filter tab.
            RunUI(() =>
            {
                transitionSettings.SelectedTab = TransitionSettingsUI.TABS.Filter;
            });

            {
                var editList =
                    ShowDialog<EditListDlg<SettingsListBase<MeasuredIon>, MeasuredIon>>(
                        transitionSettings.EditSpecialTransitionsList);

                RunDlg<EditMeasuredIonDlg>(editList.EditItem, editMeasuredIonDlg =>
                {
                    editMeasuredIonDlg.DialogResult = DialogResult.Cancel;
                });

                OkDialog(editList, editList.OkDialog);
            }
            OkDialog(transitionSettings, transitionSettings.CancelButton.PerformClick);

            // Display peptide settings, filter tab.
            var peptideSettings = ShowDialog<PeptideSettingsUI>(() =>
                SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Filter));

            {
                var editList = ShowDialog<EditListDlg<SettingsListBase<PeptideExcludeRegex>, PeptideExcludeRegex>>(
                    peptideSettings.EditExclusionList);

                RunDlg<EditExclusionDlg>(editList.EditItem, editExclusionDlg =>
                {
                    editExclusionDlg.DialogResult = DialogResult.Cancel;
                });

                OkDialog(editList, editList.OkDialog);
            }
            OkDialog(peptideSettings, peptideSettings.CancelButton.PerformClick);
        }

        private void RunOtherDialogs()
        {
            RunDlg<ChromatogramRTThresholdDlg>(SkylineWindow.ShowChromatogramRTThresholdDlg);

            var rtReplicateGraph = ShowDialog<GraphSummary>(SkylineWindow.ShowRTReplicateGraph);
            RunDlg<RTChartPropertyDlg>(SkylineWindow.ShowRTPropertyDlg);
            OkDialog(rtReplicateGraph, rtReplicateGraph.Close);

            var areaReplicateGraph = ShowDialog<GraphSummary>(SkylineWindow.ShowPeakAreaReplicateComparison);
            RunDlg<AreaChartPropertyDlg>(SkylineWindow.ShowAreaPropertyDlg);
            OkDialog(areaReplicateGraph, areaReplicateGraph.Close);
        }
    }
}
