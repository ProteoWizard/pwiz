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
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class ConfigManagerTest
    {
        [TestMethod]
        public void TestAddConfig()
        {
            TestUtils.ClearSavedConfigurations();
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(TestUtils.GetTestFilePath("TestLog.log")));
            AssertEquals(testConfigManager.ConfigList, new List<SkylineBatchConfig>());
            var addedConfig = TestUtils.GetTestConfig("one");
            testConfigManager.AddConfiguration(addedConfig);
            var oneConfig = TestUtils.ConfigListFromNames(new List<string>() { "one" });
            AssertEquals(testConfigManager.ConfigList, oneConfig);
            

            testConfigManager.AddConfiguration(TestUtils.GetTestConfig("two"));
            testConfigManager.AddConfiguration(TestUtils.GetTestConfig("three"));
            var threeConfigs = TestUtils.ConfigListFromNames(new List<string>() { "one", "two", "three" });
            AssertEquals(testConfigManager.ConfigList, threeConfigs);

            try
            {
                testConfigManager.AddConfiguration(addedConfig);
                Assert.Fail("Expected exception for duplicate configuration added.");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(e.Message, "Failed operation \"Add\": Configuration \"one\" already exists.");
            }
            AssertEquals(testConfigManager.ConfigList, threeConfigs);

            TestUtils.ClearSavedConfigurations();
        }

        [TestMethod]
        public void TestRemoveConfig()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.Remove(TestUtils.GetTestConfig("one"));
            var oneRemoved = TestUtils.ConfigListFromNames(new List<string>() { "two", "three" });
            AssertEquals(configManager.ConfigList, oneRemoved);

            try
            {
                configManager.Remove(TestUtils.GetTestConfig("one"));
                Assert.Fail("Expected exception for nonexistent configuration removed.");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(e.Message, "Failed operation \"Remove\": Configuration \"one\" does not exist.");
            }
            AssertEquals(configManager.ConfigList, oneRemoved);

            TestUtils.ClearSavedConfigurations();
        }

        [TestMethod]
        public void TestMoveConfig()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.MoveConfig(0, 2);
            var expectedMovedForward = TestUtils.ConfigListFromNames(new List<string>() {"two", "three", "one"});
            AssertEquals(configManager.ConfigList, expectedMovedForward);

            configManager.MoveConfig(1, 0);
            var expectedMovedBackward = TestUtils.ConfigListFromNames(new List<string>() {"three", "two", "one" });
            AssertEquals(configManager.ConfigList, expectedMovedBackward);

            TestUtils.ClearSavedConfigurations();
        }

        [TestMethod]
        public void TestReplaceConfig()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.ReplaceConfig(TestUtils.GetTestConfig("one"), TestUtils.GetTestConfig("oneReplaced"));
            var expectedOneReplaced = TestUtils.ConfigListFromNames(new List<string>() { "oneReplaced", "two", "three" });
            AssertEquals(configManager.ConfigList, expectedOneReplaced);

            try
            {
                configManager.ReplaceConfig(TestUtils.GetTestConfig("two"), TestUtils.GetTestConfig("oneReplaced"));
                Assert.Fail("Expected exception for duplicate config.");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(e.Message, "Failed operation \"Replace\": Configuration \"oneReplaced\" already exists.");
            }
            AssertEquals(configManager.ConfigList, expectedOneReplaced);
            try
            {
                configManager.ReplaceConfig(TestUtils.GetTestConfig("one"), TestUtils.GetTestConfig("new"));
                Assert.Fail("Expected exception for nonexistent config.");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(e.Message, "Failed operation \"Replace\": Configuration \"one\" does not exist.");
            }
            AssertEquals(configManager.ConfigList, expectedOneReplaced);

            TestUtils.ClearSavedConfigurations();
        }

        [TestMethod]
        public void TestImportExport()
        {
            var configsXmlPath = TestUtils.GetTestFilePath("configs.xml");
            var configManager = TestUtils.GetTestConfigManager();
            configManager.ExportAll(configsXmlPath);
            var testingConfigs = TestUtils.ConfigListFromNames(new List<string>() {"one", "two", "three"});
            foreach(var config in testingConfigs) 
                configManager.Remove(config);
            Assert.IsTrue(configManager.ConfigList.Count == 0);

            configManager.Import(configsXmlPath);
            AssertEquals(configManager.ConfigList, testingConfigs);

            configManager.Remove(TestUtils.GetTestConfig("one"));
            string importMessage = configManager.Import(TestUtils.GetTestFilePath("configs.xml"));
            string expectedMessage = "Number of configurations imported: 1\r\nThe following configurations already exist and were not imported:\r\n\"two\"\r\n\"three\"\r\n";
            Assert.AreEqual(importMessage, expectedMessage);


            TestUtils.ClearSavedConfigurations();

            File.Delete(configsXmlPath);
        }

        [TestMethod]
        public void TestCloseConfigs()
        {
            TestUtils.ClearSavedConfigurations();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.AddConfiguration(TestUtils.GetTestConfig("four"));
            var lastConfigs = configManager.ConfigList;
            configManager.Close();
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(TestUtils.GetTestFilePath("TestLog.log")));
            AssertEquals(testConfigManager.ConfigList, lastConfigs);


            TestUtils.ClearSavedConfigurations();
        }


        private void AssertEquals<T>(List<T> listOne, List<T> listTwo)
        {
            bool equal = listOne.Count == listTwo.Count;
            for (int i = 0; i < listOne.Count; i++)
            {
                if (!equal)
                    break;
                equal = Equals(listOne[i], listTwo[i]);
            }
            Assert.IsTrue(equal, "Actual list did not equal expected.");
        }
    }
}
