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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
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
            catch (Exception)
            {
                Assert.Fail("Expected to successfully select configurations within range");
            }

            try
            {
                testConfigManager.SelectConfig(-1);
                Assert.Fail("Expected index out of range exception");
            }
            catch (IndexOutOfRangeException e)
            {
                Assert.AreEqual(e.Message, "No configuration at index: -1");
            }
            try
            {
                testConfigManager.SelectConfig(3);
                Assert.Fail("Expected index out of range exception");
            }
            catch (IndexOutOfRangeException e)
            {
                Assert.AreEqual(e.Message, "No configuration at index: 3");
            }
        }

        [TestMethod]
        public void TestAddInsertConfig()
        {
            TestUtils.ClearSavedConfigurations();
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(TestUtils.GetTestFilePath("TestLog.log")));
            Assert.IsTrue(!testConfigManager.HasConfigs());
            var addedConfig = TestUtils.GetTestConfig("one");
            testConfigManager.AddConfiguration(addedConfig);
            var oneConfig = TestUtils.ConfigListFromNames(new List<string> { "one" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(oneConfig));

            testConfigManager.AddConfiguration(TestUtils.GetTestConfig("two"));
            testConfigManager.AddConfiguration(TestUtils.GetTestConfig("three"));
            var threeConfigs = TestUtils.ConfigListFromNames(new List<string> { "one", "two", "three" });
            Assert.IsTrue(testConfigManager.ConfigListEquals(threeConfigs));

            try
            {
                testConfigManager.AddConfiguration(addedConfig);
                Assert.Fail("Expected exception for duplicate configuration added.");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(e.Message, "Failed operation \"Add\": Configuration \"one\" already exists.");
            }
            Assert.IsTrue(testConfigManager.ConfigListEquals(threeConfigs));
        }

        [TestMethod]
        public void TestRemoveConfig()
        {
            TestUtils.ClearSavedConfigurations();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.RemoveSelected();
            Assert.IsTrue(configManager.SelectedConfig == -1);
            var oneRemoved = TestUtils.ConfigListFromNames(new List<string> { "two", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(oneRemoved));

            try
            {
                configManager.RemoveSelected();
                Assert.Fail("Expected exception for nonexistent configuration removed.");
            }
            catch (IndexOutOfRangeException e)
            {
                Assert.AreEqual("No configuration selected.", e.Message);
            }
            Assert.IsTrue(configManager.ConfigListEquals(oneRemoved));
        }

        [TestMethod]
        public void TestMoveConfig()
        {
            TestUtils.ClearSavedConfigurations();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.MoveSelectedConfig(false);
            var expectedMovedForward = TestUtils.ConfigListFromNames(new List<string> { "two", "one", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(expectedMovedForward));

            configManager.SelectConfig(2);
            configManager.MoveSelectedConfig(true);
            var expectedMovedBackward = TestUtils.ConfigListFromNames(new List<string> { "two", "three", "one" });
            Assert.IsTrue(configManager.ConfigListEquals(expectedMovedBackward));
        }

        [TestMethod]
        public void TestReplaceConfig()
        {
            TestUtils.ClearSavedConfigurations();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            configManager.ReplaceSelectedConfig(TestUtils.GetTestConfig("oneReplaced"));
            var expectedOneReplaced = TestUtils.ConfigListFromNames(new List<string> { "oneReplaced", "two", "three" });
            Assert.IsTrue(configManager.ConfigListEquals(expectedOneReplaced));

            try
            {
                configManager.SelectConfig(1);
                configManager.ReplaceSelectedConfig(TestUtils.GetTestConfig("oneReplaced"));
                Assert.Fail("Expected exception for duplicate config.");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(e.Message, "Failed operation \"Replace\": Configuration \"oneReplaced\" already exists.");
            }
            Assert.IsTrue(configManager.ConfigListEquals(expectedOneReplaced));
            Assert.IsTrue(configManager.ConfigListEquals(expectedOneReplaced));
        }


       


        #endregion

        #region XML Parsing

        [TestMethod]
        public void TestImportExport()
        {
            TestUtils.ClearSavedConfigurations();
            var configsXmlPath = TestUtils.GetTestFilePath("configs.xml");
            var configManager = TestUtils.GetTestConfigManager();
            configManager.ExportAll(configsXmlPath);
            int i = 0;
            while (configManager.HasConfigs() && i < 4)
            {
                configManager.SelectConfig(0);
                configManager.RemoveSelected();
                i++;
            }
            Assert.IsFalse(i == 4, "Failed to remove all configs.");

            var testingConfigs = TestUtils.ConfigListFromNames(new List<string> { "one", "two", "three" });
            configManager.Import(configsXmlPath);
            Assert.IsTrue(configManager.ConfigListEquals(testingConfigs));

            configManager.SelectConfig(0);
            configManager.RemoveSelected();
            string importMessage = configManager.Import(TestUtils.GetTestFilePath("configs.xml"));
            string expectedMessage = "Number of configurations imported: 1\r\nThe following configurations already exist and were not imported:\r\n\"two\"\r\n\"three\"\r\n";
            Assert.AreEqual(importMessage, expectedMessage);

            File.Delete(configsXmlPath);
        }

        [TestMethod]
        public void TestCloseReopenConfigs()
        {
            TestUtils.ClearSavedConfigurations();
            TestUtils.ClearSavedConfigurations();
            var configManager = TestUtils.GetTestConfigManager();
            configManager.AddConfiguration(TestUtils.GetTestConfig("four"));
            var testingConfigs = TestUtils.ConfigListFromNames(new List<string> { "one", "two", "three", "four" });
            configManager.Close();
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(TestUtils.GetTestFilePath("TestLog.log")));
            Assert.IsTrue(testConfigManager.ConfigListEquals(testingConfigs));
        }

        #endregion

        #region Managing Logs

        [TestMethod]
        public void TestMultipleOldLogs()
        {
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(TestUtils.GetTestFilePath("OldLogs\\TestLog.log")));
            Assert.IsTrue(testConfigManager.HasOldLogs());
            Assert.IsTrue(testConfigManager.GetOldLogFiles().Length == 3);
        }

        [TestMethod]
        public void TestDeleteOldLog()
        {
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(TestUtils.GetTestFilePath("OldLogs\\TestLog.log")));
            testConfigManager.SelectLog(3);
            var deletingLogFullName = TestUtils.GetTestFilePath("OldLogs\\TestLog_20201110_094940.log");
            var filesAfterDelete = DeleteLogs(testConfigManager, new List<string> { deletingLogFullName });
            var oldLogsAfterDelete = testConfigManager.GetOldLogFiles();

            Assert.IsTrue(testConfigManager.SelectedLog == 2);
            Assert.IsTrue(filesAfterDelete.Count == 3); // count includes old logs and current log
            Assert.IsFalse(filesAfterDelete.Contains(deletingLogFullName));
            Assert.IsTrue(oldLogsAfterDelete.Length == 2);
            Assert.IsFalse(oldLogsAfterDelete.Contains(Path.GetFileName(deletingLogFullName)));
        }

        [TestMethod]
        public void TestDeleteAllOldLogs()
        {
            var currentLogFullName = TestUtils.GetTestFilePath("OldLogs\\TestLog.log");
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(currentLogFullName));
            var allOldLogs = TestUtils.GetAllLogFiles(Path.GetDirectoryName(currentLogFullName));
            allOldLogs.Remove(currentLogFullName);


            testConfigManager.SelectLog(3);
            
            var filesAfterDelete = DeleteLogs(testConfigManager, allOldLogs);
            var allLogsAfterDelete = testConfigManager.GetAllLogFiles();

            Assert.IsTrue(testConfigManager.SelectedLog == 0);
            Assert.IsTrue(filesAfterDelete.Count == 1); // count includes old logs and current log
            Assert.IsTrue(filesAfterDelete.Contains(currentLogFullName));
            Assert.IsTrue(allLogsAfterDelete.Length == 1);
            Assert.IsTrue(allLogsAfterDelete.Contains(Path.GetFileName(currentLogFullName)));
        }


        
        private List<string> DeleteLogs(ConfigManager testConfigManager, List<string> logsToDelete)
        {
            var deletingLogsAsObjectArray = new object[logsToDelete.Count];
            for (int i = 0; i < logsToDelete.Count; i++)
            {
                var logFileName = Path.GetFileName(logsToDelete[i]);
                File.Copy(logsToDelete[i], TestUtils.GetTestFilePath(logFileName), true);
                deletingLogsAsObjectArray[i] = logFileName;
            }
            testConfigManager.DeleteLogs(deletingLogsAsObjectArray);
            var filesAfterDelete = TestUtils.GetAllLogFiles(Path.GetDirectoryName(logsToDelete[0]) + "\\");
            foreach (var log in logsToDelete)
            {
                var logFileName = Path.GetFileName(log);
                File.Copy(TestUtils.GetTestFilePath(logFileName), log, true);
                File.Delete(TestUtils.GetTestFilePath(logFileName));
            }

            return filesAfterDelete;
        }


        #endregion


    }
}
