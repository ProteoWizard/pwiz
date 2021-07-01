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
using System.Configuration;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AutoQC;
using SharedBatch.Properties;

namespace AutoQCTest
{
    [TestClass]
    public class ConfigManagerTest
    {

        #region ConfigList Operations

        [TestMethod]
        public void TestSelectConfig()
        {
            var testConfigManager = TestUtils.GetTestConfigManager();
            try
            {
                testConfigManager.SelectConfig(0);
                Assert.IsTrue(testConfigManager.SelectedConfig == 0);
                testConfigManager.SelectConfig(1);
                Assert.IsTrue(testConfigManager.SelectedConfig == 1);
                testConfigManager.SelectConfig(2);
                Assert.IsTrue(testConfigManager.SelectedConfig == 2);
                testConfigManager.DeselectConfig();
                Assert.IsTrue(testConfigManager.SelectedConfig == -1);
            }
            catch (Exception e)
            {
                Assert.Fail("Expected to successfully select configurations within range. Threw exception: " + e.Message);
            }

            var selectedNegativeIndex = false;
            try
            {
                testConfigManager.SelectConfig(-1);
                selectedNegativeIndex = true;
            }
            catch (IndexOutOfRangeException e)
            {
                Assert.AreEqual("There is no configuration at index: -1", e.Message);
            }
            Assert.IsTrue(!selectedNegativeIndex, "Expected index out of range exception");

            var selectedIndexAboveRange = false;
            try
            {
                testConfigManager.SelectConfig(3);
                selectedIndexAboveRange = true;
            }
            catch (IndexOutOfRangeException e)
            {
                Assert.AreEqual("There is no configuration at index: 3", e.Message);
            }
            Assert.IsTrue(!selectedIndexAboveRange, "Expected index out of range exception");
        }

