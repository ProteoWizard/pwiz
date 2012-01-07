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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class FastaImporter
    {
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

        public IEnumerable<PeptideGroupDocNode> Import(TextReader reader, ILongWaitBroker longWaitBroker, long lineCount)
        {
            // Set starting values for limit counters
            _countPeptides = Document.PeptideCount;
            _countIons = Document.TransitionCount;

            // Store set of existing FASTA sequences to keep from duplicating
            HashSet<FastaSequence> set = new HashSet<FastaSequence>();
            foreach (PeptideGroupDocNode nodeGroup in Document.Children)
            {
                FastaSequence fastaSeq = nodeGroup.Id as FastaSequence;
                if (fastaSeq != null)
                    set.Add(fastaSeq);
            }

            List<PeptideGroupDocNode> peptideGroupsNew = new List<PeptideGroupDocNode>();
            PeptideGroupBuilder seqBuilder = null;

            long linesRead = 0;
            int progressPercent = 0;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                linesRead++;
                if (longWaitBroker != null)
                {
                    if (longWaitBroker.IsCanceled || longWaitBroker.IsDocumentChanged(Document))
                        return new PeptideGroupDocNode[0];
                    int progressNew = (int) (linesRead*100/lineCount);
                    if (progressPercent != progressNew)
                        longWaitBroker.ProgressValue = progressPercent = progressNew;
                }

                if (line.StartsWith(">"))
                {
                    if (_countIons > SrmDocument.MAX_TRANSITION_COUNT ||
                            _countPeptides > SrmDocument.MAX_PEPTIDE_COUNT)
                        throw new InvalidDataException("Document size limit exceeded.");

                    if (seqBuilder != null)
                        AddPeptideGroup(peptideGroupsNew, set, seqBuilder);

                    seqBuilder = _modMatcher == null
                        ? new PeptideGroupBuilder(line, PeptideList, Document.Settings)
                        : new PeptideGroupBuilder(line, _modMatcher, Document.Settings);
                    if (longWaitBroker != null)
                        longWaitBroker.Message = string.Format("Adding protein {0}", seqBuilder.Name);
                }
                else if (seqBuilder == null)
                {
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

        private void AddPeptideGroup(ICollection<PeptideGroupDocNode> listGroups,
            ICollection<FastaSequence> set, PeptideGroupBuilder builder)
        {
            PeptideGroupDocNode nodeGroup = builder.ToDocNode();
            FastaSequence fastaSeq = nodeGroup.Id as FastaSequence;
            if (fastaSeq != null && set.Contains(fastaSeq))
                return;
            listGroups.Add(nodeGroup);
            _countPeptides += nodeGroup.PeptideCount;
            _countIons += nodeGroup.TransitionCount;
        }

        /// <summary>
        /// Converts columnar data into FASTA format
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
                    throw new LineColNumberedIoException("Too few columns found", lineNum, -1);
                int lastCol = columns.Length - 1;
                string seq = columns[lastCol].Trim();
                if (!FastaSequence.IsExSequence(seq))
                    throw new LineColNumberedIoException("Last column does not contain a valid protein sequence", lineNum, lastCol);
                sb.Append(">").Append(columns[0].Trim().Replace(" ", "_")); // ID
                for (int i = 1; i < lastCol; i++)
                    sb.Append(" ").Append(columns[i].Trim()); // Description                    
                sb.AppendLine();
                sb.AppendLine(seq); // Sequence
            }
            return sb.ToString();
        }
    }

    public class MassListImporter
    {
        private const int INSPECT_LINES = 50;

// ReSharper disable NotAccessedField.Local
        private int _countPeptides;
        private int _countIons;
// ReSharper restore NotAccessedField.Local

        public MassListImporter(SrmDocument document, IFormatProvider provider, char separator)
        {
            Document = document;
            FormatProvider = provider;
            Separator = separator;
        }

        public SrmDocument Document { get; private set; }
        public SrmSettings Settings { get { return Document.Settings; } }
        public IFormatProvider FormatProvider { get; private set; }
        public char Separator { get; private set; }

        public IEnumerable<PeptideGroupDocNode> Import(TextReader reader, ILongWaitBroker longWaitBroker, long lineCount)
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
                return Import(reader, longWaitBroker, lineCount, null, dictNameSeqAll);
            }
            catch (LineColNumberedIoException x)
            {
                throw new InvalidDataException(x.Message, x);
            }
        }

        public IEnumerable<PeptideGroupDocNode> Import(TextReader reader,
                                                       ILongWaitBroker longWaitBroker,
                                                       long lineCount,
                                                       ColumnIndices indices,
                                                       IDictionary<string, FastaSequence> dictNameSeq)
        {
            // Get the lines used to guess the necessary columns and create the row reader
            string line;
            MassListRowReader rowReader;
            List<string> lines = new List<string>();
            if (indices != null)
            {
                rowReader = new GeneralRowReader(FormatProvider, Separator, indices, Settings);
            }
            else
            {
                // Check first line for validity
                line = reader.ReadLine();
                if (line == null)
                    throw new InvalidDataException("Empty transition list.");
                string[] fields = line.ParseDsvFields(Separator);
                if (fields.Length < 3)
                    throw new InvalidDataException("Invalid transition list.  Transition lists must contain at least precursor m/z, product m/z, and peptide sequence.");
                lines.Add(line);

                rowReader = ExPeptideRowReader.Create(lines, FormatProvider, Separator, Settings);
                if (rowReader == null)
                {
                    for (int i = 1; i < INSPECT_LINES; i++)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                            break;
                        lines.Add(line);
                    }
                    rowReader = GeneralRowReader.Create(lines, null, FormatProvider, Separator, Settings);
                    if (rowReader == null)
                    {
                        // Check for a possible header row
                        string[] headers = lines[0].Split(Separator);
                        lines.RemoveAt(0);
                        rowReader = GeneralRowReader.Create(lines, headers, FormatProvider, Separator, Settings);
                        if (rowReader == null)
                            throw new LineColNumberedIoException("Failed to find peptide column.", 1, -1);
                    }
                }
            }

            // Set starting values for limit counters
            _countPeptides = Document.PeptideCount;
            _countIons = Document.TransitionCount;

            List<PeptideGroupDocNode> peptideGroupsNew = new List<PeptideGroupDocNode>();
            PeptideGroupBuilder seqBuilder = null;

            // Process cached lines and then remaining lines
            long lineIndex = 0;
            while ((line = (lineIndex < lines.Count ? lines[(int)lineIndex] : reader.ReadLine())) != null)
            {
                lineIndex++;
                rowReader.NextRow(line, lineIndex);

                if (longWaitBroker != null)
                {
                    if (longWaitBroker.IsCanceled)
                        return new PeptideGroupDocNode[0];

                    int percentComplete = (int)(lineIndex * 100 / lineCount);

                    if (longWaitBroker.ProgressValue != percentComplete)
                    {
                        longWaitBroker.ProgressValue = percentComplete;
                        longWaitBroker.Message = string.Format("Importing {0}",
                            rowReader.TransitionInfo.ProteinName ?? rowReader.TransitionInfo.PeptideSequence);
                    }
                }

                seqBuilder = AddRow(seqBuilder, rowReader, dictNameSeq, peptideGroupsNew, lineIndex);
            }

            // Add last sequence.
            if (seqBuilder != null)
                AddPeptideGroup(peptideGroupsNew, seqBuilder);

            return peptideGroupsNew;
        }

        private PeptideGroupBuilder AddRow(PeptideGroupBuilder seqBuilder,
                                           MassListRowReader rowReader,
                                           IDictionary<string, FastaSequence> dictNameSeq,
                                           ICollection<PeptideGroupDocNode> peptideGroupsNew,
                                           long lineNum)
        {
            var info = rowReader.TransitionInfo;
            string name = info.ProteinName;
            if (seqBuilder == null || (name != null && !Equals(name, seqBuilder.BaseName)))
            {
                if (seqBuilder != null)
                    AddPeptideGroup(peptideGroupsNew, seqBuilder);
                FastaSequence fastaSeq;
                if (name != null && dictNameSeq.TryGetValue(name, out fastaSeq) && fastaSeq != null)
                    seqBuilder = new PeptideGroupBuilder(fastaSeq, Document.Settings);
                else
                {
                    string safeName = name != null ?
                        Helpers.GetUniqueName(name, dictNameSeq.Keys) :
                        Document.GetPeptideGroupId(true);
                    seqBuilder = new PeptideGroupBuilder(">>" + safeName, true, Document.Settings) {BaseName = name};
                }
            }
            try
            {
                seqBuilder.AppendTransition(info);
            }
            catch (InvalidDataException x)
            {
                throw new LineColNumberedIoException(x.Message, lineNum, -1, x);
            }
            return seqBuilder;
        }

        private void AddPeptideGroup(ICollection<PeptideGroupDocNode> listGroups, PeptideGroupBuilder builder)
        {
            PeptideGroupDocNode nodeGroup = builder.ToDocNode();
            listGroups.Add(nodeGroup);
            _countPeptides += nodeGroup.PeptideCount;
            _countIons += nodeGroup.TransitionCount;
        }

        private abstract class MassListRowReader
        {
            protected MassListRowReader(IFormatProvider provider,
                                        char separator,
                                        ColumnIndices indices,
                                        SrmSettings settings)
            {
                FormatProvider = provider;
                Separator = separator;
                Indices = indices;
                Settings = settings;
            }

            protected SrmSettings Settings { get; private set; }
            protected string[] Fields { get; private set; }
            private IFormatProvider FormatProvider { get; set; }
            private char Separator { get; set; }
            private ColumnIndices Indices { get; set; }
            protected int ProteinColumn { get { return Indices.ProteinColumn; } }
            protected int PeptideColumn { get { return Indices.PeptideColumn; } }
            protected int LabelTypeColumn { get { return Indices.LabelTypeColumn; } }
            private int PrecursorColumn { get { return Indices.PrecursorColumn; } }
            protected double PrecursorMz { get { return ColumnMz(Fields, PrecursorColumn, FormatProvider); } }
            private int ProductColumn { get { return Indices.ProductColumn; } }
            private double ProductMz { get { return ColumnMz(Fields, ProductColumn, FormatProvider); } }

            private double MzMatchTolerance { get { return Settings.TransitionSettings.Instrument.MzMatchTolerance; } }

            public ExTransitionInfo TransitionInfo { get; private set; }

            private bool IsHeavyAllowed
            {
                get { return Settings.PeptideSettings.Modifications.HasHeavyImplicitModifications; }
            }

            private bool IsHeavyTypeAllowed(IsotopeLabelType labelType)
            {
                return Settings.GetPrecursorCalc(labelType, null) != null;
            }

            public void NextRow(string line, long lineNum)
            {
                Fields = line.ParseDsvFields(Separator);

                ExTransitionInfo info = CalcTransitionInfo(lineNum);

                if (!FastaSequence.IsExSequence(info.PeptideSequence))
                    throw new LineColNumberedIoException(string.Format("Invalid peptide sequence {0} found", info.PeptideSequence), lineNum, PeptideColumn);
                if (!info.DefaultLabelType.IsLight && !IsHeavyTypeAllowed(info.DefaultLabelType))
                    throw new LineColNumberedIoException("Isotope labeled entry found without matching settings", "Check the Modifications tab in Transition Settings.", lineNum, LabelTypeColumn);

                info = CalcPrecursorExplanations(info, lineNum);

                TransitionInfo = CalcTransitionExplanations(info, lineNum);
            }

            protected abstract ExTransitionInfo CalcTransitionInfo(long lineNum);

            private ExTransitionInfo CalcPrecursorExplanations(ExTransitionInfo info, long lineNum)
            {
                var peptideMods = Settings.PeptideSettings.Modifications;

                // Enumerate all possible variable modifications looking for an explanation
                // for the precursor information
                double precursorMz = info.PrecursorMz;
                double nearestMz = double.MaxValue;
                foreach (var nodePep in Peptide.CreateAllDocNodes(Settings, info.PeptideSequence))
                {
                    var variableMods = nodePep.ExplicitMods;
                    var defaultLabelType = info.DefaultLabelType;
                    double precursorMassH = Settings.GetPrecursorMass(defaultLabelType, info.PeptideSequence, variableMods);
                    int precursorCharge = CalcPrecursorCharge(precursorMassH, precursorMz, MzMatchTolerance);
                    if (precursorCharge > 0)
                    {
                        info.TransitionExps.Add(new TransitionExp(variableMods, precursorCharge, defaultLabelType));
                    }
                    else
                    {
                        nearestMz = NearestMz(info.PrecursorMz, nearestMz, precursorMassH, -precursorCharge);
                    }

                    if (!IsHeavyAllowed || info.IsExplicitLabelType)
                        continue;

                    foreach (var labelType in peptideMods.GetHeavyModifications().Select(typeMods => typeMods.LabelType))
                    {
                        precursorMassH = Settings.GetPrecursorMass(labelType, info.PeptideSequence, null);
                        precursorCharge = CalcPrecursorCharge(precursorMassH, precursorMz, MzMatchTolerance);
                        if (precursorCharge > 0)
                        {
                            info.TransitionExps.Add(new TransitionExp(variableMods, precursorCharge, labelType));
                        }
                        else
                        {
                            nearestMz = NearestMz(info.PrecursorMz, nearestMz, precursorMassH, -precursorCharge);
                        }
                    }
                }

                if (info.TransitionExps.Count == 0)
                {
                    // TODO: Consistent central formatting for m/z values
                    // Use Math.Round() to avoid forcing extra decimal places
                    nearestMz = Math.Round(nearestMz, 4);
                    precursorMz = Math.Round(SequenceMassCalc.PersistentMZ(precursorMz), 4);
                    double deltaMz = Math.Round(Math.Abs(precursorMz - nearestMz), 4);
                    throw new MzMatchException(string.Format("Precursor m/z {0} does not match the closest possible value {1} (delta = {2})", precursorMz, nearestMz, deltaMz), lineNum, PrecursorColumn);
                }
                else if (!Settings.TransitionSettings.Instrument.IsMeasurable(precursorMz))
                {
                    precursorMz = Math.Round(SequenceMassCalc.PersistentMZ(precursorMz), 4);
                    throw new LineColNumberedIoException(string.Format("The precursor m/z {0} is out of range for the instrument settings", precursorMz), "Check the Instrument tab in the Transition Settings.", lineNum, PrecursorColumn);
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

            private static int CalcPrecursorCharge(double precursorMassH, double precursorMz, double tolerance)
            {
                return TransitionCalc.CalcPrecursorCharge(precursorMassH, precursorMz, tolerance);
            }

            private ExTransitionInfo CalcTransitionExplanations(ExTransitionInfo info, long lineNum)
            {
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
                    int productCharge = TransitionCalc.CalcProductCharge(productPrecursorMass,
                        transitionExp.Precursor.PrecursorCharge, productMasses, potentialLosses, productMz,
                        MzMatchTolerance, calc.MassType, out ionType, out ordinal, out losses);

                    if (productCharge > 0 && ionType.HasValue && ordinal.HasValue)
                    {
                        transitionExp.Product = new ProductExp(productCharge, ionType.Value, ordinal.Value, losses);
                    }
                    else
                    {
                        info.TransitionExps.Remove(transitionExp);
                    }
                }

                if (info.TransitionExps.Count == 0)
                {
                    // TODO: Consistent central formatting for m/z values
                    // Use Math.Round() to avoid forcing extra decimal places
                    productMz = Math.Round(productMz, 4);
                    throw new MzMatchException(string.Format("Product m/z value {0} has no matching product ion", productMz), lineNum, ProductColumn);
                }
                else if (!Settings.TransitionSettings.Instrument.IsMeasurable(productMz))
                {
                    productMz = Math.Round(productMz, 4);
                    throw new LineColNumberedIoException(string.Format("The product m/z {0} is out of range for the instrument settings", productMz), "Check the Instrument tab in the Transition Settings.", lineNum, ProductColumn);
                }

                return info;
            }

            private static double ColumnMz(string[] fields, int column, IFormatProvider provider)
            {
                try
                {
                    return double.Parse(fields[column], provider);
                }
                catch (FormatException)
                {
                    return 0;   // Invalid m/z
                }                
            }

            protected static int FindPrecursor(string[] fields,
                                               string sequence,
                                               IsotopeLabelType labelType,
                                               int iSequence,
                                               double tolerance,
                                               IFormatProvider provider,
                                               SrmSettings settings,
                                               out IList<TransitionExp> transitionExps)
            {
                transitionExps = new List<TransitionExp>();
                int indexPrec = -1;
                foreach (PeptideDocNode nodePep in Peptide.CreateAllDocNodes(settings, sequence))
                {
                    var mods = nodePep.ExplicitMods;
                    var calc = settings.GetPrecursorCalc(labelType, mods);
                    if (calc == null)
                        continue;

                    double precursorMassH = calc.GetPrecursorMass(sequence);

                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (indexPrec != -1 && i != indexPrec)
                            continue;
                        if (i == iSequence)
                            continue;

                        double precursorMz = ColumnMz(fields, i, provider);
                        if (precursorMz == 0)
                            continue;

                        int charge = CalcPrecursorCharge(precursorMassH, precursorMz, tolerance);
                        if (charge > 0)
                        {
                            indexPrec = i;
                            transitionExps.Add(new TransitionExp(mods, charge, labelType));
                        }
                    }
                }
                return indexPrec;
            }

            protected static int FindProduct(string[] fields, string sequence, IEnumerable<TransitionExp> transitionExps,
                int iSequence, int iPrecursor, double tolerance, IFormatProvider provider, SrmSettings settings)
            {
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
                        int charge = TransitionCalc.CalcProductCharge(productPrecursorMass,
                                                                      transitionExp.Precursor.PrecursorCharge,
                                                                      productMasses,
                                                                      potentialLosses,
                                                                      productMz,
                                                                      tolerance,
                                                                      calc.MassType,
                                                                      out ionType,
                                                                      out ordinal,
                                                                      out losses);
                        if (charge > 0)
                            return i;
                    }
                }

                return -1;
            }
        }

        private class GeneralRowReader : MassListRowReader
        {
            public GeneralRowReader(IFormatProvider provider,
                                     char separator,
                                     ColumnIndices indices,
                                     SrmSettings settings)
                : base(provider, separator, indices, settings)
            {
            }

            private static IsotopeLabelType GetLabelType(string typeId)
            {
                return (Equals(typeId, "H") ? IsotopeLabelType.heavy : IsotopeLabelType.light);
            }

            protected override ExTransitionInfo CalcTransitionInfo(long lineNum)
            {
                string proteinName = null;
                if (ProteinColumn != -1)
                    proteinName = Fields[ProteinColumn];
                string peptideSequence = RemoveSequenceNotes(Fields[PeptideColumn]);
                var info = new ExTransitionInfo(proteinName, peptideSequence, PrecursorMz);

                if (LabelTypeColumn != -1)
                {
                    info.DefaultLabelType = GetLabelType(Fields[LabelTypeColumn]);
                    info.IsExplicitLabelType = true;                    
                }

                return info;
            }

            public static GeneralRowReader Create(IList<string> lines, IList<string> headers,
                IFormatProvider provider, char separator, SrmSettings settings)
            {
                // Split the first line into fields.
                Debug.Assert(lines.Count > 0);
                string[] fields = lines[0].ParseDsvFields(separator);
                double tolerance = settings.TransitionSettings.Instrument.MzMatchTolerance;

                int iLabelType = FindLabelType(fields, lines, separator);

                // Look for sequence column
                string sequence;
                int iSequence = -1;
                int iPrecursor;
                IList<TransitionExp> transitionExps;
                do
                {
                    int iStart = iSequence + 1;
                    iSequence = FindSequence(fields, iStart, out sequence);

                    // If no sequence column found, return null.  After this,
                    // all errors throw.
                    if (iSequence == -1)
                    {
                        // If this is not the first time through, then error on finding a valid precursor.
                        if (iStart > 0)
                            throw new MzMatchException("No valid precursor m/z column found.", 1, -1);
                        return null;
                    }

                    IsotopeLabelType labelType = IsotopeLabelType.light;
                    if (iLabelType != -1)
                        labelType = GetLabelType(fields[iLabelType]);
                    iPrecursor = FindPrecursor(fields, sequence, labelType, iSequence,
                        tolerance, provider, settings, out transitionExps);
                    // If no match, and no specific label type, then try heavy.
                    if (settings.PeptideSettings.Modifications.HasHeavyModifications &&
                            iPrecursor == -1 && iLabelType == -1)
                    {
                        var peptideMods = settings.PeptideSettings.Modifications;
                        foreach (var typeMods in peptideMods.GetHeavyModifications())
                        {
                            if (settings.GetPrecursorCalc(labelType, null) != null)
                            {
                                iPrecursor = FindPrecursor(fields, sequence, typeMods.LabelType, iSequence,
                                    tolerance, provider, settings, out transitionExps);
                            }
                        }
                    }
                }
                while (iPrecursor == -1);

                int iProduct = FindProduct(fields, sequence, transitionExps, iSequence, iPrecursor,
                    tolerance, provider, settings);
                if (iProduct == -1)
                    throw new MzMatchException("No valid product m/z column found.", 1, -1);

                int iProtein = FindProtein(fields, iSequence, lines, headers, provider, separator);

                var indices = new ColumnIndices(iProtein, iSequence, iPrecursor, iProduct, iLabelType);

                return new GeneralRowReader(provider, separator, indices, settings);
            }

            private static int FindSequence(string[] fields, int start, out string sequence)
            {
                for (int i = start; i < fields.Length; i++)
                {
                    string seqPotential = RemoveSequenceNotes(fields[i]);
                    if (seqPotential.Length < 2)
                        continue;
                    if (FastaSequence.IsExSequence(seqPotential))
                    {
                        sequence = seqPotential;
                        return i;
                    }
                }
                sequence = null;
                return -1;                
            }

            private static string RemoveSequenceNotes(string seq)
            {
                if (seq.IndexOf('[') == -1)
                    return seq;
                StringBuilder seqBuild = new StringBuilder(seq.Length);
                bool inNote = false;
                foreach (var c in seq)
                {
                    if (!inNote)
                    {
                        if (c == '[')
                            inNote = true;
                        else
                            seqBuild.Append(c);
                    }
                    else if (c == ']')
                    {
                        inNote = false;                        
                    }
                }
                return seqBuild.ToString();
            }

            private static readonly string[] EXCLUDE_PROTEIN_VALUES = new[] { "true", "false", "heavy", "light", "unit" };

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
                        if (fieldValue.Length > 2 && !EXCLUDE_PROTEIN_VALUES.Contains(fieldValue.ToLower()))
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
                        AddCount(fieldsNext[iSequence], sequenceCounts);
                        for (int i = 0; i < valueCounts.Length; i++)
                        {
                            int iField = listDescriptive[i];
                            string key = (iField >= fieldsNext.Length ? "" : fieldsNext[iField]);
                            AddCount(key, valueCounts[i]);
                        }
                    }
                    for (int i = valueCounts.Length - 1; i >= 0; i--)
                    {
                        // Discard any column with empty cells or which is less repetitive
                        int count;
                        if (valueCounts[i].TryGetValue("", out count) || valueCounts[i].Count > sequenceCounts.Count)
                            listDescriptive.RemoveAt(i);
                    }
                    // If more than one possible value, and there are headers, look for
                    // one with the word protein in it.
                    if (headers != null && listDescriptive.Count > 1)
                    {
                        foreach (int i in listDescriptive)
                        {
                            if (headers[i].ToLower().Contains("protein"))
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
                    if (Equals(fields[i], "H") || Equals(fields[i], "L"))
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
                    if (!Equals(fieldsNext[iLabelType], "H") && !Equals(fieldsNext[iLabelType], "L"))
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
        }

        private class ExPeptideRowReader : MassListRowReader
        {
            private const string REGEX_PEPTIDE_FORMAT =@"([^. ]+)\.([A-Z]+)\.[^. ]+\.(light|{0})";

            private ExPeptideRowReader(IFormatProvider provider,
                                       char separator,
                                       ColumnIndices indices,
                                       Regex exPeptideRegex,
                                       SrmSettings settings)
                : base(provider, separator, indices, settings)
            {
                ExPeptideRegex = exPeptideRegex;
            }

            private Regex ExPeptideRegex { get; set; }

            protected override ExTransitionInfo CalcTransitionInfo(long lineNum)
            {
                string exPeptide = Fields[PeptideColumn];
                Match match = ExPeptideRegex.Match(exPeptide);
                if (!match.Success)
                    throw new LineColNumberedIoException(string.Format("Invalid extended peptide format {0}", exPeptide), lineNum, PeptideColumn);

                try
                {
                    string proteinName = match.Groups[1].Value;
                    string peptideSequence = match.Groups[2].Value;

                    var info = new ExTransitionInfo(proteinName, peptideSequence, PrecursorMz)
                        {
                            DefaultLabelType = GetLabelType(match, Settings),
                            IsExplicitLabelType = true
                        };

                    return info;
                }
                catch (Exception)
                {
                    throw new LineColNumberedIoException(string.Format("Invalid extended peptide format {0}", exPeptide), lineNum, PeptideColumn);
                }
            }

            public static ExPeptideRowReader Create(IList<string> lines,
                IFormatProvider provider, char separator, SrmSettings settings)
            {
                // Split the first line into fields.
                Debug.Assert(lines.Count > 0);
                string[] fields = lines[0].ParseDsvFields(separator);

                // Create the ExPeptide regular expression
                var modSettings = settings.PeptideSettings.Modifications;
                var heavyTypeNames = from typedMods in modSettings.GetHeavyModifications()
                                     select typedMods.LabelType.Name;
                string exPeptideFormat = string.Format(REGEX_PEPTIDE_FORMAT, string.Join("|", heavyTypeNames.ToArray()));
                var exPeptideRegex = new Regex(exPeptideFormat);

                // Look for sequence column
                string sequence;
                IsotopeLabelType labelType;
                int iExPeptide = FindExPeptide(fields, exPeptideRegex, settings, out sequence, out labelType);
                // If no sequence column found, return null.  After this,
                // all errors throw.
                if (iExPeptide == -1)
                    return null;

                if (!labelType.IsLight && !modSettings.HasHeavyImplicitModifications)
                    throw new LineColNumberedIoException("Isotope labeled entry found without matching settings.\r\nCheck the Modifications tab in Transition Settings.", 1, iExPeptide);

                double tolerance = settings.TransitionSettings.Instrument.MzMatchTolerance;
                IList<TransitionExp> transitionExps;
                int iPrecursor = FindPrecursor(fields, sequence, labelType, iExPeptide,
                    tolerance, provider, settings, out transitionExps);
                if (iPrecursor == -1)
                    throw new MzMatchException("No valid precursor m/z column found.", 1, -1);

                int iProduct = FindProduct(fields, sequence, transitionExps, iExPeptide, iPrecursor,
                    tolerance, provider, settings);
                if (iProduct == -1)
                    throw new MzMatchException("No valid product m/z column found.", 1, -1);

                var indices = new ColumnIndices(iExPeptide, iExPeptide, iPrecursor, iProduct, iExPeptide);
                return new ExPeptideRowReader(provider, separator, indices, exPeptideRegex, settings);
            }

            private static int FindExPeptide(string[] fields, Regex exPeptideRegex, SrmSettings settings,
                out string sequence, out IsotopeLabelType labelType)
            {
                labelType = IsotopeLabelType.light;

                for (int i = 0; i < fields.Length; i++)
                {
                    Match match = exPeptideRegex.Match(fields[i]);
                    if (match.Success)
                    {
                        string sequencePart = match.Groups[2].Value;
                        if (FastaSequence.IsExSequence(sequencePart))
                        {
                            sequence = sequencePart;
                            labelType = GetLabelType(match, settings);
                            return i;
                        }
                        // Very strange case where there is a match, but it
                        // doesn't have a peptide in the second group.
                        break;
                    }
                }
                sequence = null;
                return -1;
            }

            private static IsotopeLabelType GetLabelType(Match pepExMatch, SrmSettings settings)
            {
                var modSettings = settings.PeptideSettings.Modifications;
                var typedMods = modSettings.GetModificationsByName(pepExMatch.Groups[3].Value);
                return (typedMods != null ? typedMods.LabelType : IsotopeLabelType.light);
            }
        }

        public static bool IsColumnar(string text,
            out IFormatProvider provider, out char sep, out Type[] columnTypes)
        {
            provider = CultureInfo.InvariantCulture;
            sep = '\0';
            int endLine = text.IndexOf('\n');
            string line = (endLine != -1 ? text.Substring(0, endLine) : text);
            string localDecimalSep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string[] columns;
            if (TrySplitColumns(line, '\t', out columns))
            {
                // If the current culture's decimal separator is different from the
                // invariant culture, and their are more occurances of the current
                // culture's decimal separator in the line, then use current culture.
                string invDecimalSep = CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator;
                if (!Equals(localDecimalSep, invDecimalSep))
                {
                    if (CountDecimals(columns, CultureInfo.CurrentCulture) >
                            CountDecimals(columns, CultureInfo.InvariantCulture))
                        provider = CultureInfo.CurrentCulture;
                }
                sep = '\t';
            }
            // Excel CSVs for cultures with a comma decimal use semi-colons.
            else if (Equals(",", localDecimalSep) && TrySplitColumns(line, ';', out columns))
            {
                provider = CultureInfo.CurrentCulture;
                sep = ';';                
            }
            else if (TrySplitColumns(line, ',', out columns))
            {
                sep = ',';                
            }

            if (sep == '\0')
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
        public ColumnIndices(int proteinColumn, int peptideColumn, int precursorColumn, int productColumn)
            : this(proteinColumn, peptideColumn, precursorColumn, productColumn, -1)            
        {
        }

        public ColumnIndices(int proteinColumn, int peptideColumn, int precursorColumn, int productColumn, int labelTypeColumn)
        {
            ProteinColumn = proteinColumn;
            PeptideColumn = peptideColumn;
            PrecursorColumn = precursorColumn;
            ProductColumn = productColumn;
            LabelTypeColumn = labelTypeColumn;
        }

        public int ProteinColumn { get; set; }
        public int PeptideColumn { get; set; }
        public int PrecursorColumn { get; set; }
        public int ProductColumn { get; set; }

        /// <summary>
        /// A column specifying the <see cref="IsotopeLabelType"/> (optional)
        /// </summary>
        public int LabelTypeColumn { get; set; }
    }

    /// <summary>
    /// All possible explanations for a single transition
    /// </summary>
    public sealed class ExTransitionInfo
    {
        public ExTransitionInfo(string proteinName, string peptideSequence, double precursorMz)
        {
            ProteinName = proteinName;
            PeptideSequence = peptideSequence;
            PrecursorMz = precursorMz;
            DefaultLabelType = IsotopeLabelType.light;
            TransitionExps = new List<TransitionExp>();
        }

        public string ProteinName { get; private set; }
        public string PeptideSequence { get; private set; }
        public double PrecursorMz { get; private set; }

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
        public TransitionExp(ExplicitMods mods, int precursorCharge, IsotopeLabelType labelType)
        {
            Precursor = new PrecursorExp(mods, precursorCharge, labelType);
        }

        public PrecursorExp Precursor { get; private set; }
        public ProductExp Product { get; set; }
    }

    public sealed class PrecursorExp
    {
        public PrecursorExp(ExplicitMods mods, int precursorCharge, IsotopeLabelType labelType)
        {
            VariableMods = mods;
            PrecursorCharge = precursorCharge;
            LabelType = labelType;
        }

        public ExplicitMods VariableMods { get; private set; }
        public int PrecursorCharge { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }

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
        public ProductExp(int productCharge, IonType ionType, int fragmentOrdinal, TransitionLosses losses)
        {
            Charge = productCharge;
            IonType = ionType;
            FragmentOrdinal = fragmentOrdinal;
            Losses = losses;
        }

        public int Charge { get; private set; }
        public IonType IonType { get; private set; }
        public int FragmentOrdinal { get; private set; }
        public TransitionLosses Losses { get; private set; }
    }
   
    public class MzMatchException : LineColNumberedIoException
    {
        private const string SUGGESTION = "\r\nCheck the Modification tab in the Peptide Settings, the m/z types on the Prediction tab, or the m/z match tolerance on the Instrument tab of the Transition Settings.";

        public MzMatchException(string message, long lineNum, int colNum) : base(message, SUGGESTION, lineNum, colNum) { }
    }

    public class LineColNumberedIoException : IOException
    {
        public LineColNumberedIoException(string message, long lineNum, int colIndex)
            : base(FormatMessage(message, lineNum, colIndex))
        {
            PlainMessage = message + ".";
            LineNumber = lineNum;
            ColumnIndex = colIndex;
        }

        public LineColNumberedIoException(string message, string suggestion, long lineNum, int colIndex)
            : base(FormatMessage(message, lineNum, colIndex) + "\r\n" + suggestion)
        {
            PlainMessage = message + ".\r\n" + suggestion;
            LineNumber = lineNum;
            ColumnIndex = colIndex;
        }

        public LineColNumberedIoException(string message, long lineNum, int colIndex, Exception inner)
            : base(FormatMessage(message, lineNum, colIndex), inner)
        {
            PlainMessage = message + ".";
            LineNumber = lineNum;
            ColumnIndex = colIndex;
        }

        private static string FormatMessage(string message, long lineNum, int colIndex)
        {
            if (colIndex == -1)
                return string.Format("{0}, line {1}.", message, lineNum);
            else
                return string.Format("{0}, line {1}, col {2}.", message, lineNum, colIndex + 1);
        }

        public string PlainMessage { get; private set; }
        public long LineNumber { get; private set; }
        public int ColumnIndex { get; private set; }
    }

    public class PeptideGroupBuilder
    {
        private readonly StringBuilder _sequence = new StringBuilder();
        private readonly List<PeptideDocNode> _peptides;
        private readonly SrmSettings _settings;
        private readonly Enzyme _enzyme;
        private readonly bool _customName;

        private FastaSequence _activeFastaSeq;
        private Peptide _activePeptide;
        // Order is important to making the variable modification choice deterministic
        // when more than one potential set of variable modifications work to explain
        // the contents of the active peptide.
        private List<ExplicitMods> _activeVariableMods;
        private List<PrecursorExp> _activePrecursorExps;
        private double _activePrecursorMz;
        private readonly List<ExTransitionInfo> _activeTransitionInfos;
        private readonly List<TransitionGroupDocNode> _transitionGroups;

        private readonly ModificationMatcher _modMatcher;
        private bool _hasExplicitMods;

        public PeptideGroupBuilder(FastaSequence fastaSequence, SrmSettings settings)
        {
            _activeFastaSeq = fastaSequence;
            if (fastaSequence != null)
            {
                BaseName = Name = fastaSequence.Name;
                Description = fastaSequence.Description;
                Alternatives = fastaSequence.Alternatives.ToArray();
            }
            _settings = settings;
            _enzyme = _settings.PeptideSettings.Enzyme;
            _peptides = new List<PeptideDocNode>();
            _transitionGroups = new List<TransitionGroupDocNode>();
            _activeTransitionInfos = new List<ExTransitionInfo>();
        }

        public PeptideGroupBuilder(string line, bool peptideList, SrmSettings settings)
            : this(null, settings)
        {
            int start = (line.Length > 0 && line[0] == '>' ? 1 : 0);
            // If there is a second >, then this is a custom name, and not
            // a real FASTA sequence.
            if (line.Length > 1 && line[1] == '>')
            {
                _customName = true;
                start++;
            }
            // Split ID from description at first space or tab
            int split = IndexEndId(line);
            if (split == -1)
            {
                BaseName = Name = line.Substring(start);
                Description = "";
            }
            else
            {
                BaseName = Name = line.Substring(start, split - start);
                string[] descriptions = line.Substring(split + 1).Split((char)1);
                Description = descriptions[0];
                var listAlternatives = new List<AlternativeProtein>();
                for (int i = 1; i < descriptions.Length; i++)
                {
                    string alternative = descriptions[i];
                    split = IndexEndId(alternative);
                    if (split == -1)
                        listAlternatives.Add(new AlternativeProtein(alternative, null));
                    else
                    {
                        listAlternatives.Add(new AlternativeProtein(alternative.Substring(0, split),
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
            return line.IndexOfAny(new[] {' ', '\t'});
        }

        /// <summary>
        /// Used in the case where the user supplied name may be different
        /// from the <see cref="Name"/> property.
        /// </summary>
        public string BaseName { get; set; }

        public string Name { get; private set; }
        public string Description { get; private set; }
        public AlternativeProtein[] Alternatives { get; private set; }
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
            var seq = FastaSequence.StripModifications(seqMod);
            _hasExplicitMods = _hasExplicitMods || !Equals(seq, seqMod);
            // Get rid of whitespace
            seq = seq.Replace(" ", "").Trim();
            // Get rid of 
            if (seq.EndsWith("*"))
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
                    nodePep = new PeptideDocNode(peptide, new TransitionGroupDocNode[0]);
                }
                _peptides.Add(nodePep);
            }
        }

        public void AppendTransition(ExTransitionInfo info)
        {
            // Treat this like a peptide list from now on.
            PeptideList = true;

            if (_activeFastaSeq == null && AA.Length > 0)
                _activeFastaSeq = new FastaSequence(Name, Description, Alternatives, AA);

            string sequence = info.PeptideSequence;
            if (_activePeptide != null)
            {
                if (!Equals(sequence, _activePeptide.Sequence))
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
                            intersectVariableMods = new List<ExplicitMods>(_activeVariableMods.Intersect(
                                infoActive.PotentialVarMods));
                        }
                        
                    }

                    if (intersectVariableMods.Count > 0)
                    {
                        _activeVariableMods = intersectVariableMods;
                    }
                    else if (_activePrecursorMz == info.PrecursorMz)
                    {
                        throw new InvalidDataException(string.Format("Failed to explain all transitions for {0} m/z {1} with a single set of modifications", sequence, _activePrecursorMz));
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
                        // CONSIDER: Use fasta sequence format code currently in SrmDocument to show formatted sequence.
                        throw new InvalidDataException(string.Format("The peptide {0} was not found in the sequence {1}.", sequence, _activeFastaSeq.Name));
                    end = begin + sequence.Length;
                }
                _activePeptide = new Peptide(_activeFastaSeq, sequence, begin, end, _enzyme.CountCleavagePoints(sequence));
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
                throw new InvalidDataException(string.Format("Failed to explain all transitions for m/z {0} with a single precursor", _activePrecursorMz));
            }
            else
            {
                CompleteTransitionGroup();
            }
            if (_activePrecursorMz == 0)
            {
                _activePrecursorMz = info.PrecursorMz;
                _activePrecursorExps = new List<PrecursorExp>(info.TransitionExps.Select(exp => exp.Precursor));
            }
            _activeTransitionInfos.Add(info);
        }

        private void CompletePeptide(bool andTransitionGroup)
        {
            if (andTransitionGroup)
                CompleteTransitionGroup();

            _transitionGroups.Sort(Peptide.CompareGroups);
            _peptides.Add(new PeptideDocNode(_activePeptide, _activeVariableMods[0],
                FinalizeTransitionGroups(_transitionGroups), false));

            _transitionGroups.Clear();

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
                                             _activePeptide.MissedCleavages);
            }
        }

        private static TransitionGroupDocNode[] FinalizeTransitionGroups(IList<TransitionGroupDocNode> groups)
        {
            var finalGroups = new List<TransitionGroupDocNode>();
            foreach (var nodeGroup in groups)
            {
                int iGroup = finalGroups.Count - 1;
                if (iGroup == -1 || !Equals(finalGroups[iGroup].TransitionGroup, nodeGroup.TransitionGroup))
                    finalGroups.Add(nodeGroup);
                else
                {
                    // Found repeated group, so merge transitions
                    foreach (var nodeTran in nodeGroup.Children)
                        finalGroups[iGroup] = (TransitionGroupDocNode) finalGroups[iGroup].Add(nodeTran);
                }
            }

            // If anything changed, make sure transitions are sorted
            if (!ArrayUtil.ReferencesEqual(groups, finalGroups))
            {
                for (int i = 0; i < finalGroups.Count; i++)
                {
                    var nodeGroup = finalGroups[i];
                    var arrayTran = CompleteTransitions(nodeGroup.Children.Cast<TransitionDocNode>());
                    finalGroups[i] = (TransitionGroupDocNode) nodeGroup.ChangeChildrenChecked(arrayTran);
                }
            }
            return finalGroups.ToArray();
        }

        private void CompleteTransitionGroup()
        {
            // Just use the first precursor explanation, even if there are multiple
            var precursorExp = _activePrecursorExps[0];
            var transitionGroup = new TransitionGroup(_activePeptide,
                precursorExp.PrecursorCharge, precursorExp.LabelType);
            var transitions = _activeTransitionInfos.ConvertAll(info =>
                {
                    var productExp = info.TransitionExps.Single(exp => Equals(precursorExp, exp.Precursor)).Product;
                    var ionType = productExp.IonType;
                    var ordinal = productExp.FragmentOrdinal;
                    int offset = Transition.OrdinalToOffset(ionType, ordinal, _activePeptide.Sequence.Length);
                    var tran = new Transition(transitionGroup, ionType, offset, 0, productExp.Charge);
                    // m/z and library info calculated later
                    return new TransitionDocNode(tran, productExp.Losses, 0, null, null);
                });
            // m/z calculated later
            _transitionGroups.Add(new TransitionGroupDocNode(transitionGroup, CompleteTransitions(transitions)));

            _activePrecursorMz = 0;
            _activePrecursorExps.Clear();
            _activeTransitionInfos.Clear();
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
            PeptideGroupDocNode nodeGroup;
            SrmSettingsDiff diff = SrmSettingsDiff.ALL;
            if (PeptideList)
            {
                if (_activePeptide != null)
                {
                    CompletePeptide(true);
                    diff = SrmSettingsDiff.PROPS;
                }
                nodeGroup = new PeptideGroupDocNode(_activeFastaSeq ?? new PeptideGroup(),
                    Name, Description, _peptides.ToArray());
            }
            else if (_customName)
            {
                nodeGroup = new PeptideGroupDocNode(
                    new FastaSequence(null, null, Alternatives, _sequence.ToString()),
                    Name, Description, new PeptideDocNode[0]);                
            }
            else
            {
                nodeGroup = new PeptideGroupDocNode(
                    new FastaSequence(Name, Description, Alternatives, _sequence.ToString()),
                    null, null, new PeptideDocNode[0]);
            }
            if (_hasExplicitMods)
                nodeGroup = (PeptideGroupDocNode) nodeGroup.ChangeAutoManageChildren(false);
            // Materialize children, so that we have accurate accounting of
            // peptide and transition counts.
            return nodeGroup.ChangeSettings(_settings, diff);
        }
    }
}
