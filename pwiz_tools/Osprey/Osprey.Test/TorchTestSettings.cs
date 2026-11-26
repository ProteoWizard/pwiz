/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Models;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Pins the CPU arithmetic of libtorch for the test assembly, so a test that predicts a library or
    /// fine-tunes in-process computes the same bits on every x86-64 CPU of one operating system. With
    /// the seeds fixed, libtorch still chose its kernels by the CPU's instruction set and split its
    /// sums by the thread count, and the subset's library and tuned models differed from machine to
    /// machine. Measured on the tuned subset run, each of these alone changed the training library,
    /// both tuned models and the library predicted with them: 1, 4, 8 or 16 threads; ATen's kernels
    /// at the default level against AVX2; MKL's at SSE4.2 against AVX2; oneDNN's at SSE4.1 against
    /// AVX2. AVX2 is the level pinned because every machine that runs these tests has it and it
    /// costs nothing there; a CPU with AVX-512 is held to the AVX2 kernels.
    /// </summary>
    /// <remarks>
    /// libtorch reads these once, from the C runtime's environment, when it loads: the first library
    /// phase of a test loads it, after <see cref="Apply"/> has run from the assembly's initializer.
    /// The Windows and Linux builds of libtorch are compiled apart and still differ from each other.
    /// </remarks>
    public static class TorchTestSettings
    {
        /// <summary>The libtorch threads every test computes with: one, so no sum depends on the machine's cores.</summary>
        public const int THREADS = 1;

        private const string AVX2_ATEN = @"avx2";
        private const string AVX2_MKL = @"AVX2";
        private const string AVX2_ONEDNN = @"AVX2";

        private static readonly (string Name, string Value)[] SETTINGS =
        {
            (@"OMP_NUM_THREADS", THREADS.ToString(CultureInfo.InvariantCulture)),
            (@"MKL_NUM_THREADS", THREADS.ToString(CultureInfo.InvariantCulture)),
            (@"ATEN_CPU_CAPABILITY", AVX2_ATEN),
            // Conditional numerical reproducibility: MKL's AVX2 code path on any CPU that has AVX2.
            (@"MKL_CBWR", AVX2_MKL),
            (@"ONEDNN_MAX_CPU_ISA", AVX2_ONEDNN),
        };

        /// <summary>
        /// Sets the variables in the C runtime's environment, which libtorch reads, and the process's,
        /// which child processes inherit.
        /// </summary>
        public static void Apply()
        {
            foreach (var setting in SETTINGS)
            {
                Assert.IsTrue(CrtEnvironment.Set(setting.Name, setting.Value), setting.Name);
                Environment.SetEnvironmentVariable(setting.Name, setting.Value);
            }
        }
    }
}
