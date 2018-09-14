//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#include "Serializer_pepXML.hpp"
#include "Diff.hpp"
#include "References.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include "TextWriter.hpp"
#include "boost/range/adaptor/transformed.hpp"
#include "boost/range/algorithm/max_element.hpp"
#include "boost/range/algorithm/min_element.hpp"
#include "boost/range/algorithm_ext/erase.hpp"
#include <cstring>


using namespace pwiz::identdata;
using namespace pwiz::identdata::examples;
using namespace pwiz::util;
namespace proteome = pwiz::proteome;

ostream* os_ = 0;

struct EnzymePtr_specificity
{
    typedef int result_type;
    int operator()(const EnzymePtr& x) const {return x->terminalSpecificity;}
};

struct EnzymePtr_missedCleavages
{
    typedef int result_type;
    int operator()(const EnzymePtr& x) const {return x->missedCleavages;}
};

struct UserParamNameIs
{
    UserParamNameIs(const string& name) : name_(name) {}

    bool operator() (const UserParam& up) const { return up.name == name_; }

    string name_;
};

void stripUnmappedMetadata(IdentData& mzid)
{
    mzid.bibliographicReference.clear();
    mzid.analysisSampleCollection.samples.clear();
    mzid.auditCollection.clear();
    mzid.provider = Provider();
    mzid.dataCollection.inputs.sourceFile.clear();

    BOOST_FOREACH(AnalysisSoftwarePtr& as, mzid.analysisSoftwareList)
    {
        as->URI.clear();
        as->customizations.clear();
        as->contactRolePtr.reset();
    }

    SpectrumIdentificationProtocol& sip = *mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];

    // pepXML only provides a single min_number_termini and max_num_internal_cleavages for all enzymes
    int minSpecificity = *boost::range::min_element(sip.enzymes.enzymes | boost::adaptors::transformed(EnzymePtr_specificity()));
    int maxMissedCleavages = *boost::range::max_element(sip.enzymes.enzymes | boost::adaptors::transformed(EnzymePtr_missedCleavages()));
    BOOST_FOREACH(const EnzymePtr& ez, sip.enzymes.enzymes)
    {
        ez->terminalSpecificity = (proteome::Digestion::Specificity) minSpecificity;
        ez->missedCleavages = maxMissedCleavages;
    }

    // pepXML doesn't map these elements
    sip.massTable.clear();
    sip.threshold.clear();
    sip.databaseFilters.clear();
    sip.databaseTranslation.reset();

    // pepXML doesn't map these attributes
    mzid.analysisCollection.spectrumIdentification[0]->searchDatabase[0]->name.clear();
    mzid.analysisCollection.spectrumIdentification[0]->searchDatabase[0]->version.clear();
    mzid.analysisCollection.spectrumIdentification[0]->searchDatabase[0]->releaseDate.clear();
    mzid.analysisCollection.spectrumIdentification[0]->searchDatabase[0]->databaseName.clear();

    // pepXML doesn't reliably store location or file format
    string& location = mzid.analysisCollection.spectrumIdentification[0]->inputSpectra[0]->location;
    location = BFS_STRING(bfs::path(location).replace_extension("").filename());
    mzid.analysisCollection.spectrumIdentification[0]->inputSpectra[0]->fileFormat = CVParam();

    string& location2 = mzid.analysisCollection.spectrumIdentification[0]->searchDatabase[0]->location;
    location2 = BFS_STRING(bfs::path(location2).replace_extension("").filename());

    // pepXML doesn't support protein sequences
    BOOST_FOREACH(DBSequencePtr& dbSequence, mzid.sequenceCollection.dbSequences)
    {
        dbSequence->seq.clear();
        dbSequence->length = 0;
        dbSequence->id = "DBSeq_" + dbSequence->accession;
    }

    // pepXML can only support one mass type (we pick the max mass in case one of them is 0)
    BOOST_FOREACH(PeptidePtr& peptide, mzid.sequenceCollection.peptides)
    BOOST_FOREACH(ModificationPtr& mod, peptide->modification)
        mod->monoisotopicMassDelta = mod->avgMassDelta = max(mod->monoisotopicMassDelta, mod->avgMassDelta);

    // pepXML doesn't support fragment metadata
    mzid.dataCollection.analysisData.spectrumIdentificationList[0]->fragmentationTable.clear();

    BOOST_FOREACH(SpectrumIdentificationResultPtr& sir, mzid.dataCollection.analysisData.spectrumIdentificationList[0]->spectrumIdentificationResult)
    BOOST_FOREACH(SpectrumIdentificationItemPtr& sii, sir->spectrumIdentificationItem)
    {
        // pepXML doesn't support fragment metadata or mass tables
        sii->fragmentation.clear();
        sii->massTablePtr.reset();

        for (size_t i=0; i < sii->peptideEvidencePtr.size(); ++i)
        {
            PeptideEvidence& pe = *sii->peptideEvidencePtr[i];

            // pepXML does not store peptide start and end offsets
            pe.start = pe.end = 0;

            // pepXML's alternative_proteins do not store prev/next AA or missed cleavages
            if (i > 0)
                pe.pre = pe.post = '?';
        }
    }

    // pepXML doesn't have protein assembly
    mzid.analysisCollection.proteinDetection = ProteinDetection();
    mzid.dataCollection.analysisData.proteinDetectionListPtr.reset();

    // pepXML expects the residues to be '.' or an amino acid list
    BOOST_FOREACH(SearchModificationPtr& sm, mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->modificationParams)
        if (sm->residues.empty())
            sm->residues.push_back('.');
}

