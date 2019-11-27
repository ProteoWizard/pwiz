/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Prosit
{
    /// <summary>
    /// Prosit based retention time scoring. Modeled after
    /// SSRCalc3's implementation of the interface. The score is
    /// simply the predicted iRT from Prosit.
    /// </summary>
    public class PrositRetentionScoreCalculator : IRetentionScoreCalculator
    {
        public PrositRetentionScoreCalculator(string name)
        {
            Name = name;
            ScoreProvider = new RetentionScoreProvider();
        }

        public string Name { get; }

        public double? ScoreSequence(Target modifiedSequence)
        {
            try
            {
                return ScoreProvider.GetScore(modifiedSequence);
            }
            catch(PrositException)
            {
            }

            return null;
        }

        public double UnknownScore => 0;

        public IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount)
        {
            minCount = 0;
            return peptides;
        }

        public IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides)
        {
            return new Target[] { };
        }

        public RetentionScoreProvider ScoreProvider { get; private set; }
    }
}
