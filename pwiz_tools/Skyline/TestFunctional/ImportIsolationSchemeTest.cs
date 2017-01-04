/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportIsolationSchemeTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestImportIsolationScheme()
        {
            Run(@"TestFunctional\ImportIsolationSchemeTest.zip");
        }

        private const string VARIABLE_64_SCIEX = "lgillet_I150211_008_131.mzML";
        private const string NOWIDTH_SCIEX = "lgillet_I150211_008_2_nowidth.mzML";
        private const string CORRUPT_SCIEX = "lgillet_I150211_008_2_corrupt.mzML";
        private const string VARIABLE_19_THERMO = "B_D140314_SGSDSsample1_R01_MHRM_T0_42.mzML";
        private const string VARIABLE_2_THERMO = "B_D140314_SGSDSsample1_R01_MHRM_T0_2.mzML";
        private const string DEMUX_6DA_400to500 = "HeLa_DIA_6Da_demux_400to500_82.mzML";
        private const string DEMUX_6DA_500to600 = "HeLa_DIA_6Da_demux_500to600_82.mzML";
        private const string OVERLAP_6DA_400to500 = "HeLa_DIA_6Da_overlap_400to500_82.mzML";
        private const string OVERLAP_6DA_500to600 = "HeLa_DIA_6Da_overlap_500to600_82.mzML";
        private const string OVERLAP_6DA_600to700 = "HeLa_DIA_6Da_overlap_600to700_82.mzML";
        private const string OVERLAP_6DA_700to800 = "HeLa_DIA_6Da_overlap_700to800_82.mzML";
        private const string OVERLAP_6DA_800to900 = "HeLa_DIA_6Da_overlap_800to900_82.mzML";
        private const string OVERLAP_6DA_900to1000 = "HeLa_DIA_6Da_overlap_900to1000_82.mzML";
        private const string OVERLAP_24DA_1 = "HeLa_DIA_24Da_overlap_2159_112.mzML";
        private const string OVERLAP_24DA_2 = "HeLa_DIA_24Da_overlap_2160_112.mzML";

        protected override void DoTest()
        {
            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
            });
            var isoEditor = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
            RunUI(() => isoEditor.UseResults = false);
            ImportIsoList(isoEditor, VARIABLE_64_SCIEX, 64, 0.5);
            ImportIsoList(isoEditor, VARIABLE_19_THERMO, 19, 1);
            ImportIsoList(isoEditor, DEMUX_6DA_400to500, 35);
            ImportIsoList(isoEditor, OVERLAP_6DA_400to500, 36, null, true);
            ImportIsoList(isoEditor, OVERLAP_24DA_1, 52, null, true, 1);
            ImportIsoList(isoEditor, new[] {OVERLAP_24DA_1, OVERLAP_24DA_2}, 52, null, true, 1);
            ImportIsoList(isoEditor, new[] {DEMUX_6DA_400to500, DEMUX_6DA_500to600}, 70, null, false, 1);
            ImportIsoList(isoEditor, new[]
            {
                OVERLAP_6DA_400to500,
                OVERLAP_6DA_500to600,
                OVERLAP_6DA_600to700,
                OVERLAP_6DA_700to800,
                OVERLAP_6DA_800to900,
                OVERLAP_6DA_900to1000,
            }, 216, null, true, 5);
            ImportErrorMessage(isoEditor, NOWIDTH_SCIEX,
                Resources.EditIsolationSchemeDlg_ReadIsolationRanges_Missing_isolation_range_for_the_isolation_target__0__m_z_in_the_file__1_, 2);
            ImportErrorMessage(isoEditor, CORRUPT_SCIEX,
                Resources.EditIsolationSchemeDlg_ImportRangesFromFiles_Failed_reading_isolation_scheme_);
            ImportErrorMessage(isoEditor, VARIABLE_2_THERMO,
                Resources.EditIsolationSchemeDlg_ReadIsolationRanges_No_repeating_isolation_scheme_found_in__0_, 1);
            OkDialog(isoEditor, isoEditor.CancelButton.PerformClick);
            OkDialog(transitionSettings, transitionSettings.CancelButton.PerformClick);
        }

        private void PerformImport(EditIsolationSchemeDlg isoEditor, string rawFileName)
        {
            PerformImport(isoEditor, new [] {rawFileName});
        }

        private void PerformImport(EditIsolationSchemeDlg isoEditor, string[] rawFileNames)
        {
            var importDlg = ShowDialog<OpenDataSourceDialog>(isoEditor.ImportRanges);
            RunUI(() =>
            {
                importDlg.CurrentDirectory = new MsDataFilePath(TestFilesDir.FullPath);
                foreach (var rawFileName in rawFileNames)
                    importDlg.SelectFile(rawFileName);
            });
            OkDialog(importDlg, importDlg.AcceptButton.PerformClick);
        }

        private void ImportIsoList(EditIsolationSchemeDlg isoEditor, string rawFileName, int windowCount,
            double? marginWidth = null, bool overlapping = false, int misplacedRanges = 0)
        {
            ImportIsoList(isoEditor, new [] {rawFileName}, windowCount, marginWidth, overlapping, misplacedRanges);
        }

        private void ImportIsoList(EditIsolationSchemeDlg isoEditor, string[] rawFileNames, int windowCount,
            double? marginWidth = null, bool overlapping = false, int misplacedRanges = 0)
        {
            PerformImport(isoEditor, rawFileNames);
//            PauseTest();
            if (!TryWaitForConditionUI(10*1000, () => isoEditor.IsolationWindowGrid.RowCount == windowCount + 1))
                // window count + empty row
            {
                RunUI(() => Assert.Fail(TextUtil.LineSeparate(string.Format("Expecting {0} isolation ranges, found {1}", windowCount, isoEditor.IsolationWindowGrid.RowCount - 1),
                    TextUtil.LineSeparate(isoEditor.IsolationWindowGrid.Items.Select(i => string.Format("{0}, {1}", i.Start, i.End))))));
            }
                
            double allowedDelta = overlapping ? 0.05 : 0.00001;
            RunUI(() =>
            {
                if (marginWidth.HasValue)
                    Assert.IsTrue(isoEditor.SpecifyMargin);
                else
                    Assert.IsFalse(isoEditor.SpecifyMargin);
                if (overlapping && misplacedRanges < 2)
                    Assert.IsTrue(isoEditor.Overlap);
                else
                    Assert.IsFalse(isoEditor.Overlap);

                double? lastEnd = null;
                double overlap = marginWidth*2 ?? 0;
                int misplaced = 0;
                int overlapStartIndex = 0;
                for (int i = 0; i < isoEditor.IsolationWindowGrid.Items.Count; i++)
                {
                    var isolationWindow = isoEditor.IsolationWindowGrid.Items[i];
                    if (lastEnd.HasValue &&
                        (!overlapping || isolationWindow.Start > isoEditor.IsolationWindowGrid.Items[overlapStartIndex].Start))
                    {
                        try
                        {
                            Assert.AreEqual(lastEnd.Value - overlap, isolationWindow.Start.Value, allowedDelta);
                        }
                        catch (AssertFailedException)
                        {
                            if (misplaced < misplacedRanges)
                            {
                                lastEnd = null;
                                misplaced++;
                                if (overlapping)
                                    overlapStartIndex = i;
                                continue;
                            }
                            throw;
                        }
                    }
                    lastEnd = isolationWindow.End;
                    if (marginWidth.HasValue)
                        Assert.AreEqual(marginWidth.Value, isolationWindow.StartMargin.Value, allowedDelta);
                    else
                        Assert.IsNull(isolationWindow.StartMargin);
                }
                Assert.AreEqual(misplacedRanges, misplaced);
            });
        }

        private void ImportErrorMessage(EditIsolationSchemeDlg isoEditor, string rawFileName, string message, int insertions = 0)
        {
            PerformImport(isoEditor, rawFileName);
            var messageDlg = WaitForOpenForm<MessageDlg>();
            if (insertions == 0)
                Assert.IsTrue(messageDlg.Message.Contains(message), "Expected:<{0}> in the message <{1}>", message, messageDlg.Message);
            else
                AssertEx.AreComparableStrings(message, messageDlg.Message, insertions);
            OkDialog(messageDlg, messageDlg.OkDialog);
        }
    }
}