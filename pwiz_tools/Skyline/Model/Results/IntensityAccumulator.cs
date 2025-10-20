/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results
{
    public class IntensityAccumulator
    {
        public double TotalIntensity { get; set; }
        public double MeanMassError { get; set; }
        bool _highAcc;
        ChromExtractor _extractor;
        private double _targetMz;


        public IntensityAccumulator(bool highAcc, ChromExtractor extractor, double targetMz)
        {
            _highAcc = highAcc;
            _extractor = extractor;
            _targetMz = targetMz;
        }

        public void AddPoint(double mz, double intensity)
        {
            if (_extractor == ChromExtractor.summed)
                TotalIntensity += intensity;
            else if (intensity > TotalIntensity)
            {
                TotalIntensity = intensity;
                MeanMassError = 0;
            }

            // Accumulate weighted mean mass error for summed, or take a single
            // mass error of the most intense peak for base peak.
            if (_highAcc && (_extractor == ChromExtractor.summed || MeanMassError == 0))
            {
                if (TotalIntensity > 0.0)
                {
                    double deltaPeak = mz - _targetMz;
                    MeanMassError += (deltaPeak - MeanMassError) * intensity / TotalIntensity;
                }
            }
        }
        public override string ToString() // Debug convenience, not user facing
        {
            return $@"mz:{_targetMz} i:{TotalIntensity} e:{MeanMassError:F6}";
        }
    }
}