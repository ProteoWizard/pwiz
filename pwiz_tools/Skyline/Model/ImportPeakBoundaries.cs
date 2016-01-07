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
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
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
        public HashSet<string> UnrecognizedPeptides { get; private set; }
        public HashSet<string> UnrecognizedFiles { get; private set; }
        public HashSet<UnrecognizedChargeState> UnrecognizedChargeStates { get; private set; } 
	    
        public PeakBoundaryImporter(SrmDocument document)
	    {
            Document = document;
            AnnotationsAdded = new List<string>();
            UnrecognizedFiles = new HashSet<string>();
            UnrecognizedPeptides = new HashSet<string>();
            UnrecognizedChargeStates = new HashSet<UnrecognizedChargeState>();
	    }

        public struct UnrecognizedChargeState
        {
            public string Peptide;
            public string File;
            public int Charge;

            public UnrecognizedChargeState(int charge, string file, string peptide)
            {
                Charge = charge;
                File = file;
                Peptide = peptide;
            }

            public string PrintLine(char separator)
            {
                var sb = new StringBuilder();
                sb.Append(Peptide);
                sb.Append(separator); // Not L10N
                sb.Append(File);
                sb.Append(separator); // Not L10N
                sb.Append(Charge);
                return sb.ToString();
            }
        }

        public enum Field { modified_peptide, filename, start_time, end_time, charge, is_decoy, sample_name }

        public static readonly int[] REQUIRED_FIELDS =
            {
                (int) Field.modified_peptide, 
                (int) Field.filename, 
                (int) Field.start_time,
                (int) Field.end_time
            };

        public static int[] REQUIRED_NO_CHROM { get { return REQUIRED_FIELDS.Take(2).ToArray(); }}

        // ReSharper disable NonLocalizedString
        public static readonly string[][] FIELD_NAMES =
        {
            new[] {"PeptideModifiedSequence", "FullPeptideName", "EG.ModifiedSequence", ColumnCaptions.PeptideModifiedSequence},
            new[] {"FileName", "filename", "R.FileName", ColumnCaptions.FileName},
            new[] {"MinStartTime", "leftWidth", ColumnCaptions.MinStartTime},
            new[] {"MaxEndTime", "rightWidth", ColumnCaptions.MaxEndTime},
            new[] {"PrecursorCharge", "Charge", "FG.Charge", ColumnCaptions.PrecursorCharge},
            new[] {"PrecursorIsDecoy", "Precursor Is Decoy", "IsDecoy", "decoy", ColumnCaptions.IsDecoy},
            new[] {"SampleName", ColumnCaptions.SampleName}
        };
        // ReSharper restore NonLocalizedString

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

        public bool IsMinutesPeakBoundaries(TextReader reader)
        {
            long linesRead = 0;
            var peakTimes = new List<double>();
            string line = reader.ReadLine();
            int[] fieldIndices;
            int fieldsTotal;
            char correctSeparator = ReadFirstLine(line, FIELD_NAMES, REQUIRED_FIELDS, out fieldIndices, out fieldsTotal);
            // Find the first 50 peak times that are not #N/A, if any is larger than the maxRT, then times are in seconds
            while (peakTimes.Count < 50)
            {
                if (string.IsNullOrEmpty(line = reader.ReadLine()))
                    break;
                linesRead++;
                var dataFields = new DataFields(fieldIndices, line.ParseDsvFields(correctSeparator), FIELD_NAMES);
                double? startTime = dataFields.GetTime(Field.start_time,
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
            var chromFileInfos = document.Settings.MeasuredResults.MSDataFileInfos;
            double maxRt = chromFileInfos.Select(info => info.MaxRetentionTime).Max();
            return maxRt != 0 ? maxRt : 720;    // 12 hours as default maximum RT in minutes
        }

        public SrmDocument Import(TextReader reader, IProgressMonitor progressMonitor, long lineCount, bool isMinutes, bool removeMissing = false, bool changePeaks = true)
        {
            var status = new ProgressStatus(Resources.PeakBoundaryImporter_Import_Importing_Peak_Boundaries);
            double timeConversionFactor = isMinutes ? 1.0 : 60.0;
            int linesRead = 0;
            int progressPercent = 0;
            var docNew = (SrmDocument) Document.ChangeIgnoreChangingChildren(true);
            var docReference = docNew;
            var sequenceToNode = new Dictionary<Tuple<string, bool>, IList<IdentityPath>>();
            var fileNameToFileMatch = new Dictionary<string, ChromSetFileMatch>();
            var trackAdjustedResults = new HashSet<ResultsKey>();
            var modMatcher = new ModificationMatcher();
            // Make the dictionary of modified peptide strings to doc nodes and paths
            for (int i = 0; i < Document.MoleculeCount; ++i)
            {
                IdentityPath peptidePath = Document.GetPathTo((int) SrmDocument.Level.Molecules, i);
                PeptideDocNode peptideNode = (PeptideDocNode) Document.FindNode(peptidePath);
                var peptidePair = new Tuple<string, bool>(peptideNode.RawTextId, peptideNode.IsDecoy);
                IList<IdentityPath> idPathList;
                // Each (sequence, isDecoy) pair can be associated with more than one peptide, 
                // to handle the case of duplicate peptides in the doucment.
                if (sequenceToNode.TryGetValue(peptidePair, out idPathList))
                {
                    idPathList.Add(peptidePath);
                    sequenceToNode[peptidePair] = idPathList;
                }
                else
                {
                    idPathList = new List<IdentityPath> { peptidePath };
                    sequenceToNode.Add(peptidePair, idPathList);
                }
            }

            // Add annotations as possible columns
            var allFieldNames = new List<string[]>(FIELD_NAMES);
            allFieldNames.AddRange(from def in Document.Settings.DataSettings.AnnotationDefs
                                   where def.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.precursor_result)
                                   select new[] { def.Name });

            string line = reader.ReadLine();
            linesRead++;
            int[] fieldIndices;
            int fieldsTotal;
            // If we aren't changing peaks, allow start and end time to be missing
            var requiredFields = changePeaks ? REQUIRED_FIELDS : REQUIRED_NO_CHROM;
            char correctSeparator = ReadFirstLine(line, allFieldNames, requiredFields, out fieldIndices, out fieldsTotal);

            while ((line = reader.ReadLine()) != null)
            {
                linesRead++;
                if (progressMonitor != null)
                {
                    if (progressMonitor.IsCanceled)
                        return Document;
                    int progressNew = (int) (linesRead*100/lineCount);
                    if (progressPercent != progressNew)
                    {
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(progressNew));
                        progressPercent = progressNew;
                    }
                }
                var dataFields = new DataFields(fieldIndices, line.ParseDsvFields(correctSeparator), allFieldNames);
                if (dataFields.Length != fieldsTotal)
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Line__0__field_count__1__differs_from_the_first_line__which_has__2_,
                        linesRead, dataFields.Length, fieldsTotal));
                }
                string modifiedPeptideString = dataFields.GetField(Field.modified_peptide);
                modMatcher.CreateMatches(Document.Settings, 
                                        new List<string> {modifiedPeptideString}, 
                                        Settings.Default.StaticModList,
                                        Settings.Default.HeavyModList);
                // Convert the modified peptide string into a standardized form that 
                // converts unimod, names, etc, into masses, eg [+57.0]
                var nodeForModPep = modMatcher.GetModifiedNode(modifiedPeptideString);
                if (nodeForModPep == null)
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Peptide_has_unrecognized_modifications__0__at_line__1_, modifiedPeptideString, linesRead));
                }
                nodeForModPep = nodeForModPep.ChangeSettings(Document.Settings, SrmSettingsDiff.ALL);
                modifiedPeptideString = nodeForModPep.RawTextId; // Modified sequence, or custom ion name
                string fileName = dataFields.GetField(Field.filename);
                bool isDecoy = dataFields.IsDecoy(linesRead);
                var peptideIdentifier = new Tuple<string, bool>(modifiedPeptideString, isDecoy);
                int charge;
                bool chargeSpecified = dataFields.TryGetCharge(linesRead, out charge);
                string sampleName = dataFields.GetField(Field.sample_name);

                double? startTime = null;
                double? endTime = null;
                if (changePeaks)
                {
                    startTime = dataFields.GetTime(Field.start_time, Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_, linesRead);
                    if (startTime.HasValue)
                        startTime = startTime / timeConversionFactor;
                    endTime = dataFields.GetTime(Field.end_time, Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_end_time_, linesRead);
                    if (endTime.HasValue)
                        endTime = endTime / timeConversionFactor;
                }

                // Error if only one of startTime and endTime is null
                if (startTime == null && endTime != null)
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Missing_start_time_on_line__0_, linesRead));
                if (startTime != null && endTime == null)
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Missing_end_time_on_line__0_, linesRead));
                // Add filename to second dictionary if not yet encountered
                ChromSetFileMatch fileMatch;
                if (!fileNameToFileMatch.TryGetValue(fileName, out fileMatch))
                {
                    fileMatch = Document.Settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(fileName));
                    fileNameToFileMatch.Add(fileName, fileMatch);
                }
                if (fileMatch == null)
                {
                    UnrecognizedFiles.Add(fileName);
                    continue;
                }
                var chromSet = fileMatch.Chromatograms;
                string nameSet = chromSet.Name;
                ChromFileInfoId[] fileIds;
                if (sampleName == null)
                {
                    fileIds = chromSet.MSDataFileInfos.Select(x => x.FileId).ToArray();
                }
                else
                {
                    var sampleFile = chromSet.MSDataFileInfos.FirstOrDefault(info => Equals(sampleName, info.FilePath.GetSampleName()));
                    if (sampleFile == null)
                    {
                        throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Sample__0__on_line__1__does_not_match_the_file__2__, sampleName, linesRead, fileName));
                    }
                    fileIds = new[] {sampleFile.FileId};
                }
                // Look up the IdentityPath of peptide in first dictionary
                IList<IdentityPath> pepPaths;
                if (!sequenceToNode.TryGetValue(peptideIdentifier, out pepPaths))
                {
                    UnrecognizedPeptides.Add(modifiedPeptideString);
                    continue;
                }

                // Define the annotations to be added
                var annotations = dataFields.GetAnnotations();
                AnnotationsAdded = annotations.Keys.ToList();

                // Loop over all the transition groups in that peptide to find matching charge,
                // or use all transition groups if charge not specified
                bool foundSample = false;
                foreach (var pepPath in pepPaths)
                {
                    var nodePep = (PeptideDocNode)docNew.FindNode(pepPath);
                    for(int i = 0; i < nodePep.Children.Count; ++i)
                    {
                        var groupRelPath = nodePep.GetPathTo(i);
                        var groupNode = (TransitionGroupDocNode) nodePep.FindNode(groupRelPath);
                        if (!chargeSpecified || charge == groupNode.TransitionGroup.PrecursorCharge)
                        {
                            var groupFileIndices =
                                new HashSet<int>(groupNode.ChromInfos.Select(x => x.FileId.GlobalIndex));
                            // Loop over the files in this groupNode to find the correct sample
                            // Change peak boundaries for the transition group
                            foreach (var fileId in fileIds)
                            {
                                if (groupFileIndices.Contains(fileId.GlobalIndex))
                                {
                                    var groupPath = new IdentityPath(pepPath, groupNode.Id);
                                    // Attach annotations
                                    docNew = docNew.AddPrecursorResultsAnnotations(groupPath, fileId, annotations);
                                    // Change peak
                                    var filePath = chromSet.GetFileInfo(fileId).FilePath;
                                    if (changePeaks)
                                    {
                                        docNew = docNew.ChangePeak(groupPath, nameSet, filePath,
                                            null, startTime, endTime, UserSet.IMPORTED, null, false);
                                    }
                                    // For removing peaks that are not in the file, if removeMissing = true
                                    trackAdjustedResults.Add(new ResultsKey(fileId.GlobalIndex, groupNode.Id));
                                    foundSample = true;
                                }
                            }
                        }
                    }
                }
                if (!foundSample)
                {
                    UnrecognizedChargeStates.Add(new UnrecognizedChargeState(charge, fileName, modifiedPeptideString));
                }
            }
            // Remove peaks from the document that weren't in the file.
            if (removeMissing)
                docNew = RemoveMissing(docNew, trackAdjustedResults, changePeaks);
            // If nothing has changed, return the old Document before ChangeIgnoreChangingChildren was turned off
            if (!ReferenceEquals(docNew, docReference))
                Document = (SrmDocument) Document.ChangeIgnoreChangingChildren(false).ChangeChildrenChecked(docNew.Children);
            return Document;
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
                        if (chromInfos == null)
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

        private static char ReadFirstLine(string line, IList<string[]> allFieldNames, ICollection<int> requiredFields, out int[] fieldIndices, out int fieldsTotal)
        {

            if (line == null)
            {
                throw new IOException(Resources.PeakBoundaryImporter_Import_Failed_to_read_the_first_line_of_the_file);
            }
            char? correctSeparator = DetermineCorrectSeparator(line, allFieldNames, requiredFields, out fieldIndices, out fieldsTotal);
            if (correctSeparator == null)
            {
                string fieldNames = string.Empty;
                // Keep ReSharper from complaining
                if (fieldIndices != null)
                {
                    string[] missingFields = fieldIndices.Where((index, i) => index == -1 && requiredFields.Contains(i))
                                                         .Select((index, i) => allFieldNames[i][0]).ToArray();
                    fieldNames = string.Join(", ", missingFields); // Not L10N
                }
                throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Failed_to_find_the_necessary_headers__0__in_the_first_line, fieldNames));
            }
            return correctSeparator.Value;
        }

        public static char? DetermineCorrectSeparator(string line,
                                                       IList<string[]> fieldNames,
                                                       ICollection<int> requiredFields,
                                                       out int[] fieldIndices,
                                                       out int fieldsTotal)
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

                if (requiredFieldsFound == requiredFields.Count)
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
        /// UI for warning about unrecognized peptides in imported file
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public bool UnrecognizedPeptidesCancel(IWin32Window parent)
        {
            const int itemsToShow = 10;
            var peptides = UnrecognizedPeptides.ToList();
            if (peptides.Any())
            {
                var sb = new StringBuilder();
                sb.AppendLine(peptides.Count == 1
                    ? Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following_peptide_in_the_peak_boundaries_file_was_not_recognized_
                    : string.Format(Resources.SkylineWindow_ImportPeakBoundaries_The_following__0__peptides_in_the_peak_boundaries_file_were_not_recognized__,
                                                  peptides.Count));
                sb.AppendLine();
                int peptidesToShow = Math.Min(peptides.Count, itemsToShow);
                for (int i = 0; i < peptidesToShow; ++i)
                {
                    sb.AppendLine(peptides[i]);
                }
                if (peptidesToShow < peptides.Count)
                {
                    sb.AppendLine("..."); // Not L10N
                }
                sb.AppendLine();
                sb.Append(peptides.Count == 1
                    ? Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_this_peptide_
                    : Resources.SkylineWindow_ImportPeakBoundaries_Continue_peak_boundary_import_ignoring_these_peptides_);
                var dlgPeptides = MultiButtonMsgDlg.Show(parent, sb.ToString(), MultiButtonMsgDlg.BUTTON_OK);
                if (dlgPeptides == DialogResult.Cancel)
                {
                    return false;
                }
            }
            var files = UnrecognizedFiles.ToList();
            if (files.Any())
            {
                var sb = new StringBuilder();
                sb.AppendLine(files.Count == 1
                    ? Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following_file_name_in_the_peak_boundaries_file_was_not_recognized_
                    : string.Format(Resources.SkylineWindow_ImportPeakBoundaries_The_following__0__file_names_in_the_peak_boundaries_file_were_not_recognized_,
                             files.Count));
                sb.AppendLine();
                int filesToShow = Math.Min(files.Count, itemsToShow);
                for (int i = 0; i < filesToShow; ++i)
                {
                    sb.AppendLine(files[i]);
                }
                if (filesToShow < files.Count)
                {
                    sb.AppendLine("..."); // Not L10N
                }
                sb.AppendLine();
                sb.Append(files.Count == 1
                    ? Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_this_file_
                    : Resources.SkylineWindow_ImportPeakBoundaries_Continue_peak_boundary_import_ignoring_these_files_);
                var dlgFiles = MultiButtonMsgDlg.Show(parent, sb.ToString(), MultiButtonMsgDlg.BUTTON_OK);
                if (dlgFiles == DialogResult.Cancel)
                {
                    return false;
                }
            }
            var charges = UnrecognizedChargeStates.ToList();
            if (charges.Any())
            {
                var sb = new StringBuilder();
                sb.AppendLine(files.Count == 1
                              ? Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following_peptide__file__and_charge_state_combination_was_not_recognized_
                              : string.Format(Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following__0__peptide__file__and_charge_state_combinations_were_not_recognized_, 
                                            charges.Count()));
                sb.AppendLine();
                int chargesToShow = Math.Min(charges.Count, itemsToShow);
                for (int i = 0; i < chargesToShow; ++i)
                {
                    sb.AppendLine(charges[i].PrintLine(' ')); // Not L10N
                }
                if (chargesToShow < charges.Count)
                {
                    sb.AppendLine("..."); // Not L10N
                }
                sb.AppendLine();
                sb.Append(files.Count == 1
                    ? Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_these_charge_states_
                    : Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_this_charge_state_);
                var dlgFiles = MultiButtonMsgDlg.Show(parent, sb.ToString(), MultiButtonMsgDlg.BUTTON_OK);
                if (dlgFiles == DialogResult.Cancel)
                {
                    return false;
                }
            }
            return true;
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
                return fieldIndex != -1 ? _dataFields[fieldIndex] : null;
            }

            public string GetField(Field field)
            {
                return GetField((int) field);
            }

            public Dictionary<string, string> GetAnnotations()
            {
                var annotations = new Dictionary<string, string>();
                for (int i = FIELD_NAMES.Length; i < _fieldNames.Count; ++i)
                {
                    string value = GetField(i);
                    if (value != null)
                        annotations.Add(_fieldNames[i][0], value);
                }
                return annotations;
            }

            public double? GetTime(Field field, string message, long linesRead)
            {
                double? startTime;
                string startTimeString = GetField(field);
                double startTimeTemp;
                if (double.TryParse(startTimeString, out startTimeTemp))
                    startTime = startTimeTemp;
                // #N/A means remove the peak
                else if (startTimeString.Equals(TextUtil.EXCEL_NA))
                    startTime = null;
                else
                    throw new IOException(string.Format(message, startTimeString, linesRead));
                return startTime;
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
                            case "false": // Not L10N
                                decoyNum = 0;
                                break;
                            case "true": // Not L10N
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

            public bool TryGetCharge(long linesRead, out int charge)
            {
                string chargeString = GetField(Field.charge);
                if (chargeString == null)
                {
                    charge = -1;
                    return false;
                }

                if (!int.TryParse(chargeString, out charge))
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_charge_state_, chargeString, linesRead));
                }
                return true;
            }
        }
    }
}