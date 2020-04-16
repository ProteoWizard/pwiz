using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkMod : Immutable
    {
        public CrosslinkMod(int indexAa, CrosslinkerDef crosslinkerDef, IEnumerable<LinkedPeptide> linkedPeptides)
        {
            IndexAa = indexAa;
            CrosslinkerDef = crosslinkerDef;
            LinkedPeptides = ImmutableList.ValueOf(linkedPeptides);
        }

        public int IndexAa { get; private set; }

        public CrosslinkerDef CrosslinkerDef { get; private set; }

        public ImmutableList<LinkedPeptide> LinkedPeptides { get; private set; }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            var massType = settings.TransitionSettings.Prediction.PrecursorMassType;
            MoleculeMassOffset moleculeMassOffset = CrosslinkerDef.IntactFormula.GetMoleculeMassOffset(massType);
            foreach (var linkedPeptide in LinkedPeptides)
            {
                moleculeMassOffset = moleculeMassOffset.Plus(linkedPeptide.GetNeutralFormula(settings, labelType));
            }

            return moleculeMassOffset;
        }

        public ModificationSite ModificationSite
        {
            get
            {
                return new ModificationSite(IndexAa, CrosslinkerDef.Name);
            }
        }
    }
}
