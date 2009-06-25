///
/// FeatureSequenced.cpp
///

#include "FeatureSequenced.hpp"

using namespace pwiz;
using namespace eharmony;

FeatureSequenced::FeatureSequenced(FeaturePtr _feature) : ms2(""), ms1_5(""), peptideCount(0), feature(_feature){}

FeatureSequenced::FeatureSequenced(const FeatureSequenced& _fs) : feature(_fs.feature), ms2(_fs.ms2), ms1_5(_fs.ms1_5), calculatedMass(_fs.calculatedMass), ppProb(_fs.ppProb), peptideCount(_fs.peptideCount) 
{}

    
bool FeatureSequenced::operator==(const FeatureSequenced& that) const
{
    return *feature == *that.feature &&
            ms2 == that.ms2 &&
            ms1_5 == that.ms1_5;

}
    
bool FeatureSequenced::operator!=(const FeatureSequenced& that) const 
{ 
    return !(*this == that); 
}


