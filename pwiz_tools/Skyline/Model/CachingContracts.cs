/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.5) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Interface for parameter types used with ReplicateCachingReceiver.
    /// Provides the document, cache key, and settings needed for caching.
    /// </summary>
    public interface ICachingParameters
    {
        /// <summary>The document for this computation.</summary>
        SrmDocument Document { get; }

        /// <summary>
        /// The cache key (typically replicate index). Entries are cached by this key.
        /// Use -1 for "all replicates" mode.
        /// </summary>
        int CacheKey { get; }

        /// <summary>
        /// Settings that affect the result. Cache is cleared when these change.
        /// Compared using Equals, so use an anonymous object or implement proper equality.
        /// </summary>
        object CacheSettings { get; }
    }

    /// <summary>
    /// Interface for result types used with ReplicateCachingReceiver.
    /// Provides the document the result was computed from.
    /// </summary>
    public interface ICachingResult
    {
        /// <summary>The document this result was computed from.</summary>
        SrmDocument Document { get; }
    }
}
