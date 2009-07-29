//
// DiffTest.cpp
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

#include "Diff.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::mziddata;
using boost::shared_ptr;


ostream* os_ = 0;
const double epsilon = numeric_limits<double>::epsilon();

void testString()
{
    if (os_) *os_ << "testString()\n";

    Diff<string> diff("goober", "goober");
    unit_assert(diff.a_b.empty() && diff.b_a.empty());
    unit_assert(!diff);

    diff("goober", "goo");
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
}

void testIdentifiableType()
{
    if (os_) *os_ << "testIdentifiableType()\n";

    IdentifiableType a, b;
    a.id="a";
    a.name="a_name";
    b = a;

    Diff<IdentifiableType> diff(a, b);
    if (diff && os_) *os_ << diff << endl;
    unit_assert(!diff);

    b.id="b";
    b.name="b_name";
    
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testParamContainer()
{
    if (os_) *os_ << "testParamContainer()\n";

    ParamContainer a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);

    Diff<ParamContainer> diff(a, b);
    unit_assert(!diff);

    a.userParams.push_back(UserParam("different", "1"));
    b.userParams.push_back(UserParam("different", "2"));
    a.cvParams.push_back(MS_charge_state);
    b.cvParams.push_back(MS_intensity);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.userParams.size() == 1);
    unit_assert(diff.a_b.userParams[0] == UserParam("different","1"));
    unit_assert(diff.b_a.userParams.size() == 1);
    unit_assert(diff.b_a.userParams[0] == UserParam("different","2"));

    unit_assert(diff.a_b.cvParams.size() == 1);
    unit_assert(diff.a_b.cvParams[0] == MS_charge_state);
    unit_assert(diff.b_a.cvParams.size() == 1);
    unit_assert(diff.b_a.cvParams[0] == MS_intensity);
}

void testFragmentArray()
{
    if (os_) *os_ << "testFragmentArray()\n";

    FragmentArray a, b;

    a.values.push_back(1.0);
    a.Measure_ref = "Measure_ref";
    b = a;

    Diff<FragmentArray> diff(a, b);
    unit_assert(!diff);
    if (os_) *os_ << diff << endl;

    a.values.push_back(2.1);
    b.values.push_back(2.0);
    b.Measure_ref = "fer_erusaeM";
    diff(a, b);

    // a diff was found
    unit_assert(diff);

    // the values of the diff are correct
    unit_assert(diff.a_b.values.size() == 1);
    unit_assert(diff.a_b.values.size() == 1);
    unit_assert_equal(*diff.a_b.values.begin(), 2.1, epsilon);
    unit_assert_equal(*diff.b_a.values.begin(), 2.0, epsilon);
    unit_assert(diff.a_b.Measure_ref == "Measure_ref");
    unit_assert(diff.b_a.Measure_ref == "fer_erusaeM");

    if (os_) *os_ << diff << endl;
}

void testIonType()
{
    if (os_) *os_ << "testIonType()\n";

    IonType a, b;
    a.index.push_back(1);
    a.charge = 1;
    a.paramGroup.set(MS_frag__a_ion);
    a.fragmentArray.push_back(FragmentArrayPtr(new FragmentArray));

    b = a;

    Diff<IonType> diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(!diff);

    b.index.back() = 2; 
    b.charge = 2;
    b.paramGroup.set(MS_frag__z_ion);
    b.fragmentArray.back()->Measure_ref = "Graduated_cylinder";
    diff(a, b);

    // a diff was found
    unit_assert(diff);
    if (os_) *os_ << diff << endl;

    // and correctly
    unit_assert(diff.a_b.index.size() == 1);
    unit_assert(diff.b_a.index.size() == 1);
    unit_assert_equal(*diff.a_b.index.begin(), 1, epsilon);
    unit_assert_equal(*diff.b_a.index.begin(), 2, epsilon);
    unit_assert_equal(diff.a_b.charge, 1, epsilon);
    unit_assert_equal(diff.b_a.charge, 2, epsilon);
    unit_assert(diff.a_b.paramGroup.empty());
    unit_assert(diff.b_a.paramGroup.hasCVParam(MS_frag__z_ion));
    // TODO finish
    //    unit_assert(diff.a_b.fragmentArray.size() == 1);
    //    unit_assert(diff.b_a.fragmentArray.size() == 1);
    //    unit_assert(diff.a_b.fragmentArray.back()->Measure_ref == "");
    //    unit_assert(diff.b_a.fragmentArray.back()->Measure_ref == "Graduated_cylinder");

}


void testMaterial()
{
    Material a, b;

    a.contactRole.Contact_ref = "Contact_ref";
    a.cvParams.set(MS_sample_number);
    b = a;

    Diff<Material> diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(!diff);

    a.contactRole.Contact_ref = "other_ref";
    diff(a, b);
    unit_assert(diff);

    b.contactRole.Contact_ref = "other_ref";
    b.cvParams.set(MS_sample_name);
    diff(a, b);
    unit_assert(diff);
}


