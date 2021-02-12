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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AutoQC;

namespace AutoQCTest
{
    [TestClass]
    public class ConfigManagerTest
    {

        #region ConfigList Operations

        [TestMethod]
        public void TestSelectConfig()
        {
            TestUtils.ClearSavedConfigurations();
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
            TestUtils.ClearSavedConfigurations();
            var testConfigManager = new AutoQcConfigManager();
            Assert.IsTrue(!testConfigManager.HasConfigs());
            var addedConfig = TestUtils.GetTestConfig("one");
            testConfigManager.AddConfiguration(addedConfig);
            var oneConfig = TestUtils.ConfigListFromNames(new [] { "one" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(oneConfig));

            testConfigManager.AddConfiguration(TestUtils.GetTestConfig("two"));
            testConfigManager.AddConfiguration(TestUtils.GetTestConfig("three"));
            var threeConfigs = TestUtils.ConfigListFromNames(new[] { "one", "two", "three" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(threeConfigs));

            var addedDuplicateConfig = false;
            try
            {
                testConfigManager.AddConfiguration(addedConfig);
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
            TestUtils.ClearSavedConfigurations();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.RemoveSelected();
            Assert.AreEqual(0, configManager.SelectedConfig);
            var oneRemoved = TestUtils.ConfigListFromNames(new [] { "two", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(oneRemoved));

            configManager.DeselectConfig();
            var removedNonexistantConfig = false;
            try
            {
                configManager.RemoveSelected();
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
            TestUtils.ClearSavedConfigurations();
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
            TestUtils.ClearSavedConfigurations();
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
            TestUtils.ClearSavedConfigurations();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.Import(TestUtils.GetTestFilePath("bad.xml"));
            configManager.SelectConfig(3);
            configManager.UpdateSelectedEnabled(true);
            Assert.IsTrue(!configManager.GetSelectedConfig().IsEnabled);
        }



        #endregion

        #region XML Parsing

        [TestMethod]
        public void TestImportExport()
        {
            TestUtils.ClearSavedConfigurations();
            var configsXmlPath = TestUtils.GetTestFilePath("configs.xml");
            var configManager = TestUtils.GetTestConfigManager();
            configManager.ExportConfigs(configsXmlPath, new [] {0,1,2});
            int i = 0;
            while (configManager.HasConfigs() && i < 4)
            {
                configManager.SelectConfig(0);
                configManager.RemoveSelected();
                i++;
            }
            Assert.IsFalse(i == 4, "Failed to remove all configs.");

            var testingConfigs = TestUtils.ConfigListFromNames(new[] { "one", "two", "three" });
            configManager.Import(configsXmlPath);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            configManager.SelectConfig(2);
            configManager.RemoveSelected();
            configManager.Import(TestUtils.GetTestFilePath("configs.xml"));
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            File.Delete(configsXmlPath);
        }

        [TestMethod]
        public void TestCloseReopenConfigs()
        {
            TestUtils.ClearSavedConfigurations();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.AddConfiguration(TestUtils.GetTestConfig("four"));
            var testingConfigs = TestUtils.ConfigListFromNames(new [] { "one", "two", "three", "four" });
            configManager.Close();
            var testConfigManager = new AutoQcConfigManager();
            Assert.IsTrue(testConfigManager.ConfigListEquals(testingConfigs));
        }

        #endregion

    }
}
