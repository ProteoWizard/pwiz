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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    /// <summary>
    /// Reads the &lt;peptide&gt; or &lt;molecule&gt; element of one <see cref="PeptideDocNode"/>,
    /// and everything below it. The counterpart of <see cref="MoleculeWriter"/>.
    /// <para>
    /// One of these is made per molecule, which is what lets the things that are the same for all
    /// of a molecule - and for one precursor while its transitions are being read - be fields
    /// rather than parameters handed down through every level. Molecules are read on several
    /// threads at once against one <see cref="DocumentReader"/>, so this is also what keeps that
    /// state off the reader they share.
    /// </para>
    /// </summary>
    public class MoleculeReader : DocumentSerializer
    {
        private readonly DocumentReader _documentReader;
        private readonly PeptideGroup _peptideGroup;

        /// <summary>
        /// The molecule's element. Replaced while reading a document written before 3.72, whose
        /// molecules are handed back in a modernized form - it is still the same molecule.
        /// </summary>
        private XElement _element;

        public MoleculeReader(DocumentReader documentReader, XElement element, PeptideGroup peptideGroup)
        {
            _documentReader = documentReader;
            _element = element;
            _peptideGroup = peptideGroup;
            Settings = documentReader.Settings;
            DocumentFormat = documentReader.DocumentFormat;
        }

        /// <summary>
        /// Whether this is a small molecule rather than a peptide, which the tag says.
        /// </summary>
        private bool IsCustomMolecule
        {
            get { return _element.Name == EL.molecule; }
        }

        private DocumentFormat FormatVersion
        {
            get { return _documentReader.FormatVersion; }
        }

        private bool DocumentMayContainMoleculesWithEmbeddedIons
        {
            get { return _documentReader.DocumentMayContainMoleculesWithEmbeddedIons; }
        }

        private AnnotationScrubber AnnotationScrubber
        {
            get { return _documentReader.AnnotationScrubber; }
        }

        private Annotations ReadTargetAnnotations(XmlReader reader, AnnotationDef.AnnotationTarget target)
        {
            return _documentReader.ReadTargetAnnotations(reader, target);
        }

        /// <summary>
        /// The annotations of one element, which are its first children.
        /// </summary>
        private Annotations ReadTargetAnnotations(XElement element, AnnotationDef.AnnotationTarget target)
        {
            using (var reader = OpenReader(element))
            {
                reader.ReadStartElement();
                return reader.IsStartElement()
                    ? ReadTargetAnnotations(reader, target)
                    : Annotations.EMPTY;
            }
        }

        /// <summary>
        /// A reader standing on <paramref name="element"/>, for the parts of a molecule which read
        /// themselves out of a stream and so take a reader rather than an element. Everything which
        /// decides what to read next navigates the elements instead - see the class comment.
        /// </summary>
        private static XmlReader OpenReader(XElement element)
        {
            var reader = element.CreateReader();
            reader.Read();
            return reader;
        }

        /// <summary>
        /// A reader standing on one named child, or null when there is no such child. The parts
        /// which take a reader answer null or nothing for an element which is not theirs, so a
        /// caller which has already looked can simply not ask.
        /// </summary>
        private static XmlReader OpenReader(XElement parent, string name)
        {
            var element = parent.Element(name);
            return element == null ? null : OpenReader(element);
        }

        private SpectrumHeaderInfo ReadTransitionGroupLibInfo(XmlReader reader)
        {
            // Look for an appropriate deserialization helper for spectrum
            // header info on the current tag.
            var helpers = PeptideLibraries.SpectrumHeaderXmlHelpers;
            var helper = reader.FindHelper(helpers);
            if (helper != null)
            {
                var libInfo = helper.Deserialize(reader);
                return libInfo.ChangeLibraryName(_documentReader.GetUniqueString(libInfo.LibraryName));
            }

            return null;
        }

        /// <summary>
        /// Whether the precursor whose transitions are being read kept chrom infos, which is what
        /// says which of the two things a transition's element holds. Set from the precursor's own
        /// element, which is always read first - see <see cref="ReadPrecursorResults"/>.
        /// </summary>
        private static eIonMobilityUnits GetAttributeMobilityUnits(XmlReader reader, string attrName, ChromFileInfo fileInfo)
        {
            string ionMobilityUnitsString = reader.GetAttribute(attrName);
            eIonMobilityUnits ionMobilityUnits =
              string.IsNullOrEmpty( ionMobilityUnitsString) ?
              (fileInfo == null ? eIonMobilityUnits.none : fileInfo.IonMobilityUnits) : // Use the file-level declaration if no local declaration
              TypeSafeEnum.Parse<eIonMobilityUnits>(ionMobilityUnitsString);
            return ionMobilityUnits;
        }

        private static Annotations ReadAndRemoveScoreAnnotation(Annotations annotations, string annotationName, ref float? annotationValue)
        {
            string annotationText = annotations.GetAnnotation(annotationName);
            if (String.IsNullOrEmpty(annotationText))
                return annotations;
            double scoreValue;
            if (Double.TryParse(annotationText, out scoreValue))
                annotationValue = (float) scoreValue;
            return annotations.RemoveAnnotation(annotationName);
        }


        /// <summary>
        /// A precursor's peaks, which its transitions' peaks are read against: which of the two
        /// shapes they are in, and what each of them says its transitions are like in one file
        /// unless they say otherwise - see <see cref="MoleculeWriter.SetColumnarPrecursorPeak"/>.
        /// <para>
        /// The elements themselves, looked up by the replicate and file a transition's peak names,
        /// rather than values taken off them on the way past. Nothing is remembered here that the
        /// document does not still say.
        /// </para>
        /// </summary>
        private class PrecursorPeaks
        {
            /// <summary>
            /// What a transition's peak is read against when there is no precursor element above it
            /// at all, which is the v0.1 format and nothing else.
            /// </summary>
            public static readonly PrecursorPeaks LEGACY = new PrecursorPeaks(null);

            private readonly Dictionary<Tuple<string, string>, XElement> _peaks =
                new Dictionary<Tuple<string, string>, XElement>();

            public PrecursorPeaks(XElement precursorResults)
            {
                foreach (var peak in precursorResults?.Elements(EL.precursor_peak) ?? Enumerable.Empty<XElement>())
                {
                    // The peak count ratio is an aggregate of the transitions, so only a precursor
                    // which kept chrom infos ever wrote one - see
                    // DocumentWriter.WriteTransitionGroupChromInfo, which always does, against
                    // WriteTransitionGroupResults, which never does. This used to be told from
                    // chosen_peak_index, which now says something else: a precursor can be in the
                    // columnar shape and still not know which candidate peaks its peaks are.
                    IsLegacyShape = IsLegacyShape || peak.Attribute(ATTR.peak_count_ratio) != null;
                    // The first peak a file has is the one that counts, as it is everywhere else
                    // these are read: a document old enough has one per optimization step.
                    var key = KeyOf(peak);
                    if (!_peaks.ContainsKey(key))
                        _peaks.Add(key, peak);
                }

                IsLegacyShape = IsLegacyShape || precursorResults == null;
            }

            public bool IsLegacyShape { get; private set; }

            /// <summary>
            /// The precursor's peak in the replicate and file one transition's peak names, or an
            /// empty element when it has none there - which answers every attribute the same way an
            /// absent one does.
            /// </summary>
            public XElement Find(XElement transitionPeak)
            {
                return _peaks.TryGetValue(KeyOf(transitionPeak), out var peak)
                    ? peak
                    : new XElement(EL.precursor_peak);
            }

            private static Tuple<string, string> KeyOf(XElement peak)
            {
                return Tuple.Create(peak.GetAttribute(ATTR.replicate), peak.GetAttribute(ATTR.file));
            }
        }

        /// <summary>
        /// Helper class for reading information from a transition element into
        /// memory for use in both <see cref="Transition"/> and <see cref="TransitionGroup"/>.
        ///
        /// This class exists to share code between <see cref="ReadTransitionXml"/>
        /// and <see cref="ReadUngroupedTransitionListXml"/>.
        /// </summary>
        private class TransitionInfo
        {
            private readonly MoleculeReader _moleculeReader;
            private readonly PrecursorPeaks _precursorPeaks;

            public TransitionInfo(MoleculeReader moleculeReader, PrecursorPeaks precursorPeaks)
            {
                _moleculeReader = moleculeReader;
                _precursorPeaks = precursorPeaks;
            }
            public SrmSettings Settings { get { return _moleculeReader.Settings; } }
            public ExplicitMods ExplicitMods { get; private set; }
            public IonType IonType { get; private set; }
            public int Ordinal { get; private set; }
            public int MassIndex { get; private set; }
            public Adduct PrecursorAdduct { get; private set; }
            public Adduct ProductAdduct { get; private set; }
            public int? DecoyMassShift { get; private set; }
            public TransitionLosses Losses { get; private set; }

            public List<IonOrdinal> LinkedFragmentIons { get; private set; }
            public List<LegacyComplexFragmentIonName> LegacyFragmentIons { get; private set; }
            public bool OrphanedCrosslinkIon { get; private set; }
            public Annotations Annotations { get; private set; }
            public TransitionLibInfo LibInfo { get; private set; }
            public Results<TransitionChromInfo> Results { get; private set; }

            /// <summary>
            /// What a document written without the chrom infos has instead of them.
            /// </summary>
            public TransitionResultsData ColumnarResults { get; private set; }
            public MeasuredIon MeasuredIon { get; private set; }
            public bool Quantitative { get; private set; }
            public ExplicitTransitionValues ExplicitValues { get; private set; }

            public void ReadXml(XElement element, DocumentFormat formatVersion, out double? declaredMz, ExplicitTransitionValues pre422ExplicitTransitionValues)
            {
                ReadXmlAttributes(element, formatVersion, pre422ExplicitTransitionValues);
                ReadXmlElements(element, out declaredMz);
            }

            public void ReadXmlAttributes(XElement element, DocumentFormat formatVersion, ExplicitTransitionValues pre422ExplicitTransitionValues)
            {
                // Accept uppercase and lowercase for backward compatibility with v0.1
                IonType = element.GetEnumAttribute(ATTR.fragment_type, IonType.y, XmlUtil.EnumCase.lower);
                Ordinal = element.GetIntAttribute(ATTR.fragment_ordinal);
                MassIndex = element.GetIntAttribute(ATTR.mass_index);
                // NOTE: PrecursorCharge is used only in TransitionInfo.ReadUngroupedTransitionListXml()
                //       to support v0.1 document format
                PrecursorAdduct = Adduct.FromStringAssumeProtonated(element.GetAttribute(ATTR.precursor_charge));
                ProductAdduct = Adduct.FromStringAssumeProtonated(element.GetAttribute(ATTR.product_charge));
                DecoyMassShift = element.GetNullableIntAttribute(ATTR.decoy_mass_shift);
                Quantitative = element.GetBoolAttribute(ATTR.quantitative, true);
                OrphanedCrosslinkIon = element.GetBoolAttribute(ATTR.orphaned_crosslink_ion);
                string measuredIonName = element.GetAttribute(ATTR.measured_ion_name);
                if (measuredIonName != null)
                {
                    MeasuredIon = Settings.TransitionSettings.Filter.MeasuredIons.SingleOrDefault(
                        i => i.Name.Equals(measuredIonName));
                    if (MeasuredIon == null)
                        throw new InvalidDataException(String.Format(Resources.TransitionInfo_ReadXmlAttributes_The_reporter_ion__0__was_not_found_in_the_transition_filter_settings_, measuredIonName));
                    IonType = IonType.custom;
                }

                ExplicitValues = pre422ExplicitTransitionValues ?? ReadExplicitTransitionValuesAttributes(element, formatVersion);
            }

            public void ReadXmlElements(XElement element, out double? declaredProductMz)
            {
                declaredProductMz = null;
                LinkedFragmentIons = new List<IonOrdinal>();
                // The annotations are reliably first in all versions, and read themselves out of a
                // stream, so they get a reader over this transition.
                Annotations = _moleculeReader.ReadTargetAnnotations(element,
                    AnnotationDef.AnnotationTarget.transition);
                foreach (var child in element.Elements())
                {  // The order of these elements may depend on the version of the file being read
                    var name = child.Name.LocalName;
                    if (Equals(name, EL.losses))
                        Losses = ReadTransitionLosses(child);
                    else if (Equals(name, EL.linked_fragment_ion))
                    {
                        if (_moleculeReader.FormatVersion < DocumentFormat.FLAT_CROSSLINKS)
                        {
                            LegacyFragmentIons = LegacyFragmentIons ?? new List<LegacyComplexFragmentIonName>();
                            LegacyFragmentIons.Add(ReadLegacyLinkedFragmentIon(child));
                        }
                        else
                        {
                            LinkedFragmentIons.Add(ReadLinkedFragmentIon(child));
                        }
                    }
                    else if (Equals(name, EL.transition_lib_info))
                        LibInfo = ReadTransitionLibInfo(child);
                    else if (Equals(name, EL.results_data))
                    {
                        using (var resultsReader = OpenReader(child))
                        {
                            Results = ReadTransitionResults(resultsReader);
                        }
                    }
                    else if (Equals(name, EL.transition_results))
                        ColumnarResults = _moleculeReader.ReadColumnarTransitionResults(child, _precursorPeaks);
                    // Anything else is an informational element which is not read at all: those
                    // values are always calculated from the settings to ensure consistency. Note
                    // that we do use product_mz for sanity checks and to disambiguate some older
                    // mass-only small molecule documents.
                    else if (Equals(name, EL.product_mz))
                        declaredProductMz = double.Parse(child.Value, CultureInfo.InvariantCulture);
                }
            }

            private TransitionLosses ReadTransitionLosses(XElement element)
            {
                {
                    var staticMods = Settings.PeptideSettings.Modifications.StaticModifications;
                    MassType massType = Settings.TransitionSettings.Prediction.FragmentMassType;

                    var listLosses = new List<TransitionLoss>();
                    foreach (var lossElement in element.Elements(EL.neutral_loss))
                    {
                        string nameMod = lossElement.GetAttribute(ATTR.modification_name);
                        if (String.IsNullOrEmpty(nameMod))
                        {
                            // Deserialize reads attributes only, so a reader standing on the
                            // element is all it needs.
                            using (var lossReader = OpenReader(lossElement))
                            {
                                listLosses.Add(new TransitionLoss(null, FragmentLoss.Deserialize(lossReader),
                                    massType));
                            }
                        }
                        else
                        {
                            int indexLoss = lossElement.GetIntAttribute(ATTR.loss_index);
                            int indexMod = staticMods.IndexOf(mod => Equals(nameMod, mod.Name));
                            if (indexMod == -1)
                            {
                                throw new InvalidDataException(
                                    String.Format(Resources.TransitionInfo_ReadTransitionLosses_No_modification_named__0__was_found_in_this_document,
                                        nameMod));
                            }
                            StaticMod modLoss = staticMods[indexMod];
                            if (!modLoss.HasLoss || indexLoss >= modLoss.Losses.Count)
                            {
                                throw new InvalidDataException(
                                    String.Format(Resources.TransitionInfo_ReadTransitionLosses_Invalid_loss_index__0__for_modification__1__,
                                        indexLoss, nameMod));
                            }
                            listLosses.Add(new TransitionLoss(modLoss, modLoss.Losses[indexLoss], massType));
                        }
                    }

                    return new TransitionLosses(listLosses, massType);
                }
            }

            private LegacyComplexFragmentIonName ReadLegacyLinkedFragmentIon(XElement element)
            {
                IonOrdinal fragmentIonType;
                string strFragmentType = element.GetAttribute(ATTR.fragment_type);
                if (strFragmentType == null)
                {
                    // blank fragment type means orphaned fragment ion
                    fragmentIonType = IonOrdinal.Empty;
                }
                else
                {
                    fragmentIonType = new IonOrdinal(TypeSafeEnum.Parse<IonType>(strFragmentType), element.GetIntAttribute(ATTR.fragment_ordinal));
                }

                var modificationSite = new ModificationSite(element.GetIntAttribute(ATTR.index_aa),
                    element.GetAttribute(ATTR.modification_name));
                var linkedIon = new LegacyComplexFragmentIonName(modificationSite, fragmentIonType);
                foreach (var child in element.Elements())
                {
                    if (!Equals(child.Name.LocalName, EL.linked_fragment_ion))
                    {
                        throw new InvalidDataException();
                    }

                    linkedIon.Children.Add(ReadLegacyLinkedFragmentIon(child));
                }

                return linkedIon;
            }

            private IonOrdinal ReadLinkedFragmentIon(XElement element)
            {
                var ionType = element.GetEnumAttribute(ATTR.fragment_type, IonType.custom, XmlUtil.EnumCase.unkown);
                var ordinal = element.GetIntAttribute(ATTR.fragment_ordinal);
                return ionType == IonType.custom ? IonOrdinal.Empty : new IonOrdinal(ionType, ordinal);
            }

            private static TransitionLibInfo ReadTransitionLibInfo(XElement element)
            {
                return new TransitionLibInfo(element.GetIntAttribute(ATTR.rank),
                    element.GetFloatAttribute(ATTR.intensity));
            }

            /// <summary>
            /// The chrom infos of the compact format, which is the one encoding still read as
            /// chrom infos. They are not kept either: <see cref="TransitionResultsData"/> turns
            /// them into the columnar results and lets them go.
            /// </summary>
            private Results<TransitionChromInfo> ReadTransitionResults(XmlReader reader)
            {
                if (reader.IsStartElement(EL.results_data))
                {
                    string strContent = reader.ReadElementString();
                    byte[] data = Convert.FromBase64String(strContent);
                    var protoTransitionResults = new SkylineDocumentProto.Types.TransitionResults();
                    protoTransitionResults.MergeFrom(data);
                    return TransitionChromInfo.FromProtoTransitionResults(_moleculeReader.AnnotationScrubber, Settings, protoTransitionResults);
                }
                return null;
            }
        }

        /// <summary>
        /// Deserialize any explicitly set CE, DT, etc information from transition attributes
        /// </summary>
        private static ExplicitTransitionValues ReadExplicitTransitionValuesAttributes(XElement element, DocumentFormat formatVersion )
        {
            double? importedCollisionEnergy = element.GetNullableDoubleAttribute(ATTR.explicit_collision_energy);
            double? importedIonMobilityHighEnergyOffset =
                element.GetNullableDoubleAttribute(ATTR.explicit_drift_time_high_energy_offset_msec) ??
                element.GetNullableDoubleAttribute(ATTR.explicit_ion_mobility_high_energy_offset);
            double? importedSLens = element.GetNullableDoubleAttribute(formatVersion.CompareTo(DocumentFormat.VERSION_3_52) < 0 ? ATTR.s_lens_obsolete : ATTR.explicit_s_lens);
            double? importedConeVoltage = element.GetNullableDoubleAttribute(formatVersion.CompareTo(DocumentFormat.VERSION_3_52) < 0 ? ATTR.cone_voltage_obsolete : ATTR.explicit_cone_voltage);
            double? importedDeclusteringPotential = element.GetNullableDoubleAttribute(ATTR.explicit_declustering_potential);
            return ExplicitTransitionValues.Create(importedCollisionEnergy,
                importedIonMobilityHighEnergyOffset, importedSLens, importedConeVoltage, importedDeclusteringPotential);
        }

        /// <summary>
        /// Deserialize any explictly set CE, DT, etc information from precursor attributes
        /// </summary>
        private static ExplicitTransitionGroupValues ReadExplicitTransitionGroupValuesAttributes(XElement element, DocumentFormat formatVersion, out ExplicitTransitionValues pre422ExplicitValues)
        {
            double? importedCompensationVoltage = element.GetNullableDoubleAttribute(ATTR.explicit_compensation_voltage); // Found in older formats, obsolete as of 4.22. Now a combination of ion mobility and ion mobility units values.
            double? importedDriftTimeMsec = element.GetNullableDoubleAttribute(ATTR.explicit_drift_time_msec);
            var importedIonMobilityUnits = eIonMobilityUnits.none;
            if (importedDriftTimeMsec.HasValue)
            {
                importedIonMobilityUnits = eIonMobilityUnits.drift_time_msec;
            }
            else if (importedCompensationVoltage.HasValue)
            {
                importedIonMobilityUnits = eIonMobilityUnits.compensation_V;
            }
            else
            {
                var attr = element.GetAttribute(ATTR.explicit_ion_mobility_units);
                importedIonMobilityUnits = SmallMoleculeTransitionListReader.IonMobilityUnitsFromAttributeValue(attr);
            }
            double? importedIonMobility = importedDriftTimeMsec ?? importedCompensationVoltage ?? element.GetNullableDoubleAttribute(ATTR.explicit_ion_mobility);
            double? importedCCS = element.GetNullableDoubleAttribute(ATTR.explicit_ccs_sqa);
            pre422ExplicitValues = formatVersion >= DocumentFormat.VERSION_4_22 ? null : ReadExplicitTransitionValuesAttributes(element, formatVersion); // Formerly (pre-4.22) these per-transition values were serialized at peptide level
            // CollisionEnergy was made per-transition in 4.22, we added a per-precursor override in 20.12
            double? importedCollisionEnergy = pre422ExplicitValues?.CollisionEnergy ?? element.GetNullableDoubleAttribute(ATTR.explicit_collision_energy);
            if (pre422ExplicitValues != null)
            {
                pre422ExplicitValues = pre422ExplicitValues.ChangeCollisionEnergy(null); // As of 20.12 we're back to tracking this at precursor level (with per-transition overrides)
            }
            return ExplicitTransitionGroupValues.Create(importedCollisionEnergy, importedIonMobility, importedIonMobilityUnits, importedCCS);
        }

        /// <summary>
        /// Deserializes the <see cref="PeptideDocNode"/> of the element this reader was made for.
        /// </summary>
        /// <returns>A new <see cref="PeptideDocNode"/></returns>
        public PeptideDocNode ReadPeptideXml()
        {
            int? start = _element.GetNullableIntAttribute(ATTR.start);
            int? end = _element.GetNullableIntAttribute(ATTR.end);
            string sequence = _element.GetAttribute(ATTR.sequence);
            string lookupSequence = _element.GetAttribute(ATTR.lookup_sequence);
            // If the group has no sequence, then this is a v0.1 peptide list or a custom ion
            if (_peptideGroup.Sequence == null)
            {
                // Ignore the start and end values
                start = null;
                end = null;
            }
            int missedCleavages = _element.GetIntAttribute(ATTR.num_missed_cleavages);
            // CONSIDER: Trusted value
            int? rank = _element.GetNullableIntAttribute(ATTR.rank);
            double? concentrationMultiplier = _element.GetNullableDoubleAttribute(ATTR.concentration_multiplier);
            double? internalStandardConcentration =
                _element.GetNullableDoubleAttribute(ATTR.internal_standard_concentration);
            string normalizationMethod = _element.GetAttribute(ATTR.normalization_method);
            string attributeGroupId = _element.GetAttribute(ATTR.attribute_group_id);
            string surrogateCalibrationCurve = _element.GetAttribute(ATTR.surrogate_calibration_curve);
            bool autoManageChildren = _element.GetBoolAttribute(ATTR.auto_manage_children, true);
            bool isDecoy = _element.GetBoolAttribute(ATTR.decoy);
            var standardType = StandardType.FromName(_element.GetAttribute(ATTR.standard_type));
            double? importedRetentionTimeValue = _element.GetNullableDoubleAttribute(ATTR.explicit_retention_time);
            double? importedRetentionTimeWindow = _element.GetNullableDoubleAttribute(ATTR.explicit_retention_time_window);
            var importedRetentionTime = importedRetentionTimeValue.HasValue
                ? new ExplicitRetentionTimeInfo(importedRetentionTimeValue.Value, importedRetentionTimeWindow)
                : null;
            var annotations = Annotations.EMPTY;
            ExplicitMods mods = null, lookupMods = null;
            CrosslinkStructure crosslinkStructure = null;
            PeptideResults results = null;
            TransitionGroupDocNode[] children = null;
            Adduct adduct = Adduct.EMPTY;
            CustomMolecule customMolecule = null;
            if (IsCustomMolecule)
            {
                // Reads attributes only, so a reader standing on this element is all it needs.
                using (var moleculeReader = OpenReader(_element))
                {
                    customMolecule = CustomMolecule.Deserialize(moleculeReader, out adduct);
                }
            }

            Target chromatogramTarget = null;
            if (customMolecule != null)
            {
                if (DocumentMayContainMoleculesWithEmbeddedIons && customMolecule.ParsedMolecule.IsMassOnly && customMolecule.MonoisotopicMass.IsMassH())
                {
                    // Defined by mass only, assume it's not massH despite how it may have been written
                    customMolecule = new CustomMolecule(
                        customMolecule.MonoisotopicMass.ChangeIsMassH(false),
                        customMolecule.AverageMass.ChangeIsMassH(false),
                        customMolecule.Name);
                }
                // If user changed any molecule details (other than formula or mass) after chromatogram extraction, this info continues the target->chromatogram association
                var encodedChromatogramTarget = _element.GetAttribute(ATTR.chromatogram_target);
                if (!string.IsNullOrEmpty(encodedChromatogramTarget))
                {
                    chromatogramTarget = Target.FromSerializableString(encodedChromatogramTarget);
                }
            }
            Assume.IsTrue(DocumentMayContainMoleculesWithEmbeddedIons || adduct.IsEmpty); // Shouldn't be any charge info at the peptide/molecule level
            var peptide = IsCustomMolecule ?
                new Peptide(customMolecule) :
                new Peptide(_peptideGroup as FastaSequence, sequence, start, end, missedCleavages, isDecoy);
            if (IsCustomMolecule && DocumentMayContainMoleculesWithEmbeddedIons)
            {
                // If this is an older small molecule file, clean up any problems with former data
                // model. The handler works by reading ahead and handing back a reader over what it
                // made of the molecule, so what it made is loaded back to go on navigating.
                using (var pre372Reader = OpenReader(_element))
                {
                    var handled = new Pre372CustomIonTransitionGroupHandler(pre372Reader,
                        Settings.TransitionSettings.Instrument.MzMatchTolerance).Read(ref peptide);
                    _element = XElement.Load(handled);
                    // The handler pretty-prints what it made, and its indentation would otherwise
                    // arrive as text nodes standing between an _element and the children read below.
                    foreach (var indent in _element.DescendantNodes().OfType<XText>()
                                 .Where(text => text.Value.Trim().Length == 0).ToArray())
                    {
                        indent.Remove();
                    }
                }
            }

            // The annotations and the modifications read themselves out of a stream, and the
            // modifications are several sibling elements rather than one, so they share a reader
            // over this molecule. Nothing after them depends on where it ends up.
            using (var modsReader = OpenReader(_element))
            {
                modsReader.ReadStartElement();
                if (modsReader.IsStartElement())
                    annotations = ReadTargetAnnotations(modsReader, AnnotationDef.AnnotationTarget.peptide);
                if (!IsCustomMolecule)
                {
                    mods = ReadExplicitMods(modsReader, peptide)?.ConvertFromLegacyCrosslinkStructure();
                    SkipImplicitModsElement(modsReader);
                    lookupMods = ReadLookupMods(modsReader, lookupSequence);
                    crosslinkStructure = ReadCrosslinkStructure(modsReader);
                    if (crosslinkStructure != null && !crosslinkStructure.IsEmpty)
                    {
                        mods = mods ?? new ExplicitMods(peptide, null, null);
                        mods = mods.ChangeCrosslinkStructure(crosslinkStructure);
                    }
                }
            }

            results = ReadPeptideResults(_element.Element(EL.peptide_results));

            var precursorElements = _element.Elements(EL.precursor).ToArray();
            var selectedTransitions = _element.Element(EL.selected_transitions);
            if (precursorElements.Length > 0)
            {
                children = ReadTransitionGroupListXml(precursorElements, peptide, mods);
            }
            else if (selectedTransitions != null)
            {
                // Support for v0.1
                children = ReadUngroupedTransitionListXml(selectedTransitions, peptide, mods);
            }

            mods = mods?.RemoveLegacyCrosslinkMap();
            ModifiedSequenceMods sourceKey = null;
            if (lookupSequence != null)
                sourceKey = new ModifiedSequenceMods(lookupSequence, lookupMods);

            PeptideDocNode peptideDocNode = new PeptideDocNode(peptide, Settings, mods, sourceKey, standardType, rank,
                importedRetentionTime, annotations, results, children ?? new TransitionGroupDocNode[0], autoManageChildren);
            peptideDocNode = peptideDocNode
                .ChangeConcentrationMultiplier(concentrationMultiplier)
                .ChangeInternalStandardConcentration(internalStandardConcentration)
                .ChangeNormalizationMethod(NormalizationMethod.FromName(normalizationMethod))
                .ChangeAttributeGroupId(attributeGroupId)
                .ChangeSurrogateCalibrationCurve(surrogateCalibrationCurve)
                .ChangeOriginalMoleculeTarget(chromatogramTarget);

            return peptideDocNode;
        }

        private ExplicitMods ReadLookupMods(XmlReader reader, string lookupSequence)
        {
            if (!reader.IsStartElement(EL.lookup_modifications))
                return null;
            reader.Read();
            string sequence = FastaSequence.StripModifications(lookupSequence);
            var mods = ReadExplicitMods(reader, new Peptide(sequence));
            reader.ReadEndElement();
            return mods;
        }

        private CrosslinkStructure ReadCrosslinkStructure(XmlReader reader)
        {
            if (!reader.IsStartElement(EL.crosslinks))
            {
                return null;
            }
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return CrosslinkStructure.EMPTY;
            }
            reader.Read();
            var peptides = new List<Peptide>();
            var explicitModsList = new List<ExplicitMods>();
            while (reader.IsStartElement(EL.linked_peptide))
            {
                var peptide = new Peptide(reader.GetAttribute(ATTR.sequence));
                ExplicitMods explicitMods;
                if (reader.IsEmptyElement)
                {
                    explicitMods = null;
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();
                    explicitMods = ReadExplicitMods(reader, peptide);
                    reader.ReadEndElement();
                }
                peptides.Add(peptide);
                explicitModsList.Add(explicitMods);
            }

            var crosslinks = new List<Crosslink>();
            while (reader.IsStartElement(EL.crosslink))
            {
                var crosslinkName = reader.GetAttribute(ATTR.modification_name);
                StaticMod crosslinker =
                    Settings.PeptideSettings.Modifications.StaticModifications.FirstOrDefault(mod =>
                        mod.Name == crosslinkName);
                if (crosslinker == null)
                {
                    throw new InvalidDataException(string.Format(@"Crosslinker {0} not found.", crosslinkName));
                }
                List<CrosslinkSite> sites = new List<CrosslinkSite>();
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();
                    while (reader.IsStartElement(EL.site))
                    {
                        sites.Add(new CrosslinkSite(reader.GetIntAttribute(ATTR.peptide_index), reader.GetIntAttribute(ATTR.index_aa)));
                        reader.ReadStartElement();
                    }
                    crosslinks.Add(new Crosslink(crosslinker, sites));
                    reader.ReadEndElement();
                }
            }
            reader.ReadEndElement();
            return new CrosslinkStructure(peptides, explicitModsList, crosslinks);
        }

        private void SkipImplicitModsElement(XmlReader reader)
        {
            if (!reader.IsStartElement(EL.implicit_modifications))
                return;
            reader.Skip();
        }

        public ExplicitMods ReadExplicitMods(XmlReader reader, Peptide peptide)
        {
            IList<ExplicitMod> staticMods = null;
            TypedExplicitModifications staticTypedMods = null;
            IList<TypedExplicitModifications> listHeavyMods = null;
            bool isVariable = false;

            if (reader.IsStartElement(EL.variable_modifications))
            {
                staticTypedMods = ReadExplicitMods(reader, EL.variable_modifications,
                    EL.variable_modification, peptide, IsotopeLabelType.light);
                staticMods = staticTypedMods.Modifications;
                isVariable = true;
            }
            if (reader.IsStartElement(EL.explicit_modifications))
            {
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();

                    if (!isVariable)
                    {
                        if (reader.IsStartElement(EL.explicit_static_modifications))
                        {
                            staticTypedMods = ReadExplicitMods(reader, EL.explicit_static_modifications,
                                EL.explicit_modification, peptide, IsotopeLabelType.light);
                            staticMods = staticTypedMods.Modifications;
                        }
                        // For format version 0.2 and earlier it was not possible
                        // to have unmodified types.  The absence of a type simply
                        // meant it had no modifications.
                        else if (FormatVersion.CompareTo(DocumentFormat.VERSION_0_2) <= 0)
                        {
                            staticTypedMods = new TypedExplicitModifications(peptide,
                                IsotopeLabelType.light, new ExplicitMod[0]);
                            staticMods = staticTypedMods.Modifications;
                        }
                    }
                    listHeavyMods = new List<TypedExplicitModifications>();
                    while (reader.IsStartElement(EL.explicit_heavy_modifications))
                    {
                        var heavyMods = ReadExplicitMods(reader, EL.explicit_heavy_modifications,
                            EL.explicit_modification, peptide, IsotopeLabelType.heavy);
                        heavyMods = heavyMods.AddModMasses(staticTypedMods);
                        listHeavyMods.Add(heavyMods);
                    }
                    if (FormatVersion.CompareTo(DocumentFormat.VERSION_0_2) <= 0 && listHeavyMods.Count == 0)
                    {
                        listHeavyMods.Add(new TypedExplicitModifications(peptide,
                            IsotopeLabelType.heavy, new ExplicitMod[0]));
                    }

                    reader.ReadEndElement();
                }
            }
            if (staticMods == null && listHeavyMods == null)
                return null;

            listHeavyMods = (listHeavyMods != null ?
                listHeavyMods.ToArray() : new TypedExplicitModifications[0]);

            return new ExplicitMods(peptide, staticMods, listHeavyMods, isVariable);
        }

        private TypedExplicitModifications ReadExplicitMods(XmlReader reader, string name,
            string nameElMod, Peptide peptide, IsotopeLabelType labelTypeDefault)
        {
            if (!reader.IsStartElement(name))
                return new TypedExplicitModifications(peptide, labelTypeDefault, new ExplicitMod[0]);

            var typedMods = ReadLabelType(reader, labelTypeDefault);
            var listMods = new List<ExplicitMod>();

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                while (reader.IsStartElement(nameElMod))
                {
                    int indexAA = reader.GetIntAttribute(ATTR.index_aa);
                    string nameMod = reader.GetAttribute(ATTR.modification_name);
                    int indexMod = typedMods.Modifications.IndexOf(mod => Equals(nameMod, mod.Name));
                    if (indexMod == -1)
                        throw new InvalidDataException(string.Format(Resources.TransitionInfo_ReadTransitionLosses_No_modification_named__0__was_found_in_this_document, nameMod));
                    StaticMod modAdd = typedMods.Modifications[indexMod];
                    var explicitMod = new ExplicitMod(indexAA, modAdd);
                    if (reader.IsEmptyElement)
                    {
                        // Consume tag
                        reader.Read();
                    }
                    else
                    {
                        reader.Read();
                        explicitMod = explicitMod.ChangeLinkedPeptide(ReadLinkedPeptide(reader));
                        reader.ReadEndElement();
                    }

                    listMods.Add(explicitMod);
                }
                reader.ReadEndElement();
            }
            return new TypedExplicitModifications(peptide, typedMods.LabelType, listMods.ToArray());
        }

        private LegacyLinkedPeptide ReadLinkedPeptide(XmlReader reader)
        {
            if (!reader.IsStartElement(EL.linked_peptide))
            {
                return null;
            }

            int indexAa = reader.GetIntAttribute(ATTR.index_aa);
            var sequence = reader.GetAttribute(ATTR.sequence);
            Peptide peptide = null;
            if (!string.IsNullOrEmpty(sequence))
            {
                peptide = new Peptide(sequence);
            }
            ExplicitMods explicitMods = null;
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();
                explicitMods = ReadExplicitMods(reader, peptide);
                reader.ReadEndElement();
            }
            return new LegacyLinkedPeptide(peptide, indexAa, explicitMods);

        }

        /// <summary>
        /// The two values a molecule keeps, read straight into a <see cref="PeptideResults"/>. A
        /// document written the old way records the peak count ratio and the retention time here
        /// too, but both are aggregated from the precursors and worked out again on demand, so they
        /// are not even read. Null when the molecule has neither value, which is the usual case.
        /// </summary>
        private PeptideResults ReadPeptideResults(XElement element)
        {
            if (element == null)
            {
                return null;
            }

            var measuredResults = Settings.MeasuredResults;
            if (measuredResults == null)
                throw new InvalidDataException(SerializationResources.SrmDocument_ReadResults_No_results_information_found_in_the_document_settings);

            int replicateCount = measuredResults.Chromatograms.Count;
            var fileIdsByReplicate = new List<ChromFileInfoId>[replicateCount];
            var excludeByReplicate = new List<bool>[replicateCount];
            var concentrationByReplicate = new List<double?>[replicateCount];
            bool anythingToKeep = false;

            ChromatogramSet chromatogramSet = null;
            int index = -1;
            foreach (var resultElement in element.Elements(EL.peptide_result))
            {
                string name = resultElement.GetAttribute(ATTR.replicate);
                if (chromatogramSet == null || !Equals(name, chromatogramSet.Name))
                {
                    if (!measuredResults.TryGetChromatogramSet(name, out chromatogramSet, out index))
                        throw new InvalidDataException(string.Format(SerializationResources.SrmDocument_ReadResults_No_replicate_named__0__found_in_measured_results, name));
                }

                string fileId = resultElement.GetAttribute(ATTR.file);
                var fileInfoId = fileId != null
                    ? chromatogramSet.FindFileById(fileId)
                    : chromatogramSet.MSDataFileInfos[0].FileId;
                if (fileInfoId == null)
                    throw new InvalidDataException(string.Format(SerializationResources.SrmDocument_ReadResults_No_file_with_id__0__found_in_the_replicate__1__, fileId, name));

                bool exclude = resultElement.GetBoolAttribute(ATTR.exclude_from_calibration);
                double? concentration = resultElement.GetNullableDoubleAttribute(ATTR.analyte_concentration);

                (fileIdsByReplicate[index] = fileIdsByReplicate[index] ?? new List<ChromFileInfoId>()).Add(fileInfoId);
                (excludeByReplicate[index] = excludeByReplicate[index] ?? new List<bool>()).Add(exclude);
                (concentrationByReplicate[index] = concentrationByReplicate[index] ?? new List<double?>())
                    .Add(concentration);
                anythingToKeep = anythingToKeep || exclude || concentration.HasValue;
            }

            if (!anythingToKeep)
            {
                return null;
            }

            var fileIds = new List<ChromFileInfoId>();
            var counts = new List<int>();
            var excludeFromCalibration = new List<bool>();
            var analyteConcentrations = new List<double?>();
            for (int replicateIndex = 0; replicateIndex < replicateCount; replicateIndex++)
            {
                counts.Add(fileIdsByReplicate[replicateIndex]?.Count ?? 0);
                if (fileIdsByReplicate[replicateIndex] == null)
                    continue;
                fileIds.AddRange(fileIdsByReplicate[replicateIndex]);
                excludeFromCalibration.AddRange(excludeByReplicate[replicateIndex]);
                analyteConcentrations.AddRange(concentrationByReplicate[replicateIndex]);
            }

            // Two independent maps, each keeping only the files something was set for, and null when
            // that is none of them.
            var chromFileIds = new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds);
            var results = PeptideResults.EMPTY
                .ChangeExcludeFromCalibration(new ChromFileIdMap<bool>(chromFileIds, excludeFromCalibration))
                .ChangeAnalyteConcentrations(new ChromFileIdMap<double?>(chromFileIds, analyteConcentrations));
            return results.NullIfEmpty();
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionGroupDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="peptide">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <returns>A new array of <see cref="TransitionGroupDocNode"/></returns>
        private TransitionGroupDocNode[] ReadTransitionGroupListXml(IEnumerable<XElement> precursorElements,
            Peptide peptide, ExplicitMods mods)
        {
            return precursorElements.Select(element => ReadTransitionGroupXml(element, peptide, mods)).ToArray();
        }

        private TransitionGroupDocNode ReadTransitionGroupXml(XElement element, Peptide peptide, ExplicitMods mods)
        {
            var precursorCharge = element.GetIntAttribute(ATTR.charge);
            var precursorAdduct = Adduct.FromChargeProtonated(precursorCharge);  // Read integer charge
            var typedMods = ReadLabelType(element, IsotopeLabelType.light);

            int? decoyMassShift = element.GetNullableIntAttribute(ATTR.decoy_mass_shift);
            var explicitTransitionGroupValues = ReadExplicitTransitionGroupValuesAttributes(element, FormatVersion, out var pre422ExplicitValues);
            if (peptide.IsCustomMolecule)
            {
                var ionFormula = element.GetAttribute(ATTR.ion_formula);
                if (ionFormula != null)
                {
                    ionFormula = ionFormula.Trim(); // We've seen trailing spaces in the wild
                }
                string neutralFormula;
                Adduct adduct;
                var isFormulaWithAdduct = IonInfo.IsFormulaWithAdduct(ionFormula, out var _, out adduct, out neutralFormula);
                if (isFormulaWithAdduct)
                {
                    precursorAdduct = adduct;
                }
                else
                {
                    Assume.Fail(@"Unable to determine adduct in " + ionFormula);
                }
                if (!string.IsNullOrEmpty(neutralFormula))
                {
                    var ion = precursorAdduct.ApplyToFormula(neutralFormula);
                    var moleculeWithAdduct = precursorAdduct.ApplyToMolecule(peptide.CustomMolecule.ParsedMolecule);
                    Assume.IsTrue(ion.CompareTolerant(moleculeWithAdduct, BioMassCalc.MassTolerance) == 0, @"Expected precursor ion formula to match parent molecule with adduct applied");
                }
            }
            var group = new TransitionGroup(peptide, precursorAdduct, typedMods.LabelType, false, decoyMassShift);
            var children = new TransitionDocNode[0];    // Empty until proven otherwise
            bool autoManageChildren = element.GetBoolAttribute(ATTR.auto_manage_children, true);
            double? precursorConcentration = element.GetNullableDoubleAttribute(ATTR.precursor_concentration);

            TransitionGroupDocNode nodeGroup;
            if (!element.HasElements)
            {
                nodeGroup = new TransitionGroupDocNode(group,
                                                  Annotations.EMPTY,
                                                  Settings,
                                                  mods,
                                                  null,
                                                  explicitTransitionGroupValues,
                                                  null,
                                                  children,
                                                  autoManageChildren);
            }
            else
            {
                Annotations annotations;
                SpectrumClassFilter spectrumClassFilter;
                SpectrumHeaderInfo libInfo;
                // The three things at the head of a precursor read themselves out of a stream, so
                // they share a reader over it. Everything after them is found by name.
                using (var headerReader = OpenReader(element))
                {
                    headerReader.ReadStartElement();
                    annotations = ReadTargetAnnotations(headerReader, AnnotationDef.AnnotationTarget.precursor);
                    spectrumClassFilter = SpectrumClassFilter.ReadXml(headerReader);
                    libInfo = ReadTransitionGroupLibInfo(headerReader);
                }

                var precursorResults = element.Element(EL.precursor_results);
                var columnarResults = ReadPrecursorResults(precursorResults, out var sharedTransitionAreas);

                nodeGroup = new TransitionGroupDocNode(group,
                                                  annotations,
                                                  Settings,
                                                  mods,
                                                  libInfo,
                                                  explicitTransitionGroupValues,
                                                  // Empty, and only for the replicate count: a
                                                  // precursor keeps no chrom infos of its own.
                                                  columnarResults == null
                                                      ? null
                                                      : Settings.MeasuredResults.EmptyTransitionGroupResults,
                                                  children,
                                                  autoManageChildren);
                if (!spectrumClassFilter.IsEmpty)
                {
                    nodeGroup = nodeGroup.ChangeSpectrumClassFilter(spectrumClassFilter);
                }

                // Which of the two things a transition's element can hold, and what it says unless
                // it says otherwise, are both on the precursor's peaks - which are simply handed
                // over, since an element can be read alongside another rather than only before it.
                children = ReadTransitionListXml(element, nodeGroup, mods, pre422ExplicitValues,
                    precursorResults, out var transitionResults);
                transitionResults = ApplySharedTransitionAreas(transitionResults, sharedTransitionAreas);

                nodeGroup = (TransitionGroupDocNode)nodeGroup.ChangeChildrenChecked(children);

                // After the children, since replacing them is what discards the columnar results
                // derived from whatever was there before, and since the transitions' results are
                // stored by the index of the transition among them.
                if (columnarResults != null)
                    nodeGroup = nodeGroup.ChangeAbbreviatedResults(columnarResults);
                if (transitionResults.Any(results => results != null))
                {
                    // Taken from the precursor rather than made here, because giving results the
                    // transitions they belong to is the precursor's job and nothing else's.
                    if (!nodeGroup.HasAbbreviatedResults)
                        nodeGroup = nodeGroup.ChangeAbbreviatedResults(TransitionGroupResults.Empty);
                    var groupResults = nodeGroup.AbbreviatedResults;
                    for (int iTran = 0; iTran < transitionResults.Length; iTran++)
                        groupResults = transitionResults[iTran]?.AddTo(groupResults, ((TransitionDocNode) children[iTran]).Transition) ?? groupResults;
                    nodeGroup = nodeGroup.ChangeAbbreviatedResults(groupResults);
                }
            }
            nodeGroup = nodeGroup.ChangePrecursorConcentration(precursorConcentration);
            return nodeGroup;
        }

        private TypedModifications ReadLabelType(XElement element, IsotopeLabelType labelTypeDefault)
        {
            return ReadLabelType(element.GetAttribute(ATTR.isotope_label), labelTypeDefault);
        }

        /// <summary>
        /// See <see cref="ReadLabelType(XElement,IsotopeLabelType)"/>. The modifications still read
        /// themselves out of a stream, so they ask with the name they have in hand.
        /// </summary>
        private TypedModifications ReadLabelType(XmlReader reader, IsotopeLabelType labelTypeDefault)
        {
            return ReadLabelType(reader.GetAttribute(ATTR.isotope_label), labelTypeDefault);
        }

        private TypedModifications ReadLabelType(string typeName, IsotopeLabelType labelTypeDefault)
        {
            if (string.IsNullOrEmpty(typeName))
                typeName = labelTypeDefault.Name;
            var typedMods = Settings.PeptideSettings.Modifications.GetModificationsByName(typeName);
            if (typedMods == null)
                throw new InvalidDataException(string.Format(Resources.SrmDocument_ReadLabelType_The_isotope_modification_type__0__does_not_exist_in_the_document_settings, typeName));
            return typedMods;
        }

        /// <summary>
        /// The precursor's columnar results, or null when it has none. Read out of the same
        /// element a document has always had, in either of the two things which can be on it.
        /// <para>
        /// A document written before <see cref="ATTR.chosen_peak_index"/> was part of the format
        /// carries everything about each peak here. Nearly all of it is read back from the .skyd
        /// once the peak has been matched to a candidate peak there, so only what the .skyd cannot
        /// give back is kept, and <see cref="TransitionGroupResults.NeedsPeakIndexes"/> says the
        /// matching still has to be done. Nothing becomes a
        /// <see cref="TransitionGroupChromInfo"/>: the peaks are treated as though their
        /// boundaries were set by hand until the .skyd says otherwise.
        /// </para>
        /// </summary>
        private TransitionGroupResults ReadPrecursorResults(XElement precursorResults,
            out SharedTransitionAreas sharedTransitionAreas)
        {
            sharedTransitionAreas = null;
            if (precursorResults == null)
            {
                return null;
            }

            var areasByPosition = new List<float[]>();
            var truncatedByPosition = new List<bool?>();
            var forcedIntegrationByPosition = new List<bool>();
            var peaks = new List<PrecursorPeak>();
            var qValues = new List<float>();
            var zScores = new List<float>();
            var userSets = new List<UserSet>();
            var annotations = new List<Annotations>();
            bool needsPeakIndexes = false;
            var chromFileIds = ReadColumnarResults(precursorResults, EL.precursor_peak, (r, fileInfoId) =>
            {
                int? chosenPeakIndex = r.GetNullableIntAttribute(ATTR.chosen_peak_index);
                needsPeakIndexes = needsPeakIndexes || !chosenPeakIndex.HasValue;
                peaks.Add(new PrecursorPeak(r.GetFloatAttribute(ATTR.retention_time),
                    r.GetNullableFloatAttribute(ATTR.start_time) ?? 0,
                    r.GetNullableFloatAttribute(ATTR.end_time) ?? 0,
                    chosenPeakIndex ?? PrecursorPeak.NO_PEAK_INDEX));
                float? qValue = r.GetNullableFloatAttribute(ATTR.qvalue);
                float? zScore = r.GetNullableFloatAttribute(ATTR.zscore);
                userSets.Add(ReadUserSet(r));
                areasByPosition.Add(ReadTransitionAreas(r));
                // What the transitions below say unless they say otherwise - see
                // MoleculeWriter.SetColumnarPrecursorPeak. Named apart from the precursor's own
                // ATTR.truncated, which on a peak that kept chrom infos is something else
                // entirely: how many of its transitions were truncated, which is a count.
                bool? truncated = ReadTruncated(r, ATTR.transition_truncated, false);
                bool forcedIntegration = r.GetBoolAttribute(ATTR.transition_forced_integration, false);
                truncatedByPosition.Add(truncated);
                forcedIntegrationByPosition.Add(forcedIntegration);

                var peakAnnotations = ReadPositionAnnotations(r, AnnotationDef.AnnotationTarget.precursor_result);
                // The scores were annotations before they were attributes, and a document old
                // enough to have them that way is exactly one being upgraded here.
                peakAnnotations = ReadAndRemoveScoreAnnotation(peakAnnotations,
                    MProphetResultsHandler.AnnotationName, ref qValue);
                peakAnnotations = ReadAndRemoveScoreAnnotation(peakAnnotations,
                    MProphetResultsHandler.MAnnotationName, ref zScore);
                annotations.Add(peakAnnotations);
                qValues.Add(qValue ?? float.NaN);
                zScores.Add(zScore ?? float.NaN);
            });
            sharedTransitionAreas = areasByPosition.Any(positionAreas => positionAreas != null)
                ? new SharedTransitionAreas(chromFileIds, areasByPosition, truncatedByPosition,
                    forcedIntegrationByPosition)
                : null;
            return new TransitionGroupResults(chromFileIds, peaks)
                .ChangeUserSets(userSets)
                .ChangeQValues(qValues)
                .ChangeZScores(zScores)
                .ChangeAnnotations(annotations)
                .ChangeNeedsPeakIndexes(needsPeakIndexes);
        }

        /// <summary>
        /// Reads the columnar results out of the peak elements a document has always had. One entry
        /// per replicate and file, in that order, which is what makes the flat positions.
        /// <para>
        /// One entry per file, not per element. A document written before the chosen peak indexes
        /// were part of the format has an element for every optimization step, and a cache
        /// corruption issue could write the same one twice. Nothing kept here can differ between
        /// the steps of one file, so the first element a file has is the one that counts.
        /// </para>
        /// </summary>
        private ChromFileIds ReadColumnarResults(XElement resultsElement, string peakStart,
            Action<XElement, ChromFileInfoId> readPeak)
        {
            var results = Settings.MeasuredResults;
            if (results == null)
                throw new InvalidDataException(SerializationResources.SrmDocument_ReadResults_No_results_information_found_in_the_document_settings);

            var counts = new int[results.Chromatograms.Count];
            var fileIds = new List<ChromFileInfoId>();
            if (resultsElement == null)
            {
                return new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds);
            }

            ChromatogramSet chromatogramSet = null;
            int index = -1;
            int replicateStart = 0;
            foreach (var peakElement in resultsElement.Elements(peakStart))
            {
                string name = peakElement.GetAttribute(ATTR.replicate);
                if (chromatogramSet == null || !Equals(name, chromatogramSet.Name))
                {
                    if (!results.TryGetChromatogramSet(name, out chromatogramSet, out index))
                        throw new InvalidDataException(String.Format(SerializationResources.SrmDocument_ReadResults_No_replicate_named__0__found_in_measured_results, name));
                    replicateStart = fileIds.Count;
                }

                string fileId = peakElement.GetAttribute(ATTR.file);
                var fileInfoId = fileId != null
                    ? chromatogramSet.FindFileById(fileId)
                    : chromatogramSet.MSDataFileInfos[0].FileId;
                if (fileInfoId == null)
                    throw new InvalidDataException(String.Format(SerializationResources.SrmDocument_ReadResults_No_file_with_id__0__found_in_the_replicate__1__, fileId, name));

                if (peakElement.GetIntAttribute(ATTR.step) != 0 || HasFile(fileIds, replicateStart, fileInfoId))
                {
                    continue;
                }

                // Whatever the element also holds that nothing here wants - the original and
                // reintegrated peaks of the older format - is simply not asked for, rather than
                // skipped past.
                readPeak(peakElement, fileInfoId);

                fileIds.Add(fileInfoId);
                counts[index]++;
            }

            return new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds);
        }

        /// <summary>
        /// Whether one replicate's entries, which start at <paramref name="replicateStart"/>,
        /// already include a file.
        /// </summary>
        private static bool HasFile(IList<ChromFileInfoId> fileIds, int replicateStart, ChromFileInfoId fileInfoId)
        {
            for (int i = replicateStart; i < fileIds.Count; i++)
            {
                if (ReferenceEquals(fileIds[i], fileInfoId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What was read for one transition, held until the precursor which owns its results has
        /// its children and can be told. Either the columnar values or the chrom infos a document
        /// written the old way carries, never both.
        /// </summary>
        private class TransitionResultsData
        {
            public TransitionResultsData(ChromFileIds chromFileIds, IList<TransitionPeak> peaks,
                IList<Annotations> annotations, IList<CustomPeakBounds> peakBounds,
                IList<CustomPeakMetrics> peakMetrics)
            {
                ChromFileIds = chromFileIds;
                Peaks = peaks;
                Annotations = annotations;
                PeakBounds = peakBounds;
                PeakMetrics = peakMetrics;
            }

            public TransitionResultsData(Results<TransitionChromInfo> chromInfos)
            {
                ChromInfos = chromInfos;
            }

            private ChromFileIds ChromFileIds { get; }
            private IList<TransitionPeak> Peaks { get; }
            private IList<Annotations> Annotations { get; }
            private IList<CustomPeakBounds> PeakBounds { get; }
            private IList<CustomPeakMetrics> PeakMetrics { get; }
            private Results<TransitionChromInfo> ChromInfos { get; }

            /// <summary>
            /// These results with the areas the precursor carried filled in at the files this
            /// transition said nothing about, which is every file where all of the precursor's
            /// transitions had nothing to say beyond their area.
            /// <para>
            /// Matched by file rather than by counting positions, and an entry of the transition's
            /// own wins. That is what reads a document whose transitions were written at every
            /// file, back when a transition was either left out of the precursor's areas
            /// altogether or written out in full, the same way it was written.
            /// </para>
            /// </summary>
            public TransitionResultsData WithSharedAreas(SharedTransitionAreas shared, int transitionIndex)
            {
                if (ChromInfos != null)
                {
                    // A document old enough to keep chrom infos has no shared areas to fill in.
                    return this;
                }

                // Two layouts, so replicate and file are the only way across: this transition's
                // positions are its own and the precursor's are the precursor's. The maps are put
                // on one layout rather than one being indexed with the other's positions.
                var chromFileIds = ChromFileIds.Union(shared.ChromFileIds);
                var written = new ChromFileIdMap<TransitionPeak?>(ChromFileIds,
                        Peaks.Select(peak => (TransitionPeak?) peak))
                    .WithFileIds(chromFileIds);
                var writtenAnnotations = MapOnto(Annotations, chromFileIds);
                var writtenPeakBounds = MapOnto(PeakBounds.Select(bounds => (CustomPeakBounds?) bounds), chromFileIds);
                var writtenPeakMetrics = MapOnto(PeakMetrics, chromFileIds);
                var sharedAreas = new ChromFileIdMap<float[]>(shared.ChromFileIds, shared.AreasByPosition)
                    .WithFileIds(chromFileIds);
                var sharedTruncated = new ChromFileIdMap<bool?>(shared.ChromFileIds, shared.TruncatedByPosition)
                    .WithFileIds(chromFileIds);
                var sharedForced = new ChromFileIdMap<bool>(shared.ChromFileIds, shared.ForcedIntegrationByPosition)
                    .WithFileIds(chromFileIds);

                var fileIds = new List<ChromFileInfoId>();
                var counts = new List<int>();
                var peaks = new List<TransitionPeak>();
                var annotations = new List<Annotations>();
                var peakBounds = new List<CustomPeakBounds>();
                var peakMetrics = new List<CustomPeakMetrics>();
                for (int replicateIndex = 0;
                     replicateIndex < chromFileIds.ReplicatePositions.ReplicateCount;
                     replicateIndex++)
                {
                    int count = 0;
                    foreach (var fileId in chromFileIds.GetFileIds(replicateIndex))
                    {
                        sharedAreas.TryGetValue(replicateIndex, fileId, out var areas);
                        float? sharedArea = areas == null ? (float?) null : areas[transitionIndex];
                        if (written.TryGetValue(replicateIndex, fileId, out var peak) && peak.HasValue)
                        {
                            // The area is the precursor's wherever it carries one: the element was
                            // written without it, so what came off the element is nothing.
                            peaks.Add(sharedArea.HasValue
                                ? peak.Value.ChangeArea(sharedArea.Value)
                                : peak.Value);
                            annotations.Add(Get(writtenAnnotations, replicateIndex, fileId) ??
                                            pwiz.Skyline.Model.Annotations.EMPTY);
                            peakBounds.Add(Get(writtenPeakBounds, replicateIndex, fileId) ?? default);
                            peakMetrics.Add(Get(writtenPeakMetrics, replicateIndex, fileId));
                        }
                        else if (sharedArea.HasValue)
                        {
                            // No element at all means nothing beyond the area and the flags the
                            // precursor carried, and MakePlainPeak is what a peak which says only
                            // that looks like.
                            sharedTruncated.TryGetValue(replicateIndex, fileId, out var truncated);
                            sharedForced.TryGetValue(replicateIndex, fileId, out var forcedIntegration);
                            peaks.Add(TransitionGroupResults.MakePlainPeak(sharedArea.Value, truncated,
                                forcedIntegration));
                            annotations.Add(pwiz.Skyline.Model.Annotations.EMPTY);
                            peakBounds.Add(default);
                            peakMetrics.Add(null);
                        }
                        else
                        {
                            continue;
                        }

                        fileIds.Add(fileId);
                        count++;
                    }

                    counts.Add(count);
                }

                return new TransitionResultsData(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds),
                    peaks, annotations, peakBounds, peakMetrics);
            }

            /// <summary>
            /// One of the lists read alongside the peaks, on <paramref name="chromFileIds"/> instead
            /// of this transition's own layout.
            /// </summary>
            private ChromFileIdMap<T> MapOnto<T>(IEnumerable<T> values, ChromFileIds chromFileIds)
            {
                return values == null
                    ? null
                    : new ChromFileIdMap<T>(ChromFileIds, values).WithFileIds(chromFileIds);
            }

            private static T Get<T>(ChromFileIdMap<T> map, int replicateIndex, ChromFileInfoId fileId)
            {
                return map != null && map.TryGetValue(replicateIndex, fileId, out var value) ? value : default;
            }

            /// <summary>
            /// The precursor's results with this transition's added at
            /// <paramref name="transitionIndex"/>.
            /// </summary>
            public TransitionGroupResults AddTo(TransitionGroupResults groupResults, Transition transition)
            {
                return ChromInfos != null
                    // The chrom infos are not kept: everything a peak would lose with them goes on
                    // the transition's results instead, until the .skyd says which candidate peak
                    // it is and gives the rest back.
                    ? groupResults.ChangeTransitionFromChromInfos(transition, ChromInfos)
                    : groupResults.ChangeTransitionResults(transition, ChromFileIds, Peaks, Annotations,
                        PeakBounds, PeakMetrics);
            }
        }

        /// <summary>
        /// One transition's peaks, out of the same element a document has always had. The three
        /// sparse values are read as one entry per position alongside the peaks, and it is
        /// <see cref="TransitionGroupResults"/> which turns each of them into a map of its own
        /// holding only the entries which say something.
        /// <para>
        /// The two formats put different things on the element. A document written knowing the
        /// chosen peak indexes writes an entry only for what its precursor does not already say, so
        /// boundaries here are ones the transition does not share. A document written before that
        /// writes everything about the peak, so its boundaries are just where the peak is - which
        /// is the precursor's own unless this transition disagrees, and
        /// <see cref="TransitionGroupResults.ChangeTransitionResults"/> drops the ones which agree.
        /// </para>
        /// </summary>
        private TransitionResultsData ReadColumnarTransitionResults(XElement transitionResults,
            PrecursorPeaks precursorPeaks)
        {
            bool isLegacyShape = precursorPeaks.IsLegacyShape;
            var peaks = new List<TransitionPeak>();
            var annotations = new List<Annotations>();
            var peakBounds = new List<CustomPeakBounds>();
            var peakMetrics = new List<CustomPeakMetrics>();
            var chromFileIds = ReadColumnarResults(transitionResults, EL.transition_peak, (r, fileInfoId) =>
            {
                // Protect against negative areas, since they can cause real problems for ratio
                // calculations.
                float area = Math.Max(0, r.GetFloatAttribute(ATTR.area));
                // Left out of the element when it is the same as the precursor's for this file,
                // which is nearly always, so the precursor's is what it means when it is absent.
                var userSet = ReadUserSet(r);
                var identified = r.GetEnumAttribute(ATTR.identified, PeakIdentificationFastLookup.Dict,
                    PeakIdentification.FALSE, XmlUtil.EnumCase.upper);
                float? startTime = r.GetNullableFloatAttribute(ATTR.start_time);
                float? endTime = r.GetNullableFloatAttribute(ATTR.end_time);
                float? massError = r.GetNullableFloatAttribute(ATTR.mass_error_ppm);

                // The flags are written by both shapes - see DocumentWriter.WriteTransitionResults -
                // so they are read the same way from either. Whether the peak is empty is the one
                // they say differently: the older shape says it by having no end time, while the
                // columnar shape uses the end time for boundaries the user set and so says this
                // outright.
                bool isEmpty = isLegacyShape
                    ? endTime.GetValueOrDefault() == 0
                    : r.GetBoolAttribute(ATTR.empty, false);
                // In the columnar shape these two say only what the precursor's peak for this file
                // does not already say, so what it says is what leaving them off means. The older
                // shape has no such thing: there, absent truncation means it was never worked out.
                bool? truncated;
                bool forcedIntegration;
                if (isLegacyShape)
                {
                    truncated = r.GetNullableBoolAttribute(ATTR.truncated);
                    forcedIntegration = r.GetBoolAttribute(ATTR.forced_integration, false);
                }
                else
                {
                    // The precursor's peak for the same file, which is simply looked up: it is a
                    // sibling element still sitting there, not something that had to be remembered
                    // on the way past.
                    var precursorPeak = precursorPeaks.Find(r);
                    truncated = ReadTruncated(r, ATTR.truncated,
                        ReadTruncated(precursorPeak, ATTR.transition_truncated, false));
                    forcedIntegration = r.GetBoolAttribute(ATTR.forced_integration,
                        precursorPeak.GetBoolAttribute(ATTR.transition_forced_integration, false));
                }

                peaks.Add(new TransitionPeak(area, userSet, truncated, isEmpty, identified, forcedIntegration));

                peakBounds.Add(startTime.HasValue && endTime.HasValue
                    ? new CustomPeakBounds(startTime.Value, endTime.Value)
                    : default);
                peakMetrics.Add(CustomPeakMetrics.Create(massError, identified));

                // Last, because these are the element's child elements.
                annotations.Add(ReadPositionAnnotations(r, AnnotationDef.AnnotationTarget.transition_result));
            });
            return new TransitionResultsData(chromFileIds, peaks, annotations, peakBounds, peakMetrics);
        }

        /// <summary>
        /// The transition areas a precursor carries for the transitions which had nothing else to
        /// say, so were not written at all. See <see cref="DocumentWriter"/>.
        /// </summary>
        private class SharedTransitionAreas
        {
            public SharedTransitionAreas(ChromFileIds chromFileIds, IList<float[]> areasByPosition,
                IList<bool?> truncatedByPosition, IList<bool> forcedIntegrationByPosition)
            {
                ChromFileIds = chromFileIds;
                AreasByPosition = areasByPosition;
                TruncatedByPosition = truncatedByPosition;
                ForcedIntegrationByPosition = forcedIntegrationByPosition;
            }

            public ChromFileIds ChromFileIds { get; }
            public IList<float[]> AreasByPosition { get; }

            /// <summary>
            /// What each position's peak said about the transitions it carries areas for, which is
            /// everything a transition left out of the document has beyond its area.
            /// </summary>
            public IList<bool?> TruncatedByPosition { get; }
            public IList<bool> ForcedIntegrationByPosition { get; }

            /// <summary>
            /// The results of one transition, or null when the precursor carried nothing for it,
            /// which means it was written out on its own.
            /// </summary>
            public TransitionResultsData MakeTransitionResults(int transitionIndex)
            {
                var replicatePositions = ChromFileIds.ReplicatePositions;
                var fileIds = new List<ChromFileInfoId>();
                var counts = new List<int>();
                var peaks = new List<TransitionPeak>();
                for (int replicateIndex = 0; replicateIndex < replicatePositions.ReplicateCount; replicateIndex++)
                {
                    int count = 0;
                    foreach (int position in replicatePositions[replicateIndex])
                    {
                        if (AreasByPosition[position] == null)
                        {
                            continue;
                        }

                        fileIds.Add(ChromFileIds.FileIds[position]);
                        // A transition is only left out when every one of its peaks said nothing
                        // beyond its area and the flags its precursor carried, and MakePlainPeak is
                        // what each of them said.
                        peaks.Add(TransitionGroupResults.MakePlainPeak(
                            AreasByPosition[position][transitionIndex], TruncatedByPosition[position],
                            ForcedIntegrationByPosition[position]));
                        count++;
                    }

                    counts.Add(count);
                }

                if (peaks.Count == 0)
                {
                    return null;
                }

                return new TransitionResultsData(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds),
                    peaks.ToArray(), null, null, null);
            }
        }

        /// <summary>
        /// The transitions' results, with the areas the precursor wrote once for all of them filled
        /// in at every file where none of them had anything of its own to say.
        /// <para>
        /// Filled in file by file rather than transition by transition: a transition which wrote
        /// nothing anywhere has no element at all, and one which wrote at some files still needs
        /// the precursor's areas at the rest.
        /// </para>
        /// </summary>
        private static TransitionResultsData[] ApplySharedTransitionAreas(TransitionResultsData[] transitionResults,
            SharedTransitionAreas sharedTransitionAreas)
        {
            if (sharedTransitionAreas == null)
            {
                return transitionResults;
            }

            var resultsNew = new TransitionResultsData[transitionResults.Length];
            for (int iTran = 0; iTran < transitionResults.Length; iTran++)
            {
                resultsNew[iTran] = transitionResults[iTran] == null
                    ? sharedTransitionAreas.MakeTransitionResults(iTran)
                    : transitionResults[iTran].WithSharedAreas(sharedTransitionAreas, iTran);
            }

            return resultsNew;
        }

        private static float[] ReadTransitionAreas(XElement element)
        {
            return element.GetFloatsAttribute(ATTR.transition_areas);
        }

        /// <summary>
        /// Truncation in the columnar shape, where leaving the attribute off means
        /// <paramref name="defaultValue"/> and truncation which was never worked out has a value of
        /// its own - see <see cref="DocumentSerializer.TRUNCATED_UNKNOWN"/>.
        /// </summary>
        private static bool? ReadTruncated(XElement element, string name, bool? defaultValue)
        {
            string value = element.GetAttribute(name);
            if (value == null)
            {
                return defaultValue;
            }

            return Equals(value, TRUNCATED_UNKNOWN) ? (bool?) null : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static UserSet ReadUserSet(XElement element)
        {
            return element.GetEnumAttribute(ATTR.user_set, UserSetFastLookup.Dict, UserSet.FALSE,
                XmlUtil.EnumCase.upper);
        }

        /// <summary>
        /// The annotations of one peak, which are child elements of its columnar results element.
        /// </summary>
        private Annotations ReadPositionAnnotations(XElement element, AnnotationDef.AnnotationTarget annotationTarget)
        {
            return element.HasElements ? ReadTargetAnnotations(element, annotationTarget) : Annotations.EMPTY;
        }

        /// <summary>
        /// Deserializes ungrouped transitions in v0.1 format from a <see cref="XmlReader"/>
        /// into an array of <see cref="TransitionGroupDocNode"/> objects with
        /// children <see cref="TransitionDocNode"/> from the XML correctly distributed.
        /// 
        /// There were no "heavy" transitions in v0.1, making this a matter of
        /// distributing multiple precursor charge states, though in most cases
        /// there will be only one.
        /// </summary>
        /// <param name="reader">The reader positioned on a &lt;transition&gt; start tag</param>
        /// <param name="peptide">A previously read <see cref="Peptide"/> instance</param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <returns>An array of <see cref="TransitionGroupDocNode"/> instances for
        ///         inclusion in a <see cref="PeptideDocNode"/> child list</returns>
        private TransitionGroupDocNode[] ReadUngroupedTransitionListXml(XElement selectedTransitions, Peptide peptide,
            ExplicitMods mods)
        {
            // The v0.1 format has no precursor element above these to read them against.
            TransitionInfo info = new TransitionInfo(this, PrecursorPeaks.LEGACY);
            TransitionGroup curGroup = null;
            List<TransitionDocNode> curList = null;
            var listGroups = new List<TransitionGroup>();
            var mapGroupToList = new Dictionary<TransitionGroup, List<TransitionDocNode>>();
            foreach (var transitionElement in selectedTransitions.Elements(EL.transition))
            {
                info.ReadXml(transitionElement, FormatVersion, out var declaredProductMz, null);

                // If the transition is not in the current group
                if (curGroup == null || curGroup.PrecursorAdduct != info.PrecursorAdduct)
                {
                    // Look for an existing group that matches
                    curGroup = null;
                    foreach (TransitionGroup group in listGroups)
                    {
                        if (group.PrecursorAdduct == info.PrecursorAdduct)
                        {
                            curGroup = group;
                            break;
                        }
                    }
                    if (curGroup != null)
                        curList = mapGroupToList[curGroup];
                    else
                    {
                        // No existing group matches, so create a new one
                        curGroup = new TransitionGroup(peptide, info.PrecursorAdduct, IsotopeLabelType.light);
                        curList = new List<TransitionDocNode>();
                        listGroups.Add(curGroup);
                        mapGroupToList.Add(curGroup, curList);
                    }
                }
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, peptide.Length);
                Transition transition = new Transition(curGroup, info.IonType,
                    offset, info.MassIndex, info.ProductAdduct);

                // No heavy transition support in v0.1, and no full-scan filtering
                var massH = Settings.GetFragmentMass(null, mods, transition, null);
                var node = new TransitionDocNode(transition, info.Losses, massH, TransitionDocNode.TransitionQuantInfo.DEFAULT, ExplicitTransitionValues.EMPTY);
                curList.Add(node);
                ValidateSerializedVsCalculatedProductMz(declaredProductMz, node); // Sanity check
            }

            // Use collected information to create the DocNodes.
            var list = new List<TransitionGroupDocNode>();
            foreach (TransitionGroup group in listGroups)
            {
                list.Add(new TransitionGroupDocNode(group, Annotations.EMPTY,
                    Settings, mods, null, ExplicitTransitionGroupValues.EMPTY, null, mapGroupToList[group].ToArray(), true));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionDocNode"/> objects from
        /// a <see cref="TransitionDocNode"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="nodeGroup">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <param name="pre422ExplicitTransitionValues">Explicit transition values that may have been serialzied at precursor level in older formats</param>
        /// <returns>A new array of <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode[] ReadTransitionListXml(XElement precursorElement,
            TransitionGroupDocNode nodeGroup, ExplicitMods mods, ExplicitTransitionValues pre422ExplicitTransitionValues,
            XElement precursorResults, out TransitionResultsData[] transitionResults)
        {
            var group = nodeGroup.TransitionGroup;
            var isotopeDist = nodeGroup.IsotopeDist;
            var list = new List<TransitionDocNode>();
            // One per transition, in the order they are read, which is the order the precursor
            // stores them in. The compact format leaves these null: its transitions carry chrom
            // infos, and the columnar form is worked out from them by UpdateResults.
            var resultsList = new List<TransitionResultsData>();
            CrosslinkBuilder crosslinkBuilder = new CrosslinkBuilder(Settings, nodeGroup.Peptide, mods, nodeGroup.LabelType);
            var transitionData = precursorElement.Element(EL.transition_data);
            if (transitionData != null)
            {
                byte[] data = Convert.FromBase64String(transitionData.Value);
                var transitionDataProto = new SkylineDocumentProto.Types.TransitionData();
                transitionDataProto.MergeFrom(data);
                foreach (var transitionProto in transitionDataProto.Transitions)
                {
                    list.Add(TransitionDocNode.FromTransitionProto(AnnotationScrubber, Settings, group, mods,
                        isotopeDist, pre422ExplicitTransitionValues, crosslinkBuilder, transitionProto,
                        out var chromInfos));
                    resultsList.Add(chromInfos == null ? null : new TransitionResultsData(chromInfos));
                }
            }
            else
            {
                // Made once for the precursor, and asked by each of its transitions in turn.
                var precursorPeaks = new PrecursorPeaks(precursorResults);
                foreach (var transitionElement in precursorElement.Elements(EL.transition))
                {
                    list.Add(ReadTransitionXml(transitionElement, group, mods, isotopeDist,
                        pre422ExplicitTransitionValues, crosslinkBuilder, precursorPeaks, out var columnarResults));
                    resultsList.Add(columnarResults);
                }
            }

            transitionResults = resultsList.ToArray();
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes a single <see cref="TransitionDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a transition</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <param name="isotopeDist">Isotope peak distribution to use for assigning M+N m/z values</param>
        /// <param name="pre422ExplicitTransitionValues">Items that may have been saved at precursor level in older formats</param>
        /// <param name="crosslinkBuilder">CrosslinkBuilder object that can be shared across all transitions</param>
        /// <returns>A new <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode ReadTransitionXml(XElement element, TransitionGroup group,
            ExplicitMods mods, IsotopeDistInfo isotopeDist, ExplicitTransitionValues pre422ExplicitTransitionValues,
            CrosslinkBuilder crosslinkBuilder, PrecursorPeaks precursorPeaks,
            out TransitionResultsData columnarResults)
        {
            TransitionInfo info = new TransitionInfo(this, precursorPeaks);

            info.ReadXmlAttributes(element, FormatVersion, pre422ExplicitTransitionValues);
            var isPrecursor = Transition.IsPrecursor(info.IonType);
            var isCustom = Transition.IsCustom(info.IonType, group);
            CustomMolecule customMolecule = null;
            Adduct adduct = Adduct.EMPTY;
            if (isCustom)
            {
                if (info.MeasuredIon != null)
                    customMolecule = info.MeasuredIon.SettingsCustomIon;
                else if (isPrecursor)
                    customMolecule = group.CustomMolecule;
                else
                {
                    using (var moleculeReader = OpenReader(element))
                    {
                        customMolecule = CustomMolecule.Deserialize(moleculeReader, out adduct);
                    }
                    if (DocumentMayContainMoleculesWithEmbeddedIons && customMolecule.ParsedMolecule.IsMassOnly && customMolecule.MonoisotopicMass.IsMassH())
                    {
                        // Defined by mass only, assume it's not massH despite how it may have been written
                        customMolecule = new CustomMolecule(customMolecule.MonoisotopicMass.ChangeIsMassH(false), customMolecule.AverageMass.ChangeIsMassH(false),
                            customMolecule.Name);
                    }
                }
            }
            info.ReadXmlElements(element, out var declaredProductMz);

            if (adduct.IsEmpty)
            {
                adduct = info.ProductAdduct;
                var isPre362NonReporterCustom = DocumentMayContainMoleculesWithEmbeddedIons && customMolecule != null &&
                                                 !(customMolecule is SettingsCustomIon); // Leave reporter ions alone
                if (isPre362NonReporterCustom && adduct.IsProteomic)
                {
                    adduct = Adduct.NonProteomicProtonatedFromCharge(adduct.AdductCharge);
                }
                // Watch all-mass declaration with mz same as mass with a charge-only adduct, which older versions don't describe succinctly
                if (!isPrecursor && isPre362NonReporterCustom &&
                    Math.Abs(declaredProductMz.Value - customMolecule.MonoisotopicMass / Math.Abs(adduct.AdductCharge)) < .001)
                {
                    CustomMolecule newFormula = null;
                    if (!customMolecule.ParsedMolecule.IsMassOnly &&
                        Math.Abs(customMolecule.MonoisotopicMass - Math.Abs(adduct.AdductCharge) * declaredProductMz.Value) < .01)
                    {
                        // Adjust hydrogen count to get a molecular mass that makes sense for charge and mz
                        newFormula = customMolecule.AdjustElementCount(@"H", -adduct.AdductCharge);
                    }
                    if (!CustomMolecule.IsNullOrEmpty(newFormula))
                    {
                        customMolecule = newFormula;
                    }
                    else
                    {
                        // All we can really say about the adduct is that it has a charge
                        adduct = Adduct.FromChargeNoMass(adduct.AdductCharge);
                    }
                }
            }
            else
            {
                // We parsed an adduct out of the molecule description, as in older versions - make sure it agrees with parsed charge
                // ReSharper disable once PossibleNullReferenceException
                Assume.IsTrue(adduct.AdductCharge == info.ProductAdduct.AdductCharge);
            }

            Transition transition;
            if (isCustom)
            {
                transition = new Transition(group, isPrecursor ? group.PrecursorAdduct : adduct, info.MassIndex,
                    customMolecule, info.IonType);
            }
            else if (isPrecursor)
            {
                transition = new Transition(group, info.IonType, group.Peptide.Length - 1, info.MassIndex,
                    adduct.IsEmpty ? group.PrecursorAdduct : adduct, info.DecoyMassShift);
            }
            else
            {
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, group.Peptide.Length);
                transition = new Transition(group, info.IonType, offset, info.MassIndex, adduct, info.DecoyMassShift);
            }

            var losses = info.Losses;
            
            var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(transition, losses, isotopeDist);
            if (group.DecoyMassShift.HasValue && !info.DecoyMassShift.HasValue)
                throw new InvalidDataException(Resources.SrmDocument_ReadTransitionXml_All_transitions_of_decoy_precursors_must_have_a_decoy_mass_shift);
            var quantInfo = new TransitionDocNode.TransitionQuantInfo(isotopeDistInfo, info.LibInfo, info.Quantitative);

            TransitionDocNode node;
            if (mods != null && mods.HasCrosslinks)
            {
                IEnumerable<IonOrdinal> parts;
                if (info.LegacyFragmentIons != null)
                {
                    parts = LegacyComplexFragmentIonName.ToIonChain(mods.LegacyCrosslinkMap, info.LegacyFragmentIons);
                }
                else
                {
                    parts = info.LinkedFragmentIons;
                }

                parts = parts.Prepend(info.OrphanedCrosslinkIon
                    ? IonOrdinal.Empty
                    : IonOrdinal.FromTransition(transition));
                var complexFragmentIon = new NeutralFragmentIon(parts, info.Losses);
                var chargedIon = new ComplexFragmentIon(transition, complexFragmentIon, mods);
                node = crosslinkBuilder.MakeTransitionDocNode(chargedIon, isotopeDist, info.Annotations, quantInfo,
                    info.ExplicitValues, null);
            }
            else
            {
                var mass = Settings.GetFragmentMass(group, mods, transition, isotopeDist);
                node = new TransitionDocNode(transition, info.Annotations, losses,
                    mass, quantInfo, info.ExplicitValues, null);
            }

            ValidateSerializedVsCalculatedProductMz(declaredProductMz, node);  // Sanity check

            // The columnar results go to the precursor, which is what keeps a transition's now. A
            // document written the old way has its chrom infos turned into them here and then does
            // not hold on to the chrom infos: they are read back from the .skyd, or worked out from
            // the columnar results.
            columnarResults = info.ColumnarResults ??
                              (info.Results == null ? null : new TransitionResultsData(info.Results));

            return node;
        }

        /// <summary>
        /// Verify that any mz values we serialize for informational purposes agree with what we calculate upon reading in again
        /// </summary>
        private void ValidateSerializedVsCalculatedProductMz(double? declaredProductMz, TransitionDocNode node)
        {
            if (node.ComplexFragmentIon.IsCrosslinked && FormatVersion <= DocumentFormat.VERSION_22_23)
            {
                // Recent bugfixes for crosslinked peptides might result in different m/z's
                return;
            }
            if (declaredProductMz.HasValue && Math.Abs(declaredProductMz.Value - node.Mz.Value) >= .001)
            {
                var toler = node.Transition.IsPrecursor() ? .5 : // We do see mz-only transition lists where precursor mz is given as double and product mz as int
                    FormatVersion.CompareTo(DocumentFormat.VERSION_3_6) <= 0 && node.Transition.IonType == IonType.z ? 1.007826 : // Known issue fixed in SVN 7007
                        (FormatVersion.CompareTo(DocumentFormat.VERSION_1_7) <= 0 ? .005 : .0025); // Unsure if 1.7 is the precise watershed, but this gets a couple of older tests passing
                Assume.IsTrue(Math.Abs(declaredProductMz.Value - node.Mz.Value) < toler,
                    string.Format(@"error reading mz values - declared mz value {0} does not match calculated value {1}",
                        declaredProductMz.Value, node.Mz.Value));
            }
        }
    }
}
