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


#include "IO.hpp"
#include "Diff.hpp"
#include "TextWriter.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::cv;
using namespace pwiz::identdata;
namespace proteome = pwiz::proteome;
using boost::iostreams::stream_offset;

ostream* os_ = 0;

template <typename object_type>
void testObject(const object_type& a)
{
    if (os_) *os_ << "testObject(): " << typeid(a).name() << endl;

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    object_type b; 
    istringstream iss(oss.str());
    IO::read(iss, b);

    // compare 'a' and 'b'

    Diff<object_type, DiffConfig> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff_string<TextWriter>(diff) << endl;
    unit_assert(!diff);
}


void testIdentifiable()
{
    if (os_) *os_ << "testIdentifiable\n" ;

    Identifiable a;
    a.id = "id";
    a.name = "name";

    testObject(a);
}


void testIdentifiableParamContainer()
{
    if (os_) *os_ << "testIdentifiableParamContainer\n" ;

    IdentifiableParamContainer a;
    a.id = "id";
    a.name = "name";

    a.set(MS_TIC, 123);
    a.userParams.push_back(UserParam("abc", "123", "!@#"));

    testObject(a);
}


void testCV()
{
    if (os_) *os_ << "testCV\n" ;

    CV a;
    a.URI = "abcd";
    a.id = "efgh";
    a.fullName = "ijkl";
    a.version = "mnop";

    testObject(a);
}


void testBibliographicReference()
{
    if (os_) *os_ << "testBibliographicReference\n" ;

    BibliographicReference br;

    br.id = "id";
    br.authors = "abcd";
    br.publication = "efg";
    br.publisher = "hijk";
    br.editor = "lmnop";
    br.year = 1984;
    br.volume = "qrs";
    br.issue = "tuv";
    br.pages = "wx";
    br.title = "yz";

    testObject(br);
}


void testPerson()
{
    if (os_) *os_ << "testPerson\n" ;

    Person a;

    a.set(MS_contact_address, "123 abc");
    a.set(MS_contact_phone_number, "456");
    a.set(MS_contact_email, "efg@hijk.op");
    a.set(MS_contact_fax_number, "789");
    a.set(MS_contact_toll_free_phone_number, "012-345-678");

    a.lastName = "tuv";
    a.firstName = "wx";
    a.midInitials = "yz";

    a.affiliations.push_back(OrganizationPtr(new Organization("ref")));

    testObject(a);
}


void testOrganization()
{
    if (os_) *os_ << "testOrganization\n" ;
    
    Organization a;

    a.set(MS_contact_address, "123 abc");
    a.set(MS_contact_phone_number, "456");
    a.set(MS_contact_email, "efg@hijk.op");
    a.set(MS_contact_fax_number, "789");
    a.set(MS_contact_toll_free_phone_number, "012-345-678");

    a.parent = OrganizationPtr(new Organization("ref"));

    testObject(a);
}


void testContactRole()
{
    if (os_) *os_ << "testContactRole\n" ;
    
    ContactRole a;
    a.contactPtr = ContactPtr(new Contact("ref"));
    a.cvid = MS_software_vendor;

    testObject(a);
}


void testProvider()
{
    if (os_) *os_ << "testProvider\n" ;
    
    Provider a;

    // Reduced to a previously tested object.
    a.contactRolePtr.reset(new ContactRole(MS_role_type, ContactPtr(new Contact("abc"))));

    testObject(a);
}


void testSample()
{
    if (os_) *os_ << "testSample\n" ;
    
    Sample a;

    // Reduced to a previously tested object.
    a.contactRole.push_back(ContactRolePtr(new ContactRole(MS_software_vendor, ContactPtr(new Contact("abc")))));
    a.contactRole.push_back(ContactRolePtr(new ContactRole(MS_programmer, ContactPtr(new Contact("def")))));
    a.set(MS_septum);

    a.subSamples.push_back(SamplePtr(new Sample("subSample_ref")));

    testObject(a);
}


void testAnalysisSoftware()
{
    if (os_) *os_ << "testAnalysisSoftware\n" ;

    AnalysisSoftware a;
    a.version = "abcd";
    a.URI = "efg";
    a.customizations = "hijk";
    ContactRolePtr cont = ContactRolePtr(new ContactRole());
    cont->contactPtr = ContactPtr(new Contact("ref"));
    cont->cvid = MS_software_vendor;
    a.contactRolePtr = cont;
    a.softwareName.set(MS_Mascot);

    testObject(a);
}


