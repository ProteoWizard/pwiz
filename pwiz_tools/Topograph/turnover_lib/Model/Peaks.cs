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
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model
{
    public class Peaks : SimpleChildCollection<DbPeptideFileAnalysis, MzKey, DbPeak>
    {
        public Peaks(PeptideFileAnalysis peptideFileAnalysis, DbPeptideFileAnalysis dbPeptideFileAnalysis) : base(peptideFileAnalysis.Workspace, dbPeptideFileAnalysis)
        {
            PeptideFileAnalysis = peptideFileAnalysis;
        }
        public Peaks(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis.Workspace)
        {
            PeptideFileAnalysis = peptideFileAnalysis;
            SetId(peptideFileAnalysis.Id);
            PeakStart = peptideFileAnalysis.PeakStart;
            PeakEnd = peptideFileAnalysis.PeakEnd;
        }

        protected override IEnumerable<KeyValuePair<MzKey, DbPeak>> GetChildren(DbPeptideFileAnalysis parent)
        {
            foreach (var peak in parent.Peaks)
            {
                yield return new KeyValuePair<MzKey, DbPeak>(peak.MzKey, peak);
            }
        }

        protected override int GetChildCount(DbPeptideFileAnalysis parent)
        {
            return parent.PeakCount;
        }

        protected override void SetChildCount(DbPeptideFileAnalysis parent, int childCount)
        {
            parent.PeakCount = childCount;
        }

        protected override void SetParent(DbPeak child, DbPeptideFileAnalysis parent)
        {
            child.PeptideFileAnalysis = parent;
        }

        public PeptideFileAnalysis PeptideFileAnalysis { get; private set; }
        protected override void Load(DbPeptideFileAnalysis parent)
        {
            base.Load(parent);
            PeakStart = parent.PeakStart;
            PeakEnd = parent.PeakEnd;
        }

        protected override DbPeptideFileAnalysis UpdateDbEntity(ISession session)
        {
            var result = base.UpdateDbEntity(session);
            result.PeakStart = PeakStart;
            result.PeakStartTime = PeakStartTime;
            result.PeakEnd = PeakEnd;
            result.PeakEndTime = PeakEndTime;
            return result;
        }

        public int? PeakStart { get; set; }
        public int? PeakEnd { get; set; }
        public double Background { get { return 0; } }
        public double? PeakStartTime
        {
            get { return PeakStart.HasValue ? (double?)PeptideFileAnalysis.TimeFromScanIndex(PeakStart.Value) : null; }
        }
        public double? PeakEndTime
        {
            get { return PeakEnd.HasValue ? (double?)PeptideFileAnalysis.TimeFromScanIndex(PeakEnd.Value) : null; }
        }
        private void FindPeak()
        {
            var scanIndexes = PeptideFileAnalysis.ScanIndexesArray;
            int firstDetectedScan = Array.BinarySearch(PeptideFileAnalysis.ScanIndexesArray, PeptideFileAnalysis.FirstDetectedScan);
            if (firstDetectedScan < 0)
            {
                firstDetectedScan = ~firstDetectedScan;
            }
            firstDetectedScan = Math.Min(firstDetectedScan, PeptideFileAnalysis.ScanIndexesArray.Length - 1);
            int lastDetectedScan = Array.BinarySearch(scanIndexes, PeptideFileAnalysis.LastDetectedScan);
            if (lastDetectedScan < 0)
            {
                lastDetectedScan = ~lastDetectedScan;
            }
            lastDetectedScan = Math.Min(lastDetectedScan, scanIndexes.Length - 1);

            var peakFinder = new PeakFinder
            {
                Chromatograms = PeptideFileAnalysis.Chromatograms.GetFilteredChromatograms(),
                //TODO
                IsotopesEluteEarlier = true,
                IsotopesEluteLater = true,
                FirstDetectedScan = firstDetectedScan,
                LastDetectedScan = lastDetectedScan,
            };
            int peakStart, peakEnd;
            peakFinder.FindPeak(out peakStart, out peakEnd);
            PeakStart = scanIndexes[peakStart];
            PeakEnd = scanIndexes[peakEnd];
        }
        public void CalcIntensities()
        {
            if (PeakStart == null || PeptideFileAnalysis.AutoFindPeak)
            {
                FindPeak();
            }
            for (int charge = PeptideFileAnalysis.MinCharge; charge <= PeptideFileAnalysis.MaxCharge; charge++)
            {
                for (int massIndex = 0; massIndex < PeptideFileAnalysis.PeptideAnalysis.GetMassCount(); massIndex++)
                {
                    var mzKey = new MzKey(charge, massIndex);
                    double mz = PeptideFileAnalysis.TurnoverCalculator.GetMzs(charge)[massIndex];
                    var peak = new DbPeak
                    {
                        MzKey = mzKey,
                        Mz = mz,
                        PeakStart = PeakStart.Value,
                        PeakEnd = PeakEnd.Value
                    };
                    AddChild(peak.MzKey, peak);
                    var chromatogram = PeptideFileAnalysis.Chromatograms.GetChild(mzKey);
                    PeakFinder.ComputePeak(chromatogram, peak, Background);
                }
            }
        }

        public int MassCount 
        { 
            get
            {
                return PeptideFileAnalysis.PeptideAnalysis.GetMassCount();
            }
        }

        public IList<double> GetIntensities(int charge, IntensityScaleMode scaleMode)
        {
            if (scaleMode == IntensityScaleMode.relative_total)
            {
                var relativeIntensities = GetRelativeIntensities();
                IList<double> averageIntensities;
                double scalingFactor = GetTotalScalingFactor(relativeIntensities, out averageIntensities);
                return Scale(relativeIntensities[charge], scalingFactor);
            }
            int massCount = MassCount;
            var intensities = new double[massCount];
            for (int iMass = 0; iMass < massCount; iMass++)
            {
                var mzKey = new MzKey(charge, iMass);
                var peak = GetChild(mzKey);
                if (peak == null)
                {
                    intensities[iMass] = double.NaN;
                }
                else
                {
                    intensities[iMass] = peak.TotalArea;
                }
            }
            if (scaleMode == IntensityScaleMode.none)
            {
                return intensities;
            }
            if (scaleMode == IntensityScaleMode.relative_include_all)
            {
                return Scale(intensities, 100.0 / GetSum(intensities));
            }
            if (scaleMode == IntensityScaleMode.relative_exclude_any_charge)
            {
                double total = 0;
                for (int iMass = 0; iMass < massCount; iMass++)
                {
                    if (ExcludedMzs.IsMassExcludedForAnyCharge(iMass))
                    {
                        continue;
                    }
                    total += intensities[iMass];
                }
                return Scale(intensities, 100.0 / total);
            }
            throw new InvalidOperationException("Unknown scale mode " + scaleMode);
        }

        public ExcludedMzs ExcludedMzs { get { return PeptideFileAnalysis.ExcludedMzs; } }

        private double GetTotalScalingFactor(Dictionary<int, IList<double>> relativeIntensities, out IList<double> averageIntensities)
        {
            int massCount = PeptideFileAnalysis.PeptideAnalysis.GetMassCount();
            var unscaledAverageIntensities = new double[massCount];
            for (int massIndex = 0; massIndex < massCount; massIndex++)
            {
                int count = 0;
                double total = 0;
                foreach (var entry in relativeIntensities)
                {
                    var mzKey = new MzKey(entry.Key, massIndex);
                    if (PeptideFileAnalysis.ExcludedMzs.IsExcluded(mzKey))
                    {
                        continue;
                    }
                    total += entry.Value[massIndex];
                    count++;
                }
                if (count == 0)
                {
                    unscaledAverageIntensities[massIndex] = double.NaN;
                }
                else
                {
                    unscaledAverageIntensities[massIndex] = total / count;
                }
            }
            double scalingFactor = 100.0 / GetSum(unscaledAverageIntensities);
            averageIntensities = Scale(unscaledAverageIntensities, scalingFactor);
            return scalingFactor;
        }

        private static double GetSum(IList<double> values)
        {
            double total = 0;
            foreach (var value in values)
            {
                if (double.IsNaN(value))
                {
                    continue;
                }
                total += value;
            }
            return total;
        }

        private static IList<double> Scale(IList<double> values, double factor)
        {
            var result = new double[values.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = values[i] * factor;
            }
            return result;
        }

        public IList<double> GetAverageIntensities()
        {
            IList<double> averageIntensities;
            GetTotalScalingFactor(GetRelativeIntensities(), out averageIntensities);
            return averageIntensities;
        }

        private Dictionary<int, IList<double>> GetRelativeIntensities()
        {
            var result = new Dictionary<int, IList<double>>();
            for (int charge = PeptideFileAnalysis.PeptideAnalysis.MinCharge; 
                charge <= PeptideFileAnalysis.PeptideAnalysis.MaxCharge; charge++)
            {
                result.Add(charge, GetIntensities(charge, IntensityScaleMode.relative_exclude_any_charge));
            }
            return result;
        }
    }
}