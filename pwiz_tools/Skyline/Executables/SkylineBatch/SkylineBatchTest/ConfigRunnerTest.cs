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

        // TODO: tests for configRunner.run
    }
    
}
