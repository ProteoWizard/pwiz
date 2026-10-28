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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// A folder of libtorch native libraries, loaded into the process before TorchSharp first needs
    /// libtorch. This lets an application keep its CPU and CUDA libtorch builds, whose files have the
    /// same names, in folders of their own and choose between them when it runs. TorchSharp loads
    /// libtorch by file name, and the operating system hands back a library of that name that is
    /// already loaded, so once a runtime is loaded TorchSharp uses it.
    /// The folder must not be the one TorchSharp.dll is in: TorchSharp looks there first, by full
    /// path, and a second libtorch loaded beside the first breaks both.
    /// A process can load only one libtorch, so the first runtime loaded is the one it keeps.
    /// </summary>
    public class LibTorchRuntime
    {
        private static readonly object LOAD_LOCK = new object();

        /// <summary>
        /// The runtime this process loaded, or null before one is loaded.
        /// </summary>
        public static LibTorchRuntime Loaded { get; private set; }

        /// <summary>
        /// Loads the CUDA runtime of <paramref name="runtimes"/> when asked to, it is installed and the
        /// GPU can use it, otherwise the CPU runtime, unless this process already loaded one, and
        /// returns the runtime loaded.
        /// </summary>
        public static LibTorchRuntime Load(LibTorchRuntimes runtimes, bool useCuda)
        {
            var runtime = Loaded;
            if (runtime == null)
            {
                runtime = new LibTorchRuntime(useCuda && runtimes.CanUseCuda && runtimes.IsCudaInstalled
                    ? runtimes.CudaInstaller.RuntimeDirectory
                    : runtimes.CpuRuntimeDirectory);
                runtime.Load();
            }
            return runtime;
        }

        public LibTorchRuntime(string directory)
        {
            Directory = Path.GetFullPath(directory);
        }

        public string Directory { get; }

        /// <summary>
        /// Whether this is a CUDA build of libtorch, which can also run on the CPU.
        /// </summary>
        public bool IsCuda => File.Exists(Path.Combine(Directory, GetLibraryFileName(@"torch_cuda")));

        /// <summary>
        /// Loads every native library in the folder, unless this runtime is already loaded.
        /// Throws <see cref="InvalidOperationException"/> when another runtime is already loaded,
        /// since a process cannot change its libtorch.
        /// </summary>
        public void Load()
        {
            lock (LOAD_LOCK)
            {
                if (Loaded != null)
                {
                    if (!string.Equals(Loaded.Directory, Directory, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(string.Format(
                            @"The libtorch in {0} cannot be loaded, because this process already loaded the one in {1}.",
                            Directory, Loaded.Directory));
                    }
                    return;
                }
                CheckTorchSharpFindsNoOtherLibTorch();
                LoadLibraries();
            }
        }

        /// <summary>
        /// Loads the libraries in passes, because a library whose dependencies have not been
        /// found yet can load once they are. Only libtorch itself has to load: TorchSharp also
        /// tolerates optional CUDA libraries that do not. This runtime counts as
        /// <see cref="Loaded"/> from the first library that loads: a load that fails partway has
        /// still put some of these libraries in the process, while one that put none there - a
        /// missing or empty folder - leaves the process free to load another runtime and report
        /// the real error again.
        /// </summary>
        private void LoadLibraries()
        {
            var remaining = System.IO.Directory.EnumerateFiles(Directory)
                .Where(IsNativeLibrary).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            var errors = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
            bool loadedAny = true;
            while (remaining.Count > 0 && loadedAny)
            {
                loadedAny = false;
                foreach (var path in remaining.ToArray())
                {
                    try
                    {
                        NativeLibrary.Load(path);
                        Loaded = this;
                        remaining.Remove(path);
                        errors.Remove(path);
                        loadedAny = true;
                    }
                    catch (Exception x) when (x is DllNotFoundException || x is BadImageFormatException)
                    {
                        errors[path] = x;
                    }
                }
            }

            string torchPath = Path.Combine(Directory, GetLibraryFileName(IsCuda ? @"torch_cuda" : @"torch_cpu"));
            if (!File.Exists(torchPath))
                throw new FileNotFoundException(string.Format(@"The folder {0} has no libtorch.", Directory), torchPath);
            if (errors.TryGetValue(torchPath, out var error))
                throw new IOException(string.Format(@"Failed to load {0}: {1}", torchPath, error.Message), error);
        }

        /// <summary>
        /// Throws if libtorch is also where TorchSharp looks for it by itself: beside TorchSharp.dll or in
        /// the runtimes\&lt;rid&gt;\native folders there, as a build that once referenced a libtorch package
        /// can leave behind. TorchSharp would load that one too, by full path, beside this one.
        /// </summary>
        private void CheckTorchSharpFindsNoOtherLibTorch()
        {
            string torchSharpDirectory = Path.GetDirectoryName(typeof(TorchSharp.torch).Assembly.Location);
            if (string.IsNullOrEmpty(torchSharpDirectory))
                return;
            var directories = new List<string> { torchSharpDirectory };
            string runtimesDirectory = Path.Combine(torchSharpDirectory, @"runtimes");
            if (System.IO.Directory.Exists(runtimesDirectory))
            {
                directories.AddRange(System.IO.Directory.EnumerateDirectories(runtimesDirectory)
                    .Select(rid => Path.Combine(rid, @"native")));
            }
            foreach (var directory in directories)
            {
                foreach (var name in new[] { @"torch_cpu", @"torch_cuda" })
                {
                    string path = Path.Combine(directory, GetLibraryFileName(name));
                    if (File.Exists(path) && !string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Directory, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(string.Format(
                            @"Another libtorch is in {0}, where TorchSharp would load it beside the one in {1}. Delete the libtorch files from {0}.",
                            directory, Directory));
                    }
                }
            }
        }

        private static bool IsNativeLibrary(string path)
        {
            string fileName = Path.GetFileName(path);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return fileName.EndsWith(@".dll", StringComparison.OrdinalIgnoreCase);
            return fileName.EndsWith(@".so", StringComparison.Ordinal) || fileName.Contains(@".so.");
        }

        private static string GetLibraryFileName(string name)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? name + @".dll" : @"lib" + name + @".so";
        }
    }
}
