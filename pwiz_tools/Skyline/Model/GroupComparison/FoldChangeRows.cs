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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.GroupComparison
{
    public abstract class AbstractFoldChangeRow
    {
        public AbstractFoldChangeRow(Protein protein, Databinding.Entities.Peptide peptide,
            IsotopeLabelType labelType,
            int? msLevel, IDictionary<Replicate, ReplicateRow> replicateResults)
        {
            Protein = protein;
            Peptide = peptide;
            IsotopeLabelType = labelType;
            MsLevel = msLevel;
            ReplicateAbundances = replicateResults;
        }

        public Protein Protein { get; private set; }
        public Databinding.Entities.Peptide Peptide { get; private set; }
        public IsotopeLabelType IsotopeLabelType { get; private set; }
        public int? MsLevel { get; private set; }

        [OneToMany(IndexDisplayName = "Replicate")]
        public IDictionary<Replicate, ReplicateRow> ReplicateAbundances { get; private set; }

        public abstract IEnumerable<FoldChangeRow> GetFoldChangeRows();
    }

    public class FoldChangeRow : AbstractFoldChangeRow
    {
        public FoldChangeRow(Protein protein, Databinding.Entities.Peptide peptide, IsotopeLabelType labelType,
            int? msLevel, GroupIdentifier group, int replicateCount, FoldChangeResult foldChangeResult,
            IDictionary<Replicate, ReplicateRow> replicateResults)
            : base(protein, peptide, labelType, msLevel, replicateResults)
        {
            ReplicateCount = replicateCount;
            FoldChangeResult = foldChangeResult;
            Group = group;
        }

        public GroupIdentifier Group { get; private set; }
        public int ReplicateCount { get; private set; }
        public FoldChangeResult FoldChangeResult { get; private set; }

        public override IEnumerable<FoldChangeRow> GetFoldChangeRows()
        {
            yield return this;
        }
    }

    public class FoldChangeDetailRow : AbstractFoldChangeRow
    {
        public FoldChangeDetailRow(Protein protein, Databinding.Entities.Peptide peptide,
            IsotopeLabelType labelType,
            int? msLevel, Dictionary<GroupIdentifier, FoldChangeResult> foldChangeResults,
            IDictionary<Replicate, ReplicateRow> replicateResult) : base(protein, peptide, labelType, msLevel,
            replicateResult)
        {
            FoldChangeResults = foldChangeResults;
        }

        [OneToMany(ItemDisplayName = "FoldChange", IndexDisplayName = "GroupIdentifier")]
        public IDictionary<GroupIdentifier, FoldChangeResult> FoldChangeResults { get; private set; }

        public override IEnumerable<FoldChangeRow> GetFoldChangeRows()
        {
            return FoldChangeResults.Select(kvp =>
                new FoldChangeRow(Protein, Peptide, IsotopeLabelType, MsLevel, kvp.Key, 0, kvp.Value,
                    ReplicateAbundances));
        }
    }

    [InvariantDisplayName("ReplicateAbundance")]
    public class ReplicateRow : IReplicateValue
    {
        public ReplicateRow(Replicate replicate, GroupIdentifier groupIdentifier, GroupIdentifier? identity,
            double? abundance)
        {
            Replicate = replicate;
            ReplicateGroup = groupIdentifier;
            ReplicateSampleIdentity = identity;
            Abundance = abundance;
        }

        public Replicate Replicate { get; private set; }

        [Format(Formats.CalibrationCurve)] 
        public double? Abundance { get; private set; }
        public GroupIdentifier? ReplicateSampleIdentity { get; private set; }
        public GroupIdentifier ReplicateGroup { get; private set; }

        Replicate IReplicateValue.GetReplicate()
        {
            return Replicate;
        }

        public override string ToString()
        {
            var parts = new List<string> { Replicate.ToString() };
            if (Abundance.HasValue)
            {
                parts.Add(Abundance.Value.ToString(Formats.CalibrationCurve));
            }

            return string.Join(@" ", parts);
        }
    }
}