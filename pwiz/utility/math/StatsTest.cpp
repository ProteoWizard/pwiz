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


#include "Stats.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::math;


ostream* os_ = 0;


void test()
{
    Stats::vector_type a(2);
    a(0) = 1;
    a(1) = 2;

    Stats::vector_type b(2);
    b(0) = 3;
    b(1) = 4;

    Stats::vector_type c(2);
    c(0) = 5;
    c(1) = 6;

    Stats::data_type data;
    data.push_back(a);
    data.push_back(b);
    data.push_back(c);

    Stats stats(data);
    if (os_) *os_ << "mean: " << stats.mean() << endl;    
    if (os_) *os_ << "covariance: " << stats.covariance() << endl;

    // mean & covariance computed using good old-fashioned reckoning 
    Stats::vector_type mean(2);
    mean(0) = 3;
    mean(1) = 4;
    Stats::matrix_type covariance(2,2);
    covariance(0,0) = covariance(0,1) = covariance(1,0) = covariance(1,1) = 8/3.;

    // verify results
    const double epsilon = 1e-12;
    unit_assert_vectors_equal(stats.mean(), mean, epsilon);
    unit_assert_matrices_equal(stats.covariance(), covariance, epsilon); 

    double rms0_good = sqrt(35./3);
    double rms1_good = sqrt(56./3);
    double rms0_test = sqrt(stats.meanOuterProduct()(0,0));
    double rms1_test = sqrt(stats.meanOuterProduct()(1,1));
    unit_assert_equal(rms0_test, rms0_good, epsilon);
    unit_assert_equal(rms1_test, rms1_good, epsilon);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "StatsTest\n";
        test(); 
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

