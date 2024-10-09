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
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.Caching
{
    /// <summary>
    /// Keeps track of the progress of a request from a <see cref="Producer"/> and sends
    /// out notification when the work is completed.
    /// </summary>
    public class Receiver : IDisposable
    {
        private bool _notificationPending;
        private WorkOrder _workOrder;
        private readonly IProductionListener _listener;
        public Receiver(ProductionFacility cache, Control ownerControl, Producer factory)
        {
            Cache = cache;
            OwnerControl = ownerControl;
            Producer = factory;
            OwnerControl.HandleDestroyed += OwnerControlHandleDestroyed;
            _listener = new Listener(this);
        }

        public ProductionFacility Cache
        {
            get;
        }
        
        public Control OwnerControl { get; private set; }
        
        public Producer Producer { get; }

        private void OnProductAvailable()
        {
            var productAvailable = ProductAvailable;
            if (productAvailable != null)
            {
                if (!_notificationPending)
                {
                    _notificationPending = true;
                    CommonActionUtil.SafeBeginInvoke(OwnerControl, () =>
                    {
                        _notificationPending = false;
                        productAvailable();
                    });
                }
            }
        }

        private void OnProductStatusChanged()
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

        public event Action ProductAvailable;
        public event Action ProgressChange;

        public int GetProgressValue()
        {
            if (_workOrder != null)
            {
                return Cache.GetProgressValue(_workOrder);
            }

            return 0;
        }

        public Exception GetError()
        {
            if (_workOrder != null)
            {
                return Cache.GetResult(_workOrder)?.Exception;
            }

            return null;
        }

        public bool TryGetProduct(WorkOrder newWorkOrder, out object resultValue)
        {
            lock (this)
            {
                if (!Equals(newWorkOrder, _workOrder))
                {
                    Cache.Listen(newWorkOrder, _listener);
                    if (_workOrder != null)
                    {
                        Cache.Unlisten(_workOrder, _listener);
                    }

                    _workOrder = newWorkOrder;
                }

                var result = Cache.GetResult(_workOrder);
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
                Cache.Unlisten(_workOrder, _listener);
                _workOrder = null;
                if (OwnerControl != null)
                {
                    Messages.WriteAsyncDebugMessage("CalculatedValueListener destroyed: {0}", Producer.ValueType); // N.B. see TraceWarningListener for output details
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
            var resultSpec = _workOrder;
            return resultSpec != null && null == Cache.GetResult(resultSpec);
        }

        public WorkOrder CurrentWorkOrder
        {
            get { return _workOrder; }
        }

        private class Listener : IProductionListener
        {
            public Listener(Receiver receiver)
            {
                Receiver = receiver;
            }
            public Receiver Receiver { get; }
            public void OnProductAvailable(WorkOrder key, ProductionResult result)
            {
                Receiver.OnProductAvailable();
            }

            public void OnProductStatusChanged(WorkOrder key, int progress)
            {
                Receiver.OnProductStatusChanged();
            }

            public bool HasPendingNotifications
            {
                get
                {
                    return Receiver.OwnerControl != null && Receiver._notificationPending;
                }
            }
        }

    }

    public class Receiver<TParam, TResult> : Receiver
    {
        public Receiver(ProductionFacility cache, Control ownerControl,
            Producer<TParam, TResult> factory) : base(cache, ownerControl, factory)
        {
        }
        
        public new Producer<TParam, TResult> Producer
        {
            get { return (Producer<TParam, TResult>)base.Producer; }
        }

        public bool TryGetProduct(TParam workParameter, out TResult resultValue)
        {
            if (TryGetProduct(Producer.MakeWorkOrder(workParameter), out var resultObject))
            {
                resultValue = (TResult) resultObject;
                return true;
            }

            resultValue = default;
            return false;
        }

        public bool TryGetCurrentProduct(out TResult resultValue)
        {
            if (CurrentWorkOrder == null || !TryGetProduct(CurrentWorkOrder, out var resultObject))
            {
                resultValue = default;
                return false;
            }

            resultValue = (TResult)resultObject;
            return true;
        }
    }
}
