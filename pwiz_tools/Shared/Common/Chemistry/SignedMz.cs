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
using System.Globalization;

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
        private readonly double _mz;

        public SignedMz(double mz, bool isNegative)
        {
            bool isNegativeMz = mz < 0;
            _mz = isNegative != isNegativeMz ? -mz : mz;
        }

        public SignedMz(double mz)  // For deserialization - a negative value is taken to mean negative polarity
        {
            _mz = mz;
        }

        public static readonly SignedMz ZERO = new SignedMz(0);

        /// <summary>
        /// Returns the mz value, which is normally a positive number even for negative ion mode
        /// (Check the IsNegative flag to know the ion mode, or use RawValue for a value that is negative if ion is negative)
        /// </summary>
        public double Value
        {
            get { return Math.Abs(_mz); }
        }

        /// <summary>
        /// For serialization etc - returns a negative number if IsNegative is true 
        /// </summary>
        public double RawValue
        {
            get { return _mz;  }
        }

        public bool IsNegative
        {
            get { return _mz < 0; }
        }

        public static implicit operator double(SignedMz mz)
        {
            return mz.Value;
        }

        public static SignedMz operator +(SignedMz mz, double step)
        {
            return new SignedMz(mz.Value + step, mz.IsNegative);
        }

        public static SignedMz operator +(SignedMz mz, SignedMz step)
        {
            // Extra care necessary to deal with zero correctly
            if (mz.IsNegative != step.IsNegative && mz.Value != 0 && step.Value != 0)
                throw new InvalidOperationException(@"polarity mismatch");
            return new SignedMz(mz.Value + step.Value, mz.IsNegative || step.IsNegative);
        }

        /// <summary>
        /// Subtracts from the positive value of a <see cref="SignedMz"/> always producing
        /// a new <see cref="SignedMz"/> of the same sign with the absolute value of the difference.
        /// </summary>
        public static SignedMz operator -(SignedMz mz, double step)
        {
            return new SignedMz(mz.Value - step, mz.IsNegative);
        }

        /// <summary>
        /// Subtracts the positive value of a <see cref="SignedMz"/> from another <see cref="SignedMz"/>
        /// always producing  a new <see cref="SignedMz"/> of the same sign as the two operands with
        /// the absolute value of the difference as its value.
        /// </summary>
        public static SignedMz operator -(SignedMz mz, SignedMz step)
        {
            // Extra care necessary to deal with zero correctly
            if (mz.IsNegative != step.IsNegative && mz.Value != 0 && step.Value != 0)
                throw new InvalidOperationException(@"polarity mismatch");
            return new SignedMz(mz.Value - step.Value, mz.IsNegative || step.IsNegative);
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
            return _mz.GetHashCode();
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return Value.ToString(format, formatProvider);
        }

        public int CompareTo(object obj)
        {
            return CompareTo((SignedMz)obj);
        }

        public int CompareTo(SignedMz other)
        {
            if (_mz >= 0 && other._mz >= 0)
                return _mz.CompareTo(other._mz);
            if (_mz < 0 && other._mz < 0)
                return Value.CompareTo(other.Value);
            return _mz < 0 ? -1 : 1;
        }

        public int CompareTolerant(SignedMz other, double tolerance)
        {
            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1; // Not interested in tolerance when signs disagree 
            }
            // Same sign
            if (Math.Abs(Value - other.Value) <= tolerance)
                return 0;
            return CompareTo(other);
        }

        public SignedMz ChangeMz(double mz)
        {
            return new SignedMz(mz, IsNegative);  // New mz, same polarity
        }

        public override string ToString()
        {
            return _mz.ToString(CultureInfo.InvariantCulture);
        }
    }
}
