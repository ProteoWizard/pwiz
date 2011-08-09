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


#ifndef _IDENTDATA_HPP_
#define _IDENTDATA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include "pwiz/data/common/ParamTypes.hpp"
#include "boost/logic/tribool.hpp"
#include <vector>
#include <string>
#include <map>


#ifdef USE_RAW_PTR
#define TYPEDEF_SHARED_PTR(type) typedef type* type##Ptr
#define BOOST_FOREACH(a, b) a;
#else
#include <boost/foreach.hpp>
#define TYPEDEF_SHARED_PTR(type) typedef boost::shared_ptr<type> type##Ptr
#endif


namespace pwiz {
namespace identdata {


using namespace pwiz::cv;
using namespace pwiz::data;

/// returns a default list of CVs used in an IdentData document;
/// currently includes PSI-MS, Unit Ontology, and UNIMOD
PWIZ_API_DECL std::vector<CV> defaultCVList();


struct PWIZ_API_DECL Identifiable
{
    Identifiable(const std::string& id_ = "",
                 const std::string& name_ = "");
    virtual ~Identifiable() {}

    std::string id;
    std::string name;

    virtual bool empty() const;
};


struct PWIZ_API_DECL IdentifiableParamContainer : public ParamContainer
{
    IdentifiableParamContainer(const std::string& id_ = "",
                 const std::string& name_ = "");
    virtual ~IdentifiableParamContainer() {}

    std::string id;
    std::string name;

    virtual bool empty() const;
};


struct PWIZ_API_DECL BibliographicReference : public Identifiable
{
    BibliographicReference();

    std::string authors;
    std::string publication;
    std::string publisher;
    std::string editor;
    int year;
    std::string volume;
    std::string issue;
    std::string pages;
    std::string title;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(BibliographicReference);


struct PWIZ_API_DECL Contact : public IdentifiableParamContainer
{
    Contact(const std::string& id_ = "",
            const std::string& name_ = "");
    virtual ~Contact() {}

    virtual bool empty() const;
};

TYPEDEF_SHARED_PTR(Contact);


struct PWIZ_API_DECL Organization : public Contact
{
    Organization(const std::string& id_ = "",
                 const std::string& name_ = "");

    boost::shared_ptr<Organization> parent;

    virtual bool empty() const;
};

TYPEDEF_SHARED_PTR(Organization);


struct PWIZ_API_DECL Person : public Contact
{
    Person(const std::string& id_ = "",
           const std::string& name_ = "");

    std::string lastName;
    std::string firstName;
    std::string midInitials;

    std::vector<OrganizationPtr> affiliations;

    virtual bool empty() const;
};

TYPEDEF_SHARED_PTR(Person);


struct PWIZ_API_DECL ContactRole : public CVParam
{
    ContactRole(CVID role_ = CVID_Unknown,
                const ContactPtr& contactPtr_ = ContactPtr());

    ContactPtr contactPtr;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(ContactRole);


struct PWIZ_API_DECL Sample : public IdentifiableParamContainer
{
    Sample(const std::string& id_ = "",
           const std::string& name_ = "");

    std::vector<ContactRolePtr> contactRole;
    std::vector<boost::shared_ptr<Sample> > subSamples;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Sample);


struct PWIZ_API_DECL AnalysisSoftware : public Identifiable
{
    AnalysisSoftware(const std::string& id_ = "",
                     const std::string& name_ = "");

    // SoftwareType attributes
    std::string version;
    std::string URI;
    std::string customizations;

    // SoftwareType elements
    ContactRolePtr contactRolePtr;
    ParamContainer softwareName;

    virtual bool empty() const;
};

TYPEDEF_SHARED_PTR(AnalysisSoftware);


struct PWIZ_API_DECL Provider : public Identifiable
{
    Provider(const std::string id_ = "",
             const std::string name_ = "");

    ContactRolePtr contactRolePtr;
    AnalysisSoftwarePtr analysisSoftwarePtr;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Provider);


// TODO find example document w/ this in it and determine best
// representation for data model
struct PWIZ_API_DECL AnalysisSampleCollection
{
    std::vector<SamplePtr> samples;

    bool empty() const;
};


struct PWIZ_API_DECL SearchDatabase : public IdentifiableParamContainer
{
    SearchDatabase(const std::string& id_ = "",
                   const std::string& name_ = "");

    std::string location;
    std::string version;
    std::string releaseDate;
    long numDatabaseSequences;
    long numResidues;

