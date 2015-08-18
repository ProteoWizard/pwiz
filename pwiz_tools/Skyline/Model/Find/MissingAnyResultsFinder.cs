/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            return nodeTran != null && nodeTran.HasResults && nodeTran.Results.Any(chromInfo => chromInfo == null);
        }
    }
}