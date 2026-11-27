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

using System;
using pwiz.Quasar.Core.Elution;

namespace pwiz.Quasar.Core.Clustering
{
    /// <summary>
    /// The run-level inputs and the settings of the window clustering (spec Stages 2 and 3).
    /// The run-level ones (prior, noise, scale, apex tolerance) come from Stage 2a and 2b.
    /// </summary>
    public sealed class WindowClusterSettings
    {
        public const int DEFAULT_MIN_MEMBERS = 6;
        public const int DEFAULT_MIN_ANCHORS = 1;
        public const int DEFAULT_MAX_RESIDUAL_PASSES = 2;

        /// <summary>Scans added on each side of a template's 5% support for its fits, for the baseline.</summary>
        public const int DEFAULT_SEGMENT_PADDING = 2;

        /// <summary>Most times a group's template is refitted and its members rechecked.</summary>
        public const int DEFAULT_MAX_GROUPING_ROUNDS = 5;

        public WindowClusterSettings(EmgShapePrior prior, NoiseModel noise, double cycleTime)
        {
            Prior = prior ?? throw new ArgumentNullException(nameof(prior));
            Noise = noise ?? throw new ArgumentNullException(nameof(noise));
            if (!(cycleTime > 0.0))
                throw new ArgumentOutOfRangeException(nameof(cycleTime));
            CycleTime = cycleTime;

            // 0.5 x the run's median FWHM, and the CWT at the run's sigma, as the spec sets them.
            var typical = new EmgShape(0.0, prior.Sigma, prior.Tau);
            var (left, right) = typical.HeightCrossings(0.5);
            ApexTolerance = 0.5 * (right - left);
            CwtScaleScans = prior.Sigma / cycleTime;
        }

        /// <summary>The run's typical peak shape, for provisional templates and the template fits.</summary>
        public EmgShapePrior Prior { get; }

        public NoiseModel Noise { get; }

        /// <summary>The time between consecutive scans of one window, in the axis's time unit.</summary>
        public double CycleTime { get; }

        /// <summary>The largest apex difference at which an anchor component can join a template.</summary>
        public double ApexTolerance { get; set; }

        /// <summary>The Mexican hat scale, in scans, for finding anchor peaks (spec Stage 2c).</summary>
        public double CwtScaleScans { get; set; }

        public double MembershipThreshold { get; set; } = TemplateFit.DEFAULT_MEMBERSHIP_THRESHOLD;
        public int MinMembers { get; set; } = DEFAULT_MIN_MEMBERS;
        public int MinAnchors { get; set; } = DEFAULT_MIN_ANCHORS;
        public int MaxResidualPasses { get; set; } = DEFAULT_MAX_RESIDUAL_PASSES;
        public int SegmentPadding { get; set; } = DEFAULT_SEGMENT_PADDING;
        public int MaxGroupingRounds { get; set; } = DEFAULT_MAX_GROUPING_ROUNDS;
    }
}
