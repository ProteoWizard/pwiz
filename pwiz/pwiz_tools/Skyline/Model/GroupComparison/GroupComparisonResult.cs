/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Common.DataAnalysis;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class GroupComparisonResult
    {
        public GroupComparisonResult(GroupComparisonSelector selector, int replicateCount, LinearFitResult linearFitResult)
        {
            Selector = selector;
            LinearFitResult = linearFitResult;
            ReplicateCount = replicateCount;
        }

        public GroupComparisonSelector Selector { get; private set; }
        public int ReplicateCount { get; private set; }
        public LinearFitResult LinearFitResult { get; private set; }
    }
}