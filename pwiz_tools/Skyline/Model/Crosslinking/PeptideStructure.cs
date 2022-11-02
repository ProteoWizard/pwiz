/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// A main peptide crosslinked with zero or more other peptides by one or more crosslinks.
    /// </summary>
    public class PeptideStructure
    {
        public PeptideStructure(Peptide peptide, ExplicitMods explicitMods)
        {
            var crosslinkStructure = explicitMods?.CrosslinkStructure ?? CrosslinkStructure.EMPTY;
            Peptides = ImmutableList.ValueOf(crosslinkStructure.LinkedPeptides.Prepend(peptide));
            ExplicitModList =
                ImmutableList.ValueOf(
                    crosslinkStructure.LinkedExplicitMods.Prepend(
                        explicitMods?.ChangeCrosslinkStructure(CrosslinkStructure.EMPTY)));
            Crosslinks = crosslinkStructure.Crosslinks;
        }

        public ImmutableList<Peptide> Peptides { get; private set; }
        public ImmutableList<ExplicitMods> ExplicitModList { get; private set; }
        public ImmutableList<Crosslink> Crosslinks { get; private set; }
    }
}