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
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class RegressionWeighting : LabeledValues<string>
    {
        private readonly GetWeightingFunc _getWeightingFunc;

        public static readonly RegressionWeighting NONE = new RegressionWeighting(@"none",
            () => QuantificationStrings.RegressionWeighting_NONE, null, (x, y) => 1);
        public static readonly RegressionWeighting ONE_OVER_X = new RegressionWeighting(@"1/x",
            ()=>QuantificationStrings.RegressionWeighting_ONE_OVER_X, () => @"1_over_x", (x, y)=>1/x);
        public static readonly RegressionWeighting ONE_OVER_X_SQUARED = new RegressionWeighting(@"1/(x*x)",
            ()=> QuantificationStrings.RegressionWeighting_ONE_OVER_X_SQUARED, () => @"1_over_x_squared", (x, y)=>1/(x * x));

        public static readonly ImmutableList<RegressionWeighting> All =
            ImmutableList<RegressionWeighting>.ValueOf(new[] {NONE, ONE_OVER_X, ONE_OVER_X_SQUARED});
        
        public RegressionWeighting(string name, Func<String> getLabelFunc, Func<string> getInvariantNameFunc, GetWeightingFunc getWeightingFunc) : base(name, getLabelFunc, getInvariantNameFunc)
        {
            _getWeightingFunc = getWeightingFunc;
        }

        public override string ToString()
        {
            return Label;
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
