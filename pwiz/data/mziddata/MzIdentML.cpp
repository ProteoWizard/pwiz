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

#include "pwiz/utility/misc/Std.hpp"
#include "MzIdentML.hpp"
#include "boost/date_time/gregorian/gregorian.hpp"
#include "boost/regex.hpp"


namespace pwiz {
namespace mziddata {


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
// Identifiable
//

PWIZ_API_DECL Identifiable::Identifiable(const std::string& id_,
                                   const std::string& name_)
    : id(id_), name(name_)
{
}

PWIZ_API_DECL bool Identifiable::empty() const
{
    return id.empty() &&
        name.empty();
}


//
// ExternalData
//

PWIZ_API_DECL ExternalData::ExternalData(const std::string id_,
                                         const std::string name_)
    : Identifiable(id_, name_)
{
}


PWIZ_API_DECL bool ExternalData::empty() const
{
    return Identifiable::empty() &&
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
    return Identifiable::empty() &&
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
           CVParam::empty();
}

//
// Contact
//

PWIZ_API_DECL Contact::Contact(const string& id_,
                               const string& name_)
    : Identifiable(id_, name_)
{
}


PWIZ_API_DECL bool Contact::empty() const
{
    return Identifiable::empty() &&
        address.empty() &&
        phone.empty() &&
        email.empty() &&
        fax.empty() &&
        tollFreePhone.empty() &&
        ParamContainer::empty();
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
        ParamContainer::empty();
}

//
// Enzymes
//

PWIZ_API_DECL bool Enzymes::empty() const
{
    return indeterminate(independent) &&
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
        ParamContainer::empty() &&
        fragmentArray.empty();
}

//
// SpectrumIdentificationItem
//

PWIZ_API_DECL SpectrumIdentificationItem::SpectrumIdentificationItem(
    const string& id, const string& name)
    : Identifiable(id, name),
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
    return Identifiable::empty() &&
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
        ParamContainer::empty();
}

PWIZ_API_DECL proteome::DigestedPeptide SpectrumIdentificationItem::digestedPeptide(const SpectrumIdentificationProtocol& sip, const PeptideEvidence& peptideEvidence) const
{
    using namespace proteome;

    if (!peptidePtr.get() || peptidePtr->empty())
        throw runtime_error("[SpectrumIdentificationItem::digestedPeptide] null or empty Peptide reference");
    if (peptideEvidence.empty())
        throw runtime_error("[SpectrumIdentificationItem::digestedPeptide] null or empty PeptideEvidence element");

    const Peptide& peptide = *peptidePtr;

    vector<boost::regex> cleavageAgentRegexes;
    BOOST_FOREACH(const EnzymePtr& enzymePtr, sip.enzymes.enzymes)
    {
        const Enzyme& enzyme = *enzymePtr;
        string regex = enzyme.siteRegexp;
        if (regex.empty())
        {
            CVParam enzymeTerm = enzyme.enzymeName.cvParamChild(MS_cleavage_agent_name);

            if (enzymeTerm.empty())
                enzymeTerm = CVParam(Digestion::getCleavageAgentByName(enzyme.enzymeName.userParams[0].name));

            try {regex = Digestion::getCleavageAgentRegex(enzymeTerm.cvid);} catch (...) {}
        }

        if (!regex.empty())
            cleavageAgentRegexes.push_back(boost::regex(regex));
    }

    if (cleavageAgentRegexes.empty())
        throw runtime_error("[SpectrumIdentificationItem::digestedPeptide] unknown cleavage agent");

    const PeptideEvidence& pe = peptideEvidence;

    if (pe.pre.empty() || pe.pre == "?" || pe.post.empty() || pe.post == "?")
        throw runtime_error("[SpectrumIdentificationItem::digestedPeptide] invalid pre/post on PeptideEvidence element");

    string peptideSequenceInContext = peptide.peptideSequence;
    if (pe.pre != "-") peptideSequenceInContext = pe.pre + peptideSequenceInContext;
    if (pe.post != "-") peptideSequenceInContext += pe.post;

    int nTerminusIsSpecific = pe.pre == "-" ? 1 : 0;
    int cTerminusIsSpecific = pe.post == "-" ? 1 : 0;

    int bestSpecificity = -1;
    boost::shared_ptr<DigestedPeptide> bestResult;

    BOOST_FOREACH(const boost::regex& regex, cleavageAgentRegexes)
    {
        Digestion::Config config;
        config.minimumSpecificity = Digestion::NonSpecific;
        Digestion peptideInContext(peptideSequenceInContext, regex, config);

        // if enzymes are independent, both termini of a peptide must be cleaved by the same enzyme
        if (sip.enzymes.independent)
        {
            nTerminusIsSpecific = pe.pre == "-" ? 1 : 0;
            cTerminusIsSpecific = pe.post == "-" ? 1 : 0;
        }

        try
        {
            DigestedPeptide result = peptideInContext.find_first(peptide.peptideSequence);
            nTerminusIsSpecific |= result.NTerminusIsSpecific() ? 1 : 0;
            cTerminusIsSpecific |= result.CTerminusIsSpecific() ? 1 : 0;

            if (nTerminusIsSpecific + cTerminusIsSpecific > bestSpecificity)
            {
                bestSpecificity = nTerminusIsSpecific + cTerminusIsSpecific;
                bestResult.reset(new DigestedPeptide(result,
                                                     pe.start-1, // offset is 0-based
                                                     result.missedCleavages(),
                                                     nTerminusIsSpecific == 1,
                                                     cTerminusIsSpecific == 1,
                                                     pe.pre,
                                                     pe.post));
            }
        }
        catch (runtime_error&)
        {}
    }

    if (!bestResult.get())
        throw runtime_error("[SpectrumIdentificationItem::digestedPeptide] invalid PeptideEvidence element");
    return *bestResult;
}

PWIZ_API_DECL vector<proteome::DigestedPeptide> SpectrumIdentificationItem::digestedPeptides(const SpectrumIdentificationProtocol& sip) const
{
    using namespace proteome;

    if (!peptidePtr.get() || peptidePtr->empty())
        throw runtime_error("[SpectrumIdentificationItem::digestedPeptides] null or empty Peptide reference");
    if (peptideEvidence.empty())
        throw runtime_error("[SpectrumIdentificationItem::digestedPeptides] no PeptideEvidence elements");

    const Peptide& peptide = *peptidePtr;

    vector<boost::regex> cleavageAgentRegexes;
    BOOST_FOREACH(const EnzymePtr& enzymePtr, sip.enzymes.enzymes)
    {
        const Enzyme& enzyme = *enzymePtr;
        string regex = enzyme.siteRegexp;
        if (regex.empty())
        {
            CVParam enzymeTerm = enzyme.enzymeName.cvParamChild(MS_cleavage_agent_name);

            if (enzymeTerm.empty())
                enzymeTerm = CVParam(Digestion::getCleavageAgentByName(enzyme.enzymeName.userParams[0].name));

            try {regex = Digestion::getCleavageAgentRegex(enzymeTerm.cvid);} catch (...) {}
        }

        if (!regex.empty())
            cleavageAgentRegexes.push_back(boost::regex(regex));
    }

    if (cleavageAgentRegexes.empty())
        throw runtime_error("[SpectrumIdentificationItem::digestedPeptide] unknown cleavage agent");

    vector<proteome::DigestedPeptide> results;

    BOOST_FOREACH(const PeptideEvidencePtr& pePtr, peptideEvidence)
    {
        const PeptideEvidence& pe = *pePtr;

        if (pe.pre.empty() || pe.pre == "?" || pe.post.empty() || pe.post == "?")
            continue;

        string peptideSequenceInContext = peptide.peptideSequence;
        if (pe.pre != "-") peptideSequenceInContext = pe.pre + peptideSequenceInContext;
        if (pe.post != "-") peptideSequenceInContext += pe.post;

        int nTerminusIsSpecific = pe.pre == "-" ? 1 : 0;
        int cTerminusIsSpecific = pe.post == "-" ? 1 : 0;

        int bestSpecificity = -1;
        boost::shared_ptr<DigestedPeptide> bestResult;

        BOOST_FOREACH(const boost::regex& regex, cleavageAgentRegexes)
        {
            Digestion::Config config;
            config.minimumSpecificity = Digestion::NonSpecific;
            Digestion peptideInContext(peptideSequenceInContext, regex, config);

            // if enzymes are independent, both termini of a peptide must be cleaved by the same enzyme
            if (sip.enzymes.independent)
            {
                nTerminusIsSpecific = pe.pre == "-" ? 1 : 0;
                cTerminusIsSpecific = pe.post == "-" ? 1 : 0;
            }

            try
            {
                DigestedPeptide result = peptideInContext.find_first(peptide.peptideSequence);
                nTerminusIsSpecific |= result.NTerminusIsSpecific() ? 1 : 0;
                cTerminusIsSpecific |= result.CTerminusIsSpecific() ? 1 : 0;

                if (nTerminusIsSpecific + cTerminusIsSpecific > bestSpecificity)
                {
                    bestSpecificity = nTerminusIsSpecific + cTerminusIsSpecific;
                    bestResult.reset(new DigestedPeptide(result,
                                                         pe.start-1, // offset is 0-based
                                                         result.missedCleavages(),
                                                         nTerminusIsSpecific == 1,
                                                         cTerminusIsSpecific == 1,
                                                         pe.pre,
                                                         pe.post));
                }
            }
            catch (runtime_error&)
            {}
        }

        if (bestResult.get())
            results.push_back(*bestResult);
    }
    return results;
}


//
// SpectrumIdentificationResult
//

PWIZ_API_DECL bool SpectrumIdentificationResult::empty() const
{
    return Identifiable::empty() &&
        spectrumID.empty() &&
        (!spectraDataPtr.get() || spectraDataPtr->empty()) &&
        spectrumIdentificationItem.empty() &&
        ParamContainer::empty();
}


//
// SpectrumIdentification
//

PWIZ_API_DECL SpectrumIdentification::SpectrumIdentification(
    const std::string& id_,const std::string& name_)
    : Identifiable(id_, name_)
{
}


PWIZ_API_DECL bool SpectrumIdentification::empty() const
{
    return Identifiable::empty() &&
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
    : Identifiable(id_, name_)
{
}
    

PWIZ_API_DECL bool ProteinDetectionList::empty() const
{
    return Identifiable::empty() &&
        proteinAmbiguityGroup.empty() &&
        ParamContainer::empty();
}

//
// ProteinDetectionHypothesis
//

PWIZ_API_DECL ProteinDetectionHypothesis::ProteinDetectionHypothesis(
    const std::string& id_, const std::string& name_)
    : Identifiable(id_, name_), passThreshold(0)
{
}

PWIZ_API_DECL bool ProteinDetectionHypothesis::empty() const
{
    return (!dbSequencePtr.get() || dbSequencePtr->empty()) &&
        passThreshold == 0 &&
        peptideHypothesis.empty() &&
        ParamContainer::empty();
}

//
// ProteinAmbiguityGroup
//

PWIZ_API_DECL ProteinAmbiguityGroup::ProteinAmbiguityGroup(
    const std::string& id_, const std::string& name_)
    : Identifiable(id_, name_)
    
{
}

PWIZ_API_DECL bool ProteinAmbiguityGroup::empty() const
{
    return proteinDetectionHypothesis.empty() &&
        ParamContainer::empty();
}


//
// Provider
//

PWIZ_API_DECL Provider::Provider(const std::string id_,
                                 const std::string name_)
    : Identifiable(id_, name_)
{
}


PWIZ_API_DECL bool Provider::empty() const
{
    return Identifiable::empty() &&
        contactRole.empty();
}


//
// SpectrumIdentificationList
//

PWIZ_API_DECL SpectrumIdentificationList::SpectrumIdentificationList(
    const string& id_, const string& name_)
    : Identifiable(id_, name_), numSequencesSearched(0)
{
}

PWIZ_API_DECL bool SpectrumIdentificationList::empty() const
{
    return Identifiable::empty() &&
        numSequencesSearched == 0 &&
        fragmentationTable.empty() &&
        spectrumIdentificationResult.empty();
}


//
// ProteinDetectionProtocol
//

PWIZ_API_DECL ProteinDetectionProtocol::ProteinDetectionProtocol(
    const std::string& id_, const std::string& name_)
    : Identifiable(id_, name_)
{
}

PWIZ_API_DECL bool ProteinDetectionProtocol::empty() const
{
    return Identifiable::empty() &&
        (!analysisSoftwarePtr.get() || analysisSoftwarePtr->empty()) &&
        analysisParams.empty() &&
        threshold.empty();
}

//
// PeptideEvidence
//

PWIZ_API_DECL PeptideEvidence::PeptideEvidence(const string& id,
                                               const string& name)
    : Identifiable(id, name),
      start(0), end(0),
      frame(0), isDecoy(false),
      missedCleavages(0)
{
}


PWIZ_API_DECL bool PeptideEvidence::empty() const
{
    return Identifiable::empty() &&
        (!dbSequencePtr.get() || dbSequencePtr->empty()) &&
        start == 0 &&
        end == 0 &&
        pre.empty() &&
        post.empty() &&
        (!translationTablePtr.get() || translationTablePtr->empty()) &&
        frame == 0 &&
        isDecoy == false &&
        missedCleavages == 0 &&
        ParamContainer::empty();
}


//
// FragmentArray
//

PWIZ_API_DECL bool FragmentArray::empty() const
{
    return values.empty() &&
        (!measurePtr.get() || measurePtr->empty()) &&
        ParamContainer::empty();
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
    : Identifiable(id, name)
{
}

PWIZ_API_DECL bool TranslationTable::empty() const
{
    return Identifiable::empty() &&
        ParamContainer::empty();
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
        ParamContainer::empty();
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
    : Identifiable(id_, name_)
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
    : Identifiable(id, name)
{
}
    

PWIZ_API_DECL bool Measure::empty() const
{
    return Identifiable::empty() &&
        ParamContainer::empty();
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

PWIZ_API_DECL Sample::SubSample::SubSample(const std::string& id_,
                                           const std::string& name_)
    : samplePtr(SamplePtr(new Sample(id_, name_)))
{}

PWIZ_API_DECL bool Sample::SubSample::empty() const
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
    : Identifiable(id, name)
{
}


PWIZ_API_DECL bool Peptide::empty() const
{
    return Identifiable::empty() &&
        peptideSequence.empty() &&
        modification.empty() &&
        substitutionModification.empty() &&
        ParamContainer::empty();
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
    : Identifiable(id_, name_)
{
}

PWIZ_API_DECL bool AnalysisSoftware::empty() const
{
    return Identifiable::empty() &&
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
    : Identifiable(id_, name_), length(0)
{
}

PWIZ_API_DECL bool DBSequence::empty() const
{
    return Identifiable::empty() &&
        length == 0 &&
        accession.empty() &&
        (!searchDatabasePtr.get() || searchDatabasePtr->empty()) &&
        seq.empty() &&
        ParamContainer::empty();
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
// SearchModification
//

PWIZ_API_DECL SearchModification::SearchModification()
    : fixedMod(false),
      massDelta(0)
{
}


PWIZ_API_DECL bool SearchModification::empty() const
{
    return unimodName.empty() &&
           massDelta == 0 &&
           residues.empty() &&
           specificityRules.empty();
}

//
// ProteinDetection
//

PWIZ_API_DECL ProteinDetection::ProteinDetection(const std::string id_,
                                                 const std::string name_)
    : Identifiable(id_, name_)
{
}


PWIZ_API_DECL bool ProteinDetection::empty() const
{
    return Identifiable::empty() &&
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
    : Identifiable(id_, name_)
{
}


PWIZ_API_DECL bool SpectrumIdentificationProtocol::empty() const
{
    return Identifiable::empty() &&
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
    : Identifiable(id, name)
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
        ParamContainer::empty();
}

//
// SourceFile
//

PWIZ_API_DECL bool SourceFile::empty() const
{
    return location.empty() &&
        fileFormat.empty() &&
        externalFormatDocumentation.empty() &&
        ParamContainer::empty();
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
    : Identifiable(id_), creationDate(creationDate_), version_("1.0.0")
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
    return Identifiable::empty() &&
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

