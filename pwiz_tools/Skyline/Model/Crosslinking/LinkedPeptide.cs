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
using JetBrains.Annotations;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class LinkedPeptide : Immutable
    {
        public static readonly ImmutableSortedList<ModificationSite, LinkedPeptide> EMPTY_CROSSLINK_STRUCTURE 
            = ImmutableSortedList<ModificationSite, LinkedPeptide>.EMPTY;

        public LinkedPeptide(Peptide peptide, int indexAa, ExplicitMods explicitMods)
        {
            Peptide = peptide;
            IndexAa = indexAa;
            ExplicitMods = explicitMods;
        }

        public Peptide Peptide { get; private set; }
        public int IndexAa { get; private set; }

        public int Ordinal
        {
            get { return IndexAa + 1; }
        }

        public ExplicitMods ExplicitMods
        {
            get; private set;
        }
    }
}
