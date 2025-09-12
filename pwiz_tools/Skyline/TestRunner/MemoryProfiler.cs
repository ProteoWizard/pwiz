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
using System.IO;    

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

                var result = TestRunnerLib.MiniDump.WriteMiniDump(dumpFile);
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

    }
}
