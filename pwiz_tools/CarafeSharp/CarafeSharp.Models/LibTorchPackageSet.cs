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
using System.Globalization;
using System.IO;
using System.Linq;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The NuGet packages that together hold one libtorch runtime, such as libtorch-cuda-12.8-win-x64,
    /// which is split across part packages to fit nuget.org's size limit. The build that references
    /// the runtime writes the list with LibTorchPackageSet.targets, taking each package's hash from
    /// the NuGet cache, so a download is checked against the exact packages the build used.
    /// </summary>
    public class LibTorchPackageSet
    {
        /// <summary>
        /// nuget.org's flat container, which serves every version of every package at a fixed URL.
        /// </summary>
        public const string NUGET_PACKAGE_SOURCE = @"https://api.nuget.org/v3-flatcontainer/";

        private const char SEPARATOR = '\t';

        /// <summary>
        /// Reads the file LibTorchPackageSet.targets writes: the runtime's package id and version,
        /// then one line per package with its id, version, SHA-512 (base 64, as NuGet records it)
        /// and size in bytes, all tab separated.
        /// </summary>
        public static LibTorchPackageSet Read(string path)
        {
            var lines = File.ReadAllLines(path).Where(line => line.Length > 0).ToList();
            if (lines.Count < 2)
                throw new InvalidDataException(string.Format(@"{0} lists no packages.", path));
            var header = lines[0].Split(SEPARATOR);
            if (header.Length != 2)
                throw new InvalidDataException(string.Format(@"{0} does not start with a package id and version.", path));
            var packages = new List<LibTorchPackage>();
            foreach (var line in lines.Skip(1))
            {
                var fields = line.Split(SEPARATOR);
                if (fields.Length != 4 || !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out long size))
                    throw new InvalidDataException(string.Format(@"{0} has an invalid package line: {1}", path, line));
                packages.Add(new LibTorchPackage(fields[0], fields[1], fields[2], size));
            }
            return new LibTorchPackageSet(header[0], header[1], packages);
        }

        public LibTorchPackageSet(string id, string version, IEnumerable<LibTorchPackage> packages)
        {
            Id = id;
            Version = version;
            Packages = packages.ToList();
        }

        public string Id { get; }
        public string Version { get; }
        public IList<LibTorchPackage> Packages { get; }

        /// <summary>
        /// The name of the folder the runtime is installed in, which changes with its version.
        /// </summary>
        public string Name => Id + @"." + Version;

        public long TotalSize => Packages.Sum(package => package.Size);
    }

    public class LibTorchPackage
    {
        public LibTorchPackage(string id, string version, string sha512, long size)
        {
            Id = id;
            Version = version;
            Sha512 = sha512;
            Size = size;
        }

        public string Id { get; }
        public string Version { get; }

        /// <summary>
        /// The SHA-512 of the .nupkg file in base 64, as NuGet records it in its cache.
        /// </summary>
        public string Sha512 { get; }

        /// <summary>
        /// The size of the .nupkg file in bytes.
        /// </summary>
        public long Size { get; }

        public string FileName => string.Format(@"{0}.{1}.nupkg", Id.ToLowerInvariant(), Version.ToLowerInvariant());

        /// <summary>
        /// Where a NuGet flat container, such as <see cref="LibTorchPackageSet.NUGET_PACKAGE_SOURCE"/>,
        /// serves the package.
        /// </summary>
        public Uri GetUri(string packageSource)
        {
            string id = Uri.EscapeDataString(Id.ToLowerInvariant());
            string version = Uri.EscapeDataString(Version.ToLowerInvariant());
            return new Uri(new Uri(packageSource), string.Format(@"{0}/{1}/{2}", id, version, Uri.EscapeDataString(FileName)));
        }

        public override string ToString()
        {
            return Id + @" " + Version;
        }
    }
}
