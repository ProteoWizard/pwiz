//
// TraData.cpp
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#define PWIZ_SOURCE

#include "TraData.hpp"


namespace pwiz {
namespace tradata {


using namespace std;
using msdata::CVParamIs;
using msdata::CVParamIsChildOf;


PWIZ_API_DECL CVParam ParamContainer::cvParam(CVID cvid) const
{
    // first look in our own cvParams

    vector<CVParam>::const_iterator it = 
        find_if(cvParams.begin(), cvParams.end(), CVParamIs(cvid));
   
    if (it!=cvParams.end()) return *it;

    return CVParam();
}


PWIZ_API_DECL CVParam ParamContainer::cvParamChild(CVID cvid) const
{
    // first look in our own cvParams

    vector<CVParam>::const_iterator it = 
        find_if(cvParams.begin(), cvParams.end(), CVParamIsChildOf(cvid));
   
    if (it!=cvParams.end()) return *it;

    return CVParam();
}


PWIZ_API_DECL bool ParamContainer::hasCVParam(CVID cvid) const
{
    CVParam param = cvParam(cvid);
    return (param.cvid != CVID_Unknown);
}


PWIZ_API_DECL bool ParamContainer::hasCVParamChild(CVID cvid) const
{
    CVParam param = cvParamChild(cvid);
    return (param.cvid != CVID_Unknown);
}


namespace {
struct HasName
{
    string name_;
    HasName(const string& name) : name_(name) {}
    bool operator()(const UserParam& userParam) {return name_ == userParam.name;}
};
} // namespace


PWIZ_API_DECL UserParam ParamContainer::userParam(const string& name) const
{
    vector<UserParam>::const_iterator it = 
        find_if(userParams.begin(), userParams.end(), HasName(name));
    return it!=userParams.end() ? *it : UserParam();
}


PWIZ_API_DECL void ParamContainer::set(CVID cvid, const string& value, CVID units)
{
    vector<CVParam>::iterator it = find_if(cvParams.begin(), cvParams.end(), CVParamIs(cvid));
   
    if (it!=cvParams.end())
    {
        it->value = value;
        it->units = units;
        return;
    }

    cvParams.push_back(CVParam(cvid, value, units));
}


PWIZ_API_DECL bool ParamContainer::empty() const
{
    return cvParams.empty() && userParams.empty();
}


PWIZ_API_DECL void ParamContainer::clear()
{
    cvParams.clear();
    userParams.clear();
}


PWIZ_API_DECL bool ParamContainer::operator==(const ParamContainer& that) const
{
    return false;//!Diff<ParamContainer>(*this, that);
}


PWIZ_API_DECL bool ParamContainer::operator!=(const ParamContainer& that) const
{
    return !(*this == that);
}




PWIZ_API_DECL Contact::Contact(const string& id) : id(id) {}


PWIZ_API_DECL bool Contact::empty() const
{
    return id.empty() && ParamContainer::empty();
}




PWIZ_API_DECL bool Publication::empty() const
{
    return id.empty() && ParamContainer::empty();
}




PWIZ_API_DECL Software::Software(const string& _id) : id(_id) {}


PWIZ_API_DECL
Software::Software(const string& _id,
                   const CVParam& _param,
                   const string& _version)
:   id(_id), version(_version)
{
    cvParams.push_back(_param);    
}


PWIZ_API_DECL bool Software::empty() const
{
    return id.empty() && version.empty() && ParamContainer::empty();
}




PWIZ_API_DECL bool Evidence::empty() const
{
    return ParamContainer::empty();
}


PWIZ_API_DECL bool Prediction::empty() const
{
    return transitionSource.empty() && ParamContainer::empty() &&
           intensityRank == unsigned int() &&
           recommendedTransitionRank == unsigned int() &&
           relativeIntensity == double() &&
           (!contactPtr.get() || contactPtr->empty()) && 
           (!softwarePtr.get() || softwarePtr->empty());
}


PWIZ_API_DECL bool Validation::empty() const
{
    return transitionSource.empty() && ParamContainer::empty() &&
           intensityRank == unsigned int() &&
           recommendedTransitionRank == unsigned int() &&
           relativeIntensity == double();
}


PWIZ_API_DECL bool RetentionTime::empty() const
{
    return ParamContainer::empty() &&
           normalizationStandard.empty() &&
           localRetentionTime == double() &&
           normalizedRetentionTime == double() &&
           predictedRetentionTime == double();
}


PWIZ_API_DECL bool Interpretation::empty() const
{
    return productSeries.empty() &&
           productAdjustment.empty() &&
           ParamContainer::empty() &&
           productOrdinal == unsigned int() &&
           /* TODO: how do we test a bool? * primary == unsigned int() && */
           mzDelta == double();
}


PWIZ_API_DECL Instrument::Instrument(const string& id) : id(id) {}


PWIZ_API_DECL bool Instrument::empty() const
{
    return id.empty() && ParamContainer::empty();
}




PWIZ_API_DECL bool Configuration::empty() const
{
    return validations.empty() &&
           (!contactPtr.get() || contactPtr->empty()) && 
           (!instrumentPtr.get() || instrumentPtr->empty()) && 
           ParamContainer::empty();
}




PWIZ_API_DECL Protein::Protein(const string& id) : id(id) {}


PWIZ_API_DECL bool Protein::empty() const
{
    return sequence.empty() && ParamContainer::empty();
}




PWIZ_API_DECL Peptide::Peptide(const string& id) : id(id) {}


PWIZ_API_DECL bool Peptide::empty() const
{
    return evidence.empty() &&
           groupLabel.empty() &&
           id.empty() &&
           unmodifiedSequence.empty() &&
           modifiedSequence.empty() &&
           labelingCategory.empty() &&
           retentionTime.empty() &&
           (!proteinPtr.get() || proteinPtr->empty()) &&
           ParamContainer::empty();
}




PWIZ_API_DECL Compound::Compound(const string& id) : id(id) {}


PWIZ_API_DECL bool Compound::empty() const
{
    return id.empty() && retentionTime.empty() && ParamContainer::empty();
}




PWIZ_API_DECL bool Transition::empty() const
{
    return interpretationList.empty() &&
           configurationList.empty() &&
           (!peptidePtr.get() || peptidePtr->empty()) &&
           (!compoundPtr.get() || compoundPtr->empty()) &&
           name.empty();
}




PWIZ_API_DECL bool TraData::empty() const
{
    return version.empty() &&
           cvs.empty() &&
           contactPtrs.empty() &&
           publications.empty() &&
           instrumentPtrs.empty() &&
           softwarePtrs.empty() &&
           proteinPtrs.empty() &&
           peptidePtrs.empty() &&
           compoundPtrs.empty() &&
           transitions.empty();
}


} // namespace tradata
} // namespace pwiz
