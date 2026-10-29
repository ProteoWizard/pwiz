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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The runtime choice an application makes from the driver it finds and the runtimes it has,
    /// without loading any: each reason the GPU cannot be used, in the order they are checked, and
    /// the package list and the installed marker that make the CUDA runtime available.
    /// </summary>
    [TestClass]
    public class LibTorchRuntimesTest
    {
        private const string PACKAGE_SET_ID = @"libtorch-test-win-x64";
        private const string VERSION = @"1.0.0";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestLibTorchRuntimesChoice()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"LibTorchRuntimes_" + Guid.NewGuid().ToString(@"N"));
            try
            {
                string appDirectory = Path.Combine(folder, @"app");
                string torchDirectory = Path.Combine(appDirectory, LibTorchRuntimes.TORCH_FOLDER);
                Directory.CreateDirectory(torchDirectory);
                string runtimesDirectory = Path.Combine(folder, @"runtimes");
                var runtimes = new LibTorchRuntimes(appDirectory, runtimesDirectory) { CudaDriver = null };

                // No driver: the first reason, before anything else is looked at
                Assert.IsTrue(runtimes.IsCudaDriverFound);
                Assert.AreEqual(CudaUnavailability.no_driver, runtimes.GetCudaUnavailability());
                Assert.IsFalse(runtimes.CanUseCuda);
                Assert.IsNull(runtimes.CudaInstaller);
                Assert.IsFalse(runtimes.IsCudaInstalled);
                Assert.AreEqual(Path.Combine(torchDirectory, LibTorchRuntimes.CPU_FOLDER), runtimes.CpuRuntimeDirectory);

                // The driver too old, then the GPU too old, then no package list in this build
                runtimes.CudaDriver = new CudaDriver(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION - 10, CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY, @"Test GPU");
                Assert.AreEqual(CudaUnavailability.driver_too_old, runtimes.GetCudaUnavailability());
                Assert.IsFalse(runtimes.CanUseCuda);
                runtimes.CudaDriver = new CudaDriver(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION, CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY - 1, @"Test GPU");
                Assert.AreEqual(CudaUnavailability.device_too_old, runtimes.GetCudaUnavailability());
                Assert.IsFalse(runtimes.CanUseCuda);
                runtimes.CudaDriver = new CudaDriver(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION, CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY, @"Test GPU");
                Assert.AreEqual(CudaUnavailability.no_package_list, runtimes.GetCudaUnavailability());
                Assert.IsTrue(runtimes.CanUseCuda);

                // The package list makes the runtime installable; the installed marker makes it loadable
                File.WriteAllText(Path.Combine(torchDirectory, PACKAGE_SET_ID + LibTorchRuntimes.PACKAGE_SET_SUFFIX),
                    PACKAGE_SET_ID + "\t" + VERSION + "\npart1\t" + VERSION + "\tabc=\t1000\n");
                runtimes.ApplicationDirectory = appDirectory; // Re-reads the list
                Assert.AreEqual(CudaUnavailability.none, runtimes.GetCudaUnavailability());
                Assert.IsNotNull(runtimes.CudaInstaller);
                Assert.AreEqual(1000, runtimes.CudaInstaller.PackageSet.TotalSize);
                Assert.AreEqual(Path.Combine(runtimesDirectory, PACKAGE_SET_ID + @"." + VERSION), runtimes.CudaInstaller.RuntimeDirectory);
                Assert.IsFalse(runtimes.IsCudaInstalled);
                Directory.CreateDirectory(runtimes.CudaInstaller.RuntimeDirectory);
                File.WriteAllText(Path.Combine(runtimes.CudaInstaller.RuntimeDirectory, LibTorchRuntimeInstaller.INSTALLED_FILE_NAME), PACKAGE_SET_ID);
                Assert.IsTrue(runtimes.IsCudaInstalled);

                // The numbers as NVIDIA shows them
                Assert.AreEqual(@"12.8", LibTorchRuntimes.FormatCudaVersion(12080));
                Assert.AreEqual(@"7.0", LibTorchRuntimes.FormatComputeCapability(70));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
