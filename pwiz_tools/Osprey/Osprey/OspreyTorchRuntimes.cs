/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.IO;
using System.Net.Http;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;

namespace pwiz.Osprey
{
    /// <summary>
    /// The libtorch runtime Osprey predicts libraries with: the CPU runtime shipped in torch\cpu
    /// beside Osprey.exe, or the NVIDIA CUDA runtime downloaded on request (<c>--install-cuda-runtime</c>)
    /// into the folder <c>--torch-runtimes</c> names, <see cref="DefaultRuntimesDirectory"/> otherwise.
    /// Skyline passes its Tools folder, so one download serves its AlphaPeptDeep and Osprey. One build
    /// serves the CPU and the GPU: the runtime is chosen when the first library phase runs
    /// (<see cref="Load"/>), a process keeps the first libtorch it loads, and the log says which.
    /// </summary>
    internal sealed class OspreyTorchRuntimes
    {
        public const string DEVICE_AUTO = @"auto";
        public const string DEVICE_GPU = @"gpu";
        public const string DEVICE_CPU = @"cpu";

        private const string OSPREY_FOLDER = @"Osprey";

        private readonly LocalizedRuntimes _runtimes;

        public OspreyTorchRuntimes(LibraryBuildConfig build)
            : this(build.TorchRuntimesDirectory)
        {
        }

        /// <param name="runtimesDirectory">The folder of installed runtimes, or null for <see cref="DefaultRuntimesDirectory"/>.</param>
        public OspreyTorchRuntimes(string runtimesDirectory)
        {
            _runtimes = new LocalizedRuntimes(AppContext.BaseDirectory, runtimesDirectory ?? DefaultRuntimesDirectory);
        }

