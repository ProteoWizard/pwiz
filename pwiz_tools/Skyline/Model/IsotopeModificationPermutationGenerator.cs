using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class IsotopeModificationPermutationGenerator
    {
        public IsotopeModificationPermutationGenerator(StaticMod isotopeModification, bool simplePermutation, SrmDocument document)
        {
            IsotopeModification = isotopeModification;
            SimplePermutation = simplePermutation;
            Document = document;
            NewSettings = document.Settings;
            PartialLabelTypes = new List<IsotopeLabelType>();
            PartialLabelTypes.Add(IsotopeLabelType.light);

            foreach (var labelType in document.Settings.PeptideSettings.Modifications.GetHeavyModificationTypes())
            {
                if (FullyHeavyLabelType == null && !char.IsDigit(labelType.Name.Last()))
                {
                    FullyHeavyLabelType = labelType;
                    break;
                }
            }

            LastSortOrder = 0;
            LabelTypesByName = document.Settings.PeptideSettings.Modifications.GetHeavyModificationTypes()
                .ToDictionary(labelType => labelType.Name);
            if (LabelTypesByName.Any())
            {
                LastSortOrder = LabelTypesByName.Values.Max(labelType => labelType.SortOrder);
            }

            if (FullyHeavyLabelType == null)
            {
                FullyHeavyLabelType = new IsotopeLabelType(IsotopeLabelType.HEAVY_NAME, ++LastSortOrder);
                AddLabelType(FullyHeavyLabelType);
            }
        }

        public StaticMod IsotopeModification { get; private set; }
        public bool SimplePermutation { get; private set; }

        public SrmDocument Document { get; private set; }

        public SrmSettings NewSettings { get; private set; }

        public IsotopeLabelType FullyHeavyLabelType { get; private set; }

        public List<IsotopeLabelType> PartialLabelTypes { get; private set; }

        public Dictionary<string, IsotopeLabelType> LabelTypesByName { get; private set; }

        public int LastSortOrder { get; private set; }

        public SrmDocument GetNewDocument()
        {
            foreach (var peptide in Document.Peptides)
            {
                int residueCount = PotentiallyModifiedResidues(peptide, IsotopeModification).Count;
                int permutationCount = SimplePermutation ? residueCount : 1 << residueCount;
                EnsurePartialLabelTypes(permutationCount);
            }

            var doc = Document.ChangeSettings(NewSettings);
            var globalStaticMods = Properties.Settings.Default.StaticModList;
            var globalHeavyMods = Properties.Settings.Default.HeavyModList;
            foreach (var peptideGroup in Document.PeptideGroups)
            {
                foreach (var peptideDocNode in peptideGroup.Peptides)
                {
                    var potentiallyModifiedResidues = PotentiallyModifiedResidues(peptideDocNode, IsotopeModification);
                    if (potentiallyModifiedResidues.Count == 0)
                    {
                        continue;
                    }

                    var newTypedExplicitModifications = PermuteTypedExplicitModifications(peptideDocNode, potentiallyModifiedResidues);
                    var newExplicitMods = new ExplicitMods(peptideDocNode.Peptide, peptideDocNode.ExplicitMods?.StaticModifications, newTypedExplicitModifications);
                    var identityPath = new IdentityPath(peptideGroup.PeptideGroup, peptideDocNode.Peptide);
                    doc = doc.ChangePeptideMods(identityPath, newExplicitMods, false, globalStaticMods,
                        globalHeavyMods);
                }
            }

            return doc;
        }

        public PeptideGroupDocNode PermutePeptideGroup(PeptideGroupDocNode peptideGroupDocNode)
        {
            if (!peptideGroupDocNode.IsProteomic)
            {
                return peptideGroupDocNode;
            }

            var newChildren = new List<DocNode>();
            foreach (var child in peptideGroupDocNode.Molecules)
            {
                var newChild = PermutePeptide(child);
                newChildren.Add(newChild);
            }

            if (ArrayUtil.ReferencesEqual(newChildren, peptideGroupDocNode.Children))
            {
                return peptideGroupDocNode;
            }

            return (PeptideGroupDocNode) peptideGroupDocNode.ChangeChildren(newChildren);
        }

        public PeptideDocNode PermutePeptide(PeptideDocNode peptideDocNode)
        {
            if (!peptideDocNode.IsProteomic)
            {
                return peptideDocNode;
            }
            List<TransitionGroupDocNode> lightPrecursors = new List<TransitionGroupDocNode>();
            var newChildren = new List<TransitionGroupDocNode>();
            var oldChildren = new Dictionary<Tuple<IsotopeLabelType, Adduct>, TransitionGroupDocNode>();
            foreach (var oldChild in peptideDocNode.TransitionGroups)
            {
                if (oldChild.IsLight)
                {
                    lightPrecursors.Add(oldChild);
                }
                else
                {
                    oldChildren.Add(Tuple.Create(oldChild.LabelType, oldChild.PrecursorAdduct), oldChild);
                }
                newChildren.Add(oldChild);
            }
            if (lightPrecursors.Count == 0)
            {
                return peptideDocNode;
            }
            var potentiallyModifiedResidues = PotentiallyModifiedResidues(peptideDocNode, IsotopeModification);
            if (potentiallyModifiedResidues.Count == 0)
            {
                return peptideDocNode;
            }

            var newTypedExplicitModifications = PermuteTypedExplicitModifications(peptideDocNode, potentiallyModifiedResidues);
            var newExplicitMods = new ExplicitMods(peptideDocNode.Peptide, peptideDocNode.ExplicitMods?.StaticModifications, newTypedExplicitModifications);

            foreach (var typedExplicitModifications in newTypedExplicitModifications)
            {
                foreach (var lightPrecursor in lightPrecursors)
                {
                    var key = Tuple.Create(typedExplicitModifications.LabelType, lightPrecursor.PrecursorAdduct);
                    TransitionGroupDocNode newPrecursor;
                    if (!oldChildren.TryGetValue(key, out newPrecursor))
                    {
                        var transitionGroup = new TransitionGroup(peptideDocNode.Peptide, lightPrecursor.PrecursorAdduct,
                            typedExplicitModifications.LabelType);
                        newPrecursor = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, NewSettings, newExplicitMods, lightPrecursor.LibInfo, lightPrecursor.ExplicitValues, null, new TransitionDocNode[0], lightPrecursor.AutoManageChildren);
                    }

                    newChildren.Add(newPrecursor);
                }
            }

            var newPeptide = (PeptideDocNode) peptideDocNode
                .ChangeExplicitMods(newExplicitMods)
                .ChangeAutoManageChildren(false)
                .ChangeChildren(newChildren.Cast<DocNode>().ToList());
            return newPeptide;
        }

        public List<TypedExplicitModifications> PermuteTypedExplicitModifications(PeptideDocNode peptideDocNode, IList<int> potentiallyModifiedResidues)
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
                    labelType = GetPartialLabelType(i);
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

        public void EnsurePartialLabelTypes(int labelTypeCount)
        {
            while (PartialLabelTypes.Count < labelTypeCount)
            {
                GetPartialLabelType(PartialLabelTypes.Count);
            }
        }

        public IsotopeLabelType GetPartialLabelType(int index)
        {
            if (index < PartialLabelTypes.Count)
            {
                return PartialLabelTypes[index];
            }
            Assume.AreEqual(index, PartialLabelTypes.Count);
            var name = FullyHeavyLabelType.Name + index;
            IsotopeLabelType labelType;
            if (!LabelTypesByName.TryGetValue(name, out labelType))
            {
                labelType = new IsotopeLabelType(name, ++LastSortOrder);
                AddLabelType(labelType);
            }
            PartialLabelTypes.Add(labelType);
            return labelType;
        }

        public void AddLabelType(IsotopeLabelType labelType)
        {
            NewSettings = AddLabelTypeToSettings(NewSettings, labelType);
            LabelTypesByName.Add(labelType.Name, labelType);
        }

        public static SrmSettings AddLabelTypeToSettings(SrmSettings settings, IsotopeLabelType labelType)
        {
            var heavyModifications = settings.PeptideSettings.Modifications.HeavyModifications.ToList();
            heavyModifications.Add(new TypedModifications(labelType, ImmutableList.Empty<StaticMod>()));
            var peptideModifications = settings.PeptideSettings.Modifications;
            peptideModifications = new PeptideModifications(peptideModifications.StaticModifications, peptideModifications.MaxVariableMods, peptideModifications.MaxNeutralLosses, 
                heavyModifications, peptideModifications.InternalStandardTypes);
            return settings.ChangePeptideSettings(settings.PeptideSettings.ChangeModifications(peptideModifications));
        }
    }
}
