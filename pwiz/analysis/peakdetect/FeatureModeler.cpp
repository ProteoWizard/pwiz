//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and 
// limitations under the License.
//
                                                                                                     

#define PWIZ_SOURCE
#include "FeatureModeler.hpp"
#include "MZRTField.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include <stdexcept>


namespace pwiz {
namespace analysis {


using namespace pwiz::data::peakdata;
using namespace pwiz::math;
using namespace std;


void FeatureModeler::fitFeatures(FeatureField& featureField) const
{
    throw runtime_error("[FeatureModeler::fitFeatures()] Not implemented.");
}


void FeatureModeler_Gaussian::fitFeature(Feature& feature) const
{
    cout << "we're here!\n";

    cout.precision(10);

    /*
    double sumIntensity = 0;
    double sumIntensityMZ = 0;
    */

    for (vector<PeakelPtr>::const_iterator peakel=feature.peakels.begin(); peakel!=feature.peakels.end(); ++peakel)
    {
        cout << "peakel " << peakel-feature.peakels.begin() << ":\n";

        double peakelSumIntensity = 0;
        double peakelSumIntensityMZ = 0;

        for (vector<Peak>::const_iterator peak=(*peakel)->peaks.begin(); peak!=(*peakel)->peaks.end(); ++peak)
        {
            cout << "  peak" << peak-(*peakel)->peaks.begin() << ":\n";
            cout << "    mz: " << peak->mz << " ";

            double peakSumIntensity = 0;
            double peakSumIntensityMZ = 0;

            for (vector<OrderedPair>::const_iterator it=peak->data.begin(); it!=peak->data.end(); ++it)
            {
                peakSumIntensity += it->y;
                peakSumIntensityMZ += (it->x * it->y);
            }
        
            double peakMeanMZ = peakSumIntensityMZ/peakSumIntensity;
            cout << peakMeanMZ << " (" << peakMeanMZ - peak->mz << ")\n";

            peakelSumIntensity += peakSumIntensity;
            peakelSumIntensityMZ += peakSumIntensityMZ;
        }

        double peakelMeanMZ = peakelSumIntensityMZ/peakelSumIntensity;

        cout << "  mz: " << (*peakel)->mz << " " << peakelMeanMZ << " ("<< peakelMeanMZ - (*peakel)->mz << ")\n";
    }
}


} // namespace analysis
} // namespace pwiz


