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

namespace CommonTest.DataBinding.SampleData
{
    public class Peptide
    {
        public Peptide(string sequence)
        {
            Sequence = sequence;
        }
        public string Sequence { get; private set; }
        public IList<AminoAcid> AminoAcidsList { get {
            return
                Sequence.Select(c => new AminoAcid(AminoAcidFormulas.LongNames.First(kvp => kvp.Value == c).Key)).
                    ToArray(); } }
        public IDictionary<int,AminoAcid> AminoAcidsDict
        {
            get
            {
                var list = AminoAcidsList;
                var dict = new Dictionary<int, AminoAcid>();
                for (int i = 0; i < list.Count; i++)
                {
                    dict.Add(i, list[i]);
                }
                return dict;
            }
        }
        public Molecule Molecule { get { return AminoAcidFormulas.Default.GetFormula(Sequence); } }
    }
}
