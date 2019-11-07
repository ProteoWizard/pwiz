/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.Model.Results
{
    public class FastOverlapDemultiplexer : IDemultiplexer
    {
        MsDataFileImpl _dataFile;
        MassTolerance _fragmentTolerance;
        private Dictionary<int, IList<MsPrecursor>> _precursorsByScanIndex = new Dictionary<int, IList<MsPrecursor>>();

        public FastOverlapDemultiplexer(MsDataFileImpl demuxProcessedFile)
        {
            _dataFile = demuxProcessedFile;
            _dataFile.EnableCaching(200);
            _fragmentTolerance = new MassTolerance(10);
        }

        /// <summary>
        /// Given an original spectrum, return two spectra each with an isolation window that is half of the original spectrum.
        /// </summary>
        public MsDataSpectrum[] GetDeconvolvedSpectra(int index, MsDataSpectrum originalSpectrum)
        {
            if (originalSpectrum.Precursors.Count != 1)
            {
                return new[] {originalSpectrum};
            }
            MsPrecursor originalPrecursor = originalSpectrum.Precursors.First();
            double? lowerMzNullable = GetLowerMz(originalPrecursor);
            double? upperMzNullable = GetUpperMz(originalPrecursor);
            if (!lowerMzNullable.HasValue || !upperMzNullable.HasValue)
            {
                return new[] {originalSpectrum};
            }
            double lowerMz = lowerMzNullable.Value;
            double upperMz = upperMzNullable.Value;

            MsDataSpectrum earlyLow = FindOverlappingSpectrum(lowerMz, Enumerable.Range(1, index - 1).Select(i => index - i), originalPrecursor);
            MsDataSpectrum earlyHigh = FindOverlappingSpectrum(upperMz, Enumerable.Range(1, index - 1).Select(i => index - i), originalPrecursor);
            MsDataSpectrum lateLow = FindOverlappingSpectrum(lowerMz,
                Enumerable.Range(index + 1, _dataFile.SpectrumCount - index - 1), originalPrecursor);
            MsDataSpectrum lateHigh = FindOverlappingSpectrum(upperMz,
                Enumerable.Range(index + 1, _dataFile.SpectrumCount - index - 1), originalPrecursor);
            double? middlePrecursorMz = GetMiddlePrecursorMz(new[] {earlyLow, lateLow}, new[] {earlyHigh, lateHigh});
            if (!middlePrecursorMz.HasValue)
            {
                return new[] {originalSpectrum};
            }
            double[] mzs = originalSpectrum.Mzs;
            List<KeyValuePair<double, double>> lowerPeaks = new List<KeyValuePair<double, double>>();
            List<KeyValuePair<double, double>> upperPeaks = new List<KeyValuePair<double, double>>();
            // ReSharper disable CollectionNeverQueried.Local
            List<KeyValuePair<double, double>> removedPeaks = new List<KeyValuePair<double, double>>();
            // ReSharper restore CollectionNeverQueried.Local
            for (int i = 0; i < mzs.Length; i++)
            {
                double mz = mzs[i];
                double earlyLowIntensity = GetIntensity(earlyLow, mz);
                double earlyHighIntensity = GetIntensity(earlyHigh, mz);
                double lateLowIntensity = GetIntensity(lateLow, mz);
                double lateHighIntensity = GetIntensity(lateHigh, mz);

                double totalLow = earlyLowIntensity + lateLowIntensity;
                double totalHigh = earlyHighIntensity + lateHighIntensity;
                double total = totalLow + totalHigh;

                if (total > 0)
                {
                    double fractionLow = totalLow/total;
                    if (fractionLow > 0.0f)
                    {
                        double intensity = originalSpectrum.Intensities[i]*fractionLow;
                        lowerPeaks.Add(new KeyValuePair<double, double>(mz, intensity));
                    }

                    double fractionHigh = totalHigh/total;
                    if (fractionHigh > 0.0f)
                    {
                        double intensity = originalSpectrum.Intensities[i]*fractionHigh;
                        upperPeaks.Add(new KeyValuePair<double, double>(mz, intensity));
                    }
                }
                else
                {
                    removedPeaks.Add(new KeyValuePair<double, double>(mz, originalSpectrum.Intensities[i]));
                }
            }

            MsDataSpectrum lowerSpectrum = MakeDeconvSpectrum(originalSpectrum, lowerPeaks);
            lowerSpectrum.Precursors = ImmutableList.Singleton(MakeMsPrecursor(lowerMz, middlePrecursorMz.Value));
            MsDataSpectrum upperSpectrum = MakeDeconvSpectrum(originalSpectrum, upperPeaks);
            upperSpectrum.Precursors = ImmutableList.Singleton(MakeMsPrecursor(middlePrecursorMz.Value, upperMz));
            return new[] {lowerSpectrum, upperSpectrum};
        }

        /// <summary>
        /// Finds the m/z where the upper edge of the lower spectra meets the lower edge of the upper spectra.
        /// </summary>
        private double? GetMiddlePrecursorMz(IList<MsDataSpectrum> lowSpectra,
            IList<MsDataSpectrum> highSpectra)
        {
            IList<double> middleMzs = lowSpectra.Select(spectrum => spectrum == null ? null : GetUpperMz(spectrum.Precursors.First()))
                .Concat(highSpectra.Select(spectrum => spectrum == null ? null : GetLowerMz(spectrum.Precursors.First())))
                .OfType<double>().ToArray();
            if (middleMzs.Count == 0)
            {
                return null;
            }
            return middleMzs.Average();
        }

        private MsPrecursor MakeMsPrecursor(double lowerMz, double upperMz)
        {
            
            return new MsPrecursor
            {
                PrecursorMz = new SignedMz((lowerMz + upperMz) / 2),
                IsolationWindowTargetMz = new SignedMz((lowerMz + upperMz) / 2),
                IsolationWindowLower = (upperMz - lowerMz) / 2,
                IsolationWindowUpper = (upperMz - lowerMz) / 2,
            };
        }

        private double? GetLowerMz(MsPrecursor precursor)
        {
            return precursor.IsolationMz - precursor.IsolationWidth / 2;
        }

        private double? GetUpperMz(MsPrecursor precursor)
        {
            return precursor.IsolationMz + precursor.IsolationWidth /2;
        }

        private static MsDataSpectrum MakeDeconvSpectrum(MsDataSpectrum originalSpectrum, List<KeyValuePair<double, double>> peaks)
        {
            return new MsDataSpectrum
            {
                RetentionTime = originalSpectrum.RetentionTime,
                Centroided = originalSpectrum.Centroided,
                IonMobility = originalSpectrum.IonMobility,
                Level = originalSpectrum.Level,
                Mzs = peaks.Select(p=>p.Key).ToArray(),
                Intensities = peaks.Select(p=>p.Value).ToArray(),
            };
        }


        private double GetIntensity(MsDataSpectrum spectrum, double mz)
        {
            if (spectrum == null)
            {
                // if we're at a boundary, return minimum value. This means if the peak
                // is not in the other stripe, we get it. Otherwise, they essentially
                // get the intensity.
                return double.Epsilon;
            }
            var indices = _fragmentTolerance.GetIndices(spectrum.Mzs, mz);
            return indices.Sum(i => spectrum.Intensities[i]);
        }

        public MsDataSpectrum FindOverlappingSpectrum(double mz, IEnumerable<int> spectrumIndexes, MsPrecursor originalPrecursor)
        {
            foreach (var spectrumIndex in spectrumIndexes)
            {
                var precursors = GetPrecursors(spectrumIndex);
                if (precursors.Count != 1)
                {
                    continue;
                }
                if (Contains(precursors[0], mz))
                {
                    return _dataFile.GetSpectrum(spectrumIndex);
                }
                if (Equals(precursors[0].IsolationMz, originalPrecursor.IsolationMz))
                {
                    return null;
                }
            }
            return null;
        }

        private IList<MsPrecursor> GetPrecursors(int scanIndex)
        {
            IList<MsPrecursor> precursors;
            if (!_precursorsByScanIndex.TryGetValue(scanIndex, out precursors))
            {
                precursors = _dataFile.GetPrecursors(scanIndex, 1);
                _precursorsByScanIndex[scanIndex] = precursors;
            }
            return precursors;
        }


        private bool Contains(MsPrecursor precursor, double mz)
        {
            return GetLowerMz(precursor) <= mz && GetUpperMz(precursor) >= mz;
        }
        class MassTolerance
        {
            // ReSharper disable NotAccessedField.Local
            private readonly double _ppmTolerance;
            // ReSharper restore NotAccessedField.Local
            private readonly double _percent;

            public MassTolerance(double ppmTolerance)
            {
                _ppmTolerance = ppmTolerance;
                _percent = ppmTolerance / 1000000.0;
            }

            /**
         * @param peaks -- assumes sorted array of peaks
         * @param target
         * @return all matching masses in range
         */
            public IEnumerable<int> GetIndices(double[] peaks, double target)
            {
                double lowerMz = target - target * _percent;
                double upperMz = target + target * _percent;
                int lowerIndex = Array.BinarySearch(peaks, lowerMz);
                if (lowerIndex < 0)
                {
                    lowerIndex = ~lowerIndex;
                }
                for (int i = lowerIndex; i < peaks.Length; i++)
                {
                    if (peaks[i] > upperMz)
                    {
                        yield break;
                    }
                    yield return i;
                }
            }
        }
    }
}
