/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests doing "Rescore" while also changing the transition settings forcing multiple calls
    /// to Document.ChangeSettings while the ChromatogramManager is working.
    /// </summary>
    [TestClass]
    public class RescoreInPlaceTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRescoreInPlace()
        {
            TestFilesZip = @"TestFunctional\RescoreInPlaceTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Document.sky")));
            // Add many permutations of the peptide GNPTVEVELTTEK to the document.
            var peptideSequences = PermuteString("GNPTVEVELTTE").Distinct().Select(s => s + "K").Take(2000);
            RunUI(()=>SkylineWindow.Paste(TextUtil.LineSeparate(peptideSequences)));

            // Import the file "S_1.mzML" into the document multiple times,
            // copying it to a new name each time
            for (int iFile = 1; iFile <= 3; iFile++)
            {
                var filePath = TestFilesDir.GetTestPath("S_" + iFile + ".mzML");
                if (iFile != 1)
                {
                    File.Copy(TestFilesDir.GetTestPath("S_1.mzML"), filePath);
                }
                BeginImportResultsFile(filePath);
                ChangeSettingsUntilDocumentLoaded();
            }

            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg =>
            {
                dlg.Rescore(false);
            });
            ChangeSettingsUntilDocumentLoaded();
        }

        private void BeginImportResultsFile(string path)
        {
            RunLongDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                RunDlg<OpenDataSourceDialog>(
                    () => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFile(null),
                    openDataSourceDialog =>
                    {
                        openDataSourceDialog.SelectFile(path);
                        openDataSourceDialog.Open();
                    });
                WaitForConditionUI(() => importResultsDlg.NamedPathSets != null);
            }, importResultsDlg => importResultsDlg.OkDialog());
        }

        /// <summary>
        /// Repeatedly bring up the Transition Settings dialog and make a small change
        /// to the MzMatchTolerance.
        /// Changing the MzMatchTolerance causes <see cref="TransitionGroupDocNode.MustReadAllChromatograms"/>
        /// to return true which increases the number of reads of the .skyd file.
        /// </summary>
        private void ChangeSettingsUntilDocumentLoaded()
        {
            var delay = 0;
            do
            {
                bool transitionSettingsUiClosed = false;
                var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(() =>
                {
                    SkylineWindow.ShowTransitionSettingsUI();
                    transitionSettingsUiClosed = true;
                });
                RunUI(() =>
                {
                    transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                    var newTolerance =
                        SkylineWindow.Document.Settings.TransitionSettings.Instrument.MzMatchTolerance == 0.055
                            ? 0.056
                            : 0.055;

                    transitionSettingsUi.MZMatchTolerance = newTolerance;
                });
                while (!transitionSettingsUiClosed)
                {
                    SkylineWindow.BeginInvoke(new Action(() => transitionSettingsUi.OkDialog()));
                    WaitForConditionUI(() => transitionSettingsUiClosed || FindOpenForm<AlertDlg>() != null);
                    AlertDlg alertDlg = FindOpenForm<AlertDlg>();
                    if (alertDlg != null)
                    {
                        Assert.IsFalse(transitionSettingsUiClosed,
                            "Unexpected alert found after TransitionSettingsUi closed: {0}",
                            TextUtil.LineSeparate(alertDlg.Message, alertDlg.DetailMessage));
                        OkDialog(alertDlg, alertDlg.OkDialog);
                    }
                }

                Thread.Sleep(delay);
                // Increase the delay a little each time so that ChromatogramManager has 
                // more time to complete its work without being interrupted by the transition
                // settings change
                delay += 200;
            } while (!SkylineWindow.Document.IsLoaded);
        }

        public static IEnumerable<string> PermuteString(string s)
        {
            if (s.Length <= 1)
            {
                return new[] { s };
            }
            return Enumerable.Range(0, s.Length)
                .SelectMany(index => PermuteString(s.Remove(index, 1)).Select(p => s[index] + p));
        }
    }
}
