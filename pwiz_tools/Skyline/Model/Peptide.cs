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
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class Peptide : Identity  // "Peptide" is a misnomer at this point - this could be a small molecule or a peptide.
    {
        private readonly FastaSequence _fastaSequence;

        public Peptide(string sequence)
            :this(null, sequence, null, null, 0)
        {
        }

        public Peptide(FastaSequence fastaSequence, string sequence, int? begin, int? end, int missedCleavages)
            :this(fastaSequence, sequence, begin, end, missedCleavages, false)
        {
        }

        public Peptide(FastaSequence fastaSequence, string sequence, int? begin, int? end, int missedCleavages, bool isDecoy)
        {
            _fastaSequence = fastaSequence;

            Sequence = sequence;
            Begin = begin;
            End = end;
            MissedCleavages = missedCleavages;
            IsDecoy = isDecoy;

            Validate();
        }

        public Peptide(DocNodeCustomIon customIon)
        {
            CustomIon = customIon;

            Validate();
        }

        public FastaSequence FastaSequence { get { return _fastaSequence; } }

        public string Sequence { get; private set; } // If this is null, expect CustomIon not null
        public int? Begin { get; private set; }
        public int? End { get; private set; } // non-inclusive
        public int MissedCleavages { get; private set; }
        public bool IsDecoy { get; private set; }
        public DocNodeCustomIon CustomIon { get; private set;} // If this is null, expect Sequence not null
        public bool IsCustomIon { get { return (CustomIon != null); }}
        public int Length { get { return Sequence != null ? Sequence.Length : 0; }}

        // CONSIDER: Polymorphism?
        public string TextId { get { return IsCustomIon ? CustomIon.InvariantName : Sequence; } }

        public int Order { get { return Begin ?? 0; } }

        public char PrevAA
        {
            get
            {
                if (!Begin.HasValue)
                    return 'X'; // Not L10N
                int begin = Begin.Value;
                return (begin == 0 ? '-' : _fastaSequence.Sequence[begin - 1]); // Not L10N
            }
        }

        public char NextAA
        {
            get
            {
                if (!End.HasValue)
                    return 'X';
                int end = End.Value;
                return (end == _fastaSequence.Sequence.Length ? '-' : _fastaSequence.Sequence[end]); // Not L10N
            }
        }

        public static int CompareGroups(DocNode node1, DocNode node2)
        {
            return CompareGroups((TransitionGroupDocNode) node1, (TransitionGroupDocNode) node2);    
        }

        public static int CompareGroups(TransitionGroupDocNode node1, TransitionGroupDocNode node2)
        {
            return CompareGroups(node1.TransitionGroup, node2.TransitionGroup);
        }

        public static int CompareGroups(TransitionGroup group1, TransitionGroup group2)
        {
            int chargeDiff = group1.PrecursorCharge - group2.PrecursorCharge;
            if (chargeDiff != 0)
                return chargeDiff;
            return group1.LabelType.CompareTo(group2.LabelType);
        }

        public IEnumerable<TransitionGroup> GetTransitionGroups(SrmSettings settings, PeptideDocNode nodePep, ExplicitMods mods, bool useFilter)
        {
            if (IsCustomIon)
            {
                // We can't generate nodes as we do with peptides, so just filter what we do have on instrument mz range
                foreach (var group in nodePep.TransitionGroups.Where(tranGroup => tranGroup.TransitionGroup.IsCustomIon))
                {
                    if (!useFilter || settings.TransitionSettings.IsMeasurablePrecursor(group.PrecursorMz))
                      yield return group.TransitionGroup;
                }
            }
            else
            {
                IList<int> precursorCharges = settings.TransitionSettings.Filter.PrecursorCharges;
                if (!useFilter)
                {
                    precursorCharges = new List<int>();
                    for (int i = TransitionGroup.MIN_PRECURSOR_CHARGE; i < TransitionGroup.MAX_PRECURSOR_CHARGE; i++)
                        precursorCharges.Add(i);
                }

                var modSettings = settings.PeptideSettings.Modifications;

                double precursorMassLight = settings.GetPrecursorMass(IsotopeLabelType.light, Sequence, mods);
                var listPrecursorMasses = new List<KeyValuePair<IsotopeLabelType, double>>
                {new KeyValuePair<IsotopeLabelType, double>(IsotopeLabelType.light, precursorMassLight)};

                foreach (var typeMods in modSettings.GetHeavyModifications())
                {
                    IsotopeLabelType labelType = typeMods.LabelType;
                    double precursorMass = precursorMassLight;
                    if (settings.HasPrecursorCalc(labelType, mods))
                        precursorMass = settings.GetPrecursorMass(labelType, Sequence, mods);

                    listPrecursorMasses.Add(new KeyValuePair<IsotopeLabelType, double>(labelType, precursorMass));
                }

                foreach (int charge in precursorCharges)
                {
                    if (useFilter && !settings.Accept(settings, this, mods, charge))
                        continue;

                    for (int i = 0; i < listPrecursorMasses.Count; i++)
                    {
                        var pair = listPrecursorMasses[i];
                        IsotopeLabelType labelType = pair.Key;
                        double precursorMass = pair.Value;
                        // Only return a heavy group, if the precursor masses differ
                        // between the light and heavy calculators
                        if (i == 0 || precursorMass != precursorMassLight)
                        {
                            if (settings.TransitionSettings.IsMeasurablePrecursor(SequenceMassCalc.GetMZ(precursorMass,charge)))
                                yield return new TransitionGroup(this, null, charge, labelType);
                        }
                    }
                }
            }
        }

        public IEnumerable<PeptideDocNode> CreateDocNodes(SrmSettings settings, IPeptideFilter filter)
        {
            int maxModCount = filter.MaxVariableMods ?? settings.PeptideSettings.Modifications.MaxVariableMods;

            // Always return the unmodified peptide doc node first
            var nodePepUnmod = new PeptideDocNode(this);
            bool allowVariableMods;
            if (filter.Accept(settings, this, null, out allowVariableMods))
                yield return nodePepUnmod;

            // Stop if no variable modifications are allowed for this peptide.
            if (!allowVariableMods || maxModCount == 0)
                yield break;

            // First build a list of the amino acids in this peptide which can be modified,
            // and the modifications which apply to them.
            List<KeyValuePair<IList<StaticMod>, int>> listListMods = null;

            var mods = settings.PeptideSettings.Modifications;
            // Nothing to do, if no variable mods in the document
            if (mods.HasVariableModifications)
            {
                // Enumerate each amino acid in the sequence
                int len = Sequence.Length;
                for (int i = 0; i < len; i++)
                {
                    char aa = Sequence[i];
                    // Test each modification to see if it applies
                    foreach (var mod in mods.VariableModifications)
                    {
                        // If the modification does apply, store it in the list
                        if (mod.IsMod(aa, i, len))
                        {
                            if (listListMods == null)
                                listListMods = new List<KeyValuePair<IList<StaticMod>, int>>();
                            if (listListMods.Count == 0 || listListMods[listListMods.Count - 1].Value != i)
                                listListMods.Add(new KeyValuePair<IList<StaticMod>, int>(new List<StaticMod>(), i));
                            listListMods[listListMods.Count - 1].Key.Add(mod);
                        }
                    }
                }
            }

            // If no applicable modifications were found, return a single DocNode for the
            // peptide passed in
            if (listListMods == null)
                yield break;

            maxModCount = Math.Min(maxModCount, listListMods.Count);
            for (int modCount = 1; modCount <= maxModCount; modCount++)
            {
                var modStateMachine = new VariableModStateMachine(nodePepUnmod, modCount, listListMods);
                foreach (var nodePep in modStateMachine.GetStates())
                {
                    if (filter.Accept(settings, nodePep.Peptide, nodePep.ExplicitMods, out allowVariableMods))
                        yield return nodePep;
                }
            }
        }

        /// <summary>
        /// Returns all possible variably modified <see cref="PeptideDocNode"/> objects
        /// for a peptide sequence under specific settings.
        /// </summary>
        public static IEnumerable<PeptideDocNode> CreateAllDocNodes(SrmSettings settings, string sequence)
        {
            var peptide = new Peptide(null, sequence, null, null,
                settings.PeptideSettings.Enzyme.CountCleavagePoints(sequence));
            return CreateAllDocNodes(settings, peptide);
        }

        public static IEnumerable<PeptideDocNode> CreateAllDocNodes(SrmSettings settings, Peptide peptide)
        {
            return peptide.CreateDocNodes(settings, PeptideFilter.UNFILTERED);
        }

        /// <summary>
        /// State machine that provides a <see cref="IEnumerable{PeptideDocNode}"/> for
        /// enumerating all modified states of a peptide, given the peptide, a number of
        /// possible modifications, and the set of possible modifications.
        /// </summary>
        private sealed class VariableModStateMachine : ModificationStateMachine<StaticMod, ExplicitMod, PeptideDocNode>
        {
            private readonly PeptideDocNode _nodePepUnmod;

            public VariableModStateMachine(PeptideDocNode nodePepUnmod, int modCount,
                IList<KeyValuePair<IList<StaticMod>, int>> listListMods)
                : base(modCount, listListMods)
            {
                _nodePepUnmod = nodePepUnmod;
            }

            protected override ExplicitMod CreateMod(int indexAA, StaticMod mod)
            {
                return new ExplicitMod(indexAA, mod);
            }

            protected override PeptideDocNode CreateState(ExplicitMod[] mods)
            {
                var explicitMods = new ExplicitMods(_nodePepUnmod.Peptide, mods,
                                                    new TypedExplicitModifications[0], true);
                // Make a new copy of the peptid ID to give it a new GlobalIndex.
                return new PeptideDocNode((Peptide)_nodePepUnmod.Peptide.Copy(), explicitMods, _nodePepUnmod.ExplicitRetentionTime);
            }
        }

        private void Validate()
        {
            if (IsCustomIon)
            {
                Assume.IsNull(_fastaSequence);
                Assume.IsNull(Sequence);
                CustomIon.Validate();
            }
            else if (_fastaSequence == null)
            {
                if (Begin.HasValue || End.HasValue)
                    throw new InvalidDataException(Resources.Peptide_Validate_Peptides_without_a_protein_sequence_do_not_support_the_start_and_end_properties);

                // No FastaSequence checked the sequence, so check it here.
                FastaSequence.ValidateSequence(Sequence);
            }
            else
            {
                // Otherwise, validate the peptide sequence against the group sequence
                if (!Begin.HasValue || !End.HasValue)
                    throw new InvalidDataException(Resources.Peptide_Validate_Peptides_from_protein_sequences_must_have_start_and_end_values);
                if (0 > Begin.Value || End.Value > _fastaSequence.Sequence.Length)
                    throw new InvalidDataException(Resources.Peptide_Validate_Peptide_sequence_exceeds_the_bounds_of_the_protein_sequence);

                string sequenceCheck = _fastaSequence.Sequence.Substring(Begin.Value, End.Value - Begin.Value);
                if (!Equals(Sequence, sequenceCheck))
                {
                    throw new InvalidDataException(
                        string.Format(Resources.Peptide_Validate_The_peptide_sequence__0__does_not_agree_with_the_protein_sequence__1__at__2__3__,
                                      Sequence, sequenceCheck, Begin.Value, End.Value));
                }
            }
            // CONSIDER: Validate missed cleavages some day?
        }

        #region object overrides

        public bool Equals(Peptide obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var equal = Equals(obj._fastaSequence, _fastaSequence) &&
                Equals(obj.Sequence, Sequence) &&
                obj.Begin.Equals(Begin) &&
                obj.End.Equals(End) &&
                obj.MissedCleavages == MissedCleavages &&
                Equals(obj.CustomIon, CustomIon) &&
                obj.IsDecoy == IsDecoy;
            return equal; // For debugging convenience
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Peptide)) return false;
            return Equals((Peptide) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (_fastaSequence != null ? _fastaSequence.GetHashCode() : 0);
                result = (result*397) ^ (Sequence != null ? Sequence.GetHashCode() : 0);
                result = (result*397) ^ (Begin.HasValue ? Begin.Value : 0);
                result = (result*397) ^ (End.HasValue ? End.Value : 0);
                result = (result*397) ^ MissedCleavages;
                result = (result*397) ^ IsDecoy.GetHashCode();
                result = (result*397) ^ (CustomIon != null ? CustomIon.GetHashCode() : 0);
                return result;
            }
        }

        public override string ToString()
        {
            if (IsCustomIon)
                return CustomIon.DisplayName;

            if (!Begin.HasValue || !End.HasValue)
            {
                if (MissedCleavages == 0)
                    return Sequence;
                else
                    return string.Format(TextUtil.SpaceSeparate(Sequence, Resources.Peptide_ToString_missed__0__), MissedCleavages);
            }
            else
            {
                string format = "{0}.{1}.{2} [{3}, {4}]"; // Not L10N
                if (MissedCleavages > 0)
                    format = TextUtil.SpaceSeparate(format, Resources.Peptide_ToString__missed__5__);
                return string.Format(format, PrevAA, Sequence, NextAA, Begin.Value, End.Value - 1, MissedCleavages);
            }
        }

        #endregion
    }

    /// <summary>
    /// State machine that provides a <see cref="IEnumerable{TState}"/> for
    /// enumerating modified states of a peptide, given a number of possible
    /// modifications, and the set of possible modifications in a list by amino
    /// acid index.
    /// </summary>
    internal abstract class ModificationStateMachine<TMod, TExMod, TState>
    {
        private readonly int _modCount;
        private readonly IList<KeyValuePair<IList<TMod>, int>> _listListMods;

        /// <summary>
        /// Contains indexes into _listListMods specifying amino acids currently
        /// modified.
        /// </summary>
        private readonly int[] _arrayModIndexes1;

        /// <summary>
        /// Contains indexes into the static mod lists of _listListMods specifying
        /// which modification is currently applied to the amino acid specified
        /// by _arrayModIndexes1.
        /// </summary>
        private readonly int[] _arrayModIndexes2;

        /// <summary>
        /// Index to the currently active elements in _arrayModIndexes arrays.
        /// </summary>
        private int _cursorIndex;

        protected ModificationStateMachine(int modCount, IList<KeyValuePair<IList<TMod>, int>> listListMods)
        {
            _modCount = modCount;
            _listListMods = listListMods;

            // Fill the mod indexes list with the first possible state
            _arrayModIndexes1 = new int[_modCount];
            for (int i = 0; i < modCount; i++)
                _arrayModIndexes1[i] = i;
            // Second set of indexes start all zero initialized
            _arrayModIndexes2 = new int[_modCount];
            // Set the cursor to the last modification
            _cursorIndex = modCount - 1;
        }

        public IEnumerable<TState> GetStates()
        {
            while (_cursorIndex >= 0)
            {
                yield return Current;

                if (!ShiftCurrentMod())
                {
                    // Attempt to advance any mod to the left of the current mod
                    do
                    {
                        _cursorIndex--;
                    }
                    while (_cursorIndex >= 0 && !ShiftCurrentMod());

                    // If a mod was successfully advanced, reset all mods to its right
                    // and start over with them.
                    if (_cursorIndex >= 0)
                    {
                        for (int i = 1; i < _modCount - _cursorIndex; i++)
                        {
                            _arrayModIndexes1[_cursorIndex + i] = _arrayModIndexes1[_cursorIndex] + i;
                            _arrayModIndexes2[_cursorIndex + i] = 0;
                        }
                        _cursorIndex = _modCount - 1;
                    }
                }
            }
        }

        private bool ShiftCurrentMod()
        {
            int modIndex = _arrayModIndexes1[_cursorIndex];
            if (_arrayModIndexes2[_cursorIndex] < _listListMods[modIndex].Key.Count - 1)
            {
                // Shift the current amino acid through all possible modification states
                _arrayModIndexes2[_cursorIndex]++;
            }
            else if (modIndex < _listListMods.Count - _modCount + _cursorIndex)
            {
                // Shift the current modification through all possible positions
                _arrayModIndexes1[_cursorIndex]++;
                _arrayModIndexes2[_cursorIndex] = 0;
            }
            else
            {
                // Current modification has seen all possible states
                return false;
            }
            return true;
        }

        private TState Current
        {
            get
            {
                var mods = new TExMod[_modCount];
                for (int i = 0; i < _modCount; i++)
                {
                    var pair = _listListMods[_arrayModIndexes1[i]];
                    var mod = pair.Key[_arrayModIndexes2[i]];

                    mods[i] = CreateMod(pair.Value, mod);
                }
                return CreateState(mods);
            }
        }

        protected abstract TExMod CreateMod(int indexAA, TMod mod);

        protected abstract TState CreateState(TExMod[] mods);
    }

    public sealed class PeptideModKey
    {
        public PeptideModKey(Peptide peptide, ExplicitMods modifications)
        {
            Peptide = peptide;
            Modifications = modifications;
        }

        private Peptide Peptide { get; set; }
        private ExplicitMods Modifications { get; set; }

        #region object overrides

        private bool Equals(PeptideModKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Peptide, Peptide) &&
                Equals(other.Modifications, Modifications);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(PeptideModKey)) return false;
            return Equals((PeptideModKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Peptide.GetHashCode() * 397) ^
                    (Modifications != null ? Modifications.GetHashCode() : 0);
            }
        }

        #endregion
    }

    public sealed class PeptideSequenceModKey
    {
        public PeptideSequenceModKey(Peptide peptide, ExplicitMods modifications)
        {
            Sequence = peptide.Sequence;
            // For consistent keys in peptide matching, clear the variable flag, if it is present.
            Modifications = modifications != null && modifications.IsVariableStaticMods
                ? modifications.ChangeIsVariableStaticMods(false)
                : modifications;
            IsDecoy = peptide.IsDecoy;
            CustomIon = peptide.CustomIon;
        }

        private string Sequence { get; set; }
        private ExplicitMods Modifications { get; set; }
        private bool IsDecoy { get; set; }
        private CustomIon CustomIon { get; set; }

        #region object overrides

        private bool Equals(PeptideSequenceModKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(CustomIon, other.CustomIon) &&
                Equals(other.Sequence, Sequence) &&
                Equals(other.Modifications, Modifications) &&
                Equals(other.IsDecoy, IsDecoy);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(PeptideSequenceModKey)) return false;
            return Equals((PeptideSequenceModKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Sequence != null ? Sequence.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Modifications != null ? Modifications.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsDecoy.GetHashCode();
                hashCode = (hashCode * 397) ^ (CustomIon != null ? CustomIon.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }

    public sealed class ModifiedSequenceMods
    {
        public ModifiedSequenceMods(string modifiedSequence, ExplicitMods explicitMods)
        {
            ModifiedSequence = modifiedSequence;
            // Strip explicit mods of protein identification information
            ExplicitMods = explicitMods != null
                ? explicitMods.ChangePeptide(new Peptide(explicitMods.Peptide.Sequence))
                :null;
        }

        public string Sequence { get { return FastaSequence.StripModifications(ModifiedSequence); }}
        public string ModifiedSequence { get; private set; }
        public ExplicitMods ExplicitMods { get; private set; }

        #region object overrides

        private bool Equals(ModifiedSequenceMods other)
        {
            return string.Equals(ModifiedSequence, other.ModifiedSequence) &&
                Equals(ExplicitMods, other.ExplicitMods);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ModifiedSequenceMods && Equals((ModifiedSequenceMods) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ModifiedSequence.GetHashCode()*397) ^
                    (ExplicitMods != null ? ExplicitMods.GetHashCode() : 0);
            }
        }

        #endregion
    }
}