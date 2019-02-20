using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Ways of combining replicates together on a graph into a single point.
    /// Replicates may be combined based on the value of an annotation on the replicate.
    /// </summary>
    public class ReplicateGroupOp
    {
        private ReplicateGroupOp(ReplicateValue groupByValue, GraphValues.AggregateOp aggregateOp)
        {
            GroupByValue = groupByValue;
            AggregateOp = aggregateOp;
        }

        public ReplicateValue GroupByValue { get; private set; }
        public GraphValues.AggregateOp AggregateOp { get; private set; }
        [Localizable(true)]
        public string ReplicateAxisTitle
        {
            get
            {
                if (GroupByValue == null)
                {
                    return Resources.ReplicateGroupOp_ReplicateAxisTitle;
                }

                return GroupByValue.Title;
            }
        }

        /// <summary>
        /// Returns the ReplicateGroupOp based on the current value of Settings.Default.GroupByReplicateAnnotation,
        /// and the current Settings.Default.ShowPeptideCV.  Note that if the ReplicateGroupOp is not grouping on
        /// an annotation, the AggregateOp will always be set to MEAN.
        /// </summary>
        public static ReplicateGroupOp FromCurrentSettings(SrmSettings settings)
        {
            return FromCurrentSettings(settings, GraphValues.AggregateOp.FromCurrentSettings());
        }

        /// <summary>
        /// Returns the ReplicateGroupOp based on the current value of Settings.Default.GroupByReplicateAnnotation,
        /// and the specified AggregateOp.  Note that if the ReplicateGroupOp is not grouping on an annotation,
        /// the AggregateOp will be override with the value MEAN.
        /// </summary>
        public static ReplicateGroupOp FromCurrentSettings(SrmSettings settings, GraphValues.AggregateOp aggregateOp)
        {
            ReplicateValue replicateValue = null;
            string annotationName = Settings.Default.GroupByReplicateAnnotation;
            if (null != annotationName)
            {
                replicateValue = ReplicateValue.GetGroupableReplicateValues(settings)
                    .FirstOrDefault(value => value.ToPersistedString() == annotationName);
            }
            if (null == replicateValue)
            {
                aggregateOp = GraphValues.AggregateOp.MEAN;
            }
            return new ReplicateGroupOp(replicateValue, aggregateOp);
        }
    }
}