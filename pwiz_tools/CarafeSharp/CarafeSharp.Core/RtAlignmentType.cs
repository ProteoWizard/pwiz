/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// <c>-rt_align</c> (CarafeSharp only): how a training run puts its runs' retention times on one scale.
    /// <see cref="none"/> divides every run by one rt_max, as Carafe does; <see cref="kde"/>, CarafeSharp's default, maps
    /// each run's minutes onto the pretrained Chronologer's hydrophobic index (<see cref="RtAlignment"/>) with Chronologer's
    /// KDE ridge alignment, or for a run with too few peptides for it a LOESS or a line (<see cref="RtMapFit"/>), which
    /// removes the runs' drift and lets runs on different gradients train together.
    /// </summary>
    public enum RtAlignmentType
    {
        none,
        kde,
    }

    /// <summary>
    /// <c>-rt_select</c> (CarafeSharp only): which observation of a peptide form found in several runs becomes its RT
    /// training row once the runs are aligned. <see cref="best"/> takes its best-scoring precursor's, as Carafe does;
    /// <see cref="median"/> the median over the runs of each run's best.
    /// </summary>
    public enum RtSelectionType
    {
        best,
        median,
    }
}
