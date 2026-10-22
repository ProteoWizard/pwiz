/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Regression test for <see cref="AbstractUnitTestEx.GetSystemResourceString"/>. On net8 the .NET
    /// runtime's built-in message strings moved from mscorlib to System.Private.CoreLib's
    /// "System.Private.CoreLib.Strings" resource. Reaching them the old way -- via
    /// new ResourceManager(assembly.GetName().Name, ...) -- makes ResourceManager grovel for a
    /// nonexistent stream and call Environment.FailFast, which uncatchably terminates the whole
    /// TestRunner process (it took the net8 suite down mid-run). This guards the safe replacement.
    /// </summary>
    [TestClass]
    public class SystemResourceStringTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void TestGetSystemResourceString()
        {
            // The path is substituted into the "Could not find file '{0}'." template, so it appears in
            // the returned message in every UI language -- assert on that rather than localized wording.
            const string missingPath = @"C:\some\missing\file.fasta";
            var message = GetSystemResourceString("IO.FileNotFound_FileName", missingPath);
            Assert.IsFalse(string.IsNullOrEmpty(message), "GetSystemResourceString returned no text");
            StringAssert.Contains(message, missingPath);

            // An unmapped id must fail as an ordinary catchable exception -- never Environment.FailFast,
            // which would terminate the whole TestRunner process (as the old net8 code path did).
            Assert.ThrowsException<ArgumentException>(() => GetSystemResourceString("Totally.Bogus_ResourceId"));
        }
    }
}
