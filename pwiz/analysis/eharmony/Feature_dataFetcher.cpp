/// 
/// Feature_dataFetcher.cpp
///

#include "Feature_dataFetcher.hpp"
#include "pwiz/data/misc/PeakData.hpp"

using namespace std;
using namespace pwiz::eharmony;
using namespace pwiz::data::peakdata;

typedef pair<pair<double,double>, Feature> FeatBinPair;

void getCoordinates(const vector<Feature>& f, vector<FeatBinPair>& result)
{
   
    vector<Feature>::const_iterator f_it = f.begin();
    for(; f_it != f.end(); ++f_it)
        {
            result.push_back(make_pair(make_pair(f_it->mzMonoisotopic, f_it->retentionTime),*f_it));
            
        }

}

Feature_dataFetcher::Feature_dataFetcher(istream& is)
{
    FeatureFile ff;
    ff.read(is);
    
    vector<FeatBinPair> features;
    getCoordinates(ff.features, features);

    _bin = Bin<Feature>(features,.005, 1000); // be lenient for adjust rt? should be a config option

}

Feature_dataFetcher::Feature_dataFetcher(vector<Feature>& f)
{
    vector<FeatBinPair> features;
    getCoordinates(f, features);
    _bin = Bin<Feature>(features, .005, 1000); 

}

void Feature_dataFetcher::update(const Feature& f)
{
    double mz = f.mzMonoisotopic;
    double rt = f.retentionTime;
    _bin.update(f, make_pair(mz,rt));

}

void Feature_dataFetcher::erase(const Feature& f)
{
    double mz = f.mzMonoisotopic;
    double rt = f.retentionTime;
    _bin.erase(f, make_pair(mz,rt));

}

vector<Feature> Feature_dataFetcher::getFeatures(double mz, double rt) 
{
    pair<double,double> coords = make_pair(mz,rt);
    vector<Feature> features;
    _bin.getBinContents(coords,features);
    
    return features;

}
