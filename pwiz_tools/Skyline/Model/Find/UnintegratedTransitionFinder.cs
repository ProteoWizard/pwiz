﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds all peaks which have an area of 0 indicating Skyline was not able to find a 
    /// peak to integrated.
    /// </summary>
    public class UnintegratedTransitionFinder : AbstractFinder
    {
        public override string Name
        {
            get { return @"unintegrated_transitions"; }
        }

        public override string DisplayName
        {
            get
            {
                return Resources.UnintegratedTransitionFinder_DisplayName_Unintegrated_transitions;
            }
        }

        public override FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            var transitionChromInfo = bookmarkEnumerator.CurrentChromInfo as TransitionChromInfo;
            if (transitionChromInfo == null)
            {
                return null;
            }
            bool integrateAll = bookmarkEnumerator.Document.Settings.TransitionSettings.Integration.IsIntegrateAll;
            if (!transitionChromInfo.IsGoodPeak(integrateAll))
            {
                return new FindMatch(Resources.UnintegratedTransitionFinder_Match_Unintegrated_transition);
            }
            return null;
        }
    }
}
