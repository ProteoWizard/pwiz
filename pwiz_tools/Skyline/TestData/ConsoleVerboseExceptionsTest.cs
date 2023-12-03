using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;
using Resources = pwiz.Skyline.Properties.Resources;

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
                // The stack trace should be reported if verbose exceptions is true
                Assert.IsTrue(consoleOutput.Contains(string.Format(Resources.ConsoleVerboseExceptionsTest_ConsoleVerboseErrorsTest_Stack_Trace___0_, x.StackTrace)));
                consoleBuffer = new StringBuilder();
                writer = new CommandStatusWriter(new StringWriter(consoleBuffer));
                writer.IsVerboseExceptions = false;
                writer.WriteLine(x);
                consoleOutput = consoleBuffer.ToString();
                // The stack trace should not be reported if verbose exceptions is false
                Assert.IsFalse(consoleOutput.Contains(string.Format(Resources.ConsoleVerboseExceptionsTest_ConsoleVerboseErrorsTest_Stack_Trace___0_, x.StackTrace)));
            }
        }
    }
}