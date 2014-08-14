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
using System.Text;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.V01
{
    [Serializable]
    [XmlRoot(ElementName = "srm_settings")]
    public class XmlSrmDocument
    {
        public const string EXT = "sky"; // Not L10N

        // For serialization
        protected XmlSrmDocument()
        {            
        }

        public XmlSrmDocument(SrmSettings settings, XmlFastaSequence[] proteins)
        {
            Settings = settings;
            Proteins = proteins;
        }

        [XmlElement(ElementName = "settings_summary")] 
        public SrmSettings Settings { get; set; }

        [XmlArray(ElementName = "selected_proteins")] 
        [XmlArrayItem(ElementName = "protein")]
        public XmlFastaSequence[] Proteins { get; set; }
    }

    [Serializable]
    public class XmlFastaSequence
    {
        // For serialization
        protected XmlFastaSequence()
        {            
        }

        public XmlFastaSequence(FastaSeqV01 sequence, XmlPeptide[] peptides)
        {
            Name = sequence.Id;
            if (sequence.Descriptions.Length > 0)
            {
                Description = sequence.Descriptions[0];
                List<XmlAlternativeProtein> alternatives = new List<XmlAlternativeProtein>();
                for (int i = 1; i < sequence.Descriptions.Length; i++)
                {
                    string desc = sequence.Descriptions[i].Trim();
                    int space = desc.IndexOf(' ');
                    if (space > 0)
                        alternatives.Add(new XmlAlternativeProtein(desc.Substring(0, space), desc.Substring(space + 1)));
                    else
                        alternatives.Add(new XmlAlternativeProtein(desc, null));
                }
                if (alternatives.Count > 0)
                    Alternatives = alternatives.ToArray();
            }
            PeptideList = sequence.PeptideList;
            Sequence = FormatAA(sequence.AA);
            Peptides = peptides;
        }

        public FastaSeqV01 GetModel()
        {
            // For backward compatibility, check the first amino acid
            // for 'X', and assume it is a peptide list, if it is.
            bool peptideList = PeptideList || Sequence[0] == 'X'; // Not L10N

            FastaSeqV01Builder seqBuilder = new FastaSeqV01Builder
                                                  { Id = Name, PeptideList = peptideList };

            List<string> descriptions = new List<string> {Description};
            if (Alternatives != null)
            {
                foreach (XmlAlternativeProtein alternative in Alternatives)
                    descriptions.Add(alternative.Name + " " + alternative.Description); // Not L10N
            }
            seqBuilder.Descriptions = descriptions.ToArray();
            string sequence = Sequence.Trim();
            if (peptideList && sequence.Length > 0 && sequence[0] == FastaSeqV01.PEPTIDE_SEPARATOR)
            {
                peptideList = false;
                sequence = sequence.Substring(1);
            }
            foreach (char aa in sequence)
            {
                if (peptideList && aa == '\n')
                    seqBuilder.EndPeptide();
                if (!char.IsWhiteSpace(aa))
                    seqBuilder.Append(aa);
            }
            if (peptideList)
                seqBuilder.EndPeptide();
            return seqBuilder.ToFastaSequence();
        }

        public int TransitionCount
        {
            get
            {
                int n = 0;
                foreach (XmlPeptide peptide in Peptides)
                    n += peptide.Transitions.Length;
                return n;
            }
        }

        private string FormatAA(string aa)
        {
            const string lineSeparator = "\r\n        "; // Not L10N

            if (PeptideList)
                return string.Join(lineSeparator, aa.Split(FastaSeqV01.PEPTIDE_SEPARATOR));

            StringBuilder sb = new StringBuilder();
            if (aa.Length > 50)
                sb.Append(lineSeparator);
            for (int i = 0; i < aa.Length; i += 10)
            {
                if (aa.Length - i <= 10)
                    sb.Append(aa.Substring(i));
                else
                {
                    sb.Append(aa.Substring(i, Math.Min(10, aa.Length - i)));
                    sb.Append(i % 50 == 40 ? "\r\n        " : " "); // Not L10N
                }
            }                

            return sb.ToString();
        }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "peptide_list")]
        public bool PeptideList { get; set; }

        [XmlArray(ElementName = "alternatives")]
        [XmlArrayItem(ElementName = "alternative_protein")]
        public XmlAlternativeProtein[] Alternatives { get; set; }

        [XmlElement(ElementName = "sequence")]
        public string Sequence { get; set; }

        [XmlArray(ElementName = "selected_peptides")]
        [XmlArrayItem(ElementName = "peptide")]
        public XmlPeptide[] Peptides { get; set;}
    }

    [Serializable]
    public class XmlAlternativeProtein
    {
        // For serialization
        protected XmlAlternativeProtein()
        {            
        }

        public XmlAlternativeProtein(string name, string description)
        {
            Name = name;
            Description = description;
        }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }
    }

    [Serializable]
    public class XmlPeptide
    {
        // For serialization
        protected XmlPeptide()
        {            
        }

        public XmlPeptide(PepV01 peptide, XmlTransition[] transitions)
        {
            Begin = peptide.Begin;
            End = peptide.End;
            Sequence = peptide.Sequence;
            PrevAA = peptide.PrevAA;
            NextAA = peptide.NextAA;
            NeutralMass = SequenceMassCalc.PersistentNeutral(peptide.MassH);
            MissedCleavages = peptide.MissedCleavages;
            if (peptide.PredictedRetentionTime.HasValue)
                PredictedRetentionTime = Math.Round(peptide.PredictedRetentionTime.Value, 2);
            Transitions = transitions;
        }

        public PepV01 GetModel(FastaSeqV01 sequence)
        {
            double mh = NeutralMass + BioMassCalc.MassProton;
            return new PepV01(sequence, Begin, End, MissedCleavages, mh, PredictedRetentionTime);
        }

        [XmlAttribute(AttributeName = "start")]
        public int Begin { get; set; }

        [XmlAttribute(AttributeName = "end")]
        public int End { get; set; }

        [XmlAttribute(AttributeName = "sequence")]
        public string Sequence { get; set; }

        [XmlAttribute(AttributeName = "prev_aa")]
        public char PrevAA { get; set; }

        [XmlAttribute(AttributeName = "next_aa")]
        public char NextAA { get; set; }

        [XmlAttribute(AttributeName = "calc_neutral_pep_mass")]
        public double NeutralMass { get; set; }

        [XmlAttribute(AttributeName = "num_missed_cleavages")]
        public int MissedCleavages { get; set; }

        // String required due to lack of good support for Nullable<T>
        [XmlAttribute(AttributeName = "predicted_retention_time")]
        public string PredictedRetentionTimeAttr
        {
            get { return XmlUtil.ToAttr(PredictedRetentionTime); }
            set
            {
                PredictedRetentionTime = double.Parse(value, CultureInfo.InvariantCulture);

                // Predicted retention time of 0 is not valid, and
                // is assigned null for backward compatibility.
                if (PredictedRetentionTime == 0)
                    PredictedRetentionTime = null;
            }
        }

        [XmlIgnore]
        public double? PredictedRetentionTime { get; set; }

        /*
                [XmlAttribute(AttributeName = "library")]
                public string Library { get; set; }
        */
        [XmlArray(ElementName = "selected_transitions")]
        [XmlArrayItem(ElementName = "transition")]
        public XmlTransition[] Transitions { get; set; }
    }

    [Serializable]
    public class XmlTransition
    {
        // For serialization
        protected XmlTransition()
        {
        }

        public XmlTransition(SrmTransition transition)
        {
            FragmentType = transition.Fragment.IType;
            FragmentOrdinal = transition.Fragment.Ordinal;
            NeutralMass = SequenceMassCalc.PersistentNeutral(transition.Fragment.MassH);
            PrecursorCharge = transition.PrecursorCharge;
            ProductCharge = transition.Charge;
            PrecursorMz = SequenceMassCalc.PersistentMZ(transition.PrecursorMZ);
            ProductMz = SequenceMassCalc.PersistentMZ(transition.MZ);
            CollisionEnergy = Math.Round(transition.CollisionEnergy, 6);
            if (transition.DeclusteringPotential.HasValue)
                DeclusteringPotential = Math.Round(transition.DeclusteringPotential.Value, 6);

            PepV01 peptide = transition.Fragment.Peptide;
            if (peptide.PredictedRetentionTime.HasValue && transition.RetentionTimeWindow > 0)
            {
                double start = Math.Round(peptide.PredictedRetentionTime.Value - transition.RetentionTimeWindow/2, 2);
                StartRT = start;
                StopRT = Math.Round(start + transition.RetentionTimeWindow, 2);
            }
        }

        public SrmTransition GetModel(PepV01 peptide)
        {
            int offset = FragmentIon.OrdinalToOffset(FragmentType,
                                                     FragmentOrdinal, peptide.Length);
            double mh = NeutralMass + BioMassCalc.MassProton;
            FragmentIon fragment = new FragmentIon(peptide, FragmentType, offset, mh);
            double start = StartRT ?? 0;
            double stop = StopRT ?? 0;
            return new SrmTransition(fragment, CollisionEnergy,
                                     DeclusteringPotential, start - stop,
                                     PrecursorCharge, ProductCharge);
        }

        [XmlAttribute(AttributeName = "fragment_type")]
        public IonType FragmentType { get; set; }

        [XmlAttribute(AttributeName = "fragment_ordinal")]
        public int FragmentOrdinal { get; set; }

        [XmlAttribute(AttributeName = "calc_neutral_mass")]
        public double NeutralMass { get; set; }

        [XmlAttribute(AttributeName = "precursor_charge")] // Not L10N
        public int PrecursorCharge { get; set; }

        [XmlAttribute(AttributeName = "product_charge")] // Not L10N
        public int ProductCharge { get; set; }

        /*
        public int LibraryRank { get; set; }
         */

        [XmlElement(ElementName = "precursor_mz")]
        public double PrecursorMz { get; set; }

        [XmlElement(ElementName = "product_mz")]
        public double ProductMz { get; set; }

        [XmlElement(ElementName = "collision_energy")] 
        public double CollisionEnergy { get; set; }

        // String required due to lack of good support for Nullable<T>
        [XmlElement(ElementName = "declustering_potential")]
        public string DeclusteringPotentialAttr
        {
            get { return XmlUtil.ToAttr(StartRT); }            
            set { DeclusteringPotential = double.Parse(value, CultureInfo.InvariantCulture); }
        }

        [XmlIgnore]
        public double? DeclusteringPotential { get; set; }

        // String required due to lack of good support for Nullable<T>
        [XmlElement(ElementName = "start_rt")] // Not L10N
        public string StartRTAttr
        {
            get { return XmlUtil.ToAttr<double>(StartRT ?? 0.0); }            
            set { StartRT = double.Parse(value, CultureInfo.InvariantCulture); }
        }

        [XmlIgnore]
        public double? StartRT { get; set; }

        // String required due to lack of good support for Nullable<T>
        [XmlElement(ElementName = "stop_rt")]
        public string StopRTAttr
        {
            get { return XmlUtil.ToAttr<double>(StopRT ?? 0.0); }
            set { StopRT = double.Parse(value, CultureInfo.InvariantCulture); }
        }

        [XmlIgnore]
        public double? StopRT { get; set; }
    }
}