//
// $Id$
//

#include "MinimumPepXML.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <fstream>
#include <algorithm>

using namespace std;
using namespace pwiz::data::pepxml;
using namespace pwiz::util;

ostream* os_ = 0;

Specificity makeSpecificity()
{
    Specificity specificity;
    specificity.cut = "theCake";
    specificity.noCut = "notTheCake";
    specificity.sense = "non";
    specificity.minSpace = 2;

    return specificity;

}

SampleEnzyme makeSampleEnzyme()
{
    SampleEnzyme sampleEnzyme;
    sampleEnzyme.name = "oxiclean";
    sampleEnzyme.description = "makes your whites whiter.";
    sampleEnzyme.fidelity = "scoundrel";
    sampleEnzyme.independent = true;
   
    Specificity specificity = makeSpecificity();
    sampleEnzyme.specificity = specificity;

    return sampleEnzyme;

}

SearchDatabase makeSearchDatabase()
{
    SearchDatabase searchDatabase;
    searchDatabase.localPath = "http://www.eharmony.com";
    searchDatabase.databaseName = "yenta";
    searchDatabase.databaseReleaseIdentifier = "village busy body";
    searchDatabase.sizeInDbEntries = 2;
    searchDatabase.sizeOfResidues = 3;
    searchDatabase.type = "online dating service";

    return searchDatabase;
}

Q3RatioResult makeQ3RatioResult()
{
    Q3RatioResult q;
    q.lightFirstScan = 1;
    q.lightLastScan = 3;
    q.lightMass = 100;
    q.heavyFirstScan = 2;
    q.heavyLastScan = 4;
    q.heavyMass = 101;
    q.lightArea = 50;
    q.heavyArea = 55;
    q.q2LightArea = 25;
    q.q2HeavyArea = 30;
    q.decimalRatio = 0.85;

    return q;

}

PeptideProphetResult makePeptideProphetResult()
{
    PeptideProphetResult peptideProphetResult;
    peptideProphetResult.probability = 0.98;

    peptideProphetResult.allNttProb.push_back(0.0000);
    peptideProphetResult.allNttProb.push_back(0.0000);
    peptideProphetResult.allNttProb.push_back(0.780);

    return peptideProphetResult;

}

AnalysisResult makeAnalysisResult()
{
    AnalysisResult analysisResult;
    analysisResult.analysis = "peptideprophet";
    
    PeptideProphetResult pp = makePeptideProphetResult();
    analysisResult.peptideProphetResult = pp;

    return analysisResult;

}

AlternativeProtein makeAlternativeProtein()
{
    AlternativeProtein alternativeProtein;
    alternativeProtein.protein = "Dos Pinos";
    alternativeProtein.proteinDescr = "leche";
    alternativeProtein.numTolTerm = "5";

    return alternativeProtein;

}

ModAminoAcidMass makeModAminoAcidMass()
{
    ModAminoAcidMass modAminoAcidMass;
    modAminoAcidMass.position = 2;
    modAminoAcidMass.mass = 12.345;

    return modAminoAcidMass;

}

ModificationInfo makeModificationInfo()
{
    ModificationInfo modificationInfo;
    modificationInfo.modifiedPeptide = "GATO";
    modificationInfo.modAminoAcidMass = makeModAminoAcidMass();

    return modificationInfo;
}

SearchHit makeSearchHit()
{
    SearchHit searchHit;
    searchHit.hitRank = 1;
    searchHit.peptide = "RAGMALLICK";
    searchHit.peptidePrevAA = "R";
    searchHit.peptideNextAA = "V";
    searchHit.protein = "PA";
    searchHit.proteinDescr = "Bioinfomagicist";
    searchHit.numTotalProteins = 1;
    searchHit.numMatchedIons = 9;
    searchHit.calcNeutralPepMass = 4.21399;
    searchHit.massDiff = .0004;
    searchHit.numTolTerm = 2;
    searchHit.numMissedCleavages = 3;
    searchHit.isRejected = 0;
    
    AnalysisResult analysisResult = makeAnalysisResult();
    searchHit.analysisResult = analysisResult;

    AlternativeProtein alternativeProtein = makeAlternativeProtein();
    searchHit.alternativeProteins.push_back(alternativeProtein);

    searchHit.modificationInfo = makeModificationInfo();

    return searchHit;

}

