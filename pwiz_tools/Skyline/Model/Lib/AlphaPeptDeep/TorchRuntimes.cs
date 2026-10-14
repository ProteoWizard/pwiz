/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using pwiz.CarafeSharp.Models;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Tools;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    /// <summary>
    /// The libtorch runtimes AlphaPeptDeep prediction runs on: the CPU runtime Skyline ships in
    /// torch\cpu, and the CUDA runtime it downloads from nuget.org the first time it predicts on a
    /// computer whose GPU can use it, into the Tools folder, like the search engines Skyline downloads.
    /// </summary>
    public class TorchRuntimes
    {
        private const string TORCH_FOLDER = @"torch";
        private const string CPU_FOLDER = @"cpu";
        // Skyline.csproj names the CUDA package set's list <package id>.packages.txt
        private const string PACKAGE_SET_SUFFIX = @".packages.txt";

        private CudaDriver _cudaDriver;
        private bool _cudaDriverFound;

        public TorchRuntimes()
        {
            ApplicationDirectory = Path.GetDirectoryName(typeof(TorchRuntimes).Assembly.Location);
            RuntimesDirectory = ToolDescriptionHelpers.GetToolsDirectory();
            PackageSource = LibTorchPackageSet.NUGET_PACKAGE_SOURCE;
            // Tests predict on the CPU, so their results do not depend on the computer, unless one sets CudaDriver
            if (Program.UnitTest)
                CudaDriver = null;
        }

        /// <summary>
        /// The folder of the Skyline executable, whose torch folder holds the CPU runtime and the
        /// list of the CUDA runtime's packages.
        /// </summary>
        public string ApplicationDirectory { get; set; }

        /// <summary>
        /// Where downloaded runtimes are installed.
        /// </summary>
        public string RuntimesDirectory { get; set; }

        /// <summary>
        /// The NuGet flat container the CUDA runtime's packages are downloaded from.
        /// </summary>
        public string PackageSource { get; set; }

        /// <summary>
        /// The NVIDIA driver, or null when the computer has none. Found when first asked for, unless set.
        /// </summary>
        public CudaDriver CudaDriver
        {
            get
            {
                if (!_cudaDriverFound)
                {
                    _cudaDriver = CudaDriver.Find();
                    _cudaDriverFound = true;
                }
                return _cudaDriver;
            }
            set
            {
                _cudaDriver = value;
                _cudaDriverFound = true;
            }
        }

        public LibTorchRuntime CpuRuntime => new LibTorchRuntime(Path.Combine(ApplicationDirectory, TORCH_FOLDER, CPU_FOLDER));

        /// <summary>
        /// Installs the CUDA runtime, or null when this build lists no CUDA runtime packages.
        /// </summary>
        public LibTorchRuntimeInstaller CudaInstaller
        {
            get
            {
                string torchDirectory = Path.Combine(ApplicationDirectory, TORCH_FOLDER);
                if (!Directory.Exists(torchDirectory))
                    return null;
                string packageSetPath = Directory.EnumerateFiles(torchDirectory, @"*" + PACKAGE_SET_SUFFIX).SingleOrDefault();
                if (packageSetPath == null)
                    return null;
                return new LibTorchRuntimeInstaller(LibTorchPackageSet.Read(packageSetPath), RuntimesDirectory);
            }
        }

        public bool CanUseCuda => CudaDriver?.CanRunLibTorch ?? false;

        /// <summary>
        /// Why this computer cannot predict on the GPU, for the user, or null when it can.
        /// </summary>
        public string GetCudaUnavailableReason()
        {
            var driver = CudaDriver;
            if (driver == null)
                return ModelResources.TorchRuntimes_GetCudaUnavailableReason_No_NVIDIA_GPU_was_found__Predicting_on_the_GPU_needs_an_NVIDIA_GPU_and_its_driver_;
            if (!driver.IsDriverNewEnough)
            {
                return string.Format(ModelResources.TorchRuntimes_GetCudaUnavailableReason_The_NVIDIA_driver_supports_CUDA__0___but_predicting_on_the_GPU_needs_a_driver_for_CUDA__1__or_later__Update_the_NVIDIA_driver_to_predict_on_the_GPU_,
                    FormatCudaVersion(driver.Version), FormatCudaVersion(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION));
            }
            if (!driver.IsDeviceNewEnough)
            {
                return string.Format(ModelResources.TorchRuntimes_GetCudaUnavailableReason_The__0__has_compute_capability__1___but_predicting_on_the_GPU_needs_compute_capability__2__or_later_,
                    driver.DeviceName ?? ModelResources.TorchRuntimes_GetCudaUnavailableReason_NVIDIA_GPU,
                    FormatComputeCapability(driver.ComputeCapability), FormatComputeCapability(CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY));
            }
            if (CudaInstaller == null)
                return ModelResources.TorchRuntimes_GetCudaUnavailableReason_This_build_of_Skyline_does_not_list_the_NVIDIA_CUDA_runtime_packages_;
            return null;
        }

        public bool IsCudaInstalled => CudaInstaller?.IsInstalled ?? false;

        /// <summary>
        /// Whether this process can still choose between the CPU and the GPU: it has not loaded a libtorch
        /// yet, and a process cannot change the one it loaded.
        /// </summary>
        public bool CanChooseRuntime => LibTorchRuntime.Loaded == null;

        /// <summary>
        /// Loads the CUDA runtime when asked to, it is installed and the GPU can use it, otherwise the CPU
        /// runtime, unless this process already loaded one, and returns the runtime loaded.
        /// </summary>
        public LibTorchRuntime Load(bool useCuda)
        {
            var runtime = LibTorchRuntime.Loaded;
            if (runtime == null)
            {
                runtime = useCuda && CanUseCuda && IsCudaInstalled ? CudaInstaller.Runtime : CpuRuntime;
                runtime.Load();
            }
            return runtime;
        }

        /// <summary>
        /// Downloads the CUDA runtime's packages one at a time and installs the runtime from them.
        /// </summary>
        public void InstallCuda(IProgressMonitor progressMonitor, ref IProgressStatus progressStatus)
        {
            var installer = CudaInstaller;
            var packages = installer.PackageSet.Packages;
            // One segment per package, sized by the package, ending at its share of the total
            long totalSize = installer.PackageSet.TotalSize;
            long sizeSoFar = 0;
            var segmentEnds = packages.Select(package => (int)((sizeSoFar += package.Size) * 100 / totalSize)).ToArray();
            progressStatus = progressStatus.ChangeSegments(0, ImmutableList.ValueOf(segmentEnds));

            Directory.CreateDirectory(RuntimesDirectory);
            installer.Begin();
            bool completed = false;
            try
            {
                for (int i = 0; i < packages.Count; i++)
                {
                    if (progressMonitor.IsCanceled)
                        throw new OperationCanceledException();
                    if (i > 0)
                        progressStatus = progressStatus.NextSegment();

                    var package = packages[i];
                    progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangeMessage(string.Format(
                        ModelResources.TorchRuntimes_InstallCuda_Downloading_the_NVIDIA_CUDA_runtime__package__0__of__1__,
                        i + 1, packages.Count)));
                    string packagePath = Path.Combine(RuntimesDirectory, package.FileName);
                    try
                    {
                        using (var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus))
                        {
                            httpClient.DownloadFile(package.GetUri(PackageSource), packagePath, package.Size);
                        }
                        installer.AddPackage(package, packagePath);
                    }
                    finally
                    {
                        FileEx.SafeDelete(packagePath, true);
                    }
                }
                progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangeMessage(
                    ModelResources.TorchRuntimes_InstallCuda_Installing_the_NVIDIA_CUDA_runtime));
                installer.Complete();
                completed = true;
            }
            finally
            {
                if (!completed)
                    installer.Abandon();
            }
            progressMonitor.UpdateProgress(progressStatus = progressStatus.Complete());
        }

        // CUDA versions are 1000 * major + 10 * minor, shown as NVIDIA shows them in every language
        private static string FormatCudaVersion(int version)
        {
            return string.Format(CultureInfo.InvariantCulture, @"{0}.{1}", version / 1000, version % 1000 / 10);
        }

        // Compute capabilities are 10 * major + minor
        private static string FormatComputeCapability(int computeCapability)
        {
            return string.Format(CultureInfo.InvariantCulture, @"{0}.{1}", computeCapability / 10, computeCapability % 10);
        }
    }
}
