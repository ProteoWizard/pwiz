/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Why a computer cannot predict on the GPU, from <see cref="LibTorchRuntimes.GetCudaUnavailability"/>.
    /// </summary>
    public enum CudaUnavailability
    {
        /// <summary>The GPU can be used.</summary>
        none,
        /// <summary>No NVIDIA driver, or no CUDA device.</summary>
        no_driver,
        /// <summary>The driver supports a CUDA older than libtorch needs.</summary>
        driver_too_old,
        /// <summary>The GPU's compute capability is older than libtorch needs.</summary>
        device_too_old,
        /// <summary>This build lists no CUDA runtime packages to download.</summary>
        no_package_list
    }

    /// <summary>
    /// The libtorch runtimes an application predicts with, laid out as LibTorchRuntimeLayout.targets
    /// builds them: the CPU runtime shipped in torch\cpu beside the application, and the CUDA runtime
    /// downloaded from nuget.org into a folder of installed runtimes the first time the application
    /// predicts on a computer whose GPU can use it. Skyline installs into its Tools folder, like the
    /// search engines it downloads; Osprey into the folder its command line names. A process loads
    /// one runtime and keeps it (<see cref="LibTorchRuntime"/>).
    /// </summary>
    public class LibTorchRuntimes
    {
        public const string TORCH_FOLDER = @"torch";
        public const string CPU_FOLDER = @"cpu";
        // LibTorchPackageSet.targets names the CUDA package set's list <package id>.packages.txt
        public const string PACKAGE_SET_SUFFIX = @".packages.txt";

        private CudaDriver _cudaDriver;
        private bool _cudaDriverFound;
        private string _applicationDirectory;
        private string _runtimesDirectory;
        private LibTorchRuntimeInstaller _cudaInstaller;
        private bool _cudaInstallerRead;

        /// <param name="applicationDirectory">The folder whose torch folder holds the CPU runtime and
        /// the list of the CUDA runtime's packages: the application's.</param>
        /// <param name="runtimesDirectory">Where downloaded runtimes are installed, each in a folder
        /// named for its package set.</param>
        public LibTorchRuntimes(string applicationDirectory, string runtimesDirectory)
        {
            ApplicationDirectory = applicationDirectory;
            RuntimesDirectory = runtimesDirectory;
            PackageSource = LibTorchPackageSet.NUGET_PACKAGE_SOURCE;
        }

        /// <summary>
        /// The folder of the application, whose torch folder holds the CPU runtime and the list of
        /// the CUDA runtime's packages.
        /// </summary>
        public string ApplicationDirectory
        {
            get { return _applicationDirectory; }
            set
            {
                _applicationDirectory = value;
                _cudaInstallerRead = false;
            }
        }

        /// <summary>
        /// Where downloaded runtimes are installed.
        /// </summary>
        public string RuntimesDirectory
        {
            get { return _runtimesDirectory; }
            set
            {
                _runtimesDirectory = value;
                _cudaInstallerRead = false;
            }
        }

        /// <summary>
        /// The NuGet flat container the CUDA runtime's packages are downloaded from.
        /// </summary>
        public string PackageSource { get; set; }

        /// <summary>
        /// The NVIDIA driver, or null when the computer has none. Found when first asked for, unless set.
        /// A test that predicts sets it to null, so its results do not depend on the computer; nothing
        /// here tells a test from an application a developer runs through the test harness.
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

        /// <summary>
        /// Whether <see cref="CudaDriver"/> has been found or set, so asking for it will not load
        /// the driver now.
        /// </summary>
        public bool IsCudaDriverFound => _cudaDriverFound;

        public LibTorchRuntime CpuRuntime => new LibTorchRuntime(Path.Combine(ApplicationDirectory, TORCH_FOLDER, CPU_FOLDER));

        /// <summary>
        /// Installs the CUDA runtime, or null when this build lists no CUDA runtime packages. Read
        /// once from the package list, until a folder changes.
        /// </summary>
        public LibTorchRuntimeInstaller CudaInstaller
        {
            get
            {
                if (!_cudaInstallerRead)
                {
                    _cudaInstaller = ReadCudaInstaller();
                    _cudaInstallerRead = true;
                }
                return _cudaInstaller;
            }
        }

        private LibTorchRuntimeInstaller ReadCudaInstaller()
        {
            string torchDirectory = Path.Combine(ApplicationDirectory, TORCH_FOLDER);
            if (!Directory.Exists(torchDirectory))
                return null;
            string packageSetPath = Directory.EnumerateFiles(torchDirectory, @"*" + PACKAGE_SET_SUFFIX).SingleOrDefault();
            if (packageSetPath == null)
                return null;
            return new LibTorchRuntimeInstaller(LibTorchPackageSet.Read(packageSetPath), RuntimesDirectory);
        }

        public bool CanUseCuda => CudaDriver?.CanRunLibTorch ?? false;

        /// <summary>
        /// Why this computer cannot predict on the GPU, or <see cref="CudaUnavailability.none"/> when it
        /// can. The application puts it into words for its user, with <see cref="FormatCudaVersion"/>
        /// and <see cref="FormatComputeCapability"/> for the numbers.
        /// </summary>
        public CudaUnavailability GetCudaUnavailability()
        {
            var driver = CudaDriver;
            if (driver == null)
                return CudaUnavailability.no_driver;
            if (!driver.IsDriverNewEnough)
                return CudaUnavailability.driver_too_old;
            if (!driver.IsDeviceNewEnough)
                return CudaUnavailability.device_too_old;
            if (CudaInstaller == null)
                return CudaUnavailability.no_package_list;
            return CudaUnavailability.none;
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
            // One segment per package, sized by the package, ending at its share of the total, but at
            // least 1% long, since segment ends must increase
            long totalSize = installer.PackageSet.TotalSize;
            long sizeSoFar = 0;
            int previousEnd = 0;
            var segmentEnds = packages.Select(package => previousEnd =
                Math.Max(previousEnd + 1, (int)((sizeSoFar += package.Size) * 100 / totalSize))).ToArray();
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
                    progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangeMessage(GetDownloadingMessage(i + 1, packages.Count)));
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
                progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangeMessage(GetInstallingMessage()));
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

        /// <summary>
        /// The progress message while package <paramref name="index"/> of <paramref name="count"/>
        /// downloads. An application with localized text overrides it.
        /// </summary>
        protected virtual string GetDownloadingMessage(int index, int count)
        {
            return string.Format(CultureInfo.InvariantCulture, @"Downloading the NVIDIA CUDA runtime, package {0} of {1}", index, count);
        }

        /// <summary>
        /// The progress message while the downloaded packages are installed.
        /// </summary>
        protected virtual string GetInstallingMessage()
        {
            return @"Installing the NVIDIA CUDA runtime";
        }

        /// <summary>
        /// A CUDA version (1000 * major + 10 * minor, as the driver reports it) shown as NVIDIA shows
        /// it in every language.
        /// </summary>
        public static string FormatCudaVersion(int version)
        {
            return string.Format(CultureInfo.InvariantCulture, @"{0}.{1}", version / 1000, version % 1000 / 10);
        }

        /// <summary>
        /// A compute capability (10 * major + minor) shown as NVIDIA shows it.
        /// </summary>
        public static string FormatComputeCapability(int computeCapability)
        {
            return string.Format(CultureInfo.InvariantCulture, @"{0}.{1}", computeCapability / 10, computeCapability % 10);
        }
    }
}