EnzymaticSearchConstraint makeEnzymaticSearchConstraint()
{
    EnzymaticSearchConstraint enzymaticSearchConstraint;
    
    enzymaticSearchConstraint.enzyme = "emyzne";
    enzymaticSearchConstraint.maxNumInternalCleavages = 1;
    enzymaticSearchConstraint.minNumTermini = 1;

    return enzymaticSearchConstraint;

}

AminoAcidModification makeAminoAcidModification()
{
    AminoAcidModification aminoAcidModification;
    
    aminoAcidModification.aminoAcid = "pm";
    aminoAcidModification.massDiff = 9.63333;
    aminoAcidModification.mass = 82.65;
    aminoAcidModification.variable = "c";
    aminoAcidModification.symbol = "r";

    return aminoAcidModification;

}

SearchSummary makeSearchSummary()
{
    SearchSummary searchSummary;
    searchSummary.baseName = "mseharmony";
    searchSummary.searchEngine = "yahooooo";
    searchSummary.precursorMassType = "A";
    searchSummary.fragmentMassType = "B";
    searchSummary.searchID = "ego";

    EnzymaticSearchConstraint enzymaticSearchConstraint = makeEnzymaticSearchConstraint();
    searchSummary.enzymaticSearchConstraint = enzymaticSearchConstraint;

    AminoAcidModification aminoAcidModification = makeAminoAcidModification();
    searchSummary.aminoAcidModifications.push_back(aminoAcidModification);
    searchSummary.aminoAcidModifications.push_back(aminoAcidModification);

    SearchDatabase searchDatabase = makeSearchDatabase();
    searchSummary.searchDatabase = searchDatabase;

    return searchSummary;

}


SearchResult makeSearchResult()
{
    SearchResult searchResult;
    SearchHit searchHit = makeSearchHit();
    searchResult.searchHit.push_back(SearchHitPtr(new SearchHit(searchHit)));

    return searchResult;

}

SpectrumQuery makeSpectrumQuery()
{
    SpectrumQuery spectrumQuery;
    spectrumQuery.spectrum = "ultraviolet";
    spectrumQuery.startScan = 19120414;
    spectrumQuery.endScan = 19120415;
    spectrumQuery.precursorNeutralMass = 46328;
    spectrumQuery.assumedCharge = 1;
    spectrumQuery.index = 3547;
    spectrumQuery.retentionTimeSec = 432000; 
    
    SearchResult searchResult = makeSearchResult();
    SearchResultPtr srp(new SearchResult(searchResult));
    spectrumQuery.searchResult.push_back(srp);

    return spectrumQuery;

}


MSMSRunSummary makeMSMSRunSummary()
{
    MSMSRunSummary msmsRunSummary;

    SampleEnzyme sampleEnzyme = makeSampleEnzyme();
    msmsRunSummary.sampleEnzyme = makeSampleEnzyme();

    SearchSummary searchSummary = makeSearchSummary();
    msmsRunSummary.searchSummary.push_back(SearchSummaryPtr(new SearchSummary(searchSummary)));

    SpectrumQuery spectrumQuery = makeSpectrumQuery();
    msmsRunSummary.spectrumQueries.push_back(SpectrumQueryPtr(new SpectrumQuery(spectrumQuery)));
    msmsRunSummary.spectrumQueries.push_back(SpectrumQueryPtr(new SpectrumQuery(spectrumQuery)));

    return msmsRunSummary;

}

MatchPtr makeMatch()
{
    MatchPtr match(new Match());
    match->spectrumQuery = makeSpectrumQuery();
    match->feature->mz = 1.234;
    match->feature->retentionTime = 5.678;

    return match;

}

void testSpecificity()
{
    if (os_) *os_ << "\ntestSpecificity() ... \n";

    Specificity specificity = makeSpecificity();

    ostringstream oss;
    XMLWriter writer(oss);
    specificity.write(writer);

    Specificity readSpecificity;
    istringstream iss(oss.str());
    readSpecificity.read(iss);

    unit_assert(specificity == readSpecificity);

    if (os_) *os_ << oss.str() << endl;

}

