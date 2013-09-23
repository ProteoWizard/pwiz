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

        public enum Field { modified_peptide, filename, start_time, end_time, charge }

        public static readonly string[] FIELD_NAMES = new[]
            {
                "PeptideModifiedSequence",
                "FileName",
                "MinStartTime",
                "MaxEndTime",
                "PrecursorCharge"
            };

        private string FormatMods(string modifiedPeptideSequence)
        {
            return modifiedPeptideSequence.Replace(".0]", "]");
        }

        public SrmDocument Import(TextReader reader, ILongWaitBroker longWaitBroker, long lineCount)
        {
            long linesRead = 0;
            int progressPercent = 0;
            var docNew = Document;
            var sequenceToNode = new Dictionary<string, KeyValuePair<IdentityPath, PeptideDocNode>>();
            var fileNameToFileMatch = new Dictionary<string, ChromSetFileMatch>();
            // Make the dictionary of modified peptide strings to doc nodes and paths
            for (int i = 0; i < Document.PeptideCount; ++i)
            {
                IdentityPath peptidePath = Document.GetPathTo((int) SrmDocument.Level.Peptides, i);
                PeptideDocNode peptideNode = (PeptideDocNode) Document.FindNode(peptidePath);
                var peptidePair = new KeyValuePair<IdentityPath, PeptideDocNode>(peptidePath, peptideNode);
                string formattedModifiedSeq = FormatMods(Document.Settings.GetModifiedSequence(peptideNode));
                sequenceToNode.Add(formattedModifiedSeq, peptidePair);
            }
            string line = reader.ReadLine();
            if (line == null)
            {
                throw new IOException(Resources.PeakBoundaryImporter_Import_Failed_to_read_the_first_line_of_the_file);
            }
            linesRead++;

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
            int fieldsFound = -1;
            int fieldsTotal = -1;
            // Try each separator in turn and look for a match (in any order) to the five columns we need
            foreach (char separator in separators)
            {
                string[] headerFields = line.ParseDsvFields(separator);
                fieldsTotal = headerFields.Length;
                var fieldsForSeparator = new int[FIELD_NAMES.Length];
                for (int i = 0; i < FIELD_NAMES.Length; i++)
                    fieldsForSeparator[i] = Array.IndexOf(headerFields, FIELD_NAMES[i]);
                int fieldsFoundForSeparator = fieldsForSeparator.Count(index => index != -1);

                if (fieldsFoundForSeparator == FIELD_NAMES.Length)
                {
                    fieldIndices = fieldsForSeparator;
                    correctSeparator = separator;
                    break;
                }

                // Keep track of the best match for use in failure message, if necessary
                if (fieldsFoundForSeparator > fieldsFound)
                {
                    fieldsFound = fieldsFoundForSeparator;
                    fieldIndices = fieldsForSeparator;
                }
            }
            if (correctSeparator == null)
            {
                string fieldNames = string.Empty;
                // Keep ReSharper from complaining
                if (fieldIndices != null)
                {
                    string[] missingFields = fieldIndices.Where(index => index != -1)
                                                         .Select((i, index) => FIELD_NAMES[i]).ToArray();
                    fieldNames = string.Join(", ", missingFields);
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
                string[] dataFields = line.ParseDsvFields((char) correctSeparator);
                if (dataFields.Length != fieldsTotal)
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Line__0__field_count__1__differs_from_the_first_line__which_has__2_,
                        linesRead, dataFields.Length, fieldsTotal));
                }
                string modifiedPeptideString = FormatMods(dataFields[fieldIndices[(int) Field.modified_peptide]]);
                string fileName = dataFields[fieldIndices[(int) Field.filename]];
                int charge;
                double startTimeTemp, endTimeTemp;
                double? startTime, endTime;
                string chargeString = dataFields[fieldIndices[(int) Field.charge]];
                if (!int.TryParse(chargeString, out charge))
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_charge_state_, chargeString, linesRead));
                }
                string startTimeString = dataFields[fieldIndices[(int) Field.start_time]];
                if (double.TryParse(startTimeString, out startTimeTemp))
                    startTime = startTimeTemp;
                // #N/A means remove the peak
                else if (startTimeString.Equals(TextUtil.EXCEL_NA))
                    startTime = null;
                else
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_start_time_, startTimeString, linesRead));

                string endTimeString = dataFields[fieldIndices[(int)Field.end_time]];
                if(double.TryParse(endTimeString, out endTimeTemp))
                    endTime = endTimeTemp;
                else if (endTimeString.Equals(TextUtil.EXCEL_NA))
                    endTime = null;
                else
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_value___0___on_line__1__is_not_a_valid_end_time_, endTimeString, linesRead));
                    
                // Error if only one of startTime and endTime is null
                if (startTime == null && endTime != null)
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Missing_start_time_on_line__0_, linesRead));
                if (startTime != null && endTime == null)
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_Missing_end_time_on_line__0_, linesRead));
                // Add filename to second dictionary if not yet encountered
                if (!fileNameToFileMatch.ContainsKey(fileName))
                {
                    ChromSetFileMatch fileMatch = Document.Settings.MeasuredResults.FindMatchingMSDataFile(fileName);
                    fileNameToFileMatch.Add(fileName, fileMatch);
                }
                if (fileNameToFileMatch[fileName] == null)
                {
                    throw new IOException(string.Format(Resources.PeakBoundaryImporter_Import_The_file__0__on_line__1__has_not_been_imported_into_this_document_, fileName, linesRead));
                }
                string nameSet = fileNameToFileMatch[fileName].Chromatograms.Name;
                string filePath = fileNameToFileMatch[fileName].FilePath;
                // Look up the IdentityPath of peptide in first dictionary
                if (!sequenceToNode.ContainsKey(modifiedPeptideString))
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
                var pepPath = sequenceToNode[modifiedPeptideString].Key;
                var nodePep = sequenceToNode[modifiedPeptideString].Value;
                // Loop over all the transition groups in that peptide to find matching charge
                for (int i = 0; i < nodePep.Children.Count; ++i)
                {
                    IdentityPath groupRelPath = nodePep.GetPathTo(i);
                    var groupNode = (TransitionGroupDocNode) nodePep.FindNode(groupRelPath);
                    if (charge == groupNode.TransitionGroup.PrecursorCharge)
                    {
                        IdentityPath groupPath = new IdentityPath(pepPath, groupNode.Id);
                        // Change peak boundaries for the transition group
                        docNew = docNew.ChangePeak(groupPath, nameSet, filePath,
                                                   null, startTime, endTime, null, false);
                        break;
                    }
                }
            }
            if (docNew != null)
            {
                Document = docNew;
            }
            return docNew;
        }
    }
}