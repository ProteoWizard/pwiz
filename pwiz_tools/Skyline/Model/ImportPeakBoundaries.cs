/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Text;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Object to import Peak Boundaries from a file into an
    /// existing SRM document.
    /// </summary>
    public class PeakBoundaryImporter
    {
        public SrmDocument Document { get; private set; }
        public int CountMissing { get; private set; }
        public List<string> AnnotationsAdded { get; private set; }
        // Each unrecognized item mapped to the first input-file line it appeared on, so callers (e.g. the
        // command line) can point the user at the offending row. The keys are the deduped set of
        // unrecognized items the GUI and audit log consume (via .Keys).
        public Dictionary<string, long> UnrecognizedPeptides { get; private set; }
        public Dictionary<string, long> UnrecognizedFiles { get; private set; }
        public Dictionary<UnrecognizedChargeState, long> UnrecognizedChargeStates { get; private set; }

        public PeakBoundaryImporter(SrmDocument document)
        {
            Document = document;
            AnnotationsAdded = new List<string>();
            UnrecognizedFiles = new Dictionary<string, long>();
            UnrecognizedPeptides = new Dictionary<string, long>();
            UnrecognizedChargeStates = new Dictionary<UnrecognizedChargeState, long>();
        }

        public struct UnrecognizedChargeState : IEquatable<UnrecognizedChargeState>
        {
            public string Peptide;
            public string File;
            public Adduct Charge;

            public UnrecognizedChargeState(Adduct charge, string file, string peptide)
            {
                Charge = charge;
                File = file;
                Peptide = peptide;
            }

            public string PrintLine(char separator)
            {
                var sb = new StringBuilder();
                sb.Append(Peptide);
                sb.Append(separator);
                sb.Append(File);
                sb.Append(separator);
                sb.Append(Charge);
                return sb.ToString();
            }

            public override string ToString()
            {
                return PrintLine(' ');
            }

            public bool Equals(UnrecognizedChargeState other)
            {
                return Peptide == other.Peptide && File == other.File && Equals(Charge, other.Charge);
            }

            public override bool Equals(object obj)
            {
                return obj is UnrecognizedChargeState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Peptide != null ? Peptide.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (File != null ? File.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Charge != null ? Charge.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        // replicate_name is appended last so the integer value of every existing Field is unchanged
        public enum Field { modified_peptide, filename, apex_time, start_time, end_time, charge, is_decoy, sample_name, q_value, score, replicate_name }

        public static readonly int[] REQUIRED_FIELDS =
            {
                (int) Field.modified_peptide,
                (int) Field.filename,
                (int) Field.start_time,
                (int) Field.end_time
            };

        public static int[] REQUIRED_NO_CHROM { get { return REQUIRED_FIELDS.Take(2).ToArray(); }}

        /// <summary>
        /// File-identity columns: a peak-boundary row is matched to a result file by its on-disk file name
        /// or, alternatively, by its (vendor-independent) replicate name. At least one of these columns must
        /// be present in the file, but filename is no longer individually required (see <see cref="RequiredFieldsForImport"/>).
        /// </summary>
        public static readonly int[] FILE_ID_FIELDS =
            {
                (int) Field.filename,
                (int) Field.replicate_name
            };

        /// <summary>
        /// Required columns for <see cref="Import(string,IProgressMonitor,long,bool,bool)"/>. File identity is
        /// validated separately via <see cref="FILE_ID_FIELDS"/> (filename OR replicate name), so filename is
        /// dropped from the always-required set, allowing a file to key purely on replicate name.
        /// </summary>
        private static int[] RequiredFieldsForImport(bool changePeaks)
        {
            return (changePeaks ? REQUIRED_FIELDS : REQUIRED_NO_CHROM).Where(f => f != (int) Field.filename).ToArray();
        }

        // ReSharper disable LocalizableElement

        private static string[] _peptide_synonyms; // Cache for performance reasons (L10N stuff is expensive)
        public static string[] PEPTIDE_SYNONYMS
        {
            get
            {
                if (_peptide_synonyms == null)
                {
                    var headers = new List<string>()
                    {
                        "PeptideModifiedSequence", "ModifiedSequence", "FullPeptideName", "EG.ModifiedSequence",
                    };
                    // Also recognize column captions in all supported display languages
                    foreach (var culture in CultureUtil.AvailableDisplayLanguages())
                    {
                        using (var c = new CurrentCultureSetter(culture))
                        {
                            headers.Add(ColumnCaptions.PeptideModifiedSequence);
                            headers.Add(ColumnCaptions.ModifiedSequence);
                        }
                    }
                    _peptide_synonyms = headers.ToArray();
                }
                return _peptide_synonyms;
            }
        }

        private static string[] _molecule_synonyms; // Cache for performance reasons (L10N stuff is expensive)
        public static string[] MOLECULE_SYNONYMS
        {
            get
            {
                if (_molecule_synonyms == null) 
                {
                    var headers = new List<string>()
                    {
                        "MoleculeName"
                    };
                    // Also recognize column captions in all supported display languages
                    foreach (var culture in CultureUtil.AvailableDisplayLanguages())
                    {
                        using (var c = new CurrentCultureSetter(culture))
                        {
                            headers.Add(ColumnCaptions.Molecule);
                            headers.Add(ColumnCaptions.MoleculeName);
                        }
                    }
                    _molecule_synonyms = headers.ToArray();
                }
                return _molecule_synonyms;
            }
        }

        // NOTE: The first name is what appears in error messages about missing required fields
        private static string[][] _field_names; // Cache for performance reasons (L10N stuff is expensive)
        public static string[][] FIELD_NAMES
        {
            get
            {
                // Since tests may change localized versions of column captions and reading
                // a whole file dwarfs newing up this array.

                if (_field_names == null)
                {
                    // Recognize column captions in all supported display languages
                    var allFieldNames = new List<string []>();
                    foreach (var culture in CultureUtil.AvailableDisplayLanguages())
                    {
                        using (var c = new CurrentCultureSetter(culture))
                        {
                            var currentFieldNames = new[]
                            {
                                PEPTIDE_SYNONYMS.Concat(MOLECULE_SYNONYMS).ToArray(),
                                new[] {"FileName", "filename", "align_origfilename", "R.FileName", ColumnCaptions.FileName},
                                new[] {"Apex", "RetentionTime", "BestRetentionTime", "RT", ColumnCaptions.RetentionTime, ColumnCaptions.BestRetentionTime},
                                new[] {"MinStartTime", "leftWidth", ColumnCaptions.MinStartTime},
                                new[] {"MaxEndTime", "rightWidth", ColumnCaptions.MaxEndTime},
                                new[] {"PrecursorCharge", "Charge", "FG.Charge", ColumnCaptions.PrecursorCharge,
                                    "Adduct", "PrecursorAdduct", ColumnCaptions.PrecursorAdduct},
                                new[] {"PrecursorIsDecoy", "Precursor Is Decoy", "IsDecoy", "decoy", ColumnCaptions.IsDecoy},
                                new[] {"SampleName", ColumnCaptions.SampleName},
                                new[] {"DetectionQValue", "m_score", ColumnCaptions.DetectionQValue},
                                new[] {"DetectionZScore", "d_score", ColumnCaptions.DetectionZScore},
                                // Accept every report header that carries the replicate name: the invariant/export
                                // "ReplicateName", the Replicate.Name property caption ("Replicate Name"), and the
                                // Replicate object column caption ("Replicate", whose exported value is also the name).
                                new[] {MProphetResultsHandler.REPLICATE_NAME_COLUMN, ColumnCaptions.ReplicateName, ColumnCaptions.Replicate},
                            };
                            for (var field = 0; field < currentFieldNames.Length; field++)
                            {
                                if (field == allFieldNames.Count)
                                {
                                    allFieldNames.Add(currentFieldNames[field]);
                                }
                                else
                                {
                                    var seen = allFieldNames[field];
                                    var newNames = currentFieldNames[field].Where(header => !seen.Contains(header));
                                    allFieldNames[field] = allFieldNames[field].Concat(newNames).ToArray();
                                }
                            }
                        }
                    }
                    _field_names = allFieldNames.ToArray();
                }

                return _field_names;
            }
        }
        // ReSharper restore LocalizableElement

        public static readonly string[] STANDARD_FIELD_NAMES = FIELD_NAMES.Select(fieldNames => fieldNames[0]).ToArray();


        /// <summary>
        /// Imports peak boundaries from a file to an SrmDocument
        /// </summary>
        /// <param name="inputFile">path to the input file</param>
        /// <param name="progressMonitor"></param>
        /// <param name="lineCount">number of lines in the file</param>
        /// <param name="removeMissing">if true, all results in the document that are NOT found in the file will have peak boundaries removed</param>
        /// <param name="changePeaks">set to false to to only import annotations, not actually adjust peaks</param>
        /// <returns></returns>
        public SrmDocument Import(string inputFile, IProgressMonitor progressMonitor, long lineCount, bool removeMissing = false, bool changePeaks = true)
        {
            CountMissing = 0;
            // Determine if the peak times are in minutes or seconds
            bool isMinutes = true;
            if (changePeaks)
            {
                using (var reader = new StreamReader(inputFile))
                {
                    isMinutes = IsMinutesPeakBoundaries(reader);
                }
            }

            // Import the peak times
            SrmDocument doc;
            using (var reader = new StreamReader(inputFile))
            {
                doc = Import(reader, progressMonitor, lineCount, isMinutes, removeMissing, changePeaks);
            }
            return doc;
        }

        public ModifiedDocument ModifyDocument(SrmDocument.DOCUMENT_TYPE documentType, string inputFile, IProgressMonitor progressMonitor, long lineCount,
            bool removeMissing = false, bool changePeaks = true)
        {
            var originalDocument = Document;
            var modifiedDocument =
                new ModifiedDocument(Import(inputFile, progressMonitor, lineCount, removeMissing, changePeaks));
            if (ReferenceEquals(modifiedDocument.Document, originalDocument))
            {
                return null;
            }

            var docPair = SrmDocumentPair.Create(originalDocument, modifiedDocument.Document, documentType);
            var allInfo = new List<MessageInfo>();
            AddMessageInfo(allInfo, MessageType.removed_unrecognized_peptide, docPair.OldDocumentType, UnrecognizedPeptides.Keys);
            AddMessageInfo(allInfo, MessageType.removed_unrecognized_file, docPair.OldDocumentType,
                UnrecognizedFiles.Keys.Select(AuditLogPath.Create));
            AddMessageInfo(allInfo, MessageType.removed_unrecognized_charge_state, docPair.OldDocumentType, UnrecognizedChargeStates.Keys);

            var auditLogEntry = AuditLogEntry.CreateSimpleEntry(MessageType.imported_peak_boundaries,
                    docPair.OldDocumentType,
                    Path.GetFileName(inputFile))
                .AppendAllInfo(allInfo);
            return modifiedDocument.ChangeAuditLogEntry(auditLogEntry);
        }

        private static void AddMessageInfo<T>(IList<MessageInfo> messageInfos, MessageType type, SrmDocument.DOCUMENT_TYPE docType, IEnumerable<T> items)
        {
            messageInfos.AddRange(items.Select(item => new MessageInfo(type, docType, item)));
        }

        public bool IsMinutesPeakBoundaries(TextReader reader)
        {
            long linesRead = 0;
            var peakTimes = new List<double>();
            string line = reader.ReadLine();
            int[] fieldIndices;
            char correctSeparator = ReadFirstLine(line, FIELD_NAMES, RequiredFieldsForImport(true), FILE_ID_FIELDS, out fieldIndices, out _);
            // Find the first 50 peak times that are not #N/A, if any is larger than the maxRT, then times are in seconds
            while (peakTimes.Count < 50)
            {
                if (string.IsNullOrEmpty(line = reader.ReadLine()))
                    break;
                linesRead++;
                var dataFields = new DataFields(fieldIndices, line.ParseDsvFields(correctSeparator), FIELD_NAMES);
                double? startTime = dataFields.GetTime(Field.start_time, 1.0,
                        Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_,
                        linesRead);
                if (startTime.HasValue)
                    peakTimes.Add(startTime.Value);
            }
            bool isMinutes = peakTimes.Count == 0 || peakTimes.Max() < GetMaxRt(Document);
            return isMinutes;
        }

        public static double GetMaxRt(SrmDocument document)
        {
            if (!document.Settings.HasResults)
            {
                return 0;
            }
            var chromFileInfos = document.Settings.MeasuredResults.MSDataFileInfos;
            double maxRt = chromFileInfos.Select(info => info.MaxRetentionTime).Max();
            return maxRt != 0 ? maxRt : 720;    // 12 hours as default maximum RT in minutes
        }

        public SrmDocument Import(TextReader reader, IProgressMonitor progressMonitor, long lineCount, bool isMinutes, bool removeMissing = false, bool changePeaks = true)
        {
            _progressMonitor = progressMonitor;
            _status = new ProgressStatus(ModelResources.PeakBoundaryImporter_Import_Importing_Peak_Boundaries);
            _progressPercent = -1;

            // Read the entire file first, resolving every line to the peptides and result files it refers to,
            // and grouping the changes by peptide. Nothing about the document is changed by this pass.
            var peptideChanges = ReadPeakBoundaries(reader, lineCount, isMinutes, changePeaks, out var boundaryLines);
            if (peptideChanges == null)
                return Document;    // Canceled

            // Apply the changes for each peptide on a background thread. Because all of the changes for a
            // peptide are applied to a single PeptideDocNode, the document itself is rebuilt only once, at
            // the end, instead of once per line of the file.
            if (!ApplyPeakBoundaries(peptideChanges, changePeaks))
                return Document;    // Canceled

            // Report the lines which matched a peptide and a file, but no precursor of that peptide. This is
            // done here, in line order, so that the reported line numbers do not depend on thread scheduling.
            foreach (var boundaryLine in boundaryLines)
            {
                if (boundaryLine.FoundSample)
                    continue;
                var unrecognizedChargeState = new UnrecognizedChargeState(boundaryLine.Charge,
                    boundaryLine.FileIdentity, boundaryLine.ModifiedPeptideString);
                if (!UnrecognizedChargeStates.ContainsKey(unrecognizedChargeState))
                    UnrecognizedChargeStates[unrecognizedChargeState] = boundaryLine.LineNumber;
            }

            var docReference = (SrmDocument) Document.ChangeIgnoreChangingChildren(true);
            var docNew = ReplacePeptides(docReference, peptideChanges);
            // Remove peaks from the document that weren't in the file.
            if (removeMissing)
            {
                var trackAdjustedResults = new HashSet<ResultsKey>();
                foreach (var pepChanges in peptideChanges)
                    trackAdjustedResults.UnionWith(pepChanges.AdjustedResults);
                docNew = RemoveMissing(docNew, trackAdjustedResults, changePeaks);
            }
            // If nothing has changed, return the old Document before ChangeIgnoreChangingChildren was turned off
            if (!ReferenceEquals(docNew, docReference))
                Document = (SrmDocument) Document.ChangeIgnoreChangingChildren(false).ChangeChildrenChecked(docNew.Children);
            return Document;
        }

        /// <summary>
        /// Reads every line of the peak boundaries file and resolves it against the document, without
        /// changing anything. Lines which cannot be resolved are recorded in <see cref="UnrecognizedPeptides"/>
        /// and <see cref="UnrecognizedFiles"/>, exactly as they were when the file was read and applied in a
        /// single pass.
        /// </summary>
        /// <returns>The changes to be made, grouped by the peptide they apply to, or null if canceled</returns>
        private List<PeptidePeakBoundaries> ReadPeakBoundaries(TextReader reader, long lineCount, bool isMinutes,
            bool changePeaks, out List<PeakBoundaryLine> boundaryLines)
        {
            double timeConversionFactor = isMinutes ? 1.0 : 60.0;
            int linesRead = 0;
            boundaryLines = new List<PeakBoundaryLine>();
            var peptideChanges = new List<PeptidePeakBoundaries>();
            // Keyed on the Peptide Identity, which is reference unique to a single peptide in the document
            var peptideChangesByPeptide = new Dictionary<ReferenceValue<Identity>, PeptidePeakBoundaries>();
            var sequenceToNode = MakeSequenceDictionary(Document);
            var fileNameToFileMatch = new Dictionary<Tuple<string, string, bool>, ResultFileMatch>();
            var modMatcher = new ModificationMatcher();
            var canonicalSequenceDict = new Dictionary<string, string>();

            // Add annotations as possible columns
            var allFieldNames = new List<string[]>(FIELD_NAMES);
            allFieldNames.AddRange(from def in Document.Settings.DataSettings.AnnotationDefs
                                   where def.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.precursor_result)
                                   select new[] { def.Name });

            string line = reader.ReadLine();
            linesRead++;
            int[] fieldIndices;
            int fieldsTotal;
            // If we aren't changing peaks, allow start and end time to be missing. File identity (filename or
            // replicate name) is required via FILE_ID_FIELDS rather than as an always-required column.
            var requiredFields = RequiredFieldsForImport(changePeaks);
            char correctSeparator = ReadFirstLine(line, allFieldNames, requiredFields, FILE_ID_FIELDS, out fieldIndices, out fieldsTotal);

            // Determine whether the input list is proteomic or small molecule
            var header = new DataFields(fieldIndices, line.ParseDsvFields(correctSeparator), allFieldNames);
            var peptideColumnName = header.GetField(Field.modified_peptide);
            var listIsProteomic = !MOLECULE_SYNONYMS.Any(s => string.Equals(s, peptideColumnName, StringComparison.CurrentCultureIgnoreCase));

            while ((line = reader.ReadLine()) != null)
            {
                linesRead++;
                if (_progressMonitor != null)
                {
                    if (_progressMonitor.IsCanceled)
                        return null;
                    UpdateProgress((int) (linesRead * PERCENT_READING / lineCount));
                }
                var dataFields = new DataFields(fieldIndices, line.ParseDsvFields(correctSeparator), allFieldNames);
                if (dataFields.Length != fieldsTotal)
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Line__0__field_count__1__differs_from_the_first_line__which_has__2_,
                        linesRead, dataFields.Length, fieldsTotal));
                }
                string modifiedPeptideString = dataFields.GetField(Field.modified_peptide);
                string fileName = dataFields.GetField(Field.filename);
                string replicateName = dataFields.GetField(Field.replicate_name);
                // FileName takes precedence; the replicate name is used to identify the file only when no
                // file name is given for this row, so files keyed on FileName behave exactly as before.
                bool useReplicate = string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(replicateName);
                string fileIdentity = useReplicate ? replicateName : fileName;
                bool isDecoy = dataFields.IsDecoy(linesRead);
                IList<IdentityPath> pepPaths;

                // When used in a mixed peptide/molecule document, the "molecule peak boundaries" report will set the molecule name
                // field for peptide "PEPTIDER" as "pep_PEPTIDER". But our "as small molecules" tests use the same convention converting
                // peptides to molecules, so it might be an actual named node in the document, so check that first before stripping "pep_".
                var nodeFound = sequenceToNode.TryGetValue(Tuple.Create(modifiedPeptideString, isDecoy), out pepPaths);
                var lineIsProteomic = listIsProteomic;
                if (!listIsProteomic && !nodeFound && 
                    modifiedPeptideString.StartsWith(RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator))
                {
                    modifiedPeptideString = modifiedPeptideString.Substring(RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator.Length);
                    nodeFound = sequenceToNode.TryGetValue(Tuple.Create(modifiedPeptideString, isDecoy), out pepPaths);
                    lineIsProteomic = true;
                }

                if (lineIsProteomic && !nodeFound)
                {
                    string canonicalSequence;
                    if (!canonicalSequenceDict.TryGetValue(modifiedPeptideString, out canonicalSequence))
                    {
                        if (modifiedPeptideString.Any(c => c < 'A' || c > 'Z'))
                        {
                            Exception modificationException = null;
                            PeptideDocNode nodeForModPep = null; 
                            try
                            {
                                modMatcher.CreateMatches(Document.Settings,
                                    new List<string> { modifiedPeptideString },
                                    Settings.Default.StaticModList,
                                    Settings.Default.HeavyModList);
                                nodeForModPep = modMatcher.GetModifiedNode(modifiedPeptideString);
                            }
                            catch (Exception e)
                            {
                                modificationException = e;
                            }
                                
                            if (nodeForModPep == null)
                            {
                                string message =
                                    string.Format(
                                        ModelResources
                                            .PeakBoundaryImporter_Import_Peptide_has_unrecognized_modifications__0__at_line__1_,
                                        modifiedPeptideString, linesRead);
                                throw new IOException(message, modificationException);
                            }
                            nodeForModPep = nodeForModPep.ChangeSettings(Document.Settings, SrmSettingsDiff.ALL);
                            // Convert the modified peptide string into a standardized form that 
                            // converts unimod, names, etc, into masses, eg [+57.0]
                            canonicalSequence = nodeForModPep.ModifiedTarget.Sequence;
                            canonicalSequenceDict.Add(modifiedPeptideString, canonicalSequence);
                        }
                    }
                    if (null != canonicalSequence)
                    {
                        sequenceToNode.TryGetValue(Tuple.Create(canonicalSequence, isDecoy), out pepPaths);
                    }
                }
                if (null == pepPaths)
                {
                    if (!UnrecognizedPeptides.ContainsKey(modifiedPeptideString))
                        UnrecognizedPeptides[modifiedPeptideString] = linesRead;
                    continue;
                }
                Adduct charge;
                bool chargeSpecified = dataFields.TryGetCharge(linesRead, out charge, lineIsProteomic);
                // When the charge column contains a bare integer (e.g. "2"), match
                // by charge value rather than exact adduct formula, since the user
                // just wants to specify the charge state, not a specific adduct
                bool chargeIsNumeric = chargeSpecified &&
                    int.TryParse(dataFields.GetField(Field.charge), out _);
                string sampleName = dataFields.GetField(Field.sample_name);

                double? apexTime = dataFields.GetTime(Field.apex_time, timeConversionFactor,
                    Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_time_, linesRead);
                double? startTime = dataFields.GetTime(Field.start_time, timeConversionFactor,
                    Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_, linesRead);
                double? endTime = dataFields.GetTime(Field.end_time, timeConversionFactor,
                    Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_end_time_, linesRead);

                // Error if only one of startTime and endTime is null
                if (startTime == null && endTime != null)
                {
                    if (changePeaks)
                        throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Missing_start_time_on_line__0_, linesRead));
                    endTime = null;
                }
                if (startTime != null && endTime == null)
                {
                    if (changePeaks)
                        throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Missing_end_time_on_line__0_, linesRead));
                    startTime = null;
                }

                var fileKey = Tuple.Create(fileIdentity, sampleName, useReplicate);
                // Add file identity to second dictionary if not yet encountered
                ResultFileMatch fileMatch;
                if (!fileNameToFileMatch.TryGetValue(fileKey, out fileMatch) && Document.Settings.HasResults)
                {
                    ChromSetFileMatch chromSetFileMatch;
                    if (useReplicate)
                    {
                        chromSetFileMatch = FindReplicateFileMatch(replicateName, sampleName, linesRead);
                    }
                    else
                    {
                        var dataFileUri = MsDataFileUri.Parse(fileName);
                        if (sampleName != null && dataFileUri is MsDataFilePath msDataFilePath)
                        {
                            var uriWithSample = new MsDataFilePath(msDataFilePath.FilePath, sampleName, msDataFilePath.SampleIndex, msDataFilePath.LockMassParameters);
                            chromSetFileMatch = Document.Settings.MeasuredResults.FindMatchingMSDataFile(uriWithSample);
                            if (chromSetFileMatch == null)
                            {
                                var bareFileMatch = Document.Settings.MeasuredResults.FindMatchingMSDataFile(dataFileUri);
                                if (bareFileMatch != null)
                                {
                                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Sample__0__on_line__1__does_not_match_the_file__2__, sampleName, linesRead, fileName));
                                }
                            }
                        }
                        else
                        {
                            chromSetFileMatch = Document.Settings.MeasuredResults.FindMatchingMSDataFile(dataFileUri);
                        }
                    }

                    fileMatch = ResultFileMatch.MakeMatch(Document.Settings.MeasuredResults, chromSetFileMatch);
                    fileNameToFileMatch.Add(fileKey, fileMatch);
                }
                if (fileMatch == null)
                {
                    if (!UnrecognizedFiles.ContainsKey(fileIdentity))
                        UnrecognizedFiles[fileIdentity] = linesRead;
                    continue;
                }
                // Define the annotations to be added
                var annotations = dataFields.GetAnnotations();
                if (!changePeaks)
                {
                    if (apexTime.HasValue)
                        annotations.Add(ComparePeakBoundaries.APEX_ANNOTATION, dataFields.GetField(Field.apex_time));
                    if (startTime.HasValue && endTime.HasValue)
                    {
                        annotations.Add(ComparePeakBoundaries.START_TIME_ANNOTATION, dataFields.GetField(Field.start_time));
                        annotations.Add(ComparePeakBoundaries.END_TIME_ANNOTATION, dataFields.GetField(Field.end_time));
                    }
                }
                AnnotationsAdded = annotations.Keys.ToList();

                var boundaryLine = new PeakBoundaryLine
                {
                    LineNumber = linesRead,
                    ModifiedPeptideString = modifiedPeptideString,
                    FileIdentity = fileIdentity,
                    Charge = charge
                };
                boundaryLines.Add(boundaryLine);
                var change = new PeakBoundaryChange
                {
                    ReplicateIndex = fileMatch.ReplicateIndex,
                    FileId = fileMatch.FileId,
                    FilePath = fileMatch.FilePath,
                    StartTime = startTime,
                    EndTime = endTime,
                    Annotations = annotations,
                    Charge = charge,
                    ChargeSpecified = chargeSpecified,
                    ChargeIsNumeric = chargeIsNumeric
                };

                // A line whose modified sequence matches more than one peptide in the document is applied to
                // each of them, so its change is duplicated once per matching peptide.
                foreach (var pepPath in pepPaths)
                {
                    if (!peptideChangesByPeptide.TryGetValue(pepPath.Child, out var pepChanges))
                    {
                        pepChanges = new PeptidePeakBoundaries(pepPath);
                        peptideChangesByPeptide.Add(pepPath.Child, pepChanges);
                        peptideChanges.Add(pepChanges);
                    }
                    pepChanges.Changes.Add(new LineChange(boundaryLine, change));
                }
            }
            return peptideChanges;
        }

        /// <summary>
        /// Applies the changes to each peptide on a background thread, leaving the new
        /// <see cref="PeptideDocNode"/> in <see cref="PeptidePeakBoundaries.ResultNode"/>. Nothing shared is
        /// modified, so the peptides can be processed in any order and in parallel.
        /// </summary>
        /// <returns>False if the import was canceled</returns>
        private bool ApplyPeakBoundaries(IList<PeptidePeakBoundaries> peptideChanges, bool changePeaks)
        {
            bool canceled = false;
            int peptidesDone = 0;
            int peptideCount = peptideChanges.Count;
            ParallelEx.ForEach(peptideChanges, pepChanges =>
            {
                if (canceled)
                    return;
                if (_progressMonitor != null && _progressMonitor.IsCanceled)
                {
                    canceled = true;
                    return;
                }
                ApplyPeakBoundaries(pepChanges, changePeaks);
                if (_progressMonitor != null)
                {
                    int done = Interlocked.Increment(ref peptidesDone);
                    UpdateProgress(PERCENT_READING + (int) (done * (100L - PERCENT_READING) / peptideCount));
                }
            });
            return !canceled;
        }

        /// <summary>
        /// Applies all of the changes for a single peptide with a <see cref="MoleculeIntegrator"/>, which
        /// reads the peptide's chromatograms from the .skyd file once and reuses them for every change.
        /// </summary>
        private void ApplyPeakBoundaries(PeptidePeakBoundaries pepChanges, bool changePeaks)
        {
            var nodePep = (PeptideDocNode) Document.FindNode(pepChanges.PeptidePath);
            var integrator = new MoleculeIntegrator(Document, nodePep) { ChangePeaks = changePeaks };
            foreach (var lineChange in pepChanges.Changes)
            {
                var matched = integrator.ApplyChange(lineChange.Change);
                if (matched.Count == 0)
                    continue;
                lineChange.Line.FoundSample = true;
                // For removing peaks that are not in the file, if removeMissing = true
                foreach (var groupId in matched)
                    pepChanges.AdjustedResults.Add(new ResultsKey(lineChange.Change.FileId.GlobalIndex, groupId));
            }
            pepChanges.ResultNode = integrator.MoleculeDocNode;
        }

        /// <summary>
        /// Puts the changed peptides back into the document. Each peptide group gets its children replaced
        /// with all of its changed peptides at once, and then the document children are replaced once, so
        /// that the cost of rebuilding the document is paid a single time for the whole file.
        /// </summary>
        private static SrmDocument ReplacePeptides(SrmDocument document, IEnumerable<PeptidePeakBoundaries> peptideChanges)
        {
            // Group the changed peptides by the peptide group they belong to
            var changesByGroup = new Dictionary<ReferenceValue<Identity>, List<PeptidePeakBoundaries>>();
            foreach (var pepChanges in peptideChanges)
            {
                var groupId = pepChanges.PeptidePath.GetIdentity(0);
                if (!changesByGroup.TryGetValue(groupId, out var groupChanges))
                {
                    groupChanges = new List<PeptidePeakBoundaries>();
                    changesByGroup.Add(groupId, groupChanges);
                }
                groupChanges.Add(pepChanges);
            }

            bool anyChanged = false;
            var newChildren = new List<DocNode>(document.Children.Count);
            foreach (PeptideGroupDocNode nodePepGroup in document.Children)
            {
                if (!changesByGroup.TryGetValue(nodePepGroup.Id, out var groupChanges))
                {
                    newChildren.Add(nodePepGroup);
                    continue;
                }
                var nodePepGroupNew = nodePepGroup;
                foreach (var pepChanges in groupChanges)
                {
                    if (!ReferenceEquals(pepChanges.ResultNode, nodePepGroupNew.FindNode(pepChanges.PeptidePath.Child)))
                        nodePepGroupNew = (PeptideGroupDocNode) nodePepGroupNew.ReplaceChild(pepChanges.ResultNode);
                }
                anyChanged = anyChanged || !ReferenceEquals(nodePepGroup, nodePepGroupNew);
                newChildren.Add(nodePepGroupNew);
            }
            return anyChanged ? (SrmDocument) document.ChangeChildren(newChildren) : document;
        }

        /// <summary>
        /// Percent complete attributed to reading the file, with the rest attributed to applying the changes
        /// </summary>
        private const int PERCENT_READING = 50;

        private IProgressMonitor _progressMonitor;
        private IProgressStatus _status;
        private int _progressPercent;
        private readonly object _progressLock = new object();

        private void UpdateProgress(int progressNew)
        {
            lock (_progressLock)
            {
                if (_progressPercent == progressNew)
                    return;
                _progressPercent = progressNew;
                _progressMonitor.UpdateProgress(_status = _status.ChangePercentComplete(progressNew));
            }
        }

        /// <summary>
        /// Resolves a replicate name to a single result file, for peak-boundary rows that key on the
        /// (vendor-independent) replicate name instead of the on-disk file name. Returns null when no
        /// replicate matches the name (the row is then reported as unrecognized). For a multi-file replicate
        /// (e.g. a multi-sample .wiff) an optional <paramref name="sampleName"/> selects a single file within
        /// the replicate; without it, or if it does not match exactly one file, the row is ambiguous and throws.
        /// </summary>
        private ChromSetFileMatch FindReplicateFileMatch(string replicateName, string sampleName, long linesRead)
        {
            if (!Document.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out var chromSet, out _))
                return null;    // No replicate by that name - the row is reported as unrecognized
            if (chromSet.FileCount == 0)
                return null;    // Degenerate replicate with no files - reported as unrecognized

            ChromFileInfo fileInfo;
            if (!string.IsNullOrEmpty(sampleName))
            {
                // A SampleName narrows a multi-file replicate (e.g. a multi-sample .wiff) to a single file.
                var sampleMatches = chromSet.MSDataFileInfos
                    .Where(info => Equals(info.FilePath.GetSampleName(), sampleName)).ToArray();
                if (sampleMatches.Length != 1)
                {
                    throw new IOException(string.Format(
                        Resources.PeakBoundaryImporter_FindReplicateFileMatch_The_sample___0___on_line__1__does_not_match_a_single_file_in_the_replicate___2__,
                        sampleName, linesRead, replicateName));
                }
                fileInfo = sampleMatches[0];
            }
            else if (chromSet.FileCount == 1)
            {
                fileInfo = chromSet.MSDataFileInfos[0];
            }
            else
            {
                // More than one file and no SampleName to pick one - fail rather than guess which file to adjust.
                throw new IOException(string.Format(
                    Resources.PeakBoundaryImporter_FindReplicateFileMatch_The_replicate___0___on_line__1__contains_multiple_files__so_the_replicate_name_alone_is_ambiguous__Specify_a_FileName__and_optionally_a_SampleName__to_identify_a_single_file_,
                    replicateName, linesRead));
            }
            return new ChromSetFileMatch(chromSet, fileInfo.FilePath, GetGlobalFileOrder(chromSet, fileInfo));
        }

        /// <summary>
        /// The running index of a file across all chromatogram sets, matching the fileOrder that
        /// <see cref="MeasuredResults.FindMatchingMSDataFile"/> assigns to a <see cref="ChromSetFileMatch"/>.
        /// </summary>
        private int GetGlobalFileOrder(ChromatogramSet matchSet, ChromFileInfo matchFile)
        {
            int fileOrder = 0;
            foreach (var set in Document.Settings.MeasuredResults.Chromatograms)
            {
                if (ReferenceEquals(set, matchSet))
                    return fileOrder + set.MSDataFileInfos.IndexOf(matchFile);
                fileOrder += set.FileCount;
            }
            return fileOrder;
        }

        /// <summary>
        /// Creates a dictionary mapping from strings to list of peptide IdentityPaths.
        /// Peptides can be looked up by either their low precision or high precision modified sequence.
        /// </summary>
        private IDictionary<Tuple<string, bool>, IList<IdentityPath>> MakeSequenceDictionary(SrmDocument document)
        {
            var sequenceToNode = new Dictionary<Tuple<string, bool>, IList<IdentityPath>>();
            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    IdentityPath peptidePath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                    foreach (var sequenceKey in ListSequenceKeys(molecule))
                    {
                        var peptidePair = Tuple.Create(sequenceKey, molecule.IsDecoy);
                        IList<IdentityPath> idPathList;
                        // Each (sequence, isDecoy) pair can be associated with more than one peptide, 
                        // to handle the case of duplicate peptides in the doucment.
                        if (sequenceToNode.TryGetValue(peptidePair, out idPathList))
                        {
                            idPathList.Add(peptidePath);
                        }
                        else
                        {
                            idPathList = new List<IdentityPath> { peptidePath };
                            sequenceToNode.Add(peptidePair, idPathList);
                        }
                    }
                }
            }
            return sequenceToNode;
        }

        /// <summary>
        /// Lists the strings that can be used to refer to the PeptideDocNode.
        /// A peptide can be looked up by either its narrow or wide modified sequence.
        /// A small molecule can be looked up by its RawTextId.
        /// </summary>
        private IEnumerable<string> ListSequenceKeys(PeptideDocNode peptideDocNode)
        {
            if (peptideDocNode.IsProteomic)
            {
                yield return peptideDocNode.ModifiedTarget.Sequence;
                if (!string.IsNullOrEmpty(peptideDocNode.ModifiedSequenceDisplay) &&
                    peptideDocNode.ModifiedSequenceDisplay != peptideDocNode.ModifiedTarget.Sequence)
                {
                    yield return peptideDocNode.ModifiedSequenceDisplay;
                }
            }
            else
            {
                yield return peptideDocNode.CustomMolecule.DisplayName;
                if (!Equals(peptideDocNode.CustomMolecule.DisplayName, peptideDocNode.CustomMolecule.InvariantName))
                {
                    yield return peptideDocNode.CustomMolecule.InvariantName;
                }
            }
        }

        /// <summary>
        /// Removes peaks and annotations that were in the document but not in the file, so that all peptide results that were not explicitly imported as part of this file are now blank
        /// </summary>
        /// <param name="docNew">SrmDocument for which missing peaks should be removed</param>
        /// <param name="trackAdjustedResults">List of peaks that were in the imported file</param>
        /// <param name="changePeaks">If true, remove both peaks and annotations.  If false, only remove annotations</param>
        /// <returns></returns>
        private SrmDocument RemoveMissing(SrmDocument docNew, ICollection<ResultsKey> trackAdjustedResults, bool changePeaks)
        {
            var measuredResults = docNew.Settings.MeasuredResults;
            var chromatogramSets = measuredResults.Chromatograms;
            for (int i = 0; i < chromatogramSets.Count; ++i)
            {
                var set = chromatogramSets[i];
                var nameSet = set.Name;
                for (int k = 0; k < docNew.MoleculeCount; ++k)
                {
                    IdentityPath pepPath = docNew.GetPathTo((int)SrmDocument.Level.Molecules, k);
                    var pepNode = (PeptideDocNode)Document.FindNode(pepPath);
                    if (pepNode.IsDecoy)
                        continue;
                    if (pepNode.GlobalStandardType != null)
                        continue;
                    foreach (var groupNode in pepNode.TransitionGroups)
                    {
                        var groupPath = new IdentityPath(pepPath, groupNode.Id);
                        var chromInfos = groupNode.Results[i];
                        if (chromInfos.IsEmpty)
                            continue;

                        foreach (var groupChromInfo in chromInfos)
                        {
                            if (groupChromInfo == null)
                                continue;
                            var key = new ResultsKey(groupChromInfo.FileId.GlobalIndex, groupNode.Id);
                            if (!trackAdjustedResults.Contains(key))
                            {
                                CountMissing++;
                                var fileId = groupChromInfo.FileId;
                                var fileInfo = set.GetFileInfo(fileId);
                                var filePath = fileInfo.FilePath;
                                // Remove annotations for defs that were imported into the document and were on this peptide prior to import
                                var newAnnotationValues = groupChromInfo.Annotations.ListAnnotations().ToList();
                                newAnnotationValues = newAnnotationValues.Where(a => !AnnotationsAdded.Contains(a.Key)).ToList();
                                var newAnnotations = new Annotations(groupChromInfo.Annotations.Note, newAnnotationValues, groupChromInfo.Annotations.ColorIndex);
                                var newGroupNode = groupNode.ChangePrecursorAnnotations(fileId, newAnnotations);
                                if (!ReferenceEquals(groupNode, newGroupNode))
                                    docNew = (SrmDocument) docNew.ReplaceChild(groupPath.Parent, newGroupNode);
                                // Adjust peaks to null if they weren't in the file
                                if (changePeaks)
                                {
                                    docNew = docNew.ChangePeak(groupPath, nameSet, filePath,
                                        null, null, null, UserSet.IMPORTED, null, false);
                                }
                            }
                        }
                    }
                }
            }
            return docNew;
        }

        private static char ReadFirstLine(string line, IList<string[]> allFieldNames, ICollection<int> requiredFields,
            ICollection<int> anyOfFields, out int[] fieldIndices, out int fieldsTotal)
        {

            if (line == null)
            {
                throw new IOException(Resources.PeakBoundaryImporter_Import_Failed_to_read_the_first_line_of_the_file);
            }
            char? correctSeparator = DetermineCorrectSeparator(line, allFieldNames, requiredFields, out fieldIndices, out fieldsTotal, anyOfFields);
            if (correctSeparator == null)
            {
                var missingFields = new List<string>();
                // Keep ReSharper from complaining
                var indices = fieldIndices; // out parameter cannot be captured in a lambda
                if (indices != null)
                {
                    missingFields.AddRange(indices.Select((index, i) => new Tuple<int, int>(index, i))
                        .Where(t => t.Item1 == -1 && requiredFields.Contains(t.Item2))
                        .Select(t => allFieldNames[t.Item2][0]));
                    // When none of the file-identity columns is present, report that either a FileName or a
                    // ReplicateName column is needed (rather than naming only FileName).
                    if (anyOfFields != null && !anyOfFields.Any(i => i < indices.Length && indices[i] != -1))
                        missingFields.Add(string.Format(ModelResources.PeakBoundaryImporter_ReadFirstLine__0__or__1_,
                            allFieldNames[(int) Field.filename][0], allFieldNames[(int) Field.replicate_name][0]));
                }
                throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Failed_to_find_the_necessary_headers__0__in_the_first_line,
                    string.Join(@", ", missingFields)));
            }
            return correctSeparator.Value;
        }

        public static char? DetermineCorrectSeparator(string line,
                                                       IList<string[]> fieldNames,
                                                       ICollection<int> requiredFields,
                                                       out int[] fieldIndices,
                                                       out int fieldsTotal,
                                                       ICollection<int> anyOfFields = null)
        {
            // Try TSV,CSV,and SPACE as possible delimiters for the file
            var separators = new[]
                {
                    TextUtil.CsvSeparator,
                    TextUtil.SEPARATOR_TSV,
                    TextUtil.SEPARATOR_SPACE
                };

            if (line.IndexOfAny(separators) == -1)
            {
                throw new IOException(string.Format(TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV
                    ? Resources.PeakBoundaryImporter_DetermineCorrectSeparator_The_first_line_does_not_contain_any_of_the_possible_separators_comma__tab_or_space_
                    : Resources.PeakBoundaryImporter_DetermineCorrectSeparator_The_first_line_does_not_contain_any_of_the_possible_separators_semicolon__tab_or_space_));
            }

            fieldIndices = null;
            fieldsTotal = 0;
            int requiredFieldsMax = -1;
            foreach (char separator in separators)
            {
                string[] headerFields = line.ParseDsvFields(separator);
                var fieldsForSeparator = new int[fieldNames.Count];
                for (int i = 0; i < fieldNames.Count; i++)
                {
                    fieldsForSeparator[i] = -1;
                    for (int j = 0; j < fieldNames[i].Length; ++j)
                    {
                        fieldsForSeparator[i] = Array.IndexOf(headerFields, fieldNames[i][j]);
                        if (fieldsForSeparator[i] != -1)
                            break;
                    }
                }
                int requiredFieldsFound = fieldsForSeparator.Where((index, i) => index != -1 && requiredFields.Contains(i)).Count();
                // At least one of the "any of" columns (e.g. filename OR replicate name) must also be present
                bool anyOfFound = anyOfFields == null ||
                                  anyOfFields.Any(i => i < fieldsForSeparator.Length && fieldsForSeparator[i] != -1);

                if (requiredFieldsFound == requiredFields.Count && anyOfFound)
                {
                    fieldIndices = fieldsForSeparator;
                    fieldsTotal = headerFields.Length;
                    return separator;
                }

                // Keep track of the best match for use in failure message, if necessary
                if (requiredFieldsFound > requiredFieldsMax)
                {
                    requiredFieldsMax = requiredFieldsFound;
                    fieldIndices = fieldsForSeparator;
                    fieldsTotal = headerFields.Length;
                }
            }
            return null;
        }

        /// <summary>
        /// All of the changes from the peak boundaries file which apply to a single peptide in the document.
        /// They are applied together, on a single thread, so that the peptide is rebuilt for each change but
        /// the document is rebuilt only once for the whole file.
        /// </summary>
        private class PeptidePeakBoundaries
        {
            public PeptidePeakBoundaries(IdentityPath peptidePath)
            {
                PeptidePath = peptidePath;
                Changes = new List<LineChange>();
                AdjustedResults = new List<ResultsKey>();
            }

            public IdentityPath PeptidePath { get; }
            public List<LineChange> Changes { get; }
            /// <summary>
            /// The peaks that were adjusted, which <see cref="RemoveMissing"/> uses to tell which peaks
            /// were not in the file
            /// </summary>
            public List<ResultsKey> AdjustedResults { get; }
            /// <summary>
            /// The peptide with all of its changes applied, set by the thread which applied them
            /// </summary>
            public PeptideDocNode ResultNode { get; set; }
        }

        /// <summary>
        /// A change to be made, together with the line of the file it came from. A line which matches more
        /// than one peptide in the document produces one of these for each of them, all sharing a single
        /// <see cref="PeakBoundaryLine"/> and a single <see cref="PeakBoundaryChange"/>.
        /// </summary>
        private class LineChange
        {
            public LineChange(PeakBoundaryLine line, PeakBoundaryChange change)
            {
                Line = line;
                Change = change;
            }

            public PeakBoundaryLine Line { get; }
            public PeakBoundaryChange Change { get; }
        }

        /// <summary>
        /// The parts of a line of the peak boundaries file which are needed to report the line as having an
        /// unrecognized charge state.
        /// </summary>
        private class PeakBoundaryLine
        {
            public long LineNumber { get; set; }
            public string ModifiedPeptideString { get; set; }
            public string FileIdentity { get; set; }
            public Adduct Charge { get; set; }
            /// <summary>
            /// True if any precursor of any matching peptide was adjusted. Set to true by the threads
            /// applying the changes, which never set it back to false.
            /// </summary>
            public bool FoundSample { get; set; }
        }

        /// <summary>
        /// A result file matched by a line of the peak boundaries file, resolved down to everything a
        /// <see cref="PeakBoundaryChange"/> needs. Resolving this once per distinct file name in the file,
        /// instead of once per line, keeps <see cref="ChromatogramSet.FindFile"/> and
        /// <see cref="ChromatogramSet.GetFileInfo"/> off the per-line path.
        /// </summary>
        private class ResultFileMatch
        {
            /// <summary>
            /// Returns null when <paramref name="fileMatch"/> is null, i.e. the file name was not recognized
            /// </summary>
            public static ResultFileMatch MakeMatch(MeasuredResults measuredResults, ChromSetFileMatch fileMatch)
            {
                if (fileMatch == null)
                    return null;
                var chromSet = fileMatch.Chromatograms;
                measuredResults.TryGetChromatogramSet(chromSet.Name, out _, out int replicateIndex);
                var fileId = chromSet.FindFile(fileMatch.FilePath);
                return new ResultFileMatch
                {
                    ReplicateIndex = replicateIndex,
                    FileId = fileId,
                    // A null fileId matches no precursor result, so the line ends up reported as an
                    // unrecognized charge state, and there is no file to ask for a path
                    FilePath = fileId == null ? null : chromSet.GetFileInfo(fileId).FilePath
                };
            }

            public int ReplicateIndex { get; private set; }
            public ChromFileInfoId FileId { get; private set; }
            public MsDataFileUri FilePath { get; private set; }
        }

        private class ResultsKey
        {
            private int FileId { get; set; }
            private Identity NodeId { get; set; }

            public ResultsKey(int fileId, Identity nodeId)
            {
                FileId = fileId;
                NodeId = nodeId;
            }

            private bool Equals(ResultsKey other)
            {
                return Equals(NodeId, other.NodeId) && FileId == other.FileId;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ResultsKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((NodeId != null ? NodeId.GetHashCode() : 0)*397) ^ FileId;
                }
            }
        }

        private class DataFields
        {
            private readonly int[] _fieldIndices;
            private readonly string[] _dataFields;
            private readonly IList<string[]> _fieldNames;

            public DataFields(int[] fieldIndices, string[] dataFields, IList<string[]> fieldNames)
            {
                _fieldIndices = fieldIndices;
                _dataFields = dataFields;
                _fieldNames = fieldNames;
            }

            public int Length { get { return _dataFields.Length; } }

            private string GetField(int i)
            {
                int fieldIndex = _fieldIndices[i];
                if (fieldIndex == -1 || fieldIndex >= _dataFields.Length)
                {
                    return null;
                }
                return _dataFields[fieldIndex];
            }

            public string GetField(Field field)
            {
                return GetField((int) field);
            }

            public Dictionary<string, string> GetAnnotations()
            {
                var annotations = new Dictionary<string, string>();
                string qvalueText = GetField(Field.q_value);
                if (qvalueText != null)
                    annotations.Add(MProphetResultsHandler.AnnotationName, qvalueText);
                string scoreText = GetField(Field.score);
                if (scoreText != null)
                    annotations.Add(MProphetResultsHandler.MAnnotationName, scoreText);
                for (int i = FIELD_NAMES.Length; i < _fieldNames.Count; ++i)
                {
                    string value = GetField(i);
                    if (value != null)
                        annotations.Add(_fieldNames[i][0], value);
                }
                return annotations;
            }

            public double? GetTime(Field field, double timeConversionFactor, string message, long linesRead)
            {
                string timeText = GetField(field);
                if (timeText == null)
                    return null;

                double time;
                if (double.TryParse(timeText, out time))
                    return time / timeConversionFactor;
                else if (double.TryParse(timeText, NumberStyles.Float, CultureInfo.InvariantCulture, out time))
                    return time / timeConversionFactor;

                // #N/A means remove the peak
                if (timeText.Equals(TextUtil.EXCEL_NA))
                    return null;

                throw new IOException(string.Format(message, timeText, linesRead));
            }

            public bool IsDecoy(long linesRead)
            {
                bool isDecoy = false;
                string decoyString = GetField(Field.is_decoy);
                if (decoyString != null)
                {
                    int decoyNum;
                    if (!int.TryParse(decoyString, out decoyNum))
                    {
                        switch (decoyString.ToLowerInvariant())
                        {
                            case @"false":
                                decoyNum = 0;
                                break;
                            case @"true":
                                decoyNum = 1;
                                break;
                            default:
                                throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_decoy_value__0__on_line__1__is_invalid__must_be_0_or_1_,
                                                                    decoyString, linesRead));
                        }
                    }
                    if (decoyNum != 1 && decoyNum != 0)
                    {
                        throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_decoy_value__0__on_line__1__is_invalid__must_be_0_or_1_,
                                                            decoyString, linesRead));
                    }
                    isDecoy = decoyNum == 1;
                }
                return isDecoy;
            }

            public bool TryGetCharge(long linesRead, out Adduct charge, bool assumeProteomic)
            {
                string chargeString = GetField(Field.charge);
                if (chargeString == null)
                {
                    charge = Adduct.EMPTY;
                    return false;
                }

                if (!Adduct.TryParse(chargeString, out charge, assumeProteomic ? Adduct.ADDUCT_TYPE.proteomic : Adduct.ADDUCT_TYPE.non_proteomic)) // If proteomic, read (for example) "2" as Adduct.DOUBLY_PROTONATED
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_charge_state_, chargeString, linesRead));
                }
                return true;
            }
        }
    }
}
