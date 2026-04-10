// Machine Learning math utilities for Osprey
// Port of osprey-ml/src/lib.rs utility functions

using System;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Shared math utilities for the ML module.
    /// </summary>
    public static class MlMath
    {
        /// <summary>
        /// L2 norm of a vector.
        /// </summary>
        public static double Norm(double[] values)
        {
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i] * values[i];
            return Math.Sqrt(sum);
        }

        /// <summary>
        /// Mean of a vector.
        /// </summary>
        public static double Mean(double[] values)
        {
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];
            return sum / values.Length;
        }

        /// <summary>
        /// Population standard deviation of a vector.
        /// </summary>
        public static double Std(double[] values)
        {
            double mean = Mean(values);
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
            {
                double diff = values[i] - mean;
                sum += diff * diff;
            }
            return Math.Sqrt(sum / values.Length);
        }

        /// <summary>
        /// Check if two arrays are element-wise close within epsilon.
        /// </summary>
        public static bool AllClose(double[] lhs, double[] rhs, double eps)
        {
            if (lhs.Length != rhs.Length)
                return false;
            for (int i = 0; i < lhs.Length; i++)
            {
                if (Math.Abs(lhs[i] - rhs[i]) > eps)
                    return false;
            }
            return true;
        }
    }
}
