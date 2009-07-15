//
// MzIdentML.hpp
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


#ifndef _MZIDDATA_HPP_
#define _MZIDDATA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/data/msdata/cv.hpp"
#include "pwiz/data/msdata/CVParam.hpp"
#include "boost/shared_ptr.hpp"
#include <vector>
#include <string>
#include <map>


namespace pwiz {
namespace mziddata {

// these types are used verbatim from MSData
using msdata::CVParam;
using msdata::UserParam;

/// The base class for elements that may contain cvParams or userParams
struct PWIZ_API_DECL ParamContainer
{
    /// a collection of controlled vocabulary terms
    std::vector<CVParam> cvParams;

    /// a collection of uncontrolled user terms
    std::vector<UserParam> userParams;
    
    /// finds cvid in the container:
    /// - returns first CVParam result such that (result.cvid == cvid); 
    /// - if not found, returns CVParam(CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam cvParam(CVID cvid) const; 

    /// finds child of cvid in the container:
    /// - returns first CVParam result such that (result.cvid is_a cvid); 
    /// - if not found, CVParam(CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam cvParamChild(CVID cvid) const; 

    /// returns true iff cvParams contains exact cvid (recursive)
    bool hasCVParam(CVID cvid) const;

    /// returns true iff cvParams contains a child (is_a) of cvid (recursive)
    bool hasCVParamChild(CVID cvid) const;

    /// finds UserParam with specified name 
    /// - returns UserParam() if name not found 
    /// - not recursive: looks only at local userParams
    UserParam userParam(const std::string&) const; 

    /// set/add a CVParam (not recursive)
    void set(CVID cvid, const std::string& value = "", CVID units = CVID_Unknown);

    /// set/add a CVParam (not recursive)
    template <typename value_type>
    void set(CVID cvid, value_type value, CVID units = CVID_Unknown)
    {
        set(cvid, boost::lexical_cast<std::string>(value), units);
    }

    /// returns true iff the element contains no params or param groups
    bool empty() const;

    /// clears the collections
    void clear();

    /// returns true iff this and that have the exact same cvParams and userParams
    bool operator==(const ParamContainer& that) const;

    /// returns !(this==that)
    bool operator!=(const ParamContainer& that) const;
};

struct PWIZ_API_DECL IdentifiableType
{
    virtual ~IdentifiableType() {}
    
    std::string id;
    std::string name;

    virtual bool empty() const;
};

struct PWIZ_API_DECL BibliographicReference : public IdentifiableType
{
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

typedef boost::shared_ptr<BibliographicReference> BibliographicReferencePtr;

struct PWIZ_API_DECL ContactRole
{
    std::string Contact_ref;
    ParamContainer role;

    bool empty() const;
};

struct PWIZ_API_DECL Contact : public IdentifiableType
{
    virtual ~Contact() {}
    
    std::string address;
    std::string phone;
    std::string email;
    std::string fax;
    std::string tollFreePhone;

    virtual bool empty() const;
};

typedef boost::shared_ptr<Contact> ContactPtr;

struct PWIZ_API_DECL Affiliations 
{
    std::string organization_ref;

    bool empty() const;
};

struct PWIZ_API_DECL Person : public Contact
{
    std::string lastName;
    std::string firstName;
    std::string midInitials;
    
    std::vector<Affiliations> affiliations;

    virtual bool empty() const;
};

typedef boost::shared_ptr<Person> PersonPtr;

struct PWIZ_API_DECL Organization : public Contact
{
    struct Parent
    {
        std::string organization_ref;
    };

    Parent parent;

    virtual bool empty() const;
};

typedef boost::shared_ptr<Organization> OrganizationPtr;

struct PWIZ_API_DECL Provider : public IdentifiableType // : public Contact
{
    ContactRole contactRole;
};

typedef boost::shared_ptr<Provider> ProviderPtr;

struct PWIZ_API_DECL Material : public IdentifiableType
{
    ContactRole contactRole;

    ParamContainer cvParam;
};

struct PWIZ_API_DECL Sample : public Material
{
    // SampleType schema elements
    struct Component{
        std::string Sample_ref;

        bool empty() const;
    };

