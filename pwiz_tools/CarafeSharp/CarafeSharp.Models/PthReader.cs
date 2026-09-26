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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Razorvine.Pickle;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Reads a PyTorch zip-format checkpoint written by <c>torch.save(model.state_dict())</c>:
    /// a zip holding <c>&lt;name&gt;/data.pkl</c> (the pickled dictionary) and one
    /// <c>&lt;name&gt;/data/&lt;key&gt;</c> entry per storage.
    ///
    /// Each storage is read whole and every tensor is rebuilt as a strided view of it, exactly
    /// as <c>torch._utils._rebuild_tensor_v2</c> does. TorchSharp.PyBridge cannot read
    /// peptdeep's RT checkpoint because it sizes each tensor's buffer to the tensor rather than
    /// to its storage, and the flattened LSTM weights share one storage at non-zero offsets
    /// (dotnet TorchSharp.PyBridge issue #18).
    /// </summary>
    internal static class PthReader
    {
        private static readonly object CONSTRUCTOR_LOCK = new object();
        private static bool _constructorsRegistered;

        public static Dictionary<string, Tensor> Read(Stream checkpointStream)
        {
            RegisterConstructors();
            using (var archive = new ZipArchive(checkpointStream, ZipArchiveMode.Read, true))
            {
                var pickleEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(@"/data.pkl", StringComparison.Ordinal));
                if (pickleEntry == null)
                    throw new InvalidDataException(@"Not a zip-format PyTorch checkpoint: no data.pkl entry.");
                string prefix = pickleEntry.FullName.Substring(0, pickleEntry.FullName.Length - @"data.pkl".Length);
                var byteOrder = archive.GetEntry(prefix + @"byteorder");
                if (byteOrder != null)
                {
                    using (var reader = new StreamReader(byteOrder.Open()))
                    {
                        if (reader.ReadToEnd().Trim() != @"little")
                            throw new InvalidDataException(@"Big-endian PyTorch checkpoints are not supported.");
                    }
                }

                object root;
                using (var unpickler = new CheckpointUnpickler(archive, prefix))
                using (var pickleStream = pickleEntry.Open())
                {
                    root = unpickler.load(pickleStream);
                }
                if (!(root is IDictionary dictionary))
                    throw new InvalidDataException(@"Checkpoint does not hold a state_dict.");
                var result = new Dictionary<string, Tensor>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value is Tensor value)
                        result[(string)entry.Key] = value;
                }
                return result;
            }
        }

        private static void RegisterConstructors()
        {
            lock (CONSTRUCTOR_LOCK)
            {
                if (_constructorsRegistered)
                    return;
                Unpickler.registerConstructor(@"collections", @"OrderedDict", new OrderedDictConstructor());
                Unpickler.registerConstructor(@"torch._utils", @"_rebuild_tensor_v2", new RebuildTensorConstructor());
                foreach (var storageType in StorageType.ALL)
                    Unpickler.registerConstructor(@"torch", storageType.Name, storageType);
                _constructorsRegistered = true;
            }
        }

        /// <summary>A pickled <c>torch.*Storage</c> class, standing for its element type.</summary>
        private sealed class StorageType : IObjectConstructor
        {
            public static readonly StorageType[] ALL =
            {
                new StorageType(@"FloatStorage", ScalarType.Float32, 4),
                new StorageType(@"DoubleStorage", ScalarType.Float64, 8),
                new StorageType(@"HalfStorage", ScalarType.Float16, 2),
                new StorageType(@"BFloat16Storage", ScalarType.BFloat16, 2),
                new StorageType(@"LongStorage", ScalarType.Int64, 8),
                new StorageType(@"IntStorage", ScalarType.Int32, 4),
                new StorageType(@"ShortStorage", ScalarType.Int16, 2),
                new StorageType(@"CharStorage", ScalarType.Int8, 1),
                new StorageType(@"ByteStorage", ScalarType.Byte, 1),
                new StorageType(@"BoolStorage", ScalarType.Bool, 1),
            };

            private StorageType(string name, ScalarType dtype, int elementSize)
            {
                Name = name;
                DType = dtype;
                ElementSize = elementSize;
            }

            public string Name { get; }

            public ScalarType DType { get; }

            public int ElementSize { get; }

            public object construct(object[] args)
            {
                throw new PickleException(@"Storage types are not constructed directly.");
            }
        }

        /// <summary>
        /// Resolves the persistent storage references in <c>data.pkl</c> to 1-D tensors over
        /// the whole storage, cached so tensors sharing a storage share one read.
        /// </summary>
        private sealed class CheckpointUnpickler : Unpickler
        {
            private readonly ZipArchive _archive;
            private readonly string _prefix;
            private readonly Dictionary<string, Tensor> _storages = new Dictionary<string, Tensor>(StringComparer.Ordinal);

            public CheckpointUnpickler(ZipArchive archive, string prefix)
            {
                _archive = archive;
                _prefix = prefix;
            }

            protected override object persistentLoad(object pid)
            {
                // ('storage', storage_type, key, location, numel)
                var parts = (object[])pid;
                if (parts.Length < 5 || !Equals(parts[0], @"storage"))
                    throw new PickleException(@"Unsupported persistent id in checkpoint.");
                var storageType = (StorageType)parts[1];
                string key = (string)parts[2];
                long numel = Convert.ToInt64(parts[4]);
                if (_storages.TryGetValue(key, out var cached))
                    return cached;
                var entry = _archive.GetEntry(_prefix + @"data/" + key);
                if (entry == null)
                    throw new InvalidDataException(string.Format(@"Checkpoint storage {0} is missing.", key));
                var bytes = new byte[numel * storageType.ElementSize];
                using (var stream = entry.Open())
                {
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n == 0)
                            throw new InvalidDataException(string.Format(@"Checkpoint storage {0} is truncated.", key));
                        read += n;
                    }
                }
                var storage = empty(new[] { numel }, storageType.DType);
                storage.bytes = bytes;
                _storages.Add(key, storage);
                return storage;
            }
        }

        /// <summary>
        /// <c>_rebuild_tensor_v2(storage, storage_offset, size, stride, requires_grad, hooks)</c>:
        /// a contiguous copy of the strided view, so no result aliases a shared storage.
        /// </summary>
        private sealed class RebuildTensorConstructor : IObjectConstructor
        {
            public object construct(object[] args)
            {
                var storage = (Tensor)args[0];
                long offset = Convert.ToInt64(args[1]);
                long[] size = ((object[])args[2]).Select(Convert.ToInt64).ToArray();
                long[] stride = ((object[])args[3]).Select(Convert.ToInt64).ToArray();
                return storage.as_strided(size, stride, offset).clone();
            }
        }

        private sealed class OrderedDictConstructor : IObjectConstructor
        {
            public object construct(object[] args)
            {
                return new PickledOrderedDict();
            }
        }

        /// <summary>
        /// An <c>OrderedDict</c> target for SETITEMS; <c>__setstate__</c> accepts and ignores the
        /// <c>_metadata</c> attribute a state_dict carries.
        /// </summary>
        private sealed class PickledOrderedDict : Hashtable
        {
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once UnusedParameter.Local
            public void __setstate__(Hashtable state)
            {
            }
        }
    }
}
