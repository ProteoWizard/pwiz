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
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    public interface IRegressionFunction
    {
        double Slope { get; }
        double Intercept { get; }
        double GetY(double x);

        /// <summary>
        /// Get a description of the numerical parameters of the regression.
        /// </summary>
        string GetRegressionDescription(double r, double window);

        void GetCurve(RetentionTimeStatistics statistics, out double[] hyrdoScores, out double[] predictions);
    }

    /// <summary>
    /// Describes a slope and intercept for converting from a
    /// hydrophobicity factor to a predicted retention time in minutes.
    /// </summary>
    [XmlRoot("predict_retention_time")]
    public sealed class RetentionTimeRegression : XmlNamedElement
    {
        public const double DEFAULT_WINDOW = 10;

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
        // May be true when the dictionary is null, because peptides were missing
        private bool _isMissingStandardPeptides;

        private ImmutableList<MeasuredRetentionTime> _peptidesTimes;
       
        public RetentionTimeRegression(string name, RetentionScoreCalculatorSpec calculator,
                                       double? slope, double? intercept, double window,
                                       IList<MeasuredRetentionTime> peptidesTimes)
            : this(name, calculator, GetRegressionLine(slope,intercept),window,peptidesTimes)
        {
        }

        private static IRegressionFunction GetRegressionLine(double? slope, double? intercept)
        {
            if (slope.HasValue && intercept.HasValue)
            {
                return new RegressionLineElement(slope.Value, intercept.Value);
            }
            else if (slope.HasValue || intercept.HasValue)
            {
                throw new InvalidDataException(Resources.RetentionTimeRegression_RetentionTimeRegression_Slope_and_intercept_must_both_have_values_or_both_not_have_values);
            }
            return null;
        }

        public RetentionTimeRegression(string name, RetentionScoreCalculatorSpec calculator,
            IRegressionFunction conversion, double window, IList<MeasuredRetentionTime> peptidesTimes) 
            : base(name)
        {
            TimeWindow = window;
            
            PeptideTimes = peptidesTimes;
            Conversion = conversion;

            _calculator = calculator;
            InsufficientCorrelation = false;

            Validate();
        }

        [TrackChildren]
        public RetentionScoreCalculatorSpec Calculator
        {
            get { return _calculator; }
            private set { _calculator = value; }
        }

        [Track]
        public double TimeWindow { get; private set; }

        [TrackChildren]
        public IRegressionFunction Conversion { get; private set; }

        [Track]
        public bool AutoCalcRegression { get { return Conversion == null; } }

        public bool IsUsable { get { return Conversion != null && Calculator.IsUsable; } }

        public bool IsAutoCalculated { get { return Conversion == null || _listFileIdToConversion != null; } }

        public bool IsStandardPeptide(PeptideDocNode nodePep)
        {
            return _dictStandardPeptides != null && _dictStandardPeptides.ContainsKey(nodePep.Peptide.GlobalIndex);
        }

        [Track]
        public IList<MeasuredRetentionTime> PeptideTimes
        {
            get { return _peptidesTimes; }
            private set { _peptidesTimes = MakeReadOnly(value); }
        }

        public bool InsufficientCorrelation { get; private set; }

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

        public RetentionTimeRegression ClearEquations(IDictionary<int, PeptideDocNode> dictStandardPeptides = null)
        {
            if (Conversion == null)
            {
                if (dictStandardPeptides == null && _dictStandardPeptides == null)
                {
                    // If no standard peptides, only return this, if _isMissingStandardPeptides is set,
                    // otherwise fall through so it gets set.
                    if (_isMissingStandardPeptides)
                        return this;
                }
                if (dictStandardPeptides != null && _dictStandardPeptides != null)
                {
                    if (dictStandardPeptides.Count == dictStandardPeptides.Intersect(_dictStandardPeptides).Count())
                    {
                        // If being cleared because of insufficient correlation for all runs, only
                        // return this, if it is already also insufficient
                        if (InsufficientCorrelation)
                            return this;
                    }
                }
            }

            return ChangeProp(ImClone(this), im =>
                    {
                        im.Conversion = null;
                        im._listFileIdToConversion = null;
                        im._isMissingStandardPeptides = (dictStandardPeptides == null);
                        im._dictStandardPeptides = (dictStandardPeptides != null ? MakeReadOnly(dictStandardPeptides) : null);
                        im.InsufficientCorrelation = !im._isMissingStandardPeptides;    // missing peptides supersedes correlation issues
                    });
        }

        public RetentionTimeRegression ForceRecalculate()
        {
            return ChangeProp(ImClone(this), im =>
                    {
                        im.Conversion = null;
                        im._listFileIdToConversion = null;
                        im._isMissingStandardPeptides = false;
                        im._dictStandardPeptides = null;
                        im.InsufficientCorrelation = false;
                    });
        }

        public RetentionTimeRegression ChangeInsufficientCorrelation(bool insufficient)
        {
            return ChangeProp(ImClone(this), im => im.InsufficientCorrelation = insufficient);
        }

        #endregion

        public double? GetRetentionTime(Target seq)
        {
            return GetRetentionTime(seq, Conversion);
        }

        public double? GetRetentionTime(Target seq, ChromFileInfoId fileId)
        {
            return GetRetentionTime(seq, GetConversion(fileId));
        }

        public double? GetRetentionTime(Target seq, IRegressionFunction conversion)
        {
            double? score = Calculator.ScoreSequence(seq);
            if (score.HasValue)
                return GetRetentionTime(score.Value, conversion, false);
            return null;
        }

        public double? GetRetentionTime(double score, bool fullPrecision = false)
        {
            return GetRetentionTime(score, Conversion, fullPrecision);
        }

        public double? GetRetentionTime(double score, ChromFileInfoId fileId, bool fullPrecision = false)
        {
            return GetRetentionTime(score, GetConversion(fileId), fullPrecision);
        }

        private static double? GetRetentionTime(double score, IRegressionFunction conversion, bool fullPrecision)
        {
            if (conversion != null)
            {
                // CONSIDER: Return the full value in more cases?
                double time = conversion.GetY(score);
                if (!fullPrecision)
                    time = GetRetentionTimeDisplay(time).Value;
                return time;
            }

            return null;
        }

        public IRegressionFunction GetConversion(ChromFileInfoId fileId)
        {
            return GetRegressionFunction(fileId) ?? Conversion;
        }

        public RegressionLine GetUnconversion(ChromFileInfoId fileId)
        {
            double slope, intercept;
            var regressionLineFromFile = GetRegressionFunction(fileId);
            var regressionLine = Conversion as RegressionLineElement;
            if (null != regressionLineFromFile)
            {
                slope = regressionLineFromFile.Slope;
                intercept = regressionLineFromFile.Intercept;
            }
            else if (null != regressionLine)
            {
                slope = regressionLine.Slope;
                intercept = regressionLine.Intercept;
            }
            else
            {
                return null;
            }
            return new RegressionLine(1.0/slope, -intercept/slope);
        }

        private RegressionLine GetRegressionFunction(ChromFileInfoId fileId)
        {
            RegressionLine conversion = null;
            if (fileId != null && _listFileIdToConversion != null)
            {
                _listFileIdToConversion.TryGetValue(fileId.GlobalIndex, out conversion);
            }
            return conversion;
        }

        public bool IsAutoCalcRequired(SrmDocument document, SrmDocument previous)
        {
            // Any time there is no regression information, an auto-calc is required
            // unless the document has no results
            if (Conversion == null)
            {
                if ((!document.Settings.HasResults && _dictStandardPeptides != null) || InsufficientCorrelation)
                    return false;

                // If prediction settings have change, then do auto-recalc
                if (previous == null || !ReferenceEquals(this,
                    previous.Settings.PeptideSettings.Prediction.RetentionTime))
                {
                    // If it has already been determined that standard peptides are missing
                    // and no previous document is given, then no auto-recalc is required
                    if (previous == null && _isMissingStandardPeptides)
                        return document.HasAllRetentionTimeStandards(); // Recalc if all standards are now present
                    return true;
                }

                // Otherwise, only if any of the transition groups or their results
                // have changed.  This is important to avoid an infinite loop when
                // not enough information is present to actually calculate the Conversion
                // parameter.
                using (var enumPrevious = previous.PeptideTransitionGroups.GetEnumerator())
                {
                    foreach (var nodeGroup in document.PeptideTransitionGroups)
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
            }

            // If there is a documentwide regression, but no per-file information
            // then no auto-calc is required.
            if (_listFileIdToConversion == null || _dictStandardPeptides == null)
                return false;

            // If any of the standard peptides do not match exactly, then auto-calc
            // is required.
            int countMatching = 0;
            foreach (var nodePep in document.Molecules)
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
        /// <param name="fileId">The file id (optional) with which an iRT regression may be associated</param>
        /// <returns>Calculated values for the peptides using this regression</returns>
        public RetentionTimeStatistics CalcStatistics(List<MeasuredRetentionTime> peptidesTimes,
            IDictionary<Target, double> scoreCache, ChromFileInfoId fileId = null)
        {
            var listPeptides = new List<Target>();
            var listHydroScores = new List<double>();
            var listPredictions = new List<double>();
            var listRetentionTimes = new List<double>();

            bool usableCalc = Calculator.IsUsable;
            foreach (var peptideTime in peptidesTimes)
            {
                var seq = peptideTime.PeptideSequence;
                double score = usableCalc ? ScoreSequence(Calculator, scoreCache, seq) : 0;
                listPeptides.Add(seq);
                listHydroScores.Add(score);
                var predictedRT = fileId != null
                    ? GetRetentionTime(score, fileId, true)
                    : GetRetentionTime(score, true);
                listPredictions.Add(predictedRT ?? 0);
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
            // TODO (MAX): Fix this hacky way of dealing with the default value.
            var conversion = Conversion as RegressionLineElement;
            if (conversion == null || TimeWindow + conversion.Slope + conversion.Intercept != 0 || Calculator != null)
            {
                if (Calculator == null)
                    throw new InvalidDataException(Resources.RetentionTimeRegression_Validate_Retention_time_regression_must_specify_a_sequence_to_score_calculator);
                if (TimeWindow <= 0)
                    throw new InvalidDataException(string.Format(Resources.RetentionTimeRegression_Validate_Invalid_negative_retention_time_window__0__, TimeWindow));
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
            else if (reader.IsStartElement(@"irt_calculator"))
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
                writer.WriteElement(EL.regression_rt, Conversion as RegressionLineElement);

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
                   obj._isMissingStandardPeptides == _isMissingStandardPeptides &&
                   obj.TimeWindow == TimeWindow &&
                   Equals(obj._listFileIdToConversion, _listFileIdToConversion) &&
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
                result = (result*397) ^ _isMissingStandardPeptides.GetHashCode();
                result = (result*397) ^ TimeWindow.GetHashCode();
                result = (result*397) ^ PeptideTimes.GetHashCodeDeep();
                result = (result*397) ^ (_listFileIdToConversion != null
                                             ? _listFileIdToConversion.GetHashCode()
                                                 : 0);
                return result;
            }
        }

        #endregion

        public struct CalculateRegressionSummary
        {
            public CalculatedRegressionInfo Best;
            public CalculatedRegressionInfo[] All;
        }

        public struct CalculatedRegressionInfo
        {
            public RetentionScoreCalculatorSpec Calculator;
            public RetentionTimeRegression Regression;
            public RetentionTimeStatistics Statistics;
            public double RVal;
        }


        public static CalculateRegressionSummary CalcBestRegressionLongOperationRunner(string name,
            IList<RetentionScoreCalculatorSpec> calculators, IList<MeasuredRetentionTime> measuredPeptides,
            RetentionTimeScoreCache scoreCache,
            bool allPeptides,
            RegressionMethodRT regressionMethod,
            CancellationToken token)
        {
            CalculateRegressionSummary result = new CalculateRegressionSummary();
            new LongOperationRunner
            {
                JobTitle = @"Calculating best regression"
            }.Run(longWaitBroker =>
            {
                using (var linkedTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(longWaitBroker.CancellationToken, token))
                {
                    longWaitBroker.SetProgressCheckCancel(0, calculators.Count);
                    result = CalcBestRegressionBackground(name, calculators, measuredPeptides, scoreCache, allPeptides,
                        regressionMethod, linkedTokenSource.Token, longWaitBroker);
                }
            });
            return result;
        }

            /// <summary>
            /// Calculates and returns the best regression, along with all the other regressions.
            /// Although all regression are calculated on a separate thread, this function will wait for them to comeplete
            /// and should therefore also be called on a non-UI thread.
            /// </summary>
            public static CalculateRegressionSummary CalcBestRegressionBackground(string name, IList<RetentionScoreCalculatorSpec> calculators, IList<MeasuredRetentionTime> measuredPeptides,
            RetentionTimeScoreCache scoreCache,
            bool allPeptides,
            RegressionMethodRT regressionMethod,
            CancellationToken token,
            ILongWaitBroker longWaitBroker = null)
        {
            var data = new List<CalculatedRegressionInfo>(calculators.Count);
            var queueWorker = new QueueWorker<RetentionScoreCalculatorSpec>(null, (calculator, i) =>
            {
                var regressionInfo = new CalculatedRegressionInfo { Calculator = calculator };
                regressionInfo.Regression = CalcSingleRegression(name,
                    calculator,
                    measuredPeptides,
                    scoreCache,
                    allPeptides,
                    regressionMethod,
                    out regressionInfo.Statistics,
                    out regressionInfo.RVal,
                    token);

                lock (data)
                {
                    data.Add(regressionInfo);
                    longWaitBroker?.SetProgressCheckCancel(data.Count, calculators.Count);
                }
            });

            var maxThreads = Math.Max(1, Environment.ProcessorCount / 2);
            queueWorker.RunAsync(maxThreads,
                @"RetentionTimeRegression.CalcBestRegressionBackground");
            queueWorker.Add(calculators, true);

            // Pass on exception
            if (queueWorker.Exception != null)
                throw queueWorker.Exception;

            token.ThrowIfCancellationRequested();

            var ordered = data.OrderByDescending(r => Math.Abs(r.RVal)).ToArray();
            return new CalculateRegressionSummary
            {
                All = ordered,
                Best = ordered.FirstOrDefault()
            };
        }

        public static RetentionTimeRegression CalcSingleRegression(string name,
            RetentionScoreCalculatorSpec calculator,
            IList<MeasuredRetentionTime> measuredPeptides,
            RetentionTimeScoreCache scoreCache,
            bool allPeptides,
            RegressionMethodRT regressionMethod,
            out RetentionTimeStatistics statistics,
            out double rVal,
            CancellationToken token)
        {
            // Get a list of peptide names for use by the calculators to choose their regression peptides
            var listPeptides = measuredPeptides.Select(pep => pep.PeptideSequence).ToList();

            // Set these now so that we can return null on some conditions
            statistics = null;
            rVal = double.NaN;

            int count = listPeptides.Count;
            if (count == 0)
                return null;

            // scores of peptides by calculator
            var peptideScores = new List<double>();
            // peptides calculator will use
            var calcPeptides = new List<Target>();
            // actual retention times for the peptides in peptideScores 
            var listRTs = new List<double>();

            var dictMeasuredPeptides = new Dictionary<Target, double>();
            foreach (var measured in measuredPeptides)
            {
                if (!dictMeasuredPeptides.ContainsKey(measured.PeptideSequence))
                    dictMeasuredPeptides.Add(measured.PeptideSequence, measured.RetentionTime);
            }

            if (!calculator.IsUsable)
                return null;

            try
            {
                listRTs = new List<double>();
                int minCount;
                calcPeptides = allPeptides
                    ? listPeptides
                    : calculator.ChooseRegressionPeptides(listPeptides, out minCount).ToList();
                peptideScores = RetentionTimeScoreCache.CalcScores(calculator, calcPeptides, scoreCache, token);
            }
            catch (Exception)
            {
                return null;
            }

            foreach (var calcPeptide in calcPeptides)
                listRTs.Add(dictMeasuredPeptides[calcPeptide]);

            var aStatValues = new Statistics(peptideScores);
            var statRT = new Statistics(listRTs);
            var stat = aStatValues;
            IRegressionFunction regressionFunction;
            switch (regressionMethod)
            {
                case RegressionMethodRT.linear:
                    regressionFunction = new RegressionLineElement(statRT.Slope(stat), statRT.Intercept(stat));
                    break;
                case RegressionMethodRT.kde:
                    var kdeAligner = new KdeAligner();
                    kdeAligner.Train(stat.CopyList(), statRT.CopyList(), token);

                    kdeAligner.GetSmoothedValues(out var xArr, out var ySmoothed);
                    regressionFunction =
                        new PiecewiseLinearRegressionFunction(xArr, ySmoothed, kdeAligner.GetRmsd());
                    stat = new Statistics(ySmoothed);
                    break;
                case RegressionMethodRT.log:
                    regressionFunction = new LogRegression(stat.CopyList(), statRT.CopyList(), true);
                    stat = new Statistics(peptideScores.Select(x => regressionFunction.GetY(x)));
                    break;
                case RegressionMethodRT.loess:
                    regressionFunction = new LoessRegression(stat.CopyList(), statRT.CopyList(), true, token);
                    stat = new Statistics(peptideScores.Select(x => regressionFunction.GetY(x)));
                    break;
                default:
                    return null;
            }

            rVal = statRT.R(stat);

            // Make sure sets containing unknown scores have very low correlations to keep
            // such scores from ending up in the final regression.
            rVal = !peptideScores.Contains(calculator.UnknownScore) ? rVal : 0;

            //double slope = bestCalcStatRT.Slope(statBest);
            //double intercept = bestCalcStatRT.Intercept(statBest);

            // Suggest a time window of 4*StdDev (or 2 StdDev on either side of
            // the mean == ~95% of training data).
            Statistics residuals = statRT.Residuals(stat);
            double window = residuals.StdDev() * 4;
            // At minimum suggest a 0.5 minute window, in case of something wacky
            // like only 2 data points.  The RetentionTimeRegression class will
            // throw on a window of zero.
            if (window < 0.5)
                window = 0.5;

            // Save statistics
            var listPredicted = peptideScores.Select(regressionFunction.GetY).ToList();
            statistics = new RetentionTimeStatistics(rVal, calcPeptides, peptideScores, listPredicted, listRTs);

            // Get MeasuredRetentionTimes for only those peptides chosen by the calculator
            var setBestPeptides = new HashSet<Target>();
            foreach (var pep in calcPeptides)
                setBestPeptides.Add(pep);
            var calcMeasuredRts = measuredPeptides.Where(pep => setBestPeptides.Contains(pep.PeptideSequence)).ToArray();
            return new RetentionTimeRegression(name, calculator, regressionFunction, window, calcMeasuredRts);
        }

        private static double ScoreSequence(IRetentionScoreCalculator calculator,
            IDictionary<Target, double> scoreCache, Target sequence)
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
                            IList<MeasuredRetentionTime> variableTargetPeptides,
                            IList<MeasuredRetentionTime> variableOrigPeptides,
                            RetentionScoreCalculatorSpec calculator,
                            RegressionMethodRT regressionMethod)
        {
            RetentionTimeRegression result = null;
            OperationCanceledException cancelEx = null;

            new LongOperationRunner
            {
                JobTitle = Resources.RetentionTimeRegression_FindThreshold_Finding_threshold
            }.Run(longWaitBroker =>
            {
                var calculators = new[] {calculator};
                var scoreCache = new RetentionTimeScoreCache(calculators, measuredPeptides, null);
                var summary = CalcBestRegressionBackground(NAME_INTERNAL,
                    calculators,
                    measuredPeptides,
                    scoreCache,
                    true,
                    regressionMethod, longWaitBroker.CancellationToken);
                var regressionInitial = summary.Best.Regression;
                var statisticsAll = summary.Best.Statistics;
                calculator = summary.Best.Calculator;

                var outIndexes = new HashSet<int>();
                RetentionTimeStatistics statisticsRefined = null;

                try
                {
                    result = regressionInitial.FindThreshold(threshold,
                        precision,
                        0,
                        measuredPeptides.Count,
                        standardPeptides,
                        variableTargetPeptides,
                        variableOrigPeptides,
                        statisticsAll,
                        calculator,
                        regressionMethod,
                        scoreCache,
                        longWaitBroker.CancellationToken,
                        ref statisticsRefined,
                        ref outIndexes);
                }
                catch (OperationCanceledException ex)
                {
                    cancelEx = ex;
                    throw;
                }
            });

            if (cancelEx != null)
                throw new OperationCanceledException(cancelEx.Message, cancelEx);

            return result;
        }

        public RetentionTimeRegression FindThreshold(
                            double threshold,
                            int? precision,
                            int left,
                            int right,
                            IList<MeasuredRetentionTime> standardPeptides,
                            IList<MeasuredRetentionTime> variableTargetPeptides,
                            IList<MeasuredRetentionTime> variableOrigPeptides,
                            RetentionTimeStatistics statistics,
                            RetentionScoreCalculatorSpec calculator,
                            RegressionMethodRT regressionMethod,
                            RetentionTimeScoreCache scoreCache,
                            CancellationToken token,
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
                        token.ThrowIfCancellationRequested();
                        RecalcRegression(bestOut, standardPeptides, variableTargetPeptides, variableOrigPeptides, statisticsResult, calculator, regressionMethod, scoreCache, token,
                            out statisticsResult, ref outIndexes);
                        if (bestOut >= variableTargetPeptides.Count || statisticsResult == null || !IsAboveThreshold(statisticsResult.R, threshold, precision))
                            break;
                        bestOut++;
                    }
                    worstIn = bestOut;
                }

                // Remove values until above the threshold
                for (;;)
                {
                    token.ThrowIfCancellationRequested();
                    var regression = RecalcRegression(worstIn, standardPeptides, variableTargetPeptides, variableOrigPeptides, statisticsResult, calculator, regressionMethod, scoreCache, token,
                        out statisticsResult, ref outIndexes);
                    // If there are only 2 left, then this is the best we can do and still have
                    // a linear equation.
                    if (worstIn <= 2 || (statisticsResult != null && IsAboveThreshold(statisticsResult.R, threshold, precision)))
                        return regression;
                    worstIn--;
                }
            }

            token.ThrowIfCancellationRequested();

            int mid = (left + right) / 2;

            HashSet<int> outIndexesNew = outIndexes;
            // Rerun the regression
            var regressionNew = RecalcRegression(mid, standardPeptides, variableTargetPeptides, variableOrigPeptides, statistics, calculator, regressionMethod, scoreCache, token,
                out var statisticsNew, ref outIndexesNew);
            // If no regression could be calculated, give up to avoid infinite recursion.
            if (regressionNew == null)
                return this;

            statisticsResult = statisticsNew;
            outIndexes = outIndexesNew;

            if (IsAboveThreshold(statisticsResult.R, threshold, precision))
            {
                return regressionNew.FindThreshold(threshold, precision, mid + 1, right,
                    standardPeptides, variableTargetPeptides, variableOrigPeptides, statisticsResult, calculator, regressionMethod, scoreCache, token,
                    ref statisticsResult, ref outIndexes);
            }

            return regressionNew.FindThreshold(threshold, precision, left, mid - 1,
                standardPeptides, variableTargetPeptides, variableOrigPeptides, statisticsResult, calculator, regressionMethod, scoreCache, token,
                ref statisticsResult, ref outIndexes);
        }

        private RetentionTimeRegression RecalcRegression(int mid,
                    IEnumerable<MeasuredRetentionTime> requiredPeptides,
                    IList<MeasuredRetentionTime> variableTargetPeptides,
                    IList<MeasuredRetentionTime> variableOrigPeptides,
                    RetentionTimeStatistics statistics,
                    RetentionScoreCalculatorSpec calculator,
                    RegressionMethodRT regressionMethod,
                    RetentionTimeScoreCache scoreCache,
                    CancellationToken token,
                    out RetentionTimeStatistics statisticsResult,
                    ref HashSet<int> outIndexes)
        {
            // Create list of deltas between predicted and measured times
            var listTimes = statistics.ListRetentionTimes;
            var listPredictions = statistics.ListPredictions;
            var listHydroScores = statistics.ListHydroScores;
            var listDeltas = new List<DeltaIndex>();
            int iNextStat = 0;
            double unknownScore = Calculator.UnknownScore;
            for (int i = 0; i < variableTargetPeptides.Count; i++)
            {
                double delta;
                if (variableTargetPeptides[i].RetentionTime == 0 || (variableOrigPeptides != null && variableOrigPeptides[i].RetentionTime == 0))
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
                    var peptideTime = variableTargetPeptides[i];
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
            int countOut = variableTargetPeptides.Count - mid - 1;
            for (int i = 0; i < countOut; i++)
            {
                outIndexes.Add(listDeltas[i].Index);
            }
            var peptidesTimesTry = new List<MeasuredRetentionTime>(variableTargetPeptides.Count);
            for (int i = 0; i < variableTargetPeptides.Count; i++)
            {
                if (outIndexes.Contains(i))
                    continue;
                peptidesTimesTry.Add(variableTargetPeptides[i]);
            }

            peptidesTimesTry.AddRange(requiredPeptides);

            return CalcSingleRegression(Name, calculator, peptidesTimesTry, scoreCache, true,regressionMethod,
                                      out statisticsResult, out _, token);
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
        // public const string PROSITRTCALC = "Prosit RT Calc";

        public static IRetentionScoreCalculator GetCalculatorByName(string calcName)
        {
            switch (calcName)
            {
                case SSRCALC_300_A:
                    return new SSRCalc3(SSRCALC_300_A, SSRCalc3.Column.A300);
                case SSRCALC_100_A:
                    return new SSRCalc3(SSRCALC_100_A, SSRCalc3.Column.A100);
                // case PROSITRTCALC:
                //    return new PrositRetentionScoreCalculator(PROSITRTCALC);

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

            private double Delta { get; set; }
            public int Index { get; private set; }
            public int CompareTo(DeltaIndex other)
            {
                return -Delta.CompareTo(other.Delta);
            }
        }
    }

    public sealed class RetentionTimeScoreCache
    {
        private readonly Dictionary<string, Dictionary<Target, double>> _cache =
            new Dictionary<string, Dictionary<Target, double>>();

        public RetentionTimeScoreCache(IEnumerable<IRetentionScoreCalculator> calculators,
            IList<MeasuredRetentionTime> peptidesTimes, RetentionTimeScoreCache cachePrevious)
        {
            foreach (var calculator in calculators)
            {
                var cacheCalc = new Dictionary<Target, double>();
                _cache.Add(calculator.Name, cacheCalc);
                Dictionary<Target, double> cacheCalcPrevious;
                if (cachePrevious == null || !cachePrevious._cache.TryGetValue(calculator.Name, out cacheCalcPrevious))
                    cacheCalcPrevious = null;

                foreach (var peptideTime in peptidesTimes)
                {
                    var seq = peptideTime.PeptideSequence;
                    if (!cacheCalc.ContainsKey(seq))
                        cacheCalc.Add(seq, CalcScore(calculator, seq, cacheCalcPrevious));
                }
            }
        }

        public void RecalculateCalcCache(RetentionScoreCalculatorSpec calculator, CancellationToken token)
        {
            var calcCache = _cache[calculator.Name];
            if(calcCache != null)
            {
                try
                {
                    var newCalcCache = new Dictionary<Target, double>();
                    foreach (var key in calcCache.Keys)
                    {
                        //force recalculation
                        newCalcCache.Add(key, CalcScore(calculator, key, null));
                        ProgressMonitor.CheckCanceled(token);
                    }

                    _cache[calculator.Name] = newCalcCache;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        public double CalcScore(IRetentionScoreCalculator calculator, Target peptide)
        {
            Dictionary<Target, double> cacheCalc;
            _cache.TryGetValue(calculator.Name, out cacheCalc);
            return CalcScore(calculator, peptide, cacheCalc);
        }

        public static List<double> CalcScores(IRetentionScoreCalculator calculator, List<Target> peptides,
            RetentionTimeScoreCache scoreCache, CancellationToken token)
        {
            Dictionary<Target, double> cacheCalc;
            if (scoreCache == null || !scoreCache._cache.TryGetValue(calculator.Name, out cacheCalc))
                cacheCalc = null;

            var result = new List<double>(peptides.Count);
            foreach (var pep in peptides)
            {
                result.Add(CalcScore(calculator, pep, cacheCalc));
                ProgressMonitor.CheckCanceled(token);
            }
            return result;
        }

        private static double CalcScore(IRetentionScoreCalculator calculator, Target peptide,
            IDictionary<Target, double> cacheCalc)
        {
            double score;
            if (cacheCalc == null || !cacheCalc.TryGetValue(peptide, out score))
                score = calculator.ScoreSequence(peptide) ?? calculator.UnknownScore;
            return score;
        }
    }

    public class CalculatorException : Exception
    {
        public CalculatorException(string message) : base(message)
        {
        }

        public CalculatorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public interface IRetentionScoreCalculator
    {
        string Name { get; }

        double? ScoreSequence(Target modifiedSequence);

        double UnknownScore { get; }

        IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount);

        IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides);

        RetentionScoreProvider ScoreProvider { get; }
    }

    public abstract class RetentionScoreCalculatorSpec : XmlNamedElement, IRetentionScoreCalculator
    {
        protected RetentionScoreCalculatorSpec(string name)
            : base(name)
        {
        }

        public abstract double? ScoreSequence(Target sequence);

        public abstract double UnknownScore { get; }

        public abstract IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount);

        public abstract IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides);

        public virtual RetentionScoreProvider ScoreProvider
        {
            get { return null; }
        }

        public virtual bool IsUsable { get { return true; } }

        public virtual RetentionScoreCalculatorSpec Initialize(IProgressMonitor loadMonitor)
        {
            return this;
        }

        [Track(defaultValues:typeof(DefaultValuesNull))]
        public AuditLogPath AuditLogPersistencePath
        {
            get { return AuditLogPath.Create(PersistencePath); }
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

    public interface IRetentionScoreSource
    {
        /// <summary>
        /// Make sure that the current retention scores are still valid
        /// (Not by actually looking at the scores, but by checking
        /// that the state required to calculate the scores has not changed)
        /// If invalid, false should be returned, prompting the score provider
        /// to clear its cache
        /// </summary>
        /// <returns>True if still valid, false if not valid</returns>
        bool ValidateSource();

        /// <summary>
        /// Called if no score for the given target is cached.
        /// Should return the new score or null if no score can be
        /// calculated.
        /// </summary>
        /// <param name="target">The sequence to score</param>
        /// <returns></returns>
        double? GetScore(Target target);
    }

    public class RetentionScoreProvider
    {
        private readonly Dictionary<Target, double> _scoreCache;
        private readonly object _cacheLock = new object();
        private IRetentionScoreSource _scoreSource;

        public RetentionScoreProvider()
        {
            _scoreCache = new Dictionary<Target, double>();
            _scoreSource = null;
        }


        public void Attach(IRetentionScoreSource source)
        {
            _scoreSource = source;
            if (source == null)
            {
                lock (_cacheLock)
                {
                    _scoreCache.Clear();
                }
            }
        }

        public void AddScores(Dictionary<Target, double> scores, bool replace)
        {
            foreach (var kvp in scores)
                AddScore(kvp.Key, kvp.Value, replace);
        }

        public void AddScore(Target target, double score, bool replace)
        {
            lock (_cacheLock)
            {
                if (!replace && _scoreCache.TryGetValue(target, out _))
                    return; // Already exists and don't replace

                // Add new iRT or replace
                _scoreCache[target] = score;
            }
        }

        public double? GetScore(Target target)
        {
            if (_scoreSource == null)
                return null;
            var valid = _scoreSource.ValidateSource();

            lock (_cacheLock)
            {
                if (!valid)
                    _scoreCache.Clear();

                if (_scoreCache.TryGetValue(target, out var iRT))
                    return iRT;
            }

            var score = _scoreSource.GetScore(target);
            if (score != null)
                AddScore(target, score.Value, true);

            return score;
        }

        public class GetScoreEventArgs : EventArgs
        {
            public GetScoreEventArgs(Target target)
            {
                Target = target;
                Score = null;
            }

            public Target Target { get; private set; }
            public double? Score { get; set; }
        }

        public class ValidateEventArgs : EventArgs
        {
            public ValidateEventArgs()
            {
                Valid = false;
            }

            public bool Valid { get; set; }
        }
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

        public override double? ScoreSequence(Target sequence)
        {
            return _impl.ScoreSequence(sequence);
        }

        public override double UnknownScore
        {
            get { return _impl.UnknownScore; }
        }

        public override IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides)
        {
            return _impl.GetStandardPeptides(peptides);
        }

        private RetentionScoreCalculator()
        {
        }

        public override IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount)
        {
            return _impl.ChooseRegressionPeptides(peptides, out minCount);
        }

        private void Validate()
        {
            _impl = RetentionTimeRegression.GetCalculatorByName(Name);
            if (_impl == null)
                throw new InvalidDataException(string.Format(Resources.RetentionScoreCalculator_Validate_The_retention_time_calculator__0__is_not_valid, Name));
        }

        public override RetentionScoreProvider ScoreProvider
        {
            get { return _impl.ScoreProvider; }
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
                                        List<Target> peptides,
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
        public List<Target> Peptides { get; private set; }
        public List<double> ListHydroScores { get; private set; }
        public List<double> ListPredictions { get; private set; }
        public List<double> ListRetentionTimes { get; private set; }

        public IDictionary<Target, double> ScoreCache
        {
            get
            {
                var scoreCache = new Dictionary<Target, double>();
                for (int i = 0; i < Peptides.Count; i++)
                {
                    var sequence = Peptides[i];
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
        /// <summary>
        /// To support using iRT values, which can be negative, in place of measured retention times
        /// </summary>
        private readonly bool _allowNegative;

        public MeasuredRetentionTime(Target peptideSequence, double retentionTime, bool allowNegative = false, bool isStandard = false)
        {
            Assume.IsFalse(peptideSequence.IsEmpty);
            PeptideSequence = peptideSequence;
            RetentionTime = retentionTime;
            IsStandard = isStandard;
            _allowNegative = allowNegative;

            Validate();
        }

        [Track]
        public Target PeptideSequence { get; private set; }
        [Track]
        public double RetentionTime { get; private set; }

        public bool IsStandard { get; private set; }

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
            if (PeptideSequence.IsProteomic && !FastaSequence.IsValidPeptideSequence(PeptideSequence.Sequence))
            {
                throw new InvalidDataException(string.Format(Resources.MeasuredRetentionTime_Validate_The_sequence__0__is_not_a_valid_peptide,
                                                             PeptideSequence));
            }
            if (!_allowNegative && RetentionTime < 0)
                throw new InvalidDataException(Resources.MeasuredRetentionTime_Validate_Measured_retention_times_must_be_positive_values);            
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
            var val = reader.GetAttribute(ATTR.peptide);
            PeptideSequence = Target.FromSerializableString(val);
            RetentionTime = reader.GetDoubleAttribute(ATTR.time);

            // Consume tag
            reader.Read();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR.peptide, PeptideSequence.ToSerializableString());
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

        public override string ToString()
        {
            return string.Format(@"{0}: {1:F01}{2}", PeptideSequence, RetentionTime,
                IsStandard ? @"*" : String.Empty);
        }

        #endregion
    }

    public enum TimeSource { scan, peak }

    public interface IRetentionTimeProvider
    {
        string Name { get; }

        double? GetRetentionTime(Target sequence);

        TimeSource? GetTimeSource(Target sequence);

        IEnumerable<MeasuredRetentionTime> PeptideRetentionTimes { get; }
    }

    /// <summary>
    /// Describes slopes and intercepts for use in converting
    /// from a given m/z value to a collision energy for use in
    /// SRM experiments.
    /// </summary>
    [XmlRoot("predict_collision_energy")]
    public sealed class CollisionEnergyRegression : OptimizableRegression, IAuditLogComparable
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

        [TrackChildren]
        public ChargeRegressionLine[] Conversions
        {
            get { return _conversions; }
            set
            {
                _conversions = value;
                Array.Sort(_conversions);
            }
        }

        public override OptimizationType OptType
        {
            get { return OptimizationType.collision_energy; }
        }

        protected override double DefaultStepSize
        {
            get { return DEFAULT_STEP_SIZE; }
        }

        protected override int DefaultStepCount
        {
            get { return DEFAULT_STEP_COUNT; }
        }

        public double GetCollisionEnergy(Adduct charge, double mz, int step)
        {
            return GetCollisionEnergy(charge, mz) + (step*StepSize);
        }

        public double GetCollisionEnergy(Adduct charge, double mz)
        {
            ChargeRegressionLine rl = GetRegressionLine(charge);
            return (rl == null ? 0 : Math.Round(rl.GetY(mz), 6));
        }

        public ChargeRegressionLine GetRegressionLine(Adduct adduct)
        {
            ChargeRegressionLine rl = null;
            int delta = int.MaxValue;

            var charge = Math.Abs(adduct.AdductCharge);  // CONSIDER(bspratt) is this really how we want to handle neg charges? Or is it more complex?

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
                throw new InvalidDataException(Resources.CollisionEnergyRegression_Validate_Collision_energy_regressions_require_at_least_one_regression_function);

            HashSet<int> seen = new HashSet<int>();
            foreach (ChargeRegressionLine regressionLine in _conversions)
            {
                int charge = regressionLine.Charge;
                if (seen.Contains(charge))
                {
                    throw new InvalidDataException(
                        string.Format(Resources.CollisionEnergyRegression_Validate_Collision_energy_regression_contains_multiple_coefficients_for_charge__0__,
                                      charge));
                }
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

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return CollisionEnergyList.NONE;
        }

        public override string AuditLogText
        {
            get { return ReferenceEquals(this, CollisionEnergyList.NONE) ? LogMessage.NONE : base.AuditLogText; }
        }
    }

    /// <summary>
    /// Represents a regression line that applies to a transition with
    /// a specific charge state.
    /// </summary>
    [XmlRoot("charge_regression_line")]
    public sealed class ChargeRegressionLine : IXmlSerializable, IComparable<ChargeRegressionLine>, IRegressionFunction, IAuditLogObject
    {
        public ChargeRegressionLine(int charge, double slope, double intercept)
        {
            Charge = charge;
            RegressionLine = new RegressionLine(slope, intercept);
        }

        [Track]
        public int Charge { get; private set; }

        public RegressionLine RegressionLine { get; private set; }

        [Track]
        public double Slope { get { return RegressionLine.Slope; } }

        [Track]
        public double Intercept { get { return RegressionLine.Intercept; } }

        public double GetY(double x)
        {
            return RegressionLine.GetY(x);
        }

        public string GetRegressionDescription(double r, double window)
        {
            return RegressionLine.GetRegressionDescription(r,window);
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hyrdoScores, out double[] predictions)
        {
            RegressionLine.GetCurve(statistics,out hyrdoScores, out predictions);
        }

        public double GetX(double y)
        {
            return RegressionLine.GetX(y);
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

        public override string ToString()
        {
            return string.Format(@"Charge: {0} Slope: {1} Intercept: {2}", Charge, RegressionLine.Slope,
                RegressionLine.Intercept);
        }

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

        public string AuditLogText
        {
            get { return Reflector<ChargeRegressionLine>.ToString(this); }
        }

        public bool IsName
        {
            get { return false; }
        }
    }

    /// <summary>
    /// Regression calculation for declustering potential, defined separately
    /// from <see cref="NamedRegressionLine"/> to allow an element name to
    /// be associated with it.
    /// </summary>
    [XmlRoot("predict_declustering_potential")]    
    public sealed class DeclusteringPotentialRegression : NamedRegressionLine, IAuditLogComparable
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

        public override OptimizationType OptType
        {
            get { return OptimizationType.declustering_potential; }
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

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return DeclusterPotentialList.NONE;
        }

        public override string AuditLogText
        {
            get { return ReferenceEquals(this, DeclusterPotentialList.NONE) ? LogMessage.NONE : base.AuditLogText; }
        }
    }

    [XmlRoot("predict_compensation_voltage")]
    public class CompensationVoltageParameters : OptimizableRegression, IAuditLogComparable
    {
        public enum Tuning { none = 0, rough = 1, medium = 2, fine = 3 }

        public const int MIN_STEP_COUNT = 1;
        public const int MAX_STEP_COUNT = 10;

        public virtual Tuning TuneLevel { get { return Tuning.none; } }
        public CompensationVoltageRegressionRough RegressionRough { get; private set; }
        public CompensationVoltageRegressionMedium RegressionMedium { get; private set; }
        public CompensationVoltageRegressionFine RegressionFine { get; private set; }

        [Track]
        public double MinCov { get; protected set; }
        [Track]
        public double MaxCov { get; protected set; }
        [Track]
        public int StepCountRough { get; protected set; }
        [Track]
        public int StepCountMedium { get; protected set; }
        [Track]
        public int StepCountFine { get; protected set; }

        public double StepSizeRough { get { return (MaxCov - MinCov) / Math.Max(1, StepCountRough*2); } }
        public double StepSizeMedium { get { return StepSizeRough / (StepCountMedium + 1); } }
        public double StepSizeFine { get { return StepSizeMedium / (StepCountFine + 1); } }

        public CompensationVoltageParameters(string name, double min, double max, int stepsRough, int stepsMedium, int stepsFine)
            : base(name, -1, -1)
        {
            MinCov = min;
            MaxCov = max;
            StepCountRough = stepsRough;
            StepCountMedium = stepsMedium;
            StepCountFine = stepsFine;
            InitializeSubRegressions();
        }

        public CompensationVoltageParameters(CompensationVoltageParameters other)
            : this(other.Name, other.MinCov, other.MaxCov, other.StepCountRough, other.StepCountMedium, other.StepCountFine)
        {
        }

        protected void InitializeSubRegressions()
        {
            if (TuneLevel.Equals(Tuning.none))
            {
                RegressionRough = new CompensationVoltageRegressionRough(this);
                RegressionMedium = new CompensationVoltageRegressionMedium(this);
                RegressionFine = new CompensationVoltageRegressionFine(this);
            }
        }

        public override OptimizationType OptType { get { return GetOptimizationType(TuneLevel); } }

        [Track]
        public override double StepSize { get { return GetStepSize(TuneLevel); } }

        [Track]
        public override int StepCount
        {
            get { return GetStepCount(TuneLevel); }
            protected set
            {
                switch (TuneLevel)
                {
                    case Tuning.fine:
                        StepCountFine = value;
                        break;
                    case Tuning.medium:
                        StepCountMedium = value;
                        break;
                    case Tuning.rough:
                        StepCountRough = value;
                        break;
                }
            }
        }

        protected override double DefaultStepSize { get { return -1; } }
        protected override int DefaultStepCount { get { return -1; } }

        public static Tuning GetTuneLevel(string tuneLevel)
        {
            if (Equals(tuneLevel, ExportOptimize.COV_FINE))
                return Tuning.fine;
            if (Equals(tuneLevel, ExportOptimize.COV_MEDIUM))
                return Tuning.medium;
            return Equals(tuneLevel, ExportOptimize.COV_ROUGH) ? Tuning.rough : Tuning.none;
        }

        public static OptimizationType GetOptimizationType(Tuning tuneLevel)
        {
            switch (tuneLevel)
            {
                case Tuning.fine:
                    return OptimizationType.compensation_voltage_fine;
                case Tuning.medium:
                    return OptimizationType.compensation_voltage_medium;
                default:
                    return OptimizationType.compensation_voltage_rough;
            }
        }

        public double GetStepSize(Tuning tuneLevel)
        {
            switch (TuneLevel)
            {
                case Tuning.fine:
                    return StepSizeFine;
                case Tuning.medium:
                    return StepSizeMedium;
                default:
                    return StepSizeRough;
            }
        }

        public int GetStepCount(Tuning tuneLevel)
        {
            switch (tuneLevel)
            {
                case Tuning.fine:
                    return StepCountFine;
                case Tuning.medium:
                    return StepCountMedium;
                default:
                    return StepCountRough;
            }
        }

        #region Implementation of IXmlSerializable

        protected enum ATTR
        {
            tune_level,
            min_cov,
            max_cov,
            step_count_rough,
            step_count_medium,
            step_count_fine
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected CompensationVoltageParameters()
        {
        }

        public override void ReadXml(XmlReader reader)
        {
            ReadName(reader);
            MinCov = reader.GetDoubleAttribute(ATTR.min_cov);
            MaxCov = reader.GetDoubleAttribute(ATTR.max_cov);
            StepCountRough = reader.GetIntAttribute(ATTR.step_count_rough);
            StepCountMedium = reader.GetIntAttribute(ATTR.step_count_medium);
            StepCountFine = reader.GetIntAttribute(ATTR.step_count_fine);

            InitializeSubRegressions();
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            WriteName(writer);
            writer.WriteAttribute(ATTR.min_cov, MinCov);
            writer.WriteAttribute(ATTR.max_cov, MaxCov);
            writer.WriteAttribute(ATTR.step_count_rough, StepCountRough);
            writer.WriteAttribute(ATTR.step_count_medium, StepCountMedium);
            writer.WriteAttribute(ATTR.step_count_fine, StepCountFine);
        }

        public static CompensationVoltageParameters Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new CompensationVoltageParameters());
        }

        #endregion

        #region object overrides

        public bool Equals(CompensationVoltageParameters obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                   Equals(obj.TuneLevel, TuneLevel) && Equals(obj.MinCov, MinCov) && Equals(obj.MaxCov, MaxCov) &&
                   Equals(obj.StepCountRough, StepCountRough) && Equals(obj.StepCountMedium, StepCountMedium) &&
                   Equals(obj.StepCountFine, StepCountFine);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return ReferenceEquals(this, obj) || Equals(obj as CompensationVoltageParameters);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ TuneLevel.GetHashCode();
                result = (result*397) ^ MinCov.GetHashCode();
                result = (result*397) ^ MaxCov.GetHashCode();
                result = (result*397) ^ StepCountRough.GetHashCode();
                result = (result*397) ^ StepCountMedium.GetHashCode();
                result = (result*397) ^ StepCountFine.GetHashCode();
                return result;
            }
        }

        #endregion

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return CompensationVoltageList.NONE;
        }

        public override string AuditLogText
        {
            get { return ReferenceEquals(this, CompensationVoltageList.NONE) ? LogMessage.NONE : base.AuditLogText; }
        }
    }

    [XmlRoot("predict_compensation_voltage_rough")]
    public sealed class CompensationVoltageRegressionRough : CompensationVoltageParameters
    {
        public CompensationVoltageRegressionRough(CompensationVoltageParameters parent)
            : base(parent)
        {
        }

        public override Tuning TuneLevel { get { return Tuning.rough; } }

        private CompensationVoltageRegressionRough()
        {
        }

        public new static CompensationVoltageRegressionRough Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new CompensationVoltageRegressionRough());
        }
    }

    [XmlRoot("predict_compensation_voltage_medium")]
    public sealed class CompensationVoltageRegressionMedium : CompensationVoltageParameters
    {
        public CompensationVoltageRegressionMedium(CompensationVoltageParameters parent)
            : base(parent)
        {
        }

        public override Tuning TuneLevel { get { return Tuning.medium; } }

        private CompensationVoltageRegressionMedium()
        {
        }

        public new static CompensationVoltageRegressionMedium Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new CompensationVoltageRegressionMedium());
        }
    }

    [XmlRoot("predict_compensation_voltage_fine")]
    public sealed class CompensationVoltageRegressionFine : CompensationVoltageParameters
    {
        public CompensationVoltageRegressionFine(CompensationVoltageParameters parent)
            : base(parent)
        {
        }

        public override Tuning TuneLevel { get { return Tuning.fine; } }

        private CompensationVoltageRegressionFine()
        {
        }

        public new static CompensationVoltageRegressionFine Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new CompensationVoltageRegressionFine());
        }
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

        [Track]
        public double Slope { get { return RegressionLine.Slope; } }

        [Track]
        public double Intercept { get { return RegressionLine.Intercept; } }

        public double GetY(double x)
        {
            return RegressionLine.GetY(x);
        }

        public string GetRegressionDescription(double r, double window)
        {
            return RegressionLine.GetRegressionDescription(r, window);
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hyrdoScores, out double[] predictions)
        {
            RegressionLine.GetCurve(statistics, out hyrdoScores, out predictions);
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

        private double _stepSize;
        private int _stepCount;

        protected OptimizableRegression(string name, double stepSize, int stepCount)
            : base(name)
        {
            _stepSize = stepSize;
            _stepCount = stepCount;
        }

        public abstract OptimizationType OptType { get; }

        [Track]
        public virtual double StepSize
        {
            get { return _stepSize; }
            protected set { _stepSize = value; }
        }

        [Track]
        public virtual int StepCount
        {
            get { return _stepCount; }
            protected set { _stepCount = value; }
        }

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
                throw new InvalidDataException(string.Format(Resources.OptimizableRegression_Validate_The_optimization_step_size__0__is_not_greater_than_zero, StepSize));
            if (MIN_OPT_STEP_COUNT > StepCount || StepCount > MAX_OPT_STEP_COUNT)
            {
                throw new InvalidDataException(
                    string.Format(Resources.OptimizableRegression_Validate_The_number_of_optimization_steps__0__is_not_between__1__and__2__,
                                  StepCount, MIN_OPT_STEP_COUNT, MAX_OPT_STEP_COUNT));
            }
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected OptimizableRegression()
        {
        }

        protected void ReadName(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
        }

        public override void ReadXml(XmlReader reader)
        {
            ReadName(reader);
            StepSize = reader.GetDoubleAttribute(ATTR.step_size, DefaultStepSize);
            StepCount = reader.GetIntAttribute(ATTR.step_count, DefaultStepCount);

            Validate();
        }

        protected void WriteName(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
        }

        public override void WriteXml(XmlWriter writer)
        {
            WriteName(writer);
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

        [Track]
        public double Slope { get { return _regressionLine.Slope; } }

        [Track]
        public double Intercept { get { return _regressionLine.Intercept; } }

        public double GetY(double x)
        {
            return _regressionLine.GetY(x);
        }

        public string GetRegressionDescription(double r, double window)
        {
            return _regressionLine.GetRegressionDescription(r, window);
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hydroScores, out double[] predictions)
        {
            _regressionLine.GetCurve(statistics, out hydroScores, out predictions);
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



    public sealed class PiecewiseLinearRegressionFunction : IRegressionFunction
    {
        private readonly double[] _xArr;
        private readonly double[] _yPred;
        private readonly double _minX;
        private readonly double _maxX;
        private readonly double _rmsd;

        /// <summary>
        /// Creates a piecewiselinearRegression function
        /// </summary>
        /// <param name="xArr">Array of independent values where two linear piece functions intersect.</param>
        /// <param name="yPred">Corresponding dependent variable values at intersection points</param>
        /// <param name="rmsd"></param>
        public PiecewiseLinearRegressionFunction(double[] xArr, double[] yPred, double rmsd)
        {
            _xArr = xArr;
            _yPred = yPred;
            _minX = xArr[0];
            _maxX = xArr[xArr.Length - 1];
            _rmsd = rmsd;
        }

        public double Slope { get { throw new NotSupportedException(); }}
        public double Intercept { get { throw new NotSupportedException(); }}

        public double RMSD { get { return _rmsd;} }

        public int LinearFunctionsCount { get { return _xArr.Length - 1; } }

        public double GetY(double x)
        {
            if (x <= _minX)
                return _yPred[0];
            if (x >= _maxX)
                return _yPred[_yPred.Length - 1];

            int leftIndex = GetLeftIndex(x, 0, _xArr.Length);
            return Interpolate(x, leftIndex, leftIndex + 1);
        }

        public string GetRegressionDescription(double r, double window)
        {
            // ReSharper disable LocalizableElement
            return string.Format("{0} = {1}\n" + "rmsd =  {2}",
                Resources.PiecewiseLinearRegressionFunction_GetRegressionDescription_piecwise_linear_functions,
                _xArr.Length - 1,
                Math.Round(_rmsd, 4)
            );
            // ReSharper restore LocalizableElement

        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hydroScores, out double[] predictions)
        {
            var minHydro = Double.MaxValue;
            var maxHydro = Double.MinValue;
            foreach (var hydroScore in statistics.ListHydroScores)
            {
                minHydro = Math.Min(minHydro, hydroScore);
                maxHydro = Math.Max(maxHydro, hydroScore);
            }
            var addMin = minHydro < _xArr[0];
            var addMax = maxHydro > _xArr.Last();
            var points = _xArr.Length + (addMax ? 1 : 0) + (addMin ? 1 : 0);
            hydroScores = new double[points];
            predictions = new double[points];
            var offset = 0;
            if (addMin)
            {
                hydroScores[0] = minHydro;
                predictions[0] = GetY(minHydro);
                offset = 1;
            }
            if (addMax)
            {
                hydroScores[hydroScores.Length - 1] = maxHydro;
                predictions[predictions.Length - 1] = GetY(maxHydro);
            }
            for (var i = 0; i < _xArr.Length; i++)
            {
                hydroScores[offset + i] = _xArr[i];
                predictions[offset + i] = _yPred[i];
            }
        }

        private int GetLeftIndex(double x, int i , int l)
        {
            if (l <= 2)
                return i;

            int j = i + l/2;
            double mid = _xArr[j];
            if (x == mid)
                return j;
            else if (x > mid)
                return GetLeftIndex(x, j, l - j + i);
            else
                return GetLeftIndex(x, i, j - i);      
        }

        private double Interpolate(double x, int i, int j)
        {
            double leftX = _xArr[i];
            double rightX = _xArr[j];
            double leftY = _yPred[i];
            double rightY = _yPred[j];
            if (x == leftX)
                return leftY;
            else if (x == rightX)
                return rightY;
            else
                return leftY + (x - leftX)*(rightY - leftY)/(rightX - leftX);
        }
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
    public sealed class RegressionLine : IIrtRegression
    {
        public RegressionLine()
        {
            Slope = 0;
            Intercept = 0;
            XValues = new double[0];
            YValues = new double[0];
            IrtIndependent = false;
        }

        public RegressionLine(double slope, double intercept, bool irtIndependent = false)
        {
            Slope = slope;
            Intercept = intercept;
            IrtIndependent = irtIndependent;
        }

        public RegressionLine(double[] x, double[] y, bool irtIndependent = false)
        {
            var statX = new Statistics(x);
            var statY = new Statistics(y);
            Slope = statY.Slope(statX);
            Intercept = statY.Intercept(statX);
            XValues = x;
            YValues = y;
            IrtIndependent = irtIndependent;
        }

        // XML Serializable properties
        [Track]
        public double Slope { get; private set; }

        [Track]
        public double Intercept { get; private set; }

        public string DisplayEquation => IrtIndependent
            ? string.Format(@"{0} = {1:F3} * {2} {3} {4:F3}",
                Resources.IIrtRegression_DisplayEquation_Measured_RT, Slope, Resources.IIrtRegression_DisplayEquation_iRT, Intercept >= 0 ? '+' : '-', Math.Abs(Intercept))
            : string.Format(@"{0} = {1:F3} * {2} {3} {4:F3}",
                Resources.IIrtRegression_DisplayEquation_iRT, Slope, Resources.IIrtRegression_DisplayEquation_Measured_RT, Intercept >= 0 ? '+' : '-', Math.Abs(Intercept));

        public bool IrtIndependent { get; }

        /// <summary>
        /// Use the y = m*x + b formula to calculate the desired y
        /// for a given x.
        /// </summary>
        /// <param name="x">Value in x dimension</param>
        /// <returns></returns>
        public double GetY(double x)
        {
            return Slope * x + Intercept;
        }

        public IIrtRegression ChangePoints(double[] x, double[] y)
        {
            return new RegressionLine(x, y);
        }
        public double[] XValues { get; }
        public double[] YValues { get; }

        public string GetRegressionDescription(double r, double window)
        {
            // ReSharper disable LocalizableElement
            return String.Format("{0} = {1:F02}, {2} = {3:F02}\n" + "{4} = {5:F01}\n" + "r = {6}",
                                          Resources.Regression_slope,
                                          Slope,
                                          Resources.Regression_intercept,
                                          Intercept,
                                          Resources.GraphData_AddRegressionLabel_window,
                                          window,
                                          Math.Round(r, RetentionTimeRegression.ThresholdPrecision));
            // ReSharper restore LocalizableElement
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] lineScores, out double[] lineTimes)
        {
            // Find maximum hydrophobicity score points for drawing the regression line
            lineScores = new[] { Double.MaxValue, 0 };
            lineTimes = new[] { Double.MaxValue, 0 };

            for (int i = 0; i < statistics.ListHydroScores.Count; i++)
            {
                double score = statistics.ListHydroScores[i];
                double time = statistics.ListPredictions[i];
                if (score < lineScores[0])
                {
                    lineScores[0] = score;
                    lineTimes[0] = time;
                }
                if (score > lineScores[1])
                {
                    lineScores[1] = score;
                    lineTimes[1] = time;
                }
            }   
        }

        /// <summary>
        /// Use the y = m*x + b formula to calculate the desired x
        /// for a given y.
        /// </summary>
        /// <param name="y">Value in y dimension</param>
        /// <returns></returns>
        public double GetX(double y)
        {
            return  (y - Intercept) / Slope;
        }

        #region IXmlSerializable helpers

        /// <summary>
        /// For serialization
        /// </summary>

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
