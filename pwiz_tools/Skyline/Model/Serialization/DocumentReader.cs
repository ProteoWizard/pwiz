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
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using pwiz.Common.Collections;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    public class DocumentReader : DocumentSerializer
    {
        private readonly XmlReadContext _context = new XmlReadContext();
        public DocumentFormat FormatVersion { get; private set; }
        public PeptideGroupDocNode[] Children { get; private set; }


        private PeptideChromInfo ReadPeptideChromInfo(XmlReader reader, ChromFileInfoId fileId)
        {
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            bool excludeFromCalibration = reader.GetBoolAttribute(ATTR.exclude_from_calibration);
            return new PeptideChromInfo(fileId, peakCountRatio, retentionTime, ImmutableList<PeptideLabelRatio>.EMPTY)
                .ChangeExcludeFromCalibration(excludeFromCalibration);
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
                return libInfo.ChangeLibraryName(_context.GetNonDuplicatedName(libInfo.LibraryName));
            }

            return null;
        }

        private TransitionGroupChromInfo ReadTransitionGroupChromInfo(XmlReader reader, ChromFileInfoId fileId)
        {
            int optimizationStep = reader.GetIntAttribute(ATTR.step);
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            float? startTime = reader.GetNullableFloatAttribute(ATTR.start_time);
            float? endTime = reader.GetNullableFloatAttribute(ATTR.end_time);
            float? ccs = reader.GetNullableFloatAttribute(ATTR.ccs);
            float? driftTimeMS1 = reader.GetNullableFloatAttribute(ATTR.drift_time_ms1);
            float? driftTimeFragment = reader.GetNullableFloatAttribute(ATTR.drift_time_fragment);
            float? driftTimeWindow = reader.GetNullableFloatAttribute(ATTR.drift_time_window);
            float? fwhm = reader.GetNullableFloatAttribute(ATTR.fwhm);
            float? area = reader.GetNullableFloatAttribute(ATTR.area);
            float? backgroundArea = reader.GetNullableFloatAttribute(ATTR.background);
            float? height = reader.GetNullableFloatAttribute(ATTR.height);
            float? massError = reader.GetNullableFloatAttribute(ATTR.mass_error_ppm);
            int? truncated = reader.GetNullableIntAttribute(ATTR.truncated);
            PeakIdentification identified = reader.GetEnumAttribute(ATTR.identified, PeakIdentificationFastLookup.Dict,
                PeakIdentification.FALSE, XmlUtil.EnumCase.upper);
            float? libraryDotProduct = reader.GetNullableFloatAttribute(ATTR.library_dotp);
            float? isotopeDotProduct = reader.GetNullableFloatAttribute(ATTR.isotope_dotp);
            float? qvalue = reader.GetNullableFloatAttribute(ATTR.qvalue);
            float? zscore = reader.GetNullableFloatAttribute(ATTR.zscore);
            var annotations = Annotations.EMPTY;
            if (!reader.IsEmptyElement)
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader, _context);
                // Convert q value and mProphet score annotations to numbers for the ChromInfo object
                annotations = ReadAndRemoveScoreAnnotation(annotations, MProphetResultsHandler.AnnotationName, ref qvalue);
                annotations = ReadAndRemoveScoreAnnotation(annotations, MProphetResultsHandler.MAnnotationName, ref zscore);
            }
            // Ignore userSet during load, since all values are still calculated
            // from the child transitions.  Otherwise inconsistency is possible.
