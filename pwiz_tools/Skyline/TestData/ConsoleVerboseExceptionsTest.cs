/*
 * Original author: Henry Sanford <henrytsanford .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
                // Throw an exception with verbose errors
                var output = RunCommand("--overwrite",
                    "--new=" + newDocumentPath,
                    "--verbose-errors",
                    "--exception");
            const string exceptionLocation =
                @"pwiz.Skyline.CommandLine.HandleExceptions[T](CommandArgs commandArgs, Func`1 func, Action`1 outputFunc)";
            // The stack trace should be reported
            CheckRunCommandOutputContains(exceptionLocation, output);
            // Throw an exception without verbose errors
            output = RunCommand("--overwrite",
                "--new=" + newDocumentPath,
                "--exception");
            // The stack trace should not be reported
            Assert.IsFalse(output.Contains(exceptionLocation));
        }
    }
}