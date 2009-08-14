//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
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


#ifndef _MINIMUMPEPXML_HPP_
#define _MINIMUMPEPXML_HPP_

#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "boost/shared_ptr.hpp"

#include <iostream>
#include <stdexcept>

using namespace std;
using namespace pwiz::minimxml;
using namespace pwiz::data::peakdata;

namespace pwiz{
namespace data{
namespace pepxml{

void setLogStream(ostream& os);

struct PWIZ_API_DECL Specificity
{
    Specificity(){}
    
    std::string cut;
    std::string noCut;
    std::string sense;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const Specificity& that) const;
    bool operator!=(const Specificity& that) const;

};

struct PWIZ_API_DECL SampleEnzyme
{
    SampleEnzyme(){}
   
    std::string name;
    Specificity specificity;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const SampleEnzyme& that) const;
    bool operator!=(const SampleEnzyme& that) const;

};

struct PWIZ_API_DECL SearchDatabase
{
    SearchDatabase(){}

    std::string localPath;
    std::string type;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const SearchDatabase& that) const;
    bool operator!=(const SearchDatabase& that) const;

};

struct PWIZ_API_DECL Q3RatioResult
{
    Q3RatioResult() : lightFirstScan(0), lightLastScan(0), lightMass(0), heavyFirstScan(0), heavyLastScan(0), heavyMass(0), lightArea(0), heavyArea(0), q2LightArea(0), q2HeavyArea(0), decimalRatio(0) {}
    
    int lightFirstScan;
    int lightLastScan;
    double lightMass;
    int heavyFirstScan;
    int heavyLastScan;
    double heavyMass;
    double lightArea;
    double heavyArea;
    double q2LightArea;
    double q2HeavyArea;
    double decimalRatio;

    void write(XMLWriter& writer) const;
    void read(istream& is);

    bool operator==(const Q3RatioResult& that) const;
    bool operator!=(const Q3RatioResult& that) const;

};

struct PWIZ_API_DECL PeptideProphetResult
{
    PeptideProphetResult() : probability(0) {}

    double probability;
    std::vector<double> allNttProb;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const PeptideProphetResult& that) const;
    bool operator!=(const PeptideProphetResult& that) const;

};

struct PWIZ_API_DECL AnalysisResult
{
    AnalysisResult() : analysis("peptideprophet_result") {}

    std::string analysis;
    PeptideProphetResult peptideProphetResult;
    Q3RatioResult q3RatioResult;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const AnalysisResult& that) const;
    bool operator!=(const AnalysisResult& that) const;

};

struct PWIZ_API_DECL AlternativeProtein
{
    AlternativeProtein(){}
    
    std::string protein;
    std::string proteinDescr;
    std::string numTolTerm;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const AlternativeProtein& that) const;
    bool operator!=(const AlternativeProtein& that) const;

};

struct PWIZ_API_DECL ModAminoAcidMass
{
    ModAminoAcidMass() : position(0), mass(0) {}

    int position;
    double mass;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const ModAminoAcidMass& that) const;
    bool operator!=(const ModAminoAcidMass& that) const;

};

struct PWIZ_API_DECL ModificationInfo
{
    ModificationInfo(){}

    std::string modifiedPeptide;
    ModAminoAcidMass modAminoAcidMass;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const ModificationInfo& that) const;
    bool operator!=(const ModificationInfo& that) const;

};

struct PWIZ_API_DECL SearchHit
{
    SearchHit() : hitRank(0),numTotalProteins(0), numMatchedIons(0), totalNumIons(0), calcNeutralPepMass(0), massDiff(0), numTolTerm(0), numMissedCleavages(0), isRejected(0) {}

    int hitRank;
    std::string peptide;
    std::string peptidePrevAA;
    std::string peptideNextAA;
    std::string protein;
    std::string proteinDescr;
    int numTotalProteins;
    int numMatchedIons;
    int totalNumIons;
    double calcNeutralPepMass;
    double massDiff;
    int numTolTerm;
    int numMissedCleavages;
    int isRejected; // bool?
    AnalysisResult analysisResult;
    std::vector<AlternativeProtein> alternativeProteins;
    ModificationInfo modificationInfo;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const SearchHit& that) const;
    bool operator!=(const SearchHit& that) const;

};