    CVParam fileFormat;
    ParamContainer databaseName;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SearchDatabase);


struct PWIZ_API_DECL DBSequence : public IdentifiableParamContainer
{
    DBSequence(const std::string id_ = "",
               const std::string name_ = "");

    int length;
    std::string accession;
    SearchDatabasePtr searchDatabasePtr;

    std::string seq;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(DBSequence);


struct PWIZ_API_DECL Modification : public ParamContainer
{
    Modification();

    int location;
    std::vector<char> residues;
    double avgMassDelta;
    double monoisotopicMassDelta;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Modification);


struct PWIZ_API_DECL SubstitutionModification
{
    SubstitutionModification();

    char originalResidue;
    char replacementResidue;
    int location;
    double avgMassDelta;
    double monoisotopicMassDelta;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SubstitutionModification);


struct PWIZ_API_DECL Peptide : public IdentifiableParamContainer
{
    Peptide(const std::string& id="",
            const std::string& name="");

    std::string peptideSequence;
    std::vector<ModificationPtr> modification;
    std::vector<SubstitutionModificationPtr> substitutionModification;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Peptide);


struct PWIZ_API_DECL SearchModification : public ParamContainer
{
    SearchModification();

    bool fixedMod;
    double massDelta;
    std::vector<char> residues;
    CVParam specificityRules;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SearchModification);


struct PWIZ_API_DECL Enzyme : public Identifiable
{
    Enzyme(const std::string& id = "",
           const std::string& name = "");

    std::string nTermGain;
    std::string cTermGain;
    proteome::Digestion::Specificity terminalSpecificity;
    int missedCleavages;
    int minDistance;

    std::string siteRegexp;
    ParamContainer enzymeName;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Enzyme);


struct PWIZ_API_DECL Enzymes
{
    boost::logic::tribool independent;

    std::vector<EnzymePtr> enzymes;

    bool empty() const;
};


struct PWIZ_API_DECL Residue
{
    Residue();

    char code;
    double mass;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Residue);


struct PWIZ_API_DECL AmbiguousResidue : public ParamContainer
{
    AmbiguousResidue();

    char code;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(AmbiguousResidue);


struct PWIZ_API_DECL MassTable
{
    MassTable(const std::string id = "");

    std::string id;
    std::vector<int> msLevel;

    std::vector<ResiduePtr> residues;
    std::vector<AmbiguousResiduePtr> ambiguousResidue;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(MassTable);


struct PWIZ_API_DECL Filter
{
    ParamContainer filterType;
    ParamContainer include;
    ParamContainer exclude;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Filter);


struct PWIZ_API_DECL TranslationTable : public IdentifiableParamContainer
{
    TranslationTable(const std::string& id = "",
                     const std::string& name = "");
};

TYPEDEF_SHARED_PTR(TranslationTable);


struct PWIZ_API_DECL DatabaseTranslation
{
    std::vector<int> frames;
    std::vector<TranslationTablePtr> translationTable;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(DatabaseTranslation);


struct PWIZ_API_DECL SpectrumIdentificationProtocol : public Identifiable
{
    SpectrumIdentificationProtocol(const std::string& id_ = "",
                                   const std::string& name_ = "");

    AnalysisSoftwarePtr analysisSoftwarePtr;

    CVParam searchType;
    ParamContainer additionalSearchParams;
    std::vector<SearchModificationPtr> modificationParams;
    Enzymes enzymes;
    std::vector<MassTablePtr> massTable;
    ParamContainer fragmentTolerance;
    ParamContainer parentTolerance;
    ParamContainer threshold;
    std::vector<FilterPtr> databaseFilters;
    DatabaseTranslationPtr databaseTranslation;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SpectrumIdentificationProtocol);


struct PWIZ_API_DECL Measure : public IdentifiableParamContainer
{
    Measure(const std::string id = "",
            const std::string name = "");

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Measure);


struct PWIZ_API_DECL FragmentArray
{
    std::vector<double> values;
    MeasurePtr measurePtr;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(FragmentArray);


struct PWIZ_API_DECL IonType : public CVParam
{
    IonType();

    std::vector<int> index;
    int charge;
    std::vector<FragmentArrayPtr> fragmentArray;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(IonType);


struct PWIZ_API_DECL PeptideEvidence : public IdentifiableParamContainer
{
    PeptideEvidence(const std::string& id = "",
                    const std::string& name = "");

