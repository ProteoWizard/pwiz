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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using pwiz.Quasar.Features.Data;
using pwiz.Quasar.Features.Hardklor;

namespace pwiz.Quasar.Features
{
    /// <summary>
    /// Where a run's .hk file is kept: beside the raw file (<c>run.hk</c>), or, when that folder cannot be written
    /// (a read-only share), in the application's own cache folder.
    /// </summary>
    public sealed class HkStore
    {
        /// <summary>
        /// The settings a search depends on, as text for <see cref="HkFile.Stamp.Settings"/>: the isotope model
        /// (<paramref name="model"/>), Hardklor's settings for each analyzer searched, Kronik's linking, the DIA rule
        /// and whether peaks were vendor-centroided. Any change makes earlier .hk files stale.
        /// </summary>
        public static string SearchSettings(string model, bool centroid) => SearchSettings(model, centroid, HardklorSettings.Default, KronikSettings.Default);

        /// <summary>
        /// The settings text for a search with Hardklor settings <paramref name="search"/>, linked with
        /// <paramref name="kronik"/>. The text is Glimpse's, character for character, so the two read each other's files.
        /// </summary>
        public static string SearchSettings(string model, bool centroid, HardklorSettings search, KronikSettings kronik)
        {
            var k = kronik;
            return string.Create(CultureInfo.InvariantCulture,
                $@"model {model}; orbitrap {Of(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.orbitrap, search))}; "
                + $@"astral {Of(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.astral, search))}; iontrap {Of(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.ion_trap, search))}; "
                + $@"kronik {k.PpmTolerance}ppm gap{k.GapTolerance} min{k.MinScans}; dia windows {PeptideLikeObserver.MAX_DIA_WINDOWS}; centroid {centroid}");
        }

        private readonly string _cacheDir;

        /// <param name="cacheDir">The application's cache folder, for runs whose own folder cannot be written.</param>
        public HkStore(string cacheDir)
        {
            _cacheDir = cacheDir;
        }

        /// <summary>The cache-folder file of a run: its name and a hash of its full path, so runs of the same name differ.</summary>
        public string CachePath(string rawPath)
        {
            var full = Path.GetFullPath(rawPath.TrimEnd('\\', '/'));
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full.ToUpperInvariant())))[..16];
            return Path.Combine(_cacheDir, $@"{Path.GetFileNameWithoutExtension(full)}-{hash}.hk");
        }

        /// <summary>The .hk file made from <paramref name="stamp"/>, beside the run or in the cache; null when neither is.</summary>
        public string FindUsable(string rawPath, HkFile.Stamp stamp) =>
            new[] { HkFile.PathBeside(rawPath), CachePath(rawPath) }.FirstOrDefault(p => HkFile.Matches(p, stamp));

        /// <summary>Save the run's signal beside it, else in the cache. Returns where it went.</summary>
        public string Save(string rawPath, PeptideLikeSignal signal, HkFile.Stamp stamp)
        {
            var beside = HkFile.PathBeside(rawPath);
            try
            {
                HkFile.Write(beside, signal, stamp);
                return beside;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(beside + @".tmp");
                var cache = CachePath(rawPath);
                HkFile.Write(cache, signal, stamp);
                return cache;
            }
        }

        /// <summary>One analyzer's Hardklor settings in the settings text, or "none" when it is not searched.</summary>
        private static string Of(HardklorSettings s) => s is null ? @"none" : string.Create(CultureInfo.InvariantCulture,
            $@"{InstrumentText(s.Instrument)} {s.Resolution} z{s.MinCharge}-{s.MaxCharge} r{s.Correlation:R} d{s.Depth} max{s.MaxMoleculeMz} area{s.DistributionArea}");

        /// <summary>
        /// The instrument as Glimpse writes it: the name of its enum member there (Orbitrap, Tof, Qit, Fticr).
        /// Quasar's members are snake_case, so the text is spelled out to keep .hk files interchangeable.
        /// </summary>
        private static string InstrumentText(HardklorInstrument instrument) => instrument switch
        {
            HardklorInstrument.orbitrap => @"Orbitrap",
            HardklorInstrument.tof => @"Tof",
            HardklorInstrument.qit => @"Qit",
            HardklorInstrument.fticr => @"Fticr",
            _ => ((int)instrument).ToString(CultureInfo.InvariantCulture),
        };

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Left behind; the next save replaces it.
            }
        }
    }
}
