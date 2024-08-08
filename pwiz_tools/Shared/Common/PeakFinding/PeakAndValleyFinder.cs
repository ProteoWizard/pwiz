/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Common.Collections;

namespace pwiz.Common.PeakFinding
{
    internal class PeakAndValleyFinder
    {
        private IList<float> _intensitiesWithWings;
        private int _fullWidthHalfMax;
        internal int _widthDataWings;
        private IList<float> _chrom2D;
        private IList<int> _plusCrosses;
        private IList<int> _minusCrosses;
        private IList<int> _peaks;
        private IList<int> _valleys;


        public PeakAndValleyFinder(IList<float> intensities)
        {
            int maxIntensityIndex = -1;
            double baselineIntensity = 0;
            if (intensities.Count > 0)
            {
                float maxIntensityFloat = intensities.Max();
                if (maxIntensityFloat > 0)
                {
                    maxIntensityIndex = intensities.IndexOf(maxIntensityFloat);
                    baselineIntensity = intensities.Min();
                }
            }
            int fwhm = 6;
            if (maxIntensityIndex != -1)
            {
                double halfHeight = (intensities[maxIntensityIndex] - baselineIntensity) / 2 + baselineIntensity;
                int iStart = 0;
                for (int i = maxIntensityIndex - 1; i >= 0; i--)
                {
                    if (intensities[i] < halfHeight)
                    {
                        iStart = i;
                        break;
                    }
                }
                int len = intensities.Count;
                int iEnd = len - 1;
                for (int i = maxIntensityIndex + 1; i < len; i++)
                {
                    if (intensities[i] < halfHeight)
                    {
                        iEnd = i;
                        break;
                    }
                }
                fwhm = Math.Max(fwhm, iEnd - iStart);
            }
            _fullWidthHalfMax = fwhm * 3;
            _min_len = (int) (fwhm/4.0 + 0.5);
            _widthDataWings = _fullWidthHalfMax * 2;
            _intensitiesWithWings =
                Enumerable.Repeat((float) baselineIntensity, _widthDataWings)
                    .Concat(intensities)
                    .Concat(Enumerable.Repeat((float)baselineIntensity, _widthDataWings))
                    .ToArray();
            var gs2D = new GaussSmother();
            gs2D.SetGaussWeights(GetSd(), 2);
            gs2D.InvertWeights();
            gs2D.TrimWeightsByFracMax(0.005f);
            _chrom2D = gs2D.SmoothVect(_intensitiesWithWings);
            FindCrossPoints(0f);
            FindsPeaksAndValleys();
        }
        private float GetSd()
        {
            return _fullWidthHalfMax / 2.35482f;
        }

        const int SWITCH_LENGTH = 2;
        private void FindCrossPoints(float threshold)
        {
            _plusCrosses = new List<int>();
            _minusCrosses = new List<int>();
            var chromatogram = _chrom2D;
            for (int i = 0; i < chromatogram.Count - SWITCH_LENGTH; i++)
            {
                if (chromatogram[i] < threshold)
                {
                    if (SubList(chromatogram, i + 1, SWITCH_LENGTH).All(value => value >= threshold))
                    {
                        _plusCrosses.Add(i);
                    }
                }
                if (chromatogram[i] > threshold)
                {
                    if (SubList(chromatogram, i + 1, SWITCH_LENGTH).All(value => value <= threshold))
                    {
                        _minusCrosses.Add(i);
                    }
                }
            }
        }

        private IEnumerable<T> SubList<T>(IList<T> list, int start, int count)
        {
            int max = Math.Min(list.Count, start + count);
            for (int i = start; i < max; i++)
            {
                yield return list[i];
            }
        }

