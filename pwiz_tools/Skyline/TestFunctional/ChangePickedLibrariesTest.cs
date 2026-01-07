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
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests changing the set of libraries in the document while the spectrum match window is displayed
    /// </summary>
    [TestClass]
    public class ChangePickedLibrariesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChangePickedLibraries()
        {
            TestFilesZip = @"TestFunctional\ChangePickedLibrariesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            Assert.IsNotNull(FindOpenForm<GraphSpectrum>());
            var libraryNames = new[] { "Rat (NIST) (Rat_plasma2)", "Rat (GPM) (Rat_plasma2)", "Rat_Prosit" };
            // Delay various amounts of milliseconds before changing the libraries in the Peptide Settings dialog.
            // The bug happens if the timer in GraphSpectrum.UpdateManager fires while pressing OK on the Peptide Settings dialog.
            foreach (var delay in new[] { 0, 20, 40, 80, 160 })
            {
                Thread.Sleep(delay);
                RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
                {
                    peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
                    peptideSettingsUi.PickedLibraries = libraryNames.Take(2).ToArray();
                    peptideSettingsUi.OkDialog();
                });
                Thread.Sleep(delay);
                RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
                {
                    peptideSettingsUi.PickedLibraries = libraryNames;
                    peptideSettingsUi.OkDialog();
                });
            }
        }
    }
}
