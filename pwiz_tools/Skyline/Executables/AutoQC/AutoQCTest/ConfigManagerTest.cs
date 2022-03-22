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
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AutoQC;
using SharedBatch.Properties;
using SharedBatchTest;

namespace AutoQCTest
{
    [TestClass]
    public class ConfigManagerTest: AbstractUnitTest
    {

        #region ConfigList Operations

        [TestMethod]
        public void TestSelectConfig()
        {
            var testConfigManager = TestUtils.GetTestConfigManager();
            try
            {
                testConfigManager.SelectConfig(0);
                Assert.IsTrue(testConfigManager.State.BaseState.Selected == 0);
                testConfigManager.SelectConfig(1);
                Assert.IsTrue(testConfigManager.State.BaseState.Selected == 1);
                testConfigManager.SelectConfig(2);
                Assert.IsTrue(testConfigManager.State.BaseState.Selected == 2);
                testConfigManager.DeselectConfig();
                Assert.IsTrue(testConfigManager.State.BaseState.Selected == -1);
            }
            catch (Exception e)
            {
                Assert.Fail("Expected to successfully select configurations within range. Threw exception: " + e.Message);
            }

            var selectedNegativeIndex = false;
            try
            {
                testConfigManager.State.BaseState.SelectIndex(-2).ValidateState();
                selectedNegativeIndex = true;
            }
            catch (IndexOutOfRangeException e)
            {
                Assert.AreEqual("There is no configuration at index: -2", e.Message);
            }
            Assert.IsTrue(!selectedNegativeIndex, "Expected index out of range exception");

            var selectedIndexAboveRange = false;
            try
            {
                testConfigManager.State.BaseState.SelectIndex(3).ValidateState();
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
            Assert.IsTrue(!testConfigManager.State.BaseState.HasConfigs());
            var addedConfig = TestUtils.GetTestConfig("one");
            testConfigManager.SetState(testConfigManager.State,
                testConfigManager.State.UserAddConfig(addedConfig, null));
            var oneConfig = TestUtils.ConfigListFromNames(new [] { "one" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(oneConfig));


            testConfigManager.SetState(testConfigManager.State,
                testConfigManager.State.UserAddConfig(TestUtils.GetTestConfig("two"), null));
            testConfigManager.SetState(testConfigManager.State,
                testConfigManager.State.UserAddConfig(TestUtils.GetTestConfig("three"), null));
            var threeConfigs = TestUtils.ConfigListFromNames(new[] { "one", "two", "three" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(threeConfigs));

            var addedDuplicateConfig = false;
            try
            {
                testConfigManager.SetState(testConfigManager.State,
                    testConfigManager.State.UserAddConfig(addedConfig, null));
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
            configManager.SetState(configManager.State, configManager.State.UserRemoveSelected(null, out _));
            Assert.AreEqual(0, configManager.State.BaseState.Selected);
            var oneRemoved = TestUtils.ConfigListFromNames(new [] { "two", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(oneRemoved));

            configManager.DeselectConfig();
            var removedNonexistantConfig = false;
            try
            {
                configManager.SetState(configManager.State, configManager.State.UserRemoveSelected(null, out _));
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
            configManager.SetState(configManager.State, configManager.State.ReplaceSelectedConfig(TestUtils.GetTestConfig("oneReplaced"), null));
            //Assert.IsTrue(configManager.ConfigOrderEquals(new[] { "oneReplaced", "two", "three" }));
            var expectedOneReplaced = TestUtils.ConfigListFromNames(new [] { "oneReplaced", "two", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(expectedOneReplaced));

            var replacedWithDuplicate = false;
            try
            {
                configManager.SelectConfig(1);
                configManager.SetState(configManager.State, configManager.State.ReplaceSelectedConfig(TestUtils.GetTestConfig("oneReplaced"), null));
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
                        return !configManager.State.GetSelectedConfig().IsEnabled;
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
            configManager.State.BaseState.ExportConfigs(configsXmlPath, 21.1M, new [] {0,1,2});
            int i = 0;
            while (configManager.State.BaseState.HasConfigs() && i < 4)
            {
                configManager.SelectConfig(0);
                configManager.SetState(configManager.State, configManager.State.UserRemoveSelected(null, out _));
                i++;
            }
            Assert.IsFalse(i == 4, "Failed to remove all configs.");

            var testingConfigs = TestUtils.ConfigListFromNames(new[] { "one", "two", "three" });
            configManager.Import(configsXmlPath, null);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            configManager.SelectConfig(2);
            configManager.SetState(configManager.State, configManager.State.UserRemoveSelected(null, out _));
            configManager.Import(TestUtils.GetTestFilePath("configs.xml"), null);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            File.Delete(configsXmlPath);
        }

        [TestMethod]
        public void TestCloseReopenConfigs()
        {
            TestUtils.InitializeSettingsImportExport();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SetState(configManager.State, configManager.State.UserAddConfig(TestUtils.GetTestConfig("four"), null));
            var testingConfigs = TestUtils.ConfigListFromNames(new [] { "one", "two", "three", "four" });
            configManager.Close();
            var testConfigManager = new AutoQcConfigManager();
            // Simulate loading saved configs from file
            testConfigManager.Import(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath, null);
            Assert.IsTrue(testConfigManager.ConfigListEquals(testingConfigs));
            var version = AutoQC.Properties.Settings.Default.XmlVersion;
            Assert.AreEqual(version, ConfigList.XmlVersion, $"Expected ConfigList version '{version}. But it was '{ConfigList.XmlVersion}.'");
        }

        // TODO (Ali): ask how best to modify this for new xml version scheme
        /*[TestMethod]
        public void TestConfigListVersion()
        {
            ClearInstalledVersion(); // Clear out the saved InstalledVersion in user.config

            var version = "1000.2.3.4";
            SetInstalledVersion(version);
            // Initialize an AutoQcConfigManager; This will set the version on the ConfigList to be the same as the InstalledVersion
            var configManager = new AutoQcConfigManager();
            Assert.AreEqual(version, ConfigList.Version);
            configManager.Close(); // persist to the <ConfigList> in user.config
            ReloadConfigList();
            Assert.AreEqual(version, ConfigList.Version,
                $"Expected ConfigList version to be {version}.  But it was {ConfigList.Version}");
            var userConfigVersion = GetUserConfigVersion(); // Version attribute saved in user.config
            Assert.AreEqual(version, userConfigVersion,
                $"Expected ConfigList version in user.config to be {version}.  But it was {userConfigVersion}");


            ClearInstalledVersion();
            // Initialize an AutoQcConfigManager; This will set the version on the ConfigList to be the same as the InstalledVersion (blank at this point)
            configManager = new AutoQcConfigManager();
            configManager.Close(); // This will persist the <ConfigList> to user.config
            Assert.AreEqual(string.Empty, ConfigList.Version,
                $"Expected ConfigList version after initializing AutoQcConfigManager to be blank since InstalledVersion is blank.  But it was '{ConfigList.Version}'.");

            // The Version attribute written to user.config should be 0.0.0.0 since InstalledVersion was blank 
            userConfigVersion = GetUserConfigVersion();
            Assert.IsNotNull(userConfigVersion);
            Assert.AreEqual(ConfigList.DUMMY_VER, userConfigVersion,
                    $"InstalledVersion was empty so we expect ConfigList in user.config to have a dummy version '{ConfigList.DUMMY_VER}'. But it was '{userConfigVersion}'.");

            ReloadConfigList();
            // Version should remain empty after reloading since we don't read the Version attribute from user.config
            Assert.AreEqual(string.Empty, ConfigList.Version,
                $"Expected ConfigList Version to be blank.  But it was '{ConfigList.Version}'.");

        }*/

        private static string GetUserConfigVersion()
        {
            var filePath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                .FilePath;

            using (var stream = new StreamReader(filePath))
            using (var reader = XmlReader.Create(stream))
            {
                while (reader.Read())
                {
                    if (reader.IsStartElement("ConfigList"))
                    {
                        return reader.GetAttribute("version");
                    }
                }
            }

            return null;
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
