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

FeatureSequenced makeFeatureSequenced(const Feature& feature, string _ms1_5="", string _ms2="")
{
    FeatureSequenced fs;
    fs.feature = feature;
    fs.ms1_5 = _ms1_5;
    fs.ms2 = _ms2;

    return fs;

}

void test()
{
    
    if (os_) *os_ << "\ntest() ... \n\n";

    // make a vector of features
    Feature a = makeFeature(1,2);
    Feature b = makeFeature(3,4);
    Feature c = makeFeature(5,6);

    // make FeatureSequenced objects
    FeatureSequenced fs_a(a);
    FeatureSequenced fs_b(b);
    FeatureSequenced fs_c(c);

    vector<Feature> features;
    features.push_back(a);
    features.push_back(b);
    features.push_back(c);

    // test vector<Feature> constructor
    if (os_) *os_ << "constructing Feature_dataFetcher ... " << endl;
    Feature_dataFetcher fdf(features);
    if (os_) *os_ << "constructed. " << endl;

    if (os_) *os_ << "testing getFeatures ... " << endl;
    vector<FeatureSequenced> test_a = fdf.getFeatures(1,2);
    vector<FeatureSequenced> test_b = fdf.getFeatures(3,4);
    vector<FeatureSequenced> test_c = fdf.getFeatures(5,6);

    unit_assert(find(test_a.begin(), test_a.end(), fs_a) != test_a.end());
    unit_assert(find(test_b.begin(), test_b.end(), fs_b) != test_b.end());
    unit_assert(find(test_c.begin(), test_c.end(), fs_c) != test_c.end());

    if (os_) 
        {
            *os_ << "testing vector<Feature> constructor ... \n";
            ostringstream oss;
            XMLWriter writer(oss);
            vector<FeatureSequenced>::iterator a_it = test_a.begin();
            for(; a_it != test_a.end(); ++a_it) a_it->feature.write(writer);
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

    vector<FeatureSequenced> test_is_a = fdf_is.getFeatures(1,2);
    vector<FeatureSequenced> test_is_b = fdf_is.getFeatures(3,4);
    vector<FeatureSequenced> test_is_c = fdf_is.getFeatures(5,6);

    unit_assert(find(test_is_a.begin(), test_is_a.end(), fs_a) != test_is_a.end());
    unit_assert(find(test_is_b.begin(), test_is_b.end(), fs_b) != test_is_b.end());
    unit_assert(find(test_is_c.begin(), test_is_c.end(), fs_c) != test_is_c.end());


    if (os_)
        {
            *os_ << "\ntesting istream constructor ... \n";
      
        }


    return;

}

void testMerge()
{
    if(os_) *os_ << "\ntestMerge()...\n" << endl;

    Feature a = makeFeature(5,1);
    Feature b = makeFeature(20,9);

    FeatureSequenced fs_b = makeFeatureSequenced(b);

    vector<Feature> v_a;
    vector<Feature> v_b;

    v_a.push_back(a);
    v_b.push_back(b);

    Feature_dataFetcher fdf_a(v_a);
    Feature_dataFetcher fdf_b(v_b);
    
    fdf_a.merge(fdf_b);
    vector<FeatureSequenced> binContents = fdf_a.getFeatures(b.mzMonoisotopic, b.retentionTime);
    
    unit_assert(binContents.size() > 0);

    if (os_)
        {
   	    *os_ << "Merged FeatureSequenced:\n " << endl;

	    XMLWriter writer(*os_);
	    binContents.begin()->feature.write(writer);
	    *os_ << "ms1_5: " <<  binContents.begin()->ms1_5 << endl;
	    *os_ << "ms2: " << binContents.begin()->ms2 << endl;

     	    *os_ << "\nOriginal FeatureSequenced:\n " << endl;   
	     
	    fs_b.feature.write(writer);
	    *os_ << "ms1_5: " << fs_b.ms1_5 << endl;
	    *os_ << "ms2: " << fs_b.ms2 << endl;

        }  

    unit_assert(binContents.back() == fs_b);

}

int main(int argc, char* argv[])
{

    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "Feature_dataFetcherTest ... \n";

            test();
            testMerge();

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
