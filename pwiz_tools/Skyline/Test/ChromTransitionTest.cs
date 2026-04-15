/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ChromTransitionTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestChromTransitionFlagValues()
        {
            Assert.AreEqual(ChromTransition.FlagValues.fragment, MakeChromTransition(ChromSource.fragment).Flags);
            Assert.AreEqual(ChromTransition.FlagValues.ms1, MakeChromTransition(ChromSource.ms1).Flags);
            Assert.AreEqual(ChromTransition.FlagValues.sim, MakeChromTransition(ChromSource.sim).Flags);
            Assert.AreEqual(ChromTransition.FlagValues.unknown, MakeChromTransition(ChromSource.unknown).Flags);
        }

        [TestMethod]
        public void TestChromTransitionMissingMassErrors()
        {
            foreach (var chromSource in ListChromSourceValues())
            {
                ChromTransition chromTransition = MakeChromTransition(chromSource);
                Assert.AreEqual(chromSource, chromTransition.Source);
                Assert.IsFalse(chromTransition.MissingMassErrors);
                chromTransition.MissingMassErrors = true;
                Assert.IsTrue(chromTransition.MissingMassErrors);
                Assert.AreEqual(chromSource, chromTransition.Source);
                chromTransition.MissingMassErrors = false;
                Assert.IsFalse(chromTransition.MissingMassErrors);
                Assert.AreEqual(chromSource, chromTransition.Source);
            }
        }

        [TestMethod]
        public void TestChromTransitionSource()
        {
            foreach (var chromSource1 in ListChromSourceValues())
            {
                var chromTransition = MakeChromTransition(chromSource1);
                Assert.AreEqual(chromSource1, chromTransition.Source);
                Assert.IsFalse(chromTransition.MissingMassErrors);
                foreach (var missingMassErrors in new[] {true, false})
                {
                    chromTransition.MissingMassErrors = missingMassErrors;
                    Assert.AreEqual(chromSource1, chromTransition.Source);
                    Assert.AreEqual(missingMassErrors, chromTransition.MissingMassErrors);
                    foreach (var chromSource2 in ListChromSourceValues())
                    {
                        chromTransition.Source = chromSource2;
                        Assert.AreEqual(missingMassErrors, chromTransition.MissingMassErrors);
                        Assert.AreEqual(chromSource2, chromTransition.Source);
                    }
                    chromTransition.Source = chromSource1;
                    Assert.AreEqual(chromSource1, chromTransition.Source);
                }
            }
        }

        [TestMethod]
        public unsafe void TestChromTransitionSize()
        {
            Assert.AreEqual(4, sizeof(ChromTransition4));
            Assert.AreEqual(16, sizeof(ChromTransition5));

            // Explicit per-version expected sizes. Update when adding a new
            // CacheFormatVersion that changes ChromTransition — AND update
            // ChromTransition.GetStructSize to match.
            var expectedSizes = new Dictionary<CacheFormatVersion, int>
            {
                { CacheFormatVersion.Two, 4 },
                { CacheFormatVersion.Three, 4 },
                { CacheFormatVersion.Four, 4 },
                { CacheFormatVersion.Five, 16 },
                { CacheFormatVersion.Six, 16 },
                { CacheFormatVersion.Seven, 24 },
                { CacheFormatVersion.Eight, 24 },
                { CacheFormatVersion.Nine, 24 },
                { CacheFormatVersion.Ten, 24 },
                { CacheFormatVersion.Eleven, 24 },
                { CacheFormatVersion.Twelve, 24 },
                { CacheFormatVersion.Thirteen, 24 },
                { CacheFormatVersion.Fourteen, 24 },
                { CacheFormatVersion.Fifteen, 24 },
                { CacheFormatVersion.Sixteen, 24 },
                { CacheFormatVersion.Seventeen, 24 },
                { CacheFormatVersion.Eighteen, 24 },
                { CacheFormatVersion.Nineteen, 24 },
                { CacheFormatVersion.Twenty, 28 },
            };
            foreach (CacheFormatVersion v in Enum.GetValues(typeof(CacheFormatVersion)))
            {
                Assert.IsTrue(expectedSizes.TryGetValue(v, out var expected),
                    "Add expected ChromTransition size for CacheFormatVersion." + v +
                    " and update ChromTransition.GetStructSize to match.");
                Assert.AreEqual(expected, ChromTransition.GetStructSize(v),
                    "ChromTransition.GetStructSize(" + v + ") mismatch");
            }

            // Current struct layout must match whatever GetStructSize reports for CURRENT.
            // If you add a field to ChromTransition, bump CacheFormatVersion.CURRENT and
            // update GetStructSize together — this assert will fail if only one side moved.
            Assert.AreEqual(sizeof(ChromTransition),
                ChromTransition.GetStructSize(CacheFormatVersion.CURRENT),
                "ChromTransition struct size doesn't match GetStructSize(CURRENT). " +
                "Did you add a field without bumping the cache format version?");
        }

        /// <summary>
        /// Verifies that "ToString()" on enum values are what we expect.
        /// The [Flags] attribute on the FlagValues enum tells the runtime that "ToString()" 
        /// should produce comma delimited combinations of values that were OR'd together.
        /// </summary>
        [TestMethod]
        public void TestFlagValuesToString()
        {
            Assert.AreEqual("unknown", ChromTransition.FlagValues.unknown.ToString());
            Assert.AreEqual("missing_mass_errors", ChromTransition.FlagValues.missing_mass_errors.ToString());
            Assert.AreEqual("ms1", ChromTransition.FlagValues.ms1.ToString());
            Assert.AreEqual("ms1, missing_mass_errors", (ChromTransition.FlagValues.ms1 | ChromTransition.FlagValues.missing_mass_errors).ToString());
            Assert.AreEqual("fragment", ChromTransition.FlagValues.fragment.ToString());
            Assert.AreEqual("fragment, missing_mass_errors", (ChromTransition.FlagValues.fragment | ChromTransition.FlagValues.missing_mass_errors).ToString());
            Assert.AreEqual("sim", ChromTransition.FlagValues.sim.ToString());
            Assert.AreEqual("sim, missing_mass_errors", (ChromTransition.FlagValues.sim | ChromTransition.FlagValues.missing_mass_errors).ToString());
        }

        private ChromSource[] ListChromSourceValues()
        {
            return new[] {ChromSource.unknown, ChromSource.fragment, ChromSource.ms1, ChromSource.sim};
        }

        private ChromTransition MakeChromTransition(ChromSource chromSource)
        {
            return new ChromTransition(0, 0, IonMobilityFilter.EMPTY, chromSource, 0);
        }
    }
}
