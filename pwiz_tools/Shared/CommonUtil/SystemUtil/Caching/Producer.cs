/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.Caching
{
    public interface IProducer
    {
        object ProduceResult(ProductionMonitor productionMonitor, object workParameter, IDictionary<WorkOrder, object> inputs);
        /// <summary>
        /// Returns the list of other things that are needed to produce the products.
        /// These are intermediate results which might also be needed by other producers.
        /// </summary>
        IEnumerable<WorkOrder> GetInputs(object workParameter);
    }

    public interface IProducer<in TParameter, out TResult> : IProducer
    {
        public TResult ProduceResult(ProductionMonitor productionMonitor, TParameter workParameter,
            IDictionary<WorkOrder, object> dependencies);
    }

    public abstract class Producer : IProducer
    {
        public static Producer<TParameter, TResult> FromFunction<TParameter, TResult>(Func<ProductionMonitor, TParameter, TResult> func)
        {
            return Producer<TParameter, TResult>.FromFunction(func);
        }
        
        protected Producer(Type parameterType, Type valueType)
        {
            ParameterType = parameterType;
            ValueType = valueType;
        }
        public abstract object ProduceResult(ProductionMonitor productionMonitor, object workParameter, IDictionary<WorkOrder, object> dependencies);
        public Type ParameterType { get; }
        public Type ValueType { get; }
        public virtual IEnumerable<WorkOrder> GetInputs(object workParameter)
        {
            return Array.Empty<WorkOrder>();
        }

    }

    public abstract class Producer<TParameter, TResult> : Producer, IProducer<TParameter, TResult>
    {
        public static Producer<TParameter, TResult> FromFunction(Func<ProductionMonitor, TParameter, TResult> func)
        {
            return new Impl(func);
        }

        protected Producer() :base(typeof(TParameter), typeof(TResult))
        {
        }

        public sealed override object ProduceResult(ProductionMonitor productionMonitor, object workParameter, IDictionary<WorkOrder, object> inputs)
        {
            return ProduceResult(productionMonitor, (TParameter)workParameter, inputs);
        }

        public abstract TResult ProduceResult(ProductionMonitor productionMonitor, TParameter parameter,
            IDictionary<WorkOrder, object> inputs);

        public sealed override IEnumerable<WorkOrder> GetInputs(object workParameter)
        {
            return GetInputs((TParameter)workParameter);
        }

        public virtual IEnumerable<WorkOrder> GetInputs(TParameter parameter)
        {
            return Array.Empty<WorkOrder>();
        }

        private class Impl : Producer<TParameter, TResult>
        {
            private Func<ProductionMonitor, TParameter, TResult> _impl;
            public Impl(Func<ProductionMonitor, TParameter, TResult> impl)
            {
                _impl = impl;
            }

            public override TResult ProduceResult(ProductionMonitor productionMonitor, TParameter parameter, IDictionary<WorkOrder, object> dependencies)
            {
                return _impl(productionMonitor, parameter);
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

        public WorkOrder MakeWorkOrder(TParameter workParameter)
        {
            return new WorkOrder(this, workParameter);
        }

        public Receiver<TParameter, TResult> RegisterCustomer(Control ownerControl, Action productAvailableAction)
        {
            var customer = new Receiver<TParameter, TResult>(ProductionFacility.DEFAULT, ownerControl, this);
            if (productAvailableAction != null)
            {
                customer.ProductAvailable += productAvailableAction;
            }

            return customer;
        }
    }
}
