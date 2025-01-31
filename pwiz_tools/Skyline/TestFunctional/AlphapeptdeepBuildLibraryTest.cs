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
            //PauseTest();
            OkDialog(peptideSettings, peptideSettings.OkDialog);



            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
            });
            
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);
        }
        protected override void Cleanup()
        {
            DirectoryEx.SafeDelete("TestAlphapeptdeepBuildLibrary");
        }
    }
}
