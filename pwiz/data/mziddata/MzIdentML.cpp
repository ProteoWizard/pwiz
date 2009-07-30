//
// MzIdentML.cpp
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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
#include <iterator>

namespace pwiz {
namespace mziddata {


using namespace std;
using namespace boost::logic;
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
// ExternalData
//

bool ExternalData::empty() const
{
    return IdentifiableType::empty() &&
        location.empty();
}


//
// BibliographicReference
//

PWIZ_API_DECL BibliographicReference::BibliographicReference() : year(0)
{
}

PWIZ_API_DECL bool BibliographicReference::empty() const
{
    return IdentifiableType::empty() &&
        authors.empty() &&
        publication.empty() &&
        publisher.empty() &&
        editor.empty() &&
        year != 0 &&
        volume.empty() &&
        issue.empty() &&
        pages.empty() &&
        title.empty();
}

//
// ContactRole
//

PWIZ_API_DECL bool ContactRole::empty() const
{
    return Contact_ref.empty() &&
        role.empty();
}

//
// Contact
//

PWIZ_API_DECL bool Contact::empty() const
{
    return address.empty() &&
        phone.empty() &&
        email.empty() &&
        fax.empty() &&
        tollFreePhone.empty();
}

//
// Affiliations
//

PWIZ_API_DECL bool Affiliations::empty() const
{
    return organization_ref.empty();
}

//
// Person
//

PWIZ_API_DECL bool Person::empty() const
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

PWIZ_API_DECL bool Organization::empty() const
{
    return Contact::empty() &&
        parent.organization_ref.empty();
}


//
// Modification
//

PWIZ_API_DECL Modification::Modification()
    : location(0),
      avgMassDelta(0),
      monoisotopicMassDelta(0)
{
}

PWIZ_API_DECL bool Modification::empty() const
{
    return location == 0 &&
        residues.empty() &&
        avgMassDelta == 0 &&
        monoisotopicMassDelta == 0 &&
        paramGroup.empty();
}

//
// Enzymes
//

PWIZ_API_DECL bool Enzymes::empty() const
{
    return independent.empty() &&
        enzymes.empty();
}

//
// MassTable
//


PWIZ_API_DECL bool MassTable::empty() const
{
    return id.empty() &&
        msLevel.empty() &&
        residues.empty() &&
        ambiguousResidue.empty();
}


//
// IonType
//

PWIZ_API_DECL IonType::IonType() : charge(0)
{
}


PWIZ_API_DECL bool IonType::empty() const
{
    return charge == 0 &&
        index.empty() &&
        paramGroup.empty() &&
        fragmentArray.empty();
}

//
// SpectrumIdentificationItem
//

PWIZ_API_DECL SpectrumIdentificationItem::SpectrumIdentificationItem()
    : chargeState(0),
    experimentalMassToCharge(0),
    calculatedMassToCharge(0),
    calculatedPI(0),
    rank(0),
    passThreshold(0) 
{
}

PWIZ_API_DECL bool SpectrumIdentificationItem::empty() const
{
    return chargeState == 0 &&
        experimentalMassToCharge == 0 &&
        calculatedMassToCharge == 0 &&
        calculatedPI == 0 &&
        Peptide_ref.empty() &&
        rank == 0 &&
        passThreshold == 0 &&
        MassTable_ref.empty() &&
        Sample_ref.empty() &&
        peptideEvidence.empty() &&
        fragmentation.empty() &&
        paramGroup.empty();
}



PWIZ_API_DECL bool ProteinDetectionList::empty() const
{
    return proteinAmbiguityGroup.empty() &&
        paramGroup.empty();
}

PWIZ_API_DECL ProteinDetectionHypothesis::ProteinDetectionHypothesis() : passThreshold(0) 
{
}

PWIZ_API_DECL bool ProteinDetectionHypothesis::empty() const
{
    return DBSequence_ref.empty() &&
        passThreshold == 0 &&
        peptideHypothesis.empty() &&
        paramGroup.empty();
}

//
// ProteinAmbiguityGroup
//
bool ProteinAmbiguityGroup::empty() const
{
    return proteinDetectionHypothesis.empty() &&
        paramGroup.empty();
}


//
// SpectrumIdentificationList
//

PWIZ_API_DECL SpectrumIdentificationList::SpectrumIdentificationList() :
    numSequencesSearched(0)
{
}

PWIZ_API_DECL bool SpectrumIdentificationList::empty() const
{
    return numSequencesSearched == 0 &&
        fragmentationTable.empty() &&
        spectrumIdentificationResult.empty();
}

//
// PeptideEvidence
//

PWIZ_API_DECL PeptideEvidence::PeptideEvidence()
    : start(0), end(0),
      frame(0), isDecoy(false),
      missedCleavages(0)
{
}


PWIZ_API_DECL bool PeptideEvidence::empty() const
{
    return DBSequence_ref.empty() &&
        start == 0 &&
        end == 0 &&
        pre.empty() &&
        post.empty() &&
        frame == 0 &&
        isDecoy == false &&
        missedCleavages == 0 &&
        paramGroup.empty();
}


//
// FragmentArray
//

PWIZ_API_DECL bool FragmentArray::empty() const
{
    return values.empty() &&
        Measure_ref.empty() ;
}

//
// Filter
//

PWIZ_API_DECL bool Filter::empty() const
{
    return filterType.empty() &&
        include.empty() &&
        exclude.empty();
}

//
// Residue
//

PWIZ_API_DECL Residue::Residue() :
    Mass(0)
{
}

PWIZ_API_DECL bool Residue::empty() const
{
    return Code.empty() &&
        Mass == 0;
}

//
// AmbiguousResidue
//

PWIZ_API_DECL bool AmbiguousResidue::empty() const
{
    return Code.empty() &&
        params.empty();
}


//
// Enzyme
//

PWIZ_API_DECL Enzyme::Enzyme()
    : semiSpecific(indeterminate)
{
}

PWIZ_API_DECL bool Enzyme::empty() const
{
    return id.empty() &&
        nTermGain.empty() &&
        cTermGain.empty() &&
        semiSpecific == indeterminate &&
        missedCleavages.empty() &&
        minDistance.empty() &&
        siteRegexp.empty() &&
        enzymeName.empty();
}


//
// FragmentArray
//

PWIZ_API_DECL FragmentArray& FragmentArray::setValues(const std::string& values)
{
    istringstream iss(values);

    this->values.clear();
    copy(istream_iterator<double>(iss), istream_iterator<double>(), back_inserter(this->values));

    return *this;
}

PWIZ_API_DECL FragmentArray& FragmentArray::setValues(const std::vector<double>& values)
{
    this->values.clear();
    copy(values.begin(), values.end(), back_inserter(this->values));
    
    return *this;
}

PWIZ_API_DECL string FragmentArray::getValues() const
{
    ostringstream oss;
    copy(values.begin(), values.end(), ostream_iterator<double>(oss, " "));

    return oss.str();
}


//
// IonType
//

IonType& IonType::setIndex(const string& value)
{
    istringstream iss(value);

    index.clear();
    copy(istream_iterator<double>(iss), istream_iterator<double>(), back_inserter(index));

    return *this;
}

IonType& IonType::setIndex(const vector<int>& value)
{
    index.clear();
    copy(value.begin(), value.end(), back_inserter(index));
    
    return *this;
}

string IonType::getIndex() const
{
    ostringstream oss;
    copy(index.begin(), index.end(), ostream_iterator<int>(oss, " "));

    return oss.str();
}

//
// Material
//

bool Material::empty() const
{
    return contactRole.empty() &&
        cvParams.empty();
}

//
// Measure
//

PWIZ_API_DECL bool Measure::empty() const
{
    return paramGroup.empty();
}


//
// Sample
//

bool Sample::empty() const
{
    return Material::empty() &&
        subSamples.empty();
}


//
// SubstitutionModification
//

PWIZ_API_DECL SubstitutionModification::SubstitutionModification() :
    location(0),
    avgMassDelta(0),
    monoisotopicMassDelta(0)
{
}


PWIZ_API_DECL bool SubstitutionModification::empty() const
{
    return originalResidue.empty() &&
        replacementResidue.empty() &&
        location == 0 &&
        avgMassDelta == 0 &&
        monoisotopicMassDelta == 0;
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

bool AnalysisCollection::empty() const
{
    return spectrumIdentification.empty() &&
        proteinDetection.empty();
}

//
// ModParam
//

ModParam::ModParam()
    : massDelta(0)
{
}

bool ModParam::empty() const
{
    return massDelta == 0 &&
        residues.empty() &&
        cvParams.empty();
}


//
// SearchModification
//

SearchModification::SearchModification()
    : fixedMod(false)
{
}


bool SearchModification::empty() const
{
    return modParam.empty() &&
        specificityRules.empty();
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

bool AnalysisProtocolCollection::empty() const
{
    return spectrumIdentificationProtocol.empty() &&
        proteinDetectionProtocol.empty();
}

bool Sample::subSample::empty() const
{
    return Sample_ref.empty();
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
// SearchDatabase
//

PWIZ_API_DECL SearchDatabase::SearchDatabase()
{
    numDatabaseSequences = 0;
    numResidues = 0;
}

PWIZ_API_DECL bool SearchDatabase::empty() const
{
    return version.empty() &&
        releaseDate.empty() &&
        numDatabaseSequences == 0 &&
        numResidues == 0;
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

