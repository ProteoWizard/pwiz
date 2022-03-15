/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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

using System.Runtime.InteropServices;

namespace pwiz.Skyline.Util
{
    public static class MemoryInfo
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            // ReSharper disable MemberCanBePrivate.Local
            // ReSharper disable once NotAccessedField.Local
            public uint dwLength;
#pragma warning disable 169
#pragma warning disable 649
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
#pragma warning restore 649
#pragma warning restore 169
            // ReSharper restore MemberCanBePrivate.Local
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static long TotalBytes
        {
            get
            {
                MEMORYSTATUSEX statEX = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(statEX))
                    return (long) statEX.ullTotalPhys;
                return 0;
            }
        }

        public static long AvailableBytes
        {
            get
            {
                MEMORYSTATUSEX statEX = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(statEX))
                    return (long) statEX.ullAvailPhys;
                return 0;
            }
        }

        public readonly static long Kibibyte = 1024;
        public readonly static long Mebibyte = Kibibyte * 1024;
        public readonly static long Gibibyte = Mebibyte * 1024;
    }
}