void testAnalysisSampleCollection()
{
    if (os_) *os_ << "testAnalysisSampleCollection\n" ;

    AnalysisSampleCollection a;
    SamplePtr b(new Sample());
    b->subSamples.push_back(SamplePtr(new Sample("ref")));

    testObject(a);
}


void testDBSequence()
{
    DBSequence a;

    a.id = "id";
    a.name = "name";
    a.length = 3;
    a.accession = "abc";
    a.searchDatabasePtr = SearchDatabasePtr(new SearchDatabase("def"));
    a.seq = "ghi";
    a.set(MS_protein_description, "blahbitty blah blah");

    testObject(a);
}


void testModification()
{
    Modification a;

    a.location = 1;
    a.avgMassDelta = 1.001001;
    a.residues.push_back('A');
    a.residues.push_back('C');
    a.monoisotopicMassDelta = 100.1001;

    a.set(UNIMOD_Gln__pyro_Glu);

    testObject(a);
}


void testSubstitutionModification()
{
    SubstitutionModification a;

    a.originalResidue = 'L';
    a.replacementResidue = 'V';
    a.location = 1;
    a.avgMassDelta = 2.;
    a.monoisotopicMassDelta = 3.;

    testObject(a);
}


void testPeptide()
{
    Peptide a;

    a.id = "id";
    a.name = "name";
    a.peptideSequence = "abc";

    ModificationPtr mod(new Modification);
    mod->location = 1;
    a.modification.push_back(mod);

    SubstitutionModificationPtr sub(new SubstitutionModification);
    sub->location = 2;
    a.substitutionModification.push_back(sub);
    
    a.set(MS_peptide);

    testObject(a);
}


void testSequenceCollection()
{
    SequenceCollection a;

    a.dbSequences.push_back(DBSequencePtr(new DBSequence("db_id")));
    a.peptides.push_back(PeptidePtr(new Peptide("pep_id")));
    a.peptideEvidence.push_back(PeptideEvidencePtr(new PeptideEvidence("pe1")));
    
    testObject(a);
}


void testSpectrumIdentification()
{
    if (os_) *os_ << "testSpectrumIdentification\n" ;

    SpectrumIdentification a;
    a.spectrumIdentificationProtocolPtr =
        SpectrumIdentificationProtocolPtr( new SpectrumIdentificationProtocol("sip"));
    a.spectrumIdentificationListPtr =
        SpectrumIdentificationListPtr(new SpectrumIdentificationList("sil"));
    a.activityDate = "123";
    a.inputSpectra.push_back(SpectraDataPtr(new SpectraData("is_sd")));
    a.searchDatabase.push_back(SearchDatabasePtr(new SearchDatabase("sd_sd")));

    testObject(a);
}


void testProteinDetection()
{
    if (os_) *os_ << "testProteinDetection\n" ;

    ProteinDetection a;

    a.id = "id";
    a.name = "name";
    a.proteinDetectionProtocolPtr = ProteinDetectionProtocolPtr(new ProteinDetectionProtocol("abc"));
    a.proteinDetectionListPtr = ProteinDetectionListPtr(new ProteinDetectionList("def"));
    a.activityDate = "ghi";

    testObject(a);
}


void testAnalysisCollection()
{
    if (os_) *os_ << "testAnalysisCollection\n" ;

    AnalysisCollection a;

    SpectrumIdentificationPtr b(new SpectrumIdentification());
    b->activityDate = "abc";
    a.spectrumIdentification.push_back(b);
    a.proteinDetection.activityDate = "def";

    testObject(a);
}


void testSearchModification()
{
    if (os_) *os_ << "testSearchModification\n" ;

    SearchModification a;

    a.fixedMod = true;
    a.massDelta = 3.14;
    a.residues.push_back('Q');
    a.residues.push_back('E');
    a.set(UNIMOD_Gln__pyro_Glu);
    a.specificityRules.cvid = MS_modification_specificity_peptide_N_term;

    testObject(a);
}


void testEnzyme()
{
    if (os_) *os_ << "testEnzyme\n" ;

    Enzyme a;

    a.id = "id";
    a.nTermGain = "n";
    a.cTermGain = "c";
    a.terminalSpecificity = proteome::Digestion::SemiSpecific;
    a.missedCleavages = 1;
    a.minDistance = 2;
    
    a.siteRegexp = "tyrannosaurus regex";
    a.enzymeName.set(MS_Trypsin);
    
    testObject(a);
}


