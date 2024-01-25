using System.Collections.Generic;

namespace pwiz.Common.SystemUtil
{
    public static class HashedObject
    {
        public static HashedObject<T> ValueOf<T>(T t)
        {
            if (ReferenceEquals(t, null))
            {
                return null;
            }
            return new HashedObject<T>(t);
        }
    }

    public sealed class HashedObject<T>
    {
        private int _hashCode;

        public HashedObject(T value)
        {
            _hashCode = value.GetHashCode();
            Value = value;
        }

        public T Value { get; }

        private bool Equals(HashedObject<T> other)
        {
            return _hashCode == other._hashCode && EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is HashedObject<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
    }
}
