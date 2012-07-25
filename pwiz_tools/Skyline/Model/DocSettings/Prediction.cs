/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    public interface IRegressionFunction
    {
        double GetY(double x);
    }

    /// <summary>
    /// Describes a slope and intercept for converting from a
    /// hydrophobicity factor to a predicted retention time in minutes.
    /// </summary>
    [XmlRoot("predict_retention_time")]
    public sealed class RetentionTimeRegression : XmlNamedElement
    {
        public static double? GetRetentionTimeDisplay(double? rt)
        {
            if (!rt.HasValue)
                return null;
            return Math.Round(rt.Value, 2);
        }

        private RetentionScoreCalculatorSpec _calculator;

        // Support for auto-calculate regression
        private ImmutableSortedList<int, RegressionLine> _listFileIdToConversion;
        // Peptide standards used to derive the above conversions
        private ImmutableDictionary<int, PeptideDocNode> _dictStandardPeptides;

        private ReadOnlyCollection<MeasuredRetentionTime> _peptidesTimes;
       
        public RetentionTimeRegression(string name, RetentionScoreCalculatorSpec calculator,
                                       double? slope, double? intercept, double window,
                                       IList<MeasuredRetentionTime> peptidesTimes)
            : base(name)
        {
            TimeWindow = window;
            if (slope.HasValue && intercept.HasValue)
                Conversion = new RegressionLineElement(slope.Value, intercept.Value);
            else if (slope.HasValue || intercept.HasValue)
                throw new InvalidDataException("Slope and intercept must both have values or both not have values");
            PeptideTimes = peptidesTimes;

            _calculator = calculator;
            
            Validate();
        }

        public RetentionScoreCalculatorSpec Calculator
        {
            get { return _calculator; }
            private set { _calculator = value; }
        }

        public double TimeWindow { get; private set; }

        public RegressionLineElement Conversion { get; private set; }

        public bool IsUsable { get { return Conversion != null && Calculator.IsUsable; } }

        public bool IsAutoCalculated { get { return Conversion == null || _listFileIdToConversion != null; } }

        public IList<MeasuredRetentionTime> PeptideTimes
        {
            get { return _peptidesTimes; }
            private set { _peptidesTimes = MakeReadOnly(value); }
        }

        #region Property change methods

        public RetentionTimeRegression ChangeCalculator(RetentionScoreCalculatorSpec prop)
        {
            return ChangeProp(ImClone(this), im => im.Calculator = prop);
        }

        public RetentionTimeRegression ChangeTimeWindow(double prop)
        {
            return ChangeProp(ImClone(this), im => im.TimeWindow = prop);
        }

        public RetentionTimeRegression ChangeEquations(RegressionLineElement conversion,
                                                       IEnumerable<KeyValuePair<int, RegressionLine>> fileIdToConversions,
                                                       IDictionary<int, PeptideDocNode> dictStandardPeptides)
        {
            return ChangeProp(ImClone(this), im =>
                    {
                        im.Conversion = conversion;
                        im._listFileIdToConversion = ImmutableSortedList.FromValues(fileIdToConversions);
                        im._dictStandardPeptides = MakeReadOnly(dictStandardPeptides);
                    });
        }

        public RetentionTimeRegression ClearEquations()
        {
            if (Conversion == null)
                return this;

            return ChangeProp(ImClone(this), im =>
                    {
                        im.Conversion = null;
                        im._listFileIdToConversion = null;
                        im._dictStandardPeptides = null;
                    });
        }

        #endregion

        public double? GetRetentionTime(string seq)
        {
            return GetRetentionTime(seq, Conversion);
        }

        public double? GetRetentionTime(string seq, ChromFileInfoId fileId)
        {
            return GetRetentionTime(seq, GetConversion(fileId));
        }

        private double? GetRetentionTime(string seq, IRegressionFunction conversion)
        {
            double? score = Calculator.ScoreSequence(seq);
            if (score.HasValue)
                return GetRetentionTime(score.Value, conversion);
            return null;
        }

        public double? GetRetentionTime(double score)
        {
            return GetRetentionTime(score, Conversion);
        }

        public double? GetRetentionTime(double score, ChromFileInfoId fileId)
        {
            return GetRetentionTime(score, GetConversion(fileId));
        }

        private static double? GetRetentionTime(double score, IRegressionFunction conversion)
        {
            // CONSIDER: Return the full value?
            return conversion != null
                       ? GetRetentionTimeDisplay(conversion.GetY(score))
                       : null;
        }

        private IRegressionFunction GetConversion(ChromFileInfoId fileId)
        {
            IRegressionFunction conversion = Conversion;
            if (fileId != null && _listFileIdToConversion != null)
            {
                int iConversion = _listFileIdToConversion.BinarySearch(fileId.GlobalIndex, true);
                conversion = iConversion >= 0
                                 ? _listFileIdToConversion[iConversion].Value
                                 : null;
            }
            return conversion;
        }

        public bool IsAutoCalcRequired(SrmDocument document, SrmDocument previous)
        {
            // Any time there is no regression information, an auto-calc is required
            // unless the document has no results
            if (Conversion == null)
            {
                if (!document.Settings.HasResults)
                    return false;

                // If prediction settings have change, then do auto-recalc
                if (previous == null || !ReferenceEquals(this,
                        previous.Settings.PeptideSettings.Prediction.RetentionTime))
                    return true;

                // Otherwise, only if any of the transition groups or their results
                // have changed.  This is important to avoid an infinite loop when
                // not enough information is present to actually calculate the Conversion
                // parameter.
                var enumPrevious = previous.TransitionGroups.GetEnumerator();
                foreach (var nodeGroup in document.TransitionGroups)
                {
                    if (!enumPrevious.MoveNext())
                        return true;
                    var nodeGroupPrevious = enumPrevious.Current;
                    if (nodeGroupPrevious == null)
                        return true;
                    if (!ReferenceEquals(nodeGroup.Id, nodeGroupPrevious.Id) ||
                        !ReferenceEquals(nodeGroup.Results, nodeGroupPrevious.Results))
                        return true;
                }
                return enumPrevious.MoveNext();
            }

            // If there is a documentwide regression, but no per-file information
            // then no auto-calc is required.
            if (_listFileIdToConversion == null || _dictStandardPeptides == null)
                return false;

            // If any of the standard peptides do not match exactly, then auto-calc
            // is required.
            int countMatching = 0;
            foreach (var nodePep in document.Peptides)
            {
                PeptideDocNode nodePepStandard;
                if (!_dictStandardPeptides.TryGetValue(nodePep.Peptide.GlobalIndex, out nodePepStandard))
                    continue;
                if (!ReferenceEquals(nodePep, nodePepStandard))
                    return true;
                countMatching++;
            }
            // Or any are missing.
            return countMatching != _dictStandardPeptides.Count;
        }

        /// <summary>
        /// Calculate the correlation statistics for this regression with a set
        /// of peptide measurements.
        /// </summary>
        /// <param name="peptidesTimes">List of peptide-time pairs</param>
        /// <param name="scoreCache">Cached pre-calculated scores for these peptides</param>
        /// <returns>Calculated values for the peptides using this regression</returns>
        public RetentionTimeStatistics CalcStatistics(List<MeasuredRetentionTime> peptidesTimes,
            IDictionary<string, double> scoreCache)
        {
            var listPeptides = new List<string>();
            var listHydroScores = new List<double>();
            var listPredictions = new List<double>();
            var listRetentionTimes = new List<double>();

            bool usableCalc = Calculator.IsUsable;
            foreach (var peptideTime in peptidesTimes)
            {
                string seq = peptideTime.PeptideSequence;
                double score = usableCalc ? ScoreSequence(Calculator, scoreCache, seq) : 0;
                listPeptides.Add(seq);
                listHydroScores.Add(score);
                listPredictions.Add(GetRetentionTime(score) ?? 0);
                listRetentionTimes.Add(peptideTime.RetentionTime);
            }

            Statistics statRT = new Statistics(listRetentionTimes);
            Statistics statScores = new Statistics(listHydroScores);
            double r = statRT.R(statScores);

            return new RetentionTimeStatistics(r, listPeptides, listHydroScores,
                listPredictions, listRetentionTimes);
        }

        // Support for serializing multiple calculator types
        private static readonly IXmlElementHelper<RetentionScoreCalculatorSpec>[] CALCULATOR_HELPERS =
        {
            new XmlElementHelperSuper<RetentionScoreCalculator, RetentionScoreCalculatorSpec>(),
            new XmlElementHelperSuper<RCalcIrt, RetentionScoreCalculatorSpec>(),
        };

        public static IXmlElementHelper<RetentionScoreCalculatorSpec>[] CalculatorXmlHelpers
        {
            get { return CALCULATOR_HELPERS; }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private RetentionTimeRegression()
        {
        }

        private enum ATTR
        {
            calculator,
            time_window
        }

        private enum EL
        {
            regression_rt
        }

        private void Validate()
        {
            // TODO: Fix this hacky way of dealing with the default value.
            if (Conversion == null || TimeWindow + Conversion.Slope + Conversion.Intercept != 0 || Calculator != null)
            {
                if (Calculator == null)
                    throw new InvalidDataException("Retention time regression must specify a sequence to score calculator.");
                if (TimeWindow <= 0)
                    throw new InvalidDataException(string.Format("Invalid negative retention time window {0}.", TimeWindow));
            }
        }

        public static RetentionTimeRegression Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new RetentionTimeRegression());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read start tag attributes
            base.ReadXml(reader);
            string calculatorName = reader.GetAttribute(ATTR.calculator);
            TimeWindow = reader.GetDoubleAttribute(ATTR.time_window);
            // Consume start tag
            reader.ReadStartElement();

            if (!string.IsNullOrEmpty(calculatorName))
                _calculator = new RetentionScoreCalculator(calculatorName);
            // TODO: Fix this hacky way of dealing with the default value.
            else if (reader.IsStartElement("irt_calculator"))
                _calculator = RCalcIrt.Deserialize(reader);

            Conversion = reader.DeserializeElement<RegressionLineElement>(EL.regression_rt);

            // Read all measured retention times
            var list = new List<MeasuredRetentionTime>();
            reader.ReadElements(list);
            PeptideTimes = list.ToArray();

            // Consume end tag
            reader.ReadEndElement();

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.time_window, TimeWindow);

            if (_calculator != null)
            {
                var irtCalc = _calculator as RCalcIrt;
                if (irtCalc != null)
                    writer.WriteElement(irtCalc);
                else
                    writer.WriteAttributeString(ATTR.calculator, _calculator.Name);
            }

            // Write conversion inner-tag, if not auto-convert
            if (!IsAutoCalculated)
                writer.WriteElement(EL.regression_rt, Conversion);

            // Write all measured retention times
            writer.WriteElements(PeptideTimes);
        }

        #endregion

        #region object overrides

        public bool Equals(RetentionTimeRegression obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                   Equals(obj.Calculator, Calculator) &&
                   Equals(obj.Conversion, Conversion) &&
                   obj.TimeWindow == TimeWindow &&
                   ArrayUtil.EqualsDeep(_listFileIdToConversion, _listFileIdToConversion) &&
                   ArrayUtil.EqualsDeep(obj.PeptideTimes, PeptideTimes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as RetentionTimeRegression);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ Calculator.GetHashCode();
                result = (result*397) ^ (Conversion != null ? Conversion.GetHashCode() : 0);
                result = (result*397) ^ TimeWindow.GetHashCode();
                result = (result*397) ^ PeptideTimes.GetHashCodeDeep();
                result = (result*397) ^ (_listFileIdToConversion != null
                                             ? _listFileIdToConversion.GetHashCodeDeep(
                                                 p => (p.Key.GetHashCode()*397) ^ p.Value.GetHashCode())
                                             : 0);
                return result;
            }
        }

        #endregion

        public static RetentionTimeRegression CalcRegression(string name,
                                                             IList<RetentionScoreCalculatorSpec> calculators,
                                                             IList<MeasuredRetentionTime> measuredPeptides,
                                                             out RetentionTimeStatistics statistics)
        {
            RetentionScoreCalculatorSpec s;
            return CalcRegression(name, calculators, measuredPeptides, null, false, out statistics, out s);
        }

        /// <summary>
        /// This function chooses the best calculator (by r value) and returns a regression based on that calculator.
        /// </summary>
        /// <param name="name">Name of the regression</param>
        /// <param name="calculators">An IEnumerable of calculators to choose from (cannot be null)</param>
        /// <param name="measuredPeptides">A List of MeasuredRetentionTime objects to build the regression from</param>
        /// <param name="scoreCache">A RetentionTimeScoreCache to try getting scores from before calculating them</param>
        /// <param name="allPeptides">If true, do not let the calculator pick which peptides to use in the regression</param>
        /// <param name="statistics">Statistics from the regression of the best calculator</param>
        /// <param name="calculatorSpec">The best calculator</param>
        /// <returns></returns>
        public static RetentionTimeRegression CalcRegression(string name,
                                                             IList<RetentionScoreCalculatorSpec> calculators,
                                                             IList<MeasuredRetentionTime> measuredPeptides,
                                                             RetentionTimeScoreCache scoreCache,
                                                             bool allPeptides,
                                                             out RetentionTimeStatistics statistics,
                                                             out RetentionScoreCalculatorSpec calculatorSpec)
        {
            // Get a list of peptide names for use by the calculators to choose their regression peptides
            List<string> listPeptides = measuredPeptides.Select(pep => pep.PeptideSequence).ToList();

            // Set these now so that we can return null on some conditions
            calculatorSpec = calculators.ElementAt(0);
            statistics = null;

            int count = listPeptides.Count;
            if (count == 0)
                return null;

            RetentionScoreCalculatorSpec[] calculatorCandidates = calculators == null ?
                                                                  new RetentionScoreCalculatorSpec[0] : calculators.ToArray();
            int calcs = calculatorCandidates.Length;

            // An array, indexed by calculator, of scores of peptides by each calculator
            List<double>[] peptideScoresByCalc = new List<double>[calcs];
            // An array, indexed by calculator, of the peptides each calculator will use
            List<string>[] calcPeptides = new List<string>[calcs];
            // An array, indexed by calculator, of actual retention times for the peptides in peptideScoresByCalc 
            List<double>[] listRTs = new List<double>[calcs];

            var dictMeasuredPeptides = new Dictionary<string, double>();
            foreach (var measured in measuredPeptides)
            {
                if (!dictMeasuredPeptides.ContainsKey(measured.PeptideSequence))
                    dictMeasuredPeptides.Add(measured.PeptideSequence, measured.RetentionTime);
            }
            var setExcludeCalcs = new HashSet<int>();
            for (int i = 0; i < calcs; i++)
            {
                if (setExcludeCalcs.Contains(i))
                    continue;

                var calc = calculatorCandidates[i];
                if(!calc.IsUsable)
                {
                    setExcludeCalcs.Add(i);
                    continue;
                }
                
                try
                {
                    listRTs[i] = new List<double>();
                    calcPeptides[i] = allPeptides ? listPeptides : calc.ChooseRegressionPeptides(listPeptides).ToList();
                    peptideScoresByCalc[i] = RetentionTimeScoreCache.CalcScores(calc, calcPeptides[i], scoreCache);
                }
                catch (Exception)
                {
                    setExcludeCalcs.Add(i);
                    listRTs[i] = null;
                    calcPeptides[i] = null;
                    peptideScoresByCalc[i] = null;
                    continue;
                }

                foreach(var calcPeptide in calcPeptides[i])
                {
                    listRTs[i].Add(dictMeasuredPeptides[calcPeptide]);
                }
            }
            Statistics[] aStatValues = new Statistics[calcs];
            for (int i = 0; i < calcs; i++)
            {
                if(setExcludeCalcs.Contains(i))
                    continue;

                aStatValues[i] = new Statistics(peptideScoresByCalc[i]);
            }
            double r = double.MinValue;
            RetentionScoreCalculatorSpec calcBest = null;
            Statistics statBest = null;
            List<double> listBest = null;
            int bestCalcIndex = 0;
            Statistics bestCalcStatRT = null;
            for (int i = 0; i < calcs; i++)
            {
                if(setExcludeCalcs.Contains(i))
                    continue;

                Statistics statRT = new Statistics(listRTs[i]);
                Statistics stat = aStatValues[i];
                double rVal = statRT.R(stat);

                // Make sure sets containing unknown scores have very low correlations to keep
                // such scores from ending up in the final regression.
                rVal = !peptideScoresByCalc[i].Contains(calculatorCandidates[i].UnknownScore) ? rVal : 0;
                if (r < rVal)
                {
                    bestCalcIndex = i;
                    r = rVal;
                    statBest = stat;
                    listBest = peptideScoresByCalc[i];
                    calcBest = calculatorCandidates[i];
                    bestCalcStatRT = statRT;
                }
            }

            if (calcBest == null)
                return null;

            calculatorSpec = calcBest;

            double slope = bestCalcStatRT.Slope(statBest);
            double intercept = bestCalcStatRT.Intercept(statBest);

            // Suggest a time window of 4*StdDev (or 2 StdDev on either side of
            // the mean == ~95% of training data).
            Statistics residuals = bestCalcStatRT.Residuals(statBest);
            double window = residuals.StdDev() * 4;
            // At minimum suggest a 0.5 minute window, in case of something wacky
            // like only 2 data points.  The RetentionTimeRegression class will
            // throw on a window of zero.
            if (window < 0.5)
                window = 0.5;

            // Save statistics
            RegressionLine rlBest = new RegressionLine(slope, intercept);
            var listPredicted = listBest.Select(rlBest.GetY).ToList();
            statistics = new RetentionTimeStatistics(r, calcPeptides[bestCalcIndex], listBest, listPredicted, listRTs[bestCalcIndex]);

            // Get MeasuredRetentionTimes for only those peptides chosen by the calculator
            var setBestPeptides = new HashSet<string>();
            foreach (string pep in calcPeptides[bestCalcIndex])
                setBestPeptides.Add(pep);
            var calcMeasuredRts = measuredPeptides.Where(pep => setBestPeptides.Contains(pep.PeptideSequence)).ToArray();
            return new RetentionTimeRegression(name, calcBest, slope, intercept, window, calcMeasuredRts);
        }

        private static double ScoreSequence(IRetentionScoreCalculator calculator,
            IDictionary<string, double> scoreCache, string sequence)
        {
            double score;
            if (scoreCache == null || !scoreCache.TryGetValue(sequence, out score))
                score = calculator.ScoreSequence(sequence) ?? calculator.UnknownScore;
            return score;
        }

        public static RetentionTimeRegression FindThreshold(
                            double threshold,
                            int? precision,
                            IList<MeasuredRetentionTime> measuredPeptides,
                            IList<MeasuredRetentionTime> standardPeptides,
                            IList<MeasuredRetentionTime> variablePeptides,
                            RetentionScoreCalculatorSpec calculator,
                            Func<bool> isCanceled)
        {
            var calculators = new[] {calculator};
            RetentionTimeScoreCache scoreCache = new RetentionTimeScoreCache(calculators, measuredPeptides, null);
            RetentionTimeStatistics statisticsAll;
            var regressionInitial = CalcRegression(NAME_INTERNAL,
                                                  calculators,
                                                  measuredPeptides,
                                                  scoreCache,
                                                  true,
                                                  out statisticsAll,
                                                  out calculator);

            var outIndexes = new HashSet<int>();
            RetentionTimeStatistics statisticsRefined = null;
            return regressionInitial.FindThreshold(threshold,
                                                   precision,
                                                   0,
                                                   measuredPeptides.Count,
                                                   standardPeptides,
                                                   variablePeptides,
                                                   statisticsAll,
                                                   calculator,
                                                   scoreCache,
                                                   isCanceled,
                                                   ref statisticsRefined,
                                                   ref outIndexes);

        }

        public RetentionTimeRegression FindThreshold(
                            double threshold,
                            int? precision,
                            int left,
                            int right,
                            IList<MeasuredRetentionTime> standardPeptides,
                            IList<MeasuredRetentionTime> variablePeptides,
                            RetentionTimeStatistics statistics,
                            RetentionScoreCalculatorSpec calculator,
                            RetentionTimeScoreCache scoreCache,
                            Func<bool> isCanceled,
                            ref RetentionTimeStatistics statisticsResult,
                            ref HashSet<int> outIndexes)
        {
            if (left > right)
            {
                int worstIn = right;
                int bestOut = left;
                if (IsAboveThreshold(statisticsResult.R, threshold, precision))
                {
                    // Add back outliers until below the threshold
                    for (;;)
                    {
                        if (isCanceled())
                            throw new OperationCanceledException();
                        RecalcRegression(bestOut, standardPeptides, variablePeptides, statisticsResult, calculator, scoreCache,
                            ref statisticsResult, ref outIndexes);
                        if (bestOut >= variablePeptides.Count || !IsAboveThreshold(statisticsResult.R, threshold, precision))
                            break;
                        bestOut++;
                    }
                    worstIn = bestOut;
                }

                // Remove values until above the threshold
                for (;;)
                {
                    if (isCanceled())
                        throw new OperationCanceledException();
                    var regression = RecalcRegression(worstIn, standardPeptides, variablePeptides, statisticsResult, calculator, scoreCache,
                        ref statisticsResult, ref outIndexes);
                    // If there are only 2 left, then this is the best we can do and still have
                    // a linear equation.
                    if (worstIn <= 2 || IsAboveThreshold(statisticsResult.R, threshold, precision))
                        return regression;
                    worstIn--;
                }
            }

            // Check for cancelation
            if (isCanceled())
                throw new OperationCanceledException();

            int mid = (left + right) / 2;

            // Rerun the regression
            var regressionNew = RecalcRegression(mid, standardPeptides, variablePeptides, statistics, calculator, scoreCache,
                ref statisticsResult, ref outIndexes);

            // If no regression could be calculated, give up to avoid infinite recursion.
            if (regressionNew == null)
                return this;

            if (IsAboveThreshold(statisticsResult.R, threshold, precision))
            {
                return regressionNew.FindThreshold(threshold, precision, mid + 1, right,
                    standardPeptides, variablePeptides, statisticsResult, calculator, scoreCache, isCanceled,
                    ref statisticsResult, ref outIndexes);
            }

            return regressionNew.FindThreshold(threshold, precision, left, mid - 1,
                standardPeptides, variablePeptides, statisticsResult, calculator, scoreCache, isCanceled,
                ref statisticsResult, ref outIndexes);
        }

        private RetentionTimeRegression RecalcRegression(int mid,
                    IEnumerable<MeasuredRetentionTime> requiredPeptides,
                    IList<MeasuredRetentionTime> variablePeptides,
                    RetentionTimeStatistics statistics,
                    RetentionScoreCalculatorSpec calculator,
                    RetentionTimeScoreCache scoreCache,
                    ref RetentionTimeStatistics statisticsResult,
                    ref HashSet<int> outIndexes)
        {
            // Create list of deltas between predicted and measured times
            var listTimes = statistics.ListRetentionTimes;
            var listPredictions = statistics.ListPredictions;
            var listHydroScores = statistics.ListHydroScores;
            var listDeltas = new List<DeltaIndex>();
            int iNextStat = 0;
            double unknownScore = Calculator.UnknownScore;
            for (int i = 0; i < variablePeptides.Count; i++)
            {
                double delta;
                if (variablePeptides[i].RetentionTime == 0)
                    delta = double.MaxValue;    // Make sure zero times are always outliers
                else if (!outIndexes.Contains(i) && iNextStat < listPredictions.Count)
                {
                    delta = listHydroScores[iNextStat] != unknownScore
                                ? Math.Abs(listPredictions[iNextStat] - listTimes[iNextStat])
                                : double.MaxValue;
                    iNextStat++;
                }
                else
                {
                    // Recalculate values for the indexes that were not used to generate
                    // the current regression.
                    var peptideTime = variablePeptides[i];
                    double score = scoreCache.CalcScore(Calculator, peptideTime.PeptideSequence);
                    delta = double.MaxValue;
                    if (score != unknownScore)
                    {
                        double? predictedTime = GetRetentionTime(score);
                        if (predictedTime.HasValue)
                            delta = Math.Abs(predictedTime.Value - peptideTime.RetentionTime);
                    }
                }
                listDeltas.Add(new DeltaIndex(delta, i));
            }

            // Sort descending
            listDeltas.Sort();

            // Remove points with the highest deltas above mid
            outIndexes = new HashSet<int>();
            int countOut = variablePeptides.Count - mid - 1;
            for (int i = 0; i < countOut; i++)
            {
                outIndexes.Add(listDeltas[i].Index);
            }
            var peptidesTimesTry = new List<MeasuredRetentionTime>(variablePeptides.Count);
            for (int i = 0; i < variablePeptides.Count; i++)
            {
                if (outIndexes.Contains(i))
                    continue;
                peptidesTimesTry.Add(variablePeptides[i]);
            }

            peptidesTimesTry.AddRange(requiredPeptides);

            RetentionScoreCalculatorSpec s;
            return CalcRegression(Name, new[] { calculator }, peptidesTimesTry, scoreCache, true,
                                      out statisticsResult, out s);
        }

        public static int ThresholdPrecision { get { return 4; } }

        public static bool IsAboveThreshold(double value, double threshold)
        {
            return IsAboveThreshold(value, threshold, null);
        }

        public static bool IsAboveThreshold(double value, double threshold, int? precision)
        {
            return Math.Round(value, precision ?? ThresholdPrecision) >= threshold;
        }

        public const string SSRCALC_300_A = "SSRCalc 3.0 (300A)";
        public const string SSRCALC_100_A = "SSRCalc 3.0 (100A)";

        public static IRetentionScoreCalculator GetCalculatorByName(string calcName)
        {
            switch (calcName)
            {
                case SSRCALC_300_A:
                    return new SSRCalc3(SSRCALC_300_A, SSRCalc3.Column.A300);
                case SSRCALC_100_A:
                    return new SSRCalc3(SSRCALC_100_A, SSRCalc3.Column.A100);
            }
            return null;
        }

        public bool SamePeptides(RetentionTimeRegression rtRegressionNew)
        {
            if (_dictStandardPeptides == null && rtRegressionNew._dictStandardPeptides == null)
                return true;
            if (_dictStandardPeptides == null || rtRegressionNew._dictStandardPeptides == null)
                return false;
            if (_dictStandardPeptides.Count != rtRegressionNew._dictStandardPeptides.Count)
                return false;
            foreach (var idPeptide in _dictStandardPeptides)
            {
                PeptideDocNode nodePep;
                if (!rtRegressionNew._dictStandardPeptides.TryGetValue(idPeptide.Key, out nodePep))
                    return false;
                if (!ReferenceEquals(nodePep, idPeptide.Value))
                    return false;
            }
            return true;
        }

        private sealed class DeltaIndex : IComparable<DeltaIndex>
        {
            public DeltaIndex(double delta, int index)
            {
                Delta = delta;
                Index = index;
            }

            public double Delta { get; private set; }
            public int Index { get; private set; }
            public int CompareTo(DeltaIndex other)
            {
                if (Delta > other.Delta)
                {
                    return -1;
                }
                if (Delta == other.Delta)
                {
                    return 0;
                }
                return 1;
            }
        }
    }

    public sealed class RetentionTimeScoreCache
    {
        private readonly Dictionary<string, Dictionary<string, double>> _cache =
            new Dictionary<string, Dictionary<string, double>>();

        public RetentionTimeScoreCache(IEnumerable<IRetentionScoreCalculator> calculators,
            IList<MeasuredRetentionTime> peptidesTimes, RetentionTimeScoreCache cachePrevious)
        {
            foreach (var calculator in calculators)
            {
                var cacheCalc = new Dictionary<string, double>();
                _cache.Add(calculator.Name, cacheCalc);
                Dictionary<string, double> cacheCalcPrevious;
                if (cachePrevious == null || !cachePrevious._cache.TryGetValue(calculator.Name, out cacheCalcPrevious))
                    cacheCalcPrevious = null;

                foreach (var peptideTime in peptidesTimes)
                {
                    string seq = peptideTime.PeptideSequence;
                    if (!cacheCalc.ContainsKey(seq))
                        cacheCalc.Add(seq, CalcScore(calculator, seq, cacheCalcPrevious));
                }
            }
        }

        public void RecalculateCalcCache(RetentionScoreCalculatorSpec calculator)
        {
            var calcCache = _cache[calculator.Name];
            if(calcCache != null)
            {
                var newCalcCache = new Dictionary<string, double>();
                foreach (var key in calcCache.Keys)
                {
                    //force recalculation
                    newCalcCache.Add(key, CalcScore(calculator, key, null));
                }

                _cache[calculator.Name] = newCalcCache;
            }
        }

        public double CalcScore(IRetentionScoreCalculator calculator, string peptide)
        {
            Dictionary<string, double> cacheCalc;
            if (!_cache.TryGetValue(calculator.Name, out cacheCalc))
                cacheCalc = null;
            return CalcScore(calculator, peptide, cacheCalc);
        }

        public static List<double> CalcScores(IRetentionScoreCalculator calculator, List<string> peptides,
            RetentionTimeScoreCache scoreCache)
        {
            Dictionary<string, double> cacheCalc;
            if (scoreCache == null || !scoreCache._cache.TryGetValue(calculator.Name, out cacheCalc))
                cacheCalc = null;
            return peptides.ConvertAll(pep => CalcScore(calculator, pep, cacheCalc));
        }

        private static double CalcScore(IRetentionScoreCalculator calculator, string peptide,
            IDictionary<string, double> cacheCalc)
        {
            double score;
            if (cacheCalc == null || !cacheCalc.TryGetValue(peptide, out score))
                score = calculator.ScoreSequence(peptide) ?? calculator.UnknownScore;
            return score;
        }
    }

    public class CalculatorException : Exception
    {
        public CalculatorException(string message)
            : base(message)
        {
        }
    }

    public interface IRetentionScoreCalculator
    {
        string Name { get; }

        double? ScoreSequence(string modifiedSequence);

        double UnknownScore { get; }

        IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides);

        IEnumerable<string> GetStandardPeptides(IEnumerable<string> peptides);
    }

    public abstract class RetentionScoreCalculatorSpec : XmlNamedElement, IRetentionScoreCalculator
    {
        protected RetentionScoreCalculatorSpec(string name)
            : base(name)
        {
        }

        public abstract double? ScoreSequence(string sequence);

        public abstract double UnknownScore { get; }

        public abstract IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides);

        public abstract IEnumerable<string> GetStandardPeptides(IEnumerable<string> peptides);

        public virtual bool IsUsable { get { return true; } }

        public virtual RetentionScoreCalculatorSpec Initialize(IProgressMonitor loadMonitor)
        {
            return this;
        }

        public virtual string PersistencePath { get { return null; } }

        public virtual string PersistMinimized(string pathDestDir, SrmDocument document)
        {
            return null;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected RetentionScoreCalculatorSpec()
        {
        }

        #endregion
    }

    [XmlRoot("retention_score_calculator")]
    public class RetentionScoreCalculator : RetentionScoreCalculatorSpec
    {
        private IRetentionScoreCalculator _impl;

        public RetentionScoreCalculator(string name)
            : base(name)
        {
            Validate();
        }

        public override double? ScoreSequence(string sequence)
        {
            return _impl.ScoreSequence(sequence);
        }

        public override double UnknownScore
        {
            get { return _impl.UnknownScore; }
        }

        public override IEnumerable<string> GetStandardPeptides(IEnumerable<string> peptides)
        {
            return _impl.GetStandardPeptides(peptides);
        }

        private RetentionScoreCalculator()
        {
        }

        public override IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides)
        {
            return _impl.ChooseRegressionPeptides(peptides);
        }

        private void Validate()
        {
            _impl = RetentionTimeRegression.GetCalculatorByName(Name);
            if (_impl == null)
                throw new InvalidDataException(string.Format("The retention time calculator {0} is not valid.", Name));
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            // Consume tag
            reader.Read();

            Validate();
        }

        public static RetentionScoreCalculator Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new RetentionScoreCalculator());
        }
    }

    public sealed class RetentionTimeStatistics
    {
        public RetentionTimeStatistics(double r,
                                        List<string> peptides,
                                        List<double> listHydroScores,
                                        List<double> listPredictions,
                                        List<double> listRetentionTimes)
        {
            R = r;
            Peptides = peptides;
            ListHydroScores = listHydroScores;
            ListPredictions = listPredictions;
            ListRetentionTimes = listRetentionTimes;
        }

        public double R { get; private set; }
        public List<string> Peptides { get; private set; }
        public List<double> ListHydroScores { get; private set; }
        public List<double> ListPredictions { get; private set; }
        public List<double> ListRetentionTimes { get; private set; }

        public IDictionary<string, double> ScoreCache
        {
            get
            {
                var scoreCache = new Dictionary<string, double>();
                for (int i = 0; i < Peptides.Count; i++)
                {
                    string sequence = Peptides[i];
                    if (!scoreCache.ContainsKey(sequence))
                        scoreCache.Add(sequence, ListHydroScores[i]);                    
                }
                return scoreCache;
            }
        }
    }

    [XmlRoot("measured_rt")]
    public sealed class MeasuredRetentionTime : IXmlSerializable
    {
        public MeasuredRetentionTime(string peptideSequence, double retentionTime)
        {
            PeptideSequence = peptideSequence;
            RetentionTime = retentionTime;

            Validate();
        }

        public string PeptideSequence { get; private set; }
        public double RetentionTime { get; private set; }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private MeasuredRetentionTime()
        {
        }

        private enum ATTR
        {
            peptide,
            time
        }

        private void Validate()
        {
            if (!FastaSequence.IsExSequence(PeptideSequence))
                throw new InvalidDataException(string.Format("The sequence {0} is not a valid peptide.", PeptideSequence));
            if (RetentionTime < 0)
                throw new InvalidDataException("Measured retention times must be positive values.");            
        }

        public static MeasuredRetentionTime Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MeasuredRetentionTime());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            PeptideSequence = reader.GetAttribute(ATTR.peptide);
            RetentionTime = reader.GetDoubleAttribute(ATTR.time);

            // Consume tag
            reader.Read();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR.peptide, PeptideSequence);
            writer.WriteAttribute(ATTR.time, RetentionTime);
        }

        #endregion

        #region object overrides

        public bool Equals(MeasuredRetentionTime obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.PeptideSequence, PeptideSequence) && obj.RetentionTime == RetentionTime;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(MeasuredRetentionTime)) return false;
            return Equals((MeasuredRetentionTime)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (PeptideSequence.GetHashCode() * 397) ^ RetentionTime.GetHashCode();
            }
        }

        #endregion
    }

    public enum TimeSource { scan, peak }

    public interface IRetentionTimeProvider
    {
        string Name { get; }

        double? GetRetentionTime(string sequence);

        TimeSource? GetTimeSource(string sequence);

        IEnumerable<MeasuredRetentionTime> PeptideRetentionTimes { get; }
    }

    /// <summary>
    /// Describes slopes and intercepts for use in converting
    /// from a given m/z value to a collision energy for use in
    /// SRM experiments.
    /// </summary>
    [XmlRoot("predict_collision_energy")]
    public sealed class CollisionEnergyRegression : OptimizableRegression
    {
        public const double DEFAULT_STEP_SIZE = 1.0;
        public const int DEFAULT_STEP_COUNT = 5;
        public const double MIN_STEP_SIZE = 0.0001;
        public const double MAX_STEP_SIZE = 100;

        private ChargeRegressionLine[] _conversions;

        public CollisionEnergyRegression(string name,
                                         IEnumerable<ChargeRegressionLine> conversions)
            : this(name, conversions, DEFAULT_STEP_SIZE, DEFAULT_STEP_COUNT)
        {
            
        }
        public CollisionEnergyRegression(string name,
                                         IEnumerable<ChargeRegressionLine> conversions,
                                         double stepSize,
                                         int stepCount)
            : base(name, stepSize, stepCount)
        {
            Conversions = conversions.ToArray();

            Validate();
        }

        public ChargeRegressionLine[] Conversions
        {
            get { return _conversions; }
            set
            {
                _conversions = value;
                Array.Sort(_conversions);
            }
        }

        protected override double DefaultStepSize
        {
            get { return DEFAULT_STEP_SIZE; }
        }

        protected override int DefaultStepCount
        {
            get { return DEFAULT_STEP_COUNT; }
        }

        public double GetCollisionEnergy(int charge, double mz, int step)
        {
            return GetCollisionEnergy(charge, mz) + (step*StepSize);
        }

        public double GetCollisionEnergy(int charge, double mz)
        {
            ChargeRegressionLine rl = GetRegressionLine(charge);
            return (rl == null ? 0 : Math.Round(rl.GetY(mz), 6));
        }

        public ChargeRegressionLine GetRegressionLine(int charge)
        {
            ChargeRegressionLine rl = null;
            int delta = int.MaxValue;

            // These should be very short lists (maximum 5 elements).
            // An array with linear-time look-up is used over a map
            // for ease of persistence to XML.
            foreach (ChargeRegressionLine conversion in Conversions)
            {
                int deltaConv = Math.Abs(charge - conversion.Charge);
                if (deltaConv < delta ||
                    // For equal deltas, choose the larger charge (abitrary decision)
                    (deltaConv == delta && charge < conversion.Charge))
                {
                    rl = conversion;
                    delta = deltaConv;
                    if (delta == 0)
                        break;
                }
            }
            return rl;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private CollisionEnergyRegression()
        {
        }

        private enum EL
        {
            regression_ce,
            // v0.1 value
            regressions
        }

        private void Validate()
        {
            if (_conversions == null || _conversions.Length == 0)
                throw new InvalidDataException("Collision energy regressions require at least one regression function.");

            HashSet<int> seen = new HashSet<int>();
            foreach (ChargeRegressionLine regressionLine in _conversions)
            {
                int charge = regressionLine.Charge;
                if (seen.Contains(charge))
                    throw new InvalidDataException(string.Format("Collision energy regression contains multiple coefficients for charge {0}.", charge));
                seen.Add(charge);
            }
        }

        public static CollisionEnergyRegression Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new CollisionEnergyRegression());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read name attribute
            base.ReadXml(reader);

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                // Consume start tag
                reader.ReadStartElement();
                if (!reader.IsStartElement(EL.regressions))
                    ReadXmlConversions(reader);
                else
                {
                    // Support for v0.1 format
                    reader.ReadStartElement();
                    ReadXmlConversions(reader);
                    reader.ReadEndElement();
                }
                // Consume end tag
                reader.ReadEndElement();
            }

            Validate();
        }

        private void ReadXmlConversions(XmlReader reader)
        {
            var list = new List<ChargeRegressionLine>();
            while (reader.IsStartElement(EL.regression_ce))
                list.Add(ChargeRegressionLine.Deserialize(reader));
            Conversions = list.ToArray();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write name attribute
            base.WriteXml(writer);

            // Write conversion inner tags
            foreach (ChargeRegressionLine line in Conversions)
            {
                writer.WriteStartElement(EL.regression_ce);
                line.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        #endregion

        #region object overrides

        public bool Equals(CollisionEnergyRegression obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && ArrayUtil.EqualsDeep(obj._conversions, _conversions);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as CollisionEnergyRegression);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                {
                    return (base.GetHashCode() * 397) ^ _conversions.GetHashCodeDeep();
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a regression line that applies to a transition with
    /// a specific charge state.
    /// </summary>
    public sealed class ChargeRegressionLine : IXmlSerializable, IComparable<ChargeRegressionLine>, IRegressionFunction
    {
        public ChargeRegressionLine(int charge, double slope, double intercept)
        {
            Charge = charge;
            RegressionLine = new RegressionLine(slope, intercept);
        }

        public int Charge { get; private set; }

        public RegressionLine RegressionLine { get; private set; }

        public double Slope { get { return RegressionLine.Slope; } }

        public double Intercept { get { return RegressionLine.Intercept; } }

        public double GetY(double x)
        {
            return RegressionLine.GetY(x);
        }

        public int CompareTo(ChargeRegressionLine other)
        {
            return Charge - other.Charge;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private ChargeRegressionLine()
        {
        }

        private enum ATTR
        {
            charge
        }

        public static ChargeRegressionLine Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChargeRegressionLine());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            Charge = reader.GetIntAttribute(ATTR.charge, 2);
            RegressionLine = RegressionLine.Deserialize(reader);
            // Consume tag
            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR.charge, Charge);
            RegressionLine.WriteXmlAttributes(writer);
        }

        #endregion

        #region object overrides

        public bool Equals(ChargeRegressionLine obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.RegressionLine, RegressionLine) && obj.Charge == Charge;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(ChargeRegressionLine)) return false;
            return Equals((ChargeRegressionLine)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RegressionLine.GetHashCode() * 397) ^ Charge;
            }
        }

        #endregion
    }

    /// <summary>
    /// Regression calculation for declustering potential, defined separately
    /// from <see cref="NamedRegressionLine"/> to allow an element name to
    /// be associated with it.
    /// </summary>
    [XmlRoot("predict_declustering_potential")]    
    public sealed class DeclusteringPotentialRegression : NamedRegressionLine
    {
        public const double DEFAULT_STEP_SIZE = 1.0;
        public const int DEFAULT_STEP_COUNT = 5;
        public const double MIN_STEP_SIZE = 0.0001;
        public const double MAX_STEP_SIZE = 100;

        public DeclusteringPotentialRegression(string name, double slope, double intercept)
            : this (name, slope, intercept, DEFAULT_STEP_SIZE, DEFAULT_STEP_COUNT)
        {            
        }

        public DeclusteringPotentialRegression(string name, double slope, double intercept, double stepSize, int stepCount)
            : base(name, slope, intercept, stepSize, stepCount)
        {
        }

        public double GetDeclustringPotential(double mz, int step)
        {
            return GetDeclustringPotential(mz) + (step*StepSize);
        }

        public double GetDeclustringPotential(double mz)
        {
            return Math.Round(GetY(mz), 6);
        }

        protected override double DefaultStepSize
        {
            get { return DEFAULT_STEP_SIZE; }
        }

        protected override int DefaultStepCount
        {
            get { return DEFAULT_STEP_COUNT; }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private DeclusteringPotentialRegression()
        {
        }

        public static DeclusteringPotentialRegression Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new DeclusteringPotentialRegression());
        }

        #endregion
    }

    /// <summary>
    /// A regression line with an associated name for use in cases
    /// where only a single set of regression coefficients is necessary.
    /// </summary>
    public abstract class NamedRegressionLine : OptimizableRegression, IRegressionFunction
    {
        protected NamedRegressionLine(string name, double slope, double intercept, double stepSize, int stepCount)
            : base(name, stepSize, stepCount)
        {
            RegressionLine = new RegressionLine(slope, intercept);
        }

        public RegressionLine RegressionLine { get; private set; }

        public double Slope { get { return RegressionLine.Slope; } }

        public double Intercept { get { return RegressionLine.Intercept; } }

        public double GetY(double x)
        {
            return RegressionLine.GetY(x);
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        protected NamedRegressionLine()
        {
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            RegressionLine = RegressionLine.Deserialize(reader);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            RegressionLine.WriteXmlAttributes(writer);
        }

        #endregion

        #region object overrides

        public bool Equals(NamedRegressionLine obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && Equals(obj.RegressionLine, RegressionLine);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as NamedRegressionLine);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                {
                    return (base.GetHashCode()*397) ^ RegressionLine.GetHashCode();
                }
            }
        }

        #endregion
    }

    public abstract class OptimizableRegression : XmlNamedElement
    {
        public const int MIN_RECALC_REGRESSION_VALUES = 4;
        public const int MIN_OPT_STEP_COUNT = 1;
        public const int MAX_OPT_STEP_COUNT = 10;

        protected OptimizableRegression(string name, double stepSize, int stepCount)
            : base(name)
        {
            StepSize = stepSize;
            StepCount = stepCount;
        }

        public double StepSize { get; private set; }

        public int StepCount { get; private set; }

        protected abstract double DefaultStepSize { get; }

        protected abstract int DefaultStepCount { get; }

        #region Property change methods

        public OptimizableRegression ChangeStepSize(double prop)
        {
            return ChangeProp(ImClone(this), im => im.StepSize = prop);
        }

        public OptimizableRegression ChangeStepCount(int prop)
        {
            return ChangeProp(ImClone(this), im => im.StepCount = prop);
        }        

        #endregion

        #region Implementation of IXmlSerializable

        enum ATTR
        {
            step_size,
            step_count
        }

        private void Validate()
        {
            if (StepSize <= 0)
                throw new InvalidDataException(string.Format("The optimization step size {0} is not greater than zero.", StepSize));
            if (MIN_OPT_STEP_COUNT > StepCount || StepCount > MAX_OPT_STEP_COUNT)
                throw new InvalidDataException(string.Format("The number of optimization steps {0} is not between {1} and {2}.",
                    StepCount, MIN_OPT_STEP_COUNT, MAX_OPT_STEP_COUNT));
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected OptimizableRegression()
        {
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);

            StepSize = reader.GetDoubleAttribute(ATTR.step_size, DefaultStepSize);
            StepCount = reader.GetIntAttribute(ATTR.step_count, DefaultStepCount);

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);

            writer.WriteAttribute(ATTR.step_size, StepSize);
            writer.WriteAttribute(ATTR.step_count, StepCount);
        }

        #endregion

        #region object overrides

        public bool Equals(OptimizableRegression other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                other.StepSize == StepSize &&
                other.StepCount == StepCount;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as OptimizableRegression);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ StepSize.GetHashCode();
                result = (result*397) ^ StepCount;
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// The simplest XML element wrapper for an unnamed <see cref="RegressionLine"/>.
    /// </summary>
    public sealed class RegressionLineElement : IXmlSerializable, IRegressionFunction
    {
        private RegressionLine _regressionLine;

        public RegressionLineElement(double slope, double intercept)
        {
            _regressionLine = new RegressionLine(slope, intercept);
        }

        public RegressionLineElement(RegressionLine regressionLine)
        {
            _regressionLine = regressionLine;
        }

        public double Slope { get { return _regressionLine.Slope; } }

        public double Intercept { get { return _regressionLine.Intercept; } }

        public double GetY(double x)
        {
            return _regressionLine.GetY(x);
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private RegressionLineElement()
        {
        }

        public static RegressionLineElement Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new RegressionLineElement());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            _regressionLine = RegressionLine.Deserialize(reader);
            // Consume tag
            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            _regressionLine.WriteXmlAttributes(writer);
        }

        #endregion

        #region object overrides

        public bool Equals(RegressionLineElement obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj._regressionLine, _regressionLine);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (RegressionLineElement)) return false;
            return Equals((RegressionLineElement) obj);
        }

        public override int GetHashCode()
        {
            return _regressionLine.GetHashCode();
        }

        #endregion
    }

    /// <summary>
    /// Slope and intercept pair used to calculate a y-value from
    /// a given x based on a linear regression.
    /// 
    /// The class can read its properties from the attributes on
    /// an XML element, but does not itself represent a full XML
    /// element.  Use one of the wrapper classes for full XML
    /// serialization.
    /// </summary>
    public sealed class RegressionLine : IRegressionFunction
    {
        public RegressionLine(double slope, double intercept)
        {
            Slope = slope;
            Intercept = intercept;
        }

        // XML Serializable properties
        public double Slope { get; private set; }

        public double Intercept { get; private set; }

        /// <summary>
        /// Use the y = m*x + b formula to calculate the desired y
        /// for a given x.
        /// </summary>
        /// <param name="x">Value in x dimension</param>
        /// <returns></returns>
        public double GetY(double x)
        {
            return Slope*x + Intercept;
        }

        #region IXmlSerializable helpers

        /// <summary>
        /// For serialization
        /// </summary>
        private RegressionLine()
        {
        }

        private enum ATTR
        {
            slope,
            intercept
        }

        public static RegressionLine Deserialize(XmlReader reader)
        {
            RegressionLine regressionLine = new RegressionLine();
            regressionLine.ReadXmlAttributes(reader);
            return regressionLine;
        }

        public void ReadXmlAttributes(XmlReader reader)
        {
            Slope = reader.GetDoubleAttribute(ATTR.slope);
            Intercept = reader.GetDoubleAttribute(ATTR.intercept);
        }

        public void WriteXmlAttributes(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.slope, Slope);
            writer.WriteAttribute(ATTR.intercept, Intercept);
        }

        #endregion

        #region object overrides

        public bool Equals(RegressionLine obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.Slope == Slope && obj.Intercept == Intercept;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (RegressionLine)) return false;
            return Equals((RegressionLine) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Slope.GetHashCode()*397) ^ Intercept.GetHashCode();
            }
        }

        #endregion
    }
}