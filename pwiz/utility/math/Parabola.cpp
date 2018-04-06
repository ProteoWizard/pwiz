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


#define PWIZ_SOURCE

#include "Parabola.hpp"
#include "pwiz/utility/misc/Std.hpp"


#ifndef NDEBUG
#define NDEBUG
#endif // NDEBUG
#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/io.hpp>
#include <boost/numeric/ublas/lu.hpp>
#include <boost/numeric/ublas/triangular.hpp>
#include <boost/numeric/ublas/vector_proxy.hpp>
namespace ublas = boost::numeric::ublas;


#if BOOST_UBLAS_TYPE_CHECK
goo; // need -DNDEBUG in makefile!
#endif


namespace pwiz {
namespace math {


PWIZ_API_DECL Parabola::Parabola(double a, double b, double c)
:   a_(3)
{
    a_[0] = a;
    a_[1] = b;
    a_[2] = c;
}


PWIZ_API_DECL Parabola::Parabola(vector<double> a)
:   a_(a)
{
    if (a_.size() != 3)
        throw logic_error("[Parabola::Parabola()] 3 coefficients required.");
}


namespace {


void solve(ublas::matrix<double>& A, ublas::vector<double>& a)
{
    ublas::permutation_matrix<size_t> pm(3);
    int singular = lu_factorize(A, pm);
    if (singular)
        throw runtime_error("[Parabola.cpp::solve()] Matrix is singular.");

    lu_substitute(A, pm, a); // may cause assertion without NDEBUG defined for ublas, probably due to roundoff errors
}


void fitExact(const vector< pair<double,double> >& samples, vector<double>& coefficients)
{
    if (samples.size() != 3)
        throw logic_error("[Parabola.cpp::fitExact()] Exactly 3 samples required.\n");

    // fit parabola to the 3 samples (xi,yi):
    //
    //   ( x1^2  x1  1 )( a[0] )   ( y1 )
    //   ( x2^2  x2  1 )( a[1] ) = ( y2 )
    //   ( x3^2  x3  1 )( a[2] )   ( y3 )

    ublas::matrix<double> A(3,3);
    ublas::vector<double> a(3);

    for (int i=0; i<3; i++)
    {
        double x = samples[i].first;
        double y = samples[i].second;
        A(i,0) = x*x;
        A(i,1) = x;
        A(i,2) = 1;
        a(i) = y;
    }

    solve(A, a);
    copy(a.begin(), a.end(), coefficients.begin());
}


void fitWeightedLeastSquares(const vector< pair<double,double> >& samples,
                             const vector<double>& weights,
                             vector<double>& coefficients)
{
    if (samples.size() != weights.size())
        throw logic_error("[Parabola.cpp::fitWeightedLeastSquares] Wrong weight count.");

    // given samples {(xi,yi)} and weights {wi}
    // minimize e(a) = sum[wi(p(a,xi)-yi)^2],
    // where p(a,xi) = a[0](xi)^2 + a[1](xi) + a[0]c
    //
    // de/da == 0 =>
    //   ( sum_wx4  sum_wx3  sum_wx2 )( a[0] )   ( sum_wyx2 )
    //   ( sum_wx3  sum_wx2  sum_wx1 )( a[1] ) = ( sum_wyx1 )
    //   ( sum_wx2  sum_wx1  sum_wx0 )( a[2] )   ( sum_wyx0 )
    //
    // where:
    //   sum_wxn means sum[wi*(xi)^n]
    //   sum_wyxn means sum[wi*yi*(xi)^n]

    double sum_wx4 = 0;
    double sum_wx3 = 0;
    double sum_wx2 = 0;
    double sum_wx1 = 0;
    double sum_wx0 = 0;
    double sum_wyx2 = 0;
    double sum_wyx1 = 0;
    double sum_wyx0 = 0;

    for (unsigned int i=0; i<samples.size(); i++)
    {
        double x = samples[i].first;
        double y = samples[i].second;
        double w = weights[i];

        sum_wx4 += w*pow(x,4);
        sum_wx3 += w*pow(x,3);
        sum_wx2 += w*x*x;
        sum_wx1 += w*x;
        sum_wx0 += w;
        sum_wyx2 += w*y*x*x;
        sum_wyx1 += w*y*x;
        sum_wyx0 += w*y;
    }

    ublas::matrix<double> A(3,3);
    ublas::vector<double> a(3);

    A(0,0) = sum_wx4;
    A(1,0) = A(0,1) = sum_wx3;
    A(2,0) = A(1,1) = A(0,2) = sum_wx2;
    A(1,2) = A(2,1) = sum_wx1;
    A(2,2) = sum_wx0;

    a(0) = sum_wyx2;
    a(1) = sum_wyx1;
    a(2) = sum_wyx0;

    solve(A, a);
    copy(a.begin(), a.end(), coefficients.begin());
}

} // namespace


PWIZ_API_DECL Parabola::Parabola(const vector< pair<double,double> >& samples)
:   a_(3)
{
    if (samples.size() < 3)
        throw logic_error("[Parabola::Parabola()] At least 3 samples required.");

    if (samples.size() == 3)
        fitExact(samples, a_);
    else
        fitWeightedLeastSquares(samples, vector<double>(samples.size(),1), a_);
}


// construct by weighted least squares
PWIZ_API_DECL
Parabola::Parabola(const std::vector< std::pair<double,double> >& samples,
                   const std::vector<double>& weights)
:   a_(3)
{
    fitWeightedLeastSquares(samples, weights, a_);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Parabola& p)
{
    vector<double> a = p.coefficients();
    os << "[Parabola (" << a[0] << ", " << a[1] << ", " << a[2] << ")]";
    return os;
}


} // namespace math 
} // namespace pwiz

