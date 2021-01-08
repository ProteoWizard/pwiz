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
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.DataBinding
{
    public class CaptionedDendrogramData
    {
        public CaptionedDendrogramData(DendrogramData dendrogramData, IEnumerable<CaptionedValues> captionLevels)
        {
            DendrogramData = dendrogramData;
            CaptionLevels = ImmutableList.ValueOf(captionLevels);
            foreach (var captionLevel in CaptionLevels)
            {
                if (captionLevel.Values.Count != dendrogramData.LeafCount)
                {
                    throw new ArgumentException(@"Wrong number of captions", nameof(captionLevels));
                }
            }
        }

        public DendrogramData DendrogramData { get; private set; }

        public ImmutableList<CaptionedValues> CaptionLevels { get; private set; }

        public int LeafCount => DendrogramData.LeafCount;

        public IEnumerable<IColumnCaption> GetLeafCaptions()
        {
            for (int iLeaf = 0; iLeaf < LeafCount; iLeaf++)
            {
                yield return CaptionComponentList.SpaceSeparate(CaptionLevels.Select(level => level.Values[iLeaf]).ToList());
            }
        }
    }
}
