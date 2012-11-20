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


#include "LinearSolver.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/numeric/ublas/matrix_sparse.hpp>
#include <boost/numeric/ublas/banded.hpp>
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::math;


namespace ublas = boost::numeric::ublas;


ostream* os_ = 0;


void testDouble()
{
    if (os_) *os_ << "testDouble()\n";

    LinearSolver<> solver;

    ublas::matrix<double> A(2,2);
    A(0,0) = 1; A(0,1) = 2;
    A(1,0) = 3; A(1,1) = 4;
   
    ublas::vector<double> y(2);
    y(0) = 5;
    y(1) = 11;

    ublas::vector<double> x = solver.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    unit_assert(x(0) == 1.);
    unit_assert(x(1) == 2.);
}


void testComplex()
{
    if (os_) *os_ << "testComplex()\n";

    LinearSolver<> solver;
    
    ublas::matrix< complex<double> > A(2,2);
    A(0,0) = 1; A(0,1) = 2;
    A(1,0) = 3; A(1,1) = 4;
   
    ublas::vector< complex<double> > y(2);
    y(0) = 5;
    y(1) = 11;

    ublas::vector< complex<double> > x = solver.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    unit_assert(x(0) == 1.);
    unit_assert(x(1) == 2.);
}

void testDoubleQR()
{
    if (os_) *os_ << "testDoubleQR()\n";

    LinearSolver<LinearSolverType_QR> solver;

    ublas::matrix<double> A(2,2);
    A(0,0) = 1.; A(0,1) = 2.;
    A(1,0) = 3.; A(1,1) = 4.;
   
    ublas::vector<double> y(2);
    y(0) = 5.;
    y(1) = 11.;

    ublas::vector<double> x = solver.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    if (os_) *os_ << x(0) << " - 1. = " << x(0) - 1. << endl;

    unit_assert_equal(x(0), 1., 1e-14);
    unit_assert_equal(x(1), 2., 1e-14);
}

/*
void testComplexQR()
{
    if (os_) *os_ << "testComplex()\n";

    LinearSolver<LinearSolverType_QR> solver;
    
    ublas::matrix< complex<double> > A(2,2);
    A(0,0) = 1; A(0,1) = 2;
    A(1,0) = 3; A(1,1) = 4;
   
    ublas::vector< complex<double> > y(2);
    y(0) = 5;
    y(1) = 11;

    ublas::vector< complex<double> > x = solver.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    unit_assert(x(0) == 1.);
    unit_assert(x(1) == 2.);
}
*/


void testSparse()
{
    if (os_) *os_ << "testSparse()\n";

    LinearSolver<> solver;

    ublas::mapped_matrix<double> A(2,2,4);
    A(0,0) = 1.; A(0,1) = 2.;
    A(1,0) = 3.; A(1,1) = 4.;
   
    ublas::vector<double> y(2);
    y(0) = 5.;
    y(1) = 11.;

    ublas::vector<double> x = solver.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    unit_assert_equal(x(0), 1., 1e-14);
    unit_assert_equal(x(1), 2., 1e-14);
}


/*
void testSparseComplex()
{
    if (os_) *os_ << "testSparseComplex()\n";

    LinearSolver<> solver;

    ublas::mapped_matrix< complex<double> > A(2,2,4);
    A(0,0) = 1.; A(0,1) = 2.;
    A(1,0) = 3.; A(1,1) = 4.;
   
    ublas::vector< complex<double> > y(2);
    y(0) = 5.;
    y(1) = 11.;

    ublas::vector< complex<double> > x = solver.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    unit_assert(norm(x(0)-1.) < 1e-14);
    unit_assert(norm(x(1)-2.) < 1e-14);
}
*/


void testBanded()
{
    if (os_) *os_ << "testBanded()\n";

    LinearSolver<> solver;

    ublas::banded_matrix<double> A(2,2,1,1);
    A(0,0) = 1.; A(0,1) = 2.;
    A(1,0) = 3.; A(1,1) = 4.;
   
    ublas::vector<double> y(2);
    y(0) = 5.;
    y(1) = 11.;

    ublas::vector<double> x = solver.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    unit_assert_equal(x(0), 1., 1e-14);
    unit_assert_equal(x(1), 2., 1e-14);
}


void testBandedComplex()
{
    if (os_) *os_ << "testBandedComplex()\n";

    LinearSolver<> solver;

    ublas::banded_matrix< complex<double> > A(2,2,1,1);
    A(0,0) = 1.; A(0,1) = 2.;
    A(1,0) = 3.; A(1,1) = 4.;
   
    ublas::vector< complex<double> > y(2);
    y(0) = 5.;
    y(1) = 11.;

    ublas::vector< complex<double> > x = solver.solve(A, y);

    if (os_) *os_ << "A: " << A << endl;
    if (os_) *os_ << "y: " << y << endl;
    if (os_) *os_ << "x: " << x << endl;

    unit_assert(norm(x(0)-1.) < 1e-14);
    unit_assert(norm(x(1)-2.) < 1e-14);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "LinearSolverTest\n";

        testDouble();
        testComplex();
        testDoubleQR();
        //testComplexQR();
        testSparse();
        //testSparseComplex(); // lu_factorize doesn't like mapped_matrix<complex> 
        testBanded();
        //testBandedComplex(); // FIXME: GCC 4.2 doesn't like this test with link=shared
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

