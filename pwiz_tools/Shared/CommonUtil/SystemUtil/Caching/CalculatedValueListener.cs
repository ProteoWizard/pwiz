using System;
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.Caching
{
    public interface ICalculatedValueListener
    {
        public void OnResultAvailable(ResultSpec key, CalculatorResult result);
        public void OnProgressChanged(ResultSpec key, int progress);
    }
    
    
    public class CalculatedValueListener : ICalculatedValueListener, IDisposable
    {
        public static CalculatedValueListener<TParam1, TParam2, TResult> FromFactory<TParam1, TParam2, TResult>(
            Control owner, ResultFactory<Tuple<TParam1, TParam2>, TResult> factory)
        {
            return new CalculatedValueListener<TParam1, TParam2, TResult>(CalculatedValueCache.INSTANCE, owner,
                factory);
        }
        
        
        private ResultSpec _resultSpec;
        public CalculatedValueListener(CalculatedValueCache cache, Control ownerControl, ResultFactory factory)
        {
            Cache = cache;
            OwnerControl = ownerControl;
            Factory = factory;
            OwnerControl.HandleDestroyed += OwnerControlHandleDestroyed;
        }

        public CalculatedValueCache Cache
        {
            get;
        }
        
        public Control OwnerControl { get; private set; }
        
        public ResultFactory Factory { get; }

        public void OnResultAvailable(ResultSpec key, CalculatorResult result)
        {
            var resultsAvailable = ResultsAvailable;
            if (resultsAvailable != null)
            {
                Cache.IncrementWaitingCount();
                if (!CommonActionUtil.SafeBeginInvoke(OwnerControl, () =>
                    {
                        try
                        {
                            resultsAvailable();
                        }
                        finally
                        {
                            Cache.DecrementWaitingCount();
                        }
                    }))
                {
                    Cache.DecrementWaitingCount();
                }
            }
        }

        public void OnProgressChanged(ResultSpec key, int progress)
        {
            lock (this)
            {
                var progressChange = ProgressChange;
                if (progressChange != null)
                {
                    CommonActionUtil.SafeBeginInvoke(OwnerControl, () => { progressChange(); });
                }
            }
        }

        public event Action ResultsAvailable;
        public event Action ProgressChange;

        public int GetProgressValue()
        {
            if (_resultSpec != null)
            {
                return Cache.GetProgressValue(_resultSpec);
            }

            return 0;
        }

        public Exception GetError()
        {
            if (_resultSpec != null)
            {
                return Cache.GetResult(_resultSpec)?.Exception;
            }

            return null;
        }

        public bool TryGetValue(object argument, out object resultValue)
        {
            lock (this)
            {
                var newResultSpec = new ResultSpec(Factory, argument);
                if (!Equals(newResultSpec, _resultSpec))
                {
                    Cache.Listen(newResultSpec, this);
                    if (_resultSpec != null)
                    {
                        Cache.Unlisten(_resultSpec, this);
                    }

                    _resultSpec = newResultSpec;
                }

                var result = Cache.GetResult(_resultSpec);
                if (result == null || result.Exception != null)
                {
                    resultValue = null;
                    return false;
                }

                resultValue = result.Value;
                return true;
            }
        }
        public void Dispose()
        {
            lock (this)
            {
                Cache.Unlisten(_resultSpec, this);
                _resultSpec = null;
                if (OwnerControl != null)
                {
                    OwnerControl.HandleDestroyed -= OwnerControlHandleDestroyed;
                    OwnerControl = null;
                }
            }
        }

        private void OwnerControlHandleDestroyed(object sender, EventArgs eventArgs)
        {
            Dispose();
        }

        public bool IsProcessing()
        {
            var resultSpec = _resultSpec;
            return resultSpec != null && null == Cache.GetResult(resultSpec);
        }
    }

    public class CalculatedValueListener<TParam, TResult> : CalculatedValueListener
    {
        public CalculatedValueListener(CalculatedValueCache cache, Control ownerControl,
            ResultFactory<TParam, TResult> factory) : base(cache, ownerControl, factory)
        {
            
        }

        public bool TryGetValue(TParam argument, out TResult resultValue)
        {
            if (base.TryGetValue(argument, out var resultObject))
            {
                resultValue = (TResult) resultObject;
                return true;
            }

            resultValue = default;
            return false;
        }
    }

    public class CalculatedValueListener<TParam1, TParam2, TResult> : CalculatedValueListener<Tuple<TParam1, TParam2>, TResult>
    {
        public CalculatedValueListener(CalculatedValueCache cache, Control ownerControl,
            ResultFactory<Tuple<TParam1, TParam2>, TResult> factory) : base(cache, ownerControl, factory)
        {
        }

        public bool TryGetValue(TParam1 param1, TParam2 param2, out TResult resultValue)
        {
            return TryGetValue(Tuple.Create(param1, param2), out resultValue);
        }
    }
}
