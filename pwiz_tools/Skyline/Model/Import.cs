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
using System.Runtime.Serialization;
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

        public FastaImporter(SrmDocument document, bool peptideList)
        {
            Document = document;
            PeptideList = peptideList;
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
                    if (longWaitBroker.IsCanceled)
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

                    seqBuilder = new PeptideGroupBuilder(line, PeptideList, Document.Settings);
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
                    throw new InvalidDataException(string.Format("Too few columns found, line {0}", lineNum));
                string seq = columns[columns.Length - 1].Trim();
                if (!FastaSequence.IsExSequence(seq))
                    throw new InvalidDataException(string.Format("Last column does not contain a valid protein sequence, line {0}", lineNum));
                sb.Append(">").Append(columns[0].Trim().Replace(" ", "_")); // ID
                for (int i = 1; i < columns.Length - 1; i++)
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

        private int _countPeptides;
        private int _countIons;

        public MassListImporter(SrmDocument document, IFormatProvider provider, char separator)
        {
            Document = document;
            FormatProvider = provider;
            Separator = separator;
        }

        public SrmDocument Document { get; private set; }
        public IFormatProvider FormatProvider { get; private set; }
        public char Separator { get; private set; }

        public IEnumerable<PeptideGroupDocNode> Import(TextReader reader, string textSeq)
        {
            // Check first line for validity
            string line = reader.ReadLine();
            if (line == null)
                throw new InvalidDataException("Empty mass list.");
            string[] fields = line.ParseDsvFields(Separator);
            if (fields.Length < 3)
                throw new InvalidDataException("Invalid mass list.  Mass lists must contain at least precursor m/z, product m/z, and peptide sequence.");

            double mzMatchTolerance = Document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            try
            {
                return ImportPeptideGroups(reader, line, textSeq, mzMatchTolerance);
            }
            catch (MzMatchException x)
            {
                // TODO: Test different mass tolerance levels up to the maximum to determine
                //       if one would work, and give a more detailed error message pointing
                //       the user to the tolerance setting.  This would have to be done where
                //       the reader can be reset to the beginning.
                throw new InvalidDataException(string.Format("{0}\n\nCheck the Modification tab in the Peptide Settings, the m/z types on the Prediction tab, or the m/z match tolerance on the Instrument tab of the Transition Settings.", x.Message), x);
            }
        }

        private List<PeptideGroupDocNode> ImportPeptideGroups(TextReader reader, string line, string textSeq, double tolerance)
        {
            // Get the lines used to guess the necessary columns.
            List<string> lines = new List<string> { line };
            MassListRowReader rowReader = ExPeptideRowReader.Create(lines, FormatProvider, Separator, tolerance, Document.Settings);
            if (rowReader == null)
            {
                for (int i = 1; i < INSPECT_LINES; i++)
                {
                    line = reader.ReadLine();
                    if (line == null)
                        break;
                    lines.Add(line);
                }
                rowReader = GeneralRowReader.Create(lines, null, FormatProvider, Separator, tolerance, Document.Settings);
                if (rowReader == null)
                {
                    // Check for a possible header row
                    string[] headers = lines[0].Split(Separator);
                    lines.RemoveAt(0);
                    rowReader = GeneralRowReader.Create(lines, headers, FormatProvider, Separator, tolerance, Document.Settings);
                    if (rowReader == null)
                        throw new InvalidDataException("Failed to find peptide column.");
                }
            }

            // Set starting values for limit counters
            _countPeptides = Document.PeptideCount;
            _countIons = Document.TransitionCount;

            // Store set of existing group names
            HashSet<string> set = new HashSet<string>();
            foreach (PeptideGroupDocNode nodeGroup in Document.Children)
                set.Add(nodeGroup.Name);
            // And set of FASTA sequences, which will be empty unless fasta sequence
            // information is available for this list.
            HashSet<FastaSequence> setSeq = new HashSet<FastaSequence>();

            List<PeptideGroupDocNode> peptideGroupsNew = new List<PeptideGroupDocNode>();
            PeptideGroupBuilder seqBuilder = null;
            string currentName;
            if (string.IsNullOrEmpty(textSeq))
                currentName = GetSafeGroupName("peptides", 1, set);
            else
            {
                // If there is sequence information, preserve it in the active builder.
                string[] linesSeq = textSeq.Split(new[] { '\n' });
                seqBuilder = new PeptideGroupBuilder(linesSeq[0], false, Document.Settings);
                for (int i = 1; i < linesSeq.Length; i++)
                    seqBuilder.AppendSequence(linesSeq[i]);
                currentName = seqBuilder.Name;

                // Store set of existing FASTA sequences to keep from duplicating
                foreach (PeptideGroupDocNode nodeGroup in Document.Children)
                {
                    FastaSequence fastaSeq = nodeGroup.Id as FastaSequence;
                    if (fastaSeq != null)
                        setSeq.Add(fastaSeq);
                }
            }

            // First process lines already read
            int lineNum = 1;
            foreach (string lineRead in lines)
            {
                rowReader.NextRow(lineRead, lineNum++);
                seqBuilder = AddRow(seqBuilder, ref currentName, rowReader, set, setSeq, peptideGroupsNew);
            }

            // Process remaining lines
            while ((line = reader.ReadLine()) != null)
            {
                rowReader.NextRow(line, lineNum++);
                seqBuilder = AddRow(seqBuilder, ref currentName, rowReader, set, setSeq, peptideGroupsNew);
            }

            // Add last sequence.
            if (seqBuilder != null)
                AddPeptideGroup(peptideGroupsNew, setSeq, seqBuilder);
            return peptideGroupsNew;
        }

        private PeptideGroupBuilder AddRow(PeptideGroupBuilder seqBuilder, ref string currentName,
            IMassListRow rowReader, ICollection<string> set, ICollection<FastaSequence> setSeq,
            ICollection<PeptideGroupDocNode> peptideGroupsNew)
        {
            string name = rowReader.ProteinName;
            if (seqBuilder == null || (name != null && !currentName.Equals(name)))
            {
                if (seqBuilder != null)
                    AddPeptideGroup(peptideGroupsNew, setSeq, seqBuilder);
                if (name != null)
                    currentName = name;
                name = GetSafeGroupName(currentName, set);
                seqBuilder = new PeptideGroupBuilder(">>" + name, true, Document.Settings);
            }
            seqBuilder.AppendTransition(rowReader);
            return seqBuilder;
        }

        private static string GetSafeGroupName(string name, ICollection<string> set)
        {
            if (set.Contains(name))
                return GetSafeGroupName(name, 1, set);
            return name;
        }

        private static string GetSafeGroupName(string name, int num, ICollection<string> set)
        {
            while (set.Contains(name + num))
                num++;
            return name + num;
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

        private sealed class ExTransitionInfo
        {
            public ExTransitionInfo()
            {
                LabelType = IsotopeLabelType.light;
            }

            public string ProteinName { get; set; }
            public string PeptideSequence { get; set; }
            public int? ProductCharge { get; set; }
            public IonType? IonType { get; set; }
            public int? FragmentOrdinal { get; set; }
            public TransitionLosses Losses { get; set; }
            public IsotopeLabelType LabelType { get; set; }
            public bool LabelTypeExplicit { get; set; }
        }

        private abstract class MassListRowReader : IMassListRow
        {
            protected MassListRowReader(IFormatProvider provider, char separator,
                int precursorColumn, int productColumn, double tolerance, SrmSettings settings)
            {
                FormatProvider = provider;
                Separator = separator;
                PrecursorColumn = precursorColumn;
                ProductColumn = productColumn;
                MzMatchTolerance = tolerance;
                Settings = settings;
            }

            protected SrmSettings Settings { get; private set; }
            protected string[] Fields { get; private set; }
            protected IFormatProvider FormatProvider { get; private set; }
            private char Separator { get; set; }
            protected int PrecursorColumn { get; private set; }
            private int ProductColumn { get; set; }

            protected double MzMatchTolerance { get; private set; }

            // PeptideGroup
            public string ProteinName { get; private set; }
            // Peptide
            public string PeptideSequence { get; private set; }
            // TransitionGrup
            public int PrecursorCharge { get; private set; }
            public IsotopeLabelType LabelType { get; private set; }
            // Transition
            public IonType IonType { get; private set; }
            public int Ordinal { get; private set; }
            public TransitionLosses Losses { get; private set; }
            public int Offset { get { return Transition.OrdinalToOffset(IonType, Ordinal, PeptideSequence.Length); } }
            public int ProductCharge { get; private set; }

            protected bool IsHeavyAllowed
            {
                get { return Settings.PeptideSettings.Modifications.HasHeavyImplicitModifications; }
            }

            public void NextRow(string line, int lineNum)
            {
                Fields = line.ParseDsvFields(Separator);

                ExTransitionInfo info = CalcTransitionInfo(lineNum);
                if (!FastaSequence.IsExSequence(info.PeptideSequence))
                    throw new InvalidDataException(string.Format("Invalid peptide sequence {0} found, line {1}.", info.PeptideSequence, lineNum));

                if (!info.LabelType.IsLight && !IsHeavyAllowed)
                    throw new InvalidDataException(string.Format("Isotope labeled entry found without matching settings, line {0}.\nCheck the Modifications tab in Transition Settings.", lineNum));

                string seq = info.PeptideSequence;
                double precursorMassH = Settings.GetPrecursorMass(info.LabelType, info.PeptideSequence, null);
                double precursorMz = ColumnMz(Fields, PrecursorColumn, FormatProvider);
                var instrument = Settings.TransitionSettings.Instrument;
                if (!instrument.IsMeasurable(precursorMz))
                    throw new InvalidDataException(string.Format("The precursor m/z {0} is out of range for the instrument settings, line {1}.\nCheck the Instrument tab in the Transition Settings.", precursorMz, lineNum));

                int precursorCharge = CalcPrecursorCharge(precursorMassH, precursorMz, MzMatchTolerance);
                if (precursorCharge < 1)
                {
                    double nearestMz = SequenceMassCalc.GetMZ(precursorMassH, -precursorCharge);
                    if (!info.LabelType.IsLight && !info.LabelTypeExplicit)
                    {
                        // Need to check the light version also for the closest possible value
                        precursorMassH = Settings.GetPrecursorMass(IsotopeLabelType.light, info.PeptideSequence, null);
                        precursorCharge = CalcPrecursorCharge(precursorMassH, precursorMz, 0);
                        double nearestMzLight = SequenceMassCalc.GetMZ(precursorMassH, -precursorCharge);
                        if (Math.Abs(nearestMzLight - precursorMz) < Math.Abs(nearestMz - precursorMz))
                            nearestMz = nearestMzLight;
                    }
                    // TODO: Consistent central formatting for m/z values
                    // Use Math.Round() to avoid forcing extra decimal places
                    nearestMz = Math.Round(nearestMz, 4);
                    precursorMz = Math.Round(SequenceMassCalc.PersistentMZ(precursorMz), 4);
                    double deltaMz = Math.Round(Math.Abs(precursorMz - nearestMz), 4);
                    throw new MzMatchException(string.Format("Precursor m/z {0} does not match the closest possible value {1} (delta = {2}), line {3}.", precursorMz, nearestMz, deltaMz, lineNum));
                }

                double productMz = ColumnMz(Fields, ProductColumn, FormatProvider);
                if (!instrument.IsMeasurable(productMz, precursorMz))
                    throw new InvalidDataException(string.Format("The product m/z value {0} is out of range for the instrument settings, line {1}.\nCheck the Instrument tab in the Transition Settings.", productMz, lineNum));

                var calc = Settings.GetFragmentCalc(info.LabelType, null);
                if (info.IonType.HasValue)
                {
                    double productMassHTry = calc.GetFragmentMass(seq, info.IonType.Value, info.FragmentOrdinal.Value);
                    double productMzTry = SequenceMassCalc.GetMZ(productMassHTry, info.ProductCharge.Value);
                    if (!MatchMz(productMz, productMzTry, MzMatchTolerance))
                        info.ProductCharge = 0;
                }
                else
                {
                    double productPrecursorMass = calc.GetPrecursorFragmentMass(seq);
                    double[,] productMasses = calc.GetFragmentIonMasses(seq);
                    // TODO: Allow variable modifications
                    var potentialLosses = TransitionGroup.CalcPotentialLosses(seq,
                        Settings.PeptideSettings.Modifications, null, calc.MassType);
                    
                    IonType? ionType;
                    int? ordinal;
                    TransitionLosses losses;
                    info.ProductCharge = TransitionCalc.CalcProductCharge(productPrecursorMass, precursorCharge,
                        productMasses, potentialLosses, productMz, MzMatchTolerance, calc.MassType,
                        out ionType, out ordinal, out losses);

                    if (info.ProductCharge > 0)
                    {
                        info.IonType = ionType;
                        info.FragmentOrdinal = ordinal;
                        info.Losses = losses;
                    }
                }
                if (info.ProductCharge < 1)
                {
                    // TODO: Consistent central formatting for m/z values
                    // Use Math.Round() to avoid forcing extra decimal places
                    productMz = Math.Round(productMz, 4);
                    throw new MzMatchException(string.Format("Product m/z value {0} has no matching product ion, line {1}.", productMz, lineNum));
                }

                ProteinName = info.ProteinName;
                PeptideSequence = info.PeptideSequence;
                PrecursorCharge = precursorCharge;
                IonType = info.IonType.Value;
                Ordinal = info.FragmentOrdinal.Value;
                Losses = info.Losses;
                LabelType = info.LabelType;
                ProductCharge = info.ProductCharge.Value;
            }

            protected abstract ExTransitionInfo CalcTransitionInfo(int lineNum);

            protected static int CalcPrecursorCharge(double precursorMassH, double precursorMz, double tolerance)
            {
                return TransitionCalc.CalcPrecursorCharge(precursorMassH, precursorMz, tolerance);
            }

            private static bool MatchMz(double mz1, double mz2, double tolerance)
            {
                return MatchMz(Math.Abs(mz1 - mz2), tolerance);
            }

            private static bool MatchMz(double delta, double tolerance)
            {
                return (delta <= tolerance);                
            }

            protected static double ColumnMz(string[] fields, int column, IFormatProvider provider)
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

            protected static int FindPrecursor(string[] fields, string sequence,
                IsotopeLabelType labelType, int iSequence, double tolerance,
                IFormatProvider provider, SrmSettings settings, out int precursorCharge)
            {
                double precursorMassH = settings.GetPrecursorMass(labelType, sequence, null);

                for (int i = 0; i < fields.Length; i++)
                {
                    if (i == iSequence)
                        continue;

                    double precursorMz = ColumnMz(fields, i, provider);
                    if (precursorMz == 0)
                        continue;

                    int charge = CalcPrecursorCharge(precursorMassH, precursorMz, tolerance);
                    if (charge > 0)
                    {
                        precursorCharge = charge;
                        return i;
                    }
                }
                precursorCharge = 0;
                return -1;
            }

            protected static int FindProduct(string[] fields, string sequence,
                IsotopeLabelType labelType, int iSequence, int iPrecursor, int precursoCharge, double tolerance,
                IFormatProvider provider, SrmSettings settings)
            {
                var calc = settings.GetFragmentCalc(labelType, null);
                double productPrecursorMass = calc.GetPrecursorFragmentMass(sequence);
                double[,] productMasses = calc.GetFragmentIonMasses(sequence);
                // TODO: Allow variable modifications
                var potentialLosses = TransitionGroup.CalcPotentialLosses(sequence,
                    settings.PeptideSettings.Modifications, null, calc.MassType);

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
                    int charge = TransitionCalc.CalcProductCharge(productPrecursorMass, precursoCharge,
                        productMasses, potentialLosses, productMz, tolerance, calc.MassType,
                        out ionType, out ordinal, out losses);
                    if (charge > 0)
                        return i;
                }

                return -1;
            }
        }

        private class GeneralRowReader : MassListRowReader
        {
            private GeneralRowReader(IFormatProvider provider, char separator, int peptideColumn, int proteinColumn,
                    int precursorColumn, int productColumn, int labelTypeColumn, double tolerance, SrmSettings settings)
                : base(provider, separator, precursorColumn, productColumn, tolerance, settings)
            {
                PeptideColumn = peptideColumn;
                ProteinColumn = proteinColumn;
                LabelTypeColumn = labelTypeColumn;
            }

            private int PeptideColumn { get; set; }
            private int ProteinColumn { get; set; }
            private int LabelTypeColumn { get; set; }

            private static IsotopeLabelType GetLabelType(string typeId)
            {
                return (Equals(typeId, "H") ? IsotopeLabelType.heavy : IsotopeLabelType.light);
            }

            protected override ExTransitionInfo CalcTransitionInfo(int lineNum)
            {
                ExTransitionInfo info = new ExTransitionInfo
                { PeptideSequence = RemoveSequenceNotes(Fields[PeptideColumn]) };

                if (ProteinColumn != -1)
                    info.ProteinName = Fields[ProteinColumn];
                if (LabelTypeColumn != -1)
                {
                    info.LabelType = GetLabelType(Fields[LabelTypeColumn]);
                    info.LabelTypeExplicit = true;                    
                }
                else
                {
                    // If no isotope label type can be found, and the current
                    // precursor m/z column does not match with no label, try heavy types.
                    double precursorMassH = Settings.GetPrecursorMass(info.LabelType, info.PeptideSequence, null);
                    double precursorMz = ColumnMz(Fields, PrecursorColumn, FormatProvider);

                    int precursorCharge = CalcPrecursorCharge(precursorMassH, precursorMz, MzMatchTolerance);
                    if (IsHeavyAllowed && precursorCharge < 1)
                    {
                        var modifications = Settings.PeptideSettings.Modifications;
                        foreach (var typeMods in modifications.GetHeavyModifications())
                        {
                            info.LabelType = typeMods.LabelType;
                            precursorMassH = Settings.GetPrecursorMass(info.LabelType, info.PeptideSequence, null);
                            precursorCharge = TransitionCalc.CalcPrecursorCharge(precursorMassH, precursorMz, MzMatchTolerance);
                            if (precursorCharge > 0)
                                break;
                        }
                        
                    }
                }

                return info;
            }

            public static GeneralRowReader Create(IList<string> lines, IList<string> headers,
                IFormatProvider provider, char separator, double tolerance, SrmSettings settings)
            {
                // Split the first line into fields.
                Debug.Assert(lines.Count > 0);
                string[] fields = lines[0].ParseDsvFields(separator);

                int iLabelType = FindLabelType(fields, lines, separator);

                // Look for sequence column
                string sequence;
                int iSequence = -1;
                int iPrecursor;
                int precursorCharge;
                IsotopeLabelType labelType;
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
                            throw new MzMatchException("No valid precursor m/z column found.");
                        return null;
                    }

                    labelType = IsotopeLabelType.light;
                    if (iLabelType != -1)
                        labelType = GetLabelType(fields[iLabelType]);
                    iPrecursor = FindPrecursor(fields, sequence, labelType, iSequence,
                        tolerance, provider, settings, out precursorCharge);
                    // If no match, and no specific label type, then try heavy.
                    if (settings.PeptideSettings.Modifications.HasHeavyModifications &&
                            iPrecursor == -1 && iLabelType == -1)
                    {
                        var modifications = settings.PeptideSettings.Modifications;
                        foreach (var typeMods in modifications.GetHeavyModifications())
                        {
                            if (settings.GetPrecursorCalc(labelType, null) != null)
                            {
                                iPrecursor = FindPrecursor(fields, sequence, typeMods.LabelType, iSequence,
                                                           tolerance, provider, settings, out precursorCharge);
                                if (iPrecursor != -1)
                                {
                                    labelType = typeMods.LabelType;
                                    break;                                    
                                }
                            }
                        }
                    }
                }
                while (iPrecursor == -1);

                int iProduct = FindProduct(fields, sequence, labelType, iSequence, iPrecursor, precursorCharge,
                    tolerance, provider, settings);
                if (iProduct == -1)
                    throw new MzMatchException("No valid product m/z column found.");

                int iProtein = FindProtein(fields, iSequence, lines, headers, provider, separator);

                return new GeneralRowReader(provider, separator, iSequence, iProtein,
                    iPrecursor, iProduct, iLabelType, tolerance, settings);
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
                    try
                    {
                        double.Parse(fieldValue, provider);
                    }
                    catch (FormatException)
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

            private ExPeptideRowReader(IFormatProvider provider, char separator,
                    int exPeptideColumn, Regex exPeptideRegex, int precursorColumn, int productColumn,
                    double tolerance, SrmSettings settings)
                : base(provider, separator, precursorColumn, productColumn, tolerance, settings)
            {
                ExPeptideColumn = exPeptideColumn;
                ExPeptideRegex = exPeptideRegex;
            }

            private int ExPeptideColumn { get; set; }
            private Regex ExPeptideRegex { get; set; }

            protected override ExTransitionInfo CalcTransitionInfo(int lineNum)
            {
                string exPeptide = Fields[ExPeptideColumn];
                Match match = ExPeptideRegex.Match(exPeptide);
                if (!match.Success)
                    throw new InvalidDataException(string.Format("Invalid extended peptide format {0}, line {1}.", exPeptide, lineNum));

                try
                {
                    ExTransitionInfo info = new ExTransitionInfo
                        {
                            ProteinName = match.Groups[1].Value,
                            PeptideSequence = match.Groups[2].Value,

//                            After further dicussing with Jeff Whiteaker, it turns out these
//                            values were more convention than anything reliable that should be
//                            relied up during import.

//                            ProductCharge = int.Parse(match.Groups[3].Value),
//                            IonType = (IonType) Enum.Parse(typeof (IonType), match.Groups[4].Value.ToLower()),
//                            FragmentOrdinal = int.Parse(match.Groups[5].Value),

                            LabelType = GetLabelType(match, Settings),
                            LabelTypeExplicit = true
                        };
                    return info;
                }
                catch (Exception)
                {
                    throw new InvalidDataException(string.Format("Invalid extended peptide format {0}, line {1}.", exPeptide, lineNum));
                }
            }

            public static ExPeptideRowReader Create(IList<string> lines,
                IFormatProvider provider, char separator, double tolerance, SrmSettings settings)
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
                    throw new InvalidDataException("Isotope labelled entry found without matching settings.\nCheck the Modifications tab in Transition Settings.");


                int precursorCharge;
                int iPrecursor = FindPrecursor(fields, sequence, labelType, iExPeptide,
                    tolerance, provider, settings, out precursorCharge);
                if (iPrecursor == -1)
                    throw new MzMatchException("No valid precursor m/z column found.");

                int iProduct = FindProduct(fields, sequence, labelType, iExPeptide, iPrecursor,
                    precursorCharge, tolerance, provider, settings);
                if (iProduct == -1)
                    throw new MzMatchException("No valid product m/z column found.");

                return new ExPeptideRowReader(provider, separator, iExPeptide, exPeptideRegex,
                    iPrecursor, iProduct, tolerance, settings);
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
                    if (line.Split(new[] { localDecimalSep }, StringSplitOptions.None).Length >
                            line.Split(new[] { invDecimalSep }, StringSplitOptions.None).Length)
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

    public interface IMassListRow
    {
        // PeptideGroup
        string ProteinName { get; }
        // Peptide
        string PeptideSequence { get; }
        // TransitionGrup
        int PrecursorCharge { get; }
        IsotopeLabelType LabelType { get; }
        // Transition
        IonType IonType { get; }
        int Ordinal { get; }
        int Offset { get; }
        int ProductCharge { get; }
        TransitionLosses Losses { get; }
    }

    
    [Serializable]
    public class MzMatchException : IOException
    {
        public MzMatchException() { }
        public MzMatchException( string message ) : base( message ) { }
        public MzMatchException( string message, Exception inner ) : base( message, inner ) { }
        protected MzMatchException(SerializationInfo info, StreamingContext context ) : base( info, context ) { }
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
        private List<TransitionGroupDocNode> _transitionGroups;
        private TransitionGroup _activeTransitionGroup;
        private List<TransitionDocNode> _transitions;

        public PeptideGroupBuilder(string line, bool peptideList, SrmSettings settings)
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
                Name = line.Substring(start);
                Description = "";
            }
            else
            {
                Name = line.Substring(start, split - start);
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

            _settings = settings;
            _enzyme = _settings.PeptideSettings.Enzyme;
            _peptides = new List<PeptideDocNode>();
            PeptideList = peptideList;
        }

        private static int IndexEndId(string line)
        {
            return line.IndexOfAny(new[] {' ', '\t'});
        }

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

        public void AppendSequence(string seq)
        {
            // Get rid of whitespace
            seq = seq.Replace(" ", "").Trim();
            // Get rid of 
            if (seq.EndsWith("*"))
                seq = seq.Substring(0, seq.Length - 1);

            if (!PeptideList)
                _sequence.Append(seq);
            else
            {
                Peptide peptide = new Peptide(null, seq, null, null, _enzyme.CountCleavagePoints(seq));
                _peptides.Add(new PeptideDocNode(peptide, new TransitionGroupDocNode[0]));
            }
        }

        public void AppendTransition(IMassListRow row)
        {
            // Treat this like a peptide list from now on.
            PeptideList = true;

            if (_activeFastaSeq == null && AA.Length > 0)
                _activeFastaSeq = new FastaSequence(Name, Description, Alternatives, AA);

            string seq = row.PeptideSequence;
            if (_activePeptide != null && !seq.Equals(_activePeptide.Sequence))
                CompletePeptide();
            if (_activePeptide == null)
            {
                int? begin = null;
                int? end = null;
                if (_activeFastaSeq != null)
                {
                    begin = _activeFastaSeq.Sequence.IndexOf(seq);
                    if (begin == -1)
                        // CONSIDER: Use fasta sequence format code currently in SrmDocument to show formatted sequence.
                        throw new InvalidDataException(string.Format("The peptide {0} was not found in the sequence {1}.", seq, _activeFastaSeq.Name));
                    end = begin + seq.Length;
                }
                _activePeptide = new Peptide(_activeFastaSeq, seq, begin, end, _enzyme.CountCleavagePoints(seq));
                _transitionGroups = new List<TransitionGroupDocNode>();
            }
            if (_activeTransitionGroup != null &&
                    (row.PrecursorCharge != _activeTransitionGroup.PrecursorCharge ||
                     row.LabelType != _activeTransitionGroup.LabelType))
                CompleteTransitionGroup();
            if (_activeTransitionGroup == null)
            {
                _activeTransitionGroup = new TransitionGroup(_activePeptide, row.PrecursorCharge, row.LabelType);
                _transitions = new List<TransitionDocNode>();
            }
            var tran = new Transition(_activeTransitionGroup, row.IonType, row.Offset, row.ProductCharge);
            // m/z and library info calculated later
            _transitions.Add(new TransitionDocNode(tran, row.Losses, 0, null));
        }

        private void CompletePeptide()
        {
            CompleteTransitionGroup();

            _transitionGroups.Sort(Peptide.CompareGroups);
            _peptides.Add(new PeptideDocNode(_activePeptide, FinalizeTransitionGroups(_transitionGroups), false));

            _activePeptide = null;
            _transitionGroups = null;
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
                    var listTran = new List<TransitionDocNode>();
                    foreach (TransitionDocNode nodeTran in finalGroups[i].Children)
                        listTran.Add(nodeTran);
                    listTran.Sort(TransitionGroup.CompareTransitions);

                    finalGroups[i] = (TransitionGroupDocNode)
                        nodeGroup.ChangeChildrenChecked(listTran.ToArray());
                }
            }
            return finalGroups.ToArray();
        }

        private void CompleteTransitionGroup()
        {
            _transitions.Sort(TransitionGroup.CompareTransitions);
            // m/z calculated later
            _transitionGroups.Add(new TransitionGroupDocNode(_activeTransitionGroup, 0, RelativeRT.Matching, _transitions.ToArray(), false));

            _activeTransitionGroup = null;
            _transitions = null;
        }

        public PeptideGroupDocNode ToDocNode()
        {
            PeptideGroupDocNode nodeGroup;
            SrmSettingsDiff diff = SrmSettingsDiff.ALL;
            if (PeptideList)
            {
                if (_activePeptide != null)
                {
                    CompletePeptide();
                    diff = SrmSettingsDiff.PROPS;
                }
                nodeGroup = new PeptideGroupDocNode(new PeptideGroup(), Name, Description, _peptides.ToArray());
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
            // Materialize children, so that we have accurate accounting of
            // peptide and transition counts.
            return nodeGroup.ChangeSettings(_settings, diff);
        }
    }
}
