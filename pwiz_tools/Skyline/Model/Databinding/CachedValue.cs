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
}
