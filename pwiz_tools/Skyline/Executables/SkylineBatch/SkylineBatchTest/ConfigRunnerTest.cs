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
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;

namespace SkylineBatchTest
{

    [TestClass]
    public class ConfigRunnerTest
    {

        
        [TestMethod]
        public async Task TestExecuteCommandLine()
        {
           TestUtils.InitializeInstallations();
           var testRunner = new ConfigRunner(TestUtils.GetTestConfig(), new SkylineBatchLogger(TestUtils.GetTestFilePath("testLog.log")));
           Assert.IsTrue(testRunner.IsStopped());
            var singleCommand = new List<string>
               {"echo command success  > " + TestUtils.GetTestFilePath("cmdTest.txt")};
           await testRunner.ExecuteCommandLine(singleCommand);
           Assert.IsTrue(File.Exists(TestUtils.GetTestFilePath("cmdTest.txt")));
           Assert.IsTrue(testRunner.IsCompleted());
           var multipleCommands = new List<string>
               {"cd " + TestUtils.GetTestFilePath(""), "del cmdTest.txt"};
           await testRunner.ExecuteCommandLine(multipleCommands);
           Assert.IsFalse(File.Exists(TestUtils.GetTestFilePath("cmdTest.txt")));
           Assert.IsTrue(testRunner.IsCompleted());
        }

        [TestMethod]
        public async Task TestRunFromStepFour()
        {
            TestUtils.InitializeInstallations();
            var testRunner = TestUtils.GetTestConfigRunner();
            Assert.IsTrue(testRunner.IsStopped());
            await testRunner.Run(4);
            Assert.IsTrue(testRunner.IsCompleted());
        }

        // TODO: tests for configRunner.run
    }
    
}
