/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.CommandLine;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model.DocSettings;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Skyline's command line parses under Skyline's grammar after CarafeSharp's, which Skyline
    /// hosts in-process for AlphaPeptDeep library builds, has been used. The two share the
    /// process-wide <see cref="ArgUsage"/> seams, and Carafe's grammar is installed only by
    /// CarafeSharp.exe; installed by the type itself, it kept the first of a repeated Skyline
    /// argument instead of the last, so a test's <c>--full-scan-precursor-isotopes=Count ... =None</c>
    /// left MS1 filtering on and the DIA-PASEF command-line tutorial extracted no chromatograms
    /// for its targets.
    /// </summary>
    [TestClass]
    public class CommandArgsGrammarTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCommandArgsGrammarAfterCarafe()
        {
            // Skyline's own model first, as in Skyline, where CommandArgs initializes before any
            // model file is read
            var parseOutput = new StringWriter();
            var commandArgs = new CommandArgs(new CommandStatusWriter(parseOutput), true);
            var grammar = ArgUsage.Grammar;
            var separator = ArgUsage.ArgumentValueSeparator;

            // A Carafe parse installs nothing: Carafe's grammar is CarafeSharp.exe's to install
            var carafe = CarafeCommandLine.Parse(new[] { @"--help" });
            Assert.AreEqual(CarafeCommandMode.help, carafe.Mode);
            Assert.AreSame(grammar, ArgUsage.Grammar);
            Assert.AreEqual(separator, ArgUsage.ArgumentValueSeparator);

            // So a repeated Skyline argument still keeps its last value, as Skyline's grammar says
            Assert.IsTrue(commandArgs.ParseArgs(new[]
            {
                @"--full-scan-precursor-isotopes=Count",
                @"--full-scan-acquisition-method=DIA",
                @"--full-scan-precursor-isotopes=None"
            }), parseOutput.ToString());
            Assert.AreEqual(FullScanPrecursorIsotopes.None, commandArgs.FullScanPrecursorIsotopes);
        }
    }
}
