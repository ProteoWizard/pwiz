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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("peptide_settings")]
    public class PeptideSettings : Immutable, IXmlSerializable
    {
        public PeptideSettings(Enzyme enzyme,
                               DigestSettings digestSettings,
                               PeptidePrediction prediction,
                               PeptideFilter filter,
                               PeptideLibraries libraries,
                               PeptideModifications modifications,
                               BackgroundProteome backgroundProteome
                               )
        {
            Enzyme = enzyme;
            DigestSettings = digestSettings;
            Prediction = prediction;
            Filter = filter;
            Libraries = libraries;
            Modifications = modifications;
            BackgroundProteome = backgroundProteome;
        }

        public Enzyme Enzyme { get; private set; }

        public DigestSettings DigestSettings { get; private set; }

        public BackgroundProteome BackgroundProteome { get; private set; }

        public PeptidePrediction Prediction { get; private set; }

        public PeptideFilter Filter { get; private set; }

        public PeptideLibraries Libraries { get; private set; }

        public PeptideModifications Modifications { get; private set; }

        #region Property change methods

        public PeptideSettings ChangeEnzyme(Enzyme prop)
        {
            return ChangeProp(ImClone(this), im => im.Enzyme = prop);
        }

        public PeptideSettings ChangeDigestSettings(DigestSettings prop)
        {
            return ChangeProp(ImClone(this), im => im.DigestSettings = prop);
        }

        public PeptideSettings ChangeBackgroundProteome(BackgroundProteome prop)
        {
            return ChangeProp(ImClone(this), im => im.BackgroundProteome = prop);
        }

        public PeptideSettings ChangePrediction(PeptidePrediction prop)
        {
            return ChangeProp(ImClone(this), im => im.Prediction = prop);
        }

        public PeptideSettings ChangeFilter(PeptideFilter prop)
        {
            return ChangeProp(ImClone(this), im => im.Filter = prop);
        }

        public PeptideSettings ChangeLibraries(PeptideLibraries prop)
        {
            return ChangeProp(ImClone(this), im => im.Libraries = prop);
        }

        public PeptideSettings ChangeModifications(PeptideModifications prop)
        {
            return ChangeProp(ImClone(this), im => im.Modifications = prop);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private PeptideSettings()
        {
        }

        public static PeptideSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new PeptideSettings());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();

                // Read child elements.
                Enzyme = reader.DeserializeElement<Enzyme>();
                DigestSettings = reader.DeserializeElement<DigestSettings>();
                BackgroundProteome = reader.DeserializeElement<BackgroundProteome>();
                Prediction = reader.DeserializeElement<PeptidePrediction>();
                Filter = reader.DeserializeElement<PeptideFilter>();
                Libraries = reader.DeserializeElement<PeptideLibraries>();
                Modifications = reader.DeserializeElement<PeptideModifications>();
                reader.ReadEndElement();
            }

            // Defer validation to the SrmSettings object
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write child elements
            writer.WriteElement(Enzyme);
            writer.WriteElement(DigestSettings);
            if (!BackgroundProteome.IsNone)
                writer.WriteElement(BackgroundProteome);
            writer.WriteElement(Prediction);
            writer.WriteElement(Filter);
            writer.WriteElement(Libraries);
            writer.WriteElement(Modifications);
        }

        #endregion

        #region object overrides

        public bool Equals(PeptideSettings obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.Enzyme, Enzyme) &&
                   Equals(obj.DigestSettings, DigestSettings) &&
                   Equals(obj.Prediction, Prediction) &&
                   Equals(obj.Filter, Filter) &&
                   Equals(obj.Libraries, Libraries) &&
                   Equals(obj.Modifications, Modifications) &&
                   Equals(obj.BackgroundProteome, BackgroundProteome);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (PeptideSettings)) return false;
            return Equals((PeptideSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Enzyme.GetHashCode();
                result = (result*397) ^ DigestSettings.GetHashCode();
                result = (result*397) ^ Prediction.GetHashCode();
                result = (result*397) ^ Filter.GetHashCode();
                result = (result*397) ^ Libraries.GetHashCode();
                result = (result*397) ^ Modifications.GetHashCode();
                result = (result*397) ^ BackgroundProteome.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    [XmlRoot("peptide_prediction")]
    public class PeptidePrediction : Immutable, IValidating, IXmlSerializable
    {
        public const int MAX_TREND_PREDICTION_REPLICATES = 20;
        public const double MIN_MEASURED_RT_WINDOW = 0.5;
        public const double MAX_MEASURED_RT_WINDOW = 30.0;
        public const double DEFAULT_MEASURED_RT_WINDOW = 2.0;

        public PeptidePrediction(RetentionTimeRegression retentionTime)
            : this(retentionTime, true, DEFAULT_MEASURED_RT_WINDOW)
        {            
        }

        public PeptidePrediction(RetentionTimeRegression retentionTime, bool useMeasuredRTs, double? measuredRTWindow)
        {
            RetentionTime = retentionTime;
            UseMeasuredRTs = useMeasuredRTs;
            MeasuredRTWindow = measuredRTWindow;

            DoValidate();
        }

        public RetentionTimeRegression RetentionTime { get; private set; }

        public bool UseMeasuredRTs { get; private set; }

        public double? MeasuredRTWindow { get; private set; }

        public int CalcMaxTrendReplicates(SrmDocument document)
        {
            if (!UseMeasuredRTs || !MeasuredRTWindow.HasValue)
                throw new InvalidOperationException("Calculating scheduling from trends requires a retention time window for measured data.");

            int i;
            for (i = 1; i < MAX_TREND_PREDICTION_REPLICATES; i++)
            {
                foreach (var nodePep in document.Peptides)
                {
                    foreach(TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        double windowRT;
                        double? centerTime = PredictRetentionTime(document,
                                                                  nodePep,
                                                                  nodeGroup,
                                                                  i,
                                                                  ExportSchedulingAlgorithm.Trends,
                                                                  true,
                                                                  out windowRT);
                        if (centerTime.HasValue && windowRT > MeasuredRTWindow.Value)
                            return i - 1;
                    }
                }
            }
            return i;
        }

        public double? PredictRetentionTime(SrmDocument document,
                                            PeptideDocNode nodePep,
                                            TransitionGroupDocNode nodeGroup,
                                            int? replicateNum,
                                            ExportSchedulingAlgorithm algorithm,
                                            bool singleWindow,
                                            out double windowRT)
        {
            // Safe defaults
            double? predictedRT = null;
            windowRT = 0;
            // Use measurements, if set and available
            bool useMeasured = (UseMeasuredRTs && MeasuredRTWindow.HasValue && document.Settings.HasResults);
            if (useMeasured)
            {
                var schedulingGroups = GetSchedulingGroups(nodePep, nodeGroup);
                var peakTime = TransitionGroupDocNode.GetSchedulingPeakTimes(schedulingGroups, document, algorithm, replicateNum);
                if (peakTime != null)
                    predictedRT = peakTime.CenterTime;
                if (predictedRT.HasValue)
                    windowRT = MeasuredRTWindow.Value;
                else if (nodePep.Children.Count > 1)
                {
                    // If their are other children of this peptide, look for one
                    // with results that can be used to predict the retention time.
                    foreach (TransitionGroupDocNode nodeGroupOther in nodePep.Children)
                    {
                        if (!ReferenceEquals(nodeGroup, nodeGroupOther))
                        {
                            peakTime = nodeGroupOther.GetSchedulingPeakTimes(document, algorithm, replicateNum);
                            if (peakTime != null)
                                predictedRT = peakTime.CenterTime;

                            if (predictedRT.HasValue)
                            {
                                windowRT = MeasuredRTWindow.Value;
                                break;
                            }
                        }
                    }
                }
            }
            // If no retention time yet, and there is a predictor, use the predictor
            if (!predictedRT.HasValue && RetentionTime != null && RetentionTime.IsUsable)
            {
                // but only if not using measured results, or the instrument supports
                // variable scheduling windows
                if (!useMeasured || !singleWindow || MeasuredRTWindow == RetentionTime.TimeWindow)
                {
                    predictedRT = RetentionTime.GetRetentionTime(document.Settings.GetModifiedSequence(nodePep));
                    windowRT = RetentionTime.TimeWindow;
                }
            }
            return predictedRT;
        }

        /// <summary>
        /// Used to help make sure that all precursors with matching retention time for
        /// a peptide get the same scheduling time when using measured results, which
        /// may have had different retention time boundaries set by the user.
        /// </summary>
        private static IEnumerable<TransitionGroupDocNode> GetSchedulingGroups(PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup)
        {
            if (nodeGroup.RelativeRT != RelativeRT.Matching)
                return new[] {nodeGroup};
            return
                nodePep.Children.Cast<TransitionGroupDocNode>().Where(
                    nodeGroupChild => nodeGroupChild.RelativeRT == RelativeRT.Matching);
        }

        /// <summary>
        /// Scheduling strategies include:
        /// - single_window - methods only allow for a single retention time window for all scheduling
        /// - all_variable_window - methods allow variable windows, but all peptides must be scheduled
        /// - any - anything goes, as long as any peptide may be scheduled
        /// </summary>
        public enum SchedulingStrategy { single_window, all_variable_window, any }

        /// <summary>
        /// Tells whether a document can be scheduled, given certain scheduling restrictions.
        /// </summary>
        /// <param name="document">The document to schedule</param>
        /// <param name="schedulingStrategy">True if the instrument supports only a single global
        /// retention time window, or false if the instrument can set the window for each transition</param>
        /// <returns>True if a scheduled method may be created from this document</returns>
        public bool CanSchedule(SrmDocument document, SchedulingStrategy schedulingStrategy)
        {
            // Check if results information can be used for retention times
            bool resultsAvailable = (UseMeasuredRTs && document.Settings.HasResults);
            bool singleWindow = (schedulingStrategy == SchedulingStrategy.single_window);

            //  If the user has assigned a retention time predictor and the calculator is usable
            if (RetentionTime != null && RetentionTime.IsUsable)
            {
                // As long as the instrument is not limited to a single retention
                // time window, or their is no option to use results information,
                // then this document can be scheduled.
                if (!singleWindow || !resultsAvailable || MeasuredRTWindow == RetentionTime.TimeWindow)
                    return true;                
            }
            // If no results available (and no predictor), then no scheduling
            if (!resultsAvailable)
                return false;

            // Otherwise, if every precursor has enough result information
            // to predict a retention time, then this document can be scheduled.
            bool anyTimes = false;
            foreach (var nodePep in document.Peptides)
            {
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    double windowRT;
                    if (PredictRetentionTime(document, nodePep, nodeGroup, null, ExportSchedulingAlgorithm.Average, singleWindow, out windowRT).HasValue)
                        anyTimes = true;
                    else if (schedulingStrategy != SchedulingStrategy.any)
                        return false;
                }
            }
            return anyTimes;
        }

        #region Property change methods

        public PeptidePrediction ChangeRetentionTime(RetentionTimeRegression prop)
        {
            return ChangeProp(ImClone(this), im => im.RetentionTime = prop);
        }

        public PeptidePrediction ChangeUseMeasuredRTs(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.UseMeasuredRTs = prop);
        }

        public PeptidePrediction ChangeMeasuredRTWindow(double? prop)
        {
            return ChangeProp(ImClone(this), im => im.MeasuredRTWindow = prop);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private PeptidePrediction()
        {
        }

        private enum ATTR
        {
            use_measured_rts,
            measured_rt_window
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (UseMeasuredRTs)
            {
                if (!MeasuredRTWindow.HasValue)
                    MeasuredRTWindow = DEFAULT_MEASURED_RT_WINDOW;
                else if (MIN_MEASURED_RT_WINDOW > MeasuredRTWindow || MeasuredRTWindow > MAX_MEASURED_RT_WINDOW)
                {
                    throw new InvalidDataException(string.Format("The retention time window {0} for a scheduled method based on measured results must be between {1} and {2}.",
                                                                 MeasuredRTWindow, MIN_MEASURED_RT_WINDOW, MAX_MEASURED_RT_WINDOW));
                }
            }

            // Defer further validation to the SrmSettings object
        }

        public static PeptidePrediction Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new PeptidePrediction());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            bool? useMeasuredRTs = reader.GetNullableBoolAttribute(ATTR.use_measured_rts);
            MeasuredRTWindow = reader.GetNullableDoubleAttribute(ATTR.measured_rt_window);
            // Keep XML values, if written by v0.5 or later 
            if (useMeasuredRTs.HasValue)
                UseMeasuredRTs = useMeasuredRTs.Value;
            // Use reasonable defaults for documents saved prior to v0.5
            else
            {
                UseMeasuredRTs = true;
                if (!MeasuredRTWindow.HasValue)
                    MeasuredRTWindow = DEFAULT_MEASURED_RT_WINDOW;
            }

            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                // Read child element
                RetentionTime = reader.DeserializeElement<RetentionTimeRegression>();
                reader.ReadEndElement();                
            }

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write this bool whether it is true or false, to allow its absence
            // as a marker of needing default values.
            writer.WriteAttribute(ATTR.use_measured_rts, UseMeasuredRTs);
            writer.WriteAttributeNullable(ATTR.measured_rt_window, MeasuredRTWindow);

            // Write child elements
            if (RetentionTime != null)
                writer.WriteElement(RetentionTime);
        }

        #endregion

        #region object overrides

        public bool Equals(PeptidePrediction other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.RetentionTime, RetentionTime) &&
                other.UseMeasuredRTs.Equals(UseMeasuredRTs) &&
                other.MeasuredRTWindow.Equals(MeasuredRTWindow);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (PeptidePrediction)) return false;
            return Equals((PeptidePrediction) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (RetentionTime != null ? RetentionTime.GetHashCode() : 0);
                result = (result*397) ^ UseMeasuredRTs.GetHashCode();
                result = (result*397) ^ (MeasuredRTWindow.HasValue ? MeasuredRTWindow.Value.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Supports filtering of a <see cref="Peptide"/> collection
    /// generated during enzyme digestion.
    /// </summary>
    public interface IPeptideFilter
    {
        /// <summary>
        /// Returns true, if a peptide should be included in an enzyme
        /// digestion list for a sequence.
        /// </summary>
        /// <param name="settings">Settings under which filtering is requested</param>
        /// <param name="peptide">The peptide being considered</param>
        /// <param name="explicitMods">Any modifications which will be applied to the peptide, or null for none</param>
        /// <param name="allowVariableMods">True if variable modifications of this peptide may produce
        /// acceptable variants of this peptide</param>
        /// <returns>True if the peptide should be included</returns>
        bool Accept(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods, out bool allowVariableMods);

        /// <summary>
        /// Returns a potential override to the maximum number of variable modifcations in the SrmSettings.
        /// </summary>
        int? MaxVariableMods { get; }
    }

    [XmlRoot("peptide_filter")]
    public class PeptideFilter : Immutable, IValidating, IPeptideFilter, IXmlSerializable
    {
        public const int MIN_EXCLUDE_NTERM_AA = 0;
        public const int MAX_EXCLUDE_NTERM_AA = 10000;
        public const int MIN_MIN_LENGTH = 2;
        public const int MAX_MIN_LENGTH = 100;
        public const int MIN_MAX_LENGTH = 5;
        public const int MAX_MAX_LENGTH = 200;

        public static readonly IPeptideFilter UNFILTERED = new AllPeptidesFilter();

        private ReadOnlyCollection<PeptideExcludeRegex> _exclusions;

        // Cached regular expressions for fast matching
        private Regex _regexExclude;
        private Regex _regexExcludeMod;
        private Regex _regexInclude;
        private Regex _regexIncludeMod;

        public PeptideFilter(int excludeNTermAAs, int minPeptideLength,
                             int maxPeptideLength, IList<PeptideExcludeRegex> exclusions, bool autoSelect)
        {
            Exclusions = exclusions;
            ExcludeNTermAAs = excludeNTermAAs;
            MinPeptideLength = minPeptideLength;
            MaxPeptideLength = maxPeptideLength;
            AutoSelect = autoSelect;
            DoValidate();
        }

        public int ExcludeNTermAAs { get; private set; }

        public int MinPeptideLength { get; private set; }

        public int MaxPeptideLength { get; private set; }

        public bool AutoSelect { get; private set; }

        public IList<PeptideExcludeRegex> Exclusions
        {
            get { return _exclusions; }
            private set
            {
                _exclusions = MakeReadOnly(value);

                _regexExclude = _regexExcludeMod =
                    _regexInclude = _regexIncludeMod = null;
            }
        }

        #region Property change methods

        public PeptideFilter ChangeExcludeNTermAAs(int prop)
        {
            return ChangeProp(ImClone(this), im => im.ExcludeNTermAAs = prop);
        }

        public PeptideFilter ChangeMinPeptideLength(int prop)
        {
            return ChangeProp(ImClone(this), im => im.MinPeptideLength = prop);
        }

        public PeptideFilter ChangeMaxPeptideLength(int prop)
        {
            return ChangeProp(ImClone(this), im => im.MaxPeptideLength = prop);
        }

        public PeptideFilter ChangeAutoSelect(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.AutoSelect = prop);
        }

        public PeptideFilter ChangeExclusions(IList<PeptideExcludeRegex> prop)
        {
            return ChangeProp(ImClone(this), im => im.Exclusions = prop);
        }
        #endregion

        public bool Accept(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods, out bool allowVariableMods)
        {
            allowVariableMods = false;

            // Must begin after excluded C-terminal AAs
            if (peptide.Begin.HasValue && peptide.Begin.Value < ExcludeNTermAAs)
                return false;

            // Must be within acceptable length range
            string sequence = peptide.Sequence;
            int len = sequence.Length;
            if (MinPeptideLength > len || len > MaxPeptideLength)
                return false;

            // No exclusion matches allowed
            if (_regexExclude != null && _regexExclude.Match(sequence).Success)
                return false;
            if (_regexInclude != null && !_regexInclude.Match(sequence).Success)
                return false;

            // Allow variable mods beyond this point, since filtering occurs based
            // on the modification state of the peptide.
            allowVariableMods = true;

            if (_regexExcludeMod != null || _regexIncludeMod != null)
            {
                var calcMod = settings.GetPrecursorCalc(IsotopeLabelType.light, explicitMods);
                // Use narrow format, since this is mostly what is presented to
                // the user creating the exclusion expressions.
                string sequenceMod = calcMod.GetModifiedSequence(sequence, true);
                if (_regexExcludeMod != null && _regexExcludeMod.Match(sequenceMod).Success)
                    return false;
                if (_regexIncludeMod != null && !_regexIncludeMod.Match(sequenceMod).Success)
                    return false;
            }

            return true;
        }

        public int? MaxVariableMods
        {
            get { return null; }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private PeptideFilter()
        {
        }

        private enum ATTR
        {
            start,
            min_length,
            max_length,
            auto_select,
        }

        private enum EL
        {
            peptide_exclusions
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            // These values are repeated in PeptideSettingsUI
            ValidateIntRange("excluded n-terminal amino acids", ExcludeNTermAAs,
                MIN_EXCLUDE_NTERM_AA, MAX_EXCLUDE_NTERM_AA);
            ValidateIntRange("minimum peptide length", MinPeptideLength,
                MIN_MIN_LENGTH, MAX_MIN_LENGTH);
            ValidateIntRange("maximum peptide length", MaxPeptideLength,
                Math.Max(MIN_MAX_LENGTH, MinPeptideLength), MAX_MAX_LENGTH);

            if (_regexExclude != null)
                return;

            // Build and validate the exclusion regular expression
            StringBuilder sb = new StringBuilder();
            StringBuilder sbMod = new StringBuilder();
            StringBuilder sbInc = new StringBuilder();
            StringBuilder sbIncMod = new StringBuilder();
            foreach (PeptideExcludeRegex exclude in _exclusions)
            {
                if (!string.IsNullOrEmpty(exclude.Regex))
                {
                    // Try each individual expression to make sure it is a valid Regex,
                    // in order to give the user a more informative error expression.
                    try
                    {
                        new Regex(exclude.Regex);
                    }
                    catch(ArgumentException x)
                    {
                        throw new InvalidDataException(string.Format("The peptide exclusion {0} has an invalid regular expression '{1}'.", exclude.Name, exclude.Regex), x);
                    }

                    // Add this expression to the single expression that will be used
                    // in filtering.
                    if (exclude.IsIncludeMatch)
                        AddRegEx(exclude.IsMatchMod ? sbIncMod : sbInc, exclude.Regex);
                    else
                        AddRegEx(exclude.IsMatchMod ? sbMod : sb, exclude.Regex);
                }
            }

            // Hold the constructed Regex expressions for use in filtering.
            _regexExclude = ExcludeExprToRegEx(sb.ToString());
            _regexExcludeMod = ExcludeExprToRegEx(sbMod.ToString());
            _regexInclude = ExcludeExprToRegEx(sbInc.ToString());
            _regexIncludeMod = ExcludeExprToRegEx(sbIncMod.ToString());
        }

        private static void AddRegEx(StringBuilder sb, string regex)
        {
            if (sb.Length > 0)
                sb.Append('|');
            sb.Append(regex);
        }

        private static Regex ExcludeExprToRegEx(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return null;

            try
            {
                return new Regex(expression);
            }
            catch (ArgumentException x)
            {
                throw new InvalidDataException("Invalid exclusion list.", x);
            }
        }

        private static void ValidateIntRange(string label, int n, int min, int max)
        {
            if (min > n || n > max)
                throw new InvalidDataException(string.Format("The value {1} for {0} must be between {2} and {3}.", label, n, min, max));
        }

        public static PeptideFilter Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new PeptideFilter());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read start tag attributes
            ExcludeNTermAAs = reader.GetIntAttribute(ATTR.start);
            MinPeptideLength = reader.GetIntAttribute(ATTR.min_length);
            MaxPeptideLength = reader.GetIntAttribute(ATTR.max_length);
            AutoSelect = reader.GetBoolAttribute(ATTR.auto_select);

            var list = new List<PeptideExcludeRegex>();

            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                // Read child elements
                reader.ReadElementList(EL.peptide_exclusions, list);
                reader.ReadEndElement();
            }

            Exclusions = list;

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.start, ExcludeNTermAAs);
            writer.WriteAttribute(ATTR.min_length, MinPeptideLength);
            writer.WriteAttribute(ATTR.max_length, MaxPeptideLength);
            writer.WriteAttribute(ATTR.auto_select, AutoSelect);
            // Write child elements
            writer.WriteElementList(EL.peptide_exclusions, Exclusions);
        }

        #endregion

        #region object overrides

        public bool Equals(PeptideFilter obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.ExcludeNTermAAs == ExcludeNTermAAs &&
                   obj.MinPeptideLength == MinPeptideLength &&
                   obj.MaxPeptideLength == MaxPeptideLength &&
                   obj.AutoSelect.Equals(AutoSelect) &&
                   ArrayUtil.EqualsDeep(obj._exclusions, _exclusions);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (PeptideFilter)) return false;
            return Equals((PeptideFilter) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = ExcludeNTermAAs;
                result = (result*397) ^ MinPeptideLength;
                result = (result*397) ^ MaxPeptideLength;
                result = (result*397) ^ AutoSelect.GetHashCode();
                result = (result*397) ^ _exclusions.GetHashCodeDeep();
                return result;
            }
        }

        #endregion

        /// <summary>
        /// Used for choosing all peptides unfiltered.
        /// </summary>
        private class AllPeptidesFilter : IPeptideFilter
        {
            #region Implementation of IPeptideFilter

            public bool Accept(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods, out bool allowVariableMods)
            {
                allowVariableMods = true;
                return true;
            }

            public int? MaxVariableMods
            {
                get { return null; }
            }

            #endregion
        }
    }

    [XmlRoot("peptide_modifications")]
    public class PeptideModifications : Immutable, IValidating, IXmlSerializable
    {
        public const int DEFAULT_MAX_VARIABLE_MODS = 3;
        public const int MIN_MAX_VARIABLE_MODS = 1;
        public const int MAX_MAX_VARIABLE_MODS = 10;
        public const int DEFAULT_MAX_NEUTRAL_LOSSES = 1;
        public const int MIN_MAX_NEUTRAL_LOSSES = 1;
        public const int MAX_MAX_NEUTRAL_LOSSES = 5;

        [ThreadStatic]
        private static PeptideModifications _serializationContext;

        public static void SetSerializationContext(PeptideModifications mods)
        {
            _serializationContext = mods;
        }

        private ReadOnlyCollection<TypedModifications> _modifications;
        private ReadOnlyCollection<IsotopeLabelType> _internalStandardTypes;

        public PeptideModifications(IList<StaticMod> staticMods,
            IList<TypedModifications> heavyMods)
            : this(staticMods, DEFAULT_MAX_VARIABLE_MODS, DEFAULT_MAX_NEUTRAL_LOSSES,
                   heavyMods, new[] { IsotopeLabelType.heavy })
        {
            // Make sure the internal standard type is reference equal with
            // the first heavy type.
            var enumHeavy = heavyMods.GetEnumerator();
            if (enumHeavy.MoveNext() && enumHeavy.Current != null)
                InternalStandardTypes = new[] {enumHeavy.Current.LabelType};
        }

        public PeptideModifications(IList<StaticMod> staticMods,
            int maxVariableMods, int maxNeutralLosses,
            IEnumerable<TypedModifications> heavyMods,
            IList<IsotopeLabelType> internalStandardTypes)
        {
            MaxVariableMods = maxVariableMods;
            MaxNeutralLosses = maxNeutralLosses;

            var modifications = new List<TypedModifications>
                                    {new TypedModifications(IsotopeLabelType.light, staticMods)};
            modifications.AddRange(heavyMods);
            _modifications = MakeReadOnly(modifications.ToArray());

            Debug.Assert(internalStandardTypes.Count > 0);
            InternalStandardTypes = internalStandardTypes;

            DoValidate();
        }

        public int MaxVariableMods { get; private set; }
        public int MaxNeutralLosses { get; private set; }
        public int CountLabelTypes { get { return _modifications.Count; } }

        public IList<IsotopeLabelType> InternalStandardTypes
        {
            get { return _internalStandardTypes; }
            private set { _internalStandardTypes = MakeReadOnly(value); }
        }

        public IList<StaticMod> StaticModifications
        {
            get { return _modifications[0].Modifications; }
        }

        public bool HasVariableModifications
        {
            get { return StaticModifications.Contains(mod => mod.IsVariable); }
        }

        public IEnumerable<StaticMod> VariableModifications
        {
            get 
            {
                return from mod in StaticModifications
                       where mod.IsVariable
                       select mod;
            }
        }

        public bool HasNeutralLosses
        {
            get { return StaticModifications.Contains(mod => mod.HasLoss && !mod.IsExplicit); }
        }

        public IEnumerable<StaticMod> NeutralLossModifications
        {
            get
            {
                return from mod in StaticModifications
                       where mod.HasLoss && !mod.IsExplicit
                       select mod;
            }
        }

        public IList<StaticMod> HeavyModifications
        {
            get { return GetModifications(IsotopeLabelType.heavy); }
        }

        public IList<StaticMod> GetModifications(IsotopeLabelType labelType)
        {
            int index = GetModIndex(labelType);
            if (index == -1)
                return new StaticMod[0];
            return _modifications[index].Modifications;
        }

        private int GetModIndex(IsotopeLabelType labelType)
        {
            return _modifications.IndexOf(mod => ReferenceEquals(labelType, mod.LabelType));
        }

        public TypedModifications GetModificationsByName(string typeName)
        {
            int index = GetModIndexByName(typeName);
            if (index == -1)
                return null;
            return _modifications[index];
        }

        private int GetModIndexByName(string typeName)
        {
            return _modifications.IndexOf(mod => Equals(typeName, mod.LabelType.Name));
        }

        public IEnumerable<TypedModifications> GetHeavyModifications()
        {
            for (int i = 1; i < _modifications.Count; i++)
                yield return _modifications[i];
        }

        public IEnumerable<IsotopeLabelType> GetHeavyModificationTypes()
        {
            return from typedMod in GetHeavyModifications()
                   select typedMod.LabelType;
        }

        public IEnumerable<IsotopeLabelType> GetModificationTypes()
        {
            return from typedMod in _modifications
                   select typedMod.LabelType;
        }

        public bool HasHeavyModifications
        {
            get { return GetHeavyModifications().Contains(mods => mods.Modifications.Count > 0); }
        }

        public bool HasHeavyImplicitModifications
        {
            get 
            {
                return GetHeavyModifications().Contains(typedMods => typedMods.HasImplicitModifications);
            }
        }

        public bool HasModification(StaticMod staticMod)
        {
            return _modifications.SelectMany(typeModifications => typeModifications.Modifications).Contains(staticMod);
        }

        #region Property change methods

        public PeptideModifications ChangeMaxVariableMods(int prop)
        {
            return ChangeProp(ImClone(this), im => im.MaxVariableMods = prop);
        }

        public PeptideModifications ChangeMaxNeutralLosses(int prop)
        {
            return ChangeProp(ImClone(this), im => im.MaxNeutralLosses = prop);
        }

        public PeptideModifications ChangeInternalStandardTypes(IList<IsotopeLabelType> prop)
        {
            return ChangeProp(ImClone(this), im => im.InternalStandardTypes = prop);
        }

        public PeptideModifications ChangeStaticModifications(IList<StaticMod> prop)
        {
            return ChangeModifications(IsotopeLabelType.light, prop);
        }

        public PeptideModifications ChangeHeavyModifications(IList<StaticMod> prop)
        {
            return ChangeModifications(IsotopeLabelType.heavy, prop);
        }

        public PeptideModifications ChangeModifications(IsotopeLabelType labelType, IList<StaticMod> prop)
        {
            int index = GetModIndex(labelType);
            if (index == -1)
                throw new IndexOutOfRangeException(string.Format("Modification type {0} not found.", labelType));
            var modifications = _modifications.ToArrayStd();
            modifications[index] = new TypedModifications(labelType, prop);
            return ChangeProp(ImClone(this), im => im._modifications = MakeReadOnly(modifications));
        }

        public PeptideModifications DeclareExplicitMods(SrmDocument doc,
            IList<StaticMod> listStaticMods, IList<StaticMod> listHeavyMods)
        {
            var modifications = new List<TypedModifications>
                                    {DeclareExplicitMods(doc, listStaticMods, _modifications[0])};
            foreach (TypedModifications typedMods in GetHeavyModifications())
                modifications.Add(DeclareExplicitMods(doc, listHeavyMods, typedMods));

            // If nothing changed, return this
            if (ArrayUtil.ReferencesEqual(modifications, _modifications))
                return this;

            return ChangeProp(ImClone(this), im => im._modifications = MakeReadOnly(modifications));
        }

        private static TypedModifications DeclareExplicitMods(SrmDocument doc,
            IEnumerable<StaticMod> listMods, TypedModifications typedMods)
        {
            Dictionary<string, StaticMod> explicitStaticMods;
            IList<StaticMod> mods = SplitModTypes(typedMods.Modifications,
                listMods, out explicitStaticMods);

            foreach (PeptideDocNode nodePep in doc.Peptides)
            {
                if (!nodePep.HasExplicitMods)
                    continue;
                // Variable modifications do not count
                if (nodePep.ExplicitMods.IsVariableStaticMods && typedMods.LabelType.IsLight)
                    continue;
                var explicitMods = nodePep.ExplicitMods.GetModifications(typedMods.LabelType);
                if (explicitMods == null)
                    continue;
                DeclareExplicitMods(mods, explicitStaticMods, explicitMods);
            }

            if (ArrayUtil.EqualsDeep(mods, typedMods.Modifications))
                return typedMods;
            return new TypedModifications(typedMods.LabelType, mods);
        }

        private static IList<StaticMod> SplitModTypes(IEnumerable<StaticMod> mods,
            IEnumerable<StaticMod> listModsGlobal, out Dictionary<string, StaticMod> explicitMods)
        {
            List<StaticMod> implicitMods = new List<StaticMod>();
            // Make sure all global mods are available to explicit mods with their
            // current settings values
            explicitMods = listModsGlobal.ToDictionary(mod => mod.Name);
            foreach (StaticMod mod in mods)
            {
                if (!mod.IsUserSet)
                {
                    implicitMods.Add(mod);
                    explicitMods.Remove(mod.Name);
                }
                // If the global list did not contain this mod for some reason,
                // then use the value on the peptide
                else if (!explicitMods.ContainsKey(mod.Name))
                {
                    explicitMods.Add(mod.Name, mod);                    
                }
            }
            return implicitMods;
        }

        private static void DeclareExplicitMods(IList<StaticMod> mods,
            IDictionary<string, StaticMod> explicitStaticMods,
            IEnumerable<ExplicitMod> explicitMods)
        {
            // Enumerate all modifications user has made explicitly
            foreach (ExplicitMod mod in explicitMods)
            {
                string modName = mod.Modification.Name;
                // If the current modification cannot be found in the document static mods
                if (mods.IndexOf(modStatic => Equals(modName, modStatic.Name)) == -1)
                {
                    StaticMod modStatic;
                    // Try to get the desired modification from available global modifications
                    if (!explicitStaticMods.TryGetValue(modName, out modStatic))
                        // Otherwise, remove this modification if it is no longer available
                        continue;
                    // Make sure it is marked explicit
                    if (!modStatic.IsUserSet)
                        modStatic = modStatic.ChangeExplicit(true);
                    mods.Add(modStatic);
                }
            }
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private PeptideModifications()
        {
        }

        private enum EL
        {
            static_modifications,
            heavy_modifications,
            internal_standard
        }

        private enum ATTR
        {
            max_variable_mods,
            max_neutral_losses,
            isotope_label,
            internal_standard,
            name
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (MIN_MAX_VARIABLE_MODS > MaxVariableMods || MaxVariableMods > MAX_MAX_VARIABLE_MODS)
            {
                throw new InvalidDataException(string.Format("Maximum variable modifications {0} must be between {1} and {2}",
                    MaxVariableMods, MIN_MAX_VARIABLE_MODS, MAX_MAX_VARIABLE_MODS));
            }
            if (MIN_MAX_NEUTRAL_LOSSES > MaxNeutralLosses || MaxNeutralLosses > MAX_MAX_NEUTRAL_LOSSES)
            {
                throw new InvalidDataException(string.Format("Maximum neutral losses {0} must be between {1} and {2}",
                    MaxNeutralLosses, MIN_MAX_NEUTRAL_LOSSES, MAX_MAX_NEUTRAL_LOSSES));
            }
        }

        public static PeptideModifications Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new PeptideModifications());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            var list = new List<TypedModifications>();
            var internalStandardTypes = new[] {IsotopeLabelType.heavy};

            MaxVariableMods = reader.GetIntAttribute(ATTR.max_variable_mods, DEFAULT_MAX_VARIABLE_MODS);
            MaxNeutralLosses = reader.GetIntAttribute(ATTR.max_neutral_losses, DEFAULT_MAX_NEUTRAL_LOSSES);

            // Consume tag
            if (reader.IsEmptyElement)
            {
                list.Add(new TypedModifications(IsotopeLabelType.light, new StaticMod[0]));
                reader.Read();                
            }
            else
            {
                var internalStandardNames = new List<string>();
                string internalStandardName = reader.GetAttribute(ATTR.internal_standard);

                reader.ReadStartElement();

                if (internalStandardName != null)
                    internalStandardNames.Add(internalStandardName);
                else
                {
                    while (reader.IsStartElement(EL.internal_standard))
                    {
                        internalStandardNames.Add(reader.GetAttribute(ATTR.name));
                        reader.Read();
                    }
                }

                if (internalStandardNames.Count > 0)
                    internalStandardTypes = new IsotopeLabelType[internalStandardNames.Count];

                SetInternalStandardType(IsotopeLabelType.light, internalStandardNames, internalStandardTypes);

                // Read child elements
                var listMods = new List<StaticMod>();
                reader.ReadElementList(EL.static_modifications, listMods);
                list.Add(new TypedModifications(IsotopeLabelType.light, listMods));
                int typeOrder = IsotopeLabelType.FirstHeavy;
                while (reader.IsStartElement(EL.heavy_modifications))
                {
                    // If first heavy tag has no isotope_label attribute, use the default heavy type
                    var labelType = IsotopeLabelType.heavy;
                    string typeName = reader.GetAttribute(ATTR.isotope_label);
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        labelType = new IsotopeLabelType(typeName, typeOrder);
                        if (Equals(labelType, IsotopeLabelType.heavy))
                            labelType = IsotopeLabelType.heavy;
                        // If the created label type is going to be used and there are serialization
                        // context modifications, try to use the label types from the context.
                        else if (_serializationContext != null)
                        {
                            var modsContext = _serializationContext.GetModificationsByName(typeName);
                            if (modsContext != null)
                            {
                                // CONSIDER: Should this require full equality, including order?
                                labelType = modsContext.LabelType;
                            }
                        }
                    }
                    else if (typeOrder > IsotopeLabelType.FirstHeavy)
                        throw new InvalidDataException(string.Format("Heavy modifications found without '{0}' attribute.", ATTR.isotope_label));
                    typeOrder++;

                    SetInternalStandardType(labelType, internalStandardNames, internalStandardTypes);

                    // If no internal standard type was given, use the first heavy type.
                    if (internalStandardNames.Count == 0)
                    {
                        internalStandardNames.Add(labelType.Name);
                        internalStandardTypes = new[] {labelType};
                    }
                    listMods = new List<StaticMod>();
                    reader.ReadElementList(EL.heavy_modifications, listMods);

                    list.Add(new TypedModifications(labelType, listMods));
                }
                int iMissingType = internalStandardTypes.IndexOf(labelType => labelType == null);
                if (iMissingType != -1)
                    throw new InvalidDataException(string.Format("Internal standard type {0} not found.", internalStandardNames[iMissingType]));
                reader.ReadEndElement();
            }

            if (list.Count < 2)
                list.Add(new TypedModifications(IsotopeLabelType.heavy, new StaticMod[0]));

            _modifications = MakeReadOnly(list.ToArray());
            InternalStandardTypes = internalStandardTypes;
        }

        private static void SetInternalStandardType(IsotopeLabelType labelType,
            IList<string> labelNames, IList<IsotopeLabelType> labelTypes)
        {
            int i = labelNames.IndexOf(labelType.Name);
            if (i != -1)
                labelTypes[i] = labelType;
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attibutes
            writer.WriteAttribute(ATTR.max_variable_mods, MaxVariableMods);
            writer.WriteAttribute(ATTR.max_neutral_losses, MaxNeutralLosses);
            if (InternalStandardTypes.Count == 1)
            {
                var internalStandardType = InternalStandardTypes[0];
                if (!ReferenceEquals(internalStandardType, IsotopeLabelType.heavy))
                    writer.WriteAttribute(ATTR.internal_standard, internalStandardType.Name);
            }
            else
            {
                foreach (var labelType in InternalStandardTypes)
                {
                    writer.WriteStartElement(EL.internal_standard);
                    writer.WriteAttribute(ATTR.name, labelType.Name);
                    writer.WriteEndElement();
                }
            }

            // Write child elements
            if (StaticModifications.Count > 0)
                writer.WriteElementList(EL.static_modifications, StaticModifications);
            foreach (var heavyMods in GetHeavyModifications())
            {
                writer.WriteStartElement(EL.heavy_modifications);
                if (!ReferenceEquals(heavyMods.LabelType, IsotopeLabelType.heavy))
                    writer.WriteAttribute(ATTR.isotope_label, heavyMods.LabelType);
                writer.WriteElements(heavyMods.Modifications);
                writer.WriteEndElement();
            }
        }

        #endregion

        #region object overrides

        public bool Equals(PeptideModifications obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.MaxVariableMods == MaxVariableMods &&
                   obj.MaxNeutralLosses == MaxNeutralLosses &&
                   ArrayUtil.EqualsDeep(obj.InternalStandardTypes, InternalStandardTypes) &&
                   ArrayUtil.EqualsDeep(obj._modifications, _modifications);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (PeptideModifications)) return false;
            return Equals((PeptideModifications) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = MaxVariableMods.GetHashCode();
                result = (result * 397) ^ MaxNeutralLosses.GetHashCode();
                result = (result * 397) ^ InternalStandardTypes.GetHashCodeDeep();
                result = (result * 397) ^ _modifications.GetHashCodeDeep();
                return result;
            }
        }

        #endregion

    }

    // Order is important: PeptideSettingsUI refers to these as integers
    public enum PeptidePick { library, filter, both, either }

    [XmlRoot("peptide_libraries")]
    public sealed class PeptideLibraries : Immutable, IValidating, IXmlSerializable
    {
        public const int MIN_PEPTIDE_COUNT = 1;
        public const int MAX_PEPTIDE_COUNT = 20;

        private ReadOnlyCollection<LibrarySpec> _librarySpecs;
        private ReadOnlyCollection<Library> _libraries;
        private ReadOnlyCollection<Library> _disconnectedLibraries;

        public PeptideLibraries(PeptidePick pick, PeptideRankId rankId, int? peptideCount,
            IList<LibrarySpec> librarySpecs, IList<Library> libraries)
        {
            Pick = pick;
            RankId = rankId;
            PeptideCount = peptideCount;
            LibrarySpecs = librarySpecs;
            Libraries = libraries;

            DoValidate();
        }

        public PeptidePick Pick { get; private set; }
        public PeptideRankId RankId { get; private set; }
        public int? PeptideCount { get; private set; }

        public bool HasLibraries { get { return _librarySpecs.Count > 0; } }

        public IList<LibrarySpec> LibrarySpecs
        {
            get { return _librarySpecs; }
            private set { _librarySpecs = MakeReadOnly(value); }
        }

        public IEnumerable<LibrarySpec> LibrarySpecsUnloaded
        {
            get
            {
                for (int i = 0; i < _librarySpecs.Count; i++)
                {
                    var lib = _libraries[i];
                    if (lib == null || !lib.IsLoaded)
                        yield return _librarySpecs[i];
                }
            }
        }

        public IList<Library> Libraries
        {
            get { return _libraries; }
            private set { _libraries = MakeReadOnly(value); }
        }

        public IList<Library> DisconnectedLibraries
        {
            get { return _disconnectedLibraries; }
        }

        public Library GetLibrary(string name)
        {
            for (int i = 0; i < _libraries.Count; i++)
            {
                if (Equals(name, GetLibraryName(i)))
                    return _libraries[i];
            }
            return null;
        }

        private string GetLibraryName(int index)
        {
            // CONSIDER: It should be possible to just check _librarySpecs
            //           since its values may only be null after load, until
            //           SrmSettings.Validate, but this is the safest code.
            return (_librarySpecs[index] != null ? _librarySpecs[index].Name :
                (_libraries[index] != null ? _libraries[index].Name : null));
        }

        public bool IsLoaded
        {
            get
            {
                foreach (Library lib in _libraries)
                {
                    if (lib == null || !lib.IsLoaded)
                        return false;
                }
                return true;
            }
        }

        public bool Contains(LibKey key)
        {
            Debug.Assert(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && lib.Contains(key))
                    return true;
            }
            return false;
        }

        public bool ContainsAny(LibSeqKey key)
        {
            Debug.Assert(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && lib.ContainsAny(key))
                    return true;
            }
            return false;
        }

        public bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            Debug.Assert(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && lib.TryGetLibInfo(key, out libInfo))
                    return true;
            }
            libInfo = null;
            return false;
        }

        public bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum)
        {
            Debug.Assert(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && lib.TryLoadSpectrum(key, out spectrum))
                    return true;
            }
            spectrum = null;
            return false;
        }

        public bool TryGetRetentionTimes(LibKey key, string filePath, out double[] retentionTimes)
        {
            Debug.Assert(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && lib.TryGetRetentionTimes(key, filePath, out retentionTimes))
                    return true;
            }
            retentionTimes = null;
            return false;
        }

        public bool TryGetRetentionTimes(string filePath, out LibraryRetentionTimes retentionTimes)
        {
            Debug.Assert(IsLoaded);

            foreach (Library lib in _libraries)
            {
                // Only one of the available libraries may claim ownership of the file
                // in question.
                if (lib != null && lib.TryGetRetentionTimes(filePath, out retentionTimes))
                    return true;
            }
            retentionTimes = null;
            return false;
        }

        /// <summary>
        /// Loads all the spectra found in all the loaded libraries with the
        /// given LibKey and IsotopeLabelType.
        /// </summary>
        /// <param name="key">The LibKey to match on</param>
        /// <param name="labelType">The IsotopeLabelType to match on</param>
        /// <param name="bestMatch">True if only best-match spectra are included</param>
        public IEnumerable<SpectrumInfo> GetSpectra(LibKey key, IsotopeLabelType labelType, bool bestMatch)
        {
            Debug.Assert(IsLoaded);

            return _libraries.Where(lib => lib != null).SelectMany(lib => lib.GetSpectra(key, labelType, bestMatch));
        }

        #region Property change methods

        public PeptideLibraries ChangePick(PeptidePick prop)
        {
            return ChangeProp(ImClone(this), im => im.Pick = prop);
        }

        public PeptideLibraries ChangeRankId(PeptideRankId prop)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.RankId = prop;
                                                     if (prop == null)
                                                         im.PeptideCount = null;
                                                 });
        }

        public PeptideLibraries ChangePeptideCount(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.PeptideCount = prop);
        }

        public PeptideLibraries ChangeLibrarySpecs(IList<LibrarySpec> prop)
        {
            return ChangeProp(ImClone(this),
                              im =>
                                  {
                                      im.LibrarySpecs = prop;
                                      // Keep the libraries array in synch, reloading all libraries, if necessary.
                                      // CONSIDER: Loop checking name matching?
                                      if (im.Libraries.Count != prop.Count)
                                          im.Libraries = new Library[prop.Count];
                                  });
        }        

        public PeptideLibraries ChangeLibraries(IList<Library> prop)
        {
            return ChangeProp(ImClone(this), im => im.Libraries = prop);
        }

        public PeptideLibraries ChangeLibraries(IList<LibrarySpec> specs, IList<Library> libs)
        {
            return ChangeProp(ImClone(this),
                              im =>
                                  {
                                      im.LibrarySpecs = specs;
                                      im.Libraries = libs;
                                  });
        }

        public PeptideLibraries Disconnect()
        {
            var libClone = ImClone(this);
            libClone._disconnectedLibraries = _libraries;
            libClone.Libraries = new Library[0];
            libClone.LibrarySpecs = new LibrarySpec[0];
            return libClone;
        }

        public delegate string FindLibrary(string libraryName, string fileName);

        public PeptideLibraries MergeLibrarySpecs(PeptideLibraries newPepLibraries, FindLibrary findLibrary)
        {
            IList<LibrarySpec> librarySpecs = LibrarySpecs.ToList();

            for (int i = 0; i < newPepLibraries.Libraries.Count; i++)
            {
                LibrarySpec spec = newPepLibraries.LibrarySpecs[i];
                Library library = newPepLibraries.Libraries[i];
                string libraryName;
                string fileName;
                if (library != null)
                {
                    libraryName = library.Name;
                    fileName = library.FileNameHint;
                }
                else if (spec != null)
                {
                    libraryName = spec.Name;
                    fileName = Path.GetFileName(spec.FilePath);
                }
                else
                {
                    // If no library and no spec, give up
                    continue;
                }

                // If already in the list, nothing more to do
                if (librarySpecs.Contains(s => Equals(s.Name, libraryName)))
                    continue;

                // If the named spec is in the global settings, use it
                // CONSIDER: Remove access to Settings from model?
                LibrarySpec specSettings;
                if (Settings.Default.SpectralLibraryList.TryGetValue(libraryName, out specSettings))
                {
                    librarySpecs.Add(specSettings);
                    continue;
                }

                // If it is a library spec, and the path exists then use the spec
                if (spec != null && File.Exists(spec.FilePath))
                {
                    librarySpecs.Add(spec);
                    Settings.Default.SpectralLibraryList.Add(spec);
                    continue;
                }

                // If there is no filename, give up
                if (fileName == null)
                    continue;

                // Look for the library in the user's default library path
                var pathLibrary = Path.Combine(Settings.Default.LibraryDirectory, fileName);
                if (!File.Exists(pathLibrary) && findLibrary != null)
                {
                    pathLibrary = findLibrary(libraryName, fileName);
                    if (pathLibrary == null)
                        continue;
                }

                // Create a new library spec from the library, or change existing library
                // spec path to the newly found path.
                var libSpec = (library != null
                                   ? library.CreateSpec(pathLibrary)
                                   : spec.ChangeFilePath(pathLibrary));
                librarySpecs.Add(libSpec);
                Settings.Default.SpectralLibraryList.Add(libSpec);
            }

            return ChangeLibrarySpecs(librarySpecs);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private PeptideLibraries()
        {
        }

        // Temporary storage of the rank ID name, until all LibrarySpecs
        // are connected during SrmSettings.Validate().
        private string _rankIdName;

        private void EnsureRankId()
        {
            string idName = (RankId != null ? RankId.Value : _rankIdName);
            if (idName == null)
                return;

            // Look for the rank ID in the specified LibrarySpecs.
            // They should all have it, or this is not a valid ranking.
            PeptideRankId idFound = null;
            foreach (LibrarySpec spec in LibrarySpecs)
            {
                // Not possible to reconcile until all library specs are loaded
                if (spec == null)
                    return;

                // Find the rank ID in each library.
                idFound = null;

                foreach (PeptideRankId id in spec.PeptideRankIds)
                {
                    if (Equals(idName, id.Value))
                    {
                        idFound = id;
                        break;
                    }
                }

                // Not found in one of the libraries.
                if (idFound == null)
                    break;
            }

            if (idFound == null)
                throw new InvalidDataException(string.Format("Specified libraries do not support the '{0}' peptide ranking.", idName));

            // No longer necessary
            _rankIdName = null;

            RankId = idFound;
        }

        private enum ATTR
        {
            pick,
            rank_type,
            peptide_count
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if ((Pick == PeptidePick.filter || Pick == PeptidePick.either) && RankId != null)
                throw new InvalidDataException("The specified method of matching library spectra does not support peptide ranking.");
            if (_rankIdName == null && RankId == null && PeptideCount != null)
                throw new InvalidDataException("Limiting peptides per protein requires a ranking method to be specified.");

            EnsureRankId();

            if (PeptideCount.HasValue && (PeptideCount.Value < MIN_PEPTIDE_COUNT || PeptideCount.Value > MAX_PEPTIDE_COUNT))
            {
                throw new InvalidDataException(string.Format("Library picked peptide count {0} must be between {1} and {2}.",
                    PeptideCount, MIN_PEPTIDE_COUNT, MAX_PEPTIDE_COUNT));
            }

            // Libraries and library specs must match.  If they do not, then
            // there was a coding error.
            Helpers.Assume(LibrariesMatch(), "Libraries and library specifications do not match.");

            // Leave connecting the libraries to the LibrarySpecs in the
            // SpectralLibraryList until the root settings object is validated.
        }

        private bool LibrariesMatch()
        {
            if (LibrarySpecs.Count != Libraries.Count)
                return false;
            for (int i = 0; i < LibrarySpecs.Count; i++)
            {
                if (LibrarySpecs[i] != null && Libraries[i] != null &&
                        !Equals(LibrarySpecs[i].Name, Libraries[i].Name))
                    return false;
            }
            return true;
        }

        // Support for serializing multiple library types
        private static readonly IXmlElementHelper<Library>[] LIBRARY_HELPERS =
        {
            new XmlElementHelperSuper<BiblioSpecLibrary, Library>(),                 
            new XmlElementHelperSuper<BiblioSpecLiteLibrary, Library>(),                 
            new XmlElementHelperSuper<XHunterLibrary, Library>(),                 
            new XmlElementHelperSuper<NistLibrary, Library>(),
            new XmlElementHelperSuper<SpectrastLibrary, Library>(),
        };

        private static readonly IXmlElementHelper<LibrarySpec>[] LIBRARY_SPEC_HELPERS =
        {
            new XmlElementHelperSuper<BiblioSpecLibSpec, LibrarySpec>(),                 
            new XmlElementHelperSuper<BiblioSpecLiteSpec, LibrarySpec>(),                 
            new XmlElementHelperSuper<XHunterLibSpec, LibrarySpec>(),                 
            new XmlElementHelperSuper<NistLibSpec, LibrarySpec>(),                 
            new XmlElementHelperSuper<SpectrastSpec, LibrarySpec>(),                 
        };

        public static IXmlElementHelper<LibrarySpec>[] LibrarySpecXmlHelpers
        {
            get { return LIBRARY_SPEC_HELPERS; }
        }

        private static readonly IXmlElementHelper<SpectrumHeaderInfo>[] LIBRARY_HEADER_HELPERS =
        {
            new XmlElementHelperSuper<BiblioSpecSpectrumHeaderInfo, SpectrumHeaderInfo>(),
            new XmlElementHelperSuper<XHunterSpectrumHeaderInfo, SpectrumHeaderInfo>(),                 
            new XmlElementHelperSuper<NistSpectrumHeaderInfo, SpectrumHeaderInfo>(),                 
            new XmlElementHelperSuper<SpectrastSpectrumHeaderInfo, SpectrumHeaderInfo>(),                 
        };

        public static IXmlElementHelper<SpectrumHeaderInfo>[] SpectrumHeaderXmlHelpers
        {
            get { return LIBRARY_HEADER_HELPERS; }
        }

        public static PeptideLibraries Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new PeptideLibraries());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Pick = reader.GetEnumAttribute(ATTR.pick, PeptidePick.library);
            PeptideCount = reader.GetNullableIntAttribute(ATTR.peptide_count);

            _rankIdName = reader.GetAttribute(ATTR.rank_type);

            var list = new List<XmlNamedElement>();

            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                // Read child elements
                IXmlElementHelper<Library> helperLib;
                IXmlElementHelper<LibrarySpec> helperSpec = null;
                while ((helperLib = reader.FindHelper(LIBRARY_HELPERS)) != null ||
                        (helperSpec = reader.FindHelper(LIBRARY_SPEC_HELPERS)) != null)
                {
                    if (helperLib != null)
                        list.Add(helperLib.Deserialize(reader));
                    else
                        list.Add(helperSpec.Deserialize(reader));

                }
                reader.ReadEndElement();
            }

            var libraries = new Library[list.Count];
            var librarySpecs = new LibrarySpec[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Library)
                    libraries[i] = (Library) list[i];
                else
                    librarySpecs[i] = (LibrarySpec) list[i];
            }
            Libraries = libraries;
            LibrarySpecs = librarySpecs;

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.pick, Pick);
            if (RankId != null)
                writer.WriteAttribute(ATTR.rank_type, RankId.Value);
            writer.WriteAttributeNullable(ATTR.peptide_count, PeptideCount);

            // Write child elements
            var libraries = (_libraries.Count > 0 || _disconnectedLibraries == null ?
                _libraries : _disconnectedLibraries);

            if (libraries.Count > 0)
            {
                // writer.WriteElements(_libraries, LIBRARY_HELPERS);
                for (int i = 0; i < libraries.Count; i++)
                {
                    // If there is a library, write it.  Otherwise, write the
                    // library spec.
                    var item = libraries[i];
                    if (item == null)
                    {
                        var spec = _librarySpecs[i];
                        if (!spec.IsDocumentLocal)
                        {
                            IXmlElementHelper<LibrarySpec> helper = XmlUtil.FindHelper(spec, LIBRARY_SPEC_HELPERS);
                            if (helper == null)
                                throw new InvalidOperationException("Attempt to serialize list containing invalid type.");
                            writer.WriteElement(helper.ElementNames[0], spec);                            
                        }
                    }
                    else
                    {
                        IXmlElementHelper<Library> helper = XmlUtil.FindHelper(item, LIBRARY_HELPERS);
                        if (helper == null)
                            throw new InvalidOperationException("Attempt to serialize list containing invalid type.");
                        writer.WriteElement(helper.ElementNames[0], item);                        
                    }
                }
            }
        }

        #endregion

        #region object overrides

        public bool Equals(PeptideLibraries obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return ArrayUtil.EqualsDeep(obj._librarySpecs, _librarySpecs) &&
                ArrayUtil.EqualsDeep(obj._libraries, _libraries) &&
                Equals(obj._rankIdName, _rankIdName) &&
                Equals(obj.Pick, Pick) &&
                Equals(obj.RankId, RankId) &&
                obj.PeptideCount.Equals(PeptideCount);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (PeptideLibraries)) return false;
            return Equals((PeptideLibraries) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _librarySpecs.GetHashCodeDeep();
                result = (result*397) ^ _libraries.GetHashCodeDeep();
                result = (result*397) ^ (_rankIdName != null ? _rankIdName.GetHashCode() : 0);
                result = (result*397) ^ Pick.GetHashCode();
                result = (result*397) ^ (RankId != null ? RankId.GetHashCode() : 0);
                result = (result*397) ^ (PeptideCount.HasValue ? PeptideCount.Value : 0);
                return result;
            }
        }

        #endregion
    }
}