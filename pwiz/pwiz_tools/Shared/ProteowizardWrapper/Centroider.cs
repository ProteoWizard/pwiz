/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.ProteowizardWrapper
{
    public class Centroider
    {
        public Centroider(IList<double> mzs, IList<double> intensities)
        {
            Mzs = mzs;
            Intensities = intensities;
        }

        public IList<double> Mzs { get; private set; }
        public IList<double> Intensities { get; private set; }

        public void GetCentroidedData(out double[] centroidedMzs, out double[] centroidedIntensities)
        {
            var centroidedMzsList = new List<double>();
            var centroidedIntensitiesList = new List<double>();
            
            bool increasing = true;
            double currentMz = 0;
            double currentIntensity = 0;
            double lastIntensity = 0;
            for (int i = 0; i < Mzs.Count(); i++)
            {
                var intensity = Intensities[i];
                if (intensity < lastIntensity)
                {
                    increasing = false;
                }
                else
                {
                    if (!increasing)
                    {
                        if (currentIntensity > 0)
                        {
                            centroidedMzsList.Add(currentMz);
                            centroidedIntensitiesList.Add(currentIntensity);
                        }
                        currentIntensity = 0;
                    }
                    increasing = true;
                }
                var nextIntensity = currentIntensity + intensity;
                if (nextIntensity > 0)
                {
                    var mz = Mzs[i];
                    currentMz = (currentMz * currentIntensity + mz * intensity) / nextIntensity;
                    currentIntensity = nextIntensity;
                }
                lastIntensity = intensity;
            }
            if (currentIntensity > 0)
            {
                centroidedMzsList.Add(currentMz);
                centroidedIntensitiesList.Add(currentIntensity);
            }
            centroidedMzs = centroidedMzsList.ToArray();
            centroidedIntensities = centroidedIntensitiesList.ToArray();
        }
    }
}