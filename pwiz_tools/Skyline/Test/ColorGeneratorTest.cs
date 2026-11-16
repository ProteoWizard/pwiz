/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Collections.Generic;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ColorGeneratorTest : AbstractUnitTest
    {
        /// <summary>
        /// Peptide colors must be the same in every process and match the colors .NET Framework
        /// builds of Skyline have always shown. Expected values come from Skyline 26.1.1 (.NET Framework).
        /// </summary>
        [TestMethod]
        public void TestPeptideColorsStable()
        {
            AssertColor(Color.FromArgb(159, 199, 0), @"PEPTIDER");
            AssertColor(Color.FromArgb(0, 182, 0), @"IEAIPQIDK");
            AssertColor(Color.FromArgb(0, 138, 205), @"LGGNEQVTR");
            AssertColor(Color.FromArgb(116, 58, 111), @"C[+57.021464]PEPTIDE");
            AssertColor(Color.FromArgb(41, 95, 40), @"VLVDAGR");
            AssertColor(Color.FromArgb(0, 148, 0), @"A");
            // A sibling with the hashed color moves the peptide to the next distinguishable color.
            AssertColor(Color.FromArgb(162, 36, 84), @"PEPTIDER", Color.FromArgb(159, 199, 0));
        }

        private static void AssertColor(Color expected, string peptideName, params Color[] siblingColors)
        {
            var actual = ColorGenerator.GetColor(peptideName, new List<Color>(siblingColors));
            Assert.AreEqual(expected.ToArgb(), actual.ToArgb(), peptideName);
        }
    }
}
