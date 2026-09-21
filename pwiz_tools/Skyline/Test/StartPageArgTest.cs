/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Unit tests for <see cref="Program.ParseStartPageArg"/>, covering the
    /// --start-page=true|false launch flag introduced for scripted Skyline launches.
    /// </summary>
    [TestClass]
    public class StartPageArgTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestParseStartPageArg()
        {
            // Absent flag returns null.
            Assert.IsNull(Program.ParseStartPageArg(null));
            Assert.IsNull(Program.ParseStartPageArg(new string[0]));
            Assert.IsNull(Program.ParseStartPageArg(new[] { @"--opendoc=foo.sky" }));

            // Valid true/false values.
            Assert.AreEqual(true, Program.ParseStartPageArg(new[] { @"--start-page=true" }));
            Assert.AreEqual(false, Program.ParseStartPageArg(new[] { @"--start-page=false" }));

            // Case-insensitive on both the key and the value.
            Assert.AreEqual(true, Program.ParseStartPageArg(new[] { @"--Start-Page=TRUE" }));
            Assert.AreEqual(false, Program.ParseStartPageArg(new[] { @"--START-PAGE=False" }));

            // Composes with --opendoc in either order, value reflects the flag.
            Assert.AreEqual(true, Program.ParseStartPageArg(new[] { @"--opendoc=foo.sky", @"--start-page=true" }));
            Assert.AreEqual(false, Program.ParseStartPageArg(new[] { @"--start-page=false", @"--opendoc=foo.sky" }));

            // Bare --start-page with no value is rejected.
            AssertParseError(new[] { @"--start-page" }, @"--start-page");

            // Unparseable values are rejected, message echoes the offending token.
            AssertParseError(new[] { @"--start-page=foo" }, @"--start-page=foo");
            AssertParseError(new[] { @"--start-page=" }, @"--start-page=");
            AssertParseError(new[] { @"--start-page=1" }, @"--start-page=1");
        }

        private static void AssertParseError(string[] args, string expectedToken)
        {
            try
            {
                Program.ParseStartPageArg(args);
                Assert.Fail();
            }
            catch (ArgumentException ex)
            {
                // Message is the formatted resource; verify both the resource string and
                // that it surfaces the offending argument token to the user.
                AssertEx.Contains(ex.Message, expectedToken);
                AssertEx.Contains(ex.Message, string.Format(
                    SkylineResources.Program_ParseStartPageArg_Invalid_argument__0___Use__start_page_true_or__start_page_false,
                    expectedToken));
            }
        }
    }
}
