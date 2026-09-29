/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Compares the Osprey-written tables of two .blib files row by row, for these tests and for
    /// <c>Compare-BlibFull</c> in the regression's <c>Regression\BlibGolden.ps1</c>, which compiles
    /// this file with <c>Add-Type</c>. Keep it to the BCL and System.Data.SQLite, which is all that
    /// compile references; nothing else in this assembly is there. Rows are
    /// keyed by precursor (peptideModSeq, charge) and run, never by database id, so two libraries
    /// written in a different order still compare; doubles compare at an absolute tolerance and
    /// every other value exactly. Source files are keyed by file name without directory or
    /// extension, because an HPC phase names its inputs from a different directory.
    /// </summary>
    public static class BlibComparer
    {
        // Each projection lists its key columns first; KeyCount says how many.
        private static readonly Projection[] PROJECTIONS =
        {
            new Projection(@"RefSpectra", 2,
                @"SELECT peptideModSeq, precursorCharge, precursorMZ, retentionTime, startTime, endTime, score, ionMobility, " +
                @"peptideSeq, prevAA, nextAA, copies, numPeaks, scoreType FROM RefSpectra"),
            new Projection(@"RetentionTimes", 3,
                @"SELECT r.peptideModSeq, r.precursorCharge, s.fileName, t.retentionTime, t.startTime, t.endTime, " +
                @"t.score, t.bestSpectrum FROM RetentionTimes t JOIN RefSpectra r ON t.RefSpectraID = r.id " +
                @"JOIN SpectrumSourceFiles s ON t.SpectrumSourceID = s.id"),
            new Projection(@"OspreyRunScores", 3,
                @"SELECT r.peptideModSeq, r.precursorCharge, o.FileName, o.RunQValue, o.DiscriminantScore, " +
                @"o.PosteriorErrorProb FROM OspreyRunScores o JOIN RefSpectra r ON o.RefSpectraID = r.id"),
            new Projection(@"OspreyExperimentScores", 2,
                @"SELECT r.peptideModSeq, r.precursorCharge, o.ExperimentQValue, o.NRunsDetected, o.NRunsSearched " +
                @"FROM OspreyExperimentScores o JOIN RefSpectra r ON o.RefSpectraID = r.id"),
            new Projection(@"OspreyPeakBoundaries", 3,
                @"SELECT r.peptideModSeq, r.precursorCharge, o.FileName, o.StartRT, o.EndRT, o.ApexRT, " +
                @"o.ApexIntensity, o.IntegratedArea FROM OspreyPeakBoundaries o JOIN RefSpectra r ON o.RefSpectraID = r.id"),
            new Projection(@"OspreyCoefficients", 4,
                @"SELECT r.peptideModSeq, r.precursorCharge, o.FileName, o.ScanNumber, o.RT, o.Coefficient " +
                @"FROM OspreyCoefficients o JOIN RefSpectra r ON o.RefSpectraID = r.id"),
            new Projection(@"RefSpectraProteins", 3,
                @"SELECT r.peptideModSeq, r.precursorCharge, p.accession FROM RefSpectraProteins rp " +
                @"JOIN RefSpectra r ON rp.RefSpectraID = r.id JOIN Proteins p ON rp.ProteinID = p.id"),
            new Projection(@"Modifications", 3,
                @"SELECT r.peptideModSeq, r.precursorCharge, m.position, m.mass FROM Modifications m " +
                @"JOIN RefSpectra r ON m.RefSpectraID = r.id"),
            new Projection(@"Proteins", 1, @"SELECT accession FROM Proteins"),
            new Projection(@"SpectrumSourceFiles", 1,
                @"SELECT fileName, idFileName, cutoffScore, workflowType FROM SpectrumSourceFiles"),
            new Projection(@"OspreyMetadata", 1, @"SELECT Key, Value FROM OspreyMetadata"),
            new Projection(@"RefSpectraPeaks", 2,
                @"SELECT r.peptideModSeq, r.precursorCharge, p.peakMZ, p.peakIntensity FROM RefSpectraPeaks p " +
                @"JOIN RefSpectra r ON p.RefSpectraID = r.id")
        };

        /// <summary>
        /// Every difference between the two libraries, or an empty list when they agree at
        /// <paramref name="tolerance"/>. Each entry names the table and the key it concerns.
        /// </summary>
        public static IList<string> Compare(string expectedBlib, string actualBlib, double tolerance)
        {
            var differences = new List<string>();
            foreach (var projection in PROJECTIONS)
            {
                var expected = ReadRows(expectedBlib, projection);
                var actual = ReadRows(actualBlib, projection);
                foreach (var key in expected.Keys.Where(k => !actual.ContainsKey(k)))
                    differences.Add(string.Format(@"{0}: missing row {1}", projection.Table, key));
                foreach (var key in actual.Keys.Where(k => !expected.ContainsKey(k)))
                    differences.Add(string.Format(@"{0}: extra row {1}", projection.Table, key));
                foreach (var pair in expected.Where(p => actual.ContainsKey(p.Key)))
                {
                    string difference = CompareValues(pair.Value, actual[pair.Key], tolerance);
                    if (difference != null)
                        differences.Add(string.Format(@"{0} {1}: {2}", projection.Table, pair.Key, difference));
                }
            }
            return differences;
        }

        /// <summary>
        /// The number of rows in <paramref name="table"/>'s projection.
        /// </summary>
        public static int CountRows(string blib, string table)
        {
            return ReadRows(blib, PROJECTIONS.First(p => p.Table == table)).Count;
        }

        /// <summary>
        /// The number of rows of <paramref name="table"/> matching the SQL condition <paramref name="where"/>.
        /// </summary>
        public static int CountWhere(string blib, string table, string where)
        {
            using (var conn = new SQLiteConnection(@"Data Source=" + blib + @";Version=3;Read Only=True;Pooling=False;"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(@"SELECT COUNT(*) FROM " + table + @" WHERE " + where, conn))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
            }
        }

        private static Dictionary<string, object[]> ReadRows(string blib, Projection projection)
        {
            var rows = new Dictionary<string, object[]>();
            using (var conn = new SQLiteConnection(@"Data Source=" + blib + @";Version=3;Read Only=True;Pooling=False;"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(projection.Sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var values = new object[reader.FieldCount];
                        reader.GetValues(values);
                        string key = string.Join(@"|", values.Take(projection.KeyCount).Select(FormatKey));
                        var rowValues = values.Skip(projection.KeyCount).ToArray();
                        // A key can repeat (peaks of one spectrum, several modifications), so
                        // number the repeats in read order, which is id order.
                        string uniqueKey = key;
                        for (int n = 2; rows.ContainsKey(uniqueKey); n++)
                            uniqueKey = key + @"#" + n.ToString(CultureInfo.InvariantCulture);
                        rows.Add(uniqueKey, rowValues);
                    }
                }
            }
            return rows;
        }

        private static string FormatKey(object value)
        {
            if (value is string text && (text.EndsWith(@".mzML", StringComparison.OrdinalIgnoreCase) ||
                                         text.IndexOfAny(new[] { '\\', '/' }) >= 0))
                return Path.GetFileNameWithoutExtension(text);
            return FormatValue(value);
        }

        private static string CompareValues(object[] expected, object[] actual, double tolerance)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] is double e && actual[i] is double a)
                {
                    if (Math.Abs(e - a) > tolerance)
                        return string.Format(CultureInfo.InvariantCulture, @"column {0}: {1:R} != {2:R}", i, e, a);
                }
                else if (!Equals(FormatValue(expected[i]), FormatValue(actual[i])))
                {
                    return string.Format(@"column {0}: {1} != {2}", i, FormatValue(expected[i]), FormatValue(actual[i]));
                }
            }
            return null;
        }

        private static string FormatValue(object value)
        {
            switch (value)
            {
                case null:
                case DBNull _:
                    return string.Empty;
                case byte[] bytes:
                    var hex = new StringBuilder(bytes.Length * 2);
                    foreach (byte b in bytes)
                        hex.Append(b.ToString(@"x2", CultureInfo.InvariantCulture));
                    return hex.ToString();
                case double d:
                    return d.ToString(@"R", CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private sealed class Projection
        {
            public Projection(string table, int keyCount, string sql)
            {
                Table = table;
                KeyCount = keyCount;
                Sql = sql;
            }

            public string Table { get; }
            public int KeyCount { get; }
            public string Sql { get; }
        }
    }
}
