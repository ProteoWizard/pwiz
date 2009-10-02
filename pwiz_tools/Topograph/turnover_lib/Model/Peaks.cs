/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class Peaks : SimpleChildCollection<DbPeptideFileAnalysis, MzKey, DbPeak>
    {
        public Peaks(PeptideFileAnalysis peptideFileAnalysis, DbPeptideFileAnalysis dbPeptideFileAnalysis) : base(peptideFileAnalysis.Workspace, dbPeptideFileAnalysis)
        {
            PeptideFileAnalysis = peptideFileAnalysis;
        }
        protected override IEnumerable<KeyValuePair<MzKey, DbPeak>> GetChildren(DbPeptideFileAnalysis parent)
        {
            foreach (var peak in parent.Peaks)
            {
                yield return new KeyValuePair<MzKey, DbPeak>(peak.MzKey, peak);
            }
        }

        protected override int GetChildCount(DbPeptideFileAnalysis parent)
        {
            return parent.PeakCount;
        }

        protected override void SetChildCount(DbPeptideFileAnalysis parent, int childCount)
        {
            parent.PeakCount = childCount;
        }

        protected override void SetParent(DbPeak child, DbPeptideFileAnalysis parent)
        {
            child.PeptideFileAnalysis = parent;
        }

        public PeptideFileAnalysis PeptideFileAnalysis { get; private set; }
        protected override void OnChange()
        {
            PeptideFileAnalysis.PeptideDistributions.Clear();
            base.OnChange();
        }
    }
}