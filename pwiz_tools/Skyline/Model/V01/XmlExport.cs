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
using System.Globalization;
using System.IO;
using System.Text;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.V01
{
    public abstract class XmlMassListExporter
    {
        protected readonly CultureInfo _cultureInfo = CultureInfo.InvariantCulture;

        protected readonly XmlSrmDocument _document;

        protected XmlMassListExporter(XmlSrmDocument document)
        {
            _document = document;
        }

        public ExportStrategy Strategy { get; set; }
        public ExportMethodType MethodType { get; set; }
        public int? MaxTransitions { get; set; }
        public int MinTransitions { get; set; }

        public Dictionary<string, StringBuilder> TestOutput { get; private set; }

        public void Export(string fileName)
        {
            string baseName;
            if (fileName == null)
            {
                baseName = "memory"; // Not L10N : Internal key
                TestOutput = new Dictionary<string, StringBuilder>();
            }
            else
            {
                baseName = Path.GetDirectoryName(fileName) +
                           Path.DirectorySeparatorChar +
                           Path.GetFileNameWithoutExtension(fileName);
            }
            string baseNameBucket = baseName;
            string suffix = null;

            TextWriter writer = null;
            try
            {
                bool single = (Strategy == ExportStrategy.Single);
                if (single)
                {
                    if (fileName != null)
                        writer = new StreamWriter(fileName);
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        TestOutput[baseName] = sb;
                        writer = new StringWriter(sb);
                    }
                }

                int transitionCount = 0;
                int fileCount = 0;
                foreach (XmlFastaSequence seq in _document.Proteins)
                {
                    if (Strategy == ExportStrategy.Protein)
                    {
                        suffix = FileEscape(seq.Name);
                        NextFile(baseNameBucket, suffix, ref writer, ref fileCount, ref transitionCount);
                    }
                    else if (!single && (writer == null || ExceedsMax(transitionCount + seq.TransitionCount)))
                    {
                        NextFile(baseNameBucket, suffix, ref writer, ref fileCount, ref transitionCount);
                    }

                    foreach (XmlPeptide peptide in seq.Peptides)
                    {
                        // OLD_DO: For now just skip peptides with too few transitions.
                        //       Consider if there is a better way, or if the setting should
                        //       be moved to the ExportTransitionListDlg class.
                        if (peptide.Transitions.Length < MinTransitions)
                            continue;

                        // Make sure we can write out all the transitions for this peptide.
                        if (!single && ExceedsMax(transitionCount + peptide.Transitions.Length))
                            NextFile(baseNameBucket, suffix, ref writer, ref fileCount, ref transitionCount);

                        foreach (XmlTransition transition in peptide.Transitions)
                        {
                            if (!single && ExceedsMax(transitionCount + 1))
                                NextFile(baseNameBucket, suffix, ref writer, ref fileCount, ref transitionCount);

                            if (writer == null)
                                throw new IOException(Resources.XmlMassListExporter_Export_Unexpected_failure_writing_transitions);

                            // If this is for scheduled SRM, skip transitions lacking a
                            // predicted retention time.
                            if (MethodType == ExportMethodType.Scheduled && !peptide.PredictedRetentionTime.HasValue)
                                continue;

                            WriteTransition(writer, seq, peptide, transition);

                            transitionCount++;
                        }
                    }
                }
            }
            finally
            {
                if (writer != null)
                {
                    try { writer.Close(); }
                    catch (IOException) { }
                }
            }            
        }

        protected abstract void WriteTransition(TextWriter writer,
                                                XmlFastaSequence sequence,
                                                XmlPeptide peptide,
                                                XmlTransition transition);

        private bool ExceedsMax(int count)
        {
            return (MaxTransitions != null && count > 0 && count > MaxTransitions);
        }

// ReSharper disable RedundantAssignment
        private void NextFile(string baseName, string suffix, ref TextWriter writer,
                              ref int fileCount, ref int transitionCount)
// ReSharper restore RedundantAssignment
        {
            if (writer != null)
                writer.Close();
            transitionCount = 0;
            fileCount++;
            // Make sure file names sort into the order in which they were
            // written.  This will help the results load in tree order.
            baseName = suffix == null
                           ? string.Format("{0}_{1:0000}", baseName, fileCount) // Not L10N
                           : string.Format("{0}_{1:0000}_{2}", baseName, fileCount, suffix); // Not L10N

            if (TestOutput == null)
                writer = new StreamWriter(baseName + ".csv"); // Not L10N
            else
            {
                StringBuilder sb = new StringBuilder();
                TestOutput[baseName] = sb;
                writer = new StringWriter(sb);
            }
        }

        private static string FileEscape(IEnumerable<char> namePart)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in namePart)
            {
                sb.Append("/\\:*?\"<>|".IndexOf(c) == -1 ? c : '_'); // Not L10N
            }
            return sb.ToString();
        }
    }

    public class XmlThermoMassListExporter : XmlMassListExporter
    {
        public XmlThermoMassListExporter(XmlSrmDocument document)
            : base(document)
        {
        }

        protected override void WriteTransition(TextWriter writer,
                                                XmlFastaSequence sequence,
                                                XmlPeptide peptide,
                                                XmlTransition transition)
        {
            char separator = TextUtil.GetCsvSeparator(_cultureInfo);
            writer.Write(transition.PrecursorMz.ToString(_cultureInfo));
            writer.Write(separator);
            writer.Write(transition.ProductMz.ToString(_cultureInfo));
            writer.Write(separator);
            writer.Write(Math.Round(transition.CollisionEnergy, 1).ToString(_cultureInfo));
            writer.Write(separator);
            if (MethodType == ExportMethodType.Scheduled)
            {
                if (!transition.StartRT.HasValue || !transition.StopRT.HasValue)
                    throw new InvalidOperationException(Resources.XmlThermoMassListExporter_WriteTransition_Attempt_to_write_scheduling_parameters_failed);
                writer.Write(transition.StartRT.Value.ToString(_cultureInfo));
                writer.Write(separator);
                writer.Write(transition.StopRT.Value.ToString(_cultureInfo));
                writer.Write(separator);
                writer.Write('1'); // Not L10N
                writer.Write(separator);
            }
            writer.Write(peptide.Sequence);
            writer.Write(separator);
            writer.Write(sequence.Name);
            writer.WriteLine();
        }
    }

    public class XmlAbiMassListExporter : XmlMassListExporter
    {
        public XmlAbiMassListExporter(XmlSrmDocument document)
            : base(document)
        {
        }

        public double DwellTime { get; set; }

        protected override void WriteTransition(TextWriter writer,
                                                XmlFastaSequence sequence,
                                                XmlPeptide peptide,
                                                XmlTransition transition)
        {
            char separator = TextUtil.GetCsvSeparator(_cultureInfo);
            writer.Write(transition.PrecursorMz.ToString(_cultureInfo));
            writer.Write(separator);
            writer.Write(transition.ProductMz.ToString(_cultureInfo));
            writer.Write(separator);
            if (MethodType == ExportMethodType.Standard)
                writer.Write(Math.Round(DwellTime, 2).ToString(_cultureInfo));
            else
            {
                if (!peptide.PredictedRetentionTime.HasValue)
                    throw new InvalidOperationException(Resources.XmlThermoMassListExporter_WriteTransition_Attempt_to_write_scheduling_parameters_failed);
                writer.Write(peptide.PredictedRetentionTime.Value.ToString(_cultureInfo));
            }
            writer.Write(separator);

            // Write special ID for ABI software
            var fastaSequence = new FastaSequence(sequence.Name, sequence.Description, null, peptide.Sequence);
            var newPeptide = new Peptide(fastaSequence, peptide.Sequence, 0, peptide.Sequence.Length, peptide.MissedCleavages);
            var nodePep = new PeptideDocNode(newPeptide);
            string modifiedPepSequence = AbiMassListExporter.GetSequenceWithModsString(nodePep, _document.Settings); // Not L10N;
            
            string extPeptideId = string.Format("{0}.{1}.{2}.{3}", // Not L10N
                                                sequence.Name,
                                                modifiedPepSequence,
                                                GetTransitionName(transition),
                                                "light"); // Not L10N : file format

            writer.WriteDsvField(extPeptideId, separator);
            writer.Write(separator);
            writer.Write(Math.Round(transition.DeclusteringPotential ?? 0, 1).ToString(_cultureInfo));

            writer.Write(separator);
            writer.Write(Math.Round(transition.CollisionEnergy, 1).ToString(_cultureInfo));


            writer.WriteLine();
        }

        private static string GetTransitionName(XmlTransition transition)
        {
            return AbiMassListExporter.GetTransitionName(transition.PrecursorCharge,
                                                        transition.FragmentType.ToString().ToLower() + transition.FragmentOrdinal,
                                                        transition.ProductCharge);
        }
    }
}
