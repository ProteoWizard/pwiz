/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using pwiz.BiblioSpec;
using pwiz.Common.SystemUtil;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Search
{
    public static class SearchResults
    {
        private static bool HasTable(SQLiteConnection connection, string tableName)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
                cmd.Parameters.AddWithValue("@tableName", tableName);
                var rs = cmd.ExecuteReader();
                rs.Read();
                return 1 == Convert.ToInt32(rs.GetValue(0));
            }
        }

        public static List<SearchResult> ReadSearchResultsViaBiblioSpec(String filename, Func<int, bool> progressMonitor)
        {

            string tempFile = null;
            try
            {
                tempFile = Path.GetTempFileName();
                var blibBuild = new BlibBuild(tempFile, new[] {filename})
                                    {
                                        CompressLevel = 0,
                                    };
                var status = new ProgressStatus("");
                var progressMonitorImpl = ProgressMonitorImpl.NewProgressMonitorImpl(status, progressMonitor);
                string[] ambiguousPeptides;
                blibBuild.BuildLibrary(LibraryBuildAction.Create, progressMonitorImpl, ref status, out ambiguousPeptides);
                return ReadBiblioSpecDatabase(tempFile, progressMonitor);
            }
            finally
            {
                try
                {
                    if (tempFile != null)
                    {
                        File.Delete(tempFile);
                    }
                }
                catch (Exception exception)
                {
                    Trace.TraceWarning("Exception deleting temp file {0}", exception);
                }
            }
        }

        public static List<SearchResult> ReadBiblioSpecDatabase(string biblioSpecFile, Func<int, bool> progressMonitor)
        {
            const string sqlSelectFromRefSpectra =
                "SELECT S.peptideSeq, S.precursorMZ, S.precursorCharge, S.peptideModSeq,"
                + "\nS.retentionTime, F.fileName"
                + "\nFROM RefSpectra S"
                + "\nINNER JOIN SpectrumSourceFiles F ON S.fileID = F.id";
            const string sqlSelectFromRetentionTimes =
                "SELECT S.peptideSeq, S.precursorMZ, S.precursorCharge, S.peptideModSeq,"
                + "\nR.retentionTime, F.fileName"
                + "\nFROM RetentionTimes R"
                + "\nINNER JOIN RefSpectra S ON R.RefSpectraID = S.id"
                + "\nINNER JOIN SpectrumSourceFiles F ON R.SpectrumSourceID = F.id";

            var results = new List<SearchResult>();
            using (
                var connection =
                    new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource = biblioSpecFile}.ToString()))
            {
                connection.Open();
                var commandTexts = new List<string> {sqlSelectFromRefSpectra};
                if (HasTable(connection, "RetentionTimes"))
                {
                    commandTexts.Add(sqlSelectFromRetentionTimes);
                }
                foreach (var commandText in commandTexts)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = commandText;
                    var rs = command.ExecuteReader();
                    while (rs.Read())
                    {
                        var fileName = rs.GetString(5);
                        fileName = Path.GetFileName(fileName) ?? fileName;
                        int idxDot = fileName.IndexOf('.');
                        if (idxDot > 0)
                        {
                            fileName = fileName.Substring(0, idxDot);
                        }
                        results.Add(new SearchResult(rs.GetString(0))
                                        {
                                            PrecursorMz = rs.GetDouble(1),
                                            PrecursorCharge = rs.GetInt32(2),
                                            ModifiedSequence = rs.GetString(3),
                                            RetentionTime = rs.GetDouble(4),
                                            Filename = fileName,
                                        });
                    }
                }
            }
            return results;
        }
    }

    public class SearchResult
    {
        public SearchResult(String sequenceWithMods)
        {
            Sequence = sequenceWithMods.Replace("*", "");
            ModifiedSequence = sequenceWithMods;
        }
        public String Sequence { get; set; }
        public string ModifiedSequence { get; set; }
        public String Filename { get; set; }
        public double RetentionTime { get; set; }
        public int? PrecursorCharge { get; set; }
        public double? PrecursorMz { get; set; }
    }
}