void testSampleEnzyme()
{
    if (os_) *os_ << "\ntestSampleEnzyme() ... \n";

    SampleEnzyme sampleEnzyme = makeSampleEnzyme();

    ostringstream oss;
    XMLWriter writer(oss);
    sampleEnzyme.write(writer);

    SampleEnzyme readSampleEnzyme;
    istringstream iss(oss.str());
    readSampleEnzyme.read(iss);

    unit_assert(sampleEnzyme == readSampleEnzyme);

    if (os_) *os_ << oss.str() << endl;

}

void testSearchDatabase()
{
    if (os_) *os_ << "\ntestSearchDatabase() ... \n";

    SearchDatabase searchDatabase = makeSearchDatabase();

    ostringstream oss;
    XMLWriter writer(oss);
    searchDatabase.write(writer);

    SearchDatabase readSearchDatabase;
    istringstream iss(oss.str());
    readSearchDatabase.read(iss);

    unit_assert(searchDatabase == readSearchDatabase);

    if (os_) *os_ << oss.str() << endl;

}

void testQ3RatioResult()
{
    if (os_) *os_ << "\ntestQ3RatioResult() ... \n";
    
    Q3RatioResult q = makeQ3RatioResult();
    
    ostringstream oss;
    XMLWriter writer(oss);
    q.write(writer);

    Q3RatioResult readQ;
    istringstream iss(oss.str());
    readQ.read(iss);

    unit_assert(q == readQ);
    if (os_) *os_ << oss.str() << endl;

}

void testPeptideProphetResult()
{
    if (os_) *os_ << "\ntestPeptideProphetResult() ... \n";

    PeptideProphetResult pp = makePeptideProphetResult();

    ostringstream oss;
    XMLWriter writer(oss);
    pp.write(writer);

    PeptideProphetResult readPeptideProphetResult;
    istringstream iss(oss.str());
    readPeptideProphetResult.read(iss);

    unit_assert(pp == readPeptideProphetResult);

    if (os_) *os_ << oss.str() << endl;

}

void testAnalysisResult()
{
    if (os_) *os_ << "\ntestAnalysisResult() ...\n";

    AnalysisResult analysisResult = makeAnalysisResult();

    ostringstream oss;
    XMLWriter writer(oss);
    analysisResult.write(writer);

    AnalysisResult readAnalysisResult;
    istringstream iss(oss.str());
    readAnalysisResult.read(iss);    
   
    unit_assert(analysisResult == readAnalysisResult);
    
    if(os_) *os_ << oss.str() << endl;

    AnalysisResult analysisResult2;
    analysisResult2.analysis = "q3";
    analysisResult2.q3RatioResult = makeQ3RatioResult();

    ostringstream oss2;
    XMLWriter writer2(oss2);
    analysisResult2.write(writer2);

    AnalysisResult readAnalysisResult2;
    istringstream iss2(oss2.str());
    readAnalysisResult2.read(iss2);

    unit_assert(analysisResult2 == readAnalysisResult2);

    if(os_) *os_ << oss2.str() << endl;
    
}

void testAlternativeProtein()
{

    if (os_) *os_ << "\ntestAlternativeProtein() ...\n";

    AlternativeProtein alternativeProtein = makeAlternativeProtein();

    ostringstream oss;
    XMLWriter writer(oss);
    alternativeProtein.write(writer);

    AlternativeProtein readAlternativeProtein;
    istringstream iss(oss.str());
    readAlternativeProtein.read(iss);

    unit_assert(alternativeProtein == readAlternativeProtein);

    if(os_) *os_ << oss.str() << endl;

}

void testModAminoAcidMass()
{
    if (os_) *os_ << "\ntestModAminoAcidMass() ...\n";

    ModAminoAcidMass modAminoAcidMass = makeModAminoAcidMass();

    ostringstream oss;
    XMLWriter writer(oss);
    modAminoAcidMass.write(writer);

    ModAminoAcidMass readModAminoAcidMass;
    istringstream iss(oss.str());
    readModAminoAcidMass.read(iss);

    unit_assert(modAminoAcidMass == readModAminoAcidMass);

    if(os_) *os_ << oss.str() << endl;
}

