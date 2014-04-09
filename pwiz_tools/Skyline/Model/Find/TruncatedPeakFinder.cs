/*
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
    /// Finds all peaks that were truncated.
    /// </summary>
    public class TruncatedPeakFinder : AbstractTransitionResultFinder
    {
        public override string Name
        {
            get { return "truncated_peaks"; } // Not L10N
        }

        public override string DisplayName
        {
            get { return Resources.TruncatedPeakFinder_DisplayName_Truncated_peaks; }
        }

        protected override FindMatch MatchTransition(TransitionChromInfo transitionChromInfo)
        {
            if (transitionChromInfo.IsTruncated.GetValueOrDefault(false))
            {
                return new FindMatch(Resources.TruncatedPeakFinder_MatchTransition_Truncated_peak);
            }
            return null;
        }

        protected override FindMatch MatchTransitionGroup(TransitionGroupChromInfo transitionGroupChromInfo)
        {
            int truncatedCount = transitionGroupChromInfo.Truncated.GetValueOrDefault(0);
            if (truncatedCount > 0)
            {
                if (truncatedCount == 1)
                {
                    return new FindMatch(Resources.TruncatedPeakFinder_MatchTransitionGroup__1_truncated_peak);
                }
                return new FindMatch(string.Format(Resources.TruncatedPeakFinder_MatchTransitionGroup__0__truncated_peaks,
                                                   truncatedCount));
            }
            return null;
        }
    }
}
