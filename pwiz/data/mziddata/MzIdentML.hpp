//
// $Id$
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
#include "boost/logic/tribool.hpp"
#include <vector>
#include <string>
#include <map>


namespace pwiz {
namespace mziddata {

// these types are used verbatim from MSData
using msdata::CVParam;
using msdata::UserParam;
using msdata::ParamContainer;

struct PWIZ_API_DECL IdentifiableType
{
    //    static const int INVALID_NATURAL = -1;
    IdentifiableType(const std::string& id_ = "",
                     const std::string& name_ = "");
    virtual ~IdentifiableType() {}
    
    std::string id;
    std::string name;

    virtual bool empty() const;
};

struct PWIZ_API_DECL ExternalData : public IdentifiableType
{
    ExternalData(const std::string id_ = "",
                 const std::string name_ = "");
    
    std::string location;
    
    bool empty() const;
};

struct PWIZ_API_DECL BibliographicReference : public IdentifiableType
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

typedef boost::shared_ptr<BibliographicReference> BibliographicReferencePtr;

struct PWIZ_API_DECL Contact : public IdentifiableType
{
    Contact(const std::string& id_ = "",
            const std::string& name_ = "");
    virtual ~Contact() {}
    
    std::string address;
    std::string phone;
    std::string email;
    std::string fax;
    std::string tollFreePhone;

    // For diff messages
    ParamContainer params;
    
    virtual bool empty() const;
};

typedef boost::shared_ptr<Contact> ContactPtr;

//struct PWIZ_API_DECL Organization;

struct PWIZ_API_DECL Organization : public Contact
{
    struct Parent
    {
        Parent();
        
        //std::string organization_ref;
        //boost::shared_ptr<Organization> organizationPtr;
        ContactPtr organizationPtr;
    };

    Organization(const std::string& id_ = "",
                 const std::string& name_ = "");
    Parent parent;

    virtual bool empty() const;
};

typedef boost::shared_ptr<Organization> OrganizationPtr;

struct PWIZ_API_DECL Affiliations 
{
    Affiliations(const std::string& id_ = "");
    
    //std::string organization_ref;
    //OrganizationPtr organizationPtr;
    ContactPtr organizationPtr;

    bool empty() const;
};

struct PWIZ_API_DECL Person : public Contact
{
    Person(const std::string& id_ = "",
           const std::string& name_ = "");
    
    std::string lastName;
    std::string firstName;
    std::string midInitials;
    
    std::vector<Affiliations> affiliations;

    virtual bool empty() const;
};

typedef boost::shared_ptr<Person> PersonPtr;

struct PWIZ_API_DECL ContactRole
{
    ContactRole();
    
    //std::string Contact_ref;
    ContactPtr contactPtr;
    ParamContainer role;

    bool empty() const;
};

typedef boost::shared_ptr<ContactRole> ContactRolePtr;


struct PWIZ_API_DECL Provider : public IdentifiableType
{
    Provider(const std::string id_ = "",
             const std::string name_ = "");
    
    ContactRole contactRole;

    bool empty() const;
};

typedef boost::shared_ptr<Provider> ProviderPtr;

struct PWIZ_API_DECL Material : public IdentifiableType
{
    Material(const std::string& id_ = "",
             const std::string& name_ = "");
    
    ContactRole contactRole;
    ParamContainer cvParams;

    virtual bool empty() const;
};

struct PWIZ_API_DECL Sample;

struct PWIZ_API_DECL Sample : public Material
{
    // SampleType schema elements
    struct subSample{
        subSample(const std::string& id_ = "",
                  const std::string& name_ = "");
        
        //std::string Sample_ref;
        boost::shared_ptr<Sample> samplePtr;

        bool empty() const;
    };

    Sample(const std::string& id_ = "",
           const std::string& name_ = "");
    std::vector<subSample> subSamples;

    bool empty() const;
};

typedef boost::shared_ptr<Sample> SamplePtr;

struct PWIZ_API_DECL AnalysisSoftware : public IdentifiableType
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

typedef boost::shared_ptr<AnalysisSoftware> AnalysisSoftwarePtr;

// TODO find example document w/ this in it and determine best
// representation for data model
struct PWIZ_API_DECL AnalysisSampleCollection
{
    std::vector<SamplePtr> samples;

    virtual bool empty() const;
};


struct PWIZ_API_DECL SearchDatabase : public ExternalData
{
    SearchDatabase(const std::string& id_ = "",
                   const std::string& name_ = "");
    
    std::string version;
    std::string releaseDate;
    long numDatabaseSequences;
    long numResidues;

    ParamContainer fileFormat;
    ParamContainer DatabaseName;
    ParamContainer params;

    bool empty() const;
};

typedef boost::shared_ptr<SearchDatabase> SearchDatabasePtr;


struct PWIZ_API_DECL DBSequence : public IdentifiableType
{
    DBSequence(const std::string id_ = "",
               const std::string name_ = "");
    
