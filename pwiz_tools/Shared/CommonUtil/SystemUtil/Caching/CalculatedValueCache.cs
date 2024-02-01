using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace pwiz.Common.SystemUtil.Caching
{
    public class CalculatedValueCache
    {
        public static readonly CalculatedValueCache INSTANCE = new CalculatedValueCache();
        private Dictionary<ResultSpec, Entry> _entries = new Dictionary<ResultSpec, Entry>();

        public void Listen(ResultSpec key, CalculatedValueListener listener)
        {
            lock (this)
            {
                GetOrCreateEntry(key).AddListener(listener);
            }
        }

        public CalculatorResult GetResult(ResultSpec key)
        {
            lock (this)
            {
                _entries.TryGetValue(key, out var entry);
                return entry?.Result;
            }
        }

        public void Unlisten(ResultSpec key, CalculatedValueListener listener)
        {
            lock (this)
            {
                GetEntry(key).RemoveListener(listener);
            }
        }

        public int GetProgressValue(ResultSpec key)
        {
            lock (this)
            {
                _entries.TryGetValue(key, out var entry);
                return entry?.ProgressValue??0;
            }
        }

        private Entry GetEntry(ResultSpec key)
        {
            lock (this)
            {
                if (!_entries.TryGetValue(key, out var entry))
                {
                    throw new InvalidOperationException(string.Format("Entry {0} not found", key));
                }

                return entry;
            }
        }

        private Entry GetOrCreateEntry(ResultSpec key)
        {
            lock (this)
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    return entry;
                }

                entry = new Entry(this, key);
                _entries.Add(key, entry);
                return entry;
            }
        }

        private void RemoveEntry(Entry entry)
        {
            lock (this)
            {
                if (!_entries.Remove(entry.Key))
                {
                    throw new ArgumentException("Entry not found");
                }
            }
        }
        
        private class Entry : ICalculatedValueListener
        {
            private HashSet<ResultSpec> _dependencies;
            private Dictionary<ResultSpec, object> _dependencyResultValues = new Dictionary<ResultSpec, object>();
            private List<ICalculatedValueListener> _listeners = new List<ICalculatedValueListener>();

            private CancellationTokenSource _cancellationTokenSource;
            public Entry(CalculatedValueCache cache, ResultSpec key)
            {
                Cache = cache;
                Key = key;
                _dependencies = key.GetDependencies().ToHashSet();
            }

            public CalculatedValueCache Cache { get; }
            public ResultSpec Key { get; }
            public void OnResultAvailable(ResultSpec key, CalculatorResult result)
            {
                lock (this)
                {
                    if (Result != null)
                    {
                        return;
                    }

                    if (result.Exception != null || Equals(key, Key))
                    {
                        NotifyResultAvailable(result);
                        return;
                    }
                    _dependencyResultValues[key] = result.Value;
                    TryStartCalculation();
                }
            }

            public void AddListener(ICalculatedValueListener listener)
            {
                if (_listeners.Contains(listener))
                {
                    throw new ArgumentException(@"Listener already added");
                }

                if (_listeners.Count == 0)
                {
                    BeforeFirstListenerAdded();
                }
                _listeners.Add(listener);
            }

            public void RemoveListener(ICalculatedValueListener listener)
            {
                lock (Cache)
                {
                    if (!_listeners.Remove(listener))
                    {
                        throw new ArgumentException(@"Listener not added");
                    }

                    if (_listeners.Count == 0)
                    {
                        AfterLastListenerRemoved();
                    }
                }
            }

            private void BeforeFirstListenerAdded()
            {
                lock (Cache)
                {
                    foreach (var input in _dependencies)
                    {
                        Cache.GetOrCreateEntry(input).AddListener(this);
                    }
                    TryStartCalculation();
                }
            }

            private void AfterLastListenerRemoved()
            {
                lock (Cache)
                {
                    _cancellationTokenSource?.Cancel();
                    foreach (var input in _dependencies)
                    {
                        Cache.GetEntry(input).RemoveListener(this);
                    }
                    Cache.RemoveEntry(this);
                }
            }

            private void NotifyResultAvailable(CalculatorResult result)
            {
                ICalculatedValueListener[] listeners;
                lock (Cache)
                {
                    Result = result;
                    _cancellationTokenSource?.Cancel();
                    listeners = _listeners.ToArray();
                }

                foreach (var listener in listeners)
                {
                    listener.OnResultAvailable(Key, result);
                }
            }

            private void TryStartCalculation()
            {
                lock (Cache)
                {
                    if (_cancellationTokenSource != null)
                    {
                        return;
                    }

                    if (_dependencies.Count > _dependencyResultValues.Count)
                    {
                        return;
                    }
                    _cancellationTokenSource = new CancellationTokenSource();
                    var progressCallback = new ProgressCallback(_cancellationTokenSource.Token);
                    progressCallback.ProgressChange += OnMyProgressChanged;
                    CommonActionUtil.RunAsync(() =>
                    {
                        try
                        {
                            object value = Key.Calculator.ComputeResult(progressCallback, Key.Parameter, _dependencyResultValues);
                            NotifyResultAvailable(CalculatorResult.Success(value));
                        }
                        catch (Exception ex)
                        {
                            NotifyResultAvailable(CalculatorResult.Error(ex));
                        }
                    });
                }
            }

            public CalculatorResult Result { get; private set; }

            public bool IsWaiting()
            {
                lock (this)
                {
                    return false != _cancellationTokenSource?.IsCancellationRequested;
                }
            }

            public void OnProgressChanged(ResultSpec key, int progress)
            {
                ICalculatedValueListener[] listeners;
                lock (Cache)
                {
                    ProgressValue = progress;
                    listeners = _listeners.ToArray();
                }

                foreach (var listener in listeners)
                {
                    listener.OnProgressChanged(key, progress);
                }
            }

            private void OnMyProgressChanged(int progress)
            {
                OnProgressChanged(Key, progress);
            }
            
            public int ProgressValue { get; private set; }
        }

        public bool IsWaiting()
        {
            lock (this)
            {
                return _entries.Values.Any(entry => entry.IsWaiting());
            }
        }
    }
}
