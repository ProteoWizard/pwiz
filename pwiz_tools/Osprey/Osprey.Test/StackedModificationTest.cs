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

using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The probes that tell the validity keys, without loading a library, whether any of its
    /// entries carries two modifications on one residue - the entries whose generated decoys
    /// changed when decoy fragments started adding them - and the per-file-version cache they
    /// and the blib reader probes answer through.
    /// </summary>
    [TestClass]
    public class StackedModificationTest
    {
        private const string HEADER = "ModifiedPeptide\tStrippedPeptide\tPrecursorMz\tFragmentMz\n";

        [TestMethod]
        public void TestTsvStackedModificationProbe()
        {
            // An N-terminal modification before a modified first residue, in each notation the
            // loader reads, and two modifications written in a row on one residue.
            AssertTsvProbe(true, @"_[UniMod:1]M[UniMod:35]PEPTIDEK_");
            AssertTsvProbe(true, @"(UniMod:1)M(UniMod:35)PEPTIDEK");
            AssertTsvProbe(true, @"_PEPTM[UniMod:35](UniMod:21)IDEK_");
            // One modification per residue, however close, and an N-terminal one alone.
            AssertTsvProbe(false, @"_C[UniMod:4]M[UniMod:35]PEPTIDEK_");
            AssertTsvProbe(false, @"_[UniMod:1]APEPTIDEK_");
            AssertTsvProbe(false, @"_PEPTIDEK_");

            // A library longer than one read of the file, with the entry at its end, and a
            // line longer than the read buffer: the scan works in whole lines across reads.
            var sb = new StringBuilder(HEADER);
            for (int i = 0; i < 40000; i++)
                sb.Append("_C[UniMod:4]PEPTIDEK_\tCPEPTIDEK\t500.0\t300.0\n");
            sb.Append("_PEPTIDEK_\tPEPTIDEK\t").Append('9', 3 << 20).Append("\t300.0\n");
            AssertTsvText(false, sb.ToString());
            sb.Append("_[UniMod:1]M[UniMod:35]PEPTIDEK_\tMPEPTIDEK\t520.0\t300.0\n");
            AssertTsvText(true, sb.ToString());

            // An empty file, and no file at all.
            AssertTsvText(false, string.Empty);
            Assert.IsFalse(DiannTsvLoader.HasStackedModifications(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())));
            Assert.IsFalse(LibraryLoader.HasStackedModifications(null));
        }

        [TestMethod]
        public void TestBlibStackedModificationProbe()
        {
            double[] peaks = { 300.0, 400.0 };
            foreach (var (modSequence, expected) in new[]
                     {
                         (@"[+42.010565]M[+15.994915]PEPTIDEK", true),
                         (@"PEPTM[+15.994915][+79.966331]IDEK", true),
                         (@"[+42.010565]APEPTIDEK", false),
                         (@"PEPC[+57.021464]M[+15.994915]IDEK", false),
                     })
            {
                string path = BlibLibraryInputTest.CreateBlib(Path.GetTempFileName(), modSequence, peaks, new (int, string, int)[0]);
                try
                {
                    Assert.AreEqual(expected, BlibLoader.HasStackedModifications(path), modSequence);
                    Assert.AreEqual(expected, LibraryLoader.HasStackedModifications(new LibrarySource(LibraryFormat.Blib, path)), modSequence);
                }
                finally
                {
                    BlibLibraryInputTest.TryDeleteFile(path);
                }
            }
        }

        [TestMethod]
        public void TestFileVersionProbeCachesOnlyAnswers()
        {
            string path = Path.GetTempFileName();
            try
            {
                var counting = new CountingProbe { Fail = true };
                var probe = new FileVersionProbe(counting.Read);

                Assert.IsFalse(probe.Ask(path), @"a probe that throws answers false");
                counting.Fail = false;
                Assert.IsTrue(probe.Ask(path), @"and is asked again");
                Assert.IsTrue(probe.Ask(path));
                Assert.AreEqual(2, counting.Calls, @"an answer is kept for the version of the file");

                File.AppendAllText(path, @"x");
                Assert.IsTrue(probe.Ask(path));
                Assert.AreEqual(3, counting.Calls, @"a new version of the file is asked again");

                Assert.IsFalse(probe.Ask(null));
                Assert.IsFalse(probe.Ask(path + @".missing"));
                Assert.AreEqual(3, counting.Calls, @"a missing file is not probed");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void TestStackedModificationPositions()
        {
            Assert.IsFalse(PeptideFragmentMass.HasStackedModifications(null));
            Assert.IsFalse(PeptideFragmentMass.HasStackedModifications(new Modification[0]));
            Assert.IsFalse(PeptideFragmentMass.HasStackedModifications(new[]
            {
                new Modification { Position = 0, MassDelta = 42.010565 },
                new Modification { Position = 1, MassDelta = 15.994915 },
            }));
            Assert.IsTrue(PeptideFragmentMass.HasStackedModifications(new[]
            {
                new Modification { Position = 3, MassDelta = 57.021464 },
                new Modification { Position = 0, MassDelta = 42.010565 },
                new Modification { Position = 0, MassDelta = 15.994915 },
            }));
            var summed = PeptideFragmentMass.ModMassesByPosition(new[]
            {
                new Modification { Position = 0, MassDelta = 42.010565 },
                new Modification { Position = 0, MassDelta = 15.994915 },
            });
            Assert.AreEqual(42.010565 + 15.994915, summed[0], 1e-12);
        }

        private static void AssertTsvProbe(bool expected, string modifiedPeptide)
        {
            AssertTsvText(expected, HEADER + @"_PEPTIDEK_" + "\tPEPTIDEK\t500.0\t300.0\n" +
                                    modifiedPeptide + "\tMPEPTIDEK\t520.0\t300.0\n");
        }

        private static void AssertTsvText(bool expected, string text)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, text);
                Assert.AreEqual(expected, DiannTsvLoader.HasStackedModifications(path),
                    text.Length > 200 ? text.Substring(text.Length - 200) : text);
                Assert.AreEqual(expected, LibraryLoader.HasStackedModifications(new LibrarySource(LibraryFormat.DiannTsv, path)));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>A probe that counts its reads and throws, as a locked file does, while <see cref="Fail"/> is set.</summary>
        private sealed class CountingProbe
        {
            public int Calls;
            public bool Fail;

            public bool Read(string path)
            {
                Calls++;
                if (Fail)
                    throw new IOException(@"locked");
                return true;
            }
        }
    }
}
