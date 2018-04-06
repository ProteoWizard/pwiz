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
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AutoQCTest
{
    [TestClass]
    public class MainSettingsTest
    {
        [TestMethod]
        public void TestValidateSettings()
        {
            var mainSettings = new MainSettings();
            TestValidateMainSettings(mainSettings, "Please specify path to a Skyline file.");

            const string skyPath = "C:\\dummy\\path\\Test.sky";
            mainSettings.SkylineFilePath = skyPath;
            TestValidateMainSettings(mainSettings, string.Format("Skyline file {0} does not exist.", skyPath));


//            mainSettingsTab = new MainSettingsTab(mainControl, logger);
//            logger.Clear();
//            Assert.IsFalse(mainSettingsTab.ValidateSettings());
//            log = logger.GetLog();
//            Assert.IsTrue(log.Contains(string.Format("Skyline file {0} does not exist.", skyPath)));
//            Assert.IsTrue(log.Contains(string.Format("Folder {0} does not exist.", folderPath)));
//            Assert.IsTrue(log.Contains(string.Format("Invalid value for \"Accumulation time window\": {0}.", accumWindow)));

//            accumWindow = "-1";
////            settings.ResultsWindowString = accumWindow;
//            logger.Clear();
//            mainSettingsTab = new MainSettingsTab(mainControl, logger);
//            Assert.IsFalse(mainSettingsTab.ValidateSettings());
//            log = logger.GetLog();
//            Assert.IsTrue(
//                log.Contains(string.Format("\"Accumulation time window\" cannot be less than {0} days.",
//                    MainSettings.ACCUM_TIME_WINDOW)));
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

        [TestMethod]
        public void TestGetLastArchivalDate()
        {
            var mainSettings = new MainSettings() { SkylineFilePath = @"C:\Dummy\path\Test_file.sky" };
            var fsUtil = new TestFileSystemUtil();

            Assert.AreEqual(new DateTime(2015, 06, 01), mainSettings.GetLastArchivalDate(fsUtil));
        }
     
        [TestMethod]
        public void TestAddArchiveArgs()
        {
            var mainSettings = new MainSettings() { SkylineFilePath = @"C:\Dummy\path\Test_file.sky" };
            var date = new DateTime(2015, 6, 17);
            mainSettings.LastArchivalDate = date;
            
            var args = mainSettings.GetArchiveArgs(mainSettings.LastArchivalDate, date);
            Assert.IsNull(args);
            Assert.AreEqual(date, mainSettings.LastArchivalDate);

            date = date.AddMonths(1); // 07/17/2015
            var archiveArg = string.Format("--share-zip={0}", "Test_file_2015_06.sky.zip");
            args = mainSettings.GetArchiveArgs(mainSettings.LastArchivalDate, date);
            Assert.AreEqual(archiveArg, args);
            Assert.AreEqual(date, mainSettings.LastArchivalDate);

            date = date.AddYears(1); // 06/17/2016
            archiveArg = string.Format("--share-zip={0}", "Test_file_2015_07.sky.zip");
            args = mainSettings.GetArchiveArgs(mainSettings.LastArchivalDate, date);
            Assert.AreEqual(archiveArg, args);
            Assert.AreEqual(date, mainSettings.LastArchivalDate);
        }

        [TestMethod]
        public void TestSkylineRunnerArgs()
        {
            const string skyFile = @"C:\Dummy\path\Test_file.sky";
            const string dataFile1 = @"C:\Dummy\path\Test1.raw";

            var mainSettings = MainSettings.GetDefault();
            mainSettings.SkylineFilePath = skyFile;
            
            var accumulationWindow = AccumulationWindow.Get(DateTime.Now, MainSettings.ACCUM_TIME_WINDOW);
            Assert.AreEqual(accumulationWindow.EndDate.Subtract(accumulationWindow.StartDate).Days + 1,
                MainSettings.ACCUM_TIME_WINDOW);
           
            var expected =
                string.Format("--in=\"{0}\" --remove-before={1} --import-file=\"{3}\" --import-on-or-after={2} --save", skyFile,
                    accumulationWindow.StartDate.ToShortDateString(),
                    accumulationWindow.StartDate.ToShortDateString(), dataFile1);

            var importContext = new ImportContext(dataFile1);
            Assert.IsFalse(importContext.ImportExisting);

            var args = mainSettings.SkylineRunnerArgs(importContext);
            Assert.AreEqual(expected, args.Trim());
        }

        [TestMethod]
        public void TestSkylineRunnerArgsImportExisting()
        {
            const string skyFile = @"C:\Dummy\path\Test_file.sky";
            const string dataFile1 = @"C:\Dummy\path\Test1.raw";
            const string dataFile2 = @"C:\Dummy\path\Test2.raw";

            var mainSettings = MainSettings.GetDefault();
            mainSettings.SkylineFilePath = skyFile;

            // Create an import context.
            var importContext = new ImportContext(new List<string>() { dataFile1, dataFile2 });
            Assert.IsTrue(importContext.ImportExisting);

            // Arguments for the first file.
            var expected =
                string.Format("--in=\"{0}\" --import-file=\"{1}\" --save", skyFile, dataFile1);
            importContext.GetNextFile();
            var args = mainSettings.SkylineRunnerArgs(importContext);
            Assert.AreEqual(expected, args.Trim());

            // Arguments for the second file
            importContext.GetNextFile();
            Assert.IsTrue(importContext.ImportingLast());
            expected =
                string.Format("--in=\"{0}\" --import-file=\"{1}\" --save", skyFile, dataFile2);

            args = mainSettings.SkylineRunnerArgs(importContext);
            Assert.AreEqual(expected, args.Trim());

            Assert.IsNull(importContext.GetNextFile());
        }

        private class TestFileSystemUtil : IFileSystemUtil
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
