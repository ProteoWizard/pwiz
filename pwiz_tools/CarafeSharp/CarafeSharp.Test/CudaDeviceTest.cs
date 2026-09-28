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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The pretrained models predicting on the GPU as they do on the CPU. Only a CUDA build runs this
    /// category (<c>build.ps1 -Torch cuda</c>), which sets <c>CARAFESHARP_REQUIRE_CUDA=1</c>, so a GPU
    /// run that fell back to the CPU fails instead of passing untested. Without the variable, a
    /// machine with no usable GPU is inconclusive.
    /// </summary>
    [TestClass]
    public class CudaDeviceTest
    {
        // GPU against CPU on the pretrained models measured intensities within 8.4e-6 and RT within
        // 7e-6 of the normalized scale (1.6e-4 minutes of a 24-minute gradient); these allow a few
        // times that, for other GPUs.
        private const float MAX_INTENSITY_DIFFERENCE = 3e-5f;
        private const double MAX_RT_DIFFERENCE = 5e-5;

        [TestMethod, TestCategory(TestData.CUDA_CATEGORY)]
        public void TestGpuPredictionsMatchCpu()
        {
            var gpu = TorchDevice.Resolve(TorchDevice.GPU, out string fallback);
            if (fallback != null)
            {
                if (Environment.GetEnvironmentVariable(TestData.REQUIRE_CUDA_VARIABLE) == @"1")
                    Assert.Fail(fallback);
                Assert.Inconclusive(fallback);
            }
            var pretrained = PretrainedModels.Open();

            var peptides = new[]
            {
                new PeptideForm(@"LGGNEQVTR"), new PeptideForm(@"YILAGVENSK"), new PeptideForm(@"ADVTPADFSEWSK"),
                new PeptideForm(@"LFLQFGAQGSPFLK"), new PeptideForm(@"DGLDAASYYAPVR"),
            };
            double[] rtCpu, rtGpu;
            using (var model = RtModel.FromPretrained(pretrained, CPU))
                rtCpu = model.Predict(peptides);
            using (var model = RtModel.FromPretrained(pretrained, gpu))
                rtGpu = model.Predict(peptides);
            double rtDifference = rtCpu.Zip(rtGpu, (a, b) => Math.Abs(a - b)).Max();
            Assert.IsTrue(rtDifference <= MAX_RT_DIFFERENCE, @"RT on the GPU differs from the CPU by " + rtDifference);

            var requests = peptides.SelectMany(p => new[]
            {
                new Ms2Request(new PrecursorForm(p, 2), 30, @"Lumos"),
                new Ms2Request(new PrecursorForm(p, 3), 27, @"QE"),
            }).ToArray();
            float[] ms2Cpu, ms2Gpu;
            using (var model = Ms2Model.FromPretrained(pretrained, CPU))
                ms2Cpu = model.Predict(requests).SelectMany(p => p.Intensities).ToArray();
            using (var model = Ms2Model.FromPretrained(pretrained, gpu))
                ms2Gpu = model.Predict(requests).SelectMany(p => p.Intensities).ToArray();
            Assert.AreEqual(ms2Cpu.Length, ms2Gpu.Length);
            float intensityDifference = ms2Cpu.Zip(ms2Gpu, (a, b) => Math.Abs(a - b)).Max();
            Assert.IsTrue(intensityDifference <= MAX_INTENSITY_DIFFERENCE,
                @"Fragment intensities on the GPU differ from the CPU by " + intensityDifference);
        }
    }
}
