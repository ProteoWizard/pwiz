using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.SystemUtil
{
    public class ItemDescription : Immutable
    {
        public ItemDescription(string summary)
        {
            Title = null;
            Summary = summary;
            DetailLines = ImmutableList.Singleton(summary);
        }

        /// <summary>
        /// The name of the item being described. The name will be included as part of the tooltip
        /// in cases where the user might not be user about which item they are pointing at.
        /// </summary>
        public string Title { get; private set; }

        public ItemDescription ChangeTitle(string title)
        {
            return ChangeProp(ImClone(this), im => im.Title = title);
        }

        public string Summary { get; }
        public ImmutableList<string> DetailLines { get; private set; }

        public ItemDescription ChangeDetailLines(IEnumerable<string> detailLines)
        {
            return ChangeProp(ImClone(this), im => im.DetailLines = ImmutableList.ValueOf(detailLines));
        }

        public ItemDescription AppendDetailLines(params string[] lines)
        {
            return ChangeProp(ImClone(this), im => im.DetailLines = ImmutableList.ValueOf(DetailLines.Concat(lines)));
        }

        public override string ToString()
        {
            var lines = string.IsNullOrEmpty(Title) ? DetailLines : DetailLines.Prepend(Title);
            return string.Join(Environment.NewLine, lines);
        }
    }

    public interface IHasItemDescription
    {
        ItemDescription ItemDescription { get; }
    }
}
