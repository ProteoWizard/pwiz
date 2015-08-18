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
    /// Finds all peaks that were truncated.
    /// </summary>
    public class MissingAllResultsFinder : AbstractDocNodeFinder
    {
        public override string Name
        {
            get { return "missing_all_results"; } // Not L10N
        }

        public override string DisplayName
        {
            get { return Resources.MissingAllResultsFinder_DisplayName_Missing_all_results; }
        }

        protected override bool IsMatch(PeptideDocNode nodePep)
        {
            return nodePep != null && nodePep.HasResults && nodePep.Results.All(chromInfo => chromInfo == null);
        }

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup)
        {
            return nodeGroup != null && nodeGroup.HasResults && nodeGroup.Results.All(chromInfo => chromInfo == null);
        }

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            return nodeTran != null && nodeTran.HasResults && nodeTran.Results.All(chromInfo => chromInfo == null);
        }
    }
}