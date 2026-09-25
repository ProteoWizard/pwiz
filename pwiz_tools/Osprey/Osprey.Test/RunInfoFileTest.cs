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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// <c>&lt;stem&gt;.run-info.json</c>: it lives beside the run's <c>.spectra.bin</c>, it
    /// round-trips, its histogram keys are invariant and ordinally ordered, and a reader handed
    /// an absent, damaged or future-format file gets null rather than an exception - the file is
    /// descriptive, so a consumer degrades instead of failing on it.
    /// </summary>
    [TestClass]
    public class RunInfoFileTest
    {
        [TestMethod]
        public void TestRunInfoFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_runinfo_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            string savedCache = ArtifactPaths.CacheDir;
            try
            {
                ArtifactPaths.CacheDir = dir;
                string input = Path.Combine(dir, @"raw", @"run7.mzML");
                string path = RunInfoFile.PathFor(input);
                Assert.AreEqual(Path.GetDirectoryName(SpectraCache.GetCachePath(input)), Path.GetDirectoryName(path),
                    @"beside the spectra cache");
                Assert.AreEqual(@"run7.run-info.json", Path.GetFileName(path));

                AssertRoundTrip(path);
                AssertUnreadableIsNull(dir);
                AssertStalenessAgainstTheSpectraCache(dir);
                AssertScanWindowPerIsolationWindow(dir);
            }
            finally
            {
                ArtifactPaths.CacheDir = savedCache;
                Directory.Delete(dir, true);
            }

            Assert.AreEqual(@"30", RunInfo.CollisionEnergyKey(30.0));
            Assert.AreEqual(@"27.5", RunInfo.CollisionEnergyKey(27.5));
            Assert.AreEqual(RunInfo.NONE_KEY, RunInfo.CollisionEnergyKey(null));
        }

        private static void AssertRoundTrip(string path)
        {
            var info = new RunInfo
            {
                SourceFile = @"run7.mzML",
                SourceSize = 1234567,
                SourceMtimeMs = 1769990872952,
                RunId = @"run7",
                RunStartTime = @"2024-12-02T23:17:57.0000000-08:00",
                InstrumentVendor = @"Thermo",
                InstrumentModel = @"Stellar",
                InstrumentSerialNumber = @"QLT-S-10013",
                InstrumentConfigurations = new List<RunInfo.InstrumentConfiguration>
                {
                    new RunInfo.InstrumentConfiguration
                    {
                        Model = @"Stellar", Ionization = @"nanoelectrospray",
                        Analyzer = @"radial ejection linear ion trap", Detector = @"electron multiplier",
                    },
                },
                NMs1 = 779,
                NMs2 = 97375,
                Ms1RtRange = new[] { 0.0001, 23.97 },
                Ms2RtRange = new[] { 0.0007, 24.00 },
                Ms1ScanWindow = new[] { 350.0, 1250.0 },
                Ms2ScanWindow = new[] { 200.0, 1500.0 },
                Ms2IsolationRange = new[] { 400.43, 900.66 },
            };
            info.Ms2Analyzers[@"radial ejection linear ion trap"] = 97375;
            info.DissociationMethods[@"HCD"] = 97000;
            info.DissociationMethods[RunInfo.NONE_KEY] = 375;
            info.CollisionEnergies[RunInfo.CollisionEnergyKey(30.0)] = 90000;
            info.CollisionEnergies[RunInfo.CollisionEnergyKey(27.5)] = 7375;

            RunInfoFile.Save(path, info);
            var read = RunInfoFile.TryLoad(path);
            Assert.IsNotNull(read);
            Assert.AreEqual(RunInfo.CURRENT_FORMAT_VERSION, read.FormatVersion);
            Assert.AreEqual(info.SourceFile, read.SourceFile);
            Assert.AreEqual(info.SourceSize, read.SourceSize);
            Assert.AreEqual(info.SourceMtimeMs, read.SourceMtimeMs);
            Assert.AreEqual(info.RunStartTime, read.RunStartTime);
            Assert.AreEqual(info.InstrumentModel, read.InstrumentModel);
            Assert.AreEqual(info.InstrumentSerialNumber, read.InstrumentSerialNumber);
            Assert.AreEqual(info.InstrumentConfigurations[0].Analyzer, read.InstrumentConfigurations.Single().Analyzer);
            Assert.AreEqual(info.NMs2, read.NMs2);
            CollectionAssert.AreEqual(info.Ms2ScanWindow, read.Ms2ScanWindow);
            CollectionAssert.AreEqual(info.Ms2IsolationRange, read.Ms2IsolationRange);
            CollectionAssert.AreEqual(info.CollisionEnergies.ToList(), read.CollisionEnergies.ToList());
            // Ordinal key order, so the file is byte-stable for one input.
            CollectionAssert.AreEqual(new[] { @"HCD", RunInfo.NONE_KEY }, read.DissociationMethods.Keys.ToArray());
            CollectionAssert.AreEqual(new[] { @"27.5", @"30" }, read.CollisionEnergies.Keys.ToArray());
            Assert.AreEqual(File.ReadAllText(path), Resave(path, read), @"the written form is stable across a round trip");
            // One source yields the same bytes on every machine: '\n' newlines whatever the
            // platform's, and a start time in UTC whatever the machine's time zone.
            Assert.IsFalse(File.ReadAllText(path).Contains('\r'), @"the file is written with '\n' newlines");
            var local = new DateTime(2024, 12, 2, 23, 17, 57, DateTimeKind.Utc).ToLocalTime();
            Assert.AreEqual(@"2024-12-02T23:17:57.0000000Z", RunInfoCollector.FormatRunStartTime(local));
            Assert.AreEqual(@"2024-12-02T23:17:57.0000000", RunInfoCollector.FormatRunStartTime(
                new DateTime(2024, 12, 2, 23, 17, 57, DateTimeKind.Unspecified)), @"a time with no zone stays as the file wrote it");
            Assert.IsNull(RunInfoCollector.FormatRunStartTime(null));
        }

        private static void AssertUnreadableIsNull(string dir)
        {
            Assert.IsNull(RunInfoFile.TryLoad(Path.Combine(dir, @"absent.run-info.json")));
            Assert.IsNull(RunInfoFile.TryLoad(null));
            string garbage = Path.Combine(dir, @"garbage.run-info.json");
            File.WriteAllText(garbage, @"{ not json");
            Assert.IsNull(RunInfoFile.TryLoad(garbage));
            string future = Path.Combine(dir, @"future.run-info.json");
            File.WriteAllText(future, @"{ ""format_version"": 99 }");
            Assert.IsNull(RunInfoFile.TryLoad(future), @"a format this build does not know is not read");
            string truncatedRange = Path.Combine(dir, @"truncated.run-info.json");
            File.WriteAllText(truncatedRange, @"{ ""format_version"": 2, ""ms2_scan_window"": [ 200.0 ] }");
            Assert.IsNull(RunInfoFile.TryLoad(truncatedRange), @"a one-element range reads as absent, not as a range");
            string truncatedWindow = Path.Combine(dir, @"truncated-window.run-info.json");
            File.WriteAllText(truncatedWindow,
                @"{ ""format_version"": 2, ""ms2_scan_windows"": [ { ""isolation_center"": 450.0, ""scan_window"": [ 200.0 ] } ] }");
            Assert.IsNull(RunInfoFile.TryLoad(truncatedWindow), @"a damaged per-window range reads as absent");
        }

        /// <summary>
        /// A method whose MS2 scan range follows the isolation window - here each window scans
        /// from just above itself - has a run-wide union wider than any one window's range, so
        /// the record keeps each window's own, and an ion is judged against its window's. A
        /// window the file does not list, and every window of a version-1 file, falls back to
        /// the union.
        /// </summary>
        private static void AssertScanWindowPerIsolationWindow(string dir)
        {
            var collector = new RunInfoCollector(null);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                collector.ObserveMs2ScanWindow(new IsolationWindow(500.0, 2.0, 2.0), 502.0, 1500.0);
                collector.ObserveMs2ScanWindow(new IsolationWindow(400.0, 2.0, 2.0), 402.0, 1400.0);
            }
            var info = collector.Build(new List<Spectrum>(), new List<MS1Spectrum>());
            CollectionAssert.AreEqual(new[] { 402.0, 1500.0 }, info.Ms2ScanWindow, @"the run-wide union");
            Assert.AreEqual(2, info.Ms2ScanWindows.Count);
            Assert.AreEqual(400.0, info.Ms2ScanWindows[0].IsolationCenter, @"listed by isolation center");
            CollectionAssert.AreEqual(new[] { 502.0, 1500.0 }, info.Ms2ScanWindowFor(500.0));
            CollectionAssert.AreEqual(new[] { 402.0, 1400.0 }, info.Ms2ScanWindowFor(400.0));
            CollectionAssert.AreEqual(info.Ms2ScanWindow, info.Ms2ScanWindowFor(600.0), @"an unlisted window takes the union");

            string path = Path.Combine(dir, @"windows.run-info.json");
            RunInfoFile.Save(path, info);
            var read = RunInfoFile.TryLoad(path);
            Assert.IsNotNull(read);
            CollectionAssert.AreEqual(new[] { 502.0, 1500.0 }, read.Ms2ScanWindowFor(500.0));

            string versionOne = Path.Combine(dir, @"v1.run-info.json");
            File.WriteAllText(versionOne, @"{ ""format_version"": 1, ""ms2_scan_window"": [ 200.0, 1500.0 ] }");
            var old = RunInfoFile.TryLoad(versionOne);
            Assert.IsNotNull(old, @"a version-1 file is still read");
            Assert.IsNull(old.Ms2ScanWindows);
            CollectionAssert.AreEqual(new[] { 200.0, 1500.0 }, old.Ms2ScanWindowFor(500.0));
        }

        /// <summary>
        /// One parse writes the spectra cache and the run info with the same source fingerprint,
        /// so a run info whose fingerprint disagrees with the cache's describes another version
        /// of the source. A cache that recorded no fingerprint cannot say, and is no reason to
        /// distrust the run info.
        /// </summary>
        private static void AssertStalenessAgainstTheSpectraCache(string dir)
        {
            string source = Path.Combine(dir, @"source.mzML");
            File.WriteAllText(source, @"acquired");
            Assert.IsTrue(SpectraCache.TryComputeSourceFingerprint(source, out long size, out long mtimeMs));
            string cache = Path.Combine(dir, @"source.spectra.bin");
            SpectraCache.SaveSpectraCache(cache, new List<Spectrum>(), new List<MS1Spectrum>(), source);
            var info = new RunInfo { SourceFile = @"source.mzML", SourceSize = size, SourceMtimeMs = mtimeMs };
            Assert.IsTrue(RunInfoFile.DescribesSpectraCache(info, cache));
            info.SourceSize = size + 1;
            Assert.IsFalse(RunInfoFile.DescribesSpectraCache(info, cache), @"another size is another source");
            info.SourceSize = size;
            info.SourceMtimeMs = mtimeMs - 1000;
            Assert.IsFalse(RunInfoFile.DescribesSpectraCache(info, cache), @"another mtime is another source");

            string unfingerprinted = Path.Combine(dir, @"nosource.spectra.bin");
            SpectraCache.SaveSpectraCache(unfingerprinted, new List<Spectrum>(), new List<MS1Spectrum>());
            Assert.IsTrue(RunInfoFile.DescribesSpectraCache(info, unfingerprinted), @"nothing to compare against");
            Assert.IsTrue(RunInfoFile.DescribesSpectraCache(info, Path.Combine(dir, @"absent.spectra.bin")));
        }

        private static string Resave(string path, RunInfo info)
        {
            string copy = path + @".copy";
            RunInfoFile.Save(copy, info);
            return File.ReadAllText(copy);
        }
    }
}
