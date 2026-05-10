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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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
        /// </summary>
        public FileSaver(string fileName)
        {
            RealName = fileName;

            string dirName = Path.GetDirectoryName(fileName);
            string tempName = GetTempFileName(dirName, TEMP_PREFIX);

            if (!Equals(dirName, tempName))
                SafeName = tempName;
        }

        /// <summary>
        /// Atomically replace the final destination with the temp file.
        /// No-op if the temp file is missing (e.g. already committed,
        /// or the caller never wrote to it).
        /// </summary>
        public void Commit()
        {
            if (!File.Exists(SafeName)) return;

            try
            {
                File.Move(SafeName, RealName);
                Dispose();
            }
            catch (Exception e)
            {
                Trace.TraceWarning(@"Exception in FileSaver.Commit: {0}", e);
            }
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

        [DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint GetTempFileName(string lpPathName, string lpPrefixString,
            uint uUnique, [Out] StringBuilder lpTempFileName);

        private static string GetTempFileName(string basePath, string prefix)
        {
            // 260 is MAX_PATH in Win32 windows.h header. The buffer needs
            // a >0 capacity or GetTempFileName throws IndexOutOfRangeException.
            var sb = new StringBuilder(260);

            Directory.CreateDirectory(basePath);
            uint result = GetTempFileName(basePath, prefix, 0, sb);
            if (result == 0)
            {
                var lastWin32Error = Marshal.GetLastWin32Error();
                if (lastWin32Error == 5)
                {
                    throw new IOException(string.Format(
                        @"Access denied: unable to create a file in the folder '{0}'. " +
                        @"Adjust the folder write permissions or retry the operation " +
                        @"after moving or copying files to a different folder.",
                        basePath));
                }
                throw new IOException(string.Format(
                    @"Failed attempting to create a temporary file in the folder '{0}': " +
                    @"Win32 error {1}.",
                    basePath, lastWin32Error));
            }
            return sb.ToString();
        }
    }
}
