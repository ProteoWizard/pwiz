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


#include "erf.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <limits>
#include <cstring>
#include <cmath>


using namespace pwiz::util;
using namespace pwiz::math;
using std::numeric_limits;
using std::complex;


ostream* os_ = 0;
const double epsilon_ = numeric_limits<double>::epsilon();


/*
void test_series()
{
    cout << "test_series()\n";

    // we match real-valued erf within epsilon_*10 in range [-2,2], 
    // using 29 terms at +/-2

    for (double x=-2; x<2; x+=.2)
    {
        complex<double> resultComplex = erf_series(complex<double>(x));
        double resultReal = ((double(*)(double))erf)(x);
        //cout << resultComplex << " " << resultReal << endl;
        unit_assert_equal(resultComplex.real(), resultReal, epsilon_*10);
    }
}


void test_series2()
{
    cout << "test_series2()\n";

    // 1e-12 precision in range [-20,20] using < 1200 terms
    for (double x=-20; x<20; x+=2)
    {
        complex<double> resultComplex = erf_series2(complex<double>(x));
        double resultReal = ((double(*)(double))erf)(x);
        //cout << resultComplex << " " << resultReal << endl;
        unit_assert_equal(resultComplex.real(), resultReal, 1e-12);
    }
}


void test_1vs2()
{
    cout << "test_1vs2()\n";

    // erf_series matches erf_series2 in region [-2,2]x[-2,2] within 1e-10
    double a = 2;
    for (double x=-a; x<=a; x+=a/5.)
    for (double y=-a; y<=a; y+=a/5.)
    {
        complex<double> z(x,y);
        complex<double> result1 = erf_series(z);
        complex<double> result2 = erf_series2(z);
        //cout << z << ": " << abs(result1-result2) << endl;
        unit_assert_equal(result1, result2, 1e-10);
    }
}


void test_convergence2()
{
    complex<double> z(100,1);
    cout << erf_series2(z) << endl;
}
*/


#if defined(_MSC_VER)
void test_real()
{
    cerr << "[erfTest] Warning: test_real() not implemented for MSVC.\n"; 
}
#else
void test_real()
{
    // tests our complex erf against the not-so-standard gcc-provided double erf(double)

    if (os_) *os_ << "test_real()\n";

    double a = 10;
    for (double x=-a; x<=a; x+=a/100)
    {
        complex<double> resultComplex = erf(complex<double>(x));
        double resultReal = ((double(*)(double))::erf)(x);
        if (os_) *os_ << x << " -> " << resultComplex << " " << resultReal << endl;
        unit_assert_equal(resultComplex.real(), resultReal, 1e-12);
    }

    if (os_) *os_ << endl;
}
#endif // defined(_MSC_VER)


void test_series()
{
    if (os_) *os_ << "test_series()\n";

    // erf_series2 matches erf in region [-2,2]x[-2,2] within 1e-10
    double a = 2;
    for (double x=-a; x<=a; x+=a/5.)
    for (double y=-a; y<=a; y+=a/5.)
    {
        complex<double> z(x,y);
        complex<double> result1 = erf(z);
        complex<double> result2 = erf_series2(z);
        if (os_) *os_ << z << ": " << abs(result1-result2) << endl;
        unit_assert_equal(abs(result1-result2), 0, 1e-10);
    }

    if (os_) *os_ << endl;
}


void test_real_wrapper()
{
    if (os_) *os_ << "test_real_wrapper()\n";

    double a = 10;
    for (double x=-a; x<=a; x+=a/100)
    {
        double result_pwiz = pwiz::math::erf(x);
        double result_std = ::erf(x);
        if (os_) *os_ << x << " -> " << result_pwiz << " " << result_std << endl;
        unit_assert_equal(result_pwiz, result_std, 1e-12);
    }

    if (os_) *os_ << endl;
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "erfTest\n" << setprecision(20);
        test_real();
        test_series();
        test_real_wrapper();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

