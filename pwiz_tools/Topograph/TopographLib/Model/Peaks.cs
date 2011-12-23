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
using System.Diagnostics;
using System.Linq;
using NHibernate;
using pwiz.Crawdad;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class Peaks : SimpleChildCollection<DbPeptideFileAnalysis, String, DbPeak>
    {
        static private bool _smooth_chromatograms = true;
        private TracerChromatograms _tracerChromatograms;
        public Peaks(PeptideFileAnalysis peptideFileAnalysis, DbPeptideFileAnalysis dbPeptideFileAnalysis) : base(peptideFileAnalysis.Workspace, dbPeptideFileAnalysis)
        {
            PeptideFileAnalysis = peptideFileAnalysis;
        }
        public Peaks(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis.Workspace)
        {
            PeptideFileAnalysis = peptideFileAnalysis;
            SetId(peptideFileAnalysis.Id);
            BasePeakKey = peptideFileAnalysis.Peaks.BasePeakKey;
            IsDirty = true;
            AutoFindPeak = peptideFileAnalysis.Peaks == null ? true : peptideFileAnalysis.Peaks.AutoFindPeak;
        }

        public new bool IsDirty { get; set; }

        public bool AutoFindPeak { get; set; }

        public bool IsCalculated
        {
            get
            {
                return ChildCount != 0 && TracerPercent.HasValue;
            }
        }

        public IntegrationNote IntegrationNote { get; set; }

        protected override IEnumerable<KeyValuePair<String, DbPeak>> GetChildren(DbPeptideFileAnalysis parent)
        {
            foreach (var peak in parent.Peaks)
            {
                yield return new KeyValuePair<string, DbPeak>(peak.Name, peak);
            }
        }

        private TracerChromatograms GetTracerChromatograms()
        {
            if (_tracerChromatograms == null)
            {
                _tracerChromatograms = PeptideFileAnalysis.GetTracerChromatograms(_smooth_chromatograms);
            }
            return _tracerChromatograms;
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
            BasePeakKey = parent.BasePeakName;
            TracerPercent = parent.TracerPercent;
            DeconvolutionScore = parent.DeconvolutionScore;
            PrecursorEnrichment = parent.PrecursorEnrichment;
            PrecursorEnrichmentFormula = parent.PrecursorEnrichmentFormula;
            Turnover = parent.Turnover;
            TurnoverScore = parent.TurnoverScore;
            AutoFindPeak = parent.AutoFindPeak;
            IntegrationNote = IntegrationNote.Parse(parent.IntegrationNote);
        }

        protected override DbPeptideFileAnalysis UpdateDbEntity(ISession session)
        {
            var result = base.UpdateDbEntity(session);
            result.BasePeakName = BasePeakKey;
            result.TracerPercent = TracerPercent;
            result.DeconvolutionScore = DeconvolutionScore;
            result.PrecursorEnrichment = PrecursorEnrichment;
            result.PrecursorEnrichmentFormula = PrecursorEnrichmentFormula;
            result.Turnover = Turnover;
            result.TurnoverScore = TurnoverScore;
            result.AutoFindPeak = AutoFindPeak;
            result.IntegrationNote = IntegrationNote.ToString(IntegrationNote);
            return result;
        }

        public string BasePeakKey { get; set;}
        public DbPeak GetPeak(TracerFormula tracerFormula)
        {
            if (tracerFormula == null)
            {
                return null;
            }
            return GetChild(tracerFormula.ToString());
        }
        public TracerFormula BaseTracerFormula
        {
            get { return TracerFormula.Parse(BasePeakKey); } 
            set { BasePeakKey = value.ToString(); }
        }
        public double? TracerPercent { get; set; }
        public double? DeconvolutionScore { get; set; }
        public double? PrecursorEnrichment { get; set; }
        public string PrecursorEnrichmentFormula { get; set; }
        public double? Turnover { get; set; }
        public double? TurnoverScore { get; set; }
        public double? StartTime
        {
            get
            {
                return ChildCount == 0 ? (double?) null : ListChildren().Min(p => p.StartTime);
            }
        }
        public double? EndTime
        {
            get
            {
                return ChildCount == 0 ? (double?)null : ListChildren().Max(p => p.EndTime);
            }
        }
        private void FindPeak(IEnumerable<Peaks> otherPeaks)
        {
            var bestPeak = FindBestPeak(otherPeaks);
            var tracerChromatograms = GetTracerChromatograms();
            DbPeak basePeak;

            if (bestPeak.Value != null)
            {
                basePeak = MakeBasePeak(bestPeak.Key, tracerChromatograms.Times[bestPeak.Value.StartIndex],
                             tracerChromatograms.Times[bestPeak.Value.EndIndex]);
            }
            else
            {
                basePeak = MakeBasePeak(TracerFormula.Empty, tracerChromatograms.Times[0], tracerChromatograms.Times[0]);
            }

            BasePeakKey = basePeak.TracerFormula.ToString();
            foreach (var tracerFormula in GetTracerChromatograms().ListTracerFormulas())
            {
                if (Equals(tracerFormula, basePeak.TracerFormula))
                {
                    continue;
                }
                var peak = FindMatchingPeak(tracerFormula);
                if (peak == null)
                {
                    continue;
                }
            }
        }

        public DbPeak MakeBasePeak(TracerFormula tracerFormula, double startTime, double endTime)
        {
            var result = MakePeak(tracerFormula, startTime, endTime);
            result.Intercept = 0;
            result.RatioToBase = 1;
            result.RatioToBaseError = 0;
            result.Correlation = 1;
            BasePeakKey = result.Name;
            return result;
        }

        public DbPeak MakeSecondaryPeak(TracerFormula tracerFormula, double startTime, double endTime)
        {
            var result = MakePeak(tracerFormula, startTime, endTime);
            var linearRegression = GetLinearRegression(tracerFormula, startTime, endTime);
            result.Intercept = linearRegression.Intercept;
            result.RatioToBase = linearRegression.Slope;
            result.RatioToBaseError = linearRegression.SlopeError;
            result.Correlation = linearRegression.Correlation;
            return result;
        }

        private DbPeak MakePeak(TracerFormula tracerFormula, double startTime, double endTime)
        {
            var result = new DbPeak
                             {
                                 StartTime = startTime,
                                 EndTime = endTime,
                                 Background = GetTracerChromatograms().GetBackground(tracerFormula, startTime, endTime),
                                 Name = tracerFormula.ToString(),
                                 TotalArea = GetTracerChromatograms().GetArea(tracerFormula, startTime, endTime),
                                 TracerPercent = PeptideFileAnalysis.TurnoverCalculator.GetTracerPercent(tracerFormula),
                       };
            AddChild(result.Name, result);
            return result;
        }

        private DbPeak FindMatchingPeak(TracerFormula targetTracerFormula)
        {
            var basePeak = GetBasePeak();
            double retentionTimeShift = Workspace.GetMaxIsotopeRetentionTimeShift();
            double minStartTime = basePeak.StartTime;
            double maxEndTime = basePeak.EndTime;
            int eluteBefore, eluteAfter;
            RelativeElutionTime(basePeak.TracerFormula, targetTracerFormula, out eluteBefore, out eluteAfter);
            if (eluteBefore > 0)
            {
                minStartTime -= retentionTimeShift; 
            }
            if (eluteAfter > 0)
            {
                maxEndTime += retentionTimeShift;
            }
            return FindMatchingPeak(targetTracerFormula, minStartTime, maxEndTime);
        }

        private void RelativeElutionTime(TracerFormula tracerFormulaStd, TracerFormula tracerFormula, out int eluteBefore, out int eluteAfter)
        {
            eluteBefore = eluteAfter = 0;
            foreach (var tracerDef in PeptideAnalysis.Workspace.GetTracerDefs())
            {
                int diff = tracerFormula.GetElementCount(tracerDef.Name) - tracerFormulaStd.GetElementCount(tracerDef.Name);
                if (diff == 0)
                {
                    continue;
                }
                if (tracerDef.IsotopesEluteEarlier)
                {
                    if (diff > 0)
                    {
                        eluteBefore+=diff;
                    }
                    else
                    {
                        eluteAfter-=diff;
                    }
                }
                if (tracerDef.IsotopesEluteLater)
                {
                    if (diff > 0)
                    {
                        eluteAfter+=diff;
                    }
                    else
                    {
                        eluteBefore-=diff;
                    }
                }
            }
        }
        class LinearRegressionEntry
        {
            public LinearRegressionEntry(double startTime, double endTime, LinearRegression linearRegression)
            {
                StartTime = startTime;
                EndTime = endTime;
                LinearRegression = linearRegression;
            }
            public double StartTime { get; private set;}
            public double EndTime { get; private set;}
            public LinearRegression LinearRegression { get; private set;}
            public double Correlation { get { return LinearRegression.Correlation; } }
            public double GetDistanceTo(DbPeak peak)
            {
                return Math.Max(Math.Abs(StartTime - peak.StartTime), Math.Abs(EndTime - peak.EndTime));
            }
        }

        private DbPeak FindMatchingPeak(TracerFormula targetTracerFormula, double minStartTime, double maxEndTime)
        {
            var tracerChromatograms = GetTracerChromatograms();
            var basePeak = GetBasePeak();
            double width = basePeak.EndTime - basePeak.StartTime;
            var linearRegressions = new List<LinearRegressionEntry>();
            double minCorrelationCoeff = Workspace.GetMinCorrelationCoefficient();
            for (int offset = 0; offset < tracerChromatograms.Times.Count; offset ++)
            {
                double startTime = tracerChromatograms.Times[offset];
                if (startTime < minStartTime)
                {
                    continue;
                }
                double endTime = startTime + width;
                if (endTime > tracerChromatograms.Times[tracerChromatograms.Times.Count - 1] || endTime > maxEndTime)
                {
                    break;
                }

                var linearRegression = GetLinearRegression(targetTracerFormula, startTime, endTime);
                linearRegressions.Add(new LinearRegressionEntry(startTime, endTime, linearRegression));
            }
            var localMaxima = new List<LinearRegressionEntry>(
                linearRegressions.Where(
                    (e, i) => (i == 0 || e.Correlation >= linearRegressions[i - 1].Correlation)
                              &&
                              (i == linearRegressions.Count - 1 ||
                               e.Correlation > linearRegressions[i + 1].Correlation)));
            localMaxima.Sort((e1,e2)=>e1.GetDistanceTo(basePeak).CompareTo(e2.GetDistanceTo(basePeak)));
            var bestEntry = localMaxima.FirstOrDefault(e => e.Correlation > minCorrelationCoeff);
            if (bestEntry == null)
            {
                localMaxima.Sort((e1,e2)=>e1.Correlation.CompareTo(e2.Correlation));
                bestEntry = localMaxima[localMaxima.Count - 1];
            }

            return MakeSecondaryPeak(targetTracerFormula, bestEntry.StartTime, bestEntry.EndTime);
        }

        private LinearRegression GetLinearRegression(TracerFormula targetTracerFormula, double startTime, double endTime)
        {
            var baseStats = new Statistics(GetBaseValues());
            var targetStats = new Statistics(GetValues(targetTracerFormula, startTime, endTime));
            return Statistics.LinearRegressionWithErrorsInBothCoordinates(baseStats, targetStats);
        }

        public double[] GetValues(DbPeak dbPeak)
        {
            return GetValues(TracerFormula.Parse(dbPeak.Name), dbPeak.StartTime, dbPeak.EndTime);
        }

        public double[] GetBaseTimes()
        {
            var tracerChromatograms = GetTracerChromatograms();
            var chromatograms = tracerChromatograms.Chromatograms;
            var basePeak = GetBasePeak();
            int baseStartIndex = chromatograms.IndexFromTime(basePeak.StartTime);
            int baseEndIndex = chromatograms.IndexFromTime(basePeak.EndTime);
            double[] times = new double[baseEndIndex - baseStartIndex + 1];
            for (int i = baseStartIndex; i <= baseEndIndex; i++)
            {
                times[i - baseStartIndex] = chromatograms.Times[i];
            }
            return times;
        }

        private double[] GetBaseValues()
        {
            var basePeak = GetBasePeak();
            return GetValues(TracerFormula.Parse(basePeak.Name), basePeak.StartTime, basePeak.EndTime);
        }

        private double[] GetValues(TracerFormula tracerFormula, double startTime, double endTime)
        {
            var tracerChromatograms = GetTracerChromatograms();
            var chromatograms = tracerChromatograms.Chromatograms;
            var basePeak = GetBasePeak();
            int baseStartIndex = chromatograms.IndexFromTime(basePeak.StartTime);
            int baseEndIndex = chromatograms.IndexFromTime(basePeak.EndTime);
            double[] points = new double[baseEndIndex - baseStartIndex + 1];
            var allPoints = tracerChromatograms.Points[tracerFormula];
            for (int i = baseStartIndex; i <= baseEndIndex; i++)
            {
                double baseTime = chromatograms.TimesArray[i];
                double time = (startTime * (baseTime - basePeak.StartTime) + endTime * (basePeak.EndTime - baseTime)) 
                    / (basePeak.EndTime - basePeak.StartTime);
                double prevTime;
                int index = chromatograms.IndexFromTime(time);
                double nextTime = chromatograms.TimesArray[index];
                if (index > 0)
                {
                    prevTime = chromatograms.TimesArray[index - 1];
                }
                else
                {
                    prevTime = nextTime;
                }
                double value;
                if (nextTime < time || prevTime >= nextTime)
                {
                    value = allPoints[index];
                }
                else
                {
                    double nextValue = allPoints[index];
                    double prevValue = allPoints[index - 1];
                    value = (nextValue*(time - prevTime) + prevValue*(nextTime - time))/(nextTime - prevTime);
                }
                points[i - baseStartIndex] = value;
            }
            return points;
        }

        private KeyValuePair<TracerFormula, CrawdadPeak> FindBestPeak(IEnumerable<Peaks> otherPeaks)
        {
            TracerChromatograms tracerChromatograms = GetTracerChromatograms();
            double bestScore = 0;
            var bestPeak = new KeyValuePair<TracerFormula, CrawdadPeak>();
            double bestDistance = double.MaxValue;
            double firstDetectedTime, lastDetectedTime;
            GetFirstLastTimes(otherPeaks, out firstDetectedTime, out lastDetectedTime);

            foreach (var entry in tracerChromatograms.ListPeaks())
            {
                if (entry.Value.TimeIndex <= entry.Value.StartIndex || entry.Value.TimeIndex >= entry.Value.EndIndex)
                {
                    continue;
                }
                double distance;
                if (firstDetectedTime > lastDetectedTime)
                {
                    distance = 1;
                }
                else
                {
                    double startTime = tracerChromatograms.Chromatograms.TimesArray[entry.Value.StartIndex];
                    double endTime = tracerChromatograms.Chromatograms.TimesArray[entry.Value.EndIndex];
                    if (startTime > lastDetectedTime)
                    {
                        distance = startTime - lastDetectedTime;
                    }
                    else if (endTime < firstDetectedTime)
                    {
                        distance = firstDetectedTime - endTime;
                    }
                    else
                    {
                        distance = 0;
                    }
                }
                double score = tracerChromatograms.GetScore(entry.Value.StartIndex, entry.Value.EndIndex);
                if (distance < bestDistance || distance == bestDistance && score > bestScore)
                {
                    bestPeak = entry;
                    bestScore = score;
                    bestDistance = distance;
                    if (distance == 0)
                    {
                        break;
                    }
                }
            }
            if (bestDistance > 0)
            {
                IntegrationNote = IntegrationNote.PeakNotFoundAtMs2Id;
            }
            else
            {
                IntegrationNote = IntegrationNote.Success;
            }
            return bestPeak;
        }

        public void GetFirstLastTimes(IEnumerable<Peaks> otherPeaks, out double firstDetectedTime, out double lastDetectedTime)
        {
            var tracerChromatograms = GetTracerChromatograms();

            if (PeptideFileAnalysis.FirstDetectedScan.HasValue && PeptideFileAnalysis.LastDetectedScan.HasValue)
            {
                firstDetectedTime =
                    tracerChromatograms.Chromatograms.TimeFromScanIndex(PeptideFileAnalysis.FirstDetectedScan.Value);
                lastDetectedTime =
                    tracerChromatograms.Chromatograms.TimeFromScanIndex(PeptideFileAnalysis.LastDetectedScan.Value);
                return;
            }
            firstDetectedTime = double.MaxValue;
            lastDetectedTime = double.MinValue;
            foreach (var otherPeak in otherPeaks)
            {
                var peptideFileAnalysis = otherPeak.PeptideFileAnalysis;
                if (peptideFileAnalysis.ValidationStatus == ValidationStatus.reject)
                {
                    continue;
                }
                if (!peptideFileAnalysis.FirstDetectedScan.HasValue || !peptideFileAnalysis.LastDetectedScan.HasValue)
                {
                    continue;
                }
                var startTime = otherPeak.StartTime;
                var endTime = otherPeak.EndTime;
                if (!startTime.HasValue || !endTime.HasValue)
                {
                    continue;
                }
                var retentionTimeAlignment = peptideFileAnalysis.MsDataFile.GetRetentionTimeAlignment(PeptideFileAnalysis.MsDataFile);
                if (retentionTimeAlignment != null)
                {
                    firstDetectedTime = Math.Min(firstDetectedTime,
                        retentionTimeAlignment.GetTargetTime(startTime.Value));
                    lastDetectedTime = Math.Max(lastDetectedTime,
                        retentionTimeAlignment.GetTargetTime(endTime.Value));
                }
            }
        }

        public void CalcIntensities(IEnumerable<Peaks> otherPeaks)
        {
            if (GetChildCount() == 0 || AutoFindPeak)
            {
                var origPeaks = PeptideFileAnalysis.Peaks;
                if (AutoFindPeak || origPeaks == null || origPeaks.GetChildCount() == 0)
                {
                    FindPeak(otherPeaks);
                }
                else
                {
                    IntegrationNote = IntegrationNote.Manual;
                    MakeBasePeak(origPeaks.BaseTracerFormula, origPeaks.GetBasePeak().StartTime,
                                 origPeaks.GetBasePeak().EndTime);
                    foreach (var peak in origPeaks.ListChildren())
                    {
                        if (peak.TracerFormula.Equals(BaseTracerFormula))
                        {
                            continue;
                        }
                        MakePeak(peak.TracerFormula, peak.StartTime, peak.EndTime);
                    }
                }
            }
            FinishCalculation();
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
        public IDictionary<TracerFormula, double> ToDictionary()
        {
            var result = new SortedDictionary<TracerFormula, double>();
            foreach (var child in ListChildren())
            {
                result.Add(TracerFormula.Parse(child.Name), child.Area);
            }
            return result;
        }
        public DbPeak GetBasePeak()
        {
            return GetChild(BasePeakKey);
        }
        public Peaks ChangeBasePeak(TracerFormula baseTracerFormula)
        {
            Peaks result = new Peaks(PeptideFileAnalysis)
                               {
                                   BasePeakKey = baseTracerFormula.ToString(),
                                   AutoFindPeak = false,
                               };
            var basePeak = GetChild(baseTracerFormula.ToString());
            var newBasePeak = result.MakeBasePeak(baseTracerFormula, basePeak.StartTime, basePeak.EndTime);
            foreach (var peak in ListChildren())
            {
                if (peak.Name.Equals(newBasePeak.Name))
                {
                    continue;
                }
                var newPeak = result.MakeSecondaryPeak(TracerFormula.Parse(peak.Name), peak.StartTime, peak.EndTime);
            }
            result.FinishCalculation();
            return result;
        }

        public Peaks ChangeTime(TracerFormula tracerFormula, double newStartTime, double newEndTime)
        {
            Peaks result = new Peaks(PeptideFileAnalysis)
                               {
                                   AutoFindPeak = false,
                               };
            DbPeak oldBasePeak = GetBasePeak();
            DbPeak newBasePeak;
            bool isBasePeak;
            if (tracerFormula.Equals(TracerFormula.Parse(BasePeakKey)))
            {
                newBasePeak = result.MakeBasePeak(BaseTracerFormula, newStartTime, newEndTime);
                isBasePeak = true;
            }
            else
            {
                newBasePeak = result.MakeBasePeak(BaseTracerFormula, oldBasePeak.StartTime,
                                                      oldBasePeak.EndTime);
                isBasePeak = false;
            }
            foreach (var peak in ListChildren())
            {
                if (peak.Name.Equals(BasePeakKey))
                {
                    continue;
                }
                DbPeak newPeak;
                if (peak.Name.Equals(tracerFormula.ToString()))
                {
                    if (isBasePeak)
                    {
                        continue;
                    }
                    newPeak = result.MakeSecondaryPeak(tracerFormula, newStartTime, newEndTime);
                }
                else
                {
                    newPeak = result.MakeSecondaryPeak(TracerFormula.Parse(peak.Name), peak.StartTime, peak.EndTime);
                }
            }
            result.FinishCalculation();
            return result;
        }

        public Peaks AutoSizePeak(TracerFormula tracerFormula, AdjustPeaksMode adjustPeaksMode)
        {
            if (tracerFormula.Equals(TracerFormula.Parse(BasePeakKey)))
            {
                return this;
            }
            var result = new Peaks(PeptideFileAnalysis)
                             {
                                 AutoFindPeak = false,
                             };
            var oldBasePeak = GetBasePeak();
            var newBasePeak = result.MakeBasePeak(oldBasePeak.TracerFormula, oldBasePeak.StartTime, oldBasePeak.EndTime);
            foreach (var peak in ListChildren())
            {
                if (peak.Name.Equals(BasePeakKey))
                {
                    continue;
                }
                DbPeak newPeak = null;
                if (peak.Name.Equals(tracerFormula.ToString()))
                {
                    double width = oldBasePeak.EndTime - oldBasePeak.StartTime;
                    double minStartTime, maxEndTime;
                    if (adjustPeaksMode == AdjustPeaksMode.Full)
                    {
                        newPeak = result.FindMatchingPeak(tracerFormula);
                    }
                    else if (adjustPeaksMode == AdjustPeaksMode.Overlapping)
                    {
                        newPeak = result.FindMatchingPeak(tracerFormula, peak.StartTime - width, peak.EndTime + width);
                    }
                    else if (adjustPeaksMode == AdjustPeaksMode.Narrow)
                    {
                        newPeak = result.FindMatchingPeak(tracerFormula, 
                                                          Math.Min(peak.StartTime, peak.EndTime - width),
                                                          Math.Max(peak.EndTime, peak.StartTime + width));
                    }
                }
                if (newPeak == null)
                {
                    newPeak = result.MakeSecondaryPeak(TracerFormula.Parse(peak.Name), peak.StartTime, peak.EndTime);
                }
            }
            result.FinishCalculation();
            return result;
        }

        protected void FinishCalculation()
        {
            double totalArea = ListChildren().Sum(p=>p.Area);
            double tracerPercent = 0;
            double totalScore = 0;
            var tracerChromatograms = GetTracerChromatograms();
            foreach (var peak in ListChildren())
            {
                peak.RelativeAmountValue = totalArea == 0 ? 0 : peak.Area/totalArea;
                tracerPercent += peak.RelativeAmountValue*peak.TracerPercent;
                totalScore += peak.Area*
                              tracerChromatograms.GetScore(
                                  tracerChromatograms.Chromatograms.IndexFromTime(peak.StartTime),
                                  tracerChromatograms.Chromatograms.IndexFromTime(peak.EndTime));

            }
            TracerPercent = tracerPercent;
            DeconvolutionScore = totalArea == 0 ? 0 : totalScore/totalArea;
            IDictionary<TracerFormula, double> bestMatch;
            var peaksDict = ToDictionary();
            double turnover;
            double turnoverScore;
            var precursorEnrichment = PeptideAnalysis.GetTurnoverCalculator().ComputePrecursorEnrichmentAndTurnover(peaksDict, out turnover, out turnoverScore, out bestMatch);
            if (precursorEnrichment != null)
            {
                PrecursorEnrichmentFormula = precursorEnrichment.ToString();
                PrecursorEnrichment = precursorEnrichment.Values.Sum() / 100.0;
                Turnover = turnover;
                TurnoverScore = turnoverScore;
            }
        }

        public double CalcTracerPercentByAreas()
        {
            double totalArea = ListChildren().Sum(p => p.Area);
            double tracerPercent = 0;
            foreach (var peak in ListChildren())
            {
                tracerPercent += peak.Area / totalArea * peak.TracerPercent;
            }
            return tracerPercent;
        }

        public double CalcTracerPercentByRatios()
        {
            double totalRatio = ListChildren().Sum(p => p.RatioToBase);
            double tracerPercent = 0;
            foreach (var peak in ListChildren())
            {
                tracerPercent += peak.RatioToBase/totalRatio*peak.TracerPercent;
            }
            return tracerPercent;
        }

        public void RetentionTimeShift(out double rtShift, out double residuals)
        {
            var lstX = new List<double>();
            var lstY = new List<double>();
            foreach (var peak in ListChildren())
            {
                int eluteBefore, eluteAfter;
                RelativeElutionTime(TracerFormula.Empty, peak.TracerFormula, out eluteBefore, out eluteAfter);
                lstX.Add(eluteAfter - eluteBefore);
                lstY.Add((peak.StartTime + peak.EndTime) / 2);
            }
            var statsX = new Statistics(lstX.ToArray());
            var statsY = new Statistics(lstY.ToArray());
            rtShift = statsY.Slope(statsX);
            residuals = Statistics.StdDevB(statsY, statsX);
        }

        public double AreaUnderCurve
        {
            get
            {
                return ListChildren().Select(peak => peak.TotalArea).Sum();
            }
        }
    }

    public enum AdjustPeaksMode
    {
        Full,
        Overlapping,
        Narrow,
    }
}