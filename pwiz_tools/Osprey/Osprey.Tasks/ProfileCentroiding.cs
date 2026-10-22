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
    /// Osprey's own centroiding of profile spectra (<c>--centroid centrix</c>, Centrix in <c>Osprey.Centroid</c>), as
    /// the rest of the pipeline sees it. All three of its parts are empty for vendor centroiding, the default, so
    /// nothing about a default run, its caches, hashes or goldens, changes.
    /// <list type="bullet">
    /// <item><see cref="CreateOptions"/>: the reader's settings (<c>SpectrumFileReader.LoadAllSpectra</c>).</item>
    /// <item><see cref="CacheTag"/>: inserted into the spectra cache's name by <c>Program</c> through
    /// <see cref="ArtifactPaths.SpectraCacheTag"/>, so the Centrix spectra live in
    /// <c>{stem}.centrix-{hash}.spectra.bin</c> (and <c>{stem}.centrix-{hash}.demux.spectra.bin</c>) beside, never in
    /// place of, the vendor-centroided <c>{stem}.spectra.bin</c>. Every reader of the cache, Stage 6's rescore and the
    /// training export included, goes through <c>SpectraCache.GetCachePath</c> and so finds the same file. The cache
    /// stays a function of its source file: different settings, a different name.</item>
    /// <item><see cref="ValidityKeySuffix"/>: added to every task's validity key (<c>OspreyTask.ValidityKey</c>), so
    /// scores computed from one centroiding are never reused for another. The search hash (<c>SearchIdentity</c>)
    /// records the settings too, but cannot see the algorithm version.</item>
    /// </list>
    /// </summary>
    public static class ProfileCentroiding
    {
        /// <summary>
        /// The reader's Centrix settings, or null for vendor centroiding. A sigma of 0 (the default) is measured from
        /// the run by the reader; the solver applies to both MS levels; Centrix's other settings keep their defaults.
        /// </summary>
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
