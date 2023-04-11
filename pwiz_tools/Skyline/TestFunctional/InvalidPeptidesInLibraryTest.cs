/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests handling libraries that contain one or more invalid peptide sequences
    /// </summary>
    [TestClass]
    public class InvalidPeptidesInLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestInvalidPeptidesInLibrary()
        {
            TestFilesZip = @"TestFunctional\InvalidPeptidesInLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            VerifyInvalidPeptideMessage("BiblioSpecInvalidPeptides.blib", 2, 30);
            VerifyInvalidPeptideMessage("SomeInvalidPeptides.sptxt", 1, 4);
            VerifyInvalidPeptideMessage("InvalidElibPeptides.elib", 15, 48);
            VerifyInvalidPeptideMessage("BadExample.msp", 2, 3);
        }

        /// <summary>
        /// Verifies that adding the specified library to the document results in a warning message detailing the correct
        /// number of invalid peptides that were found.
        /// </summary>
        private void VerifyInvalidPeptideMessage(string libraryFile, int expectedInvalidCount, int expectedTotalCount)
        {
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library;
            });

            var editListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(editListDlg.AddItem);
            var libraryPath = TestFilesDir.GetTestPath(libraryFile);
            var libName = Path.GetFileNameWithoutExtension(libraryPath);
            RunUI(() =>
            {
                addLibDlg.LibraryName = libName;
                addLibDlg.LibraryPath = libraryPath;
            });
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            OkDialog(editListDlg, editListDlg.OkDialog);
            RunUI(()=>
            {
                peptideSettingsUI.PickedLibraries = peptideSettingsUI.PickedLibraries.Append(libName).ToArray();
            });

            var messageDlg = ShowDialog<AlertDlg>(peptideSettingsUI.OkDialog);

            // N.B. This assertion does not work in Japanese because it places text after the list of invalid peptides so StartsWith fails.
            if (!Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName.Equals(@"ja"))
            {
                var expectedMessage = string.Format(Resources.CachedLibrary_WarnInvalidEntries_, libName, expectedInvalidCount,
                    expectedTotalCount, string.Empty);
                StringAssert.StartsWith(messageDlg.Message, expectedMessage);
            }
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(peptideSettingsUI);
        }
    }
}
