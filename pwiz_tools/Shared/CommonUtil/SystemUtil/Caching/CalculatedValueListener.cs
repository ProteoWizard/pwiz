using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.SystemUtil.Caching
{
    public interface ICalculatedValueListener
    {
        public void OnResultAvailable(ResultSpec key, CalculatorResult result);
    }
    
    
    public class CalculatedValueListener : ICalculatedValueListener
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

        public event Action ResultsAvailable;

        public object[] TryGetValues(ResultSpec[] specs)
        {
            foreach (var specToAdd in specs.Except(_specs))
            {
                Cache.Listen(specToAdd, this);
            }

            foreach (var specToRemove in _specs.Except(specs))
            {
                Cache.Unlisten(specToRemove, this);
            }

            _specs = specs.ToHashSet();
            var results = specs.Select(spec => Cache.GetResult(spec)).ToList();
            var errors = results.Select(result => result?.Exception).Where(ex => ex != null).ToList();
            if (errors.Count != 0)
            {
                throw new AggregateException(string.Join(Environment.NewLine, errors.Select(e=>e.Message)), errors);
            }

            if (results.Contains(null))
            {
                return null;
            }
            return results.Select(r => r.Value).ToArray();
        }

        public bool TryGetValue<TParameter, TResult>(ResultFactory<TParameter, TResult> calculator, TParameter argument, out TResult result)
        {
            var spec = new ResultSpec(calculator, argument);
            var values = TryGetValues(new[] { spec });
            if (values == null)
            {
                result = default;
                return false;
            }

            result = (TResult)values[0];
            return true;
        }

        public bool TryGetValue<TParam1, TParam2, TResult>(ResultFactory<Tuple<TParam1, TParam2>, TResult> calculator,
            TParam1 param1, TParam2 param2, out TResult result)
        {
            return TryGetValue(calculator, Tuple.Create(param1, param2), out result);
        }
    }
}
