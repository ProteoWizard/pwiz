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
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Crosslinking;
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
        private readonly StringPool _stringPool = new StringPool();
        private AnnotationScrubber _annotationScrubber;
        public DocumentFormat FormatVersion
        {
            get { return DocumentFormat; }
            private set
            {
                DocumentFormat = value;
            }
        }
        public PeptideGroupDocNode[] Children { get; private set; }

        private readonly Dictionary<string, string> _uniqueSpecies = new Dictionary<string, string>();

        /// <summary>
        /// In older versions of Skyline we would handle ion notation by building it into the molecule, 
        /// so our current C12H5[M+2H] would have been C12H7 - this requires special handling on read
        /// </summary>
        public bool DocumentMayContainMoleculesWithEmbeddedIons { get { return FormatVersion <= DocumentFormat.VERSION_3_71; } }

        public bool RemoveCalculatedAnnotationValues { get; set; } = true;

        /// <summary>
        /// Avoids duplication of species strings
        /// </summary>
        public string GetUniqueSpecies(string species)
        {
            if (species == null)
                return null;
            string uniqueSpecies;
            if (!_uniqueSpecies.TryGetValue(species, out uniqueSpecies))
            {
                _uniqueSpecies.Add(species, species);
                uniqueSpecies = species;
            }
            return uniqueSpecies;
        }

        private PeptideChromInfo ReadPeptideChromInfo(XmlReader reader, ChromFileInfo fileInfo)
        {
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            bool excludeFromCalibration = reader.GetBoolAttribute(ATTR.exclude_from_calibration);
            double? analyteConcentration = reader.GetNullableDoubleAttribute(ATTR.analyte_concentration);
            return new PeptideChromInfo(fileInfo.FileId, peakCountRatio, retentionTime, ImmutableList<PeptideLabelRatio>.EMPTY)
                .ChangeExcludeFromCalibration(excludeFromCalibration)
                .ChangeAnalyteConcentration(analyteConcentration);
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
                return libInfo.ChangeLibraryName(_stringPool.GetString(libInfo.LibraryName));
            }

            return null;
        }

        private TransitionGroupChromInfo ReadTransitionGroupChromInfo(XmlReader reader, ChromFileInfo fileInfo)
        {
            int optimizationStep = reader.GetIntAttribute(ATTR.step);
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            float? startTime = reader.GetNullableFloatAttribute(ATTR.start_time);
            float? endTime = reader.GetNullableFloatAttribute(ATTR.end_time);
            float? ccs = reader.GetNullableFloatAttribute(ATTR.ccs);
            float? ionMobilityMS1 = reader.GetNullableFloatAttribute(ATTR.drift_time_ms1);
            float? ionMobilityFragment = reader.GetNullableFloatAttribute(ATTR.drift_time_fragment);
            float? ionMobilityWindow = reader.GetNullableFloatAttribute(ATTR.drift_time_window);
            var ionMobilityUnits = eIonMobilityUnits.drift_time_msec;
            if (!ionMobilityWindow.HasValue)
            {
                ionMobilityUnits = GetAttributeMobilityUnits(reader, ATTR.ion_mobility_type, fileInfo);
                ionMobilityWindow = reader.GetNullableFloatAttribute(ATTR.ion_mobility_window);
                ionMobilityMS1 = reader.GetNullableFloatAttribute(ATTR.ion_mobility_ms1);
                ionMobilityFragment = reader.GetNullableFloatAttribute(ATTR.ion_mobility_fragment);
            }
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
                annotations = ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.precursor_result);
                // Convert q value and mProphet score annotations to numbers for the ChromInfo object
                annotations = ReadAndRemoveScoreAnnotation(annotations, MProphetResultsHandler.AnnotationName, ref qvalue);
                annotations = ReadAndRemoveScoreAnnotation(annotations, MProphetResultsHandler.MAnnotationName, ref zscore);
            }
            // Ignore userSet during load, since all values are still calculated
            // from the child transitions.  Otherwise inconsistency is possible.
