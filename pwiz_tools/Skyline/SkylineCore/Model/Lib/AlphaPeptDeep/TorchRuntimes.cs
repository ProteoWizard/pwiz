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

using System.IO;
using pwiz.CarafeSharp.Core;
using pwiz.Skyline.Model.Tools;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    /// <summary>
    /// The libtorch runtimes Osprey predicts with for Skyline, in Build Library and the Osprey Search
    /// wizard: the CPU runtime the Osprey package ships in its torch\cpu, and the CUDA runtime Skyline
    /// downloads from nuget.org the first time a user asks for the GPU on a computer whose GPU can use
    /// it, into the Tools folder, like the search engines Skyline downloads, where Osprey is pointed at
    /// it (--torch-runtimes). The detection and the download are <see cref="LibTorchRuntimes"/>,
    /// shared with Osprey, which reads the package list from the installed Osprey folder, so the CUDA
    /// version is the one the Osprey Skyline runs was built with; this supplies Skyline's folders
    /// and its words. Nothing loads a runtime into Skyline's process.
    /// </summary>
    public class TorchRuntimes : LibTorchRuntimes
    {
        public TorchRuntimes()
            : base(Path.GetDirectoryName(OspreyHelpers.OspreyBinary), ToolDescriptionHelpers.GetToolsDirectory())
        {
        }

        /// <summary>
        /// Why this computer cannot predict on the GPU, for the user, or null when it can.
        /// </summary>
        public string GetCudaUnavailableReason()
        {
            switch (GetCudaUnavailability())
            {
                case CudaUnavailability.no_driver:
                    return ModelResources.TorchRuntimes_GetCudaUnavailableReason_No_NVIDIA_GPU_was_found__Predicting_on_the_GPU_needs_an_NVIDIA_GPU_and_its_driver_;
                case CudaUnavailability.driver_too_old:
                    return string.Format(ModelResources.TorchRuntimes_GetCudaUnavailableReason_The_NVIDIA_driver_supports_CUDA__0___but_predicting_on_the_GPU_needs_a_driver_for_CUDA__1__or_later__Update_the_NVIDIA_driver_to_predict_on_the_GPU_,
                        FormatCudaVersion(CudaDriver.Version), FormatCudaVersion(CudaDriver.MIN_LIBTORCH_DRIVER_VERSION));
                case CudaUnavailability.device_too_old:
                    return string.Format(ModelResources.TorchRuntimes_GetCudaUnavailableReason_The__0__has_compute_capability__1___but_predicting_on_the_GPU_needs_compute_capability__2__or_later_,
                        CudaDriver.DeviceName ?? ModelResources.TorchRuntimes_GetCudaUnavailableReason_NVIDIA_GPU,
                        FormatComputeCapability(CudaDriver.ComputeCapability), FormatComputeCapability(CudaDriver.MIN_LIBTORCH_COMPUTE_CAPABILITY));
                case CudaUnavailability.no_package_list:
                    return ModelResources.TorchRuntimes_GetCudaUnavailableReason_Osprey__which_predicts_the_library__is_not_installed__so_its_NVIDIA_CUDA_runtime_packages_are_not_known_;
                default:
                    return null;
            }
        }

        protected override string GetDownloadingMessage(int index, int count)
        {
            return string.Format(ModelResources.TorchRuntimes_InstallCuda_Downloading_the_NVIDIA_CUDA_runtime__package__0__of__1__, index, count);
        }

        protected override string GetInstallingMessage()
        {
            return ModelResources.TorchRuntimes_InstallCuda_Installing_the_NVIDIA_CUDA_runtime;
        }
    }
}
