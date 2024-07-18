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
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public abstract class AlignmentFunction
    {
        public static readonly AlignmentFunction IDENTITY = new Compound(ImmutableList.Empty<AlignmentFunction>());
        /// <summary>
        /// Returns a new AlignmentFunction using the specified forward and reverse mappings
        /// </summary>
        /// <param name="forward">Function to map from X to Y</param>
        /// <param name="reverse">Function to map from Y to X</param>
        /// <returns></returns>
        public static AlignmentFunction Define(Func<double, double> forward, Func<double, double> reverse)
        {
            return new Impl(forward, reverse);
        }

        /// <summary>
        /// Returns an AlignmentFunction composed of successively applying the mappings in the parts
        /// </summary>
        public static AlignmentFunction FromParts(IEnumerable<AlignmentFunction> parts)
        {
            var partsList = ImmutableList.ValueOf(parts);
            if (partsList.Count == 0)
            {
                return IDENTITY;
            }
            if (partsList.Count == 1)
            {
                return partsList[0];
            }

            return new Compound(partsList);
        }

        public abstract double GetY(double x);
        public abstract double GetX(double y);

        private class Impl : AlignmentFunction
        {
            private Func<double, double> _forward;
            private Func<double, double> _reverse;
            public Impl(Func<double, double> forward, Func<double, double> reverse)
            {
                _forward = forward;
                _reverse = reverse;
            }

            public override double GetY(double x)
            {
                return _forward(x);
            }

            public override double GetX(double y)
            {
                return _reverse(y);
            }
        }

        public class Compound : AlignmentFunction
        {
            public Compound(IEnumerable<AlignmentFunction> alignmentFunctions)
            {
                Parts = ImmutableList.ValueOf(alignmentFunctions.SelectMany(part=>(part as Compound)?.Parts ?? ImmutableList.Singleton(part)));
            }
            public ImmutableList<AlignmentFunction> Parts { get; }
            public override double GetY(double x)
            {
                return Parts.Aggregate(x, (v, part) => part.GetY(v));
            }

            public override double GetX(double y)
            {
                return Parts.Reverse().Aggregate(y, (v, part) => part.GetX(v));
            }
        }
    }
}
