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
using AutoQC;

namespace AutoQCTest
{
    [TestClass]
    public class AutoQCLoggerTest
    {/*
        [TestMethod]
        public void TestTinyLog()
        {
            TestUtils.DeleteAllLogFiles();
            var logFile = TestUtils.GetTestFilePath("testLog.log");
            var logger = new AutoQcLogger(logFile);
            Assert.IsTrue(File.Exists(logFile));
            var fileInfo = new FileInfo(logFile);

            var createdFileLength = fileInfo.Length;

            logger.Log("Test line 1");
            logger.Archive();
            var fileLengthAfterArchive = fileInfo.Length;
            var logFilesAfterArchive = TestUtils.GetAllLogFiles();
            var archivedFileLength = new FileInfo(logFilesAfterArchive[1]).Length;
            TestUtils.DeleteAllLogFiles();
            
            Assert.IsTrue(createdFileLength == 0);
            Assert.IsTrue(fileLengthAfterArchive == 0);
            Assert.IsTrue(logFilesAfterArchive.Count == 2);
            
            Assert.IsTrue(archivedFileLength > 0);
        }*/
    }
}




