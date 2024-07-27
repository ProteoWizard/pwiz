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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the redundant spectra dropdown menu in the <see cref="ViewLibraryDlg"/> which
    /// allows the user to view redundant spectra
    /// </summary>
    [TestClass]
    public class RedundantComboBoxTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRedundantComboBox()
        {
            TestFilesZip = @"TestFunctional\RedundantComboBoxTest.zip";
            RunFunctionalTest();
        }
        private static bool IsRecordMode { get { return false; } }  // Set to true to get peak counts


        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("BSA_Protea_label_free_meth3.sky")));
            WaitForDocumentLoaded();
            VerifyRedundantComboBox();
        }

        private void VerifyRedundantComboBox()
        {
            var dlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewMenu.ViewSpectralLibraries);
            WaitForConditionUI(() => dlg.IsUpdateComplete);
            Assert.AreEqual(690, dlg.PeptidesCount);
            // The dropdown is only visible if the peptide has redundant spectra. Index 0 does.
            VerifyRedundant(dlg, 0, true, 144);
            // Check that the peaks count of the graphed item matches the peaks of the selected spectra
            VerifyRedundant(dlg, 1, false, 346);
            RunUI(() => dlg.FilterString = "ik");
            VerifyRedundant(dlg, 4, true, 514);
            RunUI(() => Assert.AreEqual(1, dlg.RedundantComboBox.Items.Count));
            // This simulates the user clicking on or showing the drop down for the combobox, which populates the combobox
            RunUI(dlg.UpdateRedundantComboItems);
            // Checks that for this peptide, there are 11 different spectra available in the dropdown
            WaitForConditionUI(() => dlg.IsComboBoxUpdated);
            RunUI(() => Assert.AreEqual(11, dlg.RedundantComboBox.Items.Count));
            RunUI(() => dlg.RedundantComboBox.SelectedIndex = 1);
            // Checks the peaks count changes upon changing the selected redundant spectra in the dropdown
            RunUI(() => Assert.AreEqual(551, dlg.GraphItem.PeaksCount));
            RunUI(() => dlg.RedundantComboBox.SelectedIndex = 2);
            RunUI(() => Assert.AreEqual(513, dlg.GraphItem.PeaksCount));
            var fileSet = new HashSet<string>();
            var RTSet = new HashSet<string>();
            RunUI(() =>
            {
                // Different languages have different parenthesis characters - split on either
                var splitterChars = new[] { '(', '（' };
                foreach (ViewLibraryDlg.ComboOption redundantOption in dlg.RedundantComboBox.Items)
                {
                    var splitName = redundantOption.OptionName.Split(splitterChars);
                    fileSet.Add(splitName[0]);
                    RTSet.Add(splitName[1]);
                }
            });
            // Checks the naming conventions are accurate, two different file names and 11 different retention times
            Assert.AreEqual(2, fileSet.Count);
            Assert.AreEqual(11, RTSet.Count);
            VerifyRedundant(dlg, 1, false, 725);
            OkDialog(dlg, () => dlg.Close());
        }

        private static void VerifyRedundant(ViewLibraryDlg dlg, int i, bool visible, int peakCount)
        {
            RunUI(() => dlg.SelectedIndex = i);
            // The peptide at index one does not have redundant spectra
            int waitMs = 1000;
            if (!TryWaitForConditionUI(waitMs, () => IsViewLibraryDlgState(dlg, i, visible, peakCount)) && !IsRecordMode)
            {
                RunUI(() =>
                {
                    string redundantMessage = visible
                        ? string.Format("Redundant list hidden with {0} selected",
                            dlg.SelectedIndex)
                        : string.Format("Redundant list visible with {0} selected, and {1} entries",
                            dlg.SelectedIndex, dlg.RedundantComboBox.Items.Count);
                    string peaksMessage =
                        string.Format("(peaks {0}, expected {1})", dlg.GraphItem.PeaksCount, peakCount);
                    string message = TextUtil.SpaceSeparate(redundantMessage, peaksMessage);
                    Assert.Fail(message);
                });
            }
            if (IsRecordMode)
                Console.Write(dlg.GraphItem.PeaksCount + @", ");
        }

        private static bool IsViewLibraryDlgState(ViewLibraryDlg dlg, int i, bool visible, int peakCount)
        {
            return dlg.SelectedIndex == i && dlg.IsVisibleRedundantSpectraBox == visible &&
                   peakCount == dlg.GraphItem.PeaksCount;
        }

    }
}
