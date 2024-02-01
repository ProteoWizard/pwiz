using System;

namespace pwiz.Common.SystemUtil.Caching
{
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
}
