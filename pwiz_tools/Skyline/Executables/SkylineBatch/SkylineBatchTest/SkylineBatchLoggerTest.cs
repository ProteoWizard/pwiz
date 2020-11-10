/*
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class SkylineBatchLoggerTest
    {
        [TestMethod]
        public void TestTinyLog()
        {
            TestUtils.DeleteAllLogFiles();
            var logFile = TestUtils.GetTestFilePath("testLog.log");
            var logger = new SkylineBatchLogger(logFile);
            Assert.IsTrue(File.Exists(logFile));
            var fileInfo = new FileInfo(logFile);
            Assert.IsTrue(fileInfo.Length == 0);

            logger.Log("Test line 1");
            logger.Archive();
            Assert.IsTrue(fileInfo.Length == 0);
            var logFiles = TestUtils.GetAllLogFiles();
            Assert.IsTrue(logFiles.Count == 2);
            Assert.IsTrue(new FileInfo(logFiles[1]).Length > 0);
            TestUtils.DeleteAllLogFiles();
        }
    }
}




