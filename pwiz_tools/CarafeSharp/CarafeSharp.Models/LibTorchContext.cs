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
using System.Runtime.InteropServices;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// libtorch's process-wide settings object, <c>at::globalContext()</c>, for the settings TorchSharp 0.106 has no
    /// setter for. The functions are found by their C++ names in torch_cpu, which are those of the libtorch pinned in
    /// LibTorch.props (2.10.0) and could change with it; <see cref="TryOpen"/> reports any it cannot find.
    /// </summary>
    internal sealed unsafe class LibTorchContext
    {
        // MSVC names (Windows), as torch_cpu.dll exports them.
        internal const string GLOBAL_CONTEXT_WINDOWS = @"?globalContext@at@@YAAEAVContext@1@XZ";
        internal const string SET_DETERMINISTIC_CUDNN_WINDOWS = @"?setDeterministicCuDNN@Context@at@@QEAAX_N@Z";
        internal const string DETERMINISTIC_CUDNN_WINDOWS = @"?deterministicCuDNN@Context@at@@QEBA_NXZ";

        // Itanium names (Linux).
        internal const string GLOBAL_CONTEXT_LINUX = @"_ZN2at13globalContextEv";
        internal const string SET_DETERMINISTIC_CUDNN_LINUX = @"_ZN2at7Context21setDeterministicCuDNNEb";
        internal const string DETERMINISTIC_CUDNN_LINUX = @"_ZNK2at7Context18deterministicCuDNNEv";

        private static readonly Lazy<(LibTorchContext Context, string Problem)> GLOBAL = new Lazy<(LibTorchContext, string)>(() =>
        {
            var context = Open(out string problem);
            return (context, problem);
        });

        private readonly IntPtr _context;
        private readonly delegate* unmanaged<IntPtr, byte, void> _setDeterministicCuDnn;
        private readonly delegate* unmanaged<IntPtr, byte> _deterministicCuDnn;

        /// <summary>
        /// The global context of the libtorch this process loaded, or null with <paramref name="problem"/> set when
        /// torch_cpu lacks one of the functions. libtorch must be loaded first (any tensor does it); the functions are
        /// found once per process.
        /// </summary>
        public static LibTorchContext TryOpen(out string problem)
        {
            var global = GLOBAL.Value;
            problem = global.Problem;
            return global.Context;
        }

        private LibTorchContext(IntPtr context, IntPtr setDeterministicCuDnn, IntPtr deterministicCuDnn)
        {
            _context = context;
            _setDeterministicCuDnn = (delegate* unmanaged<IntPtr, byte, void>)setDeterministicCuDnn;
            _deterministicCuDnn = (delegate* unmanaged<IntPtr, byte>)deterministicCuDnn;
        }

        /// <summary>
        /// <c>torch.backends.cudnn.deterministic</c>: cuDNN convolutions use only algorithms that give the same result
        /// on every run. It does not cover cuDNN's LSTMs.
        /// </summary>
        public bool DeterministicCuDnn
        {
            get { return _deterministicCuDnn(_context) != 0; }
            set { _setDeterministicCuDnn(_context, value ? (byte)1 : (byte)0); }
        }

        private static LibTorchContext Open(out string problem)
        {
            bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            // By file name, so the operating system hands back the torch_cpu TorchSharp already loaded.
            string library = windows ? @"torch_cpu.dll" : @"libtorch_cpu.so";
            if (!NativeLibrary.TryLoad(library, out IntPtr handle))
            {
                problem = string.Format(@"{0} could not be loaded.", library);
                return null;
            }
            if (!TryGetExport(handle, windows ? GLOBAL_CONTEXT_WINDOWS : GLOBAL_CONTEXT_LINUX, out IntPtr globalContext, out problem) ||
                !TryGetExport(handle, windows ? SET_DETERMINISTIC_CUDNN_WINDOWS : SET_DETERMINISTIC_CUDNN_LINUX, out IntPtr setDeterministic, out problem) ||
                !TryGetExport(handle, windows ? DETERMINISTIC_CUDNN_WINDOWS : DETERMINISTIC_CUDNN_LINUX, out IntPtr getDeterministic, out problem))
            {
                problem = string.Format(@"{0} has no {1}.", library, problem);
                return null;
            }
            var context = ((delegate* unmanaged<IntPtr>)globalContext)();
            return new LibTorchContext(context, setDeterministic, getDeterministic);
        }

        private static bool TryGetExport(IntPtr handle, string name, out IntPtr address, out string missing)
        {
            missing = NativeLibrary.TryGetExport(handle, name, out address) ? null : name;
            return missing == null;
        }
    }
}
