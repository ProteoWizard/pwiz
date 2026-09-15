/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime;

namespace TestRunnerLib
{
    /// <summary>
    /// Answers "what is still rooting this object" as text, for
    /// <see cref="GarbageCollectionTracker"/>.
    ///
    /// <para>This is deliberately text rather than a memory dump. A GC leak reproduces roughly once
    /// per 30,000 test executions, and most of those occurrences happen inside a parallel-mode
    /// Docker worker, where a dump would have to be written to the c:\pwiz mount to survive the
    /// container and then analyzed offline - hundreds of megabytes for one root chain. Worker
    /// stdout is already relayed to the server log, so a few lines of text come back with no
    /// plumbing at all, in both serial and parallel mode.</para>
    ///
    /// <para>Uses ClrMD's PSS snapshot rather than attaching to the live process: the snapshot is a
    /// copy-on-write clone, so walking it cannot deadlock against the very process doing the
    /// walking, and the heap cannot shift underneath the traversal.</para>
    /// </summary>
    public static class GcRootReporter
    {
        /// <summary>
        /// A root chain is only useful if it is short enough to read. Anything longer than this is
        /// almost certainly walking a collection graph rather than naming the holder.
        /// </summary>
        private const int MAX_CHAIN = 24;

        /// <summary>
        /// The point is to identify the holder, not to enumerate every duplicate path to it.
        /// </summary>
        private const int MAX_PATHS_PER_TYPE = 3;

        /// <summary>
        /// Describes the GC roots holding any live object whose simple type name is in
        /// <paramref name="typeNames"/>. Returns human-readable lines; never throws for reasons
        /// intrinsic to the target process, so the caller can log whatever came back.
        /// </summary>
        public static IEnumerable<string> DescribeRoots(ICollection<string> typeNames)
        {
            if (typeNames == null || typeNames.Count == 0)
                yield break;

            var lines = new List<string>();
            try
            {
                lines.AddRange(DescribeRootsImpl(typeNames));
            }
            catch (Exception x)
            {
                // A snapshot can legitimately fail - PSS is unavailable, the runtime is mid-GC,
                // the container denies the handle. Report it rather than taking down the test run,
                // which is only reporting a leak in the first place.
                lines.Add(string.Format(@"unavailable: {0}: {1}", x.GetType().Name, x.Message));
            }

            foreach (var line in lines)
                yield return line;
        }

        private static List<string> DescribeRootsImpl(ICollection<string> typeNames)
        {
            var lines = new List<string>();
            using var dataTarget = DataTarget.CreateSnapshotAndAttach(Process.GetCurrentProcess().Id);
            var clrInfo = dataTarget.ClrVersions.FirstOrDefault();
            if (clrInfo == null)
            {
                lines.Add(@"unavailable: no CLR found in snapshot");
                return lines;
            }

            using var runtime = clrInfo.CreateRuntime();
            var heap = runtime.Heap;
            if (!heap.CanWalkHeap)
            {
                lines.Add(@"unavailable: heap is not walkable");
                return lines;
            }

            // Match on the simple name because that is what GarbageCollectionTracker records
            // (Type.Name), while ClrType.Name is namespace-qualified.
            var targetsByType = new Dictionary<string, List<ulong>>();
            foreach (var obj in heap.EnumerateObjects())
            {
                var typeName = obj.Type?.Name;
                if (typeName == null)
                    continue;
                var simpleName = SimpleName(typeName);
                if (!typeNames.Contains(simpleName))
                    continue;
                if (!targetsByType.TryGetValue(simpleName, out var list))
                    targetsByType[simpleName] = list = new List<ulong>();
                list.Add(obj.Address);
            }

            foreach (var typeName in typeNames)
            {
                if (!targetsByType.TryGetValue(typeName, out var addresses))
                {
                    // Collected between the check and the snapshot, which is worth knowing: it
                    // means the object was reachable only transiently.
                    lines.Add(string.Format(@"{0}: no live instance in snapshot", typeName));
                    continue;
                }

                lines.Add(string.Format(@"{0}: {1} live instance(s)", typeName, addresses.Count));
                lines.AddRange(DescribePathsTo(runtime, addresses));
            }

            return lines;
        }

