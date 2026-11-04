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
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The CUDA allocator settings a GPU request gives libtorch, read from the C runtime's environment as libtorch reads
    /// them. Needs no GPU: the settings are made directly, from a clean environment that is restored afterwards, since
    /// other tests' GPU requests set them in this process.
    /// </summary>
    [TestClass]
    public class TorchDeviceTest
    {
        private const string USER_SETTING = @"max_split_size_mb:256";

        [TestMethod]
        public void TestCudaAllocatorSettings()
        {
            string variable = TorchDevice.CUDA_ALLOC_CONF_VARIABLE;
            if (!OperatingSystem.IsWindows())
            {
                // Elsewhere a full card makes libtorch empty its cache itself, so nothing is set.
                string before = Environment.GetEnvironmentVariable(variable);
                TorchDevice.ConfigureCudaAllocator();
                Assert.AreEqual(before, Environment.GetEnvironmentVariable(variable));
                return;
            }

            var saved = TorchDevice.ALLOC_CONF_VARIABLES.ToDictionary(name => name,
                name => (Crt: CrtEnvironment.Get(name), DotNet: Environment.GetEnvironmentVariable(name)));
            try
            {
                // Nothing set: a CPU request sets nothing, a GPU request the default, in the C runtime's copy only,
                // which child processes do not inherit.
                ClearAllocatorSettings();
                TorchDevice.Resolve(TorchDevice.CPU, out _);
                Assert.IsNull(CrtEnvironment.Get(variable));
                TorchDevice.ConfigureCudaAllocator();
                Assert.AreEqual(TorchDevice.CUDA_ALLOC_CONF, CrtEnvironment.Get(variable));
                Assert.IsNull(Environment.GetEnvironmentVariable(variable));

                // A user's setting in any of the variables libtorch reads is kept, and the default not added.
                foreach (string name in TorchDevice.ALLOC_CONF_VARIABLES)
                {
                    ClearAllocatorSettings();
                    CrtEnvironment.Set(name, USER_SETTING);
                    TorchDevice.ConfigureCudaAllocator();
                    Assert.AreEqual(USER_SETTING, CrtEnvironment.Get(name), name);
                    if (name != variable)
                        Assert.IsNull(CrtEnvironment.Get(variable), name);
                }

                // A setting made through .NET after the process started does not reach the C runtime by itself; it is
                // passed on.
                ClearAllocatorSettings();
                const string allocConf = @"PYTORCH_ALLOC_CONF";
                Environment.SetEnvironmentVariable(allocConf, USER_SETTING);
                Assert.IsNull(CrtEnvironment.Get(allocConf));
                TorchDevice.ConfigureCudaAllocator();
                Assert.AreEqual(USER_SETTING, CrtEnvironment.Get(allocConf));
                Assert.IsNull(CrtEnvironment.Get(variable));
            }
            finally
            {
                foreach (var pair in saved)
                {
                    CrtEnvironment.Set(pair.Key, pair.Value.Crt);
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value.DotNet);
                }
            }
        }

        private static void ClearAllocatorSettings()
        {
            foreach (string name in TorchDevice.ALLOC_CONF_VARIABLES)
            {
                CrtEnvironment.Set(name, null);
                Environment.SetEnvironmentVariable(name, null);
            }
        }
    }
}
