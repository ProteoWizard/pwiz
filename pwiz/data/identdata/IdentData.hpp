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

/// Parent class representing extensions of the IdentifiableType from
/// the mzIdentML schema.
///
/// Other classes in the model can be specified as sub-classes,
/// inheriting from Identifiable. Identifiable gives classes a unique
/// identifier within the scope and a name that need not be unique.
struct PWIZ_API_DECL Identifiable
{
    Identifiable(const std::string& id_ = "",
                 const std::string& name_ = "");
    virtual ~Identifiable() {}

    std::string id;
    std::string name;

    virtual bool empty() const;
};

/// Parent class of all Identifiable objects that have ParamGroups.
///
/// Represents bibliographic references.
struct PWIZ_API_DECL IdentifiableParamContainer : public ParamContainer
{
    IdentifiableParamContainer(const std::string& id_ = "",
                 const std::string& name_ = "");
    virtual ~IdentifiableParamContainer() {}

    std::string id;
    std::string name;

    virtual bool empty() const;
};

/// Implementation for the BibliographicReferenceType tag in the
/// mzIdentML schema.
///
/// Represents bibliographic references.
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

/// Implementation of ContactType from mzIdentML.
///
/// A contact is either a person or an organization.
struct PWIZ_API_DECL Contact : public IdentifiableParamContainer
{
    Contact(const std::string& id_ = "",
            const std::string& name_ = "");
    virtual ~Contact() {}

    virtual bool empty() const;
};

TYPEDEF_SHARED_PTR(Contact);

/// \brief Implementation of AbstractOrganizationType from the
/// mzIdentML schema.
///
/// Organizations are entities like companies, universities,
/// government agencies. Any additional information such as the
/// address, email etc. should be supplied either as CV parameters or
/// as user parameters.
struct PWIZ_API_DECL Organization : public Contact
{
    Organization(const std::string& id_ = "",
                 const std::string& name_ = "");

    boost::shared_ptr<Organization> parent;

    virtual bool empty() const;
};

TYPEDEF_SHARED_PTR(Organization);

/// Implementation of PersonType from the mzIdentML schema.
///
/// A person's name and contact details. Any additional information
/// such as the address, contact email etc. should be supplied using
/// CV parameters or user parameters.
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

/// Implementation of ContactRoleType from the mzIdentML schema.
///
/// The role that a Contact plays in an organization or with respect
/// to the associating class. A Contact may have several Roles within
/// scope, and as such, associations to ContactRole allow the use of a
/// Contact in a certain manner. Examples might include a provider, or
/// a data analyst.
struct PWIZ_API_DECL ContactRole : public CVParam
{
    ContactRole(CVID role_ = CVID_Unknown,
                const ContactPtr& contactPtr_ = ContactPtr());

    ContactPtr contactPtr;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(ContactRole);

/// Implementation of the SampleType from the mzIdentML schema.
///
/// A description of the sample analysed by mass spectrometry using
/// CVParams or UserParams. If a composite sample has been analysed, a
/// parent sample should be defined, which references subsamples. This
/// represents any kind of substance used in an experimental workflow,
/// such as whole organisms, cells, DNA, solutions, compounds and
/// experimental substances (gels, arrays etc.).
struct PWIZ_API_DECL Sample : public IdentifiableParamContainer
{
    Sample(const std::string& id_ = "",
           const std::string& name_ = "");

    std::vector<ContactRolePtr> contactRole;
    std::vector<boost::shared_ptr<Sample> > subSamples;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Sample);

/// Implementation of AnalysisSoftwareType from the mzIdentML schema.
///
/// The software used for performing the analyses.
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

/// Implementation of ProviderType from the mzIdentML schema.
///
/// The provider of the document in terms of the Contact and the
/// software the produced the document instance.
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

/// Implementation of AnalysisSampleCollectionType from mzIdentML
/// schema.
///
/// The samples analysed can optionally be recorded using CV terms for
/// descriptions. If a composite sample has been analysed, the
/// subsample association can be used to build a hierarchical
/// description.
struct PWIZ_API_DECL AnalysisSampleCollection
{
    std::vector<SamplePtr> samples;

