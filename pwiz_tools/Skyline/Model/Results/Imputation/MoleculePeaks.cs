/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class MoleculePeaks : Immutable
    {
        public MoleculePeaks(IdentityPath identityPath, IEnumerable<RatedPeak> peaks)
        {
            PeptideIdentityPath = identityPath;
            Peaks = ImmutableList.ValueOf(peaks);
        }

        public IdentityPath PeptideIdentityPath { get; }
        public ImmutableList<RatedPeak> Peaks { get; private set; }

        public MoleculePeaks ChangePeaks(IEnumerable<RatedPeak> peaks, RatedPeak bestPeak,
            FormattablePeakBounds exemplaryPeakBounds)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.Peaks = ImmutableList.ValueOf(peaks);
                im.BestPeak = bestPeak;
                im.ExemplaryPeakBounds = exemplaryPeakBounds;
            });
        }
        public RatedPeak BestPeak { get; private set; }
        public double? AlignmentStandardTime { get; private set; }

        public MoleculePeaks ChangeAlignmentStandardTime(double? value)
        {
            return ChangeProp(ImClone(this), im => im.AlignmentStandardTime = value);
        }
        
        public FormattablePeakBounds ExemplaryPeakBounds { get; private set; }
    }
}