        void FindsPeaksAndValleys()
        {
            _peaks = new List<int>();
            _valleys = new List<int>();
            int iMinus = 0, lenMinus = _minusCrosses.Count;
            int iPlus = 0, lenPlus = _plusCrosses.Count;
            List<KeyValuePair<int, char>> indexes = new List<KeyValuePair<int, char>>();
            /* Merge sorted lists of plus and minus crosses */
            /* + and - always have to alternate, since we define in terms of crossing one line, although we have the rule that they can't pass
                 over one gap point apart  */
            while (iMinus < lenMinus || iPlus < lenPlus)
            {
                KeyValuePair<int, char> indexType;
                if (iMinus >= lenMinus ||
                        (iPlus < lenPlus && _plusCrosses[iPlus] < _minusCrosses[iMinus]))
                {
                    // Ignore consecutive plus crosses (fix so this can be an assert)
                    if (indexes.Count != 0 && indexes[indexes.Count - 1].Value == 'p')
                    {
                        iPlus++;
                        continue;
                    }
                    indexType = new KeyValuePair<int, char>(_plusCrosses[iPlus++], 'p');
                }
                else
                {
                    // Ignore consecutive minus crosses (fix so this can be an assert)
                    if (indexes.Count != 0 && indexes[indexes.Count - 1].Value == 'm')
                    {
                        iMinus++;
                        continue;
                    }
                    indexType = new KeyValuePair<int, char>(_minusCrosses[iMinus++], 'm');
                }
                indexes.Add(indexType);
            }

            //valley at the beginning of the chrom
            if (indexes.Count == 0 || indexes[0].Value == 'p')
                _valleys.Add(0);
            if (indexes.Count > 0)
            {
                for (int i = 0; i < indexes.Count - 1; i++)
                {
                    char trans_type = indexes[i].Value;

                    if (trans_type == 'p')
                    {
                        //find index of maximum value spanning from all_trans[idx] to all_trans[idx+1]
                        float max = 0;
                        int max_idx = indexes[i].Key;
                        for (int idx = indexes[i].Key; idx < indexes[i + 1].Key; idx++)
                        {
                            if (_chrom2D[idx] > max)
                            {
                                max = _chrom2D[idx];
                                max_idx = idx;
                            }
                        }
                        _peaks.Add(max_idx);
                    }
                    else if (trans_type == 'm')
                    {
                        //find index of minimum value spanning from all_trans[idx] to all_trans[idx+1]
                        float min = 0;
                        int min_idx = indexes[i].Key;
                        for (int idx = indexes[i].Key; idx < indexes[i + 1].Key; idx++)
                        {
                            if (_chrom2D[idx] < min)
                            {
                                min = _chrom2D[idx];
                                min_idx = idx;
                            }
                        }

                        _valleys.Add(min_idx);
                    }
                }
                //adding a valley at the end of the chromatogram
                if (_valleys.Count == _peaks.Count)
                    _valleys.Add(_intensitiesWithWings.Count - 1);
                // Make sure the arrays are the right size
            }
        }

        public List<KeyValuePair<int, int>> FindPeaks()
        {
            List<KeyValuePair<int, int>> peaks = new List<KeyValuePair<int, int>>();
            for (int i = 0; i < _peaks.Count; i++)
            {
                int lh_valley, rh_valley;
                int chrom2d_peak_loc = _peaks[i];
                int lh_valley_idx = getLhIndex(_valleys, chrom2d_peak_loc);
                if (_valleys.Count == 1)
                {
                    if (_valleys[0] > _peaks[0])
                    {
                        lh_valley = 0;
                        rh_valley = _valleys[0];
                    }
                    else
                    {
                        lh_valley = _valleys[0];
                        rh_valley = _intensitiesWithWings.Count - 1;
                    }
                }

                else if (_valleys[lh_valley_idx] > chrom2d_peak_loc)
                {
                    lh_valley = 0;

                    rh_valley = 1;
                }
                else if (lh_valley_idx >= _valleys.Count - 1)
                {
                    lh_valley = _valleys[lh_valley_idx - 1];
                    rh_valley = _intensitiesWithWings.Count - 1;
                }
                else
                {
                    lh_valley = _valleys[lh_valley_idx];
                    rh_valley = _valleys[lh_valley_idx + 1];
                }



                //now we expand from the peak locations to 
                //std::cerr<< "DEBUG: calling minimum_level" << std::endl;

                //note that this alters lh_valley, rh_valley
                delimitByMinimumLevel(ref lh_valley, ref rh_valley, chrom2d_peak_loc);

                //std::cerr<< "DEBUG: called minimum_level" << std::endl;
                //now we have lh,peak,rh
                if (rh_valley - lh_valley + 1 >= _min_len)
                {
                    peaks.Add(new KeyValuePair<int, int>(lh_valley - _widthDataWings, rh_valley - _widthDataWings));
                }
            }
            return peaks;
        }
        private static int getLhIndex(IList<int> values, int lookup)
        {
            int i = CollectionUtil.BinarySearch(values, lookup);
            if (i >= 0)
            {
                return i;
            }
            i = ~i;
            if (i >= values.Count)
            {
                return values.Count - 1;
            }
            if (i <= 1)
            {
                return 0;
            }
            return i - 1;
        }

        const float minimum_level = 0;
        private int _min_len;
        private void delimitByMinimumLevel(ref int lh_valley_idx, ref int rh_valley_idx, int peak_loc)
        {
            for (int lh = peak_loc; lh > lh_valley_idx; lh--)
            {
                if (_intensitiesWithWings[lh] <= minimum_level)
                {
                    lh_valley_idx = lh;
                    break;
                }
            }
            for (int rh = peak_loc; rh <= rh_valley_idx; rh++)
            {
                if (_intensitiesWithWings[rh] <= minimum_level)
                {
                    rh_valley_idx = rh;
                    break;
                }
            }
        }

        public IList<float> GetIntensities2d()
        {
            return _chrom2D;
        }

        public IList<float> GetIntensities1d()
        {
            var gs1d = new GaussSmother();
            gs1d.SetGaussWeights(GetSd(), 1);
            gs1d.TrimWeightsByFracMax(0.005f);
            return gs1d.SmoothVect(_intensitiesWithWings);
        }
    }
}
