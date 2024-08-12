/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public struct WeightedPoint
    {
        public WeightedPoint(double x, double y) : this(x, y, 1.0)
        {
        }

        public WeightedPoint(double x, double y, double weight) : this()
        {
            X = x;
            Y = y;
            Weight = weight;
        }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Weight { get; private set; }

        public override string ToString()
        {
            if (Weight == 1)
            {
                return string.Format(@"({0},{1})", X, Y);
            }

            return string.Format(@"({0},{1},{2})", X, Y, Weight);
        }
    }
}