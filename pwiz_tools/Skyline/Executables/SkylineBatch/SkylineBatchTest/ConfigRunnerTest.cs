﻿/*
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
           await new ProcessRunner().Run(config.SkylineSettings.CmdPath, singleCommand);
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

        [TestMethod]
        public void TestGenerateCommandFile()
        {
            TestUtils.InitializeRInstallation();
            var logger = TestUtils.GetTestLogger();
            var testRunner = new ConfigRunner(TestUtils.GetFullyPopulatedConfig(), logger);
            Assert.IsTrue(testRunner.IsStopped());

            // Skyline versions after 21.1.0.0
            var expectedInvariantReportCommandFile = TestUtils.GetTestFilePath("CommandFile_Skyline_21_1_0_0.txt");
            var invariantReportWriter = new CommandWriter(logger, true, true);
            testRunner.WriteBatchCommandsToFile(invariantReportWriter, 1, true);
            var actualInvariantReportCommandFile = invariantReportWriter.GetCommandFile();
            CompareFiles(expectedInvariantReportCommandFile, actualInvariantReportCommandFile);

            // Skyline versions after 20.2.1.454
            var expectedMultiLineCommandFile = TestUtils.GetTestFilePath("CommandFile_Skyline_20_2_1_454.txt");
            var multiLineWriter = new CommandWriter(logger, true, false);
            testRunner.WriteBatchCommandsToFile(multiLineWriter, 1, true);
            var actualMultiLineCommandFile = multiLineWriter.GetCommandFile();
            CompareFiles(expectedMultiLineCommandFile, actualMultiLineCommandFile);

            // Skyline versions after 20.2.0.0
            var expectedOldVersionCommandFile = TestUtils.GetTestFilePath("CommandFile_Skyline_20_2_0_0.txt");
            var oldVersionWriter = new CommandWriter(logger, false, false);
            testRunner.WriteBatchCommandsToFile(oldVersionWriter, 1, false);
            var actualOldVersionCommandFile = oldVersionWriter.GetCommandFile();
            CompareFiles(expectedOldVersionCommandFile, actualOldVersionCommandFile);
        }


        private void CompareFiles(string expectedFilePath, string actualFilePath)
        {
            using (var expectedReader = new StreamReader(expectedFilePath))
            using (var actualReader = new StreamReader(actualFilePath))
            {
                int line = 0;
                while (line < 1000)
                {
                    if (expectedReader.EndOfStream != actualReader.EndOfStream)
                        Assert.Fail($"Line {line}: Expected end of stream value to be {expectedReader.EndOfStream} but instead was {actualReader.EndOfStream}.");
                    var expectedLine = expectedReader.ReadLine();
                    var actualLine = actualReader.ReadLine();
                    if (expectedLine == null || actualLine == null)
                    {
                        Assert.IsTrue(expectedLine == actualLine,
                            actualFilePath + Environment.NewLine +
                            $"Line {line}: Expected reached end of file to be {expectedLine == null} but instead was {actualLine == null}.");
                        return;
                    }

                    Assert.IsTrue(expectedLine.Equals(actualLine),
                        actualFilePath + Environment.NewLine +
                        $"Line {line} does not match" + Environment.NewLine +
                                                                   "Expected:" + Environment.NewLine +
                                                                   expectedLine + Environment.NewLine +
                                                                   "Actual:" + Environment.NewLine +
                                                                   actualLine);
                    line++;
                }
                throw new Exception("Test Error: should never reach 1000 lines");
            }
        }

        // CONSIDER: add tests for configRunner.run
    }
    
}
