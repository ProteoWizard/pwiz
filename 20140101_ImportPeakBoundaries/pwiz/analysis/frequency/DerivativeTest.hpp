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


#ifndef _DERIVATIVETEST_HPP_
#define _DERIVATIVETEST_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ParametrizedFunction.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iomanip>
#include <exception>
#include <stdexcept>


namespace pwiz {
namespace frequency {
namespace DerivativeTest {


using namespace pwiz::util;


// VectorFunction is a generic interface used for numeric derivative calculations.
// Clients implement the pure virtual functions, and the calculations are handled automatically


template<typename value_type>
class VectorFunction
{
    public:

    virtual unsigned int argumentCount() const = 0;
    virtual unsigned int valueCount() const = 0;
    virtual ublas::vector<value_type> operator()(ublas::vector<double> x) const = 0;

    ublas::matrix<value_type> differenceQuotient(ublas::vector<double> x, double delta) const;
    void printDifferenceQuotientSequence(ublas::vector<double> x,
                                         std::ostream& os) const;

    protected:
    virtual ~VectorFunction(){}
};


template<typename value_type>
ublas::matrix<value_type> VectorFunction<value_type>::differenceQuotient(ublas::vector<double> x, double delta) const
{
    ublas::matrix<value_type> result(argumentCount(), valueCount());
    result.clear();
    for (unsigned int i=0; i<argumentCount(); i++)
    {
        ublas::vector<double> x2(x);
        x2(i) += delta;
        row(result, i) = ((*this)(x2)-(*this)(x))/delta;
    }
    return result;
}


template<typename value_type>
void VectorFunction<value_type>::printDifferenceQuotientSequence(ublas::vector<double> x,
                                                                 std::ostream& os) const
{
	using namespace std;

    for (double delta=.1; delta>1e-9; delta/=10)
    {
        os << scientific << setprecision(1) << "[delta: " << delta << "] ";
        os.unsetf(std::ios::scientific);
        os << setprecision(8) << differenceQuotient(x, delta) << endl;
    }
}


// Adaptors from ParametrizedFunction and its derivative, error, and error-derivative functions to
// implement the VectorFunction interface.


template<typename value_type>
class ParametrizedFunctionSlice : public VectorFunction<value_type>
{
    public:
    ParametrizedFunctionSlice(const ParametrizedFunction<value_type>& f, double x)
    :   f_(f), x_(x)
    {}

    virtual unsigned int argumentCount() const {return f_.parameterCount();}
    virtual unsigned int valueCount() const {return 1;}

    virtual ublas::vector<value_type> operator()(ublas::vector<double> p) const
    {
        ublas::vector<value_type> result(1);
        result(0) = f_(x_, p);
        return result;
    }

    private:
    const ParametrizedFunction<value_type>& f_;
    double x_;
};


template<typename value_type>
class ParametrizedDerivativeSlice : public VectorFunction<value_type>
{
    public:
    ParametrizedDerivativeSlice(const ParametrizedFunction<value_type>& f, double x)
    :   f_(f), x_(x)
    {}

    virtual unsigned int argumentCount() const {return f_.parameterCount();}
    virtual unsigned int valueCount() const {return f_.parameterCount();}

    virtual ublas::vector<value_type> operator()(ublas::vector<double> p) const
    {
        return f_.dp(x_,p);
    }

    private:
    const ParametrizedFunction<value_type>& f_;
    double x_;
};


template<typename value_type>
class AdaptedErrorFunction : public VectorFunction<double> // double: error functions are real-valued
{
    public:
    AdaptedErrorFunction(const typename ParametrizedFunction<value_type>::ErrorFunction& e)
    :   e_(e)
    {}

    virtual unsigned int argumentCount() const {return e_.parameterCount();}
    virtual unsigned int valueCount() const {return 1;}

    virtual ublas::vector<double> operator()(ublas::vector<double> p) const
    {
        ublas::vector<double> result(1);
        result(0) = e_(p);
        return result;
    }

    private:
    const typename ParametrizedFunction<value_type>::ErrorFunction& e_;
};


template<typename value_type>
class AdaptedErrorDerivative : public VectorFunction<double>
{
    public:
    AdaptedErrorDerivative(const typename ParametrizedFunction<value_type>::ErrorFunction& e)
    :   e_(e)
    {}

    virtual unsigned int argumentCount() const {return e_.parameterCount();}
    virtual unsigned int valueCount() const {return e_.parameterCount();}

    virtual ublas::vector<double> operator()(ublas::vector<double> p) const
    {
        return e_.dp(p);
    }

    private:
    const typename ParametrizedFunction<value_type>::ErrorFunction& e_;
};


// Numeric derivative calculations for ParametrizedFunction and associated ErrorFunction.


template<typename value_type>
void testDerivatives(const ParametrizedFunction<value_type>& f,
                     double x,
                     const ublas::vector<double>& p,
                     std::ostream* os = 0,
                     double delta = 1e-7,
                     double epsilon = 1e-4)
{
	using namespace std;

    if (os)
    {
        *os << "x: " << x << endl;
        *os << "p: " << p << endl;
    }

    if (os) *os << "f.dp: " << f.dp(x,p) << endl;
    ParametrizedFunctionSlice<value_type> slice(f,x);
    if (os) slice.printDifferenceQuotientSequence(p, *os);

    ublas::matrix<value_type> dp(f.dp(x,p).size(),1);
    column(dp,0) = f.dp(x,p);
    unit_assert_matrices_equal(dp, slice.differenceQuotient(p,delta), epsilon);

    if (os) *os << "f.dp2: " << f.dp2(x,p) << endl;
    ParametrizedDerivativeSlice<value_type> derivativeSlice(f,x);
    if (os) derivativeSlice.printDifferenceQuotientSequence(p, *os);

    unit_assert_matrices_equal(f.dp2(x,p), derivativeSlice.differenceQuotient(p,delta), epsilon);
}


template<typename value_type>
void testDerivatives(const typename ParametrizedFunction<value_type>::ErrorFunction& e,
                     const ublas::vector<double>& p,
                     std::ostream* os = 0,
                     double delta = 1e-7,
                     double epsilon = 1e-4)
{
	using namespace std;

    if (os) *os << "p: " << p << endl;

    if (os) *os << "e.dp: " << e.dp(p) << endl;
    AdaptedErrorFunction<value_type> adapted(e);
    if (os) adapted.printDifferenceQuotientSequence(p, *os);

    ublas::matrix<value_type> dp(e.dp(p).size(), 1);
    column(dp,0) = e.dp(p);
    unit_assert_matrices_equal(dp, adapted.differenceQuotient(p,delta), epsilon);

    if (os) *os << "e.dp2: " << e.dp2(p) << endl;
    AdaptedErrorDerivative<value_type> adaptedDerivative(e);
    if (os) adaptedDerivative.printDifferenceQuotientSequence(p, *os);

    unit_assert_matrices_equal(e.dp2(p), adaptedDerivative.differenceQuotient(p,delta), epsilon);
}


} // namespace DerivativeTest
} // namespace frequency
} // namespace pwiz


#endif // _DERIVATIVETEST_HPP_

