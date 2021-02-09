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


using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{

    [TestClass]
    public class ConfigRunnerTest
    {
        [TestMethod]
        public async Task TestExecuteSkylineCmd()
        {
           var logger = TestUtils.GetTestLogger();
           var testRunner = new ConfigRunner(TestUtils.GetTestConfig(), logger);
           var config = testRunner.Config;
           Assert.IsTrue(testRunner.IsStopped());
           var singleCommand = string.Format("--in=\"{0}\" --out=\"{1}\"", config.MainSettings.TemplateFilePath,
               TestUtils.GetTestFilePath("Copy.sky"));
            testRunner.ChangeStatus(RunnerStatus.Running);
           await testRunner.ExecuteProcess(config.SkylineSettings.CmdPath, singleCommand);
           logger.Delete();
           Assert.IsTrue(testRunner.IsRunning(), "Expected no errors or cancellations.");
           Assert.IsTrue(File.Exists(TestUtils.GetTestFilePath("Copy.sky")));
           File.Delete(TestUtils.GetTestFilePath("Copy.sky"));
        }

        [TestMethod]
        public async Task TestRunFromStepFour()
        {
            TestUtils.InitializeRInstallation();
            var logger = TestUtils.GetTestLogger();
            var testRunner = new ConfigRunner(TestUtils.GetTestConfig(), logger);
            Assert.IsTrue(testRunner.IsStopped());
            await testRunner.Run(4);
            logger.Delete();
            Assert.IsTrue(testRunner.IsCompleted(), "Expected runner to have status \"Completed\" but was: " + testRunner.GetStatus());
        }

        // CONSIDER: add tests for configRunner.run
    }
    
}
