//
// $Id$
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
#include "boost/date_time/gregorian/gregorian.hpp"
#include <iterator>
#include <iostream>

namespace pwiz {
namespace mziddata {


using namespace std;
using namespace boost::logic;
using namespace boost::gregorian;
using namespace pwiz::cv;
using namespace pwiz::data;


PWIZ_API_DECL vector<CV> defaultCVList()
{
    vector<CV> result;
    result.resize(3);

    result[0] = cv::cv("MS");
    result[1] = cv::cv("UNIMOD");
    result[2] = cv::cv("UO");
    
    return result;
}


//
// IdentifiableType
//

PWIZ_API_DECL IdentifiableType::IdentifiableType(const std::string& id_,
                                   const std::string& name_)
    : id(id_), name(name_)
{
}

PWIZ_API_DECL bool IdentifiableType::empty() const
{
    return id.empty() &&
        name.empty();
}


//
// ExternalData
//

PWIZ_API_DECL ExternalData::ExternalData(const std::string id_,
                                         const std::string name_)
    : IdentifiableType(id_, name_)
{
}


PWIZ_API_DECL bool ExternalData::empty() const
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
        year == 0 &&
        volume.empty() &&
        issue.empty() &&
        pages.empty() &&
        title.empty();
}

//
// ContactRole
//

PWIZ_API_DECL ContactRole::ContactRole()
    : contactPtr(ContactPtr(new Contact()))
{
}

PWIZ_API_DECL bool ContactRole::empty() const
{
    return (!contactPtr.get()  || contactPtr->empty()) &&
        role.empty();
}

//
// Contact
//

PWIZ_API_DECL Contact::Contact(const string& id_,
                               const string& name_)
    : IdentifiableType(id_, name_)
{
}


PWIZ_API_DECL bool Contact::empty() const
{
    return IdentifiableType::empty() &&
        address.empty() &&
        phone.empty() &&
        email.empty() &&
        fax.empty() &&
        tollFreePhone.empty() &&
        params.empty();
}

//
// Affiliations
//

PWIZ_API_DECL Affiliations::Affiliations(const string& id_)
    : organizationPtr(OrganizationPtr(new Organization(id_)))
{
}


PWIZ_API_DECL bool Affiliations::empty() const
{
    return !organizationPtr.get() || organizationPtr->empty();
}

//
// Person
//

PWIZ_API_DECL Person::Person(const std::string& id_,
           const std::string& name_)
    : Contact(id_, name_)
{
}

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

PWIZ_API_DECL Organization::Organization(const string& id_,
                                         const string& name_)
    : Contact(id_, name_)
{
}


PWIZ_API_DECL bool Organization::empty() const
{
    return Contact::empty() &&
        !parent.organizationPtr.get();
}


PWIZ_API_DECL Organization::Parent::Parent()
{
}

PWIZ_API_DECL bool Organization::Parent::empty() const
{
    return !organizationPtr.get() || organizationPtr->empty();
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

PWIZ_API_DECL MassTable::MassTable(const string id)
    : id(id)
{
}


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

PWIZ_API_DECL SpectrumIdentificationItem::SpectrumIdentificationItem(
    const string& id, const string& name)
    : IdentifiableType(id, name),
      chargeState(0),
      experimentalMassToCharge(0),
      calculatedMassToCharge(0),
      calculatedPI(0),
      rank(0),
      passThreshold(0) 
{
}

PWIZ_API_DECL bool SpectrumIdentificationItem::empty() const
{
    return IdentifiableType::empty() &&
        chargeState == 0 &&
        experimentalMassToCharge == 0 &&
        calculatedMassToCharge == 0 &&
        calculatedPI == 0 &&
        (!peptidePtr.get() || peptidePtr->empty()) &&
        rank == 0 &&
        passThreshold == 0 &&
        (!massTablePtr.get() || massTablePtr->empty()) &&
        (!samplePtr.get() || samplePtr->empty()) &&
        peptideEvidence.empty() &&
        fragmentation.empty() &&
        paramGroup.empty();
}


//
// SpectrumIdentificationResult
//

PWIZ_API_DECL bool SpectrumIdentificationResult::empty() const
{
    return IdentifiableType::empty() &&
        spectrumID.empty() &&
        (!spectraDataPtr.get() || spectraDataPtr->empty()) &&
        spectrumIdentificationItem.empty() &&
        paramGroup.empty();
}


//
// SpectrumIdentification
//

PWIZ_API_DECL SpectrumIdentification::SpectrumIdentification(
    const std::string& id_,const std::string& name_)
    : IdentifiableType(id_, name_)
{
}


PWIZ_API_DECL bool SpectrumIdentification::empty() const
{
    return IdentifiableType::empty() &&
        (!spectrumIdentificationProtocolPtr.get() || spectrumIdentificationProtocolPtr->empty()) &&
        (!spectrumIdentificationListPtr.get() || spectrumIdentificationListPtr->empty()) &&
        activityDate.empty() &&
        inputSpectra.empty() &&
        searchDatabase.empty();
}

//
// ProteinDetectionList
//

PWIZ_API_DECL ProteinDetectionList::ProteinDetectionList(
    const std::string& id_,const std::string& name_)
    : IdentifiableType(id_, name_)
{
}
    

PWIZ_API_DECL bool ProteinDetectionList::empty() const
{
    return IdentifiableType::empty() &&
        proteinAmbiguityGroup.empty() &&
        paramGroup.empty();
}

//
// ProteinDetectionHypothesis
//

PWIZ_API_DECL ProteinDetectionHypothesis::ProteinDetectionHypothesis() : passThreshold(0) 
{
}

PWIZ_API_DECL bool ProteinDetectionHypothesis::empty() const
{
    return (!dbSequencePtr.get() || dbSequencePtr->empty()) &&
        passThreshold == 0 &&
        peptideHypothesis.empty() &&
        paramGroup.empty();
}

//
// ProteinAmbiguityGroup
//

PWIZ_API_DECL bool ProteinAmbiguityGroup::empty() const
{
    return proteinDetectionHypothesis.empty() &&
        paramGroup.empty();
}


//
// Provider
//

PWIZ_API_DECL Provider::Provider(const std::string id_,
                                 const std::string name_)
    : IdentifiableType(id_, name_)
{
}


PWIZ_API_DECL bool Provider::empty() const
{
    return IdentifiableType::empty() &&
        contactRole.empty();
}


//
// SpectrumIdentificationList
//

PWIZ_API_DECL SpectrumIdentificationList::SpectrumIdentificationList(
    const string& id_, const string& name_)
    : IdentifiableType(id_, name_), numSequencesSearched(0)
{
}

PWIZ_API_DECL bool SpectrumIdentificationList::empty() const
{
    return IdentifiableType::empty() &&
        numSequencesSearched == 0 &&
        fragmentationTable.empty() &&
        spectrumIdentificationResult.empty();
}


//
// ProteinDetectionProtocol
//

PWIZ_API_DECL ProteinDetectionProtocol::ProteinDetectionProtocol(
    const std::string& id_, const std::string& name_)
    : IdentifiableType(id_, name_)
{
}

PWIZ_API_DECL bool ProteinDetectionProtocol::empty() const
{
    return IdentifiableType::empty() &&
        (!analysisSoftwarePtr.get() || analysisSoftwarePtr->empty()) &&
        analysisParams.empty() &&
        threshold.empty();
}

//
// PeptideEvidence
//

PWIZ_API_DECL PeptideEvidence::PeptideEvidence(const string& id,
                                               const string& name)
    : IdentifiableType(id, name),
      start(0), end(0),
      frame(0), isDecoy(false),
      missedCleavages(0)
{
}


PWIZ_API_DECL bool PeptideEvidence::empty() const
{
    return IdentifiableType::empty() &&
        (!dbSequencePtr.get() || dbSequencePtr->empty()) &&
        start == 0 &&
        end == 0 &&
        pre.empty() &&
        post.empty() &&
        (!translationTablePtr.get() || translationTablePtr->empty()) &&
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
        (!measurePtr.get() || measurePtr->empty()) &&
        params.empty();
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
// TranslationTable
//

PWIZ_API_DECL TranslationTable::TranslationTable(const std::string& id,
                                                 const std::string& name)
    : IdentifiableType(id, name)
{
}

PWIZ_API_DECL bool TranslationTable::empty() const
{
    return IdentifiableType::empty() &&
        params.empty();
}


//
// DatabaseTranslation
//

PWIZ_API_DECL DatabaseTranslation& DatabaseTranslation::setFrames(const std::string& values)
{
    istringstream iss(values);

    this->frames.clear();
    copy(istream_iterator<double>(iss), istream_iterator<double>(), back_inserter(this->frames));

    return *this;
}

PWIZ_API_DECL DatabaseTranslation& DatabaseTranslation::setFrames(const std::vector<int>& values)
{
    this->frames.clear();
    copy(values.begin(), values.end(), back_inserter(this->frames));
    
    return *this;
}


PWIZ_API_DECL string DatabaseTranslation::getFrames() const
{
    ostringstream oss;
    copy(frames.begin(), frames.end(), ostream_iterator<double>(oss, " "));

    return oss.str();

}


PWIZ_API_DECL bool DatabaseTranslation::empty() const
{
    return frames.empty() &&
        translationTable.empty();
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

PWIZ_API_DECL Enzyme::Enzyme(const string id)
    : id(id), semiSpecific(indeterminate), missedCleavages(0),
      minDistance(0)

{
}

PWIZ_API_DECL bool Enzyme::empty() const
{
    return id.empty() &&
        nTermGain.empty() &&
        cTermGain.empty() &&
        (indeterminate(semiSpecific) || semiSpecific== false) &&
        missedCleavages == 0 &&
        minDistance == 0 &&
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

PWIZ_API_DECL IonType& IonType::setIndex(const string& value)
{
    istringstream iss(value);

    index.clear();
    copy(istream_iterator<double>(iss), istream_iterator<double>(), back_inserter(index));

    return *this;
}

PWIZ_API_DECL IonType& IonType::setIndex(const vector<int>& value)
{
    index.clear();
    copy(value.begin(), value.end(), back_inserter(index));
    
    return *this;
}

PWIZ_API_DECL string IonType::getIndex() const
{
    ostringstream oss;
    copy(index.begin(), index.end(), ostream_iterator<int>(oss, " "));

    return oss.str();
}

//
// Material
//

PWIZ_API_DECL Material::Material(const std::string& id_,
                                 const std::string& name_)
    : IdentifiableType(id_, name_)
{
}

PWIZ_API_DECL bool Material::empty() const
{
    return contactRole.empty() &&
        cvParams.empty();
}

//
// Measure
//

PWIZ_API_DECL Measure::Measure(const string id, const string name)
    : IdentifiableType(id, name)
{
}
    

PWIZ_API_DECL bool Measure::empty() const
{
    return IdentifiableType::empty() &&
        paramGroup.empty();
}


//
// Sample
//

PWIZ_API_DECL Sample::Sample(const std::string& id_,
                             const std::string& name_)
    : Material(id_, name_)
{
}

PWIZ_API_DECL bool Sample::empty() const
{
    return Material::empty() &&
        subSamples.empty();
}

//
// subSample
//

PWIZ_API_DECL Sample::subSample::subSample(const std::string& id_,
                                           const std::string& name_)
    : samplePtr(SamplePtr(new Sample(id_, name_)))
{}

PWIZ_API_DECL bool Sample::subSample::empty() const
{
    return !samplePtr.get();
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
// Peptide
//

PWIZ_API_DECL Peptide::Peptide(const std::string& id, const std::string& name)
    : IdentifiableType(id, name)
{
}


PWIZ_API_DECL bool Peptide::empty() const
{
    return IdentifiableType::empty() &&
        peptideSequence.empty() &&
        modification.empty() &&
        substitutionModification.empty() &&
        paramGroup.empty();
}

//
// SequenceCollection
//

PWIZ_API_DECL bool SequenceCollection::empty() const
{
    return dbSequences.empty() &&
        peptides.empty();
}


//
// AnalysisSoftware
//

PWIZ_API_DECL AnalysisSoftware::AnalysisSoftware(const std::string& id_,
                                                 const std::string& name_)
    : IdentifiableType(id_, name_)
{
}

PWIZ_API_DECL bool AnalysisSoftware::empty() const
{
    return IdentifiableType::empty() &&
        version.empty() &&
        (!contactRolePtr.get() || contactRolePtr->empty())&&
        softwareName.empty() &&
        URI.empty() &&
        customizations.empty();
}


//
// DBSequence
//

PWIZ_API_DECL DBSequence::DBSequence(const std::string id_,
                                     const std::string name_)
    : IdentifiableType(id_, name_), length(0)
{
}

PWIZ_API_DECL bool DBSequence::empty() const
{
    return IdentifiableType::empty() &&
        length == 0 &&
        accession.empty() &&
        (!searchDatabasePtr.get() || searchDatabasePtr->empty()) &&
        seq.empty() &&
        paramGroup.empty();
}

//
// Analysis
//

PWIZ_API_DECL bool AnalysisCollection::empty() const
{
    return spectrumIdentification.empty() &&
        proteinDetection.empty();
}

//
// ModParam
//

PWIZ_API_DECL ModParam::ModParam()
    : massDelta(0)
{
}

PWIZ_API_DECL bool ModParam::empty() const
{
    return massDelta == 0 &&
        residues.empty() &&
        cvParams.empty();
}


//
// SearchModification
//

PWIZ_API_DECL SearchModification::SearchModification()
    : fixedMod(false)
{
}


PWIZ_API_DECL bool SearchModification::empty() const
{
    return modParam.empty() &&
        specificityRules.empty();
}

//
// ProteinDetection
//

PWIZ_API_DECL ProteinDetection::ProteinDetection(const std::string id_,
                                                 const std::string name_)
    : IdentifiableType(id_, name_)
{
}


PWIZ_API_DECL bool ProteinDetection::empty() const
{
    return IdentifiableType::empty() &&
        (!proteinDetectionProtocolPtr.get() || proteinDetectionProtocolPtr->empty()) &&
        (!proteinDetectionListPtr.get() || proteinDetectionListPtr->empty()) &&
        activityDate.empty() &&
        inputSpectrumIdentifications.empty();
}


//
// SpectrumIdentificationProtocol
//

PWIZ_API_DECL SpectrumIdentificationProtocol::SpectrumIdentificationProtocol(
    const std::string& id_, const std::string& name_)
    : IdentifiableType(id_, name_)
{
}


PWIZ_API_DECL bool SpectrumIdentificationProtocol::empty() const
{
    return IdentifiableType::empty() &&
        (!analysisSoftwarePtr.get() || analysisSoftwarePtr->empty()) &&
        searchType.empty() &&
        additionalSearchParams.empty() &&
        modificationParams.empty() &&
        enzymes.empty() &&
        massTable.empty() &&
        fragmentTolerance.empty() &&
        parentTolerance.empty() &&
        threshold.empty() &&
        databaseFilters. empty();
}

//
// AnalysisProtocol
//

PWIZ_API_DECL bool AnalysisProtocolCollection::empty() const
{
    return spectrumIdentificationProtocol.empty() &&
        proteinDetectionProtocol.empty();
}

//
// AnalysisSampleCollection
//

PWIZ_API_DECL bool AnalysisSampleCollection::empty() const
{
    return samples.empty();
}

//
// SpectraData
//
PWIZ_API_DECL SpectraData::SpectraData(const string id, const string name)
    : IdentifiableType(id, name)
{
}

PWIZ_API_DECL bool SpectraData::empty() const
{
    return location.empty() &&
        externalFormatDocumentation.empty() &&
        fileFormat.empty() &&
        spectrumIDFormat.empty();
}

//
// Inputs
//

PWIZ_API_DECL bool Inputs::empty() const
{
    return sourceFile.empty() &&
        searchDatabase.empty() &&
        spectraData.empty();
}

//
// SearchDatabase
//

PWIZ_API_DECL SearchDatabase::SearchDatabase(const std::string& id_,
                                             const std::string& name_)
    : ExternalData(id_, name_)
{
    numDatabaseSequences = 0;
    numResidues = 0;
}

PWIZ_API_DECL bool SearchDatabase::empty() const
{
    return ExternalData::empty() &&
        version.empty() &&
        releaseDate.empty() &&
        numDatabaseSequences == 0 &&
        numResidues == 0 &&
        fileFormat.empty() &&
        DatabaseName.empty() &&
        params.empty();
}

//
// SourceFile
//

PWIZ_API_DECL bool SourceFile::empty() const
{
    return location.empty() &&
        fileFormat.empty() &&
        externalFormatDocumentation.empty() &&
        paramGroup.empty();
}

//
// DataCollection
//

PWIZ_API_DECL bool AnalysisData::empty() const
{
    return spectrumIdentificationList.empty() &&
        (!proteinDetectionListPtr.get() || proteinDetectionListPtr->empty());
}

//
// DataCollection
//

PWIZ_API_DECL bool DataCollection::empty() const
{
    return inputs.empty() &&
        analysisData.empty();
}

//
// MzIdentML
//

PWIZ_API_DECL MzIdentML::MzIdentML(const std::string& id_,
                                   const std::string& creationDate_)
    : IdentifiableType(id_), creationDate(creationDate_), version_("1.0.0")
{
#ifdef _MSC_VER
    const char* format = "%Y-%m-%dT%X";
#else
    const char* format = "%Y-%m-%dT%T";
#endif
    
    if (creationDate.empty())
    {
        date d(day_clock::local_day());
        date_facet* facet(new date_facet(format));
        ostringstream out;
        out.imbue(std::locale(out.getloc(), facet));
        out << d;
        creationDate = out.str();
    }
}

PWIZ_API_DECL bool MzIdentML::empty() const
{
    return IdentifiableType::empty() &&
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

PWIZ_API_DECL const string& MzIdentML::version() const
{
    return version_;
}

} // namespace pwiz
} // namespace mziddata

