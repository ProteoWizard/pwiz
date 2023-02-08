﻿/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Chemistry
{
    public class MzTolerance : IAuditLogObject
    {
        public enum Units { mz, ppm }

        public double Value { get; private set; }
        public Units Unit { get; private set; }

        public MzTolerance(double value = 0, Units units = Units.mz)
        {
            Value = value;
            Unit = units;
        }

        public static implicit operator MzTolerance(double tolerance)
        {
            return new MzTolerance(tolerance);
        }
        public static double operator +(double d, MzTolerance tolerance)
        {
            switch (tolerance.Unit)
            {
                case Units.mz:
                    return d + tolerance.Value;
                case Units.ppm:
                    return d + Math.Abs(d) * tolerance.Value * 1e-6;
            }

            return 0;
        }

        public static double operator -(double d, MzTolerance tolerance)
        {
            switch (tolerance.Unit)
            {
                case Units.mz:
                    return d - tolerance.Value;
                case Units.ppm:
                    return d - Math.Abs(d) * tolerance.Value * 1e-6;
            }

            return 0;
        }

        public double GetMzTolerance(double mz)
        {
            switch (Unit)
            {
                case Units.mz:
                    return Value;
                case Units.ppm:
                    return Math.Abs(mz) * Value * 1e-6;
            }

            return 0;

        }

        /// <summary>returns true iff a is in (b-tolerance, b+tolerance)</summary>
        public bool IsWithinTolerance(double a, double b)
        {
            return (a >= b - this) && (a <= b + this);
        }

        /// <summary>returns true iff b - a is greater than the value in tolerance (useful for matching sorted mass lists)</summary>
        public bool LessThanTolerance(double a, double b)
        {
            return (a < b - this);
        }

        public string UnitName => Enum.GetName(typeof(Units), Unit);
        public string UnitText => Resources.ResourceManager.GetString(nameof(Units) + "_" + UnitName);

        public override string ToString()
        {
            return $"{Value} {UnitText}";
        }

        public string AuditLogText => $"\"{Value}\" {UnitText}";
        public bool IsName => false;

        protected bool Equals(MzTolerance other)
        {
            return Value.Equals(other.Value) && Unit == other.Unit;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MzTolerance)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Value.GetHashCode() * 397) ^ (int)Unit;
            }
        }
    }
}
