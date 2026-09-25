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
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// What one acquisition says about itself: the instrument it ran on, how its MS2 spectra
    /// were fragmented, and the ranges it scanned. Osprey scores nothing from this; it is
    /// recorded so a consumer that trains on Osprey's evidence (the training export, docs/22-training-export.md)
    /// can condition on the instrument and the collision energy without opening the raw data,
    /// which after staging may no longer exist.
    ///
    /// <para>Captured during the one parse that builds <c>.spectra.bin</c>, from fields the
    /// ProteoWizard read already populates, and written beside that cache as
    /// <c>&lt;stem&gt;.run-info.json</c>. It is a per-run CACHE in the sense of doc 00: derived
    /// from the source alone and independent of every search setting. The one validity key
    /// that names it is the training export's, per run, because the export copies it into
    /// its footer. The spectra cache format is unchanged.</para>
    /// </summary>
    public sealed class RunInfo
    {
        /// <summary>
        /// Version 2 added <see cref="Ms2ScanWindows"/>, the scan window of each isolation
        /// window.
        /// </summary>
        public const int CURRENT_FORMAT_VERSION = 2;

        /// <summary>
        /// The oldest format this build reads. A version-1 file has no per-window scan windows,
        /// and a reader falls back to the run-wide <see cref="Ms2ScanWindow"/>, as it did.
        /// </summary>
        public const int OLDEST_READABLE_FORMAT_VERSION = 1;

        /// <summary>The histogram key for a spectrum that carries no value.</summary>
        public const string NONE_KEY = @"none";

        /// <summary>A histogram with the ordinal key order every writer and reader agrees on.</summary>
        public static SortedDictionary<string, int> NewHistogram()
        {
            return new SortedDictionary<string, int>(StringComparer.Ordinal);
        }

        /// <summary>The key a collision energy is counted under.</summary>
        public static string CollisionEnergyKey(double? energy)
        {
            return energy.HasValue
                ? energy.Value.ToString(@"R", CultureInfo.InvariantCulture)
                : NONE_KEY;
        }

        [JsonProperty(@"format_version", Order = 0)]
        public int FormatVersion { get; set; } = CURRENT_FORMAT_VERSION;

        /// <summary>Source file NAME (never a path, so the file stays relocatable).</summary>
        [JsonProperty(@"source_file", Order = 1)]
        public string SourceFile { get; set; }

        /// <summary>The source fingerprint <c>.spectra.bin</c> records: size in bytes.</summary>
        [JsonProperty(@"source_size", Order = 2)]
        public long SourceSize { get; set; }

        /// <summary>The source fingerprint <c>.spectra.bin</c> records: mtime, Unix ms UTC.</summary>
        [JsonProperty(@"source_mtime_ms", Order = 3)]
        public long SourceMtimeMs { get; set; }

        [JsonProperty(@"run_id", Order = 4)]
        public string RunId { get; set; }

        /// <summary>Acquisition start, ISO 8601 round-trip form, or null when the file has none.</summary>
        [JsonProperty(@"run_start_time", Order = 5)]
        public string RunStartTime { get; set; }

        [JsonProperty(@"instrument_vendor", Order = 6)]
        public string InstrumentVendor { get; set; }

        [JsonProperty(@"instrument_model", Order = 7)]
        public string InstrumentModel { get; set; }

        [JsonProperty(@"instrument_serial_number", Order = 8)]
        public string InstrumentSerialNumber { get; set; }

        /// <summary>Every instrument configuration the file declares.</summary>
        [JsonProperty(@"instrument_configurations", Order = 9)]
        public List<InstrumentConfiguration> InstrumentConfigurations { get; set; } = new List<InstrumentConfiguration>();

        [JsonProperty(@"n_ms1", Order = 10)]
        public int NMs1 { get; set; }

        [JsonProperty(@"n_ms2", Order = 11)]
        public int NMs2 { get; set; }

        [JsonProperty(@"ms1_rt_range", Order = 12)]
        public double[] Ms1RtRange { get; set; }

        [JsonProperty(@"ms2_rt_range", Order = 13)]
        public double[] Ms2RtRange { get; set; }

        /// <summary>[lowest lower limit, highest upper limit] over the MS1 scan windows.</summary>
        [JsonProperty(@"ms1_scan_window", Order = 14)]
        public double[] Ms1ScanWindow { get; set; }

        /// <summary>[lowest lower limit, highest upper limit] over the MS2 scan windows.</summary>
        [JsonProperty(@"ms2_scan_window", Order = 15)]
        public double[] Ms2ScanWindow { get; set; }

        /// <summary>[lowest, highest] isolation-window bound over the MS2 spectra.</summary>
        [JsonProperty(@"ms2_isolation_range", Order = 16)]
        public double[] Ms2IsolationRange { get; set; }

        /// <summary>MS2 spectrum count per mass analyzer name.</summary>
        [JsonProperty(@"ms2_analyzers", Order = 17)]
        public SortedDictionary<string, int> Ms2Analyzers { get; set; } = NewHistogram();

        /// <summary>MS2 spectrum count per dissociation method (<see cref="NONE_KEY"/> when absent).</summary>
        [JsonProperty(@"dissociation_methods", Order = 18)]
        public SortedDictionary<string, int> DissociationMethods { get; set; } = NewHistogram();

        /// <summary>
        /// MS2 spectrum count per collision energy as the file reports it - normalized for
        /// Thermo, electron volts for Sciex, and so on, so the unit is the vendor's
        /// (<see cref="InstrumentVendor"/>). Keys are round-trip invariant numbers, or
        /// <see cref="NONE_KEY"/>.
        /// </summary>
        [JsonProperty(@"collision_energies", Order = 19)]
        public SortedDictionary<string, int> CollisionEnergies { get; set; } = NewHistogram();

        /// <summary>
        /// The MS2 scan window of each isolation window, in isolation-center order. Methods
        /// whose MS2 scan range depends on the isolation window - a range that starts above
        /// each window's precursor, say - make the run-wide <see cref="Ms2ScanWindow"/> too
        /// wide for any one window, so a consumer asking whether an ion was in range asks this.
        /// Null in a version-1 file and when no MS2 spectrum was kept.
        /// </summary>
        [JsonProperty(@"ms2_scan_windows", Order = 20)]
        public List<IsolationScanWindow> Ms2ScanWindows { get; set; }

        /// <summary>
        /// The MS2 scan window of the isolation window centered on
        /// <paramref name="isolationCenter"/> - matched by the window key the spectra cache
        /// groups spectra by - or the run-wide <see cref="Ms2ScanWindow"/> when the file
        /// records none for it (every version-1 file). Null when neither is known.
        /// </summary>
        public double[] Ms2ScanWindowFor(double isolationCenter)
        {
            if (Ms2ScanWindows != null)
            {
                int key = SpectraCache.WindowKey(isolationCenter);
                foreach (var window in Ms2ScanWindows)
                {
                    if (SpectraCache.WindowKey(window.IsolationCenter) == key && window.ScanWindow != null)
                        return window.ScanWindow;
                }
            }
            return Ms2ScanWindow;
        }

        /// <summary>One isolation window and the MS2 scan window its spectra covered.</summary>
        public sealed class IsolationScanWindow
        {
            [JsonProperty(@"isolation_center", Order = 0)]
            public double IsolationCenter { get; set; }

            [JsonProperty(@"isolation_lower", Order = 1)]
            public double IsolationLower { get; set; }

            [JsonProperty(@"isolation_upper", Order = 2)]
            public double IsolationUpper { get; set; }

            /// <summary>[lowest lower limit, highest upper limit] over the window's spectra, or null.</summary>
            [JsonProperty(@"scan_window", Order = 3)]
            public double[] ScanWindow { get; set; }
        }

        /// <summary>One declared instrument configuration.</summary>
        public sealed class InstrumentConfiguration
        {
            [JsonProperty(@"model", Order = 0)]
            public string Model { get; set; }

            [JsonProperty(@"ionization", Order = 1)]
            public string Ionization { get; set; }

            [JsonProperty(@"analyzer", Order = 2)]
            public string Analyzer { get; set; }

            [JsonProperty(@"detector", Order = 3)]
            public string Detector { get; set; }
        }
    }

    /// <summary>
    /// Path, write and read for <c>&lt;stem&gt;.run-info.json</c>.
    /// </summary>
    public static class RunInfoFile
    {
        /// <summary>
        /// Beside the run's <c>.spectra.bin</c>: same directory resolution
        /// (<see cref="ArtifactPaths.ResolveCacheDir"/>), same stem.
        /// </summary>
        public static string PathFor(string inputFile)
        {
            string fileName = Path.GetFileNameWithoutExtension(inputFile) + @".run-info.json";
            return Path.Combine(ArtifactPaths.ResolveCacheDir(inputFile), fileName);
        }

        /// <summary>
        /// Write <paramref name="info"/> through <see cref="FileSaver"/>, so presence proves
        /// the file is whole (P8). Indented with '\n' newlines rather than the platform's, so
        /// one source yields the same bytes on every machine.
        /// </summary>
        public static void Save(string path, RunInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            var json = new StringBuilder();
            using (var text = new StringWriter(json, CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(text))
            {
                text.NewLine = "\n";
                writer.Formatting = Formatting.Indented;
                JsonSerializer.CreateDefault().Serialize(writer, info);
            }
            using (var saver = new FileSaver(path))
            {
                File.WriteAllText(saver.SafeName, json.ToString());
                saver.Commit();
            }
        }

        /// <summary>
        /// Whether <paramref name="info"/> describes the version of the source the spectra
        /// cache at <paramref name="cachePath"/> was parsed from. One parse writes both and
        /// records the same fingerprint in each, so a disagreement means the run info is left
        /// over from another version of the source - a relay that shipped a new cache beside
        /// an old run info, say. True when the cache records no fingerprint to compare.
        /// </summary>
        public static bool DescribesSpectraCache(RunInfo info, string cachePath)
        {
            if (!SpectraCache.TryReadSourceFingerprint(cachePath, out long size, out long mtimeMs))
                return true;
            return info.SourceSize == size && info.SourceMtimeMs == mtimeMs;
        }

        /// <summary>
        /// Read a run-info file, or null when it is absent, unreadable or of a format this
        /// build does not know. Never throws: the file is descriptive, and a consumer degrades
        /// to "unknown" rather than failing on it.
        /// </summary>
        public static RunInfo TryLoad(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                var info = JsonConvert.DeserializeObject<RunInfo>(File.ReadAllText(path));
                if (info == null || info.FormatVersion < RunInfo.OLDEST_READABLE_FORMAT_VERSION ||
                    info.FormatVersion > RunInfo.CURRENT_FORMAT_VERSION)
                {
                    return null;
                }
                return info;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return null;
            }
        }
    }
}
