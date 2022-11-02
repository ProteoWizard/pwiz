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
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class PcaResults<TLabel>
    {
        public PcaResults(IEnumerable<TLabel> itemLabels, IEnumerable<IEnumerable<double>> itemComponents)
        {
            ItemLabels = ImmutableList.ValueOf(itemLabels);
            ItemComponents = ImmutableList.ValueOf(itemComponents.Select(ImmutableList.ValueOf));
            if (ItemComponents.Count != ItemLabels.Count)
            {
                throw new ArgumentException(@"Wrong number of items", nameof(itemComponents));
            }

            ComponentCount = ItemComponents[0].Count;

            for (int i = 1; i < ItemComponents.Count; i++)
            {
                if (ItemComponents[i].Count != ComponentCount)
                {
                    throw new ArgumentException(string.Format(@"Wrong number of values in list#{0}", i), nameof(itemComponents));
                }
            }
        }

        public ImmutableList<TLabel> ItemLabels { get; }
        public int ComponentCount { get; }
        public ImmutableList<ImmutableList<double>> ItemComponents { get; }
    }
}
