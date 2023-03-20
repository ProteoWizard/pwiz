/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Startup;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that the Skyline does not keep any locks on the .sky file after the document has been opened.
    /// </summary>
    [TestClass]
    public class DocumentFileLockingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentFileLocking()
        {
            TestFilesZip = @"TestFunctional\DocumentFileLockingTest.zip";
            RunFunctionalTest();
        }
        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            // Test opening a file from the Start Page which will give an error
            var startWindow = Program.StartWindow;
            Assert.IsNotNull(startWindow);
            Assert.IsNotNull(FindOpenForm<StartPage>());
            {
                var versionTooHigh1 = TestFilesDir.GetTestPath("VersionTooHigh.sky");
                var alertDlg = ShowDialog<AlertDlg>(() => Program.StartWindow.OpenFile(versionTooHigh1));
                // Verify that the file can be deleted while the message is showing
                File.Delete(versionTooHigh1);
                // Careful about using RunUI and OkDialog here because they can
                // end up accessing a disposed StartPage
                startWindow.Invoke((Action)alertDlg.OkDialog);
            }
            WaitForOpenForm<SkylineWindow>();

            // Test opening a file without error
            var validFile = TestFilesDir.GetTestPath("ValidFile.sky");
            RunUI(() => Program.MainWindow.OpenFile(validFile));
            File.Delete(validFile);
            File.Delete("ValidFile.skyl");
            File.Delete("ValidFile.sky.view");
            Assert.IsFalse(File.Exists(validFile));
            RunUI(()=>SkylineWindow.SaveDocument());
            Assert.IsTrue(File.Exists(validFile));
            File.Delete(validFile);
            File.Delete("ValidFile.skyl");
            File.Delete("ValidFile.sky.view");

            var versionTooHigh2 = TestFilesDir.GetTestPath("VersionTooHigh2.sky");
            RunDlg<AlertDlg>(() => Program.MainWindow.OpenFile(versionTooHigh2), alertDlg =>
            {
                File.Delete(versionTooHigh2);
                alertDlg.OkDialog();
            });
        }
    }
}
