/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.Linq;

namespace pwiz.ProteowizardWrapper
{
    /// <summary>
    /// Converts the collision energies of waters_connect CE optimization channels into the product m/z
    /// shifts Skyline uses to tell optimization steps apart. waters_connect methods acquire every CE step
    /// at the real product m/z, so the channels of one transition differ only in collision energy.
    /// Shifting their product m/z by one <see cref="OPTIMIZE_SHIFT_SIZE"/> per step, in CE order, lets them
    /// load exactly like data acquired with shifted product m/z. Data that was acquired with shifted product
    /// m/z already has a unique product m/z per channel and is left unchanged.
    /// </summary>
    public static class WatersConnectCeSteps
    {
        /// <summary>
        /// Product m/z shift per optimization step. Must equal ChromatogramInfo.OPTIMIZE_SHIFT_SIZE in Skyline,
        /// which this library cannot reference.
        /// </summary>
        public const double OPTIMIZE_SHIFT_SIZE = 0.01;

        /// <summary>
        /// Returns the product m/z shift for each channel that is part of a CE optimization series, keyed by
        /// <see cref="Channel.Index"/>. A series is two or more channels with the same polarity, precursor m/z
        /// and product m/z, and distinct positive collision energies. Channels outside a series get no entry.
        /// The center of a series gets no shift, matching how Skyline picks the center step.
        /// </summary>
        public static Dictionary<int, double> GetProductMzShifts(IEnumerable<Channel> channels)
        {
            var shifts = new Dictionary<int, double>();
            foreach (var group in channels.GroupBy(c => Tuple.Create(c.IsNegativePolarity, c.PrecursorMz, c.ProductMz)))
            {
                var steps = group.OrderBy(c => c.CollisionEnergy).ToList();
                if (!IsCeSeries(steps))
                    continue;
                int centerIndex = (steps.Count + 1) / 2 - 1;
                for (int i = 0; i < steps.Count; i++)
                    shifts[steps[i].Index] = (i - centerIndex) * OPTIMIZE_SHIFT_SIZE;
            }
            return shifts;
        }

        private static bool IsCeSeries(IList<Channel> stepsSortedByCe)
        {
            if (stepsSortedByCe.Count < 2 || stepsSortedByCe[0].CollisionEnergy <= 0)
                return false;
            for (int i = 1; i < stepsSortedByCe.Count; i++)
            {
                if (stepsSortedByCe[i].CollisionEnergy == stepsSortedByCe[i - 1].CollisionEnergy)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// The metadata of one SRM chromatogram needed to recognize a CE optimization series.
        /// </summary>
        public class Channel
        {
            public Channel(int index, bool? isNegativePolarity, double precursorMz, double productMz, double collisionEnergy)
            {
                Index = index;
                IsNegativePolarity = isNegativePolarity;
                PrecursorMz = precursorMz;
                ProductMz = productMz;
                CollisionEnergy = collisionEnergy;
            }

            public int Index { get; }
            public bool? IsNegativePolarity { get; }
            public double PrecursorMz { get; }
            public double ProductMz { get; }
            public double CollisionEnergy { get; }
        }
    }
}