void testMeasure()
{
    Measure a, b;
    a.paramGroup.set(MS_product_ion_m_z, 200);
    b = a;

    Diff<Measure> diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(!diff);

    b.paramGroup.set(MS_product_ion_intensity, 1);
    diff(a, b);
    //unit_assert(diff);
}

void testModParam()
{
    ModParam a, b;

    a.massDelta = 1;
    a.residues = "ABCD";
    a.cvParams.set(UNIMOD_Gln__pyro_Glu);
    b = a;

    Diff<ModParam> diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(!diff);

    a.massDelta = 3;
    diff(a, b);
    unit_assert(diff);

    b.massDelta = 2;
    b.residues = "EFG";
    diff(a, b);
    unit_assert(diff);

    a.residues = "EFG";
    a.cvParams.set(UNIMOD_Glu__pyro_Glu);
    diff(a, b);
    unit_assert(diff);
}


void testPeptideEvidence()
{
    if (os_) *os_ << "testPeptideEvidence()\n";

    PeptideEvidence a, b;

    Diff<PeptideEvidence> diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(!diff);

    a.DBSequence_ref = "DBSequence_ref";
    a.start = 1;
    a.end = 6;
    a.pre = "-";
    a.post = "-";
    a.TranslationTable_ref = "TranslationTable_ref";
    a.frame = 0;
    a.isDecoy = true;
    a.missedCleavages = 0;
    a.paramGroup.set(MS_mascot_score, 15.71);
    b = a;

    diff(a,b);
    unit_assert(!diff);

    a.DBSequence_ref = "not_DBSequence_ref";
    diff(a, b);
    unit_assert(diff);

    a.DBSequence_ref = "DBSequence_ref";
    b.start = 2;
    diff(a, b);
    unit_assert(diff);

    b.start = 2;
    a.end = 7;
    diff(a, b);
    unit_assert(diff);

    b.end = 7;
    a.pre = "A";
    diff(a, b);
    unit_assert(diff);

    a.pre = "-";
    b.post = "A";
    diff(a, b);
    unit_assert(diff);

    b.post = "-";
    a.TranslationTable_ref = "not_TranslationTable_ref";
    diff(a, b);
    unit_assert(diff);

    a.TranslationTable_ref = "TranslationTable_ref";
    b.frame = 1;
    diff(a, b);
    unit_assert(diff);

    b.frame = 0;
    a.isDecoy = false;
    diff(a, b);
    unit_assert(diff);
    
    a.isDecoy = true;
    b.missedCleavages = 10;
    diff(a, b);
    unit_assert(diff);

    b.missedCleavages = 0;
    a.paramGroup.set(MS_mascot_expectation_value, 0.0268534444565851);
    diff(a, b);
    unit_assert(diff);
}


void testProteinAmbiguityGroup()
{
    if (os_) *os_ << "testProteinAmbiguityGroup()\n";

    ProteinAmbiguityGroup a, b;

    a.proteinDetectionHypothesis.push_back(ProteinDetectionHypothesisPtr(new ProteinDetectionHypothesis));
    a.paramGroup.set(MS_mascot_score, 164.4);
    b = a;

    Diff<ProteinAmbiguityGroup> diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(!diff);

    b.proteinDetectionHypothesis.clear();
    diff(a, b);
    unit_assert(diff);

    b.proteinDetectionHypothesis.push_back(ProteinDetectionHypothesisPtr(new ProteinDetectionHypothesis));
    a.paramGroup.set(MS_mascot_expectation_value, 0.0268534444565851);
    diff(a, b);
    unit_assert(diff);
}


void testProteinDetectionHypothesis()
{
    if (os_) *os_ << "testProteinDetectionHypothesis()\n";

    ProteinDetectionHypothesis a, b;
    Diff<ProteinDetectionHypothesis> diff(a,b);
    unit_assert(!diff);
    
    a.DBSequence_ref = "DBSequence_ref";
    b.DBSequence_ref = "fer_ecneuqeSDB";
    diff(a,b);
    unit_assert(diff);

    a.passThreshold = true;
    b.passThreshold = false;    
    diff(a,b);
    unit_assert(diff);
    
    if (os_) *os_ << diff << endl;
    
}

void testSpectrumIdentificationList()
{
    if (os_) *os_ << "testSpectrumIdentificationList()\n";

    SpectrumIdentificationList a, b;
    Diff<SpectrumIdentificationList> diff(a,b);
    unit_assert(!diff);
    
    a.numSequencesSearched = 9;
    b.numSequencesSearched = 5;
    diff(a,b);
    unit_assert(diff);

    a.fragmentationTable.push_back(MeasurePtr(new Measure()));
    diff(a,b);
    unit_assert(diff);

    a.spectrumIdentificationResult.push_back(SpectrumIdentificationResultPtr(new SpectrumIdentificationResult()));
    diff(a,b);
    unit_assert(diff);
    

    if (os_) *os_ << diff << endl;
}


