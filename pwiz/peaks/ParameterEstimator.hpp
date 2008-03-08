//
// ParameterEstimator.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _PARAMETERESTIMATOR_HPP_
#define _PARAMETERESTIMATOR_HPP_


#include "ParametrizedFunction.hpp"
#include <memory>
#include <vector>
#include <iosfwd>


namespace pwiz {
namespace peaks {


class ParameterEstimator
{
    public:

    typedef ParametrizedFunction< std::complex<double> > Function;
    typedef data::SampleDatum<double, std::complex<double> > Datum;
    typedef std::vector<Datum> Data;
    typedef ublas::vector<double> Parameters;

    // instantiation
    static std::auto_ptr<ParameterEstimator> create(const Function& function,
                                                        const Data& data,
                                                        const Parameters& initialEstimate);
    virtual ~ParameterEstimator(){}

    // get/set current parameter estimate
    virtual const Parameters& estimate() const = 0;
    virtual void estimate(const Parameters& p) = 0;

    // return error, based on current parameter estimate
    virtual double error() const = 0;

    // update current parameters via Newton iteration, returns change in error, 
    // with optional output to log 
    virtual double iterate(std::ostream* log = 0) = 0;
};


} // namespace peaks
} // namespace pwiz


#endif // _PARAMETERESTIMATOR_HPP_

