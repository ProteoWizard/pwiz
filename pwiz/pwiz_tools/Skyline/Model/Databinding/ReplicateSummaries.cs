/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Databinding
{
    public class ReplicateSummaries
    {
        private readonly ImmutableSortedList<IsotopeLabelType, double>[] _allTotalAreas;
        public ReplicateSummaries(SrmDocument document)
        {
            Document = document;
            int replicateCount = 0;
            if (Document.Settings.HasResults)
            {
                replicateCount = Document.Settings.MeasuredResults.Chromatograms.Count;
            }
            _allTotalAreas = new ImmutableSortedList<IsotopeLabelType, double>[replicateCount];
        }
        public ReplicateSummaries GetReplicateSummaries(SrmDocument document)
        {
            if (ReferenceEquals(document, Document))
            {
                return this;
            }
            if (Equals(document, Document))
            {
                var result = new ReplicateSummaries(document);
                lock (_allTotalAreas)
                {
                    _allTotalAreas.CopyTo(result._allTotalAreas, 0);
                }
                return result;
            }
            return new ReplicateSummaries(document);
        }
        public SrmDocument Document { get; private set; }
        public double GetTotalArea(int replicateIndex, IsotopeLabelType isotopeLabelType)
        {
            if (replicateIndex >= _allTotalAreas.Length)
            {
                return 0;
            }
            ImmutableSortedList<IsotopeLabelType, double> areasByLabelType;
            lock (_allTotalAreas)
            {
                areasByLabelType = _allTotalAreas[replicateIndex];
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (null == areasByLabelType)
                {
                    areasByLabelType = _allTotalAreas[replicateIndex] =
                        ImmutableSortedList.FromValues(CalculateTotalAreas(replicateIndex));
                }
            }
            double area;
            areasByLabelType.TryGetValue(isotopeLabelType, out area);
            return area;
        }

        private IDictionary<IsotopeLabelType, double> CalculateTotalAreas(int replicateIndex)
        {
            var dictTotalAreas = new Dictionary<IsotopeLabelType, double>();
            foreach (var nodeGroup in Document.MoleculeTransitionGroups)
            {
                if (!nodeGroup.HasResults || nodeGroup.Results[replicateIndex] == null)
                    continue;
                var labelType = nodeGroup.TransitionGroup.LabelType;

                foreach (var chromInfo in nodeGroup.Results[replicateIndex])
                {
                    if (chromInfo.OptimizationStep == 0)
                    {
                        double sumTotalArea;
                        dictTotalAreas.TryGetValue(labelType, out sumTotalArea);
                        dictTotalAreas[labelType] = sumTotalArea + chromInfo.Area ?? 0;
                    }
                }
            }
            return dictTotalAreas;
        }
    }
}