        private static IEnumerable<string> DescribePathsTo(ClrRuntime runtime, List<ulong> addresses)
        {
            var lines = new List<string>();
            var gcRoot = new GCRoot(runtime.Heap, addresses);
            int pathCount = 0;
            foreach (var (root, chain) in gcRoot.EnumerateRootPaths())
            {
                lines.Add(string.Format(@"  root {0} {1}", root.RootKind, DescribeObject(runtime, root.Object.Address)));
                lines.Add(@"    " + FormatChain(runtime, chain));
                if (++pathCount >= MAX_PATHS_PER_TYPE)
                {
                    lines.Add(string.Format(@"  (stopped after {0} paths)", MAX_PATHS_PER_TYPE));
                    break;
                }
            }
            if (pathCount == 0)
            {
                // The tracker saw it alive, the snapshot finds it unrooted. That is the signature
                // of a transient root - a pending message, a thread-pool work item, a stack slot in
                // a frame that has since returned - rather than a durable reference.
                lines.Add(@"  no root path found - survivor appears transiently rooted");
            }
            return lines;
        }

        private static string FormatChain(ClrRuntime runtime, GCRoot.ChainLink chain)
        {
            var parts = new List<string>();
            for (var link = chain; link != null && parts.Count < MAX_CHAIN; link = link.Next)
                parts.Add(DescribeObject(runtime, link.Object));
            if (parts.Count >= MAX_CHAIN)
                parts.Add(@"...");
            return string.Join(@" -> ", parts);
        }

        /// <summary>
        /// A delegate in a root chain is the whole answer: it names the method whose subscription
        /// was never removed. Without this the chain stops at the handler's type, which identifies
        /// the event but not the subscriber - and for events raised by a static table like
        /// Microsoft.Win32.SystemEvents, the subscriber is the only thing worth knowing.
        /// </summary>
        private static string DescribeDelegate(ClrRuntime runtime, ClrHeap heap, ulong address)
        {
            try
            {
                var obj = heap.GetObject(address);
                if (!obj.IsValid || !IsDelegate(obj.Type))
                    return null;
                if (!obj.TryReadField<ulong>(@"_methodPtr", out var methodPtr) || methodPtr == 0)
                    return null;
                var method = runtime.GetMethodByInstructionPointer(methodPtr);
                if (method == null)
                    return null;
                var declaring = method.Type == null ? string.Empty : SimpleName(method.Type.Name) + @".";
                return declaring + method.Name;
            }
            catch (Exception)
            {
                // Best effort only - a chain without a method name is still worth printing.
                return null;
            }
        }

        private static bool IsDelegate(ClrType type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.Name == @"System.MulticastDelegate" || t.Name == @"System.Delegate")
                    return true;
            }
            return false;
        }

        private static string DescribeObject(ClrRuntime runtime, ulong address)
        {
            if (address == 0)
                return @"(null)";
            var type = runtime.Heap.GetObjectType(address);
            if (type == null)
                return string.Format(@"0x{0:x}", address);
            var name = SimpleName(type.Name);
            var method = DescribeDelegate(runtime, runtime.Heap, address);
            return method == null ? name : string.Format(@"{0}[{1}]", name, method);
        }

        /// <summary>
        /// Strips the namespace from every dotted run in the name, rather than just cutting at the
        /// last dot, so that generic arguments survive readably:
        /// System.Collections.Generic.List&lt;pwiz.Skyline.SkylineWindow&gt; becomes
        /// List&lt;SkylineWindow&gt; rather than the meaningless "SkylineWindow&gt;".
        /// </summary>
        private static string SimpleName(string typeName)
        {
            return DOTTED_RUN.Replace(typeName, m => m.Value.Substring(m.Value.LastIndexOf('.') + 1));
        }

        private static readonly Regex DOTTED_RUN =
            new Regex(@"[A-Za-z_][A-Za-z0-9_`]*(\.[A-Za-z_][A-Za-z0-9_`]*)+", RegexOptions.Compiled);
    }
}
