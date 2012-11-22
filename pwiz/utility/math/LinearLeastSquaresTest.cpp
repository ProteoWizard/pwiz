//
// $Id$
//
// Original author: Robert Burke <robert.burke@cshs.org>
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

#include "LinearLeastSquares.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::math;
using namespace pwiz::math::types;


namespace ublas = boost::numeric::ublas;


const double epsilon = 1e-16;
ostream* os_ = 0;


void testDouble()
{
    if (os_) *os_ << "testDouble()\n";

    LinearLeastSquares<> lls;
    ublas::matrix<double> A(2, 2);
    A(0,0) = 1; A(0,1) = 2;
    A(1,0) = 3; A(1,1) = 4;
   
    ublas::vector<double> y(2);
    y(0) = 5;
    y(1) = 11;

    ublas::vector<double> x = lls.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    unit_assert_equal(x(0), 1., 1e-13);
    unit_assert_equal(x(1), 2., 1e-13);
}

void testDoubleQR()
{
    if (os_) *os_ << "testDoubleQR()\n";

    LinearLeastSquares<LinearLeastSquaresType_QR> lls;
    ublas::matrix<double> A(2, 2);
    A(0,0) = 1; A(0,1) = 2;
    A(1,0) = 3; A(1,1) = 4;
   
    ublas::vector<double> y(2);
    y(0) = 5;
    y(1) = 11;

    ublas::vector<double> x = lls.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    if (os_) *os_ << "x(0) = " << x(0) - 1. << ", x(1) = " << x(1) - 2. << endl;
    unit_assert_equal(x(0), 1., 100*epsilon);
    unit_assert_equal(x(1), 2., 100*epsilon);
}

void testExactFitQR()
{
    if (os_) *os_ << "***************************\n";
    if (os_) *os_ << "testExactFit()\n";

    dmatrix m(2,2);
    dvector obs(2);

    m(0, 0) = 1;
    m(1, 0) = 0;
    m(0, 1) = 0;
    m(1, 1) = 1;

    obs(0) = 1;
    obs(1) = 1;
    
    LinearLeastSquares<LinearLeastSquaresType_QR> lls;
    const dvector result = lls.solve(m,obs);

    unit_assert_equal(obs(0), 1, epsilon);
    unit_assert_equal(obs(1), 1, epsilon);

    if (os_) *os_ << "testExactFit(): success\n";
}


void testSimpleRectangleQR()
{
    if (os_) *os_ << "***************************\n";
    if (os_) *os_ << "testSimpleRectangleQR()\n";

    dmatrix samples(4, 2);

    samples.clear();
    samples(0,0) = 1;
    samples(0,1) = 0;
    samples(1,0) = 0;
    samples(1,1) = 1;
    samples(2,0) = 1;
    samples(2,1) = 0;
    samples(3,0) = 0;
    samples(3,1) = 1;

    dvector obs(4);
    obs(0) = 2;
    obs(1) = 2;
    obs(2) = 2;
    obs(3) = 2;

    LinearLeastSquares<LinearLeastSquaresType_QR> lls;

    dvector a = lls.solve(samples, obs);
    
    unit_assert_equal(a(0), 2, epsilon);
    unit_assert_equal(a(1), 2, epsilon);
    
    if (os_) *os_ << "testSimpleRectangleQR(): success\n";
}

void testLeastSquaresQR()
{
    if (os_) *os_ << "***************************\n";
    if (os_) *os_ << "testLeastSquaresQR()\n";

    dmatrix samples(5, 2);

    samples.clear();
    samples(0,0) = 1;
    samples(0,1) = 1;
    samples(1,0) = 2;
    samples(1,1) = 2;
    samples(2,0) = 3;
    samples(2,1) = 3;
    samples(3,0) = 0;
    samples(3,1) = 4;
    samples(4,0) = -1;
    samples(4,1) = 5;

    dvector obs(5);
    obs(0) = 1;
    obs(1) = 3;
    obs(2) = 9;
    obs(3) = 3;
    obs(4) = -9;

    LinearLeastSquares<LinearLeastSquaresType_QR> lls;

    dvector a = lls.solve(samples, obs);
    
    unit_assert_equal(a(0), 3.16666666666667, epsilon*100);
    unit_assert_equal(a(1), -0.5, epsilon*100);
    
    if (os_) *os_ << "testLeastSquaresQR(): success\n";
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "LinearLeastSquaresTest\n";
        testDouble();
        testDoubleQR();
        testExactFitQR();
        testSimpleRectangleQR();
        testLeastSquaresQR();
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
