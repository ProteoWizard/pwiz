/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using JetBrains.Profiler.Api;

// ReSharper disable LocalizableElement

using System;
using System.Diagnostics;
using System.IO;    
using System.Runtime.InteropServices;

namespace TestRunner
{
    /// <summary>
    /// Support for memory snapshots if the JetBrains DotMemory is running (good for managed memory),
    /// and creating dumpfiles for inspection with WinDbg (good for unmanaged memory).
    /// </summary>
    public static class MemoryProfiler
    {
        /// <summary>
        /// Take a memory snapshot for dotMemory.
        /// </summary>
        public static void Snapshot(string name)
        {
            if (0 != (JetBrains.Profiler.Api.MemoryProfiler.GetFeatures() & MemoryFeatures.Ready))
            {
                // Uncomment to start collecting the stack traces of all allocations.
                //JetBrains.Profiler.Api.MemoryProfiler.CollectAllocations(true);

                JetBrains.Profiler.Api.MemoryProfiler.GetSnapshot(name);
            }
            // Consider: support other types of profilers.
        }

        // Capture a memory dump to a specified file for WinDbg
        public static void CaptureMemoryDump(string dumpName, string dumpDir)
        {
            try
            {
                var fullDir = Path.GetFullPath(dumpDir);
                if (!Directory.Exists(fullDir))
                {
                    Directory.CreateDirectory(fullDir);
                }
                dumpDir = fullDir;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not create dmpdir \"{dumpDir}\"\n{e}");
            }
            try
            {
                var dumpFile =  Path.GetFullPath(Path.Combine(dumpDir,$"{dumpName}.dmp"));
                var currentProcess = Process.GetCurrentProcess();
                var fileHandle = CreateFile(dumpFile,
                    GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    CREATE_ALWAYS,
                    FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);

                // Create dump using MiniDumpWriteDump
                bool result = MiniDumpWriteDump(currentProcess.Handle, (uint)currentProcess.Id, fileHandle, MiniDumpWithFullMemory, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (!result)
                {
                    Console.WriteLine($"Failed to capture memory dump to {dumpFile}");
                }
                else
                {
                    Console.WriteLine($"Memory dump captured: {dumpFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing memory dump: {ex.Message}");
            }
        }

        // Define the necessary P/Invoke signatures
        [DllImport("dbghelp.dll", SetLastError = true)]
        public static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, IntPtr hFile, uint dumpType, IntPtr exceptionParam, IntPtr userStreamParam, IntPtr callbackParam);

        // Constants for dump types
        public const uint MiniDumpNormal = 0x00000000;
        public const uint MiniDumpWithFullMemory = 0x00000002;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        // Constants for dump types and file creation
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint CREATE_ALWAYS = 2;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    }
}
