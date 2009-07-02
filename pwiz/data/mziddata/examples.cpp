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

PWIZ_API_DECL void initializeTiny(MzIdentML& mzid)
{
    mzid.id="";
    mzid.creationDate = "2009-06-23T11:04:10";
    
    mzid.cvs = defaultCVList();

    AnalysisSoftwarePtr analysisSoftwarePtr(new AnalysisSoftware());
    mzid.analysisSoftwareList.push_back(analysisSoftwarePtr);

    PersonPtr person(new Person());
    OrganizationPtr organization(new Organization());
    ProviderPtr provider(new Provider());
    mzid.auditCollection.push_back(person);
    mzid.auditCollection.push_back(organization);
    mzid.auditCollection.push_back(provider);

    //string referenceable = "reference";
    //mzid.referenceableCollection.push_back(referenceable);

    SamplePtr sample(new Sample());
    mzid.analysisSampleCollection.samples.push_back(sample);
    
    DBSequencePtr dbSequence(new DBSequence());
    mzid.sequenceCollection.push_back(dbSequence);

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

    cv.id = "PSI-MS";
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
