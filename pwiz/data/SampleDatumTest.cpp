//
// SampleDatumTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "SampleDatum.hpp"
#include "util/unit.hpp"
#include <iostream>
#include <vector>
#include <iterator>
#include <complex>


using namespace pwiz::util;
using namespace pwiz::data;
using namespace std; 


ostream* os_ = 0;


template <typename abscissa_type, typename ordinate_type>
void test()
{
    typedef SampleDatum<abscissa_type,ordinate_type> sd_type;

    vector<sd_type> v;
    v.push_back(sd_type(1,2));
    v.push_back(sd_type(3,4));
    v.push_back(sd_type(5,6));

    // write the pairs out to a stream
    ostringstream oss;
    copy(v.begin(), v.end(), ostream_iterator<sd_type>(oss, "\n"));
    if (os_) *os_ << oss.str();

    // read them back in 
    vector<sd_type> w;
    istringstream iss(oss.str());
    copy(istream_iterator<sd_type>(iss), istream_iterator<sd_type>(), back_inserter(w)); 

    // compare the two vectors
    unit_assert(v == w);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "SampleDatumTest\n";

        test<int,int>();
        test<double,double>();
        test< double,complex<double> >();
        test< complex<double>,complex<double> >();

        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unknown exception.\n";
    }

    return 1; 
}


