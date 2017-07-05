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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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

        public RetentionScoreCalculatorSpec Calculator
        {
            get { return _calculator; }
            private set { _calculator = value; }
        }

        public double TimeWindow { get; private set; }

        public IRegressionFunction Conversion { get; private set; }

        public bool IsUsable { get { return Conversion != null && Calculator.IsUsable; } }

        public bool IsAutoCalculated { get { return Conversion == null || _listFileIdToConversion != null; } }

        public bool IsStandardPeptide(PeptideDocNode nodePep)
        {
            return _dictStandardPeptides != null && _dictStandardPeptides.ContainsKey(nodePep.Peptide.GlobalIndex);
        }

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

        public double? GetRetentionTime(string seq)
        {
            return GetRetentionTime(seq, Conversion);
        }

        public double? GetRetentionTime(string seq, ChromFileInfoId fileId)
        {
            return GetRetentionTime(seq, GetConversion(fileId));
        }

        public double? GetRetentionTime(string seq, IRegressionFunction conversion)
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

        public IRegressionFunction GetUnconversion(ChromFileInfoId fileId)
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
        /// <param name="fileId">The file id (optional) with which an iRT regression may be associated</param>
        /// <returns>Calculated values for the peptides using this regression</returns>
        public RetentionTimeStatistics CalcStatistics(List<MeasuredRetentionTime> peptidesTimes,
            IDictionary<string, double> scoreCache, ChromFileInfoId fileId = null)
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
            else if (reader.IsStartElement("irt_calculator")) // Not L10N
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

        public static RetentionTimeRegression CalcRegression(string name,
                                                             IList<RetentionScoreCalculatorSpec> calculators,
                                                             RegressionMethodRT regressionMethod,
                                                             IList<MeasuredRetentionTime> measuredPeptides,
                                                             out RetentionTimeStatistics statistics)
        {
            RetentionScoreCalculatorSpec s;
            return CalcRegression(name, calculators, measuredPeptides, null, false,regressionMethod, out statistics, out s);
        }

        /// <summary>
        /// This function chooses the best calculator (by r value) and returns a regression based on that calculator.
        /// </summary>
        /// <param name="name">Name of the regression</param>
        /// <param name="calculators">An IEnumerable of calculators to choose from (cannot be null)</param>
        /// <param name="measuredPeptides">A List of MeasuredRetentionTime objects to build the regression from</param>
        /// <param name="scoreCache">A RetentionTimeScoreCache to try getting scores from before calculating them</param>
        /// <param name="allPeptides">If true, do not let the calculator pick which peptides to use in the regression</param>
        /// <param name="regressionMethod">The regression method (linear, loess, kde) to use</param>
        /// <param name="statistics">Statistics from the regression of the best calculator</param>
        /// <param name="calculatorSpec">The best calculator</param>
        /// <returns></returns>
        public static RetentionTimeRegression CalcRegression(string name,
                                                             IList<RetentionScoreCalculatorSpec> calculators,
                                                             IList<MeasuredRetentionTime> measuredPeptides,
                                                             RetentionTimeScoreCache scoreCache,
                                                             bool allPeptides,
                                                             RegressionMethodRT regressionMethod,
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

            RetentionScoreCalculatorSpec[] calculatorCandidates = calculators.ToArray();
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
                    int minCount;
                    calcPeptides[i] = allPeptides ? listPeptides : calc.ChooseRegressionPeptides(listPeptides, out minCount).ToList();
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
            IRegressionFunction bestRegressionFunction = null;
            for (int i = 0; i < calcs; i++)
            {
                if(setExcludeCalcs.Contains(i))
                    continue;

                Statistics statRT = new Statistics(listRTs[i]);
                Statistics stat = aStatValues[i];
                IRegressionFunction regressionFunction = null;
                double[] xArr;
                double[] ySmoothed;
                switch (regressionMethod)
                {
                    case RegressionMethodRT.linear:
                        regressionFunction = new RegressionLineElement(statRT.Slope(stat), statRT.Intercept(stat));
                        break;

                    case RegressionMethodRT.kde:
                        var kdeAligner = new KdeAligner();
                        kdeAligner.Train(stat.CopyList(), statRT.CopyList());
                        
                        kdeAligner.GetSmoothedValues(out xArr, out ySmoothed);
                        regressionFunction = 
                            new PiecewiseLinearRegressionFunction(xArr, ySmoothed, kdeAligner.GetRmsd());
                        stat = new Statistics(ySmoothed);
                        break;
                    case RegressionMethodRT.loess:
                        var loessAligner = new LoessAligner();
                        loessAligner.Train(stat.CopyList(), statRT.CopyList());
                        loessAligner.GetSmoothedValues(out xArr, out ySmoothed);
                        regressionFunction = 
                            new PiecewiseLinearRegressionFunction(xArr, ySmoothed, loessAligner.GetRmsd());
                        stat = new Statistics(ySmoothed);
                        break;
                }
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
                    bestRegressionFunction = regressionFunction;
                }
            }

            if (calcBest == null || bestRegressionFunction == null)
                return null;

            calculatorSpec = calcBest;

            //double slope = bestCalcStatRT.Slope(statBest);
            //double intercept = bestCalcStatRT.Intercept(statBest);

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
            IRegressionFunction rlBest = bestRegressionFunction;
            var listPredicted = listBest.Select(rlBest.GetY).ToList();
            statistics = new RetentionTimeStatistics(r, calcPeptides[bestCalcIndex], listBest, listPredicted, listRTs[bestCalcIndex]);

            // Get MeasuredRetentionTimes for only those peptides chosen by the calculator
            var setBestPeptides = new HashSet<string>();
            foreach (string pep in calcPeptides[bestCalcIndex])
                setBestPeptides.Add(pep);
            var calcMeasuredRts = measuredPeptides.Where(pep => setBestPeptides.Contains(pep.PeptideSequence)).ToArray();
            return new RetentionTimeRegression(name, calcBest, bestRegressionFunction, window, calcMeasuredRts);
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
                            IList<MeasuredRetentionTime> variableTargetPeptides,
                            IList<MeasuredRetentionTime> variableOrigPeptides,
                            RetentionScoreCalculatorSpec calculator,
                            RegressionMethodRT regressionMethod,
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
                                                  regressionMethod,
                                                  out statisticsAll,
                                                  out calculator);

            var outIndexes = new HashSet<int>();
            RetentionTimeStatistics statisticsRefined = null;
            return regressionInitial.FindThreshold(threshold,
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
                            IList<MeasuredRetentionTime> variableTargetPeptides,
                            IList<MeasuredRetentionTime> variableOrigPeptides,
                            RetentionTimeStatistics statistics,
                            RetentionScoreCalculatorSpec calculator,
                            RegressionMethodRT regressionMethod,
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
                        RecalcRegression(bestOut, standardPeptides, variableTargetPeptides, variableOrigPeptides,statisticsResult, calculator, regressionMethod, scoreCache,
                            out statisticsResult, ref outIndexes);
                        if (bestOut >= variableTargetPeptides.Count || !IsAboveThreshold(statisticsResult.R, threshold, precision))
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
                    var regression = RecalcRegression(worstIn, standardPeptides, variableTargetPeptides, variableOrigPeptides, statisticsResult, calculator, regressionMethod, scoreCache,
                        out statisticsResult, ref outIndexes);
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

            HashSet<int> outIndexesNew = outIndexes;
            RetentionTimeStatistics statisticsNew;
            // Rerun the regression
            var regressionNew = RecalcRegression(mid, standardPeptides, variableTargetPeptides, variableOrigPeptides, statistics, calculator, regressionMethod, scoreCache,
                out statisticsNew, ref outIndexesNew);
            // If no regression could be calculated, give up to avoid infinite recursion.
            if (regressionNew == null)
                return this;

            statisticsResult = statisticsNew;
            outIndexes = outIndexesNew;

            if (IsAboveThreshold(statisticsResult.R, threshold, precision))
            {
                return regressionNew.FindThreshold(threshold, precision, mid + 1, right,
                    standardPeptides, variableTargetPeptides, variableOrigPeptides, statisticsResult, calculator, regressionMethod, scoreCache, isCanceled,
                    ref statisticsResult, ref outIndexes);
            }

            return regressionNew.FindThreshold(threshold, precision, left, mid - 1,
                standardPeptides, variableTargetPeptides, variableOrigPeptides, statisticsResult, calculator, regressionMethod, scoreCache, isCanceled,
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

            RetentionScoreCalculatorSpec s;
            return CalcRegression(Name, new[] { calculator }, peptidesTimesTry, scoreCache, true,regressionMethod,
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

        public const string SSRCALC_300_A = "SSRCalc 3.0 (300A)"; // Not L10N
        public const string SSRCALC_100_A = "SSRCalc 3.0 (100A)"; // Not L10N

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
            _cache.TryGetValue(calculator.Name, out cacheCalc);
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

        double? ScoreSequence(string modifiedSequence);

        double UnknownScore { get; }

        IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides, out int minCount);

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

        public abstract IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides, out int minCount);

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

        public override IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides, out int minCount)
        {
            return _impl.ChooseRegressionPeptides(peptides, out minCount);
        }

        private void Validate()
        {
            _impl = RetentionTimeRegression.GetCalculatorByName(Name);
            if (_impl == null)
                throw new InvalidDataException(string.Format(Resources.RetentionScoreCalculator_Validate_The_retention_time_calculator__0__is_not_valid, Name));
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
        /// <summary>
        /// To support using iRT values, which can be negative, in place of measured retention times
        /// </summary>
        private readonly bool _allowNegative;

        public MeasuredRetentionTime(string peptideSequence, double retentionTime, bool allowNegative = false, bool isStandard = false)
        {
            Assume.IsFalse(String.IsNullOrEmpty(peptideSequence));
            PeptideSequence = peptideSequence;
            RetentionTime = retentionTime;
            IsStandard = isStandard;
            _allowNegative = allowNegative;

            Validate();
        }

        public string PeptideSequence { get; private set; }
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
            // FUTURE: May need to add support for small molecules
            if (!FastaSequence.IsExSequence(PeptideSequence))
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

        public override string ToString()
        {
            return string.Format("{0}: {1:F01}{2}", PeptideSequence, RetentionTime,   // Not L10N
                IsStandard ? "*" : String.Empty);   // Not L10N
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
    }

    /// <summary>
    /// Represents an observed drift time in msec for
    /// a peptide at a given charge state.
    /// </summary>
    public sealed class MeasuredDriftTimePeptide : IXmlSerializable, IComparable<MeasuredDriftTimePeptide>
    {
        public string ModifiedSequence { get; private set; }
        public int Charge { get; private set; }
        public DriftTimeInfo DriftTimeInfo { get; private set; }

        public MeasuredDriftTimePeptide(string seq, int charge, DriftTimeInfo driftTimeInfo)
        {
            ModifiedSequence = seq;
            Charge = charge;
            DriftTimeInfo = driftTimeInfo;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private MeasuredDriftTimePeptide()
        {
        }

        private enum ATTR
        {
            modified_sequence,
            charge,
            drift_time,
            ccs,
            high_energy_drift_time_offset
        }

        public static MeasuredDriftTimePeptide Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MeasuredDriftTimePeptide());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            ModifiedSequence = reader.GetAttribute(ATTR.modified_sequence);
            Charge = reader.GetIntAttribute(ATTR.charge);
            DriftTimeInfo = new DriftTimeInfo(reader.GetNullableDoubleAttribute(ATTR.drift_time),
                reader.GetNullableDoubleAttribute(ATTR.ccs),
                reader.GetDoubleAttribute(ATTR.high_energy_drift_time_offset, 0));
            // Consume tag
            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR.modified_sequence, ModifiedSequence);
            writer.WriteAttribute(ATTR.charge, Charge);
            writer.WriteAttributeNullable(ATTR.drift_time, DriftTimeInfo.DriftTimeMsec);
            writer.WriteAttribute(ATTR.high_energy_drift_time_offset, DriftTimeInfo.HighEnergyDriftTimeOffsetMsec);
            writer.WriteAttributeNullable(ATTR.ccs, DriftTimeInfo.CollisionalCrossSectionSqA);
        }

        #endregion

        #region object overrides

        public bool Equals(MeasuredDriftTimePeptide obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.ModifiedSequence, ModifiedSequence) && obj.Charge == Charge &&
                   Equals(obj.DriftTimeInfo, DriftTimeInfo);
        }

        public int CompareTo(MeasuredDriftTimePeptide other)
        {
            int result = String.Compare(ModifiedSequence, other.ModifiedSequence, StringComparison.Ordinal);
            if (result != 0)
                return result;

            result = Charge - other.Charge;
            if (result != 0)
                return result;

            return DriftTimeInfo.CompareTo(other.DriftTimeInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(MeasuredDriftTimePeptide)) return false;
            return Equals((MeasuredDriftTimePeptide)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (ModifiedSequence != null ? ModifiedSequence.GetHashCode() : 0);
                result = (result * 397) ^ (DriftTimeInfo.GetHashCode() * 397) ^ Charge;
                return result;
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
    }

    [XmlRoot("predict_compensation_voltage")]
    public class CompensationVoltageParameters : OptimizableRegression
    {
        public enum Tuning { none = 0, rough = 1, medium = 2, fine = 3 }

        public const int MIN_STEP_COUNT = 1;
        public const int MAX_STEP_COUNT = 10;

        public virtual Tuning TuneLevel { get { return Tuning.none; } }
        public CompensationVoltageRegressionRough RegressionRough { get; private set; }
        public CompensationVoltageRegressionMedium RegressionMedium { get; private set; }
        public CompensationVoltageRegressionFine RegressionFine { get; private set; }

        public double MinCov { get; protected set; }
        public double MaxCov { get; protected set; }
        public int StepCountRough { get; protected set; }
        public int StepCountMedium { get; protected set; }
        public int StepCountFine { get; protected set; }
        public double StepSizeRough { get { return (MaxCov - MinCov)/(StepCountRough*2); } }
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

        public override double StepSize { get { return GetStepSize(TuneLevel); } }

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

        public double Slope { get { return RegressionLine.Slope; } }

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

        public virtual double StepSize
        {
            get { return _stepSize; }
            protected set { _stepSize = value; }
        }

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

        public double Slope { get { return _regressionLine.Slope; } }

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
            return string.Format("{0} = {1}\n" + "rmsd =  {2}", // Not L10N
                Resources.PiecewiseLinearRegressionFunction_GetRegressionDescription_piecwise_linear_functions,
                _xArr.Length - 1,
                Math.Round(_rmsd, 4)
            );

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
            return Slope * x + Intercept;
        }

        public string GetRegressionDescription(double r, double window)
        {
            return String.Format("{0} = {1:F02}, {2} = {3:F02}\n" + "{4} = {5:F01}\n" + "r = {6}",   // Not L10N
                                          Resources.Regression_slope,
                                          Slope,
                                          Resources.Regression_intercept,
                                          Intercept,
                                          Resources.GraphData_AddRegressionLabel_window,
                                          window,
                                          Math.Round(r, RetentionTimeRegression.ThresholdPrecision));
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

    public interface IDriftTimeInfoProvider
    {
        string Name { get; }

        DriftTimeInfo GetLibraryMeasuredDriftTimeAndHighEnergyOffset(LibKey peptide, double mz, IConversionDriftTimeCCSProvider instrumentInfo);

        IDictionary<LibKey, DriftTimeInfo[]> GetIonMobilityDict();

    }

    /// <summary>
    /// Contains drift time and its Collisional Cross Section basis (if known), 
    /// and the effect on drift time in high energy spectra as in Waters MSe
    /// </summary>
    public class DriftTimeInfo : IComparable
    {
        public static readonly DriftTimeInfo EMPTY = new DriftTimeInfo(null, null, 0);
      

        public DriftTimeInfo(double? driftTimeMsec, double? collisionalCrossSectionSqA, 
            double highEnergyDriftTimeOffsetMsec)
        {
            DriftTimeMsec = driftTimeMsec;
            CollisionalCrossSectionSqA = collisionalCrossSectionSqA;
            HighEnergyDriftTimeOffsetMsec = highEnergyDriftTimeOffsetMsec;
        }

        public double? DriftTimeMsec { get; private set; } 
        public double? CollisionalCrossSectionSqA { get; private set; } 
        public double HighEnergyDriftTimeOffsetMsec { get; private set; } // As in Waters MSe, where product ions fly a bit faster due to added kinetic energy

        public double? GetHighEnergyDriftTimeMsec()
        {
            if (DriftTimeMsec.HasValue)
            {
                return DriftTimeMsec.Value + HighEnergyDriftTimeOffsetMsec;
            }
            return null;
        }

        public bool HasCollisionalCrossSection { get { return (CollisionalCrossSectionSqA ?? 0) != 0; } }
        public bool HasDriftTime { get { return (DriftTimeMsec ?? 0) != 0; } }
        public bool IsEmpty { get { return !HasDriftTime && !HasCollisionalCrossSection; } }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return 0 == CompareTo(obj as DriftTimeInfo);
        }

        public DriftTimeInfo ChangeDriftTimeMsec(double? driftTimeMsec)
        {
            return new DriftTimeInfo(driftTimeMsec, CollisionalCrossSectionSqA, HighEnergyDriftTimeOffsetMsec);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = DriftTimeMsec.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionalCrossSectionSqA.GetHashCode();
                hashCode = (hashCode * 397) ^ HighEnergyDriftTimeOffsetMsec.GetHashCode();
                return hashCode;
            }
        }

        public int CompareTo(object obj)
        {
            DriftTimeInfo other = obj as DriftTimeInfo;
            if (other == null)
                return 1;
            var val = Nullable.Compare(CollisionalCrossSectionSqA, other.CollisionalCrossSectionSqA);
            if (val != 0)
                return val;
            val = Nullable.Compare(DriftTimeMsec, other.DriftTimeMsec);
            if (val != 0)
                return val;
            var diff =  HighEnergyDriftTimeOffsetMsec - other.HighEnergyDriftTimeOffsetMsec;
            if (diff > 0)
                return 1;
            else if (diff < 0)
                return -1;
            return 0;
        }
    }

    /// <summary>
    /// Contains the drift time and window used to filter scans, and and CCS basis for the drift time if known
    /// </summary>
    public class DriftTimeFilter : IComparable
    {
        public static readonly DriftTimeFilter EMPTY = new DriftTimeFilter(null, null, null);

        public static DriftTimeFilter GetDriftTimeFilter(double? driftTimeMsec,
            double? driftTimeExtractionWindowWidthMsec,
            double? collisionalCrossSectionSqA)
        {
            if (!driftTimeMsec.HasValue
                && !driftTimeExtractionWindowWidthMsec.HasValue
                && !collisionalCrossSectionSqA.HasValue)
            {
                return EMPTY;
            }
            return new DriftTimeFilter(driftTimeMsec, 
                driftTimeExtractionWindowWidthMsec,
                collisionalCrossSectionSqA);
        }

        private DriftTimeFilter(double? driftTimeMsec,
            double? driftTimeExtractionWindowWidthMsec,
            double? collisionalCrossSectionSqA)
        {
            DriftTimeMsec = driftTimeMsec;
            DriftTimeExtractionWindowWidthMsec = driftTimeExtractionWindowWidthMsec;
            CollisionalCrossSectionSqA = collisionalCrossSectionSqA;
        }

        public double? DriftTimeMsec { get; private set; }
        public double? DriftTimeExtractionWindowWidthMsec { get; private set; }
        public double? CollisionalCrossSectionSqA { get; private set; } // The CCS value used to get the drift time, if any

        public bool HasDriftTime { get { return (DriftTimeMsec ?? 0) != 0; } }
        public bool IsEmpty { get { return !HasDriftTime; } }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return 0 == CompareTo(obj as DriftTimeFilter);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = DriftTimeMsec.GetHashCode();
                hashCode = (hashCode * 397) ^ DriftTimeExtractionWindowWidthMsec.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionalCrossSectionSqA.GetHashCode();
                return hashCode;
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as DriftTimeFilter;
            if (other == null)
                return 1;
            var val = Nullable.Compare(DriftTimeMsec, other.DriftTimeMsec);
            if (val != 0)
                return val;
            val = Nullable.Compare(CollisionalCrossSectionSqA, other.CollisionalCrossSectionSqA);
            if (val != 0)
                return val;
            return Nullable.Compare(DriftTimeExtractionWindowWidthMsec, other.DriftTimeExtractionWindowWidthMsec);
        }

        public override string ToString()
        {
            if (CollisionalCrossSectionSqA.HasValue)
            {
                return string.Format("dt{0:F04}(ccs{2:F04})/w{1:F04}", DriftTimeMsec, DriftTimeExtractionWindowWidthMsec, CollisionalCrossSectionSqA); // Not L10N
            }
            return string.Format("dt{0:F04}/w{1:F04}", DriftTimeMsec, DriftTimeExtractionWindowWidthMsec); // Not L10N
        }
    }

    public interface IIonMobilityLibrary
    {
        string Name { get; }
        DriftTimeInfo GetDriftTimeInfo(String peptide, ChargeRegressionLine regressionLine);
    }

    public abstract class IonMobilityLibrarySpec : XmlNamedElement, IIonMobilityLibrary
    {
        protected IonMobilityLibrarySpec(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Get the drift time for the charged peptide.
        /// </summary>
        /// <param name="peptide"></param>
        /// <param name="regressionLine"></param>
        /// <returns>drift time, or null</returns>
        public abstract DriftTimeInfo GetDriftTimeInfo(String peptide, ChargeRegressionLine regressionLine);

        public virtual bool IsUsable { get { return true; } }

        public virtual bool IsNone { get { return false; } }

        public virtual IonMobilityLibrarySpec Initialize(IProgressMonitor loadMonitor)
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
        protected IonMobilityLibrarySpec()
        {
        }

        #endregion
    }

    public class DriftTimeWindowWidthCalculator : IEquatable<DriftTimeWindowWidthCalculator>
    {
        private enum ATTR
        {
            resolving_power,
            peak_width_calc_type,
            width_at_dt_zero,
            width_at_dt_max
        }

        public static readonly DriftTimeWindowWidthCalculator EMPTY = new DriftTimeWindowWidthCalculator(DriftTimePeakWidthType.resolving_power, 0, 0, 0);

        public DriftTimeWindowWidthCalculator(DriftTimePeakWidthType peakWidthMode,
                                    double resolvingPower,
                                    double widthAtDriftTimeZero, 
                                    double widthAtDriftTimeMax)
        {
            PeakWidthMode = peakWidthMode;
            ResolvingPower = resolvingPower;
            PeakWidthAtDriftTimeZero = widthAtDriftTimeZero;
            PeakWidthAtDriftTimeMax = widthAtDriftTimeMax;
        }

        public DriftTimeWindowWidthCalculator(XmlReader reader, string prefix = "") : this( // Not L10N
            reader.GetEnumAttribute(prefix + ATTR.peak_width_calc_type, DriftTimePeakWidthType.resolving_power),
            reader.GetDoubleAttribute(prefix + ATTR.resolving_power, 0),
            reader.GetDoubleAttribute(prefix + ATTR.width_at_dt_zero, 0),
            reader.GetDoubleAttribute(prefix + ATTR.width_at_dt_max, 0))
        {
        }

        public void WriteXML(XmlWriter writer, string prefix = "") // Not L10N
        {
            writer.WriteAttribute(prefix + ATTR.peak_width_calc_type, PeakWidthMode);
            writer.WriteAttribute(prefix + ATTR.resolving_power, ResolvingPower);
            writer.WriteAttribute(prefix + ATTR.width_at_dt_zero, PeakWidthAtDriftTimeZero);
            writer.WriteAttribute(prefix + ATTR.width_at_dt_max, PeakWidthAtDriftTimeMax);
        }

        public enum DriftTimePeakWidthType
        {
            resolving_power,  // Agilent, etc
            linear_range      // Waters SONAR etc
        };

        public DriftTimePeakWidthType PeakWidthMode { get; private set; }

        // For Water-style (SONAR) linear peak width calcs
        public double PeakWidthAtDriftTimeZero { get; private set; }
        public double PeakWidthAtDriftTimeMax { get; private set; }

        // For Agilent-style resolving power peak width calcs
        public double ResolvingPower { get; private set; }


        public double WidthAt(double driftTime, double driftTimeMax)
        {
            if (PeakWidthMode == DriftTimePeakWidthType.resolving_power)
            {
                return (ResolvingPower > 0 ? 2.0 / ResolvingPower : double.MaxValue) * driftTime; // 2.0*driftTime/resolvingPower
            }
            Assume.IsTrue(driftTimeMax > 0, "Expected dtMax value > 0 for linear range drift window calculation"); // Not L10N
            return PeakWidthAtDriftTimeZero + driftTime * (PeakWidthAtDriftTimeMax - PeakWidthAtDriftTimeZero) / driftTimeMax;
        }

        public string Validate()
        {
            if (PeakWidthMode == DriftTimePeakWidthType.resolving_power)
            {
                if (ResolvingPower <= 0)
                    return Resources.DriftTimePredictor_Validate_Resolving_power_must_be_greater_than_0_;
            }
            else
            {
                if (PeakWidthAtDriftTimeZero < 0 || PeakWidthAtDriftTimeMax < PeakWidthAtDriftTimeZero)
                    return Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_;
            }
            return null;
        }

        public bool Equals(DriftTimeWindowWidthCalculator other)
        {
            return other != null &&
                   Equals(other.PeakWidthMode, PeakWidthMode) &&
                   Equals(other.ResolvingPower, ResolvingPower) &&
                   Equals(other.PeakWidthAtDriftTimeZero, PeakWidthAtDriftTimeZero) &&
                   Equals(other.PeakWidthAtDriftTimeMax, PeakWidthAtDriftTimeMax);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DriftTimeWindowWidthCalculator);
        }

        public override int GetHashCode()
        {
            int result = PeakWidthMode.GetHashCode();
            result = (result * 397) ^ ResolvingPower.GetHashCode();
            result = (result * 397) ^ PeakWidthAtDriftTimeZero.GetHashCode();
            result = (result * 397) ^ PeakWidthAtDriftTimeMax.GetHashCode();
            return result;
        }
    }

    /// <summary>
    /// Describes a slope and intercept for converting from a
    /// collisional cross section value to a predicted drift time in milliseconds.
    /// </summary>
    [XmlRoot("predict_drift_time")]
    public sealed class DriftTimePredictor : XmlNamedElement
    {
        public static double? GetDriftTimeDisplay(double? dtMsec)
        {
            if (!dtMsec.HasValue)
                return null;
            return Math.Round(dtMsec.Value, 4);
        }

        private ImmutableDictionary<LibKey, DriftTimeInfo> _measuredDriftTimePeptides;
        private ImmutableList<ChargeRegressionLine> _chargeRegressionLines;

        public DriftTimePredictor(string name,
                                    Dictionary<LibKey, DriftTimeInfo> measuredDriftTimePeptides,
                                    IonMobilityLibrarySpec ionMobilityLibrary,
                                    IList<ChargeRegressionLine> chargeSlopeIntercepts,
                                    DriftTimeWindowWidthCalculator.DriftTimePeakWidthType peakWidthMode,
                                    double resolvingPower,
                                    double widthAtDtZero, double widthAtDtMax)
            : base(name)
        {
            WindowWidthCalculator = new DriftTimeWindowWidthCalculator(peakWidthMode,
               resolvingPower, widthAtDtZero, widthAtDtMax);
            MeasuredDriftTimePeptides = measuredDriftTimePeptides;
            ChargeRegressionLines = (chargeSlopeIntercepts == null) ? null : chargeSlopeIntercepts.ToArray();
            IonMobilityLibrary = ionMobilityLibrary; // Actual loading, if any, happens in background
            Validate();
        }

        public static readonly DriftTimePredictor EMPTY = new DriftTimePredictor();  // For test purposes

        public IonMobilityLibrarySpec IonMobilityLibrary { get; private set; }

        public DriftTimeWindowWidthCalculator WindowWidthCalculator;

        public DriftTimeInfo GetMeasuredDriftTimeMsec(LibKey chargedPeptide)
        {
            if (MeasuredDriftTimePeptides != null)
            {
                DriftTimeInfo dt;
                if (MeasuredDriftTimePeptides.TryGetValue(chargedPeptide, out dt))
                    return dt;
            }
            return DriftTimeInfo.EMPTY;
        }

        public IDictionary<LibKey, DriftTimeInfo> MeasuredDriftTimePeptides
        {
            get { return _measuredDriftTimePeptides; }
            private set 
            {
                _measuredDriftTimePeptides = (value == null) ? null : new ImmutableDictionary<LibKey, DriftTimeInfo>(value);
            }
        }


        public IList<ChargeRegressionLine> ChargeRegressionLines
        {
            get { return _chargeRegressionLines; }
            private set
            {
                ChargeRegressionLine[] chargeRegressionLines;
                if ((value != null) && value.Any())
                {
                    int maxcharge = value.Max(obj => obj.Charge);
                    chargeRegressionLines = new ChargeRegressionLine[maxcharge+1];
                    for (int charge = 0; charge <= maxcharge; charge++)
                    {
                        int charge1 = charge;
                        var match = (from val in value where (val.Charge == charge1) select val).ToArray();
                        if (match.Any())
                            chargeRegressionLines[charge] = match.First();
                        else
                            chargeRegressionLines[charge] = null;
                   }
                }
                else
                {
                    chargeRegressionLines = new ChargeRegressionLine[0];
                }
                _chargeRegressionLines = MakeReadOnly(chargeRegressionLines);
            }
        }

        public bool IsUsable
        {
            get
            {
                // We're usable if we have measured drift times, or a CCS library
                bool usable = (_measuredDriftTimePeptides != null) && _measuredDriftTimePeptides.Any();
                if (IonMobilityLibrary != null && !IonMobilityLibrary.IsNone)
                {
                    // If we have a CCS library, we need regressions, and the library itself needs to be ready
                    usable |= ( ChargeRegressionLines != null && ChargeRegressionLines.Any() && IonMobilityLibrary.IsUsable );
                }
                return usable;
            }
        }

        #region Property change methods

        public DriftTimePredictor ChangeLibrary(IonMobilityLibrarySpec prop)
        {
            return ChangeProp(ImClone(this), im => im.IonMobilityLibrary = prop);
        }

        public DriftTimePredictor ChangeMeasuredDriftTimesFromResults(SrmDocument document, string documentFilePath,  IProgressMonitor progressMonitor = null)
        {
            // Overwrite any existing measurements with newly derived ones
            Dictionary<LibKey, DriftTimeInfo> measured;
            using( var finder = new DriftTimeFinder(document, documentFilePath, this, progressMonitor))
            {
                measured = finder.FindDriftTimePeaks(); // Returns null on cancel
            }
            return measured == null ? this : ChangeProp(ImClone(this), im => im.MeasuredDriftTimePeptides = measured);
        }

        public DriftTimePredictor ChangeDriftTimeWindowWidthCalculator(DriftTimeWindowWidthCalculator prop)
        {
            return ChangeProp(ImClone(this), im => im.WindowWidthCalculator = prop);
        }


        #endregion

        public ChargeRegressionLine GetRegressionLine(int charge)
        {
            // These should be very short lists (maximum 5 elements).
            // A simple sparse lookup table used over a map
            // for ease of persistence to XML.
            if ((charge > 0) && (charge < ChargeRegressionLines.Count()))
                return ChargeRegressionLines[charge];
            return null;
        }

        public DriftTimeInfo GetDriftTimeInfo(LibKey peptide, IConversionDriftTimeCCSProvider conversionDriftTimeCcsProvider)
        {
            var ionMobility = GetIonMobilityInfo(peptide);
            // Convert from CCS to DT if possible
            if (conversionDriftTimeCcsProvider != null && ionMobility != null && ionMobility.HasCollisionalCrossSection && peptide.PrecursorMz.HasValue)
            {
                var driftTimeMsec = conversionDriftTimeCcsProvider.DriftTimeFromCCS(ionMobility.CollisionalCrossSectionSqA.Value,
                    peptide.PrecursorMz.Value, peptide.Charge);
                ionMobility = new DriftTimeInfo(driftTimeMsec, ionMobility.CollisionalCrossSectionSqA, ionMobility.HighEnergyDriftTimeOffsetMsec);
            }
            return ionMobility;
        }

        public DriftTimeInfo GetIonMobilityInfo(LibKey peptide)
        {
            // Do we see this in our list of observed drift times?
            DriftTimeInfo driftTime = null;
            var found = false;
            if (MeasuredDriftTimePeptides != null)
            {
                if (MeasuredDriftTimePeptides.TryGetValue(peptide, out driftTime))
                    found = true;
            }
            if (!found && IonMobilityLibrary != null && !IonMobilityLibrary.IsNone)
            {
                ChargeRegressionLine regressionLine = GetRegressionLine(peptide.Charge);
                if (regressionLine != null)
                {
                    if (!IonMobilityLibrary.IsUsable)
                    {
                        // First access?  Load the library.
                        IonMobilityLibrary = IonMobilityLibrary.Initialize(null);
                    }
                    driftTime = IonMobilityLibrary.GetDriftTimeInfo(peptide.Sequence, regressionLine); //  regressionLine.GetY(peptideInfo.CollisionalCrossSection) or null;
                }
            }
            return driftTime;
        }

        /// <summary>
        /// Get the drift time in msec for the charged peptide, and the width of the window
        /// centered on that based on the drift time predictor's claimed resolving power
        /// </summary>
        public DriftTimeInfo GetDriftTimeInfo(LibKey peptide, IConversionDriftTimeCCSProvider conversionDriftTimeCcsProvider, double driftTimeMax, out double dtWindowWidthMsec)
        {
            DriftTimeInfo driftTime = GetDriftTimeInfo(peptide, conversionDriftTimeCcsProvider);
            if (driftTime != null)
            {
                dtWindowWidthMsec = WindowWidthCalculator.WidthAt(driftTime.DriftTimeMsec ?? 0, driftTimeMax); 
            }
            else
            {
                dtWindowWidthMsec = 0;
            }
            return driftTime;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private DriftTimePredictor()
        {
        }

        private enum EL
        {
            regression_dt,
            measured_dt
        }

        private void Validate()
        {
            // This is active if:
            // Measured drift times are provided, or
            // Ion mobility library is provided
            bool hasLib = ((IonMobilityLibrary != null) && !IonMobilityLibrary.IsNone);
            bool hasMeasured = ((MeasuredDriftTimePeptides != null) && MeasuredDriftTimePeptides.Any());
            if (hasLib || hasMeasured)
            {
                var messages = new List<string>();
                var msg = WindowWidthCalculator.Validate();
                if (msg != null)
                    messages.Add(msg);
                if (hasLib && String.IsNullOrEmpty(IonMobilityLibrary.PersistencePath))
                    messages.Add(Resources.DriftTimePredictor_Validate_Drift_time_predictors_using_an_ion_mobility_library_must_provide_a_filename_for_the_library_);
                if (hasLib && !ChargeRegressionLines.Any())
                    messages.Add(Resources.DriftTimePredictor_Validate_Drift_time_predictors_using_an_ion_mobility_library_must_include_per_charge_regression_values_);
                if (messages.Any())
                    throw new InvalidDataException(TextUtil.LineSeparate(messages));
            }
        }

        public static DriftTimePredictor Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new DriftTimePredictor());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read start tag attributes
            base.ReadXml(reader);
            WindowWidthCalculator = new DriftTimeWindowWidthCalculator(reader);

            // Consume start tag
            reader.ReadStartElement();
            var readHelper = new XmlElementHelper<IonMobilityLibrary>();
            if (reader.IsStartElement(readHelper.ElementNames))
            {
                IonMobilityLibrary = readHelper.Deserialize(reader);
            }

            // Read all per-charge regressions
            var list = new List<ChargeRegressionLine>();
            while (reader.IsStartElement(EL.regression_dt))
            {
                list.Add(ChargeRegressionLine.Deserialize(reader));
            }
            ChargeRegressionLines = list.ToArray();

            // Read all measured drift times
            var dict = new Dictionary<LibKey, DriftTimeInfo>();
            while (reader.IsStartElement(EL.measured_dt))
            {
                var dt = MeasuredDriftTimePeptide.Deserialize(reader);
                var key = new LibKey(dt.ModifiedSequence, dt.Charge);
                if (!dict.ContainsKey(key))
                {
                    dict.Add(key, dt.DriftTimeInfo);
                }
            }
            if (dict.Any())
                MeasuredDriftTimePeptides = dict;

            reader.Read();             // Consume end tag

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write attributes
            base.WriteXml(writer);
            WindowWidthCalculator.WriteXML(writer);

            // Write collisional cross sections
            if (IonMobilityLibrary != null && !IonMobilityLibrary.IsNone) 
            {
                var imCalc = IonMobilityLibrary as IonMobilityLibrary;
                if (imCalc != null)
                    writer.WriteElement(imCalc);
            }

            // Write all per-charge regressions
            if (ChargeRegressionLines != null)
            {
                foreach (ChargeRegressionLine line in ChargeRegressionLines.Where(chargeRegressionLine => chargeRegressionLine != null))
                {
                    writer.WriteStartElement(EL.regression_dt);
                    line.WriteXml(writer);
                    writer.WriteEndElement();
                }
            }
            // Write all measured drift times
            if (MeasuredDriftTimePeptides != null)
            {
                foreach (var dt in MeasuredDriftTimePeptides)
                {
                    writer.WriteStartElement(EL.measured_dt);
                    var mdt = new MeasuredDriftTimePeptide(dt.Key.Sequence, dt.Key.Charge, dt.Value);
                    mdt.WriteXml(writer);
                    writer.WriteEndElement();
                }
            }
        }

        #endregion

        #region object overrides

        public bool Equals(DriftTimePredictor obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                   Equals(obj.IonMobilityLibrary, IonMobilityLibrary) &&
                   ArrayUtil.EqualsDeep(obj.ChargeRegressionLines, ChargeRegressionLines) &&
                   ArrayUtil.EqualsDeep(obj.MeasuredDriftTimePeptides, MeasuredDriftTimePeptides) &&
                   Equals(obj.WindowWidthCalculator, WindowWidthCalculator);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DriftTimePredictor);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result * 397) ^ IonMobilityLibrary.GetHashCode();
                result = (result * 397) ^ CollectionUtil.GetHashCodeDeep(ChargeRegressionLines);
                result = (result * 397) ^ CollectionUtil.GetHashCodeDeep(MeasuredDriftTimePeptides);
                result = (result * 397) ^ WindowWidthCalculator.GetHashCode();
                return result;
            }
        }

        #endregion

    }

}