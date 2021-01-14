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

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AutoQC;

namespace AutoQCTest
{

    [TestClass]
    public class AutoQcConfigTest
    {

        

        [TestMethod]
        public void TestValidateMainSettings()
        {
            var skylinePath = TestUtils.GetTestFilePath("EmptyTemplate.sky");
            var folderToWatch = TestUtils.GetTestFilePath("Config");
            var resultsWindow = "51";
            var acquisitionTime = "500";

            var fileFilter = MainSettings.GetDefaultQcFileFilter();
            var instrumentType = MainSettings.GetDefaultInstrumentType();

            var badSkylinePath = TestUtils.GetTestFilePath("NotReal.sky");
            TestInvalidMainSettings(new MainSettings(badSkylinePath, folderToWatch, false, fileFilter, false, 
                resultsWindow, instrumentType, acquisitionTime),
                $"Skyline file {badSkylinePath} does not exist.");

            var badFolderPath = TestUtils.GetTestFilePath("NotReal");
            TestInvalidMainSettings(new MainSettings(skylinePath, badFolderPath, false, fileFilter, false,
                    resultsWindow, instrumentType, acquisitionTime),
                $"Folder to watch: {badFolderPath} does not exist.");

            var smallResultsWindow = "30";
            TestInvalidMainSettings(new MainSettings(skylinePath, folderToWatch, false, fileFilter, false,
                    smallResultsWindow, instrumentType, acquisitionTime),
                "\"Results time window\" cannot be less than 31 days.");

            var negativeAcquisitionTime = "-1";
            TestInvalidMainSettings(new MainSettings(skylinePath, folderToWatch, false, fileFilter, false,
                    resultsWindow, instrumentType, negativeAcquisitionTime),
                "\"Expected acquisition time\" cannot be less than 0 minutes.");

            var nonNumberAcquisitionTime = "aaa";
            try
            {
                new MainSettings(skylinePath, folderToWatch, false, fileFilter, false,
                    resultsWindow, instrumentType, nonNumberAcquisitionTime);
                Assert.Fail("Expected non-number acquisition time to throw exception upon MainSettings construction.");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual($"Invalid value for \"Acquisition Time\": {nonNumberAcquisitionTime}.", e.Message);
            }

            var testValidMainSettings = new MainSettings(TestUtils.GetTestFilePath("EmptyTemplate.sky"),
                TestUtils.GetTestFilePath("Config"),
                true, MainSettings.GetDefaultQcFileFilter(), true, "50", MainSettings.SCIEX,
                "500");
            try
            {
                testValidMainSettings.ValidateSettings();
            }
            catch (Exception)
            {
                Assert.Fail("Should have validated valid MainSettings");
            }

        }

        private void TestInvalidMainSettings(MainSettings testMainSettings, string expectedError)
        {
            try
            {
                testMainSettings.ValidateSettings();
                Assert.Fail("Should have failed to validate MainSettings with Error:" + Environment.NewLine + expectedError);
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(expectedError, e.Message);
            }
        }
        
        [TestMethod]
        public void TestValidatePanoramaSettings()
        {
            TestInvalidPanoramaSettings(new PanoramaSettings(true, "https://fake_panoramaweb.org/", "testEmail", "testPassword", "testFolder"), 
                "The server https://fake_panoramaweb.org/ does not exist");
            TestInvalidPanoramaSettings(new PanoramaSettings(true, "", "testEmail", "testPassword", "testFolder"),
                "Please specify a Panorama server URL.");
            TestInvalidPanoramaSettings(new PanoramaSettings(true, "https://panoramaweb.org/", "bad_email@bad.bad", "testPassword", "testFolder"),
                "The username and password could not be authenticated with the panorama server");
            TestInvalidPanoramaSettings(new PanoramaSettings(true, "https://panoramaweb.org/", "", "testPassword", "testFolder"),
                "Please specify a Panorama login email.");
            TestInvalidPanoramaSettings(new PanoramaSettings(true, "https://panoramaweb.org/", "testEmail", "not_the_password", "testFolder"),
                "The username and password could not be authenticated with the panorama server");
            TestInvalidPanoramaSettings(new PanoramaSettings(true, "https://panoramaweb.org/", "testEmail", "", "testFolder"),
                "Please specify a Panorama user password.");
            TestInvalidPanoramaSettings(new PanoramaSettings(true, "https://panoramaweb.org/", "testEmail", "testPassword", ""),
                "Please specify a folder on the Panorama server.");

            var noPublishToPanorama = new PanoramaSettings();
            var validPanoramaSettings = new PanoramaSettings(true, "https://panoramaweb.org/", "skyline_tester@proteinms.net", "lclcmsms", "UniqueFolder");
            try
            {
                noPublishToPanorama.ValidateSettings();
                validPanoramaSettings.ValidateSettings();
            }
            catch (Exception e)
            {
                Assert.Fail("Should have validated valid PanoramaSettings, threw exception: " + e.Message);
            }
        }


