/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class MissingScoreBehavior
    {
        public static MissingScoreBehavior FAIL = new MissingScoreBehavior(@"fail", () => ScoringResources.MissingScoreBehavior_FAIL_Fail);
        public static MissingScoreBehavior REPLACE = new MissingScoreBehavior(@"replace", () => ScoringResources.MissingScoreBehavior_REPLACE_Replace);
        public static MissingScoreBehavior SKIP = new MissingScoreBehavior(@"skip", () => ScoringResources.MissingScoreBehavior_SKIP_Skip);

        private MissingScoreBehavior(string name, Func<string> getLabelFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
        }

        private readonly Func<string> _getLabelFunc;
        public string Name { get; }
        public string Label
        {
            get { return _getLabelFunc(); }
        }
        public override string ToString()
        {
            return Label;
        }

        public static readonly ImmutableList<MissingScoreBehavior> ALL = ImmutableList.ValueOf(new []{FAIL, REPLACE, SKIP});

        public static MissingScoreBehavior FromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return FAIL;
            }

            return ALL.FirstOrDefault(x => x.Name == name) ?? FAIL;
        }
    }
}
