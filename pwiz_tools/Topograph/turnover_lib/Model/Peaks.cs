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
            IsDirty = true;
        }

        public bool IsDirty { get; set; }

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
            if (PeptideFileAnalysis.FirstDetectedScan == null || PeptideFileAnalysis.LastDetectedScan == null 
                || PeptideFileAnalysis.FirstDetectedScan == 0 || PeptideFileAnalysis.LastDetectedScan == 0)
            {
                var blindPeakFinder = new BlindPeakFinder(PeptideFileAnalysis, PeptideFileAnalysis.Chromatograms);
                int blindPeakStart, blindPeakEnd;
                blindPeakFinder.FindPeak(out blindPeakStart, out blindPeakEnd);
                PeakStart = scanIndexes[blindPeakStart];
                PeakEnd = scanIndexes[blindPeakEnd];
                return;
            }
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
                    var mzRange = PeptideFileAnalysis.TurnoverCalculator.GetMzs(charge)[massIndex];
                    var peak = new DbPeak
                    {
                        MzKey = mzKey,
                        MzRange = mzRange,
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

        public PeptideAnalysis PeptideAnalysis { get { return PeptideFileAnalysis.PeptideAnalysis; } }


        public ExcludedMzs ExcludedMzs { get { return PeptideFileAnalysis.ExcludedMzs; } }

        public IList<double> GetAverageIntensitiesExcludedAsNaN()
        {
            var averageIntensities = GetAverageIntensities();
            var result = new double[averageIntensities.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (ExcludedMzs.IsExcluded(i))
                {
                    result[i] = double.NaN;
                }
                else
                {
                    result[i] = averageIntensities[i];
                }
            }
            return result;
        }
        
        /// <summary>
        /// Returns the intensities summed across the charges and scaled 
        /// so that their total is 100.
        /// </summary>
        public IList<double> GetAverageIntensities()
        {
            int massCount = PeptideAnalysis.GetMassCount();
            var rawTotals = new double[massCount];
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
            {
                var rawIntensities = GetRawIntensities(charge);
                for (int i = 0; i < massCount; i++)
                {
                    rawTotals[i] += rawIntensities[i];
                }
            }
            double total = 0;
            for (int i = 0; i < massCount; i++)
            {
                if (!ExcludedMzs.IsMassExcluded(i))
                {
                    total += rawTotals[i];
                }
            }
            if (total == 0)
            {
                return rawTotals;
            }
            return Scale(rawTotals, 100 / total);
        }

        public IList<double> GetRawIntensities(int charge)
        {
            int massCount = PeptideAnalysis.GetMassCount();
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
            return intensities;
        }

        /// <summary>
        /// Returns the intensities scaled so that the sum of non-excluded
        /// intensities is 100.
        /// These are the values displayed in the grid.
        /// </summary>
        public IList<double> GetScaledIntensities(int charge)
        {
            var rawIntensities = GetRawIntensities(charge);
            double total = 0;
            for (int i = 0; i < rawIntensities.Count; i++)
            {
                if (ExcludedMzs.IsMassExcluded(i))
                {
                    continue;
                }
                total += rawIntensities[i];
            }
            if (total == 0)
            {
                return rawIntensities;
            }
            return Scale(rawIntensities, 100 / total);
        }

        public Dictionary<int, IList<double>> GetScaledIntensities()
        {
            var result = new Dictionary<int, IList<double>>();
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
            {
                result.Add(charge, GetScaledIntensities(charge));
            }
            return result;
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
    }
}