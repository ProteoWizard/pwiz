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

using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Blib libraries as search input: fragment ion types read from
    /// <c>RefSpectraPeakAnnotations</c> (<see cref="BlibPeakAnnotations"/>), and the decoys
    /// generated from those typed fragments.
    /// </summary>
    [TestClass]
    public class BlibLibraryInputTest
    {
        private const string SEQUENCE = @"PEPCTIDEK";
        private const string MOD_SEQUENCE = @"PEPC[+57.021464]TIDEK";
        private const int CYSTEINE_POSITION = 3;
        private const double CARBAMIDOMETHYL = 57.021464;

        [TestMethod]
        public void TestBlibPeakAnnotationNames()
        {
            AssertParsed(@"b3", 0, IonType.B, 3, 1, NeutralLossCode.None);
            AssertParsed(@"y7", 0, IonType.Y, 7, 1, NeutralLossCode.None);
            AssertParsed(@"Y7", 2, IonType.Y, 7, 2, NeutralLossCode.None);
            // The charge column wins; suffixes apply only when it is 0.
            AssertParsed(@"y7^2", 0, IonType.Y, 7, 2, NeutralLossCode.None);
            AssertParsed(@"y7^2", 3, IonType.Y, 7, 3, NeutralLossCode.None);
            AssertParsed(@"y7++", 0, IonType.Y, 7, 2, NeutralLossCode.None);
            AssertParsed(@"b4+2", 0, IonType.B, 4, 2, NeutralLossCode.None);
            AssertParsed(@"y7-H2O", 1, IonType.Y, 7, 1, NeutralLossCode.H2O);
            AssertParsed(@"b5-NH3", 1, IonType.B, 5, 1, NeutralLossCode.NH3);
            AssertParsed(@"y9-H3PO4", 1, IonType.Y, 9, 1, NeutralLossCode.H3PO4);
            // A decimal loss snaps to a known loss within half its last printed digit (at least
            // 0.005 Th), an integer one by nominal mass.
            AssertParsed(@"y4-97.9769", 1, IonType.Y, 4, 1, NeutralLossCode.H3PO4);
            AssertParsed(@"y4-44.0262", 1, IonType.Y, 4, 1, NeutralLossCode.Custom);
            AssertParsed(@"y4-44.03", 1, IonType.Y, 4, 1, NeutralLossCode.Custom);
            AssertParsed(@"y7-18.0", 1, IonType.Y, 7, 1, NeutralLossCode.H2O);
            AssertParsed(@"b5-17.0", 1, IonType.B, 5, 1, NeutralLossCode.NH3);
            AssertParsed(@"y9-98.0", 1, IonType.Y, 9, 1, NeutralLossCode.H3PO4);
            // NIST-style tails and anything after whitespace are ignored.
            AssertParsed(@"y7-18^2/0.3ppm", 0, IonType.Y, 7, 2, NeutralLossCode.H2O);
            AssertParsed(@"b3 some comment", 1, IonType.B, 3, 1, NeutralLossCode.None);
            // The other ion types are read, so they are not counted as unreadable (Apply ignores them).
            AssertParsed(@"a2", 1, IonType.A, 2, 1, NeutralLossCode.None);
            AssertParsed(@"z3", 1, IonType.Z, 3, 1, NeutralLossCode.None);
            // The names BlibWriter writes read back as the ion they were written from.
            foreach (string name in new[] { @"y7", @"b3-H2O", @"y5-NH3", @"y9-H3PO4", @"b4-44.9977" })
            {
                Assert.IsTrue(BlibPeakAnnotations.TryParseName(name, 1, out var parsed), name);
                Assert.AreEqual(name, BlibPeakAnnotations.FormatName(parsed));
            }
            // "NaN" and "Infinity" parse as numbers, but no fragment loses either.
            foreach (string name in new[] { null, string.Empty, @"p", @"?", @"precursor", @"y", @"y0", @"y7-", @"y7-junk", @"b3x",
                         @"y7-NaN", @"y7-nan", @"y7-Infinity", @"b3--Infinity", @"y7^bad", @"y7^2foo", @"y7^" })
            {
                Assert.IsFalse(BlibPeakAnnotations.TryParseName(name, 1, out _), name ?? @"null");
            }
        }

        [TestMethod]
        public void TestBlibLoaderTypesAnnotatedPeaks()
        {
            var cysteine = new[] { new Modification { Position = CYSTEINE_POSITION, MassDelta = CARBAMIDOMETHYL } };
            var modMasses = PeptideFragmentMass.ModMassesByPosition(cysteine);
            double b3 = Mz(IonType.B, 3, 1, modMasses, null);
            double y3 = Mz(IonType.Y, 3, 1, modMasses, null);
            double y5Doubly = Mz(IonType.Y, 5, 2, modMasses, null);
            double y4Water = Mz(IonType.Y, 4, 1, modMasses, NeutralLoss.H2OMass);
            double b5 = Mz(IonType.B, 5, 1, modMasses, null);
            double[] peaks = { b3, y3, y5Doubly, y4Water, b5, 700.0 };
            var annotations = new[]
            {
                (0, @"b3", 1),
                (1, @"y3", 1),
                (1, @"y3-NH3", 1),          // a lossy name for the same peak: m/z disagrees, ignored
                (2, @"y5", 2),
                (3, @"y4-H2O", 1),
                (4, @"y5", 1),              // names a different ion: m/z disagrees, ignored
                (5, @"?", 0),               // unreadable name, peak stays Unknown
            };

            string path = CreateBlib(peaks, annotations);
            try
            {
                var entry = new BlibLoader().Load(path).Single();
                var types = entry.Fragments.Select(f => f.Annotation).ToArray();
                AssertAnnotation(types[0], IonType.B, 3, 1, NeutralLossCode.None);
                AssertAnnotation(types[1], IonType.Y, 3, 1, NeutralLossCode.None);
                AssertAnnotation(types[2], IonType.Y, 5, 2, NeutralLossCode.None);
                AssertAnnotation(types[3], IonType.Y, 4, 1, NeutralLossCode.H2O);
                Assert.AreEqual(IonType.Unknown, types[4].IonType);
                Assert.AreEqual(IonType.Unknown, types[5].IonType);

                // Decoys generated from typed fragments get their own m/z, not the target's.
                var decoys = DecoyGenerator.GenerateAllWithCollisionDetection(new List<LibraryEntry> { entry },
                    new OspreyConfig(), null, false, out _);
                var decoy = decoys.Single();
                Assert.IsTrue(decoy.IsDecoy);
                Assert.AreNotEqual(entry.Fragments[0].Mz, decoy.Fragments[0].Mz);
                var decoyMods = PeptideFragmentMass.ModMassesByPosition(decoy.Modifications);
                double? decoyB3 = PeptideFragmentMass.CalculateFragmentMz(IonType.B, 3, 1, decoy.Sequence, decoyMods, null);
                Assert.IsNotNull(decoyB3);
                Assert.AreEqual(decoyB3.Value, decoy.Fragments[0].Mz, 1e-9);
            }
            finally
            {
                TryDeleteFile(path);
            }

            // Without annotation rows every peak stays Unknown, exactly as before.
            string plain = CreateBlib(peaks, new (int, string, int)[0]);
            try
            {
                var entry = new BlibLoader().Load(plain).Single();
                Assert.IsTrue(entry.Fragments.All(f => f.Annotation.IonType == IonType.Unknown));
                CollectionAssert.AreEqual(Enumerable.Range(1, peaks.Length).Select(i => (byte)i).ToArray(),
                    entry.Fragments.Select(f => f.Annotation.Ordinal).ToArray());
            }
            finally
            {
                TryDeleteFile(plain);
            }

            AssertMalformedRowsAreNotFatal(peaks);
            AssertStackedModificationsAnnotate();
            AssertNonFiniteMzIsRejected();
            AssertRejectionsAreCountedByCause();
            AssertPreferenceRule();
            AssertProbeFailuresFailClosed();
        }

        /// <summary>
        /// A library entry written by <see cref="LibraryBlibWriter"/> reads back as the same entry:
        /// peaks sorted by m/z with every b and y ion typed as it was (neutral losses and charge
        /// included), stacked N-terminal modifications summed onto the first residue, a known
        /// modification at its exact mass and an unknown one to four decimals, proteins and
        /// retention time. An a ion and an untyped peak are written unannotated.
        /// </summary>
        [TestMethod]
        public void TestLibraryBlibWriterRoundTrip()
        {
            const string sequence = @"MPEPCTIDEK";
            const double methyl = 14.01565;
            var modifications = new[]
            {
                new Modification { Position = 0, MassDelta = 42.010565 },
                new Modification { Position = 0, MassDelta = 15.994915 },
                new Modification { Position = 4, MassDelta = CARBAMIDOMETHYL },
                new Modification { Position = 9, MassDelta = methyl },
            };
            var modMasses = PeptideFragmentMass.ModMassesByPosition(modifications);
            var typed = new[]
            {
                Annotation(IonType.Y, 3, 1, NeutralLossCode.None, 0),
                Annotation(IonType.B, 2, 1, NeutralLossCode.None, 0),
                Annotation(IonType.Y, 5, 2, NeutralLossCode.None, 0),
                Annotation(IonType.Y, 4, 1, NeutralLossCode.H2O, 0),
                Annotation(IonType.B, 4, 1, NeutralLossCode.Custom, 44.9977),
            };
            var fragments = typed.Select((a, i) => new LibraryFragment
            {
                Mz = PeptideFragmentMass.CalculateFragmentMz(a.IonType, a.Ordinal, a.Charge, sequence, modMasses,
                    a.HasNeutralLoss ? a.NeutralLossMass : null).Value,
                RelativeIntensity = 1000f - i,
                Annotation = a
            }).Concat(new[]
            {
                new LibraryFragment { Mz = 650.5, RelativeIntensity = 5f, Annotation = Annotation(IonType.A, 2, 1, NeutralLossCode.None, 0) },
                new LibraryFragment { Mz = 700.25, RelativeIntensity = 3f, Annotation = Annotation(IonType.Unknown, 0, 1, NeutralLossCode.None, 0) },
            }).ToArray();
            var entry = new LibraryEntry(7, sequence, @"_(UniMod:1)M(UniMod:35)PEPC(UniMod:4)TIDEK_", 2, 624.28, 12.5)
            {
                Modifications = modifications,
                Fragments = fragments,
                ProteinIds = new[] { @"P12345", @"Q67890" }
            };

            string path = Path.GetTempFileName();
            try
            {
                Assert.AreEqual(1, LibraryBlibWriter.Write(path, new[] { entry }, @"library.tsv"));
                Assert.AreEqual(typed.Length, BlibComparer.CountWhere(path, BlibPeakAnnotations.TABLE_NAME, @"mzTheoretical > 0"));
                var read = new BlibLoader().Load(path).Single();
                Assert.AreEqual(@"M[+58.0055]PEPC[+57.0215]TIDEK[+14.0157]", read.ModifiedSequence);
                Assert.AreEqual(entry.PrecursorMz, read.PrecursorMz);
                Assert.AreEqual(entry.Charge, read.Charge);
                Assert.AreEqual(entry.RetentionTime, read.RetentionTime);
                CollectionAssert.AreEqual(entry.ProteinIds.ToArray(), read.ProteinIds.ToArray());

                var readMods = PeptideFragmentMass.ModMassesByPosition(read.Modifications);
                Assert.AreEqual(3, readMods.Count);
                Assert.AreEqual(modMasses[0], readMods[0], 5e-5);
                Assert.AreEqual(CARBAMIDOMETHYL, readMods[4]);
                Assert.AreEqual(methyl, readMods[9], 5e-5);

                var sorted = fragments.OrderBy(f => f.Mz).ToArray();
                CollectionAssert.AreEqual(sorted.Select(f => f.Mz).ToArray(), read.Fragments.Select(f => f.Mz).ToArray());
                // The reader scales the base peak to 1, as a DIA-NN library already is.
                float basePeak = fragments.Max(f => f.RelativeIntensity);
                CollectionAssert.AreEqual(sorted.Select(f => f.RelativeIntensity / basePeak).ToArray(),
                    read.Fragments.Select(f => f.RelativeIntensity).ToArray());
                for (int i = 0; i < sorted.Length; i++)
                {
                    var expected = sorted[i].Annotation;
                    var actual = read.Fragments[i].Annotation;
                    if (expected.IonType == IonType.B || expected.IonType == IonType.Y)
                    {
                        AssertAnnotation(actual, expected.IonType, expected.Ordinal, expected.Charge, expected.NeutralLoss);
                        Assert.AreEqual(expected.CustomLossMass, actual.CustomLossMass);
                    }
                    else
                    {
                        Assert.AreEqual(IonType.Unknown, actual.IonType);
                    }
                }

                LibraryBlibWriter.Write(path, new[] { entry }, @"library.tsv", false);
                Assert.AreEqual(0, BlibComparer.CountWhere(path, BlibPeakAnnotations.TABLE_NAME, @"1"));
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// A malformed annotation row is passed over or counted, never fatal: a NULL RefSpectraID
        /// is skipped, a peakIndex or charge that is not an integer counts as a rejection or as no
        /// charge, and a table written without the id column reads the same. The summary line
        /// states how many of the library's spectra were typed.
        /// </summary>
        private static void AssertMalformedRowsAreNotFatal(double[] peaks)
        {
            string path = CreateBlib(peaks, new[] { (0, @"b3", 1) });
            try
            {
                const string columns = @"(RefSpectraID, peakIndex, name, formula, inchiKey, otherKeys, charge, adduct, comment, mzTheoretical, mzObserved)";
                ExecuteSql(path,
                    @"INSERT INTO RefSpectraPeakAnnotations " + columns + @" VALUES (NULL, 0, 'y3', '', '', '', 1, '', '', 0, 0)",
                    @"INSERT INTO RefSpectraPeakAnnotations " + columns + @" VALUES ((SELECT MIN(id) FROM RefSpectra), 'one', 'y3', '', '', '', 'two', '', '', 0, 0)",
                    @"CREATE TABLE annotations_without_id AS SELECT RefSpectraID, peakIndex, name, formula, inchiKey, otherKeys, charge, adduct, comment, mzTheoretical, mzObserved FROM RefSpectraPeakAnnotations",
                    @"DROP TABLE RefSpectraPeakAnnotations",
                    @"ALTER TABLE annotations_without_id RENAME TO RefSpectraPeakAnnotations");
                var log = new List<string>();
                var entry = new BlibLoader().Load(path, log.Add).Single();
                AssertAnnotation(entry.Fragments[0].Annotation, IonType.B, 3, 1, NeutralLossCode.None);
                // One spectrum of one typed, one peak typed and the rest without an annotation;
                // the text peakIndex is the one range rejection, the NULL RefSpectraID no row at all.
                string summary = string.Format(OspreyIOResources.BlibAnnotationStats_Summary_Library_fragment_annotations___0_N0__of__1_N0__spectra_typed,
                    1, 1, 1, peaks.Length - 1, 0, 0, 1, 0);
                CollectionAssert.Contains(log, summary);
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Of several annotations that fit one peak, one without a neutral loss wins, then the
        /// lower charge, and on a tie the earlier row stays. The loader test cannot show this,
        /// because two names for one peak rarely both fit its m/z.
        /// </summary>
        private static void AssertPreferenceRule()
        {
            var plain = new FragmentAnnotation { IonType = IonType.Y, Ordinal = 3, Charge = 1 };
            var lossy = new FragmentAnnotation { IonType = IonType.Y, Ordinal = 3, Charge = 1, NeutralLoss = NeutralLossCode.H2O };
            var doubly = new FragmentAnnotation { IonType = IonType.Y, Ordinal = 6, Charge = 2 };
            Assert.IsTrue(BlibPeakAnnotations.IsPreferred(plain, lossy));
            Assert.IsFalse(BlibPeakAnnotations.IsPreferred(lossy, plain));
            Assert.IsTrue(BlibPeakAnnotations.IsPreferred(plain, doubly));
            Assert.IsFalse(BlibPeakAnnotations.IsPreferred(doubly, plain));
            Assert.IsTrue(BlibPeakAnnotations.IsPreferred(doubly, lossy), @"no loss outranks a lower charge");
            Assert.IsFalse(BlibPeakAnnotations.IsPreferred(plain, plain), @"a tie keeps the earlier row");
        }

        /// <summary>
        /// A probe that fails - the file is locked, or not a database - answers that the file
        /// reads differently, so the keys and the .libcache fail toward re-running rather than
        /// adopting an output written before a reader change; and that answer is not remembered,
        /// so the next question about the same version of the file reads it again. A cached
        /// false kept an annotated blib reading as unannotated for the rest of the process.
        /// </summary>
        private static void AssertProbeFailuresFailClosed()
        {
            string path = CreateBlib(new[] { 300.0, 400.0 }, new (int, string, int)[0]);
            try
            {
                byte[] blib = File.ReadAllBytes(path);
                var written = File.GetLastWriteTimeUtc(path);
                // The same length and time as the blib, so the probe sees the same version of
                // the file, but not a database: the probe fails.
                File.WriteAllBytes(path, new byte[blib.Length]);
                File.SetLastWriteTimeUtc(path, written);
                Assert.IsTrue(BlibLoader.HasPeakAnnotations(path), @"a probe that fails answers that the file reads differently");

                File.WriteAllBytes(path, blib);
                File.SetLastWriteTimeUtc(path, written);
                Assert.IsFalse(BlibLoader.HasPeakAnnotations(path), @"a failed probe must not be cached");
            }
            finally
            {
                TryDeleteFile(path);
            }

            string file = Path.GetTempFileName();
            try
            {
                var counting = new CountingProbe { Fail = true };
                var probe = new FileVersionProbe(counting.Read);
                Assert.IsTrue(probe.Ask(file), @"a probe that throws answers true");
                counting.Fail = false;
                Assert.IsFalse(probe.Ask(file), @"and is asked again");
                Assert.IsFalse(probe.Ask(file));
                Assert.AreEqual(2, counting.Calls, @"an answer is kept for the version of the file");

                File.AppendAllText(file, @"x");
                Assert.IsFalse(probe.Ask(file));
                Assert.AreEqual(3, counting.Calls, @"a new version of the file is asked again");

                Assert.IsFalse(probe.Ask(null));
                Assert.IsFalse(probe.Ask(file + @".missing"));
                Assert.AreEqual(3, counting.Calls, @"a missing file is not probed");
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// A row naming a peak the spectrum lacks, or an ion as long as the peptide, is counted
        /// apart from a name the grammar cannot read, so the summary line points at the cause.
        /// </summary>
        private static void AssertRejectionsAreCountedByCause()
        {
            var fragments = new[] { new LibraryFragment { Mz = 300.0, RelativeIntensity = 1f } };
            var stats = new BlibAnnotationStats();
            BlibPeakAnnotations.Apply(SEQUENCE, new Modification[0], fragments, new List<BlibAnnotationRow>
            {
                new BlibAnnotationRow { PeakIndex = 5, Name = @"y3", Charge = 1 },
                new BlibAnnotationRow { PeakIndex = -1, Name = @"y3", Charge = 1 },
                new BlibAnnotationRow { PeakIndex = 0, Name = @"y" + SEQUENCE.Length, Charge = 1 },
                new BlibAnnotationRow { PeakIndex = 0, Name = @"?", Charge = 1 },
                // Well-formed names of ions the reader cannot check against the peak: not unreadable.
                new BlibAnnotationRow { PeakIndex = 0, Name = @"a3", Charge = 1 },
                new BlibAnnotationRow { PeakIndex = 0, Name = @"c2", Charge = 1 },
                new BlibAnnotationRow { PeakIndex = 0, Name = @"x4", Charge = 1 },
                new BlibAnnotationRow { PeakIndex = 0, Name = @"z5", Charge = 1 },
            }, stats);
            Assert.AreEqual(3, stats.NRejectedRange);
            Assert.AreEqual(1, stats.NRejectedName, @"a, c, x and z ions are not unreadable names");
            Assert.AreEqual(4, stats.NUncheckedIonType);
            Assert.AreEqual(0, stats.NRejectedMz);
        }

        /// <summary>
        /// An N-terminal acetyl and an oxidized first methionine both sit at position 0, so a
        /// b ion's m/z carries their sum, and a correctly annotated b ion is accepted.
        /// </summary>
        private static void AssertStackedModificationsAnnotate()
        {
            const string sequence = @"MPEPTIDEK";
            var mods = BlibLoader.ParseBlibModifications(@"[+42.010565]M[+15.994915]PEPTIDEK");
            Assert.AreEqual(2, mods.Count(m => m.Position == 0));
            double b3 = PeptideFragmentMass.PROTON_MASS + 42.010565 + 15.994915;
            foreach (char aa in sequence.Substring(0, 3))
            {
                Assert.IsTrue(PeptideFragmentMass.TryGetResidueMass(aa, out double residue));
                b3 += residue;
            }
            var stats = ApplyOne(sequence, mods, b3, @"b3", out var annotation);
            Assert.AreEqual(1, stats.NPeaksAnnotated, @"the b3 annotation matches the peak once both modifications count");
            AssertAnnotation(annotation, IonType.B, 3, 1, NeutralLossCode.None);
        }

        /// <summary>
        /// A peak whose m/z is not finite matches no annotation; an infinite one would otherwise
        /// widen the ppm tolerance to infinity and pass any name.
        /// </summary>
        private static void AssertNonFiniteMzIsRejected()
        {
            var cysteine = new[] { new Modification { Position = CYSTEINE_POSITION, MassDelta = CARBAMIDOMETHYL } };
            foreach (double peakMz in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var stats = ApplyOne(SEQUENCE, cysteine, peakMz, @"y3", out var annotation);
                Assert.AreEqual(0, stats.NPeaksAnnotated, peakMz.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(1, stats.NRejectedMz, peakMz.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(IonType.Unknown, annotation.IonType);
            }
        }

        /// <summary>Applies one annotation row to a one-peak spectrum.</summary>
        private static BlibAnnotationStats ApplyOne(string sequence, IReadOnlyList<Modification> modifications,
            double peakMz, string name, out FragmentAnnotation annotation)
        {
            var fragments = new[]
            {
                new LibraryFragment
                {
                    Mz = peakMz,
                    RelativeIntensity = 1f,
                    Annotation = new FragmentAnnotation { IonType = IonType.Unknown, Ordinal = 1, Charge = 1 },
                },
            };
            var stats = new BlibAnnotationStats();
            BlibPeakAnnotations.Apply(sequence, modifications, fragments,
                new List<BlibAnnotationRow> { new BlibAnnotationRow { PeakIndex = 0, Name = name, Charge = 1 } }, stats);
            annotation = fragments[0].Annotation;
            return stats;
        }

        private static double Mz(IonType ionType, int ordinal, byte charge, IReadOnlyDictionary<int, double> modMasses, double? loss)
        {
            double? mz = PeptideFragmentMass.CalculateFragmentMz(ionType, ordinal, charge, SEQUENCE, modMasses, loss);
            Assert.IsNotNull(mz);
            return mz.Value;
        }

        private static string CreateBlib(double[] peaks, (int PeakIndex, string Name, int Charge)[] annotations)
        {
            return CreateBlib(Path.GetTempFileName(), MOD_SEQUENCE, peaks, annotations);
        }

        /// <summary>
        /// A one-spectrum blib of <see cref="SEQUENCE"/> at <paramref name="path"/>, written the
        /// way Osprey writes its own, plus the given <c>RefSpectraPeakAnnotations</c> rows.
        /// </summary>
        internal static string CreateBlib(string path, string modSequence, double[] peaks,
            (int PeakIndex, string Name, int Charge)[] annotations)
        {
            long refId;
            using (var writer = new BlibWriter(path))
            {
                long fileId = writer.AddSourceFile(@"test.mzML", @"library.tsv", 0.01);
                refId = writer.AddSpectrum(SEQUENCE, modSequence, 525.25, 2, 10.0, 9.0, 11.0,
                    peaks, peaks.Select((_, i) => 100f + i).ToArray(), 0.01, fileId, 1, 0.0);
                writer.FinalizeDatabase();
            }
            using (var conn = new SQLiteConnection(@"Data Source=" + path + @";Version=3;"))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    foreach (var (peakIndex, name, charge) in annotations)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"INSERT INTO RefSpectraPeakAnnotations
                                (RefSpectraID, peakIndex, name, formula, inchiKey, otherKeys, charge, adduct, comment, mzTheoretical, mzObserved)
                                VALUES (@ref, @peak, @name, '', '', '', @charge, '', '', @mz, @mz)";
                            cmd.Parameters.AddWithValue(@"@ref", refId);
                            cmd.Parameters.AddWithValue(@"@peak", peakIndex);
                            cmd.Parameters.AddWithValue(@"@name", name);
                            cmd.Parameters.AddWithValue(@"@charge", charge);
                            cmd.Parameters.AddWithValue(@"@mz", peaks[peakIndex]);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
            }
            SQLiteConnection.ClearAllPools();
            return path;
        }

        private static void AssertParsed(string name, int chargeColumn, IonType ionType, byte ordinal, byte charge, NeutralLossCode loss)
        {
            Assert.IsTrue(BlibPeakAnnotations.TryParseName(name, chargeColumn, out var annotation), name);
            AssertAnnotation(annotation, ionType, ordinal, charge, loss);
        }

        private static FragmentAnnotation Annotation(IonType ionType, byte ordinal, byte charge, NeutralLossCode loss, double customLoss)
        {
            return new FragmentAnnotation
            {
                IonType = ionType, Ordinal = ordinal, Charge = charge, NeutralLoss = loss, CustomLossMass = customLoss
            };
        }

        private static void AssertAnnotation(FragmentAnnotation annotation, IonType ionType, byte ordinal, byte charge, NeutralLossCode loss)
        {
            Assert.AreEqual(ionType, annotation.IonType);
            Assert.AreEqual(ordinal, annotation.Ordinal);
            Assert.AreEqual(charge, annotation.Charge);
            Assert.AreEqual(loss, annotation.NeutralLoss);
        }

        /// <summary>Runs <paramref name="statements"/> against the blib at <paramref name="path"/>.</summary>
        private static void ExecuteSql(string path, params string[] statements)
        {
            using (var conn = new SQLiteConnection(@"Data Source=" + path + @";Version=3;"))
            {
                conn.Open();
                foreach (string sql in statements)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            SQLiteConnection.ClearAllPools();
        }

        internal static void TryDeleteFile(string path)
        {
            SQLiteConnection.ClearAllPools();
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A test's temp file; a lingering handle must not fail the test.
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
                return false;
            }
        }
    }
}
