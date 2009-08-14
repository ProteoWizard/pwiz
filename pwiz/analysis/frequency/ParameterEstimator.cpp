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


#include "ParameterEstimator.hpp"
//#include "DerivativeTest.hpp" // for testing numerical derivatives only


#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/lu.hpp>
#include <boost/numeric/ublas/triangular.hpp>
#include <boost/numeric/ublas/vector_proxy.hpp>
#include <boost/numeric/ublas/io.hpp>
namespace ublas = boost::numeric::ublas;


namespace pwiz {
namespace frequency {


using namespace std;


class ParameterEstimatorImpl : public ParameterEstimator
{
    public:

    ParameterEstimatorImpl(const Function& function,
                               const Data& data,
                               const Parameters& initialEstimate);

    virtual const Parameters& estimate() const {return p_;}
    virtual void estimate(const Parameters& p) {p_ = p;}
    virtual double error() const {return e_(p_);}
    virtual double iterate(ostream* log);

    private:

    const Function& f_;
    const Data& data_;
    Parameters p_;
    Function::ErrorFunction e_;

    Parameters solve(const ublas::matrix<double>& A , const ublas::vector<double>& y) const;
};


auto_ptr<ParameterEstimator> ParameterEstimator::create(const Function& function,
                                                                const Data& data,
                                                                const Parameters& initialEstimate)
{
    return auto_ptr<ParameterEstimator>(new ParameterEstimatorImpl(function, data, initialEstimate));
}


ParameterEstimatorImpl::ParameterEstimatorImpl(const Function& function,
                                                       const Data& data,
                                                       const Parameters& initialEstimate)
:   f_(function),
    data_(data),
    p_(initialEstimate),
    e_(f_, data_)
{
    if (function.parameterCount() != initialEstimate.size())
        throw logic_error("[ParameterEstimator::ParameterEstimatorImpl()] Wrong number of parameters.");
}


double ParameterEstimatorImpl::iterate(ostream* log)
{
    // DerivativeTest::testDerivatives< complex<double> >(e_, p_); // testing only

    double error_old = error();

    ublas::vector<double> d = e_.dp(p_);
    ublas::matrix<double> d2 = e_.dp2(p_);

    // calculate new estimate:
    //   correction == inverse(d2) * d
    //   p_new = p_old - correction

    Parameters correction = solve(d2, d);

    // compare error change to prediction from parabolic approximation
    ublas::vector<double> dp = -correction;
    double error_change_predicted = inner_prod(d, dp) + .5*inner_prod(dp, prod(d2,dp));
    double error_change_actual = e_(p_-correction) - error_old;


    if (log) *log << "d: " << d << endl;
    if (log) *log << "d2: " << d2 << endl;
    if (log) *log << "correction: " << correction << endl;
    if (log) *log << "error_change_predicted: " << error_change_predicted << endl;
    if (log) *log << "error_change_actual: " << error_change_actual << endl;


    // if we can decrease error -- go for it!
    if (error_change_actual < 0)
    {
        p_ -= correction;
        return error_change_actual;
    }

    // error is going to increase if we make the full correction;
    // backtrack along correction gradient to find decreasing error

    Parameters correction_backtrack = correction;
    int zeroCount = 0;

    for (int i=0; i<10; i++)
    {
        correction_backtrack /= 2;
        double error_change_backtrack = e_(p_ - correction_backtrack) - error_old;
        if (log) *log << "error_change_backtrack: " << error_change_backtrack << endl;
        if (error_change_backtrack < 0)
        {
            // found negative error change -- go for it!
            p_ -= correction_backtrack;
            return error_change_backtrack;
        }
        else if (error_change_backtrack == 0)
        {
            zeroCount++;
            if (zeroCount >= 3) // stuck on zero -- we're outta here
                break;
        }
    }

    // don't correct
    if (log) *log << "No correction.\n";
    return 0;
}


ParameterEstimatorImpl::Parameters ParameterEstimatorImpl::solve(const ublas::matrix<double>& A , const ublas::vector<double>& y) const
{
    // solve Ax = y

    ublas::matrix<double> A_factorized = A;

    ublas::permutation_matrix<size_t> pm(e_.parameterCount());
    int singular = lu_factorize(A_factorized, pm);
    if (singular)
        throw runtime_error("[ParameterEstimatorImpl::solve()] A is singular.");

    ublas::vector<double> result(y);
    lu_substitute(A_factorized, pm, result);
    return result;
}


} // namespace frequency
} // namespace pwiz

