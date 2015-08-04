/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AutoQCTest
{
    [TestClass]
    public class AutoQCBackgroundWorkerTest

    {
        [TestMethod]
        public void TestBackgroundWorker_ProcessNewFiles()
        {
            // Create a test directory to monitor
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Assert.IsNotNull(dir);
            var testDir = Path.Combine(dir, "TestBackgroundWorker");

            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
            Assert.IsFalse(Directory.Exists(testDir));
            Directory.CreateDirectory(testDir);
            Assert.IsTrue(Directory.Exists(testDir));

            var appControl = new TestAppControl();
            var logger = new TestLogger();
            var processControl = new TestProcessControl(logger);
            var mainSettings = new MainSettings
            {
                FolderToWatch = testDir,
                InstrumentType = "NoInstrument",
                ResultsWindowString = "31",
                AcquisitionTimeString = "0"
            };

            // Start the background worker.
            var backgroundWorker = new AutoQCBackgroundWorker(appControl, processControl, logger);
            backgroundWorker.Start(mainSettings);
            Assert.IsTrue(backgroundWorker.IsRunning());

            // Create a new file in the test directory.
            Thread.Sleep(1000);
            CreateNewFile(testDir, "test1.txt");

            // Wait till the the file has been processed.
            while (!processControl.IsDone())
            {
                Thread.Sleep(500);
            }
            Assert.IsTrue(backgroundWorker.IsRunning());

            // Create another file in the test directory.
            Thread.Sleep(1000);
            CreateNewFile(testDir, "test2.txt");

            // Wait till the the file has been processed. 
            // Process4 returns exit code 1 both times. This should stop the program.
            while (!processControl.IsDone())
            {
                Thread.Sleep(500);
            }
   
            // Assert.IsTrue(appControl.Waiting);
            Thread.Sleep(2 * AutoQCBackgroundWorker.WAIT_5SEC);
            Assert.IsTrue(appControl.Stopped);

            Assert.AreEqual(Regex.Replace(logger.GetLog(), @"\s+", ""),
                Regex.Replace(GetExpectedLog_ProcessNew(), @"\s+", ""));

        }

        private static void CreateNewFile(string testDir, string fileName)
        {
            var filePath = Path.Combine(testDir, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            using (File.Create(filePath))
            {
            }
            Assert.IsTrue(File.Exists(filePath));
        }

        private static string GetExpectedLog_ProcessNew ()
        {
            var sb = new StringBuilder();
            sb.Append("Importing existing files...");
            sb.Append("No existing files found.");
            sb.Append("Importing new files...").AppendLine();
            sb.Append("Waiting for files...").AppendLine();
            sb.Append("File test1.txt added to directory.").AppendLine();
            sb.Append("File test1.txt is ready").AppendLine();
            sb.Append("Running Process1 with args:").AppendLine();
            sb.Append("Process1 exited successfully.").AppendLine();
            sb.Append("Running Process2 with args: ").AppendLine();
            sb.Append("Process2 exited with error code 1.").AppendLine();
            sb.Append("Process2 returned an error. Trying again...").AppendLine();
            sb.Append("Process2 exited successfully.").AppendLine();
            
            sb.Append("Waiting for files...").AppendLine();
            sb.Append("File test2.txt added to directory.").AppendLine();
            sb.Append("File test2.txt is ready").AppendLine();
            sb.Append("Running Process3 with args:").AppendLine();
            sb.Append("Error: Failed importing the results file").AppendLine();
            sb.Append("Process3 returned an error. Trying again...").AppendLine();
            sb.Append("Process3 exited successfully.").AppendLine();
            sb.Append("Running Process4 with args:").AppendLine();
            sb.Append("Process4 exited with error code 1.").AppendLine();
            sb.Append("Process4 returned an error. Trying again...").AppendLine();
            sb.Append("Process4 exited with error code 1.").AppendLine();
            sb.Append("Process4 returned an error. Exceeded maximum try count.  Giving up...");
            sb.Append("Finished importing files.");
            return sb.ToString();
        }

        class TestProcessControl : IProcessControl
        {
            private readonly IAutoQCLogger _logger;

            private MockProcessRunner _processRunner;
            private volatile Boolean _done;

            public TestProcessControl(IAutoQCLogger logger)
            {
                _logger = logger;
            }

            public IEnumerable<ProcessInfo> GetProcessInfos(ImportContext importContext)
            {
                var file = Path.GetFileName(importContext.GetCurrentFile());
                Assert.IsNotNull(file);

                if (file.Equals("test1.txt"))
                {
                    var procInfo1 = new ProcessInfo("Process1", "");
                    procInfo1.SetMaxTryCount(2);
                    var procInfo2 = new ProcessInfo("Process2", "");
                    procInfo2.SetMaxTryCount(2);
                    return new List<ProcessInfo> {
                        procInfo1, // Exit code 0; successful
                        procInfo2  // Exit code 1 first time; success second time
                    };
                }
                if (file.Equals("test2.txt"))
                {
                    var procInfo3 = new ProcessInfo("Process3", "");
                    procInfo3.SetMaxTryCount(2);
                    var procInfo4 = new ProcessInfo("Process4", "");
                    procInfo4.SetMaxTryCount(2);
                  
                    return new List<ProcessInfo>
                    {
                        procInfo3, // Exit code 0 but error during execution; succeed second time
                        procInfo4 // Exit code 1; Fails both times
                    };
                }
                return Enumerable.Empty<ProcessInfo>();
            }
            
            public bool RunProcess(ProcessInfo processInfo)
            {
               _processRunner = new MockProcessRunner(_logger);
                var run = _processRunner.RunProcess(processInfo);
                if (_processRunner.GetExeName().Equals("Process2") 
                    || _processRunner.GetExeName().Equals("Process4"))
                {
                    _done = _processRunner.IsDone();
                }
                return run;
            }

            public void StopProcess()
            {
                throw new NotImplementedException();
            }

            public Boolean IsDone()
            {
                if (!_done) return false;
                _done = false;
                return true;
            }
        }

        class MockProcessRunner : ProcessRunner
        {
            private Boolean _done;

            public MockProcessRunner(IAutoQCLogger logger) : base(logger)
            {
            }

            protected override int CreateAndRunProcess()
            {
                ProcessInfo procInfo = GetProcessInfo();

                Thread.Sleep(2 * 1000);
                _done = !procInfo.CanRetry();
                if (procInfo.ExeName.Equals("Process1"))
                {
                    _done = true;
                    return 0;
                }
                if (procInfo.ExeName.Equals("Process2"))
                { 
                    return procInfo.CanRetry() ? 1 : 0;
                }
                if (procInfo.ExeName.Equals("Process3"))
                {
                    if (procInfo.CanRetry())
                    {
                        WriteToLog("Error: Failed importing the results file");
                    }
                    
                    return 0;
                }
                return 1;
            }

            public Boolean IsDone()
            {
                return _done;
            }

            public string GetExeName()
            {
                return GetProcessInfo().ExeName;
            }
        }

    }
}
