/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataAnalysis.FoldChange
{
    /// <summary>
    /// Holds data for calculating fold changes.
    /// The data values are stored in the list <see cref="Abundances"/>.  
    /// The abundances are the log2 normalized intensity.
    /// <see cref="Features" /> holds integers representing which transition the abundance is for.
    /// <see cref="Subjects" /> holds integers representing the subject ids.
    /// are in the control group, and the rest of the subject ids are in the other group.
    /// </summary>
    public class FoldChangeDataSet
    {
        public FoldChangeDataSet(ICollection<double> abundances, ICollection<int> features, ICollection<int> runs, ICollection<int> subjects, ICollection<bool> subjectControls)
        {
            Abundances = ImmutableList.ValueOf(abundances);
            Runs = ImmutableList.ValueOf(runs);
            Features = ImmutableList.ValueOf(features);
            Subjects = ImmutableList.ValueOf(subjects);
            SubjectControls = ImmutableList.ValueOf(subjectControls);

            if (abundances.Count != features.Count || abundances.Count != subjects.Count || abundances.Count != Runs.Count)
            {
                throw new ArgumentException("Wrong number of rows"); // Not L10N
            }
            if (abundances.Count == 0)
            {
                FeatureCount = 0;
                SubjectCount = 0;
                RunCount = 0;
            }
            else
            {
                if (features.Min() < 0 || Runs.Min() < 0 || subjects.Min() < 0)
                {
                    throw new ArgumentException("Cannot be negative"); // Not L10N
                }
                FeatureCount = Features.Max() + 1;
                SubjectCount = Subjects.Max() + 1;
                RunCount = Runs.Max() + 1;
            }
            if (subjectControls.Count != SubjectCount)
            {
                throw new ArgumentException("Wrong number of subjects"); // Not L10N
            }
        }
        public IList<double> Abundances { get; private set; }
        public IList<int> Features { get; private set; }
        public IList<int> Runs { get; private set; }
        public IList<int> Subjects { get; private set; }
        public IList<bool> SubjectControls { get; private set; }
        public int FeatureCount { get; private set; }
        public int SubjectCount { get; private set; }
        public int RunCount { get; private set; }
        public int RowCount { get { return Abundances.Count; } }

        public bool IsSubjectInControlGroup(int subjectId)
        {
            return SubjectControls[subjectId];
        }
        
        public bool IsRowInControlGroup(int rowIndex)
        {
            return IsSubjectInControlGroup(Subjects[rowIndex]);
        }

        public int GetFeatureCountForRun(int runId)
        {
            return GetFeaturesForRun(runId).Count;
        }

        public ICollection<int> GetFeaturesForRun(int runId)
        {
            var result = new HashSet<int>();
            for (int iRow = 0; iRow < RowCount; iRow++)
            {
                if (Runs[iRow] == runId)
                {
                    result.Add(Features[iRow]);
                }
            }
            return result;
        }
    }
}
