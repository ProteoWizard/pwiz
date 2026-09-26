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
            // torch.CUDA initializes the CUDA backend, which throws on a CPU-only build, so it
            // is only touched once CUDA is known to be there.
            if (IsCudaAvailable())
                return torch.CUDA;
            fallbackMessage = @"No CUDA device is available to this build; running on the CPU.";
            return CPU_DEVICE;
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