    bool empty() const;
};


/// Implementation of SearchDatabaseType from the mzIdentML schema.
///
/// A database for searching mass spectra. Examples include a set of
/// amino acid sequence entries, or annotated spectra libraries.
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


/// Implementation of DBSequenceType from the mzIdentML schema.
///
/// A database sequence from the specified SearchDatabase (nucleic
/// acid or amino acid). If the sequence is nucleic acid, the source
/// nucleic acid sequence should be given in the seq attribute rather
/// than a translated sequence.
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


/// Implementation of ModificationType from the mzIdentML schema.
///
/// A molecule modification specification. If n modifications have
/// been found on a peptide, there should be n instances of
/// Modification. If multiple modifications are provided as cvParams,
/// it is assumed that the modification is ambiguous i.e. one
/// modification or another. A cvParam must be provided with the
/// identification of the modification sourced from a suitable CV
/// e.g. UNIMOD. If the modification is not present in the CV (and
/// this will be checked by the semantic validator within a given
/// tolerance window), there is a â€œunknown modificationâ€ CV
/// term that must be used instead. A neutral loss should be defined
/// as an additional CVParam within Modification. If more complex
/// information should be given about neutral losses (such as
/// presence/absence on particular product ions), this can
/// additionally be encoded within the FragmentationArray.
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


/// Implementation of SubstitutionModificationType from the mzIdentML
/// schema.
///
/// A modification where one residue is substituted by another (amino
/// acid change).
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

/// Implementation of PeptideType from the mzIdentML schema.
///
/// One (poly)peptide (a sequence with modifications). The combination
/// of Peptide sequence and modifications must be unique in the file.
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


/// Implementation of SearchModificationType from the mzIdentML schema.
///
/// Filters applied to the search database. The filter must include at
/// least one of Include and Exclude. If both are used, it is assumed
/// that inclusion is performed first.
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


/// Implementation of EnzymeType from the mzIdentML schema.
///
/// The details of an individual cleavage enzyme should be provided by
/// giving a regular expression or a CV term if a "standard" enzyme
/// cleavage has been performed.
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


/// Implementation of EnzymesType from the mzIdentML schema.
///
/// The list of enzymes used in experiment.
struct PWIZ_API_DECL Enzymes
{
    boost::logic::tribool independent;

    std::vector<EnzymePtr> enzymes;

    bool empty() const;
};


/// Implementation of ResidueType from the mzIdentML schema.
///
/// Representation of the Residue tags that holds a letter code and
/// residue mass in Daltons (not including any fixed modifications).
struct PWIZ_API_DECL Residue
{
    Residue();

    char code;
    double mass;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Residue);


/// Implementation of AmbiguousResidueType from the mzIdentML schema.
///
/// Ambiguous residues e.g. X can be specified by the Code attribute
/// and a set of parameters for example giving the different masses
/// that will be used in the search.
struct PWIZ_API_DECL AmbiguousResidue : public ParamContainer
{
    AmbiguousResidue();

    char code;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(AmbiguousResidue);


/// Implementation of MassTableType from the mzIdentML schema.
///
/// Ambiguous residues e.g. X can be specified by the Code attribute
/// and a set of parameters for example giving the different masses
/// that will be used in the search.
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


/// Implementation of FilterType from the mzIdentML schema.
///
/// Filters applied to the search database. The filter must include at
/// least one of Include and Exclude. If both are used, it is assumed
/// that inclusion is performed first.
struct PWIZ_API_DECL Filter
{
    ParamContainer filterType;
    ParamContainer include;
    ParamContainer exclude;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Filter);


/// Implementation of TranslationTableType from the mzIdentML schema.
///
/// The table used to translate codons into nucleic acids e.g. by
/// reference to the NCBI translation table.
struct PWIZ_API_DECL TranslationTable : public IdentifiableParamContainer
{
    TranslationTable(const std::string& id = "",
                     const std::string& name = "");
};

TYPEDEF_SHARED_PTR(TranslationTable);


/// Implementation of DatabaseTranslationType from the mzIdentML schema.
///
/// A specification of how a nucleic acid sequence database was
/// translated for searching.
struct PWIZ_API_DECL DatabaseTranslation
{
    std::vector<int> frames;
    std::vector<TranslationTablePtr> translationTable;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(DatabaseTranslation);


/// Implementation of SpectrumIdentificationProtocolType from the
/// mzIdentML schema.
///
/// The parameters and settings of a SpectrumIdentification analysis.
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


/// Implementation of MeasureType from the mzIdentML schema.
///
/// References to CV terms defining the measures about product ions to
/// be reported in SpectrumIdentificationItem.
struct PWIZ_API_DECL Measure : public IdentifiableParamContainer
{
    Measure(const std::string id = "",
            const std::string name = "");

