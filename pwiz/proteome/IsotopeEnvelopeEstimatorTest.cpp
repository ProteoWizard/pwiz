//
// IsotopeEnvelopeEstimatorTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "IsotopeEnvelopeEstimator.hpp"
#include "IsotopeCalculator.hpp"
#include "util/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


void testInstantiationWithNull()
{
    try 
    {
        IsotopeEnvelopeEstimator::Config config;
        IsotopeEnvelopeEstimator estimator(config);
    }
    catch (...)
    {
        if (os_) *os_ << "Null IsotopeCalculator* check ok.\n";
        return;
    }

    throw runtime_error("Failed to check for null IsotopeCalculator*.");
}


void test()
{
    const double abundanceCutoff = .01;
    const double massPrecision = .1; 
    IsotopeCalculator isotopeCalculator(abundanceCutoff, massPrecision);

    IsotopeEnvelopeEstimator::Config config;
    config.isotopeCalculator = &isotopeCalculator;

    IsotopeEnvelopeEstimator estimator(config);
 
    if (os_)
        for (int mass=100; mass<=3000; mass+=100)
            *os_ << mass << ":\n" << estimator.isotopeEnvelope(mass) << endl;

    // TODO: external verification of these estimates
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "IsotopeEnvelopeEstimatorTest\n";
        testInstantiationWithNull();
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