void testEnzymes()
{
    if (os_) *os_ << "testEnzymes\n" ;

    Enzymes a;

    a.independent = "yes";
    a.enzymes.push_back(EnzymePtr(new Enzyme()));
    a.enzymes.back()->siteRegexp = "pxegeRetiS";

    testObject(a);
}


void testResidue()
{
    if (os_) *os_ << "testResidue\n" ;

    Residue a;

    a.code = 'A';
    a.mass = 2;

    testObject(a);
}


void testAmbiguousResidue()
{
    if (os_) *os_ << "testAmbiguousResidue\n" ;

    AmbiguousResidue a;

    a.code = 'B';
    a.set(MS_alternate_single_letter_codes, "D N");

    testObject(a);
}


void testMassTable()
{
    MassTable a;
    
    a.id = "id";
    a.msLevel.push_back(1);

    ResiduePtr b(new Residue());
    b->code = 'B';
    a.residues.push_back(b);
    
    AmbiguousResiduePtr c(new AmbiguousResidue());
    c->code = 'C';
    a.ambiguousResidue.push_back(c);

    testObject(a);
}


void testFilter()
{
    Filter a;

    a.filterType.set(MS_DB_filter_taxonomy);
    a.include.set(MS_DB_filter_on_accession_numbers);
    a.exclude.set(MS_DB_MW_filter);

    testObject(a);
}


void testSpectrumIdentificationProtocol()
{
    SpectrumIdentificationProtocol a;

    a.id = "id";
    
    a.analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware("ref"));

    a.searchType.cvid = MS_ms_ms_search;
    a.additionalSearchParams.set(MS_parent_mass_type_mono);
    a.modificationParams.push_back(SearchModificationPtr(new SearchModification()));
    a.enzymes.independent = "no";
    a.massTable.push_back(MassTablePtr(new MassTable("MT_1")));
    a.massTable.back()->msLevel.push_back(1);
    a.massTable.back()->msLevel.push_back(2);
    a.fragmentTolerance.set(MS_search_tolerance_plus_value, "0.6", UO_dalton);
    a.parentTolerance.set(MS_search_tolerance_plus_value, "3", UO_dalton);
    a.threshold.set(MS_Mascot_SigThreshold, "0.05");

    FilterPtr b(new Filter());
    b->filterType.set(MS_DB_filter_taxonomy);
    a.databaseFilters.push_back(b);

    testObject(a);
}


void testProteinDetectionProtocol()
{
    ProteinDetectionProtocol a;

    a.id = "id";
    a.id="PDP_MascotParser_1";
    a.analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware("ref"));
    a.analysisParams.set(MS_Mascot_SigThreshold, "0.05");
    a.threshold.set(MS_Mascot_SigThreshold, "0.05", CVID_Unknown);

    testObject(a);
}


void testAnalysisProtocolCollection()
{
    AnalysisProtocolCollection a;

    SpectrumIdentificationProtocolPtr b(new SpectrumIdentificationProtocol());
    b->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware("ref"));
    a.spectrumIdentificationProtocol.push_back(b);

    ProteinDetectionProtocolPtr c(new ProteinDetectionProtocol());
    c->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware("ref"));
    a.proteinDetectionProtocol.push_back(c);
    
    testObject(a);
}


void testSpectraData()
{
    SpectraData a;

    a.id = "id";

    a.location = "here";
    a.externalFormatDocumentation.push_back("there");
    a.fileFormat.cvid = MS_mzML_format;
    
    testObject(a);
}


void testSearchDatabase()
{
    SearchDatabase a;

    a.id = "id";
    a.location = "here";

    a.version = "1.01a";
    a.releaseDate="now";
    a.numDatabaseSequences = 1;
    a.numResidues = 2;

    a.fileFormat.cvid = MS_FASTA_format;
    a.databaseName.userParams.push_back(UserParam("5peptideMix_20090515.fasta"));
    
    testObject(a);
}


void testSourceFile()
{
    SourceFile a;

    a.id = "id";

    a.location = "there";
    a.fileFormat.cvid = MS_mzML_format;
    a.externalFormatDocumentation.push_back("somewhere else");
    a.set(MS_Mascot_DAT_format);
    
    testObject(a);
}


