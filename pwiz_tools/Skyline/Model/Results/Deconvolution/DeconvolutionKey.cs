using System;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Deconvolution
{
    public class DeconvolutionKey : Immutable
    {
        public DeconvolutionKey(PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode,
            MassDistribution massDistribution)
        {
            PeptideDocNode = peptideDocNode;
            TransitionGroupDocNode = transitionGroupDocNode;
            MassDistribution = massDistribution;
        }

        public PeptideDocNode PeptideDocNode { get; private set; }
        public TransitionGroupDocNode TransitionGroupDocNode { get; private set; }
        public MassDistribution MassDistribution { get; private set; }

        public bool IsNegativeCharge
        {
            get
            {
                return TransitionGroupDocNode.PrecursorCharge < 0;
            }
        }
    }
}
