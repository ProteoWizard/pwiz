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
            // A decimal loss snaps to a known loss within 0.005 Th, an integer one by nominal mass.
            AssertParsed(@"y4-97.9769", 1, IonType.Y, 4, 1, NeutralLossCode.H3PO4);
            AssertParsed(@"y4-44.0262", 1, IonType.Y, 4, 1, NeutralLossCode.Custom);
            // NIST-style tails and anything after whitespace are ignored.
            AssertParsed(@"y7-18^2/0.3ppm", 0, IonType.Y, 7, 2, NeutralLossCode.H2O);
            AssertParsed(@"b3 some comment", 1, IonType.B, 3, 1, NeutralLossCode.None);
            // "NaN" and "Infinity" parse as numbers, but no fragment loses either.
            foreach (string name in new[] { null, string.Empty, @"p", @"?", @"precursor", @"a2", @"z3", @"y", @"y0", @"y7-", @"y7-junk", @"b3x",
                         @"y7-NaN", @"y7-nan", @"y7-Infinity", @"b3--Infinity" })
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
                (1, @"y3-NH3", 1),          // a lossy alternative for the same peak loses
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

            AssertStackedModificationsAnnotate();
            AssertNonFiniteMzIsRejected();
            AssertRejectionsAreCountedByCause();
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
            }, stats);
            Assert.AreEqual(3, stats.NRejectedRange);
            Assert.AreEqual(1, stats.NRejectedName);
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

        /// <summary>A peak whose m/z is not a number matches no annotation.</summary>
        private static void AssertNonFiniteMzIsRejected()
        {
            var cysteine = new[] { new Modification { Position = CYSTEINE_POSITION, MassDelta = CARBAMIDOMETHYL } };
            var stats = ApplyOne(SEQUENCE, cysteine, double.NaN, @"y3", out var annotation);
            Assert.AreEqual(0, stats.NPeaksAnnotated);
            Assert.AreEqual(1, stats.NRejectedMz);
            Assert.AreEqual(IonType.Unknown, annotation.IonType);
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

        private static void AssertAnnotation(FragmentAnnotation annotation, IonType ionType, byte ordinal, byte charge, NeutralLossCode loss)
        {
            Assert.AreEqual(ionType, annotation.IonType);
            Assert.AreEqual(ordinal, annotation.Ordinal);
            Assert.AreEqual(charge, annotation.Charge);
            Assert.AreEqual(loss, annotation.NeutralLoss);
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
    }
}
