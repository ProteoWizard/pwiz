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

using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The per-file resume test shared by every task that reuses outputs file by file: is this
    /// output already on disk, written by this task, by this build, with this validity key?
    ///
    /// <para>The answer is read from the output itself. Every pipeline artifact embeds its
    /// <see cref="pwiz.Osprey.Core.ArtifactStamp"/> in the same atomic commit as its content, so
    /// there is nothing to clear before a recompute and nothing to stamp after it: a stale
    /// output is simply overwritten, and an interrupted write leaves the previous file or none.</para>
    /// </summary>
    internal static class PerFileResumeDriver
    {
        /// <summary>
        /// True when <paramref name="outputPath"/> exists and carries a stamp current for
        /// <paramref name="taskName"/> and <paramref name="validityKey"/> - i.e. the output can be
        /// reused instead of recomputed.
        /// </summary>
        internal static bool IsCurrent(string outputPath, string taskName, string validityKey)
        {
            return ArtifactValidity.IsCurrent(outputPath, taskName, validityKey);
        }
    }
}
