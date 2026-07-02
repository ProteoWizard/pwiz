/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="OspreyVersion"/> git-hash version stamping
    /// (TODO-osprey_version_git_hash.md). The Skyline-style informational
    /// version (<c>YEAR.ORDINAL.BRANCH.DOY-&lt;hash&gt;[-dirty]</c>) parses and
    /// renders correctly, and an in-checkout build stamps a real commit hash and
    /// a nonzero commit day-of-year so every Osprey binary is traceable to its
    /// source -- the defect this change closes.
    /// </summary>
    [TestClass]
    public class OspreyVersionTest
    {
        [TestMethod]
        public void TestFormatDisplayRendersHashInParentheses()
        {
            Assert.AreEqual(@"26.1.1.182 (b2373f9f9c)",
                OspreyVersion.FormatDisplay(@"26.1.1.182-b2373f9f9c"));
            // A dirty marker stays inside the parentheses with the hash.
            Assert.AreEqual(@"26.1.1.182 (b2373f9f9c-dirty)",
                OspreyVersion.FormatDisplay(@"26.1.1.182-b2373f9f9c-dirty"));
        }

        [TestMethod]
        public void TestFormatDisplayLeavesBareNumericVersionUnchanged()
        {
            // The non-git / OSPREY_VERSION_OVERRIDE fallback carries no hash.
            Assert.AreEqual(@"26.1.1.0", OspreyVersion.FormatDisplay(@"26.1.1.0"));
            Assert.AreEqual(@"26.1.1.182", OspreyVersion.FormatDisplay(@"26.1.1.182"));
            Assert.AreEqual(string.Empty, OspreyVersion.FormatDisplay(string.Empty));
            Assert.IsNull(OspreyVersion.FormatDisplay(null));
        }

        [TestMethod]
        public void TestExtractGitHashTakesHashWithoutDirtyMarker()
        {
            Assert.AreEqual(@"b2373f9f9c", OspreyVersion.ExtractGitHash(@"26.1.1.182-b2373f9f9c"));
            Assert.AreEqual(@"b2373f9f9c", OspreyVersion.ExtractGitHash(@"26.1.1.182-b2373f9f9c-dirty"));
            Assert.AreEqual(string.Empty, OspreyVersion.ExtractGitHash(@"26.1.1.0"));
            Assert.AreEqual(string.Empty, OspreyVersion.ExtractGitHash(string.Empty));
        }

        [TestMethod]
        public void TestInCheckoutBuildStampsHashAndNonzeroDoy()
        {
            // The bit-parity regression harness pins OSPREY_VERSION_OVERRIDE to a
            // hash-free constant; skip the stamp assertion under it. Every other
            // in-checkout build (this test build, CI, a release build) must carry
            // a real hash + nonzero DOY so the binary is traceable to its commit.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(@"OSPREY_VERSION_OVERRIDE")))
            {
                Assert.Inconclusive(@"OSPREY_VERSION_OVERRIDE pins a hash-free version.");
                return;
            }

            string hash = OspreyVersion.GitHash;
            Assert.IsTrue(Regex.IsMatch(hash, @"^[0-9a-f]{7,}$"),
                string.Format(@"Expected a 7+ hex-char git hash, got '{0}' (informational '{1}'). " +
                    @"An in-checkout build must stamp the commit hash via Directory.Build.targets.",
                    hash, OspreyVersion.InformationalVersion));

            string[] parts = OspreyVersion.Current.Split('.');
            Assert.AreEqual(4, parts.Length,
                string.Format(@"Version should be YEAR.ORDINAL.BRANCH.DOY, got '{0}'", OspreyVersion.Current));
            int doy = int.Parse(parts[3], CultureInfo.InvariantCulture);
            Assert.IsTrue(doy > 0,
                string.Format(@"DOY must be the nonzero commit day-of-year, got {0} (version '{1}')",
                    doy, OspreyVersion.Current));
        }
    }
}