void testModificationInfo()
{
    if (os_) *os_ << "\ntestModificationInfo() ...\n";

    ModificationInfo modificationInfo = makeModificationInfo();

    ostringstream oss;
    XMLWriter writer(oss);
    modificationInfo.write(writer);

    ModificationInfo readModificationInfo;
    istringstream iss(oss.str());
    readModificationInfo.read(iss);

    unit_assert(modificationInfo == readModificationInfo);

    if(os_) *os_ << oss.str() << endl;

}

void testSearchHit()
{
    if (os_) *os_ << "\ntestSearchHit() ...\n";

    SearchHit searchHit = makeSearchHit();

    ostringstream oss;
    XMLWriter writer(oss);
    searchHit.write(writer);

    SearchHit readSearchHit;
    istringstream iss(oss.str());
    readSearchHit.read(iss);

    unit_assert(searchHit == readSearchHit);
    
    if(os_) *os_ << oss.str() << endl;
}

void testSearchResult()
{
    if(os_) *os_ << "\ntestSearchResult() ... \n";

    SearchResult searchResult = makeSearchResult();

    ostringstream oss;
    XMLWriter writer(oss);
    searchResult.write(writer);

    SearchResult readSearchResult;
    istringstream iss(oss.str());
    readSearchResult.read(iss);

    unit_assert(searchResult == readSearchResult);

    if(os_) *os_ << oss.str() << endl;


}

void testEnzymaticSearchConstraint()
{
    if (os_) *os_ << "\ntestEnzymaticSearchConstraint() ... \n";

    EnzymaticSearchConstraint enzymaticSearchConstraint = makeEnzymaticSearchConstraint();

    ostringstream oss;
    XMLWriter writer(oss);
    enzymaticSearchConstraint.write(writer);

    EnzymaticSearchConstraint readEnzymaticSearchConstraint;
    istringstream iss(oss.str());
    readEnzymaticSearchConstraint.read(iss);

    unit_assert(enzymaticSearchConstraint == readEnzymaticSearchConstraint);

    if(os_) *os_ << oss.str() << endl;

}

void testAminoAcidModification()
{
    if (os_) *os_ << "\ntestAminoAcidModification() ... \n";

    AminoAcidModification aminoAcidModification = makeAminoAcidModification();

    ostringstream oss;
    XMLWriter writer(oss);
    aminoAcidModification.write(writer);

    AminoAcidModification readAminoAcidModification;
    istringstream iss(oss.str());
    readAminoAcidModification.read(iss);

    unit_assert(aminoAcidModification == readAminoAcidModification);

    if(os_) *os_ << oss.str() << endl;

}

void testSearchSummary()
{
    if(os_) *os_ << "\ntestSearchSummary() ... \n";
    
    SearchSummary searchSummary = makeSearchSummary();

    ostringstream oss;
    XMLWriter writer(oss);
    searchSummary.write(writer);

    SearchSummary readSearchSummary;
    istringstream iss(oss.str());
    readSearchSummary.read(iss);

    unit_assert(searchSummary == readSearchSummary);

    if(os_) *os_ << oss.str() << endl;

}

void testSpectrumQuery()
{
    if(os_) *os_ << "\ntestSpectrumQuery() ... \n";
    
    SpectrumQuery spectrumQuery = makeSpectrumQuery();

    ostringstream oss;
    XMLWriter writer(oss);
    spectrumQuery.write(writer);

    if(os_) *os_ << oss.str() << endl;

    SpectrumQuery readSpectrumQuery;
    istringstream iss(oss.str());
    readSpectrumQuery.read(iss);

    readSpectrumQuery.write(writer);
    if(os_) *os_ << oss.str() << endl;
    unit_assert(spectrumQuery == readSpectrumQuery);

    if(os_) *os_ << oss.str() << endl;

}

void testMSMSRunSummary()
{
    if(os_) *os_ << "\ntestMSMSRunSummary() ... \n";

    MSMSRunSummary msmsRunSummary = makeMSMSRunSummary();

    ostringstream oss;
    XMLWriter writer(oss);
    msmsRunSummary.write(writer);

    MSMSRunSummary readMSMSRunSummary;
    istringstream iss(oss.str());
    readMSMSRunSummary.read(iss);

    unit_assert(msmsRunSummary == readMSMSRunSummary);

    if(os_) *os_ << oss.str() << endl;

}