    bool empty() const;
};

TYPEDEF_SHARED_PTR(Measure);


/// Implementation of FragmentArrayType from the mzIdentML schema.
///
/// Contains the types of measures that will be reported in generic
/// arrays for each SpectrumIdentificationItem e.g. product ion m/z,
/// product ion intensity, product ion m/z error
struct PWIZ_API_DECL FragmentArray
{
    std::vector<double> values;
    MeasurePtr measurePtr;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(FragmentArray);


/// Implementation of IonTypeType from the mzIdentML schema.
///
/// IonType defines the index of fragmentation ions being reported,
/// importing a CV term for the type of ion e.g. b ion. Example: if b3
/// b7 b8 and b10 have been identified, the index attribute will
/// contain 3 7 8 10, and the corresponding values will be reported in
/// parallel arrays below
struct PWIZ_API_DECL IonType : public CVParam
{
    IonType();

    std::vector<int> index;
    int charge;
    std::vector<FragmentArrayPtr> fragmentArray;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(IonType);


/// Implementation of PeptideEvidenceType from the mzIdentML schema.
///
/// PeptideEvidence links a specific Peptide element to a specific
/// position in a DBSequence. There must only be one PeptideEvidence
/// item per Peptide-to-DBSequence-position.
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


/// Implementation of SequenceCollectionType from the mzIdentML schema.
///
/// The collection of sequences (DBSequence or Peptide) identified and
/// their relationship between each other (PeptideEvidence) to be
/// referenced elsewhere in the results.
struct PWIZ_API_DECL SequenceCollection
{
    std::vector<DBSequencePtr> dbSequences;
    std::vector<PeptidePtr> peptides;
    std::vector<PeptideEvidencePtr> peptideEvidence;
    bool empty() const;
};


/// Implementation of SpectrumIdentificationItemType from the
/// mzIdentML schema.
///
/// An identification of a single (poly)peptide, resulting from
/// querying an input spectra, along with the set of confidence values
/// for that identification.  PeptideEvidence elements should be given
/// for all mappings of the corresponding Peptide sequence within
/// protein sequences.
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


/// Implementation of SpectraDataType from the mzIdentML schema.
///
/// A data set containing spectra data (consisting of one or more
/// spectra).
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


/// Implementation of SpectrumIdentificationResultType from the
/// mzIdentML schema.
///
/// All identifications made from searching one spectrum. For PMF
/// data, all peptide identifications will be listed underneath as
/// SpectrumIdentificationItems. For MS/MS data, there will be ranked
/// SpectrumIdentificationItems corresponding to possible different
/// peptide IDs.
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


/// Implementation of SpectrumIdentificationListType from the
/// mzIdentML schema.
///
/// Represents the set of all search results from
/// SpectrumIdentification.
struct PWIZ_API_DECL SpectrumIdentificationList : public IdentifiableParamContainer
{
    SpectrumIdentificationList(const std::string& id_ = "",
                               const std::string& name_ = "");

    long numSequencesSearched;

    std::vector<MeasurePtr> fragmentationTable;
    std::vector<SpectrumIdentificationResultPtr> spectrumIdentificationResult;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SpectrumIdentificationList);


/// Implementation of SpectrumIdentificationType from the mzIdentML schema.
///
/// An Analysis which tries to identify peptides in input spectra,
/// referencing the database searched, the input spectra, the output
/// results and the protocol that is run.
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


/// Implementation of ProteinDetectionProtocolType from the mzIdentML
/// schema.
///
/// The parameters and settings of a ProteinDetection process.
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


/// Implementation of PeptideHypothesisType from the mzIdentML schema.
///
/// Peptide evidence on which this ProteinHypothesis is based by
/// reference to a PeptideEvidence element.
struct PWIZ_API_DECL PeptideHypothesis
{
    PeptideEvidencePtr peptideEvidencePtr;
    std::vector<SpectrumIdentificationItemPtr> spectrumIdentificationItemPtr;

