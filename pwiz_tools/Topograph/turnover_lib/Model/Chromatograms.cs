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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class Chromatograms : EntityModelCollection<DbPeptideFileAnalysis, MzKey, DbChromatogram, ChromatogramData>
    {
        public Chromatograms(PeptideFileAnalysis peptideFileAnalysis, DbPeptideFileAnalysis dbPeptideFileAnalysis) 
            : base(peptideFileAnalysis.Workspace, dbPeptideFileAnalysis)
        {
            Parent = peptideFileAnalysis;
        }

        public Chromatograms(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis.Workspace)
        {
            Parent = peptideFileAnalysis;
        }

        public PeptideFileAnalysis PeptideFileAnalysis { get { return (PeptideFileAnalysis) Parent; } }
        protected override IEnumerable<KeyValuePair<MzKey, DbChromatogram>> GetChildren(DbPeptideFileAnalysis parent)
        {
            foreach (var dbChromatogram in parent.Chromatograms)
            {
                yield return new KeyValuePair<MzKey, DbChromatogram>(dbChromatogram.MzKey, dbChromatogram);
            }
        }

        public override ChromatogramData WrapChild(DbChromatogram entity)
        {
            return new ChromatogramData(PeptideFileAnalysis, entity);
        }

        protected override int GetChildCount(DbPeptideFileAnalysis parent)
        {
            return parent.ChromatogramCount;
        }

        protected override void SetChildCount(DbPeptideFileAnalysis parent, int childCount)
        {
            parent.ChromatogramCount = childCount;
        }

        public IList<ChromatogramData> GetFilteredChromatograms()
        {
            var result = new List<ChromatogramData>();
            foreach (var chromatogram in ListChildren())
            {
                if (PeptideFileAnalysis.ExcludedMzs.IsExcluded(chromatogram.MzKey.MassIndex))
                {
                    continue;
                }
                result.Add(chromatogram);
            }
            return result;
        }
    }
}
