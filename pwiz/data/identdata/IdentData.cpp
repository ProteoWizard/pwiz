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
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/data/common/Unimod.hpp"
#include "IdentData.hpp"
#include "boost/xpressive/xpressive_dynamic.hpp"


namespace pwiz {
namespace identdata {


using namespace boost::logic;
using namespace boost::gregorian;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::chemistry;
namespace bxp = boost::xpressive;


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
// IdentifiableParamContainer
//

PWIZ_API_DECL IdentifiableParamContainer::IdentifiableParamContainer(const std::string& id_,
                                   const std::string& name_)
    : id(id_), name(name_)
{
}

PWIZ_API_DECL bool IdentifiableParamContainer::empty() const
{
    return ParamContainer::empty() &&
           id.empty() &&
           name.empty();
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

PWIZ_API_DECL ContactRole::ContactRole(CVID role_,
                                       const ContactPtr& contactPtr_)
    : CVParam(role_), contactPtr(contactPtr_)
{
}

PWIZ_API_DECL bool ContactRole::empty() const
{
    return (!contactPtr.get() || contactPtr->empty()) &&
           CVParam::empty();
}


//
// Contact
//

PWIZ_API_DECL Contact::Contact(const string& id_,
                               const string& name_)
    : IdentifiableParamContainer(id_, name_)
{
}


PWIZ_API_DECL bool Contact::empty() const
{
    return IdentifiableParamContainer::empty();
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
           (!parent.get() || parent->empty());
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
           CVParam::empty() &&
           fragmentArray.empty();
}


//
// SpectrumIdentificationItem
//

PWIZ_API_DECL SpectrumIdentificationItem::SpectrumIdentificationItem(
    const string& id, const string& name)
    : IdentifiableParamContainer(id, name),
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
    return IdentifiableParamContainer::empty() &&
           chargeState == 0 &&
           experimentalMassToCharge == 0 &&
           calculatedMassToCharge == 0 &&
           calculatedPI == 0 &&
           (!peptidePtr.get() || peptidePtr->empty()) &&
           rank == 0 &&
           passThreshold == 0 &&
           (!massTablePtr.get() || massTablePtr->empty()) &&
           (!samplePtr.get() || samplePtr->empty()) &&
           peptideEvidencePtr.empty() &&
           fragmentation.empty();
}


//
// SpectrumIdentificationResult
//

PWIZ_API_DECL SpectrumIdentificationResult::SpectrumIdentificationResult(const std::string& id_,
                                                                         const std::string& name_)
    : IdentifiableParamContainer(id_, name_)
{
}

PWIZ_API_DECL bool SpectrumIdentificationResult::empty() const
{
    return IdentifiableParamContainer::empty() &&
           spectrumID.empty() &&
           (!spectraDataPtr.get() || spectraDataPtr->empty()) &&
           spectrumIdentificationItem.empty();
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
    : IdentifiableParamContainer(id_, name_)
{
}
    

PWIZ_API_DECL bool ProteinDetectionList::empty() const
{
    return IdentifiableParamContainer::empty() &&
           proteinAmbiguityGroup.empty();
}


//
// PeptideHypothesis
//

PWIZ_API_DECL bool PeptideHypothesis::empty() const
{
    return (!peptideEvidencePtr.get() || peptideEvidencePtr->empty()) &&
           spectrumIdentificationItemPtr.empty();
}


//
// ProteinDetectionHypothesis
//

PWIZ_API_DECL ProteinDetectionHypothesis::ProteinDetectionHypothesis(
    const std::string& id_, const std::string& name_)
    : IdentifiableParamContainer(id_, name_), passThreshold(0)
{
}

PWIZ_API_DECL bool ProteinDetectionHypothesis::empty() const
{
    return (!dbSequencePtr.get() || dbSequencePtr->empty()) &&
           passThreshold == 0 &&
           peptideHypothesis.empty() &&
           IdentifiableParamContainer::empty();
}


//
// ProteinAmbiguityGroup
//

PWIZ_API_DECL ProteinAmbiguityGroup::ProteinAmbiguityGroup(
    const std::string& id_, const std::string& name_)
    : IdentifiableParamContainer(id_, name_)
    
{
}

PWIZ_API_DECL bool ProteinAmbiguityGroup::empty() const
{
    return proteinDetectionHypothesis.empty() &&
           IdentifiableParamContainer::empty();
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
           (!contactRolePtr.get() || contactRolePtr->empty());
}


//
// SpectrumIdentificationList
//

PWIZ_API_DECL SpectrumIdentificationList::SpectrumIdentificationList(
    const string& id_, const string& name_)
    : IdentifiableParamContainer(id_, name_), numSequencesSearched(0)
{
}

PWIZ_API_DECL bool SpectrumIdentificationList::empty() const
{
    return IdentifiableParamContainer::empty() &&
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
    : IdentifiableParamContainer(id, name),
      start(0), end(0),
      pre(0), post(0),
      frame(0), isDecoy(false)
{
}


PWIZ_API_DECL bool PeptideEvidence::empty() const
{
    return IdentifiableParamContainer::empty() &&
           (!peptidePtr.get() || peptidePtr->empty()) &&
           (!dbSequencePtr.get() || dbSequencePtr->empty()) &&
           start == 0 &&
           end == 0 &&
           pre == 0 &&
           post == 0 &&
           (!translationTablePtr.get() || translationTablePtr->empty()) &&
           frame == 0 &&
           isDecoy == false;
}


//
// FragmentArray
//

PWIZ_API_DECL bool FragmentArray::empty() const
{
    return values.empty() &&
           (!measurePtr.get() || measurePtr->empty());
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
    : IdentifiableParamContainer(id, name)
{
}


//
// DatabaseTranslation
//

PWIZ_API_DECL bool DatabaseTranslation::empty() const
{
    return frames.empty() &&
           translationTable.empty();
}


//
// Residue
//

PWIZ_API_DECL Residue::Residue() :
    code(0),
    mass(0)
{
}

PWIZ_API_DECL bool Residue::empty() const
{
    return code == 0 &&
           mass == 0;
}


//
// AmbiguousResidue
//

PWIZ_API_DECL AmbiguousResidue::AmbiguousResidue() :
    code(0)
{
}

PWIZ_API_DECL bool AmbiguousResidue::empty() const
{
    return code == 0 &&
           ParamContainer::empty();
}


//
// Enzyme
//

PWIZ_API_DECL Enzyme::Enzyme(const string& id,
                             const std::string& name)
    : Identifiable(id, name),
      terminalSpecificity(proteome::Digestion::FullySpecific),
      missedCleavages(0),
      minDistance(0)

{
}

PWIZ_API_DECL bool Enzyme::empty() const
{
    return id.empty() &&
           nTermGain.empty() &&
           cTermGain.empty() &&
           terminalSpecificity == proteome::Digestion::NonSpecific && // CONSIDER: boost::optional
           missedCleavages == 0 &&
           minDistance == 0 &&
           siteRegexp.empty() &&
           enzymeName.empty();
}


//
// Measure
//

PWIZ_API_DECL Measure::Measure(const string id, const string name)
    : IdentifiableParamContainer(id, name)
{
}
    
PWIZ_API_DECL bool Measure::empty() const
{
    return IdentifiableParamContainer::empty();
}


//
// Sample
//

PWIZ_API_DECL Sample::Sample(const std::string& id_,
                             const std::string& name_)
    : IdentifiableParamContainer(id_, name_)
{
}

PWIZ_API_DECL bool Sample::empty() const
{
    return IdentifiableParamContainer::empty() &&
           subSamples.empty() &&
           contactRole.empty();
}


//
// SubstitutionModification
//

PWIZ_API_DECL SubstitutionModification::SubstitutionModification() :
    originalResidue(0),
    replacementResidue(0),
    location(0),
    avgMassDelta(0),
    monoisotopicMassDelta(0)
{
}


PWIZ_API_DECL bool SubstitutionModification::empty() const
{
    return originalResidue == 0 &&
           replacementResidue == 0 &&
           location == 0 &&
           avgMassDelta == 0 &&
           monoisotopicMassDelta == 0;
}


//
// Peptide
//

PWIZ_API_DECL Peptide::Peptide(const std::string& id, const std::string& name)
    : IdentifiableParamContainer(id, name)
{
}


PWIZ_API_DECL bool Peptide::empty() const
{
    return IdentifiableParamContainer::empty() &&
           peptideSequence.empty() &&
           modification.empty() &&
           substitutionModification.empty();
}


//
// SequenceCollection
//

PWIZ_API_DECL bool SequenceCollection::empty() const
{
    return dbSequences.empty() &&
           peptides.empty() &&
           peptideEvidence.empty();
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
    : IdentifiableParamContainer(id_, name_), length(0)
{
}

PWIZ_API_DECL bool DBSequence::empty() const
{
    return IdentifiableParamContainer::empty() &&
           length == 0 &&
           accession.empty() &&
           (!searchDatabasePtr.get() || searchDatabasePtr->empty()) &&
           seq.empty();
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
    return ParamContainer::empty() &&
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
           databaseFilters.empty();
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
    : IdentifiableParamContainer(id_, name_)
{
    numDatabaseSequences = 0;
    numResidues = 0;
}

PWIZ_API_DECL bool SearchDatabase::empty() const
{
    return IdentifiableParamContainer::empty() &&
           location.empty() &&
           version.empty() &&
           releaseDate.empty() &&
           numDatabaseSequences == 0 &&
           numResidues == 0 &&
           fileFormat.empty() &&
           databaseName.empty();
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
// IdentData
//

PWIZ_API_DECL IdentData::IdentData(const std::string& id_,
                                   const std::string& creationDate_)
    : Identifiable(id_), creationDate(creationDate_), version_("1.1.0")
{
    if (creationDate.empty())
        creationDate = pwiz::util::encode_xml_datetime(bpt::second_clock::universal_time());
}

PWIZ_API_DECL bool IdentData::empty() const
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

PWIZ_API_DECL const string& IdentData::version() const
{
    return version_;
}


namespace {

bool hasValidFlankingSymbols(const PeptideEvidence& pe)
{
    return ((pe.pre >= 'A' && pe.pre <= 'Z') || pe.pre == '-' || (pe.isDecoy && pe.pre == '?')) &&
           ((pe.post >= 'A' && pe.post <= 'Z') || pe.post == '-' || (pe.isDecoy && pe.post == '?'));
}

// uses cleavageAgent or cleavageAgentRegex to find the most specific peptide evidence;
// returns true iff it is possible to get a better result with another call on the same peptide
bool findPeptideEvidenceWithRegex(const PeptideEvidence& pe,
                                  const Peptide& peptide,
                                  const string& peptideSequenceInContext,
                                  CVID cleavageAgent,
                                  const string& cleavageAgentRegex,
                                  bool independent,
                                  int& nTerminusIsSpecific,
                                  int& cTerminusIsSpecific,
                                  int& bestSpecificity,
                                  shared_ptr<proteome::DigestedPeptide>& bestResult)
{
    using namespace proteome;

    if (cleavageAgent == MS_unspecific_cleavage)
    {
        bestSpecificity = 0;
        bestResult.reset(new DigestedPeptide(peptide.peptideSequence, pe.start-1, 0, false, false, string(1, pe.pre), string(1, pe.post)));
        return false;
    }
    else if (cleavageAgent == MS_no_cleavage)
    {
        bestSpecificity = 2;
        bestResult.reset(new DigestedPeptide(peptide.peptideSequence, pe.start-1, 0, true, true, string(1, pe.pre), string(1, pe.post)));
        return false;
    }

    Digestion::Config config;
    config.minimumSpecificity = Digestion::NonSpecific;
    scoped_ptr<Digestion> peptideInContextPtr;
    if (cleavageAgent != CVID_Unknown)
        peptideInContextPtr.reset(new Digestion(peptideSequenceInContext, cleavageAgent, config));
    else
        peptideInContextPtr.reset(new Digestion(peptideSequenceInContext, cleavageAgentRegex, config));
    const Digestion& peptideInContext = *peptideInContextPtr;

    // if enzymes are independent, both termini of a peptide must be cleaved by the same enzyme
    if (independent)
    {
        nTerminusIsSpecific = pe.pre == '-' ? 1 : 0;
        cTerminusIsSpecific = pe.post == '-' ? 1 : 0;
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
                                                 string(1, pe.pre),
                                                 string(1, pe.post)));
        }
    }
    catch (runtime_error&)
    {}

    return bestSpecificity < 2;
}

} // namespace


PWIZ_API_DECL proteome::DigestedPeptide digestedPeptide(const SpectrumIdentificationProtocol& sip, const PeptideEvidence& pe)
{
    using namespace proteome;

    if (pe.empty())
        throw runtime_error("[identdata::digestedPeptide] null or empty PeptideEvidence element");
    if (!pe.peptidePtr.get() || pe.peptidePtr->empty())
        throw runtime_error("[identdata::digestedPeptide] null or empty Peptide reference: " + pe.id);

    const Peptide& peptide = *pe.peptidePtr;

    vector<CVID> cleavageAgents = identdata::cleavageAgents(sip.enzymes);

    vector<string> cleavageAgentRegexes;
    if (cleavageAgents.empty())
    {
        cleavageAgentRegexes = identdata::cleavageAgentRegexes(sip.enzymes);

        if (cleavageAgentRegexes.empty())
        {
            if (!sip.enzymes.enzymes.empty() && sip.enzymes.enzymes[0]->terminalSpecificity == Digestion::NonSpecific)
                cleavageAgents.push_back(MS_unspecific_cleavage);
            else
                throw runtime_error("[identdata::digestedPeptide] unknown cleavage agent");
        }
    }

    if (!hasValidFlankingSymbols(pe))
        throw runtime_error("[identdata::digestedPeptide] invalid pre/post on PeptideEvidence element: " + pe.id);

    string peptideSequenceInContext = peptide.peptideSequence;
    if (pe.pre != '-') peptideSequenceInContext = pe.pre + peptideSequenceInContext;
    if (pe.post != '-') peptideSequenceInContext += pe.post;

    int nTerminusIsSpecific = pe.pre == '-' ? 1 : 0;
    int cTerminusIsSpecific = pe.post == '-' ? 1 : 0;

    int bestSpecificity = -1;
    boost::shared_ptr<DigestedPeptide> bestResult;

    BOOST_FOREACH(CVID cleavageAgent, cleavageAgents)
    {
        if (!findPeptideEvidenceWithRegex(pe, peptide, peptideSequenceInContext, cleavageAgent, "",
                                          sip.enzymes.independent, nTerminusIsSpecific, cTerminusIsSpecific,
                                          bestSpecificity, bestResult))
            break;
    }

    BOOST_FOREACH(const string& regex, cleavageAgentRegexes)
    {
        if (!findPeptideEvidenceWithRegex(pe, peptide, peptideSequenceInContext, CVID_Unknown, regex,
                                          sip.enzymes.independent, nTerminusIsSpecific, cTerminusIsSpecific,
                                          bestSpecificity, bestResult))
            break;
    }

    if (!bestResult.get())
        throw runtime_error("[identdata::digestedPeptide] invalid PeptideEvidence element: " + pe.id);
    return *bestResult;
}

PWIZ_API_DECL vector<proteome::DigestedPeptide> digestedPeptides(const SpectrumIdentificationProtocol& sip, const SpectrumIdentificationItem& sii)
{
    using namespace proteome;

    if (!sii.peptidePtr.get() || sii.peptidePtr->empty())
        throw runtime_error("[identdata::digestedPeptides] null or empty Peptide reference");
    if (sii.peptideEvidencePtr.empty())
        throw runtime_error("[identdata::digestedPeptides] no PeptideEvidence elements");

    const Peptide& peptide = *sii.peptidePtr;

    vector<CVID> cleavageAgents = identdata::cleavageAgents(sip.enzymes);

    vector<string> cleavageAgentRegexes;
    if (cleavageAgents.empty())
    {
        cleavageAgentRegexes = identdata::cleavageAgentRegexes(sip.enzymes);

        if (cleavageAgentRegexes.empty())
            throw runtime_error("[identdata::digestedPeptides] unknown cleavage agent");
    }

    vector<proteome::DigestedPeptide> results;

    BOOST_FOREACH(const PeptideEvidencePtr& pePtr, sii.peptideEvidencePtr)
    {
        const PeptideEvidence& pe = *pePtr;

        if (!hasValidFlankingSymbols(pe))
            continue;

        string peptideSequenceInContext = peptide.peptideSequence;
        if (pe.pre != '-') peptideSequenceInContext = pe.pre + peptideSequenceInContext;
        if (pe.post != '-') peptideSequenceInContext += pe.post;

        int nTerminusIsSpecific = pe.pre == '-' ? 1 : 0;
        int cTerminusIsSpecific = pe.post == '-' ? 1 : 0;

        int bestSpecificity = -1;
        boost::shared_ptr<DigestedPeptide> bestResult;

        BOOST_FOREACH(CVID cleavageAgent, cleavageAgents)
        {
            if (!findPeptideEvidenceWithRegex(pe, peptide, peptideSequenceInContext, cleavageAgent, "",
                                              sip.enzymes.independent, nTerminusIsSpecific, cTerminusIsSpecific,
                                              bestSpecificity, bestResult))
                break;
        }

        BOOST_FOREACH(const string& regex, cleavageAgentRegexes)
        {
            if (!findPeptideEvidenceWithRegex(pe, peptide, peptideSequenceInContext, CVID_Unknown, regex,
                                              sip.enzymes.independent, nTerminusIsSpecific, cTerminusIsSpecific,
                                              bestSpecificity, bestResult))
                break;
        }

        if (bestResult.get())
            results.push_back(*bestResult);
    }
    return results;
}

PWIZ_API_DECL proteome::Peptide peptide(const Peptide& peptide)
{
    proteome::Peptide result(peptide.peptideSequence);
    proteome::ModificationMap& modMap = result.modifications();
    BOOST_FOREACH(const ModificationPtr& mod, peptide.modification)
    {
        int location = mod->location-1;
        if (location == -1)
            location = proteome::ModificationMap::NTerminus();
        else if (location == (int) peptide.peptideSequence.length())
            location = proteome::ModificationMap::CTerminus();
        modMap[location] = modification(*mod);
    }
    return result;
}

PWIZ_API_DECL proteome::Modification modification(const Modification& mod)
{
    CVParam firstUnimodAnnotation = mod.cvParamChild(UNIMOD_unimod_root_node);

    // if mod is unannotated, return mod without formula
    if (firstUnimodAnnotation.empty())
        return proteome::Modification(mod.monoisotopicMassDelta, mod.avgMassDelta);

    // HACK: allow Unimod CVParams that aren't yet supported by pwiz::unimod to be manually added by the caller
    try
    {
        unimod::Modification umod = unimod::modification(firstUnimodAnnotation.cvid);

        // return mod with formula
        return proteome::Modification(umod.deltaComposition);
    }
    catch (runtime_error&)
    {
        return proteome::Modification(mod.monoisotopicMassDelta, mod.avgMassDelta);
    }
}

PWIZ_API_DECL CVID cleavageAgent(const Enzyme& ez)
{
    CVID result = proteome::Digestion::getCleavageAgentByName(ez.enzymeName.cvParamChild(MS_cleavage_agent_name).name());
    if (result == CVID_Unknown && !ez.enzymeName.userParams.empty())
        result = proteome::Digestion::getCleavageAgentByName(ez.enzymeName.userParams[0].name);
    if (result == CVID_Unknown && !ez.name.empty())
        result = proteome::Digestion::getCleavageAgentByName(ez.name);
    if (result == CVID_Unknown)
        result = proteome::Digestion::getCleavageAgentByRegex(ez.siteRegexp);
    return result;
}

PWIZ_API_DECL std::vector<CVID> cleavageAgents(const Enzymes& enzymes)
{
    vector<CVID> result;
    BOOST_FOREACH(const EnzymePtr& enzymePtr, enzymes.enzymes)
        try
        {
            CVID ca = cleavageAgent(*enzymePtr);
            if (ca != CVID_Unknown)
                result.push_back(ca);
        }
        catch (exception&) {}

    return result;
}


PWIZ_API_DECL string cleavageAgentRegex(const Enzyme& ez)
{
    using namespace proteome;

    if (ez.siteRegexp.empty())
    {
        CVParam enzymeTerm = ez.enzymeName.cvParamChild(MS_cleavage_agent_name);

        if (enzymeTerm.empty() && !ez.enzymeName.userParams.empty())
            enzymeTerm = CVParam(Digestion::getCleavageAgentByName(ez.enzymeName.userParams[0].name));

        try {return Digestion::getCleavageAgentRegex(enzymeTerm.cvid);} catch (exception&) {}
    }
    else
        return ez.siteRegexp;

    throw runtime_error("[identdata::cleavageAgentRegex] unable to determine a regular expression for enzyme");
}

PWIZ_API_DECL std::vector<string> cleavageAgentRegexes(const Enzymes& enzymes)
{
    vector<string> result;
    BOOST_FOREACH(const EnzymePtr& enzymePtr, enzymes.enzymes)
        try {result.push_back(cleavageAgentRegex(*enzymePtr));} catch (exception&) {}
    return result;
}

PWIZ_API_DECL void snapModificationsToUnimod(const SpectrumIdentification& si)
{
    const SpectrumIdentificationProtocol& sip = *si.spectrumIdentificationProtocolPtr;

    // TODO: what about asymmetric tolerances?
    CVParam precursorToleranceParam = sip.parentTolerance.cvParam(MS_search_tolerance_plus_value);
    MZTolerance precursorTolerance(precursorToleranceParam.valueAs<double>());
    if (precursorToleranceParam.units == UO_parts_per_million)
        precursorTolerance.units = MZTolerance::PPM;

    BOOST_FOREACH(const SearchModificationPtr& modPtr, sip.modificationParams)
    {
        SearchModification& mod = *modPtr;
        vector<char> residues = mod.residues;
        if (residues.empty() || (residues.size() == 1 && residues[0] == '.'))
        {
            residues.clear();
            residues.push_back('x');
        }
        
        mod.cvParams.clear();
        BOOST_FOREACH(char residue, residues)
        {
            vector<unimod::Modification> possibleMods = unimod::modifications(mod.massDelta,
                                                                              0.0001,
                                                                              boost::logic::indeterminate,
                                                                              boost::logic::indeterminate,
                                                                              unimod::site(residue),
                                                                              unimod::position(mod.specificityRules.cvid));

            BOOST_FOREACH(const unimod::Modification& possibleMod, possibleMods)
            {
                // skip AA substitutions
                if (possibleMod.specificities[0].classification == unimod::Classification::Substitution)
                    continue;
                mod.set(possibleMod.cvid);
            }
        }
        if (mod.cvParams.empty())
            mod.set(MS_unknown_modification);
    }

    if (!si.spectrumIdentificationListPtr.get())
        return;

    const SpectrumIdentificationList& sil = *si.spectrumIdentificationListPtr;

    // loop over SIIs instead of Peptides to get access to calculatedMassToCharge without recalculating it
    set<PeptidePtr> snappedPeptides;
    BOOST_FOREACH(const SpectrumIdentificationResultPtr& sir, sil.spectrumIdentificationResult)
    BOOST_FOREACH(const SpectrumIdentificationItemPtr& sii, sir->spectrumIdentificationItem)
    {
        if (!sii->peptidePtr.get())
            throw runtime_error("[identdata::snapModificationsToUnimod] NULL PeptidePtr in " + sii->id);

        // skip the peptide if it's already been processed
        pair<set<PeptidePtr>::iterator, bool> insertResult = snappedPeptides.insert(sii->peptidePtr);
        if (!insertResult.second)
            continue;

        Peptide& peptide = *sii->peptidePtr;
        double precursorMass = Ion::neutralMass(sii->calculatedMassToCharge, sii->chargeState);
        double precursorAbsoluteTolerance = precursorMass - (precursorMass - precursorTolerance);

        BOOST_FOREACH(const ModificationPtr& modPtr, peptide.modification)
        {
            Modification& mod = *modPtr;
            vector<char> residues = mod.residues;
            if (residues.empty())
            {
                if (mod.location == 0)
                    residues.push_back('n');
                else if (mod.location == (int) peptide.peptideSequence.length()+1)
                    residues.push_back('c');
                else
                    throw runtime_error("[identdata::snapModificationsToUnimod] no residues specified for a non-terminal modification in peptide \"" + peptide.id + "\"");
            }

            mod.cvParams.clear();
            BOOST_FOREACH(char residue, residues)
            {
                vector<unimod::Modification> possibleMods = unimod::modifications(mod.monoisotopicMassDelta,
                                                                                  precursorAbsoluteTolerance,
                                                                                  boost::logic::indeterminate,
                                                                                  boost::logic::indeterminate,
                                                                                  unimod::site(residue));

                BOOST_FOREACH(const unimod::Modification& possibleMod, possibleMods)
                {
                    // skip AA substitutions
                    if (possibleMod.specificities[0].classification == unimod::Classification::Substitution)
                        continue;
                    mod.set(possibleMod.cvid);
                }
            }
            if (mod.cvParams.empty())
                mod.set(MS_unknown_modification);
        }
    }
}


} // namespace identdata
} // namespace pwiz
