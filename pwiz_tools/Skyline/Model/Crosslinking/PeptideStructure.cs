using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkStructure : Immutable
    {
        public static readonly CrosslinkStructure EMPTY = new CrosslinkStructure(ImmutableList<Peptide>.EMPTY,
            ImmutableList<ExplicitMods>.EMPTY, ImmutableList<Crosslink>.EMPTY);
        public CrosslinkStructure(IEnumerable<Peptide> peptides, IEnumerable<ExplicitMods> explicitModsList, IEnumerable<Crosslink> crosslinks)
        {
            LinkedPeptides = ImmutableList.ValueOf(peptides);
            if (explicitModsList == null)
            {
                LinkedExplicitMods = ImmutableList.ValueOf(new ExplicitMods[LinkedPeptides.Count]);
            }
            else
            {
                LinkedExplicitMods = ImmutableList.ValueOf(explicitModsList);
            }
            if (LinkedExplicitMods.Any(mod => mod != null && mod.HasCrosslinks))
            {
                throw new ArgumentException(@"Cannot nest crosslinks");
            }
            Crosslinks = ImmutableList.ValueOfOrEmpty(crosslinks);
        }

        public static CrosslinkStructure ToPeptide(Peptide peptide, ExplicitMods explicitMods, StaticMod mod, int aaIndex1, int aaIndex2)
        {
            return new CrosslinkStructure(ImmutableList.Singleton(peptide), ImmutableList.Singleton(explicitMods),
                ImmutableList.Singleton(
                    new Crosslink(mod,
                        new[] {new CrosslinkSite(0, aaIndex1), new CrosslinkSite(1, aaIndex2),})
                ));
        }

        public bool IsEmpty
        {
            get
            {
                return LinkedPeptides.Count == 0 && Crosslinks.Count == 0;
            }
        }

        public ImmutableList<Peptide> LinkedPeptides { get; private set; }
        public ImmutableList<ExplicitMods> LinkedExplicitMods { get; private set; }
        public ImmutableList<Crosslink> Crosslinks { get; private set; }

        protected bool Equals(CrosslinkStructure other)
        {
            return LinkedPeptides.Equals(other.LinkedPeptides) && LinkedExplicitMods.Equals(other.LinkedExplicitMods) && Crosslinks.Equals(other.Crosslinks);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CrosslinkStructure) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = LinkedPeptides.GetHashCode();
                hashCode = (hashCode * 397) ^ LinkedExplicitMods.GetHashCode();
                hashCode = (hashCode * 397) ^ Crosslinks.GetHashCode();
                return hashCode;
            }
        }

        public bool HasCrosslinks
        {
            get
            {
                return Crosslinks.Count > 0;
            }
        }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            MoleculeMassOffset result = MoleculeMassOffset.EMPTY;
            for (int i = 0; i < LinkedPeptides.Count; i++)
            {
                IPrecursorMassCalc massCalc = settings.GetPrecursorCalc(labelType, LinkedExplicitMods[i]);
                result = result.Plus(new MoleculeMassOffset(Molecule.Parse(massCalc.GetMolecularFormula(LinkedPeptides[i].Sequence)), 0, 0));
            }

            foreach (var crosslink in Crosslinks)
            {
                result = result.Plus(crosslink.Crosslinker.GetMoleculeMassOffset());
            }

            return result;
        }

        public bool IsConnected()
        {
            var allPeptideIndexes = Crosslinks.SelectMany(link => link.Sites.PeptideIndexes).ToHashSet();
            if (allPeptideIndexes.Count == 0)
            {
                return false;
            }

            if (allPeptideIndexes.Count != allPeptideIndexes.Max() + 1)
            {
                return false;
            }

            if (allPeptideIndexes.Min() != 0)
            {
                return false;
            }
            var visitedPeptides = new HashSet<int> { 0 };
            IReadOnlyList<Crosslink> remainingCrosslinks = Crosslinks;
            while (true)
            {
                var nextQueue = new List<Crosslink>();
                foreach (var crosslink in remainingCrosslinks)
                {
                    if (crosslink.Sites.PeptideIndexes.Any(visitedPeptides.Contains))
                    {
                        visitedPeptides.UnionWith(crosslink.Sites.PeptideIndexes);
                    }
                    else
                    {
                        nextQueue.Add(crosslink);
                    }
                }

                if (nextQueue.Count == 0)
                {
                    return true;
                }

                if (nextQueue.Count == remainingCrosslinks.Count)
                {
                    return false;
                }

                remainingCrosslinks = nextQueue;
            }
        }
    }

    public class PeptideStructure
    {
        public PeptideStructure(Peptide peptide, ExplicitMods explicitMods)
        {
            var crosslinkStructure = explicitMods?.Crosslinks ?? CrosslinkStructure.EMPTY;
            Peptides = ImmutableList.ValueOf(crosslinkStructure.LinkedPeptides.Prepend(peptide));
            ExplicitModList =
                ImmutableList.ValueOf(
                    crosslinkStructure.LinkedExplicitMods.Prepend(
                        explicitMods?.ChangeCrosslinks(CrosslinkStructure.EMPTY)));
            Crosslinks = crosslinkStructure.Crosslinks;
        }

        public ImmutableList<Peptide> Peptides { get; private set; }
        public ImmutableList<ExplicitMods> ExplicitModList { get; private set; }
        public ImmutableList<Crosslink> Crosslinks { get; private set; }
    }
}