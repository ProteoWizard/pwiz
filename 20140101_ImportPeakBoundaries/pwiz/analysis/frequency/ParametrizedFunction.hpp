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


#ifndef _PARAMETRIZEDFUNCTION_HPP_
#define _PARAMETRIZEDFUNCTION_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/misc/SampleDatum.hpp"


#include "boost/numeric/ublas/vector.hpp"
#include "boost/numeric/ublas/matrix.hpp"
#include "boost/numeric/ublas/io.hpp"
#include "boost/numeric/ublas/matrix_proxy.hpp"
namespace ublas = boost::numeric::ublas;


#include <complex>


namespace pwiz {
namespace frequency {


template<typename value_type>
class ParametrizedFunction
{
    public:
    virtual unsigned int parameterCount() const = 0;
    virtual value_type operator()(double x, const ublas::vector<double>& p) const = 0;
    virtual ublas::vector<value_type> dp(double x, const ublas::vector<double>& p) const = 0;
    virtual ublas::matrix<value_type> dp2(double x, const ublas::vector<double>& p) const = 0;
    virtual ~ParametrizedFunction(){}

    class ErrorFunction;
};


template<typename value_type>
class ParametrizedFunction<value_type>::ErrorFunction
{
    public:

    typedef data::SampleDatum<double, value_type> Datum;
    typedef std::vector<Datum> Data;

    ErrorFunction(const ParametrizedFunction<value_type>& f, const Data& data)
    :   f_(f), data_(data)
    {}

    int parameterCount() const {return f_.parameterCount();}

    double operator()(const ublas::vector<double>& p) const
    {
        double result = 0;
        for (typename Data::const_iterator it=data_.begin(); it!=data_.end(); ++it)
            result += norm(std::complex<double>(f_(it->x,p) - it->y));
        return result;
    }

    ublas::vector<double> dp(const ublas::vector<double>& p) const
    {
        ublas::vector<double> result(parameterCount());
        result.clear();
        for (typename Data::const_iterator it=data_.begin(); it!=data_.end(); ++it)
        {
            std::complex<double> diffconj = conj(std::complex<double>(f_(it->x,p) - it->y));
            result += 2 * real(diffconj*f_.dp(it->x,p));
        }
        return result;
    }

    ublas::matrix<double> dp2(const ublas::vector<double>& p) const
    {
        ublas::matrix<double> result(parameterCount(), parameterCount());
        result.clear();
        for (typename Data::const_iterator it=data_.begin(); it!=data_.end(); ++it)
        {
            std::complex<double> diffconj = conj(std::complex<double>(f_(it->x, p) - it->y));
            ublas::vector<value_type> dp = f_.dp(it->x,p);
            ublas::matrix<value_type> dp2 = f_.dp2(it->x,p);
            result += 2 * real(diffconj*dp2 + outer_prod(conj(dp),dp));
        }
        return result;
    }

    private:
    const ParametrizedFunction<value_type>& f_;
    const Data& data_;
};


} // namespace frequency
} // namespace pwiz


#endif // _PARAMETRIZEDFUNCTION_HPP_

