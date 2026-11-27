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
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Features;
using pwiz.Quasar.Features.Hardklor;
using pwiz.Quasar.IO;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// Where a run's .hk file is saved and found: beside the raw file, else the cache folder (ported from
    /// skyline-glimpse's HkStoreTests, every assertion and expected value kept).
    /// </summary>
    [TestClass]
    public class HkStoreTest
    {
        private string _dir;

        [TestInitialize]
        public void CreateFolder()
        {
            _dir = Directory.CreateTempSubdirectory("quasar-hkstore").FullName;
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            Directory.Delete(_dir, true);
        }

        /// <summary>Saved beside the raw file when its folder can take it, else in the cache, and found again.</summary>
        [TestMethod]
        public void TestHkStoreSaveAndFind()
        {
            SavedBesideTheRawFileAndFoundThere();
            SavedInTheCacheWhenTheRawFilesFolderCannotTakeIt();
        }

        /// <summary>Cache file names, and the settings text that makes a saved search stale.</summary>
        [TestMethod]
        public void TestHkStoreNamesAndSettings()
        {
            CacheFilesAreNamedByRunAndPath();
            TheSettingsTextNamesEverythingASearchDependsOn();
        }

        private static PeptideLikeSignal Signal()
        {
            var run = new FakeRun();
            for (var s = 0; s < 3; s++)
                run.Ms1(1 + s * 0.1, new[] { 500.0, 500.5, 501.0 }, new[] { 100.0, 50, 25 });
            using (var observer = new PeptideLikeObserver(new PatternEnvelopeFinder((500.0, 2, 3)), workers: 2))
            {
                ScanPass.Run(run.Spectra, observer);
                Assert.IsTrue(observer.Completion.Wait(TimeSpan.FromSeconds(30)));
                return observer.Completion.Result;
            }
        }

        private void SavedBesideTheRawFileAndFoundThere()
        {
            var raw = Path.Combine(_dir, "data", "run.raw");
            Directory.CreateDirectory(Path.Combine(_dir, "data"));
            File.WriteAllBytes(raw, new byte[10]);
            var store = new HkStore(Path.Combine(_dir, "cache"));
            var stamp = HkFile.Stamp.Of(raw, 3, "s");
            Assert.IsNull(store.FindUsable(raw, stamp));                 // nothing saved yet

            var where = store.Save(raw, Signal(), stamp);
            Assert.AreEqual(Path.Combine(_dir, "data", "run.hk"), where);
            Assert.AreEqual(where, store.FindUsable(raw, stamp));
            Assert.IsNull(store.FindUsable(raw, stamp with { Settings = "other" }));
            Assert.IsFalse(Directory.Exists(Path.Combine(_dir, "cache")));
        }

        private void SavedInTheCacheWhenTheRawFilesFolderCannotTakeIt()
        {
            var raw = Path.Combine(_dir, "share", "run.raw");
            Directory.CreateDirectory(Path.Combine(_dir, "share"));
            File.WriteAllBytes(raw, new byte[10]);
            Directory.CreateDirectory(Path.Combine(_dir, "share", "run.hk"));   // run.hk cannot be a file there
            var store = new HkStore(Path.Combine(_dir, "cache"));
            var stamp = HkFile.Stamp.Of(raw, 3, "s");

            var where = store.Save(raw, Signal(), stamp);
            Assert.AreEqual(store.CachePath(raw), where);
            StringAssert.StartsWith(where, Path.Combine(_dir, "cache"));
            Assert.AreEqual(where, store.FindUsable(raw, stamp));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "share", "run.hk.tmp")));
        }

        private static void CacheFilesAreNamedByRunAndPath()
        {
            // Windows paths: elsewhere a backslash is no separator, so these names mean something else.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;
            var store = new HkStore(@"C:\cache");
            var a = store.CachePath(@"D:\one\run.raw");
            StringAssert.Matches(a, new Regex(@"^C:\\cache\\run-[0-9A-F]{16}\.hk$"));
            Assert.AreNotEqual(a, store.CachePath(@"D:\two\run.raw"));    // same name, another folder
            Assert.AreEqual(a, store.CachePath(@"d:\ONE\run.raw"));       // Windows paths ignore case
            Assert.AreEqual(store.CachePath(@"D:\w\waters.raw"), store.CachePath(@"D:\w\waters.raw\"));
        }

        private static void TheSettingsTextNamesEverythingASearchDependsOn()
        {
            Assert.AreEqual(
                "model m; orbitrap Orbitrap 60000 z1-7 r0.9876883405951378 d2 max5000 areaTrue; "
                + "astral Tof 80000 z1-7 r0.9876883405951378 d2 max5000 areaTrue; iontrap none; "
                + "kronik 10ppm gap1 min3; dia windows 1000; centroid True",
                HkStore.SearchSettings("m", true));
            StringAssert.EndsWith(HkStore.SearchSettings("m", false), "centroid False");
            // Other search settings: another text, so saved searches made with the old ones are searched again.
            Assert.AreEqual(
                "model m; orbitrap Orbitrap 60000 z1-4 r0.9876883405951378 d2 max5000 areaTrue; "
                + "astral Tof 80000 z1-4 r0.9876883405951378 d2 max5000 areaTrue; iontrap none; "
                + "kronik 5.5ppm gap0 min2; dia windows 1000; centroid True",
                HkStore.SearchSettings("m", true, HardklorSettings.Default with { MaxCharge = 4 }, new KronikSettings(5.5, 0, 2)));
            StringAssert.Contains(HkStore.SearchSettings("m", true,
                HardklorSettings.Default with { Depth = 3, Correlation = 0.95 }, KronikSettings.Default), "z1-7 r0.95 d3 ");
            Assert.AreEqual("pwiz isotope tables, 13C 0.01084, spacing 1.00286864", PwizEnvelopeModel.Description);
        }
    }
}
