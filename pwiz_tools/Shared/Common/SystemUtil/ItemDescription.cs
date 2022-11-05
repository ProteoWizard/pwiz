/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// A description of something suitable for display as a tooltip.
    /// </summary>
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
        /// in cases where the user might not be sure about which item they are pointing at.
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
