using System;
using System.Collections.Generic;

namespace pwiz.Common.SystemUtil.Caching
{
    public interface IProducer
    {
        object ComputeResult(ProgressCallback progressCallback, object parameter, IDictionary<WorkOrder, object> dependencies);
        IEnumerable<WorkOrder> GetInputs(object parameter);
    }

    public interface IProducer<in TParameter, out TResult> : IProducer
    {
        public TResult ProduceResult(ProgressCallback progressCallback, TParameter parameter,
            IDictionary<WorkOrder, object> dependencies);
    }

    public abstract class Producer : IProducer
    {
        public static Producer<TParameter, TResult> FromFunction<TParameter, TResult>(Func<ProgressCallback, TParameter, TResult> func)
        {
            return Producer<TParameter, TResult>.FromFunction(func);
        }
        
        protected Producer(Type parameterType, Type valueType)
        {
            ParameterType = parameterType;
            ValueType = valueType;
        }
        public abstract object ComputeResult(ProgressCallback progressCallback, object parameter, IDictionary<WorkOrder, object> dependencies);
        public Type ParameterType { get; }
        public Type ValueType { get; }
        public virtual IEnumerable<WorkOrder> GetInputs(object parameter)
        {
            return Array.Empty<WorkOrder>();
        }

    }

    public abstract class Producer<TParameter, TResult> : Producer, IProducer<TParameter, TResult>
    {
        public static Producer<TParameter, TResult> FromFunction(Func<ProgressCallback, TParameter, TResult> func)
        {
            return new Impl(func);
        }

        protected Producer() :base(typeof(TParameter), typeof(TResult))
        {
        }

        public sealed override object ComputeResult(ProgressCallback progressCallback, object parameter, IDictionary<WorkOrder, object> inputs)
        {
            return ProduceResult(progressCallback, (TParameter)parameter, inputs);
        }

        public abstract TResult ProduceResult(ProgressCallback progressCallback, TParameter parameter,
            IDictionary<WorkOrder, object> inputs);

        public sealed override IEnumerable<WorkOrder> GetInputs(object parameter)
        {
            return GetInputs((TParameter)parameter);
        }

        public virtual IEnumerable<WorkOrder> GetInputs(TParameter parameter)
        {
            return Array.Empty<WorkOrder>();
        }

        private class Impl : Producer<TParameter, TResult>
        {
            private Func<ProgressCallback, TParameter, TResult> _impl;
            public Impl(Func<ProgressCallback, TParameter, TResult> impl)
            {
                _impl = impl;
            }

            public override TResult ProduceResult(ProgressCallback progressCallback, TParameter parameter, IDictionary<WorkOrder, object> dependencies)
            {
                return _impl(progressCallback, parameter);
            }
        }

        public TResult GetResult(IDictionary<WorkOrder, object> results, TParameter parameter)
        {
            if (results.TryGetValue(MakeWorkOrder(parameter), out var result))
            {
                return (TResult)result;
            }

            return default;
        }

        public WorkOrder MakeWorkOrder(TParameter parameter)
        {
            return new WorkOrder(this, parameter);
        }
    }
}
