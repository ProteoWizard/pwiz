using System;
using System.Collections.Generic;
using System.Threading;

namespace pwiz.Common.SystemUtil.Caching
{
    public interface IResultFactory
    {
        object ComputeResult(CancellationToken cancellationToken, object parameter, IDictionary<ResultSpec, object> dependencies);
        IEnumerable<ResultSpec> GetDependencies(object parameter);
    }

    public interface IResultFactory<in TParameter, out TResult> : IResultFactory
    {
        public TResult ComputeResult(CancellationToken cancellationToken, TParameter parameter,
            IDictionary<ResultSpec, object> dependencies);
    }

    public abstract class ResultFactory : IResultFactory
    {
        public static ResultFactory<TParameter, TResult> FromFunction<TParameter, TResult>(Func<CancellationToken, TParameter, TResult> func)
        {
            return ResultFactory<TParameter, TResult>.FromFunction(func);
        }
        
        protected ResultFactory(Type parameterType, Type valueType)
        {
            ParameterType = parameterType;
            ValueType = valueType;
        }
        public abstract object ComputeResult(CancellationToken cancellationToken, object parameter, IDictionary<ResultSpec, object> dependencies);
        public Type ParameterType { get; }
        public Type ValueType { get; }
        public virtual IEnumerable<ResultSpec> GetDependencies(object parameter)
        {
            return Array.Empty<ResultSpec>();
        }

    }

    public abstract class ResultFactory<TParameter, TResult> : ResultFactory, IResultFactory<TParameter, TResult>
    {
        public static ResultFactory<TParameter, TResult> FromFunction(Func<CancellationToken, TParameter, TResult> func)
        {
            return new Impl(func);
        }

        protected ResultFactory() :base(typeof(TParameter), typeof(TResult))
        {
        }

        public sealed override object ComputeResult(CancellationToken cancellationToken, object parameter, IDictionary<ResultSpec, object> dependencies)
        {
            return ComputeResult(cancellationToken, (TParameter)parameter, dependencies);
        }

        public abstract TResult ComputeResult(CancellationToken cancellationToken, TParameter parameter,
            IDictionary<ResultSpec, object> dependencies);

        public sealed override IEnumerable<ResultSpec> GetDependencies(object parameter)
        {
            return GetDependencies((TParameter)parameter);
        }

        public virtual IEnumerable<ResultSpec> GetDependencies(TParameter parameter)
        {
            return Array.Empty<ResultSpec>();
        }

        private class Impl : ResultFactory<TParameter, TResult>
        {
            private Func<CancellationToken, TParameter, TResult> _impl;
            public Impl(Func<CancellationToken, TParameter, TResult> impl)
            {
                _impl = impl;
            }

            public override TResult ComputeResult(CancellationToken cancellationToken, TParameter parameter, IDictionary<ResultSpec, object> dependencies)
            {
                return _impl(cancellationToken, parameter);
            }
        }

        public TResult GetResult(IDictionary<ResultSpec, object> results, TParameter parameter)
        {
            if (results.TryGetValue(MakeResultSpec(parameter), out var result))
            {
                return (TResult)result;
            }

            return default;
        }

        public ResultSpec MakeResultSpec(TParameter parameter)
        {
            return new ResultSpec(this, parameter);
        }
    }
}
