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

using System;     // GC.GetGCMemoryInfo (net8.0 only; the net472 path is pure PInvoke)

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Best-effort probe of free physical memory, used by AUTO file parallelism
    /// (<see cref="FileParallelismResolver"/>) and the first-pass FDR lane count
    /// (<see cref="FdrLaneResolver"/>) to decide how many files to work on
    /// concurrently without exhausting RAM. This is a
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
        // Gen0 collections AvailablePhysicalBytesAfterCollect may spend getting a record newer
        // than its aggressive one. One sufficed in every stale case measured.
        private const int MAX_REFRESH_COLLECTIONS = 3;

        /// <summary>
        /// Free physical memory in bytes, or 0 when it cannot be determined.
        /// Approximate (the net8.0 path reports the load as of the last GC);
        /// adequate for a parallelism sizing decision.
        /// </summary>
        public static long AvailablePhysicalBytes()
        {
            var info = GC.GetGCMemoryInfo();
            // The load is a GC's observation, so before the first GC it reads 0 and every
            // byte of RAM looks free - other processes' use included. A stage that sizes its
            // lanes as the first work of a fresh process (--task SpectraCache) would budget
            // a shared box's whole memory. One gen0 collection makes the reading real.
            if (info.Index == 0)
            {
                GC.Collect(0);
                info = GC.GetGCMemoryInfo();
            }
            // TotalAvailableMemoryBytes is the GC's view of total physical (or
            // the cgroup limit); MemoryLoadBytes is how much is currently in
            // use. Their difference is the free headroom.
            long total = info.TotalAvailableMemoryBytes;
            long used = info.MemoryLoadBytes;
            if (total <= 0)
                return 0;
            long available = total - used;
            return available > 0 ? available : 0;
        }

        /// <summary>
        /// <see cref="AvailablePhysicalBytes"/> after returning the managed heap's free memory to
        /// the OS, for a sizing decision made after an earlier stage in the same process. Freed
        /// regions stay committed through an ordinary collection and count as used: entering
        /// first-pass FDR straight after PerFileScoring on SEA-AD 82 files at 64 GB read 29.3 GB
        /// free with 21.4 GB private, and 45.2 GB free with 5.7 GB private after this.
        ///
        /// <para>Only the aggressive mode decommits. Its own record samples the load before it
        /// does, so a gen0 collection after it supplies the reading. Under dynamic heap counts
        /// (DATAS) that gen0 request can become a background collection that is still running
        /// when Collect returns, leaving the aggressive record as the latest; so repeat it until
        /// a newer record exists. Blocks the process for a few seconds - once per stage, not per
        /// file.</para>
        /// </summary>
        public static long AvailablePhysicalBytesAfterCollect()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            long aggressiveIndex = GC.GetGCMemoryInfo().Index;
            for (int i = 0; i < MAX_REFRESH_COLLECTIONS && GC.GetGCMemoryInfo().Index <= aggressiveIndex; i++)
                GC.Collect(0);
            return AvailablePhysicalBytes();
        }
    }
}