void testMSMSPipelineAnalysis()
{
    if(os_) *os_ << "\ntestMSMSPipelineAnalysis() ... \n";

    MSMSPipelineAnalysis msmsPipelineAnalysis;
    msmsPipelineAnalysis.date = "20000101";
    msmsPipelineAnalysis.summaryXML = "/2000/01/20000101/20000101.xml";
    msmsPipelineAnalysis.xmlns = "http://regis-web.systemsbiology.net/pepXML";
    msmsPipelineAnalysis.xmlnsXSI = "aruba";
    msmsPipelineAnalysis.XSISchemaLocation = "jamaica";
    
    MSMSRunSummary msrs = makeMSMSRunSummary();
    msmsPipelineAnalysis.msmsRunSummary = msrs;

    ostringstream oss;
    XMLWriter writer(oss);
    msmsPipelineAnalysis.write(writer);

    MSMSPipelineAnalysis readMSMSPipelineAnalysis;
    istringstream iss(oss.str());
    readMSMSPipelineAnalysis.read(iss);

    unit_assert(msmsPipelineAnalysis == readMSMSPipelineAnalysis);

    if(os_) *os_ << oss.str() << endl;

}

void testMatch()
{
    if(os_) *os_ << "\ntestMatch() ... \n";

    MatchPtr match = makeMatch();
    
    ostringstream oss;
    XMLWriter writer(oss);
    match->write(writer);

    Match readMatch;
    istringstream iss(oss.str());
    readMatch.read(iss);

    ostringstream checkstream;
    XMLWriter check(checkstream);
    readMatch.write(check);
    //    unit_assert(*match == readMatch);

    if(os_) *os_ << oss.str() << endl;
    if(os_) *os_ << checkstream.str() << endl;

    unit_assert(*match == readMatch);

}

void testMatchData()
{
    if(os_) *os_ << "\ntestMatchData() ... \n";

    MatchData matchData;
    matchData.warpFunctionCalculator = "Spock";
    matchData.searchNbhdCalculator = "Mr. Rogers";
    matchData.matches.push_back(makeMatch());
    matchData.matches.push_back(makeMatch());

    ostringstream oss;
    XMLWriter writer(oss);
    matchData.write(writer);

    MatchData readMatchData;
    istringstream iss(oss.str());
    readMatchData.read(iss);


    ostringstream checkstream;
    XMLWriter check(checkstream);
    readMatchData.write(check);
    //    unit_assert(*match == readMatch);

    if(os_) *os_ << oss.str() << endl;
    if(os_) *os_ << checkstream.str() << endl;



    unit_assert(matchData == readMatchData);

    if(os_) *os_ << oss.str() << endl;

}

void testInvarianceUnderProteinProphet()
{
    ifstream ifs("20080619-A-6mixtestRG_Data10_msprefix.pep.xml");
    if (!(ifs.good()) )
        {
            throw runtime_error("bad ifs");
            return;
        }

    MSMSPipelineAnalysis msmsPipelineAnalysis;
    msmsPipelineAnalysis.read(ifs);

    ofstream ofs("test.pep.xml", ios::app);
    XMLWriter writer(ofs);
    msmsPipelineAnalysis.write(writer);

}

int main(int argc, char* argv[])
{

    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "MinimumPepXMLTest ... \n";

            testSpecificity();
            testSampleEnzyme();
            testSearchDatabase();
	    testQ3RatioResult();
            testPeptideProphetResult();
            testAnalysisResult();
            testAlternativeProtein();
            testModAminoAcidMass();
            testModificationInfo();
            testSearchHit();
            testSearchResult();
            testEnzymaticSearchConstraint();
            testAminoAcidModification();
            testSearchSummary();
            testSpectrumQuery();
            testMSMSRunSummary();
            testMSMSPipelineAnalysis();
            testMatch();
            testMatchData();
            //    testInvarianceUnderProteinProphet();

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


