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
using System.Threading.Tasks;
using Parquet;
using Parquet.Schema;
using pwiz.Quasar.Core.Output;

namespace pwiz.Quasar.IO
{
    /// <summary>
    /// Writes the clusters sidecar (spec Stage 4, <c>&lt;out&gt;.clusters.parquet</c>): one row per
    /// member of every accepted cluster. Windows are written as they finish, one row group each,
    /// so a run's members are never all held at once; the window column says which is which.
    /// </summary>
    public sealed class ClustersSidecarWriter : IDisposable
    {
        public const string FORMAT_VERSION = @"1";
        public const string KEY_FORMAT_VERSION = @"quasar_clusters_format";

        private static readonly DataField<int> WINDOW = new DataField<int>(@"window");
        private static readonly DataField<int> CLUSTER = new DataField<int>(@"cluster");
        private static readonly DataField SPECTRUM_ID = new DataField(@"spectrum_id", typeof(string), isNullable: true);
        private static readonly DataField<int> TRACE_ID = new DataField<int>(@"trace_id");
        private static readonly DataField<double> MZ = new DataField<double>(@"mz");
        private static readonly DataField<int> CHARGE = new DataField<int>(@"charge");
        private static readonly DataField<bool> IS_ANCHOR = new DataField<bool>(@"is_anchor");
        private static readonly DataField<double> AREA = new DataField<double>(@"area");
        private static readonly DataField<double> SHARED_FRACTION = new DataField<double>(@"shared_fraction");
        private static readonly DataField<double> DELTA_CHI_SQUARE = new DataField<double>(@"delta_chi_square");
        private static readonly DataField<double> APEX_RT = new DataField<double>(@"apex_rt");
        private static readonly DataField<double> START_RT = new DataField<double>(@"start_rt");
        private static readonly DataField<double> END_RT = new DataField<double>(@"end_rt");

        private static readonly ParquetSchema SCHEMA = new ParquetSchema(WINDOW, CLUSTER, SPECTRUM_ID, TRACE_ID, MZ,
            CHARGE, IS_ANCHOR, AREA, SHARED_FRACTION, DELTA_CHI_SQUARE, APEX_RT, START_RT, END_RT);

        private readonly object _lock = new object();
        private readonly FileStream _stream;
        private readonly ParquetWriter _writer;

        /// <summary>Starts <paramref name="path"/>, with <paramref name="metadata"/> (tool, version, settings) in its footer.</summary>
        public ClustersSidecarWriter(string path, IDictionary<string, string> metadata)
        {
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            var options = new ParquetOptions
            {
                CompressionMethod = CompressionMethod.Zstd,
                CompressionLevel = System.IO.Compression.CompressionLevel.Optimal
            };
            _writer = RunSync(ParquetWriter.CreateAsync(SCHEMA, _stream, options));
            _writer.CustomMetadata = new Dictionary<string, string>(metadata) { [KEY_FORMAT_VERSION] = FORMAT_VERSION };
        }

        /// <summary>Writes one window's rows as a row group; safe to call from several threads.</summary>
        public void Write(IReadOnlyList<ClusterMemberRow> rows)
        {
            if (rows.Count == 0)
                return;
            lock (_lock)
            {
                using (var group = _writer.CreateRowGroup())
                {
                    WriteColumn(group, WINDOW, rows, r => r.Window);
                    WriteColumn(group, CLUSTER, rows, r => r.Cluster);
                    RunSync(group.WriteAsync(SPECTRUM_ID, rows.Select(r => r.SpectrumId).ToArray()));
                    WriteColumn(group, TRACE_ID, rows, r => r.TraceId);
                    WriteColumn(group, MZ, rows, r => r.Mz);
                    WriteColumn(group, CHARGE, rows, r => r.Charge);
                    WriteColumn(group, IS_ANCHOR, rows, r => r.IsAnchor);
                    WriteColumn(group, AREA, rows, r => r.Area);
                    WriteColumn(group, SHARED_FRACTION, rows, r => r.SharedFraction);
                    WriteColumn(group, DELTA_CHI_SQUARE, rows, r => r.DeltaChiSquare);
                    WriteColumn(group, APEX_RT, rows, r => r.ApexRt);
                    WriteColumn(group, START_RT, rows, r => r.StartRt);
                    WriteColumn(group, END_RT, rows, r => r.EndRt);
                }
            }
        }

        /// <summary>Writes the footer and closes the file.</summary>
        public void Dispose()
        {
            RunSync(_writer.DisposeAsync());
            _stream.Dispose();
        }

        /// <summary>Reads a sidecar back, for tests and tools.</summary>
        public static List<ClusterMemberRow> Read(string path, out Dictionary<string, string> metadata)
        {
            var rows = new List<ClusterMemberRow>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var reader = RunSync(ParquetReader.CreateAsync(stream));
                try
                {
                    metadata = new Dictionary<string, string>(reader.CustomMetadata);
                    for (int g = 0; g < reader.RowGroupCount; g++)
                    {
                        using (var group = reader.OpenRowGroupReader(g))
                        {
                            int n = (int)group.RowCount;
                            var window = Read<int>(group, WINDOW, n);
                            var cluster = Read<int>(group, CLUSTER, n);
                            var spectrumId = new string[n];
                            RunSync(group.ReadAsync(SPECTRUM_ID, spectrumId.AsMemory()));
                            var traceId = Read<int>(group, TRACE_ID, n);
                            var mz = Read<double>(group, MZ, n);
                            var charge = Read<int>(group, CHARGE, n);
                            var isAnchor = Read<bool>(group, IS_ANCHOR, n);
                            var area = Read<double>(group, AREA, n);
                            var shared = Read<double>(group, SHARED_FRACTION, n);
                            var deltaChiSquare = Read<double>(group, DELTA_CHI_SQUARE, n);
                            var apex = Read<double>(group, APEX_RT, n);
                            var start = Read<double>(group, START_RT, n);
                            var end = Read<double>(group, END_RT, n);
                            for (int i = 0; i < n; i++)
                                rows.Add(new ClusterMemberRow(window[i], cluster[i], spectrumId[i], traceId[i], mz[i],
                                    charge[i], isAnchor[i], area[i], shared[i], deltaChiSquare[i], apex[i], start[i], end[i]));
                        }
                    }
                }
                finally
                {
                    RunSync(reader.DisposeAsync());
                }
            }

            return rows;
        }

        private static void WriteColumn<T>(ParquetRowGroupWriter group, DataField field, IReadOnlyList<ClusterMemberRow> rows,
            Func<ClusterMemberRow, T> get) where T : struct
        {
            var values = new T[rows.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = get(rows[i]);
            RunSync(group.WriteAsync(field, new ReadOnlyMemory<T>(values)));
        }

        private static T[] Read<T>(ParquetRowGroupReader group, DataField field, int count) where T : struct
        {
            var values = new T[count];
            RunSync(group.ReadAsync(field, values.AsMemory()));
            return values;
        }

        // Synchronously bridge Parquet.Net's async API, as Osprey's parquet code does: no
        // SynchronizationContext is captured here, so GetResult cannot deadlock.
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
