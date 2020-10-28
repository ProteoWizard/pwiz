/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using SkylineBatch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SkylineBatchTest
{
    [TestClass]
    public class MainSettingsTest
    {
        [TestMethod]
        public void TestValidateSettings()
        {
            var mainSettings = new MainSettings();
            TestValidateMainSettings(mainSettings, "Please specify path to: Skyline file.");

            const string skyPath = "C:\\dummy\\path\\Test.sky";
            mainSettings.TemplateFilePath = skyPath;
            TestValidateMainSettings(mainSettings, string.Format("Skyline file {0} does not exist.", skyPath));
        }

        private void TestValidateMainSettings(MainSettings mainSettings, string expectedError)
        {
            try
            {
                mainSettings.ValidateSettings();
                Assert.Fail("Should have failed to validate main settings");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(e.Message, expectedError);
            }
        }

       

        private class TestFileSystemUtil
        {
            private readonly Dictionary<string, TestFileInfo> fileMap;

            public TestFileSystemUtil()
            {
                fileMap = new Dictionary<string, TestFileInfo>();
                var file = "Test_file_2015_04.sky.zip";
                fileMap.Add(file, new TestFileInfo(file, new DateTime(2015, 04, 01)));
                file = "Test_file_2015_05.sky.zip";
                fileMap.Add(file, new TestFileInfo(file, new DateTime(2015, 05, 01)));
                file = "Test_file_2015_06.sky.zip";
                fileMap.Add(file, new TestFileInfo(file, new DateTime(2015, 06, 01)));
            }

            public IEnumerable<string> GetSkyZipFiles(string dirPath)
            {
                return fileMap.Keys;
            }

            public DateTime LastWriteTime(string filePath)
            {
                TestFileInfo fileInfo;
                if (fileMap.TryGetValue(filePath, out fileInfo))
                {
                    return fileInfo.LastWriteTime;
                }
                return DateTime.Today;
            }
        }

        private class TestFileInfo
        {
            private readonly string FilePath;
            public readonly DateTime LastWriteTime;

            public TestFileInfo(string filePath, DateTime lastWriteTime)
            {
                FilePath = filePath;
                LastWriteTime = lastWriteTime;
            }
        }

    }
}
