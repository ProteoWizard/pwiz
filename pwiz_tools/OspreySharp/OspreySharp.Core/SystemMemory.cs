/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

#if NETFRAMEWORK
using System.Runtime.InteropServices;
#else
using System;     // GC.GetGCMemoryInfo (net8.0 only; the net472 path is pure PInvoke)
#endif

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Best-effort probe of free physical memory, used only by AUTO file
    /// parallelism (<see cref="FileParallelismResolver"/>) to decide how many
    /// input files to score concurrently without exhausting RAM. This is a
    /// sizing hint, never a correctness input, so an unknown value (0) simply
    /// falls the resolver back to a CPU-bound cap.
    ///
    /// net8.0 uses <c>GC.GetGCMemoryInfo()</c>, which is cross-platform and
    /// respects container / cgroup limits on Linux (the HPC case). net472
    /// is Windows-only and predates that API, so it falls back to the
    /// <c>GlobalMemoryStatusEx</c> Win32 call (the same one Skyline's
    /// MemoryInfo uses). The PInvoke is isolated here per the project's
    /// "one place for each Win32 API" rule.
    /// </summary>
    public static class SystemMemory
    {
        /// <summary>
        /// Free physical memory in bytes, or 0 when it cannot be determined.
        /// Approximate (the net8.0 path reports the load as of the last GC);
        /// adequate for a parallelism sizing decision.
        /// </summary>
        public static long AvailablePhysicalBytes()
        {
#if NETFRAMEWORK
            var status = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(status))
                return (long)status.ullAvailPhys;
            return 0;
#else
            var info = GC.GetGCMemoryInfo();
            // TotalAvailableMemoryBytes is the GC's view of total physical (or
            // the cgroup limit); MemoryLoadBytes is how much is currently in
            // use. Their difference is the free headroom.
            long total = info.TotalAvailableMemoryBytes;
            long used = info.MemoryLoadBytes;
            if (total <= 0)
                return 0;
            long available = total - used;
            return available > 0 ? available : 0;
#endif
        }

#if NETFRAMEWORK
        // Layout must match the Win32 MEMORYSTATUSEX struct exactly; the fields
        // are populated by GlobalMemoryStatusEx via marshaling (the compiler
        // cannot see those writes), and only ullAvailPhys is read here.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            // ReSharper disable NotAccessedField.Local
            // ReSharper disable UnassignedField.Compiler
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            // ReSharper restore UnassignedField.Compiler
            // ReSharper restore NotAccessedField.Local

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport(@"kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
#endif
    }
}
