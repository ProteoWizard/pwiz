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
using System.Linq;
using System.Text;
using System.Text.Json;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The safetensors format (https://github.com/huggingface/safetensors): an 8-byte
    /// little-endian header length, a JSON header mapping each tensor name to its dtype, shape
    /// and byte range, then the raw little-endian tensor data. CarafeSharp stores fine-tuned
    /// models this way because the files are not pickles and Python reads them directly.
    /// </summary>
    internal static class SafetensorsFile
    {
        private const string METADATA_KEY = @"__metadata__";

        private static readonly Dictionary<ScalarType, (string Name, int Size)> DTYPES = new Dictionary<ScalarType, (string, int)>
        {
            { ScalarType.Float32, (@"F32", 4) },
            { ScalarType.Float64, (@"F64", 8) },
            { ScalarType.Float16, (@"F16", 2) },
            { ScalarType.BFloat16, (@"BF16", 2) },
            { ScalarType.Int64, (@"I64", 8) },
            { ScalarType.Int32, (@"I32", 4) },
            { ScalarType.Int16, (@"I16", 2) },
            { ScalarType.Int8, (@"I8", 1) },
            { ScalarType.Byte, (@"U8", 1) },
            { ScalarType.Bool, (@"BOOL", 1) },
        };

        public static void Write(string path, IReadOnlyDictionary<string, Tensor> tensors, IReadOnlyDictionary<string, string> metadata = null)
        {
            var header = new Dictionary<string, object>(StringComparer.Ordinal);
            if (metadata != null && metadata.Count > 0)
                header[METADATA_KEY] = metadata;
            var payloads = new List<byte[]>();
            long offset = 0;
            // Ordinal key order, so the same weights always produce the same bytes.
            foreach (string name in tensors.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var tensor = tensors[name].detach().cpu().contiguous();
                if (!DTYPES.TryGetValue(tensor.dtype, out var dtype))
                    throw new NotSupportedException(string.Format(@"Tensor {0} has unsupported type {1}.", name, tensor.dtype));
                byte[] data = tensor.bytes.ToArray();
                header[name] = new Dictionary<string, object>
                {
                    { @"dtype", dtype.Name },
                    { @"shape", tensor.shape },
                    { @"data_offsets", new[] { offset, offset + data.Length } },
                };
                payloads.Add(data);
                offset += data.Length;
            }
            byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header));
            // Pad the header with spaces to an 8-byte boundary, as the reference writer does.
            int padded = (json.Length + 7) / 8 * 8;
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ulong)padded);
                writer.Write(json);
                for (int i = json.Length; i < padded; i++)
                    writer.Write((byte)' ');
                foreach (byte[] data in payloads)
                    writer.Write(data);
            }
        }

        public static Dictionary<string, Tensor> Read(string path, out Dictionary<string, string> metadata)
        {
            metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            var result = new Dictionary<string, Tensor>(StringComparer.Ordinal);
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                long headerLength = (long)reader.ReadUInt64();
                if (headerLength <= 0 || headerLength > stream.Length - 8)
                    throw new InvalidDataException(string.Format(@"{0} is not a safetensors file.", path));
                long dataStart = 8 + headerLength;
                using (var header = JsonDocument.Parse(reader.ReadBytes((int)headerLength)))
                {
                    foreach (var property in header.RootElement.EnumerateObject())
                    {
                        if (property.Name == METADATA_KEY)
                        {
                            foreach (var item in property.Value.EnumerateObject())
                                metadata[item.Name] = item.Value.GetString();
                            continue;
                        }
                        string dtypeName = property.Value.GetProperty(@"dtype").GetString();
                        var dtype = DTYPES.First(p => p.Value.Name == dtypeName).Key;
                        long[] shape = property.Value.GetProperty(@"shape").EnumerateArray().Select(e => e.GetInt64()).ToArray();
                        long[] range = property.Value.GetProperty(@"data_offsets").EnumerateArray().Select(e => e.GetInt64()).ToArray();
                        stream.Position = dataStart + range[0];
                        var tensor = empty(shape, dtype);
                        tensor.bytes = reader.ReadBytes((int)(range[1] - range[0]));
                        result[property.Name] = tensor;
                    }
                }
            }
            return result;
        }
    }
}
