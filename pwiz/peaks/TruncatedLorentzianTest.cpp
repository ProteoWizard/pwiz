//
// TruncatedLorentzianTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "TruncatedLorentzian.hpp"
#include "DerivativeTest.hpp"


#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/io.hpp>
namespace ublas = boost::numeric::ublas;


#include <iostream>
#include <iomanip>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::peaks;


ostream* os_ = 0;


int main(int argc, char* argv[])
{
    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
    if (os_) *os_ << "TruncatedLorentzianTest\n";

    if (os_) *os_ << setprecision(8);

    ublas::vector<double> p(4);

    p(TruncatedLorentzian::AlphaR) = 1;
    p(TruncatedLorentzian::AlphaI) = 5;
    p(TruncatedLorentzian::Tau) = 2;
    p(TruncatedLorentzian::F0) = 0;
    double T = 1;

/*
    p(TruncatedLorentzian::AlphaR) = 5e6;
    p(TruncatedLorentzian::AlphaI) = 0;
    p(TruncatedLorentzian::Tau) = 1;
    p(TruncatedLorentzian::F0) = 159455;
    double T = .384;
*/

    TruncatedLorentzian L(T);

//    L.outputSamples(cout, p);

/*
    for (int i=0; i<10; i++)
        DerivativeTest::testDerivatives(L, i, p, 1e-5, 1e-3);
*/

    if (os_) *os_ << "L(0): " << L(0,p) << endl;

    return 0;
}