    std::vector<Component> components;
    
};

typedef boost::shared_ptr<Sample> SamplePtr;

struct PWIZ_API_DECL AnalysisSoftware : public IdentifiableType
{
    // SoftwareType attributes
    std::string version;

    // SoftwareType elements
    ContactRole contactRole;
    ParamContainer softwareName;

    // Included in examples, but not in schema
    std::string URI;
    std::string customizations;

    virtual bool empty() const;
};

typedef boost::shared_ptr<AnalysisSoftware> AnalysisSoftwarePtr;

// TODO find example document w/ this in it and determine best
// representation for data model
struct PWIZ_API_DECL AnalysisSampleCollection
{
    std::vector<SamplePtr> samples;

    virtual bool empty() const;
};


typedef boost::shared_ptr<AnalysisSampleCollection> AnalysisSampleCollectionPtr;

struct PWIZ_API_DECL DBSequence : public IdentifiableType
{
    std::string length;
    std::string accession;
    std::string SearchDatabase_ref;

    std::string seq;

    ParamContainer paramGroup;
};

typedef boost::shared_ptr<DBSequence> DBSequencePtr;

struct PWIZ_API_DECL Modification
{
    std::string location;
    std::string residues;
    std::string avgMassDelta;
    std::string monoisotopicMassDelta;

    ParamContainer paramGroup;

    bool empty() const;
};
    

struct PWIZ_API_DECL SubstitutionModification
{
    std::string originalResidue;
    std::string replacementResidue;
    std::string location;
    std::string avgMassDelta;
    std::string monoisotopicMassDelta;

    bool empty() const;
};


struct PWIZ_API_DECL Peptide : public IdentifiableType
{
    std::string peptideSequence;
    Modification modification;
    SubstitutionModification substitutionModification;

    ParamContainer paramGroup;
};

typedef boost::shared_ptr<Peptide> PeptidePtr;

struct PWIZ_API_DECL SequenceCollection
{
    std::vector<DBSequencePtr> dbSequences;
    std::vector<PeptidePtr> peptides;

    bool empty() const;
};

struct PWIZ_API_DECL SpectrumIdentification : public IdentifiableType
{
    std::string SpectrumIdentificationProtocol_ref;
    std::string SpectrumIdentificationList_ref;
    std::string activityDate;

    std::vector<std::string> inputSpectra;
    std::vector<std::string> searchDatabase;
};

typedef boost::shared_ptr<SpectrumIdentification> SpectrumIdentificationPtr;

struct PWIZ_API_DECL ProteinDetection : public IdentifiableType
{
    std::string ProteinDetectionProtocol_ref;
    std::string ProteinDetectionList_ref;
    std::string activityDate;

    std::vector< std::string > inputSpectrumIdentifications;

    virtual bool empty() const;
};

typedef boost::shared_ptr<ProteinDetection> ProteinDetectionPtr;

struct PWIZ_API_DECL AnalysisCollection
{
    std::vector<SpectrumIdentificationPtr> spectrumIdentification;
    ProteinDetection proteinDetection;

    bool empty() const;
};

struct PWIZ_API_DECL ModParam
{
    std::string massDelta;
    std::string residues;

    std::vector<CVParam> cvParams;
};

struct PWIZ_API_DECL SearchModification
{
    std::string fixedMod;
    
    ModParam modParam;
    std::vector<CVParam> specificityRules;

    bool empty() const;
};

typedef boost::shared_ptr<SearchModification> SearchModificationPtr;


struct PWIZ_API_DECL Enzyme
{
    std::string id;
    std::string nTermGain;
    std::string cTermGain;
    std::string semiSpecific;
    std::string missedCleavages;
    std::string minDistance;

    std::string siteRegexp;
    ParamContainer enzymeName;

    bool empty() const;
};

typedef boost::shared_ptr<Enzyme> EnzymePtr;


struct PWIZ_API_DECL Enzymes
{
    std::string independent;

    std::vector<EnzymePtr> enzymes;
};

struct PWIZ_API_DECL Residue
{
    std::string Code;
    std::string Mass;

    bool empty() const;
};

typedef boost::shared_ptr<Residue> ResiduePtr;


struct PWIZ_API_DECL AmbiguousResidue
{
    std::string Code;
    
    ParamContainer params;

