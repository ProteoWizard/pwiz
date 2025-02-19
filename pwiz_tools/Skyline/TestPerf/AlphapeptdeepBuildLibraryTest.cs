﻿/*
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
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
            _pythonTestUtil = new PythonTestUtil(BuildLibraryDlg.ALPHAPEPTDEEP_PYTHON_VERSION, @"AlphaPeptDeep", true);
            TestFilesZip = "TestFunctional/AlphapeptdeepBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        private string LibraryPathWithoutIrt =>
            TestContext.GetTestPath("TestAlphapeptdeepBuildLibrary\\LibraryWithoutIrt.blib");
        private string LibraryPathWithIrt =>
            TestContext.GetTestPath("TestAlphapeptdeepBuildLibrary\\LibraryWithIrt.blib");

        private string _storedHashWithoutIrt = @"2b31dec24e3a22bf2807769864ec6d7045236be32a9f78e0548e62975afe7318";

        private string _storedHashWithIrt = @"2b31dec24e3a22bf2807769864ec6d7045236be32a9f78e0548e62975afe7318";

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
            RunUI(() =>
            {
                _buildLibraryDlg.LibraryName = libraryWithoutIrt;
                _buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;
                _buildLibraryDlg.AlphaPeptDeep = true;
            });

            AlertDlg nvidiaResult;
            if (!_pythonTestUtil.HavePythonPrerequisite(_buildLibraryDlg))
            {
                _pythonTestUtil.CancelPython(_buildLibraryDlg);
                

                _pythonTestUtil.InstallPythonTestNvidia(_buildLibraryDlg);
                WaitForClosedForm<LongWaitDlg>();

                //_pythonTestUtil.InstallPython(_buildLibraryDlg);
                //WaitForClosedForm<LongWaitDlg>();

                //AbstractFunctionalTest.RunLongDlg<LongWaitDlg>(nvidiaResult.OkDialog, buildDlg => { }, dlg => { });
            }
            
            AlphapeptdeepBuildLibrary(libraryWithoutIrt, LibraryPathWithoutIrt, _storedHashWithoutIrt, IrtStandard.PIERCE);
            OkDialog(_peptideSettings, _peptideSettings.OkDialog);

            // test with iRT
            _peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            _buildLibraryDlg = ShowDialog<BuildLibraryDlg>(_peptideSettings.ShowBuildLibraryDlg);

            AlphapeptdeepBuildLibrary(libraryWithIrt, LibraryPathWithIrt, _storedHashWithIrt, IrtStandard.PIERCE);
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
        private void AlphapeptdeepBuildLibrary( string libraryName, string libraryPath, string storedHash, IrtStandard iRTtype = null)
        {

            RunUI(() =>
            {
                _buildLibraryDlg.LibraryName = libraryName;
                _buildLibraryDlg.LibraryPath = libraryPath;
                _buildLibraryDlg.AlphaPeptDeep = true;
                if (iRTtype != null) _buildLibraryDlg.IrtStandard = iRTtype;
            });

            // Test the control path where Python needs installation and is

            if (!_pythonTestUtil.HavePythonPrerequisite(_buildLibraryDlg))
            {
                    //PauseTest();

                    // Test the control path where Python is installable
                    if (!_pythonTestUtil.InstallPython(_buildLibraryDlg))
                    {
                        OkDialog(_buildLibraryDlg, _buildLibraryDlg.OkWizardPage);
                        AbstractFunctionalTest.WaitForClosedForm<LongWaitDlg>();
                    }

                //PauseTest({
                TestResultingLib(storedHash);
            }
            else
            {
                AbstractFunctionalTest.RunLongDlg<LongWaitDlg>(_buildLibraryDlg.OkWizardPage, WaitForClosedForm, dlg => { 
                });

                TestResultingLib(storedHash);
            }
            
        }
        protected override void Cleanup()
        {
            DirectoryEx.SafeDelete("TestAlphapeptdeepBuildLibrary");
        }

        private void TestResultingLib(string storedHash)
        {
            Assert.IsTrue(_pythonTestUtil.HavePythonPrerequisite(_buildLibraryDlg));

            //PauseTest();

            //OkDialog(_peptideSettings, _peptideSettings.OkDialog);

            // Test the hash of the created library
            string libHash = PythonInstallerUtil.GetFileHash(_buildLibraryDlg.BuilderLibFilepath);

            Assert.AreEqual(storedHash, libHash);
        }
    }
}
