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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Callback to determine which cache entries should be removed.
    /// Called before each lookup with the current document and a read-only view of cached entries.
    /// </summary>
    /// <param name="currentDocument">The current document being displayed</param>
    /// <param name="cachedEntries">Map of cache key to the document each entry was computed from</param>
    /// <returns>Keys that should be removed from the cache</returns>
    public delegate IEnumerable<int> CleanCacheCallback(
        SrmDocument currentDocument,
        IReadOnlyDictionary<int, SrmDocument> cachedEntries);

    /// <summary>
    /// Wraps a Receiver with a local cache to enable fast replicate switching.
    ///
    /// The standard Producer/Receiver pattern only caches the most recent result.
    /// When switching between replicates (e.g., replicate 1 → 2 → 1), each switch
    /// triggers a full recalculation because the old result is discarded.
    ///
    /// This wrapper maintains a local cache keyed by replicate index, allowing
    /// instant switching between previously-viewed replicates.
    ///
    /// Cache validity is determined by the cleanCache callback, which is called
    /// before each lookup. The callback examines the cached entries and returns
    /// which keys should be removed. This allows different cache invalidation
    /// strategies for different use cases:
    /// - Simple: Clear all if document changed (RT graph)
    /// - Smart: Keep entries whose ChromatogramSet still exists (Relative Abundance)
    ///
    /// Additionally, when switching away from a calculation in progress, this wrapper
    /// keeps a "completion listener" attached so the calculation continues in the
    /// background and its result is cached. This means switching away and back
    /// doesn't lose progress - like browser tabs that keep loading when you switch away.
    /// </summary>
    /// <typeparam name="TParam">Parameter type implementing ICachingParameters</typeparam>
    /// <typeparam name="TResult">Result type implementing ICachingResult</typeparam>
    public class ReplicateCachingReceiver<TParam, TResult> : IDisposable
        where TParam : ICachingParameters
        where TResult : class, ICachingResult
    {
        private readonly Receiver<TParam, TResult> _receiver;
        private readonly CleanCacheCallback _cleanCache;

        // Cache stores results paired with their source document
        private readonly ConcurrentDictionary<int, CacheEntry> _localCache = new ConcurrentDictionary<int, CacheEntry>();
        private readonly ConcurrentDictionary<int, CompletionListener> _pendingListeners = new ConcurrentDictionary<int, CompletionListener>();
        private object _cachedSettings;
        private int _currentCacheKey = int.MinValue;

        // Exception from background completion listener, to be thrown on UI thread
        private Exception _pendingException;
        // Track the last reported exception to ensure we only report each error once
        private Exception _reportedException;

        /// <summary>
        /// A cached result paired with its source document.
        /// </summary>
        private readonly struct CacheEntry
        {
            public TResult Result { get; }
            public SrmDocument Document { get; }

            public CacheEntry(TResult result, SrmDocument document)
            {
                Result = result;
                Document = document;
            }
        }

        /// <summary>
        /// Creates a caching wrapper around a Receiver.
        /// </summary>
        /// <param name="receiver">The underlying Receiver for background computation</param>
        /// <param name="cleanCache">Callback to determine which cache entries to remove.
        /// Default: removes all entries if any cached document differs from current.</param>
        public ReplicateCachingReceiver(
            Receiver<TParam, TResult> receiver,
            CleanCacheCallback cleanCache = null)
        {
            _receiver = receiver;
            _cleanCache = cleanCache ?? DefaultCleanCache;
        }

        /// <summary>
        /// Default cache cleaning strategy: clear all entries if any cached document
        /// differs from the current document. This requires exact document match.
        /// </summary>
        public static IEnumerable<int> DefaultCleanCache(SrmDocument currentDoc, IReadOnlyDictionary<int, SrmDocument> cachedEntries)
        {
            // If any entry is from a different document, remove all
            if (cachedEntries.Values.Any(cachedDoc => !ReferenceEquals(cachedDoc, currentDoc)))
                return cachedEntries.Keys.ToList();
            return Enumerable.Empty<int>();
        }

        /// <summary>
        /// Event raised when the Receiver's progress changes.
        /// </summary>
        public event Action ProgressChange
        {
            add => _receiver.ProgressChange += value;
            remove => _receiver.ProgressChange -= value;
        }

        /// <summary>
        /// Gets the current progress value from the underlying Receiver.
        /// </summary>
        public int GetProgressValue() => _receiver.GetProgressValue();

        /// <summary>
        /// Returns true if the underlying Receiver is currently processing.
        /// </summary>
        public bool IsProcessing() => _receiver.IsProcessing();

        /// <summary>
        /// Removes stale cache entries using the cleanCache callback.
        /// Call this before TryGetCachedResult to ensure prior data is valid.
        /// </summary>
        /// <param name="document">The current document</param>
        public void CleanStaleEntries(SrmDocument document)
        {
            if (_localCache.Count == 0)
                return;

            var cachedDocuments = new ReadOnlyDictionary<int, SrmDocument>(
                _localCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Document));
            var keysToRemove = _cleanCache(document, cachedDocuments);

            foreach (var key in keysToRemove)
            {
                _localCache.TryRemove(key, out _);
                // Also clean up any pending listener for this key
                if (_pendingListeners.TryRemove(key, out var listener))
                {
                    listener.Unlisten();
                }
            }
        }

        /// <summary>
        /// Tries to get a cached result for the given cache key.
        /// Used to provide prior data for incremental updates.
        /// Call CleanStaleEntries first to ensure prior data is valid.
        /// </summary>
        /// <param name="cacheKey">The cache key (typically replicate index)</param>
        /// <param name="result">The cached result if available</param>
        /// <returns>True if a cached result exists for this key</returns>
        public bool TryGetCachedResult(int cacheKey, out TResult result)
        {
            if (_localCache.TryGetValue(cacheKey, out var entry))
            {
                result = entry.Result;
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Tries to get a cached result with its source document for the given cache key.
        /// Used for incremental updates when switching replicates - the cached result
        /// may be from an older document version but can still be used as prior data.
        /// </summary>
        /// <param name="cacheKey">The cache key (typically replicate index)</param>
        /// <param name="result">The cached result if available</param>
        /// <param name="sourceDocument">The document the result was computed from</param>
        /// <returns>True if a cached result exists for this key</returns>
        public bool TryGetCachedResultWithDocument(int cacheKey, out TResult result, out SrmDocument sourceDocument)
        {
            if (_localCache.TryGetValue(cacheKey, out var entry))
            {
                result = entry.Result;
                sourceDocument = entry.Document;
                return true;
            }
            result = default;
            sourceDocument = null;
            return false;
        }

        /// <summary>
        /// Throws any pending exception from background processing.
        /// Follows the LongWaitDlg pattern: exceptions are stored on the background thread
        /// and re-thrown on the UI thread via WrapAndThrowException.
        /// </summary>
        private void ThrowIfError()
        {
            // Check for pending exception from background listener (thrown once, then cleared)
            var pendingEx = Interlocked.Exchange(ref _pendingException, null);
            if (pendingEx != null)
            {
                _reportedException = pendingEx;
                ExceptionUtil.WrapAndThrowException(pendingEx);
            }

            // Check for error from underlying receiver (only throw if not already reported)
            var error = _receiver.GetError();
            if (error != null && !ReferenceEquals(error, _reportedException))
            {
                _reportedException = error;
                ExceptionUtil.WrapAndThrowException(error);
            }
        }

        /// <summary>
        /// Returns true if there is a pending or reported error.
        /// Use this to check for errors without triggering exception display.
        /// </summary>
        public bool HasError => _pendingException != null || _receiver.GetError() != null;

        /// <summary>
        /// Tries to get a cached or computed result for the given parameters.
        ///
        /// First calls the cleanCache callback to remove stale entries, then checks
        /// the local cache. An entry is a cache hit only if its document matches
        /// the current document exactly (ReferenceEquals). Entries from older document
        /// versions are kept for incremental updates via TryGetCachedResultWithDocument.
        ///
        /// On cache miss, falls back to the underlying Receiver.
        /// When the Receiver returns a result, it's stored in the local cache.
        ///
        /// If switching away from a calculation in progress, a completion listener
        /// is added to keep the calculation running in the background.
        /// </summary>
        /// <param name="param">Parameters for the computation</param>
        /// <param name="result">The result if available</param>
        /// <returns>True if a result is available, false if still computing</returns>
        public bool TryGetProduct(TParam param, out TResult result)
        {
            ThrowIfError();

            var document = param.Document;
            var settings = param.CacheSettings;
            var cacheKey = param.CacheKey;

            // Clear cache if settings changed
            if (!Equals(settings, _cachedSettings))
            {
                ClearCache();
                _cachedSettings = settings;
            }

            // Clean stale cache entries using the callback
            if (_localCache.Count > 0)
            {
                var cachedDocuments = new ReadOnlyDictionary<int, SrmDocument>(
                    _localCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Document));
                var keysToRemove = _cleanCache(document, cachedDocuments);
                foreach (var key in keysToRemove)
                {
                    _localCache.TryRemove(key, out _);
                    // Also clean up any pending listener for this key
                    if (_pendingListeners.TryRemove(key, out var listener))
                    {
                        listener.Unlisten();
                    }
                }
            }

            // Check local cache for exact hit
            if (_localCache.TryGetValue(cacheKey, out var entry))
            {
                if (ReferenceEquals(entry.Document, document))
                {
                    // Exact cache hit: same document, same replicate
                    result = entry.Result;
                    return true;
                }
                // Entry from older document version is kept for incremental updates
                // via TryGetCachedResultWithDocument
            }

            // If switching to a different replicate while calculation is in progress,
            // keep the old calculation running so its result gets cached
            if (cacheKey != _currentCacheKey && _receiver.IsProcessing())
            {
                KeepCalculationAlive(_currentCacheKey);
            }
            _currentCacheKey = cacheKey;

            // Fall back to Receiver
            if (_receiver.TryGetProduct(param, out result))
            {
                // Store in local cache with the document the result was computed from
                _localCache[cacheKey] = new CacheEntry(result, result.Document);
                TrackCachedResult(result);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Keeps the current calculation alive after switching away, so its result
        /// gets cached when complete.
        /// </summary>
        private void KeepCalculationAlive(int cacheKey)
        {
            // Get the current work order before the Receiver switches away from it
            var workOrder = _receiver.CurrentWorkOrder;
            if (workOrder == null)
                return;

            // Add our completion listener to keep the calculation running
            // TryAdd atomically checks for existing key and adds if not present
            var listener = new CompletionListener(this, cacheKey, workOrder);
            if (_pendingListeners.TryAdd(cacheKey, listener))
            {
                _receiver.Cache.Listen(workOrder, listener);
            }
        }

        /// <summary>
        /// Tries to get the current product - checks local cache first, then underlying Receiver.
        /// Does NOT throw on error - use HasError to check for errors without side effects.
        /// </summary>
        public bool TryGetCurrentProduct(out TResult result)
        {
            // Check local cache first for the current cache key
            if (_currentCacheKey != int.MinValue && _localCache.TryGetValue(_currentCacheKey, out var entry))
            {
                result = entry.Result;
                return true;
            }
            return _receiver.TryGetCurrentProduct(out result);
        }

        /// <summary>
        /// Clears the local cache and cancels any pending background calculations.
        /// Call when you know cached data is stale.
        /// </summary>
        public void ClearCache()
        {
            _localCache.Clear();
            _cachedSettings = null;
            _currentCacheKey = int.MinValue;
            _reportedException = null;  // Allow new errors to be reported

            // Clean up pending listeners
            foreach (var listener in _pendingListeners.Values)
            {
                listener.Unlisten();
            }
            _pendingListeners.Clear();
        }

        public void Dispose()
        {
            ClearCache();
            _receiver.Dispose();
        }

        /// <summary>
        /// Listener that keeps a calculation alive after the main Receiver switches away.
        /// When the result arrives, it's stored in the local cache and the listener removes itself.
        /// </summary>
        private class CompletionListener : IProductionListener
        {
            private readonly ReplicateCachingReceiver<TParam, TResult> _owner;
            private readonly int _cacheKey;
            private readonly WorkOrder _workOrder;
            private int _isListening = 1;  // 1 = listening, 0 = unlistened

            public CompletionListener(ReplicateCachingReceiver<TParam, TResult> owner, int cacheKey, WorkOrder workOrder)
            {
                _owner = owner;
                _cacheKey = cacheKey;
                _workOrder = workOrder;
            }

            public void OnProductAvailable(WorkOrder key, ProductionResult result)
            {
                if (result.Exception != null)
                {
                    // Store exception for later throwing on UI thread (follows LongWaitDlg pattern)
                    _owner._pendingException = result.Exception;
                }
                else if (result.Value is TResult typedResult)
                {
                    // Store successful result in cache with its source document
                    _owner._localCache[_cacheKey] = new CacheEntry(typedResult, typedResult.Document);
                    TrackCachedResult(typedResult);
                }

                // Clean up: unlisten and remove from pending
                Unlisten();
                _owner._pendingListeners.TryRemove(_cacheKey, out _);
            }

            public void OnProductStatusChanged(WorkOrder key, int progress)
            {
                // We don't track progress for background calculations
            }

            public bool HasPendingNotifications => false;

            public void Unlisten()
            {
                // Atomic check-and-set to prevent double Unlisten from concurrent threads
                if (Interlocked.Exchange(ref _isListening, 0) == 0)
                    return;
                _owner._receiver.Cache.Unlisten(_workOrder, this);
            }
        }

        #region Cache tracking for tests

        /// <summary>
        /// When true, records all cached results for later inspection by tests.
        /// Use ScopedAction to enable/disable around test code.
        /// Setting to true clears any previously tracked results.
        /// </summary>
        public static bool TrackCaching
        {
            get => _trackCaching;
            set
            {
                _trackCaching = value;
                if (value)
                    _cachedSinceTracked = new List<TResult>();
                // Don't clear on false - test needs to read the results
            }
        }

        // ReSharper disable once StaticMemberInGenericType
        private static bool _trackCaching;
        private static List<TResult> _cachedSinceTracked;

        /// <summary>
        /// Returns all results cached since TrackCaching was enabled.
        /// Use First() to get the initial (full) calculation after document reopen.
        /// Subsequent entries should be incremental updates with zero recalculation.
        /// </summary>
        public static IEnumerable<TResult> CachedSinceTracked =>
            _cachedSinceTracked ?? Enumerable.Empty<TResult>();

        private static void TrackCachedResult(TResult result)
        {
            if (_trackCaching && _cachedSinceTracked != null)
                _cachedSinceTracked.Add(result);
        }

        #endregion
    }
}
