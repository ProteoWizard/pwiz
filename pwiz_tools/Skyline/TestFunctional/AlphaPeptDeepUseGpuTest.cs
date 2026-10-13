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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Models;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// The Use GPU checkbox on the AlphaPeptDeep page of Build Library, on computers whose GPU cannot
    /// predict and on one whose GPU can but has no CUDA runtime yet. The driver is made up, so the
    /// test runs the same everywhere; a real download and GPU prediction need a real GPU.
    /// </summary>
    [TestClass]
    public class AlphaPeptDeepUseGpuTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAlphaPeptDeepUseGpu()
        {
            TestFilesZip = @"TestFunctional\AlphaPeptDeepUseGpuTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() => buildLibraryDlg.AlphaPeptDeep = true);

            // Tests find no driver
            CheckUseGpuRefused(buildLibraryDlg, null,
                ModelResources.TorchRuntimes_GetCudaUnavailableReason_No_NVIDIA_GPU_was_found__Predicting_on_the_GPU_needs_an_NVIDIA_GPU_and_its_driver_);
            CheckUseGpuRefused(buildLibraryDlg, new CudaDriver(12040, 89, @"Test GPU"),
                string.Format(ModelResources.TorchRuntimes_GetCudaUnavailableReason_The_NVIDIA_driver_supports_CUDA__0___but_predicting_on_the_GPU_needs_a_driver_for_CUDA__1__or_later__Update_the_NVIDIA_driver_to_predict_on_the_GPU_,
                    @"12.4", @"12.8"));
            CheckUseGpuRefused(buildLibraryDlg, new CudaDriver(12080, 61, @"Test GPU"),
                string.Format(ModelResources.TorchRuntimes_GetCudaUnavailableReason_The__0__has_compute_capability__1___but_predicting_on_the_GPU_needs_compute_capability__2__or_later_,
                    @"Test GPU", @"6.1", @"7.0"));

            // A GPU that can predict, with no CUDA runtime installed, asks before downloading it
            if (LibTorchRuntime.Loaded == null)
            {
                RunUI(() => buildLibraryDlg.TorchRuntimes = new TorchRuntimes
                {
                    CudaDriver = new CudaDriver(12080, 89, @"Test GPU"),
                    RuntimesDirectory = TestFilesDir.GetTestPath(@"libtorch")
                });
                var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() => buildLibraryDlg.UseGpu = true);
                AssertEx.Contains(confirmDlg.Message, SettingsUIResources.BuildLibraryDlg_InstallCudaRuntime_Predicting_on_the_GPU_needs_the_NVIDIA_CUDA_runtime
                    .Substring(0, SettingsUIResources.BuildLibraryDlg_InstallCudaRuntime_Predicting_on_the_GPU_needs_the_NVIDIA_CUDA_runtime.IndexOf('{')));
                OkDialog(confirmDlg, confirmDlg.BtnCancelClick);
                RunUI(() => Assert.IsFalse(buildLibraryDlg.UseGpu));
            }

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.CancelDialog);
        }

        /// <summary>
        /// Checks Use GPU with the given driver, before any runtime is loaded, and expects the message
        /// saying why the GPU cannot predict, after which the box is unchecked again.
        /// </summary>
        private static void CheckUseGpuRefused(BuildLibraryDlg buildLibraryDlg, CudaDriver driver, string expectedMessage)
        {
            if (LibTorchRuntime.Loaded != null)
                expectedMessage = SettingsUIResources.BuildLibraryDlg_cbUseGpu_CheckedChanged_Skyline_chooses_between_the_CPU_and_the_GPU_when_it_first_predicts_a_library__Restart_Skyline_to_change_it_;
            RunUI(() =>
            {
                buildLibraryDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = driver };
                Assert.IsFalse(buildLibraryDlg.UseGpu);
            });
            var messageDlg = ShowDialog<MessageDlg>(() => buildLibraryDlg.UseGpu = true);
            Assert.AreEqual(expectedMessage, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            RunUI(() => Assert.IsFalse(buildLibraryDlg.UseGpu));
        }
    }
}