void testTranslation(const string& str)
{
    // test that search engine name is written using preferred name
    unit_assert(bal::contains(str, "search_engine=\"Mascot\""));

    // test that score names are written using preferred name
    unit_assert(bal::contains(str, "name=\"ionscore\""));
    unit_assert(bal::contains(str, "name=\"homologyscore\""));
    unit_assert(bal::contains(str, "name=\"identityscore\""));
    unit_assert(bal::contains(str, "name=\"expect\""));
    unit_assert(bal::contains(str, "name=\"an extra score\""));

    // test that nativeID is preserved
    unit_assert(bal::contains(str, "spectrumNativeID=\"controllerType=0 controllerNumber=1 scan=420\""));
}

void testSerializeReally(IdentData& mzid, const Serializer_pepXML::Config& config)
{
    if (os_) *os_ << "begin testSerialize" << endl;

    Serializer_pepXML serializer(config);
    ostringstream oss;
    serializer.write(oss, mzid, "tiny.pepXML");

    if (os_) *os_ << "oss:\n" << oss.str() << endl;
    if (config.readSpectrumQueries)
        testTranslation(oss.str());

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    IdentData mzid2;
    serializer.read(iss, mzid2);

    References::resolve(mzid2);

    // remove DecoyPrefix userParam that is redundant with the decoy DB prefix cvParam
    boost::range::remove_erase_if(mzid2.analysisProtocolCollection.spectrumIdentificationProtocol[0]->additionalSearchParams.userParams, UserParamNameIs("DecoyPrefix"));

    Diff<IdentData, DiffConfig> diff(mzid, mzid2);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);
}

void testSerialize()
{
    IdentData mzid;
    initializeBasicSpectrumIdentification(mzid);
    stripUnmappedMetadata(mzid);
    testSerializeReally(mzid, Serializer_pepXML::Config());


    // test non-specific enzyme
    mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->enzymes.enzymes.clear();
    EnzymePtr noEnzyme(new Enzyme);
    noEnzyme->id = "ENZ_1";
    noEnzyme->cTermGain = "OH";
    noEnzyme->nTermGain = "H";
    noEnzyme->missedCleavages = 2;
    noEnzyme->minDistance = 1;
    noEnzyme->terminalSpecificity = proteome::Digestion::NonSpecific;
    noEnzyme->siteRegexp = "(?<=[KR])";
    noEnzyme->enzymeName.set(MS_Trypsin_P);
    mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->enzymes.enzymes.push_back(noEnzyme);
    testSerializeReally(mzid, Serializer_pepXML::Config());


    // test sense="N" enzymes
    mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->enzymes.enzymes.clear();
    EnzymePtr aspN(new Enzyme);
    aspN->id = "ENZ_1";
    aspN->cTermGain = "OH";
    aspN->nTermGain = "H";
    aspN->missedCleavages = 2;
    aspN->minDistance = 1;
    aspN->terminalSpecificity = proteome::Digestion::FullySpecific;
    aspN->siteRegexp = "(?=[BD])";
    aspN->enzymeName.set(MS_Asp_N);
    mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->enzymes.enzymes.push_back(aspN);
    testSerializeReally(mzid, Serializer_pepXML::Config());

    aspN->missedCleavages = 4;
    aspN->minDistance = 2;
    aspN->terminalSpecificity = proteome::Digestion::SemiSpecific;
    aspN->siteRegexp = "(?=[BND])";
    aspN->enzymeName.clear();
    aspN->enzymeName.userParams.push_back(UserParam("custom"));
    testSerializeReally(mzid, Serializer_pepXML::Config());


    // test with readSpectrumQueries == false

    // clear the original SequenceCollection
    mzid.sequenceCollection.dbSequences.clear();
    mzid.sequenceCollection.peptides.clear();
    mzid.sequenceCollection.peptideEvidence.clear();

    // clear the original analysis data
    mzid.analysisCollection.spectrumIdentification[0]->inputSpectra[0]->spectrumIDFormat = CVParam();
    mzid.analysisCollection.spectrumIdentification[0]->spectrumIdentificationListPtr.reset();
    mzid.dataCollection.analysisData.spectrumIdentificationList.clear();
    mzid.dataCollection.analysisData.proteinDetectionListPtr.reset();

    testSerializeReally(mzid, Serializer_pepXML::Config(false));
}

