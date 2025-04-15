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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Irt;
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
            TestFilesZip = "TestPerf/AlphapeptdeepBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        private string LibraryPathWithoutIrt =>
            TestContext.GetTestPath($"{TestFilesDir.FullPath}\\LibraryWithoutIrt.blib");
        private string LibraryPathWithIrt =>
            TestContext.GetTestPath($"{TestFilesDir.FullPath}\\LibraryWithIrt.blib");

        private PythonTestUtil _pythonTestUtil;
        private PeptideSettingsUI _peptideSettings;
        protected override void DoTest()
        {
            RunUI(() => OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky")));

            const string answerWithoutIrt = "predict_transformed.speclib.tsv";
            const string libraryWithoutIrt = "AlphaPeptDeepLibraryWithoutIrt";
            const string libraryWithIrt = "AlphaPeptDeepLibraryWithIrt";

            _peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            AlphapeptdeepBuildLibrary(libraryWithoutIrt, LibraryPathWithoutIrt, answerWithoutIrt);
            
            OkDialog(_peptideSettings, _peptideSettings.OkDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
                //spectralLibraryViewer.ChangeSelectedLibrary(libraryWithIrt);
            });
            
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);

            MultiButtonMsgDlg saveChangesDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.NewDocument(), WAIT_TIME);
            AssertEx.AreComparableStrings(SkylineResources.SkylineWindow_CheckSaveDocument_Do_you_want_to_save_changes, saveChangesDlg.Message);
            OkDialog(saveChangesDlg, saveChangesDlg.ClickNo);

            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);

        }

        /// <summary>
        /// Test goes through building of a Library by AlphaPeptDeep with or without iRT
        /// </summary>
        /// <param name="libraryName">Name of the library to build</param>
        /// <param name="libraryPath">Path of the library to build</param>
        /// <param name="answerFile">Path to library answersheet</param>
        /// <param name="iRTtype">iRT standard type</param>
        private void AlphapeptdeepBuildLibrary(string libraryName, string libraryPath, string answerFile, IrtStandard iRTtype = null)
        {

            string builtLibraryPath = null;
            RunLongDlg<BuildLibraryDlg>(_peptideSettings.ShowBuildLibraryDlg, buildLibraryDlg =>
            {
                RunUI(() =>
                {
                    buildLibraryDlg.LibraryName = libraryName;
                    buildLibraryDlg.LibraryPath = libraryPath;
                    buildLibraryDlg.AlphaPeptDeep = true;
                });

                if (!_pythonTestUtil.HavePythonPrerequisite(buildLibraryDlg))
                {
                    _pythonTestUtil.CancelPython(buildLibraryDlg);

                    _pythonTestUtil.InstallPythonTestNvidia(buildLibraryDlg);
                }
                else
                {
                    RunUI(() => { buildLibraryDlg.OkWizardPage(); });
                    WaitForClosedForm<BuildLibraryDlg>();
                }

                builtLibraryPath = buildLibraryDlg.BuilderLibFilepath;
            }, _ => { });

            TestResultingLibByValues(builtLibraryPath, TestFilesDir.GetTestPath(answerFile));
        }

        protected override void Cleanup()
        {
            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);

            DirectoryEx.SafeDelete(TestFilesDir.FullPath);

            TestFilesDir.CheckForFileLocks("TestAlphapeptdeepBuildLibrary");

            DirectoryEx.SafeDelete("TestAlphapeptdeepBuildLibrary");
        }

        private void TestResultingLibByValues(string product, string answer)
        {
            using (var answerReader = new StreamReader(answer))
            {
                using (var productReader = new StreamReader(product))
                {
                    AssertEx.FieldsEqual(productReader, answerReader, 13, null, true, 0, 1e-1);
                }
            }
        }
     
    }
}
