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
                return FdrScoresSidecar.ReadStamp(path, PassFromName(path));
            if (EndsWith(path, FdrExperimentSidecar.EXT))
                return FdrExperimentSidecar.ReadStamp(path, PassFromName(path));
            if (EndsWith(path, Pass2CompetitionDecoys.EXT))
                return Pass2CompetitionDecoys.ReadStamp(path);
            if (EndsWith(path, RetainedBaseIdSidecar.EXT))
                return RetainedBaseIdSidecar.ReadStamp(path);
            if (EndsWith(path, BLIB_EXT))
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
        /// The pass a first- or second-pass artifact belongs to, from the pass label every such
        /// file name carries (<see cref="FdrScoresSidecar.LABEL_FIRST_PASS"/> or
        /// <see cref="FdrScoresSidecar.LABEL_SECOND_PASS"/>).
        /// </summary>
        private static FdrScoresSidecar.Pass PassFromName(string path)
        {
            string name = Path.GetFileName(path);
            return name.IndexOf(@"." + FdrScoresSidecar.LABEL_SECOND_PASS + @".", StringComparison.OrdinalIgnoreCase) >= 0
                ? FdrScoresSidecar.Pass.SecondPass
                : FdrScoresSidecar.Pass.FirstPass;
        }
    }
}
