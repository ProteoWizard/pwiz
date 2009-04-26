///
/// FeatureSequenced.hpp
///

#ifndef _FEATURESEQUENCED_HPP_
#define _FEATURESEQUENCED_HPP_

#include "pwiz/data/misc/PeakData.hpp"

namespace pwiz{
namespace eharmony{

using namespace pwiz::data::peakdata;

struct PWIZ_API_DECL FeatureSequenced
{

    FeatureSequenced() : ms2(""), ms1_5("") {}
    FeatureSequenced(const Feature& _feature) : feature(_feature), ms2(""), ms1_5("") {}
    FeatureSequenced(const FeatureSequenced& _fs) : feature(_fs.feature), ms2(_fs.ms2), ms1_5(_fs.ms1_5) {}

    Feature feature;
    std::string ms2;
    std::string ms1_5;

    bool operator==(const FeatureSequenced& that)
    {
        return feature == that.feature &&
	  ms2 == that.ms2 &&
	  ms1_5 == that.ms1_5;

    }
};

} // namespace eharmony
} // namespace pwiz

#endif
