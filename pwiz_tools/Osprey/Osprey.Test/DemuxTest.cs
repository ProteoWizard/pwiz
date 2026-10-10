/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * The Eclipse staggered-DIA fixture (Data/Demux) was made by Michael MacCoss for
 * ProteoWizard/pwiz PR #4710, and the cache checks follow that PR's DemuxTest.
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
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mzml;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// <c>--demux auto</c>: Osprey reading a multiplexed run through ProteoWizard's own
    /// demultiplexer (<see cref="SpectrumListDemux"/>), on a slice of a real staggered-window
    /// Orbitrap Eclipse acquisition (Data/Demux/README.md), and leaving ordinary DIA alone.
    /// </summary>
    [TestClass]
    public class DemuxTest
    {
        // The real-data fixture (Data/Demux/README.md).
        private const string FIXTURE_FOLDER = @"pwiz_tools/Osprey/Osprey.Test/Data/Demux";
        private const string FIXTURE_STAGGERED = @"eclipse-ev13-staggered-slice.mzML";
        private const string FIXTURE_MSCONVERT = @"eclipse-ev13-msconvert-demux-slice.mzML";

        // The slice: 204 MS2 spectra in cycles of 8 staggered 12 Th windows, each window covering
        // 2 of the 9 6 Th demux windows, so demultiplexing gives 2 x 204 = 408 MS2 spectra.
        private const int SLICE_MS2_COUNT = 204;
        private const int SLICE_WINDOWS = 8;
        private const int SLICE_OVERLAPS = 2;
        private const int SLICE_DEMUX_WINDOWS = 9;
        private const int SLICE_DEMUX_MS2_COUNT = 408;
        private const double DEMUX_WINDOW_WIDTH = 6.0;

        // Ordinary DIA subsets that SubsetPipelineTest searches.
        private const string STELLAR_ZIP = @"StellarSubset.zip";
        private const string STELLAR_MZML = @"Ste-2024-12-02_HeLa_4mz_sDIA_400-900_20.mzML";
        private const string ASTRAL_ZIP = @"AstralSubset.zip";
        private const string ASTRAL_MZML = @"Ast-2024-12-05_HeLa_3mzDIA_6mIIT_400-900_49.mzML";

        /// <summary>
        /// Detection and reading: the staggered scheme of the Eclipse slice, its demultiplexed
        /// read against ProteoWizard's demultiplexer applied directly and against msconvert's
        /// (C++) output, the same read without demultiplexing, and ordinary DIA left alone.
        /// </summary>
        [TestMethod]
        public void TestPwizDemuxEclipseFixture()
        {
            string folder = Path.Combine(IOTest.FindPwizRoot(), FIXTURE_FOLDER);
            string staggeredPath = Path.Combine(folder, FIXTURE_STAGGERED);
            string msconvertPath = Path.Combine(folder, FIXTURE_MSCONVERT);

            var scheme = CheckDetectedScheme(staggeredPath);
            // msconvert's output is 9 abutting 6 Th windows: demultiplexed already, so nothing
            // more to demultiplex.
            Assert.IsNull(SpectrumFileReader.DetectDemultiplexScheme(msconvertPath));

            var asAcquired = CheckReadAsAcquired(staggeredPath, scheme);
            var demuxParams = DemuxCache.CreateParams(AutoConfig(4));
            Assert.IsNotNull(demuxParams);
            var demuxed = SpectrumFileReader.LoadAllSpectra(staggeredPath, demuxParams);
            Assert.AreEqual(scheme, demuxed.DemultiplexScheme);
            Assert.IsNull(demuxed.MultiplexedSchemeNotDemultiplexed);
            Assert.AreEqual(SLICE_DEMUX_MS2_COUNT, demuxed.Ms2Spectra.Count);
            Assert.AreEqual(asAcquired.Ms1Spectra.Count, demuxed.Ms1Spectra.Count);
            Assert.AreEqual(SLICE_DEMUX_WINDOWS, DistinctWindows(demuxed.Ms2Spectra).Count);
            foreach (var spectrum in demuxed.Ms2Spectra)
                Assert.AreEqual(DEMUX_WINDOW_WIDTH, spectrum.IsolationWindow.UpperBound - spectrum.IsolationWindow.LowerBound, 0.01);

            // The output does not depend on the threads that solved it.
            var serial = SpectrumFileReader.LoadAllSpectra(staggeredPath, new SpectrumListDemux.Params
            {
                Optimization = demuxParams.Optimization,
                MassError = demuxParams.MassError,
                SolveThreads = 1,
            });
            AssertSpectraIdentical(serial.Ms2Spectra, demuxed.Ms2Spectra);

            CheckAgainstPwizDemux(staggeredPath, demuxParams, demuxed.Ms2Spectra);
            CheckAgainstMsconvert(msconvertPath, demuxed.Ms2Spectra);

            // Ordinary DIA, real data: never demultiplexed, never reported as multiplexed.
            CheckNotMultiplexed(STELLAR_ZIP, STELLAR_MZML);
            CheckNotMultiplexed(ASTRAL_ZIP, ASTRAL_MZML);
        }

        /// <summary>
        /// The demultiplexed cache through the entry point Stages 1-4 and <c>--task SpectraCache</c>
        /// use: demux off writes the <c>.spectra.bin</c> and warns; demux auto then ignores that
        /// cache, writes <c>.demux.spectra.bin</c> with every demux window, reuses it, rescores from
        /// it, refuses to rescore from the <c>.spectra.bin</c> alone, and rebuilds it when its
        /// settings descriptor changes. Also the task validity-key suffix.
        /// </summary>
        [TestMethod]
        public void TestPwizDemuxCache()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"OspreyPwizDemuxCache" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                string source = Path.Combine(dir, FIXTURE_STAGGERED);
                File.Copy(Path.Combine(IOTest.FindPwizRoot(), FIXTURE_FOLDER, FIXTURE_STAGGERED), source);
                string plainCache = SpectraCache.GetCachePath(source);
                string demuxCache = SpectraCache.GetDemuxCachePath(source);
                string fileName = Path.GetFileName(source);
                Assert.AreNotEqual(plainCache, demuxCache);

                // Demux off: searched as acquired, from the .spectra.bin, with a warning.
                var offConfig = new OspreyConfig { DemuxMode = DemuxMode.off, NThreads = 2 };
                var offLog = new TestLog();
                var off = offLog.Context(offConfig);
                var index = ScoringTaskShared.EnsureSpectraCache(source, false, out _, off);
                Assert.AreEqual(plainCache, index.CachePath);
                Assert.AreEqual(SLICE_MS2_COUNT, index.Ms2Count);
                Assert.AreEqual(SLICE_WINDOWS, index.IsolationWindows.Count);
                Assert.IsTrue(File.Exists(plainCache));
                Assert.IsFalse(File.Exists(demuxCache));
                CollectionAssert.Contains(offLog.Warnings, string.Format(
                    OspreyTasksResources.ScoringTaskShared_EnsureSpectraCache__0__was_acquired_with_overlapping_isolation_windows_and_is_searched_as_acquired,
                    fileName, OspreyArgNames.Text(OspreyArgNames.DEMUX)));
                Assert.AreEqual(plainCache, ScoringTaskShared.LoadSpectraForRescore(source, fileName, fileName, false, off).CachePath);
                Assert.AreEqual(SpectraCacheRejection.Absent, DemuxCache.Check(source, offConfig));
                Assert.AreEqual(string.Empty, DemuxCache.ValidityKeySuffix(offConfig));

                // Demux auto, with the plain cache of the multiplexed run present: that cache is
                // not what this search reads, so the run is read again, demultiplexed.
                var autoConfig = AutoConfig(2);
                var autoLog = new TestLog();
                var auto = autoLog.Context(autoConfig);
                index = ScoringTaskShared.EnsureSpectraCache(source, false, out _, auto);
                Assert.AreEqual(demuxCache, index.CachePath);
                Assert.AreEqual(SLICE_DEMUX_MS2_COUNT, index.Ms2Count);
                // Every demux window, including any that first appears after others repeat.
                Assert.AreEqual(SLICE_DEMUX_WINDOWS, index.IsolationWindows.Count);
                Assert.IsTrue(File.Exists(demuxCache));
                Assert.AreEqual(0, autoLog.Warnings.Count);
                Assert.AreEqual(SpectraCacheRejection.None, DemuxCache.Check(source, autoConfig));

                // A second call reuses the demultiplexed cache rather than demultiplexing again.
                var stamp = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(demuxCache, stamp);
                index = ScoringTaskShared.EnsureSpectraCache(source, false, out _, auto);
                Assert.AreEqual(demuxCache, index.CachePath);
                Assert.AreEqual(SLICE_DEMUX_MS2_COUNT, index.Ms2Count);
                Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(demuxCache));
                CollectionAssert.Contains(autoLog.Infos, string.Format(
                    OspreyTasksResources.DemuxCache_TryOpen_Streaming_demultiplexed_spectra_from_cache___0_, demuxCache));

                // Rescoring reads the same demultiplexed cache.
                index = ScoringTaskShared.LoadSpectraForRescore(source, fileName, fileName, true, auto);
                Assert.AreEqual(demuxCache, index.CachePath);
                Assert.AreEqual(SLICE_DEMUX_MS2_COUNT, index.Ms2Count);

                // The validity key carries the descriptor, which does not depend on threads.
                string descriptor = DemuxCache.Descriptor(DemuxCache.CreateParams(autoConfig));
                StringAssert.Contains(DemuxCache.ValidityKeySuffix(autoConfig), descriptor);
                Assert.AreEqual(DemuxCache.ValidityKeySuffix(autoConfig), DemuxCache.ValidityKeySuffix(AutoConfig(8)));

                // A demultiplexed cache written with other settings is rebuilt.
                var stale = SpectraCache.LoadSpectraCache(demuxCache, source, descriptor);
                Assert.IsNotNull(stale);
                SpectraCache.SaveSpectraCache(demuxCache, stale.Ms2Spectra, stale.Ms1Spectra, source, @"pwiz-demux/0;stale");
                Assert.AreEqual(SpectraCacheRejection.DemuxSettingsChanged, DemuxCache.Check(source, autoConfig));
                index = ScoringTaskShared.EnsureSpectraCache(source, false, out _, auto);
                Assert.AreEqual(demuxCache, index.CachePath);
                Assert.AreEqual(SLICE_DEMUX_MS2_COUNT, index.Ms2Count);
                Assert.AreEqual(SpectraCacheRejection.None, DemuxCache.Check(source, autoConfig));

                // Rescoring with only the .spectra.bin of a multiplexed run is an error: the
                // earlier stages searched the demultiplexed cache.
                File.Delete(demuxCache);
                Assert.IsTrue(File.Exists(plainCache));
                Assert.ThrowsException<SpectraCacheException>(() =>
                    ScoringTaskShared.LoadSpectraForRescore(source, fileName, fileName, false, auto));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static SpectrumListDemux.Scheme CheckDetectedScheme(string staggeredPath)
        {
            var scheme = SpectrumFileReader.DetectDemultiplexScheme(staggeredPath);
            Assert.IsNotNull(scheme);
            Assert.AreEqual(SLICE_WINDOWS, scheme.SpectraPerCycle);
            Assert.AreEqual(1, scheme.PrecursorsPerSpectrum);
            Assert.AreEqual(SLICE_OVERLAPS, scheme.OverlapsPerSpectrum);
            Assert.AreEqual(SLICE_DEMUX_WINDOWS, scheme.DemuxWindows);
            Assert.AreEqual(SpectrumListDemux.Optimization.OverlapOnly, scheme.Optimization);
            return scheme;
        }

        private static SpectrumFileResult CheckReadAsAcquired(string staggeredPath, SpectrumListDemux.Scheme scheme)
        {
            var asAcquired = SpectrumFileReader.LoadAllSpectra(staggeredPath);
            Assert.IsNull(asAcquired.DemultiplexScheme);
            Assert.AreEqual(scheme, asAcquired.MultiplexedSchemeNotDemultiplexed);
            Assert.AreEqual(SLICE_MS2_COUNT, asAcquired.Ms2Spectra.Count);
            Assert.AreEqual(SLICE_WINDOWS, DistinctWindows(asAcquired.Ms2Spectra).Count);
            return asAcquired;
        }

        /// <summary>
        /// The demultiplexed read is exactly ProteoWizard's demultiplexer applied to the mzML
        /// directly, as msconvert's demultiplex filter applies it.
        /// </summary>
        private static void CheckAgainstPwizDemux(string path, SpectrumListDemux.Params demuxParams,
            IReadOnlyList<Spectrum> actual)
        {
            using var stream = File.OpenRead(path);
            var msData = new MzmlReader().Read(stream);
            var spectrumList = msData.Run.SpectrumList;
            Assert.IsNotNull(spectrumList);
            using var demux = new SpectrumListDemux(spectrumList, demuxParams);
            int ms2 = 0;
            for (int i = 0; i < demux.Count; i++)
            {
                var expected = demux.GetSpectrum(i, true);
                if (expected.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) != 2)
                    continue;
                Assert.IsTrue(ms2 < actual.Count);
                var spectrum = actual[ms2++];
                var window = expected.Precursors[0].IsolationWindow;
                double center = window.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>();
                Assert.AreEqual(center, spectrum.IsolationWindow.Center, 0.0);
                Assert.AreEqual(window.CvParam(CVID.MS_isolation_window_lower_offset).ValueAs<double>(),
                    spectrum.IsolationWindow.LowerOffset, 0.0);
                Assert.AreEqual(window.CvParam(CVID.MS_isolation_window_upper_offset).ValueAs<double>(),
                    spectrum.IsolationWindow.UpperOffset, 0.0);
                var mzs = expected.GetMZArray()?.Data.ToArray() ?? Array.Empty<double>();
                var intensities = expected.GetIntensityArray()?.Data.Select(v => (float)v).ToArray() ?? Array.Empty<float>();
                CollectionAssert.AreEqual(mzs, spectrum.Mzs);
                CollectionAssert.AreEqual(intensities, spectrum.Intensities);
            }
            Assert.AreEqual(actual.Count, ms2);
        }

        /// <summary>
        /// Against msconvert's (C++) demultiplexing of the same slice: the same spectra in the
        /// same order, with the same isolation windows and retention times. Peaks are not compared:
        /// the C# demultiplexer searches each bin's full tolerance window where C++ searches only
        /// part of it (pwiz #4814), and its parity with C++ otherwise is tested in pwiz/analysis.
        /// </summary>
        private static void CheckAgainstMsconvert(string msconvertPath, IReadOnlyList<Spectrum> actual)
        {
            var reference = SpectrumFileReader.LoadAllSpectra(msconvertPath).Ms2Spectra;
            Assert.AreEqual(SLICE_DEMUX_MS2_COUNT, reference.Count);
            Assert.AreEqual(reference.Count, actual.Count);
            for (int i = 0; i < reference.Count; i++)
            {
                Assert.AreEqual(reference[i].IsolationWindow.LowerBound, actual[i].IsolationWindow.LowerBound, 1e-6);
                Assert.AreEqual(reference[i].IsolationWindow.UpperBound, actual[i].IsolationWindow.UpperBound, 1e-6);
                Assert.AreEqual(reference[i].RetentionTime, actual[i].RetentionTime, 1e-6);
            }
            CollectionAssert.AreEqual(DistinctWindows(reference), DistinctWindows(actual));
        }

        private static void AssertSpectraIdentical(IReadOnlyList<Spectrum> expected, IReadOnlyList<Spectrum> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].ScanNumber, actual[i].ScanNumber);
                Assert.AreEqual(expected[i].RetentionTime, actual[i].RetentionTime, 0.0);
                Assert.AreEqual(expected[i].IsolationWindow.Center, actual[i].IsolationWindow.Center, 0.0);
                CollectionAssert.AreEqual(expected[i].Mzs, actual[i].Mzs);
                CollectionAssert.AreEqual(expected[i].Intensities, actual[i].Intensities);
            }
        }

        /// <summary>The distinct isolation windows, as (lower, upper) bounds rounded to 0.01 m/z,
        /// in ascending order.</summary>
        private static List<(double Lower, double Upper)> DistinctWindows(IEnumerable<Spectrum> spectra)
        {
            return spectra.Select(s => (Math.Round(s.IsolationWindow.LowerBound, 2), Math.Round(s.IsolationWindow.UpperBound, 2)))
                .Distinct().OrderBy(w => w.Item1).ToList();
        }

        private static OspreyConfig AutoConfig(int threads)
        {
            return new OspreyConfig { DemuxMode = DemuxMode.auto, NThreads = threads };
        }

        /// <summary>
        /// Ordinary DIA from a <c>TestData</c> subset zip is not detected as multiplexed.
        /// </summary>
        private static void CheckNotMultiplexed(string zipName, string entryName)
        {
            string dir = Path.Combine(Path.GetTempPath(), @"OspreyDemuxSubset" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, entryName);
                using (var zip = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, @"TestData", zipName)))
                {
                    var entry = zip.GetEntry(entryName);
                    Assert.IsNotNull(entry, entryName);
                    entry.ExtractToFile(path);
                }
                Assert.IsNull(SpectrumFileReader.DetectDemultiplexScheme(path), entryName);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>Collects what a <see cref="PipelineContext"/> logs.</summary>
        private class TestLog
        {
            public List<string> Infos { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();

            public PipelineContext Context(OspreyConfig config)
            {
                return new PipelineContext(config, new OspreyTask[0], Infos.Add, Warnings.Add, null);
            }
        }
    }
}
