using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
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