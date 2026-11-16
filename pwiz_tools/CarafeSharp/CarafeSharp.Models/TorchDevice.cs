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
            {
                DisableTf32();
                return torch.CUDA;
            }
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

        /// <summary>
        /// Turns TF32 off for CUDA matrix multiplications and cuDNN convolutions and LSTMs, so the GPU predicts in
        /// float32 as the CPU does: the equivalent of <c>torch.backends.cuda.matmul.allow_tf32 = False</c> and
        /// <c>torch.backends.cudnn.allow_tf32 = False</c>. Fine-tuning allows cuDNN TF32 again
        /// (<see cref="AllowTf32ForTraining"/>).
        /// </summary>
        /// <remarks>
        /// libtorch uses TF32, a 10-bit mantissa, for cuDNN convolutions and LSTMs on Ampere and later GPUs by default.
        /// With it, GPU retention times differed from Carafe's references far beyond the limits the CPU meets (Stellar
        /// 1.0e-4 rt_norm against 2e-6, library RT up to 0.77 min against 0.011); without it they are about the CPU's
        /// (docs/04-testing.md, "GPU parity"), and CarafeSharp aims to give the same library on the CPU and the GPU. It
        /// is free: on an RTX 4000 Ada (2026-11-04), the full-size parity comparisons with TF32 off took 0.98 to 1.06
        /// times their TF32 wall time, and library prediction 0.99 to 1.00 (two runs each; 0.91 to 1.05 in an earlier
        /// A/B), within run-to-run noise. Setting <c>NVIDIA_TF32_OVERRIDE=0</c> is no longer needed for this. Both flags
        /// are process-wide libtorch settings; CarafeSharp never turns TF32 on.
        /// </remarks>
        internal static void DisableTf32()
        {
            backends.cuda.matmul.allow_tf32 = false;
            backends.cudnn.allow_tf32 = false;
        }

        /// <summary>
        /// Allows cuDNN TF32, libtorch's default, while fine-tuning on <paramref name="device"/>, and turns it off again
        /// when disposed; null on the CPU, which has no TF32.
        /// </summary>
        /// <remarks>
        /// With cuDNN TF32 off, two GPU fine-tunes of the same data gave different RT models on an RTX 4000 Ada
        /// (RegressionTest.TestRegressionSubsetRepeatsOnGpu failed 4 of 4 times; with TF32 on it passed 3 of 3), most likely
        /// because in float32 cuDNN picks a gradient algorithm whose order of additions changes from run to run. libtorch's
        /// <c>cudnn.deterministic</c> would rule those out, but TorchSharp 0.106 has no setter for it, and its
        /// <c>use_deterministic_algorithms</c> is marked not implemented. Training therefore keeps the TF32 it had,
        /// which repeats byte for byte, including the predictions it makes for its calibration and metrics; prediction
        /// outside fine-tuning is in float32. <c>NVIDIA_TF32_OVERRIDE=0</c> would turn this TF32 off as well.
        /// </remarks>
        public static IDisposable AllowTf32ForTraining(Device device)
        {
            if (device.type != DeviceType.CUDA)
                return null;
            backends.cudnn.allow_tf32 = true;
            return new Tf32Off();
        }

        private sealed class Tf32Off : IDisposable
        {
            public void Dispose()
            {
                DisableTf32();
            }
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
