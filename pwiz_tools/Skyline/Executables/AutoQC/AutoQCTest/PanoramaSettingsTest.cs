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
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AutoQCTest
{
    [TestClass]
    public class PanoramaSettingsTest
    {
        [TestMethod]
        public void TestValidatePanoramaSettings()
        {
            var logger = new TestLogger();
            var settings = new PanoramaSettings();

            var mainControl = new TestAppControl();
            mainControl.SetUIPanoramaSettings(settings);

            var panoramaSettingsTab = new PanoramaSettingsTab(mainControl, logger);
            
            Assert.IsTrue(panoramaSettingsTab.ValidateSettings());
            var log = logger.GetLog();
            Assert.IsTrue(log.Contains("Will NOT publish Skyline documents to Panorama."));

            settings.PublishToPanorama = true;
            logger.Clear();
            panoramaSettingsTab.Settings = settings;
            Assert.IsFalse(panoramaSettingsTab.ValidateSettings());
            log = logger.GetLog();
            Assert.IsTrue(log.Contains("Please specify a Panorama server URL."));
            Assert.IsTrue(log.Contains("Please specify a Panorama login name."));
            Assert.IsTrue(log.Contains("Please specify a Panorama user password."));
            Assert.IsTrue(log.Contains("Please specify a folder on the Panorama server."));
        }
    }
}