    bool empty() const;
};

typedef boost::shared_ptr<AmbiguousResidue> AmbiguousResiduePtr;


struct PWIZ_API_DECL MassTable
{
    std::string id;
    std::string msLevel;
    
    std::vector<ResiduePtr> residues;
    std::vector<AmbiguousResiduePtr> ambiguousResidue; 
};

struct PWIZ_API_DECL Filter
{
    ParamContainer filterType;
    ParamContainer include;
    ParamContainer exclude;

    bool empty() const;
};

typedef boost::shared_ptr<Filter> FilterPtr;

struct PWIZ_API_DECL SpectrumIdentificationProtocol : public IdentifiableType
{
    std::string AnalysisSoftware_ref;

    ParamContainer searchType; // Only 1 element is allowed.
    ParamContainer additionalSearchParams;
    std::vector<SearchModificationPtr> modificationParams;
    Enzymes enzymes;
    MassTable massTable;
    ParamContainer fragmentTolerance;
    ParamContainer parentTolerance;
    ParamContainer threshold;
    std::vector<FilterPtr> databaseFilters;
};

typedef boost::shared_ptr<SpectrumIdentificationProtocol> SpectrumIdentificationProtocolPtr;


struct PWIZ_API_DECL ProteinDetectionProtocol : public IdentifiableType
{
    std::string AnalysisSoftware_ref;

    ParamContainer analysisParams;
    ParamContainer threshold;
};

typedef boost::shared_ptr<ProteinDetectionProtocol> ProteinDetectionProtocolPtr;


struct PWIZ_API_DECL AnalysisProtocolCollection
{
    std::vector<SpectrumIdentificationProtocolPtr> spectrumIdentificationProtocol;
    std::vector<ProteinDetectionProtocolPtr> proteinDetectionProtocol;

    
    bool empty() const;
};

typedef boost::shared_ptr<AnalysisProtocolCollection> AnalysisProtocolCollectionPtr;


struct PWIZ_API_DECL SpectraData : public IdentifiableType
{
    std::string location;

    std::vector<std::string> externalFormatDocumentation;
    ParamContainer fileFormat;
};

typedef boost::shared_ptr<SpectraData> SpectraDataPtr;

struct PWIZ_API_DECL SearchDatabase : public IdentifiableType
{
    SearchDatabase();
    
    std::string version;
    std::string releaseDate;
    int numDatabaseSequences;
    int numResidues;

    ParamContainer fileFormat;
    ParamContainer DatabaseName;
};

typedef boost::shared_ptr<SearchDatabase> SearchDatabasePtr;


struct PWIZ_API_DECL SourceFile : public IdentifiableType
{
    std::string location;
    ParamContainer fileFormat;

    std::vector<std::string> externalFormatDocumentation;

    ParamContainer paramGroup;
};

typedef boost::shared_ptr<SourceFile> SourceFilePtr;

/// DataCollection's Input element. Contains 0+ of SourceFile,
/// SearchDatabase, SpectraData
struct PWIZ_API_DECL Inputs
{
    // Replace these 3 members w/ their types
    std::vector<SourceFilePtr> sourceFile;
    std::vector<SearchDatabasePtr> searchDatabase;
    std::vector<SpectraDataPtr> spectraData;
    
    bool empty() const;
};

typedef boost::shared_ptr<Inputs> InputsPtr;


struct PWIZ_API_DECL Measure : public IdentifiableType
{
    ParamContainer paramGroup;
};

typedef boost::shared_ptr<Measure> MeasurePtr;

struct PWIZ_API_DECL FragmentArray
{
    std::vector<float> values;
    std::string Measure_ref;

    FragmentArray& setValues(const std::string& values);
    FragmentArray& setValues(const std::vector<float>& values);
    std::string getValues() const;

    bool empty() const;
};

typedef boost::shared_ptr<FragmentArray> FragmentArrayPtr;


struct PWIZ_API_DECL IonType
{
    std::vector<int> index;
    int charge;

    ParamContainer paramGroup;
    std::vector<FragmentArrayPtr> fragmentArray;

    IonType& setIndex(const std::string& value);
    IonType& setIndex(const std::vector<int>& value);

    std::string getIndex() const;

