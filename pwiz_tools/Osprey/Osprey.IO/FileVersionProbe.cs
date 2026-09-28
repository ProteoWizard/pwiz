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
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// A yes/no question about a library file's content, asked once per version of the file
    /// (full path, size, mtime) in a process. The validity keys and the library cache ask it
    /// on every task and load, so the answer is remembered - but only an answer. Every question
    /// is phrased so that true adds a key or cache term (the file reads differently since a
    /// reader change), and a probe that throws, because the file is locked or unreadable,
    /// answers true: "I cannot tell" resolves to re-running (P15 in
    /// 00-pipeline-architecture.md), never to adopting an output written before the change.
    /// That answer is not cached, so the next question reads the file again.
    /// </summary>
    internal sealed class FileVersionProbe
    {
        private readonly Func<string, bool> _probe;
        private readonly ConcurrentDictionary<string, bool> _answers =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        /// <param name="probe">Reads the file at the path it is given; may throw.</param>
        public FileVersionProbe(Func<string, bool> probe)
        {
            _probe = probe;
        }

        /// <summary>
        /// The probe's answer for the current version of <paramref name="path"/>: false for a
        /// missing file, which no reader reads; true, uncached, for one the probe cannot read.
        /// </summary>
        public bool Ask(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            string version;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    return false;
                version = string.Format(CultureInfo.InvariantCulture, @"{0}|{1}|{2}",
                    info.FullName, info.Length, info.LastWriteTimeUtc.Ticks);
            }
            catch (IOException)
            {
                // Deleted or moved after the existence check: missing.
                return false;
            }
            if (_answers.TryGetValue(version, out bool answer))
                return answer;
            try
            {
                answer = _probe(path);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return true;
            }
            _answers[version] = answer;
            return answer;
        }
    }
}
