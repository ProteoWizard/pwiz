/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Runtime.InteropServices;
using TorchSharp;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Chooses the libtorch device. A GPU request falls back to the CPU, as Carafe's does,
    /// when the build carries no CUDA runtime or no CUDA device is present.
    /// </summary>
    public static class TorchDevice
    {
        public const string CPU = @"cpu";
        public const string GPU = @"gpu";

        /// <summary>The variable CarafeSharp sets libtorch's CUDA allocator with.</summary>
        internal const string CUDA_ALLOC_CONF_VARIABLE = @"PYTORCH_CUDA_ALLOC_CONF";

        /// <summary>
        /// The variables libtorch 2.10 reads its allocator settings from, in its order: the first one set is used, so a
        /// setting in any of them is the user's.
        /// </summary>
        internal static readonly string[] ALLOC_CONF_VARIABLES = { CUDA_ALLOC_CONF_VARIABLE, @"PYTORCH_HIP_ALLOC_CONF", @"PYTORCH_ALLOC_CONF" };

        /// <summary>
        /// The allocator settings CarafeSharp uses on Windows when the user has set none: each block rounded to one of four
        /// sizes per power of two, and the cache held to three quarters of the card.
        /// </summary>
        internal const string CUDA_ALLOC_CONF = @"roundup_power2_divisions:4,per_process_memory_fraction:0.75";

        private static readonly Device CPU_DEVICE = torch.CPU;

        /// <summary>
        /// The device for <paramref name="requested"/> (<c>cpu</c>, <c>gpu</c> or <c>cuda</c>).
        /// <paramref name="fallbackMessage"/> is set when a GPU was requested but not available.
        /// </summary>
        public static Device Resolve(string requested, out string fallbackMessage)
        {
            fallbackMessage = null;
            if (string.IsNullOrEmpty(requested) || string.Equals(requested, CPU, StringComparison.OrdinalIgnoreCase))
                return CPU_DEVICE;
            if (!string.Equals(requested, GPU, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(requested, @"cuda", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(@"Unknown device '{0}' (expected cpu or gpu).", requested), nameof(requested));
            }
            ConfigureCudaAllocator();
            // torch.CUDA initializes the CUDA backend, which throws on a CPU-only build, so it
            // is only touched once CUDA is known to be there.
            if (IsCudaAvailable())
                return torch.CUDA;
            fallbackMessage = @"No CUDA device is available to this build; running on the CPU.";
            return CPU_DEVICE;
        }

        /// <summary>
        /// On Windows, gives libtorch <see cref="CUDA_ALLOC_CONF"/> before CUDA starts, unless the user set allocator settings.
        /// </summary>
        /// <remarks>
        /// libtorch keeps the GPU memory of freed tensors to reuse it. The MS2 model's activations change size with peptide
        /// length, so freed blocks are split for requests they no longer fit and the cache grows while the process predicts:
        /// 400-470 MB a minute on a ZT Scan library, with every tensor disposed. Where a full card makes allocation fail,
        /// libtorch empties its cache and goes on (Linux); on Windows the driver backs the allocation with system memory
        /// instead, and prediction slowed 5-15 fold once a 4 GB card was full. Rounding the blocks lets freed ones be reused,
        /// and the cap makes libtorch empty its cache at three quarters of the card, as a full card does on Linux.
        /// libtorch reads the C runtime's environment (<see cref="CrtEnvironment"/>), which a setting made through .NET
        /// after the process started does not reach: such a setting is passed on to it. The default is kept out of the
        /// process's own environment, which child processes inherit.
        /// </remarks>
        internal static void ConfigureCudaAllocator()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;
            foreach (string variable in ALLOC_CONF_VARIABLES)
            {
                if (!string.IsNullOrEmpty(CrtEnvironment.Get(variable)))
                    return;
                string setThroughDotNet = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrEmpty(setThroughDotNet))
                {
                    CrtEnvironment.Set(variable, setThroughDotNet);
                    return;
                }
            }
            if (CrtEnvironment.Set(CUDA_ALLOC_CONF_VARIABLE, CUDA_ALLOC_CONF))
                Environment.SetEnvironmentVariable(CUDA_ALLOC_CONF_VARIABLE, null);
        }

        private static bool IsCudaAvailable()
        {
            try
            {
                return TryInitializeDeviceType(DeviceType.CUDA) && cuda.is_available();
            }
            catch (Exception)
            {
                // A CPU-only build has no CUDA native library to initialize.
                return false;
            }
        }
    }
}
