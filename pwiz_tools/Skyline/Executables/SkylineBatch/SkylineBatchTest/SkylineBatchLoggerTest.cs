﻿/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
 
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class SkylineBatchLoggerTest
    {
        [TestMethod]
        public void TestTinyLog()
        {
            var logFolder = TestUtils.GetTestFilePath("OldLogs\\TestTinyLog");
            if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);

            var logger = TestUtils.GetTestLogger(logFolder);
            var logFile = logger.GetFile();
            Assert.IsTrue(File.Exists(logFile));
            var fileInfo = new FileInfo(logFile);

            var createdFileLength = fileInfo.Length;

            logger.Log("Test line 1");
            var textLength = new FileInfo(logger.GetFile()).Length;

            var oldLogger = logger.Archive();
            var fileLengthAfterArchive = new FileInfo(logger.GetFile()).Length;
            var logFilesAfterArchive = TestUtils.GetAllLogFiles();
            var archivedFileLength = logFilesAfterArchive.Count > 1 ? new FileInfo(logFilesAfterArchive[1]).Length : -1;
            logger.Delete();
            oldLogger.Delete();
            Directory.Delete(logFolder);

            Assert.AreEqual(0, createdFileLength, "Expected new log to not have text.");
            Assert.AreEqual(0, fileLengthAfterArchive, "Expected log to have no text after it was archived to new file.");
            Assert.AreEqual(2, logFilesAfterArchive.Count, $"Expected a log file and an archived file. Found {logFilesAfterArchive.Count} file(s) instead.");
            Assert.AreEqual(textLength, archivedFileLength, "Expected archived file to have text.");
        }




        [TestMethod]
        public async Task TestMultipleLogs()
        {
            TestUtils.InitializeRInstallation();

            var logFolder = TestUtils.GetTestFilePath("MultipleLogsTest");
            if (Directory.Exists(logFolder)) Directory.Delete(logFolder);
            Directory.CreateDirectory(logFolder);
            
            var testConfigManager = new SkylineBatchConfigManager(new Logger(Path.Combine(logFolder, "testLog.log"), "testLog.log"));
            testConfigManager.AddConfiguration(TestUtils.GetTestConfig());
            Assert.IsTrue(testConfigManager.HasOldLogs() == false, "Expected no old logs.");

            // Run and cancel three times creates two old logs
            await testConfigManager.RunAllEnabled(4);
            testConfigManager.CancelRunners();
            await testConfigManager.RunAllEnabled(4);
            testConfigManager.CancelRunners();
            await testConfigManager.RunAllEnabled(4);
            testConfigManager.CancelRunners();

            var hasOldLogs = testConfigManager.HasOldLogs();
            var numberOldLogs = testConfigManager.GetOldLogFiles().Length;

            testConfigManager.DeleteLogs(testConfigManager.GetOldLogFiles());
            testConfigManager.GetSelectedLogger().Delete();

            var hasOldLogsAfterDelete = testConfigManager.HasOldLogs();
            var numberOldLogsAfterDelete = testConfigManager.GetOldLogFiles().Length;
            Directory.Delete(logFolder);


            Assert.AreEqual(2, numberOldLogs, $"Expected 2 old logs but got {numberOldLogs}");
            Assert.AreEqual(true, hasOldLogs, "Expected old logs.");
            
            Assert.AreEqual(0, numberOldLogsAfterDelete, $"Expected 0 old logs but got {numberOldLogs}");
            Assert.AreEqual(false, hasOldLogsAfterDelete, "Expected no old logs after deletion.");
        }

    }
}




