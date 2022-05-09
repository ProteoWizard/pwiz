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
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.ChromLib;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
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
                               PeptideIntegration integration,
                               BackgroundProteome backgroundProteome
                               )
        {
            Enzyme = enzyme;
            DigestSettings = digestSettings;
            Prediction = prediction;
            Filter = filter;
            Libraries = libraries;
            Modifications = modifications;
            Integration = integration;
            BackgroundProteome = backgroundProteome;
            if ((BackgroundProteome == null || BackgroundProteome.IsNone) &&
                Filter.PeptideUniqueness != PeptideFilter.PeptideUniquenessConstraint.none)
            {
                // No peptide uniqueness filtering without a background proteome
                Filter = Filter.ChangePeptideUniqueness(PeptideFilter.PeptideUniquenessConstraint.none);
            }
            Quantification = QuantificationSettings.DEFAULT;
        }

        [TrackChildren]
        public Enzyme Enzyme { get; private set; }

        [TrackChildren(true)]
        public DigestSettings DigestSettings { get; private set; }

        [TrackChildren]
        public BackgroundProteome BackgroundProteome { get; private set; }

        [TrackChildren(true)]
        public PeptidePrediction Prediction { get; private set; }

        [TrackChildren(true)]
        public PeptideFilter Filter { get; private set; }

        [TrackChildren(true)]
        public PeptideLibraries Libraries { get; private set; }

        [TrackChildren(true)]
        public PeptideModifications Modifications { get; private set; }

        [TrackChildren(true)]
        public PeptideIntegration Integration { get; private set; }

        [TrackChildren(true)]
        public QuantificationSettings Quantification { get; private set; }

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
            var result =  ChangeProp(ImClone(this), im => im.BackgroundProteome = prop);
            if (prop == null || prop.IsNone)
            {
                // Can't do peptide uniqueness checks without a background proteome
                result = result.ChangeFilter(result.Filter.ChangePeptideUniqueness(PeptideFilter.PeptideUniquenessConstraint.none));
            }
            return result;
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

        public PeptideSettings ChangeIntegration(PeptideIntegration prop)
        {
            return ChangeProp(ImClone(this), im => im.Integration = prop);
        }

        public PeptideSettings ChangeAbsoluteQuantification(QuantificationSettings prop)
        {
            prop = prop ?? QuantificationSettings.DEFAULT;
            if (Equals(prop, Quantification))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im => im.Quantification = prop);
        }

        public PeptideSettings MergeDefaults(PeptideSettings defPep)
        {
            PeptideSettings newPeptideSettings = ImClone(this);
            newPeptideSettings.Enzyme = newPeptideSettings.Enzyme ?? defPep.Enzyme;
            newPeptideSettings.DigestSettings = newPeptideSettings.DigestSettings ?? defPep.DigestSettings;
            newPeptideSettings.Prediction = newPeptideSettings.Prediction ?? defPep.Prediction;
            newPeptideSettings.Filter = newPeptideSettings.Filter ?? defPep.Filter;
            newPeptideSettings.Libraries = newPeptideSettings.Libraries ?? defPep.Libraries;
            newPeptideSettings.BackgroundProteome = newPeptideSettings.BackgroundProteome ?? defPep.BackgroundProteome;
            newPeptideSettings.Modifications = newPeptideSettings.Modifications ?? defPep.Modifications;
            newPeptideSettings.Integration = newPeptideSettings.Integration ?? defPep.Integration;
            return Equals(newPeptideSettings, this) ? this : newPeptideSettings;
        }

        #endregion

        public bool NeedsBackgroundProteomeUniquenessCheckProcessing
        {
            get
            {
                return BackgroundProteome != null && !BackgroundProteome.IsReadyForUniquenessFilter(Filter.PeptideUniqueness);
            }
        }

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
                Integration = reader.DeserializeElement<PeptideIntegration>();
                Quantification = reader.DeserializeElement<QuantificationSettings>();
                reader.ReadEndElement();
            }

            Quantification = Quantification ?? QuantificationSettings.DEFAULT;
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
            if (Integration.IsSerializable)
                writer.WriteElement(Integration);
            if (!Equals(Quantification, QuantificationSettings.DEFAULT))
                writer.WriteElement(Quantification);
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
                   Equals(obj.Integration, Integration) &&
                   Equals(obj.BackgroundProteome, BackgroundProteome) &&
                   Equals(obj.Quantification, Quantification);
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
                result = (result*397) ^ Integration.GetHashCode();
                result = (result*397) ^ BackgroundProteome.GetHashCode();
                result = (result*397) ^ Quantification.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    [XmlRoot("peptide_prediction")]
    public class PeptidePrediction : Immutable, IValidating, IXmlSerializable, IAuditLogComparable
    {
        public const int MAX_TREND_PREDICTION_REPLICATES = 20;
        public const double MIN_MEASURED_RT_WINDOW = 0.1;
        public const double MAX_MEASURED_RT_WINDOW = 300.0;
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

        [TrackChildren]
        public RetentionTimeRegression NonNullRetentionTime
        {
            get { return RetentionTime ?? RetentionTimeList.GetDefault(); }
        }

        public RetentionTimeRegression RetentionTime { get; private set; }

        /// <summary>
        /// Ion mobility values have moved to their proper place in TransitionSettings, but may need to be deserialized from PeptideSettings in older docs 
        /// </summary>
        public TransitionIonMobilityFiltering ObsoleteIonMobilityValues { get; private set; }

        [Track]
        public bool UseMeasuredRTs { get; private set; }

        [Track]
        public double? MeasuredRTWindow { get; set; }

        public int CalcMaxTrendReplicates(SrmDocument document)
        {
            if (!UseMeasuredRTs || !MeasuredRTWindow.HasValue)
                throw new InvalidOperationException(Resources.PeptidePrediction_CalcMaxTrendReplicates_Calculating_scheduling_from_trends_requires_a_retention_time_window_for_measured_data);

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
            return PredictRetentionTimeUsingSpecifiedReplicates(document, nodePep, nodeGroup, replicateNum, algorithm,
                singleWindow, null, out windowRT);
        }

        public double? PredictRetentionTimeForChromImport(SrmDocument document, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, out double windowRt)
        {
            return PredictRetentionTimeUsingSpecifiedReplicates(document, nodePep, nodeGroup, null,
                ExportSchedulingAlgorithm.Average, false, chromatogramSet => chromatogramSet.UseForRetentionTimeFilter, out windowRt);
        }

        private double? PredictRetentionTimeUsingSpecifiedReplicates(SrmDocument document,
                                            PeptideDocNode nodePep,
                                            TransitionGroupDocNode nodeGroup,
                                            int? replicateNum,
                                            ExportSchedulingAlgorithm algorithm,
                                            bool singleWindow,
                                            Predicate<ChromatogramSet> replicateFilter, 
                                            out double windowRT)
        {
            // If peptide has an explicitly set RT, use that
            if (nodePep.ExplicitRetentionTime != null  && 
                (MeasuredRTWindow.HasValue || nodePep.ExplicitRetentionTime.RetentionTimeWindow.HasValue))
            {
                // If peptide has an explicitly set RT window, use that, or the global setting
                windowRT = nodePep.ExplicitRetentionTime.RetentionTimeWindow ?? MeasuredRTWindow.Value;
                return nodePep.ExplicitRetentionTime.RetentionTime;
            }
            // Safe defaults
            double? predictedRT = null;
            windowRT = 0;
            // Use measurements, if set and available
            bool useMeasured = (UseMeasuredRTs && MeasuredRTWindow.HasValue && document.Settings.HasResults);
            if (useMeasured)
            {
                var schedulingGroups = GetSchedulingGroups(nodePep, nodeGroup);
                var peakTime = TransitionGroupDocNode.GetSchedulingPeakTimes(schedulingGroups, document, algorithm, replicateNum, replicateFilter);
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
                            peakTime = nodeGroupOther.GetSchedulingPeakTimes(document, algorithm, replicateNum, replicateFilter);
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
                    var modifiedSequence = document.Settings.GetSourceTarget(nodePep);
                    if (null != replicateFilter && document.Settings.HasResults)
                    {
                        var retentionTimes = new List<double>();
                        foreach (var chromSet in document.Settings.MeasuredResults.Chromatograms)
                        {
                            if (!replicateFilter(chromSet))
                            {
                                continue;
                            }
                            foreach (ChromFileInfo chromFileInfo in chromSet.MSDataFileInfos)
                            {
                                var conversion = RetentionTime.GetConversion(chromFileInfo.FileId);
                                if (null == conversion)
                                {
                                    continue;
                                }
                                double? time = RetentionTime.GetRetentionTime(modifiedSequence, conversion);
                                if (time.HasValue)
                                {
                                    retentionTimes.Add(time.Value);
                                }
                            }
                        }
                        if (retentionTimes.Count > 0)
                        {
                            predictedRT = retentionTimes.Average();
                        }
                    }
                    if (!predictedRT.HasValue)
                    {
                        predictedRT = RetentionTime.GetRetentionTime(modifiedSequence);
                    }
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
            {
                // Actually we *can* still schedule if everything has an explicit RT
                if (document.Molecules.Any(p => p.ExplicitRetentionTime == null))
                    return false;
            }
            // Otherwise, if every precursor has enough result information
            // to predict a retention time, then this document can be scheduled.
            bool anyTimes = false;
            foreach (var nodePep in document.Molecules)
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

        public PeptidePrediction ChangeObsoleteIonMobilityValues(TransitionIonMobilityFiltering prop)
        {
            return ChangeProp(ImClone(this), im => im.ObsoleteIonMobilityValues = prop);
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
            measured_rt_window,
            use_spectral_library_drift_times,
            spectral_library_drift_times_resolving_power
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
                    throw new InvalidDataException(
                        string.Format(Resources.PeptidePrediction_DoValidate_The_retention_time_window__0__for_a_scheduled_method_based_on_measured_results_must_be_between__1__and__2__,
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
            var obsoleteUseSpectralLibraryDriftTimes = reader.GetNullableBoolAttribute(ATTR.use_spectral_library_drift_times) ?? false; // Now in transition settings, read here for backward compatibility

            var obsoleteSpectralLibraryIonMobilityWindowWidthCalculator = new IonMobilityWindowWidthCalculator(reader, true, true); // Now in transition settings, read here for backward compatibility
            DriftTimePredictor obsoleteDriftTimePredictor = null;
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
                // Read child elements
                RetentionTime = reader.DeserializeElement<RetentionTimeRegression>();
                if (reader.IsStartElement(DriftTimePredictor.EL.predict_drift_time)) // Reading an older format
                    obsoleteDriftTimePredictor = reader.DeserializeElement<DriftTimePredictor>();
                reader.ReadEndElement();                
            }

            // If we're reading an older format, gather needed ion mobility info to pass along to TransitionSettings
            if ((obsoleteDriftTimePredictor != null && !obsoleteDriftTimePredictor.IsEmpty) ||
                !obsoleteSpectralLibraryIonMobilityWindowWidthCalculator.IsEmpty ||
                obsoleteUseSpectralLibraryDriftTimes)
            {
                var dir =string.IsNullOrEmpty(reader.BaseURI) ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(reader.BaseURI);
                if (obsoleteDriftTimePredictor == null)
                {
                    ObsoleteIonMobilityValues = TransitionIonMobilityFiltering.EMPTY;
                }
                else
                {
                    ObsoleteIonMobilityValues = obsoleteDriftTimePredictor.CreateTransitionIonMobilityFiltering(dir); // Creates a .imsdb file in dir
                }
                ObsoleteIonMobilityValues = ObsoleteIonMobilityValues.ChangeUseSpectralLibraryIonMobilityValues(obsoleteUseSpectralLibraryDriftTimes);
                if (ObsoleteIonMobilityValues.FilterWindowWidthCalculator.IsEmpty)
                {
                    ObsoleteIonMobilityValues =
                        ObsoleteIonMobilityValues.ChangeFilterWindowWidthCalculator(
                            obsoleteSpectralLibraryIonMobilityWindowWidthCalculator);
                }
            }
            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write this bool whether it is true or false, to allow its absence
            // as a marker of needing default values.
            writer.WriteAttribute(ATTR.use_measured_rts, UseMeasuredRTs, !UseMeasuredRTs);
            writer.WriteAttributeNullable(ATTR.measured_rt_window, MeasuredRTWindow);

            if (ObsoleteIonMobilityValues != null && !ObsoleteIonMobilityValues.IsEmpty)
            {
                // Writing backward compatible format - normally this is written to transition settings
                writer.WriteAttribute(ATTR.use_spectral_library_drift_times, ObsoleteIonMobilityValues.UseSpectralLibraryIonMobilityValues, !ObsoleteIonMobilityValues.UseSpectralLibraryIonMobilityValues);
                ObsoleteIonMobilityValues.FilterWindowWidthCalculator?.WriteXML(writer, true, true);
            }

            // Write child elements
            if (RetentionTime != null)
                writer.WriteElement(RetentionTime);
            // Writing backward compatible format?
            ObsoleteIonMobilityValues?.IonMobilityLibrary?.WriteXml(writer, ObsoleteIonMobilityValues.FilterWindowWidthCalculator);
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
                result = (result * 397) ^ UseMeasuredRTs.GetHashCode();
                result = (result * 397) ^ (MeasuredRTWindow.HasValue ? MeasuredRTWindow.Value.GetHashCode() : 0);
                return result;
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return RetentionTimeList.GetDefault();
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

    /// <summary>
    /// Filtering of variable modifications by location and mass to avoid
    /// enumerating all globally possible peptide modifications when attempting
    /// to match library spectra.
    /// </summary>
    public interface IVariableModFilter
    {
        /// <summary>
        /// Returns true if any modification is possible at the given index.
        /// </summary>
        bool IsModIndex(int index);

        /// <summary>
        /// Returns true if the given modification mass is possible at the
        /// given index.
        /// </summary>
        bool IsModMass(int index, double mass);
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

        public enum PeptideUniquenessConstraint
        {
            none,
            protein,
            gene,
            species
        };

        public static readonly IPeptideFilter UNFILTERED = new AllPeptidesFilter();

        private ImmutableList<PeptideExcludeRegex> _exclusions;

        // Cached regular expressions for fast matching
        private Regex _regexExclude;
        private Regex _regexExcludeMod;
        private Regex _regexInclude;
        private Regex _regexIncludeMod;

        public PeptideFilter(int excludeNTermAAs, int minPeptideLength,
                             int maxPeptideLength, IList<PeptideExcludeRegex> exclusions, bool autoSelect,
                             PeptideUniquenessConstraint peptideUniquenessConstraint)
        {
            Exclusions = exclusions;
            ExcludeNTermAAs = excludeNTermAAs;
            MinPeptideLength = minPeptideLength;
            MaxPeptideLength = maxPeptideLength;
            AutoSelect = autoSelect;
            PeptideUniqueness = peptideUniquenessConstraint;
            DoValidate();
        }

        [Track]
        public int ExcludeNTermAAs { get; private set; }

        [Track]
        public int MinPeptideLength { get; private set; }

        [Track]
        public int MaxPeptideLength { get; private set; }

        [Track]
        public bool AutoSelect { get; private set; }

        [TrackChildren]
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

        [Track]
        public PeptideUniquenessConstraint PeptideUniqueness { get; private set; }

        [TrackChildren(ignoreName: true)]
        public ProteinAssociation.ParsimonySettings ParsimonySettings { get; private set; }

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

        public PeptideFilter ChangePeptideUniqueness(PeptideUniquenessConstraint prop)
        {
            return ChangeProp(ImClone(this), im => im.PeptideUniqueness = prop);
        }

        public PeptideFilter ChangeParsimonySettings(ProteinAssociation.ParsimonySettings prop)
        {
            return ChangeProp(ImClone(this), im => im.ParsimonySettings = prop);
        }
        #endregion

        public bool Accept(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods, out bool allowVariableMods)
        {
            allowVariableMods = false;

            if (peptide.IsCustomMolecule)
                return false;

            // Must begin after excluded C-terminal AAs
            if (peptide.Begin.HasValue && peptide.Begin.Value < ExcludeNTermAAs)
                return false;

            // Must be within acceptable length range
            string sequence = peptide.Target.Sequence;
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
                var sequenceMod = calcMod.GetModifiedSequenceDisplay(peptide.Target).ToString();
                if (_regexExcludeMod != null && _regexExcludeMod.Match(sequenceMod).Success)
                    return false;
                if (_regexIncludeMod != null && !_regexIncludeMod.Match(sequenceMod).Success)
                    return false;
            }

            // Peptide uniqueness checks can be expensive, so do this last
            if ((settings.PeptideSettings.Filter.PeptideUniqueness != PeptideUniquenessConstraint.none)
                && !CheckPeptideUniqueness(settings, peptide.Target)) 
                return false;

            return true;
        }

        //
        // Background proteome uniqueness constraints
        //
        public bool CheckPeptideUniqueness(SrmSettings settings, Target sequence)
        {
            return CheckPeptideUniqueness(settings, new List<Target> { sequence }, null)[sequence];
        }

        public Dictionary<Target, bool> CheckPeptideUniqueness(SrmSettings settings, List<Target> sequences, SrmSettingsChangeMonitor progressMonitor)
        {
            var result = new Dictionary<Target, bool>();
            foreach (var s in sequences)
            {
                if (!string.IsNullOrEmpty(s.Sequence) && !result.ContainsKey(s)) // Watch out for non-peptide molecules
                {
                    result.Add(s, true);
                }
            }
            if (settings.PeptideSettings.Filter.PeptideUniqueness == PeptideUniquenessConstraint.none)
            {
                return result; // Everything passes
            }

            var needsBackgroundProteomeUniquenessCheckProcessing = settings.PeptideSettings.NeedsBackgroundProteomeUniquenessCheckProcessing;
            Assume.IsFalse(needsBackgroundProteomeUniquenessCheckProcessing); // Should be handled in ChangeSettings
            return settings.PeptideSettings.BackgroundProteome.PeptidesUniquenessFilter(result, settings.PeptideSettings, progressMonitor);
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
            unique_by
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
            ValidateIntRange(Resources.PeptideFilter_DoValidate_excluded_n_terminal_amino_acids, ExcludeNTermAAs,
                MIN_EXCLUDE_NTERM_AA, MAX_EXCLUDE_NTERM_AA);
            ValidateIntRange(Resources.PeptideFilter_DoValidate_minimum_peptide_length, MinPeptideLength,
                MIN_MIN_LENGTH, MAX_MIN_LENGTH);
            ValidateIntRange(Resources.PeptideFilter_DoValidate_maximum_peptide_length, MaxPeptideLength,
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
// ReSharper disable ObjectCreationAsStatement
                        new Regex(exclude.Regex);
// ReSharper restore ObjectCreationAsStatement
                    }
                    catch(ArgumentException x)
                    {
                        throw new InvalidDataException(
                            string.Format(Resources.PeptideFilter_DoValidate_The_peptide_exclusion__0__has_an_invalid_regular_expression__1__,
                                          exclude.Name, exclude.Regex), x);
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
                throw new InvalidDataException(Resources.PeptideFilter_ExcludeExprToRegEx_Invalid_exclusion_list, x);
            }
        }

        private static void ValidateIntRange(string label, int n, int min, int max)
        {
            if (min > n || n > max)
            {
                throw new InvalidDataException(string.Format(Resources.PeptideFilter_ValidateIntRange_The_value__1__for__0__must_be_between__2__and__3__,
                                                             label, n, min, max));
            }
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
            PeptideUniqueness = reader.GetEnumAttribute(ATTR.unique_by, PeptideUniquenessConstraint.none);

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
            if (PeptideUniqueness != PeptideUniquenessConstraint.none)
            {
                writer.WriteAttribute(ATTR.unique_by, PeptideUniqueness);
            }
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
                   obj.PeptideUniqueness.Equals(PeptideUniqueness) &&
                   Equals(obj.ParsimonySettings, ParsimonySettings) &&
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
                result = (result*397) ^ PeptideUniqueness.GetHashCode();
                result = (result*397) ^ ParsimonySettings?.GetHashCode() ?? 0;
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

        private ImmutableList<TypedModifications> _modifications;
        private ImmutableList<IsotopeLabelType> _internalStandardTypes;
        private ImmutableList<PeptideLabelRatio> _emptyPeptideRatios; 

        public PeptideModifications(IList<StaticMod> staticMods,
            IList<TypedModifications> heavyMods)
            : this(staticMods, DEFAULT_MAX_VARIABLE_MODS, DEFAULT_MAX_NEUTRAL_LOSSES,
                   heavyMods, new[] { IsotopeLabelType.heavy })
        {
            // Make sure the internal standard type is reference equal with
            // the first heavy type.
            var firstHeavy = heavyMods.FirstOrDefault();
            if (firstHeavy != null)
                InternalStandardTypes = new[] {firstHeavy.LabelType};
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

            InternalStandardTypes = internalStandardTypes;

            DoValidate();
        }

        [Track]
        public int MaxVariableMods { get; private set; }
        [Track]
        public int MaxNeutralLosses { get; private set; }
        public int CountLabelTypes { get { return _modifications.Count; } }

        [Track]
        public IList<IsotopeLabelType> InternalStandardTypes
        {
            get { return _internalStandardTypes; }
            private set { _internalStandardTypes = MakeReadOnly(value); }
        }

        /// <summary>
        /// If the user has selected "none" for internal standard types,
        /// we still want to calculate ratios for all of the types
        /// </summary>
        public IList<IsotopeLabelType> RatioInternalStandardTypes
        {
            get { return InternalStandardTypes.Count > 0 ? InternalStandardTypes : GetHeavyModificationTypes().ToList() ; }
        }

        [TrackChildren]
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

        public IEnumerable<StaticMod> AllHeavyModifications
        {
            get { return GetHeavyModifications().SelectMany(typedMods => typedMods.Modifications); }
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

        [TrackChildren]
        public TypedModifications[] HeavyModifications
        {
            get { return GetHeavyModifications().ToArray(); }
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

        public bool HasHeavyModifications { get; private set; }

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

        public IList<PeptideLabelRatio> CalcPeptideRatios(Func<IsotopeLabelType, IsotopeLabelType, RatioValue> calcPairedRatio,
                                                   Func<IsotopeLabelType, RatioValue> calcGlobalRatio)
        {
            // Avoid allocation if possible, as this can get big for DIA data without ratios
            IList<PeptideLabelRatio> listRatios = _emptyPeptideRatios == null ? new List<PeptideLabelRatio>() : null;
            int i = 0;

            // Cache empty ratios for perf and memory use
            foreach (var standardType in RatioInternalStandardTypes)
            {
                foreach (var labelType in GetModificationTypes())
                {
                    if (ReferenceEquals(standardType, labelType))
                        continue;

                    listRatios = AddPeptideRatio(listRatios, i++, labelType, standardType, calcPairedRatio(labelType, standardType));
                }
            }
            // Then add ratios to global standards
            foreach (var labelType in GetModificationTypes())
            {
                listRatios = AddPeptideRatio(listRatios, i++, labelType, null, calcGlobalRatio(labelType));
            }
            return listRatios ?? _emptyPeptideRatios;
        }

        private IList<PeptideLabelRatio> AddPeptideRatio(IList<PeptideLabelRatio> listRatios, int i,
            IsotopeLabelType labelType, IsotopeLabelType standardType, RatioValue ratio)
        {
            if (_emptyPeptideRatios == null)
            {
                listRatios.Add(new PeptideLabelRatio(labelType, standardType, ratio));
            }
            else if (ratio != null)
            {
                if (listRatios == null)
                    listRatios = _emptyPeptideRatios.ToArray();
                listRatios[i] = new PeptideLabelRatio(labelType, standardType, ratio);
            }
            return listRatios;
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

        /// <summary>
        /// Adds isotope modifications. This method skips any modifications which are already
        /// associated with a heavy isotope label type. If there is more than one heavy TypedModification,
        /// the new modifications get added to the first heavy TypedModification.
        /// If there are no heavy modification types (which should never happen), then 
        /// IsotopeLabelType.heavy is added.
        /// </summary>
        public PeptideModifications AddHeavyModifications(IEnumerable<StaticMod> modificationsToAdd)
        {
            var newMods = modificationsToAdd.Except(AllHeavyModifications).ToArray();
            if (!newMods.Any())
            {
                return this;
            }
            var heavyModifications = GetHeavyModifications().FirstOrDefault();
            if (heavyModifications != null)
            {
                return ChangeModifications(heavyModifications.LabelType, MakeReadOnly(heavyModifications.Modifications.Concat(newMods)));
            }
            return ChangeProp(ImClone(this),
                im => im._modifications = MakeReadOnly(
                    im._modifications.Concat(new[] {new TypedModifications(IsotopeLabelType.heavy, newMods)})));
        }

        /// <summary>
        /// Removes the specified modifications from all of the heavy TypedModifications.
        /// </summary>
        public PeptideModifications RemoveHeavyModifications(IEnumerable<StaticMod> modificationsToRemove)
        {
            return ChangeProp(ImClone(this), im =>
            {
                var newModifications = im._modifications.Select(typedMods =>
                    new TypedModifications(typedMods.LabelType,
                        MakeReadOnly(typedMods.Modifications.Except(modificationsToRemove))));
                im._modifications = MakeReadOnly(newModifications);
            });
        }

        public PeptideModifications ChangeModifications(IsotopeLabelType labelType, IList<StaticMod> prop)
        {
            int index = GetModIndex(labelType);
            if (index == -1)
                throw new IndexOutOfRangeException(string.Format(Resources.PeptideModifications_ChangeModifications_Modification_type__0__not_found, labelType));
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

        public PeptideModifications ChangeHasHeavyModifications(bool hasHeavyModifications)
        {
            return ChangeProp(ImClone(this), im => im.HasHeavyModifications = hasHeavyModifications);
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
                if (typedMods.LabelType.IsLight)
                {
                    DeclareExplicitCrosslinks(mods, explicitStaticMods, nodePep.ExplicitMods.CrosslinkStructure);
                }
            }

            if (ArrayUtil.EqualsDeep(mods, typedMods.Modifications))
                return typedMods;
            return new TypedModifications(typedMods.LabelType, mods);
        }

        private static void DeclareExplicitCrosslinks(IList<StaticMod> mods,
            IDictionary<string, StaticMod> explicitStaticMods,
            CrosslinkStructure crosslinkStructure)
        {
            // Enumerate all modifications user has made explicitly
            foreach (var crosslink in crosslinkStructure.Crosslinks)
            {
                string modName = crosslink.Crosslinker.Name;
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
                throw new InvalidDataException(
                    string.Format(Resources.PeptideModifications_DoValidate_Maximum_variable_modifications__0__must_be_between__1__and__2__,
                                  MaxVariableMods, MIN_MAX_VARIABLE_MODS, MAX_MAX_VARIABLE_MODS));
            }
            if (MIN_MAX_NEUTRAL_LOSSES > MaxNeutralLosses || MaxNeutralLosses > MAX_MAX_NEUTRAL_LOSSES)
            {
                throw new InvalidDataException(
                    string.Format(Resources.PeptideModifications_DoValidate_Maximum_neutral_losses__0__must_be_between__1__and__2__,
                                  MaxNeutralLosses, MIN_MAX_NEUTRAL_LOSSES, MAX_MAX_NEUTRAL_LOSSES));
            }

            var listRatios = CalcPeptideRatios((l, h) => null, l => null);
            _emptyPeptideRatios = ImmutableList<PeptideLabelRatio>.ValueOf(listRatios);
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
                {
                    if (internalStandardName == IsotopeLabelType.NONE_NAME)
                        internalStandardTypes = new IsotopeLabelType[0];
                    else
                        internalStandardNames.Add(internalStandardName);
                }
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
                    {
                        throw new InvalidDataException(
                            string.Format(Resources.PeptideModifications_ReadXml_Heavy_modifications_found_without__0__attribute,
                                          ATTR.isotope_label));
                    }
                    typeOrder++;

                    SetInternalStandardType(labelType, internalStandardNames, internalStandardTypes);

                    // If no internal standard type was given, use the first heavy type.
                    if (internalStandardNames.Count == 0 && internalStandardTypes.Length != 0)
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
                {
                    throw new InvalidDataException(string.Format(Resources.PeptideModifications_ReadXml_Internal_standard_type__0__not_found,
                                                                 internalStandardNames[iMissingType]));
                }
                reader.ReadEndElement();
            }

            if (list.Count < 2)
                list.Add(new TypedModifications(IsotopeLabelType.heavy, new StaticMod[0]));

            _modifications = MakeReadOnly(list.ToArray());
            InternalStandardTypes = internalStandardTypes;

            DoValidate();
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
            if (InternalStandardTypes.Count == 0)
            {
                writer.WriteAttribute(ATTR.internal_standard, IsotopeLabelType.NONE_NAME);
            }
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
                   ArrayUtil.EqualsDeep(obj._modifications, _modifications) &&
                   HasHeavyModifications == obj.HasHeavyModifications;
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
                result = (result * 397) ^ HasHeavyModifications.GetHashCode();
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

        private ImmutableList<LibrarySpec> _librarySpecs;
        private ImmutableList<Library> _libraries;
        private ImmutableList<Library> _disconnectedLibraries;

        public PeptideLibraries(PeptidePick pick, PeptideRankId rankId, int? peptideCount,
            bool hasDocLib, IList<LibrarySpec> librarySpecs, IList<Library> libraries)
        {
            Pick = pick;
            RankId = rankId;
            PeptideCount = peptideCount;
            HasDocumentLibrary = hasDocLib;
            LibrarySpecs = librarySpecs;
            Libraries = libraries;

            DoValidate();
        }

        [Track]
        public PeptidePick Pick { get; private set; }
        [Track]
        public PeptideRankId RankId { get; private set; }

        [Track]
        public bool LimitPeptidesPerProtein
        {
            get { return PeptideCount.HasValue; }
        }
        [Track]
        public int? PeptideCount { get; private set; }
        public bool HasDocumentLibrary { get; private set; }

        public bool HasLibraries { get { return _librarySpecs.Count > 0; } }
        public bool HasMidasLibrary { get { return _librarySpecs.Any(libSpec => libSpec is MidasLibSpec); } }

        [TrackChildren]
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

        public IEnumerable<MidasLibSpec> MidasLibrarySpecs
        {
            get { return _librarySpecs.Where(libSpec => libSpec is MidasLibSpec).Cast<MidasLibSpec>(); }
        }

        public IList<Library> Libraries
        {
            get { return _libraries; }
            private set { _libraries = MakeReadOnly(value); }
        }

        public IEnumerable<MidasLibrary> MidasLibraries
        {
            get { return _libraries.Where(lib => lib is MidasLibrary).Cast<MidasLibrary>(); }
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
            get { return IsNotLoadedExplained == null; }
        }

        public string IsNotLoadedExplained
        {
            get
            {
                foreach (var lib in _libraries)
                {
                    if (lib == null)
                    {
                        return @"null library";
                    }
                    string whyNot;
                    if ((whyNot = lib.IsNotLoadedExplained) != null)
                    {
                        return whyNot;
                    }
                }
                return null;
            }
        }

        public bool Contains(LibKey key)
        {
            Assume.IsTrue(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && lib.Contains(key))
                    return true;
            }
            return false;
        }

        public bool ContainsAny(Target target)
        {
            Assume.IsTrue(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && lib.ContainsAny(target))
                    return true;
            }
            return false;
        }

        public bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            Assume.IsTrue(IsLoaded);

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
            Assume.IsTrue(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && lib.TryLoadSpectrum(key, out spectrum))
                    return true;
            }
            spectrum = null;
            return false;
        }

        public bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            Assume.IsTrue(IsLoaded);

            foreach (Library lib in _libraries)
            {
                if (lib != null && !(lib is MidasLibrary) && lib.TryGetRetentionTimes(key, filePath, out retentionTimes))
                    return true;
            }
            retentionTimes = null;
            return false;
        }

        public bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes)
        {
            Assume.IsTrue(IsLoaded);

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
        /// Retrieve library ion mobility info for this particular file, if any
        /// </summary>
        private LibraryIonMobilityInfo GetLibraryDriftTimesForFilePath(LibKey[] targetIons, MsDataFileUri filePath)
        {
            foreach (var lib in _libraries)
            {
                // Only one of the available libraries may claim ownership of the file
                // in question.
                LibraryIonMobilityInfo ionMobilities;
                if (lib != null && lib.TryGetIonMobilityInfos(targetIons, filePath, out ionMobilities))
                {
                    return ionMobilities; // Found a library for this file in particular
                }
            }
            return null;
        }

        /// <summary>
        /// Combine ion mobility info from all lib and sub-libs into a single dict
        /// </summary>
        private Dictionary<LibKey, List<IonMobilityAndCCS>> GetAllSpectralLibraryIonMobilities(LibKey[] targetIons)
        {
            var ionMobilitiesDict = new Dictionary<LibKey, List<IonMobilityAndCCS>>();
            foreach (var lib in _libraries.Where(l => l != null))
            {
                // Get ion mobilities for all files in each library
                if (lib.TryGetIonMobilityInfos(targetIons, out var ionMobilities) && ionMobilities != null)
                {
                    var ionMobilityAndCcsDict = ionMobilities.GetIonMobilityDict();
                    foreach (var dt in ionMobilityAndCcsDict)
                    {
                        List<IonMobilityAndCCS> listTimes;
                        if (!ionMobilitiesDict.TryGetValue(dt.Key, out listTimes))
                        {
                            listTimes = new List<IonMobilityAndCCS>();
                            ionMobilitiesDict.Add(dt.Key, listTimes);
                        }
                        listTimes.AddRange(dt.Value);
                    }
                }
            }
            return ionMobilitiesDict;
        }

        public bool HasAnyLibraryIonMobilities(IEnumerable<LibKey> targetIons)
        {
            var targets = targetIons?.ToArray();
            return _libraries.Any(lib => lib != null && HasIonMobilities(lib, targets));
        }

        public static bool HasIonMobilities(Library lib, LibKey[] targetIons)
        {
            for (var i = 0; lib.TryGetIonMobilityInfos(targetIons, i, out var ionMobilities); i++) // Returns false when i> internal list length
            {
                if (ionMobilities != null && ionMobilities.GetIonMobilityDict().Any())
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get all ion mobilities from libs associated with this filepath.  Then look at all the others
        /// and get any values that don't appear in the initial set (how that list is used - averaged etc - is determined elsewhere).
        /// TODO(bspratt): It would be more maintainable if this and other things we get from libraries (like RT)
        /// shared some kind of common interface so there is guaranteed consistent behavior around how we pick from
        /// other libraries when the ostensibly correct library doesn't have values we need
        /// </summary>
        public bool TryGetSpectralLibraryIonMobilities(LibKey[] targetIons, MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities)
        {
            Assume.IsTrue(IsLoaded);
            // Get ion mobilities from spectral library for this ms data file, if any
            ionMobilities = GetLibraryDriftTimesForFilePath(targetIons, filePath);
            var resultDict = ionMobilities == null ?
                new Dictionary<LibKey, IonMobilityAndCCS[]>() :
                new Dictionary<LibKey, IonMobilityAndCCS[]>(ionMobilities.GetIonMobilityDict());
            // Note initial findings
            var foundDictKeys = new HashSet<LibKey>(resultDict.Keys); 
            // Look at all available libraries and sublibraries, use them to backfill any potentially missing drift time info 
            foreach (var im in GetAllSpectralLibraryIonMobilities(targetIons).Where(kvp => !foundDictKeys.Contains(kvp.Key)))
            {
                resultDict.Add(im.Key, im.Value.ToArray());
            }
            if (resultDict.Count > foundDictKeys.Count)
            {
                // Other libraries contributed some drift info
                ionMobilities = new LibraryIonMobilityInfo(filePath.GetFilePath(), false, resultDict);
            }
            return ionMobilities != null;
        }



        /// <summary>
        /// Loads all the spectra found in all the loaded libraries with the
        /// given LibKey and IsotopeLabelType.
        /// </summary>
        /// <param name="key">The LibKey to match on</param>
        /// <param name="labelType">The IsotopeLabelType to match on</param>
        /// <param name="bestMatch">True if only best-match spectra are included</param>
        public IEnumerable<SpectrumInfoLibrary> GetSpectra(LibKey key, IsotopeLabelType labelType, bool bestMatch)
        {
            Assume.IsTrue(IsLoaded);

            var redundancy = bestMatch ? LibraryRedundancy.best : LibraryRedundancy.all;
            return _libraries.Where(lib => lib != null).SelectMany(lib => lib.GetSpectra(key, labelType, redundancy));
        }

        public bool TryGetDocumentLibrary(out BiblioSpecLiteLibrary docLib)
        {
            docLib = null;
            for (int i = 0; i < _libraries.Count; i++)
            {
                if (_librarySpecs[i] != null && _librarySpecs[i].IsDocumentLibrary)
                {
                    docLib = _libraries[i] as BiblioSpecLiteLibrary;
                    return docLib != null;
                }
            }

            return false;
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

        public PeptideLibraries ChangeDocumentLibrary(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.HasDocumentLibrary = prop);
        }

        public PeptideLibraries ChangeDocumentLibraryPath(string path)
        {
            var specs = new LibrarySpec[LibrarySpecs.Count];
            var libs = new Library[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                if (LibrarySpecs[i].IsDocumentLibrary)
                {
                    specs[i] = BiblioSpecLiteSpec.GetDocumentLibrarySpec(path);
                    libs[i] = null;
                }
                else
                {
                    specs[i] = LibrarySpecs[i];
                    libs[i] = Libraries[i];
                }
            }
            return ChangeLibraries(specs, libs);
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
                                      {
                                          im.Libraries = new Library[prop.Count];
                                      }
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

        public PeptideLibraries ChangeLibrary(Library docLibrary, LibrarySpec docLibrarySpec, int indexOldLibrary)
        {
            var libs = Libraries.Select((l, i) => i == indexOldLibrary ? docLibrary : l).ToList();
            var libSpecs = LibrarySpecs.Select((s, i) => i == indexOldLibrary ? docLibrarySpec : s).ToList();
            if (indexOldLibrary == -1)
            {
                libs.Add(docLibrary);
                libSpecs.Add(docLibrarySpec);
            }
            return ChangeLibraries(libSpecs, libs);            
        }

        public PeptideLibraries Disconnect()
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._disconnectedLibraries = _libraries;
                im.Libraries = new Library[0];
                im.LibrarySpecs = new LibrarySpec[0];
            });
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
                if (librarySpecs.Contains(s => Equals(s.Name, libraryName)) ||
                        (_disconnectedLibraries != null && _disconnectedLibraries.Contains(l => Equals(l.Name, libraryName))))
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
                var pathLibrary = Settings.Default.LibraryDirectory != null
                    ? Path.Combine(Settings.Default.LibraryDirectory, fileName)
                    : null;
                if (pathLibrary != null && !File.Exists(pathLibrary) && findLibrary != null)
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

            if (ArrayUtil.ReferencesEqual(LibrarySpecs, librarySpecs))
                return this;

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

            if (HasDocumentLibrary && !LibrarySpecs.Any(spec => spec != null && spec.IsDocumentLibrary))
            {
                // Not possible to reconcile until Document Library is loaded.
                return;
            }

            // In case library specs got disconnected
            if (!LibrarySpecs.Any())
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
                throw new InvalidDataException(string.Format(Resources.PeptideLibraries_EnsureRankId_Specified_libraries_do_not_support_the___0___peptide_ranking, idName));

            // No longer necessary
            _rankIdName = null;

            RankId = idFound;
        }

        private enum ATTR
        {
            pick,
            rank_type,
            peptide_count,
            document_library
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if ((Pick == PeptidePick.filter || Pick == PeptidePick.either) && RankId != null)
                throw new InvalidDataException(Resources.PeptideLibraries_DoValidate_The_specified_method_of_matching_library_spectra_does_not_support_peptide_ranking);
            if (_rankIdName == null && RankId == null && PeptideCount != null)
                throw new InvalidDataException(Resources.PeptideLibraries_DoValidate_Limiting_peptides_per_protein_requires_a_ranking_method_to_be_specified);

            EnsureRankId();

            if (PeptideCount.HasValue && (PeptideCount.Value < MIN_PEPTIDE_COUNT || PeptideCount.Value > MAX_PEPTIDE_COUNT))
            {
                throw new InvalidDataException(string.Format(Resources.PeptideLibraries_DoValidate_Library_picked_peptide_count__0__must_be_between__1__and__2__,
                                                             PeptideCount, MIN_PEPTIDE_COUNT, MAX_PEPTIDE_COUNT));
            }

            // Libraries and library specs must match.  If they do not, then
            // there was a coding error.
            Assume.IsTrue(LibrariesMatch(), Resources.PeptideLibraries_DoValidate_Libraries_and_library_specifications_do_not_match_);

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
            new XmlElementHelperSuper<ChromatogramLibrary, Library>(),
            new XmlElementHelperSuper<XHunterLibrary, Library>(),
            new XmlElementHelperSuper<NistLibrary, Library>(),
            new XmlElementHelperSuper<SpectrastLibrary, Library>(),
            new XmlElementHelperSuper<MidasLibrary, Library>(),
            new XmlElementHelperSuper<EncyclopeDiaLibrary,Library>()
        };

        private static readonly IXmlElementHelper<LibrarySpec>[] LIBRARY_SPEC_HELPERS =
        {
            new XmlElementHelperSuper<BiblioSpecLibSpec, LibrarySpec>(),
            new XmlElementHelperSuper<BiblioSpecLiteSpec, LibrarySpec>(),
            new XmlElementHelperSuper<ChromatogramLibrarySpec, LibrarySpec>(),
            new XmlElementHelperSuper<XHunterLibSpec, LibrarySpec>(),
            new XmlElementHelperSuper<NistLibSpec, LibrarySpec>(),
            new XmlElementHelperSuper<SpectrastSpec, LibrarySpec>(),
            new XmlElementHelperSuper<MidasLibSpec, LibrarySpec>(), 
            new XmlElementHelperSuper<EncyclopeDiaSpec,LibrarySpec>()
        };

        public static IXmlElementHelper<LibrarySpec>[] LibrarySpecXmlHelpers
        {
            get { return LIBRARY_SPEC_HELPERS; }
        }

        private static readonly IXmlElementHelper<SpectrumHeaderInfo>[] LIBRARY_HEADER_HELPERS =
        {
            new XmlElementHelperSuper<BiblioSpecSpectrumHeaderInfo, SpectrumHeaderInfo>(),
            new XmlElementHelperSuper<ChromLibSpectrumHeaderInfo, SpectrumHeaderInfo>(),
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
            HasDocumentLibrary = reader.GetBoolAttribute(ATTR.document_library);

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
                var library = list[i] as Library;
                if (library != null)
                    libraries[i] = library;
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
            if (RankId != null || _rankIdName != null)
            {
                // If libraries were never connected properly, then _rankIdName may still contain
                // the rank ID.
                writer.WriteAttribute(ATTR.rank_type, RankId != null ? RankId.Value : _rankIdName);
                writer.WriteAttributeNullable(ATTR.peptide_count, PeptideCount);
            }
            writer.WriteAttribute(ATTR.document_library, HasDocumentLibrary);

            // Write child elements
            var libraries = (_libraries.Count > 0 || _disconnectedLibraries == null ?
                _libraries : _disconnectedLibraries);

            if (libraries.Count > 0)
            {
                // writer.WriteElements(_libraries, LIBRARY_HELPERS);
                for (int i = 0; i < libraries.Count; i++)
                {
                    // First make sure it's not the document library
                    var spec = (!ReferenceEquals(libraries, _disconnectedLibraries) ? _librarySpecs[i] : null);
                    if (spec == null || !spec.IsDocumentLibrary)
                    {
                        // If there is a library, write it.  Otherwise, write the
                        // library spec.
                        var item = libraries[i];
                        if (item == null)
                        {
                            if (spec != null && !spec.IsDocumentLocal)
                            {
                                IXmlElementHelper<LibrarySpec> helper = XmlUtil.FindHelper(spec, LIBRARY_SPEC_HELPERS);
                                if (helper == null)
                                    throw new InvalidOperationException(
                                        Resources.
                                            PeptideLibraries_WriteXml_Attempt_to_serialize_list_containing_invalid_type);
                                writer.WriteElement(helper.ElementNames[0], spec);
                            }
                        }
                        else
                        {
                            IXmlElementHelper<Library> helper = XmlUtil.FindHelper(item, LIBRARY_HELPERS);
                            if (helper == null)
                                throw new InvalidOperationException(
                                    Resources.
                                        PeptideLibraries_WriteXml_Attempt_to_serialize_list_containing_invalid_type);
                            writer.WriteElement(helper.ElementNames[0], item);
                        }
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


    [XmlRoot("peptide_integration")]
    public sealed class PeptideIntegration : Immutable, IValidating, IXmlSerializable
    {
        public PeptideIntegration(PeakScoringModelSpec peakScoringModel)
        {
            AutoTrain = AutoTrainType.none;
            PeakScoringModel = peakScoringModel ?? LegacyScoringModel.DEFAULT_UNTRAINED_MODEL;
            ScoreQValueMap = ScoreQValueMap.EMPTY;
        }

        public enum AutoTrainType { none, default_model, mprophet_model }

        public AutoTrainType AutoTrain { get; private set; }
        public bool IsAutoTrain => !Equals(AutoTrain, AutoTrainType.none);
        [TrackChildren]
        public PeakScoringModelSpec PeakScoringModel { get; private set; }
        public bool IsSerializable { get { return IsAutoTrain || PeakScoringModel.IsTrained; } }
        public MProphetResultsHandler ResultsHandler { get; private set; }

        #region Property change methods

        public PeptideIntegration ChangePeakScoringModel(PeakScoringModelSpec prop)
        {
            return ChangeProp(ImClone(this), im => im.PeakScoringModel = prop);
        }

        public PeptideIntegration ChangeAutoTrain(AutoTrainType prop)
        {
            return ChangeProp(ImClone(this), im => im.AutoTrain = prop);
        }

        /// <summary>
        /// Changing this starts a peak reintegration when it is set on the document.
        /// </summary>
        public PeptideIntegration ChangeResultsHandler(MProphetResultsHandler prop)
        {
            return ChangeProp(ImClone(this), im =>
            {
                if (prop != null)
                    im.PeakScoringModel = prop.ScoringModel;
                im.ResultsHandler = prop;
            });
        }

        public ScoreQValueMap ScoreQValueMap { get; private set; }

        public PeptideIntegration ChangeScoreQValueMap(ScoreQValueMap scoreQValueMap)
        {
            return ChangeProp(ImClone(this), im => im.ScoreQValueMap = scoreQValueMap);
        }

        #endregion

        #region Implementation of IXmlSerializable

        private enum ATTR
        {
            auto_train
        }

        void IValidating.Validate()
        {
        }

        // Support for serializing multiple peak scoring models
        private static readonly IXmlElementHelper<PeakScoringModelSpec>[] PEAK_SCORING_MODEL_SPEC_HELPERS =
        {
            new XmlElementHelperSuper<MProphetPeakScoringModel, PeakScoringModelSpec>(),                 
            new XmlElementHelperSuper<LegacyScoringModel, PeakScoringModelSpec>(),                 
        };

        public static PeptideIntegration Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new PeptideIntegration(null));
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (Enum.TryParse<AutoTrainType>(reader.GetAttribute(ATTR.auto_train), out var autoTrain))
                AutoTrain = autoTrain;

            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                // Read child element
                var helperSpec = reader.FindHelper(PEAK_SCORING_MODEL_SPEC_HELPERS);
                if (helperSpec != null)
                    PeakScoringModel = helperSpec.Deserialize(reader);
                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write child elements
            if (IsAutoTrain)
                writer.WriteAttribute(ATTR.auto_train, AutoTrain);
            if (PeakScoringModel.IsTrained)
            {
                var helper = XmlUtil.FindHelper(PeakScoringModel, PEAK_SCORING_MODEL_SPEC_HELPERS);
                if (helper == null)
                    throw new InvalidOperationException(Resources.PeptideLibraries_WriteXml_Attempt_to_serialize_list_containing_invalid_type);
                writer.WriteElement(helper.ElementNames[0], PeakScoringModel);                            
            }
        }

        #endregion

        #region object overrides

        private bool Equals(PeptideIntegration other)
        {
            return AutoTrain == other.AutoTrain && Equals(PeakScoringModel, other.PeakScoringModel) &&
                   Equals(ScoreQValueMap, other.ScoreQValueMap);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var other = obj as PeptideIntegration;
            return other != null && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = AutoTrain.GetHashCode();
                result = (result * 397) ^ (PeakScoringModel != null ? PeakScoringModel.GetHashCode() : 0);
                result = (result * 397) ^ ScoreQValueMap.GetHashCode();
                return result;
            }
        }

        #endregion
    }
}
