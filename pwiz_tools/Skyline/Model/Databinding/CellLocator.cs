/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Class for finding which DocNode and replicate a particular cell in a grid belongs to.
    /// This is figured out by walking up the ancestor tree of a pivoted column, and looking
    /// at all of the non-pivoted columns in the same row.
    /// </summary>
    public class CellLocator
    {
        private ImmutableList<RowItemValues> _skylineDocNodeValues;
        private RowItemValues _replicateValues;
        private CellLocator(IEnumerable<RowItemValues> skylineDocNodeValues, RowItemValues replicateValues)
        {
            _skylineDocNodeValues = ImmutableList.ValueOf(skylineDocNodeValues);
            _replicateValues = replicateValues;
        }

        public SkylineDocNode GetSkylineDocNode(RowItem rowItem)
        {
            return _skylineDocNodeValues.SelectMany(v=>v.GetRowValues(rowItem))
                .OfType<SkylineDocNode>().FirstOrDefault();
        }

        public Replicate GetReplicate(RowItem rowItem)
        {
            return _replicateValues.GetRowValues(rowItem).OfType<IReplicateValue>()
                .Select(r => r.GetReplicate()).FirstOrDefault(r => r != null);
        }

        public static CellLocator ForColumn(DataPropertyDescriptor columnPropertyDescriptor,
            ICollection<DataPropertyDescriptor> otherPropertyDescriptors)
        {
            var docNodeValues = new[]
                {
                    typeof(Entities.Transition),
                    typeof(Precursor),
                    typeof(Entities.Peptide),
                    typeof(Protein)
                }.Select(type => RowItemValues.ForCell(type, columnPropertyDescriptor, otherPropertyDescriptors))
                .Where(v => !v.IsEmpty).ToList();
            var replicateValue = RowItemValues.ForCell(typeof(IReplicateValue), columnPropertyDescriptor,
                otherPropertyDescriptors);
            return new CellLocator(docNodeValues, replicateValue);
        }
    }
}
