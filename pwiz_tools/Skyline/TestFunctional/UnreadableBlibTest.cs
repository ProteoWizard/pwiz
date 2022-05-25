/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Data.SQLite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class UnreadableBlibTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestUnreadableBlib()
        {
            TestFilesZip = @"TestFunctional\UnreadableBlibTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var skyPath = TestFilesDir.GetTestPath("UnreadableBlibTestDocument.sky");
            const string blibFileName = "UnreadableBlibTest.blib";
            var blibPath = TestFilesDir.GetTestPath(blibFileName);
            var backupPath = TestFilesDir.GetTestPath("backup.blib");
            File.Copy(blibPath, backupPath);
            var sqliteConnectionString = new SQLiteConnectionStringBuilder { DataSource = blibPath }.ToString();

            // Delete the "RefSpectra" table from the library, and make sure the user gets a helpful error
            // message when they try to open the document.
            using (var connection = new SQLiteConnection(sqliteConnectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE RefSpectra";
                    cmd.ExecuteNonQuery();
                }
            }
            var messageDlg = ShowDialog<MessageDlg>(() => SkylineWindow.OpenFile(skyPath));
            // Make sure that the error message mentions the name of the library file
            StringAssert.Contains(messageDlg.Message, blibFileName);
            // The "More Info" part of the error should mention the table that is missing
            StringAssert.Contains(messageDlg.DetailMessage, "RefSpectra");
            OkDialog(messageDlg, messageDlg.OkDialog);

            // Delete the "SpectrumSourceFiles" table and make sure the user gets a helpful error message again
            File.Copy(backupPath, blibPath, true);
            using (var connection = new SQLiteConnection(sqliteConnectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE SpectrumSourceFiles";
                    cmd.ExecuteNonQuery();
                }
            }
            messageDlg = ShowDialog<MessageDlg>(() => SkylineWindow.OpenFile(skyPath));
            StringAssert.Contains(messageDlg.Message, blibFileName);
            StringAssert.Contains(messageDlg.DetailMessage, "SpectrumSourceFiles");
            OkDialog(messageDlg, messageDlg.OkDialog);

            // Restore the valid database and make sure the document can open without error
            File.Copy(backupPath, blibPath, true);
            RunUI(()=>SkylineWindow.OpenFile(skyPath));
            WaitForDocumentLoaded();
        }
    }
}
