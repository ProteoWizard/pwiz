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

using System;
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
        private static readonly double LOG10 = Math.Log(10.0);

        public static double Score(double logUnforcedArea,
                                   double unforcedCountScore,
                                   double unforcedCountScoreStandard,
                                   double identifiedCount)
        {
            return logUnforcedArea + LOG10*unforcedCountScore + LegacyLogUnforcedAreaCalc.STANDARD_MULTIPLIER*LOG10*unforcedCountScoreStandard + 1000*identifiedCount;
        }

        private readonly ReadOnlyCollection<Type> _calculators;

        public LegacyScoringModel() : base(Resources.LegacyScoringModel_LegacyScoringModel_Skyline_Legacy)
        {
            _calculators = new ReadOnlyCollection<Type>(new[]
                             {
                                 typeof(LegacyLogUnforcedAreaCalc),
                                 typeof(LegacyUnforcedCountScoreCalc),
                                 typeof(LegacyUnforcedCountScoreStandardCalc),
                                 typeof(LegacyIdentifiedCountCalc)
                             });
        }

        private enum FeatureOrder
        {
            log_unforced_area,
            unforced_count_score,
            unforced_count_score_standard,
            identified_count
        };

        public override IList<Type> PeakFeatureCalculators
        {
            get { return _calculators; }
        }
        
        public override IPeakScoringModel Train(IList<IList<double[]>> targets, IList<IList<double[]>> decoys)
        {
            // No training needed, since legacy scoring was fixed
            return this;
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
