///
/// Feature_dataFetcher.hpp
///

#ifndef _FEATURE_DATAFETCHER_HPP_
#define _FEATURE_DATAFETCHER_HPP_

#include "Bin.hpp"
#include "pwiz/data/misc/PeakData.hpp"

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
    Feature_dataFetcher(std::vector<Feature>& features);
    Feature_dataFetcher(const Feature_dataFetcher& fdf) : _bin(fdf.getBin()) {}
    
    void update(const Feature& f);
    void erase(const Feature& f);

    std::vector<Feature> getFeatures(double mz, double rt) ;
    Bin<Feature> getBin() const { return _bin; } 
    
    void setMS2LabeledFlag(const bool& flag) { _ms2Labeled = flag; }
    const bool& getMS2LabeledFlag() const { return _ms2Labeled; }

private:

    bool _ms2Labeled;
    Bin<Feature> _bin;
    
};

} // namespace eharmony
} // namespace pwiz


#endif //_FEATURE_DATAFETCHER_HPP_
