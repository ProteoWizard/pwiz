using System.Linq;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds <see cref="DocNode"/> elements with any missing results.
    /// </summary>
    public class MissingAnyResultsFinder : AbstractDocNodeFinder
    {
        public override string Name
        {
            get { return "missing_any_results"; } // Not L10N
        }

        public override string DisplayName
        {
            get { return Resources.MissingAnyResultsFinder_DisplayName_Missing_any_results; }
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