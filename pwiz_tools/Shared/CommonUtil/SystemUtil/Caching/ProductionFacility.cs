using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace pwiz.Common.SystemUtil.Caching
{
    public class ProductionFacility
    {
        public static readonly ProductionFacility DEFAULT = new ProductionFacility();
        private Dictionary<WorkOrder, Entry> _entries = new Dictionary<WorkOrder, Entry>();

        public void Listen(WorkOrder key, IProductionListener listener)
        {
            if (key == null)
            {
                return;
            }
            lock (this)
            {
                GetOrCreateEntry(key).AddListener(listener);
            }
        }

        public ProductionResult GetResult(WorkOrder key)
        {
            lock (this)
            {
                _entries.TryGetValue(key, out var entry);
                return entry?.Result;
            }
        }

        public void Unlisten(WorkOrder key, IProductionListener listener)
        {
            if (key == null)
            {
                return;
            }
            lock (this)
            {
                GetEntry(key).RemoveListener(listener);
            }
        }

        public int GetProgressValue(WorkOrder key)
        {
            lock (this)
            {
                _entries.TryGetValue(key, out var entry);
                return entry?.ProgressValue??0;
            }
        }

        private Entry GetEntry(WorkOrder key)
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

        private Entry GetOrCreateEntry(WorkOrder key)
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
        
        private class Entry : IProductionListener
        {
            private HashSet<WorkOrder> _dependencies;
            private Dictionary<WorkOrder, object> _dependencyResultValues = new Dictionary<WorkOrder, object>();
            private List<IProductionListener> _listeners = new List<IProductionListener>();

            private CancellationTokenSource _cancellationTokenSource;
            public Entry(ProductionFacility cache, WorkOrder key)
            {
                Cache = cache;
                Key = key;
                _dependencies = key.GetInputs().ToHashSet();
            }

            public ProductionFacility Cache { get; }
            public WorkOrder Key { get; }
            public void OnProductAvailable(WorkOrder key, ProductionResult result)
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

            public void AddListener(IProductionListener listener)
            {
                lock (Cache)
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
            }

            public void RemoveListener(IProductionListener listener)
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

            private void NotifyResultAvailable(ProductionResult result)
            {
                IProductionListener[] listeners;
                lock (Cache)
                {
                    Result = result;
                    _cancellationTokenSource?.Cancel();
                    listeners = _listeners.ToArray();
                }

                foreach (var listener in listeners)
                {
                    listener.OnProductAvailable(Key, result);
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
                    
                    foreach (var dependency in _dependencies.Except(_dependencyResultValues.Keys))
                    {
                        var result = Cache.GetResult(dependency);
                        if (result != null)
                        {
                            if (result.Exception != null)
                            {
                                NotifyResultAvailable(result);
                                return;
                            }
                            _dependencyResultValues.Add(dependency, result.Value);
                        }
                    }
                    if (_dependencies.Count > _dependencyResultValues.Count)
                    {
                        return;
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                    var progressCallback = new ProductionMonitor(_cancellationTokenSource.Token, OnMyProgressChanged);
                    CommonActionUtil.RunAsync(() =>
                    {
                        try
                        {
                            Cache.IncrementWaitingCount();
                            object value = Key.Producer.ProduceResult(progressCallback, Key.WorkParameter,
                                _dependencyResultValues);
                            NotifyResultAvailable(ProductionResult.Success(value));
                        }
                        catch (Exception ex)
                        {
                            NotifyResultAvailable(ProductionResult.Error(ex));
                        }
                        finally
                        {
                            Cache.DecrementWaitingCount();
                        }
                    });
                }
            }

            public ProductionResult Result { get; private set; }

            public bool HasPendingNotifications
            {
                get
                {
                    lock (Cache)
                    {
                        return _listeners.Any(listener => listener.HasPendingNotifications);
                    }

                }
            }

            public void OnProductStatusChanged(WorkOrder key, int progress)
            {
                IProductionListener[] listeners;
                lock (Cache)
                {
                    ProgressValue = progress;
                    listeners = _listeners.ToArray();
                }

                foreach (var listener in listeners)
                {
                    listener.OnProductStatusChanged(key, progress);
                }
            }

            private void OnMyProgressChanged(int progress)
            {
                OnProductStatusChanged(Key, progress);
            }
            
            public int ProgressValue { get; private set; }
        }

        public bool IsWaiting()
        {
            lock (this)
            {
                return _waitingCount != 0 || _entries.Values.Any(entry=>entry.HasPendingNotifications);
            }
        }

        private int _waitingCount;
        public int GetWaitingCount()
        {
            return _waitingCount;
        }
        public void IncrementWaitingCount()
        {
            Interlocked.Increment(ref _waitingCount);
        }

        public void DecrementWaitingCount()
        {
            Interlocked.Decrement(ref _waitingCount);
        }
    }
}
