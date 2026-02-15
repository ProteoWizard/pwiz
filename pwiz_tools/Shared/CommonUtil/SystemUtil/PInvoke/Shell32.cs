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

using System;
using System.Runtime.InteropServices;

namespace pwiz.Common.SystemUtil.PInvoke
{
    public static class Shell32
    {
        private static Guid _folderDownloads = new Guid(@"374DE290-123F-4565-9164-39C4925E467B");

        public static string GetDownloadsFolder()
        {
            string path = null;
            IntPtr pathPtr = default;
            try
            {
                int hr = SHGetKnownFolderPath(ref _folderDownloads, 0, IntPtr.Zero, out pathPtr);
                if (hr == 0) // successful call, so attempt to read result
                {
                    path = Marshal.PtrToStringUni(pathPtr);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }

            return path;
        }

        // From: https://www.pinvoke.net/default.aspx/shell32.SHGetKnownFolderPath
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetKnownFolderPath(ref Guid id, int flags, IntPtr token, out IntPtr path);

    }
}