struct PWIZ_API_DECL SearchResult
{
    SearchResult(){}

    SearchHit searchHit;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const SearchResult& that) const;
    bool operator!=(const SearchResult& that) const;
    
};

struct PWIZ_API_DECL EnzymaticSearchConstraint
{
    EnzymaticSearchConstraint() : maxNumInternalCleavages(0), minNumTermini(0){}

    std::string enzyme;
    int maxNumInternalCleavages;
    int minNumTermini;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const EnzymaticSearchConstraint& that) const;
    bool operator!=(const EnzymaticSearchConstraint& that) const;

};

struct PWIZ_API_DECL AminoAcidModification
{
    AminoAcidModification() : massDiff(0), mass(0) {}

    std::string aminoAcid;
    double massDiff;
    double mass;
    std::string variable;
    std::string symbol;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const AminoAcidModification& that) const;
    bool operator!=(const AminoAcidModification& that) const;

};

struct PWIZ_API_DECL SearchSummary
{
    SearchSummary(){}

    std::string baseName;
    std::string searchEngine;
    std::string precursorMassType;
    std::string fragmentMassType;
    std::string searchID;
    SearchDatabase searchDatabase;
    EnzymaticSearchConstraint enzymaticSearchConstraint;
    std::vector<AminoAcidModification> aminoAcidModifications;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);
    
    bool operator==(const SearchSummary& that) const;
    bool operator!=(const SearchSummary& that) const;

};

struct PWIZ_API_DECL SpectrumQuery
{
    SpectrumQuery() : startScan(0), endScan(0), precursorNeutralMass(0), assumedCharge(0), index(0), retentionTimeSec(0) {}

    std::string spectrum;
    int startScan;
    int endScan;
    double precursorNeutralMass;
    int assumedCharge;
    int index;
    double retentionTimeSec;
    SearchResult searchResult;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);
    
    bool operator==(const SpectrumQuery& that) const;
    bool operator!=(const SpectrumQuery& that) const;
    
};

struct PWIZ_API_DECL MSMSRunSummary
{
    MSMSRunSummary(){}

    SampleEnzyme sampleEnzyme;
    SearchSummary searchSummary;
    std::vector<SpectrumQuery> spectrumQueries;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const MSMSRunSummary& that) const;
    bool operator!=(const MSMSRunSummary& that) const;
    
};

struct PWIZ_API_DECL MSMSPipelineAnalysis
{
    MSMSPipelineAnalysis(){}

    std::string date;
    std::string summaryXML;
    std::string xmlns;
    std::string xmlnsXSI;
    std::string XSISchemaLocation;
    
    MSMSRunSummary msmsRunSummary;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const MSMSPipelineAnalysis& that) const;
    bool operator!=(const MSMSPipelineAnalysis& that) const;

};

struct PWIZ_API_DECL Match
{
    Match() : score(0), feature(new Feature()) {}
    Match(const SpectrumQuery& _spectrumQuery, FeaturePtr _feature, double _score = 0) : score(_score), spectrumQuery(_spectrumQuery), feature(_feature) {}
   
    double score;

    SpectrumQuery spectrumQuery;
    FeaturePtr feature;

    void write(minimxml::XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const Match& that) const;
    bool operator!=(const Match& that) const;

private:
    Match(Match&);
    Match operator=(Match&);

};

typedef boost::shared_ptr<Match> MatchPtr;

struct PWIZ_API_DECL MatchData
{
    MatchData(){}
    MatchData(std::string wfc, std::string snc) : warpFunctionCalculator(wfc), searchNbhdCalculator(snc) {}
    MatchData(std::vector<MatchPtr> _matches) : matches(_matches){}

    std::string warpFunctionCalculator;
    std::string searchNbhdCalculator;
    std::vector<MatchPtr> matches;

    void write(minimxml::XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const MatchData& that) const;
    bool operator!=(const MatchData& that) const;

};

} // namespace pepxml
} // namespace data
} // namespace pwiz



#endif // _MINIMUMPEPXML_HPP_

//  LocalWords:  RatioResult
