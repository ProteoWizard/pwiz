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

using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds tranisitions and precursors missing library data.
    /// </summary>
    public class MissingLibraryDataFinder : AbstractDocNodeFinder
    {
        public override string Name
        {
            get { return "missing_library_data"; } // Not L10N
        }

        public override string DisplayName
        {
            get { return Resources.MissingLibraryDataFinder_DisplayName_No_matching_library_data; }
        }

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup)
        {
            return nodeGroup != null && !nodeGroup.HasLibInfo;
        }

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            return nodeTran != null && !nodeTran.HasLibInfo;
        }
    }
}