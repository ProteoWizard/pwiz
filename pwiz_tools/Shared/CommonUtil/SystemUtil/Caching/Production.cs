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
using System.Threading;

namespace pwiz.Common.SystemUtil.Caching
{
    public class WorkOrder
    {
        private int _hashCode;
        public WorkOrder(Producer producer, object parameter)
        {
            Producer = producer;
            WorkParameter = parameter;
            unchecked
            {
                _hashCode = Producer.GetHashCode() * 397 ^ EqualityComparer<object>.Default.GetHashCode(WorkParameter);
            }
        }

        public Producer Producer { get; }
        public object WorkParameter { get; private set; }
        public Type ValueType
        {
            get { return Producer.ValueType; }
        }

        protected bool Equals(WorkOrder other)
        {
            return Equals(Producer, other.Producer) && Equals(WorkParameter, other.WorkParameter);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((WorkOrder)obj);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public IEnumerable<WorkOrder> GetInputs()
        {
            return Producer.GetInputs(WorkParameter);
        }
    }
    public interface IProductionListener
    {
        public void OnProductAvailable(WorkOrder key, ProductionResult result);
        public void OnProductStatusChanged(WorkOrder key, int progress);
        public bool HasPendingNotifications { get; }
    }
    public class ProductionResult
    {
        private ProductionResult()
        {
        }

        public static ProductionResult Success(object value)
        {
            return new ProductionResult
            {
                Value = value
            };
        }

        public static ProductionResult Error(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }
            return new ProductionResult { Exception = exception };
        }

        public object Value { get; private set; }
        public Exception Exception { get; private set; }
    }

    public class ProductionMonitor
    {
        private Action<int> _progressChange;
        public ProductionMonitor(CancellationToken cancellationToken, Action<int> progressChange)
        {
            CancellationToken = cancellationToken;
            _progressChange = progressChange;
        }
        public CancellationToken CancellationToken { get; }

        public void SetProgress(int progress)
        {
            _progressChange?.Invoke(progress);
        }
    }
}
