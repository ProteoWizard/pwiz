/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results.Scoring
{
    /// <summary>
    /// The calculated features for a single peak and a composite score.
    /// </summary>
    public struct ScoredPeak
    {
        public static ScoredPeak Empty = new ScoredPeak(null);

        public float[] Features { get; private set; }
        public double Score { get; set; }

        /// <summary>
        /// Construct a peak with the given feature values.  By default, the
        /// initial score is the simply the value of the first feature in
        /// the array.
        /// </summary>
        /// <param name="features">Array of feature values.</param>
        /// <param name="score">Explicit score value</param>
        public ScoredPeak(float[] features, double? score = null) : this()
        {
            Features = features;
            if (features != null)
                Score = score ?? Features[0];    // Use the first feature as the initial score.
        }

        public bool IsEmpty { get { return Features == null; } }
        // for debugging...
        public override string ToString()
        {
            return string.Format(@"{0:0.00}", Score);
        }

        public ScoredPeak CalcScore(IList<double> weights, bool replaceUnknownFeatureScores)
        {
            IList<float> features = Features;
            if (replaceUnknownFeatureScores)
            {
                features = LinearModelParams.ReplaceUnknownFeatureScores(features);
            }
            return new ScoredPeak(Features, LinearModelParams.Score(features, weights, 0));
        }
    }
}
