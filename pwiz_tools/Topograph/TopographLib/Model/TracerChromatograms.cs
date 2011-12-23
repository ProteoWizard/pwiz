using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Crawdad;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class TracerChromatograms
    {
        private const int MAX_PEAKS = 20;
        public TracerChromatograms(Chromatograms chromatograms, bool smoothed)
        {
            Smoothed = smoothed;
            Chromatograms = chromatograms;
            PeptideFileAnalysis = chromatograms.PeptideFileAnalysis;
            var pointDict = new Dictionary<TracerFormula, IList<double>>();
            var chromatogramsDict = new Dictionary<MzKey, IList<double>>();
            foreach (var chromatogram in chromatograms.GetFilteredChromatograms())
            {
                var intensities = chromatogram.GetIntensities();
                if (smoothed)
                {
                    intensities = ChromatogramData.SavitzkyGolaySmooth(intensities);
                }
                chromatogramsDict.Add(chromatogram.MzKey, intensities);
            }
            int massCount = PeptideFileAnalysis.PeptideAnalysis.GetMassCount();
            var times = chromatograms.Times.ToArray();
            var scores = new List<double>();
            var turnoverCalculator = PeptideAnalysis.GetTurnoverCalculator();
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
                peaks.Add(entry.Key, peakFinder.CalcPeaks(MAX_PEAKS));
            }
            Points = points;
            Scores = scores;
            Times = chromatograms.Times;
            RawPeaks = peaks;
        }

        public Chromatograms Chromatograms { get; private set; }

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
            int startIndex = Chromatograms.IndexFromTime(startTime);
            int endIndex = Chromatograms.IndexFromTime(endTime);
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
    }
}
