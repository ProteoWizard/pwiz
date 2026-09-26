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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Reads PyTorch <c>torch.save(state_dict)</c> checkpoints and safetensors files, and
    /// copies them into a module with the key and shape checks peptdeep itself skips
    /// (it loads with <c>strict=False</c>, which silently ignores a misnamed layer).
    ///
    /// Loading copies into the module's own tensors (<c>copy_</c>) so each parameter keeps its
    /// <c>requires_grad</c>; TorchSharp.PyBridge's <c>load_py</c> would overwrite it with the
    /// pickled value (false for a state_dict) and freeze the whole model for fine-tuning.
    /// </summary>
    public static class StateDict
    {
        /// <summary>
        /// Prefixes a checkpoint acquires when saved from a wrapped model: <c>module.</c> from
        /// <c>nn.DataParallel</c> (more than one GPU) and <c>_orig_mod.</c> from
        /// <c>torch.compile</c>.
        /// </summary>
        private static readonly string[] WRAPPER_PREFIXES = { @"module.", @"_orig_mod." };

        /// <summary>Reads a <c>.pth</c> checkpoint stored inside a zip archive.</summary>
        public static Dictionary<string, Tensor> ReadPthFromZip(string zipPath, string entryName)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var entry = archive.GetEntry(entryName);
                if (entry == null)
                    throw new FileNotFoundException(string.Format(@"{0} has no entry {1}.", zipPath, entryName), entryName);
                // PyTorchUnpickler needs a seekable stream; zip entry streams are not.
                using (var buffer = new MemoryStream())
                {
                    using (var entryStream = entry.Open())
                        entryStream.CopyTo(buffer);
                    buffer.Position = 0;
                    return ReadPth(buffer);
                }
            }
        }

        /// <summary>Reads a <c>.pth</c> or <c>.pt</c> checkpoint file, such as a Carafe fine-tuned model.</summary>
        public static Dictionary<string, Tensor> ReadPthFile(string path)
        {
            using (var stream = File.OpenRead(path))
                return ReadPth(stream);
        }

        /// <summary>Reads a <c>.pth</c> checkpoint from a seekable stream.</summary>
        public static Dictionary<string, Tensor> ReadPth(Stream stream)
        {
            var result = new Dictionary<string, Tensor>(StringComparer.Ordinal);
            foreach (var pair in PthReader.Read(stream))
                result[StripWrapperPrefix(pair.Key)] = pair.Value;
            return result;
        }

        public static Dictionary<string, Tensor> ReadSafetensors(string path)
        {
            var result = new Dictionary<string, Tensor>(StringComparer.Ordinal);
            foreach (var pair in SafetensorsFile.Read(path))
                result[StripWrapperPrefix(pair.Key)] = pair.Value;
            return result;
        }

        public static void WriteSafetensors(nn.Module module, string path)
        {
            SafetensorsFile.Write(path, module.state_dict());
        }

        /// <summary>
        /// Copies <paramref name="source"/> into <paramref name="module"/>. Every module key must
        /// be present with the same shape and every source key must be used.
        /// </summary>
        public static void Load(nn.Module module, IReadOnlyDictionary<string, Tensor> source)
        {
            var target = module.state_dict();
            var missing = target.Keys.Where(k => !source.ContainsKey(k)).ToList();
            var unexpected = source.Keys.Where(k => !target.ContainsKey(k)).ToList();
            if (missing.Count > 0 || unexpected.Count > 0)
            {
                throw new InvalidDataException(string.Format(@"Checkpoint does not match {0}. Missing: [{1}]. Unexpected: [{2}].",
                    module.GetName(), string.Join(@", ", missing), string.Join(@", ", unexpected)));
            }
            using (no_grad())
            {
                foreach (var pair in target)
                {
                    if (!source.TryGetValue(pair.Key, out var value))
                        continue;
                    if (!pair.Value.shape.SequenceEqual(value.shape))
                    {
                        throw new InvalidDataException(string.Format(@"Checkpoint tensor {0} has shape [{1}], expected [{2}].",
                            pair.Key, string.Join(@",", value.shape), string.Join(@",", pair.Value.shape)));
                    }
                    pair.Value.copy_(value.to(pair.Value.dtype, pair.Value.device));
                }
            }
        }

        private static string StripWrapperPrefix(string key)
        {
            foreach (string prefix in WRAPPER_PREFIXES)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    return key.Substring(prefix.Length);
            }
            return key;
        }
    }
}
