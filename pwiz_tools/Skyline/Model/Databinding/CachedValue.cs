using System;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Holder of a value which needs to be recalculated whenever the document changes.
    /// The recalculate function gets called whenever the SrmDocument in the DataSchema
    /// is different.
    /// If the recalculate function returns null, then the CachedValue retains the previous
    /// value.
    /// </summary>
    public class CachedValue<T>
    {
        private readonly SkylineDataSchema _dataSchema;
        private readonly Func<T> _getterFunc;
        private object _documentReference;
        private T _value;
        public CachedValue(SkylineDataSchema dataSchema, Func<T> getterFunc)
        {
            _dataSchema = dataSchema;
            _getterFunc = getterFunc;
        }

        public T Value
        {
            get
            {
                return GetValue();
            }
        }

        public T GetValue()
        {
            if (!ReferenceEquals(_documentReference, _dataSchema.Document.ReferenceId))
            {
                var newValue = _getterFunc();
                if (!ReferenceEquals(null, newValue))
                {
                    _value = newValue;
                }
                _documentReference = _dataSchema.Document.ReferenceId;
            }
            return _value;
        }
    }
    /// <summary>
    /// Factory methods for <see cref="CachedValue{T}"/>.
    /// </summary>
    public static class CachedValue
    {
        public static CachedValue<T> Create<T>(SkylineDataSchema dataSchema, Func<T> getterFunc)
        {
            return new CachedValue<T>(dataSchema, getterFunc);
        }
    }

    public abstract class CachedValue<TOwner, TValue>
    {
        private ushort _flags;
        private object _documentReferenceId;
        private TValue _value;

        private ushort GetFlagMask(int index)
        {
            if (index < 0 || index >= 16)
            {
                throw new ArgumentOutOfRangeException();
            }

            return (ushort)(1u << index);
        }
        private bool GetFlag(int index)
        {
            return 0 == (_flags & GetFlagMask(index));
        }

        private void SetFlag(int index, bool value)
        {
            if (value)
            {
                _flags |= GetFlagMask(index);
            }
            else
            {
                _flags &= (ushort)~GetFlagMask(index);
            }
        }

        public TValue GetValue(TOwner owner)
        {
            return GetOrCalculate(owner, 0, CalculateValue, ref _value);
        }

        protected abstract SrmDocument GetDocument(TOwner owner);
        protected abstract TValue CalculateValue(TOwner owner);

        protected TValueX GetOrCalculate<TValueX>(TOwner owner, int valueIndex, Func<TOwner, TValueX> calculateFunc,
            ref TValueX backingField)
        {
            var document = GetDocument(owner);
            if (!ReferenceEquals(document.ReferenceId, _documentReferenceId))
            {
                _flags = 0;
                _documentReferenceId = document.ReferenceId;
            }

            if (!GetFlag(valueIndex))
            {
                // Value is stale. Calculate it again.
                TValueX calculatedValue = calculateFunc(owner);
                // Update the value in the backing field, unless the new value is null.
                // If the new value is null, then we keep the last value that was calculated.
                if (!ReferenceEquals(calculatedValue, null))
                {
                    backingField = calculatedValue;
                }
            }
            return backingField;
        }
    }

    public abstract class CachedValues<TOwner, TValue, TValue1> : CachedValue<TOwner, TValue>
    {
        private TValue1 _value1;

        public TValue1 GetValue1(TOwner owner)
        {
            return GetOrCalculate(owner, 1, CalculateValue1, ref _value1);
        }

        protected abstract TValue1 CalculateValue1(TOwner owner);
    }

    public abstract class CachedValues<TOwner, TValue, TValue1, TValue2> : CachedValues<TOwner, TValue, TValue1>
    {
        private TValue2 _value2;

        public TValue2 GetValue2(TOwner owner)
        {
            return GetOrCalculate(owner, 2, CalculateValue2, ref _value2);
        }

        protected abstract TValue2 CalculateValue2(TOwner owner);
    }
}