void testPepXMLSpecificity()
{
    PepXMLSpecificity result;
    Enzyme ez;

    ez.enzymeName.set(MS_Trypsin);
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("KR", result.cut);
    unit_assert_operator_equal("P", result.no_cut);

    ez.enzymeName.clear();
    ez.enzymeName.set(MS_Trypsin_P);
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("KR", result.cut);
    unit_assert_operator_equal("", result.no_cut);

    ez.enzymeName.clear();
    ez.enzymeName.userParams.push_back(UserParam("trypsin/p"));
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("KR", result.cut);
    unit_assert_operator_equal("", result.no_cut);

    ez.enzymeName.clear();
    ez.name = "trypsin/p";
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("KR", result.cut);
    unit_assert_operator_equal("", result.no_cut);

    ez.name.clear();
    ez.enzymeName.set(MS_Asp_N);
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("N", result.sense);
    unit_assert_operator_equal("BD", result.cut);
    unit_assert_operator_equal("", result.no_cut);

    ez.enzymeName.clear();
    ez.siteRegexp = proteome::Digestion::getCleavageAgentRegex(MS_Trypsin);
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("KR", result.cut);
    unit_assert_operator_equal("P", result.no_cut);

    ez.siteRegexp = proteome::Digestion::getCleavageAgentRegex(MS_Trypsin_P);
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("KR", result.cut);
    unit_assert_operator_equal("", result.no_cut);

    ez.siteRegexp = proteome::Digestion::getCleavageAgentRegex(MS_Asp_N);
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("N", result.sense);
    unit_assert_operator_equal("BD", result.cut);
    unit_assert_operator_equal("", result.no_cut);


    // REMEMBER: update the pepXMLSpecificity function when new CV enzymes are added
    bool allCleavageAgentsHandled = true;
    ez.siteRegexp.clear();
    BOOST_FOREACH(CVID cleavageAgent, proteome::Digestion::getCleavageAgents())
        try
        {
            ez.enzymeName.clear();
            ez.enzymeName.set(cleavageAgent);
            result = pepXMLSpecificity(ez);
        }
        catch (exception& e)
        {
            cerr << e.what() << endl;
            allCleavageAgentsHandled = false;
        }
    unit_assert(allCleavageAgentsHandled);


    ez.siteRegexp = "(?<=[QWERTY])(?=[QWERTY])";
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("QWERTY", result.cut);
    unit_assert_operator_equal("ABCDFGHIJKLMNOPSUVZ", result.no_cut);

    ez.siteRegexp = "(?<![QWERTY])(?![QWERTY])";
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("ABCDFGHIJKLMNOPSUVZ", result.cut);
    unit_assert_operator_equal("QWERTY", result.no_cut);

    ez.siteRegexp = "(?<=[QWERTY])";
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("QWERTY", result.cut);
    unit_assert_operator_equal("", result.no_cut);

    ez.siteRegexp = "(?=[QWERTY])";
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("N", result.sense);
    unit_assert_operator_equal("QWERTY", result.cut);
    unit_assert_operator_equal("", result.no_cut);

    ez.siteRegexp = "(?<![QWERTY])";
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("C", result.sense);
    unit_assert_operator_equal("ABCDFGHIJKLMNOPSUVZ", result.cut);
    unit_assert_operator_equal("", result.no_cut);

    ez.siteRegexp = "(?![QWERTY])";
    result = pepXMLSpecificity(ez);
    unit_assert_operator_equal("N", result.sense);
    unit_assert_operator_equal("ABCDFGHIJKLMNOPSUVZ", result.cut);
    unit_assert_operator_equal("", result.no_cut);
}


