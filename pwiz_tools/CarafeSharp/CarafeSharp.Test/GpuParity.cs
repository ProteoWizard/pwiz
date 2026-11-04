/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// How the full-size comparisons with Carafe judge a CUDA run (<c>Compare-CarafeParity.ps1 -Torch cuda</c>).
    ///
    /// Limits: Carafe's references were written on CUDA, but CarafeSharp on the CPU is the backend that
    /// reproduces them within the comparisons' limits. On the GPU, TF32 (libtorch's default for cuDNN
    /// convolutions and LSTMs on Ampere and later) moves RT further than those limits allow, so each
    /// comparison has a pinned GPU limit beside its CPU one, measured on one machine. A value beyond the
    /// CPU limit passes on the GPU while it stays under the pin, and is reported with a
    /// <see cref="PINNED_PREFIX"/> line the script collects; a value beyond the pin fails, so the pin is
    /// a regression check of the GPU path, not a waiver.
    ///
    /// Self-consistency: on the GPU every prediction comparison predicts twice in-process and requires
    /// the two to be bit-identical, and compares with the outputs a previous run saved in
    /// <see cref="OUTPUTS_VARIABLE"/>, when the script sets it. Any difference fails. Outputs with
    /// nothing saved yet are saved only by <see cref="SavePending"/>, after the comparison passed, so a
    /// failing run never becomes the baseline.
    /// See docs/04-testing.md ("GPU parity").
    /// </summary>
    public static class GpuParity
    {
        /// <summary>The folder the parity script keeps GPU outputs in between runs, one per backend.</summary>
        public const string OUTPUTS_VARIABLE = @"CARAFESHARP_PARITY_OUTPUTS";

        /// <summary>Starts each line that reports a value judged by a GPU pin; the parity script prints them.</summary>
        public const string PINNED_PREFIX = @"GPU pinned:";

        /// <summary>Starts each line that reports a self-consistency check; the parity script counts them.</summary>
        public const string REPEAT_PREFIX = @"GPU repeat:";

        private static readonly List<KeyValuePair<string, byte[]>> _pendingSaves = new List<KeyValuePair<string, byte[]>>();

        /// <summary>True when the comparisons run on the GPU (<see cref="TestData.ComparisonDeviceName"/>).</summary>
        public static bool IsGpu
        {
            get { return TestData.ComparisonDeviceName == TorchDevice.GPU; }
        }

        /// <summary>
        /// Requires <paramref name="measured"/> below <paramref name="cpuLimit"/>, or on the GPU below
        /// <paramref name="gpuPin"/>, reporting the measured value when the pin decided it.
        /// </summary>
        public static void AssertBelow(TestContext context, string label, string quantity, double measured,
            double cpuLimit, double gpuPin, string unit = null)
        {
            if (IsGpu && !(measured < cpuLimit))
            {
                // Invariant, for the script that parses it. The pins were measured with TF32 on.
                string tf32 = Environment.GetEnvironmentVariable(@"NVIDIA_TF32_OVERRIDE") == @"0" ? @"TF32 off" : @"TF32";
                context.WriteLine(string.Format(CultureInfo.InvariantCulture, @"{0} {1} max|diff| {2:E2}{3} (CPU limit {4:G2}; GPU pin {5:G2}, {6}) - {7}",
                    PINNED_PREFIX, quantity, measured, unit != null ? @" " + unit : string.Empty, cpuLimit, gpuPin, tf32, label));
                Assert.IsTrue(measured < gpuPin,
                    string.Format(@"{0} {1} max |diff| {2} exceeds the pinned GPU limit {3} (CPU limit {4})", label, quantity, measured, gpuPin, cpuLimit));
                return;
            }
            Assert.IsTrue(measured < cpuLimit, label + @" " + quantity + @" max |diff| " + measured);
        }

        /// <summary>
        /// Requires two in-process predictions of the same values to be bit-identical, and the first
        /// to match the saved outputs of a previous run under <paramref name="key"/>, queuing it for
        /// <see cref="SavePending"/> when there are none.
        /// </summary>
        public static void AssertRepeatable(TestContext context, string label, string key, double[] first, double[] second)
        {
            Assert.AreEqual(first.Length, second.Length, label + @" repeat value count");
            int differs = CountDifferences(first, second, out double maxDiff);
            context.WriteLine(@"{0} {1}: {2} values predicted twice in-process, {3} differ (max |diff| {4:E2})",
                REPEAT_PREFIX, label, first.Length, differs, maxDiff);
            Assert.AreEqual(0, differs, label + @": the GPU prediction is not repeatable in-process, max |diff| " + maxDiff);

            byte[] bytes = new byte[first.Length * sizeof(double)];
            Buffer.BlockCopy(first, 0, bytes, 0, bytes.Length);
            string savedPath = SavedPath(context, label, key + @".bin");
            if (savedPath == null)
                return;
            if (!File.Exists(savedPath))
            {
                QueueSave(context, label, savedPath, bytes);
                return;
            }
            byte[] savedBytes = File.ReadAllBytes(savedPath);
            Assert.AreEqual(bytes.Length, savedBytes.Length, label + @": value count differs from the saved outputs " + savedPath);
            double[] saved = new double[first.Length];
            Buffer.BlockCopy(savedBytes, 0, saved, 0, savedBytes.Length);
            int savedDiffers = CountDifferences(saved, first, out double savedMaxDiff);
            context.WriteLine(@"{0} {1}: against the saved outputs {2}, {3} differ (max |diff| {4:E2})",
                REPEAT_PREFIX, label, savedPath, savedDiffers, savedMaxDiff);
            Assert.AreEqual(0, savedDiffers, label + @": the GPU prediction differs from the saved outputs " + savedPath + @", max |diff| " + savedMaxDiff);
        }

        /// <summary>
        /// Requires two in-process builds of the same library file to be byte-identical, and the first
        /// to match the hash a previous run saved under <paramref name="key"/>, queuing it for
        /// <see cref="SavePending"/> when there is none.
        /// </summary>
        public static void AssertRepeatableFile(TestContext context, string label, string key, string firstPath, string secondPath)
        {
            string first = Sha256(firstPath), second = Sha256(secondPath);
            context.WriteLine(@"{0} {1}: {2} built twice in-process, SHA-256 {3} and {4}",
                REPEAT_PREFIX, label, Path.GetFileName(firstPath), first, second);
            Assert.AreEqual(first, second, label + @": the GPU library is not repeatable in-process");

            string savedPath = SavedPath(context, label, key + @".sha256");
            if (savedPath == null)
                return;
            if (!File.Exists(savedPath))
            {
                QueueSave(context, label, savedPath, Encoding.ASCII.GetBytes(first));
                return;
            }
            string saved = File.ReadAllText(savedPath).Trim();
            context.WriteLine(@"{0} {1}: against the saved outputs {2}, SHA-256 {3}", REPEAT_PREFIX, label, savedPath, saved);
            Assert.AreEqual(saved, first, label + @": the GPU library differs from the saved outputs " + savedPath);
        }

        /// <summary>Forgets the outputs a comparison that failed before <see cref="SavePending"/> queued.</summary>
        public static void DiscardPendingSaves()
        {
            _pendingSaves.Clear();
        }

        /// <summary>Saves the queued outputs for the next run; called once the comparison has passed.</summary>
        public static void SavePending(TestContext context)
        {
            foreach (var save in _pendingSaves)
            {
                File.WriteAllBytes(save.Key, save.Value);
                context.WriteLine(@"{0} saved {1} for the next run", REPEAT_PREFIX, save.Key);
            }
            _pendingSaves.Clear();
        }

        /// <summary>
        /// A file name for the saved outputs of <paramref name="folder"/> (<see cref="FolderKey"/>); a committed
        /// subset's folders have the same names as the package's, so they get a prefix of their own.
        /// </summary>
        public static string Key(string folder, string suffix)
        {
            return (TestData.IsSubset ? @"subset_" : string.Empty) + FolderKey(folder).Replace('/', '_') + @"_" + suffix;
        }

        /// <summary>The last three path parts of a reference folder, '/'-separated, which tell the datasets apart.</summary>
        public static string FolderKey(string folder)
        {
            var parts = folder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(@"/", parts.Skip(Math.Max(0, parts.Length - 3)));
        }

        private static string SavedPath(TestContext context, string label, string fileName)
        {
            string folder = Environment.GetEnvironmentVariable(OUTPUTS_VARIABLE);
            if (string.IsNullOrEmpty(folder))
            {
                context.WriteLine(@"{0} {1}: {2} is not set, so there are no saved outputs to compare with", REPEAT_PREFIX, label, OUTPUTS_VARIABLE);
                return null;
            }
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, fileName);
        }

        private static void QueueSave(TestContext context, string label, string path, byte[] bytes)
        {
            _pendingSaves.Add(new KeyValuePair<string, byte[]>(path, bytes));
            context.WriteLine(@"{0} {1}: no saved outputs, {2} is saved if the comparison passes", REPEAT_PREFIX, label, path);
        }

        private static int CountDifferences(double[] expected, double[] actual, out double maxDiff)
        {
            int differs = 0;
            maxDiff = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                if (BitConverter.DoubleToInt64Bits(expected[i]) == BitConverter.DoubleToInt64Bits(actual[i]))
                    continue;
                differs++;
                maxDiff = Math.Max(maxDiff, Math.Abs(expected[i] - actual[i]));
            }
            return differs;
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString(@"x2")));
        }
    }
}
