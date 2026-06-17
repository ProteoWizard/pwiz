/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// The per-file resume sidecar mechanics shared by the two per-file tasks
    /// (<see cref="PerFileScoringTask"/> and <see cref="PerFileRescoreTask"/>):
    /// probe an output for a current validity sidecar, clear a stale one before
    /// recomputing, and stamp a fresh one after a successful write. Each task
    /// keeps its own load / compute / output shape; only the
    /// <see cref="TaskValiditySidecar"/> dance is centralized here so the two
    /// tasks cannot drift on it.
    ///
    /// This is sidecar mechanics only -- it never touches the task's actual
    /// output file. (A task that must delete a failed output, e.g. the rescore
    /// task removing a partially-written reconciled parquet, does that itself.)
    /// </summary>
    internal static class PerFileResumeDriver
    {
        /// <summary>
        /// True when <paramref name="outputPath"/> exists on disk AND carries a
        /// validity sidecar for <paramref name="taskName"/> whose key matches
        /// <paramref name="validityKey"/> -- i.e. the output is durably present
        /// and can be reused instead of recomputed. The file-existence gate
        /// matters: a sidecar can outlive its output (or a different invocation
        /// may have left one), so a matching key alone is not enough.
        /// </summary>
        internal static bool IsCurrent(string outputPath, string taskName, string validityKey)
        {
            return File.Exists(outputPath)
                && TaskValiditySidecar.IsValid(outputPath, taskName, validityKey);
        }

        /// <summary>
        /// Remove any validity sidecar for the (output, task) pair so a stale one
        /// cannot mark a partially-written or superseded output as valid. Called
        /// before recomputing an output and after a failed write. Best-effort;
        /// never throws.
        /// </summary>
        internal static void ClearStale(string outputPath, string taskName)
        {
            TaskValiditySidecar.Delete(outputPath, taskName);
        }

        /// <summary>
        /// Stamp a fresh validity sidecar next to <paramref name="outputPath"/>
        /// after a successful write. A sidecar-write failure is non-fatal -- the
        /// output is already on disk; we just lose the ability to skip it on a
        /// later resume -- so the failure is logged via
        /// <paramref name="logWarning"/> and swallowed rather than thrown.
        /// </summary>
        internal static void Stamp(string outputPath, string taskName, string version,
            string validityKey, IEnumerable<string> inputs, Action<string> logWarning)
        {
            try
            {
                TaskValiditySidecar.Write(outputPath, taskName, version, validityKey, inputs);
            }
            catch (Exception ex)
            {
                logWarning(string.Format(
                    @"  Failed to write {0} sidecar for {1}: {2}",
                    taskName, outputPath, ex.Message));
            }
        }
    }
}
