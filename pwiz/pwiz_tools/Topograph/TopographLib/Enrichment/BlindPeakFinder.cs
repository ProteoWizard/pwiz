using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Enrichment
{
    public class BlindPeakFinder
    {
        public BlindPeakFinder(PeptideFileAnalysis peptideFileAnalysis, Chromatograms chromatograms)
        {
            using (peptideFileAnalysis.GetReadLock())
            {
                PeptideFileAnalysis = peptideFileAnalysis;
                Chromatograms = chromatograms;
                TurnoverCalculator = peptideFileAnalysis.TurnoverCalculator;
                MinCharge = peptideFileAnalysis.MinCharge;
                MaxCharge = peptideFileAnalysis.MaxCharge;
                ExcludedMzs = peptideFileAnalysis.ExcludedMzs;
                //Times = peptideFileAnalysis.Times;
            }
        }

        public PeptideFileAnalysis PeptideFileAnalysis { get; private set; }
        public Chromatograms Chromatograms { get; private set; }
        public TurnoverCalculator TurnoverCalculator { get; private set; }
        public int MinCharge { get; private set; }
        public int MaxCharge { get; private set; }
        public ExcludedMzs ExcludedMzs { get; private set; }
        public IList<double> Times { get; private set; }

        public void FindPeak(out int peakStart, out int peakEnd)
        {
            peakStart = peakEnd = 0;
            double bestScore = 0;
            IList<double[]> massDistributions = TurnoverCalculator
                .GetMassDistributions(ExcludedMzs.IsMassExcluded);
            var intensities = GetIntensities();
            var maxIntensities = MaxIntensities(intensities);
            maxIntensities = ChromatogramData.SavitzkyGolaySmooth(maxIntensities);
            var peakIndexes = new PeakDivider().DividePeaks(maxIntensities);
            for (int index = 0; index < peakIndexes.Count; index+=2)
            {
                var areas = new double[intensities.Count];
                for (int iMass = 0; iMass < areas.Length; iMass++)
                {
                    if (ExcludedMzs.IsMassExcluded(iMass))
                    {
                        areas[iMass] = double.NaN;
                    }
                    else
                    {
                        for (int i = peakIndexes[index]; i < peakIndexes[index+1];i++)
                        {
                            areas[iMass] += intensities[iMass][i];
                        }
                    }
                }
                double score = TurnoverCalculator.CalcTracerScore(Filter(areas, ExcludedMzs.IsMassExcluded),
                                                                  massDistributions);
                if (score > bestScore)
                {
                    peakStart = peakIndexes[index];
                    peakEnd = peakIndexes[index+1];
                    bestScore = score;
                }
            }
        }

        private static double[] Filter(IList<double> vector, Func<int,bool> excludeFunc)
        {
            var result = new List<double>();
            for (int i = 0; i < vector.Count; i++)
            {
                if (excludeFunc(i))
                {
                    continue;
                }
                result.Add(vector[i]);
            }
            return result.ToArray();
        }

        private IList<IList<double>> GetIntensities()
        {
            var result = new List<IList<double>>();
            int minCharge = MinCharge;
            int maxCharge = MaxCharge;
            //var times = PeptideFileAnalysis.Times;
//            for (int iMass = 0; iMass < TurnoverCalculator.MassCount; iMass ++)
//            {
//                var vector = new double[times.Count];
//                for (int charge = minCharge; charge <= maxCharge; charge++)
//                {
//                    var mzKey = new MzKey(charge, iMass);
//                    var chromatogram = Chromatograms.GetChild(mzKey);
//                    var intensities = chromatogram.GetIntensities();
//                    for (int i = 0; i < vector.Length; i++)
//                    {
//                        vector[i] += intensities[i];
//                    }
//                }
//                result.Add(vector);
//            }
            return result;
        }

        private IList<double> MaxIntensities(IList<IList<double>> allIntensities)
        {
            IList<double> result = null;
            foreach (var list in allIntensities)
            {
                if (result == null)
                {
                    result = list.ToArray();
                }
                else
                {
                    for (int i = 0; i < result.Count; i++)
                    {
                        result[i] = Math.Max(result[i], list[i]);
                    }
                }
            }
            return result;
        }
    }
}
