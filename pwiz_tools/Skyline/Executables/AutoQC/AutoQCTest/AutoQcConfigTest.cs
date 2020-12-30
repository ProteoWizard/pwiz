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

            TestInvalidMainSettings("skylineFilePath", TestUtils.GetTestFilePath("NotReal.sky"), 
                $"Skyline file {TestUtils.GetTestFilePath("NotReal.sky")} does not exist.");
            TestInvalidMainSettings("folderToWatch", TestUtils.GetTestFilePath("NotReal"), 
                $"Folder to watch: {TestUtils.GetTestFilePath("NotReal")} does not exist.");
            TestInvalidMainSettings("resultsWindow", "30",
                "\"Results time window\" cannot be less than 31 days.");
            TestInvalidMainSettings("acquisitionTime", "aaaa", "Invalid value for \"Acquisition Time\": aaaa.");
            TestInvalidMainSettings("acquisitionTime", "-1", "\"Expected acquisition time\" cannot be less than 0 minutes.");
            var testValidMainSettings = new MainSettings(TestUtils.GetTestFilePath("EmptyTemplate.sky"), TestUtils.GetTestFilePath("Config"),
                true, MainSettings.GetDefaultQcFileFilter(), true, "50", MainSettings.SCIEX,
                "500", DateTime.MaxValue, DateTime.MinValue);
            try
            {
                testValidMainSettings.ValidateSettings();
            }
            catch (Exception)
            {
                Assert.Fail("Should have validated valid MainSettings");
            }

        }

        private void TestInvalidMainSettings(string invalidVariable, string invalidValue, string expectedError)
        {
            var skylineFilePath = TestUtils.GetTestFilePath("EmptyTemplate.sky");
            var folderToWatch = TestUtils.GetTestFilePath("Config");
            var resultsWindow = "51";
            var acquisitionTime = "500";

            var fileFilter = MainSettings.GetDefaultQcFileFilter();
            var instrumentType = MainSettings.GetDefaultInstrumentType();

            switch (invalidVariable)
            {
                case "skylineFilePath":
                    skylineFilePath = invalidValue;
                    break;
                case "folderToWatch":
                    folderToWatch = invalidValue;
                    break;
                case "resultsWindow":
                    resultsWindow = invalidValue;
                    break;
                case "acquisitionTime":
                    acquisitionTime = invalidValue;
                    break;
                default:
                    throw new ArgumentException("No such variable: " + invalidVariable);
            }

            try
            {
                var invalidMainSettings = new MainSettings(skylineFilePath, folderToWatch, true, fileFilter,
                    false, resultsWindow, instrumentType, acquisitionTime,
                    DateTime.MinValue, DateTime.MinValue);
                invalidMainSettings.ValidateSettings();
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
            TestInvalidPanoramaSettings("panoramaServerUrl", "https://fake_panoramaweb.org/",
                "The server https://fake_panoramaweb.org/ does not exist");
            TestInvalidPanoramaSettings("panoramaServerUrl", "",
                "Please specify a Panorama server URL.");
            TestInvalidPanoramaSettings("panoramaUserEmail", "bad_email@bad.bad",
                "The username and password could not be authenticated with the panorama server");
            TestInvalidPanoramaSettings("panoramaUserEmail", "",
                "Please specify a Panorama login email.");
            TestInvalidPanoramaSettings("panoramaPassword", "not_the_password",
                "The username and password could not be authenticated with the panorama server");
            TestInvalidPanoramaSettings("panoramaPassword", "",
                "Please specify a Panorama user password.");
            TestInvalidPanoramaSettings("panoramaFolder", "", 
                "Please specify a folder on the Panorama server.");


            var noPublishToPanorama = new PanoramaSettings();
            var validPanoramaSettings = new PanoramaSettings(true, "https://panoramaweb.org/", "alimarsh@mit.edu",
                "Alonfshss1!", "/00Developer/Ali/QC/");
            try
            {
                noPublishToPanorama.ValidateSettings();
                validPanoramaSettings.ValidateSettings();
            }
            catch (Exception)
            {
                Assert.Fail("Should have validated valid PanoramaSettings");
            }
        }


        private void TestInvalidPanoramaSettings(string invalidVariable, string invalidValue, string expectedError)
        {
            var panoramaServerUrl = "https://panoramaweb.org/";
            var panoramaUserEmail = "alimarsh@mit.edu";
            var panoramaPassword = "Alonfshss1!";
            var panoramaFolder = "/00Developer/Ali/QC/";

            switch (invalidVariable)
            {
                case "panoramaServerUrl":
                    panoramaServerUrl = invalidValue;
                    break;
                case "panoramaUserEmail":
                    panoramaUserEmail = invalidValue;
                    break;
                case "panoramaPassword":
                    panoramaPassword = invalidValue;
                    break;
                case "panoramaFolder":
                    panoramaFolder = invalidValue;
                    break;
                default:
                    throw new ArgumentException("No such variable: " + invalidVariable);
            }
            var invalidPanoramaSettings = new PanoramaSettings(true, panoramaServerUrl, panoramaUserEmail,
                panoramaPassword, panoramaFolder);
            try
            {
                invalidPanoramaSettings.ValidateSettings();
                Assert.Fail("Should have failed to validate MainSettings with Error:" + Environment.NewLine + expectedError);
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(expectedError, e.Message);
            }
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
            var panoramaSettingsOne = new PanoramaSettings(true, "https://panoramaweb.org/", "alimarsh@mit.edu",
                "Alonfshss1!", "/00Developer/Ali/QC/");
            var panoramaSettingsTwo = new PanoramaSettings(true, "https://panoramaweb.org/", "alimarsh@mit.edu",
                "Alonfshss1!", "/00Developer/Ali/QC/");
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

            var publishingPanorama = new PanoramaSettings(true, "https://panoramaweb.org/", "alimarsh@mit.edu",
                "Alonfshss1!", "/00Developer/Ali/QC/");
            var differentPanoramaSettings = new AutoQcConfig("Config", false, DateTime.MinValue, DateTime.MinValue, TestUtils.GetTestMainSettings("Config"), publishingPanorama, TestUtils.GetTestSkylineSettings());
            Assert.IsFalse(Equals(testConfig, differentPanoramaSettings));
        }

    }
}