void testInputs()
{
    Inputs a;

    SourceFilePtr b(new SourceFile());
    b->location = "blah";
    a.sourceFile.push_back(b);

    SearchDatabasePtr c(new SearchDatabase());
    c->version = "1.0b";
    a.searchDatabase.push_back(c);

    SpectraDataPtr d(new SpectraData());
    d->location = "bleh";
    a.spectraData.push_back(d);

    testObject(a);
}


void testMeasure()
{
    Measure a;

    a.id = "id";
    a.set(MS_product_ion_m_z);

    testObject(a);
}


void testFragmentArray()
{
    FragmentArray a;

    a.values.push_back(1.);
    a.values.push_back(2.);
    a.values.push_back(3.);
    a.values.push_back(4.);
    a.measurePtr = MeasurePtr(new Measure("ref"));

    testObject(a);
}


void testIonType()
{
    IonType a;

    a.index.push_back(0);
    a.index.push_back(1);
    a.index.push_back(2);
    a.index.push_back(3);
    a.charge = 2;

    a.cvid = MS_frag__a_ion;
    FragmentArrayPtr b(new FragmentArray());
    a.fragmentArray.push_back(b);
    
    testObject(a);
}


void testPeptideEvidence()
{
    PeptideEvidence a;

    a.id = "id";
    a.dbSequencePtr = DBSequencePtr(new DBSequence("dbs_ref"));
    a.start = 1;
    a.end = 2;
    a.pre = 'A';
    a.post = 'C';
    a.translationTablePtr = TranslationTablePtr(new TranslationTable("tranny_ref"));
    a.frame = 3;
    a.isDecoy = true;
    
    a.set(MS_Mascot_score, "15.71");

    testObject(a);
}


void testSpectrumIdentificationItem()
{
    SpectrumIdentificationItem a;

    a.id = "id";
    
    a.chargeState = 1;
    a.experimentalMassToCharge = 1.1;
    a.calculatedMassToCharge = 2.2;
    a.calculatedPI = 3.3;
    a.peptidePtr = PeptidePtr(new Peptide("pep_ref"));
    a.rank = 4;
    a.passThreshold = true;
    a.massTablePtr = MassTablePtr(new MassTable("mt_ref"));
    a.samplePtr = SamplePtr(new Sample("s_ref"));
    a.peptideEvidencePtr.push_back(PeptideEvidencePtr(new PeptideEvidence("pe_ref")));

    IonTypePtr c(new IonType());
    c->charge = 5;
    a.fragmentation.push_back(c);

    a.set(MS_Mascot_score, "15.71");
    
    testObject(a);
}


void testSpectrumIdentificationResult()
{
    SpectrumIdentificationResult a;

    a.id = "id";

    a.spectrumID = "sid";
    a.spectraDataPtr = SpectraDataPtr(new SpectraData("sd_ref"));

    SpectrumIdentificationItemPtr b(new SpectrumIdentificationItem());
    b->chargeState = 1;
    a.spectrumIdentificationItem.push_back(b);
    
    a.set(MS_Mascot_score, "15.71");

    testObject(a);
}


PeptideHypothesis testPeptideHypothesis()
{
    PeptideHypothesis a;

    a.peptideEvidencePtr.reset(new PeptideEvidence("pe1"));
    a.spectrumIdentificationItemPtr.push_back(SpectrumIdentificationItemPtr(new SpectrumIdentificationItem("sii1")));

    testObject(a);
    return a;
}


void testProteinDetectionHypothesis()
{
    ProteinDetectionHypothesis a;

    a.id = "id";
    a.dbSequencePtr = DBSequencePtr(new DBSequence("dbs_ref"));
    a.passThreshold = "pt";
    a.peptideHypothesis.push_back(testPeptideHypothesis());
    a.set(MS_Mascot_score, "164.4");

    testObject(a);
}


void testProteinAmbiguityGroup()
{
    ProteinAmbiguityGroup a;

    a.id = "id";
    ProteinDetectionHypothesisPtr b(new ProteinDetectionHypothesis());
    b->dbSequencePtr = DBSequencePtr(new DBSequence("dbs_ref"));
    a.proteinDetectionHypothesis.push_back(b);
    a.set(MS_Mascot_score, "164.4");

    testObject(a);
}


