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
using System;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class RegressionWeighting
    {
        private readonly Func<String> _getLabelFunc;
        private readonly GetWeightingFunc _getWeightingFunc;

        public static readonly RegressionWeighting NONE = new RegressionWeighting("none", // Not L10N
            () => QuantificationStrings.RegressionWeighting_NONE, (x, y) => 1);
        public static readonly RegressionWeighting ONE_OVER_X = new RegressionWeighting("1/x", // Not L10N
            ()=>QuantificationStrings.RegressionWeighting_ONE_OVER_X, (x, y)=>1/x);
        public static readonly RegressionWeighting ONE_OVER_X_SQUARED = new RegressionWeighting("1/(x*x)", // Not L10N
            ()=> QuantificationStrings.RegressionWeighting_ONE_OVER_X_SQUARED, (x, y)=>1/(x * x));

        public static readonly ImmutableList<RegressionWeighting> All =
            ImmutableList<RegressionWeighting>.ValueOf(new[] {NONE, ONE_OVER_X, ONE_OVER_X_SQUARED});
        
        public RegressionWeighting(string name, Func<String> getLabelFunc, GetWeightingFunc getWeightingFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
            _getWeightingFunc = getWeightingFunc;
        }

        public String Name { get; private set; }

        public override string ToString()
        {
            return _getLabelFunc();
        }

        public double GetWeighting(double x, double y)
        {
            return _getWeightingFunc(x, y);
        }

        public delegate double GetWeightingFunc(double x, double y);

        public static RegressionWeighting Parse(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return NONE;
            }
            return All.FirstOrDefault(w => w.Name == name) ?? NONE;
        }
    }
}
