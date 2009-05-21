///
/// FeatureSequenced.hpp
///

#ifndef _FEATURESEQUENCED_HPP_
#define _FEATURESEQUENCED_HPP_

#include "pwiz/data/misc/PeakData.hpp"
#include "boost/shared_ptr.hpp"

namespace pwiz{
namespace eharmony{

using namespace pwiz::data::peakdata;

struct PWIZ_API_DECL FeatureSequenced
{

    FeatureSequenced() : ms2(""), ms1_5("") { feature = boost::shared_ptr<Feature>(new Feature());}
    FeatureSequenced(const Feature& _feature) : ms2(""), ms1_5("") { feature = boost::shared_ptr<Feature> (new Feature(_feature));}
    FeatureSequenced(const FeatureSequenced& _fs) : feature(_fs.feature), ms2(_fs.ms2), ms1_5(_fs.ms1_5) {}

    boost::shared_ptr<Feature> feature;
    std::string ms2;
    std::string ms1_5;

    bool operator==(const FeatureSequenced& that) const
    {
        return *feature == *that.feature &&
	  ms2 == that.ms2 &&
	  ms1_5 == that.ms1_5;

    }
    
    bool operator!=(const FeatureSequenced& that) const { return !(*this == that); }

};

} // namespace eharmony
} // namespace pwiz

#endif
