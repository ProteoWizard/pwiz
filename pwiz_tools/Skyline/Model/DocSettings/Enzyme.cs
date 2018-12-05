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
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Used to get a set of predicted peptides from an FASTA sequence.
    /// </summary>
    [XmlRoot("enzyme")]
    public sealed class Enzyme : XmlNamedElement
    {
        private char[] _cleavageC;
        private char[] _restrictC;
        private char[] _cleavageN;
        private char[] _restrictN;
        private char[] _cleavageAll;

        private static readonly char[] nonAAs = {'*', '-'};

        public Enzyme(string name, string cleavage, string restrict)
            : this(name, cleavage, restrict, null, null)
        {
        }

        public Enzyme(string name, string cleavage, string restrict, SequenceTerminus type)
            : this(name,
                type == SequenceTerminus.C ? cleavage : null,
                type == SequenceTerminus.C ? restrict : null,
                type == SequenceTerminus.N ? cleavage : null,
                type == SequenceTerminus.N ? restrict : null)
        {            
        }

        public Enzyme(string name, string cleavage, string restrict, string cleavageN, string restrictN, bool isSemiCleaving = false)
            : base(name)
        {
            CleavageC = MakeEmptyNull(cleavage);
            RestrictC = MakeEmptyNull(restrict);
            CleavageN = MakeEmptyNull(cleavageN);
            RestrictN = MakeEmptyNull(restrictN);

            IsSemiCleaving = isSemiCleaving;

            Validate();
        }

        private static string MakeEmptyNull(string s)
        {
            return !string.IsNullOrEmpty(s) ? s : null;
        }

        [Track]
        public bool IsSemiCleaving { get; private set; }
        [Track]
        public string CleavageC { get; private set; }
        [Track]
        public string RestrictC { get; private set; }
        [Track]
        public string CleavageN { get; private set; }
        [Track]
        public string RestrictN { get; private set; }

        [Track]
        public SequenceTerminus? Type
        {
            get
            {
                if (string.IsNullOrEmpty(CleavageN))
                    return SequenceTerminus.C;
                if (string.IsNullOrEmpty(CleavageC))
                    return SequenceTerminus.N;
                return null;
            }
        }

        public bool IsCTerm { get { return Type == SequenceTerminus.C; } }
        public bool IsNTerm { get { return Type == SequenceTerminus.N; } }
        public bool IsBothTerm { get { return Type == null; } }

        public Enzyme ChangeSemiCleaving(bool isSemi)
        {
            return ChangeProp(ImClone(this), im => im.IsSemiCleaving = isSemi);
        }

        // Using this handrolled Regex equivalent because actual Regex is a bottleneck in multithreading
        // due to cacheing of the compiled regex by the system - the cache can be a lock contention
        private List<int> GetMatches(string sequence)
        {
            var len = sequence.Length;
            var result = new List<int>();
            var last = -1;
            for (var startat = 0; (startat = sequence.IndexOfAny(_cleavageAll, startat)) >= 0; startat++)
            {
                if (_cleavageC != null && startat < len - 1 &&  // Never matches the end
                       Array.Exists(_cleavageC, c => c == sequence[startat]) &&
                       (_restrictC == null || !Array.Exists(_restrictC, c => c == sequence[startat + 1])))
                {
                    if (startat != last)
                        result.Add(last = startat);
                }
                else if (_cleavageN != null && startat > 0 && // Never matches the start
                      Array.Exists(_cleavageN, c => c == sequence[startat]) &&
                      (_restrictN == null || !Array.Exists(_restrictN, c => c == sequence[startat - 1])))
                {
                    if (startat-1 != last)
                        result.Add(last = startat - 1);
                }
            }
            return result;
        }

        private class Matcher
        {
            private readonly int[] _matches;
            private int _lastMatch;
            private int _lastResetMatch = -1;

            public Matcher(string sequence, List<int> matches )
            {
                Length = sequence.Length;
                _matches = matches.ToArray();
            }

            public int Length { get; private set; }

            public void Reset()
            {
                _lastMatch = ++_lastResetMatch;
            }

            public int? Match(int startat)
            {
                if (_lastMatch >= _matches.Length)
                {
                    if (_matches.Length == 0 || _matches[_matches.Length - 1] < startat)
                        return null;
                    _lastMatch = _lastResetMatch = 0;
                }
                while (startat > _matches[_lastMatch])
                {
                    _lastMatch++;
                    if (_lastMatch >= _matches.Length)
                    {
                        return null;
                    }
                }
                return _matches[_lastMatch];
            }
        }

        public IEnumerable<Peptide> Digest(FastaSequence fastaSeq, DigestSettings settings, int? maxPeptideSequenceLength = null, int? minPeptideSequenceLength = null)
        {
            int begin = 0;
            var sequence = fastaSeq.Sequence;
            int len = sequence.Length;
            int maxPepLen = maxPeptideSequenceLength ?? int.MaxValue;
            int minPepLen = minPeptideSequenceLength ?? 1;
            var matcher = new Matcher(sequence, GetMatches(sequence));
            while (begin < len)
            {
                matcher.Reset();
                int end = begin;
                int endFirst = begin;
                int missed = 0;
                do
                {
                    int endLast = end;
                    var m = matcher.Match(end);
                    end = m + 1 ?? len;

                    // Save the end of the first cleavage
                    if (missed == 0)
                        endFirst = end;

                    // Deal with 'ragged ends', or cleavages one amino acid apart
                    // i.e. KR, RR, etc. for trypsin
                    if (settings.ExcludeRaggedEnds && end < len)
                    {
                        var mNext = matcher.Match(end);
                        if (mNext.HasValue && mNext == end)
                        {
                            // If there are no missed cleavages, then move the
                            // begin index to the next cleavage point that is
                            // not part of a run.
                            if (missed == 0)
                                endFirst = GetDiscontiguousCleavageIndex(matcher, mNext);
                            break;
                        }
                    }

                    // If the sequence is longer than we care to consider, quit
                    int count = end - begin;
                    bool tooLong = maxPeptideSequenceLength.HasValue && count > maxPeptideSequenceLength;

                    if (!minPeptideSequenceLength.HasValue || count >= minPeptideSequenceLength.Value)
                    {
                        // Single amino acid peptides have no fragment ions.
                        if (count > 1 && sequence.IndexOfAny(nonAAs, begin, count) == -1)
                        {
                            if (!tooLong)
                            {
                                yield return new Peptide(fastaSeq, sequence.Substring(begin, end - begin),
                                    begin, end, missed);
                            }

                            if (IsSemiCleaving)
                            {
                                // Add N-terminal semi-cleavage, which excludes peptides already included in prior peptide,
                                // when missed cleavage occurs
                                for (int i = Math.Min(count - 1, maxPepLen); i >= Math.Max(endLast - begin + 1, minPepLen); i--)
                                    yield return new Peptide(fastaSeq, sequence.Substring(begin, i), begin, begin + i, missed);
                                // Add C-terminal semi-cleavage, which excludes peptides to come in subsequent peptides,
                                // when missed cleavage occurs
                                for (int i = Math.Min(count - 1, maxPepLen); i >= Math.Max(end - endFirst + 1, minPepLen); i--)
                                    yield return new Peptide(fastaSeq, sequence.Substring(end - i, i), end - i, end, missed);
                            }
                        }
                    }

                    if (tooLong)
                        break;

                    // Increment missed cleavages for next loop.
                    missed++;
                }
                while (end < len && missed <= settings.MaxMissedCleavages);

                begin = endFirst;
            }
        }

        /// <summary>
        /// Counts the number of cleavage locations in a sequence.  Useful for
        /// calculating missed cleavages on peptide sequences that were not
        /// generated with the <see cref="Digest"/> function.
        /// </summary>
        /// <param name="seq">The sequence to inspect</param>
        /// <returns>Number of cleavage points</returns>
        public int CountCleavagePoints(string seq)
        {
            return GetMatches(seq).Count;
        }

        /// <summary>
        /// Returns the next cleavage index that is more than one
        /// amino acid beyond the last.
        /// </summary>
        /// <param name="matcher">state of current match set</param>
        /// <param name="mNext">Previously matched cleavage</param>
        /// <returns>The index of the next valid match</returns>
        private int GetDiscontiguousCleavageIndex(Matcher matcher, int? mNext)
        {
            // Loop while matches are one amino acid apart.
            int? mLast;
            do
            {
                do
                {
                    mLast = mNext;
                    mNext = matcher.Match(mLast.Value + 1);
                }
                while (mNext.HasValue && mNext == mLast + 1);

                // Make sure this new cleavage point is not itself part of a run
                // of cleavage points.
                mLast = mNext;
                if (mNext.HasValue)
                    mNext = matcher.Match(mNext.Value + 1);
            }
            while (mNext.HasValue && mNext.Value == mLast.Value + 1);

            // Return the cleavage point, or the end of the string.
            return mLast + 1 ?? matcher.Length;
        }

        /// <summary>
        /// Enzyme uses its full <see cref="ToString"/> value as its key.
        /// </summary>
        /// <returns>ToString value for use as key in the settings list</returns>
        public override string GetKey()
        {
            return ToString();
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private Enzyme()
        {
        }

        private enum ATTR
        {
            cut,
            no_cut,
            sense,
            cut_c,
            no_cut_c,
            cut_n,
            no_cut_n,
            semi,
        }

        private void Validate()
        {
            if (string.IsNullOrEmpty(CleavageC) && string.IsNullOrEmpty(CleavageN))
                throw new InvalidDataException(Resources.Enzyme_Validate_Enzymes_must_have_at_least_one_cleavage_point);
            var cleavageAll = String.Empty;
            if (string.IsNullOrEmpty(CleavageC))
            {
                if (!string.IsNullOrEmpty(RestrictC))
                    throw new InvalidDataException(Resources.Enzyme_Validate_Enzyme_must_have_C_terminal_cleavage_to_have_C_terminal_restrictions_);
                CleavageC = RestrictC = null;
                _cleavageC = null;
                _restrictC = null;
            }
            else
            {
                AminoAcid.ValidateAAList(CleavageC);
                if (!string.IsNullOrEmpty(RestrictC))
                    AminoAcid.ValidateAAList(RestrictC);
                _cleavageC = CleavageC.ToCharArray();
                _restrictC = RestrictC == null? null : RestrictC.ToCharArray();
                cleavageAll = CleavageC;
            }
            if (string.IsNullOrEmpty(CleavageN))
            {
                if (!string.IsNullOrEmpty(RestrictN))
                    throw new InvalidDataException(Resources.Enzyme_Validate_Enzyme_must_have_N_terminal_cleavage_to_have_N_terminal_restrictions_);
                CleavageN = RestrictN = null;
                _cleavageN = null;
                _restrictN = null;
            }
            else
            {
                AminoAcid.ValidateAAList(CleavageN);
                if (!string.IsNullOrEmpty(RestrictN))
                    AminoAcid.ValidateAAList(RestrictN);

                _cleavageN = CleavageN.ToCharArray();
                _restrictN = RestrictN == null ? null : RestrictN.ToCharArray();
                cleavageAll += CleavageN;
            }
            _cleavageAll = cleavageAll.ToCharArray();
        }

        private static SequenceTerminus ToSeqTerminus(string value)
        {
            return (SequenceTerminus)Enum.Parse(typeof(SequenceTerminus), value, true);
        }

        public static Enzyme Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new Enzyme());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            IsSemiCleaving = reader.GetBoolAttribute(ATTR.semi);
            CleavageC = reader.GetAttribute(ATTR.cut) ?? reader.GetAttribute(ATTR.cut_c);
            RestrictC = MakeEmptyNull(reader.GetAttribute(ATTR.no_cut) ?? reader.GetAttribute(ATTR.no_cut_c));
            var type = reader.GetAttribute(ATTR.sense, ToSeqTerminus);
            if (!type.HasValue)
            {
                CleavageN = reader.GetAttribute(ATTR.cut_n);
                RestrictN = MakeEmptyNull(reader.GetAttribute(ATTR.no_cut_n));
            }
            else if (type.Value == SequenceTerminus.N)
            {
                CleavageN = CleavageC;
                CleavageC = null;
                RestrictN = RestrictC;
                RestrictC = null;
            }
            // Consume tag
            reader.Read();

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            // If not cleavage both directions, then write the enzyme out the old way
            if (IsBothTerm)
            {
                writer.WriteAttributeString(ATTR.cut_c, CleavageC);
                writer.WriteAttributeString(ATTR.no_cut_c, RestrictC);
                writer.WriteAttributeString(ATTR.cut_n, CleavageN);
                writer.WriteAttributeString(ATTR.no_cut_n, RestrictN);                
            }
            else if (IsCTerm)
            {
                writer.WriteAttributeString(ATTR.cut, CleavageC);
                writer.WriteAttributeString(ATTR.no_cut, RestrictC);
                writer.WriteAttribute(ATTR.sense, SequenceTerminus.C);
            }
            else
            {
                writer.WriteAttributeString(ATTR.cut, CleavageN);
                writer.WriteAttributeString(ATTR.no_cut, RestrictN);
                writer.WriteAttribute(ATTR.sense, SequenceTerminus.N);
            }
            writer.WriteAttribute(ATTR.semi, IsSemiCleaving);
        }

        #endregion

        #region object overrides

        public override string ToString()
        {
            string textC = ToString(CleavageC, RestrictC, SequenceTerminus.C);
            string textN = ToString(CleavageN, RestrictN, SequenceTerminus.N);
            if (string.IsNullOrEmpty(textN))
                return string.Format(@"{0} {1}", Name, textC);
            if (string.IsNullOrEmpty(textC))
                return string.Format(@"{0} {1} n-term", Name, textN);
            return string.Format(@"{0} {1} c-term & {2} n-term", Name, textC, textN);
        }

        public override string AuditLogText
        {
            get { return ToString(); }
        }

        private static string ToString(string cleavage, string restrict, SequenceTerminus term)
        {
            if (string.IsNullOrEmpty(cleavage))
                return string.Empty;
            if (string.IsNullOrEmpty(restrict))
                restrict = @"-";
            return term == SequenceTerminus.C
                ? @"[" + cleavage + @" | " + restrict + @"]"
                : @"[" + restrict + @" | " + cleavage + @"]";
        }

        private bool Equals(Enzyme other)
        {
            return base.Equals(other) &&
                string.Equals(CleavageC, other.CleavageC) &&
                string.Equals(RestrictC, other.RestrictC) &&
                string.Equals(CleavageN, other.CleavageN) &&
                string.Equals(RestrictN, other.RestrictN) &&
                IsSemiCleaving == other.IsSemiCleaving;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Enzyme && Equals((Enzyme) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ (CleavageC != null ? CleavageC.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (RestrictC != null ? RestrictC.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (CleavageN != null ? CleavageN.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (RestrictN != null ? RestrictN.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ IsSemiCleaving.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }

    /// <summary>
    /// Settings used with an <see cref="Enzyme"/> to digest
    /// a <see cref="FastaSequence"/> into a <see cref="Peptide"/>
    /// collection.
    /// </summary>
    [XmlRoot("digest_settings")]
    public sealed class DigestSettings : IXmlSerializable
    {
        public const int MIN_MISSED_CLEAVAGES = 0;
        public const int MAX_MISSED_CLEAVAGES = 9;

        public DigestSettings(int maxMissedCleavages, bool excludeRaggedEnds)
        {
            MaxMissedCleavages = maxMissedCleavages;
            ExcludeRaggedEnds = excludeRaggedEnds;
        }

        [Track]
        public int MaxMissedCleavages { get; private set; }

        [Track]
        public bool ExcludeRaggedEnds { get; private set; }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private DigestSettings()
        {
        }

        private enum ATTR
        {
            max_missed_cleavages,
            exclude_ragged_ends
        }

        private void Validate()
        {
            ValidateIntRange(Resources.DigestSettings_Validate_maximum_missed_cleavages, MaxMissedCleavages,
                MIN_MISSED_CLEAVAGES, MAX_MISSED_CLEAVAGES);
        }

        private static void ValidateIntRange(string label, int n, int min, int max)
        {
            if (min > n || n > max)
            {
                throw new InvalidDataException(string.Format(Resources.DigestSettings_ValidateIntRange_The_value__1__for__0__must_be_between__2__and__3__,
                                                             label, n, min, max));
            }
        }

        public static DigestSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new DigestSettings());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            MaxMissedCleavages = reader.GetIntAttribute(ATTR.max_missed_cleavages);
            ExcludeRaggedEnds = reader.GetBoolAttribute(ATTR.exclude_ragged_ends);
            // Consume tag
            reader.Read();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR.max_missed_cleavages, MaxMissedCleavages);
            writer.WriteAttribute(ATTR.exclude_ragged_ends, ExcludeRaggedEnds);
        }

        #endregion

        #region object overrides

        public bool Equals(DigestSettings obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.MaxMissedCleavages == MaxMissedCleavages &&
                   obj.ExcludeRaggedEnds.Equals(ExcludeRaggedEnds);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (DigestSettings)) return false;
            return Equals((DigestSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (MaxMissedCleavages*397) ^ ExcludeRaggedEnds.GetHashCode();
            }
        }

        #endregion
    }

    /// <summary>
    /// Describes a single regular expression to exclude from a
    /// <see cref="Peptide"/> collection.
    /// </summary>
    [XmlRoot("exclusion")]
    public sealed class PeptideExcludeRegex : XmlNamedElement
    {
        public PeptideExcludeRegex(string name, string regex)
            :  this(name, regex, false, false)
        {
        }

        public PeptideExcludeRegex(string name, string regex, bool includeMatch, bool matchMod)
            : base(name)
        {
            Regex = regex;
            IsIncludeMatch = includeMatch;
            IsMatchMod = matchMod;

            Validate();
        }

        /// <summary>
        /// The regular expression string to exclude, if a peptide
        /// sequence matches
        /// </summary>
        [Track]
        public string Regex { get; private set; }
        //TODO: custom localzier? might need a newer one
        [Track]
        public bool IsIncludeMatch { get; private set; }

        /// <summary>
        /// True if the filter should be applied to the light strutural
        /// modified sequence string.
        /// </summary>
        [Track]
        public bool IsMatchMod { get; private set; }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization.
        /// </summary>
        private PeptideExcludeRegex()
        {
        }

        private enum ATTR
        {
            regex,
            include,
            match_mod_sequence
        }

        private void Validate()
        {
            if (string.IsNullOrEmpty(Regex))
                throw new InvalidDataException(Resources.PeptideExcludeRegex_Validate_Peptide_exclusion_must_have_a_regular_expression);
        }

        public static PeptideExcludeRegex Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new PeptideExcludeRegex());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            Regex = reader.GetAttribute(ATTR.regex);
            IsIncludeMatch = reader.GetBoolAttribute(ATTR.include);
            IsMatchMod = reader.GetBoolAttribute(ATTR.match_mod_sequence);
            // Consume tag
            reader.Read();

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttributeString(ATTR.regex, Regex);
            writer.WriteAttribute(ATTR.include, IsIncludeMatch);
            writer.WriteAttribute(ATTR.match_mod_sequence, IsMatchMod);
        }

        #endregion

        #region object overrides

        public bool Equals(PeptideExcludeRegex other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other.Regex, Regex) &&
                other.IsIncludeMatch.Equals(IsIncludeMatch) &&
                other.IsMatchMod.Equals(IsMatchMod);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as PeptideExcludeRegex);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ Regex.GetHashCode();
                result = (result*397) ^ IsIncludeMatch.GetHashCode();
                result = (result*397) ^ IsMatchMod.GetHashCode();
                return result;
            }
        }

        #endregion // object overrides
    }
}
