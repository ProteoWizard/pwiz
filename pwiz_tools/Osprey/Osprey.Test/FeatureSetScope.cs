/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using System;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Applies a resolution and <c>--feature-set</c> to the process-wide <see cref="PinFeatureMask"/>
    /// for the life of a using block, and restores what was applied before, so a test that resolves
    /// features under one search's settings cannot leave them for the next test.
    /// </summary>
    public sealed class FeatureSetScope : IDisposable
    {
        private readonly ResolutionMode _savedResolution;
        private readonly OspreyFeatureSet? _savedFeatureSet;

        public FeatureSetScope(ResolutionMode mode, OspreyFeatureSet? featureSet)
        {
            _savedResolution = PinFeatureMask.AppliedResolutionMode;
            _savedFeatureSet = PinFeatureMask.AppliedFeatureSet;
            PinFeatureMask.ApplySearchSettings(mode, featureSet);
        }

        public void Dispose()
        {
            PinFeatureMask.ApplySearchSettings(_savedResolution, _savedFeatureSet);
        }
    }
}