    PeptidePtr peptidePtr;
    DBSequencePtr dbSequencePtr;
    int start;
    int end;
    char pre;
    char post;
    TranslationTablePtr translationTablePtr;
    int frame;
    bool isDecoy;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(PeptideEvidence);


struct PWIZ_API_DECL SequenceCollection
{
    std::vector<DBSequencePtr> dbSequences;
    std::vector<PeptidePtr> peptides;
    std::vector<PeptideEvidencePtr> peptideEvidence;
    bool empty() const;
};


struct PWIZ_API_DECL SpectrumIdentificationItem : public IdentifiableParamContainer
{
    SpectrumIdentificationItem(const std::string& id = "",
                               const std::string& name = "");

    int chargeState;
    double experimentalMassToCharge;
    double calculatedMassToCharge;
    double calculatedPI;
    PeptidePtr peptidePtr;
    int rank;
    bool passThreshold;
    MassTablePtr massTablePtr;
    SamplePtr samplePtr;

    std::vector<PeptideEvidencePtr> peptideEvidencePtr;
    std::vector<IonTypePtr> fragmentation;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SpectrumIdentificationItem);


struct PWIZ_API_DECL SpectraData : public Identifiable
{
    SpectraData(const std::string id = "",
                const std::string name = "");

    std::string location;

    std::vector<std::string> externalFormatDocumentation;
    CVParam fileFormat;
    CVParam spectrumIDFormat;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SpectraData);


struct PWIZ_API_DECL SpectrumIdentificationResult : public IdentifiableParamContainer
{
    SpectrumIdentificationResult(const std::string& id_ = "",
                                 const std::string& name_ = "");

    std::string spectrumID;
    SpectraDataPtr spectraDataPtr;

    std::vector<SpectrumIdentificationItemPtr> spectrumIdentificationItem;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SpectrumIdentificationResult);


struct PWIZ_API_DECL SpectrumIdentificationList : public Identifiable
{
    SpectrumIdentificationList(const std::string& id_ = "",
                               const std::string& name_ = "");

    long numSequencesSearched;

    std::vector<MeasurePtr> fragmentationTable;
    std::vector<SpectrumIdentificationResultPtr> spectrumIdentificationResult;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SpectrumIdentificationList);


struct PWIZ_API_DECL SpectrumIdentification : public Identifiable
{
    SpectrumIdentification(const std::string& id_ = "",
                           const std::string& name_ = "");

    SpectrumIdentificationProtocolPtr spectrumIdentificationProtocolPtr;
    SpectrumIdentificationListPtr spectrumIdentificationListPtr;
    std::string activityDate;

    std::vector<SpectraDataPtr> inputSpectra;
    std::vector<SearchDatabasePtr> searchDatabase;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SpectrumIdentification);


struct PWIZ_API_DECL ProteinDetectionProtocol : public Identifiable
{
    ProteinDetectionProtocol(const std::string& id_ = "",
                             const std::string& name_ = "");

    AnalysisSoftwarePtr analysisSoftwarePtr;

    ParamContainer analysisParams;
    ParamContainer threshold;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(ProteinDetectionProtocol);


struct PWIZ_API_DECL PeptideHypothesis
{
    PeptideEvidencePtr peptideEvidencePtr;
    std::vector<SpectrumIdentificationItemPtr> spectrumIdentificationItemPtr;

    bool empty() const;
};


struct PWIZ_API_DECL ProteinDetectionHypothesis : public IdentifiableParamContainer
{
    ProteinDetectionHypothesis(const std::string& id_ = "",
                               const std::string& name_ = "");

    DBSequencePtr dbSequencePtr;
    bool passThreshold;
    std::vector<PeptideHypothesis> peptideHypothesis;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(ProteinDetectionHypothesis);


struct PWIZ_API_DECL ProteinAmbiguityGroup : public IdentifiableParamContainer
{
    ProteinAmbiguityGroup(const std::string& id_ = "",
                          const std::string& name_ = "");

    std::vector<ProteinDetectionHypothesisPtr> proteinDetectionHypothesis;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(ProteinAmbiguityGroup);


struct PWIZ_API_DECL ProteinDetectionList : public IdentifiableParamContainer
{
    ProteinDetectionList(const std::string& id_ = "",
                         const std::string& name_ = "");

    std::vector<ProteinAmbiguityGroupPtr> proteinAmbiguityGroup;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(ProteinDetectionList);


struct PWIZ_API_DECL ProteinDetection : public Identifiable
{
    ProteinDetection(const std::string id_ = "",
                     const std::string name_ = "");

    ProteinDetectionProtocolPtr proteinDetectionProtocolPtr;
    ProteinDetectionListPtr proteinDetectionListPtr;
    std::string activityDate;

