/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using pwiz.Skyline.Model;
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
        public static ReplicateGroupOp FromCurrentSettings(SrmDocument document)
        {
            return FromCurrentSettings(document, GraphValues.AggregateOp.FromCurrentSettings());
        }

        /// <summary>
        /// Returns the ReplicateGroupOp based on the current value of Settings.Default.GroupByReplicateAnnotation,
        /// and the specified AggregateOp.  Note that if the ReplicateGroupOp is not grouping on an annotation,
        /// the AggregateOp will be override with the value MEAN.
        /// </summary>
        public static ReplicateGroupOp FromCurrentSettings(SrmDocument document, GraphValues.AggregateOp aggregateOp)
        {
            ReplicateValue replicateValue = null;
            string annotationName = Settings.Default.GroupByReplicateAnnotation;
            if (null != annotationName)
            {
                replicateValue = ReplicateValue.GetGroupableReplicateValues(document)
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