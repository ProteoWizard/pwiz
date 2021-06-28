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
using SkylineBatch;


namespace SkylineBatchTest
{
    [TestClass]
    public class TemplateFileDependencyTest
    {

        #region ConfigList Operations

        [TestMethod]
        public void TestChangeDependent()
        {
            TestUtils.InitializeRInstallation();
            var testConfigManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());

            int baseIndex = 0;
            testConfigManager.UserAddConfig(TestUtils.GetFullyPopulatedConfig());
            testConfigManager.UserAddConfig(TestUtils.GetTestDependentConfig("dependent", testConfigManager.GetConfig(baseIndex)));
            int dependentIndex = 1;
            AssertDependency(testConfigManager.GetConfig(dependentIndex), testConfigManager.GetConfig(baseIndex),
                "Error setting up dependency");

            testConfigManager.SelectConfig(baseIndex);

            var nameChangedConfig = TestUtils.GetFullyPopulatedConfig("newName");
            testConfigManager.UserReplaceSelected(nameChangedConfig);
            AssertDependency(testConfigManager.GetConfig(dependentIndex), testConfigManager.GetConfig(baseIndex),
                "Dependency error after base name change");

            var pathChangedConfig = TestUtils.GetChangedConfig(nameChangedConfig, new Dictionary<string, object>()
            {
                { "OutputFilePath", TestUtils.GetTestFilePath("newOutputPath.sky")}
            });
            testConfigManager.UserReplaceSelected(pathChangedConfig);
            AssertDependency(testConfigManager.GetConfig(dependentIndex), testConfigManager.GetConfig(baseIndex),
                "Dependency error after base refined output file path change");

            var bothChangedConfig = TestUtils.GetChangedConfig(pathChangedConfig, new Dictionary<string, object>()
            {
                {"Name", "BothChangedName"},
                {"OutputFilePath", TestUtils.GetTestFilePath("bothChangedOutputPath.sky")}
            });
            testConfigManager.UserReplaceSelected(bothChangedConfig);
            AssertDependency(testConfigManager.GetConfig(dependentIndex), testConfigManager.GetConfig(baseIndex),
                "Dependency error after base name and refined output file path change");

            testConfigManager.UserReplaceSelected(TestUtils.GetTestConfig());
            AssertNoDependency(testConfigManager.GetConfig(dependentIndex), false);
        }

        [TestMethod]
        public void TestDeleteStoppedDependent()
        {
            TestUtils.InitializeRInstallation();
            var testConfigManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());

            int baseIndex = 0;
            testConfigManager.UserAddConfig(TestUtils.GetFullyPopulatedConfig());
            testConfigManager.UserAddConfig(TestUtils.GetTestDependentConfig("dependent", testConfigManager.GetConfig(baseIndex)));
            int dependentIndex = 1;
           
            testConfigManager.SelectConfig(baseIndex);
            testConfigManager.UserRemoveSelected();
            dependentIndex--;
            AssertNoDependency(testConfigManager.GetConfig(dependentIndex), false);
            testConfigManager.UserRemoveSelected();

            baseIndex = 0;
            var populatedConfig = TestUtils.GetFullyPopulatedConfig("base");
            var existingOutputConfig = TestUtils.GetChangedConfig(populatedConfig,
                new Dictionary<string, object>()
                {
                    {"OutputFilePath", TestUtils.GetTestFilePath("emptyTemplate.sky")}
                });
            testConfigManager.UserAddConfig(existingOutputConfig);
            testConfigManager.UserAddConfig(TestUtils.GetTestDependentConfig("dependent", testConfigManager.GetConfig(baseIndex)));
            dependentIndex = 1;

            testConfigManager.SelectConfig(baseIndex);
            testConfigManager.UserRemoveSelected();
            dependentIndex--;
            AssertNoDependency(testConfigManager.GetConfig(dependentIndex), true);
        }


        private void AssertDependency(SkylineBatchConfig dependentConfig, SkylineBatchConfig baseConfig, string message = "")
        {
            Assert.AreEqual(baseConfig.Name,
                dependentConfig.MainSettings.Template.DependentConfigName,
                message);
            Assert.AreEqual(baseConfig.RefineSettings.OutputFilePath,
                dependentConfig.MainSettings.Template.FilePath,
                message);
            dependentConfig.Validate();
        }

        private void AssertNoDependency(SkylineBatchConfig wasDependentConfig, bool expectedTemplateExist)
        {
            Assert.AreEqual(string.Empty, wasDependentConfig.MainSettings.Template.DependentConfigName,
                "Dependent config name was not null");
            var validatedInvalidConfig = false;
            try
            {
                wasDependentConfig.Validate();
                validatedInvalidConfig = true;
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(e.Message, $"The Skyline template file {wasDependentConfig.MainSettings.Template.FilePath} does not exist.\r\nPlease provide a valid file.");
            }
            Assert.AreEqual(expectedTemplateExist, validatedInvalidConfig, "Unexpected validation result");
        }
        
        #endregion

    }
}
