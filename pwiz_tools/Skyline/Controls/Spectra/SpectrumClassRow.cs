﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Results.Spectra;

namespace pwiz.Skyline.Controls.Spectra
{
    public class SpectrumClassRow
    {
        public SpectrumClassRow(MatchingPrecursors matchingPrecursors, SpectrumClass spectrumClass)
        {
            MatchingPrecursors = matchingPrecursors;
            Properties = spectrumClass;
            Files = new Dictionary<string, FileSpectrumInfo>();
        }

        public MatchingPrecursors MatchingPrecursors { get; }

        public SpectrumClass Properties { get; }
        [OneToMany(ItemDisplayName = "Info")]
        public Dictionary<string, FileSpectrumInfo> Files { get; }
    }
}
