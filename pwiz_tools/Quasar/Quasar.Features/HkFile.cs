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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Parquet;
using Parquet.Schema;
using pwiz.Quasar.Features.Data;

namespace pwiz.Quasar.Features
{
    /// <summary>
    /// What a run's peptide-like search produced, kept next to the raw file as <c>run.hk</c> (Parquet) so reopening
    /// the file shows the Peptide-like trace without searching again.
    /// </summary>
    /// <remarks>
    /// <para>One row per isotope envelope: the spectrum index, MS level and retention time of its scan; the
    /// series it was linked in (0 for MS1, then one per DIA isolation window); Hardklor's report (monoisotopic
    /// mass, charge, intensity, base peak m/z, correlation, m/z range); the persistent feature it belongs to
    /// (<c>feature</c>, -1 for none); and the peptide-like signal of its scan, the summed intensity of the scan's
    /// peaks in persistent envelopes, each peak once (<c>scan_peptide_like</c>, and <c>scan_peptide_like_2plus</c>
    /// counting only multiply charged MS1 envelopes; the same for MS2). The rows of one feature are the feature:
    /// its seed is its most intense envelope. Envelopes' peaks are not kept: Hardklor finds the same envelopes in
    /// a spectrum again in about a millisecond.</para>
    /// <para>File metadata says what the search was made from (format, the raw file's size and time, the
    /// spectrum count, the search settings); a file that does not match is ignored and replaced. It also lists
    /// every searched scan of each series (<c>glimpse.series</c>), since a searched scan can have no envelope.</para>
    /// <para>The format is Glimpse's, unchanged: Quasar and Glimpse read and write the same files. Parquet.Net's
    /// asynchronous API is waited on synchronously here (no async/await), with the same calls in the same order,
    /// so the bytes written are the same.</para>
    /// </remarks>
    public static class HkFile
    {
        public const int FORMAT_VERSION = 2;

        private static readonly DataField<int> SCAN = new DataField<int>(@"scan");
        private static readonly DataField<int> MS_LEVEL = new DataField<int>(@"ms_level");
        private static readonly DataField<float> RT = new DataField<float>(@"rt");
        private static readonly DataField<int> SERIES_FIELD = new DataField<int>(@"series");
        private static readonly DataField<double> MONO_MASS = new DataField<double>(@"mono_mass");
        private static readonly DataField<int> CHARGE = new DataField<int>(@"charge");
        private static readonly DataField<float> INTENSITY = new DataField<float>(@"intensity");
        private static readonly DataField<double> BASE_PEAK_MZ = new DataField<double>(@"base_peak_mz");
        private static readonly DataField<float> CORRELATION = new DataField<float>(@"correlation");
        private static readonly DataField<double> LOW_MZ = new DataField<double>(@"low_mz");
        private static readonly DataField<double> HIGH_MZ = new DataField<double>(@"high_mz");
        private static readonly DataField<int> FEATURE = new DataField<int>(@"feature");
        private static readonly DataField<double> SCAN_SIGNAL = new DataField<double>(@"scan_peptide_like");
        private static readonly DataField<double> SCAN_SIGNAL_2PLUS = new DataField<double>(@"scan_peptide_like_2plus");

        private static readonly ParquetSchema SCHEMA = new ParquetSchema(
            SCAN, MS_LEVEL, RT, SERIES_FIELD, MONO_MASS, CHARGE, INTENSITY, BASE_PEAK_MZ, CORRELATION, LOW_MZ, HIGH_MZ, FEATURE, SCAN_SIGNAL, SCAN_SIGNAL_2PLUS);

        private const string FORMAT_KEY = @"glimpse.hk.format";
        private const string RAW_SIZE_KEY = @"glimpse.raw.size";
        private const string RAW_MODIFIED_KEY = @"glimpse.raw.modified";
        private const string SPECTRA_KEY = @"glimpse.spectra";
        private const string SETTINGS_KEY = @"glimpse.settings";
        private const string DDA_KEY = @"glimpse.dda";
        private const string SERIES_KEY = @"glimpse.series";

