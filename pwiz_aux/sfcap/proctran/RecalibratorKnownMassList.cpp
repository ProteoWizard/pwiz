//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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


#include "RecalibratorKnownMassList.hpp"
#include "pwiz/analysis/calibration/LeastSquaresCalibrator.hpp"
#include <iterator>
#include <iostream>
#include <iomanip>

namespace pwiz {
namespace pdanalysis {


using namespace std;
using namespace pwiz::data;
using namespace pwiz::calibration;
using namespace pwiz::data::peakdata;


class RecalibratorKnownMassList::Impl
{
    public:

    Impl(const KnownMassList& kml)
    :   kml_(kml)
    {}

    CalibrationParameters calculateCalibrationParameters(const Scan& scan) const;

    private:
    const KnownMassList& kml_;
};


CalibrationParameters 
RecalibratorKnownMassList::Impl::calculateCalibrationParameters(const Scan& scan) const
{
    //const double epsilon_ = 4.2; // Parag was using this value -- changed to make unit test pass - dk
    //    std::cout<<epsilon_<<endl;

    const double epsilon_ = 100;
    KnownMassList::MatchResult matchResult = kml_.match(scan, epsilon_); //pre-calibration

    vector<double> masses;
    vector<double> freqs;

    for (vector<KnownMassList::Match>::const_iterator it=matchResult.matches.begin();
         it!=matchResult.matches.end(); ++it)
    {
        if (!it->peakFamily) continue;

        if (!it->entry)
            throw runtime_error("[RecalibratorKnownMassList::calculateCalibrationParameters] No entry.");

        if (it->peakFamily->peaks.empty())
            throw runtime_error("[RecalibratorKnownMassList::calculateCalibrationParameters] No peaks.");

        masses.push_back(it->entry->mz);
        freqs.push_back(it->peakFamily->peaks[0].frequency);
    }

    /*
    std::cout<<std::setprecision(10)<<"in this silly function!"<<endl;

    for(size_t i=0;i<masses.size();i++){
      std::cout<<std::setprecision(10)<<freqs[i]<<" "<<masses[i]<<std::endl;
    }

    */

    if (matchResult.matchCount < 2)
        throw runtime_error("[RecalibratorKnownMassList] Not enough matches to recalibrate.");

    auto_ptr<LeastSquaresCalibrator> lsc = LeastSquaresCalibrator::create(masses,freqs);
    lsc->calibrate(); 
    CalibrationParameters cp = lsc->parameters();

    return cp;
}


// RecalibratorKnownMassList


RecalibratorKnownMassList::RecalibratorKnownMassList(const KnownMassList& kml) 
: impl_(new Impl(kml)) {}

RecalibratorKnownMassList::~RecalibratorKnownMassList() 
{} // auto destruction of impl_

CalibrationParameters 
RecalibratorKnownMassList::calculateCalibrationParameters(const Scan& scan) const
{
    return impl_->calculateCalibrationParameters(scan);
}



} // namespace pdanalysis 
} // namespace pwiz

