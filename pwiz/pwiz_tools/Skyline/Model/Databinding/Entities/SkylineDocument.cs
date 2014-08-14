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

using System;
using pwiz.Skyline.Model.Databinding.Collections;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class SkylineDocument : SkylineDocNode<SrmDocument>
    {
        public SkylineDocument(SkylineDataSchema dataSchema) : base(dataSchema, IdentityPath.ROOT)
        {
            
        }

        protected override SrmDocument CreateEmptyNode()
        {
            throw new InvalidOperationException();
        }

        private Proteins _proteins;
        public Proteins Proteins { get { return _proteins = _proteins ?? new Proteins(DataSchema); } }
        private ReplicateList _replicates;
        public ReplicateList Replicates { get { return _replicates = _replicates ?? new ReplicateList(DataSchema); } }
    }
}
