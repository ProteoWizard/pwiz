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
using System.IO;
using System.Text;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Reads the <see cref="ArtifactStamp"/> a pipeline artifact carries inside itself, whatever
    /// its format, so a resume check, the pipeline driver and a cross-task "who wrote this?"
    /// question all ask one place. The format is recognized by the artifact's file-name ending,
    /// which every artifact family owns (see each family's <c>EXT</c>).
    /// </summary>
    public static class ArtifactValidity
    {
        private const string JSON_EXT = @".json";
        private const string HTML_EXT = @".html";
        private const string PARQUET_EXT = @".parquet";
        private const string BLIB_EXT = @".blib";

        // "SQLite format 3" plus its NUL terminator, the first 16 bytes of every SQLite 3 file.
        private static readonly byte[] SQLITE_HEADER = Encoding.ASCII.GetBytes(@"SQLite format 3" + '\0');

        /// <summary>
        /// The stamp inside the artifact at <paramref name="path"/>, or null when the file is
        /// missing, unreadable, unstamped or of a kind that carries no stamp. Never throws.
        /// </summary>
        public static ArtifactStamp ReadStamp(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            if (EndsWith(path, PARQUET_EXT))
                return ParquetScoreCache.ReadStamp(path);
            if (EndsWith(path, JSON_EXT))
                return ArtifactStamp.TryReadJsonHead(path);
            if (EndsWith(path, HTML_EXT))
                return ArtifactStamp.TryReadHtmlHead(path);
            if (EndsWith(path, FdrScoresSidecar.EXT))
                return FdrScoresSidecar.ReadStamp(path, PassFromName(path, FdrScoresSidecar.EXT));
            if (EndsWith(path, FdrExperimentSidecar.EXT))
                return FdrExperimentSidecar.ReadStamp(path, PassFromName(path, FdrExperimentSidecar.EXT));
            if (EndsWith(path, Pass2CompetitionDecoys.EXT))
                return Pass2CompetitionDecoys.ReadStamp(path);
            if (EndsWith(path, RetainedBaseIdSidecar.EXT))
                return RetainedBaseIdSidecar.ReadStamp(path);
            // The output library is named by -o, verbatim, so it need not end in .blib. Any
            // other SQLite file is read the same way rather than reading as unstamped, which
            // would re-run the whole of SecondPassFDR on every invocation.
            if (EndsWith(path, BLIB_EXT) || HasSqliteHeader(path))
                return BlibWriter.ReadStamp(path);
            return null;
        }

        /// <summary>
        /// True when the artifact at <paramref name="path"/> exists and carries a stamp that is
        /// current for <paramref name="task"/> and <paramref name="key"/> - see
        /// <see cref="ArtifactStamp.IsCurrent"/>. The one test for "may this run reuse it".
        /// </summary>
        public static bool IsCurrent(string path, string task, string key)
        {
            var stamp = ReadStamp(path);
            return stamp != null && stamp.IsCurrent(task, key);
        }

        private static bool EndsWith(string path, string ending)
        {
            return path.EndsWith(ending, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether the file starts with the 16-byte SQLite 3 header. Never throws.
        /// </summary>
        private static bool HasSqliteHeader(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var header = new byte[SQLITE_HEADER.Length];
                    return fs.Read(header, 0, header.Length) == header.Length &&
                           header.AsSpan().SequenceEqual(SQLITE_HEADER);
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return false;
            }
        }

        /// <summary>
        /// The pass a first- or second-pass artifact belongs to, from the pass label every such
        /// file name carries immediately before its <paramref name="ext"/>
        /// (<c>&lt;stem&gt;.2nd-pass.fdr_scores.bin</c>). Only that position counts: the stem is
        /// the user's input or output name, and one that itself contains <c>.2nd-pass.</c> must
        /// not turn a first-pass artifact into a second-pass one.
        /// </summary>
        private static FdrScoresSidecar.Pass PassFromName(string path, string ext)
        {
            return EndsWith(path, @"." + FdrScoresSidecar.LABEL_SECOND_PASS + ext)
                ? FdrScoresSidecar.Pass.SecondPass
                : FdrScoresSidecar.Pass.FirstPass;
        }
    }
}
