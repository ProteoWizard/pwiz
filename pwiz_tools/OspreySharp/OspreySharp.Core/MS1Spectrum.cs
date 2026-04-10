using System;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// An MS1 full-scan spectrum. Maps to osprey-core/src/types.rs MS1Spectrum.
    /// </summary>
    public class MS1Spectrum
    {
        public uint ScanNumber { get; set; }
        public double RetentionTime { get; set; }
        public double[] Mzs { get; set; }
        public float[] Intensities { get; set; }

        public int Count { get { return Mzs.Length; } }
        public bool IsEmpty { get { return Count == 0; } }

        /// <summary>
        /// Finds the most intense peak within a ppm tolerance of the target m/z using binary search.
        /// Returns null if no peak is found.
        /// </summary>
        public MzIntensityPair? FindPeakPpm(double targetMz, double tolerancePpm)
        {
            double toleranceDa = targetMz * tolerancePpm / 1e6;
            double lowerMz = targetMz - toleranceDa;
            double upperMz = targetMz + toleranceDa;

            int lo = LowerBound(Mzs, lowerMz);
            if (lo >= Mzs.Length || Mzs[lo] > upperMz)
                return null;

            float bestIntensity = float.MinValue;
            double bestMz = 0;
            bool found = false;

            for (int i = lo; i < Mzs.Length && Mzs[i] <= upperMz; i++)
            {
                if (Intensities[i] > bestIntensity)
                {
                    bestIntensity = Intensities[i];
                    bestMz = Mzs[i];
                    found = true;
                }
            }

            if (!found)
                return null;

            return new MzIntensityPair(bestMz, bestIntensity);
        }

        /// <summary>
        /// Binary search for the first index where Mzs[index] >= value.
        /// </summary>
        private static int LowerBound(double[] array, double value)
        {
            int lo = 0;
            int hi = array.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (array[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }

    /// <summary>
    /// A paired m/z and intensity value returned from peak searches.
    /// </summary>
    public struct MzIntensityPair
    {
        public double Mz { get; private set; }
        public float Intensity { get; private set; }

        public MzIntensityPair(double mz, float intensity)
        {
            Mz = mz;
            Intensity = intensity;
        }
    }
}
