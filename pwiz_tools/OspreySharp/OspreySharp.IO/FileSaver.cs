/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Atomic file write helper. Construct with the final destination
    /// path; <see cref="SafeName"/> gives a sibling temp path to write
    /// into. Call <see cref="Commit"/> on successful write to atomically
    /// rename the temp into the final destination; <see cref="Dispose"/>
    /// (called on disposal-without-Commit, e.g. when an exception
    /// unwinds the writing block) deletes the temp without touching the
    /// final destination.
    ///
    /// A crash mid-write therefore leaves either the previous final
    /// content (if any) or no file at all -- never a partially-written
    /// destination that a downstream resume check could mistake for a
    /// finished output.
    ///
    /// Direct port of <c>SharedBatch/FileSaver.cs</c>
    /// (<c>pwiz_tools/Skyline/Executables/SharedBatch/SharedBatch/FileSaver.cs</c>),
    /// chosen over the more feature-rich Skyline
    /// <c>Util/UtilIO.cs</c> FileSaver because that variant pulls in
    /// significant Skyline-internal dependencies. Once OspreySharp gains
    /// dependencies under <c>pwiz_tools/Shared</c>, this should move to
    /// <c>pwiz_tools/Shared/CommonUtil/SystemUtil</c> as a
    /// <c>SimpleFileSaver</c> shared by OspreySharp + SkylineBatch +
    /// AutoQC.
    /// </summary>
    public sealed class FileSaver : IDisposable
    {
        private const string TEMP_PREFIX = @"~OS";

        /// <summary>Sibling temp path the caller writes into.</summary>
        public string SafeName { get; private set; }

        /// <summary>Final destination path supplied to the constructor.</summary>
        public string RealName { get; private set; }

        /// <summary>
        /// Allocate a sibling temp file for <paramref name="fileName"/>.
        /// Resolves <paramref name="fileName"/> against the current
        /// working directory so a bare-filename argument (no directory
        /// component) lands the temp file alongside its destination
        /// rather than failing inside <see cref="Path.GetDirectoryName(System.ReadOnlySpan{char})"/>
        /// downstream. Diverges from the SharedBatch original which
        /// happens to never see relative paths.
        /// </summary>
        public FileSaver(string fileName)
        {
            RealName = Path.GetFullPath(fileName);

            string dirName = Path.GetDirectoryName(RealName);
            string tempName = GetTempFileName(dirName, TEMP_PREFIX);

            if (!Equals(dirName, tempName))
                SafeName = tempName;
        }

        /// <summary>
        /// Replace the final destination with the temp file. No-op if
        /// the temp file is missing (e.g. already committed, or the
        /// caller never wrote to it). When the destination already
        /// exists, it is deleted first; the underlying File.Move is
        /// not atomic across the (delete, move) pair, so a crash in
        /// that narrow window can leave the destination missing with
        /// the temp still present (recoverable by hand or by re-running
        /// the pipeline). Diverges from the SharedBatch original which
        /// throws on overwrite -- OspreySharp's per-file artifacts are
        /// re-written by every pipeline run, so overwrite is the
        /// common case.
        /// </summary>
        public void Commit()
        {
            if (!File.Exists(SafeName)) return;

            // Diverges from the SharedBatch original which catches the
            // exception and writes to Trace.TraceWarning. OspreySharp's
            // CLI logging path doesn't subscribe to Trace, so swallowing
            // the exception there silently drops the file. Let it
            // propagate so the caller's try/catch can log via
            // AnalysisPipeline.LogWarning / LogError.
            if (File.Exists(RealName))
                File.Delete(RealName);
            File.Move(SafeName, RealName);
            Dispose();
        }

        /// <summary>
        /// Delete the temp file. Called automatically on
        /// <see cref="Commit"/> success and on disposal of an
        /// uncommitted instance, so the typical caller pattern is
        /// <c>using (var fs = new FileSaver(path)) { ... write to
        /// fs.SafeName ... fs.Commit(); }</c> which leaves no temp on
        /// success and no partial destination on failure.
        /// </summary>
        public void Dispose()
        {
            if (!File.Exists(SafeName)) return;
            File.Delete(SafeName);
            SafeName = null;
        }

        // Number of retries on filename collision before giving up. Matches
        // the loose contract of Win32 GetTempFileName, which iterates over
        // generated names until one doesn't collide; in practice a single
        // attempt almost always succeeds.
        private const int TEMP_NAME_ATTEMPTS = 100;

        /// <summary>
        /// Cross-platform replacement for Win32 <c>GetTempFileName</c>:
        /// allocate a unique 0-byte file under <paramref name="basePath"/>
        /// whose name starts with <paramref name="prefix"/>. Uses
        /// <see cref="Path.GetRandomFileName"/> for the unique suffix and
        /// <see cref="FileMode.CreateNew"/> to claim the name atomically;
        /// on collision (extremely rare), retries a bounded number of
        /// times. Works on net472 and net8.0 on Windows / Linux / macOS,
        /// unlike the kernel32.dll P/Invoke this replaced.
        /// </summary>
        private static string GetTempFileName(string basePath, string prefix)
        {
            Directory.CreateDirectory(basePath);
            for (int attempt = 0; attempt < TEMP_NAME_ATTEMPTS; attempt++)
            {
                string candidate = Path.Combine(basePath, prefix + Path.GetRandomFileName());
                try
                {
                    // CreateNew + FileShare.None claims the name and creates
                    // a 0-byte file; immediate close keeps the file in place
                    // so the caller's downstream writer can reopen it.
                    using (new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                    }
                    return candidate;
                }
                catch (IOException) when (File.Exists(candidate))
                {
                    // Name collision -- another process or attempt got there
                    // first. Try a fresh random name.
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new IOException(string.Format(
                        @"Access denied: unable to create a file in the folder '{0}'. " +
                        @"Adjust the folder write permissions or retry the operation " +
                        @"after moving or copying files to a different folder.",
                        basePath), ex);
                }
            }
            throw new IOException(string.Format(
                @"Failed to allocate a temporary file in the folder '{0}' " +
                @"after {1} attempts.", basePath, TEMP_NAME_ATTEMPTS));
        }
    }
}
