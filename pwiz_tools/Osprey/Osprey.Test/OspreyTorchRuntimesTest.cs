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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Models;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The options that choose the libtorch runtime and where the CUDA runtime lives, what the
    /// hosted CarafeSharp settings get for them, the reasons Osprey gives for predicting on the CPU,
    /// and the CPU runtime loading from the torch folder beside the executable.
    /// </summary>
    [TestClass]
    public class OspreyTorchRuntimesTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestTorchRuntimeOptions()
        {
            // --device is auto unless given. The hosted CarafeSharp settings never see auto - they
            // get CarafeSharp's own default, which the workflow replaces with the device of the
            // runtime it loads - and the device as given otherwise
            var config = OspreyCommandArgs.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_FASTA + @"p.fasta"), out var settings);
            Assert.IsNull(config.LibraryBuild.Device);
            Assert.AreNotEqual(OspreyTorchRuntimes.DEVICE_AUTO, settings.Library.Device);
            config = OspreyCommandArgs.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_FASTA + @"p.fasta", OspreyCommandArgs.ARG_DEVICE + @"AUTO"), out settings);
            Assert.AreEqual(OspreyTorchRuntimes.DEVICE_AUTO, config.LibraryBuild.Device);
            Assert.AreNotEqual(OspreyTorchRuntimes.DEVICE_AUTO, settings.Library.Device);
            config = OspreyCommandArgs.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_FASTA + @"p.fasta", OspreyCommandArgs.ARG_DEVICE + @"GPU"), out settings);
            Assert.AreEqual(OspreyTorchRuntimes.DEVICE_GPU, config.LibraryBuild.Device);
            Assert.AreEqual(OspreyTorchRuntimes.DEVICE_GPU, settings.Library.Device);

            // The runtimes folder and the install need no --fasta: the install is a command of its own
            config = OspreyCommandArgs.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_INSTALL_CUDA_RUNTIME.ArgumentText,
                OspreyCommandArgs.ARG_TORCH_RUNTIMES + @"d:\torch"));
            Assert.IsTrue(config.LibraryBuild.InstallCudaRuntime);
            Assert.AreEqual(@"d:\torch", config.LibraryBuild.TorchRuntimesDirectory);
            Assert.IsFalse(OspreyCommandArgs.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_FASTA + @"p.fasta")).LibraryBuild.InstallCudaRuntime);
            StringAssert.EndsWith(OspreyTorchRuntimes.DefaultRuntimesDirectory, Path.Combine(@"Osprey", LibTorchRuntimes.TORCH_FOLDER));

            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"OspreyTorch_" + Guid.NewGuid().ToString(@"N"));
            try
            {
                // Each reason the GPU is not predicted on, with the computer's numbers in it
                var torch = new OspreyTorchRuntimes(folder);
                torch.Runtimes.CudaDriver = null;
                Assert.AreEqual(OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_no_NVIDIA_GPU_was_found__predicting_on_the_GPU_needs_an_NVIDIA_GPU_and_its_driver,
                    torch.DescribeGpuUnavailable(torch.Runtimes.GetCudaUnavailability()));
                torch.Runtimes.CudaDriver = new CudaDriver(12000, CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY, @"Test GPU");
                Assert.AreEqual(string.Format(OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_the_NVIDIA_driver_supports_CUDA__0___but_predicting_on_the_GPU_needs_a_driver_for_CUDA__1__or_later,
                        @"12.0", LibTorchRuntimes.FormatCudaVersion(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION)),
                    torch.DescribeGpuUnavailable(torch.Runtimes.GetCudaUnavailability()));
                torch.Runtimes.CudaDriver = new CudaDriver(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION, 60, @"Test GPU");
                Assert.AreEqual(string.Format(OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_the__0__has_compute_capability__1___but_predicting_on_the_GPU_needs__2__or_later,
                        @"Test GPU", @"6.0", LibTorchRuntimes.FormatComputeCapability(CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY)),
                    torch.DescribeGpuUnavailable(torch.Runtimes.GetCudaUnavailability()));

                // A GPU that could be used: this build's package list is in the torch folder beside
                // the test assembly, as beside Osprey.exe, so the only thing missing is the download
                torch.Runtimes.CudaDriver = new CudaDriver(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION, CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY, @"Test GPU");
                Assert.AreEqual(CudaUnavailability.none, torch.Runtimes.GetCudaUnavailability());
                Assert.IsFalse(torch.Runtimes.IsCudaInstalled);
                string downloadSize = OspreyTorchRuntimes.FormatGigabytes(torch.Runtimes.CudaInstaller.PackageSet.TotalSize);
                Assert.IsTrue(double.Parse(downloadSize, CultureInfo.CurrentCulture) > 1, downloadSize);
                Assert.AreEqual(string.Format(OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_the_NVIDIA_CUDA_runtime_is_not_installed_in__0___run__1__to_download_it__about__2__GB_,
                        folder, OspreyArgNames.Text(OspreyArgNames.INSTALL_CUDA_RUNTIME), downloadSize),
                    torch.DescribeGpuUnavailable(CudaUnavailability.none));

                // --device gpu is a requirement: with a usable GPU and no runtime the check names the
                // install, with no driver it names the driver, and auto or cpu is never refused
                Assert.AreEqual(torch.DescribeGpuUnavailable(CudaUnavailability.none), torch.CheckGpu(OspreyTorchRuntimes.DEVICE_GPU));
                Assert.IsNull(torch.CheckGpu(OspreyTorchRuntimes.DEVICE_AUTO));
                Assert.IsNull(torch.CheckGpu(null));
                Assert.IsNull(torch.CheckGpu(OspreyTorchRuntimes.DEVICE_CPU));
                torch.Runtimes.CudaDriver = null;
                Assert.AreEqual(OspreyResources.OspreyTorchRuntimes_DescribeGpuUnavailable_no_NVIDIA_GPU_was_found__predicting_on_the_GPU_needs_an_NVIDIA_GPU_and_its_driver,
                    torch.CheckGpu(OspreyTorchRuntimes.DEVICE_GPU));
                torch.Runtimes.CudaDriver = new CudaDriver(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION, CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY, @"Test GPU");

                // The CPU runtime loads from the torch folder beside the executable, never from beside
                // TorchSharp.dll, and cpu is what the library phases get; auto with no CUDA runtime
                // installed lands on the CPU too
                Assert.AreEqual(OspreyTorchRuntimes.DEVICE_CPU, torch.Load(OspreyTorchRuntimes.DEVICE_CPU));
                Assert.IsNotNull(LibTorchRuntime.Loaded);
                Assert.IsFalse(LibTorchRuntime.Loaded.IsCuda);
                Assert.AreEqual(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, LibTorchRuntimes.TORCH_FOLDER, LibTorchRuntimes.CPU_FOLDER)),
                    LibTorchRuntime.Loaded.Directory, true);
                Assert.AreEqual(OspreyTorchRuntimes.DEVICE_CPU, torch.Load(null));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
