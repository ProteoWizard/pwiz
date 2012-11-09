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
using System.Xml.Schema;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class LegacyScoringModel : Immutable, IPeakScoringModel
    {
        private static readonly double LOG10 = Math.Log(10.0);

        public static double Score(double logUnforcedArea,
                                   double unforcedCountScore,
                                   double unforcedCountScoreStandard,
                                   double identifiedCount)
        {
            return logUnforcedArea + LOG10*unforcedCountScore + LegacyLogUnforcedAreaCalc.STANDARD_MULTIPLIER*LOG10*unforcedCountScoreStandard + 1000*identifiedCount;
        }

        private ReadOnlyCollection<Type> _calculators;

        public LegacyScoringModel()
        {
            PeakFeatureCalculators = new[]
                             {
                                 typeof(LegacyLogUnforcedAreaCalc),
                                 typeof(LegacyUnforcedCountScoreCalc),
                                 typeof(LegacyUnforcedCountScoreStandardCalc),
                                 typeof(LegacyIdentifiedCountStandardCalc)
                             };
        }

        public string Name
        {
            get { return "Skyline Legacy"; }
        }

        private enum FeatureOrder
        {
            log_unforced_area,
            unforced_count_score,
            unforced_count_score_standard,
            identified_count
        };

        public IList<Type> PeakFeatureCalculators
        {
            get { return _calculators; }
            private set { _calculators = MakeReadOnly(value); }
        }
        
        public IPeakScoringModel Train(IList<IList<double[]>> targets, IList<IList<double[]>> decoys)
        {
            // No training needed, since legacy scoring was fixed
            return this;
        }

        public double Score(double[] features)
        {
            return Score(features[(int) FeatureOrder.log_unforced_area],
                         features[(int) FeatureOrder.unforced_count_score],
                         features[(int) FeatureOrder.unforced_count_score_standard],
                         features[(int) FeatureOrder.identified_count]);
        }

        #region IXmlSerializable

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            throw new NotImplementedException();
        }

        public void WriteXml(XmlWriter writer)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
