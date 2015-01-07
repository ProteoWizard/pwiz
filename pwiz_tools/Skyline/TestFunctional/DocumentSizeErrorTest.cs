/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for DocumentSizeErrorTest
    /// </summary>
    [TestClass]
    public class DocumentSizeErrorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentSizeError()
        {
            TestFilesZip = @"TestFunctional\DocumentSizeErrorTest.zip";
            RunFunctionalTest();
        }


        /// <summary>
        /// Tests that we fail gracefully when document settings would result in an excessive number of nodes.
        /// </summary>
        protected override void DoTest()
        {
            // User managed to create an interesting incomplete file that can only be opened with proper local paths
            string text = File.ReadAllText(TestFilesDir.GetTestPath("wildsettings.sky"));
            text = text.Replace(@"__TESTPATH__", TestFilesDir.FullPath);
            File.WriteAllText(TestFilesDir.GetTestPath("wildsettings.sky"), text);

            // Open the file and it should fail quickly
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("wildsettings.sky")));
            // Should present an error dialog
            var errorDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() =>
            {
                Assert.IsTrue(errorDlg.Message.Contains(String.Format(
                                    Resources.PeptideGroupDocNode_ChangeSettings_The_current_document_settings_would_cause_the_number_of_targeted_transitions_to_exceed__0_n0___The_document_settings_must_be_more_restrictive_or_add_fewer_proteins_,
                                    SrmDocument.MAX_TRANSITION_COUNT)));
                errorDlg.OkDialog();
            });
        }
    }
}
