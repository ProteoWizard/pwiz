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
    /// Finds all transitions that were manually integrated.
    /// </summary>
    public class ManuallyIntegratedPeakFinder : AbstractTransitionResultFinder
    {
        public override string Name
        {
            get
            {
                return "manually_integrated_peaks"; // Not L10N
            }
        }
        public override string DisplayName
        {
            get { return Resources.ManuallyIntegratedPeakFinder_DisplayName_Manually_integrated_peaks; }
        }

        protected override FindMatch MatchTransition(TransitionChromInfo transitionChromInfo)
        {
            return transitionChromInfo.IsUserSetManual
                       ? new FindMatch(Resources.ManuallyIntegratedPeakFinder_MatchTransition_Manually_integrated_peak)
                       : null;
        }

        protected override FindMatch MatchTransitionGroup(TransitionGroupChromInfo transitionGroupChromInfo)
        {
            return transitionGroupChromInfo.IsUserSetManual
                       ? new FindMatch(Resources.ManuallyIntegratedPeakFinder_DisplayName_Manually_integrated_peaks)
                       : null;
        }
    }
}
