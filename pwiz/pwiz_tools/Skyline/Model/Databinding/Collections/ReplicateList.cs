/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public class ReplicateList : SkylineObjectList<int, Replicate>
    {
        public ReplicateList(SkylineDataSchema dataSchema) : base(dataSchema)
        {
            OnDocumentChanged();
        }
        protected override IList<int> ListKeys()
        {
            if (!DataSchema.Document.Settings.HasResults)
            {
                return new int[0];
            }
            return Enumerable.Range(0, DataSchema.Document.Settings.MeasuredResults.Chromatograms.Count).ToArray();
        }

        protected override Replicate ConstructItem(int key)
        {
            return new Replicate(DataSchema, key);
        }

        public override int GetKey(Replicate value)
        {
            return value.ReplicateIndex;
        }

        public override IList<Replicate> DeepClone()
        {
            return new ReplicateList(DataSchema.Clone());
        }
    }
}
