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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model
{
    public class PeptideGroupDocNode : DocNodeParent
    {
        private string _name;
        private string _description;

        public PeptideGroupDocNode(PeptideGroup id, string name, string description, PeptideDocNode[] children)
            : this(id, Annotations.EMPTY, name, description, children)
        {
        }

        public PeptideGroupDocNode(PeptideGroup id, Annotations annotations, string name, string description,
                PeptideDocNode[] children)
            : this(id, annotations, name, description, children, true)
        {
        }

        public PeptideGroupDocNode(PeptideGroup id, Annotations annotations, String name, String description,
                PeptideDocNode[] children, bool autoManageChildren)
            : base (id, annotations, children, autoManageChildren)
        {
            _name = name;
            _description = description;
        }

        public PeptideGroup PeptideGroup { get { return (PeptideGroup)Id; } }

        public override AnnotationDef.AnnotationTarget AnnotationTarget
        {
            get { return AnnotationDef.AnnotationTarget.protein; }
        }

        public bool IsPeptideList { get { return !(PeptideGroup is FastaSequence); } }

        public string Name { get { return PeptideGroup.Name ?? _name; } }
        public string Description { get { return PeptideGroup.Description ?? _description; } }

        /// <summary>
        /// Node level depths below this node
        /// </summary>
// ReSharper disable InconsistentNaming
        public enum Level { Peptides, TransitionGroups, Transitions }
// ReSharper restore InconsistentNaming

        public int PeptideCount { get { return GetCount((int)Level.Peptides); } }
        public int TransitionGroupCount { get { return GetCount((int)Level.TransitionGroups); } }
        public int TransitionCount { get { return GetCount((int)Level.Transitions); } }

        public PeptideGroupDocNode ChangeName(string name)
        {
            // Only allow set, if the id object has no name
            Debug.Assert(PeptideGroup.Name == null);
            return ChangeProp(ImClone(this), (im, v) => im._name = v, name);
        }

        public PeptideGroupDocNode ChangeDescription(string desc)
        {
            // Only allow set, if the id object has no name
            Debug.Assert(PeptideGroup.Description == null);
            return ChangeProp(ImClone(this), (im, v) => im._description = v, desc);
        }

        public PeptideGroupDocNode ChangeSettings(SrmSettings settingsNew, SrmSettingsDiff diff)
        {
            if (diff.DiffPeptides && settingsNew.PeptideSettings.Filter.AutoSelect && AutoManageChildren)
            {
                IList<DocNode> childrenNew = new List<DocNode>();

                int countPeptides = 0;
                int countIons = 0;

                Dictionary<int, DocNode> mapIndexToChild = CreateGlobalIndexToChildMap();
                Dictionary<PeptideModKey, DocNode> mapIdToChild = CreatePeptideModToChildMap();

                foreach(PeptideDocNode nodePep in GetPeptideNodes(settingsNew, true))
                {
                    PeptideDocNode nodePeptide = nodePep;
                    SrmSettingsDiff diffNode = SrmSettingsDiff.ALL;

                    DocNode existing;
                    // Add values that existed before the change. First check for exact match by
                    // global index, which will happen when explicit modifications are added,
                    // and then by content identity.
                    if (mapIndexToChild.TryGetValue(nodePep.Id.GlobalIndex, out existing) ||
                        mapIdToChild.TryGetValue(new PeptideModKey(nodePep.Peptide, nodePep.ExplicitMods), out existing))
                    {
                        nodePeptide = (PeptideDocNode) existing;
                        diffNode = diff;
                    }

                    if (nodePeptide != null)
                    {
                        // Materialize children of the peptide.
                        nodePeptide = nodePeptide.ChangeSettings(settingsNew, diffNode);

                        childrenNew.Add(nodePeptide);

                        // Make sure a single peptide group does not exceed document limits.
                        countPeptides++;
                        countIons += nodePeptide.TransitionCount;
                        if (countIons > SrmDocument.MAX_TRANSITION_COUNT ||
                                countPeptides > SrmDocument.MAX_PEPTIDE_COUNT)
                            throw new InvalidDataException("Document size limit exceeded.");
                    }
                }

                if (PeptideGroup.Sequence != null)
                    childrenNew = PeptideGroup.RankPeptides(childrenNew, settingsNew, true);

                return (PeptideGroupDocNode) ChangeChildrenChecked(childrenNew);
            }
            else
            {
                var nodeResult = this;

                if (diff.DiffPeptides && diff.SettingsOld != null)
                {
                    // If variable modifications changed, remove all peptides with variable
                    // modifications which are no longer possible.
                    var modsNew = settingsNew.PeptideSettings.Modifications;
                    var modsVarNew = modsNew.VariableModifications.ToArray();
                    var modsOld = diff.SettingsOld.PeptideSettings.Modifications;
                    var modsVarOld = modsOld.VariableModifications.ToArray();
                    if (modsNew.MaxVariableMods < modsOld.MaxVariableMods ||
                            !ArrayUtil.EqualsDeep(modsVarNew, modsVarOld))
                    {
                        IList<DocNode> childrenNew = new List<DocNode>();
                        foreach (PeptideDocNode nodePeptide in nodeResult.Children)
                        {
                            if (nodePeptide.AreVariableModsPossible(modsNew.MaxVariableMods, modsVarNew))
                                childrenNew.Add(nodePeptide);
                        }

                        nodeResult = (PeptideGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                    }
                }

                // Check for changes affecting children
                if (diff.DiffPeptideProps || diff.DiffExplicit ||
                    diff.DiffTransitionGroups || diff.DiffTransitionGroupProps ||
                    diff.DiffTransitions || diff.DiffTransitionProps ||
                    diff.DiffResults)
                {
                    IList<DocNode> childrenNew = new List<DocNode>();

                    // Enumerate the nodes making necessary changes.
                    foreach (PeptideDocNode nodePeptide in nodeResult.Children)
                        childrenNew.Add(nodePeptide.ChangeSettings(settingsNew, diff));

                    if (ArrayUtil.ReferencesEqual(childrenNew, Children))
                        childrenNew = Children;

                    nodeResult = (PeptideGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                }
                return nodeResult;
            }
        }

        public IEnumerable<PeptideDocNode> GetPeptideNodes(SrmSettings settings, bool useFilter)
        {
            // FASTA sequences can generate a comprehensive list of available peptides.
            FastaSequence fastaSeq = Id as FastaSequence;
            if (fastaSeq != null)
            {
                foreach (PeptideDocNode nodePep in fastaSeq.CreatePeptideDocNodes(settings, useFilter))
                    yield return nodePep;
            }
            // Peptide lists without variable modifications just return their existing children.
            else if (!settings.PeptideSettings.Modifications.HasVariableModifications)
            {
                foreach (PeptideDocNode nodePep in Children)
                {
                    if (!nodePep.HasVariableMods)
                        yield return nodePep;                    
                }
            }
            // If there are variable modifications, fill out the available list.
            else
            {
                var setNonExplicit = new HashSet<Peptide>();
                IPeptideFilter filter = (useFilter ? settings : PeptideFilter.UNFILTERED);
                foreach (PeptideDocNode nodePep in Children)
                {
                    if (nodePep.HasExplicitMods && !nodePep.HasVariableMods)
                        yield return nodePep;
                    else if (setNonExplicit.Contains(nodePep.Peptide))
                        continue;
                    else
                    {
                        bool returnedResult = false;
                        var peptide = nodePep.Peptide;
                        // The peptide will be returned as the Id of the unmodified instance of this
                        // peptide.  If the peptide DocNode is explicitly modified this will cause
                        // two nodes in the tree to have the same Id.  So, use a copy instead.
                        if (nodePep.HasExplicitMods)
                            peptide = (Peptide) peptide.Copy();
                        foreach (PeptideDocNode nodePepResult in peptide.CreateDocNodes(settings, filter))
                        {
                            yield return nodePepResult;
                            returnedResult = true;
                        }
                        // Make sure the peptide is not removed due to filtering
                        if (!returnedResult)
                            yield return nodePep;
                        setNonExplicit.Add(nodePep.Peptide);
                    }
                }                
            }
        }

        private Dictionary<PeptideModKey, DocNode> CreatePeptideModToChildMap()
        {
            var map = new Dictionary<PeptideModKey, DocNode>();
            foreach (PeptideDocNode child in Children)
            {
                var key = new PeptideModKey(child.Peptide, child.ExplicitMods);
                // Skip repeats.  These can only be created by the user, and should be
                // matched by the global index dictionary.
                if (!map.ContainsKey(key))
                    map.Add(key, child);
            }
            return map;
        }

        private Dictionary<int, DocNode> CreateGlobalIndexToChildMap()
        {
            return Children.ToDictionary(child => child.Id.GlobalIndex);
        }

        #region object overrides

        public bool Equals(PeptideGroupDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && Equals(obj._name, _name) && Equals(obj._description, _description);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as PeptideGroupDocNode);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (_name != null ? _name.GetHashCode() : 0);
                result = (result*397) ^ (_description != null ? _description.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public class PeptideGroup : Identity
    {
        public virtual string Name { get { return null; } }
        public virtual string Description { get { return null; } }
        public virtual string Sequence { get { return null; } }

        public static IList<DocNode> RankPeptides(IList<DocNode> listPeptides, SrmSettings settings, bool useLimit)
        {
            // If no rank ID is set, just return the input list
            PeptideRankId rankId = settings.PeptideSettings.Libraries.RankId;

            // Transfer input list to a typed array
            var listRanks = new List<KeyValuePair<PeptideDocNode, float>>();
            for (int i = 0; i < listPeptides.Count; i++)
            {
                var peptide = (PeptideDocNode) listPeptides[i];
                // Remove any old rank information, if peptides are no longer ranked.
                if (rankId == null && peptide.Rank.HasValue)
                    peptide = peptide.ChangeRank(null);
                listRanks.Add(new KeyValuePair<PeptideDocNode, float>(peptide, peptide.GetRankValue(rankId)));
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
                    break;
                if (!peptide.Rank.HasValue || peptide.Rank.Value != rank)
                    peptide = peptide.ChangeRank(rank);
                listRanks[i] = new KeyValuePair<PeptideDocNode, float>(peptide, peptideRank.Value);

                rank++;
            }

            // Reduce array length to desired limit, if necessary
            int? peptideCount = settings.PeptideSettings.Libraries.PeptideCount;
            int numPeptides = useLimit && peptideCount.HasValue && peptideCount.Value < listPeptides.Count
                              ? peptideCount.Value
                              : listPeptides.Count;
            PeptideDocNode[] peptidesNew = new PeptideDocNode[numPeptides];
            for (int i = 0; i < peptidesNew.Length; i++)
            {
                // TODO: Remove any peptide groups without the correct rank value
                peptidesNew[i] = listRanks[i].Key;
            }
            
            // Re-sort by order in FASTA sequence
            Array.Sort(peptidesNew, FastaSequence.ComparePeptides);

            return peptidesNew;
        }
    }

    public class FastaSequence : PeptideGroup
    {
        public const string PEPTIDE_SEQUENCE_SEPARATOR = "::";
        public static bool IsSequence(string seq)
        {
            return IsSequence(seq, AminoAcid.IsAA);
        }

        public static bool IsExSequence(string seq)
        {
            return IsSequence(seq, AminoAcid.IsExAA);
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

        public FastaSequence(string name, string description, IList<AlternativeProtein> alternatives, string sequence)
        {
            // Null name means it is editable by the user.
            _name = (string.IsNullOrEmpty(name) ? null : name);

            _description = description;
            Alternatives = new ReadOnlyCollection<AlternativeProtein>(alternatives ?? new AlternativeProtein[0]);
            _sequence = sequence;

            Validate();
        }

        public override string Name { get { return _name; } }
        public override string Description { get { return _description; } }
        public override string Sequence { get { return _sequence; } }
        public IList<AlternativeProtein> Alternatives { get; private set; }

        public IEnumerable<PeptideDocNode> CreatePeptideDocNodes(SrmSettings settings, bool useFilter)
        {
            PeptideSettings pepSettings = settings.PeptideSettings;
            DigestSettings digest = pepSettings.DigestSettings;
            IPeptideFilter filter = (useFilter ? settings : PeptideFilter.UNFILTERED);

            foreach (var peptide in pepSettings.Enzyme.Digest(this, digest))
            {
                foreach (var nodePep in peptide.CreateDocNodes(settings, filter))
                    yield return nodePep;
            }
        }

        public IEnumerable<PeptideDocNode> CreateFullPeptideDocNodes(SrmSettings settings, bool useFilter)
        {
            if (settings.PeptideSettings.Libraries.RankId == null)
            {
                foreach (var nodePep in CreatePeptideDocNodes(settings, useFilter))
                    yield return nodePep.ChangeSettings(settings, SrmSettingsDiff.ALL);
            }
            else
            {
                var listDocNodes = new List<DocNode>();
                foreach (var nodePep in CreatePeptideDocNodes(settings, useFilter))
                    listDocNodes.Add(nodePep.ChangeSettings(settings, SrmSettingsDiff.ALL));

                // Rank and filter peptides by rank.
                foreach (PeptideDocNode nodePep in RankPeptides(listDocNodes, settings, useFilter))
                    yield return nodePep;
            }
        }

        public PeptideDocNode CreateFullPeptideDocNode(SrmSettings settings, String peptideSequence)
        {
            foreach (var peptideDocNode in CreateFullPeptideDocNodes(settings, false))
            {
                if (peptideSequence == peptideDocNode.Peptide.Sequence)
                    return peptideDocNode;
            }

            int begin = Sequence.IndexOf(peptideSequence);
            if (begin < 0)
                return null;

            var peptide = new Peptide(this, peptideSequence, begin, begin + peptideSequence.Length,
                settings.PeptideSettings.Enzyme.CountCleavagePoints(peptideSequence));

            return new PeptideDocNode(peptide, new TransitionGroupDocNode[0])
                .ChangeSettings(settings, SrmSettingsDiff.ALL);
        }

        public static void ValidateSequence(string seq)
        {
            if (string.IsNullOrEmpty(seq))
                throw new InvalidDataException("A protein sequence may not be empty.");
            for (int i = 0; i < seq.Length; i++)
            {
                char c = seq[i];
                if (!AminoAcid.IsExAA(c) && c != '*' && c != '-')
                    throw new InvalidDataException(string.Format("A protein sequence may not contain the character '{0}' at {1}.", seq[i], i));
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
                ArrayUtil.EqualsDeep(obj.Alternatives, Alternatives);
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
                throw new InvalidOperationException("Peptides without FASTA sequence information may not be compared.");
            if (pep1.FastaSequence != pep2.FastaSequence)
                throw new InvalidOperationException("Peptides in different FASTA sequences may not be compared.");

            return Comparer<int>.Default.Compare(pep1.Begin.Value, pep2.Begin.Value);
        }
    }

    public class AlternativeProtein
    {
        public AlternativeProtein(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; set; }

        public string Description { get; set; }

        #region object overrides

        public bool Equals(AlternativeProtein obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.Name, Name) && Equals(obj.Description, Description);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (AlternativeProtein)) return false;
            return Equals((AlternativeProtein) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Name.GetHashCode()*397) ^ (Description != null ? Description.GetHashCode() : 0);
            }
        }

        #endregion
    }

// ReSharper disable InconsistentNaming
    public enum SequenceTerminus { N, C }
// ReSharper restore InconsistentNaming
}
