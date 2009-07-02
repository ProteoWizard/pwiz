//
// MzIdentML.cpp
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#include "MzIdentML.hpp"


namespace pwiz {
namespace mziddata {


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


//
// IdentifiableType
//

bool IdentifiableType::empty() const
{
    return id.empty() &&
        name.empty();
}

//
// BibliographicReference
//

bool BibliographicReference::empty() const
{
    return authors.empty() &&
        publication.empty() &&
        publisher.empty() &&
        editor.empty() &&
     // int year;
        volume.empty() &&
        issue.empty() &&
        pages.empty() &&
        title.empty();
}

//
// ContactRole
//

bool ContactRole::empty() const
{
    return Contact_ref.empty() &&
        role.empty();
}

//
// Contact
//

bool Contact::empty() const
{
    return address.empty() &&
        phone.empty() &&
        email.empty() &&
        fax.empty() &&
        tollFreePhone.empty();
}

//
// Person
//

bool Person::empty() const
{
    return Contact::empty() &&
        lastName.empty() &&
        firstName.empty() &&
        midInitials.empty() &&
        affiliations.empty();
}

//
// Organization
//

bool Organization::empty() const
{
    return Contact::empty() &&
        parent.organization_ref.empty();
}


//
// SequenceCollection
//

bool SequenceCollection::empty() const
{
    return dbSequences.empty() &&
        peptides.empty();
}

//
// AnalysisSoftware
//

bool AnalysisSoftware::empty() const
{
    return IdentifiableType::empty() &&
        version.empty() &&
        contactRole.empty() &&
        softwareName.empty() &&
        URI.empty() &&
        customizations.empty();
}

//
// Analysis
//

bool Analysis::empty() const
{
    return spectrumIdentification.empty() &&
        (!proteinDetection.get() || proteinDetection->empty());
}

//
// ProteinDetection
//

bool ProteinDetection::empty() const
{
    return ProteinDetectionProtocol_ref.empty() &&
        ProteinDetectionList_ref.empty() &&
        activityDate.empty() &&
        inputSpectrumIdentifications.empty();
}

//
// AnalysisProtocol
//

bool AnalysisProtocol::empty() const
{
    return spectrumIdentificationProtocol.empty() &&
        proteinDetectionProtocol.empty();
}

//
// AnalysisSampleCollection
//

bool AnalysisSampleCollection::empty() const
{
    return samples.empty();
}

//
// Inputs
//

bool Inputs::empty() const
{
    return sourceFile.empty() &&
        searchDatabase.empty() &&
        spectraData.empty();
}

//
// DataCollection
//

bool AnalysisData::empty() const
{
    return spectrumIdentificationList.empty() &&
        proteinDetectionList.empty();
}

//
// DataCollection
//

bool DataCollection::empty() const
{
    return inputs.empty() &&
        analysisData.empty();
}

//
// MzIdentML
//
bool MzIdentML::empty() const
{
    return IdentifiableType::empty() &&
        version.empty() &&
        cvs.empty() &&
        provider.empty() &&
        auditCollection.empty() &&
        analysisSampleCollection.empty() &&
        sequenceCollection.empty() &&
        analysisCollection.empty() &&
        analysisProtocolCollection.empty() &&
        dataCollection.empty() &&
        bibliographicReference.empty();
}

} // namespace pwiz
} // namespace mziddata

