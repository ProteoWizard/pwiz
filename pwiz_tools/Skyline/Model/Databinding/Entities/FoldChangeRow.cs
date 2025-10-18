using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public abstract class AbstractFoldChangeRow
    {
        private GroupComparisonDef _groupComparisonDef;
        public AbstractFoldChangeRow(GroupComparisonDef groupComparisonDef, Protein protein, Peptide peptide,
            IsotopeLabelType labelType,
            int? msLevel, IDictionary<Replicate, ReplicateRow> replicateResults)
        {
            _groupComparisonDef = groupComparisonDef;
            Protein = protein;
            Peptide = peptide;
            IsotopeLabelType = labelType;
            MsLevel = msLevel;
            ReplicateAbundances = replicateResults;
        }

        public Protein Protein { get; private set; }
        public Peptide Peptide { get; private set; }
        public IsotopeLabelType IsotopeLabelType { get; private set; }
        public int? MsLevel { get; private set; }

        [OneToMany(IndexDisplayName = "Replicate")]
        public IDictionary<Replicate, ReplicateRow> ReplicateAbundances { get; private set; }
        public abstract IEnumerable<FoldChangeRow> GetFoldChangeRows();
        public string GroupComparison
        {
            get { return _groupComparisonDef?.Name; }
        }

        public GroupComparisonDef GetGroupComparisonDef()
        {
            return _groupComparisonDef;
        }
    }

    [RowSourceName(ROW_SOURCE_NAME)]
    public class FoldChangeRow : AbstractFoldChangeRow
    {
        public const string ROW_SOURCE_NAME = "pwiz.Skyline.Controls.GroupComparison.FoldChangeBindingSource.FoldChangeRow";
        public FoldChangeRow(GroupComparisonDef groupComparison, Protein protein, Peptide peptide, IsotopeLabelType labelType,
            int? msLevel, GroupIdentifier group, int replicateCount, FoldChangeResult foldChangeResult, IDictionary<Replicate, ReplicateRow> replicateResults)
            : base(groupComparison, protein, peptide, labelType, msLevel, replicateResults)
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

    [RowSourceName(ROW_SOURCE_NAME)]
    public class FoldChangeDetailRow : AbstractFoldChangeRow
    {
        public const string ROW_SOURCE_NAME =
            "pwiz.Skyline.Controls.GroupComparison.FoldChangeBindingSource.FoldChangeDetailRow";
        public FoldChangeDetailRow(GroupComparisonDef groupComparisonDef, Protein protein, Peptide peptide,
            IsotopeLabelType labelType,
            int? msLevel, Dictionary<GroupIdentifier, FoldChangeResult> foldChangeResults,
            IDictionary<Replicate, ReplicateRow> replicateResult) : base(groupComparisonDef, protein, peptide, labelType, msLevel, replicateResult)
        {
            FoldChangeResults = foldChangeResults;
        }

        [OneToMany(ItemDisplayName = "FoldChange", IndexDisplayName = "GroupIdentifier")]
        public IDictionary<GroupIdentifier, FoldChangeResult> FoldChangeResults { get; private set; }

        public override IEnumerable<FoldChangeRow> GetFoldChangeRows()
        {
            return FoldChangeResults.Select(kvp =>
                new FoldChangeRow(GetGroupComparisonDef(), Protein, Peptide, IsotopeLabelType, MsLevel, kvp.Key, 0, kvp.Value, ReplicateAbundances));
        }
    }

    [InvariantDisplayName("ReplicateAbundance")]
    public class ReplicateRow : IReplicateValue
    {
        public ReplicateRow(Replicate replicate, GroupIdentifier groupIdentifier, String identity, double? abundance)
        {
            Replicate = replicate;
            ReplicateGroup = groupIdentifier;
            ReplicateSampleIdentity = identity;
            Abundance = abundance;
        }
        public Replicate Replicate { get; private set; }
        [Format(Formats.CalibrationCurve)]
        public double? Abundance { get; private set; }
        public string ReplicateSampleIdentity { get; private set; }
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

            return TextUtil.SpaceSeparate(parts);
        }
    }

}
