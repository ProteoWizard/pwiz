//
// $Id$
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
#include "pwiz/utility/misc/Std.hpp"
namespace pwiz {
namespace tradata {


using namespace pwiz::cv;


PWIZ_API_DECL vector<CV> defaultCVList()
{
    vector<CV> result;
    result.resize(3);

    result[0] = cv::cv("MS");
    result[1] = cv::cv("UNIMOD");
    result[2] = cv::cv("UO");
    
    return result;
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
    return ParamContainer::empty() &&
           (!contactPtr.get() || contactPtr->empty()) && 
           (!softwarePtr.get() || softwarePtr->empty());
}


PWIZ_API_DECL bool Validation::empty() const
{
    return ParamContainer::empty();
}


PWIZ_API_DECL bool RetentionTime::empty() const
{
    return ParamContainer::empty();
}


PWIZ_API_DECL bool Interpretation::empty() const
{
    return ParamContainer::empty();
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




PWIZ_API_DECL Modification::Modification()
: location(), monoisotopicMassDelta(), averageMassDelta()
{}


PWIZ_API_DECL bool Modification::empty() const
{
    return ParamContainer::empty() &&
           location == int() &&
           monoisotopicMassDelta == double() &&
           averageMassDelta == double();
}




PWIZ_API_DECL bool Precursor::empty() const
{
    return ParamContainer::empty();
}


PWIZ_API_DECL bool Product::empty() const
{
    return ParamContainer::empty();
}




PWIZ_API_DECL Peptide::Peptide(const string& id) : id(id) {}


PWIZ_API_DECL bool Peptide::empty() const
{
    return evidence.empty() &&
           id.empty() &&
           sequence.empty() &&
           modifications.empty() &&
           retentionTimes.empty() &&
           proteinPtrs.empty() &&
           ParamContainer::empty();
}




PWIZ_API_DECL Compound::Compound(const string& id) : id(id) {}


PWIZ_API_DECL bool Compound::empty() const
{
    return id.empty() && retentionTimes.empty() && ParamContainer::empty();
}




PWIZ_API_DECL bool Transition::empty() const
{
    return ParamContainer::empty() &&
           precursor.empty() &&
           product.empty() &&
           retentionTime.empty() &&
           interpretationList.empty() &&
           configurationList.empty() &&
           (!peptidePtr.get() || peptidePtr->empty()) &&
           (!compoundPtr.get() || compoundPtr->empty()) &&
           id.empty();
}


PWIZ_API_DECL bool Target::empty() const
{
    return ParamContainer::empty() &&
           precursor.empty() &&
           retentionTime.empty() &&
           configurationList.empty() &&
           (!peptidePtr.get() || peptidePtr->empty()) &&
           (!compoundPtr.get() || compoundPtr->empty()) &&
           id.empty();
}


PWIZ_API_DECL bool TargetList::empty() const
{
    return targetExcludeList.empty() &&
           targetIncludeList.empty() &&
           ParamContainer::empty();
}




PWIZ_API_DECL TraData::TraData() : version_("0.9.4") {}
PWIZ_API_DECL TraData::~TraData() {}


PWIZ_API_DECL bool TraData::empty() const
{
    return id.empty() &&
           cvs.empty() &&
           contactPtrs.empty() &&
           publications.empty() &&
           instrumentPtrs.empty() &&
           softwarePtrs.empty() &&
           proteinPtrs.empty() &&
           peptidePtrs.empty() &&
           compoundPtrs.empty() &&
           transitions.empty() &&
           targets.empty();
}


PWIZ_API_DECL const string& TraData::version() const
{
    return version_;
}


} // namespace tradata
} // namespace pwiz
