/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

    /// <summary>
    /// Holds a value which needs to be recalculated if the Document changes.
    /// Derived classes might hold additional values that need to be recalculated at the same time.
    /// </summary>
    /// <typeparam name="TOwner">The object that the value(s) can be calculated from. In order to save memory, this
    /// owner object is not held onto by this object, but must be passed in whenever a value is requested.</typeparam>
    /// <typeparam name="TValue">The Type of the first calculated value which is held in this object.</typeparam>
    public abstract class CachedValue<TOwner, TValue>
    {
        /// <summary>
        /// The <see cref="SrmDocument.ReferenceId"/> of the SrmDocument when the values in this object were last calculated.
        /// Whenever a value is requested from this object, the Document Reference Id is compared against that of the current
        /// document to see whether all of the values need to be recalculated.
        /// </summary>
        private object _documentReferenceId;
        /// <summary>
        /// A bitfield indicating which values have been calculated since the last time that the Document Reference Id was changed.
        /// </summary>
        private ushort _flags;
        /// <summary>
        /// The first value which is stored in this object.
        /// </summary>
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
            return 0 != (_flags & GetFlagMask(index));
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

        /// <summary>
        /// Returns the first calculated value which is stored in this object.
        /// </summary>
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
            var documentReferenceId = document.ReferenceId;
            if (!ReferenceEquals(documentReferenceId, _documentReferenceId))
            {
                _flags = 0;
                _documentReferenceId = documentReferenceId;
            }
            if (GetFlag(valueIndex))
            {
                return backingField;
            }

            TValueX calculatedValue = calculateFunc(owner);
            // Update the value in the backing field, unless the new value is null.
            // If the new value is null, then we keep the last value that was calculated.
            if (!ReferenceEquals(calculatedValue, null))
            {
                backingField = calculatedValue;
            }
            SetFlag(valueIndex, true);
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