        /// <summary>What a .hk file must have been made from to be used.</summary>
        /// <param name="RawSize">The raw file's size in bytes (0 for a folder).</param>
        /// <param name="RawModifiedUtc">Its last write time.</param>
        /// <param name="SpectrumCount">Spectra in the run.</param>
        /// <param name="Settings">The search settings, as text (any change makes old files stale).</param>
        public sealed record Stamp(long RawSize, DateTime RawModifiedUtc, int SpectrumCount, string Settings)
        {
            /// <summary>
            /// The stamp of a raw file as it is on disk now. A vendor folder (Waters .raw, Bruker .d) is stamped with the
            /// total size and newest write time of the files inside it: the folder's own time does not change when
            /// a file in it is rewritten.
            /// </summary>
            public static Stamp Of(string rawPath, int spectrumCount, string settings)
            {
                if (File.Exists(rawPath))
                    return new Stamp(new FileInfo(rawPath).Length, File.GetLastWriteTimeUtc(rawPath), spectrumCount, settings);
                // A null path throws here, as it did in Glimpse.
                // ReSharper disable once AssignNullToNotNullAttribute
                var files = new DirectoryInfo(rawPath).EnumerateFiles(@"*", SearchOption.AllDirectories).ToList();
                return new Stamp(files.Sum(f => f.Length),
                    files.Count == 0 ? Directory.GetLastWriteTimeUtc(rawPath) : files.Max(f => f.LastWriteTimeUtc), spectrumCount, settings);
            }
        }

        /// <summary>The .hk file beside a raw file: <c>run.raw</c> becomes <c>run.hk</c>.</summary>
        public static string PathBeside(string rawPath) => Path.ChangeExtension(rawPath.TrimEnd('\\', '/'), @".hk");

        /// <summary>Write the signal to <paramref name="path"/> (through a temporary file, so a reader never sees half of one).</summary>
        public static void Write(string path, PeptideLikeSignal signal, Stamp stamp)
        {
            var seriesOf = new Dictionary<int, int>();
            for (var s = 0; s < signal.Series.Count; s++)
            {
                foreach (var searched in signal.Series[s])
                    seriesOf[searched] = s;
            }

            var scans = signal.Envelopes.Keys.Where(seriesOf.ContainsKey).Order().ToArray();
            var n = scans.Sum(i => signal.Envelopes[i].Keys.Length);
            var scan = new int[n];
            var level = new int[n];
            var rt = new float[n];
            var series = new int[n];
            var mass = new double[n];
            var charge = new int[n];
            var intensity = new float[n];
            var basePeak = new double[n];
            var correlation = new float[n];
            var low = new double[n];
            var high = new double[n];
            var feature = new int[n];
            var scanSignal = new double[n];
            var scanSignal2Plus = new double[n];
            var all = signal.IntensityByScan();
            var multiplyCharged = signal.IntensityByScan(2);
            var r = 0;
            foreach (var i in scans)
            {
                var env = signal.Envelopes[i];
                var ids = signal.FeatureOf[i];
                var h = signal.Headers[i];
                for (var e = 0; e < env.Keys.Length; e++, r++)
                {
                    scan[r] = i;
                    level[r] = h.MsLevel;
                    rt[r] = (float)h.RetentionTime;
                    series[r] = seriesOf[i];
                    mass[r] = env.Keys[e].MonoMass;
                    charge[r] = env.Keys[e].Charge;
                    intensity[r] = env.Keys[e].Intensity;
                    basePeak[r] = env.Details[e].BasePeakMz;
                    correlation[r] = env.Details[e].Correlation;
                    low[r] = env.Details[e].LowMz;
                    high[r] = env.Details[e].HighMz;
                    feature[r] = ids[e];
                    scanSignal[r] = all[i];
                    scanSignal2Plus[r] = multiplyCharged[i];
                }
            }

            // A full path has a directory unless it is a root, which is no file to write.
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var temp = path + @".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // Zstd at its fastest level: about the compression of the default level, written some 50 times faster
                // (Parquet.Net's default Zstd level took 268 s for an Astral run's 17 million envelopes). Skyline's report
                // export uses Optimal (level 3); on these columns it wrote about 50% slower for no smaller file.
                var writer = RunSync(ParquetWriter.CreateAsync(SCHEMA, stream, new ParquetOptions
                {
                    CompressionMethod = CompressionMethod.Zstd,
                    CompressionLevel = System.IO.Compression.CompressionLevel.Fastest,
                }));
                try
                {
                    writer.CustomMetadata = new Dictionary<string, string>
                    {
                        [FORMAT_KEY] = FORMAT_VERSION.ToString(CultureInfo.InvariantCulture),
                        [RAW_SIZE_KEY] = stamp.RawSize.ToString(CultureInfo.InvariantCulture),
                        [RAW_MODIFIED_KEY] = stamp.RawModifiedUtc.ToUniversalTime().ToString(@"O", CultureInfo.InvariantCulture),
                        [SPECTRA_KEY] = stamp.SpectrumCount.ToString(CultureInfo.InvariantCulture),
                        [SETTINGS_KEY] = stamp.Settings,
                        [DDA_KEY] = signal.IsDda ? @"true" : @"false",
                        [SERIES_KEY] = JsonSerializer.Serialize(signal.Series),
                    };
                    using (var group = writer.CreateRowGroup())
                    {
                        RunSync(group.WriteAsync(SCAN, (ReadOnlyMemory<int>)scan));
                        RunSync(group.WriteAsync(MS_LEVEL, (ReadOnlyMemory<int>)level));
                        RunSync(group.WriteAsync(RT, (ReadOnlyMemory<float>)rt));
                        RunSync(group.WriteAsync(SERIES_FIELD, (ReadOnlyMemory<int>)series));
                        RunSync(group.WriteAsync(MONO_MASS, (ReadOnlyMemory<double>)mass));
                        RunSync(group.WriteAsync(CHARGE, (ReadOnlyMemory<int>)charge));
                        RunSync(group.WriteAsync(INTENSITY, (ReadOnlyMemory<float>)intensity));
                        RunSync(group.WriteAsync(BASE_PEAK_MZ, (ReadOnlyMemory<double>)basePeak));
                        RunSync(group.WriteAsync(CORRELATION, (ReadOnlyMemory<float>)correlation));
                        RunSync(group.WriteAsync(LOW_MZ, (ReadOnlyMemory<double>)low));
                        RunSync(group.WriteAsync(HIGH_MZ, (ReadOnlyMemory<double>)high));
                        RunSync(group.WriteAsync(FEATURE, (ReadOnlyMemory<int>)feature));
                        RunSync(group.WriteAsync(SCAN_SIGNAL, (ReadOnlyMemory<double>)scanSignal));
                        RunSync(group.WriteAsync(SCAN_SIGNAL_2PLUS, (ReadOnlyMemory<double>)scanSignal2Plus));
                    }
                }
                finally
                {
                    // Writes the footer.
                    RunSync(writer.DisposeAsync());
                }
            }
            File.Move(temp, path, true);
        }

