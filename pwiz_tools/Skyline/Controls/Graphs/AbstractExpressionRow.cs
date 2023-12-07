using System;
using System.Collections.Generic;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class AbstractExpressionRow
    {
        public AbstractExpressionRow(Protein protein, ReplicateRow[] replicateRows)
        {

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