        [TestMethod]
        public void TestAddInsertConfig()
        {
            var testConfigManager = new AutoQcConfigManager();
            Assert.IsTrue(!testConfigManager.HasConfigs());
            var addedConfig = TestUtils.GetTestConfig("one");
            testConfigManager.UserAddConfig(addedConfig);
            var oneConfig = TestUtils.ConfigListFromNames(new [] { "one" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(oneConfig));

            testConfigManager.UserAddConfig(TestUtils.GetTestConfig("two"));
            testConfigManager.UserAddConfig(TestUtils.GetTestConfig("three"));
            var threeConfigs = TestUtils.ConfigListFromNames(new[] { "one", "two", "three" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(threeConfigs));

            var addedDuplicateConfig = false;
            try
            {
                testConfigManager.UserAddConfig(addedConfig);
                addedDuplicateConfig = true;
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual("Configuration \"one\" already exists.\r\nPlease enter a unique name for the configuration.", e.Message);
            }
            Assert.IsTrue(!addedDuplicateConfig, "Expected exception to be thrown when duplicate config added.");
            Assert.IsTrue(testConfigManager.ConfigListEquals(threeConfigs));
        }

        [TestMethod]
        public void TestRemoveConfig()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.UserRemoveSelected();
            Assert.AreEqual(0, configManager.SelectedConfig);
            var oneRemoved = TestUtils.ConfigListFromNames(new [] { "two", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(oneRemoved));

            configManager.DeselectConfig();
            var removedNonexistantConfig = false;
            try
            {
                configManager.UserRemoveSelected();
                removedNonexistantConfig = true;
            }
            catch (IndexOutOfRangeException e)
            {
                Assert.AreEqual("There is no configuration selected.", e.Message);
            }
            Assert.IsTrue(!removedNonexistantConfig, "Expected exception for nonexistent configuration removed.");
            Assert.IsTrue(configManager.ConfigListEquals(oneRemoved));
        }

        [TestMethod]
        public void TestSortConfigs()
        {
            var configManager = TestUtils.GetTestConfigManager(TestUtils.ConfigListFromNames(new [] {"b", "c", "a"}));
            configManager.SortByValue(0);
            Assert.IsTrue(configManager.ConfigOrderEquals(new[] { "a", "b", "c" }));
            configManager.SortByValue(0);
            Assert.IsTrue(configManager.ConfigOrderEquals(new[] { "c", "b", "a" }));

            var configCreatedManager = TestUtils.GetTestConfigManager(new List<AutoQcConfig>()
            {
                new AutoQcConfig("middle", false, DateTime.Now, DateTime.MinValue, TestUtils.GetTestMainSettings(), TestUtils.GetTestPanoramaSettings(false), TestUtils.GetTestSkylineSettings()),
                new AutoQcConfig("last", false, DateTime.MaxValue, DateTime.MinValue, TestUtils.GetTestMainSettings(), TestUtils.GetTestPanoramaSettings(false), TestUtils.GetTestSkylineSettings()),
                new AutoQcConfig("first", false, DateTime.MinValue, DateTime.MinValue, TestUtils.GetTestMainSettings(), TestUtils.GetTestPanoramaSettings(false), TestUtils.GetTestSkylineSettings())
            });
            configCreatedManager.SortByValue(2);
            Assert.IsTrue(configCreatedManager.ConfigOrderEquals(new[] { "last", "middle", "first" }));
            configCreatedManager.SortByValue(2);
            Assert.IsTrue(configCreatedManager.ConfigOrderEquals(new[] { "first", "middle", "last" }));


            var configUserManager = TestUtils.GetTestConfigManager(new List<AutoQcConfig>()
            {
                new AutoQcConfig("noUser", false, DateTime.Now, DateTime.MinValue, TestUtils.GetTestMainSettings(), TestUtils.GetTestPanoramaSettings(false), TestUtils.GetTestSkylineSettings()),
                TestUtils.GetTestConfig("User"),
            });
            configUserManager.SortByValue(1);
            Assert.IsTrue(configUserManager.ConfigOrderEquals(new[] { "User", "noUser" }));
            configUserManager.SortByValue(1);
            Assert.IsTrue(configUserManager.ConfigOrderEquals(new[] { "noUser", "User" }));
        }

        [TestMethod]
        public void TestReplaceConfig()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.ReplaceSelectedConfig(TestUtils.GetTestConfig("oneReplaced"));
            //Assert.IsTrue(configManager.ConfigOrderEquals(new[] { "oneReplaced", "two", "three" }));
            var expectedOneReplaced = TestUtils.ConfigListFromNames(new [] { "oneReplaced", "two", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(expectedOneReplaced));

            var replacedWithDuplicate = false;
            try
            {
                configManager.SelectConfig(1);
                configManager.ReplaceSelectedConfig(TestUtils.GetTestConfig("oneReplaced"));
                replacedWithDuplicate = true;
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual("Configuration \"oneReplaced\" already exists.\r\nPlease enter a unique name for the configuration.", e.Message);
            }
            Assert.IsTrue(!replacedWithDuplicate, "Expected exception for duplicate config.");
            Assert.IsTrue(configManager.ConfigOrderEquals(new[] { "oneReplaced", "two", "three" }));
        }

        [TestMethod]
        public void TestEnableInvalid()
        {
            TestUtils.InitializeSettingsImportExport();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.Import(TestUtils.GetTestFilePath("bad.qcfg"), null);
            configManager.SelectConfig(3);
            configManager.UpdateSelectedEnabled(true);

            new Thread(() =>
            {
                TestUtils.WaitForCondition(() =>
                    {
                        return !configManager.GetSelectedConfig().IsEnabled;
                    }, new TimeSpan(0, 0, 1), 100,
                    "Configuration started when it should have had an error because it was invalid");
            });
        }



        #endregion

        #region XML Parsing

        [TestMethod]
        public void TestImportExport()
        {
            TestUtils.InitializeSettingsImportExport();
            var configsXmlPath = TestUtils.GetTestFilePath("configs.xml");
            var configManager = TestUtils.GetTestConfigManager();
            configManager.ExportConfigs(configsXmlPath, "21.1.1.166", new [] {0,1,2});
            int i = 0;
            while (configManager.HasConfigs() && i < 4)
            {
                configManager.SelectConfig(0);
                configManager.UserRemoveSelected();
                i++;
            }
            Assert.IsFalse(i == 4, "Failed to remove all configs.");

            var testingConfigs = TestUtils.ConfigListFromNames(new[] { "one", "two", "three" });
            configManager.Import(configsXmlPath, null);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            configManager.SelectConfig(2);
            configManager.UserRemoveSelected();
            configManager.Import(TestUtils.GetTestFilePath("configs.xml"), null);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            File.Delete(configsXmlPath);
        }

        [TestMethod]
        public void TestCloseReopenConfigs()
        {
            TestUtils.InitializeSettingsImportExport();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.UserAddConfig(TestUtils.GetTestConfig("four"));
            var testingConfigs = TestUtils.ConfigListFromNames(new [] { "one", "two", "three", "four" });
            configManager.Close();
            var testConfigManager = new AutoQcConfigManager();
            // Simulate loading saved configs from file
            testConfigManager.Import(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath, null);
            Assert.IsTrue(testConfigManager.ConfigListEquals(testingConfigs));
            var version = AutoQC.Properties.Settings.Default.InstalledVersion;
            Assert.AreEqual(version, ConfigList.Version, $"Expected ConfigList version '{version}. But it was '{ConfigList.Version}.'");
        }

        [TestMethod]
        public void TestConfigListVersion()
        {
            ClearInstalledVersion(); // Clear out the saved InstalledVersion in user.config

            // Initialize an AutoQcConfigManager; This will set the version on the ConfigList to be the same as the InstalledVersion (blank at this point)
            var configManager = new AutoQcConfigManager();
            configManager.Close(); // This will persist the <ConfigList> to user.config
            Assert.AreEqual(string.Empty, ConfigList.Version,
                $"Expected ConfigList version after initializing AutoQcConfigManager to be blank since InstalledVersion is blank.  But it was '{ConfigList.Version}'.");


            ReloadConfigList();
            Assert.AreEqual(ConfigList.DUMMY_VER, ConfigList.Version,
                $"InstalledVersion was empty so we expect ConfigList in user.config to have a dummy version '{ConfigList.DUMMY_VER}'. But it was '{ConfigList.Version}'.");
            
            var version = "1000.2.3.4";
            SetInstalledVersion(version);
            // Initialize an AutoQcConfigManager; This will set the version on the ConfigList to be the same as the InstalledVersion
            configManager = new AutoQcConfigManager();
            configManager.Close(); // persist to the <ConfigList> in user.config
            Assert.AreEqual(version, ConfigList.Version,
                $"Expected ConfigList version to be {version}.  But it was {ConfigList.Version}");
            

            ReloadConfigList();
            Assert.AreEqual(version, ConfigList.Version,
                $"Expect ConfigList read from user.config to have version {version}. But it was {ConfigList.Version}");

        }

        private static void ReloadConfigList()
        {
            Settings.Default.Reload();
            // ReSharper disable once NotAccessedVariable
            var list = Settings.Default.ConfigList; // Read from file
        }

        private static void SetInstalledVersion(string version)
        {
            AutoQC.Properties.Settings.Default.InstalledVersion = version;
            AutoQC.Properties.Settings.Default.Save(); // Persist the version to user.config
            AutoQC.Properties.Settings.Default.Reload();
            Assert.AreEqual(version, AutoQC.Properties.Settings.Default.InstalledVersion,
                $"Expected InstalledVersion to be '{version}'. But it was {AutoQC.Properties.Settings.Default.InstalledVersion}.");
        }

        private static void ClearInstalledVersion()
        {
            if (!string.IsNullOrEmpty(AutoQC.Properties.Settings.Default.InstalledVersion))
            {
                // Tried using Properties.Settings.Default.Properties.Remove() and Properties.Settings.Default.Properties.Clear()
                // but that did not remove the property from the user.config file.  We want to clear the InstalledVersion that
                // may have been set by a previous test.
                SetInstalledVersion(string.Empty); // Clear the InstalledVersion
            }
        }

        #endregion

    }
}
