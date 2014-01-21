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
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model.Data
{
    public class PeptideData
    {
        public PeptideData(DbPeptide dbPeptide)
        {
            FullSequence = dbPeptide.FullSequence;
            Sequence = dbPeptide.Sequence;
            ProteinName = dbPeptide.Protein;
            ProteinDescription = dbPeptide.ProteinDescription;
        }

        public PeptideData(PeptideData peptideData)
        {
            FullSequence = peptideData.FullSequence;
            Sequence = peptideData.Sequence;
            ProteinName = peptideData.ProteinName;
            ProteinDescription = peptideData.ProteinDescription;
        }

        public string FullSequence { get; private set; }
        public PeptideData SetFullSequence(string fullSequence)
        {
            return new PeptideData(this){FullSequence = fullSequence};
        }
        public string Sequence { get; private set; }
        public string ProteinName { get; private set; }
        public PeptideData SetProteinName(string value)
        {
            return new PeptideData(this){ProteinName = value};
        }
        public string ProteinDescription { get; private set; }
        public PeptideData SetProteinDescription(string value)
        {
            return new PeptideData(this){ProteinDescription = value};
        }

        public static IList<DataProperty<PeptideData>> MergeableProperties =
            ImmutableList.ValueOf(
                new DataProperty<PeptideData>[]
                    {
                        new DataProperty<PeptideData, string>(data=>data.FullSequence, (data,value)=>data.SetFullSequence(value)),
                        new DataProperty<PeptideData, string>(data => data.ProteinName,
                                                              (data, value) => data.SetProteinName(value)),
                        new DataProperty<PeptideData, string>(data => data.ProteinDescription,
                                                              (data, value) => data.SetProteinDescription(value)),
                    });

    }
}
