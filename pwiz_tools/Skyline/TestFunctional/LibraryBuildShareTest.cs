/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests creating a library, and ensures that the library can still be 
    /// read after doing "File > Share > Minimal".
    /// </summary>
    [TestClass]
    public class LibraryBuildShareTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestBuildLibraryShare()
        {
            TestFilesZip = @"TestFunctional\LibraryBuildShareTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUi.ShowBuildLibraryDlg);
            const string libraryName = "LibraryName";
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath("LibraryPath.blib");
                buildLibraryDlg.LibraryKeepRedundant = true;
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddInputFiles(new[] { TestFilesDir.GetTestPath("modless.pride.xml") });
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            Assert.IsTrue(WaitForCondition(() =>
                peptideSettingsUi.AvailableLibraries.Contains(libraryName)));
            RunUI(()=>peptideSettingsUi.PickedLibraries = new[] {libraryName});
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            Assert.AreEqual(1, SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count);
            string minimizedZipFile = TestFilesDir.GetTestPath("MinimizedDocument.sky.zip");
            RunUI(() =>
            {
                SkylineWindow.ImportFastaFile(TestFilesDir.GetTestPath("OneProtein.fasta"));
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("BeforeMinimize.sky"));
                SkylineWindow.ShareDocument(minimizedZipFile, ShareType.MINIMAL);
                SkylineWindow.OpenSharedFile(minimizedZipFile);
            });
            WaitForDocumentLoaded();
            Assert.AreEqual(1, SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count);
            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.First();
            var libraryKeys = library.Keys.ToArray();
            Assert.AreNotEqual(0, libraryKeys.Length);
            foreach (var key in libraryKeys)
            {
                var spectra = library.GetSpectra(key, null, LibraryRedundancy.all).ToArray();
                Assert.AreNotEqual(0, spectra.Length);
            }
        }
    }
}
