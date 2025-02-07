/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AlphapeptdeepBuildLibraryTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestAlphaPeptDeepBuildLibrary()
        {
            TestFilesZip = "TestFunctional/AlphapeptdeepBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        private string LibraryPathWithoutIrt =>
            TestContext.GetTestPath("TestAlphapeptdeepBuildLibrary\\LibraryWithoutIrt.blib");

        
      
        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));
            PythonTestUtil pythonUtil = new PythonTestUtil(BuildLibraryDlg.ALPHAPEPTDEEP_PYTHON_VERSION, @"AlphaPeptDeep");
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithoutIrt = "AlphaPeptDeepLibraryWithoutIrt";

            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithoutIrt;
                buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;
                buildLibraryDlg.AlphaPeptDeep = true;
            });

            // Test the control path where Python needs installation and is Cancelled
            pythonUtil.CancelPython(buildLibraryDlg);

            // Test the control path where Python is installable
            if (!pythonUtil.InstallPython(buildLibraryDlg)) 
                OkDialog(buildLibraryDlg,buildLibraryDlg.OkWizardPage);

            Assert.IsTrue(pythonUtil.HavePythonPrerequisite(buildLibraryDlg));
            ILibraryBuilder builder = buildLibraryDlg.Builder;

            //PauseTest();
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            string libHash = PythonInstallerUtil.GetFileHash(builder.ProductLibraryPath());
            Assert.AreEqual(@"e00e068c5751ac56ba6934c6f2bbc2e1dcc16a5713408197d1a1d528dfe7e1e8", libHash);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
            });
            //Assert.AreEqual(
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);
            
        }
        protected override void Cleanup()
        {
            DirectoryEx.SafeDelete("TestAlphapeptdeepBuildLibrary");
        }
    }
}
