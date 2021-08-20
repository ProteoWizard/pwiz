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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;


namespace SkylineBatchTest
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
            testConfigManager.GetSelectedLogger().Delete();
        }

        [TestMethod]
        public void TestAddInsertConfig()
        {
            var testConfigManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());
            var addedConfig = TestUtils.GetTestConfig("one");
            testConfigManager.UserAddConfig(addedConfig);
            var oneConfig = TestUtils.ConfigListFromNames(new List<string> { "one" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(oneConfig));

            testConfigManager.UserAddConfig(TestUtils.GetTestConfig("two"));
            testConfigManager.UserAddConfig(TestUtils.GetTestConfig("three"));
            var threeConfigs = TestUtils.ConfigListFromNames(new List<string> { "one", "two", "three" });
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
            Assert.IsTrue(!addedDuplicateConfig, "Expected exception for duplicate configuration added.");
            Assert.IsTrue(testConfigManager.ConfigListEquals(threeConfigs));
            testConfigManager.GetSelectedLogger().Delete();
        }

        [TestMethod]
        public void TestRemoveConfig()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.UserRemoveSelected();
            Assert.IsTrue(configManager.State.BaseState.Selected == 0);
            var oneRemoved = TestUtils.ConfigListFromNames(new List<string> { "two", "three" });
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
            configManager.GetSelectedLogger().Delete();
        }

        [TestMethod]
        public void TestMoveConfig()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.MoveSelectedConfig(false);
            var expectedMovedForward = TestUtils.ConfigListFromNames(new List<string> { "two", "one", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(expectedMovedForward));

            configManager.SelectConfig(2);
            configManager.MoveSelectedConfig(true);
            var expectedMovedBackward = TestUtils.ConfigListFromNames(new List<string> { "two", "three", "one" });
            Assert.IsTrue(configManager.ConfigListEquals(expectedMovedBackward));
            configManager.GetSelectedLogger().Delete();
        }

        [TestMethod]
        public void TestReplaceConfig()
        {
            TestUtils.InitializeRInstallation();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.UserReplaceSelected(TestUtils.GetTestConfig("oneReplaced"));
            var expectedOneReplaced = TestUtils.ConfigListFromNames(new List<string> { "oneReplaced", "two", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(expectedOneReplaced));

            var replacedWithDuplicate = false;
            try
            {
                configManager.SelectConfig(1);
                configManager.UserReplaceSelected(TestUtils.GetTestConfig("oneReplaced"));
                replacedWithDuplicate = true;
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual("Configuration \"oneReplaced\" already exists.\r\nPlease enter a unique name for the configuration.", e.Message);
            }
            Assert.IsTrue(!replacedWithDuplicate, "Expected exception for duplicate config.");
            Assert.IsTrue(configManager.ConfigListEquals(expectedOneReplaced));
            configManager.GetSelectedLogger().Delete();
        }


       


        #endregion

        #region XML Parsing

        [TestMethod]
        public void TestImportExport()
        {
            TestUtils.InitializeRInstallation();
            TestUtils.InitializeSettingsImportExport();
            var configsXmlPath = TestUtils.GetTestFilePath("configs.xml");
            var configManager = TestUtils.GetTestConfigManager();
            configManager.State.BaseState.ExportConfigs(configsXmlPath, SkylineBatch.Properties.Settings.Default.XmlVersion, new [] {0,1,2});
            int i = 0;
            while (configManager.State.BaseState.HasConfigs() && i < 4)
            {
                configManager.SelectConfig(0);
                configManager.UserRemoveSelected();
                i++;
            }
            Assert.IsFalse(i == 4, "Failed to remove all configs.");

            var testingConfigs = TestUtils.ConfigListFromNames(new List<string> { "one", "two", "three" });
            configManager.Import(configsXmlPath, null);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            configManager.SelectConfig(2);
            configManager.UserRemoveSelected();

            configManager.Import(TestUtils.GetTestFilePath("configs.xml"), null);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            configManager.GetSelectedLogger().Delete();
            File.Delete(configsXmlPath);
        }

        [TestMethod]
        public void TestCloseReopenConfigs()
        {
            TestUtils.InitializeRInstallation();
            TestUtils.InitializeSettingsImportExport();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.UserAddConfig(TestUtils.GetTestConfig("four"));
            var testingConfigs = TestUtils.ConfigListFromNames(new List<string> { "one", "two", "three", "four" });
            configManager.Close();
            configManager.GetSelectedLogger().Delete();
            var testConfigManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());
            // Simulate loading saved configs from file
            configManager.Import(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath, null);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));
            testConfigManager.GetSelectedLogger().Delete();
        }

        #endregion

    }
}
