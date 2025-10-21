/*
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

using pwiz.Common.Properties;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using JetBrains.Annotations;

namespace pwiz.Common.SystemUtil.PInvoke
{
    public static class Kernel32
    {
        public static void AttachConsoleToParentProcess()
        {
            const int parentProcessId = -1;

            AttachConsole(parentProcessId);
        }

        /// <summary>
        /// Copies a file while providing progress updates to a callback
        /// </summary>
        /// <exception cref="IOException">For all errors including cancelled.</exception>
        public static void CopyFileWithProgress(string source, string destination, bool overwrite,
            CancellationToken cancellationToken, [InstantHandle] Action<int> onProgress)
        {
            var progressRoutine = new CopyProgressRoutine(
                (totalFileSize, totalBytesTransferred, streamSize, streamBytesTransferred, dwStreamNumber,
                    dwCallbackReason, hSourceFile, hDestinationFile, lpData) =>
                {
                    int progressValue = (int) (totalBytesTransferred * 100 / totalFileSize);
                    onProgress(progressValue);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return CopyProgressResult.PROGRESS_CANCEL;
                    }
                    return CopyProgressResult.PROGRESS_CONTINUE;
                });
            var dwCopyFlags = overwrite ? 0 : CopyFileFlags.COPY_FILE_FAIL_IF_EXISTS;
            bool cancelled = false;
            if (!CopyFileEx(source, destination, progressRoutine, IntPtr.Zero, ref cancelled, dwCopyFlags))
            {
                int errorCode = Marshal.GetLastWin32Error();
                var message = Resources.Kernel32_CopyFileWithProgress_Failed_to_copy___0___to___1__;
                throw new IOException(message, new Win32Exception(errorCode));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll")]
        public static extern int GetCurrentThreadId(); 
        
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetTempFileName(string lpPathName, string lpPrefixString, uint uUnique, [Out] StringBuilder lpTempFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        #region CopyFileEx
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CopyFileEx(string lpExistingFileName, string lpNewFileName,
            CopyProgressRoutine lpProgressRoutine, IntPtr lpData,
            ref bool pbCancel, CopyFileFlags dwCopyFlags);
        private delegate CopyProgressResult CopyProgressRoutine(
            long totalFileSize, long totalBytesTransferred,
            long streamSize, long streamBytesTransferred,
            uint dwStreamNumber, CopyProgressCallbackReason dwCallbackReason,
            IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData);
        private enum CopyProgressCallbackReason : uint
        {
            CALLBACK_CHUNK_FINISHED = 0x00000000,
            CALLBACK_STREAM_SWITCH = 0x00000001
        }
        [Flags]
        private enum CopyFileFlags : uint
        {
            COPY_FILE_FAIL_IF_EXISTS = 0x00000001,
            COPY_FILE_RESTARTABLE = 0x00000002,
            COPY_FILE_OPEN_SOURCE_FOR_WRITE = 0x00000004,
            COPY_FILE_ALLOW_DECRYPTED_DESTINATION = 0x00000008,
            COPY_FILE_COPY_SYMLINK = 0x00000800, //NT 6.0+
            COPY_FILE_NO_BUFFERING = 0x00001000 //NT 6.0+
        }
        private enum CopyProgressResult : uint
        {
            PROGRESS_CONTINUE = 0,
            PROGRESS_CANCEL = 1,
            PROGRESS_STOP = 2,
            PROGRESS_QUIET = 3
        }
        #endregion
    }
}