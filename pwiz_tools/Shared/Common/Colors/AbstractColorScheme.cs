using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace pwiz.Common.Colors
{
    public abstract class AbstractColorScheme<T> : IColorScheme
    {
        public void Calibrate(IEnumerable values)
        {
            Calibrate(ConvertValues(values));
        }

        public Color? GetColor(object value)
        {
            if (TryConvertValue(value, out T convertedValue))
            {
                return GetColor(convertedValue);
            }

            return null;
        }

        public virtual void Calibrate(IEnumerable<T> values) {}

        public abstract Color? GetColor(T value);

        protected virtual bool TryConvertValue(object value, out T convertedValue)
        {
            if (value is T)
            {
                convertedValue = (T) value;
                return true;
            }

            convertedValue = default(T);
            return false;
        }

        protected IEnumerable<T> ConvertValues(IEnumerable values)
        {
            foreach (var value in values)
            {
                if (TryConvertValue(value, out T convertedValue))
                {
                    yield return convertedValue;
                }
            }
        }
    }
}
