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


#ifndef _PEAKINFO_HPP_
#define _PEAKINFO_HPP_


#include "data/FrequencyData.hpp"
#include <iostream>
#include <complex>


namespace pwiz {
namespace peaks {


/// Data structure for peak information. 
/// PeakInfo is intended to hold only peak information that is independent of the
/// methods used to process the peak data, e.g. parameters specific to a particular
/// model.

struct PeakInfo 
{
    double frequency;
    std::complex<double> intensity;
    int charge;

    PeakInfo(double f=0, std::complex<double> i=std::complex<double>(0), int c=0);
    PeakInfo(const pwiz::data::FrequencyDatum& datum, int c);
};


bool operator==(const PeakInfo& a, const PeakInfo& b);
bool operator!=(const PeakInfo& a, const PeakInfo& b);
std::ostream& operator<<(std::ostream& os, const PeakInfo& pi);
std::istream& operator>>(std::istream& is, PeakInfo& pi);


} // namespace peaks
} // namespace pwiz


#endif // _PEAKINFO_HPP_


