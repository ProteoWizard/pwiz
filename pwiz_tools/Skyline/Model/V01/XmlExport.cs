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
using System.IO;
using System.Text;

namespace pwiz.Skyline.Model.V01
{
    public abstract class XmlMassListExporter
    {
        private readonly XmlSrmDocument _document;

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
                baseName = "memory";
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
                                throw new IOException("Unexpected failure writing transitions.");

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

        private void NextFile(string baseName, string suffix, ref TextWriter writer,
                              ref int fileCount, ref int transitionCount)
        {
            if (writer != null)
                writer.Close();
            transitionCount = 0;
            fileCount++;
            // Make sure file names sort into the order in which they were
            // written.  This will help the results load in tree order.
            if (suffix == null)
                baseName = string.Format("{0}_{1:0000}", baseName, fileCount);
            else
                baseName = string.Format("{0}_{1:0000}_{2}", baseName, fileCount, suffix);
            if (TestOutput == null)
                writer = new StreamWriter(baseName + ".csv");
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
                if ("/\\:*?\"<>|".IndexOf(c) == -1)
                    sb.Append(c);
                else
                    sb.Append('_');
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
            writer.Write(transition.PrecursorMz);
            writer.Write(',');
            writer.Write(transition.ProductMz);
            writer.Write(',');
            writer.Write(Math.Round(transition.CollisionEnergy, 1));
            writer.Write(',');
            if (MethodType == ExportMethodType.Scheduled)
            {
                Debug.Assert(transition.StartRT.HasValue && transition.StartRT.HasValue);
                writer.Write(transition.StartRT.Value);
                writer.Write(',');
                writer.Write(transition.StopRT.Value);
                writer.Write(',');
                writer.Write('1');
                writer.Write(',');
            }
            writer.Write(peptide.Sequence);
            writer.Write(',');
            writer.Write(sequence.Name);
            writer.Write('\n');
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
            writer.Write(transition.PrecursorMz);
            writer.Write(',');
            writer.Write(transition.ProductMz);
            writer.Write(',');
            if (MethodType == ExportMethodType.Standard)
                writer.Write(Math.Round(DwellTime, 2));
            else
            {
                Debug.Assert(peptide.PredictedRetentionTime.HasValue);
                writer.Write(peptide.PredictedRetentionTime.Value);
            }
            writer.Write(',');

            // Write special ID for ABI software
            writer.Write(sequence.Name);
            writer.Write('.');
            writer.Write(peptide.Sequence);
            writer.Write('.');
//            Removed in v0.2 for test compatibility
//            writer.Write(transition.PrecursorCharge);
            writer.Write(transition.FragmentType.ToString().ToLower());
            writer.Write(transition.FragmentOrdinal);
            writer.Write('.');
            // OLD_DO: Support for heavy
            writer.Write("light");
            writer.Write(',');

            writer.Write(Math.Round(transition.DeclusteringPotential ?? 0, 1));
//            Removed in v0.2 for test compatibility
//            writer.Write(',');
            // EP : not used by Paulovich Lab
            writer.Write(',');
            writer.Write(Math.Round(transition.CollisionEnergy, 1));
//            Removed in v0.2 for test compatibility
//            writer.Write(',');
            // CXP : not used by Paulovich Lab
            writer.Write('\n');
        }
    }
}