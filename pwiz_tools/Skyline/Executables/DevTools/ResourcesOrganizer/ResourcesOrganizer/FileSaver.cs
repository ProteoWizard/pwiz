/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ResourcesOrganizer
{
    public sealed class FileSaver : IDisposable
    {
        public const string TEMP_PREFIX = "~SK";
        /// <summary>
        /// Construct an instance of <see cref="FileSaver"/> to manage saving to a temporary
        /// file, and then renaming to the final destination.
        /// </summary>
        /// <param name="fileName">File path to the final destination</param>
        /// <throws>IOException</throws>
        public FileSaver(string fileName)
        {
            RealName = Path.GetFullPath(fileName);

            string dirName = Path.GetDirectoryName(RealName)!;
            string tempName = GetTempFileName(dirName, TEMP_PREFIX);
            // If the directory name is returned, then starting path was bogus.
            if (!Equals(dirName, tempName))
                SafeName = tempName;
        }

        public string? SafeName { get; private set; }

        public string RealName { get; private set; }

        public bool Commit()
        {
            // This is where the file that got written is renamed to the desired file.
            // Dispose() will do any necessary temporary file clean-up.

            if (string.IsNullOrEmpty(SafeName))
                return false;
            Commit(SafeName, RealName);
            Dispose();

            return true;
        }

        private static void Commit(string pathTemp, string pathDestination)
        {
                try
                {
                    string backupFile = GetBackupFileName(pathDestination);
                    File.Delete(backupFile);

                    // First try replacing the destination file, if it exists
                    if (File.Exists(pathDestination))
                    {
                        File.Replace(pathTemp, pathDestination, backupFile, true);
                        File.Delete(backupFile);
                        return;
                    }
                }
                catch (FileNotFoundException)
                {
                }

                // Or just move, if it does not.
                File.Move(pathTemp, pathDestination);
        }

        private static string GetBackupFileName(string pathDestination)
        {
            string backupFile = FileSaver.TEMP_PREFIX + Path.GetFileName(pathDestination) + @".bak";
            string dirName = Path.GetDirectoryName(pathDestination)!;
            if (!string.IsNullOrEmpty(dirName))
                backupFile = Path.Combine(dirName, backupFile);
            // CONSIDER: Handle failure by trying a different name, or use a true temporary name?
            File.Delete(backupFile);
            return backupFile;
        }


        public void Dispose()
        {
            // Get rid of the temporary file, if it still exists.
            if (!string.IsNullOrEmpty(SafeName))
            {
                try
                {
                    if (File.Exists(SafeName))
                        File.Delete(SafeName);
                }
                catch (Exception e)
                {
                    Trace.TraceWarning(@"Exception in FileSaver.Dispose: {0}", e);
                }
                // Make sure any further calls to Dispose() do nothing.
                SafeName = null;
            }
        }
        public string GetTempFileName(string basePath, string prefix)
        {
            return GetTempFileName(basePath, prefix, 0);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetTempFileName(string lpPathName, string lpPrefixString,
            uint uUnique, [Out] StringBuilder lpTempFileName);

        private static string GetTempFileName(string basePath, string prefix, uint unique)
        {
            // 260 is MAX_PATH in Win32 windows.h header
            // 'sb' needs >0 size else GetTempFileName throws IndexOutOfRangeException.  260 is the most you'd want.
            StringBuilder sb = new StringBuilder(260);

            Directory.CreateDirectory(basePath);
            uint result = GetTempFileName(basePath, prefix, unique, sb);
            if (result == 0)
            {
                var lastWin32Error = Marshal.GetLastWin32Error();
                throw new IOException(string.Format("Error {0} GetTempFileName({1}, {2}, {3})", lastWin32Error,
                    basePath, prefix, unique));
            }

            return sb.ToString();
        }
    }
}
