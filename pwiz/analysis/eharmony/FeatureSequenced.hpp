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

    FeatureSequenced() : ms2(""), ms1_5(""), calculatedMass(0), ppProb(0), peptideCount(0) { feature = FeaturePtr(new Feature());}
    FeatureSequenced(FeaturePtr _feature);
    FeatureSequenced(const FeatureSequenced& _fs);

    FeaturePtr feature;
    std::string ms2;
    std::string ms1_5;
    double calculatedMass;
    double ppProb;
    size_t peptideCount;

    bool operator==(const FeatureSequenced& that) const;    
    bool operator!=(const FeatureSequenced& that) const;

private:

    // no copying
    
    FeatureSequenced(FeatureSequenced&);
    FeatureSequenced& operator=(FeatureSequenced&);

};

} // namespace eharmony
} // namespace pwiz

#endif
