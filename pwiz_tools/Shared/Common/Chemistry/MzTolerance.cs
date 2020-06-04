
using System;

namespace pwiz.Common.Chemistry
{
    public class MzTolerance
    {
        public enum Units { mz, ppm };

        public double Value { get; private set; }
        public Units Unit { get; private set; }

        public MzTolerance(double value = 0, Units units = Units.mz)
        {
            Value = value;
            Unit = units;
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

        /// <summary>returns true iff a is in (b-tolerance, b+tolerance)</summary>
        public static bool IsWithinTolerance(double a, double b, MzTolerance tolerance)
        {
            return (a > b - tolerance) && (a < b + tolerance);
        }

        /// <summary>returns true iff b - a is greater than the value in tolerance (useful for matching sorted mass lists)</summary>
        public static bool LessThanTolerance(double a, double b, MzTolerance tolerance)
        {
            return (a < b - tolerance);
        }
    };
}
