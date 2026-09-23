/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The library-wide minimum peptide length. This is enforced rather than assumed so
    /// that downstream code may rely on it: <c>DecoyGenerator.IsCandidateAcceptable</c>
    /// compares the full theoretical ladder, two rungs of which (y1 and b_{n-1}) are
    /// invariant under any C-terminus-preserving permutation and therefore always match,
    /// putting a structural 1/(n-1) floor under the overlap ratio. At the enforced minimum
    /// that floor is 1/5 = 0.2, comfortably under the 0.4 rejection threshold; at length 3
    /// it would be 0.5 and every candidate decoy would be rejected, silently dropping the
    /// peptide from the search.
    ///
    /// Real libraries do not come close: the Carafe libraries the regression runs against
    /// start at 7 residues, and MaxQuant's convention is 6. The bound exists so that a
    /// malformed library fails loudly instead of quietly changing what gets searched.
    ///
    /// Both format loaders are covered. An invariant enforced on only one of the two paths
    /// into the library is not an invariant, and the decoy generator cannot tell which
    /// loader produced its input.
    /// </summary>
    [TestClass]
    public class LibraryMinPeptideLengthTest
    {
        private const string HEADER =
            "ModifiedPeptide\tStrippedPeptide\tPrecursorMz\tPrecursorCharge\tTr_recalibrated\t" +
            "ProteinID\tFragmentMz\tRelativeIntensity\tFragmentType\tFragmentNumber\t" +
            "FragmentCharge\tFragmentLossType";

        private const string TOO_SHORT = "PEPTK";      // 5 residues
        private const string AT_MINIMUM = "PEPTIK";    // 6 residues

        [TestMethod]
        public void MinPeptideLengthIsSix()
        {
            // Pinned deliberately: this is a judgement encoded as a constant, not a tuning
            // knob. Changing it changes which libraries Osprey will accept, so a change
            // here should be a conscious decision that trips this test first.
            Assert.AreEqual(6, LibraryValidation.MIN_PEPTIDE_LENGTH);
        }

        [TestMethod]
        public void ValidatePeptideLengthEnforcesTheBound()
        {
            var ex = Assert.ThrowsException<InvalidDataException>(
                () => LibraryValidation.ValidatePeptideLength(TOO_SHORT));
            // The message has to identify WHICH peptide, or the error is unactionable on a
            // multi-million-row library.
            StringAssert.Contains(ex.Message, TOO_SHORT);
            StringAssert.Contains(ex.Message, LibraryValidation.MIN_PEPTIDE_LENGTH.ToString());

            // Boundary: exactly MIN_PEPTIDE_LENGTH must pass. A test that only checked the
            // rejection case would pass just as well with an off-by-one that rejected 6.
            LibraryValidation.ValidatePeptideLength(AT_MINIMUM);

            // A null sequence is some other check's problem; this one must not turn it into
            // a length complaint that sends the reader looking in the wrong place.
            LibraryValidation.ValidatePeptideLength(null);
        }

        [TestMethod]
        public void TsvLoaderEnforcesTheBound()
        {
            string shortPath = WriteTempTsv(BuildTsv(TOO_SHORT));
            string okPath = WriteTempTsv(BuildTsv(AT_MINIMUM));
            try
            {
                var loader = new DiannTsvLoader(2);
                var ex = Assert.ThrowsException<InvalidDataException>(() => loader.Load(shortPath));
                StringAssert.Contains(ex.Message, TOO_SHORT);

                var entries = loader.Load(okPath);
                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual(AT_MINIMUM, entries[0].Sequence);
            }
            finally
            {
                File.Delete(shortPath);
                File.Delete(okPath);
            }
        }

        [TestMethod]
        public void BlibLoaderEnforcesTheBound()
        {
            // The blib path reaches DecoyGenerator exactly as the TSV path does, so it has
            // to reject the same input. Written with BlibWriter rather than a checked-in
            // fixture so the test breaks if the two ever disagree about the schema.
            string shortPath = WriteTempBlib(TOO_SHORT);
            string okPath = WriteTempBlib(AT_MINIMUM);
            try
            {
                var loader = new BlibLoader();
                var ex = Assert.ThrowsException<InvalidDataException>(() => loader.Load(shortPath));
                StringAssert.Contains(ex.Message, TOO_SHORT);

                var entries = loader.Load(okPath);
                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual(AT_MINIMUM, entries[0].Sequence);
            }
            finally
            {
                File.Delete(shortPath);
                File.Delete(okPath);
            }
        }

        private static string BuildTsv(string strippedPeptide)
        {
            // Two fragments so the precursor clears the loader's minimum-fragment filter
            // and actually reaches the length check.
            string row = "_" + strippedPeptide + "_\t" + strippedPeptide +
                         "\t400.0\t2\t10.5\tsp|P00001|TEST_HUMAN\t{0}\t{1}\ty\t{2}\t1\tnoloss";
            return HEADER + "\n" +
                   string.Format(row, @"100.0", @"1.0", 1) + "\n" +
                   string.Format(row, @"200.0", @"0.8", 2) + "\n";
        }

        private static string WriteTempTsv(string content)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, content);
            return path;
        }

        private static string WriteTempBlib(string strippedPeptide)
        {
            string path = Path.GetTempFileName();
            var entry = new LibraryEntry(1, strippedPeptide, strippedPeptide, 2, 400.0, 10.5)
            {
                Fragments = new List<LibraryFragment>
                {
                    new LibraryFragment { Mz = 100.0, RelativeIntensity = 1.0f },
                    new LibraryFragment { Mz = 200.0, RelativeIntensity = 0.8f }
                }
            };
            using (var writer = new BlibWriter(path))
            {
                writer.AddSpectrum(entry, @"test.mzML", 10.5);
                writer.FinalizeDatabase();
            }
            return path;
        }
    }
}
