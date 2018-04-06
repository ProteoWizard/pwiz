//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _PEAKFAMILYDETECTORFT_HPP_
#define _PEAKFAMILYDETECTORFT_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "PeakFamilyDetector.hpp"
#include "pwiz/data/misc/PeakData.hpp"


namespace pwiz {
namespace analysis {


///
/// FT-specific implementation of PeakFamilyDetector 
/// 
class PWIZ_API_DECL PeakFamilyDetectorFT : public PeakFamilyDetector
{
    public:

    struct PWIZ_API_DECL Config
    {
        std::ostream* log;
        data::CalibrationParameters cp;
        Config() : log(0) {}
    };

    PeakFamilyDetectorFT(const Config& config);

    /// find peak families in a specified array of MZIntensityPair 
    virtual void detect(const MZIntensityPair* begin,
                        const MZIntensityPair* end,
                        std::vector<PeakFamily>& result);

    /// FT-specific exception 
    struct NoDataException : public std::runtime_error 
    {
        NoDataException() : std::runtime_error("[PeakFamilyDetectorFT::NoDataException]") {}
    };

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    PeakFamilyDetectorFT(PeakFamilyDetectorFT&);
    PeakFamilyDetectorFT& operator=(PeakFamilyDetectorFT&);
};


} // namespace analysis 
} // namespace pwiz


#endif // _PEAKFAMILYDETECTORFT_HPP_

