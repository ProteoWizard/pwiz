using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using pwiz.Common.Collections;

namespace pwiz.Common.Storage
{
    public struct ColumnData
    {
        private IEnumerable _data;

        public static ColumnData Constant<T>(T value)
        {
            return new ColumnData(new ConstantValue<T>(value));
        }
        public ColumnData(IEnumerable list)
        {
            _data = list;
        }

        public bool IsEmpty
        {
            get { return _data == null; }
        }

        public bool IsConstant<T>()
        {
            return _data is ConstantValue<T>;
        }

        public T GetValueAt<T>(int index)
        {
            if (_data == null)
            {
                return default;
            }

            if (_data is ConstantValue<T> constant)
            {
                return constant.Value;
            }

            var readOnlyList = (IReadOnlyList<T>)_data;
            if (index < 0 || index >= readOnlyList.Count)
            {
                return default;
            }
            return readOnlyList[index];
        }

        public ImmutableList<T> ToImmutableList<T>()
        {
            return ImmutableList.ValueOf(_data as IReadOnlyList<T>);
        }

        private class ConstantValue<T> : IEnumerable
        {
            public ConstantValue(T value)
            {
                Value = value;
            }
            public T Value { get; private set; }
            public IEnumerator GetEnumerator()
            {
                yield break;
            }

            protected bool Equals(ConstantValue<T> other)
            {
                return EqualityComparer<T>.Default.Equals(Value, other.Value);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ConstantValue<T>)obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<T>.Default.GetHashCode(Value);
            }
        }

        public bool Equals(ColumnData other)
        {
            return Equals(_data, other._data);
        }

        public override bool Equals(object obj)
        {
            return obj is ColumnData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_data != null ? _data.GetHashCode() : 0);
        }
    }
}
