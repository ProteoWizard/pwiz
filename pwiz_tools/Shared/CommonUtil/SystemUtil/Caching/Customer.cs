using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.Caching
{
    public class Customer : IDisposable
    {
        private bool _notificationPending;
        private WorkOrder _workOrder;
        private readonly IProductionListener _listener;
        public Customer(ProductionFacility cache, Control ownerControl, Producer factory)
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
            var resultsAvailable = ProductAvailable;
            if (resultsAvailable != null)
            {
                if (!_notificationPending)
                {
                    _notificationPending = true;
                    CommonActionUtil.SafeBeginInvoke(OwnerControl, () =>
                    {
                        _notificationPending = false;
                        resultsAvailable();
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

        public bool TryGetValue(object argument, out object resultValue)
        {
            lock (this)
            {
                var newResultSpec = new WorkOrder(Producer, argument);
                if (!Equals(newResultSpec, _workOrder))
                {
                    Cache.Listen(newResultSpec, _listener);
                    if (_workOrder != null)
                    {
                        Cache.Unlisten(_workOrder, _listener);
                    }

                    _workOrder = newResultSpec;
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
                    Trace.TraceInformation("CalculatedValueListener destroyed: {0}", Producer.ValueType);
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
            public Listener(Customer customer)
            {
                Customer = customer;
            }
            public Customer Customer { get; }
            public void OnProductAvailable(WorkOrder key, ProductionResult result)
            {
                Customer.OnProductAvailable();
            }

            public void OnProductStatusChanged(WorkOrder key, int progress)
            {
                Customer.OnProductStatusChanged();
            }

            public bool HasPendingNotifications
            {
                get
                {
                    return Customer.OwnerControl != null && Customer._notificationPending;
                }
            }
        }

    }

    public class Customer<TParam, TResult> : Customer
    {
        public Customer(ProductionFacility cache, Control ownerControl,
            Producer<TParam, TResult> factory) : base(cache, ownerControl, factory)
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

        public bool TryGetCurrentValue(out TResult resultValue)
        {
            if (CurrentWorkOrder == null || !base.TryGetValue(CurrentWorkOrder.Parameter, out var resultObject))
            {
                resultValue = default;
                return false;
            }

            resultValue = (TResult)resultObject;
            return true;
        }
    }
}