    bool empty() const;
};

typedef boost::shared_ptr<IonType> IonTypePtr;


struct PWIZ_API_DECL PeptideEvidence : public IdentifiableType
{
    std::string DBSequence_ref;
    int start;
    int end;
    std::string pre;
    std::string post;
    std::string TranslationTable_ref;
    int frame;
    bool isDecoy;
    int missedCleavages;
    
    ParamContainer paramGroup;
};

typedef boost::shared_ptr<PeptideEvidence> PeptideEvidencePtr;


struct PWIZ_API_DECL SpectrumIdentificationItem : public IdentifiableType
{
    SpectrumIdentificationItem() : chargeState(0), experimentalMassToCharge(0), calculatedMassToCharge(0), calculatedPI(0), rank(0), passThreshold(0) {}

    int chargeState;
    double experimentalMassToCharge;
    double calculatedMassToCharge;
    float calculatedPI;
    std::string Peptide_ref;
    int rank;
    bool passThreshold;
    std::string MassTable_ref;
    std::string Sample_ref;

    
    std::vector<PeptideEvidencePtr> peptideEvidence;
    std::vector<IonTypePtr> fragmentation;
    ParamContainer paramGroup;
};

typedef boost::shared_ptr<SpectrumIdentificationItem> SpectrumIdentificationItemPtr;

struct PWIZ_API_DECL SpectrumIdentificationResult : public IdentifiableType
{
    std::string spectrumID;
    std::string SpectraData_ref;
    
    std::vector<SpectrumIdentificationItemPtr> spectrumIdentificationItem;
    ParamContainer paramGroup;
};

typedef boost::shared_ptr<SpectrumIdentificationResult> SpectrumIdentificationResultPtr;

struct PWIZ_API_DECL ProteinDetectionHypothesis : public IdentifiableType
{
    std::string DBSequence_ref;
    bool passThreshold;

    // written out in the PeptideEvidence_Ref attribute of the
    // PeptideHypothesis tag
    std::vector<std::string> peptideHypothesis;
    ParamContainer paramGroup;
};

typedef boost::shared_ptr<ProteinDetectionHypothesis> ProteinDetectionHypothesisPtr;


struct PWIZ_API_DECL ProteinAmbiguityGroup : public IdentifiableType
{
    std::vector<ProteinDetectionHypothesisPtr> proteinDetectionHypothesis;
    ParamContainer paramGroup;
};

typedef boost::shared_ptr<ProteinAmbiguityGroup> ProteinAmbiguityGroupPtr;

struct PWIZ_API_DECL SpectrumIdentificationList : public IdentifiableType
{
    int numSequencesSearched;

    std::vector<MeasurePtr> fragmentationTable;
    std::vector<SpectrumIdentificationResultPtr> spectrumIdentificationResult;
};

typedef boost::shared_ptr<SpectrumIdentificationList> SpectrumIdentificationListPtr;


struct PWIZ_API_DECL ProteinDetectionList : public IdentifiableType
{
    std::vector<ProteinAmbiguityGroupPtr> proteinAmbiguityGroup;
    ParamContainer paramGroup;
};

typedef boost::shared_ptr<ProteinDetectionList> ProteinDetectionListPtr;


/// DataCollection's AnalysisData element. 
struct PWIZ_API_DECL AnalysisData
{
    std::vector<SpectrumIdentificationListPtr> spectrumIdentificationList;
    ProteinDetectionList proteinDetectionList;

    bool empty() const;
};

typedef boost::shared_ptr<AnalysisData> AnalysisDataPtr;

struct PWIZ_API_DECL DataCollection
{
    Inputs inputs;
    AnalysisData analysisData;

    bool empty() const;
};

typedef boost::shared_ptr<DataCollection> DataCollectionPtr;

struct PWIZ_API_DECL MzIdentML : public IdentifiableType
{
    std::string version;

    // attributes included in the MzIdentML schema
    std::string creationDate;

    ///////////////////////////////////////////////////////////////////////
    // Elements

    std::vector<pwiz::CV> cvs;

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
};

typedef boost::shared_ptr<MzIdentML> MzIdentMLPtr;

} // namespace mziddata 
} // namespace pwiz 

#endif // _MZIDDATA_HPP_
