/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ElibQuantifiableTransitionTest : AbstractUnitTest
    {
        /// <summary>
        /// Verifies that Skyline correctly identifies which transitions are supposed to
        /// be non-quantitative.
        ///
        /// The .elib file used by this test has small differences in the m/z values in the
        /// "PeptideQuants" and "entries" tables. This test makes sure that
        /// <see cref="EncyclopeDiaLibrary.HasVeryCloseMatch"/> correctly prevents Skyline from
        /// being confused by this.
        /// </summary>
        [TestMethod]
        public void TestElibQuantifiableTransitions()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"Test\ElibQuantifiableTransitionTest.zip");
            var libSpec = new EncyclopeDiaSpec("test", TestFilesDir.GetTestPath("ElibQuantifiableTransitionTest.elib"));
            var library = EncyclopeDiaLibrary.Load(libSpec, new DefaultFileLoadMonitor(new SilentProgressMonitor()));

            foreach (var libKey in library.Keys)
            {
                Assert.IsTrue(library.TryLoadSpectrum(libKey, out var spectrum));
                Assert.AreNotEqual(0, spectrum.Peaks.Length);
                foreach (var peak in spectrum.Peaks)
                {
                    Assert.IsTrue(peak.Quantitative);
                }
                // Verify that every m/z is at least 0.05 units apart. 
                var mzList = spectrum.MZs.OrderBy(mz => mz).ToList();
                for (int i = 1; i < mzList.Count; i++)
                {
                    Assert.IsTrue(mzList[i] - mzList[i - 1] > 0.05, "{0} - {1} > .05 failed for {2}", mzList[i], mzList[i - 1], libKey);
                }
            }
            library.ReadStream.CloseStream();
        }
    }
}
