/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Spectra.Alignment
{
    /// <summary>
    /// A spectrum with constant spacing between the m/z values.
    /// This spectrum behaves as if the intensity is a particular value at the center
    /// of a bin, and then varies linearly to the center of the bins on either size.
    /// </summary>
    public sealed class BinnedSpectrum : IReadOnlyList<KeyValuePair<double, double>>
    {
        public BinnedSpectrum(double scanWindowLower, double scanWindowUpper, IEnumerable<double> intensities)
        {
            ScanWindowLower = scanWindowLower;
            ScanWindowUpper = scanWindowUpper;
            Intensities = ImmutableList.ValueOf(intensities);
        }

        public double ScanWindowLower { get; }
        public double ScanWindowUpper { get; }
        public ImmutableList<double> Intensities { get; }
        public int Count
        {
            get { return Intensities.Count; }
        }

        public IList<double> Mzs
        {
            get
            {
                return ReadOnlyList.Create(Count, GetMzOfBin);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<double, double>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(GetKeyValuePairAt).GetEnumerator();
        }

        public KeyValuePair<double, double> this[int index]
        {
            get { return new KeyValuePair<double, double>(GetMzOfBin(index), Intensities[index]); }
        }

        /// <summary>
        /// Returns the m/z of the center of a particular bin.
        /// </summary>
        private double GetMzOfBin(int index)
        {
            return GetMzOfBin(index, ScanWindowLower, ScanWindowUpper, Count);
        }

        private static double GetMzOfBin(int index, double scanWindowLower, double scanWindowUpper, int binCount)
        {
            return scanWindowLower + (scanWindowUpper - scanWindowLower) * (index + .5) / binCount;
        }

        private KeyValuePair<double, double> GetKeyValuePairAt(int index)
        {
            return new KeyValuePair<double, double>(GetMzOfBin(index), Intensities[index]);
        }

        public static BinnedSpectrum BinSpectrum(int binCount, double scanWindowLower, double scanWindowUpper, IEnumerable<KeyValuePair<double, double>> mzIntensities)
        {
            var binnedIntensities = new double[binCount];
            double scanWindowWidth = scanWindowUpper - scanWindowLower;
            double binWidth = scanWindowWidth / binCount;
            foreach (var mzIntensity in mzIntensities)
            {
                double mz = mzIntensity.Key;
                int binIndex = (int)((mz - scanWindowLower) * binCount / scanWindowWidth);
                if (binIndex < 0 || binIndex >= binnedIntensities.Length)
                {
                    continue;
                }

                double binMz = GetMzOfBin(binIndex, scanWindowLower, scanWindowUpper, binCount);
                if (mz < binMz)
                {
                    double lowerWeight = (binMz - mz) / binWidth;
                    if (binIndex > 0)
                    {
                        binnedIntensities[binIndex - 1] += mzIntensity.Value * lowerWeight;
                    }

                    binnedIntensities[binIndex] += mzIntensity.Value * (1 - lowerWeight);
                }
                else if (mz > binMz)
                {
                    double upperWeight = (mz - binMz) / binWidth;
                    if (binIndex < binnedIntensities.Length - 1)
                    {
                        binnedIntensities[binIndex + 1] += mzIntensity.Value * upperWeight;
                    }

                    binnedIntensities[binIndex] += mzIntensity.Value * (1 - upperWeight);
                }
                else
                {
                    binnedIntensities[binIndex] += mzIntensity.Value;
                }
            }
            return new BinnedSpectrum(scanWindowLower, scanWindowUpper, binnedIntensities);
        }

        private bool Equals(BinnedSpectrum other)
        {
            return ScanWindowLower.Equals(other.ScanWindowLower) && ScanWindowUpper.Equals(other.ScanWindowUpper) &&
                   Equals(Intensities, other.Intensities);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is BinnedSpectrum other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ScanWindowLower.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanWindowUpper.GetHashCode();
                hashCode = (hashCode * 397) ^ Intensities.GetHashCode();
                return hashCode;
            }
        }
    }
}
