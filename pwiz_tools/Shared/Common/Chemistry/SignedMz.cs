/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

namespace pwiz.Common.Chemistry
{
    /// <summary>
    /// We need a way to distinguish chromatograms for negative ion modes from those for positive.
    /// The idea of m/z is inherently "positive", in the sense of sorting etc, so we carry around 
    /// the m/z value, and a sign flag, and when we sort it's by sign then by (normally positive) m/z.  
    /// The m/z value *could* be negative as a result of an arithmetic operator, that has a special
    /// meaning for comparisons but doesn't happen in normal use.
    /// There's a lot of operator magic here to minimize code changes where we used to implement mz 
    /// values as simple doubles.
    /// </summary>
    public struct SignedMz : IComparable, IEquatable<SignedMz>, IFormattable
    {
        private readonly double? _mz;
        private readonly bool _isNegative;

        public SignedMz(double? mz, bool isNegative)
        {
            _mz = mz;
            _isNegative = isNegative;
        }

        public SignedMz(double? mz)  // For deserialization - a negative value is taken to mean negative polarity
        {
            _isNegative = (mz??0) < 0;
            _mz = mz.HasValue ? Math.Abs(mz.Value) : (double?) null;
        }

        public static readonly SignedMz EMPTY = new SignedMz(null, false);
        public static readonly SignedMz ZERO = new SignedMz(0);

        /// <summary>
        /// Returns the mz value, which is normally a positive number even for negative ion mode
        /// (Check the IsNegative flag to know the ion mode, or use RawValue for a value that is negative if ion is negative)
        /// </summary>
        public double Value
        {
            get { return _mz.Value; }
        }

        public double GetValueOrDefault()
        {
            return HasValue? Value : 0.0;
        }

        /// <summary>
        /// For serialization etc - returns a negative number if IsNegative is true 
        /// </summary>
        public double? RawValue
        {
            get { return _mz.HasValue ? (_isNegative ? -_mz.Value : _mz.Value) : _mz;  }
        }

        public bool IsNegative
        {
            get { return _isNegative; }
        }

        public bool HasValue
        {
            get { return _mz.HasValue; }
        }

        public static implicit operator double(SignedMz mz)
        {
            return mz.Value;
        }

        public static implicit operator double?(SignedMz mz)
        {
            return mz._mz;
        }

        public static SignedMz operator +(SignedMz mz, double step)
        {
            return new SignedMz(mz.Value + step, mz.IsNegative);
        }

        public static SignedMz operator -(SignedMz mz, double step)
        {
            return new SignedMz(mz.Value - step, mz.IsNegative);
        }

        public static SignedMz operator +(SignedMz mz, SignedMz step)
        {
            if (mz.IsNegative != step.IsNegative)
                throw new InvalidOperationException("polarity mismatch"); // Not L10N
            return new SignedMz(mz.Value + step.Value, mz.IsNegative);
        }

        public static SignedMz operator -(SignedMz mz, SignedMz step)
        {
            if (mz.IsNegative != step.IsNegative)
                throw new InvalidOperationException("polarity mismatch"); // Not L10N
            return new SignedMz(mz.Value - step.Value, mz.IsNegative);
        }

        public static bool operator <(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) < 0;
        }

        public static bool operator <=(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) <= 0;
        }

        public static bool operator >=(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) >= 0;
        }

        public static bool operator >(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) > 0;
        }

        public static bool operator ==(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) == 0;
        }

        public static bool operator !=(SignedMz mzA, SignedMz mzB)
        {
            return !(mzA == mzB);
        }

        public bool Equals(SignedMz other)
        {
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SignedMz && Equals((SignedMz)obj);
        }

        public override int GetHashCode()
        {
            return RawValue.GetHashCode();
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return Value.ToString(format, formatProvider);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return -1;
            if (obj.GetType() != GetType()) return -1;
            return CompareTo((SignedMz)obj);
        }

        public int CompareTo(SignedMz other)
        {
            if (_mz.HasValue != other.HasValue)
            {
                return _mz.HasValue ? 1 : -1;
            }
            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1;
            }
            // Same sign
            if (_mz.HasValue)
                return Value.CompareTo(other.Value);
            return 0; // Both empty
        }

        public int CompareTolerant(SignedMz other, double tolerance)
        {
            if (_mz.HasValue != other.HasValue)
            {
                return _mz.HasValue ? 1 : -1;
            }
            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1; // Not interested in tolerance when signs disagree 
            }
            // Same sign
            if (Math.Abs(Value - other.Value) <= tolerance)
                return 0;
            return Value.CompareTo(other.Value);
        }

        public SignedMz ChangeMz(double mz)
        {
            return new SignedMz(mz, IsNegative);  // New mz, same polarity
        }

        public override string ToString()
        {
            return RawValue.ToString();
        }
    }
}