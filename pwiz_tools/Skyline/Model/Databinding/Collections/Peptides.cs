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
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public class Peptides : NodeList<Entities.Peptide> 
    {
        public Peptides(Protein protein) : base(protein.DataSchema, protein.IdentityPath)
        {
        }
        public Peptides(SkylineDataSchema dataSchema, IList<IdentityPath> ancestorIdentityPaths): base(dataSchema, ancestorIdentityPaths)
        {
        }

        protected override Entities.Peptide ConstructItem(IdentityPath identityPath)
        {
            return new Entities.Peptide(DataSchema, identityPath);
        }

        public override IList<Entities.Peptide> DeepClone()
        {
            return new Peptides(DataSchema.Clone(), AncestorIdentityPaths);
        }

        protected override int NodeDepth 
        {
            get { return 2; }
        }
    }
}
