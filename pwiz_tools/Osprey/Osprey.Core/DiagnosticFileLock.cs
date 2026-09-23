/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Sonnet 5) <noreply .at. anthropic.com>
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

using System.Collections.Concurrent;
using System.IO;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// A per-path lock for coordinating IN-PROCESS writers that share one
    /// diagnostic dump file. Two `-d` dumps can target the same real path from
    /// different call sites (e.g. a per-entry search-XIC dump written once early
    /// in scoring and appended to later in the same method), and under
    /// <c>--parallel-files</c> the same library entry can be scored on more than
    /// one file-thread at once. <see cref="FileSaver"/> gives atomic commits, not
    /// mutual exclusion between independent <c>FileSaver</c> instances aimed at
    /// the same path -- this closes that gap for writers in this process.
    ///
    /// Deliberately process-local only. True multi-node HPC fan-out sharing one
    /// output directory is NOT protected (a second process holds its own lock
    /// object) -- acceptable here because every dump this guards is a `-d` /
    /// <c>OSPREY_DUMP_*</c> opt-in for an interactive bisection session, not
    /// something turned on during a real production fan-out run.
    /// </summary>
    public static class DiagnosticFileLock
    {
        private static readonly ConcurrentDictionary<string, object> s_locks =
            new ConcurrentDictionary<string, object>();

        /// <summary>
        /// The lock object for <paramref name="path"/>, keyed by its full path so
        /// callers that pass a relative and an absolute spelling of the same file
        /// still serialize against each other.
        /// </summary>
        public static object For(string path)
        {
            string key = Path.GetFullPath(path);
            return s_locks.GetOrAdd(key, _ => new object());
        }
    }
}
