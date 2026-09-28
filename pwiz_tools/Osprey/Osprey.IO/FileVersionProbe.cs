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
    /// on every task and load, so the answer is remembered - but only an answer: a probe that
    /// throws, because the file is locked or still being copied, returns false and is asked
    /// again next time, so a transient failure does not stand for the rest of the process.
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
        /// The probe's answer for the current version of <paramref name="path"/>; false for a
        /// missing file or one the probe cannot read.
        /// </summary>
        public bool Ask(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            var info = new FileInfo(path);
            string version = string.Format(CultureInfo.InvariantCulture, @"{0}|{1}|{2}",
                info.FullName, info.Length, info.LastWriteTimeUtc.Ticks);
            if (_answers.TryGetValue(version, out bool answer))
                return answer;
            try
            {
                answer = _probe(path);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return false;
            }
            _answers[version] = answer;
            return answer;
        }
    }
}
