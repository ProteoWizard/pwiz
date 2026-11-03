/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Data;
using System.Globalization;
using System.Linq;
using pwiz.BiblioSpec.IO;
using pwiz.Common.Database;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// Marks a predicted library's decoys and entrapment peptides the way Osprey's own FASTA route
    /// marks them: the blib's DecoyPairs table (<see cref="BlibSchema.CreateDecoyPairs"/>), one row
    /// per spectrum, a decoy's row pointing at its target's. Build Library predicts for a document
    /// that may carry Skyline-generated decoys (each a shuffle of one target, which the decoy
    /// remembers) and the peptides of an entrapment FASTA; once the library is written, this adds
    /// the table, so Osprey can search the library with its own decoys (<c>--decoys-in-library</c>)
    /// and an entrapment analysis knows which peptides are foreign.
    /// </summary>
    public static class LibraryDecoyPairs
    {
        private readonly struct SpectrumKey : IEquatable<SpectrumKey>
        {
            public SpectrumKey(string sequence, int charge)
            {
                Sequence = sequence;
                Charge = charge;
            }

            public string Sequence { get; }
            public int Charge { get; }

            public bool Equals(SpectrumKey other)
            {
                return Sequence == other.Sequence && Charge == other.Charge;
            }

            public override bool Equals(object obj)
            {
                return obj is SpectrumKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return ((Sequence?.GetHashCode() ?? 0) * 397) ^ Charge;
            }
        }

        /// <summary>
        /// Writes the table for <paramref name="document"/>'s precursors into the library at
        /// <paramref name="blibPath"/>: every target precursor found in the library gets a row, with
        /// <see cref="BlibSchema"/>'s IsEntrapment set for the peptides in
        /// <paramref name="entrapmentTargets"/>; every decoy precursor found gets a row pointing at
        /// its target's. A precursor is found by its unmodified sequence and charge, which the
        /// shuffled decoys and a digest's peptides make unique; one that is not unique, or that the
        /// prediction dropped, gets no row. Returns the number of rows written.
        /// </summary>
        public static int Write(string blibPath, SrmDocument document, ICollection<Target> entrapmentTargets, string decoyMethod)
        {
            var entrapment = new HashSet<string>((entrapmentTargets ?? new List<Target>()).Select(t => t.Sequence));
            using var connection = SqliteOperations.OpenConnection(blibPath);
            var ids = ReadSpectrumIds(connection);
            var rows = new List<object[]>();
            int pairingId = 0;
            // The targets first, each with its pairing id, then the decoys pointing at them
            var targetPairings = new Dictionary<SpectrumKey, KeyValuePair<long, int>>();
            foreach (var nodePep in document.Peptides.Where(p => !p.IsDecoy))
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    var key = new SpectrumKey(nodePep.Target.Sequence, nodeGroup.PrecursorCharge);
                    if (!ids.TryGetValue(key, out long id) || targetPairings.ContainsKey(key))
                        continue;
                    pairingId++;
                    targetPairings.Add(key, new KeyValuePair<long, int>(id, pairingId));
                    rows.Add(FormatRow(id, false, entrapment.Contains(key.Sequence), pairingId, null, decoyMethod));
                }
            }
            foreach (var nodePep in document.Peptides.Where(p => p.IsDecoy))
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    var key = new SpectrumKey(nodePep.Target.Sequence, nodeGroup.PrecursorCharge);
                    var targetKey = new SpectrumKey(nodePep.SourceUnmodifiedTarget.Sequence, nodeGroup.PrecursorCharge);
                    if (!ids.TryGetValue(key, out long id) || !targetPairings.TryGetValue(targetKey, out var target))
                        continue;
                    rows.Add(FormatRow(id, true, entrapment.Contains(targetKey.Sequence), target.Value, target.Key, decoyMethod));
                }
            }
            if (!SqliteOperations.TableExists(connection, BlibSchema.TABLE_DECOY_PAIRS))
            {
                Execute(connection, BlibSchema.CreateDecoyPairs);
                Execute(connection, BlibSchema.CreateIdxDecoyPairsTarget);
            }
            // One prepared statement for every row: a FASTA build with decoys and entrapment has
            // many thousands, and a statement parsed per row is a stall after the build
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT OR REPLACE INTO DecoyPairs (RefSpectraID, IsDecoy, IsEntrapment, PairingID, TargetRefSpectraID, Method) " +
                                      @"VALUES (@id, @isDecoy, @isEntrapment, @pairingId, @targetId, @method)";
                var parameters = new[] { @"@id", @"@isDecoy", @"@isEntrapment", @"@pairingId", @"@targetId", @"@method" }
                    .Select(name =>
                    {
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = name;
                        command.Parameters.Add(parameter);
                        return parameter;
                    }).ToArray();
                foreach (var row in rows)
                {
                    for (int i = 0; i < row.Length; i++)
                        parameters[i].Value = row[i];
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            return rows.Count;
        }

        /// <summary>The counts of decoy and entrapment rows in a library's table, for tests; zero when there is no table.</summary>
        public static void Count(string blibPath, out int decoys, out int entrapment)
        {
            decoys = entrapment = 0;
            using var connection = SqliteOperations.OpenConnection(blibPath);
            if (!SqliteOperations.TableExists(connection, BlibSchema.TABLE_DECOY_PAIRS))
                return;
            decoys = ExecuteCount(connection, @"SELECT COUNT(*) FROM DecoyPairs WHERE IsDecoy = 1");
            entrapment = ExecuteCount(connection, @"SELECT COUNT(*) FROM DecoyPairs WHERE IsEntrapment = 1 AND IsDecoy = 0");
        }

        /// <summary>One row's values, in the insert statement's parameter order.</summary>
        private static object[] FormatRow(long id, bool isDecoy, bool isEntrapment, int pairingId, long? targetId, string method)
        {
            return new object[]
            {
                id, isDecoy ? 1 : 0, isEntrapment ? 1 : 0, pairingId,
                targetId.HasValue ? targetId.Value : DBNull.Value,
                method != null ? method : DBNull.Value
            };
        }

        /// <summary>The library's spectra by unmodified sequence and charge; a sequence and charge with more than one spectrum is left out, since the pairing could not name one.</summary>
        private static Dictionary<SpectrumKey, long> ReadSpectrumIds(IDbConnection connection)
        {
            var ids = new Dictionary<SpectrumKey, long>();
            var ambiguous = new HashSet<SpectrumKey>();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT id, peptideSeq, precursorCharge FROM RefSpectra";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = new SpectrumKey(reader.GetString(1), reader.GetInt32(2));
                if (ambiguous.Contains(key))
                    continue;
                if (ids.ContainsKey(key))
                {
                    ids.Remove(key);
                    ambiguous.Add(key);
                    continue;
                }
                ids.Add(key, reader.GetInt64(0));
            }
            return ids;
        }

        private static void Execute(IDbConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static int ExecuteCount(IDbConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }
}
