using System.Linq;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds <see cref="DocNode"/> elements with any missing results.
    /// </summary>
    public class MissingAnyResultsFinder : AbstractDocNodeFinder
    {
        public override string Name
        {
            get { return "missing_any_results"; }
        }

        public override string DisplayName
        {
            get { return "Missing any results"; }
        }

        protected override bool IsMatch(PeptideDocNode nodePep)
        {
            return nodePep != null && nodePep.HasResults && nodePep.Results.Any(chromInfo => chromInfo == null);
        }

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup)
        {
            return nodeGroup != null && nodeGroup.HasResults && nodeGroup.Results.Any(chromInfo => chromInfo == null);
        }

        protected override bool IsMatch(TransitionDocNode nodeTran)
        {
            return nodeTran != null && nodeTran.HasResults && nodeTran.Results.Any(chromInfo => chromInfo == null);
        }
    }
}