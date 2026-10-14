/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Checks the Java behaviors Carafe's entrapment generation depends on against values
    /// printed by Java 23 itself (java.util.Random, Carafe's SHA-1 seed derivation) and against
    /// the documented semantics of String.trim, String.split, Character.isWhitespace and
    /// Math.round.
    /// </summary>
    [TestClass]
    public class JavaCompatibilityTest
    {
        [TestMethod]
        public void TestJavaRandom()
        {
            // Bound 10 and 7 take the rejection loop, 16 the power-of-two fast path, 2^30 + 1
            // rejects about half of all draws, and bound 1 always yields 0.
            AssertNextInts(42, 10, 0, 3, 8, 4, 0, 5, 5, 8, 9, 3);
            AssertNextInts(-1, 7, 3, 6, 4, 6, 6, 4);
            AssertNextInts(123456789, 16, 10, 12, 7, 4, 6, 3);
            AssertNextInts(long.MinValue, (1 << 30) + 1,
                516548029, 663681053, 251269761, 715581077, 542832677, 827187473, 49567875, 377907320);
            AssertNextInts(7, int.MaxValue, 1569164236, 1371249164, 1608829485, 20678044);
            AssertNextInts(99, 1, 0, 0, 0);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new JavaRandom(1).NextInt(0));
        }

        [TestMethod]
        public void TestPeptideSeeds()
        {
            // Attempt 0 hashes "seed:sequence"; retries append ":attempt".
            Assert.AreEqual(-2712341878545795487L, EntrapmentSequences.DerivePeptideSeed(42, @"PEPTIDEK", 0));
            Assert.AreEqual(7541246457314735603L, EntrapmentSequences.DerivePeptideSeed(42, @"PEPTIDEK", 1));
            Assert.AreEqual(6532568452969401461L, EntrapmentSequences.DerivePeptideSeed(42, @"PEPTIDEK", 19));
            Assert.AreEqual(-3121205504333406682L, EntrapmentSequences.DerivePeptideSeed(-7, @"AAAK", 0));
            Assert.AreEqual(129867339799892588L, EntrapmentSequences.DerivePeptideSeed(0, @"LMDLIGDR", 2));
            Assert.AreEqual(2776530512890964893L, EntrapmentSequences.DerivePeptideSeed(long.MaxValue, @"ELVISLIVESK", 0));
        }

        [TestMethod]
        public void TestJavaText()
        {
            // String.trim removes every control character, but not U+00A0 or U+2003.
            Assert.AreEqual(@"a b", JavaText.Trim("\u0001\t a b\r\n\u001F"));
            Assert.AreEqual("\u00A0a\u2003", JavaText.Trim(" \u00A0a\u2003 "));
            Assert.AreEqual(string.Empty, JavaText.Trim("  \t "));

            // Character.isWhitespace: U+0085 and the non-breaking spaces are not whitespace.
            foreach (char c in " \t\n\u000B\f\r\u001C\u001F\u2003\u2028\u3000")
                Assert.IsTrue(JavaText.IsWhitespace(c), ((int)c).ToString(@"X4"));
            foreach (char c in "A*\u0000\u0085\u00A0\u2007\u202F\uFEFF")
                Assert.IsFalse(JavaText.IsWhitespace(c), ((int)c).ToString(@"X4"));

            // String.split drops trailing empty fields, but not an empty string with no separator.
            CollectionAssert.AreEqual(new[] { @"sp", @"P1", @"X" }, JavaText.Split(@"sp|P1|X", '|'));
            CollectionAssert.AreEqual(new[] { @"sp", @"P1" }, JavaText.Split(@"sp|P1||", '|'));
            CollectionAssert.AreEqual(new[] { string.Empty, @"P1", @"X" }, JavaText.Split(@"|P1|X", '|'));
            CollectionAssert.AreEqual(new[] { string.Empty }, JavaText.Split(string.Empty, '|'));
            Assert.AreEqual(0, JavaText.Split(@"||", '|').Length);

            Assert.AreEqual(@"sp|P1|X", JavaText.FirstToken("sp|P1|X desc\tmore"));
            Assert.AreEqual("a\u00A0b", JavaText.FirstToken("a\u00A0b\u000Bc"));

            // One leading and one trailing asterisk, the latter also before a final terminator.
            Assert.AreEqual(@"*PEP*", JavaText.StripTerminalAsterisks(@"**PEP**"));
            Assert.AreEqual("PEP\u0085", JavaText.StripTerminalAsterisks("PEP*\u0085"));
            Assert.AreEqual("PEP\r\n", JavaText.StripTerminalAsterisks("*PEP*\r\n"));
            Assert.AreEqual(string.Empty, JavaText.StripTerminalAsterisks(@"*"));

            // Math.round: ties toward positive infinity, exact near 0.5, NaN to 0, clamped.
            Assert.AreEqual(3L, JavaText.Round(2.5));
            Assert.AreEqual(-2L, JavaText.Round(-2.5));
            Assert.AreEqual(0L, JavaText.Round(0.49999999999999994));
            Assert.AreEqual(109462L, JavaText.Round(0.5 * 218924));
            Assert.AreEqual(0L, JavaText.Round(double.NaN));
            Assert.AreEqual(long.MaxValue, JavaText.Round(1e20));
            Assert.AreEqual(long.MinValue, JavaText.Round(double.NegativeInfinity));
        }

        private static void AssertNextInts(long seed, int bound, params int[] expected)
        {
            var rng = new JavaRandom(seed);
            var actual = expected.Select(_ => rng.NextInt(bound)).ToArray();
            CollectionAssert.AreEqual(expected, actual, @"seed " + seed + @", bound " + bound);
        }
    }
}
