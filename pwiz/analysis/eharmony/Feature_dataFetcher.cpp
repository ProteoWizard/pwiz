/// 
/// Feature_dataFetcher.cpp
///

#include "Feature_dataFetcher.hpp"
#include "pwiz/data/misc/PeakData.hpp"

using namespace std;
using namespace pwiz::eharmony;
using namespace pwiz::data::peakdata;

typedef pair<pair<double,double>, FeatureSequenced> FeatBinPair;

void getCoordinates(const vector<Feature>& f, vector<FeatBinPair>& result)
{   
    vector<Feature>::const_iterator f_it = f.begin();
    for(; f_it != f.end(); ++f_it)
        {
	    FeatureSequenced featureSequenced(*f_it);
            result.push_back(make_pair(make_pair(f_it->mzMonoisotopic, f_it->retentionTime),featureSequenced));
            
        }

}


void getCoordinates(const vector<boost::shared_ptr<FeatureSequenced> >& f, vector<FeatBinPair>& result)
{
  vector<boost::shared_ptr<FeatureSequenced> >::const_iterator f_it = f.begin();
  for(; f_it != f.end(); ++f_it)
    {;
      result.push_back(make_pair(make_pair((*f_it)->feature->mzMonoisotopic, (*f_it)->feature->retentionTime),**f_it));

    }

}

Feature_dataFetcher::Feature_dataFetcher(istream& is)
{
    FeatureFile ff;
    ff.read(is);
    
    vector<FeatBinPair> features;
    getCoordinates(ff.features, features);

    _bin = Bin<FeatureSequenced>(features,.005, 1000); // be lenient for adjust rt? should be a config option

}

Feature_dataFetcher::Feature_dataFetcher(vector<Feature>& f)
{
    vector<FeatBinPair> features;
    getCoordinates(f, features);
    _bin = Bin<FeatureSequenced>(features, .005, 1000); 

}

Feature_dataFetcher::Feature_dataFetcher(vector<boost::shared_ptr<FeatureSequenced> >& f)
{
    vector<FeatBinPair> features;
    getCoordinates(f, features);
    _bin = Bin<FeatureSequenced>(features, .005, 1000);

}

void Feature_dataFetcher::update(const FeatureSequenced& fs)
{
    double mz = fs.feature->mzMonoisotopic;
    double rt = fs.feature->retentionTime;
    _bin.update(fs, make_pair(mz,rt));

}

void Feature_dataFetcher::erase(const FeatureSequenced& fs)
{
    double mz = fs.feature->mzMonoisotopic;
    double rt = fs.feature->retentionTime;
    _bin.erase(fs, make_pair(mz,rt));

}

void Feature_dataFetcher::merge(const Feature_dataFetcher& that)
{
    Bin<FeatureSequenced> bin = that.getBin();
    vector<boost::shared_ptr<FeatureSequenced> > entries = bin.getAllContents();
    vector<boost::shared_ptr<FeatureSequenced> >::iterator it = entries.begin();
    for(; it != entries.end(); ++it) update(**it);
    
}

vector<boost::shared_ptr<FeatureSequenced> > Feature_dataFetcher::getFeatures(double mz, double rt) 
{
    pair<double,double> coords = make_pair(mz,rt);
    vector<boost::shared_ptr<FeatureSequenced> > features;
    _bin.getBinContents(coords,features);
    
    return features;

}
