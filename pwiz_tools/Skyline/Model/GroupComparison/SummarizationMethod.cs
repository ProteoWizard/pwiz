﻿/*
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
using System.Collections.Generic;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class SummarizationMethod : LabeledValues<string>
    {
        public static readonly SummarizationMethod REGRESSION 
            = new SummarizationMethod(@"regression",
                ()=>GroupComparisonStrings.SummarizationMethod_REGRESSION_Regression);
        public static readonly SummarizationMethod AVERAGING 
            = new SummarizationMethod(@"averaging",
                ()=>GroupComparisonStrings.SummarizationMethod_AVERAGING_Sum_of_transition_areas);
        public static readonly SummarizationMethod MEDIANPOLISH 
            = new SummarizationMethod(@"medianpolish",
                ()=>GroupComparisonStrings.SummarizationMethod_MEDIANPOLISH_Tukey_s_Median_Polish);

        public static readonly SummarizationMethod DEFAULT = AVERAGING;

        private SummarizationMethod(string name, Func<string> getLabelFunc) :
            base(name, getLabelFunc)
        {
        }

        public override string ToString()
        {
            return Label;
        }

        public static IList<SummarizationMethod> ListSummarizationMethods()
        {
            return new[]
            {
                AVERAGING, MEDIANPOLISH
            };
        }

        public static SummarizationMethod FromName(string name)
        {
            if (name == REGRESSION.Name)
            {
                return REGRESSION;
            }
            if (name == AVERAGING.Name)
            {
                return AVERAGING;
            }
            if (name == MEDIANPOLISH.Name)
            {
                return MEDIANPOLISH;
            }
            return DEFAULT;
        }
    }
}