void testSpectrumIdentificationList()
{
    SpectrumIdentificationList a;

    a.id = "id";
    a.numSequencesSearched = 1;

    MeasurePtr b(new Measure());
    b->set(MS_Mascot_score, "164.4");
    a.fragmentationTable.push_back(b);
    
    SpectrumIdentificationResultPtr c(new SpectrumIdentificationResult());
    c->id = "sid";
    c->spectrumID = "sID";
    a.spectrumIdentificationResult.push_back(c);

    testObject(a);
}


void testProteinDetectionList()
{
    ProteinDetectionList a;

    a.id = "id";
    ProteinAmbiguityGroupPtr b(new ProteinAmbiguityGroup());
    a.proteinAmbiguityGroup.push_back(b);

    a.set(MS_Mascot_score, "164.4");

    testObject(a);
}


void testAnalysisData()
{
    AnalysisData a;

    SpectrumIdentificationListPtr b(new SpectrumIdentificationList());
    b->id = "id";
    b->numSequencesSearched = 5;
    a.spectrumIdentificationList.push_back(b);

    a.proteinDetectionListPtr = ProteinDetectionListPtr(new ProteinDetectionList("id2"));
    a.proteinDetectionListPtr->set(MS_Mascot_score, "164.4");

    testObject(a);
}


void testDataCollection()
{
    DataCollection a;

    SourceFilePtr b(new SourceFile());
    a.inputs.sourceFile.push_back(b);

    SpectrumIdentificationListPtr c(new SpectrumIdentificationList());
    c->id = "SIL_1";
    c->numSequencesSearched = 5;
    a.analysisData.spectrumIdentificationList.push_back(c);

    testObject(a);
}


void testIdentData()
{
    IdentData a;

    examples::initializeTiny(a);

    testObject(a);

    // test ignoring sequence collection and analysis data
    {
        // write 'a' out to a stream

        ostringstream oss;
        XMLWriter writer(oss);
        IO::write(writer, a);
        if (os_) *os_ << oss.str() << endl;

        // read 'b' in from stream

        IdentData b; 
        istringstream iss(oss.str());
        IO::read(iss, b, 0, IO::IgnoreSequenceCollection, IO::IgnoreAnalysisData);

        // clear the original SequenceCollection
        a.sequenceCollection.dbSequences.clear();
        a.sequenceCollection.peptides.clear();
        a.sequenceCollection.peptideEvidence.clear();

        // clear the original analysis data
        BOOST_FOREACH(SpectrumIdentificationPtr& si, a.analysisCollection.spectrumIdentification)
            si->spectrumIdentificationListPtr.reset();
        a.analysisCollection.proteinDetection = b.analysisCollection.proteinDetection = ProteinDetection();
        a.dataCollection.analysisData.spectrumIdentificationList.clear();
        a.dataCollection.analysisData.proteinDetectionListPtr.reset();

        // compare 'a' and 'b'

        Diff<IdentData, DiffConfig> diff(a,b);
        if (diff && os_) *os_ << "diff:\n" << diff << endl;
        unit_assert(!diff);
    }
}


void test()
{
    testCV();
    testIdentifiable();
    testIdentifiableParamContainer();
    testBibliographicReference();
    testPerson();
    testOrganization();
    testContactRole();
    testProvider();
    testSample();
    testAnalysisSoftware();
    testDBSequence();
    testModification();
    testSubstitutionModification();
    testPeptide();
    testSequenceCollection();
    testSpectrumIdentification();
    testProteinDetection();
    testAnalysisCollection();
    testSearchModification();
    testEnzyme();
    testEnzymes();
    testResidue();
    testAmbiguousResidue();
    testMassTable();
    testFilter();
    testSpectrumIdentificationProtocol();
    testProteinDetectionProtocol();
    testAnalysisProtocolCollection();
    testSpectraData();
    testSearchDatabase();
    testSourceFile();
    testInputs();
    testMeasure();
    testFragmentArray();
    testIonType();
    testPeptideEvidence();
    testSpectrumIdentificationItem();
    testSpectrumIdentificationResult();
    testProteinDetectionHypothesis();
    testProteinAmbiguityGroup();
    testSpectrumIdentificationList();
    testProteinDetectionList();
    testDataCollection();
    testAnalysisData();
    testIdentData();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_IdentData")

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        if (os_) *os_ << "ok\n";
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

