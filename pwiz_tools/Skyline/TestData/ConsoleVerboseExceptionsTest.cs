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
            const string exceptionLocation =
                @"at pwiz.Skyline.CommandLine.HandleExceptions(CommandArgs commandArgs, Action func, String errorMsg, Boolean isCompositeFormat, String additionalErrorMsg)";
            var newDocumentPath = TestContext.GetTestPath("out.sky");
            // Throw an exception with verbose errors
            var output = RunCommand("--new=" + newDocumentPath,
                "--verbose-errors",
                "--exception");
            // The stack trace should be reported
            CommandLineTest.CheckRunCommandOutputContains(exceptionLocation, output);
            // Throw an exception without verbose errors
            output = RunCommand("--new=" + newDocumentPath,
                "--exception");
            // The stack trace should not be reported
            Assert.IsFalse(output.Contains(exceptionLocation));
        }
    }
}