    int length;
    std::string accession;
    //std::string SearchDatabase_ref;
    SearchDatabasePtr searchDatabasePtr;

    std::string seq;

    ParamContainer paramGroup;

    bool empty() const;
};

typedef boost::shared_ptr<DBSequence> DBSequencePtr;

struct PWIZ_API_DECL Modification
{
    Modification();
    
    int location;
    std::string residues;
    double avgMassDelta;
    double monoisotopicMassDelta;

    ParamContainer paramGroup;

    bool empty() const;
};
    
typedef boost::shared_ptr<Modification> ModificationPtr;


struct PWIZ_API_DECL SubstitutionModification
{
    SubstitutionModification();

    std::string originalResidue;
    std::string replacementResidue;
    int location;
    double avgMassDelta;
    double monoisotopicMassDelta;

    bool empty() const;
};


struct PWIZ_API_DECL Peptide : public IdentifiableType
{
    std::string peptideSequence;
    std::vector<ModificationPtr> modification;
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

struct PWIZ_API_DECL ModParam
{
    ModParam();
    
    double massDelta;
    std::string residues;

    ParamContainer cvParams;

    bool empty() const;
};

struct PWIZ_API_DECL SearchModification
{
    SearchModification();
    
    bool fixedMod;
    
    ModParam modParam;
    ParamContainer specificityRules;

    bool empty() const;
};

typedef boost::shared_ptr<SearchModification> SearchModificationPtr;


struct PWIZ_API_DECL Enzyme
{
    Enzyme();
    
    std::string id;
    std::string nTermGain;
    std::string cTermGain;
    boost::logic::tribool semiSpecific;
    int missedCleavages;
    int minDistance;

    std::string siteRegexp;
    ParamContainer enzymeName;

    bool empty() const;
};

typedef boost::shared_ptr<Enzyme> EnzymePtr;


struct PWIZ_API_DECL Enzymes
{
    std::string independent;

    std::vector<EnzymePtr> enzymes;

    bool empty() const;
};


struct PWIZ_API_DECL Residue
{
    Residue();

    std::string Code;
    double Mass;

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

    bool empty() const;
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
    SpectrumIdentificationProtocol(const std::string& id_ = "",
                                   const std::string& name_ = "");
    
    //std::string AnalysisSoftware_ref;
    AnalysisSoftwarePtr analysisSoftwarePtr;

    ParamContainer searchType; // Only 1 element is allowed.
    ParamContainer additionalSearchParams;
    std::vector<SearchModificationPtr> modificationParams;
    Enzymes enzymes;
    MassTable massTable;
    ParamContainer fragmentTolerance;
    ParamContainer parentTolerance;
    ParamContainer threshold;
    std::vector<FilterPtr> databaseFilters;
    
    bool empty() const;
};

typedef boost::shared_ptr<SpectrumIdentificationProtocol>SpectrumIdentificationProtocolPtr;


struct PWIZ_API_DECL Measure : public IdentifiableType
{
    ParamContainer paramGroup;

    bool empty() const;
};

typedef boost::shared_ptr<Measure> MeasurePtr;


struct PWIZ_API_DECL FragmentArray
{
    std::vector<double> values;
    std::string Measure_ref;

    // Used for diffs.
    ParamContainer params;
    
    FragmentArray& setValues(const std::string& values);
    FragmentArray& setValues(const std::vector<double>& values);
    std::string getValues() const;

    bool empty() const;
};

typedef boost::shared_ptr<FragmentArray> FragmentArrayPtr;


struct PWIZ_API_DECL IonType
{
    IonType();
    
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
    PeptideEvidence();
    
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

    bool empty() const;
};

typedef boost::shared_ptr<PeptideEvidence> PeptideEvidencePtr;
struct PWIZ_API_DECL SpectrumIdentificationItem : public IdentifiableType
{
    SpectrumIdentificationItem();

    int chargeState;
    double experimentalMassToCharge;
    double calculatedMassToCharge;
    double calculatedPI;
    std::string Peptide_ref;
    int rank;
    bool passThreshold;
    std::string MassTable_ref;
    std::string Sample_ref;

    
    std::vector<PeptideEvidencePtr> peptideEvidence;
    std::vector<IonTypePtr> fragmentation;
    ParamContainer paramGroup;

    bool empty() const;
};

typedef boost::shared_ptr<SpectrumIdentificationItem> SpectrumIdentificationItemPtr;

struct PWIZ_API_DECL SpectrumIdentificationResult : public IdentifiableType
{
    std::string spectrumID;
    std::string SpectraData_ref;
    
    std::vector<SpectrumIdentificationItemPtr> spectrumIdentificationItem;
    ParamContainer paramGroup;

