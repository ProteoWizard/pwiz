using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIon : Immutable
    {
        public ComplexFragmentIon(IEnumerable<Transition> transitions, TransitionLosses losses)
        {
            Transitions = ImmutableList.ValueOf(transitions);
            Losses = losses;
        }

        public ImmutableList<Transition> Transitions { get; private set; }

        public TransitionLosses Losses { get; private set; }

        public bool IncludesSite(CrosslinkSite site)
        {
            return Transitions[site.PeptideIndex].IncludesAaIndex(site.AaIndex);
        }

        public ComplexFragmentIon CloneTransition()
        {
            return ChangeProp(ImClone(this), im => im.Transitions = im.Transitions.ReplaceAt(0, (Transition)Transitions[0].Copy()));
        }

    }
}
