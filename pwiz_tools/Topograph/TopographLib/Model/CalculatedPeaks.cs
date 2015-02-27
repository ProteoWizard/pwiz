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
using NHibernate;
using pwiz.Common.Collections;
using pwiz.Crawdad;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class CalculatedPeaks
    {
        [JetBrains.Annotations.UsedImplicitly]
        private static bool _smoothChromatograms = true;
        private readonly IDictionary<TracerFormula, Peak> _peaks = new Dictionary<TracerFormula, Peak>();
        private TracerChromatograms _tracerChromatograms;

        private CalculatedPeaks(PeptideFileAnalysis peptideFileAnalysis)
        {
            PeptideFileAnalysis = peptideFileAnalysis;
            AutoFindPeak = peptideFileAnalysis.AutoFindPeak;
        }

        public PeptideFileAnalysisData.PeakSet ToPeakSetData()
        {
            var peaks = ImmutableList.ValueOf(PeptideFileAnalysis.TurnoverCalculator
                .ListTracerFormulas().Select(formula => _peaks[formula].ToPeakData()));
            return new PeptideFileAnalysisData.PeakSet
                       {
                           DeconvolutionScore = DeconvolutionScore,
                           IntegrationNote = IntegrationNote,
                           PrecursorEnrichment = PrecursorEnrichment,
                           PrecursorEnrichmentFormula = PrecursorEnrichmentFormula,
                           TracerPercent = TracerPercent,
                           Turnover = Turnover,
                           TurnoverScore = TurnoverScore,
                           Peaks = peaks,
                       };
        }

        public bool AutoFindPeak { get; private set; }
        public IntegrationNote IntegrationNote { get; private set; }

        private TracerChromatograms GetTracerChromatograms()
        {
            if (_tracerChromatograms == null)
            {
                _tracerChromatograms = PeptideFileAnalysis.GetTracerChromatograms(_smoothChromatograms);
            }
            return _tracerChromatograms;
        }

        public PeptideFileAnalysis PeptideFileAnalysis { get; private set; }
        public Workspace Workspace { get { return PeptideFileAnalysis.Workspace; } }
        public Peak? GetPeak(TracerFormula tracerFormula)
        {
            Peak peak;
            if (tracerFormula == null || !_peaks.TryGetValue(tracerFormula, out peak))
            {
                return null;
            }
            return peak;
        }
        public IDictionary<TracerFormula, Peak> Peaks { get { return new ImmutableDictionary<TracerFormula, Peak>(_peaks); } }

        public TracerFormula BasePeakKey
        {
            get; private set;
        }
        public double TracerPercent { get; private set; }
        public double DeconvolutionScore { get; private set; }
        public double? PrecursorEnrichment { get; private set; }
        public TracerPercentFormula PrecursorEnrichmentFormula { get; private set; }
        public double? Turnover { get; private set; }
        public double? TurnoverScore { get; private set; }
        public double? StartTime
        {
            get { return _peaks.Count == 0 ? null : (double?) _peaks.Values.Select(peak => peak.StartTime).Min(); }
        }
        public double? EndTime
        {
            get { return _peaks.Count == 0 ? null : (double?) _peaks.Values.Select(peak => peak.EndTime).Max(); }
        }
        private void LookForBestPeak(IEnumerable<CalculatedPeaks> otherPeaks)
        {
            var bestPeak = FindBestPeak(otherPeaks);
            var tracerChromatograms = GetTracerChromatograms();

            if (bestPeak.Value != null)
            {
                MakeBasePeak(bestPeak.Key, tracerChromatograms.Times[bestPeak.Value.StartIndex],
                             tracerChromatograms.Times[bestPeak.Value.EndIndex]);
                BasePeakKey = bestPeak.Key;
            }
            else
            {
                MakeBasePeak(TracerFormula.Empty, tracerChromatograms.Times[0], tracerChromatograms.Times[0]);
                BasePeakKey = TracerFormula.Empty;
            }

            foreach (var tracerFormula in GetTracerChromatograms().ListTracerFormulas())
            {
                if (Equals(tracerFormula, BasePeakKey))
                {
                    continue;
                }
                FindMatchingPeak(tracerFormula);
            }
        }

        private void MakeBasePeak(TracerFormula tracerFormula, double startTime, double endTime)
        {
            var result = MakePeak(tracerFormula, startTime, endTime);
            result.Intercept = 0;
            result.RatioToBase = 1;
            result.RatioToBaseError = 0;
            result.Correlation = 1;
            BasePeakKey = tracerFormula;
            _peaks[tracerFormula] = result;
        }

        private void MakeSecondaryPeak(TracerFormula tracerFormula, double startTime, double endTime)
        {
            var result = MakePeak(tracerFormula, startTime, endTime);
            var linearRegression = GetLinearRegression(tracerFormula, startTime, endTime);
            result.Intercept = linearRegression.Intercept;
            result.RatioToBase = linearRegression.Slope;
            result.RatioToBaseError = linearRegression.SlopeError;
            result.Correlation = linearRegression.Correlation;
            _peaks[tracerFormula] = result;
        }

        private Peak MakePeak(TracerFormula tracerFormula, double startTime, double endTime)
        {
            var result = new Peak
                             {
                                 StartTime = startTime,
                                 EndTime = endTime,
                                 TotalArea = GetTracerChromatograms().GetArea(tracerFormula, startTime, endTime),
                                 TracerPercent = PeptideFileAnalysis.TurnoverCalculator.GetTracerPercent(tracerFormula),
                                 Background = GetTracerChromatograms().GetBackground(tracerFormula, startTime, endTime),
                             };
            return result;
        }

        private Peak FindMatchingPeak(TracerFormula targetTracerFormula)
        {
            var basePeak = _peaks[BasePeakKey];
            double retentionTimeShift = Workspace.GetMaxIsotopeRetentionTimeShift();
            double minStartTime = basePeak.StartTime;
            double maxEndTime = basePeak.EndTime;
            int eluteBefore, eluteAfter;
            RelativeElutionTime(BasePeakKey, targetTracerFormula, out eluteBefore, out eluteAfter);
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
        public static void DeleteResults(ISession session, DbPeptideFileAnalysis dbPeptideFileAnalysis)
        {
            dbPeptideFileAnalysis.PrecursorEnrichment = null;
            dbPeptideFileAnalysis.PrecursorEnrichmentFormula = null;
            dbPeptideFileAnalysis.TracerPercent = null;
            dbPeptideFileAnalysis.Turnover = null;
            dbPeptideFileAnalysis.DeconvolutionScore = null;
            dbPeptideFileAnalysis.IntegrationNote = null;
            session.CreateSQLQuery("DELETE FROM DbPeak WHERE PeptideFileAnalysis = :peptideFileAnalysisId")
                .SetParameter("peptideFileAnalysisId", dbPeptideFileAnalysis.Id)
                .ExecuteUpdate();
        }
        public void Save(ISession session)
        {
            var dbPeptideFileAnalysis = session.Get<DbPeptideFileAnalysis>(PeptideFileAnalysis.Id);
            DeleteResults(session, dbPeptideFileAnalysis);
            dbPeptideFileAnalysis.PrecursorEnrichment = PrecursorEnrichment;
            dbPeptideFileAnalysis.PrecursorEnrichmentFormula = PrecursorEnrichmentFormula == null ? null : PrecursorEnrichmentFormula.ToString();
            dbPeptideFileAnalysis.TracerPercent = TracerPercent;
            dbPeptideFileAnalysis.Turnover = Turnover;
            dbPeptideFileAnalysis.TurnoverScore = TurnoverScore;
            dbPeptideFileAnalysis.DeconvolutionScore = DeconvolutionScore;
            dbPeptideFileAnalysis.IntegrationNote = IntegrationNote.ToString();
            dbPeptideFileAnalysis.PeakCount = Peaks.Count;
            session.Update(dbPeptideFileAnalysis);
            var tracerFormulae = PeptideFileAnalysis.TurnoverCalculator.ListTracerFormulas();
            for (int i = 0; i < tracerFormulae.Count; i++ )
            {
                var peak = _peaks[tracerFormulae[i]];
                var dbPeak = new DbPeak
                {
                    PeptideFileAnalysis = dbPeptideFileAnalysis,
                    PeakIndex = i,
                    Area = peak.Area,
                    StartTime = peak.StartTime,
                    EndTime = peak.EndTime,
                };
                session.Save(dbPeak);
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
            public double GetDistanceTo(Peak peak)
            {
                return Math.Max(Math.Abs(StartTime - peak.StartTime), Math.Abs(EndTime - peak.EndTime));
            }
        }

        private Peak FindMatchingPeak(TracerFormula targetTracerFormula, double minStartTime, double maxEndTime)
        {
            var tracerChromatograms = GetTracerChromatograms();
            var basePeak = _peaks[BasePeakKey];
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

            MakeSecondaryPeak(targetTracerFormula, bestEntry.StartTime, bestEntry.EndTime);
            return _peaks[targetTracerFormula];
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
            var chromatograms = tracerChromatograms.ChromatogramSet;
            var basePeak = _peaks[BasePeakKey];
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
            var basePeak = _peaks[BasePeakKey];
            return GetValues(BasePeakKey, basePeak.StartTime, basePeak.EndTime);
        }

        public double[] GetValues(TracerFormula tracerFormula, double startTime, double endTime)
        {
            var tracerChromatograms = GetTracerChromatograms();
            var chromatograms = tracerChromatograms.ChromatogramSet;
            var basePeak = _peaks[BasePeakKey];
            int baseStartIndex = chromatograms.IndexFromTime(basePeak.StartTime);
            int baseEndIndex = chromatograms.IndexFromTime(basePeak.EndTime);
            double[] points = new double[baseEndIndex - baseStartIndex + 1];
            var allPoints = tracerChromatograms.Points[tracerFormula];
            for (int i = baseStartIndex; i <= baseEndIndex; i++)
            {
                double baseTime = chromatograms.Times[i];
                double time = (startTime * (baseTime - basePeak.StartTime) + endTime * (basePeak.EndTime - baseTime)) 
                    / (basePeak.EndTime - basePeak.StartTime);
                double prevTime;
                int index = chromatograms.IndexFromTime(time);
                double nextTime = chromatograms.Times[index];
                if (index > 0)
                {
                    prevTime = chromatograms.Times[index - 1];
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

        private KeyValuePair<TracerFormula, CrawdadPeak> FindBestPeak(IEnumerable<CalculatedPeaks> otherPeaks)
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
                    double startTime = tracerChromatograms.ChromatogramSet.Times[entry.Value.StartIndex];
                    double endTime = tracerChromatograms.ChromatogramSet.Times[entry.Value.EndIndex];
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

        public void GetFirstLastTimes(IEnumerable<CalculatedPeaks> otherPeaks, out double firstDetectedTime, out double lastDetectedTime)
        {
            var psmTimes = PeptideFileAnalysis.PsmTimes;
            if (null != psmTimes)
            {
                firstDetectedTime = psmTimes.MinTime;
                lastDetectedTime = psmTimes.MaxTime;
                return;
            }
            firstDetectedTime = double.MaxValue;
            lastDetectedTime = double.MinValue;
            foreach (var otherPeak in otherPeaks)
            {
                if (null == otherPeak)
                {
                    continue;
                }
                var peptideFileAnalysis = otherPeak.PeptideFileAnalysis;
                if (peptideFileAnalysis.ValidationStatus == ValidationStatus.reject)
                {
                    continue;
                }
                if (null == peptideFileAnalysis.PsmTimes)
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
                if (!retentionTimeAlignment.IsInvalid)
                {
                    firstDetectedTime = Math.Min(firstDetectedTime,
                        retentionTimeAlignment.GetTargetTime(startTime.Value));
                    lastDetectedTime = Math.Max(lastDetectedTime,
                        retentionTimeAlignment.GetTargetTime(endTime.Value));
                }
            }
        }

        public static CalculatedPeaks Calculate(PeptideFileAnalysis peptideFileAnalysis, IEnumerable<CalculatedPeaks> otherPeaks)
        {
            var tracerFormulae = peptideFileAnalysis.TurnoverCalculator.ListTracerFormulas();
            var result = new CalculatedPeaks(peptideFileAnalysis);
            if (!peptideFileAnalysis.AutoFindPeak)
            {
                var previous = peptideFileAnalysis.PeakData;
                if (previous.Peaks.Count == tracerFormulae.Count)
                {
                    for (int i = 0; i < previous.Peaks.Count; i++)
                    {
                        var peak = previous.Peaks[i];
                        if (result._peaks.Count == 0)
                        {
                            result.MakeBasePeak(tracerFormulae[i], peak.StartTime, peak.EndTime);
                        }
                        else
                        {
                            result.MakeSecondaryPeak(tracerFormulae[i], peak.StartTime, peak.EndTime);
                        }
                    }
                    var maxArea = result._peaks.Values.Max(peak => peak.Area);
                    result = result.ChangeBasePeak(result._peaks.First(kvp => kvp.Value.Area.Equals(maxArea)).Key);
                    result.IntegrationNote = IntegrationNote.Manual;
                }
            }
            if (result._peaks.Count == 0 )
            {
                result.LookForBestPeak(otherPeaks);
            }
            result.FinishCalculation();
            return result;
        }

        public int MassCount 
        { 
            get
            {
                return PeptideFileAnalysis.PeptideAnalysis.GetMassCount();
            }
        }

        public PeptideAnalysis PeptideAnalysis { get { return PeptideFileAnalysis.PeptideAnalysis; } }
        public ExcludedMasses ExcludedMasses { get { return PeptideFileAnalysis.ExcludedMasses; } }
        public IDictionary<TracerFormula, double> ToDictionary()
        {
            var result = new SortedDictionary<TracerFormula, double>();
            foreach (var entry in _peaks)
            {
                result.Add(entry.Key, entry.Value.Area);
            }
            return result;
        }
        public CalculatedPeaks ChangeBasePeak(TracerFormula baseTracerFormula)
        {
            CalculatedPeaks result = new CalculatedPeaks(PeptideFileAnalysis)
                               {
                                   AutoFindPeak = false,
                               };
            var basePeak = _peaks[baseTracerFormula];
            result.MakeBasePeak(baseTracerFormula, basePeak.StartTime, basePeak.EndTime);
            foreach (var entry in _peaks)
            {
                if (entry.Key.Equals(baseTracerFormula))
                {
                    continue;
                }
                result.MakeSecondaryPeak(entry.Key, entry.Value.StartTime, entry.Value.EndTime);
            }
            result.FinishCalculation();
            return result;
        }

        public CalculatedPeaks ChangeTime(TracerFormula tracerFormula, double newStartTime, double newEndTime)
        {
            CalculatedPeaks result = new CalculatedPeaks(PeptideFileAnalysis)
                               {
                                   AutoFindPeak = false,
                               };
            Peak oldBasePeak = _peaks[BasePeakKey];
            bool isBasePeak;
            if (tracerFormula.Equals(BasePeakKey))
            {
                result.MakeBasePeak(BasePeakKey, newStartTime, newEndTime);
                isBasePeak = true;
            }
            else
            {
                result.MakeBasePeak(BasePeakKey, oldBasePeak.StartTime,
                                                               oldBasePeak.EndTime);
                isBasePeak = false;
            }
            foreach (var entry in _peaks)
            {
                if (entry.Key.Equals(tracerFormula))
                {
                    if (isBasePeak)
                    {
                        continue;
                    }
                    result.MakeSecondaryPeak(tracerFormula, newStartTime, newEndTime);
                }
                else
                {
                    result.MakeSecondaryPeak(entry.Key, entry.Value.StartTime, entry.Value.EndTime);
                }
            }
            result.FinishCalculation();
            return result;
        }

        public CalculatedPeaks AutoSizePeak(TracerFormula tracerFormula, AdjustPeaksMode adjustPeaksMode)
        {
            if (tracerFormula.Equals(BasePeakKey))
            {
                return this;
            }
            var result = new CalculatedPeaks(PeptideFileAnalysis)
                             {
                                 AutoFindPeak = false,
                             };
            var oldBasePeak = _peaks[BasePeakKey];
            result.MakeBasePeak(BasePeakKey, oldBasePeak.StartTime, oldBasePeak.EndTime);
            foreach (var entry in _peaks)
            {
                if (entry.Key.Equals(BasePeakKey))
                {
                    continue;
                }
                Peak? newPeak = null;
                if (entry.Key.Equals(tracerFormula))
                {
                    double width = oldBasePeak.EndTime - oldBasePeak.StartTime;
                    if (adjustPeaksMode == AdjustPeaksMode.Full)
                    {
                        newPeak = result.FindMatchingPeak(tracerFormula);
                    }
                    else if (adjustPeaksMode == AdjustPeaksMode.Overlapping)
                    {
                        newPeak = result.FindMatchingPeak(tracerFormula, entry.Value.StartTime - width, entry.Value.EndTime + width);
                    }
                    else if (adjustPeaksMode == AdjustPeaksMode.Narrow)
                    {
                        newPeak = result.FindMatchingPeak(tracerFormula, 
                                                          Math.Min(entry.Value.StartTime, entry.Value.EndTime - width),
                                                          Math.Max(entry.Value.EndTime, entry.Value.StartTime + width));
                    }
                }
                if (newPeak == null)
                {
                    result.MakeSecondaryPeak(entry.Key, entry.Value.StartTime, entry.Value.EndTime);
                }
            }
            result.FinishCalculation();
            return result;
        }

        private void FinishCalculation()
        {
            double totalArea = _peaks.Values.Sum(p=>p.Area);
            double tracerPercent = 0;
            double totalScore = 0;
            var tracerChromatograms = GetTracerChromatograms();
            foreach (var entry in _peaks.ToArray())
            {
                var peak = entry.Value;
                peak.RelativeAmount = totalArea == 0 ? 0 : peak.Area/totalArea;
                tracerPercent += peak.RelativeAmount*peak.TracerPercent;
                totalScore += peak.Area*
                              tracerChromatograms.GetScore(
                                  tracerChromatograms.ChromatogramSet.IndexFromTime(peak.StartTime),
                                  tracerChromatograms.ChromatogramSet.IndexFromTime(peak.EndTime));
                _peaks[entry.Key] = peak;

            }
            TracerPercent = tracerPercent;
            DeconvolutionScore = totalArea == 0 ? 0 : totalScore/totalArea;
            IDictionary<TracerFormula, double> bestMatch;
            var peaksDict = ToDictionary();
            double turnover;
            double turnoverScore;
            PrecursorEnrichmentFormula = PeptideAnalysis.GetTurnoverCalculator().ComputePrecursorEnrichmentAndTurnover(peaksDict, out turnover, out turnoverScore, out bestMatch);
            if (PrecursorEnrichmentFormula != null)
            {
                PrecursorEnrichment = PrecursorEnrichmentFormula.Values.Sum() / 100.0;
                Turnover = turnover;
                TurnoverScore = turnoverScore;
            }
        }

        public double CalcTracerPercentByAreas()
        {
            double totalArea = _peaks.Values.Sum(p => p.Area);
            double tracerPercent = 0;
            foreach (var peak in _peaks.Values)
            {
                tracerPercent += peak.Area / totalArea * peak.TracerPercent;
            }
            return tracerPercent;
        }

        public double CalcTracerPercentByRatios()
        {
            double totalRatio = _peaks.Values.Sum(p => p.RatioToBase);
            double tracerPercent = 0;
            foreach (var peak in _peaks.Values)
            {
                tracerPercent += peak.RatioToBase/totalRatio*peak.TracerPercent;
            }
            return tracerPercent;
        }

        public void RetentionTimeShift(out double rtShift, out double residuals)
        {
            var lstX = new List<double>();
            var lstY = new List<double>();
            foreach (var entry in _peaks)
            {
                int eluteBefore, eluteAfter;
                RelativeElutionTime(TracerFormula.Empty, entry.Key, out eluteBefore, out eluteAfter);
                lstX.Add(eluteAfter - eluteBefore);
                lstY.Add((entry.Value.StartTime + entry.Value.EndTime) / 2);
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
                return _peaks.Values.Select(peak => peak.Area).Sum();
            }
        }

        public struct Peak
        {
            public double StartTime { get; set; }
            public double EndTime { get; set; }
            public double Width { get { return EndTime - StartTime; } }
            public double TotalArea { get; set; }
            public double Area { get { return TotalArea - Background; } }
            public double Background { get; set; }
            public double RatioToBase { get; set; }
            public double RatioToBaseError { get; set; }
            public double Correlation { get; set; }
            public double Intercept { get; set; }
            public double TracerPercent { get; set; }
            public double RelativeAmount { get; set; }
            public PeptideFileAnalysisData.Peak ToPeakData()
            {
                return new PeptideFileAnalysisData.Peak
                           {
                               Area = Area,
                               StartTime = StartTime,
                               EndTime = EndTime,
                           };
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