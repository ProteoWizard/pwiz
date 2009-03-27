//
// Feature_dataFetcherTest.cpp
//

#include "Feature_dataFetcher.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <fstream>

using namespace std;
using namespace pwiz::data::peakdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::eharmony;

ostream* os_ = 0;

Feature makeFeature(double mz, double retentionTime)
{
    Feature feature;
    feature.mzMonoisotopic = mz;
    feature.retentionTime = retentionTime;

    return feature;
}

void test()
{
    
    if (os_) *os_ << "\ntest() ... \n\n";

    // make a vector of features
    Feature a = makeFeature(1,2);
    Feature b = makeFeature(3,4);
    Feature c = makeFeature(5,6);

    vector<Feature> features;
    features.push_back(a);
    features.push_back(b);
    features.push_back(c);

    // test vector<Feature> constructor
    if (os_) *os_ << "constructing Feature_dataFetcher ... " << endl;
    Feature_dataFetcher fdf(features);
    if (os_) *os_ << "constructed. " << endl;

    if (os_) *os_ << "testing getFeatures ... " << endl;
    vector<Feature> test_a = fdf.getFeatures(1,2);
    vector<Feature> test_b = fdf.getFeatures(3,4);
    vector<Feature> test_c = fdf.getFeatures(5,6);
    if (os_) *os_ << "getFeatures fine." << endl;

//     unit_assert(find(test_a.begin(), test_a.end(), a) != test_a.end());
//     unit_assert(find(test_b.begin(), test_b.end(), b) != test_b.end());
//     unit_assert(find(test_c.begin(), test_c.end(), c) != test_c.end());

    if (os_) 
        {
            *os_ << "testing vector<Feature> constructor ... \n";
            ostringstream oss;
            XMLWriter writer(oss);
            vector<Feature>::iterator a_it = test_a.begin();
            for(; a_it != test_a.end(); ++a_it) a_it->write(writer);
            *os_ << oss.str() << endl;
        }


    // write a FeatureFile
    ostringstream oss;
    XMLWriter writer(oss);
    FeatureFile ff;
    ff.features = features;
    ff.write(writer);
    
    //read it to an istream
    istringstream iss(oss.str());

    // test istream constructor
    Feature_dataFetcher fdf_is(iss);

    vector<Feature> test_is_a = fdf_is.getFeatures(1,2);
    vector<Feature> test_is_b = fdf_is.getFeatures(3,4);
    vector<Feature> test_is_c = fdf_is.getFeatures(5,6);

    unit_assert(find(test_is_a.begin(), test_is_a.end(), a) != test_is_a.end());
    unit_assert(find(test_is_b.begin(), test_is_b.end(), b) != test_is_b.end());
    unit_assert(find(test_is_c.begin(), test_is_c.end(), c) != test_is_c.end());


    if (os_)
        {
            *os_ << "\ntesting istream constructor ... \n";
      
        }


    return;

}

int main(int argc, char* argv[])
{

    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "Feature_dataFetcherTest ... \n";
            test();
            return 0;
        }
    catch (exception& e)
        {
            cerr << e.what() << endl;
        }
    catch (...)
        {
            cerr << "Caught unknown exception.\n";
        }

    return 1;
}
