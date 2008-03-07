//
// erfTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "erf.hpp"
#include "util/unit.hpp"
#include <iostream>
#include <iomanip>
#include <limits>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::math;


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


void test_it()
{
    if (os_) *os_ << "test_it()\n";

    double a = 10;
    for (double x=-a; x<=a; x+=a/100)
    {
        if (os_) *os_ << "a: " << a << endl;
        complex<double> resultComplex = erf(complex<double>(x));
        double resultReal = ((double(*)(double))erf)(x);
        if (os_) *os_ << resultComplex << " " << resultReal << endl;
        unit_assert_equal(resultComplex.real(), resultReal, 1e-12);
    }
}

void test_itvs2()
{
    if (os_) *os_ << "test_itvs2()\n";

    // erf_series2 matches erf in region [-2,2]x[-2,2] within 1e-10
    double a = 2;
    for (double x=-a; x<=a; x+=a/5.)
    for (double y=-a; y<=a; y+=a/5.)
    {
        complex<double> z(x,y);
        complex<double> result1 = erf(z);
        complex<double> result2 = erf_series2(z);
        //if (os_) *os_ << z << ": " << abs(result1-result2) << endl;
        unit_assert_equal(abs(result1-result2), 0, 1e-10);
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "erfTest\n" << setprecision(20);
        //test_it(); // msvc chokes on this for some reason -- fix if you're going to use this implementation
        test_itvs2();
        return 0;
    }
    catch (exception &e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

