/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;

namespace pwiz.Skyline.Controls.Graphs
{
    public class MassErrorBinFinder : AbstractFinder
    {
        private readonly HashSet<string> _peptideTextIds;
        private readonly string _displayText;

        public MassErrorBinFinder(IEnumerable<string> peptideTextIds, string displayText)
        {
            _peptideTextIds = new HashSet<string>(peptideTextIds);
            _displayText = displayText;
        }

        public override string Name
        {
            get { return @"mass_error_bin_finder"; }
        }

        public override string DisplayName
        {
            get { return GraphsResources.MassErrorBinFinder_DisplayName_Peptides; }
        }

        public override FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            if (bookmarkEnumerator.ResultsIndex == -1)
            {
                var peptide = bookmarkEnumerator.CurrentDocNode as PeptideDocNode;
                if (peptide != null && _peptideTextIds.Contains(peptide.RawTextId))
                    return new FindMatch(bookmarkEnumerator.Current, _displayText);
            }
            return null;
        }
    }
}
