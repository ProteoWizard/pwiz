using System;

namespace pwiz.Common.SystemUtil.Caching
{
    public class CalculatorResult
    {
        private CalculatorResult()
        {
        }

        public static CalculatorResult Success(object value)
        {
            return new CalculatorResult
            {
                Value = value
            };
        }

        public static CalculatorResult Error(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }
            return new CalculatorResult { Exception = exception };
        }

        public object Value { get; private set; }
        public Exception Exception { get; private set; }
    }
}
