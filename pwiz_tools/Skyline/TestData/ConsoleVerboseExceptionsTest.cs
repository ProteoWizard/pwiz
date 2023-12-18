using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class ConsoleVerboseExceptionsTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void TestConsoleVerboseExceptions()
        {
            var newDocumentPath = TestContext.GetTestPath("out.sky");
            var folder = Path.GetDirectoryName(newDocumentPath);
            Assert.IsNotNull(folder);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string output;
            using (File.OpenWrite(newDocumentPath))
            {
                // Throw an exception with verbose errors
                output = RunCommand("--overwrite", "--new=" + newDocumentPath,
                    "--verbose-errors");
            }
            const string exceptionLocation =
                @"at pwiz.Skyline.CommandLine.HandleExceptions[T](CommandArgs commandArgs, Func`1 func, Action`1 outputFunc)";
            // The stack trace should be reported
            CheckRunCommandOutputContains(exceptionLocation, output);
            // Throw an exception without verbose errors
            output = RunCommand("overwrite", "--new=" + newDocumentPath);
            // The stack trace should not be reported
            Assert.IsFalse(output.Contains(exceptionLocation));
        }
    }
}