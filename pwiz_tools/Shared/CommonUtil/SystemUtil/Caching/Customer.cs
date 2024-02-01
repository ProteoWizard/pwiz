using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.Caching
{
    public interface ICustomer
    {
        public void OnProductAvailable(WorkOrder key, ProductionResult result);
        public void OnProductStatusChanged(WorkOrder key, int progress);
        public bool HasPendingNotifications { get; }
    }
    
    
    public class Customer : ICustomer, IDisposable
    {
        private bool _notificationPending;
        public static Customer<TParam, TResult> OfProducer<TParam, TResult>(
            Control owner, Producer<TParam, TResult> factory)
        {
            return new Customer<TParam, TResult>(ProductionFacility.INSTANCE, owner,
                factory);
        }
        
        
        protected WorkOrder _workOrder;
        public Customer(ProductionFacility cache, Control ownerControl, Producer factory)
        {
            Cache = cache;
            OwnerControl = ownerControl;
            Factory = factory;
            OwnerControl.HandleDestroyed += OwnerControlHandleDestroyed;
        }

        public ProductionFacility Cache
        {
            get;
        }
        
        public Control OwnerControl { get; private set; }
        
        public Producer Factory { get; }

        public void OnProductAvailable(WorkOrder key, ProductionResult result)
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

        public void OnProductStatusChanged(WorkOrder key, int progress)
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
                var newResultSpec = new WorkOrder(Factory, argument);
                if (!Equals(newResultSpec, _workOrder))
                {
                    Cache.Listen(newResultSpec, this);
                    if (_workOrder != null)
                    {
                        Cache.Unlisten(_workOrder, this);
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
                Cache.Unlisten(_workOrder, this);
                _workOrder = null;
                if (OwnerControl != null)
                {
                    Trace.TraceInformation("CalculatedValueListener destroyed: {0}", Factory.ValueType);
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

        public bool HasPendingNotifications
        {
            get { return OwnerControl != null && _notificationPending; }
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
            if (_workOrder == null || !base.TryGetValue(_workOrder.Parameter, out var resultObject))
            {
                resultValue = default;
                return false;
            }

            resultValue = (TResult)resultObject;
            return true;
        }
    }
}
