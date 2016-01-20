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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.Common.Collections;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class PeptideGroup : Identity
    {
        public const string DECOYS = "Decoys"; // Not L10N

        public virtual string Name { get { return null; } }
        public virtual string Description { get { return null; } }
        public virtual string Sequence { get { return null; } }

        public bool IsDecoy { get; private set; }

        public PeptideGroup()
        {
            IsDecoy = false;
        }

        public PeptideGroup(bool isDecoy)
        {
            IsDecoy = isDecoy;
        }

        public static IList<DocNode> RankPeptides(IList<DocNode> listPeptides, SrmSettings settings, bool useLimit)
        {
            // If no rank ID is set, just return the input list
            PeptideRankId rankId = settings.PeptideSettings.Libraries.RankId;
            if (!IsUpdateNeeded(rankId, listPeptides))
                return listPeptides;

            // Transfer input list to a typed array
            var listRanks = new List<KeyValuePair<PeptideDocNode, float>>();
            foreach (PeptideDocNode nodePep in listPeptides)
            {
                var nodePepAdd = nodePep;
                // Remove any old rank information, if peptides are no longer ranked.
                if (rankId == null && nodePepAdd.Rank.HasValue)
                    nodePepAdd = nodePepAdd.ChangeRank(null);
                listRanks.Add(new KeyValuePair<PeptideDocNode, float>(nodePepAdd, nodePepAdd.GetRankValue(rankId)));
            }

            if (rankId == null)
                return listRanks.ConvertAll(p => p.Key).ToArray();

            // Sort desc by rank ID value
            listRanks.Sort((p1, p2) => Comparer.Default.Compare(p2.Value, p1.Value));

            int rank = 1;
            for (int i = 0; i < listRanks.Count; i++)
            {
                var peptideRank = listRanks[i];
                var peptide = peptideRank.Key;
                if(peptideRank.Value == float.MinValue)
                {
                    if (peptide.Rank.HasValue)
                        peptide = peptide.ChangeRank(null);
                }
                else if (!peptide.Rank.HasValue || peptide.Rank.Value != rank)
                {
                    peptide = peptide.ChangeRank(rank);
                }
                listRanks[i] = new KeyValuePair<PeptideDocNode, float>(peptide, peptideRank.Value);

                rank++;
            }

            // Reduce array length to desired limit, if necessary
            int? peptideCount = settings.PeptideSettings.Libraries.PeptideCount;
            int numPeptides = useLimit && peptideCount.HasValue && peptideCount.Value < listPeptides.Count
                              ? peptideCount.Value
                              : listPeptides.Count;
            var peptidesNew = new List<PeptideDocNode>();
            for (int i = 0; i < numPeptides; i++)
            {
                // TODO: Remove any peptide groups without the correct rank value
                if (useLimit && listRanks[i].Value == float.MinValue)
                    break;
                peptidesNew.Add(listRanks[i].Key);
            }

            // Re-sort by order in FASTA sequence
            peptidesNew.Sort(FastaSequence.ComparePeptides);

            return peptidesNew.ToArray();
        }

        private static bool IsUpdateNeeded(PeptideRankId rankId, IList<DocNode> listPeptides)
        {
            if (rankId != null)
                return true;

            foreach (PeptideDocNode nodePep in listPeptides)
            {
                if (nodePep.Rank.HasValue)
                    return true;
            }
            return false;
        }
    }

    public class FastaSequence : PeptideGroup
    {
        public static readonly char[] OPEN_MOD = { '[', '{', '(' }; // Not L10N
        public static readonly char[] CLOSE_MOD = { ']', '}', ')' }; // Not L10N

        public const string PEPTIDE_SEQUENCE_SEPARATOR = "::"; // Not L10N
        public static readonly Regex RGX_ALL = new Regex(@"(\[.*?\]|\{.*?\}|\(.*?\))"); // Not L10N
        public static readonly Regex RGX_LIGHT = new Regex(@"\[.*?\]"); // Not L10N
        public static readonly Regex RGX_HEAVY = new Regex(@"\{.*?\}"); // Not L10N
        public static readonly Regex RGX_DASH = new Regex(@"^-"); // Not L10N

        public static bool IsSequence(string seq)
        {
            return IsSequence(StripModifications(seq), AminoAcid.IsAA);
        }

        public static bool IsExSequence(string seq)
        {
            return IsSequence(StripModifications(seq), AminoAcid.IsExAA);
        }

        private static bool IsSequence(string seq, Func<char, bool> check)
        {
            if (seq.Length == 0)
                return false;

            foreach (char c in seq)
            {
                if (!check(c))
                    return false;
            }
            return true;
        }

        private readonly string _name;
        private readonly string _description;
        private readonly string _sequence;
        private readonly bool _isDecoy;

        public FastaSequence(string name, string description, IList<ProteinMetadata> alternatives, string sequence) 
            : this(name, description, alternatives, sequence, false)
        {
        }


        public FastaSequence(string name, string description, IList<ProteinMetadata> alternatives, string sequence, bool isDecoy)
        {
            // Null name means it is editable by the user.
            _name = (string.IsNullOrEmpty(name) ? null : name);

            _description = description;
            Alternatives = ImmutableList.ValueOfOrEmpty(alternatives);
            _sequence = sequence;
            _isDecoy = isDecoy;

            Validate();
        }

        public override string Name { get { return _name; } }
        public override string Description { get { return _description; } }
        public override string Sequence { get { return _sequence; } }
        public new bool IsDecoy { get { return _isDecoy; } }
        public ImmutableList<ProteinMetadata> Alternatives { get; private set; }
        public IEnumerable<string> AlternativesText
        {
            get { return Alternatives.Select(alt => TextUtil.SpaceSeparate(alt.Name ?? String.Empty, alt.Description ?? String.Empty)); }  // CONSIDER (bspratt) - include accession, preferredName etc?
        }

        public string FastaFileText
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(">").Append(Name).Append(" ").Append(Description); // Not L10N
                foreach (var alt in Alternatives)
                    sb.Append((char)1).Append(alt.Name ?? String.Empty).Append(" ").Append(alt.Description ?? String.Empty); // Not L10N

                for (int i = 0; i < Sequence.Length; i++)
                {
                    if (i % 60 == 0)
                        sb.AppendLine();
                    sb.Append(Sequence[i]);
                }
                sb.Append("*"); // Not L10N
                return sb.ToString();
            }
        }

        public IEnumerable<PeptideDocNode> CreatePeptideDocNodes(SrmSettings settings, bool useFilter, string peptideSequence)
        {
            PeptideSettings pepSettings = settings.PeptideSettings;
            DigestSettings digest = pepSettings.DigestSettings;
            IPeptideFilter filter = (useFilter ? settings : PeptideFilter.UNFILTERED);

            foreach (var peptide in pepSettings.Enzyme.Digest(this, digest))
            {
                if (null != peptideSequence && peptideSequence != peptide.Sequence)
                {
                    continue;
                }
                foreach (var nodePep in peptide.CreateDocNodes(settings, filter))
                    yield return nodePep;
            }
        }

        public IEnumerable<PeptideDocNode> CreateFullPeptideDocNodes(SrmSettings settings, bool useFilter, string peptideSequence)
        {
            if (settings.PeptideSettings.Libraries.RankId == null)
            {
                foreach (var nodePep in CreatePeptideDocNodes(settings, useFilter, peptideSequence))
                    yield return nodePep.ChangeSettings(settings, SrmSettingsDiff.ALL);
            }
            else
            {
                var listDocNodes = new List<DocNode>();
                foreach (var nodePep in CreatePeptideDocNodes(settings, useFilter, peptideSequence))
                    listDocNodes.Add(nodePep.ChangeSettings(settings, SrmSettingsDiff.ALL));

                // Rank and filter peptides by rank.
                foreach (PeptideDocNode nodePep in RankPeptides(listDocNodes, settings, useFilter))
                    yield return nodePep;
            }
        }

        public PeptideDocNode CreateFullPeptideDocNode(SrmSettings settings, String peptideSequence)
        {
            peptideSequence = StripModifications(peptideSequence);
            foreach (var peptideDocNode in CreateFullPeptideDocNodes(settings, false, peptideSequence))
            {
                if (peptideSequence == peptideDocNode.Peptide.Sequence)
                    return peptideDocNode;
            }

            int begin = Sequence.IndexOf(peptideSequence, StringComparison.Ordinal);
            if (begin < 0)
                return null;

            var peptide = new Peptide(this, peptideSequence, begin, begin + peptideSequence.Length,
                settings.PeptideSettings.Enzyme.CountCleavagePoints(peptideSequence));

            return new PeptideDocNode(peptide)
                .ChangeSettings(settings, SrmSettingsDiff.ALL);
        }

        public static void ValidateSequence(string seq)
        {
            if (string.IsNullOrEmpty(seq))
                throw new InvalidDataException(Resources.FastaSequence_ValidateSequence_A_protein_sequence_may_not_be_empty);
            for (int i = 0; i < seq.Length; i++)
            {
                char c = seq[i];
                if (!AminoAcid.IsExAA(c) && c != '*' && c != '-') // Not L10N
                    throw new InvalidDataException(string.Format(Resources.FastaSequence_ValidateSequence_A_protein_sequence_may_not_contain_the_character__0__at__1__, seq[i], i));
            }
        }

        private void Validate()
        {
            ValidateSequence(Sequence);
        }

        #region object overrides

        public bool Equals(FastaSequence obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj._name, _name) &&
                Equals(obj._description, _description) &&
                Equals(obj._sequence, _sequence) &&
                ArrayUtil.EqualsDeep(obj.Alternatives, Alternatives) &&
                obj.IsDecoy == IsDecoy;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (FastaSequence)) return false;
            return Equals((FastaSequence) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (_name != null ? _name.GetHashCode() : 0);
                result = (result*397) ^ (_description != null ? _description.GetHashCode() : 0);
                result = (result*397) ^ _sequence.GetHashCode();
                result = (result*397) ^ Alternatives.GetHashCodeDeep();
                result = (result*397) ^ IsDecoy.GetHashCode();
                return result;
            }
        }

        public override string ToString()
        {
            return (_name ?? base.ToString());
        }

        #endregion // object overrides

        public static int ComparePeptides(PeptideDocNode node1, PeptideDocNode node2)
        {
            return ComparePeptides(node1.Peptide, node2.Peptide);
        }

        public static int ComparePeptides(Peptide pep1, Peptide pep2)
        {
            if (pep1.FastaSequence == null || pep2.FastaSequence == null)
                throw new InvalidOperationException(Resources.FastaSequence_ComparePeptides_Peptides_without_FASTA_sequence_information_may_not_be_compared);
            if (!ReferenceEquals(pep1.FastaSequence, pep2.FastaSequence))
                throw new InvalidOperationException(Resources.FastaSequence_ComparePeptides_Peptides_in_different_FASTA_sequences_may_not_be_compared);

            return Comparer<int>.Default.Compare(pep1.Order, pep2.Order);
        }

        public static string StripModifications(string seq)
        {
            return StripModifications(seq, RGX_ALL);
        }

        public static string StripModifications(string seq, Regex rgx)
        {
            // If the sequence begins with anything other than an AA or mod, 
            // it is not a valid modified sequence.
            if (seq.Length == 0 || (!AminoAcid.IsExAA(seq[0]) && !OPEN_MOD.Contains(seq[0])))
                return seq;
            string result = rgx.Replace(seq, String.Empty);
            // Some potential modified sequences begin with [3Lt]-AAAA. The above replace
            // will remove the bracketed modification, but not the dash.
            if (!result.Contains("\n")) // Not L10N
                result = RemoveLeadingDash(result);
            else
            {
                var sb = new StringBuilder();
                var reader = new StringReader(result);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = RemoveLeadingDash(line);
                    sb.AppendLine(line);
                }
                result = sb.ToString();
            }
            return result;
        }

        private static string RemoveLeadingDash(string line)
        {
            if (line.StartsWith("-")) // Not L10N
                line = line.Substring(1);
            return line;
        }

        public static string NormalizeNTerminalMod(string seq)
        {
            // Handle the case where the sequence begins with a modification
            if (seq.Length > 0 && OPEN_MOD.Contains(seq[0]))
            {
                char openBracket = seq[0];
                char closeBracket = CLOSE_MOD[OPEN_MOD.IndexOf(c => c == openBracket)];
                int indexClose = seq.IndexOf(closeBracket, 0);
                if (indexClose != -1 && indexClose < seq.Length - 1)
                {
                    // Move amino acid following the modification to before it
                    int indexFirstAA = indexClose + 1;
                    if (seq[indexFirstAA] == '-')
                        indexFirstAA++;
                    seq = seq[indexFirstAA] + seq.Substring(0, indexClose + 1) + seq.Substring(indexFirstAA + 1);
                }
            }
            return seq;
        }
    }

// ReSharper disable InconsistentNaming
    public enum SequenceTerminus { N, C }
// ReSharper restore InconsistentNaming
}
