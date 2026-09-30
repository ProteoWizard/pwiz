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
    /// Blib libraries as search input: every peak typed from m/z within the search's fragment
    /// tolerance (<see cref="FragmentTyping"/>), whatever the blib's own
    /// <c>RefSpectraPeakAnnotations</c> table holds - it is not read - and the decoys generated
    /// from the typed peaks; and the libraries Osprey refuses rather than search wrongly.
    /// </summary>
    [TestClass]
    public class BlibLibraryInputTest
    {
        private const string SEQUENCE = @"PEPCTIDEK";
        private const string MOD_SEQUENCE = @"PEPC[+57.021464]TIDEK";
        private const int CYSTEINE_POSITION = 3;
        private const double CARBAMIDOMETHYL = 57.021464;

        /// <summary>The fragment tolerance these tests search with: an HRAM search's 10 ppm.</summary>
        internal static readonly FragmentToleranceConfig TOLERANCE = FragmentToleranceConfig.Hram(10);

        /// <summary>
        /// A blib's peaks are typed from m/z: primary b and y ions at charge 1 or 2 are typed; a
        /// neutral-loss peak and a peak no ion reaches stay Unknown. The blib's
        /// <c>RefSpectraPeakAnnotations</c> table is not read: the same blib with rows naming
        /// other ions, or with a table missing its columns, types identically. Decoys generated
        /// from the typed peaks get their own m/z.
        /// </summary>
        [TestMethod]
        public void TestBlibLoaderTypesPeaksFromMz()
        {
            var cysteine = new[] { new Modification { Position = CYSTEINE_POSITION, MassDelta = CARBAMIDOMETHYL } };
            var modMasses = PeptideFragmentMass.ModMassesByPosition(cysteine);
            double b3 = Mz(IonType.B, 3, 1, modMasses, null);
            double y3 = Mz(IonType.Y, 3, 1, modMasses, null);
            double y5Doubly = Mz(IonType.Y, 5, 2, modMasses, null);
            double y4Water = Mz(IonType.Y, 4, 1, modMasses, NeutralLoss.H2OMass);
            double b5 = Mz(IonType.B, 5, 1, modMasses, null);
            double[] peaks = { b3, y3, y5Doubly, y4Water, b5, 700.0 };

            string plain = CreateBlib(peaks, new (int, string, int)[0]);
            try
            {
                var log = new List<string>();
                var entry = new BlibLoader(TOLERANCE).Load(plain, log.Add).Single();
                AssertTypedAsExpected(entry);
                CollectionAssert.Contains(log, string.Format(
                    OspreyIOResources.BlibLoader_Load_Typed__0_N0__of__1_N0__library_peaks_as_b_or_y_ions_within__2___3_,
                    4, peaks.Length, TOLERANCE.Tolerance, TOLERANCE.Unit.GetLocalizedString()));

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
                TryDeleteFile(plain);
            }

            // Rows naming other ions than the typing gives change nothing.
            string annotated = CreateBlib(peaks, new[] { (0, @"y2", 1), (1, @"b3", 1), (4, @"y5", 1), (5, @"y6", 1) });
            try
            {
                AssertTypedAsExpected(new BlibLoader(TOLERANCE).Load(annotated).Single());
                // Nor does a table the reader could not have read.
                ExecuteSql(annotated, @"DROP TABLE RefSpectraPeakAnnotations",
                    @"CREATE TABLE RefSpectraPeakAnnotations (id INTEGER PRIMARY KEY, RefSpectraID INTEGER, peakIndex INTEGER)",
                    @"INSERT INTO RefSpectraPeakAnnotations (RefSpectraID, peakIndex) VALUES (1, 0)");
                AssertTypedAsExpected(new BlibLoader(TOLERANCE).Load(annotated).Single());
            }
            finally
            {
                TryDeleteFile(annotated);
            }
        }

        /// <summary>
        /// Blib libraries Osprey refuses rather than search wrongly. Modifications are matched as
        /// Skyline matches them, at the precision their text prints: a one-decimal SILAC label
        /// gets its exact mass, while a one-decimal mass no known modification matches, an
        /// unknown UniMod id and other text are listed - each once, with the spectra carrying it -
        /// and the library is refused. And a blib whose protein accessions mark decoys is refused
        /// unless <c>--decoys-in-library</c> is given, where a TSV's Decoy column is not needed.
        /// </summary>
        [TestMethod]
        public void TestBlibLibraryRefusals()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"BlibLibraryRefusals_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                double[] peaks = { 300.0, 400.0, 500.0 };
                string labeled = Path.Combine(dir, @"labeled.blib");
                WriteBlib(labeled, peaks, (@"PEPC[+57.0]TIDEK[+8.0]", @"P1"), (@"PEPCTIDEK", @"P1"));
                var entries = new BlibLoader(TOLERANCE).Load(labeled);
                var labels = entries.First().Modifications.OrderBy(m => m.Position).ToArray();
                Assert.AreEqual(CARBAMIDOMETHYL, labels[0].MassDelta);
                Assert.AreEqual(8.014199, labels[1].MassDelta);
                Assert.AreEqual(259, labels[1].UnimodId);

                string unidentified = Path.Combine(dir, @"unidentified.blib");
                WriteBlib(unidentified, peaks, (@"PEPC[+99.9]TIDEK", @"P1"), (@"PEPTIDEK(UniMod:99999)", @"P1"),
                    (@"PEPCTIDEK", @"P1"), (@"PEPTIDEC[+99.9]", @"P1"), (@"PEPTIDEK[Junk]", @"P1"));
                var error = Assert.ThrowsException<InvalidDataException>(() => new BlibLoader(TOLERANCE).Load(unidentified));
                string[] lines = error.Message.Split('\n').Select(l => l.Trim()).ToArray();
                Assert.AreEqual(string.Format(
                    OspreyIOResources.BlibLoader_ToException__0__library_spectra_have_modifications_Osprey_cannot_identify,
                    4, UniMod.MAX_PRECISION_TO_MATCH), lines[0]);
                CollectionAssert.AreEqual(new[]
                {
                    string.Format(OspreyIOResources.BlibLoader_ToException__0__in__1__spectra__first__2_, @"[+99.9]", 2, @"PEPC[+99.9]TIDEK (id 1)"),
                    string.Format(OspreyIOResources.BlibLoader_ToException__0__in__1__spectra__first__2_, @"(UniMod:99999)", 1, @"PEPTIDEK(UniMod:99999) (id 2)"),
                    string.Format(OspreyIOResources.BlibLoader_ToException__0__in__1__spectra__first__2_, @"[Junk]", 1, @"PEPTIDEK[Junk] (id 5)"),
                }, lines.Skip(1).ToArray());

                string withDecoys = Path.Combine(dir, @"decoys.blib");
                WriteBlib(withDecoys, peaks, (@"PEPCTIDEK", @"P1"), (@"KEDITCPEP", @"DECOY_P1"));
                var config = new OspreyConfig
                {
                    LibrarySource = new LibrarySource(LibraryFormat.Blib, withDecoys),
                    FragmentTolerance = TOLERANCE
                };
                var refusal = Assert.ThrowsException<InvalidDataException>(() => LibraryLoader.Load(config, null, null));
                StringAssert.Contains(refusal.Message, OspreyArgNames.Text(OspreyArgNames.DECOYS_IN_LIBRARY));
                StringAssert.Contains(refusal.Message, @"KEDITCPEP");
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                    // A test's temp directory; a lingering handle must not fail the test.
                }
            }
        }

        /// <summary>
        /// A library entry written by <see cref="LibraryBlibWriter"/> reads back as the same entry:
        /// peaks sorted by m/z and typed from m/z, stacked N-terminal modifications summed onto
        /// the first residue, a known modification at its exact mass and an unknown one to four
        /// decimals, proteins and retention time. No <c>RefSpectraPeakAnnotations</c> row is
        /// written: the table stays empty, as BiblioSpec leaves it, and the reader types the
        /// primary b and y ions back from m/z; a loss, an a ion and an untyped peak read back
        /// Unknown.
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
            var entry = new LibraryEntry(7, sequence, @"_(UniMod:1)M(UniMod:35)PEPC(UniMod:4)TIDEK[+14.0157]_", 2, 624.28, 12.5)
            {
                Modifications = modifications,
                Fragments = fragments,
                ProteinIds = new[] { @"P12345", @"Q67890" }
            };

            string path = Path.GetTempFileName();
            try
            {
                Assert.AreEqual(1, LibraryBlibWriter.Write(path, new[] { entry }, @"library.tsv"));
                Assert.AreEqual(0, BlibComparer.CountWhere(path, @"RefSpectraPeakAnnotations", @"1"));
                // The library retention time, where Skyline reads library retention times from, with
                // no peak boundaries.
                Assert.AreEqual(1, BlibComparer.CountWhere(path, @"RetentionTimes",
                    @"retentionTime = 12.5 AND startTime IS NULL AND endTime IS NULL AND bestSpectrum = 1"));
                var read = new BlibLoader(TOLERANCE).Load(path).Single();
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
                    if (FragmentTyping.IsPrimary(expected))
                        AssertAnnotation(actual, expected.IonType, expected.Ordinal, expected.Charge, NeutralLossCode.None);
                    else
                        Assert.AreEqual(IonType.Unknown, actual.IonType, FragmentTypeCheck.FormatIon(expected));
                }

                AssertLibraryTextAndDecoys(path);
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// The text and accessions a library precursor keeps in the blib: a modification its
        /// loader could not resolve keeps the library's own text, so two precursors never share a
        /// key; a mass known only to one decimal keeps that precision, the precision Skyline
        /// matches at; and a decoy flagged only by the library's Decoy column gets the decoy
        /// prefix on its accessions, so <c>--decoys-in-library</c> still finds it.
        /// </summary>
        private static void AssertLibraryTextAndDecoys(string path)
        {
            var fragment = new[] { new LibraryFragment { Mz = 300.0, RelativeIntensity = 1f } };
            var unresolved = new LibraryEntry(1, @"PEPTIDEK", @"_PEPTIDEK(UniMod:259)_", 2, 470.0, 10.0) { Fragments = fragment };
            var plain = new LibraryEntry(2, @"PEPTIDEK", @"_PEPTIDEK_", 2, 466.0, 10.0) { Fragments = fragment };
            var oneDecimal = new LibraryEntry(3, @"LIFAGKQLEDGR", @"LIFAGK[+114.0]QLEDGR", 2, 717.9, 20.0)
            {
                Modifications = new[] { new Modification { Position = 5, MassDelta = 114.0 } },
                Fragments = fragment
            };
            var columnDecoy = new LibraryEntry(4, @"KEDITPEP", @"KEDITPEP", 2, 464.7, 15.0)
            {
                Fragments = fragment, ProteinIds = new[] { @"P12345" }, IsDecoy = true
            };
            var prefixedDecoy = new LibraryEntry(5, @"KEDITPEPR", @"KEDITPEPR", 2, 542.8, 15.0)
            {
                Fragments = fragment, ProteinIds = new[] { @"rev_P12345" }, IsDecoy = true
            };
            LibraryBlibWriter.Write(path, new[] { unresolved, plain, oneDecimal, columnDecoy, prefixedDecoy }, @"library.tsv");
            Assert.AreEqual(1, BlibComparer.CountWhere(path, @"RefSpectra", @"peptideModSeq = 'PEPTIDEK(UniMod:259)'"));
            Assert.AreEqual(1, BlibComparer.CountWhere(path, @"RefSpectra", @"peptideModSeq = 'PEPTIDEK'"));
            Assert.AreEqual(1, BlibComparer.CountWhere(path, @"RefSpectra", @"peptideModSeq = 'LIFAGK[+114.0]QLEDGR'"));
            Assert.AreEqual(1, BlibComparer.CountWhere(path, @"Proteins", @"accession = 'DECOY_P12345'"));
            Assert.AreEqual(1, BlibComparer.CountWhere(path, @"Proteins", @"accession = 'rev_P12345'"));
            Assert.AreEqual(0, BlibComparer.CountWhere(path, @"Proteins", @"accession = 'P12345'"));
        }

        /// <summary>The types <see cref="TestBlibLoaderTypesPeaksFromMz"/>'s six peaks take.</summary>
        private static void AssertTypedAsExpected(LibraryEntry entry)
        {
            var types = entry.Fragments.Select(f => f.Annotation).ToArray();
            AssertAnnotation(types[0], IonType.B, 3, 1, NeutralLossCode.None);
            AssertAnnotation(types[1], IonType.Y, 3, 1, NeutralLossCode.None);
            AssertAnnotation(types[2], IonType.Y, 5, 2, NeutralLossCode.None);
            Assert.AreEqual(IonType.Unknown, types[3].IonType, @"a neutral loss is not typed");
            AssertAnnotation(types[4], IonType.B, 5, 1, NeutralLossCode.None);
            Assert.AreEqual(IonType.Unknown, types[5].IonType, @"no ion reaches 700.0");
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
        /// A blib at <paramref name="path"/> with one spectrum of <paramref name="peaks"/> per
        /// modified sequence, each mapped to its protein.
        /// </summary>
        private static void WriteBlib(string path, double[] peaks, params (string ModSeq, string Protein)[] spectra)
        {
            using (var writer = new BlibWriter(path))
            {
                long fileId = writer.AddSourceFile(@"test.mzML", @"library.blib", 0.01);
                foreach (var (modSeq, protein) in spectra)
                {
                    string sequence = new string(StripBrackets(modSeq).Where(char.IsLetter).ToArray());
                    long refId = writer.AddSpectrum(sequence, modSeq, 525.25, 2, 10.0, 9.0, 11.0,
                        peaks, peaks.Select((_, i) => 100f + i).ToArray(), 0.01, fileId, 1, 0.0);
                    writer.AddProteinMapping(refId, new[] { protein });
                }
                writer.FinalizeDatabase();
            }
            SQLiteConnection.ClearAllPools();
        }

        private static string StripBrackets(string modSeq)
        {
            var sb = new System.Text.StringBuilder();
            int depth = 0;
            foreach (char c in modSeq)
            {
                if (c == '[' || c == '(')
                    depth++;
                else if (c == ']' || c == ')')
                    depth--;
                else if (depth == 0)
                    sb.Append(c);
            }
            return sb.ToString();
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

        private static FragmentAnnotation Annotation(IonType ionType, byte ordinal, byte charge, NeutralLossCode loss, double customLoss)
        {
            return new FragmentAnnotation
            {
                IonType = ionType, Ordinal = ordinal, Charge = charge, NeutralLoss = loss, CustomLossMass = customLoss
            };
        }

        internal static void AssertAnnotation(FragmentAnnotation annotation, IonType ionType, byte ordinal, byte charge, NeutralLossCode loss)
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
    }
}
