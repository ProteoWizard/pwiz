/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SkydFileModifiedTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSkydFileModified()
        {
            TestFilesZip = @"TestFunctional\SkydFileModifiedTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SkydFileModifiedTest.sky"));
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                // Show the Candidate Peaks form to make sure it can handle the .skyd file changing during
                // the test
                SkylineWindow.ShowCandidatePeaks();
            });
            var sourceFile = TestFilesDir.GetTestPath("S_1.mzML");
            for (int loopNumber = 0; loopNumber < 4; loopNumber++)
            {
                var filesToImport = new List<MsDataFileUri>();
                for (int fileNumber = 0; fileNumber < 4; fileNumber++)
                {
                    string targetFile = GetFileName(loopNumber, fileNumber);
                    if (loopNumber == 0)
                    {
                        // The first time through the test loop, copy "S_1.mzML" to several different names
                        File.Copy(sourceFile, targetFile);
                    }
                    else
                    {
                        // Subsequent times through the test loop, rename the previous iteration's files
                        // to new names
                        File.Move(GetFileName(loopNumber - 1, fileNumber), targetFile);
                    }
                    filesToImport.Add(new MsDataFilePath(targetFile));
                }
                ImportResultsDlg importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                RunUI(()=>importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFileReplicates(filesToImport));
                OkDialog(importResultsDlg, importResultsDlg.OkDialog);
                RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
                {
                    transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                    double methodMatchTolerance;
                    // Make a small change to the "Method Match Tolerance"
                    // This will cause Skyline to have to read the .skyd file during TransitionGroupDocNode.ChangeResults
                    if (0 == (loopNumber & 1))
                    {
                        methodMatchTolerance = 0.055;
                    }
                    else
                    {
                        methodMatchTolerance = 0.0551;
                    }
                    transitionSettingsUi.MZMatchTolerance = methodMatchTolerance;
                    transitionSettingsUi.OkDialog();
                });
                WaitForDocumentLoaded();
                // Save the document to a new name
                RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("Document" + loopNumber + ".sky")));
            }
        }

        /// <summary>
        /// Returns the file name to be used for a particular iteration of the test.
        /// </summary>
        protected string GetFileName(int loopNumber, int fileNumber)
        {
            return TestFilesDir.GetTestPath(fileNumber + "S_" + loopNumber + "_" + fileNumber + ".mzML");
        }
    }

}