void testStripChargeFromConventionalSpectrumId()
{
    unit_assert_operator_equal("basename.123.123", stripChargeFromConventionalSpectrumId("basename.123.123.2"));
    unit_assert_operator_equal("basename.ext.123.123", stripChargeFromConventionalSpectrumId("basename.ext.123.123.12"));
    unit_assert_operator_equal("basename.2.2", stripChargeFromConventionalSpectrumId("basename.2.2.2"));
    unit_assert_operator_equal("basename.ext.3.3", stripChargeFromConventionalSpectrumId("basename.ext.3.3.3"));
    unit_assert_operator_equal("basename.123.123", stripChargeFromConventionalSpectrumId("basename.123.123"));
    unit_assert_operator_equal("basename.ext.123.123", stripChargeFromConventionalSpectrumId("basename.ext.123.123"));
    unit_assert_operator_equal("locus:1.1.1.123", stripChargeFromConventionalSpectrumId("locus:1.1.1.123.2"));
    unit_assert_operator_equal("basename.123", stripChargeFromConventionalSpectrumId("basename.123"));
    unit_assert_operator_equal("basename", stripChargeFromConventionalSpectrumId("basename"));
}


void testTranslation()
{
    unit_assert_operator_equal(MS_SEQUEST, pepXMLSoftwareNameToCVID("SEQUEST"));
    unit_assert_operator_equal(MS_SEQUEST, pepXMLSoftwareNameToCVID("Sequest"));
    unit_assert_operator_equal("Sequest", softwareCVIDToPepXMLSoftwareName(MS_SEQUEST));

    unit_assert_operator_equal(MS_MyriMatch, pepXMLSoftwareNameToCVID("MyriMatch"));
    unit_assert_operator_equal(MS_MyriMatch, pepXMLSoftwareNameToCVID("Myrimatch"));
    unit_assert_operator_equal("MyriMatch", softwareCVIDToPepXMLSoftwareName(MS_MyriMatch));

    unit_assert_operator_equal(MS_Comet, pepXMLSoftwareNameToCVID("Comet"));
    unit_assert_operator_equal("Comet", softwareCVIDToPepXMLSoftwareName(MS_Comet));

    unit_assert_operator_equal(MS_X_Tandem, pepXMLSoftwareNameToCVID("X! Tandem"));
    unit_assert_operator_equal(MS_X_Tandem, pepXMLSoftwareNameToCVID("X!Tandem"));
    unit_assert_operator_equal(MS_X_Tandem, pepXMLSoftwareNameToCVID("X! Tandem (k-score)"));
    unit_assert_operator_equal("X! Tandem", softwareCVIDToPepXMLSoftwareName(MS_X_Tandem));


    unit_assert_operator_equal(MS_MyriMatch_MVH, pepXMLScoreNameToCVID(MS_MyriMatch, "mvh"));
    unit_assert_operator_equal("mvh", scoreCVIDToPepXMLScoreName(MS_MyriMatch, MS_MyriMatch_MVH));

    unit_assert_operator_equal(MS_SEQUEST_xcorr, pepXMLScoreNameToCVID(MS_SEQUEST, "xcorr"));
    unit_assert_operator_equal("xcorr", scoreCVIDToPepXMLScoreName(MS_SEQUEST, MS_SEQUEST_xcorr));

    unit_assert_operator_equal(MS_Comet_xcorr, pepXMLScoreNameToCVID(MS_Comet, "xcorr"));
    unit_assert_operator_equal("xcorr", scoreCVIDToPepXMLScoreName(MS_Comet, MS_Comet_xcorr));

    unit_assert_operator_equal(CVID_Unknown, pepXMLScoreNameToCVID(MS_MyriMatch, "xcorr"));
    unit_assert_operator_equal("", scoreCVIDToPepXMLScoreName(MS_MyriMatch, MS_Comet_xcorr));


    unit_assert_operator_equal(MS_Thermo_nativeID_format, nativeIdStringToCVID("controllerType=1 controllerNumber=0 scan=1234"));
    unit_assert_operator_equal(MS_WIFF_nativeID_format, nativeIdStringToCVID("sample=1 period=1 cycle=1234 experiment=2"));
}


int main(int argc, char** argv)
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testPepXMLSpecificity();
        testStripChargeFromConventionalSpectrumId();
        testTranslation();
        testSerialize();
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
