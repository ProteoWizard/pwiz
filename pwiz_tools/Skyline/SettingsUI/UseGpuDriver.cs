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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Drives a Use GPU checkbox for a form that has Osprey predict a spectral library: Build Library
    /// and the Osprey Search wizard, both of which run Osprey as a child process that chooses its
    /// libtorch runtime afresh every time. Checking the box says why when this computer's GPU cannot
    /// be used, and otherwise downloads the NVIDIA CUDA runtime first if it is not installed; the box
    /// starts as the user last left it when the GPU can be used and the runtime is installed.
    /// </summary>
    public class UseGpuDriver
    {
        private readonly Form _owner;
        private readonly CheckBox _checkBox;
        private TorchRuntimes _torchRuntimes;
        private bool _updating;

        public UseGpuDriver(Form owner, CheckBox checkBox)
        {
            _owner = owner;
            _checkBox = checkBox;
        }

        /// <summary>
        /// The libtorch runtimes predicted with, which decide whether Use GPU can be checked. Setting
        /// them (a test's driver and folder) refreshes the box.
        /// </summary>
        public TorchRuntimes TorchRuntimes
        {
            get { return _torchRuntimes ??= new TorchRuntimes(); }
            set
            {
                _torchRuntimes = value;
                Update();
            }
        }

        public bool UseGpu
        {
            get { return _checkBox.Checked; }
            set { _checkBox.Checked = value; }
        }

        /// <summary>
        /// Checks Use GPU as the user last left it, if the GPU can be used and the CUDA runtime is
        /// installed. Finding the GPU loads the NVIDIA driver and initializes CUDA, which can take a
        /// moment, so the first time that runs behind an indeterminate wait, shown if it takes long.
        /// </summary>
        public void Update()
        {
            var runtimes = TorchRuntimes;
            if (!runtimes.IsCudaDriverFound)
            {
                using var longWaitDlg = new LongWaitDlg(allowCancel: false);
                longWaitDlg.Message = SettingsUIResources.BuildLibraryDlg_UpdateUseGpu_Looking_for_an_NVIDIA_GPU;
                longWaitDlg.PerformWork(_owner, 500, () =>
                {
                    var unused = runtimes.CudaDriver;
                });
            }
            SetUseGpu(runtimes.CanUseCuda && runtimes.IsCudaInstalled && Settings.Default.AlphaPeptDeepUseGpu);
        }

        /// <summary>
        /// The checkbox's CheckedChanged: a check that cannot be honored is explained and undone;
        /// one that needs the CUDA runtime offers its download, and is undone if that is declined.
        /// </summary>
        public void CheckedChanged()
        {
            if (_updating)
                return;

            string reason = _checkBox.Checked ? TorchRuntimes.GetCudaUnavailableReason() : null;
            if (reason != null)
            {
                MessageDlg.Show(_owner, reason);
                SetUseGpu(!_checkBox.Checked);
            }
            else if (_checkBox.Checked && !TorchRuntimes.IsCudaInstalled && !InstallCudaRuntime())
            {
                SetUseGpu(false);
            }
        }

        /// <summary>Remembers the choice for the next form.</summary>
        public void SaveSetting()
        {
            Settings.Default.AlphaPeptDeepUseGpu = UseGpu;
        }

        /// <summary>
        /// Checks or unchecks Use GPU without asking anything of the user.
        /// </summary>
        private void SetUseGpu(bool useGpu)
        {
            _updating = true;
            try
            {
                _checkBox.Checked = useGpu;
            }
            finally
            {
                _updating = false;
            }
        }

        /// <summary>
        /// Downloads and installs the CUDA runtime, once the user agrees to its size. Returns false if
        /// the user declines, or the download fails or is canceled.
        /// </summary>
        private bool InstallCudaRuntime()
        {
            long downloadSize = TorchRuntimes.CudaInstaller.PackageSet.TotalSize;
            string message = string.Format(SettingsUIResources.BuildLibraryDlg_InstallCudaRuntime_Predicting_on_the_GPU_needs_the_NVIDIA_CUDA_runtime,
                (downloadSize / 1e9).ToString(@"0.0", LocalizationHelper.CurrentCulture));
            using (var confirmDlg = new MultiButtonMsgDlg(message, SettingsUIResources.BuildLibraryDlg_InstallCudaRuntime_Download))
            {
                if (confirmDlg.ShowDialog(_owner) != DialogResult.OK)
                    return false;
            }

            try
            {
                using var longWaitDlg = new LongWaitDlg();
                var status = longWaitDlg.PerformWork(_owner, 0, progressMonitor =>
                {
                    IProgressStatus progressStatus = new ProgressStatus();
                    TorchRuntimes.InstallCuda(progressMonitor, ref progressStatus);
                });
                return !status.IsCanceled;
            }
            catch (Exception x)
            {
                if (ExceptionUtil.IsProgrammingDefect(x))
                    throw;
                MessageDlg.ShowWithException(_owner,
                    SettingsUIResources.BuildLibraryDlg_InstallCudaRuntime_Failed_to_download_the_NVIDIA_CUDA_runtime_, x);
                return false;
            }
        }
    }
}
