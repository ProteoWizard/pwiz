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
using System.Text;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.V01
{
    public class FastaSeqV01
    {
        public const char PEPTIDE_SEPARATOR = 'X';

        /// <summary>
        /// Used to determine if a string contains a valid amino acid
        /// sequence.
        /// </summary>
        /// <param name="seq">The string to inspect.</param>
        /// <returns>True if the string is non-zero length and contains only amino acids.</returns>
        public static bool IsSequence(string seq)
        {
            if (seq.Length == 0)
                return false;

            foreach (char c in seq)
            {
                if (!AminoAcid.IsAA(c))
                    return false;
            }
            return true;
        }

        // For serialization
        protected FastaSeqV01()
        {
        }

        public FastaSeqV01(string id, string[] descriptions, string aa, bool peptideList)
        {
            Id = id;
            Descriptions = (descriptions ?? new string[0]);
            AA = aa;
            PeptideList = peptideList;
        }

        public string Id { get; private set; }
        public string[] Descriptions { get; private set; }
        public string AA { get; private set; }
        public bool PeptideList { get; private set; }

/*
        public IEnumerable<PepV01> GetPeptides(SrmSettings settings, bool useFilter)
        {
            PeptideSettings pepSettings = settings.PeptideSettings;
            Enzyme enzyme = pepSettings.Enzyme;
            DigestSettings digest = pepSettings.DigestSettings;
            IPeptideFilter filter = (useFilter ? pepSettings.Filter : PeptideFilter.UNFILTERED);
            SequenceMassCalc massCalc = settings.GetPrecursorCalc(IsotopeLabelType.none);
            RetentionTimeRegression rtRegression = pepSettings.Prediction.RetentionTime;

            IEnumerable<PepV01> peptides = PeptideList ?
                                                            GetPeptideList(enzyme) : enzyme.Digest(this, digest, filter);

            foreach (PepV01 peptide in peptides)
            {
                peptide.CalcMass(massCalc);
                peptide.CalcRetentionTime(rtRegression);
                yield return peptide;
            }
        }
*/
        /// <summary>
        /// Get the list of peptides from a <see cref="FastaSeqV01"/> where
        /// <see cref="PeptideList"/> is true.
        /// </summary>
        /// <param name="enzyme">The enzyme used to detect missed cleavages in the peptides</param>
        /// <returns>An enumerable list of <see cref="PepV01"/></returns>
        public IEnumerable<PepV01> GetPeptideList(Enzyme enzyme)
        {
            if (!PeptideList)
                throw new InvalidOperationException(Resources.FastaSeqV01_GetPeptideList_Attempt_to_get_peptide_list_from_uncleaved_FASTA_sequence);

            int begin = 1;
            int end = begin;
            while (end < AA.Length - 1)
            {
                end = AA.IndexOf(PEPTIDE_SEPARATOR, begin);
                string seqPep = AA.Substring(begin, end - begin);
                int missedCleavages = enzyme.CountCleavagePoints(seqPep);

                yield return new PepV01(this, begin, end, missedCleavages);

                begin = end + 1;
            }
        }

        #region object overrides

        public override string ToString()
        {
            return Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            FastaSeqV01 seq = obj as FastaSeqV01;
            return seq != null &&
                   seq.Id == Id &&
                   seq.AA == AA;
        }

        public override int GetHashCode()
        {
            int result = Id.GetHashCode();
            result = 31 * result + AA.GetHashCode();
            return result;
        }

        #endregion // object overrides
    }

    public class FastaSeqV01Builder
    {
        private readonly StringBuilder _sequence = new StringBuilder();
        private bool _peptideList;

        public string Id { get; set; }
        public string[] Descriptions { get; set; }
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

        public bool PeptideList
        {
            get { return _peptideList; }
            set
            {
                _peptideList = value;
                if (_peptideList && _sequence.Length == 0)
                    EndPeptide();
            }
        }

        public void AppendSequence(string seq)
        {
            seq = seq.Trim();
            if (seq.EndsWith("*")) // Not L10N
                seq = seq.Substring(0, seq.Length - 1);
            _sequence.Append(seq.Trim());
            if (_peptideList)
                EndPeptide();
        }

        public void EndPeptide()
        {
            Debug.Assert(PeptideList);
            _sequence.Append(FastaSeqV01.PEPTIDE_SEPARATOR);
        }

        public void Append(char aa)
        {
            _sequence.Append(aa);
        }

        public FastaSeqV01 ToFastaSequence()
        {
            return new FastaSeqV01(Id, Descriptions, AA, PeptideList);
        }
    }

    public class PepV01
    {
        private readonly FastaSeqV01 _fastaSequence;
        private double _massH;

        public PepV01(FastaSeqV01 fastaSequence, int begin, int end, int missedCleavages)
        {
            _fastaSequence = fastaSequence;

            Begin = begin;
            End = end;
            MissedCleavages = missedCleavages;

            // Derived value
            Sequence = _fastaSequence.AA.Substring(Begin, End - Begin);
        }

        public PepV01(FastaSeqV01 fastaSequence, int begin, int end, int missedCleavages,
                       double mh, double? rt)
            : this(fastaSequence, begin, end, missedCleavages)
        {
            MassH = mh;
            PredictedRetentionTime = rt;
        }

        public FastaSeqV01 FastaSequence
        {
            get { return _fastaSequence; }
        }

        public string Sequence { get; private set; }
        public int Begin { get; private set; }
        public int End { get; private set; } // non-inclusive
        public int MissedCleavages { get; private set; }
        public double? PredictedRetentionTime { get; private set; }
        public double MassH
        {
            get { return _massH; }

            private set
            {
                _massH = SequenceMassCalc.PersistentMH(value);
            }
        }

        public void CalcMass(SequenceMassCalc massCalc)
        {
            MassH = massCalc.GetPrecursorMass(Sequence);
        }

        public void CalcRetentionTime(RetentionTimeRegression rtRegression)
        {
            double? rt = null;
            if (rtRegression != null)
            {
                double? score = rtRegression.Calculator.ScoreSequence(Sequence);
                if (score.HasValue)
                    rt = rtRegression.Conversion.GetY(score.Value);
            }
            PredictedRetentionTime = rt;
        }

        public char PrevAA
        {
            get
            {
                return (Begin == 0 ? '-' : _fastaSequence.AA[Begin - 1]); // Not L10N
            }
        }

        public char NextAA
        {
            get
            {
                return (End == _fastaSequence.AA.Length ? '-' : _fastaSequence.AA[End]); // Not L10N
            }
        }

        public int Length
        {
            get
            {
                return End - Begin;
            }
        }

        public IEnumerable<FragmentIon> GetFragmentIons(IEnumerable<IonType> ionTypes, SequenceMassCalc calc)
        {
            double[,] masses = calc.GetFragmentIonMasses(Sequence);
            int len = masses.GetLength(1);
            foreach (IonType ionType in ionTypes)
            {
                for (int i = 0; i < len; i++)
                {
                    yield return new FragmentIon(this, ionType, i, masses[(int)ionType, i]);
                }
            }
        }
/*
        public IEnumerable<SrmTransition> GetTransitions(SrmSettings settings)
        {
            double mh = MassH;

            SequenceMassCalc calc = settings.GetFragmentCalc(IsotopeLabelType.none);

            TransitionSettings tranSettings = settings.TransitionSettings;
            PeptideSettings pepSettings = settings.PeptideSettings;

            TransitionFilter filter = tranSettings.Filter;
            IEnumerable<int> precursorCharges = filter.PrecursorCharges;
            IEnumerable<int> charges = filter.ProductCharges;
            IEnumerable<IonType> types = filter.IonTypes;

            FragmentIon[] fragments = GetFragmentIons(types, calc).ToArray();

            IStartFragmentFinder startFinder = filter.FragmentRangeFirst;
            IEndFragmentFinder endFinder = filter.FragmentRangeLast;
            bool pro = filter.IncludeNProline;
            bool gluasp = filter.IncludeCGluAsp;

            int minMz = tranSettings.Instrument.MinMz;
            int maxMz = tranSettings.Instrument.MaxMz;

            if (fragments.Length > 0 && startFinder != null && endFinder != null)
            {
                CollisionEnergyRegression regressCE = tranSettings.Prediction.CollisionEnergy;
                DeclusteringPotentialRegression regressDP = tranSettings.Prediction.DeclusteringPotential;
                RetentionTimeRegression regressRT = pepSettings.Prediction.RetentionTime;
                double rtWindow = 0.0;
                if (regressRT != null)
                    rtWindow = regressRT.TimeWindow;

                foreach (int precursorCharge in precursorCharges)
                {
                    double mz = SequenceMassCalc.GetMZ(mh, precursorCharge);

                    // Make sure the precursor m/z falls within valid instrument range.
                    if (minMz > mz || mz > maxMz)
                        continue;

                    double ce = regressCE.GetCollisionEnergy(precursorCharge, mz);
                    double? dp = null;
                    if (regressDP != null)
                        dp = regressDP.GetY(mz);
                    foreach (int charge in charges)
                    {
                        // If precursor charge is less than 3, then charge must be 1.
                        if (precursorCharge < 3 && charge > 1)
                            continue;

                        int start = startFinder.FindStartFragment(fragments, precursorCharge, charge);
                        int end = endFinder.FindEndFragment(fragments, start);
                        if (start > end)
                            Helpers.Swap(ref start, ref end);
                        for (int i = 0; i < fragments.Length; i++)
                        {
                            FragmentIon fragment = fragments[i];

                            // Make sure the fragment m/z value falls within the valid instrument range.
                            double ionMz = SequenceMassCalc.GetMZ(fragment.MassH, charge);
                            if (minMz > ionMz || ionMz > maxMz)
                                continue;

                            if ((start <= i && i <= end) ||
                                (pro && fragment.FragmentNTermAA == 'P'))
                            {
                                yield return new SrmTransition(fragment, ce, dp, rtWindow, precursorCharge, charge);
                            }
                            else if (gluasp)
                            {
                                char c = fragments[i].FragmentCTermAA;
                                if (c == 'G' || c == 'A')
                                    yield return new SrmTransition(fragment, ce, dp, rtWindow, precursorCharge, charge);
                            }
                        }
                    }
                }
            }
        }
*/
        #region object overrides

        public override string ToString()
        {
            string format = "{0}.{1}.{2} [{3}, {4}]"; // Not L10N
            if (MissedCleavages > 0)
                format = TextUtil.SpaceSeparate(format, Resources.Peptide_ToString__missed__5__);
            return string.Format(format, PrevAA, Sequence, NextAA, Begin, End - 1, MissedCleavages);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            PepV01 pep = obj as PepV01;
            return pep != null &&
                   pep.Begin == Begin &&
                   pep.End == End &&
                   Equals(pep._fastaSequence, _fastaSequence);
        }

        public override int GetHashCode()
        {
            int result = Begin.GetHashCode();
            result = 31 * result + End.GetHashCode();
            result = 31 * result + _fastaSequence.GetHashCode();
            return result;
        }

        #endregion // object overrides
    }

// ReSharper disable InconsistentNaming
    public enum IonType { A, B, C, X, Y, Z }
// ReSharper restore InconsistentNaming

    public class FragmentIon
    {
        public static bool IsNTerminal(IonType type)
        {
            return type == IonType.A || type == IonType.B || type == IonType.C;
        }

        public static bool IsCTerminal(IonType type)
        {
            return type == IonType.X || type == IonType.Y || type == IonType.Z;
        }

        public static int OrdinalToOffset(IonType type, int ordinal, int len)
        {
            if (IsNTerminal(type))
                return ordinal - 1;
            
            return len - ordinal - 1;
        }

        private readonly PepV01 _peptide;
        private double _massH;

        public FragmentIon(PepV01 peptide, IonType type, int offset, double mh)
        {
            _peptide = peptide;

            IType = type;
            CleavageOffset = offset;
            MassH = mh;

            // Derived values
            if (IsNTerminal())
            {
                Ordinal = offset + 1;
                AA = peptide.Sequence[offset];
            }
            else
            {
                Ordinal = _peptide.Length - offset - 1;
                AA = peptide.Sequence[offset + 1];
            }
        }

        public PepV01 Peptide
        {
            get { return _peptide; }
        }

        public IonType IType { get; private set; }
        public int CleavageOffset { get; private set; }
        public int Ordinal { get; private set; }
        public char AA { get; private set; }

        public double MassH
        {
            get { return _massH; }

            private set
            {
                _massH = SequenceMassCalc.PersistentMH(value);
            }
        }
/*
        public void CalcMass(SequenceMassCalc massCalc)
        {
            MassH = massCalc.GetFragmentMass(this);
        }
*/
        public bool IsNTerminal()
        {
            return IsNTerminal(IType);
        }

        public bool IsCTerminal()
        {
            return IsCTerminal(IType);
        }

        public char FragmentNTermAA
        {
            get { return _peptide.Sequence[CleavageOffset + 1]; }
        }

        public char FragmentCTermAA
        {
            get { return _peptide.Sequence[CleavageOffset]; }
        }

        #region object overrides

        public override string ToString()
        {
            return string.Format("{0} {1}{2} - {3:F04}", AA, IType.ToString().ToLower(), // Not L10N
                                 Ordinal, MassH);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            FragmentIon ion = obj as FragmentIon;
            return ion != null &&
                   ion.IType == IType &&
                   ion.CleavageOffset == CleavageOffset &&
                   Equals(ion._peptide, _peptide);
        }

        public override int GetHashCode()
        {
            int result = IType.GetHashCode();
            result = 31 * result + CleavageOffset.GetHashCode();
            result = 31 * result + _peptide.GetHashCode();
            return result;
        }

        #endregion // object overrides
    }

    public class SrmTransition
    {
        private readonly FragmentIon _fragment;

        public SrmTransition(FragmentIon fragment, double ce, double? dp, double rtWindow, int precursorCharge, int charge)
        {
            _fragment = fragment;
            CollisionEnergy = ce;
            DeclusteringPotential = dp;
            RetentionTimeWindow = rtWindow;
            PrecursorCharge = precursorCharge;
            Charge = charge;
        }

        public FragmentIon Fragment
        {
            get { return _fragment; }
        }

        public int PrecursorCharge { get; private set; }
        public int Charge { get; private set; }
        public double CollisionEnergy { get; private set; }
        public double? DeclusteringPotential { get; private set; }
        public double RetentionTimeWindow { get; set; }

        public double PrecursorMZ
        {
            get
            {
                return SequenceMassCalc.GetMZ(_fragment.Peptide.MassH, PrecursorCharge);
            }
        }

        public double MZ
        {
            get
            {
                return SequenceMassCalc.GetMZ(_fragment.MassH, Charge);
            }
        }

        public void CalcCollisionEnergy(CollisionEnergyRegression regression)
        {
            CollisionEnergy = regression.GetCollisionEnergy(PrecursorCharge, PrecursorMZ);
        }

        public void CalcDeclusteringPotential(DeclusteringPotentialRegression regression)
        {
            if (regression == null)
                DeclusteringPotential = null;
            else
                DeclusteringPotential = regression.GetY(MZ);
        }

        public string GetChargeIndicator(int charge)
        {
            return "++++++++++".Substring(0, charge); // Not L10N
        }

        #region object overrides

        public override string ToString()
        {
            return string.Format("{0} [{1}{2}] - {3:F04}{4} -> {5:F04}{6}", // Not L10N
                                 _fragment.AA, _fragment.IType.ToString().ToLower(), _fragment.Ordinal,
                                 PrecursorMZ, GetChargeIndicator(PrecursorCharge),
                                 MZ, GetChargeIndicator(Charge));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            SrmTransition transition = obj as SrmTransition;
            return transition != null &&
                   transition.PrecursorCharge == PrecursorCharge &&
                   transition.Charge == Charge &&
                   Equals(transition._fragment, _fragment);
        }

        public override int GetHashCode()
        {
            int result = PrecursorCharge.GetHashCode();
            result = 31 * result + Charge.GetHashCode();
            result = 31 * result + _fragment.GetHashCode();
            return result;
        }

        #endregion // object overrides
    }
}