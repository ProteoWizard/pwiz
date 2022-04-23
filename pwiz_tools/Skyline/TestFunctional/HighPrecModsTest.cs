/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
// ReSharper disable AccessToModifiedClosure

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class HighPrecModsTest : AbstractFunctionalTest
    {
        private const string LIBRARY_NAME = "HighPrecModsTestLib";
        private const string TRIMETHYL = "Trimethyl (K)";
        private const string ACETYL = "Acetyl";
        [TestMethod]
        public void TestHighPrecMods()
        {
            TestFilesZip = @"TestFunctional\HighPrecModsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("HighPrecModsTest.sky")));
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.PickedStaticMods = new[] {"Carbamidomethyl Cysteine"});
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUi.ShowBuildLibraryDlg);
            
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = LIBRARY_NAME;
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath("HighPrecModsTestLib.blib");
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddInputFiles(new[] { TestFilesDir.GetTestPath("table_of_all_spectra_update_November2016.ssl") });
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            WaitForConditionUI(() => peptideSettingsUi.AvailableLibraries.Contains(LIBRARY_NAME));
            RunUI(() => peptideSettingsUi.PickedLibraries = new[] { LIBRARY_NAME });
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            var viewLibraryDlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            WaitForConditionUI(() => viewLibraryDlg.HasMatches);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(viewLibraryDlg.AddAllPeptides);
            OkDialog(messageDlg, messageDlg.OkDialog);
            OkDialog(viewLibraryDlg, viewLibraryDlg.Close);

            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() => documentGrid.ChooseView("PeptideModSeqFullNames"));
            
            Filter(documentGrid.DataboundGridControl, PropertyPath.Root, "KSAPATGGVKKPHR");
            var propPathModSeqFullNames = PropertyPath.Root.Property("ModifiedSequence").Property("FullNames");
            var modSequences = GetColumnValues(documentGrid.DataboundGridControl, propPathModSeqFullNames);
            AssertContainsModification(modSequences, TRIMETHYL, true);
            AssertContainsModification(modSequences, ACETYL, false);
            
            Filter(documentGrid.DataboundGridControl, PropertyPath.Root, "KSTGGKAPR");
            modSequences = GetColumnValues(documentGrid.DataboundGridControl, propPathModSeqFullNames);
            AssertContainsModification(modSequences, TRIMETHYL, false);
            AssertContainsModification(modSequences, ACETYL, true);

            Filter(documentGrid.DataboundGridControl, PropertyPath.Root, "TKQTAR");
            modSequences = GetColumnValues(documentGrid.DataboundGridControl, propPathModSeqFullNames);
            AssertContainsModification(modSequences, TRIMETHYL, true);
            AssertContainsModification(modSequences, ACETYL, true);
            AssertEx.VerifyModifiedSequences(SkylineWindow.Document);
        }

        private void Filter(DataboundGridControl databoundGridControl, PropertyPath propertyPath, string filterValue)
        {
            WaitForConditionUI(() => databoundGridControl.IsComplete);
            var filterDlg = ShowDialog<QuickFilterForm>(() =>
            {
                databoundGridControl.QuickFilter(databoundGridControl.FindColumn(propertyPath));
            });
            RunUI(() =>
            {
                filterDlg.SetFilterOperation(0, FilterOperations.OP_EQUALS);
                filterDlg.SetFilterOperand(0, filterValue);
            });
            OkDialog(filterDlg, filterDlg.OkDialog);
            WaitForConditionUI(() => databoundGridControl.IsComplete);
        }

        private List<string> GetColumnValues(DataboundGridControl databoundGridControl, PropertyPath propertyPath) 
        {
            List<string> list = new List<string>();
            RunUI(() =>
            {
                var column = databoundGridControl.FindColumn(propertyPath);
                for (int i = 0; i < databoundGridControl.RowCount; i++)
                {
                    var value = databoundGridControl.DataGridView.Rows[i].Cells[column.Index].FormattedValue;
                    if (value == null)
                    {
                        list.Add(null);
                    }
                    else
                    {
                        list.Add(value as string ?? value.ToString());
                    }
                }
            });
            return list;
        }

        private void AssertContainsModification(List<string> modSequences, string mod, bool expectedContains)
        {
            Assert.AreEqual(expectedContains, modSequences.Any(seq => seq.Contains(mod)), "Unexpected result looking for {0} in {1}", mod, string.Join(",", modSequences));
        }

    }
}
