///
/// Feature_dataFetcher.hpp
///

#ifndef _FEATURE_DATAFETCHER_HPP_
#define _FEATURE_DATAFETCHER_HPP_

#include "Bin.hpp"
#include "FeatureSequenced.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "boost/shared_ptr.hpp"

#include<iostream>
#include<fstream>

using namespace pwiz::data::peakdata;

namespace pwiz{
namespace eharmony{

class Feature_dataFetcher
{

public:

    Feature_dataFetcher(){}
    Feature_dataFetcher(std::istream& is);
    Feature_dataFetcher(const std::vector<FeaturePtr>& features);
    
    void update(const FeatureSequenced& fs);
    void erase(const FeatureSequenced& fs);
    void merge(const Feature_dataFetcher& that);

    std::vector<boost::shared_ptr< FeatureSequenced> > getFeatures(double mz, double rt) ;
    std::vector<FeatureSequenced> getAllContents() const;
    Bin<FeatureSequenced> getBin() const { return _bin; } 
    
    void setMS2LabeledFlag(const bool& flag) { _ms2Labeled = flag; }
    const bool& getMS2LabeledFlag() const { return _ms2Labeled; }
  
    bool operator==(const Feature_dataFetcher& that);
    bool operator!=(const Feature_dataFetcher& that);

private:

    bool _ms2Labeled;
    Bin<FeatureSequenced> _bin;
    
    // no copying
    Feature_dataFetcher(Feature_dataFetcher&);
    Feature_dataFetcher operator=(Feature_dataFetcher&);

};

} // namespace eharmony
} // namespace pwiz


#endif //_FEATURE_DATAFETCHER_HPP_
