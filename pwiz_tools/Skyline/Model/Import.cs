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
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Array = System.Array;

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

        public SrmDocument Document { get; private set; }
        public bool PeptideList { get; private set; }
        public int EmptyPeptideGroupCount { get; private set; }

        public IEnumerable<PeptideGroupDocNode> Import(TextReader reader, IProgressMonitor progressMonitor, long lineCount)
        {
            // Set starting values for limit counters
            _countPeptides = Document.PeptideCount;
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
            PeptideGroupBuilder seqBuilder = null;

            long linesRead = 0;
            int progressPercent = -1;

            string line;
            var status = new ProgressStatus(string.Empty);
            while ((line = reader.ReadLine()) != null)
            {
                linesRead++;
                if (progressMonitor != null)
                {
                    // TODO when changing from ILongWaitBroker to IProgressMonitor, the old code was:
                    // if (progressMonitor.IsCanceled || progressMonitor.IsDocumentChanged(Document))
                    // IProgressMonitor does not have IsDocumentChangesd.
                    if (progressMonitor.IsCanceled)
                        return new PeptideGroupDocNode[0];
                    int progressNew = (int) (linesRead*100/lineCount);
                    if (progressPercent != progressNew)
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(progressPercent = progressNew));
                }

                if (line.StartsWith(">")) // Not L10N
                {
                    if (_countIons > SrmDocument.MAX_TRANSITION_COUNT ||
                            _countPeptides > SrmDocument.MAX_PEPTIDE_COUNT)
                        throw new InvalidDataException(Resources.FastaImporter_Import_Document_size_limit_exceeded);

                    if (seqBuilder != null)
                        AddPeptideGroup(peptideGroupsNew, set, seqBuilder);

                    seqBuilder = _modMatcher == null
                        ? new PeptideGroupBuilder(line, PeptideList, Document.Settings)
                        : new PeptideGroupBuilder(line, _modMatcher, Document.Settings);
                    if (progressMonitor != null)
                        progressMonitor.UpdateProgress(status = status.ChangeMessage(string.Format(Resources.FastaImporter_Import_Adding_protein__0__, seqBuilder.Name)));
                }
                else if (seqBuilder == null)
                {
                    if (line.Trim().Length == 0)
                        continue;
                    break;
                }
                else
                {
                    seqBuilder.AppendSequence(line);
                }
            }
            // Add last sequence.
            if (seqBuilder != null)
                AddPeptideGroup(peptideGroupsNew, set, seqBuilder);
            return peptideGroupsNew;
        }

        private void AddPeptideGroup(List<PeptideGroupDocNode> listGroups,
            ICollection<FastaSequence> set, PeptideGroupBuilder builder)
        {
            PeptideGroupDocNode nodeGroup = builder.ToDocNode();
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
                        var nonEmptyGroups = listGroups.Where(g => g.MoleculeCount > 0).ToArray();
                        listGroups.Clear();
                        listGroups.AddRange(nonEmptyGroups);

                    }
                    return;
                }
            }
            listGroups.Add(nodeGroup);
            _countPeptides += nodeGroup.MoleculeCount;
            _countIons += nodeGroup.TransitionCount;
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
                    throw new LineColNumberedIoException(Resources.FastaImporter_ToFasta_Too_few_columns_found, lineNum, -1);
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
                        Resources.FastaImporter_ToFasta_Last_column_does_not_contain_a_valid_protein_sequence, lineNum,
                        fastaCol);
                sb.Append(">").Append(columns[0].Trim().Replace(" ", "_")); // ID // Not L10N
                for (int i = 1; i < fastaCol; i++)
                    sb.Append(" ").Append(columns[i].Trim()); // Description // Not L10N                    
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
        private readonly string _inputText;
        private IList<string> _lines; 

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

        public IList<string> ReadLines()
        {
            return _lines ?? (_lines = _inputFilename != null ? ReadLinesFromFile() : ReadLinesFromText());
        }

        private IList<string> ReadLinesFromFile()
        {
            var inputLines = File.ReadAllLines(_inputFilename);
            inputLines = inputLines.Where(line => line.Trim().Length > 0).ToArray();
            if (inputLines.Length == 0)
                throw new InvalidDataException(Resources.MassListImporter_Import_Empty_transition_list);
            InitFormat(inputLines);
            return inputLines;
        }

        private IList<string> ReadLinesFromText()
        {
            var inputLines = new List<string>();
            using (var readerLines = new StringReader(_inputText))
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
            InitFormat(inputLines);
            return inputLines;
        }

        private void InitFormat(IList<string> inputLines)
        {
            if (FormatProvider == null)
            {
                char sep;
                IFormatProvider provider;
                Type[] columnTypes;
                if (!MassListImporter.IsColumnar(inputLines[0], out provider, out sep, out columnTypes))
                    throw new IOException(Resources.SkylineWindow_importMassListMenuItem_Click_Data_columns_not_found_in_first_line);
                FormatProvider = provider;
                Separator = sep;
            }
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

        private MassListRowReader _rowReader;

        public MassListImporter(SrmDocument document, MassListInputs inputs)
        {
            Document = document;
            Inputs = inputs;
        }

        public SrmDocument Document { get; private set; }
        public SrmSettings Settings { get { return Document.Settings; } }
        public MassListInputs Inputs { get; private set; }
        public IFormatProvider FormatProvider { get { return Inputs.FormatProvider; } }
        public char Separator { get { return Inputs.Separator; } }

        public PeptideModifications GetModifications(SrmDocument document)
        {
            return _rowReader != null ? _rowReader.GetModifications(document) : document.Settings.PeptideSettings.Modifications;
        }

        public IEnumerable<PeptideGroupDocNode> Import(IProgressMonitor progressMonitor,
                                                       out List<MeasuredRetentionTime> irtPeptides,
                                                       out List<SpectrumMzInfo> librarySpectra,
                                                       out List<TransitionImportErrorInfo> errorList)
        {
            // Make sure all existing group names in the document are represented, and
            // existing FASTA sequences are used.
            var dictNameSeqAll = new Dictionary<string, FastaSequence>();
            // This caused problems
//            foreach (PeptideGroupDocNode nodePepGroup in Document.Children)
//            {
//                if (!dictNameSeqAll.ContainsKey(nodePepGroup.Name))
//                    dictNameSeqAll.Add(nodePepGroup.Name, nodePepGroup.PeptideGroup as FastaSequence);
//            }

            try
            {
                return Import(progressMonitor, null, dictNameSeqAll, out irtPeptides, out librarySpectra, out errorList);
            }
            catch (LineColNumberedIoException x)
            {
                throw new InvalidDataException(x.Message, x);
            }
        }

        public IEnumerable<PeptideGroupDocNode> Import(IProgressMonitor progressMonitor,
                                                       ColumnIndices indices,
                                                       IDictionary<string, FastaSequence> dictNameSeq)
        {
            List<MeasuredRetentionTime> irtPeptides;
            List<SpectrumMzInfo> librarySpectra;
            List<TransitionImportErrorInfo> errorList;
            return Import(progressMonitor, indices, dictNameSeq, out irtPeptides, out librarySpectra, out errorList);
        }

        public IEnumerable<PeptideGroupDocNode> Import(IProgressMonitor progressMonitor,
                                                       ColumnIndices indices,
                                                       IDictionary<string, FastaSequence> dictNameSeq,
                                                       out List<MeasuredRetentionTime> irtPeptides,
                                                       out List<SpectrumMzInfo> librarySpectra,
                                                       out List<TransitionImportErrorInfo> errorList)
        {
            irtPeptides = new List<MeasuredRetentionTime>();
            librarySpectra = new List<SpectrumMzInfo>();
            errorList = new List<TransitionImportErrorInfo>();
            var status = new ProgressStatus(string.Empty);
            // Get the lines used to guess the necessary columns and create the row reader
            if (progressMonitor != null)
            {
                if (progressMonitor.IsCanceled)
                    return new PeptideGroupDocNode[0];
                status = status.ChangeMessage(Resources.MassListImporter_Import_Reading_transition_list);
            }

            var lines = new List<string>(Inputs.ReadLines());

            if (progressMonitor != null)
            {
                if (progressMonitor.IsCanceled)
                    return new PeptideGroupDocNode[0];
                status = status.ChangeMessage(Resources.MassListImporter_Import_Inspecting_peptide_sequence_information);
            }
            if (indices != null)
            {
                _rowReader = new GeneralRowReader(FormatProvider, Separator, indices, Settings, lines);
            }
            else
            {
                // Check first line for validity
                var line = lines.FirstOrDefault();
                string[] fields = line.ParseDsvFields(Separator);
                string[] headers = fields.All(field => GetColumnType(field.Trim(), FormatProvider) != typeof (double))
                                       ? fields
                                       : null;
                int decoyColumn = -1;
                int irtColumn = -1;
                int libraryColumn = -1;
                var irtNames = new[] { "tr_recalibrated", "irt" }; // Not L10N
                var libraryNames = new[] { "libraryintensity", "relativeintensity", "relative_intensity", "relativefragmentintensity", "library_intensity" }; // Not L10N
                var decoyNames = new[] { "decoy" }; // Not L10N
                if (headers != null)
                {
                    lines.RemoveAt(0);
                    decoyColumn = headers.IndexOf(col => decoyNames.Contains(col.ToLowerInvariant()));
                    irtColumn = headers.IndexOf(col => irtNames.Contains(col.ToLowerInvariant()));
                    libraryColumn = headers.IndexOf(col => libraryNames.Contains(col.ToLowerInvariant()));
                    line = lines.FirstOrDefault();
                    fields = line != null ? line.ParseDsvFields(Separator) : new string[0];
                }
                if (fields.Length < 3)
                    throw new InvalidDataException(Resources.MassListImporter_Import_Invalid_transition_list_Transition_lists_must_contain_at_least_precursor_m_z_product_m_z_and_peptide_sequence);

                // If no numeric columns in the first row
                _rowReader = ExPeptideRowReader.Create(lines, decoyColumn, FormatProvider, Separator, Settings, irtColumn, libraryColumn);
                if (_rowReader == null)
                {
                    _rowReader = GeneralRowReader.Create(lines, headers, decoyColumn, FormatProvider, Separator, Settings, irtColumn, libraryColumn);
                    if (_rowReader == null && headers == null)
                    {
                        // Check for a possible header row
                        headers = lines[0].Split(Separator);
                        lines.RemoveAt(0);
                        _rowReader = GeneralRowReader.Create(lines, headers, decoyColumn, FormatProvider, Separator, Settings, irtColumn, libraryColumn);
                    }
                    if (_rowReader == null)
                        throw new LineColNumberedIoException(Resources.MassListImporter_Import_Failed_to_find_peptide_column, 1, -1);
                }
            }

            // Set starting values for limit counters
            _countPeptides = Document.PeptideCount;
            _countIons = Document.PeptideTransitionCount;

            List<PeptideGroupDocNode> peptideGroupsNew = new List<PeptideGroupDocNode>();
            PeptideGroupBuilder seqBuilder = null;

            // Process lines
            long lineIndex = 0;
            foreach (string row in lines)
            {
                lineIndex++;
                var errorInfo = _rowReader.NextRow(row, lineIndex);
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

                    int percentComplete = (int)(lineIndex * 100 / lines.Count);

                    if (status.PercentComplete != percentComplete)
                    {
                        string message = string.Format(Resources.MassListImporter_Import_Importing__0__,
                            _rowReader.TransitionInfo.ProteinName ?? _rowReader.TransitionInfo.PeptideSequence);
                        status = status.ChangePercentComplete(percentComplete).ChangeMessage(message);
                    }
                }

                seqBuilder = AddRow(seqBuilder, _rowReader, dictNameSeq, peptideGroupsNew, lineIndex, irtPeptides, librarySpectra, errorList);
            }

            // Add last sequence.
            if (seqBuilder != null)
                AddPeptideGroup(peptideGroupsNew, seqBuilder, irtPeptides, librarySpectra, errorList);

            return MergeEqualGroups(peptideGroupsNew);
        }

        private List<PeptideGroupDocNode> MergeEqualGroups(List<PeptideGroupDocNode> peptideGroups)
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
            foreach (var groupsToMerge in listKeys.Select(k => dictGroupsToMergeLists[k]))
            {
                if (groupsToMerge.Count == 1)
                    peptideGroupsNew.Add(groupsToMerge[0]);
                else
                {
                    var childrenNew = groupsToMerge.SelectMany(g => g.Children).ToArray();
                    if (childrenNew.Cast<PeptideDocNode>().All(nodePep => nodePep.Peptide.Begin.HasValue))
                    {
                        Array.Sort(childrenNew, (g1, g2) =>
                            Comparer<int>.Default.Compare(((PeptideDocNode)g1).Peptide.Begin ?? 0,
                                                          ((PeptideDocNode)g2).Peptide.Begin ?? 0));
                    }
                    var nodeGroupNew = (PeptideGroupDocNode) groupsToMerge[0].ChangeChildren(childrenNew);
                    peptideGroupsNew.Add(nodeGroupNew);
                }
            }
            return peptideGroupsNew;
        }

        private PeptideGroupBuilder AddRow(PeptideGroupBuilder seqBuilder,
                                           MassListRowReader rowReader,
                                           IDictionary<string, FastaSequence> dictNameSeq,
                                           ICollection<PeptideGroupDocNode> peptideGroupsNew,
                                           long lineNum,
                                           List<MeasuredRetentionTime> irtPeptides,
                                           List<SpectrumMzInfo> librarySpectra,
                                           List<TransitionImportErrorInfo> errorList)
        {
            var info = rowReader.TransitionInfo;
            var irt = rowReader.Irt;
            var libraryIntensity = rowReader.LibraryIntensity;
            var productMz = rowReader.ProductMz;
            if (irt == null && rowReader.IrtColumn != -1)
            {
                var error = new TransitionImportErrorInfo(string.Format(Resources.MassListImporter_AddRow_Invalid_iRT_value_at_precusor_m_z__0__for_peptide__1_, 
                                                                        rowReader.TransitionInfo.PrecursorMz, 
                                                                        rowReader.TransitionInfo.ModifiedSequence),
                                                          rowReader.IrtColumn,
                                                          lineNum);
                errorList.Add(error);
                return seqBuilder;
            }
            if (libraryIntensity == null && rowReader.LibraryColumn != -1)
            {
                var error = new TransitionImportErrorInfo(string.Format(Resources.MassListImporter_AddRow_Invalid_library_intensity_at_precursor__0__for_peptide__1_, 
                                                                        rowReader.TransitionInfo.PrecursorMz, 
                                                                        rowReader.TransitionInfo.ModifiedSequence),
                                                          rowReader.LibraryColumn,
                                                          lineNum);
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
                    seqBuilder = new PeptideGroupBuilder(fastaSeq, Document.Settings);
                else
                {
                    string safeName = name != null ?
                        Helpers.GetUniqueName(name, dictNameSeq.Keys) :
                        Document.GetPeptideGroupId(true);
                    seqBuilder = new PeptideGroupBuilder(">>" + safeName, true, Document.Settings) {BaseName = name}; // Not L10N
                }
            }
            try
            {
                seqBuilder.AppendTransition(info, irt, libraryIntensity, productMz, lineNum);
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

        private abstract class MassListRowReader
        {
            protected MassListRowReader(IFormatProvider provider,
                                        char separator,
                                        ColumnIndices indices,
                                        SrmSettings settings,
                                        IEnumerable<string> sequences)
            {
                FormatProvider = provider;
                Separator = separator;
                Indices = indices;
                Settings = settings;
                ModMatcher = CreateModificationMatcher(settings, sequences);
                NodeDictionary = new Dictionary<string, PeptideDocNode>();
            }

            private static ModificationMatcher CreateModificationMatcher(SrmSettings settings,
                IEnumerable<string> sequences)
            {
                var modMatcher = new ModificationMatcher();
                // We want AutoSelect on so we can generate transition groups, but we want the filter to 
                // be lenient because we are only using this to match modifications, not generate the
                // final transition groups
                var settingsMatcher = settings.ChangeTransitionFilter(filter => filter.ChangeAutoSelect(true))
                                           .ChangeTransitionFullScan(fullscan => fullscan.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.None, null, null))
                                           .ChangeTransitionFilter(filter => filter.ChangePrecursorCharges(Enumerable.Range(TransitionGroup.MIN_PRECURSOR_CHARGE,
                                                                                                                            TransitionGroup.MAX_PRECURSOR_CHARGE).ToArray()));
                try
                {
                    modMatcher.CreateMatches(settingsMatcher,
                                             sequences != null ? sequences.Distinct() : new string[0],
                                             Properties.Settings.Default.StaticModList,
                                             Properties.Settings.Default.HeavyModList);
                }
                catch (FormatException)
                {
                    modMatcher.CreateMatches(settingsMatcher,
                                             new string[0],
                                             Properties.Settings.Default.StaticModList,
                                             Properties.Settings.Default.HeavyModList);
                }
                return modMatcher;
            }

            protected SrmSettings Settings { get; private set; }
            protected string[] Fields { get; private set; }
            private IFormatProvider FormatProvider { get; set; }
            private char Separator { get; set; }
            private ModificationMatcher ModMatcher { get; set; }
            private Dictionary<string, PeptideDocNode> NodeDictionary { get; set; } 
            private ColumnIndices Indices { get; set; }
            protected int ProteinColumn { get { return Indices.ProteinColumn; } }
            protected int PeptideColumn { get { return Indices.PeptideColumn; } }
            protected int LabelTypeColumn { get { return Indices.LabelTypeColumn; } }
            private int PrecursorColumn { get { return Indices.PrecursorColumn; } }
            protected double PrecursorMz { get { return ColumnMz(Fields, PrecursorColumn, FormatProvider); } }
            private int ProductColumn { get { return Indices.ProductColumn; } }
            public double ProductMz { get { return ColumnMz(Fields, ProductColumn, FormatProvider); } }
            private int DecoyColumn { get { return Indices.DecoyColumn; } }
            public int IrtColumn { get { return Indices.IrtColumn; } }
            public double? Irt { get { return ColumnDouble(Fields, IrtColumn, FormatProvider); } }
            public int LibraryColumn { get { return Indices.LibraryColumn; } }
            public double? LibraryIntensity { get { return ColumnDouble(Fields, LibraryColumn, FormatProvider); } }
            protected bool IsDecoy
            {
                get { return DecoyColumn != -1 && Equals(Fields[DecoyColumn].ToLowerInvariant(), "true"); } // Not L10N
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

                ExTransitionInfo info = CalcTransitionInfo(lineNum);

                if (!FastaSequence.IsExSequence(info.PeptideSequence))
                {
                    return new TransitionImportErrorInfo(string.Format(Resources.MassListRowReader_NextRow_Invalid_peptide_sequence__0__found,
                                                                       info.PeptideSequence), 
                                                         PeptideColumn,
                                                         lineNum);
                }
                if (!info.DefaultLabelType.IsLight && !IsHeavyTypeAllowed(info.DefaultLabelType))
                {
                    return new TransitionImportErrorInfo(TextUtil.SpaceSeparate(Resources.MassListRowReader_NextRow_Isotope_labeled_entry_found_without_matching_settings_,
                                                                                Resources.MassListRowReader_NextRow_Check_the_Modifications_tab_in_Transition_Settings),
                                                         LabelTypeColumn,
                                                         lineNum);
                }

                TransitionImportErrorInfo errorInfo;
                info = CalcPrecursorExplanations(info, lineNum, out errorInfo);
                if (errorInfo != null)
                {
                    return errorInfo;
                }

                TransitionInfo = CalcTransitionExplanations(info, lineNum, out errorInfo);
                return errorInfo;

            }

            protected abstract ExTransitionInfo CalcTransitionInfo(long lineNum);

            private ExTransitionInfo CalcPrecursorExplanations(ExTransitionInfo info, long lineNum, out TransitionImportErrorInfo errorInfo)
            {
                // Enumerate all possible variable modifications looking for an explanation
                // for the precursor information
                errorInfo = null;
                double precursorMz = info.PrecursorMz;
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
                    double precursorMassH = Settings.GetPrecursorMass(defaultLabelType, info.PeptideSequence, variableMods);
                    int precursorMassShift;
                    int nearestCharge;
                    int? precursorCharge = CalcPrecursorCharge(precursorMassH, precursorMz, MzMatchTolerance, !nodePep.IsProteomic,
                                                              info.IsDecoy, out precursorMassShift, out nearestCharge);
                    if (precursorCharge.HasValue)
                    {
                        info.TransitionExps.Add(new TransitionExp(variableMods, precursorCharge.Value, defaultLabelType,
                                                                  precursorMassShift));
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
                        precursorMassH = Settings.GetPrecursorMass(labelType, info.PeptideSequence, variableMods);
                        precursorCharge = CalcPrecursorCharge(precursorMassH, precursorMz, MzMatchTolerance, !nodePep.IsProteomic,
                                                              info.IsDecoy, out precursorMassShift, out nearestCharge);
                        if (precursorCharge.HasValue)
                        {
                            info.TransitionExps.Add(new TransitionExp(variableMods, precursorCharge.Value, labelType,
                                                                      precursorMassShift));
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
                                                              lineNum);
                    
                }
                else if (!Settings.TransitionSettings.Instrument.IsMeasurable(precursorMz))
                {
                    precursorMz = Math.Round(SequenceMassCalc.PersistentMZ(precursorMz), MZ_ROUND_DIGITS);
                    errorInfo = new TransitionImportErrorInfo(TextUtil.SpaceSeparate(string.Format(Resources.MassListRowReader_CalcPrecursorExplanations_The_precursor_m_z__0__of_the_peptide__1__is_out_of_range_for_the_instrument_settings_,
                                                                                                  precursorMz, info.PeptideSequence),
                                                                                    Resources.MassListRowReader_CalcPrecursorExplanations_Check_the_Instrument_tab_in_the_Transition_Settings),
                                                              PrecursorColumn,
                                                              lineNum);
                }
                // If it's within the instrument settings but not measurable, problem must be in the isolation scheme
                else if (!Settings.TransitionSettings.IsMeasurablePrecursor(precursorMz))
                {
                    precursorMz = Math.Round(SequenceMassCalc.PersistentMZ(precursorMz), MZ_ROUND_DIGITS);
                    errorInfo = new TransitionImportErrorInfo(TextUtil.SpaceSeparate(string.Format(Resources.MassListRowReader_CalcPrecursorExplanations_The_precursor_m_z__0__of_the_peptide__1__is_outside_the_range_covered_by_the_DIA_isolation_scheme_,
                                                                                                  precursorMz, info.PeptideSequence),
                                                                                    Resources.MassListRowReader_CalcPrecursorExplanations_Check_the_isolation_scheme_in_the_full_scan_settings_),
                                                              PrecursorColumn,
                                                              lineNum);
                }

                return info;
            }

            private static double NearestMz(double precursorMz, double nearestMz, double precursorMassH, int precursorCharge)
            {
                double newMz = SequenceMassCalc.GetMZ(precursorMassH, precursorCharge);
                return Math.Abs(precursorMz - newMz) < Math.Abs(precursorMz - nearestMz)
                            ? newMz
                            : nearestMz;
            }

            private static int? CalcPrecursorCharge(double precursorMassH,
                                                   double precursorMz,
                                                   double tolerance,
                                                   bool isCustomIon,
                                                   bool isDecoy,
                                                   out int massShift,
                                                   out int nearestCharge)
            {
                return TransitionCalc.CalcPrecursorCharge(precursorMassH, precursorMz, tolerance, isCustomIon, isDecoy, out massShift, out nearestCharge);
            }

            private ExTransitionInfo CalcTransitionExplanations(ExTransitionInfo info, long lineNum, out TransitionImportErrorInfo errorInfo)
            {
                errorInfo = null;
                string sequence = info.PeptideSequence;
                double productMz = ProductMz;

                foreach (var transitionExp in info.TransitionExps.ToArray())
                {
                    var mods = transitionExp.Precursor.VariableMods;
                    var calc = Settings.GetFragmentCalc(transitionExp.Precursor.LabelType, mods);
                    double productPrecursorMass = calc.GetPrecursorFragmentMass(sequence);
                    double[,] productMasses = calc.GetFragmentIonMasses(sequence);
                    var potentialLosses = TransitionGroup.CalcPotentialLosses(sequence,
                        Settings.PeptideSettings.Modifications, mods, calc.MassType);

                    IonType? ionType;
                    int? ordinal;
                    TransitionLosses losses;
                    int massShift;
                    int productCharge = TransitionCalc.CalcProductCharge(productPrecursorMass,
                                                                         transitionExp.Precursor.PrecursorCharge,
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

                    if (productCharge > 0 && ionType.HasValue && ordinal.HasValue)
                    {
                        transitionExp.Product = new ProductExp(productCharge, ionType.Value, ordinal.Value, losses, massShift);
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
                                                              lineNum);
                }
                else if (!Settings.TransitionSettings.Instrument.IsMeasurable(productMz))
                {
                    productMz = Math.Round(productMz, MZ_ROUND_DIGITS);
                    errorInfo = new TransitionImportErrorInfo(TextUtil.SpaceSeparate(string.Format(Resources.MassListRowReader_CalcTransitionExplanations_The_product_m_z__0__is_out_of_range_for_the_instrument_settings__in_the_peptide_sequence__1_,
                                                                                        productMz, info.PeptideSequence),
                                                                                    Resources.MassListRowReader_CalcPrecursorExplanations_Check_the_Instrument_tab_in_the_Transition_Settings),
                                                              ProductColumn,
                                                              lineNum);
                }

                return info;
            }

            private static double ColumnMz(string[] fields, int column, IFormatProvider provider)
            {
                double result;
                if (double.TryParse(fields[column], NumberStyles.Number, provider, out result))
                    return result;
                return 0;   // Invalid m/z
            }

            private static double? ColumnDouble(string[] fields, int column, IFormatProvider provider)
            {
                if (column == -1)
                    return null;
                try
                {
                    return double.Parse(fields[column], provider);
                }
                catch (FormatException)
                {
                    return null;   // Invalid double format
                }
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

                    double precursorMassH = calc.GetPrecursorMass(nodePep.Peptide.Sequence);
                    bool isDecoy = iDecoy != -1 && Equals(fields[iDecoy].ToLowerInvariant(), "true");   // Not L10N
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
                        int nearestCharge;
                        int? charge = CalcPrecursorCharge(precursorMassH, precursorMz, tolerance, !nodePep.IsProteomic, isDecoy, out massShift, out nearestCharge);
                        if (charge.HasValue)
                        {
                            indexPrec = i;
                            transitionExps.Add(new TransitionExp(mods, charge.Value, labelType, massShift));
                        }
                    }
                }
                return indexPrec;
            }

            protected static int FindProduct(string[] fields, string sequence, IEnumerable<TransitionExp> transitionExps,
                int iSequence, int iPrecursor, double tolerance, IFormatProvider provider, SrmSettings settings)
            {
                double maxProductMz = 0;
                int maxIndex = -1;
                foreach (var transitionExp in transitionExps)
                {
                    var mods = transitionExp.Precursor.VariableMods;
                    var calc = settings.GetFragmentCalc(transitionExp.Precursor.LabelType, mods);
                    double productPrecursorMass = calc.GetPrecursorFragmentMass(sequence);
                    double[,] productMasses = calc.GetFragmentIonMasses(sequence);
                    var potentialLosses = TransitionGroup.CalcPotentialLosses(sequence,
                        settings.PeptideSettings.Modifications, mods, calc.MassType);

                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (i == iSequence || i == iPrecursor)
                            continue;

                        double productMz = ColumnMz(fields, i, provider);
                        if (productMz == 0)
                            continue;

                        IonType? ionType;
                        int? ordinal;
                        TransitionLosses losses;
                        int massShift;
                        int charge = TransitionCalc.CalcProductCharge(productPrecursorMass,
                                                                      transitionExp.Precursor.PrecursorCharge,
                                                                      productMasses,
                                                                      potentialLosses,
                                                                      productMz,
                                                                      tolerance,
                                                                      calc.MassType,
                                                                      transitionExp.ProductShiftType,
                                                                      out ionType,
                                                                      out ordinal,
                                                                      out losses,
                                                                      out massShift);

                        // Look for the maximum product m/z, or this function may settle for a
                        // collision energy or retention time that matches a single amino acid
                        if (charge > 0 && productMz > maxProductMz)
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
                                     IEnumerable<string> lines)
                : base(provider, separator, indices, settings, GetSequencesFromLines(lines, separator, indices))
            {
            }

            private static IsotopeLabelType GetLabelType(string typeId)
            {
                return (Equals(typeId, "H") ? IsotopeLabelType.heavy : IsotopeLabelType.light); // Not L10N
            }

            protected override ExTransitionInfo CalcTransitionInfo(long lineNum)
            {
                string proteinName = null;
                if (ProteinColumn != -1)
                    proteinName = Fields[ProteinColumn];
                string peptideSequence = RemoveSequenceNotes(Fields[PeptideColumn]);
                string modifiedSequence = RemoveModifiedSequenceNotes(Fields[PeptideColumn]);
                var info = new ExTransitionInfo(proteinName, peptideSequence, modifiedSequence, PrecursorMz, IsDecoy);

                if (LabelTypeColumn != -1)
                {
                    info.DefaultLabelType = GetLabelType(Fields[LabelTypeColumn]);
                    info.IsExplicitLabelType = true;                    
                }

                return info;
            }

            private struct PrecursorCandidate
            {
                public PrecursorCandidate(int sequenceIndex, int precursorMzIdex, string sequence, IList<TransitionExp> transitionExps) : this()
                {
                    SequenceIndex = sequenceIndex;
                    PrecursorMzIdex = precursorMzIdex;
                    Sequence = sequence;
                    TransitionExps = transitionExps;
                }

                public int SequenceIndex { get; private set; }
                public int PrecursorMzIdex { get; private set; }
                public string Sequence { get; private set; }
                public IList<TransitionExp> TransitionExps { get; private set; } 
            }

            public static GeneralRowReader Create(IList<string> lines, IList<string> headers, int iDecoy,
                IFormatProvider provider, char separator, SrmSettings settings, int iirt, int iLibrary)
            {
                // Split the first line into fields.
                Assume.IsTrue(lines.Count > 0);
                // Look for sequence column
                string[] fieldsFirstRow = null;
                PrecursorCandidate[] sequenceCandidates = null;
                int bestCandidateIndex = -1;
                int iLabelType = -1;

                double tolerance = settings.TransitionSettings.Instrument.MzMatchTolerance;

                foreach (var line in lines)
                {
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
                            return null;

                        var listNewCandidates = new List<PrecursorCandidate>();
                        foreach (var candidateIndex in newSeqCandidates)
                        {
                            string sequence = RemoveSequenceNotes(fields[candidateIndex]);
                            string modifiedSequence = RemoveModifiedSequenceNotes(fields[candidateIndex]);
                            IsotopeLabelType labelType = IsotopeLabelType.light;
                            if (iLabelType != -1)
                                labelType = GetLabelType(fields[iLabelType]);
                            IList<TransitionExp> transitionExps;
                            int candidateMzIndex = FindPrecursor(fields, sequence, modifiedSequence, labelType, candidateIndex, iDecoy,
                                                       tolerance, provider, settings, out transitionExps);
                            // If no match, and no specific label type, then try heavy.
                            if (settings.PeptideSettings.Modifications.HasHeavyModifications &&
                                    candidateMzIndex == -1 && iLabelType == -1)
                            {
                                var peptideMods = settings.PeptideSettings.Modifications;
                                foreach (var typeMods in peptideMods.GetHeavyModifications())
                                {
                                    if (settings.TryGetPrecursorCalc(typeMods.LabelType, null) != null)
                                    {
                                        candidateMzIndex = FindPrecursor(fields, sequence, modifiedSequence, typeMods.LabelType, candidateIndex, iDecoy,
                                                                   tolerance, provider, settings, out transitionExps);
                                        if (candidateMzIndex != -1)
                                            break;
                                    }
                                }
                            }

                            if (candidateMzIndex != -1)
                                listNewCandidates.Add(new PrecursorCandidate(candidateIndex, candidateMzIndex, sequence, transitionExps));
                        }

                        if (listNewCandidates.Count == 0)
                            throw new MzMatchException(Resources.GeneralRowReader_Create_No_valid_precursor_m_z_column_found, 1, -1);
                        sequenceCandidates = listNewCandidates.ToArray();
                    }

                    bestCandidateIndex = FindBestCandidate(sequenceCandidates, fields);
                    // Break if a best candidate was found
                    if (bestCandidateIndex != -1)
                        break;
                }
                if (sequenceCandidates == null)
                    return null;

                if (bestCandidateIndex == -1)
                    bestCandidateIndex = 0;

                var prec = sequenceCandidates[bestCandidateIndex];
                int iSequence = prec.SequenceIndex;
                int iPrecursor = prec.PrecursorMzIdex;
                int iProduct = FindProduct(fieldsFirstRow, prec.Sequence, prec.TransitionExps, prec.SequenceIndex, prec.PrecursorMzIdex,
                    tolerance, provider, settings);
                if (iProduct == -1)
                    throw new MzMatchException(Resources.GeneralRowReader_Create_No_valid_product_m_z_column_found, 1, -1);

                int iProtein = FindProtein(fieldsFirstRow, iSequence, lines, headers, provider, separator);

                var indices = new ColumnIndices(iProtein, iSequence, iPrecursor, iProduct, iLabelType, iDecoy, iirt, iLibrary);

                return new GeneralRowReader(provider, separator, indices, settings, lines);
            }

            private static int[] FindSequenceCandidates(string[] fields)
            {
                var listCandidates = new List<int>();
                for (int i = 0; i < fields.Length; i++)
                {
                    var fieldUpper = fields[i].ToUpper(CultureInfo.InvariantCulture);
                    if ("TRUE" == fieldUpper || "FALSE" == fieldUpper) // Not L10N
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
                string seqClean = FastaSequence.StripModifications(seq);
                int dotIndex = seqClean.IndexOf('.'); // Not L10N
                if (dotIndex != -1 || (dotIndex = seqClean.IndexOf('_')) != -1)
                    seqClean = seqClean.Substring(0, dotIndex);
                seqClean = seqClean.TrimEnd('+');
                return seqClean;
            }

            private static string RemoveModifiedSequenceNotes(string seq)
            {
                // Find all occurances of . and _
                var dotIndices = new List<int>();
                for (int i = 0; i < seq.Length; ++i)
                {
                    if (seq[i] == '.' || seq[i] == '_')
                    {
                        dotIndices.Add(i);
                    }
                }
                var matches = FastaSequence.RGX_ALL.Matches(seq);
                foreach (Match match in matches)
                {
                    int start = match.Groups[0].Index;
                    int end = start + match.Groups[0].Length - 1;
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
                return FastaSequence.NormalizeNTerminalMod(seq);  // Make sure any n-terminal mod gets moved to after the first AA
            }

            private static readonly string[] EXCLUDE_PROTEIN_VALUES = { "true", "false", "heavy", "light", "unit" }; // Not L10N

            private static int FindProtein(string[] fields, int iSequence,
                IEnumerable<string> lines, IList<string> headers,
                IFormatProvider provider, char separator)
            {

                // First look for all columns that are non-numeric with more that 2 characters
                List<int> listDescriptive = new List<int>();
                for (int i = 0; i < fields.Length; i++)
                {
                    if (i == iSequence)
                        continue;

                    string fieldValue = fields[i];
                    double tempDouble;
                    if (!double.TryParse(fieldValue, NumberStyles.Number, provider, out tempDouble))
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
                        int count;
                        if (valueCounts[i].TryGetValue(string.Empty, out count) || valueCounts[i].Count > sequenceCounts.Count)
                            listDescriptive.RemoveAt(i);
                    }
                    // If more than one possible value, and there are headers, look for
                    // one with the word protein in it.
                    if (headers != null && listDescriptive.Count > 1)
                    {
                        foreach (int i in listDescriptive)
                        {
                            if (headers[i].ToLowerInvariant().Contains("protein")) // Not L10N : Since many transition list files are generated in English
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

            private static int FindLabelType(string[] fields, IEnumerable<string> lines, char separator)
            {
                // Look for the first column containing just L or H
                int iLabelType = -1;
                for (int i = 0; i < fields.Length; i++)
                {
                    if (Equals(fields[i], "H") || Equals(fields[i], "L")) // Not L10N
                    {
                        iLabelType = i;
                        break;
                    }
                }
                if (iLabelType == -1)
                    return -1;
                // Make sure all other rows have just L or H in this column
                foreach (string line in lines)
                {
                    string[] fieldsNext = line.ParseDsvFields(separator);
                    if (!Equals(fieldsNext[iLabelType], "H") && !Equals(fieldsNext[iLabelType], "L")) // Not L10N
                        return -1;
                }
                return iLabelType;
            }

            private static void AddCount(string key, IDictionary<string, int> dict)
            {
                int count;
                if (dict.TryGetValue(key, out count))
                    dict[key]++;
                else
                    dict.Add(key, 1);
            }

            private static IEnumerable<string> GetSequencesFromLines(IEnumerable<string> lines, char separator, ColumnIndices indices)
            {
                return lines.Select(line => RemoveModifiedSequenceNotes(line.ParseDsvFields(separator)[indices.PeptideColumn]));
            }
        }

        private class ExPeptideRowReader : MassListRowReader
        {
            // Protein.Peptide.+.Label
            private const string REGEX_PEPTIDE_FORMAT = @"^([^. ]+)\.([A-Z0-9_+\-\[\]]+)\..+\.(light|{0})$"; // Not L10N

            private ExPeptideRowReader(IFormatProvider provider,
                                       char separator,
                                       ColumnIndices indices,
                                       Regex exPeptideRegex,
                                       SrmSettings settings,
                                       IEnumerable<string> lines)
                : base(provider, separator, indices, settings, GetSequencesFromLines(lines, separator, indices, exPeptideRegex))
            {
                ExPeptideRegex = exPeptideRegex;
            }

            private Regex ExPeptideRegex { get; set; }

            protected override ExTransitionInfo CalcTransitionInfo(long lineNum)
            {
                string exPeptide = Fields[PeptideColumn];
                Match match = ExPeptideRegex.Match(exPeptide);
                if (!match.Success)
                    throw new LineColNumberedIoException(string.Format(Resources.ExPeptideRowReader_CalcTransitionInfo_Invalid_extended_peptide_format__0__, exPeptide), lineNum, PeptideColumn);

                try
                {
                    string proteinName = GetProteinName(match);
                    string peptideSequence = GetSequence(match);
                    string modifiedSequence = GetModifiedSequence(match);

                    var info = new ExTransitionInfo(proteinName, peptideSequence, modifiedSequence, PrecursorMz, IsDecoy)
                        {
                            DefaultLabelType = GetLabelType(match, Settings),
                            IsExplicitLabelType = true
                        };

                    return info;
                }
                catch (Exception)
                {
                    throw new LineColNumberedIoException(
                        string.Format(Resources.ExPeptideRowReader_CalcTransitionInfo_Invalid_extended_peptide_format__0__,
                                      exPeptide),
                        lineNum, PeptideColumn);
                }
            }

            public static ExPeptideRowReader Create(IList<string> lines, int iDecoy,
                IFormatProvider provider, char separator, SrmSettings settings, int iirt, int iLibrary)
            {
                // Split the first line into fields.
                Debug.Assert(lines.Count > 0);
                string[] fields = lines[0].ParseDsvFields(separator);

                // Create the ExPeptide regular expression
                var modSettings = settings.PeptideSettings.Modifications;
                var heavyTypeNames = from typedMods in modSettings.GetHeavyModifications()
                                     select typedMods.LabelType.Name;
                string exPeptideFormat = string.Format(REGEX_PEPTIDE_FORMAT, string.Join("|", heavyTypeNames.ToArray())); // Not L10N
                var exPeptideRegex = new Regex(exPeptideFormat);

                // Look for sequence column
                string sequence;
                string modifiedSequence;
                IsotopeLabelType labelType;
                int iExPeptide = FindExPeptide(fields, exPeptideRegex, settings,
                    out sequence, out modifiedSequence, out labelType);
                // If no sequence column found, return null.  After this,
                // all errors throw.
                if (iExPeptide == -1)
                    return null;

                if (!labelType.IsLight && !modSettings.HasHeavyImplicitModifications)
                {
                    var message = TextUtil.LineSeparate(Resources.ExPeptideRowReader_Create_Isotope_labeled_entry_found_without_matching_settings,
                                                        Resources.ExPeptideRowReaderCreateCheck_the_Modifications_tab_in_Transition_Settings);
                    throw new LineColNumberedIoException(message, 1, iExPeptide);
                }

                double tolerance = settings.TransitionSettings.Instrument.MzMatchTolerance;
                IList<TransitionExp> transitionExps;
                int iPrecursor = FindPrecursor(fields, sequence, modifiedSequence, labelType, iExPeptide, iDecoy,
                                               tolerance, provider, settings, out transitionExps);
                if (iPrecursor == -1)
                    throw new MzMatchException(Resources.GeneralRowReader_Create_No_valid_precursor_m_z_column_found, 1, -1);

                int iProduct = FindProduct(fields, sequence, transitionExps, iExPeptide, iPrecursor,
                    tolerance, provider, settings);
                if (iProduct == -1)
                    throw new MzMatchException(Resources.GeneralRowReader_Create_No_valid_product_m_z_column_found, 1, -1);

                var indices = new ColumnIndices(iExPeptide, iExPeptide, iPrecursor, iProduct, iExPeptide, iDecoy, iirt, iLibrary);
                return new ExPeptideRowReader(provider, separator, indices, exPeptideRegex, settings, lines);
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

        public static bool IsColumnar(string text,
            out IFormatProvider provider, out char sep, out Type[] columnTypes)
        {
            provider = CultureInfo.InvariantCulture;
            sep = '\0'; // Not L10N
            int endLine = text.IndexOf('\n'); // Not L10N 
            string line = (endLine != -1 ? text.Substring(0, endLine) : text);
            string localDecimalSep = LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string[] columns;
            if (TrySplitColumns(line, TextUtil.SEPARATOR_TSV, out columns)) 
            {
                // If the current culture's decimal separator is different from the
                // invariant culture, and their are more occurances of the current
                // culture's decimal separator in the line, then use current culture.
                string invDecimalSep = CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator;
                if (!Equals(localDecimalSep, invDecimalSep))
                {
                    if (CountDecimals(columns, LocalizationHelper.CurrentCulture) >
                            CountDecimals(columns, CultureInfo.InvariantCulture))
                        provider = LocalizationHelper.CurrentCulture;
                }
                sep = TextUtil.SEPARATOR_TSV;
            }
            // Excel CSVs for cultures with a comma decimal use semi-colons.
            else if (Equals(",", localDecimalSep) && TrySplitColumns(line, TextUtil.SEPARATOR_CSV_INTL, out columns)) // Not L10N
            {
                provider = LocalizationHelper.CurrentCulture;
                sep = TextUtil.SEPARATOR_CSV_INTL;           
            }
            else if (TrySplitColumns(line, TextUtil.SEPARATOR_CSV, out columns))
            {
                sep = TextUtil.SEPARATOR_CSV;           
            }

            if (sep == '\0') // Not L10N
            {
                columnTypes = new Type[0];
                return false;
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
            columns = line.Split(sep);
            return columns.Length > 1;
        }

        private static Type GetColumnType(string value, IFormatProvider provider)
        {
            double result;
            if (double.TryParse(value, NumberStyles.Number, provider, out result))
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
        public ColumnIndices(int proteinColumn,
            int peptideColumn,
            int precursorColumn,
            int productColumn,
            int labelTypeColumn = -1,
            int decoyColumn = -1,
            int irtColumn = -1,
            int libraryColumn = -1)
        {
            ProteinColumn = proteinColumn;
            PeptideColumn = peptideColumn;
            PrecursorColumn = precursorColumn;
            ProductColumn = productColumn;
            LabelTypeColumn = labelTypeColumn;
            DecoyColumn = decoyColumn;
            IrtColumn = irtColumn;
            LibraryColumn = libraryColumn;
        }

        public int ProteinColumn { get; private set; }
        public int PeptideColumn { get; private set; }
        public int PrecursorColumn { get; private set; }
        public int ProductColumn { get; private set; }

        /// <summary>
        /// A column specifying the <see cref="IsotopeLabelType"/> (optional)
        /// </summary>
        public int LabelTypeColumn { get; private set; }

        /// <summary>
        /// A column specifying whether a decoy is expected (optional)
        /// </summary>
        public int DecoyColumn { get; set; }

        /// <summary>
        /// A column specifying an iRT value
        /// </summary>
        public int IrtColumn { get; private set; }

        /// <summary>
        /// A column specifying a spectral library intensity for the transition
        /// </summary>
        public int LibraryColumn { get; private set; }
    }

    /// <summary>
    /// All possible explanations for a single transition
    /// </summary>
    public sealed class ExTransitionInfo
    {
        public ExTransitionInfo(string proteinName, string peptideSequence, string modifiedSequence, double precursorMz, bool isDecoy)
        {
            ProteinName = proteinName;
            PeptideSequence = peptideSequence;
            ModifiedSequence = modifiedSequence;
            PrecursorMz = precursorMz;
            IsDecoy = isDecoy;
            DefaultLabelType = IsotopeLabelType.light;
            TransitionExps = new List<TransitionExp>();
        }

        public string ProteinName { get; private set; }
        public string PeptideSequence { get; private set; }
        public string ModifiedSequence { get; set; }
        public double PrecursorMz { get; private set; }

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
        public TransitionExp(ExplicitMods mods, int precursorCharge, IsotopeLabelType labelType, int precursorMassShift)
        {
            Precursor = new PrecursorExp(mods, precursorCharge, labelType, precursorMassShift);
        }

        public bool IsDecoy { get { return Precursor.MassShift.HasValue; } }
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
        public PrecursorExp(ExplicitMods mods, int precursorCharge, IsotopeLabelType labelType, int massShift)
        {
            VariableMods = mods;
            PrecursorCharge = precursorCharge;
            LabelType = labelType;
            MassShift = null;
            if (massShift != 0)
                MassShift = massShift;
        }

        public ExplicitMods VariableMods { get; private set; }
        public int PrecursorCharge { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
        public int? MassShift { get; private set; }

        #region object overrides

        public bool Equals(PrecursorExp other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.VariableMods, VariableMods) &&
                other.PrecursorCharge == PrecursorCharge &&
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
                result = (result*397) ^ PrecursorCharge;
                result = (result*397) ^ (LabelType != null ? LabelType.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public sealed class ProductExp
    {
        public ProductExp(int productCharge, IonType ionType, int fragmentOrdinal, TransitionLosses losses, int massShift)
        {
            Charge = productCharge;
            IonType = ionType;
            FragmentOrdinal = fragmentOrdinal;
            Losses = losses;
            MassShift = null;
            if (massShift != 0)
                MassShift = massShift;
        }

        public int Charge { get; private set; }
        public IonType IonType { get; private set; }
        public int FragmentOrdinal { get; private set; }
        public TransitionLosses Losses { get; private set; }
        public int? MassShift { get; private set; }
    }

    public class MzMatchException : LineColNumberedIoException
    {
        public MzMatchException(string message, long lineNum, int colNum)
            : base(message, TextUtil.LineSeparate(string.Empty, Resources.MzMatchException_suggestion), lineNum, colNum)
        { }
    }

    public class LineColNumberedIoException : IOException
    {
        public LineColNumberedIoException(string message, long lineNum, int colIndex)
            : base(FormatMessage(message, lineNum, colIndex))
        {
            PlainMessage = message;
            LineNumber = lineNum;
            ColumnIndex = colIndex;
        }

        public LineColNumberedIoException(string message, string suggestion, long lineNum, int colIndex)
            : base(TextUtil.LineSeparate(FormatMessage(message, lineNum, colIndex), suggestion))
        {
            PlainMessage = TextUtil.LineSeparate(message, suggestion);
            LineNumber = lineNum;
            ColumnIndex = colIndex;
        }

        public LineColNumberedIoException(string message, long lineNum, int colIndex, Exception inner)
            : base(FormatMessage(message, lineNum, colIndex), inner)
        {
            PlainMessage = message;
            LineNumber = lineNum;
            ColumnIndex = colIndex;
        }

        private static string FormatMessage(string message, long lineNum, int colIndex)
        {
            if (colIndex == -1)
                return string.Format(Resources.LineColNumberedIoException_FormatMessage__0___line__1__, message, lineNum);
            else
                return string.Format(Resources.LineColNumberedIoException_FormatMessage__0___line__1___col__2__, message, lineNum, colIndex + 1);
        }

        public string PlainMessage { get; private set; }
        public long LineNumber { get; private set; }
        public int ColumnIndex { get; private set; }
    }

    public class PeptideGroupBuilder
    {
        private readonly StringBuilder _sequence = new StringBuilder();
        private readonly List<PeptideDocNode> _peptides;
        private readonly Dictionary<int, int> _charges;
        private readonly SrmSettings _settings;
        private readonly Enzyme _enzyme;
        private readonly bool _customName;

        private FastaSequence _activeFastaSeq;
        private Peptide _activePeptide;
        private string _activeModifiedSequence;
        // Order is important to making the variable modification choice deterministic
        // when more than one potential set of variable modifications work to explain
        // the contents of the active peptide.
        private List<ExplicitMods> _activeVariableMods;
        private List<PrecursorExp> _activePrecursorExps;
        private double _activePrecursorMz;
        private readonly List<ExTransitionInfo> _activeTransitionInfos;
        private double? _irtValue;
        private readonly List<MeasuredRetentionTime> _irtPeptides;
        private readonly List<TransitionImportErrorInfo> _peptideGroupErrorInfo;
        private readonly List<TransitionGroupLibraryIrtTriple> _groupLibTriples;
        private readonly List<SpectrumMzInfo> _librarySpectra;
        private readonly List<SpectrumPeaksInfo.MI> _activeLibraryIntensities;

        private readonly ModificationMatcher _modMatcher;
        private bool _autoManageChildren;

        public PeptideGroupBuilder(FastaSequence fastaSequence, SrmSettings settings)
        {
            _activeFastaSeq = fastaSequence;
            _autoManageChildren = true;
            if (fastaSequence != null)
            {
                BaseName = Name = fastaSequence.Name;
                Description = fastaSequence.Description;
                Alternatives = fastaSequence.Alternatives.ToArray();
            }
            _settings = settings;
            _enzyme = _settings.PeptideSettings.Enzyme;
            _peptides = new List<PeptideDocNode>();
            _charges = new Dictionary<int, int>();
            _groupLibTriples = new List<TransitionGroupLibraryIrtTriple>();
            _activeTransitionInfos = new List<ExTransitionInfo>();
            _irtPeptides = new List<MeasuredRetentionTime>();
            _librarySpectra = new List<SpectrumMzInfo>();
            _activeLibraryIntensities = new List<SpectrumPeaksInfo.MI>();
            _peptideGroupErrorInfo = new List<TransitionImportErrorInfo>();
            _activeModifiedSequence = null;
        }

        public PeptideGroupBuilder(string line, bool peptideList, SrmSettings settings)
            : this(null, settings)
        {
            int start = (line.Length > 0 && line[0] == '>' ? 1 : 0); // Not L10N
            // If there is a second >, then this is a custom name, and not
            // a real FASTA sequence.
            if (line.Length > 1 && line[1] == '>') // Not L10N
            {
                _customName = true;
                start++;
            }
            // Split ID from description at first space or tab
            int split = _customName ? -1 : IndexEndId(line);
            if (split == -1)
            {
                BaseName = Name = line.Substring(start);
                Description = string.Empty;
            }
            else
            {
                BaseName = Name = line.Substring(start, split - start);
                string[] descriptions = line.Substring(split + 1).Split((char)1);
                Description = descriptions[0];
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
                Alternatives = listAlternatives.ToArray();
            }
            PeptideList = peptideList;
        }

        public PeptideGroupBuilder(string line, ModificationMatcher modMatcher, SrmSettings settings)
            : this(line, true, settings)
        {
            _modMatcher = modMatcher;
        }

        private static int IndexEndId(string line)
        {
            return line.IndexOfAny(new[] { TextUtil.SEPARATOR_SPACE, TextUtil.SEPARATOR_TSV });
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
        public ProteinMetadata[] Alternatives { get; private set; }
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

        public void AppendSequence(string seqMod)
        {
            int? charge = Transition.GetChargeFromIndicator(seqMod, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
            seqMod = Transition.StripChargeIndicators(seqMod, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
            var seq = FastaSequence.StripModifications(seqMod);
            // Auto manage the children unless there is at least one modified sequence in the fasta
            _autoManageChildren = _autoManageChildren && Equals(seq, seqMod);
            // Get rid of whitespace
            seq = seq.Replace(" ", string.Empty).Trim(); // Not L10N
            // Get rid of 
            if (seq.EndsWith("*")) // Not L10N  
                seq = seq.Substring(0, seq.Length - 1);

            if (!PeptideList)
                _sequence.Append(seq);
            else
            {
                // If there is a ModificationMatcher, use it to create the DocNode.
                PeptideDocNode nodePep;
                if (_modMatcher != null)
                    nodePep = _modMatcher.GetModifiedNode(seqMod);
                else
                {
                    Peptide peptide = new Peptide(null, seq, null, null, _enzyme.CountCleavagePoints(seq));
                    nodePep = new PeptideDocNode(peptide);
                }
                _peptides.Add(nodePep);
                if (charge.HasValue)
                    _charges.Add(nodePep.Id.GlobalIndex, charge.Value);
            }
        }

        public void AppendTransition(ExTransitionInfo info, double? irt, double? libraryIntensity, double productMz, long lineNum)
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
                                                                        lineNum);
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
                        throw new InvalidDataException(string.Format(Resources.PeptideGroupBuilder_AppendTransition_The_peptide__0__was_not_found_in_the_sequence__1__,
                                                       sequence, _activeFastaSeq.Name));
                    }
                    end = begin + sequence.Length;
                }
                _activePeptide = new Peptide(_activeFastaSeq, sequence, begin, end, _enzyme.CountCleavagePoints(sequence), info.TransitionExps[0].IsDecoy);
                _activeModifiedSequence = info.ModifiedSequence;
                _activePrecursorMz = info.PrecursorMz;
                _activeVariableMods = new List<ExplicitMods>(info.PotentialVarMods.Distinct());
                _activePrecursorExps = new List<PrecursorExp>(info.TransitionExps.Select(exp => exp.Precursor));
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
                var errorInfo = new TransitionImportErrorInfo(string.Format(Resources.PeptideGroupBuilder_AppendTransition_Failed_to_explain_all_transitions_for_m_z__0___peptide__1___with_a_single_precursor,
                                                                            precursorMz, info.PeptideSequence),
                                                                null,
                                                                lineNum);
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
                                                              lineNum);
                _peptideGroupErrorInfo.Add(errorInfo);
                return;
            }
            if (_activePrecursorMz == 0)
            {
                _activePrecursorMz = info.PrecursorMz;
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
            var docNode = new PeptideDocNode(_activePeptide, _settings, _activeVariableMods[0], null, null,
                finalTransitionGroups, false);
            var finalLibrarySpectra = new List<SpectrumMzInfo>();
            double? peptideIrt = GetPeptideIrt(finalGroupLibTriples);
            foreach (var groupLibTriple in finalGroupLibTriples)
            {
                if (groupLibTriple.SpectrumInfo == null)
                    continue;
                var sequence = groupLibTriple.NodeGroup.TransitionGroup.Peptide.Sequence;
                var mods = docNode.ExplicitMods;
                var calcPre = _settings.GetPrecursorCalc(groupLibTriple.SpectrumInfo.Label, mods);
                string modifiedSequenceWithIsotopes = calcPre.GetModifiedSequence(sequence, false);

                finalLibrarySpectra.Add(new SpectrumMzInfo
                {
                    Key = new LibKey(modifiedSequenceWithIsotopes, groupLibTriple.NodeGroup.TransitionGroup.PrecursorCharge),
                    Label = groupLibTriple.SpectrumInfo.Label,
                    PrecursorMz = groupLibTriple.SpectrumInfo.PrecursorMz,
                    SpectrumPeaks = groupLibTriple.SpectrumInfo.SpectrumPeaks
                }); 
            }
            _librarySpectra.AddRange(finalLibrarySpectra);
            _peptides.Add(docNode);
            if (peptideIrt.HasValue)
            {
                _irtPeptides.Add(new MeasuredRetentionTime(docNode.ModifiedSequence, peptideIrt.Value, true));
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
            double norm = groupTriplesNonNull.Count();
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
                            var errorInfo = new TransitionImportErrorInfo(string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Missing_iRT_value_for_peptide__0___precursor_m_z__1_,
                                                                                        _activePeptide.Sequence, precursorMz),
                                                                            null,
                                                                            null);
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
                                                                            null,
                                                                            null);
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
                                                      precursorExp.PrecursorCharge,
                                                      precursorExp.LabelType,
                                                      false,
                                                      precursorExp.MassShift);
            var transitions = _activeTransitionInfos.ConvertAll(info =>
                {
                    var productExp = info.TransitionExps.Single(exp => Equals(precursorExp, exp.Precursor)).Product;
                    var ionType = productExp.IonType;
                    var ordinal = productExp.FragmentOrdinal;
                    int offset = Transition.OrdinalToOffset(ionType, ordinal, _activePeptide.Sequence.Length);
                    int? massShift = productExp.MassShift;
                    if (massShift == null && precursorExp.MassShift.HasValue)
                        massShift = 0;
                    var tran = new Transition(transitionGroup, ionType, offset, 0, productExp.Charge, massShift);
                    // m/z and library info calculated later
                    return new TransitionDocNode(tran, productExp.Losses, 0, null, null);
                });
            // m/z calculated later
            var newTransitionGroup = new TransitionGroupDocNode(transitionGroup, CompleteTransitions(transitions));
            var currentLibrarySpectrum = !_activeLibraryIntensities.Any() ? null : 
                new SpectrumMzInfo
                {
                    Key = new LibKey(_activePeptide.Sequence, precursorExp.PrecursorCharge),
                    PrecursorMz = _activePrecursorMz,
                    Label = precursorExp.LabelType,
                    SpectrumPeaks = new SpectrumPeaksInfo(_activeLibraryIntensities.ToArray())
                };
            _groupLibTriples.Add(new TransitionGroupLibraryIrtTriple(currentLibrarySpectrum, newTransitionGroup, _irtValue, _activePrecursorMz));
            _activePrecursorMz = 0;
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
            foreach (var ionType in _settings.TransitionSettings.Filter.IonTypes)
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
            nodePepGroup = nodePepGroup.ChangeSettings(_settings, diff);

            List<DocNode> newChildren = new List<DocNode>();
            foreach (PeptideDocNode nodePep in nodePepGroup.Children)
            {
                var nodePepAdd = nodePep;
                int charge;
                if (_charges.TryGetValue(nodePep.Id.GlobalIndex, out charge))
                {
                    var settingsCharge = _settings.ChangeTransitionFilter(f => f.ChangePrecursorCharges(new[] {charge}));
                    nodePepAdd = (PeptideDocNode) nodePep.ChangeSettings(settingsCharge, diff)
                                                         .ChangeAutoManageChildren(false);
                }
                newChildren.Add(nodePepAdd);
            }
            return (PeptideGroupDocNode) nodePepGroup.ChangeChildren(newChildren);
        }
    }

    public class TransitionImportErrorInfo
    {
        public long? Row { get; private set; }
        public int? Column { get; private set; }
        public string ErrorMessage { get; private set; }

        public TransitionImportErrorInfo(string errorMessage, int? column, long? row)
        {
            ErrorMessage = errorMessage;
            Column = column;
            Row = row;
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

    public class FastaData
    {
        private FastaData(string name, string sequence)
        {
            Name = name;
            Sequence = sequence;
        }

        public string Name { get; private set; }
        public string Sequence { get; private set; }

        public static void AppendSequence(StringBuilder sequence, string line)
        {
            var seq = FastaSequence.StripModifications(line);
            // Get rid of whitespace
            seq = seq.Replace(" ", string.Empty).Trim(); // Not L10N
            // Get rid of end of sequence indicator
            if (seq.EndsWith("*")) // Not L10N  
                seq = seq.Substring(0, seq.Length - 1);
            sequence.Append(seq);
        }

        public static IEnumerable<FastaData> ParseFastaFile(TextReader reader)
        {
            string line;
            string name = string.Empty;
            StringBuilder sequence = new StringBuilder();

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(">")) // Not L10N
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        yield return new FastaData(name, sequence.ToString());

                        sequence.Clear();
                    }
                    var split = line.Split(TextUtil.SEPARATOR_SPACE);
                    // Remove the '>'
                    name = split[0].Remove(0, 1).Trim();
                }
                else
                {
                    AppendSequence(sequence, line);
                }
            }

            // Add the last fasta sequence
            yield return new FastaData(name, sequence.ToString());
        }
    }
}
