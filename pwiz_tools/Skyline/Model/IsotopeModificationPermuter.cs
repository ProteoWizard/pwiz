/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Generates all of the possible permutations of fully and partial labeled isotope modifications in a
    /// Skyline document.
    /// This can be run in either "simple" mode, in which case the only thing that matters is the number of
    /// residues that were modified, or "complete" mode, in which case the full set of all combinations of individual
    /// modified residue positions are generated.
    /// </summary>
    public class IsotopeModificationPermuter
    {
        public IsotopeModificationPermuter(StaticMod isotopeModification, bool simplePermutation, IsotopeLabelType fullHeavyLabelType, List<StaticMod> globalStaticMods, List<StaticMod> globalIsotopeMods)
        {
            IsotopeModification = isotopeModification;
            SimplePermutation = simplePermutation;
            FullyHeavyLabelType = fullHeavyLabelType;
            GlobalStaticMods = globalStaticMods;
            GlobalIsotopeMods = globalIsotopeMods;
        }

        public StaticMod IsotopeModification { get; private set; }
        public bool SimplePermutation { get; private set; }

        /// <summary>
        /// The IsotopeLabelType to use for the "fully labeled" form of the peptide (where all possibly modified
        /// residues have the modification).
        /// The names of the partially labeled forms will all be FullHeavyLabelType.Name with an integer
        /// appended.
        /// </summary>
        public IsotopeLabelType FullyHeavyLabelType { get; private set; }

        public List<StaticMod> GlobalStaticMods { get; set; }
        public List<StaticMod> GlobalIsotopeMods { get; set; }

        public bool SkipPeptide(PeptideDocNode peptideDocNode)
        {
            return null != peptideDocNode.GlobalStandardType;
        }

        public SrmDocument PermuteIsotopeModifications(IProgressMonitor progressMonitor, SrmDocument document)
        {
            var progressStatus = new ProgressStatus(Resources.IsotopeModificationPermuter_PermuteIsotopeModifications_Permuting_isotope_modifications);
            progressMonitor.UpdateProgress(progressStatus.ChangePercentComplete(0));
            int maxPermutationCount = 0;
            foreach (var peptide in document.Peptides)
            {
                if (SkipPeptide(peptide))
                {
                    continue;
                }
                int residueCount = PotentiallyModifiedResidues(peptide, IsotopeModification).Count;
                int permutationCount = SimplePermutation ? residueCount + 1 : 1 << residueCount;
                maxPermutationCount = Math.Max(permutationCount, maxPermutationCount);
            }

            var partialLabelTypes = new List<IsotopeLabelType>(maxPermutationCount);
            document = EnsureLabelTypes(progressMonitor, document, maxPermutationCount, partialLabelTypes);
            int totalPeptideCount = document.PeptideCount;
            int processedPeptideCount = 0;
            var newMoleculeGroups = new List<PeptideGroupDocNode>();
            foreach (var peptideGroup in document.MoleculeGroups)
            {
                if (!peptideGroup.IsProteomic)
                {
                    newMoleculeGroups.Add(peptideGroup);
                    continue;
                }

                var newPeptides = new List<PeptideDocNode>();
                foreach (var peptideDocNode in peptideGroup.Peptides)
                {
                    if (progressMonitor.IsCanceled)
                    {
                        throw new OperationCanceledException();
                    }

                    progressMonitor.UpdateProgress(
                        progressStatus.ChangePercentComplete(100 * processedPeptideCount++ / totalPeptideCount));

                    var newPeptide = PermuteModificationsOnPeptide(document, peptideGroup, peptideDocNode, partialLabelTypes);
                    newPeptides.Add(newPeptide);
                }

                var newChildren = ImmutableList.ValueOf(newPeptides.Cast<DocNode>());
                if (ArrayUtil.ReferencesEqual(newChildren, peptideGroup.Children))
                {
                    newMoleculeGroups.Add(peptideGroup);
                }
                else
                {
                    newMoleculeGroups.Add((PeptideGroupDocNode) peptideGroup.ChangeChildren(newChildren));
                }
            }

            document = (SrmDocument) document.ChangeChildren(ImmutableList.ValueOf(newMoleculeGroups.Cast<DocNode>()));
            var pepModsNew = document.Settings.PeptideSettings.Modifications;
            pepModsNew = pepModsNew.DeclareExplicitMods(document, GlobalStaticMods, GlobalIsotopeMods);
            if (!Equals(pepModsNew, document.Settings.PeptideSettings.Modifications))
            {
                var newSettings = document.Settings.ChangePeptideModifications(m => pepModsNew);
                document = document.ChangeSettings(newSettings);
            }
            return document;
        }

        public PeptideDocNode PermuteModificationsOnPeptide(SrmDocument document, PeptideGroupDocNode peptideGroupDocNode,
            PeptideDocNode peptideDocNode, List<IsotopeLabelType> partialLabelTypes)
        {
            if (SkipPeptide(peptideDocNode))
            {
                return peptideDocNode;
            }

            var potentiallyModifiedResidues = PotentiallyModifiedResidues(peptideDocNode, IsotopeModification);
            if (potentiallyModifiedResidues.Count == 0)
            {
                return peptideDocNode;
            }

            // Create a document containing only one peptide so that "ChangePeptideMods" does not have to walk
            // over a long list of peptides to see which modifications are in use.
            var smallDocument = (SrmDocument) document.ChangeChildren(new DocNode[]
                {peptideGroupDocNode.ChangeChildren(new DocNode[] {peptideDocNode})});
            var newTypedExplicitModifications = PermuteTypedExplicitModifications(partialLabelTypes, peptideDocNode, potentiallyModifiedResidues);
            var newExplicitMods = new ExplicitMods(peptideDocNode.Peptide, peptideDocNode.ExplicitMods?.StaticModifications, newTypedExplicitModifications);
            var identityPath = new IdentityPath(peptideGroupDocNode.PeptideGroup, peptideDocNode.Peptide);
            smallDocument = smallDocument.ChangePeptideMods(identityPath, newExplicitMods, false, GlobalStaticMods,
                GlobalIsotopeMods);
            peptideDocNode = (PeptideDocNode) smallDocument.FindPeptideGroup(peptideGroupDocNode.PeptideGroup).FindNode(peptideDocNode.Peptide);
            var lightChargeStates = peptideDocNode.TransitionGroups.Where(tg=>tg.IsLight).Select(tg => tg.PrecursorCharge).Distinct().ToList();
            var chargeStatesByLabel =
                peptideDocNode.TransitionGroups.ToLookup(tg => tg.LabelType, tg => tg.PrecursorCharge);
            var transitionGroupsToAdd = new List<TransitionGroupDocNode>();
            foreach (var typedExplicitModifications in newExplicitMods.GetHeavyModifications())
            {
                var labelType = typedExplicitModifications.LabelType;
                foreach (var chargeState in lightChargeStates.Except(chargeStatesByLabel[labelType]))
                {
                    var tranGroup = new TransitionGroup(peptideDocNode.Peptide, Adduct.FromChargeProtonated(chargeState), labelType);
                    TransitionDocNode[] transitions = peptideDocNode.GetMatchingTransitions(tranGroup, smallDocument.Settings, newExplicitMods);

                    var nodeGroup = new TransitionGroupDocNode(tranGroup, transitions);
                    nodeGroup = nodeGroup.ChangeSettings(smallDocument.Settings, peptideDocNode, newExplicitMods, SrmSettingsDiff.ALL);
                    transitionGroupsToAdd.Add(nodeGroup);
                }
            }

            if (transitionGroupsToAdd.Any())
            {
                var newChildren = peptideDocNode.TransitionGroups.Concat(transitionGroupsToAdd).ToList();
                newChildren.Sort(Peptide.CompareGroups);
                peptideDocNode = (PeptideDocNode) peptideDocNode.ChangeChildren(newChildren.Cast<DocNode>().ToList());
            }
            return peptideDocNode;
        }

        public List<TypedExplicitModifications> PermuteTypedExplicitModifications(IList<IsotopeLabelType> partialLabelTypes, PeptideDocNode peptideDocNode, IList<int> potentiallyModifiedResidues)
        {
            var typedExplicitMods = new List<TypedExplicitModifications>();
            List<ImmutableList<int>> permutations;
            if (SimplePermutation)
            {
                permutations = GenerateSimplePermutations(potentiallyModifiedResidues).ToList();
            }
            else
            {
                permutations = GenerateComplexPermutations(potentiallyModifiedResidues).ToList();
            }

            for (int i = 1; i < permutations.Count; i++)
            {
                IsotopeLabelType labelType;
                if (i == permutations.Count - 1)
                {
                    labelType = FullyHeavyLabelType;
                }
                else
                {
                    labelType = partialLabelTypes[i];
                }

                var explicitMods = permutations[i].Select(indexAA => new ExplicitMod(indexAA, IsotopeModification)).ToList();
                var typedMods = new TypedExplicitModifications(peptideDocNode.Peptide, labelType, explicitMods);
                typedExplicitMods.Add(typedMods);
            }

            return typedExplicitMods;
        }

        public IEnumerable<ImmutableList<int>> GenerateSimplePermutations(IList<int> potentialIndexes)
        {
            return Enumerable.Range(0, potentialIndexes.Count + 1)
                .Select(i => ImmutableList.ValueOf(potentialIndexes.Take(i)));
        }

        public IEnumerable<ImmutableList<int>> GenerateComplexPermutations(IList<int> potentialIndexes)
        {
            if (potentialIndexes.Count <= 1)
            {
                yield return ImmutableList<int>.EMPTY;
                if (potentialIndexes.Count == 1)
                {
                    yield return ImmutableList.Singleton(potentialIndexes[0]);
                }

                yield break;
            }

            int last = potentialIndexes[potentialIndexes.Count - 1];
            foreach (var head in GenerateComplexPermutations(potentialIndexes.Take(potentialIndexes.Count - 1).ToList()))
            {
                yield return head;
                yield return ImmutableList.ValueOf(head.Append(last));
            }
        }

        public List<int> PotentiallyModifiedResidues(PeptideDocNode peptideDocNode, StaticMod mod)
        {
            List<int> indexes = new List<int>();
            var sequence = peptideDocNode.Peptide.Sequence;
            for (int index = 0; index < sequence.Length; index++)
            {
                if (mod.Terminus == ModTerminus.N && index != 0)
                {
                    continue;
                }

                if (mod.Terminus == ModTerminus.C && index != sequence.Length - 1)
                {
                    continue;
                }

                if (mod.AAs != null)
                {
                    var aa = sequence[index];
                    if (mod.AAs.IndexOf(aa) < 0)
                    {
                        continue;
                    }
                }
                indexes.Add(index);
            }

            return indexes;
        }

        /// <summary>
        /// Ensure that the document peptide settings contain all of the necessary heavy label types (i.e. "heavy1", "heavy2", ...)
        /// to accomodate the specified number of possible permutations.
        /// </summary>
        public SrmDocument EnsureLabelTypes(IProgressMonitor progressMonitor, SrmDocument document, int maxPermutationCount, List<IsotopeLabelType> partialLabelTypes)
        {
            if (partialLabelTypes.Count == 0)
            {
                partialLabelTypes.Add(IsotopeLabelType.light);
            }

            var labelTypesByName = document.Settings.PeptideSettings.Modifications.GetHeavyModificationTypes()
                .ToDictionary(label=>label.Name);
            int maxSortValue = 0;
            if (labelTypesByName.Any())
            {
                maxSortValue = labelTypesByName.Values.Max(label => label.SortOrder);
            }

            var labelTypesToAdd = new List<IsotopeLabelType>();
            for (int i = partialLabelTypes.Count; i < maxPermutationCount - 1; i++)
            {
                if (progressMonitor.IsCanceled)
                {
                    throw new OperationCanceledException();
                }
                var name = FullyHeavyLabelType.Name + i;
                IsotopeLabelType labelType;
                if (!labelTypesByName.TryGetValue(name, out labelType))
                {
                    labelType = new IsotopeLabelType(name, ++maxSortValue);
                    labelTypesToAdd.Add(labelType);
                }
                partialLabelTypes.Add(labelType);
            }

            if (!labelTypesByName.ContainsKey(FullyHeavyLabelType.Name))
            {
                labelTypesToAdd.Add(FullyHeavyLabelType);
            }

            if (labelTypesToAdd.Any())
            {
                var settings = document.Settings;
                var heavyModifications = settings.PeptideSettings.Modifications.HeavyModifications.ToList();
                heavyModifications.AddRange(labelTypesToAdd.Select(labelType=>new TypedModifications(labelType, ImmutableList.Empty<StaticMod>())));
                var peptideModifications = settings.PeptideSettings.Modifications;
                peptideModifications = new PeptideModifications(peptideModifications.StaticModifications, peptideModifications.MaxVariableMods, peptideModifications.MaxNeutralLosses,
                    heavyModifications, peptideModifications.InternalStandardTypes);
                settings = settings.ChangePeptideSettings(settings.PeptideSettings.ChangeModifications(peptideModifications));
                document = document.ChangeSettings(settings);
            }

            return document;
        }
    }
}
