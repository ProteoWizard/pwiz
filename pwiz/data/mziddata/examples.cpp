//
// examples.cpp 
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#include "examples.hpp"


namespace pwiz {
namespace mziddata {
namespace examples {


using boost::shared_ptr;
using boost::lexical_cast;
using namespace std;


const char* dbsequenceList[] = {
    "QQRLGNQWAVGHLM"
};

const char* peptideList[] = {
    "Bombessin B-4272 pGlu-Gln-Arg-Leu-Gly-Asn-Gln-Trp-Ala-Val-Gly-His-Leu-Met-NH2 (C71 H110 N24 O18 S1) ?(C71 H111 N23 O20 S1) H+ Adducts: 1619.8223, 810.4148, 540.6123, 405.7110"
};

PWIZ_API_DECL void initializeTiny(MzIdentML& mzid)
{
    mzid.id="";
    mzid.creationDate = "2009-06-23T11:04:10";
    
    mzid.cvs = defaultCVList();

    AnalysisSoftwarePtr analysisSoftwarePtr(new AnalysisSoftware());
    analysisSoftwarePtr->id="AS_mascot_server";
    analysisSoftwarePtr->name="Mascot Server";
    analysisSoftwarePtr->version="2.2.101";
    analysisSoftwarePtr->URI="http://www.matrixscience.com/search_form_select.html";
    analysisSoftwarePtr->contactRole.Contact_ref="ORG_MSL";
    analysisSoftwarePtr->contactRole.role.set(MS_software_vendor);

    analysisSoftwarePtr->softwareName.set(MS_Mascot);
    analysisSoftwarePtr->customizations ="No customizations";
    mzid.analysisSoftwareList.push_back(analysisSoftwarePtr);

    mzid.provider.id="PROVIDER";
    mzid.provider.contactRole.Contact_ref="PERSON_DOC_OWNER";
    mzid.provider.contactRole.role.set(MS_researcher);

    PersonPtr person(new Person());
    Affiliations aff;
    aff.organization_ref="ORG_MSL";
    person->affiliations.push_back(aff);
    mzid.auditCollection.push_back(person);

    person = PersonPtr(new Person());
    person->id="PERSON_DOC_OWNER";
    person->firstName="";
    person->lastName="David Creasy";
    person->email="dcreasy@matrixscience.com";
    aff.organization_ref="ORG_DOC_OWNER";
    person->affiliations.push_back(aff);
    mzid.auditCollection.push_back(person);

    OrganizationPtr organization(new Organization());
    organization->id="ORG_MSL";
    organization->name="Matrix Science Limited";
    organization->address="64 Baker Street, London W1U 7GB, UK";
    organization->email="support@matrixscience.com";
    organization->fax="+44 (0)20 7224 1344";
    organization->phone="+44 (0)20 7486 1050";
    mzid.auditCollection.push_back(organization);

    organization=OrganizationPtr(new Organization());
    organization->id="ORG_DOC_OWNER";
    
    SamplePtr sample(new Sample());
    mzid.analysisSampleCollection.samples.push_back(sample);
    
    DBSequencePtr dbSequence(new DBSequence());
    dbSequence->id="DBSeq_Bombessin";
    dbSequence->length="14";
    dbSequence->SearchDatabase_ref="SDB_5peptideMix";
    dbSequence->accession="Bombessin";
    dbSequence->seq=dbsequenceList[0];
    dbSequence->paramGroup.set(MS_protein_description, peptideList[0]);
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);

    SpectrumIdentificationPtr spectrumIdentificationPtr(
        new SpectrumIdentification());
    mzid.analysisCollection.spectrumIdentification.push_back(spectrumIdentificationPtr);
    ProteinDetectionPtr proteinDetectionPtr(new ProteinDetection());
    mzid.analysisCollection.proteinDetection = proteinDetectionPtr;

    AnalysisProtocolPtr analysisProtocolPtr(new AnalysisProtocol());
    mzid.analysisProtocolCollection.push_back(analysisProtocolPtr);

    DataCollectionPtr dataCollectionPtr(new DataCollection());
    mzid.dataCollection.push_back(dataCollectionPtr);

}

PWIZ_API_DECL vector<CV> defaultCVList()
{
    vector<CV> cvs;

    CV  cv;

    cv.id = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Vocabularies" ;
    cv.URI = "http://www.psidev.info/PSI-MS";
    cv.version = "2.0.0";
    cvs.push_back(cv);

    cv.id = "UNIMOD";
    cv.fullName = "UNIMOD" ;
    cv.URI = "http://www.unimod.org/xml/unimod.xml";
    cvs.push_back(cv);

    cv.id = "NCBI-TAXONOMY";
    cv.fullName = "NCBI-TAXONOMY" ;
    cv.URI = "ftp://ftp.ncbi.nih.gov/pub/taxonomy/taxdump.tar.gz";
    cvs.push_back(cv);
    
    cv.id = "UO";
    cv.fullName = "UNIT-ONTOLOGY" ;
    cv.URI = "http://obo.cvs.sourceforge.net/*checkout*/obo/obo/ontology/phenotype/unit.obo";
    cvs.push_back(cv);
    
    return cvs;
}

} // namespace pwiz
} // namespace mziddata
} // namespace examples 
