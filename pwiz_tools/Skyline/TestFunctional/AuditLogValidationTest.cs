/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.AuditLog;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AuditLogValidationTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestAuditLogValidation()
        {
            TestFilesZip = "TestFunctional/AuditLogValidationTest.zip";
            RunFunctionalTest();

        }

        protected override void DoTest()
        {
            //testing scenarios:
            // - valid audit log file - opens cleanly
            var documentFile = TestFilesDir.GetTestPath(@"AuditLogValidationTest/MethodEditClean.sky");
            WaitForCondition(() => File.Exists(documentFile));

            try
            {
                ShowDialog<MessageDlg>(() => SkylineWindow.OpenFile(documentFile), 3000);
                Assert.Fail("Failed to open a well-formed document with normal audit log.");
            }
            catch (AssertFailedException)
            {
                //nothing to do, this is an expected outcome of the normal test run.
            }

            // - audit log with modified content - expect message dialog to appear
            // - audit log with modified entry hashes - expect exception
            foreach(string fileName in new[] { @"AuditLogValidationTest/MethodEditContentMod.sky", @"AuditLogValidationTest/MethodEditEntryHashMod.sky" })
            {
                TestInvalidFile(fileName, AuditLogStrings
                    .AuditLogList_ReadFromFile_The_following_audit_log_entries_were_modified);
            }

            // - audit log with entry timestamps out of order - expect exception
            TestInvalidFile(@"AuditLogValidationTest/MethodEditEntryOrderMod.sky",
                AuditLogStrings
                    .AuditLogList_Validate_Audit_log_is_corrupted__Audit_log_entry_time_stamps_and_indices_should_be_decreasing);

        }

        private void TestInvalidFile(string fileName, string expectedMessage)
        {
            //replace print formats with regex and compile the testing regex.
            expectedMessage = new Regex(@"\{[0-9]+\}").Replace(expectedMessage, @"[0-9]+");
            expectedMessage = new Regex(@"\{[0-9]+:G\}").Replace(expectedMessage, ".*");
            Regex reTest = new Regex(expectedMessage);

            var documentFile = TestFilesDir.GetTestPath(fileName);
            //if no dialog is opened the method will fail on timeout
            var messageDialog = ShowDialog<MessageDlg>(() => SkylineWindow.OpenFile(documentFile), 3000);
            //making sure we get the expected error message
            string dialogMessage = "(none)";
            RunUI(() => dialogMessage = messageDialog.Message);
            if (reTest.Match(dialogMessage).Value == String.Empty)
            {
                Console.Write(@"Error message: " + dialogMessage);
                Assert.Fail("Unexpected exception type when opening document with entries out of order audit log.");
            }
            //close the dialog
            OkDialog(messageDialog, messageDialog.ClickOk);
            WaitForDocumentLoaded();
        }

    }
}