/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Loads a spectral library from its configured source, picking the
    /// format-specific loader and using the binary <c>.libcache</c> for
    /// fast reload. Relocated out of <c>AbstractScoringTask</c> — all of
    /// its collaborators (<see cref="LibraryCache"/>, the format loaders,
    /// <see cref="LibraryDeduplicator"/>) already live in this assembly.
    ///
    /// Logging is injected via callbacks rather than read from a pipeline
    /// context, so this stays in the I/O layer with no dependency on the
    /// task framework. The logic is otherwise unchanged from the original.
    /// </summary>
    public static class LibraryLoader
    {
        /// <summary>
        /// Load spectral library from the configured source, using binary cache
        /// when available. Matches Rust's .libcache mechanism for fast reload.
        /// </summary>
        public static List<LibraryEntry> Load(OspreyConfig config,
            Action<string> logInfo, Action<string> logWarning)
        {
            // Default the injected log callbacks to no-ops so a null delegate
            // cannot NRE this public API (matches PipelineContext's pattern).
            logInfo = logInfo ?? (_ => { });
            logWarning = logWarning ?? (_ => { });

            string path = config.LibrarySource.Path;
            // Route the library cache through ArtifactPaths (like the spectra
            // cache) so it follows --cache-dir / --work-dir and need not be
            // written beside a read-only library. Filename form preserved
            // ("<library-leaf>.libcache"); only the directory is redirected.
            string cachePath = Path.Combine(
                ArtifactPaths.ResolveCacheDir(path),
                Path.GetFileName(path) + ".libcache");

            // Try loading from binary cache first, but only when it is at least
            // as new as the source library. A cache older than its source is
            // stale -- the library was rebuilt in place without clearing the
            // cache -- and using it would silently load the PREVIOUS build,
            // whose decoys and pairing no longer match the current manifest.
            if (IsCacheFresh(cachePath, path))
            {
                try
                {
                    var cached = LibraryCache.LoadCache(cachePath);
                    if (cached != null && cached.Count > 0)
                    {
                        logInfo(string.Format(
                            "Loaded {0} library entries from cache '{1}'",
                            cached.Count, cachePath));
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    logWarning(string.Format(
                        "Failed to load library cache: {0}. Falling back to source.", ex.Message));
                }
            }
            else if (File.Exists(cachePath))
            {
                logInfo(string.Format(
                    "Library cache '{0}' is older than the source library; " +
                    "ignoring the stale cache and rebuilding from source.",
                    cachePath));
            }

            // Parse from source
            logInfo(string.Format("Loading spectral library from {0}...", path));

            List<LibraryEntry> entries;

            switch (config.LibrarySource.Format)
            {
                case LibraryFormat.DiannTsv:
                    var tsvLoader = new DiannTsvLoader();
                    entries = tsvLoader.Load(path);
                    break;

                case LibraryFormat.Blib:
                    var blibLoader = new BlibLoader();
                    entries = blibLoader.Load(path);
                    break;

                default:
                    throw new NotSupportedException(string.Format(
                        "Unsupported library format: {0}", config.LibrarySource.Format));
            }

            // Deduplicate library entries
            entries = LibraryDeduplicator.DeduplicateLibrary(entries);

            logInfo(string.Format("Loaded {0} library entries", entries.Count));

            // Save binary cache for next run
            try
            {
                LibraryCache.SaveCache(cachePath, entries);
                logInfo(string.Format(
                    "Saved library cache ({0} entries) to '{1}'",
                    entries.Count, cachePath));
            }
            catch (Exception ex)
            {
                logWarning(string.Format("Failed to save library cache: {0}", ex.Message));
            }

            return entries;
        }

        /// <summary>
        /// True when the binary cache at <paramref name="cachePath"/> exists and is
        /// at least as new as the source library at <paramref name="sourcePath"/>.
        /// A cache older than its source is stale: the library was rebuilt in place,
        /// so the cache holds the previous build and must be ignored. When the source
        /// cannot be located the cache is trusted, since it is then the only copy
        /// available (the historical behavior before this check existed).
        /// </summary>
        public static bool IsCacheFresh(string cachePath, string sourcePath)
        {
            if (!File.Exists(cachePath))
                return false;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return true;
            return File.GetLastWriteTimeUtc(cachePath) >= File.GetLastWriteTimeUtc(sourcePath);
        }
    }
}
