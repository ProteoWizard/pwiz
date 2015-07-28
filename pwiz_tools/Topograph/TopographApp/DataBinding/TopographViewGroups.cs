using System.Collections.Generic;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;

namespace pwiz.Topograph.ui.DataBinding
{
    [XmlRoot("topograph_views")]
    public class TopographViewGroups : SerializableViewGroups
    {
        public TopographViewGroups(ViewSpecList oldViewSpecList)
        {
            oldViewSpecList = oldViewSpecList ?? ViewSpecList.EMPTY;
            Dictionary<ViewGroupId, List<ViewSpec>> viewGroups = new Dictionary<ViewGroupId, List<ViewSpec>>();
            foreach (var viewSpec in oldViewSpecList.ViewSpecs)
            {
                var viewGroup = TopographViewGroup.GetGroupId(viewSpec);
                List<ViewSpec> viewSpecs;
                if (!viewGroups.TryGetValue(viewGroup, out viewSpecs))
                {
                    viewSpecs = new List<ViewSpec>();
                    viewGroups.Add(viewGroup, viewSpecs);
                }
                viewSpecs.Add(viewSpec);
            }
            foreach (var entry in viewGroups)
            {
                SetViewSpecList(entry.Key, new ViewSpecList(entry.Value));
            }
        }
    }
}
