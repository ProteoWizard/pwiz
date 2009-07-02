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

struct IdentifiableType
{
    virtual ~IdentifiableType() {}
    
    std::string id;
    std::string name;

    virtual bool empty() const;
};

struct PWIZ_API_DECL BibliographicReference
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
    std::vector<CVParam> role;

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

struct PWIZ_API_DECL Person : public Contact
{
    std::string lastName;
    std::string firstName;
    std::string midInitials;
    
    std::vector<std::string> affiliations;

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

struct PWIZ_API_DECL Provider : public Contact
{
};

typedef boost::shared_ptr<Provider> ProviderPtr;

struct PWIZ_API_DECL Material : public IdentifiableType
{
    ContactRole contactRole;

    std::vector<CVParam> cvParam;
};

struct PWIZ_API_DECL Sample : public Material
{
    // SampleType schema elements
    struct Component{
        std::string Sample_ref;
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

struct PWIZ_API_DECL SearchDatabase
{
    std::string location;
    std::string id;
    std::string name;
    std::string numDatabaseSequences;
    std::string numResidues;
    std::string releaseDate;
    std::string version;
};

typedef boost::shared_ptr<SearchDatabase> SearchDatabasePtr;

struct PWIZ_API_DECL DBSequence : public IdentifiableType
{
    std::string id;
    std::string accession;
    std::string SearchDatabase_ref;

    std::string seq;

    std::vector<CVParam> cvParam;
};

typedef boost::shared_ptr<DBSequence> DBSequencePtr;

struct PWIZ_API_DECL SpectraData
{
    std::string location;
    std::string id;
    std::string name;

    std::vector<CVParam> fileFormat;
};

struct PWIZ_API_DECL SpectrumIdentification : public CVParam
{
    std::string SpectrumIdentificationProtocol_ref;
    std::string SpectrumIdentificationList_ref;
    std::string activityDate;

    std::vector< boost::shared_ptr<SpectraData> > inputSpectra;
    std::vector< boost::shared_ptr<SearchDatabase> > searchDatabase;
};

typedef boost::shared_ptr<SpectrumIdentification> SpectrumIdentificationPtr;

struct PWIZ_API_DECL ProteinDetection : public CVParam
{
    std::string ProteinDetectionProtocol_ref;
    std::string ProteinDetectionList_ref;
    std::string activityDate;

    std::vector< std::string > inputSpectrumIdentifications;

    virtual bool empty() const;
};

typedef boost::shared_ptr<ProteinDetection> ProteinDetectionPtr;

struct PWIZ_API_DECL ProteinDetectionProtocol
{
    std::string id;
    std::string AnalysisSoftware_ref;
    
    std::vector<CVParam> analysisParams;
    std::vector<CVParam> Threshold;

    std::vector< std::string > inputSpectrumIdentifications;
};

/// Parent type of SpectrumIdentification, ProteinDetection, and
/// related elements.
struct PWIZ_API_DECL Analysis
{
    std::vector<SpectrumIdentificationPtr> spectrumIdentification;
    ProteinDetectionPtr proteinDetection;

    bool empty() const;
};

typedef boost::shared_ptr<Analysis> AnalysisPtr;

/// Parent type of SpectrumIdentificationProtocol,
/// ProteinDetectinoProtocol, and related elements.
struct PWIZ_API_DECL AnalysisProtocol
{
    std::vector<CVParam> spectrumIdentificationProtocol;
    std::vector<CVParam> proteinDetectionProtocol;

    bool empty() const;
};

typedef boost::shared_ptr<AnalysisProtocol> AnalysisProtocolPtr;

struct PWIZ_API_DECL SourceFile
{
    std::string id;
    std::string location;
    std::vector<CVParam> fileFormat;
};

typedef boost::shared_ptr<SourceFile> SourceFilePtr;

/// DataCollection's Input element. Contains 0+ of SourceFile,
/// SearchDatabase, SpectraData
struct PWIZ_API_DECL Inputs
{
    // Replace these 3 members w/ their types
    std::vector< boost::shared_ptr<SourceFile> > sourceFile;
    std::vector< std::string > searchDatabase;
    std::vector< std::string > spectraData;

    bool empty() const;
};

typedef boost::shared_ptr<Inputs> InputsPtr;

/// DataCollection's AnalysisData element. 
struct PWIZ_API_DECL AnalysisData
{
    std::vector<CVParam> spectrumIdentificationList;
    std::vector<CVParam> proteinDetectionList;

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
    
    std::vector<DBSequencePtr> sequenceCollection;

    Analysis analysisCollection;

    std::vector<AnalysisProtocolPtr> analysisProtocolCollection;

    std::vector<DataCollectionPtr> dataCollection;
    
    std::vector<BibliographicReferencePtr> bibliographicReference;

    bool empty() const;
};

typedef boost::shared_ptr<MzIdentML> MzIdentMLPtr;

} // namespace mziddata 
} // namespace pwiz 

#endif // _MZIDDATA_HPP_
