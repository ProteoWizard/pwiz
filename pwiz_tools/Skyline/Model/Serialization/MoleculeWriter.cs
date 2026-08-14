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
    /// Writes the &lt;peptide&gt; or &lt;molecule&gt; element of one
    /// <see cref="PeptideDocNode"/>, and everything below it.
    /// <para>
    /// One of these is made per molecule, which is what lets the things that are the same for all
    /// of a molecule - the node itself, its <see cref="MoleculeResults"/>, its retention time
    /// score - be fields rather than parameters handed down through every level. It is the same
    /// lifetime a <see cref="MoleculeResults"/> has, and this owns the one it uses.
    /// </para>
    /// </summary>
    public class MoleculeWriter : DocumentSerializer
    {
        private readonly DocumentWriter _documentWriter;
        private readonly XmlWriter _writer;

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

        /// <summary>
        /// The retention time calculator score of this molecule, worked out while its attributes
        /// are written and read again when its peptide results are.
        /// </summary>
        private double? _scoreCalc;

        /// <summary>
        /// The files whose transition areas the precursor being written carries. Set while its
        /// results are written and read while its transitions are.
        /// </summary>
        private HashSet<ReferenceValue<ChromFileInfoId>> _sharedTransitionAreaFiles;

        public MoleculeWriter(DocumentWriter documentWriter, XmlWriter writer, PeptideDocNode peptideDocNode)
        {
            _documentWriter = documentWriter;
            _writer = writer;
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
        /// Serializes the contents of this <see cref="PeptideDocNode"/> to XML.
        /// </summary>
        public void WriteXml()
        {
            var node = PeptideDocNode;
            var peptide = node.Peptide;
            var isCustomIon = peptide.IsCustomMolecule;

            _writer.WriteStartElement(isCustomIon ? EL.molecule : EL.peptide);
            if (node.ExplicitRetentionTime != null)
            {
                _writer.WriteAttribute(ATTR.explicit_retention_time, node.ExplicitRetentionTime.RetentionTime);
                _writer.WriteAttributeNullable(ATTR.explicit_retention_time_window, node.ExplicitRetentionTime.RetentionTimeWindow);
            }

            _writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            if (node.GlobalStandardType != null)
                _writer.WriteAttribute(ATTR.standard_type, node.GlobalStandardType.Name);

            _writer.WriteAttributeNullable(ATTR.rank, node.Rank);
            _writer.WriteAttributeNullable(ATTR.concentration_multiplier, node.ConcentrationMultiplier);
            _writer.WriteAttributeNullable(ATTR.internal_standard_concentration, node.InternalStandardConcentration);
            if (null != node.NormalizationMethod)
            {
                _writer.WriteAttribute(ATTR.normalization_method, node.NormalizationMethod.Name);
            }
            _writer.WriteAttributeIfString(ATTR.attribute_group_id, node.AttributeGroupId);
            _writer.WriteAttributeIfString(ATTR.surrogate_calibration_curve, node.SurrogateCalibrationCurve);

            if (isCustomIon)
            {
                peptide.CustomMolecule.WriteXml(_writer, Adduct.EMPTY);
                // If user changed any molecule details (other than formula or mass) after chromatogram extraction, this info continues the target->chromatogram association
                _writer.WriteAttributeIfString(ATTR.chromatogram_target, node.OriginalMoleculeTarget?.ToSerializableString());
            }
            else
            {
                string sequence = peptide.Target.Sequence;
                _writer.WriteAttributeString(ATTR.sequence, sequence);
                var modSeq = Settings.GetModifiedSequence(node);
                _writer.WriteAttributeString(ATTR.modified_sequence, GetModifiedSequence(modSeq));
                if (node.SourceKey != null)
                    _writer.WriteAttributeString(ATTR.lookup_sequence, node.SourceKey.ModifiedSequence);
                if (peptide.Begin.HasValue && peptide.End.HasValue)
                {
                    _writer.WriteAttribute(ATTR.start, peptide.Begin.Value);
                    _writer.WriteAttribute(ATTR.end, peptide.End.Value);
                    _writer.WriteAttribute(ATTR.prev_aa, peptide.PrevAA);
                    _writer.WriteAttribute(ATTR.next_aa, peptide.NextAA);
                }
                var massH = Settings.GetPrecursorCalc(IsotopeLabelType.light, node.ExplicitMods).GetPrecursorMass(peptide.Target);
                _writer.WriteAttribute(ATTR.calc_neutral_pep_mass,
                    SequenceMassCalc.PersistentNeutral(massH));

                _writer.WriteAttribute(ATTR.num_missed_cleavages, peptide.MissedCleavages);
                _writer.WriteAttribute(ATTR.decoy, node.IsDecoy);
                var rtPredictor = Settings.PeptideSettings.Prediction.RetentionTime;
                if (rtPredictor != null)
                {
                    _scoreCalc = rtPredictor.Calculator.ScoreSequence(modSeq);
                    if (_scoreCalc.HasValue)
                    {
                        _writer.WriteAttributeNullable(ATTR.rt_calculator_score, _scoreCalc);
                        _writer.WriteAttributeNullable(ATTR.predicted_retention_time,
                            rtPredictor.GetRetentionTime(_scoreCalc.Value));
                    }
                }
            }

            _writer.WriteAttributeNullable(ATTR.avg_measured_retention_time, node.AverageMeasuredRetentionTime);

            // Write child elements
            DocumentWriter.WriteAnnotations(_writer, node.Annotations);
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
                WriteExplicitMods(node.Peptide.Target.Sequence, explicitMods);
                WriteImplicitMods();
                WriteLookupMods();
                WriteCrosslinkStructure(explicitMods?.CrosslinkStructure);
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
                WriteResults(peptideChromInfos, EL.peptide_results, EL.peptide_result, WritePeptideChromInfo);
            }

            foreach (TransitionGroupDocNode nodeGroup in node.Children)
            {
                _writer.WriteStartElement(EL.precursor);
                WriteTransitionGroupXml(nodeGroup);
                _writer.WriteEndElement();
            }
            _writer.WriteEndElement();
        }

        private string GetModifiedSequence(Target target)
        {
            if (DocumentFormat >= DocumentFormat.VERSION_3_73 || !target.IsProteomic)
            {
                return target.Sequence;
            }
            return new PeptideLibraryKey(target.Sequence, 0).FormatToOneDecimal().ModifiedSequence;
        }

        private void WriteLookupMods()
        {
            var node = PeptideDocNode;
            if (node.SourceKey == null || node.SourceKey.ExplicitMods == null)
                return;
            _writer.WriteStartElement(EL.lookup_modifications);
            WriteExplicitMods(node.SourceKey.Sequence, node.SourceKey.ExplicitMods);
            _writer.WriteEndElement();
        }

        private void WriteExplicitMods(string sequence, ExplicitMods mods)
        {
            if (mods == null ||
                string.IsNullOrEmpty(sequence) && !mods.HasIsotopeLabels)
                return;
            if (mods.IsVariableStaticMods)
            {
                WriteExplicitMods(EL.variable_modifications,
                    EL.variable_modification, null, mods.StaticModifications, sequence);

                // If no heavy modifications, then don't write an <explicit_modifications> tag
                if (!mods.HasHeavyModifications)
                    return;
            }
            _writer.WriteStartElement(EL.explicit_modifications);
            if (!mods.IsVariableStaticMods)
            {
                WriteExplicitMods(EL.explicit_static_modifications,
                    EL.explicit_modification, null, mods.StaticModifications, sequence);
            }
            foreach (var heavyMods in mods.GetHeavyModifications())
            {
                IsotopeLabelType labelType = heavyMods.LabelType;
                if (Equals(labelType, IsotopeLabelType.heavy))
                    labelType = null;

                WriteExplicitMods(EL.explicit_heavy_modifications,
                    EL.explicit_modification, labelType, heavyMods.Modifications, sequence);
            }
            _writer.WriteEndElement();
        }

        private void WriteImplicitMods()
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
                return;
            }

            _writer.WriteStartElement(EL.implicit_modifications);

            // implicit static modifications.
            if (hasStaticMods)
            {
                WriteExplicitMods(EL.implicit_static_modifications,
                        EL.implicit_modification, null, implicitMods.StaticModifications,
                        node.Peptide.Target.Sequence);
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

                WriteExplicitMods(EL.implicit_heavy_modifications,
                                  EL.implicit_modification, labelType, heavyMods.Modifications,
                                  node.Peptide.Target.Sequence);
            }
            _writer.WriteEndElement();
        }


        private void WriteExplicitMods(string name,
            string nameElMod, IsotopeLabelType labelType, IEnumerable<ExplicitMod> mods,
            string sequence)
        {
            if (mods == null || (labelType == null && string.IsNullOrEmpty(sequence)))
                return;
            _writer.WriteStartElement(name);
            if (labelType != null)
                _writer.WriteAttribute(ATTR.isotope_label, labelType);

            if (!string.IsNullOrEmpty(sequence))
            {
                SequenceMassCalc massCalc = Settings.TransitionSettings.Prediction.PrecursorMassType == MassType.Monoisotopic ?
                    SrmSettings.MonoisotopicMassCalc : SrmSettings.AverageMassCalc;
                foreach (ExplicitMod mod in mods)
                {
                    _writer.WriteStartElement(nameElMod);
                    _writer.WriteAttribute(ATTR.index_aa, mod.IndexAA);
                    _writer.WriteAttribute(ATTR.modification_name, mod.Modification.Name);

                    double massDiff = massCalc.GetModMass(sequence[mod.IndexAA], mod.Modification);

                    _writer.WriteAttribute(ATTR.mass_diff,
                        string.Format(CultureInfo.InvariantCulture, @"{0}{1}", (massDiff < 0 ? string.Empty : @"+"),
                            Math.Round(massDiff, 1)));
                    if (null != mod.LinkedPeptide)
                    {
                        WriteLinkedPeptide(mod.LinkedPeptide);
                    }
                    _writer.WriteEndElement();
                }
            }
            _writer.WriteEndElement();
        }

        private void WriteLinkedPeptide(LegacyLinkedPeptide linkedPeptide)
        {
            _writer.WriteStartElement(EL.linked_peptide);
            _writer.WriteAttribute(ATTR.index_aa, linkedPeptide.IndexAa);
            if (linkedPeptide.Peptide != null)
            {
                _writer.WriteAttributeIfString(ATTR.sequence, linkedPeptide.Peptide.Sequence);
                if (null != linkedPeptide.ExplicitMods)
                {
                    WriteExplicitMods(linkedPeptide.Peptide.Sequence, linkedPeptide.ExplicitMods);
                }
            }
            _writer.WriteEndElement();
        }

        private void WriteCrosslinkStructure(CrosslinkStructure crosslinkStructure)
        {
            if (crosslinkStructure == null || crosslinkStructure.IsEmpty)
            {
                return;
            }
            _writer.WriteStartElement(EL.crosslinks);
            for (int i = 0; i < crosslinkStructure.LinkedPeptides.Count; i++)
            {
                var peptide = crosslinkStructure.LinkedPeptides[i];
                _writer.WriteStartElement(EL.linked_peptide);
                _writer.WriteAttributeIfString(ATTR.sequence, peptide.Sequence);
                var explicitMods = crosslinkStructure.LinkedExplicitMods[i];
                if (null != explicitMods)
                {
                    WriteExplicitMods(peptide.Sequence, explicitMods);
                }
                _writer.WriteEndElement();
            }

            foreach (var crosslink in crosslinkStructure.Crosslinks)
            {
                _writer.WriteStartElement(EL.crosslink);
                _writer.WriteAttribute(ATTR.modification_name, crosslink.Crosslinker.Name);
                foreach (var site in crosslink.Sites)
                {
                    _writer.WriteStartElement(EL.site);
                    _writer.WriteAttribute(ATTR.peptide_index, site.PeptideIndex);
                    _writer.WriteAttribute(ATTR.index_aa, site.AaIndex);
                    _writer.WriteEndElement();
                }
                _writer.WriteEndElement();
            }
            _writer.WriteEndElement();
        }

        private void WritePeptideChromInfo(PeptideChromInfo chromInfo)
        {
            _writer.WriteAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            _writer.WriteAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            _writer.WriteAttribute(ATTR.exclude_from_calibration, chromInfo.ExcludeFromCalibration);
            _writer.WriteAttributeNullable(ATTR.analyte_concentration, chromInfo.AnalyteConcentration);
            if (_scoreCalc.HasValue)
            {
                double? rt = Settings.PeptideSettings.Prediction.RetentionTime.GetRetentionTime(_scoreCalc.Value,
                                                                                      chromInfo.FileId);
                _writer.WriteAttributeNullable(ATTR.predicted_retention_time, rt);
            }
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="TransitionGroupDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="node">The transition group document node</param>
        private void WriteTransitionGroupXml(TransitionGroupDocNode node)
        {
            var nodePep = PeptideDocNode;
            TransitionGroup group = node.TransitionGroup;
            var isCustomIon = nodePep.Peptide.IsCustomMolecule;
            _writer.WriteAttribute(ATTR.charge, group.PrecursorAdduct.AdductCharge);
            if (!group.LabelType.IsLight)
                _writer.WriteAttribute(ATTR.isotope_label, group.LabelType);
            if (!isCustomIon)
            {
                _writer.WriteAttribute(ATTR.calc_neutral_mass, node.GetPrecursorIonPersistentNeutralMass());
            }
            _writer.WriteAttribute(ATTR.precursor_mz, SequenceMassCalc.PersistentMZ(node.PrecursorMz));
            WriteExplicitTransitionGroupValuesAttributes(node.ExplicitValues);

            _writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            _writer.WriteAttributeNullable(ATTR.decoy_mass_shift, group.DecoyMassShift);
            _writer.WriteAttributeNullable(ATTR.precursor_concentration, node.PrecursorConcentration);


            TransitionPrediction predict = Settings.TransitionSettings.Prediction;
            double regressionMz = Settings.GetRegressionMz(nodePep, node);
            var ce = predict.CollisionEnergy.GetCollisionEnergy(node.TransitionGroup.PrecursorAdduct, regressionMz);
            _writer.WriteAttribute(ATTR.collision_energy, ce);

            var dpRegression = predict.DeclusteringPotential;
            if (dpRegression != null)
            {
                var dp = dpRegression.GetDeclustringPotential(regressionMz);
                _writer.WriteAttribute(ATTR.declustering_potential, dp);
            }

            if (!isCustomIon)
            {
                // modified sequence
                if (nodePep.ExplicitMods != null && nodePep.ExplicitMods.HasCrosslinks)
                {
                    _writer.WriteAttribute(ATTR.modified_sequence,
                        Settings.GetCrosslinkModifiedSequence(nodePep.Target, node.TransitionGroup.LabelType, nodePep.ExplicitMods));
                }
                else
                {
                    var calcPre = Settings.GetPrecursorCalc(node.TransitionGroup.LabelType, nodePep.ExplicitMods);
                    var seq = node.TransitionGroup.Peptide.Target;
                    _writer.WriteAttribute(ATTR.modified_sequence, calcPre.GetModifiedSequence(seq,
                        false)); // formatNarrow = false; We want InvariantCulture, not the local format
                }
                Assume.IsTrue(group.PrecursorAdduct.IsProteomic, @"expected IsProteomic tag on adduct");
            }
            else
            {
                // Custom ion
                node.CustomMolecule.WriteXml(_writer, group.PrecursorAdduct);
            }
            // Write child elements
            DocumentWriter.WriteAnnotations(_writer, node.Annotations);
            node.SpectrumClassFilter.WriteXml(_writer);
            if (node.HasLibInfo)
            {
                var helpers = PeptideLibraries.SpectrumHeaderXmlHelpers;
                var libInfo = node.LibInfo;
                if (libInfo is EncyclopeDiaLibrary.ElibSpectrumHeaderInfo && DocumentFormat < DocumentFormat.VERSION_22_25)
                {
                    // Older versions of Skyline used ChromLibSpectrumHeaderInfo instead of ElibSpectrumHeaderInfo
                    libInfo = new ChromLibSpectrumHeaderInfo(libInfo.LibraryName, 0, null);
                }
                _writer.WriteElements(new[] { libInfo }, helpers);
            }

            // The columnar results whenever the chrom infos could not be rebuilt, which is
            // every molecule when only they are being written and any molecule whose
            // chromatograms could not be read. See WriteXml.
            if (_moleculeResults == null)
            {
                WriteTransitionGroupResults(node);
            }
            else
            {
                var groupChromInfos = _moleculeResults.GetTransitionGroupChromInfos(group);
                if (groupChromInfos != null)
                {
                    WriteResults(groupChromInfos, EL.precursor_results, EL.precursor_peak, WriteTransitionGroupChromInfo);
                }
            }

            if (UseCompactFormat())
            {
                _writer.WriteStartElement(EL.transition_data);
                var transitionData = new SkylineDocumentProto.Types.TransitionData();
                // The peaks come from the chromatograms, since a transition does not keep them.
                transitionData.Transitions.AddRange(node.Transitions.Select(transition =>
                    transition.ToTransitionProto(Settings, nodePep, node,
                        _moleculeResults?.GetTransitionChromInfos(group, transition.Transition))));
                byte[] bytes = transitionData.ToByteArray();
                _writer.WriteBase64(bytes, 0, bytes.Length);
                _writer.WriteEndElement();
                _documentWriter.OnWroteTransitions(node.TransitionCount);
            }
            else
            {
                foreach (TransitionDocNode nodeTransition in node.Children)
                {
                    _writer.WriteStartElement(EL.transition);
                    WriteTransitionXml(node, nodeTransition);
                    _writer.WriteEndElement();
                }
            }
        }

        /// <summary>
        /// Serializes any optionally explicitly specified CE, RT and DT information to attributes only
        /// </summary>
        private void WriteExplicitTransitionGroupValuesAttributes(ExplicitTransitionGroupValues importedAttributes)
        {
            if (DocumentFormat < DocumentFormat.VERSION_4_22 || DocumentFormat >= DocumentFormat.VERSION_20_12) // Format supports per-precursor explicit CE?
                _writer.WriteAttributeNullable(ATTR.explicit_collision_energy, importedAttributes.CollisionEnergy);
            _writer.WriteAttributeNullable(ATTR.explicit_ion_mobility, importedAttributes.IonMobility);
            if (importedAttributes.IonMobility.HasValue)
                _writer.WriteAttribute(ATTR.explicit_ion_mobility_units, importedAttributes.IonMobilityUnits.ToString());
            _writer.WriteAttributeNullable(ATTR.explicit_ccs_sqa, importedAttributes.CollisionalCrossSectionSqA);
        }

        private void WriteTransitionGroupChromInfo(TransitionGroupChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                _writer.WriteAttribute(ATTR.step, chromInfo.OptimizationStep);
            _writer.WriteAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            _writer.WriteAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            _writer.WriteAttributeNullable(ATTR.start_time, chromInfo.StartRetentionTime);
            _writer.WriteAttributeNullable(ATTR.end_time, chromInfo.EndRetentionTime);
            _writer.WriteAttributeNullable(ATTR.ccs, chromInfo.IonMobilityInfo.CollisionalCrossSection);
            if (chromInfo.IonMobilityInfo.IonMobilityUnits != eIonMobilityUnits.none)
            {
                _writer.WriteAttributeNullable(ATTR.ion_mobility_ms1, chromInfo.IonMobilityInfo.IonMobilityMS1);
                _writer.WriteAttributeNullable(ATTR.ion_mobility_fragment, chromInfo.IonMobilityInfo.IonMobilityFragment);
                _writer.WriteAttributeNullable(ATTR.ion_mobility_window, chromInfo.IonMobilityInfo.IonMobilityWindow);
                _writer.WriteAttribute(ATTR.ion_mobility_type, chromInfo.IonMobilityInfo.IonMobilityUnits.ToString());
            }
            _writer.WriteAttributeNullable(ATTR.fwhm, chromInfo.Fwhm);
            _writer.WriteAttributeNullable(ATTR.area, chromInfo.Area);
            _writer.WriteAttributeNullable(ATTR.background, chromInfo.BackgroundArea);
            _writer.WriteAttributeNullable(ATTR.height, chromInfo.Height);
            _writer.WriteAttributeNullable(ATTR.mass_error_ppm, chromInfo.MassError);
            _writer.WriteAttributeNullable(ATTR.truncated, chromInfo.Truncated);
            _writer.WriteAttribute(ATTR.identified, chromInfo.Identified.ToString().ToLowerInvariant());
            _writer.WriteAttributeNullable(ATTR.library_dotp, chromInfo.LibraryDotProduct);
            _writer.WriteAttributeNullable(ATTR.isotope_dotp, chromInfo.IsotopeDotProduct);
            _writer.WriteAttributeNullable(ATTR.qvalue, chromInfo.QValue);
            _writer.WriteAttributeNullable(ATTR.zscore, chromInfo.ZScore);
            _writer.WriteAttribute(ATTR.user_set, chromInfo.UserSet);
            var originalPeak = chromInfo.OriginalPeak;
            if (originalPeak != null && originalPeak.StartTime.Equals(chromInfo.StartRetentionTime) && originalPeak.EndTime.Equals(chromInfo.EndRetentionTime))
            {
                _writer.WriteAttribute(ATTR.original_score, originalPeak.Score);
                originalPeak = null;
            }
            DocumentWriter.WriteAnnotations(_writer, chromInfo.Annotations);
            WriteScoredPeak(EL.original_peak, originalPeak);
            WriteScoredPeak(EL.reintegrated_peak, chromInfo.ReintegratedPeak);
        }

        private void WriteScoredPeak(string el, ScoredPeakBounds scoredPeak)
        {
            if (scoredPeak == null || DocumentFormat < DocumentFormat.PEAK_IMPUTATION)
            {
                return;
            }
            _writer.WriteStartElement(el);
            _writer.WriteAttribute(ATTR.score, scoredPeak.Score);
            _writer.WriteAttribute(ATTR.retention_time, scoredPeak.ApexTime);
            _writer.WriteAttribute(ATTR.start_time, scoredPeak.StartTime);
            _writer.WriteAttribute(ATTR.end_time, scoredPeak.EndTime);
            _writer.WriteEndElement();
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="TransitionDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="nodeGroup">The transition node's parent group node</param>
        /// <param name="nodeTransition">The transition document node</param>
        private void WriteTransitionXml(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTransition)
        {
            var nodePep = PeptideDocNode;
            Transition transition = nodeTransition.Transition;
            _writer.WriteAttribute(ATTR.fragment_type, transition.IonType);
            _writer.WriteAttribute(ATTR.quantitative, nodeTransition.ExplicitQuantitative, true);
            WriteExplicitTransitionValuesAttributes(nodeTransition.ExplicitValues);
            if (transition.IsCustom())
            {
                if (!(transition.CustomIon is SettingsCustomIon))
                {
                    transition.CustomIon.WriteXml(_writer, transition.Adduct);
                }
                else
                {
                    _writer.WriteAttributeString(ATTR.measured_ion_name, transition.CustomIon.Name);
                }
            }
            _writer.WriteAttributeNullable(ATTR.decoy_mass_shift, transition.DecoyMassShift);
            // NOTE: MassIndex is the peak index in the isotopic distribution of the precursor.
            //       0 for monoisotopic peaks and for non "precursor" ion types.
            if (transition.MassIndex != 0)
                _writer.WriteAttribute(ATTR.mass_index, transition.MassIndex);
            if (nodeTransition.HasDistInfo)
            {
                _writer.WriteAttribute(ATTR.isotope_dist_rank, nodeTransition.IsotopeDistInfo.Rank);
                _writer.WriteAttribute(ATTR.isotope_dist_proportion, nodeTransition.IsotopeDistInfo.Proportion);
            }

            if (transition.IsPrecursor())
            {
                _writer.WriteAttribute(ATTR.product_charge, transition.Charge, nodeGroup.PrecursorCharge);
            }
            else
            {
                if (!transition.IsCustom())
                {
                    _writer.WriteAttribute(ATTR.fragment_ordinal, transition.Ordinal);
                    _writer.WriteAttribute(ATTR.calc_neutral_mass, nodeTransition.GetMoleculePersistentNeutralMass());
                }
                _writer.WriteAttribute(ATTR.product_charge, transition.Charge);
                if (!transition.IsCustom())
                {
                    _writer.WriteAttribute(ATTR.cleavage_aa, transition.AA.ToString(CultureInfo.InvariantCulture));
                    if (nodeTransition.HasLoss)
                        _writer.WriteAttribute(ATTR.loss_neutral_mass, nodeTransition.LostMass); //po
                }
            }

            if (nodeTransition.ComplexFragmentIon.IsOrphan)
            {
                _writer.WriteAttribute(ATTR.orphaned_crosslink_ion, true);
            }

            // Order of elements matters for XSD validation
            DocumentWriter.WriteAnnotations(_writer, nodeTransition.Annotations);
            _writer.WriteElementString(EL.precursor_mz, SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz));
            _writer.WriteElementString(EL.product_mz, SequenceMassCalc.PersistentMZ(nodeTransition.Mz));


            double? ce = nodeTransition.GetCollisionEnergy(Settings, nodePep, nodeGroup);
            double? dp = nodeTransition.GetDeclusteringPotential(Settings, nodePep, nodeGroup);

            if (ce.HasValue)
            {
                _writer.WriteElementString(EL.collision_energy, ce.Value);
            }

            if (dp.HasValue)
            {
                _writer.WriteElementString(EL.declustering_potential, dp.Value);
            }
            WriteTransitionLosses(nodeTransition.Losses);
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
                    WriteLegacyLinkedIons(ImmutableList<ModificationSite>.EMPTY, linkedIons);
                }
                else
                {
                    WriteLinkedIons(nodeTransition.ComplexFragmentIon.NeutralFragmentIon);
                }
            }
            if (nodeTransition.HasLibInfo)
            {
                _writer.WriteStartElement(EL.transition_lib_info);
                _writer.WriteAttribute(ATTR.rank, nodeTransition.LibInfo.Rank);
                _writer.WriteAttribute(ATTR.intensity, nodeTransition.LibInfo.Intensity);
                _writer.WriteEndElement();
            }

            // The columnar results whenever the chrom infos could not be rebuilt. See
            // WriteXml for when that is.
            if (_moleculeResults == null)
            {
                // Left out when the precursor already carries these areas and there is nothing
                // else here to say.
                var groupResults = nodeGroup.AbbreviatedResults;
                if (groupResults != null && groupResults.HasTransitionResults(nodeTransition.Transition) &&
                    !groupResults.IsTransitionCoveredBySharedAreas(nodeTransition.Transition, _sharedTransitionAreaFiles))
                {
                    WriteTransitionResults(groupResults, nodeTransition.Transition);
                }
            }
            else
            {
                // Worked out from the chromatograms, since a transition does not keep them.
                var transitionChromInfos = _moleculeResults.GetTransitionChromInfos(nodeGroup.TransitionGroup,
                    nodeTransition.Transition);
                if (transitionChromInfos != null)
                {
                    if (UseCompactFormat())
                    {
                        var protoResults = new SkylineDocumentProto.Types.TransitionResults();
                        protoResults.Peaks.AddRange(TransitionDocNode.GetTransitionPeakProtos(transitionChromInfos,
                            Settings.MeasuredResults));
                        byte[] bytes = protoResults.ToByteArray();
                        _writer.WriteStartElement(EL.results_data);
                        _writer.WriteBase64(bytes, 0, bytes.Length);
                        _writer.WriteEndElement();
                    }
                    else
                    {
                        WriteResults(transitionChromInfos, EL.transition_results, EL.transition_peak, WriteTransitionChromInfo);
                    }
                }
            }

            _documentWriter.OnWroteTransitions(1);
        }

        private void WriteExplicitTransitionValuesAttributes(ExplicitTransitionValues importedAttributes)
        {
            _writer.WriteAttributeNullable(ATTR.explicit_collision_energy, importedAttributes.CollisionEnergy);
            _writer.WriteAttributeNullable(ATTR.explicit_ion_mobility_high_energy_offset, importedAttributes.IonMobilityHighEnergyOffset);
            _writer.WriteAttributeNullable(ATTR.explicit_s_lens, importedAttributes.SLens);
            _writer.WriteAttributeNullable(ATTR.explicit_cone_voltage, importedAttributes.ConeVoltage);
            _writer.WriteAttributeNullable(ATTR.explicit_declustering_potential, importedAttributes.DeclusteringPotential);
        }

        private void WriteTransitionLosses(TransitionLosses losses)
        {
            if (losses == null)
                return;
            _writer.WriteStartElement(EL.losses);
            foreach (var loss in losses.Losses)
            {
                _writer.WriteStartElement(EL.neutral_loss);
                if (loss.PrecursorMod == null)
                {
                    // Custom neutral losses are not yet implemented to cause this case
                    // TODO: Implement custome neutral losses, and remove this comment.
                    loss.Loss.WriteXml(_writer);
                }
                else
                {
                    _writer.WriteAttribute(ATTR.modification_name, loss.PrecursorMod.Name);
                    int indexLoss = loss.LossIndex;
                    if (indexLoss != 0)
                        _writer.WriteAttribute(ATTR.loss_index, indexLoss);
                }
                _writer.WriteEndElement();
            }
            _writer.WriteEndElement();
        }

        private void WriteLegacyLinkedIons(ImmutableList<ModificationSite> sitePath, IDictionary<ImmutableList<ModificationSite>, IonOrdinal> linkedIons)
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

                _writer.WriteStartElement(EL.linked_fragment_ion);
                var ionOrdinal = entry.Value;
                if (!ionOrdinal.IsEmpty)
                {
                    // blank fragment type means orphaned fragment ion
                    _writer.WriteAttribute(ATTR.fragment_type, ionOrdinal.Type);
                }

                _writer.WriteAttribute(ATTR.fragment_ordinal, ionOrdinal.Ordinal, 0);
                _writer.WriteAttribute(ATTR.index_aa, entry.Key.Last().IndexAa);
                _writer.WriteAttribute(ATTR.modification_name, entry.Key.Last().ModName);
                WriteLegacyLinkedIons(entry.Key, linkedIons);
                _writer.WriteEndElement();
            }
        }

        private void WriteLinkedIons(NeutralFragmentIon complexFragmentIon)
        {
            foreach (var part in complexFragmentIon.IonChain.Skip(1))
            {
                _writer.WriteStartElement(EL.linked_fragment_ion);
                _writer.WriteAttributeNullable(ATTR.fragment_type, part.Type);
                _writer.WriteAttribute(ATTR.fragment_ordinal, part.Ordinal, 0);
                _writer.WriteEndElement();
            }
        }

        private void WriteTransitionChromInfo(TransitionChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                _writer.WriteAttribute(ATTR.step, chromInfo.OptimizationStep);

            // Only write peak information, if it is not empty
            if (!chromInfo.IsEmpty)
            {
                _writer.WriteAttributeNullable(ATTR.mass_error_ppm, chromInfo.MassError);
                _writer.WriteAttribute(ATTR.retention_time, chromInfo.RetentionTime);
                _writer.WriteAttribute(ATTR.start_time, chromInfo.StartRetentionTime);
                _writer.WriteAttribute(ATTR.end_time, chromInfo.EndRetentionTime);
                _writer.WriteAttributeNullable(ATTR.ccs, chromInfo.IonMobility.CollisionalCrossSectionSqA);
                _writer.WriteAttributeNullable(ATTR.ion_mobility, chromInfo.IonMobility.IonMobility.Mobility);
                _writer.WriteAttributeNullable(ATTR.ion_mobility_window, chromInfo.IonMobility.IonMobilityExtractionWindowWidth);
                _writer.WriteAttribute(ATTR.area, chromInfo.Area);
                _writer.WriteAttribute(ATTR.background, chromInfo.BackgroundArea);
                _writer.WriteAttribute(ATTR.height, chromInfo.Height);
                _writer.WriteAttribute(ATTR.fwhm, chromInfo.Fwhm);
                _writer.WriteAttribute(ATTR.fwhm_degenerate, chromInfo.IsFwhmDegenerate);
                _writer.WriteAttributeNullable(ATTR.truncated, chromInfo.IsTruncated);
                _writer.WriteAttribute(ATTR.identified, chromInfo.Identified.ToString().ToLowerInvariant());
                _writer.WriteAttribute(ATTR.rank, chromInfo.Rank);
                var peakShapeValues = chromInfo.PeakShapeValues;
                if (peakShapeValues.HasValue)
                {
                    _writer.WriteAttribute(ATTR.std_dev, peakShapeValues.Value.StdDev);
                    _writer.WriteAttribute(ATTR.skewness, peakShapeValues.Value.Skewness);
                    _writer.WriteAttribute(ATTR.kurtosis, peakShapeValues.Value.Kurtosis);
                    _writer.WriteAttribute(ATTR.shape_correlation, peakShapeValues.Value.ShapeCorrelation);
                }
                if (SkylineVersion.SrmDocumentVersion.CompareTo(DocumentFormat.VERSION_3_61) >= 0)
                {
                    _writer.WriteAttributeNullable(ATTR.points_across, chromInfo.PointsAcrossPeak);
                }
                if (chromInfo.Rank != chromInfo.RankByLevel)
                    _writer.WriteAttribute(ATTR.rank_by_level, chromInfo.RankByLevel);
            }
            _writer.WriteAttribute(ATTR.user_set, chromInfo.UserSet);
            _writer.WriteAttribute(ATTR.forced_integration, chromInfo.IsForcedIntegration, false);
            DocumentWriter.WriteAnnotations(_writer, chromInfo.Annotations);
        }

        /// <summary>
        /// Writes one entry per replicate and file, in that order, which is what
        /// <see cref="ChromFileIds"/> is: the reader rebuilds the flat positions from the order
        /// they come back in.
        /// <para>
        /// The same element names a document has always used. What tells the two apart is what is
        /// on them: this leaves out everything the .skyd gives back, and writes
        /// <see cref="ATTR.chosen_peak_index"/>, which says which candidate peak there to read.
        /// </para>
        /// </summary>
        private void WriteColumnarResults(ChromFileIds chromFileIds, string start, string peakStart,
            Action<int, int> writePeak)
        {
            var replicatePositions = chromFileIds?.ReplicatePositions;
            if (replicatePositions == null || replicatePositions.TotalCount == 0)
            {
                return;
            }

            var chromatograms = Settings.MeasuredResults.Chromatograms;
            _writer.WriteStartElement(start);
            for (int replicateIndex = 0;
                 replicateIndex < Math.Min(replicatePositions.ReplicateCount, chromatograms.Count);
                 replicateIndex++)
            {
                var chromatogramSet = chromatograms[replicateIndex];
                foreach (int position in replicatePositions[replicateIndex])
                {
                    _writer.WriteStartElement(peakStart);
                    _writer.WriteAttribute(ATTR.replicate, chromatogramSet.Name);
                    if (chromatogramSet.FileCount > 1)
                    {
                        _writer.WriteAttribute(ATTR.file,
                            chromatogramSet.GetFileSaveId(chromFileIds.FileIds[position]));
                    }

                    writePeak(replicateIndex, position);
                    _writer.WriteEndElement();
                }
            }

            _writer.WriteEndElement();
        }

        /// <summary>
        /// A transition's peaks, written only when it has something its precursor does not already
        /// say: an area which did not ride on <see cref="ATTR.transition_areas"/>, boundaries of
        /// its own, something integrating between them cannot find again, or an annotation.
        /// <para>
        /// Each value is looked up by replicate and file, because each of them is its own map: they
        /// are held only where there is one, and none of them has an entry wherever another does.
        /// </para>
        /// </summary>
        private void WriteTransitionResults(TransitionGroupResults results, Transition transition)
        {
            var chromFileIds = results.GetTransitionChromFileIds(transition);
            WriteColumnarResults(chromFileIds, EL.transition_results, EL.transition_peak,
                (replicateIndex, position) =>
                {
                    var fileId = chromFileIds.FileIds[position].Value;
                    results.TryGetTransitionPeak(transition, replicateIndex, fileId, out var peak);
                    _writer.WriteAttribute(ATTR.area, peak.Area);
                    _writer.WriteAttribute(ATTR.user_set, peak.UserSet, UserSet.FALSE);
                    // Nothing else carries these, so a transition written out has to say them. They
                    // are the reason a peak which is anything but ordinary cannot ride its
                    // precursor's transition_areas - see TransitionResults.TryGetPlainArea, which
                    // decides that, and SharedTransitionAreas.MakeTransitionResults, which puts
                    // back exactly the values it treats as ordinary.
                    _writer.WriteAttributeNullable(ATTR.truncated, peak.IsTruncated);
                    _writer.WriteAttribute(ATTR.forced_integration, peak.IsForcedIntegration, false);
                    _writer.WriteAttribute(ATTR.empty, peak.IsEmpty, false);

                    var peakBounds = results.FindTransitionCustomPeakBounds(transition, replicateIndex, fileId);
                    if (peakBounds.HasValue)
                    {
                        _writer.WriteAttribute(ATTR.start_time, peakBounds.Value.StartTime);
                        _writer.WriteAttribute(ATTR.end_time, peakBounds.Value.EndTime);
                    }

                    var peakMetrics = results.FindTransitionCustomPeakMetrics(transition, replicateIndex, fileId);
                    if (peakMetrics != null)
                    {
                        _writer.WriteAttributeNullable(ATTR.mass_error_ppm, peakMetrics.MassError);
                        if (peakMetrics.Identified != PeakIdentification.FALSE)
                            _writer.WriteAttribute(ATTR.identified, peakMetrics.Identified.ToString().ToLowerInvariant());
                    }

                    // Last, because these are child elements and an XmlWriter takes no more
                    // attributes once an element has content.
                    DocumentWriter.WriteAnnotations(_writer, results.FindTransitionAnnotations(transition, replicateIndex, fileId));
                });
        }

        private void WriteTransitionGroupResults(TransitionGroupDocNode nodeGroup)
        {
            var results = nodeGroup.AbbreviatedResults;
            var sharedAreas = results?.GetSharedTransitionAreas(nodeGroup.Children.Count);
            _sharedTransitionAreaFiles = GetSharedTransitionAreaFiles(results, sharedAreas);
            WriteColumnarResults(results?.ChromFileIds, EL.precursor_results, EL.precursor_peak,
                (replicateIndex, position) =>
            {
                // No area: a precursor's is the sum of its transitions', which are written below it.
                _writer.WriteAttribute(ATTR.retention_time, results.Peaks.FlatValues[position].RetentionTime);
                // Nullable rather than the generic default-value overload, which formats with
                // ToString() and so loses digits a float needs to come back the same.
                _writer.WriteAttributeNullable(ATTR.start_time, results.GetStartTime(position));
                _writer.WriteAttributeNullable(ATTR.end_time, results.GetEndTime(position));
                // Written, even as -1, by a precursor which knows which candidate peaks its peaks
                // are; left out altogether by one which does not. Its presence is what
                // DocumentReader reads back as TransitionGroupResults.NeedsPeakIndexes, so writing
                // it either way would tell a document being read again that the matching had been
                // done when it had not, and -1 would be taken for "not a candidate peak" rather
                // than "not worked out". A precursor which does not know keeps everything its
                // peaks need instead - see WriteTransitionResults.
                if (!results.NeedsPeakIndexes)
                {
                    _writer.WriteAttribute(ATTR.chosen_peak_index,
                        results.GetChosenPeakIndex(position) ?? PrecursorPeak.NO_PEAK_INDEX);
                }
                _writer.WriteAttributeNullable(ATTR.qvalue, results.GetQValue(position));
                _writer.WriteAttributeNullable(ATTR.zscore, results.GetZScore(position));
                _writer.WriteAttribute(ATTR.user_set, results.GetUserSet(position), UserSet.FALSE);
                var areas = sharedAreas[position];
                if (areas != null)
                {
                    _writer.WriteFloatsAttribute(ATTR.transition_areas, areas);
                }

                // Last, because these are child elements and an XmlWriter takes no more attributes
                // once an element has content.
                DocumentWriter.WriteAnnotations(_writer, results.GetAnnotations(position));
            });
        }

        /// <summary>
        /// The files whose transition areas the precursor carries, so that a transition which has
        /// nothing to say beyond those areas can be left out altogether.
        /// </summary>
        private static HashSet<ReferenceValue<ChromFileInfoId>> GetSharedTransitionAreaFiles(
            TransitionGroupResults results, float[][] sharedAreas)
        {
            if (results == null)
            {
                return null;
            }

            var fileIds = new HashSet<ReferenceValue<ChromFileInfoId>>();
            for (int position = 0; position < sharedAreas.Length; position++)
            {
                if (sharedAreas[position] != null)
                {
                    fileIds.Add(results.ChromFileIds.FileIds[position]);
                }
            }

            return fileIds;
        }

        private void WriteResults<TItem>(IEnumerable<ChromInfoList<TItem>> results, string start, string startChild,
                Action<TItem> writeChromInfo)
            where TItem : ChromInfo
        {
            bool started = false;
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
                    string name = chromatogramSet.Name;
                    foreach (var chromInfo in listChromInfo)
                    {
                        if (!started)
                        {
                            _writer.WriteStartElement(start);
                            started = true;
                        }
                        _writer.WriteStartElement(startChild);
                        _writer.WriteAttribute(ATTR.replicate, name);
                        if (chromatogramSet.FileCount > 1)
                            _writer.WriteAttribute(ATTR.file, chromatogramSet.GetFileSaveId(chromInfo.FileId));
                        writeChromInfo(chromInfo);
                        _writer.WriteEndElement();
                    }
                }
            }
            if (started)
                _writer.WriteEndElement();
        }
    }
}