    bool empty() const;
};

typedef boost::shared_ptr<SpectrumIdentificationResult> SpectrumIdentificationResultPtr;

struct PWIZ_API_DECL SpectrumIdentificationList : public IdentifiableType
{
    SpectrumIdentificationList(const std::string& id_ = "",
                               const std::string& name_ = "");
    
    long numSequencesSearched;

    std::vector<MeasurePtr> fragmentationTable;
    std::vector<SpectrumIdentificationResultPtr> spectrumIdentificationResult;

    bool empty() const;
};

typedef boost::shared_ptr<SpectrumIdentificationList> SpectrumIdentificationListPtr;


struct PWIZ_API_DECL SpectrumIdentification : public IdentifiableType
{
    SpectrumIdentification(const std::string& id_ = "",
                           const std::string& name_ = "");
    
    //std::string SpectrumIdentificationProtocol_ref;
    SpectrumIdentificationProtocolPtr spectrumIdentificationProtocolPtr;
    //std::string SpectrumIdentificationList_ref;
    SpectrumIdentificationListPtr spectrumIdentificationListPtr;
    std::string activityDate;

    std::vector<std::string> inputSpectra;
    std::vector<std::string> searchDatabase;

    bool empty() const;
};

typedef boost::shared_ptr<SpectrumIdentification> SpectrumIdentificationPtr;

struct PWIZ_API_DECL ProteinDetectionProtocol : public IdentifiableType
{
    ProteinDetectionProtocol(const std::string& id_ = "",
                             const std::string& name_ = "");
    
    //std::string AnalysisSoftware_ref;
    AnalysisSoftwarePtr analysisSoftwarePtr;

    ParamContainer analysisParams;
    ParamContainer threshold;

    bool empty() const;
};

typedef boost::shared_ptr<ProteinDetectionProtocol> ProteinDetectionProtocolPtr;


struct PWIZ_API_DECL ProteinDetectionHypothesis : public IdentifiableType
{
    ProteinDetectionHypothesis();

    std::string DBSequence_ref;
    bool passThreshold;

    // written out in the PeptideEvidence_Ref attribute of the
    // PeptideHypothesis tag
    std::vector<std::string> peptideHypothesis;
    ParamContainer paramGroup;

    bool empty() const;
};

typedef boost::shared_ptr<ProteinDetectionHypothesis> ProteinDetectionHypothesisPtr;


struct PWIZ_API_DECL ProteinAmbiguityGroup : public IdentifiableType
{
    std::vector<ProteinDetectionHypothesisPtr> proteinDetectionHypothesis;
    ParamContainer paramGroup;

    bool empty() const;
};

typedef boost::shared_ptr<ProteinAmbiguityGroup> ProteinAmbiguityGroupPtr;

struct PWIZ_API_DECL ProteinDetectionList : public IdentifiableType
{
    ProteinDetectionList(const std::string& id_ = "",
                         const std::string& name_ = "");
    
    std::vector<ProteinAmbiguityGroupPtr> proteinAmbiguityGroup;
    ParamContainer paramGroup;

    bool empty() const;
};

typedef boost::shared_ptr<ProteinDetectionList> ProteinDetectionListPtr;


struct PWIZ_API_DECL ProteinDetection : public IdentifiableType
{
    ProteinDetection(const std::string id_ = "",
                     const std::string name_ = "");
    
    //std::string ProteinDetectionProtocol_ref;
    ProteinDetectionProtocolPtr proteinDetectionProtocolPtr;
    //std::string ProteinDetectionList_ref;
    ProteinDetectionListPtr proteinDetectionListPtr;
    std::string activityDate;

    std::vector<SpectrumIdentificationListPtr> inputSpectrumIdentifications;

    virtual bool empty() const;
};

typedef boost::shared_ptr<ProteinDetection> ProteinDetectionPtr;

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

typedef boost::shared_ptr<AnalysisProtocolCollection> AnalysisProtocolCollectionPtr;


struct PWIZ_API_DECL SpectraData : public IdentifiableType
{
    std::string location;

    std::vector<std::string> externalFormatDocumentation;
    ParamContainer fileFormat;
    ParamContainer spectrumIDFormat;

    bool empty() const;
};

typedef boost::shared_ptr<SpectraData> SpectraDataPtr;

struct PWIZ_API_DECL SourceFile : public IdentifiableType
{
    std::string location;
    ParamContainer fileFormat;

    std::vector<std::string> externalFormatDocumentation;

    ParamContainer paramGroup;

    bool empty() const;
};

typedef boost::shared_ptr<SourceFile> SourceFilePtr;

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

typedef boost::shared_ptr<Inputs> InputsPtr;





/// AnalysisData element. 
struct PWIZ_API_DECL AnalysisData
{
    std::vector<SpectrumIdentificationListPtr> spectrumIdentificationList;
    ProteinDetectionListPtr proteinDetectionListPtr;

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
    MzIdentML(const std::string& id_ = "",
              const std::string& version_ = "1.0.0",
              const std::string& creationDate_ = "");
    
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