        /// <summary>
        /// The signal saved at <paramref name="path"/>, or null when there is none, it cannot be read, or it was made
        /// from something other than <paramref name="expected"/> (another version of the raw file, other settings).
        /// </summary>
        public static PeptideLikeSignal TryRead(string path, Stamp expected, ScanHeader[] headers)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return Read(path, expected, headers);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null;   // damaged, truncated or not a .hk file: search again
            }
        }

        /// <summary>
        /// True when <paramref name="path"/> is a .hk file made from <paramref name="expected"/>. Reads only the file's
        /// footer, so it can decide before a pass whether the search is needed.
        /// </summary>
        public static bool Matches(string path, Stamp expected)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var reader = RunSync(ParquetReader.CreateAsync(stream));
                    try
                    {
                        return Matches(reader.CustomMetadata, expected);
                    }
                    finally
                    {
                        RunSync(reader.DisposeAsync());
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return false;   // whatever is wrong with the file, it must never stop the raw file from opening
            }
        }

        private static bool Matches(IReadOnlyDictionary<string, string> meta, Stamp expected)
        {
            return Get(meta, FORMAT_KEY) == FORMAT_VERSION.ToString(CultureInfo.InvariantCulture)
                && Get(meta, RAW_SIZE_KEY) == expected.RawSize.ToString(CultureInfo.InvariantCulture)
                && Get(meta, RAW_MODIFIED_KEY) == expected.RawModifiedUtc.ToUniversalTime().ToString(@"O", CultureInfo.InvariantCulture)
                && Get(meta, SPECTRA_KEY) == expected.SpectrumCount.ToString(CultureInfo.InvariantCulture)
                && Get(meta, SETTINGS_KEY) == expected.Settings;
        }

        private static string Get(IReadOnlyDictionary<string, string> meta, string key) =>
            meta.TryGetValue(key, out var v) ? v : string.Empty;

        private static PeptideLikeSignal Read(string path, Stamp expected, ScanHeader[] headers)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var reader = RunSync(ParquetReader.CreateAsync(stream));
                try
                {
                    return Read(reader, expected, headers);
                }
                finally
                {
                    RunSync(reader.DisposeAsync());
                }
            }
        }

        private static PeptideLikeSignal Read(ParquetReader reader, Stamp expected, ScanHeader[] headers)
        {
            var meta = reader.CustomMetadata;
            if (!Matches(meta, expected) || headers.Length != expected.SpectrumCount)
                return null;
            var seriesList = JsonSerializer.Deserialize<int[][]>(Get(meta, SERIES_KEY)) ?? throw new InvalidDataException(@"No series.");
            var dda = Get(meta, DDA_KEY) == @"true";

            // Every searched scan, with no envelopes until rows say otherwise.
            var rows = new Dictionary<int, List<(EnvelopeKey Key, EnvelopeDetail Detail, int Feature)>>();
            var all = new double[headers.Length];
            var multiplyCharged = new double[headers.Length];
            Array.Fill(all, double.NaN);
            Array.Fill(multiplyCharged, double.NaN);
            foreach (var s in seriesList)
            {
                foreach (var scan in s)
                {
                    if (scan < 0 || scan >= headers.Length)
                        throw new InvalidDataException($@"Scan {scan} beyond the run's {headers.Length} spectra.");
                    rows[scan] = new List<(EnvelopeKey Key, EnvelopeDetail Detail, int Feature)>();
                    all[scan] = 0;                 // searched; rows below give the scans with signal
                    multiplyCharged[scan] = 0;
                }
            }

            for (var g = 0; g < reader.RowGroupCount; g++)
            {
                using (var group = reader.OpenRowGroupReader(g))
                {
                    var count = checked((int)group.RowCount);
                    var scan = ReadColumn(group, count, SCAN);
                    var mass = ReadColumn(group, count, MONO_MASS);
                    var charge = ReadColumn(group, count, CHARGE);
                    var intensity = ReadColumn(group, count, INTENSITY);
                    var basePeak = ReadColumn(group, count, BASE_PEAK_MZ);
                    var correlation = ReadColumn(group, count, CORRELATION);
                    var low = ReadColumn(group, count, LOW_MZ);
                    var high = ReadColumn(group, count, HIGH_MZ);
                    var feature = ReadColumn(group, count, FEATURE);
                    var scanSignal = ReadColumn(group, count, SCAN_SIGNAL);
                    var scanSignal2Plus = ReadColumn(group, count, SCAN_SIGNAL_2PLUS);
                    for (var r = 0; r < scan.Length; r++)
                    {
                        if (!rows.TryGetValue(scan[r], out var list))
                            throw new InvalidDataException($@"Envelope in scan {scan[r]}, which no series searched.");
                        list.Add((new EnvelopeKey(mass[r], charge[r], intensity[r]), new EnvelopeDetail(basePeak[r], correlation[r], low[r], high[r]), feature[r]));
                        all[scan[r]] = scanSignal[r];
                        multiplyCharged[scan[r]] = scanSignal2Plus[r];
                    }
                }
            }

            var envelopes = new Dictionary<int, ScanEnvelopes>();
            var featureOf = new Dictionary<int, int[]>();
            foreach (var (scan, list) in rows)
            {
                featureOf[scan] = list.Select(x => x.Feature).ToArray();
                envelopes[scan] = list.Count == 0
                    ? ScanEnvelopes.Empty
                    : ScanEnvelopes.Build(list.Select(x => x.Key).ToArray(), list.Select(x => x.Detail).ToArray(),
                        list.Select(_ => Array.Empty<int>()).ToArray(), Array.Empty<int>(), Array.Empty<float>());
            }

            // The features, from their envelopes: the seed is the most intense.
            var position = new Dictionary<int, (int Series, int Position)>();
            for (var s = 0; s < seriesList.Length; s++)
            {
                for (var k = 0; k < seriesList[s].Length; k++)
                    position[seriesList[s][k]] = (s, k);
            }
            var members = new SortedDictionary<int, List<(int Position, EnvelopeKey Key, int Series)>>();
            foreach (var (scan, ids) in featureOf)
            {
                for (var e = 0; e < ids.Length; e++)
                {
                    if (ids[e] < 0)
                        continue;
                    if (!members.TryGetValue(ids[e], out var m))
                        members[ids[e]] = m = new List<(int Position, EnvelopeKey Key, int Series)>();
                    m.Add((position[scan].Position, envelopes[scan].Keys[e], position[scan].Series));
                }
            }
            var features = new List<(int Series, PersistentFeature Feature)>();
            foreach (var (id, m) in members)
            {
                if (id != features.Count)
                    throw new InvalidDataException($@"Feature {id} out of sequence.");
                var seed = m.OrderByDescending(x => x.Key.Intensity).ThenBy(x => x.Position).First();
                features.Add((seed.Series, new PersistentFeature(seed.Key.MonoMass, seed.Key.Charge, seed.Position, seed.Key.Intensity,
                    m.Min(x => x.Position), m.Max(x => x.Position), m.Count, Kronik.SummedIntensity(m.Select(x => (x.Position, x.Key.Intensity))))));
            }
            return new PeptideLikeSignal(headers, envelopes, featureOf, features, seriesList, dda, all, multiplyCharged);
        }

        private static T[] ReadColumn<T>(ParquetRowGroupReader group, int count, DataField<T> field) where T : struct
        {
            var values = new T[count];
            RunSync(group.ReadAsync(field, values.AsMemory()));
            return values;
        }

        // Parquet.Net is asynchronous only. GetAwaiter().GetResult() rethrows the original exception rather than
        // an AggregateException, as an await would, and is safe here because no SynchronizationContext is captured.
        private static T RunSync<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        private static void RunSync(Task task)
        {
            task.GetAwaiter().GetResult();
        }

        private static void RunSync(ValueTask task)
        {
            task.GetAwaiter().GetResult();
        }
    }
}
