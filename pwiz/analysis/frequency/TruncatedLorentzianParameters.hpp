//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#ifndef _TRUNCATEDLORENTZIANPARAMETERS_HPP_
#define _TRUNCATEDLORENTZIANPARAMETERS_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TruncatedLorentzian.hpp"


namespace pwiz {
namespace frequency {


/// struct for holding parameters for a Truncated Lorentzian function
struct PWIZ_API_DECL TruncatedLorentzianParameters
{
    double T;
    double tau;
    std::complex<double> alpha;
    double f0;
    
    TruncatedLorentzianParameters();
    TruncatedLorentzianParameters(const TruncatedLorentzianParameters& that);
    TruncatedLorentzianParameters(const std::string& filename);

    /// write out to file 
    void write(const std::string& filename) const;

    /// write samples to stream
    void writeSamples(std::ostream& os) const;

    /// write samples to stream
    void writeSamples(std::ostream& os, 
                      double frequencyStart, 
                      double frequencyStep, 
                      int sampleCount) const;

    /// returns parameters in format usable by TruncatedLorentzian class
    ublas::vector<double> parameters(double shift=0, std::complex<double> scale=1) const;

    /// reads in parameters from TruncatedLorentzian format
    void parameters(const ublas::vector<double>& value, double shift=0, std::complex<double> scale=1);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const TruncatedLorentzianParameters& tlp);
PWIZ_API_DECL bool operator==(const TruncatedLorentzianParameters& t, const TruncatedLorentzianParameters& u);
PWIZ_API_DECL bool operator!=(const TruncatedLorentzianParameters& t, const TruncatedLorentzianParameters& u);


} // namespace frequency
} // namespace pwiz


#endif // _TRUNCATEDLORENTZIANPARAMETERS_HPP_ 

