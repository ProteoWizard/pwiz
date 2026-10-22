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

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using pwiz.Osprey.Centroid;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Osprey's own centroiding of profile spectra (<c>--centroid centrix</c>), as the rest of the pipeline sees it: the
    /// reader's settings, the tag that keeps its spectra cache apart from the vendor-centroided one, and the term that
    /// makes every task's output depend on it. All three are empty for vendor centroiding, so nothing about a default run
    /// changes.
    /// </summary>
    public static class ProfileCentroiding
    {
        /// <summary>The reader's Centrix settings, or null for vendor centroiding.</summary>
        public static CentroidingOptions CreateOptions(OspreyConfig config)
        {
            if (config.CentroidMode == CentroidMode.vendor)
                return null;
            var solver = config.CentrixActiveSet ? CentrixSolver.active_set : CentrixSolver.coordinate_descent;
            return new CentroidingOptions(new CentrixParams { SigmaTh = config.CentrixSigmaMs1, Solver = solver },
                new CentrixParams { SigmaTh = config.CentrixSigmaMs2, Solver = solver }, config.NThreads);
        }

        /// <summary>
        /// Every setting that changes the centroids, with the centroider's algorithm version, or null for vendor
        /// centroiding.
        /// </summary>
        public static string Descriptor(OspreyConfig config)
        {
            if (config.CentroidMode == CentroidMode.vendor)
                return null;
            return string.Format(CultureInfo.InvariantCulture, @"centrix v{0} sigma_ms1={1} sigma_ms2={2} solver={3}",
                CentrixCentroider.ALGORITHM_VERSION, config.CentrixSigmaMs1, config.CentrixSigmaMs2,
                config.CentrixActiveSet ? CentrixSolver.active_set : CentrixSolver.coordinate_descent);
        }

        /// <summary>
        /// The spectra cache's tag (<see cref="ArtifactPaths.SpectraCacheTag"/>): <c>centrix-</c> and the first eight
        /// hex digits of the descriptor's SHA-256, or null for vendor centroiding.
        /// </summary>
        public static string CacheTag(OspreyConfig config)
        {
            string descriptor = Descriptor(config);
            if (descriptor == null)
                return null;
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(descriptor));
                var tag = new StringBuilder(@"centrix-");
                for (int i = 0; i < 4; i++)
                    tag.Append(hash[i].ToString(@"x2", CultureInfo.InvariantCulture));
                return tag.ToString();
            }
        }

        /// <summary>
        /// The task validity-key suffix: empty for vendor centroiding, so every existing key is unchanged, and otherwise
        /// the descriptor. The search hash records the settings but not the algorithm version, which changes the spectra
        /// every score is computed from.
        /// </summary>
        public static string ValidityKeySuffix(OspreyConfig config)
        {
            string descriptor = Descriptor(config);
            return descriptor == null ? string.Empty : @";centroid=" + descriptor;
        }
    }
}