    bool empty() const;
};


/// Implementation of ProteinDetectionHypothesisType from the
/// mzIdentML schema.
///
/// A single result of the ProteinDetection analysis (i.e. a protein). 
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


/// Implementation of ProteinAmbiguityGroupType from the mzIdentML schema.
///
/// A set of logically related results from a protein detection, for
/// example to represent conflicting assignments of peptides to
/// proteins.
struct PWIZ_API_DECL ProteinAmbiguityGroup : public IdentifiableParamContainer
{
    ProteinAmbiguityGroup(const std::string& id_ = "",
                          const std::string& name_ = "");

    std::vector<ProteinDetectionHypothesisPtr> proteinDetectionHypothesis;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(ProteinAmbiguityGroup);


/// Implementation of ProteinDetectionListType from the mzIdentML schema.
///
/// The protein list resulting from a protein detection process.
struct PWIZ_API_DECL ProteinDetectionList : public IdentifiableParamContainer
{
    ProteinDetectionList(const std::string& id_ = "",
                         const std::string& name_ = "");

    std::vector<ProteinAmbiguityGroupPtr> proteinAmbiguityGroup;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(ProteinDetectionList);


/// Implementation of ProteinDetectionType from the mzIdentML schema.
///
/// An Analysis which assembles a set of peptides (e.g. from a spectra
/// search analysis) to proteins.
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


/// Implementation of AnalysisCollectionType from the mzIdentML schema
///
/// The analyses performed to get the results, which map the input and
/// output data sets. Analyses are for example: SpectrumIdentification
/// (resulting in peptides) or ProteinDetection (assemble proteins
/// from peptides).
struct PWIZ_API_DECL AnalysisCollection
{
    std::vector<SpectrumIdentificationPtr> spectrumIdentification;
    ProteinDetection proteinDetection;

    bool empty() const;
};


/// Implementation of AnalysisProtocolCollectionType from the
/// mzIdentML schema.
///
/// The collection of protocols which include the parameters and
/// settings of the performed analyses.
struct PWIZ_API_DECL AnalysisProtocolCollection
{
    std::vector<SpectrumIdentificationProtocolPtr> spectrumIdentificationProtocol;
    std::vector<ProteinDetectionProtocolPtr> proteinDetectionProtocol;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(AnalysisProtocolCollection);


/// Implementation of SourceFileType from the mzIdentML schema.
///
/// A file from which this mzIdentML instance was created.
struct PWIZ_API_DECL SourceFile : public IdentifiableParamContainer
{
    std::string location;
    CVParam fileFormat;

    std::vector<std::string> externalFormatDocumentation;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(SourceFile);


/// Implementation of the InputsType from the mzIdentML schema.
///
/// The inputs to the analyses including the databases searched, the
/// spectral data and the source file converted to mzIdentML.
///
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


/// Implementation of AnalysisDataType from the mzIdentML schema.
///
/// Data sets generated by the analyses, including peptide and protein
/// lists.
struct PWIZ_API_DECL AnalysisData
{
    std::vector<SpectrumIdentificationListPtr> spectrumIdentificationList;
    ProteinDetectionListPtr proteinDetectionListPtr;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(AnalysisData);


/// Implementation of DataCollectionType from the mzIdentML schema.
///
/// The collection of input and output data sets of the analyses.
struct PWIZ_API_DECL DataCollection
{
    Inputs inputs;
    AnalysisData analysisData;

    bool empty() const;
};

TYPEDEF_SHARED_PTR(DataCollection);


namespace IO {struct HandlerIdentData;} // forward declaration for friend


/// Implementation of the MzIdentMLType from the mzIdentML schema.
/// 
/// The upper-most hierarchy level of mzIdentML with sub-containers
/// for example describing software, protocols and search results
/// (spectrum identifications or protein detection results).
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

/// returns a list of cleavage agent CVIDs for an identdata::Enzymes instance
PWIZ_API_DECL std::vector<CVID> cleavageAgents(const Enzymes& enzymes);

/// returns a regular expression for an identdata::Enzyme
PWIZ_API_DECL std::string cleavageAgentRegex(const Enzyme& ez);

/// returns a list of regular expressions for an identdata::Enzymes instance
PWIZ_API_DECL std::vector<std::string> cleavageAgentRegexes(const Enzymes& enzymes);

/// sets Unimod CV terms (if possible) for all SearchModifications and Modification elements
PWIZ_API_DECL void snapModificationsToUnimod(const SpectrumIdentification& si);


} // namespace identdata 
} // namespace pwiz 

#endif // _IDENTDATA_HPP_