    std::vector<SpectrumIdentificationListPtr> inputSpectrumIdentifications;

    virtual bool empty() const;
};

TYPEDEF_SHARED_PTR(ProteinDetection);


struct PWIZ_API_DECL AnalysisCollection
{
    std::vector<SpectrumIdentificationPtr> spectrumIdentification;
    ProteinDetection proteinDetection;

    bool empty() const;
};


struct PWIZ_API_DECL AnalysisProtocolCollection
{
    std::vector<SpectrumIdentificationProtocolPtr> spectrumIdentificationProtocol;
    std::vector<ProteinDetectionProtocolPtr> proteinDetectionProtocol;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(AnalysisProtocolCollection);


struct PWIZ_API_DECL SourceFile : public IdentifiableParamContainer
{
    std::string location;
    CVParam fileFormat;

    std::vector<std::string> externalFormatDocumentation;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SourceFile);


/// Input element. Contains 0+ of SourceFile, SearchDatabase,
/// SpectraData
struct PWIZ_API_DECL Inputs
{
    // Replace these 3 members w/ their types
    std::vector<SourceFilePtr> sourceFile;
    std::vector<SearchDatabasePtr> searchDatabase;
    std::vector<SpectraDataPtr> spectraData;
    
    bool empty() const;
};

TYPEDEF_SHARED_PTR(Inputs);


/// AnalysisData element. 
struct PWIZ_API_DECL AnalysisData
{
    std::vector<SpectrumIdentificationListPtr> spectrumIdentificationList;
    ProteinDetectionListPtr proteinDetectionListPtr;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(AnalysisData);


struct PWIZ_API_DECL DataCollection
{
    Inputs inputs;
    AnalysisData analysisData;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(DataCollection);


namespace IO {struct HandlerIdentData;} // forward declaration for friend


struct PWIZ_API_DECL IdentData : public Identifiable
{
    IdentData(const std::string& id_ = "",
              const std::string& creationDate_ = "");

    // attributes included in the IdentData schema
    std::string creationDate;

    ///////////////////////////////////////////////////////////////////////
    // Elements

    std::vector<CV> cvs;

    std::vector<AnalysisSoftwarePtr> analysisSoftwareList;

    Provider provider;

    std::vector<ContactPtr> auditCollection;

    AnalysisSampleCollection analysisSampleCollection;

    SequenceCollection sequenceCollection;

    AnalysisCollection analysisCollection;

    AnalysisProtocolCollection analysisProtocolCollection;

    DataCollection dataCollection;

    std::vector<BibliographicReferencePtr> bibliographicReference;

    bool empty() const;
   
    /// returns the version of this mzIdentML document;
    /// for a document created programmatically, the version is the current release version of mzIdentML;
    /// for a document created from a file/stream, the version is the schema version read from the file/stream
    const std::string& version() const;

    protected:
    std::string version_; // schema version read from the file/stream
    friend struct IO::HandlerIdentData;
};

TYPEDEF_SHARED_PTR(IdentData);


/// given a protocol and a PeptideEvidence instance, returns the PeptideEvidence as a DigestedPeptide instance
PWIZ_API_DECL proteome::DigestedPeptide digestedPeptide(const SpectrumIdentificationProtocol& sip, const PeptideEvidence& peptideEvidence);

/// given a protocol and a SpectrumIdentificationItem, builds a set of DigestedPeptides
PWIZ_API_DECL std::vector<proteome::DigestedPeptide> digestedPeptides(const SpectrumIdentificationProtocol& sip, const SpectrumIdentificationItem& sii);

/// creates a proteome::Peptide from an identdata::Peptide
PWIZ_API_DECL proteome::Peptide peptide(const Peptide& peptide);

/// creates a proteome::Modification from an identdata::Modification
PWIZ_API_DECL proteome::Modification modification(const Modification& mod);

/// returns a cleavage agent CVID for an identdata::Enzyme
PWIZ_API_DECL CVID cleavageAgent(const Enzyme& ez);

/// returns a regular expression for an identdata::Enzyme
PWIZ_API_DECL boost::regex cleavageAgentRegex(const Enzyme& ez);

/// returns a list of regular expressions for an identdata::Enzymes instance
PWIZ_API_DECL std::vector<boost::regex> cleavageAgentRegexes(const Enzymes& enzymes);

/// sets Unimod CV terms (if possible) for all SearchModifications and Modification elements
PWIZ_API_DECL void snapModificationsToUnimod(const SpectrumIdentification& si);


} // namespace identdata 
} // namespace pwiz 

#endif // _IDENTDATA_HPP_
