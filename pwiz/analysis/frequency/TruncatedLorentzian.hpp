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


#ifndef _TRUNCATEDLORENTZIAN_HPP_
#define _TRUNCATEDLORENTZIAN_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ParametrizedFunction.hpp"
#include <complex>
#include <memory>
#include "boost/shared_ptr.hpp"


namespace pwiz {
namespace frequency {


class PWIZ_API_DECL TruncatedLorentzian : public ParametrizedFunction< std::complex<double> >
{
    public:

    enum PWIZ_API_DECL ParameterIndex {AlphaR, AlphaI, Tau, F0};

    TruncatedLorentzian(double T); // cutoff value T
    ~TruncatedLorentzian();

    virtual unsigned int parameterCount() const {return 4;}
    virtual std::complex<double> operator()(double f, const ublas::vector<double>& p) const;
    virtual ublas::vector< std::complex<double> > dp(double f, const ublas::vector<double>& p) const;
    virtual ublas::matrix< std::complex<double> > dp2(double f, const ublas::vector<double>& p) const;

    void outputSamples(const std::string& filename, const ublas::vector<double>& p,
                       double shift = 0, double scale = 1) const;

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
};


} // namespace frequency
} // namespace pwiz


#endif // _TRUNCATEDLORENZIAN_HPP_

