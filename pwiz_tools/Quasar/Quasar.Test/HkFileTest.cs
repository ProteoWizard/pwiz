/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using Parquet.Schema;
using pwiz.Quasar.Features;
using pwiz.Quasar.Features.Data;
using pwiz.Quasar.IO;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The .hk file: a run's peptide-like search saved beside the raw file and read back (ported from
    /// skyline-glimpse's HkFileTests, every assertion and expected value kept).
    /// </summary>
    [TestClass]
    public class HkFileTest
    {
        private static readonly DateTime MODIFIED = new DateTime(2026, 2, 11, 20, 32, 0, DateTimeKind.Utc);

        private string _dir;

        [TestInitialize]
        public void CreateFolder()
        {
            _dir = Directory.CreateTempSubdirectory("quasar-hk").FullName;
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            Directory.Delete(_dir, true);
        }

        /// <summary>A real search, DIA windows and the row layout survive a write and a read.</summary>
        [TestMethod]
        public void TestHkFileRoundTrip()
        {
            ARealSearchSurvivesAWriteAndRead();
            DiaWindowsAndScansWithoutEnvelopesSurvive();
            ARowIsOneEnvelopeWithItsPeaks();
        }

        /// <summary>A file made from another raw file, other settings or another run, or a damaged one, is ignored.</summary>
        [TestMethod]
        public void TestHkFileIgnoresOtherFiles()
        {
            AFileMadeFromSomethingElseIsIgnored();
        }

        /// <summary>Where the file goes, and what it is stamped with.</summary>
        [TestMethod]
        public void TestHkFilePathAndStamp()
        {
            TheFileSitsBesideTheRawFile();
            TheStampOfAFileAndOfAFolder();
        }

        private static (PeptideLikeSignal Signal, ScanHeader[] Headers) Search(IReadOnlyList<SpectrumData> spectra, IEnvelopeFinder finder)
        {
            using (var observer = new PeptideLikeObserver(finder, workers: 3))
            {
                var headers = ScanPass.Run(spectra, observer);
                Assert.IsTrue(observer.Completion.Wait(TimeSpan.FromMinutes(2)));
                return (observer.Completion.Result, headers);
            }
        }

        /// <summary>
        /// The same signal: per-scan totals (both MS1 charge rules), coverage, features, series and envelopes. A
        /// signal read back has no peaks (the file keeps each scan's totals instead).
        /// </summary>
        private static void AssertSame(PeptideLikeSignal expected, PeptideLikeSignal actual, int scans)
        {
            FeatureAssert.AreSequenceEqual(expected.IntensityByScan(), actual.IntensityByScan());
            FeatureAssert.AreSequenceEqual(expected.IntensityByScan(2), actual.IntensityByScan(2));
            for (var i = 0; i < scans; i++)
            {
                Assert.AreEqual(expected.Covers(i), actual.Covers(i));
                FeatureAssert.IsEmpty(actual.PeptideLikePeaks(i));
            }
            FeatureAssert.AreJaggedEqual(expected.Series, actual.Series);
            FeatureAssert.AreSequenceEqual(expected.Features, actual.Features);
            Assert.AreEqual(expected.IsDda, actual.IsDda);
            Assert.AreEqual(expected.Counts.Envelopes, actual.Counts.Envelopes);
            Assert.AreEqual(0, actual.Counts.PeakRefs);
            Assert.IsTrue(expected.HasPeaks);
            Assert.IsFalse(actual.HasPeaks);
            Assert.ThrowsException<InvalidOperationException>(() => actual.IntensityByScan(3));   // only 1+ and 2+ are kept
        }

        private void ARealSearchSurvivesAWriteAndRead()
        {
            var (signal, headers) = Search(SmallRaw.Spectra, new AnalyzerEnvelopeFinder(new PwizEnvelopeModel()));
            Assert.IsTrue(signal.Features.Count > 0);
            var stamp = new HkFile.Stamp(123, MODIFIED, headers.Length, "settings A");
            var path = Path.Combine(_dir, "small.hk");
            HkFile.Write(path, signal, stamp);
            Assert.IsFalse(File.Exists(path + ".tmp"));

            var read = HkFile.TryRead(path, stamp, headers);
            Assert.IsNotNull(read);
            AssertSame(signal, read, headers.Length);
        }

        private void DiaWindowsAndScansWithoutEnvelopesSurvive()
        {
            // Two windows over three cycles; window B has an envelope once, so most of its scans have none.
            var run = new FakeRun();
            for (var c = 0; c < 3; c++)
            {
                var rt = 1 + c * 0.1;
                run.Ms1(rt, new[] { 500.0, 500.5, 501.0 }, new[] { 100.0, 50, 25 }, 10f, MassAnalyzer.orbitrap);
                run.Ms2(rt + 0.01, 400, 410, new[] { 300.0, 301.0 }, new[] { 80.0, 20 }, 10f, MassAnalyzer.astral);
                run.Ms2(rt + 0.02, 410, 420, c == 1 ? new[] { 600.0 } : new[] { 650.0 }, new[] { 9.0 }, 10f, MassAnalyzer.astral);
            }
            var (signal, headers) = Search(run.Spectra, new PatternEnvelopeFinder((500.0, 2, 3), (300.0, 1, 2), (600.0, 1, 1)));
            Assert.AreEqual(3, signal.Series.Count);
            var stamp = new HkFile.Stamp(0, MODIFIED, headers.Length, "s");
            var path = Path.Combine(_dir, "dia.hk");
            HkFile.Write(path, signal, stamp);
            HkFile.Write(path, signal, stamp);                       // over an existing file
            AssertSame(signal, HkFile.TryRead(path, stamp, headers), headers.Length);
        }

        private void AFileMadeFromSomethingElseIsIgnored()
        {
            var run = new FakeRun();
            for (var s = 0; s < 3; s++)
                run.Ms1(1 + s * 0.1, new[] { 500.0, 500.5, 501.0 }, new[] { 100.0, 50, 25 });
            var (signal, headers) = Search(run.Spectra, new PatternEnvelopeFinder((500.0, 2, 3)));
            var stamp = new HkFile.Stamp(1000, MODIFIED, headers.Length, "settings A");
            var path = Path.Combine(_dir, "run.hk");
            HkFile.Write(path, signal, stamp);

            Assert.IsNotNull(HkFile.TryRead(path, stamp, headers));
            Assert.IsNull(HkFile.TryRead(path, stamp with { RawSize = 1001 }, headers));                 // the raw file changed
            Assert.IsNull(HkFile.TryRead(path, stamp with { RawModifiedUtc = MODIFIED.AddSeconds(1) }, headers));
            Assert.IsNull(HkFile.TryRead(path, stamp with { Settings = "settings B" }, headers));        // other search settings
            Assert.IsNull(HkFile.TryRead(path, stamp with { SpectrumCount = 4 }, headers));
            Assert.IsNull(HkFile.TryRead(path, stamp, headers.Take(2).ToArray()));                       // another run
            Assert.IsNull(HkFile.TryRead(Path.Combine(_dir, "none.hk"), stamp, headers));
            File.WriteAllText(Path.Combine(_dir, "bad.hk"), "not parquet");
            Assert.IsNull(HkFile.TryRead(Path.Combine(_dir, "bad.hk"), stamp, headers));
            Assert.IsFalse(HkFile.Matches(Path.Combine(_dir, "bad.hk"), stamp));
            // Parquet's magic at both ends around garbage: whatever the reader throws, the file is just unusable.
            var fake = new byte[64];
            "PAR1"u8.CopyTo(fake);
            "PAR1"u8.CopyTo(fake.AsSpan(60));
            BitConverter.GetBytes(40).CopyTo(fake, 56);
            new Random(1).NextBytes(fake.AsSpan(4, 52));
            File.WriteAllBytes(Path.Combine(_dir, "garbage.hk"), fake);
            Assert.IsFalse(HkFile.Matches(Path.Combine(_dir, "garbage.hk"), stamp));
            Assert.IsNull(HkFile.TryRead(Path.Combine(_dir, "garbage.hk"), stamp, headers));
            // A truncated real file.
            var whole = File.ReadAllBytes(path);
            File.WriteAllBytes(Path.Combine(_dir, "cut.hk"), whole[..(whole.Length / 2)]);
            Assert.IsFalse(HkFile.Matches(Path.Combine(_dir, "cut.hk"), stamp));
            Assert.IsNull(HkFile.TryRead(Path.Combine(_dir, "cut.hk"), stamp, headers));
        }

        private void ARowIsOneEnvelopeWithItsPeaks()
        {
            var run = new FakeRun();
            for (var s = 0; s < 3; s++)
                run.Ms1(1 + s * 0.1, new[] { 400.0, 500.0, 500.5, 501.0 }, new[] { 7.0, 100, 50, 25 });
            var (signal, headers) = Search(run.Spectra, new PatternEnvelopeFinder((500.0, 2, 3)));
            var path = Path.Combine(_dir, "rows.hk");
            HkFile.Write(path, signal, new HkFile.Stamp(1, MODIFIED, headers.Length, "s"));

            // Parquet.Net is asynchronous only; waited on here as HkFile does.
            using (var stream = File.OpenRead(path))
            {
                var reader = ParquetReader.CreateAsync(stream).GetAwaiter().GetResult();
                try
                {
                    FeatureAssert.AreSequenceEqual(
                        new[] { "scan", "ms_level", "rt", "series", "mono_mass", "charge", "intensity", "base_peak_mz", "correlation",
                            "low_mz", "high_mz", "feature", "scan_peptide_like", "scan_peptide_like_2plus" },
                        reader.Schema.GetDataFields().Select(f => f.Name));
                    Assert.AreEqual("2", reader.CustomMetadata["glimpse.hk.format"]);
                    Assert.AreEqual("[[0,1,2]]", reader.CustomMetadata["glimpse.series"]);
                    using (var group = reader.OpenRowGroupReader(0))
                    {
                        Assert.AreEqual(3, group.RowCount);
                        FeatureAssert.AreSequenceEqual(new[] { 0, 1, 2 }, ReadInts(reader, group, "scan"));
                        FeatureAssert.AreSequenceEqual(new[] { 2, 2, 2 }, ReadInts(reader, group, "charge"));
                        FeatureAssert.AreSequenceEqual(new[] { 0, 0, 0 }, ReadInts(reader, group, "feature"));      // one feature over three scans
                        var signal3 = new double[3];
                        group.ReadAsync(Field(reader, "scan_peptide_like"), signal3.AsMemory()).GetAwaiter().GetResult();
                        FeatureAssert.AreSequenceEqual(new[] { 175.0, 175, 175 }, signal3);              // 100 + 50 + 25, not the lone 400 peak
                    }
                }
                finally
                {
                    reader.DisposeAsync().GetAwaiter().GetResult();
                }
            }
        }

        private static int[] ReadInts(ParquetReader reader, ParquetRowGroupReader group, string name)
        {
            var v = new int[3];
            group.ReadAsync(Field(reader, name), v.AsMemory()).GetAwaiter().GetResult();
            return v;
        }

        private static DataField Field(ParquetReader reader, string name) =>
            reader.Schema.GetDataFields().Single(f => f.Name == name);

        private static void TheFileSitsBesideTheRawFile()
        {
            var cases = new[]
            {
                (@"D:\data\run.raw", @"D:\data\run.hk"),
                (@"D:\data\run.RAW", @"D:\data\run.hk"),
                (@"D:\data\run.mzML", @"D:\data\run.hk"),
                (@"D:\data\waters.raw\", @"D:\data\waters.hk"),     // a vendor folder
            };
            foreach (var (raw, hk) in cases)
                Assert.AreEqual(hk, HkFile.PathBeside(raw));
        }

        private void TheStampOfAFileAndOfAFolder()
        {
            var file = Path.Combine(_dir, "run.raw");
            File.WriteAllBytes(file, new byte[42]);
            File.SetLastWriteTimeUtc(file, MODIFIED);
            Assert.AreEqual(new HkFile.Stamp(42, MODIFIED, 7, "x"), HkFile.Stamp.Of(file, 7, "x"));
            var folder = Directory.CreateDirectory(Path.Combine(_dir, "waters.raw")).FullName;
            Directory.SetLastWriteTimeUtc(folder, MODIFIED);
            Assert.AreEqual(new HkFile.Stamp(0, MODIFIED, 7, "x"), HkFile.Stamp.Of(folder, 7, "x"));          // empty: the folder's time
            // A vendor folder: the files inside it, total size and newest time (the folder's own time lags).
            File.WriteAllBytes(Path.Combine(folder, "_FUNC001.DAT"), new byte[100]);
            Directory.CreateDirectory(Path.Combine(folder, "sub"));
            File.WriteAllBytes(Path.Combine(folder, "sub", "_extern.inf"), new byte[20]);
            File.SetLastWriteTimeUtc(Path.Combine(folder, "_FUNC001.DAT"), MODIFIED.AddHours(1));
            File.SetLastWriteTimeUtc(Path.Combine(folder, "sub", "_extern.inf"), MODIFIED.AddHours(2));
            Directory.SetLastWriteTimeUtc(folder, MODIFIED);
            Assert.AreEqual(new HkFile.Stamp(120, MODIFIED.AddHours(2), 7, "x"), HkFile.Stamp.Of(folder, 7, "x"));
        }
    }
}
