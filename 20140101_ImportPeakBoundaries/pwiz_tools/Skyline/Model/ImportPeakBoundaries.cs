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
	    
        public PeakBoundaryImporter(SrmDocument document)
	    {
            Document = document;
	    }

        public enum Field { modified_peptide, filename, start_time, end_time, charge, is_decoy, sample_name }

        public static readonly int[] REQUIRED_FIELDS =
            {
                (int) Field.modified_peptide, 
                (int) Field.filename, 
                (int) Field.start_time,
                (int) Field.end_time
            };

        public static readonly string[] FIELD_NAMES =
        {
            "PeptideModifiedSequence",
            "FileName",
            "MinStartTime",
            "MaxEndTime",
            "PrecursorCharge",
            "PrecursorIsDecoy",
            "SampleName"
        };

        public SrmDocument Import(TextReader reader, ILongWaitBroker longWaitBroker, long lineCount)
        {
            long linesRead = 0;
            int progressPercent = 0;
            var docNew = (SrmDocument) Document.ChangeIgnoreChangingChildren(true);
            var docReference = docNew;
            var sequenceToNode = new Dictionary<Tuple<string, bool>, IList<IdentityPath>>();
            var fileNameToFileMatch = new Dictionary<string, ChromSetFileMatch>();
            var modMatcher = new ModificationMatcher();
            // Make the dictionary of modified peptide strings to doc nodes and paths
            for (int i = 0; i < Document.PeptideCount; ++i)
            {
                IdentityPath peptidePath = Document.GetPathTo((int) SrmDocument.Level.Peptides, i);
                PeptideDocNode peptideNode = (PeptideDocNode) Document.FindNode(peptidePath);
                var peptidePair = new Tuple<string, bool>(peptideNode.ModifiedSequence, peptideNode.IsDecoy);
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
                    idPathList = new List<IdentityPath> {peptidePath};
                    sequenceToNode.Add(peptidePair, idPathList);
                }
            
            }
            string line = reader.ReadLine();
            if (line == null)
            {
                throw new IOException(Resources.PeakBoundaryImporter_Import_Failed_to_read_the_first_line_of_the_file);
            }
            linesRead++;

            // Add annotations as possible columns
            var allFieldNames = new List<string>(FIELD_NAMES);
            allFieldNames.AddRange(from def in Document.Settings.DataSettings.AnnotationDefs
                                   where def.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.precursor_result)
                                   select def.Name);

            // Try TSV,CSV,and SPACE as possible delimiters for the file
            var separators = new[]
                {
                    TextUtil.CsvSeparator,
                    TextUtil.SEPARATOR_TSV,
                    TextUtil.SEPARATOR_SPACE
                };
            if (line.IndexOfAny(separators) == -1)
            {
                throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_first_line_does_not_contain_any_of_the_possible_separators__0___tab_or_space,
                                                    TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV ? Resources.PeakBoundaryImporter_Import_comma : Resources.PeakBoundaryImporter_Import_semicolon));
            }
            char? correctSeparator = null;
            int[] fieldIndices = null;
            int requiredFieldsMax = -1;
            int fieldsTotal = -1;
            // Try each separator in turn and look for a match (in any order) to the four columns we need
            foreach (char separator in separators)
            {
                string[] headerFields = line.ParseDsvFields(separator);
                fieldsTotal = headerFields.Length;
                var fieldsForSeparator = new int[allFieldNames.Count];
                for (int i = 0; i < allFieldNames.Count; i++)
                {
                    fieldsForSeparator[i] = Array.IndexOf(headerFields, allFieldNames[i]);
                }
                int requiredFieldsFound = fieldsForSeparator.Where((index, i) => index != -1 && REQUIRED_FIELDS.Contains(i)).Count();

                if (requiredFieldsFound == REQUIRED_FIELDS.Length)
                {
                    fieldIndices = fieldsForSeparator;
                    correctSeparator = separator;
                    break;
                }

                // Keep track of the best match for use in failure message, if necessary
                if (requiredFieldsFound > requiredFieldsMax)
                {
                    requiredFieldsMax = requiredFieldsFound;
                    fieldIndices = fieldsForSeparator;
                }
            }
            if (correctSeparator == null)
            {
                string fieldNames = string.Empty;
                // Keep ReSharper from complaining
                if (fieldIndices != null)
                {
                    string[] missingFields = fieldIndices.Where((index, i) => index == -1 && REQUIRED_FIELDS.Contains(i))
                                                         .Select((index, i) => allFieldNames[i]).ToArray();
                    fieldNames = string.Join(", ", missingFields); // Not L10N
                }
                throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Failed_to_find_the_necessary_headers__0__in_the_first_line, fieldNames));
            }
            while ((line = reader.ReadLine()) != null)
            {
                linesRead++;
                if (longWaitBroker != null)
                {
                    if (longWaitBroker.IsCanceled)
                        //TODO: got rid of line below, but want to test for it -- need another way
                        //if (longWaitBroker.IsCanceled || longWaitBroker.IsDocumentChanged(Document))
                        return Document;
                    int progressNew = (int) (linesRead*100/lineCount);
                    if (progressPercent != progressNew)
                        longWaitBroker.ProgressValue = progressPercent = progressNew;
                }
                var dataFields = new DataFields(fieldIndices, line.ParseDsvFields((char) correctSeparator), allFieldNames);
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
                modifiedPeptideString = nodeForModPep.ModifiedSequence;
                string fileName = dataFields.GetField(Field.filename);
                bool isDecoy = dataFields.IsDecoy(linesRead);
                var peptideIdentifier = new Tuple<string, bool>(modifiedPeptideString, isDecoy);
                int charge;
                bool chargeSpecified = dataFields.TryGetCharge(linesRead, out charge);
                string sampleName = dataFields.GetField(Field.sample_name);

                double? startTime = dataFields.GetTime(Field.start_time,
                    Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_, linesRead);

                double? endTime = dataFields.GetTime(Field.end_time,
                    Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_end_time_, linesRead);
                    
                // Error if only one of startTime and endTime is null
                if (startTime == null && endTime != null)
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Missing_start_time_on_line__0_, linesRead));
                if (startTime != null && endTime == null)
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Missing_end_time_on_line__0_, linesRead));
                // Add filename to second dictionary if not yet encountered
                ChromSetFileMatch fileMatch;
                if (!fileNameToFileMatch.TryGetValue(fileName, out fileMatch))
                {
                    fileMatch = Document.Settings.MeasuredResults.FindMatchingMSDataFile(fileName);
                    fileNameToFileMatch.Add(fileName, fileMatch);
                }
                if (fileMatch == null)
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_file__0__on_line__1__has_not_been_imported_into_this_document_, fileName, linesRead));
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
                    var sampleFile = chromSet.MSDataFileInfos.FirstOrDefault(info => Equals(sampleName, SampleHelp.GetPathSampleNamePart(info.FilePath)));
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
                    string sequence = FastaSequence.StripModifications(modifiedPeptideString);
                    if (Document.Peptides.Any(pep => Equals(sequence, pep.Peptide.Sequence)))
                    {
                        throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_modified_state__0__on_line__1__does_not_match_any_modified_state_in_the_document_for_the_peptide__2__, modifiedPeptideString, linesRead, sequence));
                    }
                    else
                    {
                        throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_peptide_sequence__0__on_line__1__does_not_match_any_of_the_peptides_in_the_document_, sequence, linesRead));
                    }
                }
                

                // Define the annotations to be added
                var annotations = dataFields.GetAnnotations();

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
                                    string filePath = chromSet.GetFileInfo(fileId).FilePath;
                                    docNew = docNew.ChangePeak(groupPath, nameSet, filePath,
                                        null, startTime, endTime, UserSet.IMPORTED, null, false);
                                    foundSample = true;
                                }
                            }
                        }
                    }
                }
                if (!foundSample)
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_peptide__0__was_not_present_in_the_file__1__on_line__2__, 
                                                        modifiedPeptideString, fileName, linesRead));
                }
            }
            // If nothing has changed, return the old Document before ChangeIgnoreChangingChildren was turned off
            if (!ReferenceEquals(docNew, docReference))
                Document = (SrmDocument) Document.ChangeIgnoreChangingChildren(false).ChangeChildrenChecked(docNew.Children);
            return Document;
        }

        private class DataFields
        {
            private readonly int[] _fieldIndices;
            private readonly string[] _dataFields;
            private readonly IList<string> _fieldNames;

            public DataFields(int[] fieldIndices, string[] dataFields, IList<string> fieldNames)
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
                        annotations.Add(_fieldNames[i], value);
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
                        switch (decoyString.ToLower())
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