void testProteinDetectionList()
{
    if (os_) *os_ << "testProteinDetectionList()\n";

}


void testAnalysisData()
{
}


void testSearchDatabase()
{
}


void testSpectraData()
{
}


void testSourceFile()
{
}


void testInputs()
{
}


void testEnzyme()
{
}


void testEnzymes()
{
}


void testMassTable()
{
}


void testResidue()
{
}


void testAmbiguousResidue()
{
}


void testFilter()
{
}


void testSpectrumIdentificationProtocol()
{
}


void testProteinDetectionProtocol()
{
}


void testAnalysisProtocolCollection()
{
}


void testContact()
{
}


void testAffiliations()
{
}


void testPerson()
{
}


void testOrganization()
{
}


void testBibliographicReference()
{
}


void testProteinDetection()
{
}


void testSpectrumIdentification()
{
}


void testAnalysisCollection()
{
}


void testDBSequence()
{
}


void testModification()
{
}


void testSubstitutionModification()
{
}


void testPeptide()
{
}


void testSequenceCollection()
{
}


void testSampleComponent()
{
}


void testSample()
{
}


void testSearchModification()
{
}


void testSpectrumIdentificationItem()
{
}


void testSpectrumIdentificationResult()
{
}


void testAnalysisSampleCollection()
{
}


void testProvider()
{
}


void testContactRole()
{
}


void testAnalysisSoftware()
{
    if (os_) *os_ << "testAnalysisSoftware()\n";

    AnalysisSoftware a, b;

    Diff<AnalysisSoftware> diff(a,b);
    unit_assert(!diff);

    // a.version
    a.version="version";
    // b.contactRole
    // a.softwareName
    // b.URI
    b.URI="URI";
    // a.customizations
    a.customizations="customizations";

    diff(a, b);
}


void testDataCollection()
{
    if (os_) *os_ << "testDataCollection()\n";

    DataCollection a, b;
    Diff<DataCollection> diff(a, b);
    unit_assert(!diff);

    // a.inputs
    a.inputs.sourceFile.push_back(SourceFilePtr(new SourceFile()));
    b.inputs.searchDatabase.push_back(SearchDatabasePtr(new SearchDatabase()));
    a.inputs.spectraData.push_back(SpectraDataPtr(new SpectraData()));
    
    // b.analysisData
    b.analysisData.spectrumIdentificationList.push_back(SpectrumIdentificationListPtr(new SpectrumIdentificationList()));
        
    diff(a, b);
    if (os_) *os_ << diff << endl;
    
}


void testMzIdentML()
{
    if (os_) *os_ << "testMzIdentML()\n";

    MzIdentML a, b;

    examples::initializeTiny(a);
    examples::initializeTiny(b);

    Diff<MzIdentML> diff(a, b);
    unit_assert(!diff);

    b.version = "version";
    a.cvs.push_back(CV());
    b.analysisSoftwareList.push_back(AnalysisSoftwarePtr(new AnalysisSoftware));
    a.auditCollection.push_back(ContactPtr(new Contact()));
    b.bibliographicReference.push_back(BibliographicReferencePtr(new BibliographicReference));
    // a.analysisSampleCollection
    // b.sequenceCollection
    // a.analysisCollection
    // b.analysisProtocolCollection
    // a.dataCollection
    // b.bibliographicReference

    diff(a, b);
    if (os_) *os_ << diff << endl;

    unit_assert(diff);

    unit_assert(diff.a_b.version == "0.9.0");
    unit_assert(diff.b_a.version == "version");

    unit_assert(diff.a_b.cvs.size() == 1);
    unit_assert(diff.b_a.cvs.empty());
}

void test()
{
    testString();
    testIdentifiableType();
    testParamContainer();
    testContactRole();
    testFragmentArray();
    testIonType();
    testMaterial();
    testMeasure();
    testModParam();
    testPeptideEvidence();
    testProteinAmbiguityGroup();
    testProteinDetectionHypothesis();
    testDataCollection();
    testSpectrumIdentificationList();
    testProteinDetectionList();
    testAnalysisData();
    testSearchDatabase();
    testSpectraData();
    testSourceFile();
    testInputs();
    testEnzyme();
    testEnzymes();
    testMassTable();
    testResidue();
    testAmbiguousResidue();
    testFilter();
    testSpectrumIdentificationProtocol();
    testProteinDetectionProtocol();
    testAnalysisProtocolCollection();
    testContact();
    testAffiliations();
    testPerson();
    testOrganization();
    testBibliographicReference();
    testProteinDetection();
    testSpectrumIdentification();
    testAnalysisCollection();
    testDBSequence();
    testModification();
    testSubstitutionModification();
    testPeptide();
    testSequenceCollection();
    testSampleComponent();
    testSample();
    testSearchModification();
    testSpectrumIdentificationItem();
    testSpectrumIdentificationResult();
    testAnalysisSampleCollection();
    testProvider();
    testAnalysisSoftware();
    testAnalysisSoftware();
    testMzIdentML();
}

int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

