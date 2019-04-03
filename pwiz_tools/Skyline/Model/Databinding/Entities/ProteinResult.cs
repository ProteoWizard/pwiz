﻿/*
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

using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [ProteomicDisplayName(nameof(ProteinResult))]
    [InvariantDisplayName("MoleculeListResult")]
    public class ProteinResult : Result
    {
        public ProteinResult(Protein protein, ResultFile resultFile) : base(protein, resultFile)
        {
            
        }

        public override string ToString()
        {
            return File.ToString();
        }

        [InvariantDisplayName("MoleculeList", InUiMode = UiModes.SMALL_MOLECULES)]
        public Protein Protein
        {
            get { return (Protein) SkylineDocNode; }
        }

        public ResultFile File {
            get { return GetResultFile(); }
        }
    }
}
