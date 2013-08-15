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
using System.Linq;
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

        public static double Score(double logUnforcedArea,
                                   double unforcedCountScore,
                                   double unforcedCountScoreStandard,
                                   double identifiedCount)
        {
            return logUnforcedArea + unforcedCountScore + LegacyLogUnforcedAreaCalc.STANDARD_MULTIPLIER*unforcedCountScoreStandard + 20*identifiedCount;
        }

        private readonly ReadOnlyCollection<IPeakFeatureCalculator> _calculators;

        public LegacyScoringModel()
            : this(DEFAULT_NAME, double.NaN, double.NaN)
        {
        }

        public LegacyScoringModel(string name, double decoyMean, double decoyStdev) : base(name)
        {
            _calculators = new ReadOnlyCollection<IPeakFeatureCalculator>(new List<IPeakFeatureCalculator>
                {
                    new LegacyLogUnforcedAreaCalc(),
                    new LegacyUnforcedCountScoreCalc(),
                    new LegacyUnforcedCountScoreStandardCalc(),
                    new LegacyIdentifiedCountCalc()
                });

            Weights = new[]
                {
                    1.0, 
                    1.0, 
                    LegacyLogUnforcedAreaCalc.STANDARD_MULTIPLIER, 
                    20
                };
            DecoyMean = decoyMean;
            DecoyStdev = decoyStdev;
        }

        private enum FeatureOrder
        {
            log_unforced_area,
            unforced_count_score,
            unforced_count_score_standard,
            identified_count
        };

        public override IList<IPeakFeatureCalculator> PeakFeatureCalculators
        {
            get { return _calculators; }
        }

        public override IPeakScoringModel Train(IList<IList<double[]>> targets, IList<IList<double[]>> decoys)
        {
            return ChangeProp(ImClone(this), im =>
                {
                    ScoredGroupPeaksSet decoyTransitionGroups;
                    if (decoys.FirstOrDefault() == null)
                    {
                        var allTransitionGroups = new ScoredGroupPeaksSet(targets);
                        ScoredGroupPeaksSet targetTransitionGroups;
                        allTransitionGroups.SelectTargetsAndDecoys(out targetTransitionGroups, out decoyTransitionGroups);
                    }
                    else
                    {
                        decoyTransitionGroups = new ScoredGroupPeaksSet(decoys);
                    }

                    decoyTransitionGroups.ScorePeaks(Weights);

                    im.Weights = Weights;
                    im.DecoyMean = decoyTransitionGroups.Mean;
                    im.DecoyStdev = decoyTransitionGroups.Stdev;
                });
        }

        public override double Score(double[] features)
        {
            return Score(features[(int) FeatureOrder.log_unforced_area],
                         features[(int) FeatureOrder.unforced_count_score],
                         features[(int) FeatureOrder.unforced_count_score_standard],
                         features[(int) FeatureOrder.identified_count]);
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
