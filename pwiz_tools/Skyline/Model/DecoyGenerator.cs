/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Skyline.Model
{
    public abstract class DecoyGenerator
    {
        private const int RANDOM_SEED = 7 * 7 * 7 * 7 * 7; // 7^5 recommended by Brian S.

        public Random Random { get; set; } = new Random(RANDOM_SEED);

        public bool PreservePrecursorMass { get; set; }

        public class Shuffler : DecoyGenerator
        {
            protected override SequenceMods PermuteSequence(SequenceMods sequenceMods)
            {
                return sequenceMods.Shuffle(Random);
            }

            protected override bool MultiCycle
            {
                get { return true; }
            }
        }

        public class Reverser : DecoyGenerator
        {
            protected override SequenceMods PermuteSequence(SequenceMods sequenceMods)
            {
                if (sequenceMods.Sequence.Take(sequenceMods.Sequence.Length - 1).Distinct().Count() <= 1)
                {
                    return null;
                }
                var reversed = sequenceMods.Reverse();
                if (PreservePrecursorMass && reversed.Sequence == sequenceMods.Sequence)
                {
                    return null;
                }
                return reversed;
            }
        }

        public class MassShifter : DecoyGenerator
        {
            protected override SequenceMods PermuteSequence(SequenceMods sequenceMods)
            {
                return sequenceMods;
            }
        }

        public SrmDocument AddDecoys(SrmDocument document, int numDecoys)
        {
            // Loop through the existing tree in random order creating decoys
            var settings = document.Settings;
            var enzyme = settings.PeptideSettings.Enzyme;

            var decoyNodePepList = new List<PeptideDocNode>();
            var setDecoyKeys = new HashSet<PeptideModKey>();
            while (numDecoys > 0)
            {
                int startDecoys = numDecoys;
                foreach (var nodePep in document.Peptides.ToArray().RandomOrder(RANDOM_SEED))
                {
                    if (numDecoys == 0)
                        break;

                    // Decoys should not be based on standard peptides
                    if (nodePep.GlobalStandardType != null)
                        continue;
                    const int maxIterations = 10; // Maximum number of times to try generating decoy
                    for (var iteration = 0; iteration < maxIterations; iteration++)
                    {
                        var seqMods = PermuteSequence(new SequenceMods(nodePep));
                        if (seqMods == null)
                        {
                            break;
                        }

                        var decoyPeptide = new Peptide(null, seqMods.Sequence, null, null, enzyme.CountCleavagePoints(seqMods.Sequence), true);
                        seqMods = new SequenceMods(decoyPeptide, decoyPeptide.Sequence, seqMods.Mods?.ChangePeptide(decoyPeptide));
                        var retry = false;
                        foreach (var comparableGroups in PeakFeatureEnumerator.ComparableGroups(nodePep))
                        {
                            var decoyNodeTranGroupList = GetDecoyGroups(nodePep, decoyPeptide, seqMods.Mods,
                                comparableGroups, document);
                            if (decoyNodeTranGroupList.Count == 0)
                                continue;

                            var nodePepNew = new PeptideDocNode(decoyPeptide, settings, seqMods.Mods,
                                null, nodePep.ExplicitRetentionTime, decoyNodeTranGroupList.ToArray(), false);

                            // Avoid adding empty peptide nodes
                            nodePepNew = nodePepNew.ChangeSettings(settings, SrmSettingsDiff.ALL);
                            if (nodePepNew.Children.Count == 0)
                                continue;

                            if (MultiCycle && nodePepNew.ChangeSettings(settings, SrmSettingsDiff.ALL).TransitionCount != nodePep.TransitionCount)
                            {
                                // Try to generate a new decoy if multi-cycle and the generated decoy has a different number of transitions than the target
                                retry = true;
                                break;
                            }

                            if (!Equals(nodePep.ModifiedSequence, nodePepNew.ModifiedSequence))
                            {
                                var sourceKey = new ModifiedSequenceMods(nodePep.ModifiedSequence, nodePep.ExplicitMods);
                                nodePepNew = nodePepNew.ChangeSourceKey(sourceKey);
                            }

                            // Avoid adding duplicate peptides
                            if (!setDecoyKeys.Add(nodePepNew.Key))
                            {
                                retry = MultiCycle;
                                continue;
                            }

                            decoyNodePepList.Add(nodePepNew);
                            numDecoys--;
                        }
                        if (!retry)
                            break;
                    }
                }
                // Stop if not multi-cycle or the number of decoys has not changed.
                if (!MultiCycle || startDecoys == numDecoys)
                    break;
            }
            var decoyNodePepGroup = new PeptideGroupDocNode(new PeptideGroup(true), Annotations.EMPTY,
                PeptideGroup.DECOYS, null, decoyNodePepList.ToArray(), false);
            decoyNodePepGroup = decoyNodePepGroup.ChangeSettings(document.Settings, SrmSettingsDiff.ALL);

            if (decoyNodePepGroup.PeptideCount > 0)
            {
                decoyNodePepGroup.CheckDecoys(document, out _, out _, out var proportionDecoysMatch);
                decoyNodePepGroup = decoyNodePepGroup.ChangeProportionDecoysMatch(proportionDecoysMatch);
            }

            return (SrmDocument)document.Add(decoyNodePepGroup);
        }

        private List<TransitionGroupDocNode> GetDecoyGroups(PeptideDocNode nodePep, Peptide decoyPeptide,
            ExplicitMods mods, IEnumerable<TransitionGroupDocNode> comparableGroups, SrmDocument document)
        {
            var decoyNodeTranGroupList = new List<TransitionGroupDocNode>();
            var chargeToPrecursor = new Dictionary<int, Tuple<int, TransitionGroupDocNode>>();
            bool shiftMass = nodePep.Peptide.Sequence == decoyPeptide.Sequence;
            foreach (TransitionGroupDocNode nodeGroup in comparableGroups)
            {
                var transGroup = nodeGroup.TransitionGroup;

                int precursorMassShift;
                TransitionGroupDocNode nodeGroupPrimary = null;
                if (chargeToPrecursor.TryGetValue(nodeGroup.TransitionGroup.PrecursorCharge,
                        out var primaryPrecursor))
                {
                    precursorMassShift = primaryPrecursor.Item1;
                    nodeGroupPrimary = primaryPrecursor.Item2;
                }
                else if (shiftMass)
                {
                    precursorMassShift = NextPrecursorMassShift();
                }
                else
                {
                    precursorMassShift = PreservePrecursorMass ? 0 : TransitionGroup.ALTERED_SEQUENCE_DECOY_MZ_SHIFT;
                }

                var decoyGroup = new TransitionGroup(decoyPeptide, transGroup.PrecursorAdduct,
                    transGroup.LabelType, false, precursorMassShift);

                var decoyNodeTranList = nodeGroupPrimary != null
                    ? decoyGroup.GetMatchingTransitions(document.Settings, nodeGroupPrimary, mods)
                    : GetDecoyTransitions(nodeGroup, decoyGroup, shiftMass);

                var nodeGroupDecoy = new TransitionGroupDocNode(decoyGroup,
                    Annotations.EMPTY,
                    document.Settings,
                    mods,
                    nodeGroup.LibInfo,
                    nodeGroup.ExplicitValues,
                    nodeGroup.Results,
                    decoyNodeTranList,
                    false);
                decoyNodeTranGroupList.Add(nodeGroupDecoy);

                if (primaryPrecursor == null)
                {
                    chargeToPrecursor.Add(transGroup.PrecursorCharge, Tuple.Create(precursorMassShift, nodeGroupDecoy));
                }
            }

            return decoyNodeTranGroupList;
        }

        private int NextPrecursorMassShift()
        {
            // Do not allow zero for the mass shift of the precursor
            int massShift = Random.Next(TransitionGroup.MIN_PRECURSOR_DECOY_MASS_SHIFT,
                TransitionGroup.MAX_PRECURSOR_DECOY_MASS_SHIFT);
            return massShift < 0 ? massShift : massShift + 1;
        }

        private TransitionDocNode[] GetDecoyTransitions(TransitionGroupDocNode nodeGroup, TransitionGroup decoyGroup, bool shiftMass)
        {
            var decoyNodeTranList = new List<TransitionDocNode>();
            foreach (var nodeTran in nodeGroup.Transitions)
            {
                var transition = nodeTran.Transition;
                int productMassShift = 0;
                if (shiftMass)
                    productMassShift = NextProductMassShift();
                else if (transition.IsPrecursor() && decoyGroup.DecoyMassShift.HasValue)
                    productMassShift = decoyGroup.DecoyMassShift.Value;
                var decoyTransition = new Transition(decoyGroup, transition.IonType, transition.CleavageOffset,
                    transition.MassIndex, transition.Adduct, productMassShift, transition.CustomIon);
                decoyNodeTranList.Add(new TransitionDocNode(decoyTransition, nodeTran.Losses, nodeTran.MzMassType.IsAverage() ? TypedMass.ZERO_AVERAGE_MASSH : TypedMass.ZERO_MONO_MASSH,
                    nodeTran.QuantInfo, nodeTran.ExplicitValues));
            }
            return decoyNodeTranList.ToArray();
        }

        private int NextProductMassShift()
        {
            int massShift = Random.Next(Transition.MIN_PRODUCT_DECOY_MASS_SHIFT,
                Transition.MAX_PRODUCT_DECOY_MASS_SHIFT);
            // TODO: Validation code (at least 5 from the precursor)
            return massShift < 0 ? massShift : massShift + 1;
        }



        protected abstract SequenceMods PermuteSequence(SequenceMods sequenceMods);
        protected virtual bool MultiCycle
        {
            get { return false; }
        }

        private static IList<ExplicitMod> GetReversedMods(IEnumerable<ExplicitMod> mods, int lenSeq)
        {
            return GetRearrangedMods(mods, lenSeq, i => lenSeq - i - 1);
        }

        protected class SequenceMods
        {
            public SequenceMods(PeptideDocNode peptideDocNode) : this(peptideDocNode.Peptide,
                peptideDocNode.Peptide.Sequence, peptideDocNode.ExplicitMods)
            {
            }
            public SequenceMods(Peptide peptide, string sequence, ExplicitMods explicitMods)
            {
                Peptide = peptide;
                Sequence = sequence;
                Mods = explicitMods;
            }
            
            public Peptide Peptide { get; }
            public string Sequence { get; }
            public ExplicitMods Mods { get; }

            public SequenceMods Shuffle(Random random)
            {
                char finalA = Sequence.Last();
                string sequencePrefix = Sequence.Substring(0, Sequence.Length - 1);
                if (sequencePrefix.Distinct().Count() <= 1)
                {
                    return null;
                }
                int lenPrefix = sequencePrefix.Length;

                // Calculate a random shuffling of the current positions
                int[] newIndices = new int[lenPrefix];
                string newSequence;
                do
                {
                    for (int i = 0; i < lenPrefix; i++)
                        newIndices[i] = i;
                    for (int i = 0; i < lenPrefix; i++)
                        Helpers.Swap(ref newIndices[random.Next(newIndices.Length)], ref newIndices[random.Next(newIndices.Length)]);

                    // Move the amino acids to their new positions
                    char[] shuffledArray = new char[lenPrefix];
                    for (int i = 0; i < lenPrefix; i++)
                        shuffledArray[newIndices[i]] = sequencePrefix[i];

                    newSequence = new string(shuffledArray) + finalA;
                }
                // Make sure random shuffling did not just result in the same sequence
                while (newSequence.Equals(Sequence));

                ExplicitMods newMods = null;
                if (Mods != null)
                {
                    var shuffledStaticMods = GetShuffledMods(Mods.StaticModifications, newIndices);
                    var typedStaticMods = GetStaticTypedMods(Peptide, shuffledStaticMods);
                    newMods = new ExplicitMods(Peptide,
                        shuffledStaticMods,
                        GetShuffledHeavyMods(typedStaticMods, newIndices),
                        Mods.IsVariableStaticMods);
                }

                return new SequenceMods(Peptide, newSequence, newMods);
            }

            public SequenceMods Reverse()
            {
                var reversedSequence = new string(Sequence.Take(Sequence.Length - 1).Reverse().Append(Sequence.Last()).ToArray());
                ExplicitMods reversedMods = null;
                if (Mods != null)
                {
                    var lenSeq = Sequence.Length - 1;
                    var reversedStaticMods = GetReversedMods(Mods.StaticModifications, lenSeq);
                    var typedStaticMods = GetStaticTypedMods(Peptide, reversedStaticMods);
                    reversedMods = new ExplicitMods(Peptide,
                        reversedStaticMods,
                        GetReversedHeavyMods(typedStaticMods, lenSeq),
                        Mods.IsVariableStaticMods);
                }

                return new SequenceMods(Peptide, reversedSequence, reversedMods);
            }

            private IEnumerable<TypedExplicitModifications> GetShuffledHeavyMods(TypedExplicitModifications typedStaticMods, int[] newIndices)
            {
                var shuffledHeavyMods = Mods.GetHeavyModifications().Select(typedMod =>
                    new TypedExplicitModifications(Peptide, typedMod.LabelType,
                        GetShuffledMods(typedMod.Modifications, newIndices)));
                foreach (var typedMods in shuffledHeavyMods)
                {
                    yield return typedMods.AddModMasses(typedStaticMods);
                }
            }

            private IEnumerable<TypedExplicitModifications> GetReversedHeavyMods(TypedExplicitModifications typedStaticMods, int lenSeq)
            {
                var reversedHeavyMods = Mods.GetHeavyModifications().Select(typedMod =>
                    new TypedExplicitModifications(Peptide, typedMod.LabelType,
                        GetReversedMods(typedMod.Modifications, lenSeq)));
                foreach (var typedMods in reversedHeavyMods)
                {
                    yield return typedMods.AddModMasses(typedStaticMods);
                }
            }
        }

        private static IList<ExplicitMod> GetShuffledMods(IEnumerable<ExplicitMod> mods, int[] newIndices)
        {
            return GetRearrangedMods(mods, newIndices.Length, i => newIndices[i]);
        }
        private static TypedExplicitModifications GetStaticTypedMods(Peptide peptide, IList<ExplicitMod> staticMods)
        {
            return staticMods != null
                ? new TypedExplicitModifications(peptide, IsotopeLabelType.light, staticMods)
                : null;
        }

        private static IList<ExplicitMod> GetRearrangedMods(IEnumerable<ExplicitMod> mods, int lenSeq,
            Func<int, int> getNewIndex)
        {
            if (null == mods)
            {
                return null;
            }

            var arrayMods = mods.ToList();
            for (int i = 0; i < arrayMods.Count; i++)
            {
                var mod = arrayMods[i];
                if (mod.IndexAA < lenSeq)
                    arrayMods[i] = new ExplicitMod(getNewIndex(mod.IndexAA), mod.Modification);
            }

            return arrayMods.OrderBy(mod => mod.IndexAA).ToList();
        }
    }
}
