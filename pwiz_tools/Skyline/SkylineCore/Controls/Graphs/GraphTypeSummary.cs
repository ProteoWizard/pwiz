/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Controls.Graphs
{
    // In SkylineCore because Settings persists lists of it (AreaGraphTypes, RTGraphTypes, ...);
    // the namespace is the one the graphs that use it have always had.
    [Flags]
    public enum GraphTypeSummary
    {
        invalid = 0,
        replicate = 1,
        peptide = 1 << 1,
        score_to_run_regression = 1 << 2,
        schedule = 1 << 3,
        run_to_run_regression = 1 << 4,
        histogram = 1 << 5,
        histogram2d = 1 << 6,
        detections = 1 << 7,
        detections_histogram = 1 << 8,
        abundance = 1 << 9,
        abundance_comparison = 1 << 10
    }
}
