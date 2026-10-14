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
using System.IO;
using System.Security.Cryptography;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The AlphaPeptDeep pretrained weights, <c>pretrained_models.zip</c> from the MannLabs
    /// "pre-trained-models" release. That release URL is unversioned, which is how two
    /// Carafe runs came to predict from different weights, so CarafeSharp pins the exact
    /// archive by SHA-256 and refuses any other.
    /// </summary>
    public sealed class PretrainedModels
    {
        /// <summary>
        /// Where the pinned archive came from. The URL is unversioned and the asset has been replaced
        /// before, so it is recorded here, not fetched: the archive is committed in
        /// models/alphapeptdeep-v1 and copied next to the executable.
        /// </summary>
        public const string DOWNLOAD_URL = @"https://github.com/MannLabs/alphapeptdeep/releases/download/pre-trained-models/pretrained_models.zip";

        /// <summary>
        /// The MannLabs release the archive is (<c>pretrained_models.zip</c>, before <c>_v2</c> and <c>_v3</c>), which a
        /// saved model records as its AlphaPeptDeep models' version.
        /// </summary>
        public const string VERSION = @"v1";

        /// <summary>Where the build places the committed archive, relative to the executable.</summary>
        public const string BUNDLED_RELATIVE_PATH = @"models/alphapeptdeep-" + VERSION + @"/pretrained_models.zip";

        /// <summary>The v1 archive Carafe uses (25,614,761 bytes, generic/ms2.pth dated 2022-10-28).</summary>
        public const string PINNED_SHA256 = @"75e6037db3280a513d0f6010a21dba4e8ea47a8d67127f38c77fb1f9a7d408eb";

        public const string MS2_ENTRY = @"generic/ms2.pth";
        public const string RT_ENTRY = @"generic/rt.pth";
        public const string CCS_ENTRY = @"generic/ccs.pth";

        /// <summary>Environment variable naming the archive, overriding the default location.</summary>
        public const string PATH_VARIABLE = @"CARAFESHARP_PRETRAINED_MODELS";

        /// <summary>
        /// Opens the archive at <paramref name="zipPath"/>, or at <see cref="DefaultPath"/> when it is null.
        /// </summary>
        public static PretrainedModels Open(string zipPath = null)
        {
            zipPath = zipPath ?? DefaultPath;
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException(string.Format(
                    @"AlphaPeptDeep pretrained models not found at {0}. CarafeSharp ships them as {1} beside the executable; {2} or -pretrained can name another copy of the pinned archive (SHA-256 {3}).",
                    zipPath, BUNDLED_RELATIVE_PATH, PATH_VARIABLE, PINNED_SHA256), zipPath);
            }
            string sha256 = ComputeSha256(zipPath);
            if (!string.Equals(sha256, PINNED_SHA256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(string.Format(
                    @"{0} has SHA-256 {1}, not the pinned {2}. A different pretrained archive changes every prediction.",
                    zipPath, sha256, PINNED_SHA256));
            }
            return new PretrainedModels(zipPath, sha256);
        }

        /// <summary>
        /// The archive used when none is named: <c>%CARAFESHARP_PRETRAINED_MODELS%</c> when it is set,
        /// else the committed copy beside the executable, else peptdeep's own
        /// <c>~/peptdeep/pretrained_models/pretrained_models.zip</c>, which Carafe shares.
        /// </summary>
        public static string DefaultPath
        {
            get
            {
                return ResolveDefaultPath(Environment.GetEnvironmentVariable(PATH_VARIABLE), AppContext.BaseDirectory,
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
        }

        /// <summary>
        /// <see cref="DefaultPath"/> for the given environment. A set variable wins even when it names a
        /// missing file, so a mistyped path fails in <see cref="Open"/> instead of silently falling back to
        /// another copy.
        /// </summary>
        internal static string ResolveDefaultPath(string fromEnvironment, string baseDirectory, string userProfile)
        {
            if (!string.IsNullOrEmpty(fromEnvironment))
                return fromEnvironment;
            string bundled = Path.Combine(baseDirectory, BUNDLED_RELATIVE_PATH);
            if (File.Exists(bundled))
                return bundled;
            return Path.Combine(userProfile, @"peptdeep", @"pretrained_models", @"pretrained_models.zip");
        }

        private PretrainedModels(string zipPath, string sha256)
        {
            ZipPath = zipPath;
            Sha256 = sha256;
        }

        public string ZipPath { get; }

        public string Sha256 { get; }

        internal static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }
    }
}