        private void TestInvalidPanoramaSettings(PanoramaSettings testPanoramaSettings, string expectedError)
        {
            try
            {
                testPanoramaSettings.ValidateSettings();
                Assert.Fail("Should have failed to validate PanoramaSettings with Error:" + Environment.NewLine + expectedError);
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(expectedError, e.Message);
            }
        }

        [TestMethod]
        public void TestGetLastArchivalDate()
        {
            var mainSettings = new MainSettings(@"C:\Dummy\path\Test_file.sky", "", false, null, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), "Thermo", MainSettings.ACQUISITION_TIME.ToString());
            var fsUtil = new TestFileSystemUtil();

            Assert.AreEqual(new DateTime(2015, 06, 01), mainSettings.GetLastArchivalDate(fsUtil));
        }



        [TestMethod]
        public void TestMainSettingsEquals()
        {
            var testMainSettings = TestUtils.GetTestMainSettings("test");
            Assert.IsTrue(Equals(testMainSettings, TestUtils.GetTestMainSettings("test")));
            var differentMainSettings = TestUtils.GetTestMainSettings("test2");
            Assert.IsFalse(Equals(testMainSettings, null));
            Assert.IsFalse(Equals(testMainSettings, differentMainSettings));
        }
        
        [TestMethod]
        public void TestPanoramaSettingsEquals()
        {
            var panoramaSettingsOne = new PanoramaSettings(true, "https://panoramaweb.org/", "bad@email.edu",
                "BadPassword", "badfolder");
            var panoramaSettingsTwo = new PanoramaSettings(true, "https://panoramaweb.org/", "bad@email.edu",
                "BadPassword", "badfolder");
            Assert.IsTrue(Equals(panoramaSettingsOne, panoramaSettingsTwo));
            var differentPanoramaSettings = new PanoramaSettings();
            Assert.IsFalse(Equals(panoramaSettingsOne, null));
            Assert.IsFalse(Equals(panoramaSettingsOne, differentPanoramaSettings));
        }
        
        [TestMethod]
        public void TestConfigEquals()
        {
            var testConfig = TestUtils.GetTestConfig("Config");
            Assert.IsTrue(Equals(testConfig, TestUtils.GetTestConfig("Config")));
            Assert.IsFalse(Equals(testConfig, TestUtils.GetTestConfig("other")));

            var differentMainSettings = new AutoQcConfig("Config", false, DateTime.MinValue, DateTime.MinValue, TestUtils.GetTestMainSettings("other"), TestUtils.GetTestPanoramaSettings(), TestUtils.GetTestSkylineSettings());
            Assert.IsFalse(Equals(testConfig, differentMainSettings));

            var publishingPanorama = new PanoramaSettings(true, "https://panoramaweb.org/", "bad@email.edu",
                "BadPassword", "badfolder");
            var differentPanoramaSettings = new AutoQcConfig("Config", false, DateTime.MinValue, DateTime.MinValue, TestUtils.GetTestMainSettings("Config"), publishingPanorama, TestUtils.GetTestSkylineSettings());
            Assert.IsFalse(Equals(testConfig, differentPanoramaSettings));
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