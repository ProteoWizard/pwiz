/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Reflection;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Common.SystemUtil
{
    public static class ProcessEx 
    {
        /// <summary>
        /// Returns true iff the process is running under Wine (the "wine_get_version" function is exported by ntdll.dll)
        /// </summary>
        public static bool IsRunningOnWine => Kernel32.GetProcAddress(Kernel32.GetModuleHandle(@"ntdll.dll"), @"wine_get_version") != IntPtr.Zero;

        /// <summary>
        /// Returns true iff the process is running under an OS and on volume(s) that support windows 8.3 format conversion
        /// </summary>
        private static Dictionary<string, bool> _volumesTested = new Dictionary<string, bool>();

        public static bool CanConvertUnicodePaths
        {
            get
            {
                if (IsRunningOnWine)
                {
                    return false; // Never supported on Wine
                }
                else
                {
                    // Systems may have 8.3 conversion enabled on some volumes but not others
                    // So don't assume we can convert Unicode paths unless we can actually do it everywhere we might need to
                    return
                        CanConvertUnicodePathsInDirectory(Path.GetTempPath()) &&
                        CanConvertUnicodePathsInDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)) &&
                        CanConvertUnicodePathsInDirectory(Directory.GetCurrentDirectory()) &&
                        CanConvertUnicodePathsInDirectory(PathEx.GetDownloadsPath());
                }
            }
        }
        private static bool CanConvertUnicodePathsInDirectory(string baseDir)
        {
            var volume = Path.GetPathRoot(baseDir);
            if (_volumesTested.TryGetValue(volume, out var result))
            {
                return result; // Already tested this volume
            }
            try
            {
                var fileName = Path.Combine(baseDir, Path.GetRandomFileName() + @".test试验.txt");
                File.WriteAllText(fileName, @"test data");
                var converted = PathEx.GetNonUnicodePath(fileName);
                File.Delete(fileName);
                result = !Equals(converted, fileName);
            }
            catch
            {
                result = false; // Not writable?
            }

            _volumesTested[volume] = result;
            return result;
        }
    }
}
