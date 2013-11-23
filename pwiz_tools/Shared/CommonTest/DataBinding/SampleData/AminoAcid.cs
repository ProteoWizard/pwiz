/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;

namespace CommonTest.DataBinding.SampleData
{
    public class AminoAcid
    {
        public AminoAcid(string code)
        {
            Code = code;
        }
        public char CharCode { get { return AminoAcidFormulas.LongNames[Code]; } }
        public string Code
        {
            get;
            private set;
        }
        [OneToMany(IndexDisplayName = "Element", ItemDisplayName = "Count")]
        public Molecule Molecule
        {
            get
            {
                return Molecule.Parse(AminoAcidFormulas.DefaultFormulas[CharCode]);
            }
        }
        public IList<KeyValuePair<double, double>> MassDistribution
        {
            get
            {
                return AminoAcidFormulas.Default.GetMassDistribution(Molecule, 0).ToList();
            }
        }

        public static readonly IList<AminoAcid> AMINO_ACIDS =
            ImmutableList.ValueOf(AminoAcidFormulas.LongNames.Keys.Select(code => new AminoAcid(code)));

    }
}
