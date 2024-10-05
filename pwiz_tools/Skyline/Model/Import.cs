/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Array = System.Array;
using Enzyme = pwiz.Skyline.Model.DocSettings.Enzyme;

namespace pwiz.Skyline.Model
{
    public class FastaImporter
    {
        private const int MAX_EMPTY_PEPTIDE_GROUP_COUNT = 2000;

        public static int MaxEmptyPeptideGroupCount
        {
            get { return TestMaxEmptyPeptideGroupCount ?? MAX_EMPTY_PEPTIDE_GROUP_COUNT; }
        }

        private int _countPeptides;
        private int _countIons;
        readonly ModificationMatcher _modMatcher;
        private readonly TargetMap<bool> _irtTargets;

        public FastaImporter(SrmDocument document, bool peptideList)
        {
            Document = document;
            PeptideList = peptideList;
        }

        public FastaImporter(SrmDocument document, ModificationMatcher modMatcher)
            : this(document, true)
        {
            _modMatcher = modMatcher;
        }

        public FastaImporter(SrmDocument document, IrtStandard standard)
            : this(document, false)
        {
            _irtTargets = new TargetMap<bool>(standard?.Peptides.Select(pep => new KeyValuePair<Target, bool>(pep.ModifiedTarget, true)) ??
                                              Array.Empty<KeyValuePair<Target, bool>>());
        }

        public SrmDocument Document { get; private set; }
        public bool PeptideList { get; private set; }
        public int EmptyPeptideGroupCount { get; private set; }

        public IEnumerable<PeptideGroupDocNode> Import(TextReader reader, IProgressMonitor progressMonitor, long lineCount)
        {
            bool requireLibraryMatch = Document.Settings.PeptideSettings.Libraries.Pick == PeptidePick.library
                                       || Document.Settings.PeptideSettings.Libraries.Pick == PeptidePick.both;
            // Set starting values for limit counters
            int originalPeptideCount = Document.PeptideCount;
            _countPeptides = originalPeptideCount;
            _countIons = Document.PeptideTransitionCount;

            // Store set of existing FASTA sequences to keep from duplicating
            HashSet<FastaSequence> set = new HashSet<FastaSequence>();
            foreach (PeptideGroupDocNode nodeGroup in Document.Children)
            {
                FastaSequence fastaSeq = nodeGroup.Id as FastaSequence;
                if (fastaSeq != null)
                    set.Add(fastaSeq);
            }

            var peptideGroupsNew = new List<PeptideGroupDocNode>();
            var dictGroupsNew = new Dictionary<string, int>();
            PeptideGroupBuilder seqBuilder = null;

            long linesRead = 0;
            int progressPercent = -1;

            string line;
            IProgressStatus status = new ProgressStatus(string.Empty);
            while ((line = reader.ReadLine()) != null)
            {
                linesRead++;
                if (progressMonitor != null)
                {
                    // TODO when changing from ILongWaitBroker to IProgressMonitor, the old code was:
                    // if (progressMonitor.IsCanceled || progressMonitor.IsDocumentChanged(Document))
                    // IProgressMonitor does not have IsDocumentChanged.
                    if (progressMonitor.IsCanceled)
                    {
                        EmptyPeptideGroupCount = 0;
                        return new PeptideGroupDocNode[0];
                    }
                    int progressNew = (int) (linesRead*100/lineCount);
                    if (progressPercent != progressNew)
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(progressPercent = progressNew));
                }

                if (line.StartsWith(PeptideGroupBuilder.PROTEIN_SPEC_PREFIX))
                {
                    if (!requireLibraryMatch && progressMonitor == null)
                    {
                        if (_countIons > SrmDocument.MaxTransitionCount)
                        {
                            throw new InvalidDataException(TextUtil.LineSeparate(string.Format(ModelResources.FastaImporter_Import_This_import_causes_the_document_to_contain_more_than__0_n0__transitions_in__1_n0__peptides_at_line__2_n0__,
                                SrmDocument.MaxTransitionCount, _countPeptides, linesRead), ModelResources.FastaImporter_Import_Check_your_settings_to_make_sure_you_are_using_a_library_and_restrictive_enough_transition_selection_));
                        }
                        else if (_countPeptides > SrmDocument.MAX_PEPTIDE_COUNT)
                        {
                            throw new InvalidDataException(TextUtil.LineSeparate(string.Format(ModelResources.FastaImporter_Import_This_import_causes_the_document_to_contain_more_than__0_n0__peptides_at_line__1_n0__,
                                SrmDocument.MAX_PEPTIDE_COUNT, linesRead), ModelResources.FastaImporter_Import_Check_your_settings_to_make_sure_you_are_using_a_library_));
                        }
                    }
                    try
                    {
                        if (seqBuilder != null)
                            AddPeptideGroup(peptideGroupsNew, dictGroupsNew, set, seqBuilder);

                        seqBuilder = _modMatcher == null
                            ? new PeptideGroupBuilder(line, PeptideList, Document.Settings, null, _irtTargets)
                            : new PeptideGroupBuilder(line, _modMatcher, Document.Settings, null, _irtTargets);
                    }
                    catch (Exception x)
                    {
                        throw new LineColNumberedIoException(x.Message, linesRead, -1, x);
                    }

                    if (progressMonitor != null)
                    {
                        string message = string.Format(ModelResources.FastaImporter_Import_Adding_protein__0__,
                            seqBuilder.Name);
                        int newPeptideCount = _countPeptides - originalPeptideCount;
                        if (newPeptideCount > 0)
                        {
                            message = TextUtil.LineSeparate(message,
                                string.Format(ModelResources.FastaImporter_Import__0__proteins_and__1__peptides_added, peptideGroupsNew.Count,
                                    newPeptideCount));
                        }
                        progressMonitor.UpdateProgress(status = status.ChangeMessage(message));
                    }
                }
                else if (seqBuilder == null)
                {
                    if (line.Trim().Length == 0)
                        continue;
                    break;
                }
                else
                {
                    seqBuilder.AppendSequence(line, linesRead);
                }
            }
            // Add last sequence.
            if (seqBuilder != null)
                AddPeptideGroup(peptideGroupsNew, dictGroupsNew, set, seqBuilder);
            return peptideGroupsNew;
        }

        private void AddPeptideGroup(List<PeptideGroupDocNode> listGroups,
            Dictionary<string, int> dictGroupsNew,
            ICollection<FastaSequence> set,
            PeptideGroupBuilder builder)
        {
            PeptideGroupDocNode nodeGroup = builder.ToDocNode().Merge();
            FastaSequence fastaSeq = nodeGroup.Id as FastaSequence;
            if (fastaSeq != null && set.Contains(fastaSeq))
                return;
            if (nodeGroup.MoleculeCount == 0)
            {
                EmptyPeptideGroupCount++;

                // If more than MaxEmptyPeptideGroupCount, then don't keep the empty peptide groups
                // This is not useful and is likely to cause memory and performance issues
                if (EmptyPeptideGroupCount > MaxEmptyPeptideGroupCount)
                {
                    if (EmptyPeptideGroupCount == MaxEmptyPeptideGroupCount + 1)
                    {
                        ReduceToNonEmptyGroups(listGroups, dictGroupsNew);
                    }
                    return;
                }
            }
            int indexExist;
            if (fastaSeq != null && dictGroupsNew.TryGetValue(fastaSeq.Sequence, out indexExist))
            {
                AddPeptideGroupAlternative(listGroups, indexExist, fastaSeq);
                return;
            }

            if (fastaSeq != null)
                dictGroupsNew.Add(fastaSeq.Sequence, listGroups.Count);
            listGroups.Add(nodeGroup);
            _countPeptides += nodeGroup.MoleculeCount;
            _countIons += nodeGroup.TransitionCount;
        }

        private static void ReduceToNonEmptyGroups(List<PeptideGroupDocNode> listGroups, Dictionary<string, int> dictGroupsNew)
        {
            var nonEmptyGroups = listGroups.Where(g => g.MoleculeCount > 0).ToArray();
            listGroups.Clear();
            listGroups.AddRange(nonEmptyGroups);
            dictGroupsNew.Clear();
            for (int i = 0; i < listGroups.Count; i++)
            {
                var seq = listGroups[i].Id as FastaSequence;
                if (seq != null)
                    dictGroupsNew.Add(seq.Sequence, i);
            }
        }

        private static void AddPeptideGroupAlternative(List<PeptideGroupDocNode> listGroups, int indexExist, FastaSequence fastaSeq)
        {
            var nodeGroupExist = listGroups[indexExist];
            var fastaSeqExist = (FastaSequence) nodeGroupExist.Id;
            string seqName = fastaSeq.Name;
            if (Equals(fastaSeqExist.Name, seqName) || fastaSeqExist.Alternatives.Contains(a => Equals(a.Name, seqName)))
                return;  // The new name for this sequence is already accounted for

            // Add this as an alternative to the existing node
            fastaSeq = fastaSeqExist.AddAlternative(new ProteinMetadata(fastaSeq.Name, fastaSeq.Description));
            listGroups[indexExist] = new PeptideGroupDocNode(fastaSeq, nodeGroupExist.Annotations,
                nodeGroupExist.Name, nodeGroupExist.Description,
                nodeGroupExist.Peptides.ToArray(), nodeGroupExist.AutoManageChildren);
        }

        /// <summary>
        /// Converts columnar data into FASTA format.  
        /// Assumes either:
        ///   Name multicolumnDescription Sequence
        /// or:
        ///   Name Description Sequence otherColumns
        /// </summary>
        /// <param name="text">Text string containing columnar data</param>
        /// <param name="separator">Column separator</param>
        /// <returns>Conversion to FASTA format</returns>
        public static string ToFasta(string text, char separator)
        {
            var reader = new StringReader(text);
            var sb = new StringBuilder(text.Length);
            string line;
            int lineNum = 0;
            while ((line = reader.ReadLine()) != null)
            {
                lineNum++;
                string[] columns = line.Split(separator);
                if (columns.Length < 2)
                    throw new LineColNumberedIoException(ModelResources.FastaImporter_ToFasta_Too_few_columns_found, lineNum, -1);
                int fastaCol = columns.Length - 1;  // Start with assumption of Name Description Sequence
                string seq = columns[fastaCol].Trim();
                if ((fastaCol > 2) && (!FastaSequence.IsExSequence(seq)))
                {
                    // Possibly from PasteDlg, form of Name Description Sequence Accession PreferredName Gene Species
                    fastaCol = 2;  
                    seq = columns[fastaCol].Trim();
                }
                if (!FastaSequence.IsExSequence(seq))
                    throw new LineColNumberedIoException(
                        ModelResources.FastaImporter_ToFasta_Last_column_does_not_contain_a_valid_protein_sequence, lineNum,
                        fastaCol);
                sb.Append(PeptideGroupBuilder.PROTEIN_SPEC_PREFIX).Append(columns[0].Trim().Replace(@" ", @"_")); // ID
                for (int i = 1; i < fastaCol; i++)
                    sb.Append(@" ").Append(columns[i].Trim()); // Description
                sb.AppendLine();
                sb.AppendLine(seq); // Sequence
            }
            return sb.ToString();
        }

        #region Test support

        public static int? TestMaxEmptyPeptideGroupCount { get; set; }

