using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.SystemUtil.Caching
{
    public interface ICalculatedValueListener
    {
        public void OnResultAvailable(ResultSpec key, CalculatorResult result);
        public void OnProgressChanged(ResultSpec key, int progress);
    }
    
    
    public class CalculatedValueListener : ICalculatedValueListener, IDisposable
    {
        private HashSet<ResultSpec> _specs = new HashSet<ResultSpec>();
        public CalculatedValueListener(CalculatedValueCache cache)
        {
            Cache = cache;
        }

        public CalculatedValueCache Cache
        {
            get;
        }

        public void OnResultAvailable(ResultSpec key, CalculatorResult result)
        {
            ResultsAvailable?.Invoke();
        }

        public void OnProgressChanged(ResultSpec key, int progress)
        {
            lock (this)
            {
                ProgressChange?.Invoke();
            }
        }

        public event Action ResultsAvailable;
        public event Action ProgressChange;

        public int GetProgressValue()
        {
            int count = 0;
            int total = 0;
            foreach (var spec in GetResultSpecs())
            {
                count++;
                total += Cache.GetProgressValue(spec);
            }

            if (count == 0)
            {
                return 0;
            }

            return Math.Min(100, Math.Max(0, total / count));
        }

        private ResultSpec[] GetResultSpecs()
        {
            lock (this)
            {
                return _specs.ToArray();
            }
        }

        public Exception GetError()
        {
            var errors = new List<Exception>();
            foreach (var spec in GetResultSpecs())
            {
                var result = Cache.GetResult(spec);
                var exception = result?.Exception;
                if (exception != null)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count == 0)
            {
                return null;
            }

            if (errors.Count == 1)
            {
                return errors[0];
            }

            return new AggregateException(errors);
        }

        public object[] GetValuesOrThrow(ResultSpec[] specs)
        {
            lock (this)
            {
                foreach (var specToAdd in specs.Except(_specs))
                {
                    Cache.Listen(specToAdd, this);
                    _specs.Add(specToAdd);
                }

                foreach (var specToRemove in _specs.Except(specs).ToList())
                {
                    Cache.Unlisten(specToRemove, this);
                    _specs.Remove(specToRemove);
                }
                
                var results = specs.Select(spec => Cache.GetResult(spec)).ToList();
                var errors = results.Select(result => result?.Exception).Where(ex => ex != null).ToList();
                if (errors.Count != 0)
                {
                    throw new AggregateException(string.Join(Environment.NewLine, errors.Select(e => e.Message)), errors);
                }

                if (results.Contains(null))
                {
                    return null;
                }
                return results.Select(r => r.Value).ToArray();
            }
        }

        public bool TryGetValue<TParameter, TResult>(ResultFactory<TParameter, TResult> calculator, TParameter argument, out TResult result)
        {
            var spec = new ResultSpec(calculator, argument);
            try
            {
                var values = GetValuesOrThrow(new[] { spec });
                if (values == null)
                {
                    result = default;
                    return false;
                }
                result = (TResult)values[0];
                return true;
            }
            catch (Exception)
            {
                result = default;
                return false;
            }
        }

        public bool TryGetValue<TParam1, TParam2, TResult>(ResultFactory<Tuple<TParam1, TParam2>, TResult> calculator,
            TParam1 param1, TParam2 param2, out TResult result)
        {
            return TryGetValue(calculator, Tuple.Create(param1, param2), out result);
        }

        public void Dispose()
        {
            lock (this)
            {
                foreach (var key in _specs)
                {
                    Cache.Unlisten(key, this);
                }
                _specs.Clear();
            }
        }

        public bool IsProcessing()
        {
            return GetResultSpecs().Any(spec => null == Cache.GetResult(spec));
        }
    }
}
