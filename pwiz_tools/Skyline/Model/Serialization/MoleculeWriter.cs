/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.ChromLib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    /// <summary>
    /// Builds the &lt;peptide&gt; or &lt;molecule&gt; element of one
    /// <see cref="PeptideDocNode"/>, and everything below it.
    /// <para>
    /// One of these is made per molecule, which is what lets the things that are the same for all
    /// of a molecule - the node itself and its <see cref="MoleculeResults"/> - be fields rather
    /// than parameters handed down through every level. It is the same lifetime a
    /// <see cref="MoleculeResults"/> has, and this owns the one it uses.
    /// </para>
    /// <para>
    /// There is no <see cref="XmlWriter"/> here. Everything is an <see cref="XElement"/> which is
    /// still open to being changed, which is what lets one pass decide what every level says: see
    /// <see cref="CreateResults"/>, where a transition's results and its precursor's are worked
    /// out together because neither is complete without the other.
    /// </para>
    /// </summary>
    public class MoleculeWriter : DocumentSerializer
    {
        private readonly DocumentWriter _documentWriter;

        /// <summary>
        /// One per molecule, made once, because making one reads every chromatogram of the
        /// molecule. The doc nodes no longer keep their chrom infos, so this is where every
        /// attribute of them written below comes from.
        /// <para>
        /// Null when there is no chromatogram to rebuild them from, which drops this molecule back
        /// to writing its columnar results. Those are the document's own record of its peaks and
        /// are there either way, so a document whose .skyd cannot be read - moved, deleted, or not
        /// built yet - is written with the areas and retention times it has rather than with no
        /// results at all.
        /// </para>
        /// </summary>
        private MoleculeResults _moleculeResults;

        public MoleculeWriter(DocumentWriter documentWriter, PeptideDocNode peptideDocNode)
        {
            _documentWriter = documentWriter;
            PeptideDocNode = peptideDocNode;
            Settings = documentWriter.Settings;
            DocumentFormat = documentWriter.DocumentFormat;
        }

        public PeptideDocNode PeptideDocNode { get; private set; }

        private SkylineVersion SkylineVersion
        {
            get { return _documentWriter.SkylineVersion; }
        }

        /// <summary>
        /// Whether the transitions of this molecule are written as protocol buffers. Never when its
        /// columnar results are what is being written: the compact format is an encoding of the
        /// chrom infos, and there is nothing of it which says the areas a transition keeps now. A
        /// null <see cref="_moleculeResults"/> is what says so - see
        /// <see cref="DocumentWriter.WriteChromInfos"/> and
        /// <see cref="MoleculeResults.HasChromatograms"/>.
        /// <para>
        /// The same answer for every precursor and transition of the molecule, since none of what
        /// it reads changes while one is being built.
        /// </para>
        /// </summary>
        private bool UseCompactFormat()
        {
            // A document with no results at all has no columnar results to write either, so the
            // compact format is still what shrinks a long transition list.
            if (_moleculeResults == null && Settings.MeasuredResults != null)
            {
                return false;
            }

            return DocumentFormat.CompareTo(DocumentFormat.BINARY_RESULTS) >= 0 &&
                   _documentWriter.CompactFormatOption.UseCompactFormat(_documentWriter.Document);
        }

        /// <summary>
        /// The &lt;peptide&gt; or &lt;molecule&gt; element for this <see cref="PeptideDocNode"/>.
        /// </summary>
        public XElement CreateElement()
        {
            var node = PeptideDocNode;
            var peptide = node.Peptide;
            var isCustomIon = peptide.IsCustomMolecule;

            var element = new XElement(isCustomIon ? EL.molecule : EL.peptide);
            if (node.ExplicitRetentionTime != null)
            {
                element.SetAttribute(ATTR.explicit_retention_time, node.ExplicitRetentionTime.RetentionTime);
                element.SetAttributeNullable(ATTR.explicit_retention_time_window, node.ExplicitRetentionTime.RetentionTimeWindow);
            }

            element.SetAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            if (node.GlobalStandardType != null)
                element.SetAttribute(ATTR.standard_type, node.GlobalStandardType.Name);

            element.SetAttributeNullable(ATTR.rank, node.Rank);
            element.SetAttributeNullable(ATTR.concentration_multiplier, node.ConcentrationMultiplier);
            element.SetAttributeNullable(ATTR.internal_standard_concentration, node.InternalStandardConcentration);
            if (null != node.NormalizationMethod)
            {
                element.SetAttribute(ATTR.normalization_method, node.NormalizationMethod.Name);
            }
            element.SetAttributeIfString(ATTR.attribute_group_id, node.AttributeGroupId);
            element.SetAttributeIfString(ATTR.surrogate_calibration_curve, node.SurrogateCalibrationCurve);

            // The retention time calculator score, worked out with the attributes which report it
            // and needed again by the peptide results below.
            double? scoreCalc = null;
            if (isCustomIon)
            {
                peptide.CustomMolecule.WriteXml(element, Adduct.EMPTY);
                // If user changed any molecule details (other than formula or mass) after chromatogram extraction, this info continues the target->chromatogram association
                element.SetAttributeIfString(ATTR.chromatogram_target, node.OriginalMoleculeTarget?.ToSerializableString());
            }
            else
            {
                string sequence = peptide.Target.Sequence;
                element.SetAttribute(ATTR.sequence, sequence);
                var modSeq = Settings.GetModifiedSequence(node);
                element.SetAttribute(ATTR.modified_sequence, GetModifiedSequence(modSeq));
                if (node.SourceKey != null)
                    element.SetAttribute(ATTR.lookup_sequence, node.SourceKey.ModifiedSequence);
                if (peptide.Begin.HasValue && peptide.End.HasValue)
                {
                    element.SetAttribute(ATTR.start, peptide.Begin.Value);
                    element.SetAttribute(ATTR.end, peptide.End.Value);
                    element.SetAttribute(ATTR.prev_aa, peptide.PrevAA);
                    element.SetAttribute(ATTR.next_aa, peptide.NextAA);
                }
                var massH = Settings.GetPrecursorCalc(IsotopeLabelType.light, node.ExplicitMods).GetPrecursorMass(peptide.Target);
                element.SetAttribute(ATTR.calc_neutral_pep_mass,
                    SequenceMassCalc.PersistentNeutral(massH));

                element.SetAttribute(ATTR.num_missed_cleavages, peptide.MissedCleavages);
                element.SetAttribute(ATTR.decoy, node.IsDecoy);
                var rtPredictor = Settings.PeptideSettings.Prediction.RetentionTime;
                if (rtPredictor != null)
                {
                    scoreCalc = rtPredictor.Calculator.ScoreSequence(modSeq);
                    if (scoreCalc.HasValue)
                    {
                        element.SetAttributeNullable(ATTR.rt_calculator_score, scoreCalc);
                        element.SetAttributeNullable(ATTR.predicted_retention_time,
                            rtPredictor.GetRetentionTime(scoreCalc.Value));
                    }
                }
            }

            element.SetAttributeNullable(ATTR.avg_measured_retention_time, node.AverageMeasuredRetentionTime);

            AddAnnotations(element, node.Annotations);
            if (!isCustomIon)
            {
                var explicitMods = node.ExplicitMods;
                if (DocumentFormat < DocumentFormat.FLAT_CROSSLINKS)
                {
                    if (explicitMods != null && explicitMods.HasCrosslinks)
                    {
                        try
                        {
                            explicitMods = new LegacyCrosslinkConverter(Settings, explicitMods)
                                .ConvertToLegacyFormat(new Dictionary<int, ImmutableList<ModificationSite>>());
                        }
                        catch (Exception ex)
                        {
                            throw new NotSupportedException(string.Format(SerializationResources.DocumentWriter_WritePeptideXml_Unable_to_convert_crosslinks_in__0__to_document_format__1__, node.ModifiedSequenceDisplay, DocumentFormat), ex);
                        }
                    }
                }
                // CONSIDER(bspratt) the code as written actually can use static isotope
                // label modifications, and this if clause could be removed - but Brendan wants proof of demand for this first
                element.Add(CreateExplicitModsElements(node.Peptide.Target.Sequence, explicitMods));
                element.Add(CreateImplicitModsElement());
                element.Add(CreateLookupModsElement());
                element.Add(CreateCrosslinkStructureElement(explicitMods?.CrosslinkStructure));
            }

            _moleculeResults = _documentWriter.WriteChromInfos && Settings.MeasuredResults != null
                ? new MoleculeResults(Settings, node)
                : null;
            if (_moleculeResults?.HasChromatograms == false)
            {
                _moleculeResults = null;
            }

            var peptideChromInfos = _moleculeResults?.GetPeptideChromInfos();
            if (peptideChromInfos != null)
            {
                element.Add(CreateChromInfoResultsElement(peptideChromInfos, EL.peptide_results, EL.peptide_result,
                    (peptideResult, chromInfo) => SetPeptideChromInfo(peptideResult, chromInfo, scoreCalc)));
            }

            foreach (TransitionGroupDocNode nodeGroup in node.Children)
            {
                element.Add(CreateTransitionGroupElement(nodeGroup));
            }

            return element;
        }

        #region Results

        /// <summary>
        /// One entry per replicate and file the chrom infos are held for, or null when there are
        /// none: the element exists only if something went in it.
        /// </summary>
        private XElement CreateChromInfoResultsElement<TItem>(IEnumerable<ChromInfoList<TItem>> results,
                string start, string startChild, Action<XElement, TItem> setChromInfo)
            where TItem : ChromInfo
        {
            var element = new XElement(start);
            using (var enumReplicates = Settings.MeasuredResults.Chromatograms.GetEnumerator())
            {
                foreach (var listChromInfo in results)
                {
                    bool success = enumReplicates.MoveNext();
                    Assume.IsTrue(success || Settings.MeasuredResults.Chromatograms.Count == 0);
                    if (listChromInfo.IsEmpty)
                        continue;
                    var chromatogramSet = enumReplicates.Current;
                    if (chromatogramSet == null)
                        continue;
                    foreach (var chromInfo in listChromInfo)
                    {
                        var childElement = new XElement(startChild);
                        childElement.SetAttribute(ATTR.replicate, chromatogramSet.Name);
                        if (chromatogramSet.FileCount > 1)
                            childElement.SetAttribute(ATTR.file, chromatogramSet.GetFileSaveId(chromInfo.FileId));
                        setChromInfo(childElement, chromInfo);
                        element.Add(childElement);
                    }
                }
            }
            return element.HasElements ? element : null;
        }

        /// <summary>
        /// The peaks of one precursor and of all of its transitions, in one walk of the replicates
        /// and files - which is what <see cref="ChromFileIds"/> is, and the order the reader
        /// rebuilds the flat positions from.
        /// <para>
        /// One walk because the two levels are one decision. The areas of a precursor's transitions
        /// ride on its <see cref="ATTR.transition_areas"/> wherever all of them are ordinary, and a
        /// transition whose every area is carried that way is left out of the file altogether. What
        /// a transition has left to say is only knowable with the precursor's answer in hand, so
        /// they are settled here in the same place rather than one level at a time.
        /// </para>
        /// <para>
        /// The same element names a document has always used. What tells the two apart is what is
        /// on them: this leaves out everything the .skyd gives back, and writes
        /// <see cref="ATTR.chosen_peak_index"/>, which says which candidate peak there to read.
        /// </para>
        /// </summary>
        private void AddColumnarResults(TransitionGroupDocNode nodeGroup, XElement precursorResults,
            IList<XElement> transitionElements)
        {
            var results = nodeGroup.AbbreviatedResults;
            var transitions = new Transition[transitionElements.Count];
            for (int i = 0; i < transitions.Length; i++)
            {
                transitions[i] = ((TransitionDocNode) nodeGroup.Children[i]).Transition;
            }

            // Every file any of them was found in, so that one walk reaches all of them. A
            // position in one of these objects means nothing in another, so replicate and file are
            // what the precursor and its transitions are lined up by - never a flat position.
            var chromFileIds = ChromFileIds.UnionAll(
                new[] { results.ChromFileIds }.Concat(transitions.Select(results.GetTransitionChromFileIds)));

            var transitionResults = new XElement[transitions.Length];
            var chromatograms = Settings.MeasuredResults.Chromatograms;
            for (int replicateIndex = 0;
                 replicateIndex < Math.Min(chromFileIds.ReplicatePositions.ReplicateCount, chromatograms.Count);
                 replicateIndex++)
            {
                var chromatogramSet = chromatograms[replicateIndex];
                foreach (var fileId in chromFileIds.GetFileIds(replicateIndex))
                {
                    var sharedAreas = results.GetSharedTransitionAreas(replicateIndex, fileId);

                    if (results.TryGetPrecursorPeak(replicateIndex, fileId, out _))
                    {
                        var peakElement = new XElement(EL.precursor_peak);
                        SetReplicateAndFile(peakElement, chromatogramSet, fileId);
                        SetColumnarPrecursorPeak(peakElement, results, sharedAreas, replicateIndex, fileId);
                        precursorResults.Add(peakElement);
                    }

                    if (sharedAreas != null)
                    {
                        // The areas just went on the precursor, and being shareable is exactly
                        // having nothing else to say, so no transition writes anything here.
                        continue;
                    }

                    for (int i = 0; i < transitions.Length; i++)
                    {
                        if (!results.TryGetTransitionPeak(transitions[i], replicateIndex, fileId, out var peak))
                        {
                            continue;
                        }

                        if (transitionResults[i] == null)
                        {
                            transitionResults[i] = new XElement(EL.transition_results);
                            transitionElements[i].Add(transitionResults[i]);
                        }

                        var transitionPeak = new XElement(EL.transition_peak);
                        SetReplicateAndFile(transitionPeak, chromatogramSet, fileId);
                        SetColumnarTransitionPeak(transitionPeak, results, transitions[i], replicateIndex, fileId,
                            peak);
                        transitionResults[i].Add(transitionPeak);
                    }
                }
            }
        }

        private static void SetReplicateAndFile(XElement element, ChromatogramSet chromatogramSet,
            ChromFileInfoId fileId)
        {
            element.SetAttribute(ATTR.replicate, chromatogramSet.Name);
            if (chromatogramSet.FileCount > 1)
            {
                element.SetAttribute(ATTR.file, chromatogramSet.GetFileSaveId(fileId));
            }
        }

        private void SetColumnarPrecursorPeak(XElement element, TransitionGroupResults results, float[] sharedAreas,
            int replicateIndex, ChromFileInfoId fileId)
        {
                results.TryGetPrecursorPeak(replicateIndex, fileId, out var precursorPeak);
                // No area: a precursor's is the sum of its transitions', which are written below it.
                element.SetAttribute(ATTR.retention_time, precursorPeak.RetentionTime);
                // Nullable rather than the generic default-value overload, which formats with
                // ToString() and so loses digits a float needs to come back the same.
                element.SetAttributeNullable(ATTR.start_time, results.GetStartTime(replicateIndex, fileId));
                element.SetAttributeNullable(ATTR.end_time, results.GetEndTime(replicateIndex, fileId));
                // Written, even as -1, by a precursor which knows which candidate peaks its peaks
                // are; left out altogether by one which does not. Its presence is what
                // DocumentReader reads back as TransitionGroupResults.NeedsPeakIndexes, so writing
                // it either way would tell a document being read again that the matching had been
                // done when it had not, and -1 would be taken for "not a candidate peak" rather
                // than "not worked out". A precursor which does not know keeps everything its
                // peaks need instead - see SetColumnarTransitionPeak.
                if (!results.NeedsPeakIndexes)
                {
                    element.SetAttribute(ATTR.chosen_peak_index,
                        results.GetChosenPeakIndex(replicateIndex, fileId) ?? PrecursorPeak.NO_PEAK_INDEX);
                }
                element.SetAttributeNullable(ATTR.qvalue, results.GetQValue(replicateIndex, fileId));
                element.SetAttributeNullable(ATTR.zscore, results.GetZScore(replicateIndex, fileId));
                element.SetAttribute(ATTR.user_set, results.GetUserSet(replicateIndex, fileId), UserSet.FALSE);
                if (sharedAreas != null)
                {
                    element.SetFloatsAttribute(ATTR.transition_areas, sharedAreas);
                }

                AddAnnotations(element, results.GetAnnotations(replicateIndex, fileId));
        }

        /// <summary>
        /// One transition's peak, written only where it has something its precursor does not
        /// already say: an area which did not ride on <see cref="ATTR.transition_areas"/>,
        /// boundaries of its own, something integrating between them cannot find again, or an
        /// annotation.
        /// <para>
        /// Each value is looked up by replicate and file, because each of them is its own map: they
        /// are held only where there is one, and none of them has an entry wherever another does.
        /// </para>
        /// </summary>
        private void SetColumnarTransitionPeak(XElement element, TransitionGroupResults results,
            Transition transition, int replicateIndex, ChromFileInfoId fileId, TransitionPeak peak)
        {
            element.SetAttribute(ATTR.area, peak.Area);
            element.SetAttribute(ATTR.user_set, peak.UserSet, UserSet.FALSE);
            // Nothing else carries these, so a transition written out has to say them. They
            // are the reason a peak which is anything but ordinary cannot ride its
            // precursor's transition_areas - see TransitionResults.TryGetPlainArea, which
            // decides that, and SharedTransitionAreas.MakeTransitionResults, which puts
            // back exactly the values it treats as ordinary.
            element.SetAttributeNullable(ATTR.truncated, peak.IsTruncated);
            element.SetAttribute(ATTR.forced_integration, peak.IsForcedIntegration, false);
            element.SetAttribute(ATTR.empty, peak.IsEmpty, false);

            var peakBounds = results.FindTransitionCustomPeakBounds(transition, replicateIndex, fileId);
            if (peakBounds.HasValue)
            {
                element.SetAttribute(ATTR.start_time, peakBounds.Value.StartTime);
                element.SetAttribute(ATTR.end_time, peakBounds.Value.EndTime);
            }

            var peakMetrics = results.FindTransitionCustomPeakMetrics(transition, replicateIndex, fileId);
            if (peakMetrics != null)
            {
                element.SetAttributeNullable(ATTR.mass_error_ppm, peakMetrics.MassError);
                if (peakMetrics.Identified != PeakIdentification.FALSE)
                    element.SetAttribute(ATTR.identified, peakMetrics.Identified.ToString().ToLowerInvariant());
            }

            AddAnnotations(element, results.FindTransitionAnnotations(transition, replicateIndex, fileId));
        }

        private void SetPeptideChromInfo(XElement element, PeptideChromInfo chromInfo, double? scoreCalc)
        {
            element.SetAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            element.SetAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            element.SetAttribute(ATTR.exclude_from_calibration, chromInfo.ExcludeFromCalibration);
            element.SetAttributeNullable(ATTR.analyte_concentration, chromInfo.AnalyteConcentration);
            if (scoreCalc.HasValue)
            {
                double? rt = Settings.PeptideSettings.Prediction.RetentionTime.GetRetentionTime(scoreCalc.Value,
                                                                                      chromInfo.FileId);
                element.SetAttributeNullable(ATTR.predicted_retention_time, rt);
            }
        }

        private void SetTransitionGroupChromInfo(XElement element, TransitionGroupChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                element.SetAttribute(ATTR.step, chromInfo.OptimizationStep);
            element.SetAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            element.SetAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            element.SetAttributeNullable(ATTR.start_time, chromInfo.StartRetentionTime);
            element.SetAttributeNullable(ATTR.end_time, chromInfo.EndRetentionTime);
            element.SetAttributeNullable(ATTR.ccs, chromInfo.IonMobilityInfo.CollisionalCrossSection);
            if (chromInfo.IonMobilityInfo.IonMobilityUnits != eIonMobilityUnits.none)
            {
                element.SetAttributeNullable(ATTR.ion_mobility_ms1, chromInfo.IonMobilityInfo.IonMobilityMS1);
                element.SetAttributeNullable(ATTR.ion_mobility_fragment, chromInfo.IonMobilityInfo.IonMobilityFragment);
                element.SetAttributeNullable(ATTR.ion_mobility_window, chromInfo.IonMobilityInfo.IonMobilityWindow);
                element.SetAttribute(ATTR.ion_mobility_type, chromInfo.IonMobilityInfo.IonMobilityUnits.ToString());
            }
            element.SetAttributeNullable(ATTR.fwhm, chromInfo.Fwhm);
            element.SetAttributeNullable(ATTR.area, chromInfo.Area);
            element.SetAttributeNullable(ATTR.background, chromInfo.BackgroundArea);
            element.SetAttributeNullable(ATTR.height, chromInfo.Height);
            element.SetAttributeNullable(ATTR.mass_error_ppm, chromInfo.MassError);
            element.SetAttributeNullable(ATTR.truncated, chromInfo.Truncated);
            element.SetAttribute(ATTR.identified, chromInfo.Identified.ToString().ToLowerInvariant());
            element.SetAttributeNullable(ATTR.library_dotp, chromInfo.LibraryDotProduct);
            element.SetAttributeNullable(ATTR.isotope_dotp, chromInfo.IsotopeDotProduct);
            element.SetAttributeNullable(ATTR.qvalue, chromInfo.QValue);
            element.SetAttributeNullable(ATTR.zscore, chromInfo.ZScore);
            element.SetAttribute(ATTR.user_set, chromInfo.UserSet);
            var originalPeak = chromInfo.OriginalPeak;
            if (originalPeak != null && originalPeak.StartTime.Equals(chromInfo.StartRetentionTime) && originalPeak.EndTime.Equals(chromInfo.EndRetentionTime))
            {
                element.SetAttribute(ATTR.original_score, originalPeak.Score);
                originalPeak = null;
            }
            AddAnnotations(element, chromInfo.Annotations);
            element.Add(CreateScoredPeakElement(EL.original_peak, originalPeak));
            element.Add(CreateScoredPeakElement(EL.reintegrated_peak, chromInfo.ReintegratedPeak));
        }

        private XElement CreateScoredPeakElement(string el, ScoredPeakBounds scoredPeak)
        {
            if (scoredPeak == null || DocumentFormat < DocumentFormat.PEAK_IMPUTATION)
            {
                return null;
            }
            var element = new XElement(el);
            element.SetAttribute(ATTR.score, scoredPeak.Score);
            element.SetAttribute(ATTR.retention_time, scoredPeak.ApexTime);
            element.SetAttribute(ATTR.start_time, scoredPeak.StartTime);
            element.SetAttribute(ATTR.end_time, scoredPeak.EndTime);
            return element;
        }

        private void SetTransitionChromInfo(XElement element, TransitionChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                element.SetAttribute(ATTR.step, chromInfo.OptimizationStep);

            // Only write peak information, if it is not empty
            if (!chromInfo.IsEmpty)
            {
                element.SetAttributeNullable(ATTR.mass_error_ppm, chromInfo.MassError);
                element.SetAttribute(ATTR.retention_time, chromInfo.RetentionTime);
                element.SetAttribute(ATTR.start_time, chromInfo.StartRetentionTime);
                element.SetAttribute(ATTR.end_time, chromInfo.EndRetentionTime);
                element.SetAttributeNullable(ATTR.ccs, chromInfo.IonMobility.CollisionalCrossSectionSqA);
                element.SetAttributeNullable(ATTR.ion_mobility, chromInfo.IonMobility.IonMobility.Mobility);
                element.SetAttributeNullable(ATTR.ion_mobility_window, chromInfo.IonMobility.IonMobilityExtractionWindowWidth);
                element.SetAttribute(ATTR.area, chromInfo.Area);
                element.SetAttribute(ATTR.background, chromInfo.BackgroundArea);
                element.SetAttribute(ATTR.height, chromInfo.Height);
                element.SetAttribute(ATTR.fwhm, chromInfo.Fwhm);
                element.SetAttribute(ATTR.fwhm_degenerate, chromInfo.IsFwhmDegenerate);
                element.SetAttributeNullable(ATTR.truncated, chromInfo.IsTruncated);
                element.SetAttribute(ATTR.identified, chromInfo.Identified.ToString().ToLowerInvariant());
                element.SetAttribute(ATTR.rank, chromInfo.Rank);
                var peakShapeValues = chromInfo.PeakShapeValues;
                if (peakShapeValues.HasValue)
                {
                    element.SetAttribute(ATTR.std_dev, peakShapeValues.Value.StdDev);
                    element.SetAttribute(ATTR.skewness, peakShapeValues.Value.Skewness);
                    element.SetAttribute(ATTR.kurtosis, peakShapeValues.Value.Kurtosis);
                    element.SetAttribute(ATTR.shape_correlation, peakShapeValues.Value.ShapeCorrelation);
                }
                if (SkylineVersion.SrmDocumentVersion.CompareTo(DocumentFormat.VERSION_3_61) >= 0)
                {
                    element.SetAttributeNullable(ATTR.points_across, chromInfo.PointsAcrossPeak);
                }
                if (chromInfo.Rank != chromInfo.RankByLevel)
                    element.SetAttribute(ATTR.rank_by_level, chromInfo.RankByLevel);
            }
            element.SetAttribute(ATTR.user_set, chromInfo.UserSet);
            element.SetAttribute(ATTR.forced_integration, chromInfo.IsForcedIntegration, false);
            AddAnnotations(element, chromInfo.Annotations);
        }

        #endregion

        #region Precursors and transitions

        /// <summary>
        /// The &lt;precursor&gt; element of one <see cref="TransitionGroupDocNode"/>, and every
        /// &lt;transition&gt; below it.
        /// <para>
        /// The precursor's results and its transitions' are worked out here rather than at either
        /// level alone, because they are one decision - see
        /// <see cref="CreateTransitionResultsElement"/>.
        /// </para>
        /// </summary>
        private XElement CreateTransitionGroupElement(TransitionGroupDocNode node)
        {
            var nodePep = PeptideDocNode;
            var element = new XElement(EL.precursor);
            TransitionGroup group = node.TransitionGroup;
            var isCustomIon = nodePep.Peptide.IsCustomMolecule;
            element.SetAttribute(ATTR.charge, group.PrecursorAdduct.AdductCharge);
            if (!group.LabelType.IsLight)
                element.SetAttribute(ATTR.isotope_label, group.LabelType);
            if (!isCustomIon)
            {
                element.SetAttribute(ATTR.calc_neutral_mass, node.GetPrecursorIonPersistentNeutralMass());
            }
            element.SetAttribute(ATTR.precursor_mz, SequenceMassCalc.PersistentMZ(node.PrecursorMz));
            SetExplicitTransitionGroupValuesAttributes(element, node.ExplicitValues);

            element.SetAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            element.SetAttributeNullable(ATTR.decoy_mass_shift, group.DecoyMassShift);
            element.SetAttributeNullable(ATTR.precursor_concentration, node.PrecursorConcentration);


            TransitionPrediction predict = Settings.TransitionSettings.Prediction;
            double regressionMz = Settings.GetRegressionMz(nodePep, node);
            var ce = predict.CollisionEnergy.GetCollisionEnergy(node.TransitionGroup.PrecursorAdduct, regressionMz);
            element.SetAttribute(ATTR.collision_energy, ce);

            var dpRegression = predict.DeclusteringPotential;
            if (dpRegression != null)
            {
                var dp = dpRegression.GetDeclustringPotential(regressionMz);
                element.SetAttribute(ATTR.declustering_potential, dp);
            }

            if (!isCustomIon)
            {
                // modified sequence
                if (nodePep.ExplicitMods != null && nodePep.ExplicitMods.HasCrosslinks)
                {
                    element.SetAttribute(ATTR.modified_sequence,
                        Settings.GetCrosslinkModifiedSequence(nodePep.Target, node.TransitionGroup.LabelType, nodePep.ExplicitMods));
                }
                else
                {
                    var calcPre = Settings.GetPrecursorCalc(node.TransitionGroup.LabelType, nodePep.ExplicitMods);
                    var seq = node.TransitionGroup.Peptide.Target;
                    element.SetAttribute(ATTR.modified_sequence, calcPre.GetModifiedSequence(seq,
                        false)); // formatNarrow = false; We want InvariantCulture, not the local format
                }
                Assume.IsTrue(group.PrecursorAdduct.IsProteomic, @"expected IsProteomic tag on adduct");
            }
            else
            {
                // Custom ion
                node.CustomMolecule.WriteXml(element, group.PrecursorAdduct);
            }

            AddAnnotations(element, node.Annotations);
            AddXmlWriterContent(element, w => node.SpectrumClassFilter.WriteXml(w));
            if (node.HasLibInfo)
            {
                var helpers = PeptideLibraries.SpectrumHeaderXmlHelpers;
                var libInfo = node.LibInfo;
                if (libInfo is EncyclopeDiaLibrary.ElibSpectrumHeaderInfo && DocumentFormat < DocumentFormat.VERSION_22_25)
                {
                    // Older versions of Skyline used ChromLibSpectrumHeaderInfo instead of ElibSpectrumHeaderInfo
                    libInfo = new ChromLibSpectrumHeaderInfo(libInfo.LibraryName, 0, null);
                }
                AddXmlWriterContent(element, w => w.WriteElements(new[] { libInfo }, helpers));
            }

            // Goes in ahead of the transitions, which is where the document has always had it, and
            // is filled in below once they are all here. An XElement is still open to being added
            // to after it is in the tree, so its place and its contents are two separate questions.
            var columnarPositions = _moleculeResults == null
                ? node.AbbreviatedResults?.ChromFileIds?.ReplicatePositions
                : null;
            XElement precursorResults = null;
            if (columnarPositions != null && columnarPositions.TotalCount != 0)
            {
                precursorResults = new XElement(EL.precursor_results);
                element.Add(precursorResults);
            }
            else if (_moleculeResults != null)
            {
                var groupChromInfos = _moleculeResults.GetTransitionGroupChromInfos(group);
                if (groupChromInfos != null)
                {
                    element.Add(CreateChromInfoResultsElement(groupChromInfos, EL.precursor_results,
                        EL.precursor_peak, SetTransitionGroupChromInfo));
                }
            }

            if (UseCompactFormat())
            {
                var transitionData = new SkylineDocumentProto.Types.TransitionData();
                // The peaks come from the chromatograms, since a transition does not keep them.
                transitionData.Transitions.AddRange(node.Transitions.Select(transition =>
                    transition.ToTransitionProto(Settings, nodePep, node,
                        _moleculeResults?.GetTransitionChromInfos(group, transition.Transition))));
                element.Add(new XElement(EL.transition_data, Convert.ToBase64String(transitionData.ToByteArray())));
                _documentWriter.OnWroteTransitions(node.TransitionCount);
                return element;
            }

            var transitionElements = new List<XElement>(node.Children.Count);
            foreach (TransitionDocNode nodeTransition in node.Children)
            {
                var transitionElement = CreateTransitionElement(node, nodeTransition);
                element.Add(transitionElement);
                transitionElements.Add(transitionElement);
                _documentWriter.OnWroteTransitions(1);
            }

            if (precursorResults != null)
            {
                AddColumnarResults(node, precursorResults, transitionElements);
            }
            else if (_moleculeResults != null)
            {
                for (int i = 0; i < transitionElements.Count; i++)
                {
                    // Worked out from the chromatograms, since a transition does not keep them.
                    var transitionChromInfos = _moleculeResults.GetTransitionChromInfos(group,
                        ((TransitionDocNode) node.Children[i]).Transition);
                    if (transitionChromInfos != null)
                    {
                        transitionElements[i].Add(CreateChromInfoResultsElement(transitionChromInfos,
                            EL.transition_results, EL.transition_peak, SetTransitionChromInfo));
                    }
                }
            }

            return element;
        }

        /// <summary>
        /// Serializes any optionally explicitly specified CE, RT and DT information to attributes only
        /// </summary>
        private void SetExplicitTransitionGroupValuesAttributes(XElement element, ExplicitTransitionGroupValues importedAttributes)
        {
            if (DocumentFormat < DocumentFormat.VERSION_4_22 || DocumentFormat >= DocumentFormat.VERSION_20_12) // Format supports per-precursor explicit CE?
                element.SetAttributeNullable(ATTR.explicit_collision_energy, importedAttributes.CollisionEnergy);
            element.SetAttributeNullable(ATTR.explicit_ion_mobility, importedAttributes.IonMobility);
            if (importedAttributes.IonMobility.HasValue)
                element.SetAttribute(ATTR.explicit_ion_mobility_units, importedAttributes.IonMobilityUnits.ToString());
            element.SetAttributeNullable(ATTR.explicit_ccs_sqa, importedAttributes.CollisionalCrossSectionSqA);
        }

        /// <summary>
        /// The &lt;transition&gt; element of one <see cref="TransitionDocNode"/>, without its
        /// results: those are added by <see cref="CreateTransitionGroupElement"/>, which walks the
        /// replicates once for the precursor and all of its transitions together.
        /// </summary>
        private XElement CreateTransitionElement(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTransition)
        {
            var nodePep = PeptideDocNode;
            var element = new XElement(EL.transition);
            Transition transition = nodeTransition.Transition;
            element.SetAttribute(ATTR.fragment_type, transition.IonType);
            element.SetAttribute(ATTR.quantitative, nodeTransition.ExplicitQuantitative, true);
            SetExplicitTransitionValuesAttributes(element, nodeTransition.ExplicitValues);
            if (transition.IsCustom())
            {
                if (!(transition.CustomIon is SettingsCustomIon))
                {
                    transition.CustomIon.WriteXml(element, transition.Adduct);
                }
                else
                {
                    element.SetAttribute(ATTR.measured_ion_name, transition.CustomIon.Name);
                }
            }
            element.SetAttributeNullable(ATTR.decoy_mass_shift, transition.DecoyMassShift);
            // NOTE: MassIndex is the peak index in the isotopic distribution of the precursor.
            //       0 for monoisotopic peaks and for non "precursor" ion types.
            if (transition.MassIndex != 0)
                element.SetAttribute(ATTR.mass_index, transition.MassIndex);
            if (nodeTransition.HasDistInfo)
            {
                element.SetAttribute(ATTR.isotope_dist_rank, nodeTransition.IsotopeDistInfo.Rank);
                element.SetAttribute(ATTR.isotope_dist_proportion, nodeTransition.IsotopeDistInfo.Proportion);
            }

            if (transition.IsPrecursor())
            {
                element.SetAttribute(ATTR.product_charge, transition.Charge, nodeGroup.PrecursorCharge);
            }
            else
            {
                if (!transition.IsCustom())
                {
                    element.SetAttribute(ATTR.fragment_ordinal, transition.Ordinal);
                    element.SetAttribute(ATTR.calc_neutral_mass, nodeTransition.GetMoleculePersistentNeutralMass());
                }
                element.SetAttribute(ATTR.product_charge, transition.Charge);
                if (!transition.IsCustom())
                {
                    element.SetAttribute(ATTR.cleavage_aa, transition.AA.ToString(CultureInfo.InvariantCulture));
                    if (nodeTransition.HasLoss)
                        element.SetAttribute(ATTR.loss_neutral_mass, nodeTransition.LostMass); //po
                }
            }

            if (nodeTransition.ComplexFragmentIon.IsOrphan)
            {
                element.SetAttribute(ATTR.orphaned_crosslink_ion, true);
            }

            // Order of elements matters for XSD validation
            AddAnnotations(element, nodeTransition.Annotations);
            element.Add(new XElement(EL.precursor_mz, SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz)
                .ToString(Formats.RoundTrip, CultureInfo.InvariantCulture)));
            element.Add(new XElement(EL.product_mz, SequenceMassCalc.PersistentMZ(nodeTransition.Mz)
                .ToString(Formats.RoundTrip, CultureInfo.InvariantCulture)));

            double? ce = nodeTransition.GetCollisionEnergy(Settings, nodePep, nodeGroup);
            double? dp = nodeTransition.GetDeclusteringPotential(Settings, nodePep, nodeGroup);

            if (ce.HasValue)
            {
                element.Add(new XElement(EL.collision_energy,
                    ce.Value.ToString(Formats.RoundTrip, CultureInfo.InvariantCulture)));
            }

            if (dp.HasValue)
            {
                element.Add(new XElement(EL.declustering_potential,
                    dp.Value.ToString(Formats.RoundTrip, CultureInfo.InvariantCulture)));
            }
            element.Add(CreateTransitionLossesElement(nodeTransition.Losses));
            if (!nodePep.CrosslinkStructure.IsEmpty)
            {
                if (DocumentFormat < DocumentFormat.FLAT_CROSSLINKS)
                {
                    var sitePathMap = new Dictionary<int, ImmutableList<ModificationSite>>();
                    var legacyConverter = new LegacyCrosslinkConverter(Settings, nodePep.ExplicitMods);
                    legacyConverter.ConvertToLegacyFormat(sitePathMap);
                    var ionChain = nodeTransition.ComplexFragmentIon.NeutralFragmentIon.IonChain;
                    var linkedIons = new Dictionary<ImmutableList<ModificationSite>, IonOrdinal>();
                    for (int i = 0; i < ionChain.Count; i++)
                    {
                        linkedIons.Add(sitePathMap[i], ionChain[i]);
                    }
                    element.Add(CreateLegacyLinkedIonElements(ImmutableList<ModificationSite>.EMPTY, linkedIons));
                }
                else
                {
                    element.Add(CreateLinkedIonElements(nodeTransition.ComplexFragmentIon.NeutralFragmentIon));
                }
            }
            if (nodeTransition.HasLibInfo)
            {
                var libInfoElement = new XElement(EL.transition_lib_info);
                libInfoElement.SetAttribute(ATTR.rank, nodeTransition.LibInfo.Rank);
                libInfoElement.SetAttribute(ATTR.intensity, nodeTransition.LibInfo.Intensity);
                element.Add(libInfoElement);
            }

            return element;
        }

        private void SetExplicitTransitionValuesAttributes(XElement element, ExplicitTransitionValues importedAttributes)
        {
            element.SetAttributeNullable(ATTR.explicit_collision_energy, importedAttributes.CollisionEnergy);
            element.SetAttributeNullable(ATTR.explicit_ion_mobility_high_energy_offset, importedAttributes.IonMobilityHighEnergyOffset);
            element.SetAttributeNullable(ATTR.explicit_s_lens, importedAttributes.SLens);
            element.SetAttributeNullable(ATTR.explicit_cone_voltage, importedAttributes.ConeVoltage);
            element.SetAttributeNullable(ATTR.explicit_declustering_potential, importedAttributes.DeclusteringPotential);
        }

        private XElement CreateTransitionLossesElement(TransitionLosses losses)
        {
            if (losses == null)
                return null;
            var element = new XElement(EL.losses);
            foreach (var loss in losses.Losses)
            {
                var lossElement = new XElement(EL.neutral_loss);
                if (loss.PrecursorMod == null)
                {
                    // Custom neutral losses are not yet implemented to cause this case
                    // TODO: Implement custome neutral losses, and remove this comment.
                    loss.Loss.WriteXml(lossElement);
                }
                else
                {
                    lossElement.SetAttribute(ATTR.modification_name, loss.PrecursorMod.Name);
                    int indexLoss = loss.LossIndex;
                    if (indexLoss != 0)
                        lossElement.SetAttribute(ATTR.loss_index, indexLoss);
                }
                element.Add(lossElement);
            }
            return element;
        }

        private IEnumerable<XElement> CreateLegacyLinkedIonElements(ImmutableList<ModificationSite> sitePath,
            IDictionary<ImmutableList<ModificationSite>, IonOrdinal> linkedIons)
        {
            foreach (var entry in linkedIons)
            {
                if (entry.Key.Count != sitePath.Count + 1)
                {
                    continue;
                }

                if (!sitePath.SequenceEqual(entry.Key.Take(sitePath.Count)))
                {
                    continue;
                }

                var element = new XElement(EL.linked_fragment_ion);
                var ionOrdinal = entry.Value;
                if (!ionOrdinal.IsEmpty)
                {
                    // blank fragment type means orphaned fragment ion
                    element.SetAttribute(ATTR.fragment_type, ionOrdinal.Type);
                }

                element.SetAttribute(ATTR.fragment_ordinal, ionOrdinal.Ordinal, 0);
                element.SetAttribute(ATTR.index_aa, entry.Key.Last().IndexAa);
                element.SetAttribute(ATTR.modification_name, entry.Key.Last().ModName);
                element.Add(CreateLegacyLinkedIonElements(entry.Key, linkedIons));
                yield return element;
            }
        }

        private IEnumerable<XElement> CreateLinkedIonElements(NeutralFragmentIon complexFragmentIon)
        {
            foreach (var part in complexFragmentIon.IonChain.Skip(1))
            {
                var element = new XElement(EL.linked_fragment_ion);
                element.SetAttributeNullable(ATTR.fragment_type, part.Type);
                element.SetAttribute(ATTR.fragment_ordinal, part.Ordinal, 0);
                yield return element;
            }
        }

        #endregion

        #region Modifications

        private string GetModifiedSequence(Target target)
        {
            if (DocumentFormat >= DocumentFormat.VERSION_3_73 || !target.IsProteomic)
            {
                return target.Sequence;
            }
            return new PeptideLibraryKey(target.Sequence, 0).FormatToOneDecimal().ModifiedSequence;
        }

        private XElement CreateLookupModsElement()
        {
            var node = PeptideDocNode;
            if (node.SourceKey == null || node.SourceKey.ExplicitMods == null)
                return null;
            var element = new XElement(EL.lookup_modifications);
            element.Add(CreateExplicitModsElements(node.SourceKey.Sequence, node.SourceKey.ExplicitMods));
            return element;
        }

        /// <summary>
        /// The modification elements of one sequence: an &lt;explicit_modifications&gt; element,
        /// a &lt;variable_modifications&gt; one, or both.
        /// </summary>
        private IEnumerable<XElement> CreateExplicitModsElements(string sequence, ExplicitMods mods)
        {
            if (mods == null ||
                string.IsNullOrEmpty(sequence) && !mods.HasIsotopeLabels)
                yield break;
            if (mods.IsVariableStaticMods)
            {
                yield return CreateModsElement(EL.variable_modifications,
                    EL.variable_modification, null, mods.StaticModifications, sequence);

                // If no heavy modifications, then don't write an <explicit_modifications> tag
                if (!mods.HasHeavyModifications)
                    yield break;
            }
            var element = new XElement(EL.explicit_modifications);
            if (!mods.IsVariableStaticMods)
            {
                element.Add(CreateModsElement(EL.explicit_static_modifications,
                    EL.explicit_modification, null, mods.StaticModifications, sequence));
            }
            foreach (var heavyMods in mods.GetHeavyModifications())
            {
                IsotopeLabelType labelType = heavyMods.LabelType;
                if (Equals(labelType, IsotopeLabelType.heavy))
                    labelType = null;

                element.Add(CreateModsElement(EL.explicit_heavy_modifications,
                    EL.explicit_modification, labelType, heavyMods.Modifications, sequence));
            }
            yield return element;
        }

        private XElement CreateImplicitModsElement()
        {
            var node = PeptideDocNode;

            // Get the implicit  modifications on this peptide.
            var implicitMods = new ExplicitMods(node,
                Settings.PeptideSettings.Modifications.StaticModifications,
                Properties.Settings.Default.StaticModList,
                Settings.PeptideSettings.Modifications.GetHeavyModifications(),
                Properties.Settings.Default.HeavyModList,
                true);

            bool hasStaticMods = implicitMods.StaticModifications.Count != 0 && node.CanHaveImplicitStaticMods;
            bool hasHeavyMods = implicitMods.HasHeavyModifications &&
                                Settings.PeptideSettings.Modifications.GetHeavyModifications().Any(
                                     mod => node.CanHaveImplicitHeavyMods(mod.LabelType));

            if (!hasStaticMods && !hasHeavyMods)
            {
                return null;
            }

            var element = new XElement(EL.implicit_modifications);

            // implicit static modifications.
            if (hasStaticMods)
            {
                element.Add(CreateModsElement(EL.implicit_static_modifications,
                        EL.implicit_modification, null, implicitMods.StaticModifications,
                        node.Peptide.Target.Sequence));
            }

            // implicit heavy modifications
            foreach (var heavyMods in implicitMods.GetHeavyModifications())
            {
                IsotopeLabelType labelType = heavyMods.LabelType;
                if (!node.CanHaveImplicitHeavyMods(labelType))
                {
                    continue;
                }
                if (Equals(labelType, IsotopeLabelType.heavy))
                    labelType = null;

                element.Add(CreateModsElement(EL.implicit_heavy_modifications,
                                  EL.implicit_modification, labelType, heavyMods.Modifications,
                                  node.Peptide.Target.Sequence));
            }
            return element;
        }

        private XElement CreateModsElement(string name,
            string nameElMod, IsotopeLabelType labelType, IEnumerable<ExplicitMod> mods,
            string sequence)
        {
            if (mods == null || (labelType == null && string.IsNullOrEmpty(sequence)))
                return null;
            var element = new XElement(name);
            if (labelType != null)
                element.SetAttribute(ATTR.isotope_label, labelType);

            if (!string.IsNullOrEmpty(sequence))
            {
                SequenceMassCalc massCalc = Settings.TransitionSettings.Prediction.PrecursorMassType == MassType.Monoisotopic ?
                    SrmSettings.MonoisotopicMassCalc : SrmSettings.AverageMassCalc;
                foreach (ExplicitMod mod in mods)
                {
                    var modElement = new XElement(nameElMod);
                    modElement.SetAttribute(ATTR.index_aa, mod.IndexAA);
                    modElement.SetAttribute(ATTR.modification_name, mod.Modification.Name);

                    double massDiff = massCalc.GetModMass(sequence[mod.IndexAA], mod.Modification);

                    modElement.SetAttribute(ATTR.mass_diff,
                        string.Format(CultureInfo.InvariantCulture, @"{0}{1}", (massDiff < 0 ? string.Empty : @"+"),
                            Math.Round(massDiff, 1)));
                    if (null != mod.LinkedPeptide)
                    {
                        modElement.Add(CreateLinkedPeptideElement(mod.LinkedPeptide));
                    }
                    element.Add(modElement);
                }
            }
            return element;
        }

        private XElement CreateLinkedPeptideElement(LegacyLinkedPeptide linkedPeptide)
        {
            var element = new XElement(EL.linked_peptide);
            element.SetAttribute(ATTR.index_aa, linkedPeptide.IndexAa);
            if (linkedPeptide.Peptide != null)
            {
                element.SetAttributeIfString(ATTR.sequence, linkedPeptide.Peptide.Sequence);
                if (null != linkedPeptide.ExplicitMods)
                {
                    element.Add(CreateExplicitModsElements(linkedPeptide.Peptide.Sequence, linkedPeptide.ExplicitMods));
                }
            }
            return element;
        }

        private XElement CreateCrosslinkStructureElement(CrosslinkStructure crosslinkStructure)
        {
            if (crosslinkStructure == null || crosslinkStructure.IsEmpty)
            {
                return null;
            }
            var element = new XElement(EL.crosslinks);
            for (int i = 0; i < crosslinkStructure.LinkedPeptides.Count; i++)
            {
                var peptide = crosslinkStructure.LinkedPeptides[i];
                var peptideElement = new XElement(EL.linked_peptide);
                peptideElement.SetAttributeIfString(ATTR.sequence, peptide.Sequence);
                var explicitMods = crosslinkStructure.LinkedExplicitMods[i];
                if (null != explicitMods)
                {
                    peptideElement.Add(CreateExplicitModsElements(peptide.Sequence, explicitMods));
                }
                element.Add(peptideElement);
            }

            foreach (var crosslink in crosslinkStructure.Crosslinks)
            {
                var crosslinkElement = new XElement(EL.crosslink);
                crosslinkElement.SetAttribute(ATTR.modification_name, crosslink.Crosslinker.Name);
                foreach (var site in crosslink.Sites)
                {
                    var siteElement = new XElement(EL.site);
                    siteElement.SetAttribute(ATTR.peptide_index, site.PeptideIndex);
                    siteElement.SetAttribute(ATTR.index_aa, site.AaIndex);
                    crosslinkElement.Add(siteElement);
                }
                element.Add(crosslinkElement);
            }
            return element;
        }

        #endregion

        /// <summary>
        /// The annotations of anything which has them, as child elements. Nothing here has to be
        /// added last: an <see cref="XElement"/> takes attributes after it has children.
        /// </summary>
        private static void AddAnnotations(XElement element, Annotations annotations)
        {
            if (annotations.IsEmpty)
                return;

            if (annotations.Note != null || annotations.ColorIndex > 0)
            {
                var noteElement = new XElement(EL.note);
                if (annotations.ColorIndex != 0)
                    noteElement.SetAttribute(ATTR.category, annotations.ColorIndex);
                if (annotations.Note != null)
                    noteElement.Add(annotations.Note);
                element.Add(noteElement);
            }
            foreach (var entry in annotations.ListAnnotations())
            {
                var annotationElement = new XElement(EL.annotation);
                annotationElement.SetAttribute(ATTR.name, entry.Key);
                annotationElement.Add(entry.Value);
                element.Add(annotationElement);
            }
        }

        /// <summary>
        /// Adds the child elements something which only knows how to write itself to an
        /// <see cref="XmlWriter"/> produces. Only for things which write whole elements: an
        /// <see cref="XmlWriter"/> made this way has no element of its own to put attributes on.
        /// </summary>
        private static void AddXmlWriterContent(XElement element, Action<XmlWriter> writeContent)
        {
            using (var writer = element.CreateWriter())
            {
                writeContent(writer);
            }
        }
    }
}
