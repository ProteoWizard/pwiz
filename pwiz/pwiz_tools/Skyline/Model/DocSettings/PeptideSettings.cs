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
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
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
            return ChangeProp(ImClone(this), (im, v) => im.Enzyme = v, prop);
        }

        public PeptideSettings ChangeDigestSettings(DigestSettings prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.DigestSettings = v, prop);
        }

        public PeptideSettings ChangeBackgroundProteome(BackgroundProteome prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.BackgroundProteome = v, prop);
        }

        public PeptideSettings ChangePrediction(PeptidePrediction prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Prediction = v, prop);
        }

        public PeptideSettings ChangeFilter(PeptideFilter prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Filter = v, prop);
        }

        public PeptideSettings ChangeLibraries(PeptideLibraries prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Libraries = v, prop);
        }

        public PeptideSettings ChangeModifications(PeptideModifications prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Modifications = v, prop);
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

        public double? PredictRetentionTime(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup,
            bool singleWindow, out double windowRT)
        {
            // Safe defaults
            double? predictedRT = null;
            windowRT = 0;
            // Use measurements, if set and available
            bool useMeasured = (UseMeasuredRTs && MeasuredRTWindow.HasValue);
            if (useMeasured)
            {
                predictedRT = nodeGroup.AveragePeakCenterTime;
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
                            predictedRT = nodeGroupOther.AveragePeakCenterTime;
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
            if (!predictedRT.HasValue && RetentionTime != null)
            {
                // but only if not using measured results, or the instrument supports
                // variable scheduling windows
                if (!useMeasured || !singleWindow)
                {
                    predictedRT = RetentionTime.GetRetentionTime(nodeGroup.TransitionGroup.Peptide.Sequence);
                    windowRT = RetentionTime.TimeWindow;
                }
            }
            return predictedRT;
        }

        /// <summary>
        /// Tells whether a document can be scheduled, given certain scheduling restrictions.
        /// </summary>
        /// <param name="document">The document to schedule</param>
        /// <param name="singleWindow">True if the instrument supports only a single global
        /// retention time window, or false if the instrument can set the window for each transition</param>
        /// <returns>True if a scheduled method may be created from this document</returns>
        public bool CanSchedule(SrmDocument document, bool singleWindow)
        {
            // Check if results information can be used for retention times
            bool resultsAvailable = (UseMeasuredRTs && document.Settings.HasResults);

            //  If the user has assigned a retention time predictor
            if (RetentionTime != null)
            {
                // As long as the instrument is not limited to a single retention
                // time window, or their is no option to use results information,
                // then this document can be scheduled.
                if (!singleWindow || !resultsAvailable)
                    return true;                
            }
            // If no results available (and no predictor), then no scheduling
            if (!resultsAvailable)
                return false;

            // Otherwise, if every precursor has enough result information
            // to predict a retention time, then this document can be scheduled.
            foreach (var nodePep in document.Peptides)
            {
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    double windowRT;
                    if (!PredictRetentionTime(nodePep, nodeGroup, singleWindow, out windowRT).HasValue)
                        return false;
                }
            }
            return true;
        }

        #region Property change methods

        public PeptidePrediction ChangeRetentionTime(RetentionTimeRegression prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.RetentionTime = v, prop);
        }

        public PeptidePrediction ChangeUseMeasuredRTs(bool prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.UseMeasuredRTs = v, prop);
        }

        public PeptidePrediction ChangeMeasuredRTWindow(double? prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.MeasuredRTWindow = v, prop);
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
            writer.WriteAttribute<bool>(ATTR.use_measured_rts, UseMeasuredRTs);
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
        /// <param name="peptide">The peptide being considered</param>
        /// <returns>True if the peptide should be included</returns>
        bool Accept(Peptide peptide);
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
        private Regex _regexExclude;

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
                _regexExclude = null;
            }
        }

        #region Property change methods

        public PeptideFilter ChangeExcludeNTermAAs(int prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ExcludeNTermAAs = v, prop);
        }

        public PeptideFilter ChangeMinPeptideLength(int prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.MinPeptideLength = v, prop);
        }

        public PeptideFilter ChangeMaxPeptideLength(int prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.MaxPeptideLength = v, prop);
        }

        public PeptideFilter ChangeAutoSelect(bool prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.AutoSelect = v, prop);
        }

        public PeptideFilter ChangeExclusions(IList<PeptideExcludeRegex> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Exclusions = v, prop);
        }
        #endregion

        public bool Accept(Peptide peptide)
        {
            return Accept(peptide.Sequence, peptide.Begin);
        }

        private bool Accept(string sequence, int? begin)
        {
            // Must begin after excluded C-terminal AAs
            if (begin.HasValue && begin.Value < ExcludeNTermAAs)
                return false;

            // Must be within acceptable length range
            int len = sequence.Length;
            if (MinPeptideLength > len || len > MaxPeptideLength)
                return false;

            // No exclusion matches allowed
            if (_regexExclude != null && _regexExclude.Match(sequence).Success)
                return false;

            return true;
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
                    if (sb.Length > 0)
                        sb.Append('|');
                    sb.Append(exclude.Regex);
                }
            }

            if (sb.Length > 0)
            {
                try
                {
                    // Hold the constructed Regex expression for use in filtering.
                    _regexExclude = new Regex(sb.ToString());
                }
                catch (ArgumentException x)
                {                    
                    throw new InvalidDataException("Invalid exclusion list.", x);
                }
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

            public bool Accept(Peptide peptide)
            {
                return true;
            }

            #endregion
        }
    }

    [XmlRoot("peptide_modifications")]
    public class PeptideModifications : Immutable, IXmlSerializable
    {
        private ReadOnlyCollection<StaticMod> _staticModifications;
        private ReadOnlyCollection<StaticMod> _heavyModifications;

        public PeptideModifications(IList<StaticMod> staticModifications, IList<StaticMod> heavyModifications)
        {
            StaticModifications = staticModifications;
            HeavyModifications = heavyModifications;
        }

        public IList<StaticMod> StaticModifications
        {
            get { return _staticModifications; }
            private set { _staticModifications = MakeReadOnly(value); }
        }

        public IList<StaticMod> HeavyModifications
        {
            get { return _heavyModifications; }
            private set { _heavyModifications = MakeReadOnly(value); }
        }

        public bool HasHeavyModifications { get { return _heavyModifications.Count > 0; } }
        public bool HasHeavyImplicitModifications
        {
            get { return _heavyModifications.IndexOf(mod => !mod.IsExplicit) != -1; }
        }

        #region Property change methods

        public PeptideModifications ChangeStaticModifications(IList<StaticMod> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.StaticModifications = v, prop);
        }

        public PeptideModifications ChangeHeavyModifications(IList<StaticMod> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.HeavyModifications = v, prop);
        }

        public PeptideModifications DeclareExplicitMods(SrmDocument doc,
            IList<StaticMod> listStaticMods, IList<StaticMod> listHeavyMods)
        {
            Dictionary<string, StaticMod> explicitStaticMods;
            IList<StaticMod> staticMods = SplitModTypes(StaticModifications,
                listStaticMods, out explicitStaticMods);
            Dictionary<string, StaticMod> explicitHeavyMods;
            IList<StaticMod> heavyMods = SplitModTypes(HeavyModifications,
                listHeavyMods, out explicitHeavyMods);

            foreach (PeptideDocNode nodePep in doc.Peptides)
            {
                if (!nodePep.HasExplicitMods)
                    continue;
                DeclareExplicitMods(staticMods, explicitStaticMods, nodePep.ExplicitMods.StaticModifications);
                DeclareExplicitMods(heavyMods, explicitHeavyMods, nodePep.ExplicitMods.HeavyModifications);
            }

            if (ArrayUtil.EqualsDeep(staticMods, StaticModifications))
                staticMods = StaticModifications;
            if (ArrayUtil.EqualsDeep(heavyMods, HeavyModifications))
                heavyMods = HeavyModifications;

            // If nothing changed, return this
            if (ReferenceEquals(staticMods, StaticModifications) && ReferenceEquals(heavyMods, HeavyModifications))
                return this;

            var modsClone = ImClone(this);
            modsClone.StaticModifications = staticMods;
            modsClone.HeavyModifications = heavyMods;
            return modsClone;
        }

        private static IList<StaticMod> SplitModTypes(IEnumerable<StaticMod> mods,
            IEnumerable<StaticMod> listModsGlobal, out Dictionary<string, StaticMod> explicitMods)
        {
            List<StaticMod> implicitMods = new List<StaticMod>();
            explicitMods = new Dictionary<string, StaticMod>();
            // Make sure all global mods are available to explicit mods with their
            // current settings values
            foreach (StaticMod mod in listModsGlobal)
                explicitMods.Add(mod.Name, mod);
            foreach (StaticMod mod in mods)
            {
                if (!mod.IsExplicit)
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
            foreach (ExplicitMod mod in explicitMods)
            {
                string modName = mod.Modification.Name;
                if (mods.IndexOf(modStatic => Equals(modName, modStatic.Name)) == -1)
                {
                    StaticMod modStatic;
                    // Try to get the desired modification from existing explicit mods first
                    if (!explicitStaticMods.TryGetValue(modName, out modStatic))
                        // Otherwise, use the one attached to the explicit mod
                        modStatic = mod.Modification;
                    // Make sure it is marked explicit
                    if (!modStatic.IsExplicit)
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
            heavy_modifications            
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
            var list = new List<StaticMod>();
            var listHeavy = new List<StaticMod>();

            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                // Read child elements
                reader.ReadElementList(EL.static_modifications, list);
                reader.ReadElementList(EL.heavy_modifications, listHeavy);
                reader.ReadEndElement();                
            }

            StaticModifications = list;
            HeavyModifications = listHeavy;
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write child elements
            if (_staticModifications.Count > 0)
                writer.WriteElementList(EL.static_modifications, StaticModifications);
            if (_heavyModifications.Count > 0)
                writer.WriteElementList(EL.heavy_modifications, HeavyModifications);
        }

        #endregion

        #region object overrides

        public bool Equals(PeptideModifications obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return ArrayUtil.EqualsDeep(obj._staticModifications, _staticModifications) &&
                ArrayUtil.EqualsDeep(obj._heavyModifications, _heavyModifications);
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
                int result = _staticModifications.GetHashCodeDeep();
                result = (result * 397) ^ _heavyModifications.GetHashCodeDeep();
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

        #region Property change methods

        public PeptideLibraries ChangePick(PeptidePick prop)
        {
            return ChangeProp(ImClone(this), im => im.Pick = prop);
        }

        public PeptideLibraries ChangeRankId(PeptideRankId prop)
        {
            return ChangeProp(ImClone(this), im => im.RankId = prop);
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
            Debug.Assert(LibrariesMatch());

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