/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Crawdad;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class TracerChromatograms
    {
        private const int MaxPeaks = 20;
        public TracerChromatograms(PeptideFileAnalysis peptideFileAnalysis, ChromatogramSetData chromatogramSet, bool smoothed)
        {
            Smoothed = smoothed;
            ChromatogramSet = chromatogramSet;
            PeptideFileAnalysis = peptideFileAnalysis;
            var pointDict = new Dictionary<TracerFormula, IList<double>>();
            var chromatogramsDict = new Dictionary<MzKey, IList<double>>();
            var turnoverCalculator = PeptideAnalysis.GetTurnoverCalculator();
            double massAccuracy = PeptideAnalysis.GetMassAccuracy();
            foreach (var chromatogramEntry in ChromatogramSet.Chromatograms)
            {
                if (PeptideAnalysis.ExcludedMasses.Contains(chromatogramEntry.Key.MassIndex))
                {
                    continue;
                }
                var chromatogram = chromatogramEntry.Value;
                var mzRange = turnoverCalculator.GetMzs(chromatogramEntry.Key.Charge)[chromatogramEntry.Key.MassIndex];
                var intensities = chromatogram.ChromatogramPoints.Select(point=>point.GetIntensity(mzRange, massAccuracy)).ToArray();
                if (smoothed)
                {
                    intensities = SavitzkyGolaySmooth(intensities);
                }
                chromatogramsDict.Add(chromatogramEntry.Key, intensities);
            }
            int massCount = PeptideFileAnalysis.PeptideAnalysis.GetMassCount();
            var times = chromatogramSet.Times.ToArray();
            var scores = new List<double>();
            var tracerFormulas = turnoverCalculator.ListTracerFormulas();
            var theoreticalIntensities = turnoverCalculator.GetTheoreticalIntensities(tracerFormulas);
            for (int i = 0; i < times.Length; i++)
            {
                var intensities = new List<double>();
                for (int iMass = 0; iMass < massCount; iMass++)
                {
                    double intensity = 0;
                    for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
                    {
                        IList<double> chromatogram;
                        if (chromatogramsDict.TryGetValue(new MzKey(charge, iMass), out chromatogram))
                        {
                            intensity += chromatogram[i];
                        }
                        else
                        {
                            intensity = double.NaN;
                        }
                    }
                    intensities.Add(intensity);
                }
                double score;
                IDictionary<TracerFormula, IList<double>> predictedIntensities;
                PeptideAnalysis.GetTurnoverCalculator().GetTracerAmounts(intensities, out score, out predictedIntensities, tracerFormulas, theoreticalIntensities);
                foreach (var entry in predictedIntensities)
                {
                    IList<double> list;
                    if (!pointDict.TryGetValue(entry.Key, out list))
                    {
                        list = new List<double>();
                        pointDict.Add(entry.Key, list);
                    }
                    list.Add(entry.Value.Sum());
                }
                scores.Add(score);
            }
            var points = new SortedDictionary<TracerFormula, IList<double>>();
            var peaks = new Dictionary<TracerFormula, IList<CrawdadPeak>>();
            foreach (var entry in pointDict)
            {
                points.Add(entry.Key, entry.Value);
                CrawPeakFinderWrapper peakFinder = new CrawPeakFinderWrapper();
                peakFinder.SetChromatogram(times, entry.Value);
                peaks.Add(entry.Key, peakFinder.CalcPeaks(MaxPeaks));
            }
            Points = points;
            Scores = scores;
            Times = chromatogramSet.Times;
            RawPeaks = peaks;
        }

        public ChromatogramSetData ChromatogramSet { get; private set; }

        public IList<TracerFormula> ListTracerFormulas()
        {
            var list = new List<TracerFormula>(Points.Keys);
            list.Sort();
            return list;
        }

        public bool Smoothed { get; private set; }
        public PeptideFileAnalysis PeptideFileAnalysis { get; private set; }
        public PeptideAnalysis PeptideAnalysis { get { return PeptideFileAnalysis.PeptideAnalysis; } }

        public IDictionary<TracerFormula, IList<double>> Points { get; private set; }
        public IList<double> Scores { get; private set; }
        public IList<double> Times { get; private set; }
        public IDictionary<TracerFormula, IList<CrawdadPeak>> RawPeaks { get; private set; }
        public IList<KeyValuePair<TracerFormula, CrawdadPeak>> ListPeaks()
        {
            var list = new List<KeyValuePair<TracerFormula, CrawdadPeak>>();
            foreach (var entry in RawPeaks)
            {
                foreach (var peak in entry.Value)
                {
                    list.Add(new KeyValuePair<TracerFormula, CrawdadPeak>(entry.Key, peak));
                }
            }
            list.Sort((e1,e2)=>(e2.Value.Area.CompareTo(e1.Value.Area)));
            return list;
        }

        public IDictionary<TracerFormula, double> GetDistribution(double? startTime, double? endTime)
        {
            var rawResult = new Dictionary<TracerFormula, double>();
            double total = 0;
            if (!startTime.HasValue || !endTime.HasValue)
            {
                return rawResult;
            }
            foreach (var entry in Points)
            {
                double value = 0;
                for (int i = 0; i < Times.Count; i++)
                {
                    if (Times[i] < startTime || Times[i] > endTime)
                    {
                        continue;
                    }
                    value += entry.Value[i];
                }
                total += value;
                rawResult.Add(entry.Key, value);
            }
            if (total == 0)
            {
                return rawResult;
            }
            return Dictionaries.Scale(rawResult, 1 / total);
        }

        public double GetScore(int startIndex, int endIndex)
        {
            double total = 0;
            double totalScore = 0;
            for (int i = startIndex; i <= endIndex; i++)
            {
                double sum = 0;
                foreach (var entry in Points)
                {
                    sum += entry.Value[i];
                }
                total += sum;
                totalScore += sum*Scores[i];
            }
            if (total == 0)
            {
                return 0;
            }
            return totalScore/total;
        }

        public double GetMaxIntensity(TracerFormula tracerFormula, int startIndex, int endIndex)
        {
            var points = Points[tracerFormula];
            double result = 0;
            for (int i = startIndex; i <= endIndex; i++)
            {
                result = Math.Max(result, points[i]);
            }
            return result;
        }

        public double GetBackground(TracerFormula tracerFormula, double startTime, double endTime)
        {
            return 0;
        }

        public double GetArea(TracerFormula tracerFormula, double startTime, double endTime)
        {
            double result = 0;
            int startIndex = ChromatogramSet.IndexFromTime(startTime);
            int endIndex = ChromatogramSet.IndexFromTime(endTime);
            for (int i = startIndex; i<= endIndex; i++)
            {
                int binStartIndex = Math.Max(i - 1, 0);
                int binEndIndex = Math.Min(i + 1, Times.Count - 1);
                if (binStartIndex == binEndIndex)
                {
                    continue;
                }
                double width = (Times[binEndIndex] - Times[binStartIndex])/(binEndIndex - binStartIndex);
                result += Points[tracerFormula][i] * width;
            }
            return result;
        }
        public static double[] SavitzkyGolaySmooth(IList<double> intRaw)
        {
            if (intRaw.Count < 9)
            {
                return intRaw.ToArray();
            }
            double[] intSmooth = new double[intRaw.Count];
            for (int i = 0; i < 4; i++)
            {
                intSmooth[i] = intRaw[i];
            }
            for (int i = 4; i < intSmooth.Length - 4; i++)
            {
                double sum = 59 * intRaw[i] +
                    54 * (intRaw[i - 1] + intRaw[i + 1]) +
                    39 * (intRaw[i - 2] + intRaw[i + 2]) +
                    14 * (intRaw[i - 3] + intRaw[i + 3]) -
                    21 * (intRaw[i - 4] + intRaw[i + 4]);
                intSmooth[i] = (float)(sum / 231);
            }
            for (int i = intSmooth.Length - 4; i < intSmooth.Length; i++)
            {
                intSmooth[i] = intRaw[i];
            }
            return intSmooth;
        }

    }
}
