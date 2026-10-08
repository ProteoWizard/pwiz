//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Collections.Generic;

namespace Pwiz.SeeMS;

/// <summary>
/// Fixed-capacity cache that evicts the least recently used entry: the part of the
/// System.Runtime.Caching.Generic assembly HeatmapForm used. That assembly was a .NET Framework
/// 4.0 binary that only ever shipped inside the C++ UNIFI SDK directory.
/// </summary>
public sealed class MemoryCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _entries = new();
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _mostRecentFirst = new();
    private readonly object _lock = new();

    /// <summary>Creates a cache holding at most <paramref name="capacity"/> entries (at least one).</summary>
    public MemoryCache(int capacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    /// <summary>Number of entries currently held.</summary>
    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    /// <summary>Returns the value cached for <paramref name="key"/>, or computes it with
    /// <paramref name="valueFactory"/>(<paramref name="factoryArgument"/>) and caches it. The
    /// factory runs outside the lock, so two threads asking for the same new key may both compute
    /// it; the first value stored wins.</summary>
    public TValue GetOrAdd<TArg>(TKey key, Func<TArg, TValue> valueFactory, TArg factoryArgument)
    {
        lock (_lock)
        {
            if (TryGetAndTouch(key, out var cached))
                return cached;
        }

        var value = valueFactory(factoryArgument);
        lock (_lock)
        {
            if (TryGetAndTouch(key, out var cached))
                return cached;
            _entries[key] = _mostRecentFirst.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
            if (_entries.Count > _capacity)
            {
                var oldest = _mostRecentFirst.Last!;
                _mostRecentFirst.RemoveLast();
                _entries.Remove(oldest.Value.Key);
            }
            return value;
        }
    }

    private bool TryGetAndTouch(TKey key, out TValue value)
    {
        if (_entries.TryGetValue(key, out var node))
        {
            _mostRecentFirst.Remove(node);
            _mostRecentFirst.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
        value = default!;
        return false;
    }
}
