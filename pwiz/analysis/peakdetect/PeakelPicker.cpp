//
// PeakelPicker.cpp
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
#include "PeakelPicker.hpp"
#include <stdexcept>


namespace pwiz {
namespace analysis {


using namespace pwiz::data::peakdata;
using namespace std;


namespace {

FeaturePtr findFeature(const PeakelPtr& peakel, const PeakelField& peakelField)
{
    FeaturePtr result;
    return result;
}


PeakelField::iterator removePeakels(PeakelField::iterator it, const Feature& feature, PeakelField& peakelField)
{
    if (feature.peakels.empty())
        throw runtime_error("[PeakelPicker::removePeakels()] Empty feature.");

    return ++it;
}


PeakelField::iterator process(PeakelField::iterator it, PeakelField& peakelField, FeatureField& features)
{
    //cout << "process(): " << **it << endl;

    FeaturePtr feature = findFeature(*it, peakelField);

    if (feature.get())
    {
        features.insert(feature);
        return removePeakels(it, *feature, peakelField); // returns next valid iterator
    }
    else
    {
        return ++it;
    }
}
    

} // namespace


void PeakelPicker_Basic::pick(PeakelField& peakels, FeatureField& features) const
{
    PeakelField::iterator it = peakels.begin();
    PeakelField::iterator end = peakels.end();
   
    while (it != end)
        it = process(it, peakels, features);
}


} // namespace analysis
} // namespace pwiz

