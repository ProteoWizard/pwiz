using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class ConsoleVerboseExceptionsTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void ConsoleVerboseErrorsTest()
        {
            try
            {
                // Throw an exception so we can examine how it is reported
                throw new NotImplementedException();
            }
            catch(Exception x)
            {
                var consoleBuffer = new StringBuilder();
                var writer = new CommandStatusWriter(new StringWriter(consoleBuffer));
                writer.IsVerboseExceptions = true;
                writer.WriteLine(x);
                var consoleOutput = consoleBuffer.ToString();
                // The entire exception should be reported if verbose exceptions is true
                Assert.IsTrue(consoleOutput.Contains(x.ToString()));
                consoleBuffer = new StringBuilder();
                writer = new CommandStatusWriter(new StringWriter(consoleBuffer));
                writer.IsVerboseExceptions = false;
                writer.WriteLine(x);
                consoleOutput = consoleBuffer.ToString();
                // Only the exception message should be reported if verbose exceptions is false
                Assert.IsTrue(consoleOutput.Contains(x.Message));
            }
        }
    }
}