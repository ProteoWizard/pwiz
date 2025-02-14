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
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class AlphapeptdeepBuildLibraryTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestAlphaPeptDeepBuildLibrary()
        {
            _pythonTestUtil = new PythonTestUtil(BuildLibraryDlg.ALPHAPEPTDEEP_PYTHON_VERSION, @"AlphaPeptDeep", false);
            TestFilesZip = "TestFunctional/AlphapeptdeepBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        private string LibraryPathWithoutIrt =>
            TestContext.GetTestPath("TestAlphapeptdeepBuildLibrary\\LibraryWithoutIrt.blib");
        private string LibraryPathWithIrt =>
            TestContext.GetTestPath("TestAlphapeptdeepBuildLibrary\\LibraryWithIrt.blib");

        private string StoredHashWithoutIrt = "2b31dec24e3a22bf2807769864ec6d7045236be32a9f78e0548e62975afe7318";

        private string StoredHashWithIrt = "2b31dec24e3a22bf2807769864ec6d7045236be32a9f78e0548e62975afe7318";

        private PythonTestUtil _pythonTestUtil;
        private PeptideSettingsUI _peptideSettings;
        private BuildLibraryDlg _buildLibraryDlg;

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));


            const string libraryWithoutIrt = "AlphaPeptDeepLibraryWithoutIrt";
            const string libraryWithIrt = "AlphaPeptDeepLibraryWithIrt";

            _peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            _buildLibraryDlg = ShowDialog<BuildLibraryDlg>(_peptideSettings.ShowBuildLibraryDlg);

            // test without iRT
            AlphapeptdeepBuildLibrary(libraryWithoutIrt, LibraryPathWithoutIrt, StoredHashWithoutIrt, null, true, null); //Cancel
            AlphapeptdeepBuildLibrary(libraryWithoutIrt, LibraryPathWithoutIrt, StoredHashWithoutIrt, null, false, null);
            AlphapeptdeepBuildLibrary(libraryWithoutIrt, LibraryPathWithoutIrt, StoredHashWithoutIrt, null, false, false);
            AlphapeptdeepBuildLibrary(libraryWithoutIrt, LibraryPathWithoutIrt, StoredHashWithoutIrt);


            OkDialog(_peptideSettings, _peptideSettings.OkDialog);
            _peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            _buildLibraryDlg = ShowDialog<BuildLibraryDlg>(_peptideSettings.ShowBuildLibraryDlg);

            // test with iRT
            AlphapeptdeepBuildLibrary(libraryWithIrt, LibraryPathWithIrt, StoredHashWithIrt, IrtStandard.PIERCE);
            OkDialog(_peptideSettings, _peptideSettings.OkDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithIrt);
            });

            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);
        }

        /// <summary>
        /// Test goes through building of a Library by AlphaPeptDeep with or without iRT
        /// </summary>
        /// <param name="libraryName">Name of the library to build</param>
        /// <param name="libraryPath">Path of the library to build</param>
        /// <param name="storedHash">checksum of the library to build</param>
        /// <param name="iRTtype">iRT standard type</param>
        /// <param name="cancelPython">press Cancel on Python</param>
        /// <param name="nvidiaClickNo">true clicks No, false clicks Yes, null clicks Cancel for Nvidia</param>
        private void AlphapeptdeepBuildLibrary( string libraryName, string libraryPath, string storedHash, IrtStandard iRTtype = null, bool cancelPython = false, bool? nvidiaClickNo = true)
        {
            

            RunUI(() =>
            {
                _buildLibraryDlg.LibraryName = libraryName;
                _buildLibraryDlg.LibraryPath = libraryPath;
                _buildLibraryDlg.AlphaPeptDeep = true;
                if (iRTtype != null) _buildLibraryDlg.IrtStandard = iRTtype;
            });

            // Test the control path where Python needs installation and is
            if (cancelPython)
                _pythonTestUtil.CancelPython(_buildLibraryDlg);

            //PauseTest();

            // Test the control path where Python is installable
            if (!_pythonTestUtil.InstallPython(_buildLibraryDlg, nvidiaClickNo)) 
                OkDialog(_buildLibraryDlg,_buildLibraryDlg.OkWizardPage);

            //PauseTest();


            if (nvidiaClickNo == false)
            {
                // Not Cancelled 
                Assert.IsTrue(_pythonTestUtil.HavePythonPrerequisite(_buildLibraryDlg));

                //PauseTest();

                OkDialog(_peptideSettings, _peptideSettings.OkDialog);

                // Test the hash of the created library
                string libHash = PythonInstallerUtil.GetFileHash(_buildLibraryDlg.BuilderLibFilepath);

                Assert.AreEqual(storedHash, libHash);
            }
        }
        protected override void Cleanup()
        {
            DirectoryEx.SafeDelete("TestAlphapeptdeepBuildLibrary");
        }
    }
}
