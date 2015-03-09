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
using System;
using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class GroupComparisonResults
    {
        public GroupComparisonResults(
            GroupComparer groupComparer,
            IEnumerable<GroupComparisonResult> resultRows,
            DateTime startTime,
            DateTime endTime)
        {
            GroupComparer = groupComparer;
            ResultRows = ImmutableList.ValueOf(resultRows);
            StartTime = startTime;
            EndTime = endTime;
        }

        public GroupComparer GroupComparer { get; private set; }
        public SrmDocument Document {get { return GroupComparer.SrmDocument; }}
        public IList<GroupComparisonResult> ResultRows { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
    }
}
