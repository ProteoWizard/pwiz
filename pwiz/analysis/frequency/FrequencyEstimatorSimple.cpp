//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#include "FrequencyEstimatorSimple.hpp"
#include "pwiz/utility/math/Parabola.hpp"
#include "MagnitudeLorentzian.hpp"
#include <iostream>
#include <stdexcept>
#include <algorithm>
#include <iterator>


#ifdef _MSC_VER // msvc hack
#define isnan(x) ((x) != (x))
#endif // _MSC_VER


using namespace std;
using namespace pwiz::math;
using namespace pwiz::frequency;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;


namespace pwiz {
namespace frequency {


class PWIZ_API_DECL FrequencyEstimatorSimpleImpl : public FrequencyEstimatorSimple
{
    public:

    FrequencyEstimatorSimpleImpl(Type type, unsigned int windowRadius);

    virtual Peak estimate(const FrequencyData& fd, 
                          const Peak& initialEstimate) const;

    private:
    Type type_;
    unsigned int windowRadius_;
};


PWIZ_API_DECL auto_ptr<FrequencyEstimatorSimple> FrequencyEstimatorSimple::create(Type type,
                                                                    unsigned int windowRadius)
{
    return auto_ptr<FrequencyEstimatorSimple>(
        new FrequencyEstimatorSimpleImpl(type, windowRadius));
}


FrequencyEstimatorSimpleImpl::FrequencyEstimatorSimpleImpl(Type type, unsigned int windowRadius)
:   type_(type), 
    windowRadius_(windowRadius)
{
    // TODO: do something with this 
    // if (type_ == Lorentzian) cout << "[FrequencyEstimatorSimple] Warning: Lorentzian frequency estimator does not work for unnormalized data!\n";
}


namespace {
pair<double,double> magnitudeSample(const FrequencyDatum& datum)
{
    return make_pair(datum.x, abs(datum.y));
}
} // namespace 


namespace {

bool isLocalMax(FrequencyData::const_iterator it)
{
    return norm(it->y) >= norm((it-1)->y) &&
           norm(it->y) >= norm((it+1)->y);
}

FrequencyData::const_iterator closestLocalMaximum(const FrequencyData& fd, double frequencyTarget)
{
    FrequencyData::const_iterator center = fd.findNearest(frequencyTarget);
    if (isLocalMax(center))
        return center;

    const int maxRadius = 10;
    for (int radius=1; radius<=maxRadius; radius++)
    {
        // "good" == valid iterator && local maximum
        bool leftGood = (fd.data().begin()+radius+1<=center && isLocalMax(center-radius));
        bool rightGood = (center+radius+1<fd.data().end() && isLocalMax(center+radius));

        if (leftGood)
        {
            FrequencyData::const_iterator left = center-radius;

            if (rightGood)
            {
                // if both are good, return the one closest to target
                FrequencyData::const_iterator right = center+radius;
                double leftDistance = abs(left->x - frequencyTarget);
                double rightDistance = abs(right->x - frequencyTarget);
                return (leftDistance < rightDistance) ? left : right;
            }
            
            return left;
        }

        if (rightGood)
        {
            FrequencyData::const_iterator right = center+radius;
            return right;
        }
    }


    // this shouldn't happen
    
    cerr << "Frequency target: " << frequencyTarget << endl;
    FrequencyData window(fd, center, 10);
    copy(window.data().begin(), window.data().end(), ostream_iterator<FrequencyDatum>(cerr, "\n"));
    cerr << endl;
    throw runtime_error("FrequencyEstimatorSimple::localMaximum()] Unable to find local maximum.");
}
} // namespace


PWIZ_API_DECL
Peak FrequencyEstimatorSimpleImpl::estimate(const FrequencyData& fd, 
                                            const Peak& initialEstimate) const
{
    Peak result;
    FrequencyData::const_iterator localMax = closestLocalMaximum(fd, initialEstimate.getAttribute(Peak::Attribute_Frequency));

    if (type_ == LocalMax)
    {
        result.attributes[Peak::Attribute_Frequency] = localMax->x;
        result.intensity = abs(localMax->y);
        return result;
    }

    if (fd.data().begin()+windowRadius_>localMax || localMax+windowRadius_>=fd.data().end())
    {
        cerr << endl << "Error processing peak: " << initialEstimate << endl;
        throw runtime_error("[FrequencyEstimatorSimple::estimatedPeak()] Insufficient window around data.");
    }

    vector< pair<double,double> > samples;
    transform(localMax-windowRadius_, localMax+windowRadius_+1,
              back_inserter(samples), magnitudeSample);

    if (type_ == Parabola)
    {
        math::Parabola p(samples);
        result.attributes[Peak::Attribute_Frequency] = p.center();
        result.intensity = p(p.center());
        return result;
    }
    else if (type_ == Lorentzian)
    {
        MagnitudeLorentzian ml(samples);
        double intensity = ml(ml.center());
        if (isnan(intensity)) intensity = 0;
        result.attributes[Peak::Attribute_Frequency] = ml.center();
        result.intensity = intensity;
        return result;
    }

    throw runtime_error("[FrequencyEstimatorSimple::estimatedPeak()] This isn't happening.");
}


} // namespace frequency
} // namespace pwiz

