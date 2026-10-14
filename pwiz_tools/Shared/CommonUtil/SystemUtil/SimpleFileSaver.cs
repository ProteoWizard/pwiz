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
using System.IO;
using pwiz.Common.CommonResources;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Atomic file write helper. Construct with the final destination path;
    /// <see cref="SafeName"/> gives a sibling temp path to write into. Call <see cref="Commit"/>
    /// on successful write to rename the temp into the final destination, replacing any file
    /// there; <see cref="Dispose"/> (called on disposal without Commit, e.g. when an exception
    /// unwinds the writing block) deletes the temp without touching the final destination.
    ///
    /// A crash mid-write therefore leaves either the previous final content (if any) or no file
    /// at all - never a partially written destination that a downstream resume check could
    /// mistake for a finished output. The temp is a sibling in the destination directory, so
    /// Commit is an in-volume rename that cannot truncate on a network mount the way a
    /// cross-volume copy can.
    ///
    /// This is the one atomic writer shared by the command-line tools (Osprey, CarafeSharp,
    /// SkylineBatch, AutoQC). It is deliberately the SIMPLE one: Skyline's own
    /// <c>pwiz.Skyline.Util.FileSaver</c> adds a stream manager, read-only checks with dialogs
    /// and undisposed-instance tracking, and keeps its name - a shared class called
    /// <c>FileSaver</c> in this namespace would be ambiguous in the Skyline files that import
    /// both. The history: SharedBatch's Win32 <c>GetTempFileName</c> port, re-done
    /// cross-platform for Osprey, moved here.
    /// </summary>
    public sealed class SimpleFileSaver : IDisposable
    {
        /// <summary>
        /// The prefix of every temp file this class allocates, the same one Skyline's writer
        /// uses, so a stale temp left by a crash is recognizable in any output folder.
        /// </summary>
        public const string TEMP_PREFIX = @"~SK";

        /// <summary>
        /// Forensic opt-in: when true, an uncommitted temp is left in place on disposal instead
        /// of deleted, for a developer inspecting what a write got through before an exception
        /// abandoned it. It stays under its own temp name - Dispose never touches
        /// <see cref="RealName"/> - so a normal reader of the destination sees nothing new.
        /// Off by default; a host that exposes it (Osprey's <c>OSPREY_KEEP_FAILED_WRITES</c>)
        /// sets it at startup.
        /// </summary>
        public static bool KeepFailedWrites { get; set; }

        /// <summary>Sibling temp path the caller writes into.</summary>
        public string SafeName { get; private set; }

        /// <summary>Final destination path supplied to the constructor, made absolute.</summary>
        public string RealName { get; private set; }

        /// <summary>
        /// Allocate a sibling temp file for <paramref name="fileName"/>. Resolves the path
        /// against the current working directory so a bare file name lands its temp beside
        /// its destination, and creates the destination directory when it is missing.
        /// </summary>
        public SimpleFileSaver(string fileName)
        {
            RealName = Path.GetFullPath(fileName);

            SafeName = GetTempFileName(Path.GetDirectoryName(RealName), TEMP_PREFIX);
        }

        /// <summary>
        /// Replace the final destination with the temp file, in one move that replaces any file
        /// already there. No-op if the temp file is missing (already committed, or the caller
        /// never wrote to it). A failure (the destination read-only or open elsewhere)
        /// propagates, so the caller can log it; the temp is then still on disk for
        /// <see cref="CommitAs"/>.
        /// </summary>
        public void Commit()
        {
            if (!File.Exists(SafeName))
                return;

            File.Move(SafeName, RealName, true);
            Dispose();
        }

        /// <summary>
        /// Move the finished temp file to <paramref name="path"/>, which nothing may hold,
        /// instead of <see cref="RealName"/>: for a finished file that could not replace the
        /// one at its final name (read-only, or open in another program), so the work is kept
        /// under a name the caller chooses.
        /// </summary>
        public void CommitAs(string path)
        {
            if (!File.Exists(SafeName))
                return;

            File.Move(SafeName, path);
            Dispose();
        }

        /// <summary>
        /// Delete the temp file. Called automatically on <see cref="Commit"/> success and on
        /// disposal of an uncommitted instance, so the typical caller pattern is
        /// <c>using (var fs = new SimpleFileSaver(path)) { ... write to fs.SafeName ...
        /// fs.Commit(); }</c>, which leaves no temp on success and no partial destination on
        /// failure. Under <see cref="KeepFailedWrites"/> an uncommitted temp is left in place.
        /// </summary>
        public void Dispose()
        {
            if (SafeName == null)
                return;
            if (!KeepFailedWrites && File.Exists(SafeName))
                File.Delete(SafeName);
            // Null after a commit too, so SafeName never names a file that has moved.
            SafeName = null;
        }

        // Number of retries on filename collision before giving up. Matches the loose contract
        // of Win32 GetTempFileName, which iterates over generated names until one doesn't
        // collide; in practice a single attempt almost always succeeds.
        private const int TEMP_NAME_ATTEMPTS = 100;

        /// <summary>
        /// Cross-platform replacement for Win32 <c>GetTempFileName</c>: allocate a unique
        /// 0-byte file under <paramref name="basePath"/> whose name starts with
        /// <paramref name="prefix"/>. Uses <see cref="Path.GetRandomFileName"/> for the unique
        /// suffix and <see cref="FileMode.CreateNew"/> to claim the name atomically; on
        /// collision (extremely rare), retries a bounded number of times.
        /// </summary>
        private static string GetTempFileName(string basePath, string prefix)
        {
            Directory.CreateDirectory(basePath);
            for (int attempt = 0; attempt < TEMP_NAME_ATTEMPTS; attempt++)
            {
                string candidate = Path.Combine(basePath, prefix + Path.GetRandomFileName());
                try
                {
                    // CreateNew + FileShare.None claims the name and creates a 0-byte file;
                    // immediate close keeps the file in place so the caller's downstream
                    // writer can reopen it.
                    using (new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                    }
                    return candidate;
                }
                catch (IOException) when (File.Exists(candidate))
                {
                    // Name collision - another process or attempt got there first. Try a
                    // fresh random name.
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new IOException(string.Format(
                        MessageResources.SimpleFileSaver_GetTempFileName_Access_denied__unable_to_create_a_file_in_the_folder___0____Adjust_the_folder_write_,
                        basePath), ex);
                }
            }
            throw new IOException(string.Format(
                MessageResources.SimpleFileSaver_GetTempFileName_Failed_to_allocate_a_temporary_file_in_the_folder___0___after__1__attempts_,
                basePath, TEMP_NAME_ATTEMPTS));
        }
    }
}