        /// <summary>
        /// Where the CUDA runtime is installed when the command line names no folder: under the
        /// user's local application data, since a per-machine install of Osprey is not writable.
        /// </summary>
        public static string DefaultRuntimesDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), OSPREY_FOLDER, LibTorchRuntimes.TORCH_FOLDER);

        /// <summary>The runtimes themselves, for a test to set the driver.</summary>
        public LibTorchRuntimes Runtimes => _runtimes;

        /// <summary>The CUDA runtime's download size in GB, as the log states it.</summary>
        public static string FormatGigabytes(long bytes)
        {
            return (bytes / 1e9).ToString(@"0.0", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Why a run asking for the GPU (<paramref name="device"/> gpu) cannot have it, or null when it
        /// can, or when it asks for auto or the CPU. Answered before the run starts, from the driver and
        /// the installed runtimes alone, so a run that would end up on the CPU fails at once and says
        /// what to do instead of running slowly and needing troubleshooting.
        /// </summary>
        public string CheckGpu(string device)
        {
            if (!string.Equals(device, DEVICE_GPU, StringComparison.OrdinalIgnoreCase))
                return null;
            var unavailability = _runtimes.GetCudaUnavailability();
            if (unavailability == CudaUnavailability.none && _runtimes.IsCudaInstalled)
                return null;
            return DescribeGpuUnavailable(unavailability);
        }

        /// <summary>
        /// Loads the runtime for the device asked for (auto, gpu or cpu; null is auto), logs the choice
        /// with its reason, and returns the device the library phases predict on: gpu or cpu. A run
        /// asking for the GPU has passed <see cref="CheckGpu"/> by now; the fallback below stands for
        /// a host that did not ask.
        /// </summary>
        public string Load(string device)
        {
            bool wantGpu = !string.Equals(device, DEVICE_CPU, StringComparison.OrdinalIgnoreCase);
            var runtime = LibTorchRuntime.Load(_runtimes, wantGpu);
            if (wantGpu && runtime.IsCuda)
            {
                Program.LogInfo(string.Format(OspreyResources.OspreyTorchRuntimes_Load_Predicting_on_the_GPU___0___with_the_NVIDIA_CUDA_runtime_in__1_,
                    _runtimes.CudaDriver?.DeviceName ?? OspreyResources.OspreyTorchRuntimes_Load_NVIDIA_GPU, runtime.Directory));
                return DEVICE_GPU;
            }
            // Why not the GPU: always when it was asked for, and under auto when it could be used
            // once the runtime is downloaded - the one case auto would otherwise be silent about
            string reason = null;
            if (wantGpu)
            {
                var unavailability = _runtimes.GetCudaUnavailability();
                if (unavailability == CudaUnavailability.none || string.Equals(device, DEVICE_GPU, StringComparison.OrdinalIgnoreCase))
                    reason = DescribeGpuUnavailable(unavailability);
            }
            Program.LogInfo(reason == null
                ? OspreyResources.OspreyTorchRuntimes_Load_Predicting_on_the_CPU
                : string.Format(OspreyResources.OspreyTorchRuntimes_Load_Predicting_on_the_CPU___0_, reason));
            return DEVICE_CPU;
        }

        /// <summary>
        /// Downloads and installs the CUDA runtime, for <c>--install-cuda-runtime</c>, and returns the
        /// exit code. A computer that cannot use the GPU still installs, with a note: a cluster's
        /// login node downloads for its GPU nodes.
        /// </summary>
        public int Install()
        {
            var installer = _runtimes.CudaInstaller;
            if (installer == null)
            {
                Program.LogError(OspreyResources.OspreyTorchRuntimes_Install_This_build_of_Osprey_lists_no_NVIDIA_CUDA_runtime_packages_to_download_);
                return Program.EXIT_CODE_FAILURE_TO_START;
            }
            if (_runtimes.IsCudaInstalled)
            {
                Program.LogInfo(string.Format(OspreyResources.OspreyTorchRuntimes_Install_The_NVIDIA_CUDA_runtime_is_already_installed_in__0_, installer.RuntimeDirectory));
                return Program.EXIT_CODE_SUCCESS;
            }
            var unavailability = _runtimes.GetCudaUnavailability();
            if (unavailability != CudaUnavailability.none)
            {
                Program.LogWarning(string.Format(OspreyResources.OspreyTorchRuntimes_Install_This_computer_cannot_predict_on_the_GPU___0___the_runtime_is_installed_all_the_same__for_a_computer_that_can_,
                    DescribeGpuUnavailable(unavailability)));
            }
            Program.LogInfo(string.Format(OspreyResources.OspreyTorchRuntimes_Install_Downloading_the_NVIDIA_CUDA_runtime__about__0__GB__into__1_,
                FormatGigabytes(installer.PackageSet.TotalSize), installer.RuntimeDirectory));
            try
            {
                IProgressStatus status = new ProgressStatus();
                _runtimes.InstallCuda(new ConsoleProgressMonitor(), ref status);
            }
            catch (Exception x) when (x is IOException || x is HttpRequestException || x is InvalidDataException || x is UnauthorizedAccessException)
            {
                Program.LogError(string.Format(OspreyResources.OspreyTorchRuntimes_Install_Failed_to_install_the_NVIDIA_CUDA_runtime___0_, x.Message));
                return Program.EXIT_CODE_RAN_WITH_ERRORS;
            }
            Program.LogInfo(string.Format(OspreyResources.OspreyTorchRuntimes_Install_Installed_the_NVIDIA_CUDA_runtime_in__0_, installer.RuntimeDirectory));
            return Program.EXIT_CODE_SUCCESS;
        }

        /// <summary>
        /// Why the GPU is not predicted on, for the log: the computer's driver or GPU, this build, or
        /// the runtime not yet downloaded (<paramref name="unavailability"/> none).
        /// </summary>
        public string DescribeGpuUnavailable(CudaUnavailability unavailability)
        {
            var driver = _runtimes.CudaDriver;
            switch (unavailability)
            {
                case CudaUnavailability.no_driver:
                    return OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_no_NVIDIA_GPU_was_found__predicting_on_the_GPU_needs_an_NVIDIA_GPU_and_its_driver;
                case CudaUnavailability.driver_too_old:
                    return string.Format(OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_the_NVIDIA_driver_supports_CUDA__0___but_predicting_on_the_GPU_needs_a_driver_for_CUDA__1__or_later,
                        LibTorchRuntimes.FormatCudaVersion(driver.Version), LibTorchRuntimes.FormatCudaVersion(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION));
                case CudaUnavailability.device_too_old:
                    return string.Format(OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_the__0__has_compute_capability__1___but_predicting_on_the_GPU_needs__2__or_later,
                        driver.DeviceName ?? OspreyResources.OspreyTorchRuntimes_Load_NVIDIA_GPU,
                        LibTorchRuntimes.FormatComputeCapability(driver.ComputeCapability), LibTorchRuntimes.FormatComputeCapability(CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY));
                case CudaUnavailability.no_package_list:
                    return OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_this_build_of_Osprey_lists_no_NVIDIA_CUDA_runtime_packages;
                default:
                    // None: the GPU could be used, so the package list is there (the installer is not null)
                    return string.Format(OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_the_NVIDIA_CUDA_runtime_is_not_installed_in__0___run__1__to_download_it__about__2__GB_,
                        _runtimes.RuntimesDirectory, OspreyArgNames.Text(OspreyArgNames.INSTALL_CUDA_RUNTIME),
                        FormatGigabytes(_runtimes.CudaInstaller.PackageSet.TotalSize));
            }
        }

        /// <summary>
        /// The runtimes with the download's progress messages in Osprey's words.
        /// </summary>
        private sealed class LocalizedRuntimes : LibTorchRuntimes
        {
            public LocalizedRuntimes(string applicationDirectory, string runtimesDirectory)
                : base(applicationDirectory, runtimesDirectory)
            {
            }

            protected override string GetDownloadingMessage(int index, int count)
            {
                return string.Format(OspreyResources.OspreyTorchRuntimes_GetDownloadingMessage_Downloading_the_NVIDIA_CUDA_runtime__package__0__of__1_, index, count);
            }

            protected override string GetInstallingMessage()
            {
                return OspreyResources.OspreyTorchRuntimes_GetInstallingMessage_Installing_the_NVIDIA_CUDA_runtime;
            }
        }

        /// <summary>
        /// Writes the download's progress to the log: each package's message once, then the overall
        /// percent at every tenth. The HTTP client reports every chunk, appending the bytes so far
        /// to the message on lines of its own, so the message's first line is what names the package.
        /// </summary>
        private sealed class ConsoleProgressMonitor : IProgressMonitor
        {
            private string _message;
            private int _lastTenth;

            public bool IsCanceled => false;

            public bool HasUI => false;

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                string message = status.Message?.Split('\n')[0].TrimEnd();
                if (!string.IsNullOrEmpty(message) && message != _message)
                {
                    _message = message;
                    Program.LogInfo(message);
                }
                int tenth = status.PercentComplete / 10;
                if (tenth > _lastTenth && status.PercentComplete < 100)
                {
                    _lastTenth = tenth;
                    Program.LogInfo(string.Format(CultureInfo.CurrentCulture, @"  {0}%", status.PercentComplete));
                }
                return UpdateProgressResponse.normal;
            }
        }
    }
}
