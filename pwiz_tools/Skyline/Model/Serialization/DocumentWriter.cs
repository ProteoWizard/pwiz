/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Text;
using System.Xml;
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    public class DocumentWriter : DocumentSerializer
    {
        public DocumentWriter(SrmDocument document, SkylineVersion skylineVersion)
        {
            Settings = document.Settings;
            Document = document;
            SkylineVersion = skylineVersion;
            DocumentFormat = skylineVersion.SrmDocumentVersion;
            CompactFormatOption = CompactFormatOption.FromSettings();
        }

        public SkylineVersion SkylineVersion { get; private set; }
        public SrmDocument Document { get; private set; }
        public CompactFormatOption CompactFormatOption { get; set; }

        public event Action<int> WroteTransitions;

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.format_version, SkylineVersion.SrmDocumentVersion);
            writer.WriteAttribute(ATTR.software_version, SkylineVersion.InvariantVersionName);

            writer.WriteElement(Settings.RemoveUnsupportedFeatures(SkylineVersion.SrmDocumentVersion));
            foreach (PeptideGroupDocNode nodeGroup in Document.Children)
            {
                if (nodeGroup.Id is FastaSequence)
                    writer.WriteStartElement(EL.protein);
                else
                    writer.WriteStartElement(EL.peptide_list);
                WritePeptideGroupXml(writer, nodeGroup);
                writer.WriteEndElement();
            }
        }

        private void WriteProteinMetadataXML(XmlWriter writer, ProteinMetadata proteinMetadata, bool skipNameAndDescription)
        {
            if (!skipNameAndDescription)
            {
                writer.WriteAttributeIfString(ATTR.name, proteinMetadata.Name);
                writer.WriteAttributeIfString(ATTR.description, proteinMetadata.Description);
            }
            writer.WriteAttributeIfString(ATTR.accession, proteinMetadata.Accession);
            writer.WriteAttributeIfString(ATTR.gene, proteinMetadata.Gene);
            writer.WriteAttributeIfString(ATTR.species, proteinMetadata.Species);
            writer.WriteAttributeIfString(ATTR.preferred_name, proteinMetadata.PreferredName);
            writer.WriteAttributeIfString(ATTR.websearch_status, proteinMetadata.WebSearchInfo.ToString());
        }

        private bool UseCompactFormat()
        {
            return DocumentFormat.CompareTo(DocumentFormat.BINARY_RESULTS) >= 0 &&
                   CompactFormatOption.UseCompactFormat(Document);
        }
        /// <summary>
        /// Serializes the contents of a single <see cref="PeptideGroupDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="node">The peptide group document node</param>
        private void WritePeptideGroupXml(XmlWriter writer, PeptideGroupDocNode node)
        {
            // save the identity info
            if (node.PeptideGroup.Name != null)
            {
                writer.WriteAttributeString(ATTR.name, node.PeptideGroup.Name);
            }
            if (node.PeptideGroup.Description != null)
            {
                writer.WriteAttributeString(ATTR.description, node.PeptideGroup.Description);
            }
            // save any overrides
            if ((node.ProteinMetadataOverrides.Name != null) && !Equals(node.ProteinMetadataOverrides.Name, node.PeptideGroup.Name))
            {
                writer.WriteAttributeString(ATTR.label_name, node.ProteinMetadataOverrides.Name);
            }
            if ((node.ProteinMetadataOverrides.Description != null) && !Equals(node.ProteinMetadataOverrides.Description, node.PeptideGroup.Description))
            {
                writer.WriteAttributeString(ATTR.label_description, node.ProteinMetadataOverrides.Description);
            }
            WriteProteinMetadataXML(writer, node.ProteinMetadataOverrides, true); // write the protein metadata, skipping the name and description we already wrote
            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            writer.WriteAttribute(ATTR.decoy, node.IsDecoy);

            // Write child elements
            WriteAnnotations(writer, node.Annotations);

            FastaSequence seq = node.PeptideGroup as FastaSequence;
            if (seq != null)
            {
                if (seq.Alternatives.Count > 0)
                {
                    writer.WriteStartElement(EL.alternatives);
                    foreach (ProteinMetadata alt in seq.Alternatives)
                    {
                        writer.WriteStartElement(EL.alternative_protein);
                        WriteProteinMetadataXML(writer, alt, false); // don't skip name and description
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }

                writer.WriteStartElement(EL.sequence);
                writer.WriteString(FormatProteinSequence(seq.Sequence));
                writer.WriteEndElement();
            }

            foreach (PeptideDocNode nodePeptide in node.Children)
            {
                WritePeptideXml(writer, nodePeptide);
            }
        }

        /// <summary>
        /// Formats a FASTA sequence string for output as XML element content.
        /// </summary>
        /// <param name="sequence">An unformated FASTA sequence string</param>
        /// <returns>A formatted version of the input sequence</returns>
        private static string FormatProteinSequence(string sequence)
        {
            const string lineSeparator = "\r\n        ";

            StringBuilder sb = new StringBuilder();
            if (sequence.Length > 50)
                sb.Append(lineSeparator);
            for (int i = 0; i < sequence.Length; i += 10)
            {
                if (sequence.Length - i <= 10)
                    sb.Append(sequence.Substring(i));
                else
                {
                    sb.Append(sequence.Substring(i, Math.Min(10, sequence.Length - i)));
                    // ReSharper disable once LocalizableElement
                    sb.Append(i % 50 == 40 ? "\r\n        " : @" ");
                }
            }

            return sb.ToString();
        }

        private void WriteExplicitTransitionValuesAttributes(XmlWriter writer, ExplicitTransitionValues importedAttributes)
        {
            writer.WriteAttributeNullable(ATTR.explicit_collision_energy, importedAttributes.CollisionEnergy);
            writer.WriteAttributeNullable(ATTR.explicit_ion_mobility_high_energy_offset, importedAttributes.IonMobilityHighEnergyOffset);
            writer.WriteAttributeNullable(ATTR.explicit_s_lens, importedAttributes.SLens);
            writer.WriteAttributeNullable(ATTR.explicit_cone_voltage, importedAttributes.ConeVoltage);
            writer.WriteAttributeNullable(ATTR.explicit_declustering_potential, importedAttributes.DeclusteringPotential);
        }


        /// <summary>
        /// Serializes any optionally explicitly specified CE, RT and DT information to attributes only
        /// </summary>
        private void WriteExplicitTransitionGroupValuesAttributes(XmlWriter writer, ExplicitTransitionGroupValues importedAttributes)
        {
            writer.WriteAttributeNullable(ATTR.explicit_ion_mobility, importedAttributes.IonMobility);
            if (importedAttributes.IonMobility.HasValue)
                writer.WriteAttribute(ATTR.explicit_ion_mobility_units, importedAttributes.IonMobilityUnits.ToString());
            writer.WriteAttributeNullable(ATTR.explicit_ccs_sqa, importedAttributes.CollisionalCrossSectionSqA);
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="PeptideDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="node">The peptide (or small molecule) document node</param>
        private void WritePeptideXml(XmlWriter writer, PeptideDocNode node)
        {
            var peptide = node.Peptide;
            var isCustomIon = peptide.IsCustomMolecule;

            writer.WriteStartElement(isCustomIon ? EL.molecule : EL.peptide);
            if (node.ExplicitRetentionTime != null)
            {
                writer.WriteAttribute(ATTR.explicit_retention_time, node.ExplicitRetentionTime.RetentionTime);
                writer.WriteAttributeNullable(ATTR.explicit_retention_time_window, node.ExplicitRetentionTime.RetentionTimeWindow);
            }
            double? scoreCalc = null;

            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            if (node.GlobalStandardType != null)
                writer.WriteAttribute(ATTR.standard_type, node.GlobalStandardType.Name);

            writer.WriteAttributeNullable(ATTR.rank, node.Rank);
            writer.WriteAttributeNullable(ATTR.concentration_multiplier, node.ConcentrationMultiplier);
            writer.WriteAttributeNullable(ATTR.internal_standard_concentration, node.InternalStandardConcentration);
            if (null != node.NormalizationMethod)
            {
                writer.WriteAttribute(ATTR.normalization_method, node.NormalizationMethod.Name);
            }
            writer.WriteAttributeIfString(ATTR.attribute_group_id, node.AttributeGroupId);

            if (isCustomIon)
            {
                peptide.CustomMolecule.WriteXml(writer, Adduct.EMPTY);
            }
            else
            {
                string sequence = peptide.Target.Sequence;
                writer.WriteAttributeString(ATTR.sequence, sequence);
                var modSeq = Settings.GetModifiedSequence(node);
                writer.WriteAttributeString(ATTR.modified_sequence, GetModifiedSequence(modSeq));
                if (node.SourceKey != null)
                    writer.WriteAttributeString(ATTR.lookup_sequence, node.SourceKey.ModifiedSequence);
                if (peptide.Begin.HasValue && peptide.End.HasValue)
                {
                    writer.WriteAttribute(ATTR.start, peptide.Begin.Value);
                    writer.WriteAttribute(ATTR.end, peptide.End.Value);
                    writer.WriteAttribute(ATTR.prev_aa, peptide.PrevAA);
                    writer.WriteAttribute(ATTR.next_aa, peptide.NextAA);
                }
                var massH = Settings.GetPrecursorCalc(IsotopeLabelType.light, node.ExplicitMods).GetPrecursorMass(peptide.Target);
                writer.WriteAttribute(ATTR.calc_neutral_pep_mass,
                    SequenceMassCalc.PersistentNeutral(massH));

                writer.WriteAttribute(ATTR.num_missed_cleavages, peptide.MissedCleavages);
                writer.WriteAttribute(ATTR.decoy, node.IsDecoy);
                var rtPredictor = Settings.PeptideSettings.Prediction.RetentionTime;
                if (rtPredictor != null)
                {
                    scoreCalc = rtPredictor.Calculator.ScoreSequence(modSeq);
                    if (scoreCalc.HasValue)
                    {
                        writer.WriteAttributeNullable(ATTR.rt_calculator_score, scoreCalc);
                        writer.WriteAttributeNullable(ATTR.predicted_retention_time,
                            rtPredictor.GetRetentionTime(scoreCalc.Value));
                    }
                }
            }

            writer.WriteAttributeNullable(ATTR.avg_measured_retention_time, node.AverageMeasuredRetentionTime);

            // Write child elements
            WriteAnnotations(writer, node.Annotations);
            if (!isCustomIon)
            {
                // CONSIDER(bspratt) the code as written actually can use static isotope
                // label modifications, and this if clause could be removed - but Brendan wants proof of demand for this first
                WriteExplicitMods(writer, node.Peptide.Target.Sequence, node.ExplicitMods);
                WriteImplicitMods(writer, node);
                WriteLookupMods(writer, node);
            }
            if (node.HasResults)
            {
                WriteResults(writer, Settings, node.Results,
                    EL.peptide_results, EL.peptide_result, (w, i) => WritePeptideChromInfo(w, i, scoreCalc));
            }

            foreach (TransitionGroupDocNode nodeGroup in node.Children)
            {
                writer.WriteStartElement(EL.precursor);
                WriteTransitionGroupXml(writer, node, nodeGroup);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private string GetModifiedSequence(Target target)
        {
            if (DocumentFormat >= DocumentFormat.VERSION_3_73 || !target.IsProteomic)
            {
                return target.Sequence;
            }
            return new PeptideLibraryKey(target.Sequence, 0).FormatToOneDecimal().ModifiedSequence;
        }

        private void WriteLookupMods(XmlWriter writer, PeptideDocNode node)
        {
            if (node.SourceKey == null || node.SourceKey.ExplicitMods == null)
                return;
            writer.WriteStartElement(EL.lookup_modifications);
            WriteExplicitMods(writer, node.SourceKey.Sequence, node.SourceKey.ExplicitMods);
            writer.WriteEndElement();
        }

        private void WriteExplicitMods(XmlWriter writer, string sequence, ExplicitMods mods)
        {
            if (mods == null ||
                string.IsNullOrEmpty(sequence) && !mods.HasIsotopeLabels)
                return;
            if (mods.IsVariableStaticMods)
            {
                WriteExplicitMods(writer, EL.variable_modifications,
                    EL.variable_modification, null, mods.StaticModifications, sequence);

                // If no heavy modifications, then don't write an <explicit_modifications> tag
                if (!mods.HasHeavyModifications)
                    return;
            }
            writer.WriteStartElement(EL.explicit_modifications);
            if (!mods.IsVariableStaticMods)
            {
                WriteExplicitMods(writer, EL.explicit_static_modifications,
                    EL.explicit_modification, null, mods.StaticModifications, sequence);
            }
            foreach (var heavyMods in mods.GetHeavyModifications())
            {
                IsotopeLabelType labelType = heavyMods.LabelType;
                if (Equals(labelType, IsotopeLabelType.heavy))
                    labelType = null;

                WriteExplicitMods(writer, EL.explicit_heavy_modifications,
                    EL.explicit_modification, labelType, heavyMods.Modifications, sequence);
            }
            writer.WriteEndElement();
        }

        private void WriteImplicitMods(XmlWriter writer, PeptideDocNode node)
        {
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

            writer.WriteStartElement(EL.implicit_modifications);

            // implicit static modifications.
            if (hasStaticMods)
            {
                WriteExplicitMods(writer, EL.implicit_static_modifications,
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

                WriteExplicitMods(writer, EL.implicit_heavy_modifications,
                                  EL.implicit_modification, labelType, heavyMods.Modifications,
                                  node.Peptide.Target.Sequence);
            }
            writer.WriteEndElement();
        }


        private void WriteExplicitMods(XmlWriter writer, string name,
            string nameElMod, IsotopeLabelType labelType, IEnumerable<ExplicitMod> mods,
            string sequence)
        {
            if (mods == null || (labelType == null && string.IsNullOrEmpty(sequence)))
                return;
            writer.WriteStartElement(name);
            if (labelType != null)
                writer.WriteAttribute(ATTR.isotope_label, labelType);

            if (!string.IsNullOrEmpty(sequence))
            {
                SequenceMassCalc massCalc = Settings.TransitionSettings.Prediction.PrecursorMassType == MassType.Monoisotopic ?
                    SrmSettings.MonoisotopicMassCalc : SrmSettings.AverageMassCalc;
                foreach (ExplicitMod mod in mods)
                {
                    writer.WriteStartElement(nameElMod);
                    writer.WriteAttribute(ATTR.index_aa, mod.IndexAA);
                    writer.WriteAttribute(ATTR.modification_name, mod.Modification.Name);

                    double massDiff = massCalc.GetModMass(sequence[mod.IndexAA], mod.Modification);

                    writer.WriteAttribute(ATTR.mass_diff,
                        string.Format(CultureInfo.InvariantCulture, @"{0}{1}", (massDiff < 0 ? string.Empty : @"+"),
                            Math.Round(massDiff, 1)));

                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        private void WritePeptideChromInfo(XmlWriter writer, PeptideChromInfo chromInfo, double? scoreCalc)
        {
            writer.WriteAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            writer.WriteAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            writer.WriteAttribute(ATTR.exclude_from_calibration, chromInfo.ExcludeFromCalibration);
            if (scoreCalc.HasValue)
            {
                double? rt = Settings.PeptideSettings.Prediction.RetentionTime.GetRetentionTime(scoreCalc.Value,
                                                                                      chromInfo.FileId);
                writer.WriteAttributeNullable(ATTR.predicted_retention_time, rt);
            }
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="TransitionGroupDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="nodePep">The parent peptide document node</param>
        /// <param name="node">The transition group document node</param>
        private void WriteTransitionGroupXml(XmlWriter writer, PeptideDocNode nodePep, TransitionGroupDocNode node)
        {
            TransitionGroup group = node.TransitionGroup;
            var isCustomIon = nodePep.Peptide.IsCustomMolecule;
            writer.WriteAttribute(ATTR.charge, group.PrecursorAdduct.AdductCharge);
            if (!group.LabelType.IsLight)
                writer.WriteAttribute(ATTR.isotope_label, group.LabelType);
            if (!isCustomIon)
            {
                writer.WriteAttribute(ATTR.calc_neutral_mass, node.GetPrecursorIonPersistentNeutralMass());
            }
            writer.WriteAttribute(ATTR.precursor_mz, SequenceMassCalc.PersistentMZ(node.PrecursorMz));
            WriteExplicitTransitionGroupValuesAttributes(writer, node.ExplicitValues);

            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            writer.WriteAttributeNullable(ATTR.decoy_mass_shift, group.DecoyMassShift);
            writer.WriteAttributeNullable(ATTR.precursor_concentration, node.PrecursorConcentration);


            TransitionPrediction predict = Settings.TransitionSettings.Prediction;
            double regressionMz = Settings.GetRegressionMz(nodePep, node);
            var ce = predict.CollisionEnergy.GetCollisionEnergy(node.TransitionGroup.PrecursorAdduct, regressionMz);
            writer.WriteAttribute(ATTR.collision_energy, ce);

            var dpRegression = predict.DeclusteringPotential;
            if (dpRegression != null)
            {
                var dp = dpRegression.GetDeclustringPotential(regressionMz);
                writer.WriteAttribute(ATTR.declustering_potential, dp);
            }

            if (!isCustomIon)
            {
                // modified sequence
                var calcPre = Settings.GetPrecursorCalc(node.TransitionGroup.LabelType, nodePep.ExplicitMods);
                var seq = node.TransitionGroup.Peptide.Target;
                writer.WriteAttribute(ATTR.modified_sequence, calcPre.GetModifiedSequence(seq,
                    false)); // formatNarrow = false; We want InvariantCulture, not the local format
                Assume.IsTrue(group.PrecursorAdduct.IsProteomic);
            }
            else
            {
                // Custom ion
                node.CustomMolecule.WriteXml(writer, group.PrecursorAdduct);
            }
            // Write child elements
            WriteAnnotations(writer, node.Annotations);
            if (node.HasLibInfo)
            {
                var helpers = PeptideLibraries.SpectrumHeaderXmlHelpers;
                writer.WriteElements(new[] { node.LibInfo }, helpers);
            }

            if (node.HasResults)
            {
                WriteResults(writer, Settings, node.Results,
                    EL.precursor_results, EL.precursor_peak, WriteTransitionGroupChromInfo);
            }

            if (UseCompactFormat())
            {
                writer.WriteStartElement(EL.transition_data);
                var transitionData = new SkylineDocumentProto.Types.TransitionData();
                transitionData.Transitions.AddRange(node.Transitions.Select(transition => transition.ToTransitionProto(Settings)));
                byte[] bytes = transitionData.ToByteArray();
                writer.WriteBase64(bytes, 0, bytes.Length);
                writer.WriteEndElement();
                if (WroteTransitions != null)
                    WroteTransitions(node.TransitionCount);
            }
            else
            {
                foreach (TransitionDocNode nodeTransition in node.Children)
                {
                    writer.WriteStartElement(EL.transition);
                    WriteTransitionXml(writer, nodePep, node, nodeTransition);
                    writer.WriteEndElement();
                }
            }
        }

        private static void WriteTransitionGroupChromInfo(XmlWriter writer, TransitionGroupChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                writer.WriteAttribute(ATTR.step, chromInfo.OptimizationStep);
            writer.WriteAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            writer.WriteAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            writer.WriteAttributeNullable(ATTR.start_time, chromInfo.StartRetentionTime);
            writer.WriteAttributeNullable(ATTR.end_time, chromInfo.EndRetentionTime);
            writer.WriteAttributeNullable(ATTR.ccs, chromInfo.IonMobilityInfo.CollisionalCrossSection);
            if (chromInfo.IonMobilityInfo.IonMobilityUnits != eIonMobilityUnits.none)
            {
                writer.WriteAttributeNullable(ATTR.ion_mobility_ms1, chromInfo.IonMobilityInfo.IonMobilityMS1);
                writer.WriteAttributeNullable(ATTR.ion_mobility_fragment, chromInfo.IonMobilityInfo.IonMobilityFragment);
                writer.WriteAttributeNullable(ATTR.ion_mobility_window, chromInfo.IonMobilityInfo.IonMobilityWindow);
                writer.WriteAttribute(ATTR.ion_mobility_type, chromInfo.IonMobilityInfo.IonMobilityUnits.ToString());
            }
            writer.WriteAttributeNullable(ATTR.fwhm, chromInfo.Fwhm);
            writer.WriteAttributeNullable(ATTR.area, chromInfo.Area);
            writer.WriteAttributeNullable(ATTR.background, chromInfo.BackgroundArea);
            writer.WriteAttributeNullable(ATTR.height, chromInfo.Height);
            writer.WriteAttributeNullable(ATTR.mass_error_ppm, chromInfo.MassError);
            writer.WriteAttributeNullable(ATTR.truncated, chromInfo.Truncated);
            writer.WriteAttribute(ATTR.identified, chromInfo.Identified.ToString().ToLowerInvariant());
            writer.WriteAttributeNullable(ATTR.library_dotp, chromInfo.LibraryDotProduct);
            writer.WriteAttributeNullable(ATTR.isotope_dotp, chromInfo.IsotopeDotProduct);
            writer.WriteAttributeNullable(ATTR.qvalue, chromInfo.QValue);
            writer.WriteAttributeNullable(ATTR.zscore, chromInfo.ZScore);
            writer.WriteAttribute(ATTR.user_set, chromInfo.UserSet);
            WriteAnnotations(writer, chromInfo.Annotations);
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="TransitionDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="nodePep">The transition group's parent peptide node</param>
        /// <param name="nodeGroup">The transition node's parent group node</param>
        /// <param name="nodeTransition">The transition document node</param>
        private void WriteTransitionXml(XmlWriter writer, PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup,
                                        TransitionDocNode nodeTransition)
        {
            Transition transition = nodeTransition.Transition;
            writer.WriteAttribute(ATTR.fragment_type, transition.IonType);
            writer.WriteAttribute(ATTR.quantitative, nodeTransition.ExplicitQuantitative, true);
            WriteExplicitTransitionValuesAttributes(writer, nodeTransition.ExplicitValues);
            if (transition.IsCustom())
            {
                if (!(transition.CustomIon is SettingsCustomIon))
                {
                    transition.CustomIon.WriteXml(writer, transition.Adduct);
                }
                else
                {
                    writer.WriteAttributeString(ATTR.measured_ion_name, transition.CustomIon.Name);
                }
            }
            writer.WriteAttributeNullable(ATTR.decoy_mass_shift, transition.DecoyMassShift);
            // NOTE: MassIndex is the peak index in the isotopic distribution of the precursor.
            //       0 for monoisotopic peaks and for non "precursor" ion types.
            if (transition.MassIndex != 0)
                writer.WriteAttribute(ATTR.mass_index, transition.MassIndex);
            if (nodeTransition.HasDistInfo)
            {
                writer.WriteAttribute(ATTR.isotope_dist_rank, nodeTransition.IsotopeDistInfo.Rank);
                writer.WriteAttribute(ATTR.isotope_dist_proportion, nodeTransition.IsotopeDistInfo.Proportion);
            }

            if (transition.IsPrecursor())
            {
                writer.WriteAttribute(ATTR.product_charge, transition.Charge, nodeGroup.PrecursorCharge);
            }
            else
            {
                if (!transition.IsCustom())
                {
                    writer.WriteAttribute(ATTR.fragment_ordinal, transition.Ordinal);
                    writer.WriteAttribute(ATTR.calc_neutral_mass, nodeTransition.GetMoleculePersistentNeutralMass());
                }
                writer.WriteAttribute(ATTR.product_charge, transition.Charge);
                if (!transition.IsCustom())
                {
                    writer.WriteAttribute(ATTR.cleavage_aa, transition.AA.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttribute(ATTR.loss_neutral_mass, nodeTransition.LostMass); //po
                }
            }

            // Order of elements matters for XSD validation
            WriteAnnotations(writer, nodeTransition.Annotations);
            writer.WriteElementString(EL.precursor_mz, SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz));
            writer.WriteElementString(EL.product_mz, SequenceMassCalc.PersistentMZ(nodeTransition.Mz));

            TransitionPrediction predict = Settings.TransitionSettings.Prediction;
            var optimizationMethod = predict.OptimizedMethodType;
            double? ce = null;
            double? dp = null;
            var lib = predict.OptimizedLibrary;
            if (lib != null && !lib.IsNone)
            {
                var optimization = lib.GetOptimization(OptimizationType.collision_energy,
                    Settings.GetSourceTarget(nodePep), nodeGroup.PrecursorAdduct,
                    nodeTransition.FragmentIonName, nodeTransition.Transition.Adduct);
                if (optimization != null)
                {
                    ce = optimization.Value;
                }
            }

            double regressionMz = Settings.GetRegressionMz(nodePep, nodeGroup);
            var ceRegression = predict.CollisionEnergy;
            var dpRegression = predict.DeclusteringPotential;
            if (optimizationMethod == OptimizedMethodType.None)
            {
                if (ceRegression != null && !ce.HasValue)
                {
                    ce = ceRegression.GetCollisionEnergy(nodeGroup.PrecursorAdduct, regressionMz);
                }
                if (dpRegression != null)
                {
                    dp = dpRegression.GetDeclustringPotential(regressionMz);
                }
            }
            else
            {
                if (!ce.HasValue)
                {
                    ce = OptimizationStep<CollisionEnergyRegression>.FindOptimizedValue(Settings,
                        nodePep, nodeGroup, nodeTransition, optimizationMethod, ceRegression,
                        SrmDocument.GetCollisionEnergy);
                }

                dp = OptimizationStep<DeclusteringPotentialRegression>.FindOptimizedValue(Settings,
                    nodePep, nodeGroup, nodeTransition, optimizationMethod, dpRegression,
                    SrmDocument.GetDeclusteringPotential);
            }

            if (nodeTransition.ExplicitValues.CollisionEnergy.HasValue)
                ce = nodeTransition.ExplicitValues.CollisionEnergy; // Explicitly imported, overrides any calculation

            if (ce.HasValue)
            {
                writer.WriteElementString(EL.collision_energy, ce.Value);
            }

            if (dp.HasValue)
            {
                writer.WriteElementString(EL.declustering_potential, dp.Value);
            }
            WriteTransitionLosses(writer, nodeTransition.Losses);

            if (nodeTransition.HasLibInfo)
            {
                writer.WriteStartElement(EL.transition_lib_info);
                writer.WriteAttribute(ATTR.rank, nodeTransition.LibInfo.Rank);
                writer.WriteAttribute(ATTR.intensity, nodeTransition.LibInfo.Intensity);
                writer.WriteEndElement();
            }

            if (nodeTransition.HasResults)
            {
                if (nodeTransition.HasResults)
                {
                    if (UseCompactFormat())
                    {
                        var protoResults = new SkylineDocumentProto.Types.TransitionResults();
                        protoResults.Peaks.AddRange(nodeTransition.GetTransitionPeakProtos(Settings.MeasuredResults));
                        byte[] bytes = protoResults.ToByteArray();
                        writer.WriteStartElement(EL.results_data);
                        writer.WriteBase64(bytes, 0, bytes.Length);
                        writer.WriteEndElement();
                    }
                    else
                    {
                        WriteResults(writer, Settings, nodeTransition.Results,
                            EL.transition_results, EL.transition_peak, WriteTransitionChromInfo);
                    }
                }
            }

            if (WroteTransitions != null)
                WroteTransitions(1);
        }

        private void WriteTransitionLosses(XmlWriter writer, TransitionLosses losses)
        {
            if (losses == null)
                return;
            writer.WriteStartElement(EL.losses);
            foreach (var loss in losses.Losses)
            {
                writer.WriteStartElement(EL.neutral_loss);
                if (loss.PrecursorMod == null)
                {
                    // Custom neutral losses are not yet implemented to cause this case
                    // TODO: Implement custome neutral losses, and remove this comment.
                    loss.Loss.WriteXml(writer);
                }
                else
                {
                    writer.WriteAttribute(ATTR.modification_name, loss.PrecursorMod.Name);
                    int indexLoss = loss.LossIndex;
                    if (indexLoss != 0)
                        writer.WriteAttribute(ATTR.loss_index, indexLoss);
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void WriteTransitionChromInfo(XmlWriter writer, TransitionChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                writer.WriteAttribute(ATTR.step, chromInfo.OptimizationStep);

            // Only write peak information, if it is not empty
            if (!chromInfo.IsEmpty)
            {
                writer.WriteAttributeNullable(ATTR.mass_error_ppm, chromInfo.MassError);
                writer.WriteAttribute(ATTR.retention_time, chromInfo.RetentionTime);
                writer.WriteAttribute(ATTR.start_time, chromInfo.StartRetentionTime);
                writer.WriteAttribute(ATTR.end_time, chromInfo.EndRetentionTime);
                writer.WriteAttributeNullable(ATTR.ion_mobility, chromInfo.IonMobility.IonMobility.Mobility);
                writer.WriteAttributeNullable(ATTR.ion_mobility_window, chromInfo.IonMobility.IonMobilityExtractionWindowWidth);
                writer.WriteAttribute(ATTR.area, chromInfo.Area);
                writer.WriteAttribute(ATTR.background, chromInfo.BackgroundArea);
                writer.WriteAttribute(ATTR.height, chromInfo.Height);
                writer.WriteAttribute(ATTR.fwhm, chromInfo.Fwhm);
                writer.WriteAttribute(ATTR.fwhm_degenerate, chromInfo.IsFwhmDegenerate);
                writer.WriteAttributeNullable(ATTR.truncated, chromInfo.IsTruncated);
                writer.WriteAttribute(ATTR.identified, chromInfo.Identified.ToString().ToLowerInvariant());
                writer.WriteAttribute(ATTR.rank, chromInfo.Rank);
                if (SkylineVersion.SrmDocumentVersion.CompareTo(DocumentFormat.VERSION_3_61) >= 0)
                {
                    writer.WriteAttributeNullable(ATTR.points_across, chromInfo.PointsAcrossPeak);
                }
                if (chromInfo.Rank != chromInfo.RankByLevel)
                    writer.WriteAttribute(ATTR.rank_by_level, chromInfo.RankByLevel);
            }
            writer.WriteAttribute(ATTR.user_set, chromInfo.UserSet);
            writer.WriteAttribute(ATTR.forced_integration, chromInfo.IsForcedIntegration, false);
            WriteAnnotations(writer, chromInfo.Annotations);
        }

        public static void WriteAnnotations(XmlWriter writer, Annotations annotations)
        {
            if (annotations.IsEmpty)
                return;

            if (annotations.Note != null || annotations.ColorIndex > 0)
            {
                if (annotations.ColorIndex == 0)
                    writer.WriteElementString(EL.note, annotations.Note);
                else
                {
                    writer.WriteStartElement(EL.note);
                    writer.WriteAttribute(ATTR.category, annotations.ColorIndex);
                    if (annotations.Note != null)
                    {
                        writer.WriteString(annotations.Note);
                    }
                    writer.WriteEndElement();
                }
            }
            foreach (var entry in annotations.ListAnnotations())
            {
                writer.WriteStartElement(EL.annotation);
                writer.WriteAttribute(ATTR.name, entry.Key);
                writer.WriteString(entry.Value);
                writer.WriteEndElement();
            }
        }

        private static void WriteResults<TItem>(XmlWriter writer, SrmSettings settings,
                IEnumerable<ChromInfoList<TItem>> results, string start, string startChild,
                Action<XmlWriter, TItem> writeChromInfo)
            where TItem : ChromInfo
        {
            bool started = false;
            using (var enumReplicates = settings.MeasuredResults.Chromatograms.GetEnumerator())
            {
                foreach (var listChromInfo in results)
                {
                    bool success = enumReplicates.MoveNext();
                    Assume.IsTrue(success || settings.MeasuredResults.Chromatograms.Count == 0);
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
                            writer.WriteStartElement(start);
                            started = true;
                        }
                        writer.WriteStartElement(startChild);
                        writer.WriteAttribute(ATTR.replicate, name);
                        if (chromatogramSet.FileCount > 1)
                            writer.WriteAttribute(ATTR.file, chromatogramSet.GetFileSaveId(chromInfo.FileId));
                        writeChromInfo(writer, chromInfo);
                        writer.WriteEndElement();
                    }
                }
            }
            if (started)
                writer.WriteEndElement();
        }


    }
}
