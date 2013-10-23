/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    [XmlRoot("legacy_peak_scoring_model")] // Not L10N
    public class LegacyScoringModel : PeakScoringModelSpec
    {
        public static readonly string DEFAULT_NAME = Resources.LegacyScoringModel__defaultName_Skyline_Legacy;

        // Weighting coefficients.
        private const double W0 = 1.0;  // Log unforced area
        private const double W1 = 1.0;  // Unforced count score
        private const double W2 = LegacyLogUnforcedAreaCalc.STANDARD_MULTIPLIER;    // Unforced count score standard
        private const double W3 = 20.0; // Identified count

        public static double Score(double logUnforcedArea,
                                   double unforcedCountScore,
                                   double unforcedCountScoreStandard,
                                   double identifiedCount)
        {
            return 
                W0*logUnforcedArea + 
                W1*unforcedCountScore + 
                W2*unforcedCountScoreStandard + 
                W3*identifiedCount;
        }

        private readonly ReadOnlyCollection<IPeakFeatureCalculator> _calculators;

        public LegacyScoringModel()
            : this(DEFAULT_NAME)
        {
        }

        public LegacyScoringModel(string name, bool usesDecoys = true, bool usesSecondBest = false) : base(name)
        {
            _calculators = new ReadOnlyCollection<IPeakFeatureCalculator>(new List<IPeakFeatureCalculator>
                {
                    new LegacyLogUnforcedAreaCalc(),
                    new LegacyUnforcedCountScoreCalc(),
                    new LegacyUnforcedCountScoreStandardCalc(),
                    new LegacyIdentifiedCountCalc()
                });

            UsesDecoys = usesDecoys;
            UsesSecondBest = usesSecondBest;
        }

        public override IList<IPeakFeatureCalculator> PeakFeatureCalculators
        {
            get { return _calculators; }
        }

        public override IPeakScoringModel Train(IList<IList<double[]>> targets, IList<IList<double[]>> decoys, LinearModelParams initParameters, bool includeSecondBest = false)
        {
            return ChangeProp(ImClone(this), im =>
                {
                    ScoredGroupPeaksSet decoyTransitionGroups = new ScoredGroupPeaksSet(decoys);
                    ScoredGroupPeaksSet targetTransitionGroups = new ScoredGroupPeaksSet(targets);
                    if (includeSecondBest)
                    {
                        ScoredGroupPeaksSet secondBestTransitionGroups;
                        targetTransitionGroups.SelectTargetsAndDecoys(out targetTransitionGroups, out secondBestTransitionGroups);
                        foreach (var secondBestGroup in secondBestTransitionGroups.ScoredGroupPeaksList)
                        {
                            decoyTransitionGroups.Add(secondBestGroup);
                        }
                    }

                    var parameters = new LinearModelParams(new[] {W0, W1, W2, W3});
                    decoyTransitionGroups.ScorePeaks(parameters.Weights);
                    parameters.RescaleParameters(decoyTransitionGroups.Mean, decoyTransitionGroups.Stdev);
                });
        }

        public static LegacyScoringModel Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new LegacyScoringModel());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Consume the tag.  There is nothing else to do, because there is only
            // a single type of this object that can be instantiated, and its name
            // is known.  Do not call the base class ReadXml() method, as that will
            // attempt to overwrite the name and fail.
            reader.Read();
        }
    }
}
