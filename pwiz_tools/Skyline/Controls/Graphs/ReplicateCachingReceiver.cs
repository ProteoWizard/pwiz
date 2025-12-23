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
using System.Threading;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Wraps a Receiver with a local cache to enable fast replicate switching.
    ///
    /// The standard Producer/Receiver pattern only caches the most recent result.
    /// When switching between replicates (e.g., replicate 1 → 2 → 1), each switch
    /// triggers a full recalculation because the old result is discarded.
    ///
    /// This wrapper maintains a local cache keyed by replicate index, allowing
    /// instant switching between previously-viewed replicates. The cache is
    /// invalidated when the document or settings change.
    ///
    /// Additionally, when switching away from a calculation in progress, this wrapper
    /// keeps a "completion listener" attached so the calculation continues in the
    /// background and its result is cached. This means switching away and back
    /// doesn't lose progress - like browser tabs that keep loading when you switch away.
    /// </summary>
    /// <typeparam name="TParam">Parameter type for the producer (must provide document, settings, and replicate info)</typeparam>
    /// <typeparam name="TResult">Result type from the producer</typeparam>
    public class ReplicateCachingReceiver<TParam, TResult> : IDisposable where TResult : class
    {
        private readonly Receiver<TParam, TResult> _receiver;
        private readonly Func<TParam, SrmDocument> _getDocument;
        private readonly Func<TParam, object> _getSettings;
        private readonly Func<TParam, int> _getCacheKey;

        private readonly ConcurrentDictionary<int, TResult> _localCache = new ConcurrentDictionary<int, TResult>();
        private readonly Dictionary<int, CompletionListener> _pendingListeners = new Dictionary<int, CompletionListener>();
        private SrmDocument _cachedDocument;
        private object _cachedSettings;
        private int _currentCacheKey = int.MinValue;

        // Exception from background completion listener, to be thrown on UI thread
        private Exception _pendingException;
        // Track the last reported exception to ensure we only report each error once
        private Exception _reportedException;

        /// <summary>
        /// Creates a caching wrapper around a Receiver.
        /// </summary>
        /// <param name="receiver">The underlying Receiver for background computation</param>
        /// <param name="getDocument">Function to extract document from parameters</param>
        /// <param name="getSettings">Function to extract settings from parameters (used for cache invalidation)</param>
        /// <param name="getCacheKey">Function to extract cache key (replicate index) from parameters</param>
        public ReplicateCachingReceiver(
            Receiver<TParam, TResult> receiver,
            Func<TParam, SrmDocument> getDocument,
            Func<TParam, object> getSettings,
            Func<TParam, int> getCacheKey)
        {
            _receiver = receiver;
            _getDocument = getDocument;
            _getSettings = getSettings;
            _getCacheKey = getCacheKey;
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
        /// Tries to get a cached result for the given cache key.
        /// Used to provide prior data for incremental updates.
        /// </summary>
        /// <param name="cacheKey">The cache key (typically replicate index)</param>
        /// <param name="result">The cached result if available</param>
        /// <returns>True if a cached result exists for this key</returns>
        public bool TryGetCachedResult(int cacheKey, out TResult result)
        {
            return _localCache.TryGetValue(cacheKey, out result);
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
        /// First checks local cache. If the document or settings have changed,
        /// the cache is cleared. On cache miss, falls back to the underlying Receiver.
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

            var document = _getDocument(param);
            var settings = _getSettings(param);
            var cacheKey = _getCacheKey(param);

            // Invalidate cache if document or settings changed
            if (!ReferenceEquals(document, _cachedDocument) || !Equals(settings, _cachedSettings))
            {
                ClearCache();
                _cachedDocument = document;
                _cachedSettings = settings;
            }

            // Check local cache first
            if (_localCache.TryGetValue(cacheKey, out result))
            {
                return true;
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
                // Store in local cache
                _localCache[cacheKey] = result;
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
            // Don't add duplicate listeners
            if (_pendingListeners.ContainsKey(cacheKey))
                return;

            // Get the current work order before the Receiver switches away from it
            var workOrder = _receiver.CurrentWorkOrder;
            if (workOrder == null)
                return;

            // Add our completion listener to keep the calculation running
            var listener = new CompletionListener(this, cacheKey, workOrder);
            _pendingListeners[cacheKey] = listener;
            _receiver.Cache.Listen(workOrder, listener);
        }

        /// <summary>
        /// Tries to get the current product - checks local cache first, then underlying Receiver.
        /// Does NOT throw on error - use HasError to check for errors without side effects.
        /// </summary>
        public bool TryGetCurrentProduct(out TResult result)
        {
            // Check local cache first for the current cache key
            if (_currentCacheKey != int.MinValue && _localCache.TryGetValue(_currentCacheKey, out result))
            {
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
            _cachedDocument = null;
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
                    // Store successful result in cache
                    _owner._localCache[_cacheKey] = typedResult;
                }

                // Clean up: unlisten and remove from pending
                Unlisten();
                _owner._pendingListeners.Remove(_cacheKey);
            }

            public void OnProductStatusChanged(WorkOrder key, int progress)
            {
                // We don't track progress for background calculations
            }

            public bool HasPendingNotifications => false;

            public void Unlisten()
            {
                _owner._receiver.Cache.Unlisten(_workOrder, this);
            }
        }
    }
}