//            bool userSet = reader.GetBoolAttribute(ATTR.user_set);
            const UserSet userSet = UserSet.FALSE;
            int countRatios = Settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
            return new TransitionGroupChromInfo(fileId,
                optimizationStep,
                peakCountRatio,
                retentionTime,
                startTime,
                endTime,
                TransitionGroupDriftTimeInfo.GetTransitionGroupIonMobilityInfo(ccs, driftTimeMS1, driftTimeFragment, driftTimeWindow),
                fwhm,
                area, null, null, // Ms1 and Fragment values calculated later
                backgroundArea, null, null, // Ms1 and Fragment values calculated later
                height,
                TransitionGroupChromInfo.GetEmptyRatios(countRatios),
                massError,
                truncated,
                identified,
                libraryDotProduct,
                isotopeDotProduct,
                qvalue,
                zscore,
                annotations,
                userSet);
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
        /// Reads annotations without ensuring that they use a single unique key string. This
        /// is currently only used for <see cref="ChromatogramSet"/>, because it is difficult to
        /// get it to use the version with a non-null context and the possible level of repetition
        /// is much smaller than with the document nodes and results objects.
        /// </summary>
        private static Annotations ReadAnnotations(XmlReader reader, XmlReadContext context)
        {
            string note = null;
            int color = 0;
            var annotations = new Dictionary<string, string>();
            
            if (reader.IsStartElement(EL.note))
            {
                color = reader.GetIntAttribute(ATTR.category);
                note = reader.ReadElementString();
            }
            while (reader.IsStartElement(EL.annotation))
            {
                string name = reader.GetAttribute(ATTR.name);
                if (name == null)
                    throw new InvalidDataException(Resources.SrmDocument_ReadAnnotations_Annotation_found_without_name);
                if (context != null)
                    name = context.GetNonDuplicatedName(name);
                annotations[name] = reader.ReadElementString();
            }

            return note != null || annotations.Count > 0
                ? new Annotations(note, annotations, color)
                : Annotations.EMPTY;
        }

        public static Annotations ReadAnnotations(XmlReader reader)
        {
            return ReadAnnotations(reader, null);
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
            private readonly DocumentReader _documentReader;
            public TransitionInfo(DocumentReader documentReader)
            {
                _documentReader = documentReader;
            }
            public SrmSettings Settings { get { return _documentReader.Settings; } }
            public IonType IonType { get; private set; }
            public int Ordinal { get; private set; }
            public int MassIndex { get; private set; }
            public int PrecursorCharge { get; private set; }
            public int Charge { get; private set; }
            public int? DecoyMassShift { get; private set; }
            public TransitionLosses Losses { get; private set; }
            public Annotations Annotations { get; private set; }
            public TransitionLibInfo LibInfo { get; private set; }
            public Results<TransitionChromInfo> Results { get; private set; }
            public MeasuredIon MeasuredIon { get; private set; }

            public void ReadXml(XmlReader reader)
            {
                ReadXmlAttributes(reader);
                ReadXmlElements(reader);
            }

            public void ReadXmlAttributes(XmlReader reader)
            {
                // Accept uppercase and lowercase for backward compatibility with v0.1
                IonType = reader.GetEnumAttribute(ATTR.fragment_type, IonType.y, XmlUtil.EnumCase.lower);
                Ordinal = reader.GetIntAttribute(ATTR.fragment_ordinal);
                MassIndex = reader.GetIntAttribute(ATTR.mass_index);
                // NOTE: PrecursorCharge is used only in TransitionInfo.ReadUngroupedTransitionListXml()
                //       to support v0.1 document format
                PrecursorCharge = reader.GetIntAttribute(ATTR.precursor_charge);
                Charge = reader.GetIntAttribute(ATTR.product_charge);
                DecoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
                string measuredIonName = reader.GetAttribute(ATTR.measured_ion_name);
                if (measuredIonName != null)
                {
                    MeasuredIon = Settings.TransitionSettings.Filter.MeasuredIons.SingleOrDefault(
                        i => i.Name.Equals(measuredIonName));
                    if (MeasuredIon == null)
                        throw new InvalidDataException(String.Format(Resources.TransitionInfo_ReadXmlAttributes_The_reporter_ion__0__was_not_found_in_the_transition_filter_settings_, measuredIonName));
                    IonType = IonType.custom;
                }
            }

            public void ReadXmlElements(XmlReader reader)
            {
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();
                    Annotations = ReadAnnotations(reader, _documentReader._context); // This is reliably first in all versions
                    while (true)
                    {  // The order of these elements may depend on the version of the file being read
                        if (reader.IsStartElement(EL.losses))
                            Losses = ReadTransitionLosses(reader);
                        else if (reader.IsStartElement(EL.transition_lib_info))
                            LibInfo = ReadTransitionLibInfo(reader);
                        else if (reader.IsStartElement(EL.transition_results))
                            Results = ReadTransitionResults(reader);
                        // Read and discard informational elements.  These values are always
                        // calculated from the settings to ensure consistency.
                        else if (reader.IsStartElement(EL.precursor_mz))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.product_mz))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.collision_energy))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.declustering_potential))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.start_rt))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.stop_rt))
                            reader.ReadElementContentAsDoubleInvariant();
                        else
                            break;
                    }
                    reader.ReadEndElement();
                }
            }

            private TransitionLosses ReadTransitionLosses(XmlReader reader)
            {
                if (reader.IsStartElement(EL.losses))
                {
                    var staticMods = Settings.PeptideSettings.Modifications.StaticModifications;
                    MassType massType = Settings.TransitionSettings.Prediction.FragmentMassType;

                    reader.ReadStartElement();
                    var listLosses = new List<TransitionLoss>();
                    while (reader.IsStartElement(EL.neutral_loss))
                    {
                        string nameMod = reader.GetAttribute(ATTR.modification_name);
                        if (String.IsNullOrEmpty(nameMod))
                            listLosses.Add(new TransitionLoss(null, FragmentLoss.Deserialize(reader), massType));
                        else
                        {
                            int indexLoss = reader.GetIntAttribute(ATTR.loss_index);
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
                        reader.Read();
                    }
                    reader.ReadEndElement();

                    return new TransitionLosses(listLosses, massType);
                }
                return null;
            }

            private static TransitionLibInfo ReadTransitionLibInfo(XmlReader reader)
            {
                if (reader.IsStartElement(EL.transition_lib_info))
                {
                    var libInfo = new TransitionLibInfo(reader.GetIntAttribute(ATTR.rank),
                        reader.GetFloatAttribute(ATTR.intensity));
                    reader.ReadStartElement();
                    return libInfo;
                }
                return null;
            }

            private Results<TransitionChromInfo> ReadTransitionResults(XmlReader reader)
            {
                if (reader.IsStartElement(EL.transition_results))
                    return _documentReader.ReadResults(reader, EL.transition_peak, ReadTransitionPeak);
                return null;
            }

            private TransitionChromInfo ReadTransitionPeak(XmlReader reader, ChromFileInfoId fileId)
            {
                int optimizationStep = reader.GetIntAttribute(ATTR.step);
                float? massError = reader.GetNullableFloatAttribute(ATTR.mass_error_ppm);
                float retentionTime = reader.GetFloatAttribute(ATTR.retention_time);
                float startRetentionTime = reader.GetFloatAttribute(ATTR.start_time);
                float endRetentionTime = reader.GetFloatAttribute(ATTR.end_time);
                // Protect against negative areas, since they can cause real problems
                // for ratio calculations.
                float area = Math.Max(0, reader.GetFloatAttribute(ATTR.area));
                float backgroundArea = Math.Max(0, reader.GetFloatAttribute(ATTR.background));
                float height = reader.GetFloatAttribute(ATTR.height);
                float fwhm = reader.GetFloatAttribute(ATTR.fwhm);
                // Strange issue where fwhm got set to NaN
                if (Single.IsNaN(fwhm))
                    fwhm = 0;
                bool fwhmDegenerate = reader.GetBoolAttribute(ATTR.fwhm_degenerate);
                short rank = (short) reader.GetIntAttribute(ATTR.rank);
                short rankByLevel = (short) reader.GetIntAttribute(ATTR.rank_by_level, rank);
                bool? truncated = reader.GetNullableBoolAttribute(ATTR.truncated);
                short pointsAcross = (short) reader.GetIntAttribute(ATTR.points_across, 0);
                var identified = reader.GetEnumAttribute(ATTR.identified, PeakIdentificationFastLookup.Dict,
                    PeakIdentification.FALSE, XmlUtil.EnumCase.upper);
                UserSet userSet = reader.GetEnumAttribute(ATTR.user_set, UserSetFastLookup.Dict,
                    UserSet.FALSE, XmlUtil.EnumCase.upper);
                double? driftTime = reader.GetNullableDoubleAttribute(ATTR.drift_time);
                double? driftTimeWindow = reader.GetNullableDoubleAttribute(ATTR.drift_time_window);
                var annotations = Annotations.EMPTY;
                if (!reader.IsEmptyElement)
                {
                    reader.ReadStartElement();
                    annotations = ReadAnnotations(reader, _documentReader._context);
                }
                int countRatios = _documentReader.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
                return new TransitionChromInfo(fileId,
                    optimizationStep,
                    massError,
                    retentionTime,
                    startRetentionTime,
                    endRetentionTime, 
                    DriftTimeFilter.GetDriftTimeFilter(driftTime, driftTimeWindow, null), 
                    area,
                    backgroundArea,
                    height,
                    fwhm,
                    fwhmDegenerate,
                    truncated,
                    pointsAcross,
                    identified,
                    rank,
                    rankByLevel,
                    TransitionChromInfo.GetEmptyRatios(countRatios),
                    annotations,
                    userSet);
            }
        }

        private Results<TItem> ReadResults<TItem>(XmlReader reader, string start,
            Func<XmlReader, ChromFileInfoId, TItem> readInfo)
            where TItem : ChromInfo
        {
            // If the results element is empty, then there are no results to read.
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return null;
            }

            MeasuredResults results = Settings.MeasuredResults;
            if (results == null)
                throw new InvalidDataException(Resources.SrmDocument_ReadResults_No_results_information_found_in_the_document_settings);

            reader.ReadStartElement();
            var arrayListChromInfos = new List<TItem>[results.Chromatograms.Count];
            ChromatogramSet chromatogramSet = null;
            int index = -1;
            while (reader.IsStartElement(start))
            {
                string name = reader.GetAttribute(ATTR.replicate);
                if (chromatogramSet == null || !Equals(name, chromatogramSet.Name))
                {
                    if (!results.TryGetChromatogramSet(name, out chromatogramSet, out index))
                        throw new InvalidDataException(String.Format(Resources.SrmDocument_ReadResults_No_replicate_named__0__found_in_measured_results, name));
                }
                string fileId = reader.GetAttribute(ATTR.file);
                var fileInfoId = (fileId != null
                    ? chromatogramSet.FindFileById(fileId)
                    : chromatogramSet.MSDataFileInfos[0].FileId);
                if (fileInfoId == null)
                    throw new InvalidDataException(String.Format(Resources.SrmDocument_ReadResults_No_file_with_id__0__found_in_the_replicate__1__, fileId, name));

                TItem chromInfo = readInfo(reader, fileInfoId);
                // Consume the tag
                reader.Read();

                if (!ReferenceEquals(chromInfo, default(TItem)))
                {
                    if (arrayListChromInfos[index] == null)
                        arrayListChromInfos[index] = new List<TItem>();
                    // Deal with cache corruption issue where the same results info could
                    // get written multiple times for the same precursor.
                    var listChromInfos = arrayListChromInfos[index];
                    if (listChromInfos.Count == 0 || !Equals(chromInfo, listChromInfos[listChromInfos.Count - 1]))
                        arrayListChromInfos[index].Add(chromInfo);
                }
            }
            reader.ReadEndElement();

            var arrayChromInfoLists = new ChromInfoList<TItem>[arrayListChromInfos.Length];
            for (int i = 0; i < arrayListChromInfos.Length; i++)
            {
                if (arrayListChromInfos[i] != null)
                    arrayChromInfoLists[i] = new ChromInfoList<TItem>(arrayListChromInfos[i]);
            }
            return new Results<TItem>(arrayChromInfoLists);
        }

        /// <summary>
        /// Deserializes document from XML.
        /// </summary>
        /// <param name="reader">The reader positioned at the document start tag</param>
        public void ReadXml(XmlReader reader)
        {
            double formatVersionNumber = reader.GetDoubleAttribute(ATTR.format_version);
            if (formatVersionNumber == 0)
            {
                FormatVersion = DocumentFormat.VERSION_0_1;
            }
            else
            {
                if (formatVersionNumber > DocumentFormat.CURRENT.AsDouble())
                {
                    throw new VersionNewerException(
                        string.Format(Resources.SrmDocument_ReadXml_The_document_format_version__0__is_newer_than_the_version__1__supported_by__2__,
                                      FormatVersion, formatVersionNumber, Install.ProgramNameAndVersion));
                    
                }
                FormatVersion = new DocumentFormat(formatVersionNumber);
            }

            reader.ReadStartElement();  // Start document element

            Settings = reader.DeserializeElement<SrmSettings>() ?? SrmSettingsList.GetDefault();

            if (reader.IsStartElement())
            {
                // Support v0.1 naming
                if (!reader.IsStartElement(EL.selected_proteins))
                    Children = ReadPeptideGroupListXml(reader);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement();
                    Children = ReadPeptideGroupListXml(reader);
                    reader.ReadEndElement();
                }
            }

            reader.ReadEndElement();    // End document element
            if (Children == null)
                Children = new PeptideGroupDocNode[0];
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at the start of the list.
        /// </summary>
        /// <param name="reader">The reader positioned on the element of the first node</param>
        /// <returns>An array of <see cref="PeptideGroupDocNode"/> objects for
        ///         inclusion in a <see cref="SrmDocument"/> child list</returns>
        private PeptideGroupDocNode[] ReadPeptideGroupListXml(XmlReader reader)
        {
            var list = new List<PeptideGroupDocNode>();
            while (reader.IsStartElement(EL.protein) || reader.IsStartElement(EL.peptide_list))
            {
                if (reader.IsStartElement(EL.protein))
                    list.Add(ReadProteinXml(reader));
                else
                    list.Add(ReadPeptideGroupXml(reader));
            }
            return list.ToArray();
        }

        private static ProteinMetadata ReadProteinMetadataXML(XmlReader reader, bool labelNameAndDescription)
        {
            var labelPrefix = labelNameAndDescription ? "label_" : string.Empty; // Not L10N
            return new ProteinMetadata(
                reader.GetAttribute(labelPrefix + ATTR.name),
                reader.GetAttribute(labelPrefix + ATTR.description),
                reader.GetAttribute(ATTR.preferred_name),
                reader.GetAttribute(ATTR.accession),
                reader.GetAttribute(ATTR.gene),
                reader.GetAttribute(ATTR.species),
                reader.GetAttribute(ATTR.websearch_status));
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at a &lt;protein&gt; tag.
        /// 
        /// In order to support the v0.1 format, the returned node may represent
        /// either a FASTA sequence or a peptide list.
        /// </summary>
        /// <param name="reader">The reader positioned at a protein tag</param>
        /// <returns>A new <see cref="PeptideGroupDocNode"/></returns>
        private PeptideGroupDocNode ReadProteinXml(XmlReader reader)
        {
            string name = reader.GetAttribute(ATTR.name);
            string description = reader.GetAttribute(ATTR.description);
            bool peptideList = reader.GetBoolAttribute(ATTR.peptide_list);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            var labelProteinMetadata = ReadProteinMetadataXML(reader, true);  // read label_name, label_description, and species, gene etc if any

            reader.ReadStartElement();

            var annotations = ReadAnnotations(reader, _context);

            ProteinMetadata[] alternatives;
            if (!reader.IsStartElement(EL.alternatives) || reader.IsEmptyElement)
                alternatives = new ProteinMetadata[0];
            else
            {
                reader.ReadStartElement();
                alternatives = ReadAltProteinListXml(reader);
                reader.ReadEndElement();
            }

            reader.ReadStartElement(EL.sequence);
            string sequence = DecodeProteinSequence(reader.ReadContentAsString());
            reader.ReadEndElement();

            // Support v0.1 documents, where peptide lists were saved as proteins,
            // pre-v0.1 documents, which may not have identified peptide lists correctly.
            if (sequence.StartsWith("X") && sequence.EndsWith("X")) // Not L10N
                peptideList = true;

            // All v0.1 peptide lists should have a settable label
            if (peptideList)
            {
                labelProteinMetadata = labelProteinMetadata.ChangeName(name ?? string.Empty);
                labelProteinMetadata = labelProteinMetadata.ChangeDescription(description);
            }
            // Or any protein without a name attribute
            else if (name != null)
            {
                labelProteinMetadata = labelProteinMetadata.ChangeDescription(null);
            }

            PeptideGroup group;
            if (peptideList)
                group = new PeptideGroup();
            // If there is no name attribute, ignore all info from the FASTA header line,
            // since it should be user settable.
            else if (name == null)
                group = new FastaSequence(null, null, null, sequence);
            else
                group = new FastaSequence(name, description, alternatives, sequence);

            PeptideDocNode[] children = null;
            if (!reader.IsStartElement(EL.selected_peptides))
                children = ReadPeptideListXml(reader, group);
            else if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement(EL.selected_peptides);
                children = ReadPeptideListXml(reader, group);
                reader.ReadEndElement();
            }

            reader.ReadEndElement();

            return new PeptideGroupDocNode(group, annotations, labelProteinMetadata,
                children ?? new PeptideDocNode[0], autoManageChildren);
        }

        /// <summary>
        /// Deserializes an array of <see cref="ProteinMetadata"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <returns>A new array of <see cref="ProteinMetadata"/></returns>
        private static ProteinMetadata[] ReadAltProteinListXml(XmlReader reader)
        {
            var list = new List<ProteinMetadata>();
            while (reader.IsStartElement(EL.alternative_protein))
            {
                var proteinMetaData = ReadProteinMetadataXML(reader, false);
                reader.Read();
                list.Add(proteinMetaData);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Decodes a FASTA sequence as stored in a XML document to one
        /// with all white space removed.
        /// </summary>
        /// <param name="sequence">The XML format sequence</param>
        /// <returns>The sequence suitible for use in a <see cref="FastaSequence"/></returns>
        private static string DecodeProteinSequence(IEnumerable<char> sequence)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char aa in sequence)
            {
                if (!char.IsWhiteSpace(aa))
                    sb.Append(aa);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> representing
        /// a peptide list from a <see cref="XmlReader"/> positioned at the
        /// start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a peptide group</param>
        /// <returns>A new <see cref="PeptideGroupDocNode"/></returns>
        private PeptideGroupDocNode ReadPeptideGroupXml(XmlReader reader)
        {
            ProteinMetadata proteinMetadata = ReadProteinMetadataXML(reader, true); // read label_name and label_description
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);

            PeptideGroup group = new PeptideGroup(isDecoy);

            Annotations annotations = Annotations.EMPTY;
            PeptideDocNode[] children = null;

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader, _context);

                if (!reader.IsStartElement(EL.selected_peptides))
                    children = ReadPeptideListXml(reader, group);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement(EL.selected_peptides);
                    children = ReadPeptideListXml(reader, group);
                    reader.ReadEndElement();
                }

                reader.ReadEndElement();    // peptide_list
            }

            return new PeptideGroupDocNode(group, annotations, proteinMetadata,
                children ?? new PeptideDocNode[0], autoManageChildren);
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <returns>A new array of <see cref="PeptideDocNode"/></returns>
        private PeptideDocNode[] ReadPeptideListXml(XmlReader reader, PeptideGroup group)
        {
            var list = new List<PeptideDocNode>();
            while (reader.IsStartElement(EL.molecule) || reader.IsStartElement(EL.peptide))
            {
                list.Add(ReadPeptideXml(reader, group, reader.IsStartElement(EL.molecule)));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserialize any explictly set CE, DT, etc information from attributes
        /// </summary>
        private ExplicitTransitionGroupValues ReadExplicitTransitionValuesAttributes(XmlReader reader)
        {
            double? importedCollisionEnergy = reader.GetNullableDoubleAttribute(ATTR.explicit_collision_energy);
            double? importedDriftTimeMsec = reader.GetNullableDoubleAttribute(ATTR.explicit_drift_time_msec);
            double? importedDriftTimeHighEnergyOffsetMsec = reader.GetNullableDoubleAttribute(ATTR.explicit_drift_time_high_energy_offset_msec);
            double? importedCCS = reader.GetNullableDoubleAttribute(ATTR.explicit_ccs_sqa);
            double? importedSLens = reader.GetNullableDoubleAttribute(FormatVersion.CompareTo(DocumentFormat.VERSION_3_52) < 0 ? ATTR.s_lens_obsolete : ATTR.explicit_s_lens);
            double? importedConeVoltage = reader.GetNullableDoubleAttribute(FormatVersion.CompareTo(DocumentFormat.VERSION_3_52) < 0 ? ATTR.cone_voltage_obsolete : ATTR.explicit_cone_voltage);
            double? importedCompensationVoltage = reader.GetNullableDoubleAttribute(ATTR.explicit_compensation_voltage);
            double? importedDeclusteringPotential = reader.GetNullableDoubleAttribute(ATTR.explicit_declustering_potential);
            return new ExplicitTransitionGroupValues(importedCollisionEnergy, importedDriftTimeMsec, importedDriftTimeHighEnergyOffsetMsec, importedCCS, importedSLens, importedConeVoltage,
                importedDeclusteringPotential, importedCompensationVoltage);
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a peptide or molecule</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="isCustomMolecule">if true, we're reading a custom molecule, not a peptide</param>
        /// <returns>A new <see cref="PeptideDocNode"/></returns>
        private PeptideDocNode ReadPeptideXml(XmlReader reader, PeptideGroup group, bool isCustomMolecule)
        {
            int? start = reader.GetNullableIntAttribute(ATTR.start);
            int? end = reader.GetNullableIntAttribute(ATTR.end);
            string sequence = reader.GetAttribute(ATTR.sequence);
            string lookupSequence = reader.GetAttribute(ATTR.lookup_sequence);
            // If the group has no sequence, then this is a v0.1 peptide list or a custom ion
            if (group.Sequence == null)
            {
                // Ignore the start and end values
                start = null;
                end = null;
            }
            int missedCleavages = reader.GetIntAttribute(ATTR.num_missed_cleavages);
            // CONSIDER: Trusted value
            int? rank = reader.GetNullableIntAttribute(ATTR.rank);
            double? concentrationMultiplier = reader.GetNullableDoubleAttribute(ATTR.concentration_multiplier);
            double? internalStandardConcentration =
                reader.GetNullableDoubleAttribute(ATTR.internal_standard_concentration);
            string normalizationMethod = reader.GetAttribute(ATTR.normalization_method);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);
            var standardType = StandardType.FromName(reader.GetAttribute(ATTR.standard_type));
            double? importedRetentionTimeValue = reader.GetNullableDoubleAttribute(ATTR.explicit_retention_time);
            double? importedRetentionTimeWindow = reader.GetNullableDoubleAttribute(ATTR.explicit_retention_time_window);
            var importedRetentionTime = importedRetentionTimeValue.HasValue
                ? new ExplicitRetentionTimeInfo(importedRetentionTimeValue.Value, importedRetentionTimeWindow)
                : null;
            var annotations = Annotations.EMPTY;
            ExplicitMods mods = null, lookupMods = null;
            Results<PeptideChromInfo> results = null;
            TransitionGroupDocNode[] children = null;
            var customIon = isCustomMolecule ? DocNodeCustomIon.Deserialize(reader) : null; // This Deserialize only reads attribures, doesn't advance the reader
            var peptide = isCustomMolecule ?
                new Peptide(customIon) :
                new Peptide(group as FastaSequence, sequence, start, end, missedCleavages, isDecoy);

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                if (reader.IsStartElement())
                    annotations = ReadAnnotations(reader, _context);
                if (!isCustomMolecule)
                {
                    mods = ReadExplicitMods(reader, peptide);
                    SkipImplicitModsElement(reader);
                    lookupMods = ReadLookupMods(reader, lookupSequence);
                }
                results = ReadPeptideResults(reader);

                if (reader.IsStartElement(EL.precursor))
                {
                    children = ReadTransitionGroupListXml(reader, peptide, mods, customIon);
                }
                else if (reader.IsStartElement(EL.selected_transitions))
                {
                    // Support for v0.1
                    if (reader.IsEmptyElement)
                        reader.Read();
                    else
                    {
                        reader.ReadStartElement(EL.selected_transitions);
                        children = ReadUngroupedTransitionListXml(reader, peptide, mods);
                        reader.ReadEndElement();
                    }
                }

                reader.ReadEndElement();
            }

            ModifiedSequenceMods sourceKey = null;
            if (lookupSequence != null)
                sourceKey = new ModifiedSequenceMods(lookupSequence, lookupMods);

            PeptideDocNode peptideDocNode = new PeptideDocNode(peptide, Settings, mods, sourceKey, standardType, rank,
                importedRetentionTime, annotations, results, children ?? new TransitionGroupDocNode[0], autoManageChildren);
            peptideDocNode = peptideDocNode
                .ChangeConcentrationMultiplier(concentrationMultiplier)
                .ChangeInternalStandardConcentration(internalStandardConcentration)
                .ChangeNormalizationMethod(NormalizationMethod.FromName(normalizationMethod));
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

        private void SkipImplicitModsElement(XmlReader reader)
        {
            if (!reader.IsStartElement(EL.implicit_modifications))
                return;
            reader.Skip();
        }

        private ExplicitMods ReadExplicitMods(XmlReader reader, Peptide peptide)
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
                    listMods.Add(new ExplicitMod(indexAA, modAdd));
                    // Consume tag
                    reader.Read();
                }
                reader.ReadEndElement();
            }
            return new TypedExplicitModifications(peptide, typedMods.LabelType, listMods.ToArray());
        }

        private Results<PeptideChromInfo> ReadPeptideResults(XmlReader reader)
        {
            if (reader.IsStartElement(EL.peptide_results))
                return ReadResults(reader, EL.peptide_result, ReadPeptideChromInfo);
            return null;
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionGroupDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="peptide">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <param name="customIon">Custom ion to use for reading older formats</param>
        /// <returns>A new array of <see cref="TransitionGroupDocNode"/></returns>
        private TransitionGroupDocNode[] ReadTransitionGroupListXml(XmlReader reader, Peptide peptide, ExplicitMods mods, DocNodeCustomIon customIon)
        {
            var list = new List<TransitionGroupDocNode>();
            while (reader.IsStartElement(EL.precursor))
                list.Add(ReadTransitionGroupXml(reader, peptide, mods, customIon));
            return list.ToArray();
        }

        private TransitionGroupDocNode ReadTransitionGroupXml(XmlReader reader, Peptide peptide, ExplicitMods mods, DocNodeCustomIon customIon)
        {
            int precursorCharge = reader.GetIntAttribute(ATTR.charge);
            var typedMods = ReadLabelType(reader, IsotopeLabelType.light);

            int? decoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
            var explicitTransitionGroupValues = ReadExplicitTransitionValuesAttributes(reader);
            if (peptide.IsCustomIon)
            {
                // In small molecules, different labels and charges mean different ion formulas
                var ionFormula = reader.GetAttribute(ATTR.ion_formula);
                if (!string.IsNullOrEmpty(ionFormula))
                {
                    var ionName = reader.GetAttribute(ATTR.custom_ion_name);
                    customIon = new DocNodeCustomIon(ionFormula, ionName);
                }
                else
                {
                    var mz = reader.GetDoubleAttribute(ATTR.precursor_mz); // Normally ignored, but needed for molecules that are declared by mz and charge only
                    var mass = BioMassCalc.CalculateIonMassFromMz(mz, precursorCharge); // We can't actually tell mono from average in this case
                    double massMono = reader.GetNullableDoubleAttribute(ATTR.mass_monoisotopic) ?? mass;
                    double massAverage = reader.GetNullableDoubleAttribute(ATTR.mass_average) ?? mass;
                    if (FormatVersion.CompareTo(DocumentFormat.VERSION_3_12) < 0)
                    {
                        // In Skyline 3.1 we didn't suppport more than one transition group per molecule
                        // Passed-in customIon is the primary precursor
                    }
                    // We need to determine if this is the primary precursor transition - but all we have to go on is mass
                    else if (Math.Round(massMono, SequenceMassCalc.MassPrecision) == Math.Round(customIon.MonoisotopicMass, SequenceMassCalc.MassPrecision) &&
                        Math.Round(massAverage, SequenceMassCalc.MassPrecision) == Math.Round(customIon.AverageMass, SequenceMassCalc.MassPrecision))
                    {
                        // Passed-in customIon is the primary precursor
                    }
                    else
                    {
                        customIon = new DocNodeCustomIon(massMono, massAverage, reader.GetAttribute(ATTR.custom_ion_name));
                    }
                }
            }
            var group = new TransitionGroup(peptide, customIon, precursorCharge, typedMods.LabelType, false, decoyMassShift);
            var children = new TransitionDocNode[0];    // Empty until proven otherwise
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);

            if (reader.IsEmptyElement)
            {
                reader.Read();

                return new TransitionGroupDocNode(group,
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
                reader.ReadStartElement();
                var annotations = ReadAnnotations(reader, _context);
                var libInfo = ReadTransitionGroupLibInfo(reader);
                var results = ReadTransitionGroupResults(reader);

                var nodeGroup = new TransitionGroupDocNode(group,
                                                  annotations,
                                                  Settings,
                                                  mods,
                                                  libInfo,
                                                  explicitTransitionGroupValues,
                                                  results,
                                                  children,
                                                  autoManageChildren);
                children = ReadTransitionListXml(reader, group, mods, nodeGroup.IsotopeDist);

                reader.ReadEndElement();

                return (TransitionGroupDocNode)nodeGroup.ChangeChildrenChecked(children);
            }
        }

        private TypedModifications ReadLabelType(XmlReader reader, IsotopeLabelType labelTypeDefault)
        {
            string typeName = reader.GetAttribute(ATTR.isotope_label);
            if (string.IsNullOrEmpty(typeName))
                typeName = labelTypeDefault.Name;
            var typedMods = Settings.PeptideSettings.Modifications.GetModificationsByName(typeName);
            if (typedMods == null)
                throw new InvalidDataException(string.Format(Resources.SrmDocument_ReadLabelType_The_isotope_modification_type__0__does_not_exist_in_the_document_settings, typeName));
            return typedMods;
        }

        private Results<TransitionGroupChromInfo> ReadTransitionGroupResults(XmlReader reader)
        {
            if (reader.IsStartElement(EL.precursor_results))
                return ReadResults(reader, EL.precursor_peak, ReadTransitionGroupChromInfo);
            return null;
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
        private TransitionGroupDocNode[] ReadUngroupedTransitionListXml(XmlReader reader, Peptide peptide, ExplicitMods mods)
        {
            TransitionInfo info = new TransitionInfo(this);
            TransitionGroup curGroup = null;
            List<TransitionDocNode> curList = null;
            var listGroups = new List<TransitionGroup>();
            var mapGroupToList = new Dictionary<TransitionGroup, List<TransitionDocNode>>();
            while (reader.IsStartElement(EL.transition))
            {
                // Read a transition tag.
                info.ReadXml(reader);

                // If the transition is not in the current group
                if (curGroup == null || curGroup.PrecursorCharge != info.PrecursorCharge)
                {
                    // Look for an existing group that matches
                    curGroup = null;
                    foreach (TransitionGroup group in listGroups)
                    {
                        if (group.PrecursorCharge == info.PrecursorCharge)
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
                        curGroup = new TransitionGroup(peptide, null, info.PrecursorCharge, IsotopeLabelType.light);
                        curList = new List<TransitionDocNode>();
                        listGroups.Add(curGroup);
                        mapGroupToList.Add(curGroup, curList);
                    }
                }
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, peptide.Length);
                Transition transition = new Transition(curGroup, info.IonType,
                    offset, info.MassIndex, info.Charge);

                // No heavy transition support in v0.1, and no full-scan filtering
                double massH = Settings.GetFragmentMass(IsotopeLabelType.light, mods, transition, null);

                curList.Add(new TransitionDocNode(transition, info.Losses, massH, null, null));
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
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <param name="isotopeDist">Isotope peak distribution to use for assigning M+N m/z values</param>
        /// <returns>A new array of <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode[] ReadTransitionListXml(XmlReader reader, 
            TransitionGroup group, ExplicitMods mods, IsotopeDistInfo isotopeDist)
        {
            var list = new List<TransitionDocNode>();
            while (reader.IsStartElement(EL.transition))
                list.Add(ReadTransitionXml(reader, group, mods, isotopeDist));
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
        /// <returns>A new <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode ReadTransitionXml(XmlReader reader, TransitionGroup group,
            ExplicitMods mods, IsotopeDistInfo isotopeDist)
        {
            TransitionInfo info = new TransitionInfo(this);

            // Read all the XML attributes before the reader advances through the elements
            info.ReadXmlAttributes(reader);
            var isPrecursor = Transition.IsPrecursor(info.IonType);
            var isCustom = Transition.IsCustom(info.IonType, group);
            CustomIon customIon = null;
            if (isCustom)
            {
                if (info.MeasuredIon != null)
                    customIon = info.MeasuredIon.CustomIon;
                else if (isPrecursor)
                    customIon = group.CustomIon;
                else
                    customIon = DocNodeCustomIon.Deserialize(reader);
            }
            info.ReadXmlElements(reader);

            Transition transition;
            if (isCustom)
            {
                transition = new Transition(group, isPrecursor ? group.PrecursorCharge : info.Charge, info.MassIndex,
                    customIon, info.IonType);
            }
            else if (isPrecursor)
            {
                transition = new Transition(group, info.IonType, group.Peptide.Length - 1, info.MassIndex,
                    group.PrecursorCharge, info.DecoyMassShift);
            }
            else
            {
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, group.Peptide.Length);
                transition = new Transition(group, info.IonType, offset, info.MassIndex, info.Charge, info.DecoyMassShift);
            }

            var losses = info.Losses;
            double massH = Settings.GetFragmentMass(group.LabelType, mods, transition, isotopeDist);

            var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(transition, losses, isotopeDist);

            if (group.DecoyMassShift.HasValue && !info.DecoyMassShift.HasValue)
                throw new InvalidDataException(Resources.SrmDocument_ReadTransitionXml_All_transitions_of_decoy_precursors_must_have_a_decoy_mass_shift);

            return new TransitionDocNode(transition, info.Annotations, losses,
                massH, isotopeDistInfo, info.LibInfo, info.Results);
        }



        private sealed class XmlReadContext
        {
            private readonly Dictionary<string, string> _dictNonDuplicatedNames = new Dictionary<string, string>();

            public string GetNonDuplicatedName(string name)
            {
                string nonDuplicatedName;
                if (!_dictNonDuplicatedNames.TryGetValue(name, out nonDuplicatedName))
                {
                    _dictNonDuplicatedNames.Add(name, name);
                    nonDuplicatedName = name;
                }
                return nonDuplicatedName;
            }
        }
    }


}
