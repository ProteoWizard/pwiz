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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Quasar.Core.Clustering;

namespace pwiz.Quasar.IO
{
    /// <summary>
    /// Osprey's passing precursors in one run, from the blib it writes: the known templates of
    /// <c>--features unassigned</c>. The run's q-value and boundaries come from the
    /// <c>RetentionTimes</c> table, which has a row per precursor and run; <c>OspreyRunScores</c>
    /// and <c>OspreyPeakBoundaries</c> keep only each precursor's best run.
    /// </summary>
    public static class OspreyRunReader
    {
        public const double DEFAULT_MAX_Q = 0.01;

        /// <summary>
        /// The precursors at or below <paramref name="maxQ"/> in the run whose file name, without
        /// extension, is <paramref name="runName"/>; null when the blib has no such run.
        /// </summary>
        public static List<KnownPrecursor> Read(string blibPath, string runName, double maxQ = DEFAULT_MAX_Q)
        {
            var entries = new BlibLoader(FragmentToleranceConfig.Default()).Load(blibPath)
                .Where(e => !e.IsDecoy)
                .ToDictionary(e => e.Id);
            var precursors = new List<KnownPrecursor>();
            string connection = new SQLiteConnectionStringBuilder { DataSource = blibPath, ReadOnly = true }.ToString();
            using (var conn = new SQLiteConnection(connection))
            {
                conn.Open();
                var sourceId = SourceId(conn, runName);
                if (sourceId == null)
                    return null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT RefSpectraID, startTime, endTime, retentionTime FROM RetentionTimes " +
                                      @"WHERE SpectrumSourceID = @source AND score <= @q AND retentionTime > 0";
                    cmd.Parameters.AddWithValue(@"@source", sourceId.Value);
                    cmd.Parameters.AddWithValue(@"@q", maxQ);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!entries.TryGetValue((uint)reader.GetInt64(0), out var entry))
                                continue;
                            precursors.Add(new KnownPrecursor(entry.ModifiedSequence, entry.PrecursorMz, entry.Charge,
                                reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3),
                                entry.Fragments.Select(f => f.Mz).ToArray()));
                        }
                    }
                }
            }

            return precursors;
        }

        /// <summary>The SpectrumSourceFiles id of the run named <paramref name="runName"/>, or null.</summary>
        private static long? SourceId(SQLiteConnection conn, string runName)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT id, fileName FROM SpectrumSourceFiles";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = Path.GetFileNameWithoutExtension(reader.GetString(1));
                        if (string.Equals(name, runName, StringComparison.OrdinalIgnoreCase))
                            return reader.GetInt64(0);
                    }
                }
            }

            return null;
        }
    }
}
