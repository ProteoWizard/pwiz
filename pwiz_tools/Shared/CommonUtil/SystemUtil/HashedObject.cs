/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

    /// <summary>
    /// Wraps an object that might have a GetHashCode() method that is expensive
    /// to calculate.
    /// </summary>
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
