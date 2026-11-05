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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// How this platform's file system compares file names: exactly on Linux, whose file
    /// systems are case-sensitive, and ignoring case elsewhere. Any set, map or equality test
    /// that stands for "the same file on disk" uses this, never a fixed comparison: a fixed
    /// case-insensitive one treats two distinct Linux files as one, and a fixed exact one lets
    /// two Windows paths that name one file pass as two.
    /// </summary>
    public static class PathComparison
    {
        public static readonly StringComparison Comparison =
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        public static readonly StringComparer Comparer =
            OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Whether two paths name the same file once made absolute.
        /// </summary>
        public static bool IsSamePath(string path1, string path2)
        {
            return string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), Comparison);
        }
    }
}