//            bool userSet = reader.GetBoolAttribute(ATTR.user_set);
            const UserSet userSet = UserSet.FALSE;
            var transitionGroupIonMobilityInfo = TransitionGroupIonMobilityInfo.GetTransitionGroupIonMobilityInfo(ccs,
                ionMobilityMS1, ionMobilityFragment, ionMobilityWindow, ionMobilityUnits);
            return new TransitionGroupChromInfo(fileInfo.FileId,
                optimizationStep,
                peakCountRatio,
                retentionTime,
                startTime,
                endTime,
                transitionGroupIonMobilityInfo,
                fwhm,
                area, null, null, // Ms1 and Fragment values calculated later
                backgroundArea, null, null, // Ms1 and Fragment values calculated later
                height,
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

        public Annotations ReadTargetAnnotations(XmlReader reader, AnnotationDef.AnnotationTarget target)
        {
            var annotations = ReadAnnotations(reader);
            return _annotationScrubber.ScrubAnnotations(annotations, target);
        }

        /// <summary>
        /// Reads annotations from XML. The annotations should later be passed through
        /// <see cref="AnnotationScrubber.ScrubAnnotations"/> to ensure that the keys use a single
        /// string object and also that calculated annotations are removed.
        /// </summary>
        public static Annotations ReadAnnotations(XmlReader reader)
        {
            string note = null;
            int color = Annotations.EMPTY.ColorIndex;
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
                annotations[name] = reader.ReadElementString();
            }

            return note != null || annotations.Count > 0
                ? new Annotations(note, annotations, color)
                : Annotations.EMPTY;
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
            public MeasuredIon MeasuredIon { get; private set; }
            public bool Quantitative { get; private set; }
            public ExplicitTransitionValues ExplicitValues { get; private set; }

        public void ReadXml(XmlReader reader, DocumentFormat formatVersion, out double? declaredMz, ExplicitTransitionValues pre422ExplicitTransitionValues)
            {
                ReadXmlAttributes(reader, formatVersion, pre422ExplicitTransitionValues);
                ReadXmlElements(reader, out declaredMz);
            }

            public void ReadXmlAttributes(XmlReader reader, DocumentFormat formatVersion, ExplicitTransitionValues pre422ExplicitTransitionValues)
            {
                // Accept uppercase and lowercase for backward compatibility with v0.1
                IonType = reader.GetEnumAttribute(ATTR.fragment_type, IonType.y, XmlUtil.EnumCase.lower);
                Ordinal = reader.GetIntAttribute(ATTR.fragment_ordinal);
                MassIndex = reader.GetIntAttribute(ATTR.mass_index);
                // NOTE: PrecursorCharge is used only in TransitionInfo.ReadUngroupedTransitionListXml()
                //       to support v0.1 document format
                PrecursorAdduct = Adduct.FromStringAssumeProtonated(reader.GetAttribute(ATTR.precursor_charge));
                ProductAdduct = Adduct.FromStringAssumeProtonated(reader.GetAttribute(ATTR.product_charge));
                DecoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
                Quantitative = reader.GetBoolAttribute(ATTR.quantitative, true);
                OrphanedCrosslinkIon = reader.GetBoolAttribute(ATTR.orphaned_crosslink_ion);
                string measuredIonName = reader.GetAttribute(ATTR.measured_ion_name);
                if (measuredIonName != null)
                {
                    MeasuredIon = Settings.TransitionSettings.Filter.MeasuredIons.SingleOrDefault(
                        i => i.Name.Equals(measuredIonName));
                    if (MeasuredIon == null)
                        throw new InvalidDataException(String.Format(Resources.TransitionInfo_ReadXmlAttributes_The_reporter_ion__0__was_not_found_in_the_transition_filter_settings_, measuredIonName));
                    IonType = IonType.custom;
                }

                ExplicitValues = pre422ExplicitTransitionValues ?? ReadExplicitTransitionValuesAttributes(reader, formatVersion);
            }

            public void ReadXmlElements(XmlReader reader, out double? declaredProductMz)
            {
                declaredProductMz = null;
                LinkedFragmentIons = new List<IonOrdinal>();
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();
                    Annotations = _documentReader.ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.transition); // This is reliably first in all versions
                    while (reader.IsStartElement())
                    {  // The order of these elements may depend on the version of the file being read
                        if (reader.IsStartElement(EL.losses))
                            Losses = ReadTransitionLosses(reader);
                        else if (reader.IsStartElement(EL.linked_fragment_ion))
                        {
                            if (_documentReader.FormatVersion < DocumentFormat.FLAT_CROSSLINKS)
                            {
                                LegacyFragmentIons = LegacyFragmentIons ?? new List<LegacyComplexFragmentIonName>();
                                LegacyFragmentIons.Add(ReadLegacyLinkedFragmentIon(reader));
                            }
                            else
                            {
                                LinkedFragmentIons.Add(ReadLinkedFragmentIon(reader));
                            }
                        }
                        else if (reader.IsStartElement(EL.transition_lib_info))
                            LibInfo = ReadTransitionLibInfo(reader);
                        else if (reader.IsStartElement(EL.transition_results) || reader.IsStartElement(EL.results_data))
                            Results = ReadTransitionResults(reader);
                        // Discard informational elements.  These values are always
                        // calculated from the settings to ensure consistency.
                        // Note that we do use product_mz for sanity checks and to disambiguate some older mass-only small molecule documents.
                        else if (reader.IsStartElement(EL.product_mz))
                            declaredProductMz = reader.ReadElementContentAsDoubleInvariant();
                        else 
                            reader.Skip();
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

            private LegacyComplexFragmentIonName ReadLegacyLinkedFragmentIon(XmlReader reader)
            {
                IonOrdinal fragmentIonType;
                string strFragmentType = reader.GetAttribute(ATTR.fragment_type);
                if (strFragmentType == null)
                {
                    // blank fragment type means orphaned fragment ion
                    fragmentIonType = IonOrdinal.Empty;
                }
                else
                {
                    fragmentIonType = new IonOrdinal(TypeSafeEnum.Parse<IonType>(strFragmentType), reader.GetIntAttribute(ATTR.fragment_ordinal));
                }
                    
                var modificationSite = new ModificationSite(reader.GetIntAttribute(ATTR.index_aa),
                    reader.GetAttribute(ATTR.modification_name));
                var linkedIon = new LegacyComplexFragmentIonName(modificationSite, fragmentIonType);
                bool empty = reader.IsEmptyElement;
                reader.Read();
                if (!empty)
                {
                    while (reader.IsStartElement())
                    {
                        if (reader.IsStartElement(EL.linked_fragment_ion))
                        {
                            linkedIon.Children.Add(ReadLegacyLinkedFragmentIon(reader));
                        }
                        else
                        {
                            throw new InvalidDataException();
                        }
                    }
                    reader.ReadEndElement();
                }

                return linkedIon;
            }

            private IonOrdinal ReadLinkedFragmentIon(XmlReader reader)
            {
                var ionType = reader.GetEnumAttribute(ATTR.fragment_type, IonType.custom);
                var ordinal = reader.GetIntAttribute(ATTR.fragment_ordinal);
                reader.Read();
                return ionType == IonType.custom ? IonOrdinal.Empty : new IonOrdinal(ionType, ordinal);
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
                if (reader.IsStartElement(EL.results_data))
                {
                    string strContent = reader.ReadElementString();
                    byte[] data = Convert.FromBase64String(strContent);
                    var protoTransitionResults = new SkylineDocumentProto.Types.TransitionResults();
                    protoTransitionResults.MergeFrom(data);
                    return TransitionChromInfo.FromProtoTransitionResults(_documentReader._annotationScrubber, Settings, protoTransitionResults);
                }
                if (reader.IsStartElement(EL.transition_results))
                    return _documentReader.ReadResults(reader, EL.transition_peak, ReadTransitionPeak);
                return null;
            }

            private TransitionChromInfo ReadTransitionPeak(XmlReader reader, ChromFileInfo fileInfo)
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
                short? pointsAcross = (short?) reader.GetNullableIntAttribute(ATTR.points_across);
                var identified = reader.GetEnumAttribute(ATTR.identified, PeakIdentificationFastLookup.Dict,
                    PeakIdentification.FALSE, XmlUtil.EnumCase.upper);
                UserSet userSet = reader.GetEnumAttribute(ATTR.user_set, UserSetFastLookup.Dict,
                    UserSet.FALSE, XmlUtil.EnumCase.upper);
                double? ionMobility = reader.GetNullableDoubleAttribute(ATTR.drift_time);
                eIonMobilityUnits ionMobilityUnits = eIonMobilityUnits.drift_time_msec;
                if (!ionMobility.HasValue)
                {
                    ionMobility = reader.GetNullableDoubleAttribute(ATTR.ion_mobility);
                    ionMobilityUnits = GetAttributeMobilityUnits(reader, ATTR.ion_mobility_type, fileInfo);
                }
                double? ionMobilityWindow = reader.GetNullableDoubleAttribute(ATTR.drift_time_window) ??
                                            reader.GetNullableDoubleAttribute(ATTR.ion_mobility_window);
                var annotations = Annotations.EMPTY;
                bool forcedIntegration = reader.GetBoolAttribute(ATTR.forced_integration, false);
                if (!reader.IsEmptyElement)
                {
                    reader.ReadStartElement();
                    annotations = _documentReader.ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.transition_result);
                }
                return new TransitionChromInfo(fileInfo.FileId,
                    optimizationStep,
                    massError,
                    retentionTime,
                    startRetentionTime,
                    endRetentionTime,
                    IonMobilityFilter.GetIonMobilityFilter(ionMobility, ionMobilityUnits, ionMobilityWindow, null), 
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
                    annotations,
                    userSet,
                    forcedIntegration);
            }
        }

        private Results<TItem> ReadResults<TItem>(XmlReader reader, string start,
            Func<XmlReader, ChromFileInfo, TItem> readInfo)
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
                var fileInfo = chromatogramSet.GetFileInfo(fileInfoId);

                TItem chromInfo = readInfo(reader, fileInfo);
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
                FormatVersion = new DocumentFormat(formatVersionNumber);
                if (FormatVersion.CompareTo(DocumentFormat.CURRENT) > 0)
                {
// Resharper disable ImpureMethodCallOnReadonlyValueField
                    throw new VersionNewerException(
                        string.Format(Resources.SrmDocument_ReadXml_The_document_format_version__0__is_newer_than_the_version__1__supported_by__2__,
                            formatVersionNumber, DocumentFormat.CURRENT.AsDouble(), Install.ProgramNameAndVersion));
// Resharper enable ImpureMethodCallOnReadonlyValueField
                }
            }

            reader.ReadStartElement();  // Start document element
            var srmSettings = reader.DeserializeElement<SrmSettings>() ?? SrmSettingsList.GetDefault();
            _annotationScrubber = AnnotationScrubber.MakeAnnotationScrubber(_stringPool, srmSettings.DataSettings, RemoveCalculatedAnnotationValues);
            srmSettings = _annotationScrubber.ScrubSrmSettings(srmSettings);
            Settings = srmSettings;
            
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

                if (reader.IsStartElement(AuditLogList.XML_ROOT))
                    reader.Skip();
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

        private ProteinMetadata ReadProteinMetadataXML(XmlReader reader, bool labelNameAndDescription)
        {
            var labelPrefix = labelNameAndDescription ? @"label_" : string.Empty;
            return new ProteinMetadata(
                reader.GetAttribute(labelPrefix + ATTR.name),
                reader.GetAttribute(labelPrefix + ATTR.description),
                reader.GetAttribute(ATTR.preferred_name),
                reader.GetAttribute(ATTR.accession),
                reader.GetAttribute(ATTR.gene),
                GetUniqueSpecies(reader.GetAttribute(ATTR.species)),
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

            var annotations = ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.protein);

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
            if (sequence.StartsWith(@"X") && sequence.EndsWith(@"X"))
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
        private ProteinMetadata[] ReadAltProteinListXml(XmlReader reader)
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
            var proportionDecoysMatch = reader.GetNullableDoubleAttribute(ATTR.decoy_match_proportion);

            PeptideGroup group = new PeptideGroup(isDecoy);

            Annotations annotations = Annotations.EMPTY;
            PeptideDocNode[] children = null;

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                annotations = ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.protein);

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
                children ?? new PeptideDocNode[0], autoManageChildren, proportionDecoysMatch);
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
        /// Deserialize any explictly set CE, DT, etc information from transition attributes
        /// </summary>
        private static ExplicitTransitionValues ReadExplicitTransitionValuesAttributes(XmlReader reader, DocumentFormat formatVersion )
        {
            double? importedCollisionEnergy = reader.GetNullableDoubleAttribute(ATTR.explicit_collision_energy);
            double? importedIonMobilityHighEnergyOffset =
                reader.GetNullableDoubleAttribute(ATTR.explicit_drift_time_high_energy_offset_msec) ??
                reader.GetNullableDoubleAttribute(ATTR.explicit_ion_mobility_high_energy_offset);
            double? importedSLens = reader.GetNullableDoubleAttribute(formatVersion.CompareTo(DocumentFormat.VERSION_3_52) < 0 ? ATTR.s_lens_obsolete : ATTR.explicit_s_lens);
            double? importedConeVoltage = reader.GetNullableDoubleAttribute(formatVersion.CompareTo(DocumentFormat.VERSION_3_52) < 0 ? ATTR.cone_voltage_obsolete : ATTR.explicit_cone_voltage);
            double? importedDeclusteringPotential = reader.GetNullableDoubleAttribute(ATTR.explicit_declustering_potential);
            return ExplicitTransitionValues.Create(importedCollisionEnergy,
                importedIonMobilityHighEnergyOffset, importedSLens, importedConeVoltage, importedDeclusteringPotential);
        }


        /// <summary>
        /// Deserialize any explictly set CE, DT, etc information from precursor attributes
        /// </summary>
        private static ExplicitTransitionGroupValues ReadExplicitTransitionGroupValuesAttributes(XmlReader reader, DocumentFormat formatVersion, out ExplicitTransitionValues pre422ExplicitValues)
        {
            double? importedCompensationVoltage = reader.GetNullableDoubleAttribute(ATTR.explicit_compensation_voltage); // Found in older formats, obsolete as of 4.22. Now a combination of ion mobility and ion mobility units values.
            double? importedDriftTimeMsec = reader.GetNullableDoubleAttribute(ATTR.explicit_drift_time_msec);
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
                var attr = reader.GetAttribute(ATTR.explicit_ion_mobility_units);
                importedIonMobilityUnits = SmallMoleculeTransitionListReader.IonMobilityUnitsFromAttributeValue(attr);
            }
            double? importedIonMobility = importedDriftTimeMsec ?? importedCompensationVoltage ?? reader.GetNullableDoubleAttribute(ATTR.explicit_ion_mobility);
            double? importedCCS = reader.GetNullableDoubleAttribute(ATTR.explicit_ccs_sqa);
            pre422ExplicitValues = formatVersion >= DocumentFormat.VERSION_4_22 ? null : ReadExplicitTransitionValuesAttributes(reader, formatVersion); // Formerly (pre-4.22) these per-transition values were serialized at peptide level
            // CollisionEnergy was made per-transition in 4.22, we added a per-precursor override in 20.12
            double? importedCollisionEnergy = pre422ExplicitValues?.CollisionEnergy ?? reader.GetNullableDoubleAttribute(ATTR.explicit_collision_energy);
            if (pre422ExplicitValues != null)
            {
                pre422ExplicitValues = pre422ExplicitValues.ChangeCollisionEnergy(null); // As of 20.12 we're back to tracking this at precursor level (with per-transition overrides)
            }
            return ExplicitTransitionGroupValues.Create(importedCollisionEnergy, importedIonMobility, importedIonMobilityUnits, importedCCS);
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
            string attributeGroupId = reader.GetAttribute(ATTR.attribute_group_id);
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
            CrosslinkStructure crosslinkStructure = null;
            Results<PeptideChromInfo> results = null;
            TransitionGroupDocNode[] children = null;
            Adduct adduct = Adduct.EMPTY;
            var customMolecule = isCustomMolecule ? CustomMolecule.Deserialize(reader, out adduct) : null; // This Deserialize only reads attribures, doesn't advance the reader
            if (customMolecule != null)
            {
                if (DocumentMayContainMoleculesWithEmbeddedIons && string.IsNullOrEmpty(customMolecule.Formula) && customMolecule.MonoisotopicMass.IsMassH())
                {
                    // Defined by mass only, assume it's not massH despite how it may have been written
                    customMolecule = new CustomMolecule(
                        customMolecule.MonoisotopicMass.ChangeIsMassH(false),
                        customMolecule.AverageMass.ChangeIsMassH(false),
                        customMolecule.Name);
                }
            }
            Assume.IsTrue(DocumentMayContainMoleculesWithEmbeddedIons || adduct.IsEmpty); // Shouldn't be any charge info at the peptide/molecule level
            var peptide = isCustomMolecule ?
                new Peptide(customMolecule) :
                new Peptide(group as FastaSequence, sequence, start, end, missedCleavages, isDecoy);
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                var pushReader = reader; // Preserve in case we substitute with a backward compatibility reader
                if (isCustomMolecule && DocumentMayContainMoleculesWithEmbeddedIons)
                {
                    // If this is an older small molecule file, clean up any problems with former data model
                    reader = new Pre372CustomIonTransitionGroupHandler(reader, Settings.TransitionSettings.Instrument.MzMatchTolerance).Read(ref peptide);
                }
                reader.ReadStartElement();
                if (reader.IsStartElement())
                    annotations = ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.peptide);
                if (!isCustomMolecule)
                {
                    mods = ReadExplicitMods(reader, peptide)?.ConvertFromLegacyCrosslinkStructure();
                    SkipImplicitModsElement(reader);
                    lookupMods = ReadLookupMods(reader, lookupSequence);
                    crosslinkStructure = ReadCrosslinkStructure(reader);
                    if (crosslinkStructure != null && !crosslinkStructure.IsEmpty)
                    {
                        mods = mods ?? new ExplicitMods(peptide, null, null);
                        mods = mods.ChangeCrosslinkStructure(crosslinkStructure);
                    }
                }
                results = ReadPeptideResults(reader);

                if (reader.IsStartElement(EL.precursor))
                {
                    children = ReadTransitionGroupListXml(reader, peptide, mods);
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

                pushReader.ReadEndElement();
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
                .ChangeAttributeGroupId(attributeGroupId);

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
        /// <returns>A new array of <see cref="TransitionGroupDocNode"/></returns>
        private TransitionGroupDocNode[] ReadTransitionGroupListXml(XmlReader reader, Peptide peptide, ExplicitMods mods)
        {
            var list = new List<TransitionGroupDocNode>();
            while (reader.IsStartElement(EL.precursor))
                list.Add(ReadTransitionGroupXml(reader, peptide, mods));
            return list.ToArray();
        }

        private TransitionGroupDocNode ReadTransitionGroupXml(XmlReader reader, Peptide peptide, ExplicitMods mods)
        {
            var precursorCharge = reader.GetIntAttribute(ATTR.charge);
            var precursorAdduct = Adduct.FromChargeProtonated(precursorCharge);  // Read integer charge
            var typedMods = ReadLabelType(reader, IsotopeLabelType.light);

            int? decoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
            var explicitTransitionGroupValues = ReadExplicitTransitionGroupValuesAttributes(reader, FormatVersion, out var pre422ExplicitValues);
            if (peptide.IsCustomMolecule)
            {
                var ionFormula = reader.GetAttribute(ATTR.ion_formula);
                if (ionFormula != null)
                {
                    ionFormula = ionFormula.Trim(); // We've seen trailing spaces in the wild
                }
                Molecule mol;
                string neutralFormula;
                Adduct adduct;
                var isFormulaWithAdduct = IonInfo.IsFormulaWithAdduct(ionFormula, out mol, out adduct, out neutralFormula);
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
                    var ionString = precursorAdduct.ApplyToFormula(neutralFormula);
                    var moleculeWithAdduct = precursorAdduct.ApplyToFormula(peptide.CustomMolecule.Formula);
                    Assume.IsTrue(Equals(ionString, moleculeWithAdduct), @"Expected precursor ion formula to match parent molecule with adduct applied");
                }
            }
            var group = new TransitionGroup(peptide, precursorAdduct, typedMods.LabelType, false, decoyMassShift);
            var children = new TransitionDocNode[0];    // Empty until proven otherwise
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            double? precursorConcentration = reader.GetNullableDoubleAttribute(ATTR.precursor_concentration);

            TransitionGroupDocNode nodeGroup;
            if (reader.IsEmptyElement)
            {
                reader.Read();

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
                reader.ReadStartElement();
                var annotations = ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.precursor);
                var libInfo = ReadTransitionGroupLibInfo(reader);
                var results = ReadTransitionGroupResults(reader);

                nodeGroup = new TransitionGroupDocNode(group,
                                                  annotations,
                                                  Settings,
                                                  mods,
                                                  libInfo,
                                                  explicitTransitionGroupValues,
                                                  results,
                                                  children,
                                                  autoManageChildren);
                children = ReadTransitionListXml(reader, nodeGroup, mods, pre422ExplicitValues);

                reader.ReadEndElement();

                nodeGroup = (TransitionGroupDocNode)nodeGroup.ChangeChildrenChecked(children);
            }
            nodeGroup = nodeGroup.ChangePrecursorConcentration(precursorConcentration);
            return nodeGroup;
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
                double? declaredProductMz;
                info.ReadXml(reader, FormatVersion, out declaredProductMz, null);

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
        private TransitionDocNode[] ReadTransitionListXml(XmlReader reader, 
            TransitionGroupDocNode nodeGroup, ExplicitMods mods, ExplicitTransitionValues pre422ExplicitTransitionValues)
        {
            var group = nodeGroup.TransitionGroup;
            var isotopeDist = nodeGroup.IsotopeDist;
            var list = new List<TransitionDocNode>();
            CrosslinkBuilder crosslinkBuilder = new CrosslinkBuilder(Settings, nodeGroup.Peptide, mods, nodeGroup.LabelType);
            if (reader.IsStartElement(EL.transition_data))
            {
                string strContent = reader.ReadElementString();
                byte[] data = Convert.FromBase64String(strContent);
                var transitionData = new SkylineDocumentProto.Types.TransitionData();
                transitionData.MergeFrom(data);
                foreach (var transitionProto in transitionData.Transitions)
                {
                    list.Add(TransitionDocNode.FromTransitionProto(_annotationScrubber, Settings, group, mods, isotopeDist, pre422ExplicitTransitionValues, crosslinkBuilder, transitionProto));
                }
            }
            else
            {
                while (reader.IsStartElement(EL.transition))
                    list.Add(ReadTransitionXml(reader, group, mods, isotopeDist, pre422ExplicitTransitionValues, crosslinkBuilder));
            }
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
        private TransitionDocNode ReadTransitionXml(XmlReader reader, TransitionGroup group,
            ExplicitMods mods, IsotopeDistInfo isotopeDist, ExplicitTransitionValues pre422ExplicitTransitionValues, CrosslinkBuilder crosslinkBuilder)
        {
            TransitionInfo info = new TransitionInfo(this);

            // Read all the XML attributes before the reader advances through the elements
            info.ReadXmlAttributes(reader, FormatVersion, pre422ExplicitTransitionValues);
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
                    customMolecule = CustomMolecule.Deserialize(reader, out adduct);
                    if (DocumentMayContainMoleculesWithEmbeddedIons && string.IsNullOrEmpty(customMolecule.Formula) && customMolecule.MonoisotopicMass.IsMassH())
                    {
                        // Defined by mass only, assume it's not massH despite how it may have been written
                        customMolecule = new CustomMolecule(customMolecule.MonoisotopicMass.ChangeIsMassH(false), customMolecule.AverageMass.ChangeIsMassH(false),
                            customMolecule.Name);
                    }
                }
            }
            double? declaredProductMz;
            info.ReadXmlElements(reader, out declaredProductMz);

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
                    string newFormula = null;
                    if (!string.IsNullOrEmpty(customMolecule.Formula) &&
                        Math.Abs(customMolecule.MonoisotopicMass - Math.Abs(adduct.AdductCharge) * declaredProductMz.Value) < .01)
                    {
                        // Adjust hydrogen count to get a molecular mass that makes sense for charge and mz
                        newFormula = Molecule.AdjustElementCount(customMolecule.Formula, @"H", -adduct.AdductCharge);
                    }
                    if (!string.IsNullOrEmpty(newFormula))
                    {
                        customMolecule = new CustomMolecule(newFormula, customMolecule.Name);
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
                    info.ExplicitValues, info.Results);
            }
            else
            {
                var mass = Settings.GetFragmentMass(group, mods, transition, isotopeDist);
                node = new TransitionDocNode(transition, info.Annotations, losses,
                    mass, quantInfo, info.ExplicitValues, info.Results);
            }

            ValidateSerializedVsCalculatedProductMz(declaredProductMz, node);  // Sanity check

            return node;
        }

        /// <summary>
        /// Verify that any mz values we serialize for informational purposes agree with what we calculate upon reading in again
        /// </summary>
        private void ValidateSerializedVsCalculatedProductMz(double? declaredProductMz, TransitionDocNode node)
        {
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
