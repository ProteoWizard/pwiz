/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using SortRESX.Properties;

namespace SortRESX
{
    public sealed class FileSaver : IDisposable
    {
        public const string TEMP_PREFIX = "~SK";

        private Stream _stream;

        /// <summary>
        /// Construct an instance of <see cref="FileSaver"/> to manage saving to a temporary
        /// file, and then renaming to the final destination.
        /// </summary>
        /// <param name="fileName">File path to the final destination</param>
        /// <throws>IOException</throws>
        public FileSaver(string fileName)
        {
            RealName = fileName;

            string dirName = Path.GetDirectoryName(fileName);
            string tempName = GetTempFileName(dirName, TEMP_PREFIX, 0);
            // If the directory name is returned, then starting path was bogus.
            if (!Equals(dirName, tempName))
                SafeName = tempName;
        }

        public string SafeName { get; private set; }

        public string RealName { get; private set; }

        public bool Commit()
        {
            // This is where the file that got written is renamed to the desired file.
            // Dispose() will do any necessary temporary file clean-up.

            if (string.IsNullOrEmpty(SafeName))
                return false;

            if (_stream != null)
            {
                _stream.Close();
                _stream = null;
            }

            CommitTempFile(SafeName, RealName);

            // Also move any files with maching basenames (useful for debugging with extra output files
            //            foreach (var baseMatchFile in Directory.EnumerateFiles(Path.GetDirectoryName(SafeName) ?? @".", Path.GetFileNameWithoutExtension(SafeName) + @".*"))
            //            {
            //                _streamManager.Commit(baseMatchFile, Path.ChangeExtension(RealName, baseMatchFile.Substring(SafeName.LastIndexOf('.'))), null);
            //            }

            Dispose();

            return true;
        }

        private static void CommitTempFile(string pathTemp, string pathDestination)
        {
            try
            {
                string backupFile = GetBackupFileName(pathDestination);
                FileEx.SafeDelete(backupFile, true);

                // First try replacing the destination file, if it exists
                if (File.Exists(pathDestination))
                {
                    File.Replace(pathTemp, pathDestination, backupFile, true);
                    FileEx.SafeDelete(backupFile, true);
                    return;
                }
            }
            catch (FileNotFoundException)
            {
            }

            // Or just move, if it does not.
            Helpers.TryTwice(() => File.Move(pathTemp, pathDestination));
        }
        private static string GetBackupFileName(string pathDestination)
        {
            string backupFile = FileSaver.TEMP_PREFIX + Path.GetFileName(pathDestination) + @".bak";
            string dirName = Path.GetDirectoryName(pathDestination);
            if (!string.IsNullOrEmpty(dirName))
                backupFile = Path.Combine(dirName, backupFile);
            // CONSIDER: Handle failure by trying a different name, or use a true temporary name?
            FileEx.SafeDelete(backupFile);
            return backupFile;
        }

        public void Dispose()
        {
            if (_stream != null)
            {
                try
                {
                    _stream.Close();
                }
                catch (Exception e)
                {
                    Trace.TraceWarning(@"Exception in FileSaver.Dispose: {0}", e);
                }
                _stream = null;
            }

            // Get rid of the temporary file, if it still exists.

            if (!string.IsNullOrEmpty(SafeName))
            {
                try
                {
                    if (File.Exists(SafeName))
                        FileEx.SafeDelete(SafeName);
                }
                catch (Exception e)
                {
                    Trace.TraceWarning(@"Exception in FileSaver.Dispose: {0}", e);
                }
                // Make sure any further calls to Dispose() do nothing.
                SafeName = null;
            }
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
                if (lastWin32Error == 5)
                {
                    throw new IOException(string.Format(Resources.FileStreamManager_GetTempFileName_Access_Denied__unable_to_create_a_file_in_the_folder___0____Adjust_the_folder_write_permissions_or_retry_the_operation_after_moving_or_copying_files_to_a_different_folder_, basePath));
                }
                else
                {
                    throw new IOException(LineSeparate(string.Format(Resources.FileStreamManager_GetTempFileName_Failed_attempting_to_create_a_temporary_file_in_the_folder__0__with_the_following_error_, basePath),
                        string.Format(Resources.FileStreamManager_GetTempFileName_Win32_Error__0__, lastWin32Error)));
                }
            }

            return sb.ToString();
        }
        public static string LineSeparate(params string[] lines)
        {
            var sb = new StringBuilder();
            foreach (string line in lines)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(line);
            }
            return sb.ToString();
        }
    }

}
