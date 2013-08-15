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


#include "Parabola.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>
#include <limits>
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::math;


ostream* os_ = 0;
double epsilon_ = numeric_limits<double>::epsilon();


void testBasic()
{
    if (os_) *os_ << "***************************\n";
    if (os_) *os_ << "testBasic()\n";
    Parabola p(2, 3, 4);
    unit_assert_equal(p(5), 69, epsilon_);
    p.coefficients()[0] = 3;
    unit_assert_equal(p(5), 94, epsilon_);
    if (os_) *os_ << "testBasic(): success\n";
}


void testExactFit()
{
    if (os_) *os_ << "***************************\n";
    if (os_) *os_ << "testExactFit()\n";

    vector< pair<double,double> > samples;
    samples.push_back(make_pair(1,1));
    samples.push_back(make_pair(2,3));
    samples.push_back(make_pair(3,9));

    const Parabola p(samples);

    if (os_) *os_ << p << endl;
    if (os_) *os_ << "center: (" << p.center() << ", " << p(p.center()) << ")\n";

    const vector<double>& a = p.coefficients();

    unit_assert_equal(a[0], 2, epsilon_*10);
    unit_assert_equal(a[1], -4, epsilon_*10);

    unit_assert_equal(a[2], 3, epsilon_*5);
    unit_assert_equal(p.center(), 1, epsilon_);
    unit_assert_equal(p(p.center()), 1, epsilon_*10);
    unit_assert_equal(p(0), 3, epsilon_*5);

    if (os_) *os_ << "testExactFit(): success\n";
}


void testLeastSquares()
{
    if (os_) *os_ << "***************************\n";
    if (os_) *os_ << "testLeastSquares()\n";

    vector< pair<double,double> > samples;
    samples.push_back(make_pair(1,1));
    samples.push_back(make_pair(2,3));
    samples.push_back(make_pair(3,9));
    samples.push_back(make_pair(0,3));
    samples.push_back(make_pair(-1,9));

    const Parabola p(samples);

    if (os_) *os_ << p << endl;
    if (os_) *os_ << "center: (" << p.center() << ", " << p(p.center()) << ")\n";

    const vector<double>& a = p.coefficients();

    unit_assert_equal(a[0], 2, epsilon_*10);
    unit_assert_equal(a[1], -4, epsilon_*100);
    unit_assert_equal(a[2], 3, epsilon_*10);
    unit_assert_equal(p.center(), 1, epsilon_*10);
    unit_assert_equal(p(p.center()), 1, epsilon_*100);
    unit_assert_equal(p(0), 3, epsilon_*10);

    if (os_) *os_ << "testLeastSquares(): success\n";
}


void testWeightedLeastSquares()
{
    if (os_) *os_ << "***************************\n";
    if (os_) *os_ << "testWeightedLeastSquares()\n";

    // fit to f(x) = 1/sqrt(x*x+1)

    // samples ( x, 1/(f(x)*f(x)) ), i.e. (x, x*x+1)
    vector< pair<double,double> > samples;
    samples.push_back(make_pair(0,1));
    samples.push_back(make_pair(1,2));
    samples.push_back(make_pair(2,5));
    samples.push_back(make_pair(-3,10));
    samples.push_back(make_pair(-4,17));

    // weights w = (y^6)/4 => fits data to 1/sqrt(a[0]x^2 + a[1]x + a[2])
    vector<double> weights;
    for (unsigned int i=0; i<samples.size(); i++)
    {
        double y = samples[i].second;
        weights.push_back(pow(y,6)/4);
    }

    if (os_)
    {
        *os_ << "weights: ";
        copy(weights.begin(), weights.end(), ostream_iterator<double>(*os_, " " ));
    }

    const Parabola p(samples, weights);

    if (os_) *os_ << p << endl;
    if (os_) *os_ << "center: (" << p.center() << ", " << p(p.center()) << ")\n";

    if (os_)
    {
        *os_ << "coefficients: " << setprecision(14);
        copy(p.coefficients().begin(), p.coefficients().end(), ostream_iterator<double>(*os_, " "));
        *os_ << endl;
    }

    unit_assert_equal(p.coefficients()[0], 1, epsilon_*1000);
    unit_assert_equal(p.coefficients()[1], 0, epsilon_*10e4);
    unit_assert_equal(p.coefficients()[2], 1, epsilon_*10e4);
    if (os_) *os_ << "testWeightedLeastSquares(): success\n";
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "ParabolaTest\n";
        testBasic();
        testExactFit();
        testLeastSquares();
        testWeightedLeastSquares();
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