        #endregion
    }

    public class MassListInputs
    {
        private readonly string _inputFilename;
        public string InputFilename { get { return _inputFilename; } }

        private readonly string _inputText;
        public string InputText { get { return _inputText; } }

        private IList<string> _lines;

        public IList<string> Lines { get { return _lines; } }

        public MassListInputs(string initText, bool fullText = false)
        {
            if (fullText)
                _inputText = initText;
            else
                _inputFilename = initText;
        }

        public MassListInputs(string inputText, IFormatProvider formatProvider, char separator)
        {
            _inputText = inputText;

            FormatProvider = formatProvider;
            Separator = separator;
        }

        public MassListInputs(IList<string> lines)
        {
            InitFormat(lines);
            _lines = lines;
        }

        public IList<string> ReadLines(IProgressMonitor progressMonitor, IProgressStatus status = null)
        {
            return _lines ?? (_lines = _inputFilename != null ? ReadLinesFromFile(progressMonitor, status) : ReadLinesFromText());
        }

        private IList<string> ReadLinesFromFile(IProgressMonitor progressMonitor, IProgressStatus status)
        {
            using (var reader = new LineReaderWithProgress(_inputFilename, progressMonitor, status))
            {
                var inputLines = new List<string>();
                string line;
                char[] whitespace = { ' ', '\r', '\n', '\f' }; //The usual whitespace characters, except tab which may be a separator
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim(whitespace);
                    if (line.Length > 0)
                        inputLines.Add(line);
                }
                if (inputLines.Count == 0)
                    throw new InvalidDataException(Resources.MassListImporter_Import_Empty_transition_list);
                InitFormat(inputLines);
                return inputLines;
            }
        }

        private IList<string> ReadLinesFromText()
        {
            var inputLines = ReadLinesFromText(_inputText);
            InitFormat(inputLines);
            return inputLines;
        }

        public static IList<string> ReadLinesFromText(string text)
        {
            var inputLines = new List<string>();
            using (var readerLines = new StringReader(text))
            {
                string line;
                while ((line = readerLines.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0)
                        continue;
                    inputLines.Add(line);
                }
            }
            if (inputLines.Count == 0)
                throw new InvalidDataException(Resources.MassListImporter_Import_Empty_transition_list);
            return inputLines;
        }

        /// <summary>
        /// Attempt to find the format of columnar data and throw an exception upon failure
        /// </summary>
        private void InitFormat(IList<string> inputLines)
        {
            if (FormatProvider == null)
            {
                // Throw an exception if we cannot work out the format of the input
                var inputLine = 0 < inputLines.Count ? TextUtil.LineSeparate(inputLines.Take(100)) : string.Empty;
                if (!TryInitFormat(inputLine, out var provider, out var sep))
                {
                    throw new IOException(Resources
                        .SkylineWindow_importMassListMenuItem_Click_Data_columns_not_found_in_first_line);
                }
                FormatProvider = provider;
                Separator = sep;
            }
        }

        /// <summary>
        /// Attempt to find the column separator and format provider for the input lines. Also useful for testing if data is columnar
        /// </summary>
        public static bool TryInitFormat(string inputLines, out IFormatProvider provider, out char sep)
        {
            return MassListImporter.IsColumnar(inputLines, out provider, out sep, out var columnTypes);
        }

        public IFormatProvider FormatProvider { get; set; }
        public char Separator { get; set; }
    }

    public class MassListImporter
    {
        private const int INSPECT_LINES = 50;
        public const int MZ_ROUND_DIGITS = 4;

// ReSharper disable NotAccessedField.Local
        private int _countPeptides;
        private int _countIons;
// ReSharper restore NotAccessedField.Local

        private int _linesSeen;

        // This constructor is only suitable for investigating the peptide-vs-small molecule nature of inputs
        // CONSIDER(henryS) Arguably that code should be split out into its own class
        public MassListImporter(SrmSettings settings, MassListInputs inputs, bool tolerateErrors, SrmDocument.DOCUMENT_TYPE inputType = SrmDocument.DOCUMENT_TYPE.none)
        {
            Settings = settings;
            Inputs = inputs;
            InputType = inputType;
            IsTolerateErrors = tolerateErrors;
        }

        // This constructor is suitable for investigating the peptide-vs-small molecule nature of inputs as well as actually doing an import
        public MassListImporter(SrmDocument document, MassListInputs inputs, bool tolerateErrors, SrmDocument.DOCUMENT_TYPE inputType = SrmDocument.DOCUMENT_TYPE.none)
        {
            Document = document;
            Settings = document.Settings;
            Inputs = inputs;
            InputType = inputType;
            IsTolerateErrors = tolerateErrors;
        }

        public SrmDocument Document { get; private set; }
        public MassListRowReader RowReader { get; private set; }
        public SrmSettings Settings { get; private set; }
        public MassListInputs Inputs { get; private set; }
        public IFormatProvider FormatProvider { get { return Inputs.FormatProvider; } }
        public char Separator { get { return Inputs.Separator; } }
        // What we believe the text we are importing describes: proteomics vs small_molecule vs none (meaning unknown).
        // InputType is never set to 'mixed' as we handle small molecule and proteomics input separately 
        public SrmDocument.DOCUMENT_TYPE InputType { get; private set; }
        public bool IsTolerateErrors { get; private set; }

        public PeptideModifications GetModifications(SrmDocument document)
        {
            return RowReader != null ? RowReader.GetModifications(document) : document.Settings.PeptideSettings.Modifications;
        }

        private const int PERCENT_READER = 95;

        public bool PreImport(IProgressMonitor progressMonitor, ColumnIndices indices, bool rowReadRequired = false, SrmDocument.DOCUMENT_TYPE defaultDocumentType = SrmDocument.DOCUMENT_TYPE.none)
        {
            IProgressStatus status = new ProgressStatus(ModelResources.MassListImporter_Import_Reading_transition_list).ChangeSegments(0, 3);
            // Get the lines used to guess the necessary columns and create the row reader
            if (progressMonitor != null)
            {
                if (progressMonitor.IsCanceled)
                    return false;
                progressMonitor.UpdateProgress(status);
            }

            var lines = new List<string>(Inputs.ReadLines(progressMonitor, status));
            status = status.NextSegment();
            _linesSeen = 0;
            // Decide if the input is peptide or small molecule if we haven't already
            if (InputType == SrmDocument.DOCUMENT_TYPE.none)
            {
                InputType =
                    SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(lines, Settings, 
                       defaultDocumentType) ? SrmDocument.DOCUMENT_TYPE.small_molecules : SrmDocument.DOCUMENT_TYPE.proteomic;
            }
            if (InputType == SrmDocument.DOCUMENT_TYPE.small_molecules)
            {
                if (progressMonitor != null)
                    progressMonitor.UpdateProgress(status.Complete());

                // Now that we use ColumnSelectDlg to look at Small Molecule transition lists,
                // we need to create a RowReader for these too
                // Check first line for validity
                var line = lines.FirstOrDefault();
                if (string.IsNullOrEmpty(line))
                    throw new InvalidDataException(ModelResources.MassListImporter_Import_Invalid_transition_list_Transition_lists_must_contain_at_least_precursor_m_z_product_m_z_and_peptide_sequence);

                // If no numeric columns in the first row
                if (rowReadRequired)
                {
                    SetRowReader(progressMonitor, lines.ToList(), status);
                }

                indices = ColumnIndices.FromLine(line, Separator, s => GetColumnType(s, FormatProvider));
                if (indices.Headers != null)
                {
                    lines.RemoveAt(0);
                    _linesSeen++;
                }

                return true;
            }

            if (progressMonitor != null)
            {
                if (progressMonitor.IsCanceled)
                    return false;
                progressMonitor.UpdateProgress(status = status.ChangeMessage(ModelResources.MassListImporter_Import_Inspecting_peptide_sequence_information));
            }
            if (indices != null)
            {
                RowReader = new GeneralRowReader(FormatProvider, Separator, indices, Settings, lines, progressMonitor, status);
            }
            else
            {
                SetRowReader(progressMonitor, lines, status);
            }
            return true;
        }

        /// <summary>
        /// Attempt to create a row reader and throw an exception upon failure
        /// </summary>
        private void SetRowReader(IProgressMonitor progressMonitor, List<string> lines,
            IProgressStatus status)
        {
            if (TryCreateRowReader(progressMonitor, lines, status, out var rowReader, out var mzException))
            {
                RowReader = rowReader;
            }
            else
            {
                if (mzException != null)
                {
                    throw mzException;
                }
                else if (lines.Count == 0) // Only line was a header, apparently
                {
                    throw new InvalidDataException(Resources.MassListImporter_Import_Empty_transition_list);
                }
                else 
                {
                    throw new LineColNumberedIoException(Resources.MassListImporter_Import_Failed_to_find_peptide_column, 1,
                        -1);
                }
            }
        }

        /// <summary>
        /// // Attempt to create either an ExPeptideRowReader or a GeneralRowReader
        /// </summary>
        public bool TryCreateRowReader(IProgressMonitor progressMonitor, List<string> lines,
            IProgressStatus status, out MassListRowReader rowReader, out MzMatchException mzException)
        {
            mzException = null;
            var tolerateErrors = IsTolerateErrors;

            // Check first line for validity
            var line = lines.FirstOrDefault();
            if (string.IsNullOrEmpty(line))
                throw new InvalidDataException(ModelResources
                    .MassListImporter_Import_Invalid_transition_list_Transition_lists_must_contain_at_least_precursor_m_z_product_m_z_and_peptide_sequence);
            var indices = ColumnIndices.FromLine(line, Separator, s => GetColumnType(s, FormatProvider));
            if (indices.Headers != null)
            {
                lines.RemoveAt(0);
                _linesSeen++;

            }

            // If there are no rows left after we remove the headers, we cannot create a row reader
            if (lines.Count < 1)
            {
                rowReader = null;
                return false;
            }

            // If no numeric columns in the first row
            rowReader = ExPeptideRowReader.Create(FormatProvider, Separator, indices, Settings, lines, progressMonitor, status);
            if (rowReader == null)
            {
                try
                {
                    rowReader = GeneralRowReader.Create(FormatProvider, Separator, indices, Settings, lines,
                        tolerateErrors,
                        progressMonitor, status);
                }
                catch(MzMatchException exception)
                {
                    // If we find a valid amino acid sequence but no valid precursor or product m/z column, catch the exception
                    // as it could be a small molecule transition list. If we decide later it is a peptide transition list,
                    // we throw the exception then
                    mzException = exception;
                }
            }
            return rowReader != null;
        }

        public IEnumerable<PeptideGroupDocNode> DoImport(IProgressMonitor progressMonitor,
            IDictionary<string, FastaSequence> dictNameSeq,
            List<MeasuredRetentionTime> irtPeptides,
            List<SpectrumMzInfo> librarySpectra,
            List<TransitionImportErrorInfo> errorList)
        {
            _countPeptides = Document.PeptideCount;
            _countIons = Document.PeptideTransitionCount;

            List<PeptideGroupDocNode> peptideGroupsNew = new List<PeptideGroupDocNode>();
            PeptideGroupBuilder seqBuilder = null;

            IProgressStatus status = new ProgressStatus();
            var lines = RowReader.Lines;

            // Process lines
            _linesSeen = 0;
            for (var index = 0; index < lines.Count; index++)
            {
                string row = lines[index];
                var errorInfo = RowReader.NextRow(row, ++_linesSeen);
                if (errorInfo != null)
                {
                    errorList.Add(errorInfo);
                    continue;
                }

                if (progressMonitor != null)
                {
                    if (progressMonitor.IsCanceled)
                    {
                        irtPeptides.Clear();
                        librarySpectra.Clear();
                        errorList.Clear();
                        return new PeptideGroupDocNode[0];
                    }

                    int percentComplete = (_linesSeen * PERCENT_READER / lines.Count);
                    if (status.PercentComplete != percentComplete)
                    {
                        string message = string.Format(ModelResources.MassListImporter_Import_Importing__0__,
                            RowReader.TransitionInfo.ProteinName ?? RowReader.TransitionInfo.PeptideSequence);
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(percentComplete).ChangeMessage(message));
                    }
                }

                seqBuilder = AddRow(seqBuilder, RowReader, dictNameSeq, peptideGroupsNew, row, _linesSeen, Inputs.InputFilename, irtPeptides, librarySpectra, errorList);
            }

            // Add last sequence.
            if (seqBuilder != null)
                AddPeptideGroup(peptideGroupsNew, seqBuilder, irtPeptides, librarySpectra, errorList);

            var peptideGroupsResult = MergeEqualGroups(progressMonitor, peptideGroupsNew, ref status);
            if (!ArrayUtil.ReferencesEqual(peptideGroupsResult, peptideGroupsNew))
            {
                var irtPeptidesMerged = MergeRtInfo(irtPeptides);
                irtPeptides.Clear();
                irtPeptides.AddRange(irtPeptidesMerged);
                
                var librarySpectraMerged = MergeSpectra(librarySpectra, errorList);
                librarySpectra.Clear();
                librarySpectra.AddRange(librarySpectraMerged);
            }

            return peptideGroupsResult;
        }

        private List<SpectrumMzInfo> MergeSpectra(List<SpectrumMzInfo> librarySpectra, List<TransitionImportErrorInfo> errorList)
        {
            var mergedSpectra = new List<SpectrumMzInfo>();
            foreach (var g in librarySpectra.GroupBy(s => s.Key))
            {
                var combined = g.First();
                foreach (var other in g.Skip(1))
                {
                    combined = combined.CombineSpectrumInfo(other, out var combineErrors);
                    if (combineErrors.Count > 0)
                        errorList.AddRange(combineErrors);
                }
                mergedSpectra.Add(combined);
            }

            return mergedSpectra;
        }

        private List<MeasuredRetentionTime> MergeRtInfo(List<MeasuredRetentionTime> irtPeptides)
        {
            return (from rt in irtPeptides
                group rt by rt.PeptideSequence
                into g
                select new MeasuredRetentionTime(g.Key, GetBestRt(g), true, IsStandard(g))).ToList();
        }

        private static double GetBestRt(IEnumerable<MeasuredRetentionTime> rtValues)
        {
            return new Statistics(rtValues.Select(rt => rt.RetentionTime)).Median();
        }

        private static bool IsStandard(IEnumerable<MeasuredRetentionTime> rtValues)
        {
            return rtValues.Any(rt => rt.IsStandard);
        }

        private IList<PeptideGroupDocNode> MergeEqualGroups(IProgressMonitor progressMonitor,
            IList<PeptideGroupDocNode> peptideGroups, ref IProgressStatus status)
        {
            var listKeys = new List<PeptideGroupDocNode>(); // Maintain ordered list of keys
            var dictGroupsToMergeLists = new Dictionary<PeptideGroupDocNode, List<PeptideGroupDocNode>>();
            bool merge = false;
            foreach (var nodeGroup in peptideGroups)
            {
                var nodeGroupWithoutChildren = (PeptideGroupDocNode) nodeGroup.ChangeChildren(new PeptideDocNode[0]);
                List<PeptideGroupDocNode> groupsToMerge;
                if (dictGroupsToMergeLists.TryGetValue(nodeGroupWithoutChildren, out groupsToMerge))
                    merge = true;   // Seeing a group twice means a merge is necessary
                else
                {
                    groupsToMerge = new List<PeptideGroupDocNode>();
                    dictGroupsToMergeLists.Add(nodeGroupWithoutChildren, groupsToMerge);
                    listKeys.Add(nodeGroupWithoutChildren);
                }
                groupsToMerge.Add(nodeGroup);
            }

            if (!merge)
                return peptideGroups;

            var peptideGroupsNew = new List<PeptideGroupDocNode>();
            int keysAdded = 0;
            foreach (var groupsToMerge in listKeys.Select(k => dictGroupsToMergeLists[k]))
            {
                if (groupsToMerge.Count == 1)
                    peptideGroupsNew.Add(groupsToMerge[0].Merge());
                else
                {
                    var nodeGroupNew = groupsToMerge[0];
                    foreach (var peptideGroupDocNode in groupsToMerge.Skip(1))
                        nodeGroupNew = nodeGroupNew.Merge(peptideGroupDocNode);
                    peptideGroupsNew.Add(nodeGroupNew);
                }

                keysAdded++;

                if (progressMonitor != null)
                {
                    if (progressMonitor.IsCanceled)
                        return new PeptideGroupDocNode[0];

                    int percentComplete = (keysAdded * (100 - PERCENT_READER) / listKeys.Count) + PERCENT_READER;
                    if (status.PercentComplete != percentComplete)
                    {
                        // TODO(brendanx): Switch to new message for 20.1 "Merging lists and targets"
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(percentComplete)
                            .ChangeMessage(ModelResources.MassListImporter_Import_Reading_transition_list));
                    }
                }
            }
            return peptideGroupsNew;
        }

        private PeptideGroupBuilder AddRow(PeptideGroupBuilder seqBuilder,
                                           MassListRowReader rowReader,
                                           IDictionary<string, FastaSequence> dictNameSeq,
                                           ICollection<PeptideGroupDocNode> peptideGroupsNew,
                                           string lineText,
                                           long lineNum,
                                           string sourceFile,
                                           List<MeasuredRetentionTime> irtPeptides,
                                           List<SpectrumMzInfo> librarySpectra,
                                           List<TransitionImportErrorInfo> errorList)
        {
            var info = rowReader.TransitionInfo;
            var irt = rowReader.Irt;
            var explicitRT = rowReader.ExplicitRetentionTimeInfo;
            var libraryIntensity = rowReader.LibraryIntensity;
            var productMz = rowReader.ProductMz;
            var note = rowReader.Note;
            var precursorNote = rowReader.PrecursorNote;
            var moleculeNote = rowReader.MoleculeNote;
            var moleculeListNote = rowReader.MoleculeListNote;
            if (irt == null && rowReader.IrtColumn != -1)
            {
                var error = new TransitionImportErrorInfo(string.Format(Resources.MassListImporter_AddRow_Invalid_iRT_value_at_precusor_m_z__0__for_peptide__1_, 
                        rowReader.TransitionInfo.PrecursorMz, 
                        rowReader.TransitionInfo.ModifiedSequence),
                                                          rowReader.IrtColumn,
                                                          lineNum, lineText);
                errorList.Add(error);
                return seqBuilder;
            }
            if (libraryIntensity == null && rowReader.LibraryColumn != -1)
            {
                var error = new TransitionImportErrorInfo(string.Format(Resources.MassListImporter_AddRow_Invalid_library_intensity_at_precursor__0__for_peptide__1_, 
                        rowReader.TransitionInfo.PrecursorMz, 
                        rowReader.TransitionInfo.ModifiedSequence),
                                                          rowReader.LibraryColumn,
                                                          lineNum, lineText);
                errorList.Add(error);
                return seqBuilder;
            }
            string name = info.ProteinName;
            if (info.TransitionExps.Any(t => t.IsDecoy))
                name = PeptideGroup.DECOYS;
            if (seqBuilder == null || (name != null && !Equals(name, seqBuilder.BaseName)))
            {
                if (seqBuilder != null)
                {
                    AddPeptideGroup(peptideGroupsNew, seqBuilder, irtPeptides, librarySpectra, errorList);
                }
                FastaSequence fastaSeq;
                if (name != null && dictNameSeq.TryGetValue(name, out fastaSeq) && fastaSeq != null)
                    seqBuilder = new PeptideGroupBuilder(fastaSeq, Document.Settings, sourceFile, null);
                else
                {
                    string safeName = name != null ?
                        Helpers.GetUniqueName(name, dictNameSeq.Keys) :
                        Document.GetPeptideGroupId(true);
                    seqBuilder = new PeptideGroupBuilder(PeptideGroupBuilder.PEPTIDE_LIST_PREFIX + safeName,
                        true, Document.Settings, sourceFile, null) {BaseName = name};
                }
            }
            try
            {
                seqBuilder.AppendTransition(info, irt, explicitRT, libraryIntensity, productMz, note, lineText, lineNum);
            }
            catch (InvalidDataException x)
            {
                throw new LineColNumberedIoException(x.Message, lineNum, -1, x);
            }
            return seqBuilder;
        }

        private void AddPeptideGroup(ICollection<PeptideGroupDocNode> listGroups,
                                     PeptideGroupBuilder builder,
                                     List<MeasuredRetentionTime> irtPeptides, 
                                     List<SpectrumMzInfo> librarySpectra,
                                     List<TransitionImportErrorInfo> errorList)
        {
            PeptideGroupDocNode nodeGroup = builder.ToDocNode();
            listGroups.Add(nodeGroup);
            irtPeptides.AddRange(builder.IrtPeptides);
            librarySpectra.AddRange(builder.LibrarySpectra);
            if (builder.PeptideGroupErrorInfo.Count > 0)
                errorList.AddRange(builder.PeptideGroupErrorInfo);
            _countPeptides += nodeGroup.MoleculeCount;
            _countIons += nodeGroup.TransitionCount;
        }

        public abstract class MassListRowReader
        {
            protected MassListRowReader(IFormatProvider provider,
                                        char separator,
                                        ColumnIndices indices,
                                        IList<string> lines,
                                        SrmSettings settings,
                                        IEnumerable<string> sequences,
                                        IProgressMonitor progressMonitor,
                                        IProgressStatus status)
            {
                FormatProvider = provider;
                Separator = separator;
                Indices = indices;
                Lines = lines;
                Settings = settings;
                ModMatcher = CreateModificationMatcher(settings, sequences, lines.Count, progressMonitor, status);
                NodeDictionary = new Dictionary<string, PeptideDocNode>();
            }

            private static ModificationMatcher CreateModificationMatcher(SrmSettings settings, IEnumerable<string> sequences,
                int expectedCount = 0, IProgressMonitor progressMonitor = null, IProgressStatus status = null)
            {
                var modMatcher = new ModificationMatcher();
                // We want AutoSelect on so we can generate transition groups, but we want the filter to 
                // be lenient because we are only using this to match modifications, not generate the
                // final transition groups
                var settingsMatcher = settings.ChangeTransitionFilter(filter => filter.ChangeAutoSelect(true))
                                           .ChangeTransitionFullScan(fullscan => fullscan.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.None, null, null))
                                           .ChangeTransitionFilter(filter => filter.ChangePeptidePrecursorCharges(Enumerable.Range(TransitionGroup.MIN_PRECURSOR_CHARGE,
                                                                                                                            TransitionGroup.MAX_PRECURSOR_CHARGE).Select(Adduct.FromChargeProtonated).ToArray()));
                try
                {
                    var distinctSequences = GetDistinctSequences(sequences, expectedCount, progressMonitor, status);
                    if (status != null)
                        status = status.NextSegment();

                    modMatcher.CreateMatches(settingsMatcher,
                                             distinctSequences,
                                             Properties.Settings.Default.StaticModList,
                                             Properties.Settings.Default.HeavyModList,
                                             progressMonitor, status);    // Can't use expected count
                }
                catch (FormatException)
                {
                    modMatcher.CreateMatches(settingsMatcher,
                                             new string[0],
                                             Properties.Settings.Default.StaticModList,
                                             Properties.Settings.Default.HeavyModList,
                                             progressMonitor, status);
                }
                return modMatcher;
            }

            private static IList<string> GetDistinctSequences(IEnumerable<string> sequences, int expectedCount,
                IProgressMonitor progressMonitor, IProgressStatus status)
            {
                if (sequences == null)
                    return new string[0];

                var setSeen = new HashSet<string>(expectedCount/4);
                var listSeen = new List<string>(expectedCount/4);
                int sequenceCurrent = 0;
                foreach (string sequence in sequences)
                {
                    if (progressMonitor != null)
                    {
                        sequenceCurrent++;
                        if (progressMonitor.IsCanceled)
                            return new string[0];
                        if (expectedCount > 0)
                            progressMonitor.UpdateProgress(status = status.UpdatePercentCompleteProgress(progressMonitor, sequenceCurrent, expectedCount));
                    }

                    if (!setSeen.Contains(sequence))
                    {
                        setSeen.Add(sequence);
                        listSeen.Add(sequence);
                    }
                }

                return listSeen;
            }

            protected SrmSettings Settings { get; private set; }
            protected string[] Fields { get; private set; }
            public IList<string> Lines { get; set; }
            private IFormatProvider FormatProvider { get; set; }
            public char Separator { get; private set; }
            private ModificationMatcher ModMatcher { get; set; }
            private Dictionary<string, PeptideDocNode> NodeDictionary { get; set; } 
            public ColumnIndices Indices { get; private set; }
            protected int ProteinColumn { get { return Indices.ProteinColumn; } }
            protected int PeptideColumn { get { return Indices.PeptideColumn; } }
            protected int LabelTypeColumn { get { return Indices.LabelTypeColumn; } }
            protected int FragmentNameColumn { get { return Indices.FragmentNameColumn; } }
            private int PrecursorColumn { get { return Indices.PrecursorColumn; } }
            protected double PrecursorMz { get { return ColumnMz(Fields, PrecursorColumn, FormatProvider); } }
            protected int PrecursorChargeColumn { get { return Indices.PrecursorChargeColumn; } }
            protected int? PrecursorCharge { get { return ColumnInt(Fields, PrecursorChargeColumn, FormatProvider); } }
            private int ProductColumn { get { return Indices.ProductColumn; } }
            public double ProductMz { get { return ColumnMz(Fields, ProductColumn, FormatProvider); } }
            private int ProductChargeColumn { get { return Indices.ProductChargeColumn; } }
            protected int? ProductCharge { get { return ColumnInt(Fields, ProductChargeColumn, FormatProvider); } }
            private int DecoyColumn { get { return Indices.DecoyColumn; } }
            public int IrtColumn { get { return Indices.IrtColumn; } }
            public double? Irt { get { return ColumnDouble(Fields, IrtColumn, FormatProvider); } }
            public int LibraryColumn { get { return Indices.LibraryColumn; } }
            public double? LibraryIntensity { get { return ColumnDouble(Fields, LibraryColumn, FormatProvider); } }
            protected bool IsDecoy
            {
                get { return DecoyColumn != -1 && Equals(Fields[DecoyColumn].ToLowerInvariant(), @"true"); }
            }

            public string Note { get { return ColumnString(Fields, Indices.NoteColumn); } }
            public string PrecursorNote { get { return ColumnString(Fields, Indices.PrecursorNoteColumn); } }
            public string MoleculeNote { get { return ColumnString(Fields, Indices.MoleculeNoteColumn); } }
            public string MoleculeListNote { get { return ColumnString(Fields, Indices.MoleculeListNoteColumn); } }

            public ExplicitRetentionTimeInfo ExplicitRetentionTimeInfo
            {
                get
                {
                    var explicitRT = ColumnDouble(Fields, Indices.ExplicitRetentionTimeColumn, FormatProvider);
                    return explicitRT.HasValue ? 
                        new ExplicitRetentionTimeInfo(explicitRT.Value, ColumnDouble(Fields, Indices.ExplicitRetentionTimeWindowColumn, FormatProvider)) : 
                        null;
                }
            }

            // Check the various IM related columns, return error message (or null on success)
            public string TryGetIonMobility(out double? ionMobility, out eIonMobilityUnits imUnits, out int errColumn)
            {
                var declarations = new Dictionary<eIonMobilityUnits, double?>();
                errColumn = -1;
                if (TryColumnDouble(Fields, errColumn = Indices.ExplicitDriftTimeColumn, FormatProvider, out var im))
                {
                    declarations[eIonMobilityUnits.drift_time_msec] = im;
                }
                if (TryColumnDouble(Fields, errColumn = Indices.ExplicitInverseK0Column, FormatProvider, out im))
                {
                    declarations[eIonMobilityUnits.inverse_K0_Vsec_per_cm2] = im;
                }
                if (TryColumnDouble(Fields, errColumn = Indices.ExplicitCompensationVoltageColumn, FormatProvider, out im))
                {
                    declarations[eIonMobilityUnits.compensation_V] = im;
                }
                if (TryColumnDouble(Fields, errColumn = Indices.ExplicitIonMobilityColumn, FormatProvider, out im))
                {
                    imUnits = IonMobilityFilter.IonMobilityUnitsFromL10NString(ColumnString(Fields, Indices.ExplicitIonMobilityUnitsColumn));
                    if (imUnits == eIonMobilityUnits.none)
                    {
                        ionMobility = null;
                        return ModelResources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Missing_ion_mobility_units;
                    }
                    declarations[imUnits] = im;
                }

                if (declarations.Count != 1)
                {
                    // No values (which is fine), or too many (which is not, return an error message) 
                    ionMobility = null;
                    imUnits = eIonMobilityUnits.none;
                    if (declarations.Count == 0)
                    {
                        return null;
                    }
                    return SmallMoleculeTransitionListReader.GetMultipleIonMobilitiesErrorMessage(declarations);
                }

                ionMobility = declarations.First().Value;
                imUnits = declarations.First().Key;
                return null; // No error
            }

            public ExplicitTransitionGroupValues ExplicitTransitionGroupValues
            {
                get
                {
                    TryGetIonMobility(out var explicitIonMobility, out var imUnits, out _); // Handles the several different flavors of ion mobility

                    return ExplicitTransitionGroupValues.Create(
                        ColumnDouble(Fields, Indices.ExplicitCollisionEnergyColumn, FormatProvider),
                        explicitIonMobility,
                        imUnits,
                        ColumnDouble(Fields, Indices.ExplicitCollisionCrossSectionColumn, FormatProvider));
                }
            }
            
            public ExplicitTransitionValues ExplicitTransitionValues
            {
                get
                {
                    return ExplicitTransitionValues.Create(
                            ColumnDouble(Fields, Indices.ExplicitCollisionEnergyColumn, FormatProvider),
                            ColumnDouble(Fields, Indices.ExplicitIonMobilityHighEnergyOffsetColumn, FormatProvider),
                            ColumnDouble(Fields, Indices.SLensColumn, FormatProvider),
                            ColumnDouble(Fields, Indices.ConeVoltageColumn, FormatProvider),
                            ColumnDouble(Fields, Indices.ExplicitDeclusteringPotentialColumn, FormatProvider));
                }
            }



            public PeptideModifications GetModifications(SrmDocument document)
            {
                return ModMatcher.GetDocModifications(document);
            }

            private double MzMatchTolerance { get { return Settings.TransitionSettings.Instrument.MzMatchTolerance; } }

            public ExTransitionInfo TransitionInfo { get; private set; }

            private bool IsHeavyAllowed
            {
                get { return Settings.PeptideSettings.Modifications.HasHeavyImplicitModifications; }
            }

            private bool IsHeavyTypeAllowed(IsotopeLabelType labelType)
            {
                return Settings.TryGetPrecursorCalc(labelType, null) != null;
            }

            public TransitionImportErrorInfo NextRow(string line, long lineNum)
            {
                Fields = line.ParseDsvFields(Separator);

                if (PeptideColumn == -1)
                    return new TransitionImportErrorInfo(ModelResources.MassListRowReader_NextRow_No_peptide_sequence_column_specified, null, lineNum, line);

                ExTransitionInfo info;
                try
                {
                    info = CalcTransitionInfo(lineNum);
                }
                catch (LineColNumberedIoException e)
                {
                    return new TransitionImportErrorInfo(e);
                }

                var imError = TryGetIonMobility(out var explicitIonMobility, out var imUnits, out var errColumn); // Handles the several different flavors of ion mobility
                if (!string.IsNullOrEmpty(imError))
                {
                    return new TransitionImportErrorInfo(imError, errColumn, lineNum, line);
                }

                if (!FastaSequence.IsExSequence(info.PeptideSequence))
                {
                    return new TransitionImportErrorInfo(string.Format(Resources.MassListRowReader_NextRow_Invalid_peptide_sequence__0__found,
                            info.PeptideSequence), 
                                                         PeptideColumn,
                                                         lineNum, line);
                }
                if (!info.DefaultLabelType.IsLight && !IsHeavyTypeAllowed(info.DefaultLabelType))
                {
                    return new TransitionImportErrorInfo(TextUtil.SpaceSeparate(ModelResources.MassListRowReader_NextRow_Isotope_labeled_entry_found_without_matching_settings_,
                            ModelResources.MassListRowReader_NextRow_Check_the_Modifications_tab_in_Transition_Settings),
                                                         LabelTypeColumn,
                                                         lineNum, line);
                }

                TransitionImportErrorInfo errorInfo;
                info = CalcPrecursorExplanations(info, line, lineNum, out errorInfo);
                if (errorInfo != null)
                {
                    return errorInfo;
                }

                TransitionInfo = CalcTransitionExplanations(info, line, lineNum, out errorInfo);
                return errorInfo;

            }

            protected abstract ExTransitionInfo CalcTransitionInfo(long lineNum);

            private ExTransitionInfo CalcPrecursorExplanations(ExTransitionInfo info, string lineText, long lineNum, out TransitionImportErrorInfo errorInfo)
            {
                // Enumerate all possible variable modifications looking for an explanation
                // for the precursor information
                errorInfo = null;
                double precursorMz = info.PrecursorMz;
                int? precursorZ = PrecursorCharge;
                double nearestMz = double.MaxValue;
                var peptideMods = Settings.PeptideSettings.Modifications;
                PeptideDocNode nodeForModPep = null;
                string modifiedSequence = info.ModifiedSequence;
                if (!Equals(modifiedSequence, info.PeptideSequence))
                {
                    if (!NodeDictionary.TryGetValue(modifiedSequence, out nodeForModPep))
                    {
                        nodeForModPep = ModMatcher.GetModifiedNode(modifiedSequence, null);
                        NodeDictionary.Add(modifiedSequence, nodeForModPep);
                    }
                    info.ModifiedSequence = nodeForModPep == null ? null : nodeForModPep.RawTextId;
                }
                var nodesToConsider = nodeForModPep != null ? 
                                      new List<PeptideDocNode> {nodeForModPep} :
                                      Peptide.CreateAllDocNodes(Settings, info.PeptideSequence);
                foreach (var nodePep in nodesToConsider)
                {
                    var variableMods = nodePep.ExplicitMods;
                    var defaultLabelType = info.DefaultLabelType;
                    var precursorMassH = Settings.GetPrecursorMass(defaultLabelType, info.PeptideTarget, variableMods);
                    int precursorMassShift;
                    int nearestCharge;
                    Adduct precursorCharge = CalcPrecursorCharge(precursorMassH, precursorZ, precursorMz, MzMatchTolerance, !nodePep.IsProteomic,
                                                              info.IsDecoy, out precursorMassShift, out nearestCharge);
                    if (!precursorCharge.IsEmpty)
                    {
                        info.TransitionExps.Add(new TransitionExp(variableMods, precursorCharge, defaultLabelType,
                                                                  precursorMassShift, info.ExplicitTransitionValues));
                    }
                    else
                    {
                        nearestMz = NearestMz(info.PrecursorMz, nearestMz, precursorMassH, nearestCharge);
                    }

                    if (!IsHeavyAllowed || info.IsExplicitLabelType)
                        continue;

                    foreach (var labelType in peptideMods.GetHeavyModifications().Select(typeMods => typeMods.LabelType))
                    {
                        if (!Settings.HasPrecursorCalc(labelType, variableMods))
                        {
                            continue;
                        }
                        precursorMassH = Settings.GetPrecursorMass(labelType, info.PeptideTarget, variableMods);
                        precursorCharge = CalcPrecursorCharge(precursorMassH, precursorZ, precursorMz, MzMatchTolerance, !nodePep.IsProteomic,
                                                              info.IsDecoy, out precursorMassShift, out nearestCharge);
                        if (!precursorCharge.IsEmpty)
                        {
                            info.TransitionExps.Add(new TransitionExp(variableMods, precursorCharge, labelType,
                                                                      precursorMassShift, info.ExplicitTransitionValues));
                        }
                        else
                        {
                            nearestMz = NearestMz(info.PrecursorMz, nearestMz, precursorMassH, nearestCharge);
                        }
                    }
                }

                if (info.TransitionExps.Count == 0)
                {
                    // TODO: Consistent central formatting for m/z values
                    // Use Math.Round() to avoid forcing extra decimal places
                    nearestMz = Math.Round(nearestMz, MZ_ROUND_DIGITS);
                    precursorMz = Math.Round(SequenceMassCalc.PersistentMZ(precursorMz), MZ_ROUND_DIGITS);
                    double deltaMz = Math.Round(Math.Abs(precursorMz - nearestMz), MZ_ROUND_DIGITS);
                    errorInfo = new TransitionImportErrorInfo(TextUtil.SpaceSeparate(string.Format(Resources.MassListRowReader_CalcPrecursorExplanations_,
                                precursorMz, nearestMz, deltaMz, info.PeptideSequence),
                            Resources.MzMatchException_suggestion),
                                                              PrecursorColumn,
                                                              lineNum, lineText);
                    
                }
                else if (!Settings.TransitionSettings.Instrument.IsMeasurable(precursorMz))
                {
                    precursorMz = Math.Round(SequenceMassCalc.PersistentMZ(precursorMz), MZ_ROUND_DIGITS);
                    errorInfo = new TransitionImportErrorInfo(TextUtil.SpaceSeparate(string.Format(ModelResources.MassListRowReader_CalcPrecursorExplanations_The_precursor_m_z__0__of_the_peptide__1__is_out_of_range_for_the_instrument_settings_,
                                precursorMz, info.PeptideSequence),
                            Resources.MassListRowReader_CalcPrecursorExplanations_Check_the_Instrument_tab_in_the_Transition_Settings),
                                                              PrecursorColumn,
                                                              lineNum, lineText);
                }
                // If it's within the instrument settings but not measurable, problem must be in the isolation scheme
                else if (!Settings.TransitionSettings.IsMeasurablePrecursor(precursorMz))
                {
                    precursorMz = Math.Round(SequenceMassCalc.PersistentMZ(precursorMz), MZ_ROUND_DIGITS);
                    errorInfo = new TransitionImportErrorInfo(TextUtil.SpaceSeparate(string.Format(ModelResources.MassListRowReader_CalcPrecursorExplanations_The_precursor_m_z__0__of_the_peptide__1__is_outside_the_range_covered_by_the_DIA_isolation_scheme_,
                                precursorMz, info.PeptideSequence),
                            ModelResources.MassListRowReader_CalcPrecursorExplanations_Check_the_isolation_scheme_in_the_full_scan_settings_),
                                                              PrecursorColumn,
                                                              lineNum, lineText);
                }

                return info;
            }

            private static double NearestMz(double precursorMz, double nearestMz, TypedMass precursorMassH, int precursorCharge)
            {
                var newMz = SequenceMassCalc.GetMZ(precursorMassH, precursorCharge);
                return Math.Abs(precursorMz - newMz) < Math.Abs(precursorMz - nearestMz)
                            ? newMz
                            : nearestMz;
            }

            private static Adduct CalcPrecursorCharge(TypedMass precursorMassH,
                int? precursorZ,
                double precursorMz,
                double tolerance,
                bool isCustomIon,
                bool isDecoy,
                out int massShift,
                out int nearestCharge)
            {
                return TransitionCalc.CalcPrecursorCharge(precursorMassH, precursorZ, precursorMz, tolerance, isCustomIon, isDecoy, out massShift, out nearestCharge);
            }

            private ExTransitionInfo CalcTransitionExplanations(ExTransitionInfo info, string lineText, long lineNum, out TransitionImportErrorInfo errorInfo)
            {
                errorInfo = null;
                var sequence = info.PeptideTarget;
                double productMz = ProductMz;
                int? productZ = ProductCharge;

                foreach (var transitionExp in info.TransitionExps.ToArray())
                {
                    transitionExp.Product = GetCustomIonProductExp(productMz, productZ, MzMatchTolerance, info, Settings) ??
                                            GetPrecursorIsotopeProductExp(transitionExp, sequence, productMz, productZ, MzMatchTolerance, info, Settings);
                    if (transitionExp.Product != null)
                        continue;

                    var mods = transitionExp.Precursor.VariableMods;
                    var calc = Settings.GetFragmentCalc(transitionExp.Precursor.LabelType, mods);
                    var productPrecursorMass = calc.GetPrecursorFragmentMass(sequence);
                    var productMasses = calc.GetFragmentIonMasses(sequence);
                    var potentialLosses = TransitionGroup.CalcPotentialLosses(sequence,
                        Settings.PeptideSettings.Modifications, mods, calc.MassType);
                    var types = Settings.TransitionSettings.Filter.PeptideIonTypes;

                    IonType? ionType;
                    int? ordinal;
                    TransitionLosses losses;
                    int massShift;
                    var productCharge = TransitionCalc.CalcProductCharge(productPrecursorMass,
                                                                         productZ,
                                                                         transitionExp.Precursor.PrecursorAdduct,
                                                                         types,
                                                                         productMasses,
                                                                         potentialLosses,
                                                                         productMz,
                                                                         MzMatchTolerance,
                                                                         calc.MassType,
                                                                         transitionExp.ProductShiftType,
                                                                         out ionType,
                                                                         out ordinal,
                                                                         out losses,
                                                                         out massShift);

                    if (!productCharge.IsEmpty && ionType.HasValue && ordinal.HasValue)
                    {
                        transitionExp.Product = new ProductExp(productCharge, ionType.Value, ordinal.Value, losses, massShift, info);
                    }
                    else
                    {
                        info.TransitionExps.Remove(transitionExp);
                    }
                }

                if (info.TransitionExps.Count == 0)
                {
                    productMz = Math.Round(productMz, MZ_ROUND_DIGITS);
                    // TODO: Consistent central formatting for m/z values
                    // Use Math.Round() to avoid forcing extra decimal places
                    errorInfo = new TransitionImportErrorInfo(string.Format(Resources.MassListRowReader_CalcTransitionExplanations_Product_m_z_value__0__in_peptide__1__has_no_matching_product_ion,
                            productMz, info.PeptideSequence),
                                                              ProductColumn,
                                                              lineNum, lineText);
                }
                else if (!Settings.TransitionSettings.Instrument.IsMeasurable(productMz))
                {
                    productMz = Math.Round(productMz, MZ_ROUND_DIGITS);
                    errorInfo = new TransitionImportErrorInfo(TextUtil.SpaceSeparate(string.Format(Resources.MassListRowReader_CalcTransitionExplanations_The_product_m_z__0__is_out_of_range_for_the_instrument_settings__in_the_peptide_sequence__1_,
                                productMz, info.PeptideSequence),
                            Resources.MassListRowReader_CalcPrecursorExplanations_Check_the_Instrument_tab_in_the_Transition_Settings),
                                                              ProductColumn,
                                                              lineNum, lineText);
                }

                return info;
            }

            private static ProductExp GetPrecursorIsotopeProductExp(TransitionExp transitionExp, Target sequence, double productMz, int? productZ, double tolerance,
                ExTransitionInfo info, SrmSettings settings)
            {
                var precursorAdduct = transitionExp.Precursor.PrecursorAdduct;
                // If a product charge is specified, it must be the same as the precursor
                if (productZ.HasValue && productZ.Value != precursorAdduct.AdductCharge)
                    return null;

                var fullScan = settings.TransitionSettings.FullScan;
                if (!fullScan.IsHighResPrecursor)
                    return null;

                var mods = transitionExp.Precursor.VariableMods;
                var calc = settings.GetPrecursorCalc(transitionExp.Precursor.LabelType, mods);
                var mass = calc.GetPrecursorMass(sequence);
                var massDist = calc.GetMzDistribution(sequence,
                    precursorAdduct, fullScan.IsotopeAbundances);
                var isotopeDist =
                    IsotopeDistInfo.MakeIsotopeDistInfo(massDist, mass, precursorAdduct, fullScan);

                double minDeltaMz = double.MaxValue;
                ProductExp productExp = null;
                for (int i = 0; i < isotopeDist.CountPeaks; i++)
                {
                    int massIndex = isotopeDist.PeakIndexToMassIndex(i);
                    double isotopeMz = isotopeDist.GetMZI(massIndex);
                    var deltaMz = Math.Abs(productMz - isotopeMz);
                    if (deltaMz < minDeltaMz && deltaMz <= tolerance)
                    {
                        productExp = new ProductExp(precursorAdduct, massIndex, info);
                        minDeltaMz = deltaMz;
                    }
                }

                return productExp;
            }

            /// <summary>
            /// Finds the closest custom product ion if any to a specified product m/z value.
            /// </summary>
            private static ProductExp GetCustomIonProductExp(double productMz, int? productZ, double tolerance, ExTransitionInfo info, SrmSettings settings)
            {
                double minDeltaMz = double.MaxValue;
                ProductExp productExp = null;
                var massType = settings.TransitionSettings.Prediction.FragmentMassType;

                foreach (var measuredIon in settings.TransitionSettings.Filter.MeasuredIons.Where(m => m.IsCustom))
                {
                    if (productZ.HasValue && productZ.Value != measuredIon.Charge)
                        continue;

                    var customIonMz = massType == MassType.Monoisotopic
                        ? measuredIon.SettingsCustomIon.MonoisotopicMassMz
                        : measuredIon.SettingsCustomIon.AverageMassMz;
                    var deltaMz = Math.Abs(productMz - customIonMz);
                    if (deltaMz < minDeltaMz && deltaMz <= tolerance)
                    {
                        productExp = new ProductExp(measuredIon.Adduct, measuredIon.SettingsCustomIon, info);
                        minDeltaMz = deltaMz;
                    }
                }

                return productExp;
            }

            private static double ColumnMz(string[] fields, int column, IFormatProvider provider)
            {
                double result;
                // CONSIDER: This does not allow exponents or thousands separators like the default double.Parse(). Should it?
                if (column == -1 || column >= fields.Length)
                {
                    return 0;
                }
                if (double.TryParse(fields[column], NumberStyles.Number, provider, out result))
                    return result;
                return 0;   // Invalid m/z
            }

            private static double? ColumnDouble(string[] fields, int column, IFormatProvider provider)
            {
                double result;
                if (column != -1 && column < fields.Length && double.TryParse(fields[column], NumberStyles.Float|NumberStyles.AllowThousands, provider, out result))
                    return result;
                return null;
            }

            private static bool TryColumnDouble(string[] fields, int column, IFormatProvider provider, out double result)
            {
                var attempt = ColumnDouble(fields, column, provider);
                result = attempt ?? 0;
                return attempt.HasValue;
            }

            private static int? ColumnInt(string[] fields, int column, IFormatProvider provider)
            {
                int result;
                if (column != -1 && column < fields.Length && int.TryParse(fields[column], NumberStyles.Integer, provider, out result))
                    return result;
                return null;
            }

            private static string ColumnString(string[] fields, int column)
            {
                var value = column != -1 && column < fields.Length ? fields[column].Trim() : null;
                return string.IsNullOrEmpty(value) ? null : value;
            }

            protected static int FindPrecursor(string[] fields,
                                               string sequence,
                                               string modifiedSequence,
                                               IsotopeLabelType labelType,
                                               int iSequence,
                                               int iDecoy,
                                               double tolerance,
                                               IFormatProvider provider,
                                               SrmSettings settings,
                                               out IList<TransitionExp> transitionExps)
            {
                PeptideDocNode nodeForModPep = null;
                if (!Equals(modifiedSequence, sequence))
                {
                    var modMatcher = CreateModificationMatcher(settings, new[] {modifiedSequence});
                    nodeForModPep = modMatcher.GetModifiedNode(modifiedSequence, null);
                }
                var nodesToConsider = Peptide.CreateAllDocNodes(settings, sequence).ToList();
                if (nodeForModPep != null)
                    nodesToConsider.Insert(0, nodeForModPep);

                transitionExps = new List<TransitionExp>();
                int indexPrec = -1;
                foreach (PeptideDocNode nodePep in nodesToConsider)
                {
                    var mods = nodePep.ExplicitMods;
                    var calc = settings.TryGetPrecursorCalc(labelType, mods);
                    if (calc == null)
                        continue;

                    var precursorMassH = calc.GetPrecursorMass(nodePep.Peptide.Target);
                    bool isDecoy = iDecoy != -1 && Equals(fields[iDecoy].ToLowerInvariant(), @"true");
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (indexPrec != -1 && i != indexPrec)
                            continue;
                        if (i == iSequence)
                            continue;

                        double precursorMz = ColumnMz(fields, i, provider);
                        // With increased maximum charge state, avoid guessing very high charge states for smaller values
                        if (precursorMz == 0 || precursorMz < settings.TransitionSettings.Instrument.MinMz)
                            continue;

                        int massShift;
                        var charge = CalcPrecursorCharge(precursorMassH, null, precursorMz, tolerance, !nodePep.IsProteomic, isDecoy, out massShift, out _);
                        if (!charge.IsEmpty)
                        {
                            indexPrec = i;
                            transitionExps.Add(new TransitionExp(mods, charge, labelType, massShift, null)); // This is exploratory, explicit transition values don't matter here
                        }
                    }
                }
                return indexPrec;
            }

            protected static int FindProduct(string[] fields, string seq, IEnumerable<TransitionExp> transitionExps,
                int iSequence, int iPrecursor, double tolerance, IFormatProvider provider, SrmSettings settings)
            {
                double maxProductMz = 0;
                int maxIndex = -1;
                var sequence = new Target(seq);
                var types = settings.TransitionSettings.Filter.PeptideIonTypes;
                foreach (var transitionExp in transitionExps)
                {
                    if (transitionExp.Product != null)
                        continue;

                    var mods = transitionExp.Precursor.VariableMods;
                    var calc = settings.GetFragmentCalc(transitionExp.Precursor.LabelType, mods);
                    var productPrecursorMass = calc.GetPrecursorFragmentMass(sequence);
                    var productMasses = calc.GetFragmentIonMasses(sequence);
                    var potentialLosses = TransitionGroup.CalcPotentialLosses(sequence,
                        settings.PeptideSettings.Modifications, mods, calc.MassType);

                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (i == iSequence || i == iPrecursor)
                            continue;

                        double productMz = ColumnMz(fields, i, provider);
                        if (productMz == 0)
                            continue;

                        // First check for reporter ions and precursor isotopes
                        var specialProduct = GetCustomIonProductExp(productMz, null, tolerance, null, settings) ??
                                             GetPrecursorIsotopeProductExp(transitionExp, sequence, productMz, null, tolerance, null, settings);

                        // If not found check for backbone fragment ions
                        var charge = specialProduct != null ? Adduct.SINGLY_PROTONATED :
                            TransitionCalc.CalcProductCharge(productPrecursorMass,
                                                                      null, // CONSIDER: Use product charge field?
                                                                      transitionExp.Precursor.PrecursorAdduct,
                                                                      types,
                                                                      productMasses,
                                                                      potentialLosses,
                                                                      productMz,
                                                                      tolerance,
                                                                      calc.MassType,
                                                                      transitionExp.ProductShiftType,
                                                                      out _,
                                                                      out _,
                                                                      out _,
                                                                      out _);

                        // Look for the maximum product m/z, or this function may settle for a
                        // collision energy or retention time that matches a single amino acid
                        if (!charge.IsEmpty && productMz > maxProductMz)
                        {
                            maxProductMz = productMz;
                            maxIndex = i;
                        }
                    }
                }

                return maxIndex;
            }
        }

        private class GeneralRowReader : MassListRowReader
        {
            public GeneralRowReader(IFormatProvider provider,
                char separator,
                ColumnIndices indices,
                SrmSettings settings,
                IList<string> lines,
                IProgressMonitor progressMonitor,
                IProgressStatus status)
                : base(provider, separator, indices, lines, settings, GetSequencesFromLines(lines, separator, indices), progressMonitor, status)
            {
            }

            private static IsotopeLabelType GetLabelType(string typeId)
            {
                typeId = typeId.ToLower();
                return ((Equals(typeId, IsotopeLabelType.HEAVY_NAME.Substring(0, 1)) || Equals(typeId, IsotopeLabelType.HEAVY_NAME)) ? IsotopeLabelType.heavy : IsotopeLabelType.light);
            }

            protected override ExTransitionInfo CalcTransitionInfo(long lineNum)
            {
                string proteinName = null;
                if (ProteinColumn != -1)
                    proteinName = Fields[ProteinColumn];
                string peptideSequence = RemoveSequenceNotes(Fields[PeptideColumn]);
                string modifiedSequence = RemoveModifiedSequenceNotes(Fields[PeptideColumn]);
                var info = new ExTransitionInfo(proteinName, peptideSequence, modifiedSequence, PrecursorMz, IsDecoy, ExplicitTransitionGroupValues, ExplicitTransitionValues, Note);

                if (LabelTypeColumn != -1)
                {
                    info.DefaultLabelType = GetLabelType(Fields[LabelTypeColumn]);
                    info.IsExplicitLabelType = true;                    
                }

                return info;
            }

            private struct PrecursorCandidate
            {
                public PrecursorCandidate(int sequenceIndex, int precursorMzIdex, string sequence, IList<TransitionExp> transitionExps, int labelIndex) : this()
                {
                    SequenceIndex = sequenceIndex;
                    PrecursorMzIdex = precursorMzIdex;
                    Sequence = sequence;
                    TransitionExps = transitionExps;
                    LabelIndex = labelIndex;
                }

                public int SequenceIndex { get; private set; }
                public int PrecursorMzIdex { get; private set; }
                public string Sequence { get; private set; }
                public IList<TransitionExp> TransitionExps { get; private set; }
                public int LabelIndex { get; private set; }
            }

            public static GeneralRowReader Create(IFormatProvider provider, char separator, ColumnIndices indices, SrmSettings settings, IList<string> lines,
                bool tolerateErrors, IProgressMonitor progressMonitor, IProgressStatus status)
            {
                // Split the first line into fields.
                Assume.IsTrue(lines.Count > 0);
                // Look for sequence column
                string[] fieldsFirstRow = null;
                PrecursorCandidate[] sequenceCandidates = null;
                int bestCandidateIndex = -1;
                int iLabelType = -1;

                double tolerance = settings.TransitionSettings.Instrument.MzMatchTolerance;

                var linesSeen = 0;
                status = progressMonitor != null
                    ? (status ?? new ProgressStatus()).ChangeMessage(ModelResources.MassListImporter_Import_Inspecting_peptide_sequence_information)
                    : null;

                foreach (var line in lines)
                {
                    if (progressMonitor != null)
                    {
                        if (progressMonitor.IsCanceled)
                        {
                            return null;
                        }
                        status = status.UpdatePercentCompleteProgress(progressMonitor, linesSeen++, lines.Count);
                    }
                    string[] fields = line.ParseDsvFields(separator);
                    if (fieldsFirstRow == null)
                        fieldsFirstRow = fields;

                    // Choose precursor field candidates from the first row
                    if (sequenceCandidates == null)
                    {
                        iLabelType = FindLabelType(fields, lines, separator);

                        // If no sequence column found, return null.  After this, all errors throw.
                        var newSeqCandidates = FindSequenceCandidates(fields);
                        if (newSeqCandidates.Length == 0)
                        {
                            if (tolerateErrors)
                            {
                                break; // Caller will assign columns by other means
                            }
                            return null;
                        }

                        var listNewCandidates = new List<PrecursorCandidate>();
                        foreach (var candidateIndex in newSeqCandidates)
                        {
                            string sequence = RemoveSequenceNotes(fields[candidateIndex]);
                            string modifiedSequence = RemoveModifiedSequenceNotes(fields[candidateIndex]);
                            var candidateMzIndex = -1;
                            IList<TransitionExp> transitionExps = null;
                            var usingLabelTypeColumn = iLabelType != -1;
                            // Consider the possibility that label column has been misidentified (could be some other reason for a column full
                            // of the word "light", as in CommandLineAssayImportTest\OpenSWATH_SM4_NoError.csv)
                            for (var pass = 0; pass < (usingLabelTypeColumn ? 2 : 1) && candidateMzIndex == -1; pass++)
                            {
                                IsotopeLabelType labelType;
                                if (pass == 0) 
                                {
                                    labelType = usingLabelTypeColumn ? GetLabelType(fields[iLabelType]) : IsotopeLabelType.light;
                                }
                                else
                                {
                                    // Perhaps label column was falsely identified
                                    labelType = IsotopeLabelType.light;
                                    usingLabelTypeColumn = false; 
                                }
                                candidateMzIndex = FindPrecursor(fields, sequence, modifiedSequence, labelType, candidateIndex, indices.DecoyColumn,
                                    tolerance, provider, settings, out transitionExps);
                                // If no match, and no specific label type, then try heavy.
                                if (settings.PeptideSettings.Modifications.HasHeavyModifications &&
                                    candidateMzIndex == -1 && !usingLabelTypeColumn)
                                {
                                    var peptideMods = settings.PeptideSettings.Modifications;
                                    foreach (var typeMods in peptideMods.GetHeavyModifications())
                                    {
                                        if (settings.TryGetPrecursorCalc(typeMods.LabelType, null) != null)
                                        {
                                            candidateMzIndex = FindPrecursor(fields, sequence, modifiedSequence, typeMods.LabelType, candidateIndex, indices.DecoyColumn,
                                                tolerance, provider, settings, out transitionExps);
                                            if (candidateMzIndex != -1)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            if (candidateMzIndex != -1)
                            {
                                listNewCandidates.Add(new PrecursorCandidate(candidateIndex, candidateMzIndex, sequence, transitionExps, usingLabelTypeColumn ? iLabelType : -1));
                            }
                        }

                        if (listNewCandidates.Count == 0)
                        {
                            if (tolerateErrors)
                            {
                                break; // Caller will assign columns by other means
                            }
                            throw new MzMatchException(ModelResources.GeneralRowReader_Create_No_valid_precursor_m_z_column_found, 1, -1);
                        }
                        sequenceCandidates = listNewCandidates.ToArray();
                    }

                    bestCandidateIndex = FindBestCandidate(sequenceCandidates, fields);
                    // Break if a best candidate was found
                    if (bestCandidateIndex != -1)
                        break;
                }
                if (sequenceCandidates == null)
                {
                    if (!tolerateErrors)
                    {
                        return null;
                    }
                }
                else
                {
                   
                    if (bestCandidateIndex == -1)
                        bestCandidateIndex = 0;

                    var prec = sequenceCandidates[bestCandidateIndex];
                    int iSequence = prec.SequenceIndex;
                    int iPrecursor = prec.PrecursorMzIdex;
                    int iProduct = FindProduct(fieldsFirstRow, prec.Sequence, prec.TransitionExps, prec.SequenceIndex, prec.PrecursorMzIdex,
                        tolerance, provider, settings);
                    if (iProduct == -1 && !tolerateErrors)
                        throw new MzMatchException(ModelResources.GeneralRowReader_Create_No_valid_product_m_z_column_found, 1, -1);

                    int iProt = indices.ProteinColumn;
                    if (iProt == -1)
                        iProt = FindProtein(fieldsFirstRow, iSequence, lines, indices.Headers, provider, separator);
                    int iPrecursorCharge = indices.PrecursorChargeColumn;
                    // Explicitly declaring the charge state interferes with downstream logic that matches m/z and peptide
                    // to plausible peptide modifications
                    //if (iPrecursorCharge == -1)
                    //    iPrecursorCharge = FindPrecursorCharge(fieldsFirstRow, lines, separator);
                    int iFragmentName = indices.FragmentNameColumn;
                    if (iFragmentName == -1)
                        iFragmentName = FindFragmentName(fieldsFirstRow, lines, separator);
                    iLabelType = prec.LabelIndex;

                    indices.AssignDetected(iProt, iSequence, iPrecursor, iProduct, iLabelType, iFragmentName, iPrecursorCharge);
                }
                return new GeneralRowReader(provider, separator, indices, settings, lines, progressMonitor, status);
            }

            private static int[] FindSequenceCandidates(string[] fields)
            {
                var listCandidates = new List<int>();
                for (int i = 0; i < fields.Length; i++)
                {
                    var fieldUpper = fields[i].ToUpper(CultureInfo.InvariantCulture);
                    if (@"TRUE" == fieldUpper || @"FALSE" == fieldUpper)
                        continue;
                    string seqPotential = RemoveSequenceNotes(fields[i]);
                    if (seqPotential.Length < 2)
                        continue;
                    if (FastaSequence.IsExSequence(seqPotential))
                    {
                        listCandidates.Add(i);
                    }
                }
                return listCandidates.ToArray();                
            }

            private static int FindBestCandidate(PrecursorCandidate[] precursorCandidates, string[] fields)
            {
                Assume.IsTrue(precursorCandidates.Length > 0);
                if (precursorCandidates.Length == 1)
                    return 0;
                // If any of the options has modification indicators, return it.
                for (int i = 0; i < precursorCandidates.Length; i++)
                {
                    var prec = precursorCandidates[i];
                    string seq = fields[prec.SequenceIndex];
                    if (!Equals(seq, RemoveSequenceNotes(seq)))
                        return i;
                }
                // Otherwise, it is not possible to distinguish the candidates from each other.
                return -1;
            }

            private static string RemoveSequenceNotes(string seq)
            {
                seq = RemoveSpectronautQuoting(seq);
                string seqClean = FastaSequence.StripModifications(seq);
                int dotIndex = seqClean.IndexOf('.');
                if (dotIndex != -1 || (dotIndex = seqClean.IndexOf('_')) != -1)
                    seqClean = seqClean.Substring(0, dotIndex);
                seqClean = seqClean.TrimEnd('+');
                return seqClean;
            }

            private static string RemoveSpectronautQuoting(string seq)
            {
                // Spectronaut adds underscores to the beginning and the end of most of its sequence column text
                if (seq.StartsWith(@"_") && seq.EndsWith(@"_"))
                    seq = seq.Substring(1, seq.Length - 2);
                return seq;
            }

            private static string RemoveModifiedSequenceNotes(string seq)
            {
                seq = RemoveSpectronautQuoting(seq);
                // Find all occurrences of . and _
                var dotIndices = new List<int>();
                for (int i = 0; i < seq.Length; ++i)
                {
                    if (seq[i] == '.' || seq[i] == '_')
                    {
                        dotIndices.Add(i);
                    }
                }
                var matches = FastaSequence.RGX_ALL.Matches(seq);
                int precedingNtermModLength = 0;
                foreach (Match match in matches)
                {
                    int start = match.Groups[0].Index;
                    int end = start + match.Groups[0].Length - 1;
                    // Detect the case where an N-terminal modification is specified before the first AA
                    if (start == 0)
                        precedingNtermModLength = end + 1;
                    // Ignore instances of . or _ that are within a modification tag
                    dotIndices = dotIndices.Where(index => index < start || end < index).ToList();
                }
                dotIndices.Sort();
                // Chop at the first instance of . or _ outside a modification tag
                if(dotIndices.Any())
                {
                    seq = seq.Substring(0, dotIndices.First());
                }
                seq = seq.TrimEnd('+');
                // If an N-terminal mod at the start, move it to after the first AA
                if (precedingNtermModLength > 0 && precedingNtermModLength < seq.Length)
                {
                    seq = seq.ElementAt(precedingNtermModLength) + seq.Substring(0, precedingNtermModLength) +
                          seq.Substring(precedingNtermModLength + 1);
                }
                return FastaSequence.NormalizeNTerminalMod(seq);  // Make sure any n-terminal mod gets moved to after the first AA
            }

            private static readonly string[] EXCLUDE_PROTEIN_VALUES = { @"true", @"false", @"heavy", @"light", @"unit" };

            private static int FindProtein(string[] fields, int iSequence, IEnumerable<string> lines, IList<string> headers,
                IFormatProvider provider, char separator)
            {

                // First look for all columns that are non-numeric with more that 2 characters
                List<int> listDescriptive = new List<int>();
                for (int i = 0; i < fields.Length; i++)
                {
                    if (i == iSequence)
                        continue;

                    string fieldValue = fields[i];
                    if (!double.TryParse(fieldValue, NumberStyles.Number, provider, out _))
                    {
                        if (fieldValue.Length > 2 && !EXCLUDE_PROTEIN_VALUES.Contains(fieldValue.ToLowerInvariant()))
                            listDescriptive.Add(i);
                    }                    
                }
                if (listDescriptive.Count > 0)
                {
                    // Count the distribution of values in all lines for the candidate columns
                    Dictionary<string, int> sequenceCounts = new Dictionary<string, int>();
                    Dictionary<string, int>[] valueCounts = new Dictionary<string, int>[listDescriptive.Count];
                    for (int i = 0; i < valueCounts.Length; i++)
                        valueCounts[i] = new Dictionary<string, int>();
                    foreach (string line in lines)
                    {
                        string[] fieldsNext = line.ParseDsvFields(separator);
                        if (iSequence >= fieldsNext.Length)
                            continue;
                        AddCount(fieldsNext[iSequence], sequenceCounts);
                        for (int i = 0; i < valueCounts.Length; i++)
                        {
                            int iField = listDescriptive[i];
                            string key = (iField >= fieldsNext.Length ? string.Empty : fieldsNext[iField]);
                            AddCount(key, valueCounts[i]);
                        }
                    }
                    for (int i = valueCounts.Length - 1; i >= 0; i--)
                    {
                        // Discard any column with empty cells or which is less repetitive
                        if (valueCounts[i].TryGetValue(string.Empty, out _) || valueCounts[i].Count > sequenceCounts.Count)
                            listDescriptive.RemoveAt(i);
                    }
                    // If more than one possible value, and there are headers, look for
                    // one with the word protein in it.
                    if (headers != null && listDescriptive.Count > 1)
                    {
                        foreach (int i in listDescriptive)
                        {
                            if (headers[i].ToLowerInvariant().Contains(@"protein")) // : Since many transition list files are generated in English
                                return i;
                        }
                    }
                    // At this point, just use the first possible value, if one is present
                    if (listDescriptive.Count > 0)
                    {
                        return listDescriptive[0];
                    }
                }
                return -1;
            }

            // Finds the index of the Label Type columns
            private static int FindLabelType(string[] fields, IList<string> lines, char separator)
            {
                var labelCandidates = new List<int>();
                // Look for any columns that contain something that looks like a Label Type and add them to a list
                for (int i = 0; i < fields.Length; i++)
                {
                    if (ContainsLabelType(fields[i]))
                    {
                        labelCandidates.Add(i);
                    }
                }
                if (labelCandidates.Count == 0)
                {
                    return -1;
                }
                var LabelCandidates = labelCandidates.ToArray();

                // Confirm that the rest of the column has only entries that look like Label Types and return its index,
                // if not move onto the next entry in the array
                foreach (var i in LabelCandidates)
                {
                    var allGood = true;
                    foreach (var line in lines)
                    {
                        var fieldsNext = line.ParseDsvFields(separator);
                        if (i >= fieldsNext.Length || !ContainsLabelType(fieldsNext[i]))
                        {
                            allGood = false;
                            break;
                        }
                    }
                    if (allGood)
                    {
                        return i;
                    }
                }
                return -1;
            }

            // Helper method for FindLabelType
            private static bool ContainsLabelType(string field)
            {
                field = field.ToLower(); // Now our detection is case insensitive
                if (Equals(field, IsotopeLabelType.LIGHT_NAME.Substring(0, 1)) || // Checks for "L"
                    (Equals(field, IsotopeLabelType.HEAVY_NAME.Substring(0, 1)) || // Checks for "H"
                    (Equals(field, IsotopeLabelType.LIGHT_NAME)) || // Checks for "light"
                    (Equals(field, IsotopeLabelType.HEAVY_NAME)))) // Checks for "heavy"
                {
                    return true;
                }
                return false;
            }

            // Finds the index of the Fragment Name Column
            private static int FindFragmentName(string[] fields, IList<string> lines, char separator)
            {
                var fragCandidates = new List<int>();
                // Look for any columns that contain something that looks like a Fragment Name and add them to a list
                for (int i = 0; i < fields.Length; i++)
                {
                    if (RGX_FRAGMENT_NAME.IsMatch(fields[i]))
                    {
                        fragCandidates.Add(i);
                    }
                }
                
                if (fragCandidates.Count == 0)
                {
                    return -1;
                }
                var FragCandidates = fragCandidates.ToArray();

                // Confirm that the rest of the column has only entries that look like Fragment Names and return its index,
                // if not move onto the next entry in the array
                foreach (int i in FragCandidates)
                {
                    bool allGood = true;
                    foreach (var line in lines)
                    {
                        var fieldsNext = line.ParseDsvFields(separator);
                        if ((i >= fieldsNext.Length) || !RGX_FRAGMENT_NAME.IsMatch(fieldsNext[i])) // Beware of short lines
                        {
                            allGood = false;
                            break;
                        }
                    }
                    if (allGood)
                    {
                        return i;
                    }
                }
                return -1;
            }

            // N.B. using a regex here for consistency with pwiz_tools\Skyline\SettingsUI\EditOptimizationLibraryDlg.cs(401)
            // Regular expression for finding a fragment name. Checks if the first character is a,b,c,x,y, or z and the second character is a digit
            private static readonly Regex RGX_FRAGMENT_NAME = new Regex(@"precursor|([abcxyz][\d]+)", RegexOptions.IgnoreCase);

            // This detection method for Precursor Charge interferes with downstream logic for guessing peptide modifications
            /*private static int FindPrecursorCharge (string[] fields, IList<string> lines, char separator)
            {
                var listCandidates = new List<int>();

                for (int i = 0; i < fields.Length; i++)
                {
                    // If any of the cells in the first row look like precursor charges, we add their index to the list of candidates
                    if (ContainsPrecursorCharge(fields[i]))
                    {
                        listCandidates.Add(i);
                    }
                }
                var ListCandidates = listCandidates.ToArray();

                // We test every cell in each candidate column and return the first column whose contents consistently meet our criteria
                foreach (var i in ListCandidates)
                {
                    var allGood = true;
                    foreach (var line in lines)
                    {
                        var fieldsNext = line.ParseDsvFields(separator);
                        if (!ContainsPrecursorCharge(fieldsNext[i]))
                        {
                            allGood = false;
                            break;
                        }
                    }
                    if (allGood)
                    {
                        return i;
                    }
                }
                return -1;
            }

            // Helper method for FindPrecursorCharge
            private static bool ContainsPrecursorCharge(string field)
            {
                // Checks if we can turn the string into an integer
                if (int.TryParse(field, out int j))
                {
                    // Checks if the integer is between the range of possible charges
                    if (j >= TransitionGroup.MIN_PRECURSOR_CHARGE && j <= TransitionGroup.MAX_PRECURSOR_CHARGE)
                    {
                        return true;
                    }
                }
                return false;
            }*/

            private static void AddCount(string key, IDictionary<string, int> dict)
            {
                if (dict.TryGetValue(key, out _))
                    dict[key]++;
                else
                    dict.Add(key, 1);
            }

            private static IEnumerable<string> GetSequencesFromLines(IEnumerable<string> lines, char separator, ColumnIndices indices)
            {
                return lines.Select(line => RemoveModifiedSequenceNotes(line.ParseDsvField(separator, indices.PeptideColumn)));
            }
        }

        private class ExPeptideRowReader : MassListRowReader
        {
            // Protein.Peptide.+.Label
            private const string REGEX_PEPTIDE_FORMAT = @"^([^. ]+)\.([A-Za-z 0-9_+\-\[\]]+)\..+\.(light|{0})$";

            private ExPeptideRowReader(IFormatProvider provider,
                                       char separator,
                                       ColumnIndices indices,
                                       Regex exPeptideRegex,
                                       SrmSettings settings,
                                       IList<string> lines,
                                       IProgressMonitor progressMonitor,
                                       IProgressStatus status)
                : base(provider, separator, indices, lines, settings, GetSequencesFromLines(lines, separator, indices, exPeptideRegex), progressMonitor, status)
            {
                ExPeptideRegex = exPeptideRegex;
            }

            private Regex ExPeptideRegex { get; set; }

            protected override ExTransitionInfo CalcTransitionInfo(long lineNum)
            {
                string exPeptide = Fields[PeptideColumn];
                Match match = ExPeptideRegex.Match(exPeptide);
                if (!match.Success)
                    throw new LineColNumberedIoException(string.Format(ModelResources.ExPeptideRowReader_CalcTransitionInfo_Invalid_extended_peptide_format__0__, exPeptide), lineNum, PeptideColumn);

                // Check for consistent ion mobility declaration, if any
                var err = TryGetIonMobility(out _, out _, out var errColumn);
                if (!string.IsNullOrEmpty(err))
                {
                    throw new LineColNumberedIoException(err, lineNum, errColumn);
                }

                try
                {
                    string proteinName = GetProteinName(match);
                    string peptideSequence = GetSequence(match);
                    string modifiedSequence = GetModifiedSequence(match);

                    var info = new ExTransitionInfo(proteinName, peptideSequence, modifiedSequence, PrecursorMz, IsDecoy, ExplicitTransitionGroupValues, ExplicitTransitionValues, Note)
                        {
                            DefaultLabelType = GetLabelType(match, Settings),
                            IsExplicitLabelType = true
                        };

                    return info;
                }
                catch (Exception)
                {
                    throw new LineColNumberedIoException(
                        string.Format(ModelResources.ExPeptideRowReader_CalcTransitionInfo_Invalid_extended_peptide_format__0__,
                                      exPeptide),
                        lineNum, PeptideColumn);
                }
            }

            public static ExPeptideRowReader Create(IFormatProvider provider, char separator, ColumnIndices indices, SrmSettings settings, IList<string> lines,
                IProgressMonitor progressMonitor, IProgressStatus status)
            {
                // Split the first line into fields.
                Debug.Assert(lines.Count > 0);
                string[] fields = lines[0].ParseDsvFields(separator);

                // Create the ExPeptide regular expression
                var modSettings = settings.PeptideSettings.Modifications;
                var heavyTypeNames = from typedMods in modSettings.GetHeavyModifications()
                                     select typedMods.LabelType.Name;
                string exPeptideFormat = string.Format(REGEX_PEPTIDE_FORMAT, string.Join(@"|", heavyTypeNames.ToArray()));
                var exPeptideRegex = new Regex(exPeptideFormat);

                // Look for sequence column
                string sequence;
                string modifiedSequence;
                IsotopeLabelType labelType;
                int iExPeptide = FindExPeptide(fields, exPeptideRegex, settings, out sequence, out modifiedSequence, out labelType);
                // If no sequence column found, return null.  After this,
                // all errors throw.
                if (iExPeptide == -1)
                    return null;

                if (!labelType.IsLight && !modSettings.HasHeavyImplicitModifications)
                {
                    var message = TextUtil.LineSeparate(ModelResources.ExPeptideRowReader_Create_Isotope_labeled_entry_found_without_matching_settings,
                                                        ModelResources.ExPeptideRowReaderCreateCheck_the_Modifications_tab_in_Transition_Settings);
                    throw new LineColNumberedIoException(message, 1, iExPeptide);
                }

                double tolerance = settings.TransitionSettings.Instrument.MzMatchTolerance;
                IList<TransitionExp> transitionExps;
                int iPrecursor = FindPrecursor(fields, sequence, modifiedSequence, labelType, iExPeptide, indices.DecoyColumn,
                                               tolerance, provider, settings, out transitionExps);
                if (iPrecursor == -1)
                    throw new MzMatchException(ModelResources.GeneralRowReader_Create_No_valid_precursor_m_z_column_found, 1, -1);

                int iProduct = FindProduct(fields, sequence, transitionExps, iExPeptide, iPrecursor,
                    tolerance, provider, settings);
                if (iProduct == -1)
                    throw new MzMatchException(ModelResources.GeneralRowReader_Create_No_valid_product_m_z_column_found, 1, -1);

                indices.AssignDetected(iExPeptide, iExPeptide, iPrecursor, iProduct, iExPeptide, iExPeptide, iExPeptide);
                return new ExPeptideRowReader(provider, separator, indices, exPeptideRegex, settings, lines, progressMonitor, status);
            }

            private static int FindExPeptide(string[] fields, Regex exPeptideRegex, SrmSettings settings,
                out string sequence, out string modifiedSequence, out IsotopeLabelType labelType)
            {
                labelType = IsotopeLabelType.light;

                for (int i = 0; i < fields.Length; i++)
                {
                    Match match = exPeptideRegex.Match(fields[i]);
                    if (match.Success)
                    {
                        string sequencePart = GetSequence(match);
                        if (FastaSequence.IsExSequence(sequencePart))
                        {
                            sequence = sequencePart;
                            modifiedSequence = GetModifiedSequence(match);
                            labelType = GetLabelType(match, settings);
                            return i;
                        }
                        // Very strange case where there is a match, but it
                        // doesn't have a peptide in the second group.
                        break;
                    }
                }
                sequence = null;
                modifiedSequence = null;
                return -1;
            }

            private static string GetProteinName(Match match)
            {
                return match.Groups[1].Value;
            }

            private static string GetSequence(Match match)
            {
                return FastaSequence.StripModifications(GetModifiedSequence(match));
            }

            private static string GetModifiedSequence(Match match)
            {
                return match.Groups[2].Value.Replace('_', '.');
            }

            private static IsotopeLabelType GetLabelType(Match pepExMatch, SrmSettings settings)
            {
                var modSettings = settings.PeptideSettings.Modifications;
                var typedMods = modSettings.GetModificationsByName(pepExMatch.Groups[3].Value);
                return (typedMods != null ? typedMods.LabelType : IsotopeLabelType.light);
            }

            private static IEnumerable<string> GetSequencesFromLines(IEnumerable<string> lines, char separator, ColumnIndices indices, Regex exPeptideRegex)
            {
                return lines.Select(line => GetModifiedSequence(exPeptideRegex.Match(line.ParseDsvFields(separator)[indices.PeptideColumn])));
            }
        }

        public static bool IsColumnar(string text, // Input text, possibly multiple lines separated by \n
            out IFormatProvider provider, out char sep, out Type[] columnTypes)
        {
            provider = CultureInfo.InvariantCulture;
            sep = '\0';
            columnTypes = new Type[0];

            int endLine = text.IndexOf('\n');
            string line = (endLine != -1 ? text.Substring(0, endLine) : text);
            // Avoid reporting a crosslink peptide specification as columnar just because they can contain commas
            if (CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(line.Trim(), 0) != null)
                return false;

            // Work out the column separator and the column strings
            string[] columns;
            if (TrySplitColumns(line, TextUtil.SEPARATOR_TSV, out columns)) 
            {
                sep = TextUtil.SEPARATOR_TSV;
            }
            else
            {
                bool hasCommaColumns = TrySplitColumns(line, TextUtil.SEPARATOR_CSV, out columns);
                bool hasSemiColumns = TrySplitColumns(line, TextUtil.SEPARATOR_CSV_INTL, out var semiColumns);
                if (hasCommaColumns && hasSemiColumns)
                    sep = columns.Length >= semiColumns.Length ? TextUtil.SEPARATOR_CSV : TextUtil.SEPARATOR_CSV_INTL;
                else if (hasCommaColumns)
                    sep = TextUtil.SEPARATOR_CSV;
                else if (hasSemiColumns)
                    sep = TextUtil.SEPARATOR_CSV_INTL;

                if (sep == TextUtil.SEPARATOR_CSV_INTL)
                    columns = semiColumns;
            }

            if (sep == '\0')
                return false;

            if (sep != TextUtil.SEPARATOR_CSV)
            {
                // Test for the right decimal separator when the list separator is not a comma
                var culture = CultureInfo.CurrentCulture;
                // If the local decimal separator is not a comma, then try that. Otherwise, try a comma.
                if (Equals(culture.NumberFormat.NumberDecimalSeparator,
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator))
                {
                    culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                    var nf = culture.NumberFormat;
                    nf.NumberDecimalSeparator = nf.CurrencyDecimalSeparator = nf.PercentDecimalSeparator = @",";
                    nf.NumberGroupSeparator = nf.CurrencyGroupSeparator = nf.PercentGroupSeparator = @".";
                    culture.TextInfo.ListSeparator = sep.ToString();
                }
                
                // The decimal separator that appears in the most columns wins
                var countDecimalsAsProposedCulture = CountDecimals(columns, culture);
                var countDecimalsAsCurrentProviderCulture = CountDecimals(columns, provider);
                if (countDecimalsAsProposedCulture > countDecimalsAsCurrentProviderCulture)
                {
                    provider = culture;
                }
                else if (countDecimalsAsCurrentProviderCulture == 0)
                {
                    // No obvious decimals in first line - try a few more lines
                    for (var probe = 0; probe < 100; probe++)
                    {
                        if (endLine >= text.Length || endLine < 0)
                        {
                            break;
                        }
                        var endLineNext = text.IndexOf('\n', endLine + 1);
                        var nextLine = (endLineNext < 0) ? text.Substring(endLine + 1) : text.Substring(endLine + 1, endLineNext-endLine);
                        TrySplitColumns(nextLine, sep, out var nextColumns);
                        countDecimalsAsProposedCulture = CountDecimals(nextColumns, culture);
                        countDecimalsAsCurrentProviderCulture = CountDecimals(nextColumns, provider);
                        if (countDecimalsAsProposedCulture > countDecimalsAsCurrentProviderCulture)
                        {
                            provider = culture;
                            break;
                        } else if (countDecimalsAsCurrentProviderCulture > 0)
                        {
                            break;
                        }
                        endLine = endLineNext;
                    }
                }
            }

            List<Type> listColumnTypes = new List<Type>();
            bool nonSeqFound = !char.IsWhiteSpace(sep);   // Sequence text is allowed to have white space
            foreach (string value in columns)
            {
                Type columnType = GetColumnType(value.Trim(), provider);
                if (columnType != typeof(FastaSequence))
                    nonSeqFound = true;
                listColumnTypes.Add(columnType);
            }
            columnTypes = (nonSeqFound ? listColumnTypes.ToArray() : new Type[0]);
            return nonSeqFound;
        }

        private static int CountDecimals(IEnumerable<string> values, IFormatProvider provider)
        {
            int n = 0;
            foreach (string value in values)
            {
                double result;
                if (double.TryParse(value, NumberStyles.Number, provider, out result) && result != Math.Round(result))
                {
                    n++;                    
                }
            }
            return n;
        }

        private static bool TrySplitColumns(string line, char sep, out string[] columns)
        {
            columns = line.ParseDsvFields(sep);
            return columns.Length > 1;
        }

        private static Type GetColumnType(string value, IFormatProvider provider)
        {
            if (double.TryParse(value, NumberStyles.Number, provider, out _))
                return typeof(double);
            else if (FastaSequence.IsExSequence(value))
                return typeof(FastaSequence);
            return typeof(string);
        }

        public static bool HasNumericColumn(Type[] columnTypes)
        {
            return columnTypes.IndexOf(colType => colType == typeof(double)) != -1;
        }
    }

    /// <summary>
    /// Known indices of the columns used in importing a transition list.
    /// </summary>
    public sealed class ColumnIndices
    {
        public ColumnIndices(int proteinColumn, int peptideColumn, int precursorColumn, int productColumn)
            :this()
        {
            AssignDetected(proteinColumn, peptideColumn, precursorColumn, productColumn, -1, -1, -1);
        }

        public void AssignDetected(int proteinColumn,
            int peptideColumn,
            int precursorColumn,
            int productColumn,
            int labelTypeColumn,
            int fragmentNameColumn, 
            int precursorChargeColumn)
        {
            ProteinColumn = proteinColumn;
            PeptideColumn = peptideColumn;
            PrecursorColumn = precursorColumn;
            ProductColumn = productColumn;
            LabelTypeColumn = labelTypeColumn;
            FragmentNameColumn = fragmentNameColumn;
            PrecursorChargeColumn = precursorChargeColumn;
        }

        public string[] Headers { get; set; }
        // All of these column variables must end with the string "Column" so that the reflection code in 
        // ColumnSelectDlg works
        public int ProteinColumn { get; set; }
        public int PeptideColumn { get; set; }
        public int PrecursorColumn { get; set; }
        public int PrecursorChargeColumn { get; set; }
        public int ProductColumn { get; set; }
        
        /// <summary>
        /// A column specifying the <see cref="IsotopeLabelType"/> (optional)
        /// </summary>
        public int LabelTypeColumn { get; set; }

        /// <summary>
        /// A column specifying whether a decoy is expected (optional)
        /// </summary>
        public int DecoyColumn { get; set; }

        /// <summary>
        /// A column specifying a fragment name (optional)
        /// </summary>
        public int FragmentNameColumn { get; set; }

        /// <summary>
        /// A column specifying an iRT value
        /// </summary>
        public int IrtColumn { get; set; }

        /// <summary>
        /// A column specifying a spectral library intensity for the transition
        /// </summary>
        public int LibraryColumn { get; set; }

        // After this point are new columns added that were initially only supported for small molecule transition lists

        public int ExplicitRetentionTimeColumn { get; set; }

        public int ExplicitRetentionTimeWindowColumn { get; set; }

        public int ExplicitCollisionEnergyColumn { get; set; }

        public int NoteColumn { get; set; }

        public int PrecursorNoteColumn { get; set; }

        public int MoleculeNoteColumn { get; set; }

        public int MoleculeListNoteColumn { get; set; }

        public int SLensColumn { get; set; }

        public int ConeVoltageColumn { get; set; }

        public int ExplicitIonMobilityColumn { get; set; }

        public int ExplicitIonMobilityUnitsColumn { get; set; }

        public int ExplicitIonMobilityHighEnergyOffsetColumn { get; set; }

        public int ExplicitInverseK0Column { get; set; }

        public int ExplicitDriftTimeColumn { get; set; }

        public int ExplicitCompensationVoltageColumn { get; set; }

        public int ExplicitDeclusteringPotentialColumn { get; set; }

        public int ExplicitCollisionCrossSectionColumn { get; set; }

        public int ProteinDescriptionColumn { get; set; }

        // From here on is new columns for Small Molecules only
        public int PrecursorAdductColumn { get; set; }

        public int ProductNameColumn { get; set; }

        public int ProductFormulaColumn { get; set; }

        public int ProductNeutralLossColumn { get; set; }

        public int ProductAdductColumn { get; set; }

        public int ProductChargeColumn { get; set; }

        public int InChiKeyColumn { get; set; }

        public int CASColumn { get; set; }

        public int HMDBColumn { get; set; }

        public int InChiColumn { get; set; }

        public int SMILESColumn { get; set; }

        public int KEGGColumn { get; set; }

        public int MoleculeListNameColumn { get; set; }

        public int MolecularFormulaColumn { get; set; }

        public int MoleculeNameColumn { get; set; }

        public ColumnIndices()
        {
            // Iterates through the column indices and initializes them all to -1
            foreach (var property in GetType().GetProperties())
            {
                if (property.Name.EndsWith(@"Column") && property.PropertyType == typeof(int))
                {
                    property.SetValue(this, -1);
                }
            }
        }

        public static ColumnIndices FromLine(string line, char separator, Func<string, Type> getColumnType)
        {
            var ci = new ColumnIndices();
            string[] fields = line.ParseDsvFields(separator);
            if (fields.All(field => getColumnType(field.Trim()) != typeof(double)))
                ci.FindColumns(fields);
            return ci;
        }

        private string FormatHeader(string col)
        {
            // Remove spaces and make lowercase. This matches the format of the names they are tested against
            return col.ToLowerInvariant().Replace(@" ", string.Empty);
        }

        public void FindColumns(string[] headers)
        {
            Headers = headers;
            int index = 0;
            var considered = new HashSet<string>();

            void SetPropertyValue(string propertyName, int value)
            {
                considered.Add(propertyName); // Aids in checking that we've covered all header types
                var property = GetType().GetProperties().First(p => p.Name == propertyName);
                property.SetValue(this, value);
            }

            void FindValueMatch(string key, string header, string propertyName)
            {
                considered.Add(propertyName); // Aids in checking that we've covered all header types
                foreach (var item in SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms)
                {
                    if (item.Value.Equals(key))
                    {
                        // Remove whitespace and make the strings lowercase for comparison
                        var lowerValue = item.Key.ToLower();
                        lowerValue = lowerValue.Replace(@" ", string.Empty);
                        var lowerHeader = header.ToLower();
                        lowerHeader = lowerHeader.Replace(@" ", string.Empty);
                        var lowerKey = item.Value.ToLower();
                        lowerKey = lowerKey.Replace(@" ", string.Empty);
                        if (lowerValue.Equals(lowerHeader) || lowerKey.Equals(lowerHeader))
                        {

                            SetPropertyValue(propertyName, index);
                        }
                    }
                }
            }

            foreach (string header in headers)
            {
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.moleculeGroup, header, nameof(MoleculeListNameColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.namePrecursor, header, nameof(MoleculeNameColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.nameProduct, header, nameof(ProductNameColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.formulaPrecursor, header, nameof(MolecularFormulaColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.formulaProduct, header, nameof(ProductFormulaColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.neutralLossProduct, header, nameof(ProductNeutralLossColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.mzPrecursor, header, nameof(PrecursorColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.mzProduct, header, nameof(ProductColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.chargePrecursor, header, nameof(PrecursorChargeColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.chargeProduct, header, nameof(ProductChargeColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.rtPrecursor, header, nameof(ExplicitRetentionTimeColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.rtWindowPrecursor, header, nameof(ExplicitRetentionTimeWindowColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.cePrecursor, header, nameof(ExplicitCollisionEnergyColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.imPrecursor, header, nameof(ExplicitIonMobilityColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.imPrecursor_invK0, header, nameof(ExplicitInverseK0Column));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.dtPrecursor, header, nameof(ExplicitDriftTimeColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.imHighEnergyOffset, header, nameof(ExplicitIonMobilityHighEnergyOffsetColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.imUnits, header, nameof(ExplicitIonMobilityUnitsColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.ccsPrecursor, header, nameof(ExplicitCollisionCrossSectionColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.slens, header, nameof(SLensColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.coneVoltage, header, nameof(ConeVoltageColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.compensationVoltage, header, nameof(ExplicitCompensationVoltageColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.declusteringPotential, header, nameof(ExplicitDeclusteringPotentialColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.transitionNote, header, nameof(NoteColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.precursorNote, header, nameof(PrecursorNoteColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.moleculeNote, header, nameof(MoleculeNoteColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.moleculeListNote, header, nameof(MoleculeListNoteColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.labelType, header, nameof(LabelTypeColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.adductPrecursor, header, nameof(PrecursorAdductColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.adductProduct, header, nameof(ProductAdductColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.idCAS, header, nameof(CASColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.idInChiKey, header, nameof(InChiKeyColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.idInChi, header, nameof(InChiColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.idHMDB, header, nameof(HMDBColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.idKEGG, header, nameof(KEGGColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.idSMILES, header, nameof(SMILESColumn));
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.iRT, header, nameof(IrtColumn)); // For Assay Library use
                FindValueMatch(SmallMoleculeTransitionListColumnHeaders.libraryIntensity, header, nameof(LibraryColumn)); // For Assay Library use
                index++;
            }

            SetPropertyValue(nameof(ProteinColumn), headers.IndexOf(col => ProteinNames.Contains(FormatHeader(col))));
            SetPropertyValue(nameof(DecoyColumn), headers.IndexOf(col => DecoyNames.Contains(FormatHeader(col))));
            SetPropertyValue(nameof(IrtColumn), headers.IndexOf(col => IrtColumnNames.Contains(FormatHeader(col))));
            SetPropertyValue(nameof(LibraryColumn), headers.IndexOf(col => LibraryColumnNames.Contains(FormatHeader(col))));
            SetPropertyValue(nameof(FragmentNameColumn), headers.IndexOf(col => FragmentNameNames.Contains(FormatHeader(col))));
            SetPropertyValue(nameof(ProteinDescriptionColumn), headers.IndexOf(col => ProteinDescriptionNames.Contains(FormatHeader(col))));
            SetPropertyValue(nameof(PeptideColumn), headers.IndexOf(col => PeptideNames.Contains(FormatHeader(col))));

            // Now make sure that no column has been forgotten
            foreach (var property in GetType().GetProperties())
            {
                var name = property.Name;
                if (name.EndsWith(@"Column"))
                {
                    Assume.IsTrue(considered.Contains(name), @"No FindColumns handler for " + name);
                }
            }
        }

        // Checks all the column indices and resets any that have the given index to -1
        public void ResetDuplicateColumns(int index)
        {
            // Iterates through the column indices, if they share an index with index they are reset to -1
            foreach (var property in GetType().GetProperties())
            {
                if (property.Name.EndsWith(@"Column") && property.PropertyType == typeof(int))
                {
                    if ((int)property.GetValue(this, null) == index)
                    {
                        property.SetValue(this, -1);
                    }
                }
            }
        }

        /// <summary>
        /// It's not unusual for a single column to hold a few fields worth of info, as in
        /// "744.8 858.39 10 APR.AGLCQTFVYGGCR.y7.light 105 40" where protein, peptide, and label are all stuck together,
        /// so that all three lay claim to a single column. In such cases, prioritize peptide.
        /// </summary>
        public void PrioritizePeptideColumn()
        {
            if (PeptideColumn != -1)
            {
                var save = PeptideColumn;
                ResetDuplicateColumns(PeptideColumn);
                PeptideColumn = save;
            }
        }


        // ReSharper disable StringLiteralTypo
        public static IEnumerable<string> ProteinNames { get { return new[] { @"proteinname", @"protein.name", @"protein", @"proteinid", @"uniprotid" }; } }
        public static IEnumerable<string> PrecursorChargeNames { get { return new[] { @"precursorcharge" }; } }
        public static IEnumerable<string> ProductChargeNames { get { return new[] { @"productcharge" }; } }
        public static IEnumerable<string> IrtColumnNames { get { return new[] { @"irt", @"normalizedretentiontime", @"tr_recalibrated" }; } }
        public static IEnumerable<string> LibraryColumnNames { get { return new[] { @"libraryintensity", @"relativeintensity", @"relative_intensity", @"relativefragmentintensity", @"library_intensity" }; } }
        public static IEnumerable<string> DecoyNames { get { return new[] { @"decoy" }; } }
        public static IEnumerable<string> FragmentNameNames { get { return new[] { @"fragmentname", @"fragment_name" }; } }
        public static IEnumerable<string> LabelTypeNames { get { return new[] { @"labeltype" }; } }
        public static IEnumerable<string> ExplicitRetentionTimeNames { get { return new[] { @"explicitretentiontime", @"precursorrt" }; } }
        public static IEnumerable<string> ExplicitRetentionTimeWindowNames { get { return new[] { @"explicitretentiontimewindow", @"precursorrtwindow" }; } }
        public static IEnumerable<string> ExplicitCollisionEnergyNames { get { return new[] { @"explicitcollisionenergy" }; } }
        public static IEnumerable<string> NoteNames { get { return new[] { @"note" }; } }
        public static IEnumerable<string> SLensNames { get { return new[] { @"slens", @"s-lens" }; } }
        public static IEnumerable<string> ConeVoltageNames { get { return new[] { @"conevoltage" }; } }
        public static IEnumerable<string> ExplicitDeclusteringPotentialNames { get { return new[] { @"explicitdeclusteringpotential" }; } }
        public static IEnumerable<string> ExplicitCompensationVoltageNames { get { return new[] { @"explicitcompensationvoltage" }; } }
        public static IEnumerable<string> MoleculeListNameNames { get { return new[] { @"moleculelistname", @"moleculegroup" }; } }
        public static IEnumerable<string> ProteinDescriptionNames { get { return new[] { @"proteindescription" }; } }
        public static IEnumerable<string> PeptideNames { get { return new[] { @"peptidemodifiedsequence", @"peptide" }; } }
        public static IEnumerable<string> MolecularFormulaNames { get { return new[] { @"molecularformula", @"precursorformula" }; } }
        public static IEnumerable<string> PrecursorAdductNames { get { return new[] { @"precursoradduct" }; } }
        public static IEnumerable<string> ProductNames { get { return new[] { @"productmz", @"productm/z" }; } }
        public static IEnumerable<string> PrecursorNames { get { return new[] { @"precursormz", @"precursorm/z" }; } }
        public static IEnumerable<string> ProductFormulaNames { get { return new[] { @"productformula" }; } }
        public static IEnumerable<string> ProductAdductNames { get { return new[] { @"productadduct" }; } }
        public static IEnumerable<string> ProductNameNames { get { return new[] { @"productname", @"fragmenttype", @"transitionname" }; } }
        public static IEnumerable<string> ProductNeutralLossNames { get { return new[] { @"productneutralloss" }; } }
        public static IEnumerable<string> MoleculeNameNames { get { return new[] { @"moleculename", @"precursorname" }; } }
        // ReSharper restore StringLiteralTypo
    }

    /// <summary>
    /// All possible explanations for a single transition
    /// </summary>
    public sealed class ExTransitionInfo
    {
        public ExTransitionInfo(string proteinName, string peptideSequence, string modifiedSequence, double precursorMz, bool isDecoy, ExplicitTransitionGroupValues explicitTransitionGroupValues, ExplicitTransitionValues explicitTransitionValues, string note)
        {
            ProteinName = proteinName;
            PeptideTarget = new Target(peptideSequence);
            ModifiedSequence = modifiedSequence;
            PrecursorMz = precursorMz;
            IsDecoy = isDecoy;
            DefaultLabelType = IsotopeLabelType.light;
            TransitionExps = new List<TransitionExp>();
            ExplicitTransitionGroupValues = explicitTransitionGroupValues;
            ExplicitTransitionValues = explicitTransitionValues;
            Note = note;
        }

        public string ProteinName { get; private set; }
        public Target PeptideTarget { get; private set; }
        public string PeptideSequence { get { return PeptideTarget.Sequence; } }
        public string ModifiedSequence { get; set; }
        public double PrecursorMz { get; private set; }
        public ExplicitTransitionGroupValues ExplicitTransitionGroupValues { get; private set; }
        public ExplicitTransitionValues ExplicitTransitionValues { get; private set; }
        public string Note { get; private set; }

        public bool IsDecoy { get; private set; }

        /// <summary>
        /// The first label type to try in explaining the precursor m/z value
        /// </summary>
        public IsotopeLabelType DefaultLabelType { get; set; }

        /// <summary>
        /// True if only the default label type is allowed
        /// </summary>
        public bool IsExplicitLabelType { get; set; }

        /// <summary>
        /// A list of potential explanations for the Q1 and Q3 m/z values
        /// </summary>
        public List<TransitionExp> TransitionExps { get; private set; }

        public IEnumerable<ExplicitMods> PotentialVarMods
        {
            get { return TransitionExps.Select(exp => exp.Precursor.VariableMods).Distinct(); }
        }
    }

    /// <summary>
    /// Explanation for a single transition
    /// </summary>
    public sealed class TransitionExp
    {
        public TransitionExp(ExplicitMods mods, Adduct precursorCharge, IsotopeLabelType labelType, int precursorMassShift, ExplicitTransitionValues explicitTransitionValues)
        {
            Precursor = new PrecursorExp(mods, precursorCharge, labelType, precursorMassShift); 
            ExplicitTransitionValues = explicitTransitionValues;
        }

        public bool IsDecoy { get { return Precursor.MassShift.HasValue; } }
        public ExplicitTransitionValues ExplicitTransitionValues { get; private set; }
        public TransitionCalc.MassShiftType ProductShiftType
        {
            get
            {
                return IsDecoy
                           ? TransitionCalc.MassShiftType.either
                           : TransitionCalc.MassShiftType.none;
            }
        }

        public PrecursorExp Precursor { get; private set; }
        public ProductExp Product { get; set; }
    }

    public sealed class PrecursorExp
    {
        public PrecursorExp(ExplicitMods mods, Adduct precursorAdduct, IsotopeLabelType labelType, int massShift)
        {
            VariableMods = mods;
            PrecursorAdduct = precursorAdduct;
            LabelType = labelType;
            MassShift = null;
            if (massShift != 0)
                MassShift = massShift;
        }

        public ExplicitMods VariableMods { get; private set; }
        public Adduct PrecursorAdduct { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
        public int? MassShift { get; private set; }

        #region object overrides

        public bool Equals(PrecursorExp other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.VariableMods, VariableMods) &&
                Equals(other.PrecursorAdduct, PrecursorAdduct) &&
                Equals(other.LabelType, LabelType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (PrecursorExp)) return false;
            return Equals((PrecursorExp) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (VariableMods != null ? VariableMods.GetHashCode() : 0);
                result = (result*397) ^ PrecursorAdduct.GetHashCode();
                result = (result*397) ^ (LabelType != null ? LabelType.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public sealed class ProductExp
    {
        public ProductExp(Adduct productAdduct, IonType ionType, int fragmentOrdinal, TransitionLosses losses, int massShift, ExTransitionInfo info)
        {
            Adduct = productAdduct;
            IonType = ionType;
            FragmentOrdinal = fragmentOrdinal;
            Losses = losses;
            MassShift = null;
            if (massShift != 0)
                MassShift = massShift;
            ExInfo = info;
        }

        public ProductExp(Adduct productAdduct, int massIndex, ExTransitionInfo info)
        {
            Adduct = productAdduct;
            IonType = IonType.precursor;
            MassIndex = massIndex;
            ExInfo = info;
        }

        public ProductExp(Adduct productAdduct, CustomIon customIon, ExTransitionInfo info)
        {
            Adduct = productAdduct;
            IonType = IonType.custom;
            CustomIon = customIon;
            ExInfo = info;
        }

        public Adduct Adduct { get; private set; }
        public IonType IonType { get; private set; }
        public int FragmentOrdinal { get; private set; }
        public TransitionLosses Losses { get; private set; }
        public int? MassShift { get; private set; }
        public ExTransitionInfo ExInfo { get; private set; }
        public CustomIon CustomIon { get; private set; }
        public int? MassIndex { get ; private set; }    // For precursor isotopes
    }

    public class MzMatchException : LineColNumberedIoException
    {
        public MzMatchException(string message, long lineNum, int colNum)
            : base(message, TextUtil.LineSeparate(string.Empty, Resources.MzMatchException_suggestion), lineNum, colNum)
        { }
    }

    public class PeptideGroupBuilder
    {
        // filename to use if no file has been specified
        public const string CLIPBOARD_FILENAME = @"Clipboard";
        public const string PEPTIDE_LIST_PREFIX = @">>";
        public const string PROTEIN_SPEC_PREFIX = @">";
        private readonly StringBuilder _sequence = new StringBuilder();
        private readonly List<PeptideDocNode> _peptides;
        private readonly Dictionary<int, Adduct> _charges;
        private readonly SrmSettings _settings;
        private readonly Enzyme _enzyme;
        private readonly bool _customName;

        private FastaSequence _activeFastaSeq;
        private Peptide _activePeptide;
        private ExplicitRetentionTimeInfo _activeExplicitRetentionTimeInfo;
        private string _activeModifiedSequence;
        private readonly string _sourceFile;

        private readonly TargetMap<bool> _irtTargets;
        private const int IRT_MIN_ION_COUNT = 3;

        // Order is important to making the variable modification choice deterministic
        // when more than one potential set of variable modifications work to explain
        // the contents of the active peptide.
        private List<ExplicitMods> _activeVariableMods;
        private List<PrecursorExp> _activePrecursorExps;
        private double _activePrecursorMz;
        private ExplicitTransitionGroupValues _activeExplicitTransitionGroupValues;
        private readonly List<ExTransitionInfo> _activeTransitionInfos;
        private double? _irtValue;
        private readonly List<MeasuredRetentionTime> _irtPeptides;
        private readonly List<TransitionImportErrorInfo> _peptideGroupErrorInfo;
        private readonly List<TransitionGroupLibraryIrtTriple> _groupLibTriples;
        private readonly List<SpectrumMzInfo> _librarySpectra;
        private readonly List<SpectrumPeaksInfo.MI> _activeLibraryIntensities;

        private readonly ModificationMatcher _modMatcher;
        private bool _autoManageChildren;

        public PeptideGroupBuilder(FastaSequence fastaSequence, SrmSettings settings, string sourceFile, TargetMap<bool> irtTargets)
        {
            _activeFastaSeq = fastaSequence;
            _autoManageChildren = true;
            if (fastaSequence != null)
            {
                BaseName = Name = fastaSequence.Name;
                Description = fastaSequence.Description;
                Alternatives = fastaSequence.Alternatives;
            }
            _settings = settings;
            _enzyme = _settings.PeptideSettings.Enzyme;
            _peptides = new List<PeptideDocNode>();
            _charges = new Dictionary<int, Adduct>();
            _groupLibTriples = new List<TransitionGroupLibraryIrtTriple>();
            _activeTransitionInfos = new List<ExTransitionInfo>();
            _irtPeptides = new List<MeasuredRetentionTime>();
            _librarySpectra = new List<SpectrumMzInfo>();
            _activeLibraryIntensities = new List<SpectrumPeaksInfo.MI>();
            _peptideGroupErrorInfo = new List<TransitionImportErrorInfo>();
            _activeModifiedSequence = null;
            _sourceFile = sourceFile;
            _irtTargets = irtTargets;
        }

        public PeptideGroupBuilder(string line, bool peptideList, SrmSettings settings, string sourceFile, TargetMap<bool> irtTargets)
            : this(null, settings, sourceFile, irtTargets)
        {
            int start = (line.Length > 0 && line[0] == '>' ? 1 : 0);
            // If there is a second >, then this is a custom name, and not
            // a real FASTA sequence.
            if (line.Length > 1 && line[1] == '>')
            {
                _customName = true;
                start++;
                BaseName = Name = line.Substring(start);
            }
            else
            {
                var fastaSequence = FastaData.MakeFastaSequence(line, @"A");
                BaseName = Name = fastaSequence.Name;
                Description = fastaSequence.Description;
                Alternatives = fastaSequence.Alternatives;
            }
            PeptideList = peptideList;
        }

        public PeptideGroupBuilder(string line, ModificationMatcher modMatcher, SrmSettings settings, string sourceFile, TargetMap<bool> irtTargets)
            : this(line, true, settings, sourceFile, irtTargets)
        {
            _modMatcher = modMatcher;
            if (modMatcher != null)
                _autoManageChildren = !modMatcher.HasSeenMods;   // Any specified matches turns off auto-manage
        }

        /// <summary>
        /// Used in the case where the user supplied name may be different
        /// from the <see cref="Name"/> property.
        /// </summary>
        public string BaseName { get; set; }

        public List<MeasuredRetentionTime> IrtPeptides { get { return _irtPeptides; } }
        public List<SpectrumMzInfo> LibrarySpectra { get { return _librarySpectra; } } 
        public List<TransitionImportErrorInfo> PeptideGroupErrorInfo { get { return _peptideGroupErrorInfo; } } 

        public string Name { get; private set; }
        public string Description { get; private set; }
        public ImmutableList<ProteinMetadata> Alternatives { get; private set; }
        public string AA
        {
            get
            {
                return _sequence.ToString();
            }

            set
            {
                _sequence.Remove(0, _sequence.Length);
                _sequence.Append(value);
            }
        }
        public bool PeptideList { get; private set; }

        public void AppendSequence(string seqMod, long lineNum)
        {
            var charge = Transition.GetChargeFromIndicator(seqMod, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
            seqMod = Transition.StripChargeIndicators(seqMod, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, true);
            var seq = FastaSequence.StripModifications(seqMod);
            // Auto manage the children unless there is at least one modified sequence in the fasta
            _autoManageChildren = _autoManageChildren && Equals(seq, seqMod);
            // Get rid of whitespace
            seq = seq.Replace(@" ", string.Empty).Trim();
            // Get rid of 
            if (seq.EndsWith(@"*"))
                seq = seq.Substring(0, seq.Length - 1);

            if (!PeptideList)
                _sequence.Append(seq);
            else
            {
                // If there is a ModificationMatcher, use it to create the DocNode.
                PeptideDocNode nodePep;
                if (_modMatcher != null)
                {
                    nodePep = _modMatcher.GetModifiedNode(seqMod);
                    if (nodePep == null)
                        throw new LineColNumberedIoException(string.Format(ModelResources.PeptideGroupBuilder_AppendSequence_Failed_to_interpret_modified_sequence__0_, seqMod), lineNum, -1);
                }
                else
                {
                    Peptide peptide = new Peptide(null, seq, null, null, _enzyme.CountCleavagePoints(seq));
                    nodePep = new PeptideDocNode(peptide);
                }
                _peptides.Add(nodePep);
                if (!charge.IsEmpty)
                    _charges.Add(nodePep.Id.GlobalIndex, charge);
            }
        }

        public void AppendTransition(ExTransitionInfo info, double? irt, ExplicitRetentionTimeInfo explicitRT, double? libraryIntensity, double productMz, string note, string lineText, long lineNum)
        {
            _autoManageChildren = false;
            // Treat this like a peptide list from now on.
            PeptideList = true;

            if (_activeFastaSeq == null && AA.Length > 0)
                _activeFastaSeq = new FastaSequence(Name, Description, Alternatives, AA);

            string sequence = info.PeptideSequence;
            if (_activePeptide != null)
            {
                if (IsPeptideChanged(info))
                {
                    CompletePeptide(true);
                }
                else
                {
                    var intersectVariableMods = new List<ExplicitMods>(_activeVariableMods.Intersect(
                        info.PotentialVarMods));

                    // If unable to explain the next transition with the existing peptide, but the
                    // transition has the same precursor m/z as the last, try completing the existing
                    // peptide, and see if the current precursor can be completed as a new peptide
                    if (intersectVariableMods.Count == 0 && _activePrecursorMz == info.PrecursorMz)
                    {
                        CompletePeptide(false);
                        intersectVariableMods = new List<ExplicitMods>(info.PotentialVarMods);
                        foreach (var infoActive in _activeTransitionInfos)
                        {
                            intersectVariableMods = new List<ExplicitMods>(intersectVariableMods.Intersect(
                                infoActive.PotentialVarMods));
                        }
                        
                    }

                    if (intersectVariableMods.Count > 0)
                    {
                        _activeVariableMods = intersectVariableMods;
                    }
                    else if (_activePrecursorMz == info.PrecursorMz)
                    {
                        var precursorMz = Math.Round(info.PrecursorMz, MassListImporter.MZ_ROUND_DIGITS);
                        var errorInfo = new TransitionImportErrorInfo(string.Format(Resources.PeptideGroupBuilder_AppendTransition_Failed_to_explain_all_transitions_for_0__m_z__1__with_a_single_set_of_modifications,
                                info.PeptideSequence, precursorMz),
                                                                        null,
                                                                        lineNum, lineText);
                        _peptideGroupErrorInfo.Add(errorInfo);
                        return;
                    }
                    else
                    {
                        CompletePeptide(true);
                    }
                }
            }
            if (_activePeptide == null)
            {
                int? begin = null;
                int? end = null;
                if (_activeFastaSeq != null)
                {
                    begin = _activeFastaSeq.Sequence.IndexOf(sequence, StringComparison.Ordinal);
                    if (begin == -1)
                    {
                        // CONSIDER: Use fasta sequence format code currently in SrmDocument to show formatted sequence.
                        throw new InvalidDataException(string.Format(ModelResources.PeptideGroupBuilder_AppendTransition_The_peptide__0__was_not_found_in_the_sequence__1__,
                                                       sequence, _activeFastaSeq.Name));
                    }
                    end = begin + sequence.Length;
                }
                _activePeptide = new Peptide(_activeFastaSeq, sequence, begin, end, _enzyme.CountCleavagePoints(sequence), info.TransitionExps[0].IsDecoy);
                _activeModifiedSequence = info.ModifiedSequence;
                _activePrecursorMz = info.PrecursorMz;
                _activeExplicitTransitionGroupValues = info.ExplicitTransitionGroupValues;
                _activeVariableMods = new List<ExplicitMods>(info.PotentialVarMods.Distinct());
                _activePrecursorExps = new List<PrecursorExp>(info.TransitionExps.Select(exp => exp.Precursor));
                _activeExplicitRetentionTimeInfo = explicitRT;
            }
            var intersectPrecursors = new List<PrecursorExp>(_activePrecursorExps.Intersect(
                info.TransitionExps.Select(exp => exp.Precursor)));
            if (intersectPrecursors.Count > 0)
            {
                _activePrecursorExps = intersectPrecursors;
            }
            else if (_activePrecursorMz == info.PrecursorMz)
            {
                var precursorMz = Math.Round(_activePrecursorMz, MassListImporter.MZ_ROUND_DIGITS);
                var errorInfo = new TransitionImportErrorInfo(string.Format(ModelResources.PeptideGroupBuilder_AppendTransition_Failed_to_explain_all_transitions_for_m_z__0___peptide__1___with_a_single_precursor,
                        precursorMz, info.PeptideSequence),
                                                                null,
                                                                lineNum, lineText);
                _peptideGroupErrorInfo.Add(errorInfo);
                return;
            }
            else
            {
                CompleteTransitionGroup();
            }
            if (_irtValue.HasValue && (irt == null || Math.Abs(_irtValue.Value - irt.Value) > DbIrtPeptide.IRT_MIN_DIFF))
            {
                var precursorMz = Math.Round(info.PrecursorMz, MassListImporter.MZ_ROUND_DIGITS);
                var errorInfo = new TransitionImportErrorInfo(string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                        info.PeptideSequence, precursorMz, _irtValue, irt),
                                                              null,
                                                              lineNum, lineText);
                _peptideGroupErrorInfo.Add(errorInfo);
                return;
            }
            if (_activePrecursorMz == 0)
            {
                _activePrecursorMz = info.PrecursorMz;
                _activeExplicitTransitionGroupValues = info.ExplicitTransitionGroupValues;
                _activePrecursorExps = new List<PrecursorExp>(info.TransitionExps.Select(exp => exp.Precursor));
            }
            _activeTransitionInfos.Add(info);

            if (libraryIntensity != null)
            {
                _activeLibraryIntensities.Add(new SpectrumPeaksInfo.MI { Intensity = (float)libraryIntensity.Value, Mz = productMz });    
            }
           
            _irtValue = irt;
        }


        /// <summary>
        /// If the bare peptide sequence has changed, definitely start a new peptide
        /// If the modified sequence has changed, this is more ambiguous, since
        /// Skyline may have just failed to parse the modified sequence.  Only start new
        /// peptide if modified sequences are parsed and different.
        /// </summary>
        /// <param name="info">List of transition explanations</param>
        /// <returns></returns>
        private bool IsPeptideChanged(ExTransitionInfo info)
        {
            if (info.ModifiedSequence == null && _activeModifiedSequence == null)
                return !Equals(info.PeptideSequence, _activePeptide.Sequence);
            return !Equals(info.ModifiedSequence, _activeModifiedSequence);
        }

        private void CompletePeptide(bool andTransitionGroup)
        {
            if (andTransitionGroup)
                CompleteTransitionGroup();

            _groupLibTriples.Sort(TransitionGroupLibraryIrtTriple.CompareTriples);
            var finalGroupLibTriples = FinalizeTransitionGroups(_groupLibTriples);
            var finalTransitionGroups = finalGroupLibTriples.Select(triple => triple.NodeGroup).ToArray();
            var docNode = new PeptideDocNode(_activePeptide, _settings, _activeVariableMods[0], null, _activeExplicitRetentionTimeInfo,
                finalTransitionGroups, false);
            var finalLibrarySpectra = new List<SpectrumMzInfo>();
            double? peptideIrt = GetPeptideIrt(finalGroupLibTriples);
            foreach (var groupLibTriple in finalGroupLibTriples)
            {
                if (groupLibTriple.SpectrumInfo == null)
                    continue;
                var sequence = groupLibTriple.NodeGroup.TransitionGroup.Peptide.Target;
                var mods = docNode.ExplicitMods;
                var calcPre = _settings.GetPrecursorCalc(groupLibTriple.SpectrumInfo.Label, mods);
                var modifiedSequenceWithIsotopes = calcPre.GetModifiedSequence(sequence, SequenceModFormatType.lib_precision, false);

                finalLibrarySpectra.Add(new SpectrumMzInfo
                {
                    SourceFile = _sourceFile ?? CLIPBOARD_FILENAME,
                    Key = new LibKey(modifiedSequenceWithIsotopes, groupLibTriple.NodeGroup.TransitionGroup.PrecursorAdduct),
                    Label = groupLibTriple.SpectrumInfo.Label,
                    PrecursorMz = groupLibTriple.SpectrumInfo.PrecursorMz,
                    IonMobility = groupLibTriple.SpectrumInfo.IonMobility,
                    SpectrumPeaks = groupLibTriple.SpectrumInfo.SpectrumPeaks,
                    RetentionTime = peptideIrt
                }); 
            }
            _librarySpectra.AddRange(finalLibrarySpectra);
            _peptides.Add(docNode);
            if (peptideIrt.HasValue)
            {
                _irtPeptides.Add(new MeasuredRetentionTime(docNode.ModifiedTarget, peptideIrt.Value, true));
            }
            _groupLibTriples.Clear();

            // Keep the same peptide, if the group is not being completed.
            // This is an attempt to explain a set of transitions with the same
            // peptide, but different variable modifications.
            if (andTransitionGroup)
                _activePeptide = null;
            else
            {
                // Not valid to keep the same actual peptide.  Need a copy.
                _activePeptide = new Peptide(_activePeptide.FastaSequence,
                                             _activePeptide.Sequence,
                                             _activePeptide.Begin,
                                             _activePeptide.End,
                                             _activePeptide.MissedCleavages,
                                             _groupLibTriples.Any(pair => pair.NodeGroup.IsDecoy));
            }
            _irtValue = null;
        }

        private static double? GetPeptideIrt(IEnumerable<TransitionGroupLibraryIrtTriple> groupTriples)
        {
            var groupTriplesNonNull = groupTriples.Where(triple => triple.Irt.HasValue).ToList();
            if (!groupTriplesNonNull.Any())
            {
                return null;
            }
            double weightedSum = groupTriplesNonNull.Select(triple => triple.Irt.Value).Sum();
            double norm = groupTriplesNonNull.Count;
            return weightedSum / norm;
        }

        private TransitionGroupLibraryIrtTriple[] FinalizeTransitionGroups(IList<TransitionGroupLibraryIrtTriple> groupTriples)
        {
            var finalTriples = new List<TransitionGroupLibraryIrtTriple>();
            foreach (var groupTriple in groupTriples)
            {
                int iGroup = finalTriples.Count - 1;
                if (iGroup == -1 || !Equals(finalTriples[iGroup].NodeGroup.TransitionGroup, groupTriple.NodeGroup.TransitionGroup))
                    finalTriples.Add(groupTriple);
                else
                {
                    // Check for consistent iRT values
                    double? irt1 = finalTriples[iGroup].Irt;
                    double? irt2 = groupTriple.Irt;
                    bool bothIrtsNull = (irt1 == null && irt2 == null);
                    if (!bothIrtsNull && (irt1 == null || irt2 == null))
                    {
                        for (int i = 0; i < groupTriple.NodeGroup.TransitionCount; ++i)
                        {
                            var precursorMz = Math.Round(groupTriple.PrecursorMz, MassListImporter.MZ_ROUND_DIGITS);
                            var errorInfo = new TransitionImportErrorInfo(string.Format(ModelResources.PeptideGroupBuilder_FinalizeTransitionGroups_Missing_iRT_value_for_peptide__0___precursor_m_z__1_,
                                    _activePeptide.Sequence, precursorMz),
                                                                            null, null, null);
                            _peptideGroupErrorInfo.Add(errorInfo);
                        }
                        continue;
                    }
                    else if (!bothIrtsNull && Math.Abs(irt1.Value - irt2.Value) > DbIrtPeptide.IRT_MIN_DIFF)
                    {
                        // Make sure iRT values are reported in a deterministic order for testing
                        if (irt1.Value > irt2.Value)
                            Helpers.Swap(ref irt1, ref irt2);
                        for (int i = 0; i < groupTriple.NodeGroup.TransitionCount; ++i)
                        {
                            var precursorMz = Math.Round(groupTriple.PrecursorMz, MassListImporter.MZ_ROUND_DIGITS);
                            var errorInfo = new TransitionImportErrorInfo(string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                                    _activePeptide.Sequence, precursorMz, irt1.Value, irt2.Value),
                                                                            null, null, null);
                            _peptideGroupErrorInfo.Add(errorInfo);
                        }
                        continue;
                    }

                    // Found repeated group, so merge transitions
                    var spectrumErrors = new List<TransitionImportErrorInfo>();
                    finalTriples[iGroup].SpectrumInfo = finalTriples[iGroup].SpectrumInfo == null ? groupTriple.SpectrumInfo
                                : finalTriples[iGroup].SpectrumInfo.CombineSpectrumInfo(groupTriple.SpectrumInfo, out spectrumErrors);
                    if (spectrumErrors.Any())
                    {
                        _peptideGroupErrorInfo.AddRange(spectrumErrors);
                        continue;
                    }
                    finalTriples[iGroup].NodeGroup = (TransitionGroupDocNode)finalTriples[iGroup].NodeGroup.AddAll(groupTriple.NodeGroup.Children);
                }
            }
            var groups = groupTriples.Select(pair => pair.NodeGroup).ToList();
            var finalGroups = finalTriples.Select(pair => pair.NodeGroup).ToList();
            // If anything changed, make sure transitions are sorted
            if (!ArrayUtil.ReferencesEqual(groups, finalGroups))
            {
                for (int i = 0; i < finalTriples.Count; i++)
                {
                    var nodeGroup = finalTriples[i].NodeGroup;
                    var arrayTran = CompleteTransitions(nodeGroup.Children.Cast<TransitionDocNode>());
                    finalTriples[i].NodeGroup = (TransitionGroupDocNode)nodeGroup.ChangeChildrenChecked(arrayTran);
                }
            }
            return finalTriples.ToArray();
        }

        private void CompleteTransitionGroup()
        {
            var precursorExp = GetBestPrecursorExp();
            var transitionGroup = new TransitionGroup(_activePeptide,
                                                      precursorExp.PrecursorAdduct,
                                                      precursorExp.LabelType,
                                                      false,
                                                      precursorExp.MassShift);
            var transitions = _activeTransitionInfos.ConvertAll(info =>
                {
                    var productExp = info.TransitionExps.Single(exp => Equals(precursorExp, exp.Precursor)).Product;
                    var annotations = string.IsNullOrEmpty(productExp.ExInfo.Note) ? Annotations.EMPTY : new Annotations(productExp.ExInfo.Note, null, 0);
                    var ionType = productExp.IonType;
                    Transition tran;
                    if (ionType == IonType.custom)
                    {
                        tran = new Transition(transitionGroup, productExp.Adduct, null, productExp.CustomIon);
                    }
                    else if (ionType == IonType.precursor && productExp.MassIndex.HasValue)
                    {
                        tran = new Transition(transitionGroup, productExp.MassIndex.Value, productExp.Adduct);
                    }
                    else
                    {
                        var ordinal = productExp.FragmentOrdinal;
                        int offset = Transition.OrdinalToOffset(ionType, ordinal, _activePeptide.Sequence.Length);
                        int? massShift = productExp.MassShift;
                        if (massShift == null && precursorExp.MassShift.HasValue)
                            massShift = 0;
                        tran = new Transition(transitionGroup, ionType, offset, 0, productExp.Adduct, massShift);
                    }
                    // m/z and library info calculated later
                    return new TransitionDocNode(tran, annotations, productExp.Losses, TypedMass.ZERO_MONO_MASSH, TransitionDocNode.TransitionQuantInfo.DEFAULT, productExp.ExInfo.ExplicitTransitionValues, null);
                });

            // In assay library import, most "explicit" values are actually library values (CONSIDER: at the moment only CE is not a spectral library value, but that should really change too)
            var isAssayLibraryImport = _activeLibraryIntensities.Any();
            var docNodeExplicitTransitionGroupValues = isAssayLibraryImport ?
                ExplicitTransitionGroupValues.EMPTY.ChangeCollisionEnergy(_activeExplicitTransitionGroupValues.CollisionEnergy) : // Keep just the explicit CE
                _activeExplicitTransitionGroupValues;
            var libraryIonMobilityHighEnergyOffset = // Blib holds this at precursor level, set if all fragments agree
                isAssayLibraryImport && transitions.Any() && transitions.TrueForAll(t => Equals(t.ExplicitValues.IonMobilityHighEnergyOffset, transitions.First().ExplicitValues.IonMobilityHighEnergyOffset))
                    ? transitions.First().ExplicitValues.IonMobilityHighEnergyOffset
                    : null;

            // m/z calculated later
            var newTransitionGroup = new TransitionGroupDocNode(transitionGroup, CompleteTransitions(transitions), docNodeExplicitTransitionGroupValues);
            var currentLibrarySpectrum = isAssayLibraryImport ? 
                new SpectrumMzInfo
                {
                    Key = new LibKey(_activePeptide.Sequence, precursorExp.PrecursorAdduct),
                    PrecursorMz = _activePrecursorMz,
                    IonMobility = IonMobilityAndCCS.GetIonMobilityAndCCS(_activeExplicitTransitionGroupValues.IonMobility, _activeExplicitTransitionGroupValues.IonMobilityUnits, 
                        _activeExplicitTransitionGroupValues.CollisionalCrossSectionSqA, libraryIonMobilityHighEnergyOffset), 
                    Label = precursorExp.LabelType,
                    SpectrumPeaks = new SpectrumPeaksInfo(_activeLibraryIntensities.ToArray()),
                }
                : null;
            _groupLibTriples.Add(new TransitionGroupLibraryIrtTriple(currentLibrarySpectrum, newTransitionGroup, _irtValue, _activePrecursorMz));
            _activePrecursorMz = 0;
            _activeExplicitTransitionGroupValues = ExplicitTransitionGroupValues.EMPTY;
            _activePrecursorExps.Clear();
            _activeTransitionInfos.Clear();
            _activeLibraryIntensities.Clear();
            _irtValue = null;
        }

        private PrecursorExp GetBestPrecursorExp()
        {
            // If there is only one precursor explanation, return it
            if (_activePrecursorExps.Count == 1)
                return _activePrecursorExps[0];
            // Unless the explanation comes from just one transition, then look for most reasonable given settings
            int[] fragmentTypeCounts = new int[_activePrecursorExps.Count];
            var preferredFragments = new List<IonType>();
            foreach (var ionType in _settings.TransitionSettings.Filter.PeptideIonTypes)
            {
                if (preferredFragments.Contains(ionType))
                    continue;
                preferredFragments.Add(ionType);
                // Add ion type pairs together, whether they are both in the settings or not
                switch (ionType)
                {
                    case IonType.a: preferredFragments.Add(IonType.x); break;
                    case IonType.b: preferredFragments.Add(IonType.y); break;
                    case IonType.c: preferredFragments.Add(IonType.z); break;
                    case IonType.x: preferredFragments.Add(IonType.a); break;
                    case IonType.y: preferredFragments.Add(IonType.b); break;
                    case IonType.z: preferredFragments.Add(IonType.c); break;
                }
            }
            // Count transitions with the preferred types for all possible precursors
            foreach (var tranExp in _activeTransitionInfos.SelectMany(info => info.TransitionExps))
            {
                int i = _activePrecursorExps.IndexOf(tranExp.Precursor);
                if (i == -1)
                    continue;
                if (preferredFragments.Contains(tranExp.Product.IonType))
                    fragmentTypeCounts[i]++;
            }
            // Return the precursor with the most fragments of the preferred type
            var maxExps = fragmentTypeCounts.Max();
            return _activePrecursorExps[fragmentTypeCounts.IndexOf(c => c == maxExps)];
        }

        /// <summary>
        /// Remove duplicates and sort a set of transitions.
        /// </summary>
        /// <param name="transitions">The set of transitions</param>
        /// <returns>An array of sorted, distinct transitions</returns>
        private static TransitionDocNode[] CompleteTransitions(IEnumerable<TransitionDocNode> transitions)
        {
            var arrayTran = transitions.Distinct().ToArray();
            Array.Sort(arrayTran, TransitionGroup.CompareTransitions);
            return arrayTran;
        }

        public PeptideGroupDocNode ToDocNode()
        {
            PeptideGroupDocNode nodePepGroup;
            SrmSettingsDiff diff = SrmSettingsDiff.ALL;
            if (PeptideList)
            {
                if (_activePeptide != null)
                {
                    CompletePeptide(true);
                    diff = SrmSettingsDiff.PROPS;
                }
                nodePepGroup = new PeptideGroupDocNode(_activeFastaSeq ?? new PeptideGroup(_peptides.Any(p => p.IsDecoy)),
                    Name, Description, _peptides.ToArray());
            }
            else if (_customName) // name travels in the PeptideGroupDocNode instead of the FastaSequence
            {
                nodePepGroup = new PeptideGroupDocNode(
                    new FastaSequence(null, null, Alternatives, _sequence.ToString()),
                    Name, Description, new PeptideDocNode[0]);
            }
            else  // name travels with the FastaSequence
            {
                nodePepGroup = new PeptideGroupDocNode(
                    new FastaSequence(Name, Description, Alternatives, _sequence.ToString()),
                    null, null, new PeptideDocNode[0]);
            }
            // If this is a fasta file with no explicitly modified peptides, then apply
            // the usual peptide filtering rules.  Otherwise, keep all peptides the user input.
            if (!_autoManageChildren)
                nodePepGroup = (PeptideGroupDocNode) nodePepGroup.ChangeAutoManageChildren(false);
            // Materialize children, so that we have accurate accounting of
            // peptide and transition counts.
            nodePepGroup = EnsureIrts(nodePepGroup.ChangeSettings(_settings, diff), diff);

            List<DocNode> newChildren = new List<DocNode>(nodePepGroup.Children.Count);
            foreach (PeptideDocNode nodePep in nodePepGroup.Children)
            {
                var nodePepAdd = nodePep;
                if (_charges.TryGetValue(nodePep.Id.GlobalIndex, out var charge))
                {
                    var settingsCharge = _settings.ChangeTransitionFilter(f => f.ChangePeptidePrecursorCharges(new[] {charge}));
                    nodePepAdd = (PeptideDocNode) nodePep.ChangeSettings(settingsCharge, diff)
                                                         .ChangeAutoManageChildren(false);
                }
                newChildren.Add(nodePepAdd);
            }
            return (PeptideGroupDocNode) nodePepGroup.ChangeChildren(newChildren);
        }

        private PeptideGroupDocNode EnsureIrts(PeptideGroupDocNode nodePepGroup, SrmSettingsDiff diff)
        {
            if (_irtTargets == null || _settings.TransitionSettings.Libraries.MinIonCount <= IRT_MIN_ION_COUNT ||
                nodePepGroup.PeptideCount == 0 || nodePepGroup.PeptideCount == _irtTargets.Count ||
                nodePepGroup.Peptides.Any(nodePep => !_irtTargets.ContainsKey(nodePep.ModifiedTarget)))
            {
                return nodePepGroup;
            }

            // Check if lowering the minimum ion count results in more peptides
            var nodePepGroupPermissive =
                nodePepGroup.ChangeSettings(_settings.ChangeTransitionLibraries(libs => libs.ChangeMinIonCount(IRT_MIN_ION_COUNT)), diff);
            return nodePepGroupPermissive.PeptideCount > nodePepGroup.PeptideCount
                ? (PeptideGroupDocNode)nodePepGroupPermissive.ChangeAutoManageChildren(false)
                : nodePepGroup;
        }
    }

    public class TransitionImportErrorInfo : IEquatable<TransitionImportErrorInfo>
    {
        public long? LineNum { get; private set; }
        public int? Column { get; private set; }
        public string ErrorMessage { get; private set; }
        public string LineText { get; private set; }

        public TransitionImportErrorInfo(LineColNumberedIoException e)
        {
            ErrorMessage = e.PlainMessage;
            LineText = string.Empty;
            Column = e.ColumnIndex;
            LineNum = e.LineNumber;
        }

        public TransitionImportErrorInfo(string errorMessage, int? columnIndex, long? lineNum, string lineText)
        {
            ErrorMessage = errorMessage;
            LineText = lineText;
            Column = columnIndex + 1;   // 1 based column number for reporting to a user
            LineNum = lineNum;
        }

        public bool Equals(TransitionImportErrorInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return LineNum == other.LineNum && Column == other.Column && ErrorMessage == other.ErrorMessage && LineText == other.LineText;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TransitionImportErrorInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = LineNum.GetHashCode();
                hashCode = (hashCode * 397) ^ Column.GetHashCode();
                hashCode = (hashCode * 397) ^ (ErrorMessage != null ? ErrorMessage.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (LineText != null ? LineText.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    class TransitionGroupLibraryIrtTriple
    {
        public SpectrumMzInfo SpectrumInfo { get; set; }
        public TransitionGroupDocNode NodeGroup { get; set; }
        public double? Irt { get; set; }
        public double PrecursorMz { get; set; }

        public TransitionGroupLibraryIrtTriple(SpectrumMzInfo spectrumInfo, TransitionGroupDocNode nodeGroup, double? irt, double precursorMz)
        {
            SpectrumInfo = spectrumInfo;
            NodeGroup = nodeGroup;
            Irt = irt;
            PrecursorMz = precursorMz;
        }

        public static int CompareTriples(TransitionGroupLibraryIrtTriple p1, TransitionGroupLibraryIrtTriple p2)
        {
            int groupComparison = Peptide.CompareGroups(p1.NodeGroup, p2.NodeGroup);
            if (groupComparison != 0)
                return groupComparison;
            if (!p1.Irt.HasValue)
                return p2.Irt.HasValue ? -1 : 0;
            if (!p2.Irt.HasValue)
                return 1;
            return p1.Irt.Value.CompareTo(p2.Irt.Value);
        }
    }

    public static class FastaData
    {
        private static string EMPTY_PROTEIN_SEQUENCE = @"EMPTY";
        private static void AppendSequence(StringBuilder sequence, string line)
        {
            var seq = FastaSequence.StripModifications(line);
            // Get rid of whitespace
            seq = seq.Replace(@" ", string.Empty).Trim();
            // Get rid of end of sequence indicator
            if (seq.EndsWith(@"*"))
                seq = seq.Substring(0, seq.Length - 1);
            sequence.Append(seq);
        }

        public static bool IsValidFastaChar(char c)
        {
            return c >= 0x20 && c <= 0x7E ||
                   c == '\t' || c == 0x01;
        }

        public static IEnumerable<FastaSequence> ParseFastaFile(TextReader reader, bool readNamesOnly = false)
        {
            string line;
            string fastaDescriptionLine = string.Empty;
            StringBuilder sequence = new StringBuilder();
            int lineNum = 0;

            while ((line = reader.ReadLine()) != null)
            {
                ++lineNum;
                for (int i=0; i < line.Length; ++i)
                    if (!IsValidFastaChar(line[i]))
                        throw new InvalidDataException(string.Format(
                            ModelResources.FastaData_ParseFastaFile_Error_on_line__0___invalid_non_ASCII_character___1___at_position__2___are_you_sure_this_is_a_FASTA_file_,
                            lineNum, line[i], i));
                    
                if (line.StartsWith(PeptideGroupBuilder.PROTEIN_SPEC_PREFIX))
                {
                    if (!string.IsNullOrEmpty(fastaDescriptionLine))
                    {
                        yield return MakeFastaSequence(fastaDescriptionLine, sequence.ToString());
                        sequence.Clear();
                    }
                    fastaDescriptionLine = line;
                }
                else if (!readNamesOnly)
                {
                    AppendSequence(sequence, line);
                }
            }

            // Add the last fasta sequence
            if (!string.IsNullOrEmpty(fastaDescriptionLine))
                yield return MakeFastaSequence(fastaDescriptionLine, sequence.ToString());
        }

        public static FastaSequence MakeFastaSequence(string fastaDescriptionLine, string sequence)
        {
            int start = fastaDescriptionLine.StartsWith(PeptideGroupBuilder.PROTEIN_SPEC_PREFIX) ? 1 : 0;
            int split = IndexEndId(fastaDescriptionLine);
            string name;
            string description;
            ImmutableList<ProteinMetadata> alternatives;
            if (split < 0)
            {
                name = fastaDescriptionLine.Substring(start);
                description = string.Empty;
                alternatives = ImmutableList<ProteinMetadata>.EMPTY;
            }
            else
            {
                name = fastaDescriptionLine.Substring(start, split - start);
                string[] descriptions = fastaDescriptionLine.Substring(split + 1).Split((char)1);
                description = descriptions[0];
                var listAlternatives = new List<ProteinMetadata>();
                for (int i = 1; i < descriptions.Length; i++)
                {
                    string alternative = descriptions[i];
                    split = IndexEndId(alternative);
                    if (split == -1)
                        listAlternatives.Add(new ProteinMetadata(alternative, null));
                    else
                    {
                        listAlternatives.Add(new ProteinMetadata(alternative.Substring(0, split),
                            alternative.Substring(split + 1)));
                    }
                }
                alternatives = ImmutableList.ValueOf(listAlternatives);
            }

            if (string.IsNullOrEmpty(sequence))
            {
                sequence = EMPTY_PROTEIN_SEQUENCE;
            }
            return new FastaSequence(name, string.IsNullOrEmpty(description) ? null : description, alternatives, sequence);
        }

        private static int IndexEndId(string line)
        {
            return line.IndexOfAny(new[] { TextUtil.SEPARATOR_SPACE, TextUtil.SEPARATOR_TSV });
        }
    }
}
