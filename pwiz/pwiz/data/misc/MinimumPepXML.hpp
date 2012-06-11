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
#include "boost/logic/tribool.hpp"

#include <iostream>
#include <stdexcept>

using namespace pwiz::minimxml;
using namespace pwiz::data::peakdata;

namespace pwiz{
namespace data{
namespace pepxml{

void setLogStream(std::ostream& os);

struct PWIZ_API_DECL Specificity
{
    Specificity() : minSpace(1) {}

    /// One or more 1-letter residue codes. Enzyme cleaves on the
    /// sense side of the residue(s) listed in cut unless one of the
    /// residues listed in no_cut is adjacent to the potential
    /// cleavage site.
    std::string cut;

    /// Zero or more 1-letter residue codes. Enzyme cleaves on the
    /// sense side of the residue(s) listed in cut unless one of the
    /// residues listed in no_cut is adjacent to the potential
    /// cleavage site.
    std::string noCut;

    /// Defines whether cleavage occurs on the C-terminal or
    /// N-terminal side of the residue(s) listed in cut (values "C" or
    /// "N")
    std::string sense;

    /// minimum separation between adjacent cleavages. default 1.
    size_t minSpace;
    
    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const Specificity& that) const;
    bool operator!=(const Specificity& that) const;

};

struct PWIZ_API_DECL SampleEnzyme
{
    SampleEnzyme() : independent(boost::indeterminate) {}

    /// Controlled code name for the enzyme that can be referred to by
    /// applications.
    std::string name;

    /// Free text to describe alternative names, special conditions,
    /// etc.
    std::string description;

    /// Semispecific means that at least one end of a pepide must
    /// conform to the cleavage specificity, (unless the peptide was
    /// at the terminus of the parent sequence). Nonspecific means
    /// that neither end of a peptide must conform to the cleavage
    /// specificity.
    std::string fidelity;

    /// If there are multiple specificities and independent is true,
    /// then a single peptide cannot exhibit one specificity at one
    /// terminus and a different specificity at the other. If
    /// independent is false, then a single peptide can exhibit mixed
    /// specificities.
    boost::tribool independent;

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
    std::string databaseName;
    std::string databaseReleaseIdentifier;
    size_t sizeInDbEntries;
    size_t sizeOfResidues;
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
    void read(std::istream& is);

    bool operator==(const Q3RatioResult& that) const;
    bool operator!=(const Q3RatioResult& that) const;

};

struct PWIZ_API_DECL RocDataPoint
{
    double min_prob;
    double sensitivity;
    double error;
    long num_corr;
    long num_incorr;
};

struct PWIZ_API_DECL ErrorPoint
{
    double error;
    double min_prob;
    long num_corr;
    long num_incorr;
};

struct PWIZ_API_DECL DistributionPoint
{
    double fvalue;
    long obs_1_distr;
    double model_1_pos_distr;
    double model_1_neg_distr;
    long obs_2_distr;
    double model_2_pos_distr;
    double model_2_neg_distr;
    long obs_3_distr;
    double model_3_pos_distr;
    double model_3_neg_distr;
};

struct PWIZ_API_DECL MixtureModel
{
    long precursor_ion_charge;
    std::string comments;
    double prior_probability;
    double est_tot_correct;
    long tot_num_spectra;
    long num_iterations;

    // TODO Child tags go here... Don't forget to add the struct for
    // them too
};

struct PWIZ_API_DECL PeptideProphetSummary
{
    std::string version;
    std::string author;
    double min_prob;
    std::string options;
    double est_tot_num_correct;

    std::vector<std::string> inputFile;
    std::vector<RocDataPoint> roc_data_point;
    std::vector<ErrorPoint> error_point;
    std::vector<DistributionPoint> distribution_point;
    std::vector<MixtureModel> mixture_model;
};

struct PWIZ_API_DECL PeptideProphetResult
{
    PeptideProphetResult() : probability(0) {}

    double probability;
    std::vector<double> allNttProb;
    std::string analysis;

    // TODO add search_score_summary and its parameter (2+ occurances) 

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

struct PWIZ_API_DECL Parameter
{
    Parameter(const std::string& name = "", const std::string& value = "")
        : name(name), value(value)
    {}
    
    std::string name;
    std::string value;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const Parameter& that) const;
    bool operator!=(const Parameter& that) const;
};

typedef boost::shared_ptr<Parameter> ParameterPtr;


struct PWIZ_API_DECL SearchScore : Parameter
{
    SearchScore(const std::string& name = "", const std::string& value = "")
        : Parameter(name, value)
    {}

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

};

typedef boost::shared_ptr<SearchScore> SearchScorePtr;


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

    std::vector<SearchScorePtr> searchScore;
    
    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const SearchHit& that) const;
    bool operator!=(const SearchHit& that) const;

};

typedef boost::shared_ptr<SearchHit> SearchHitPtr;


PWIZ_API_DECL bool operator==(const SearchHitPtr left, const SearchHitPtr right);

struct PWIZ_API_DECL SearchResult
{
    SearchResult(size_t searchId = 0) :searchId(searchId){}

    /// Unique identifier to search summary
    size_t searchId;
    
    std::vector<SearchHitPtr> searchHit;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const SearchResult& that) const;
    bool operator!=(const SearchResult& that) const;
    
};

typedef boost::shared_ptr<SearchResult> SearchResultPtr;

PWIZ_API_DECL bool operator==(SearchResultPtr left, SearchResultPtr right);


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
    std::string peptideTerminus;
    std::string binary;
    std::string description;
    std::string symbol;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const AminoAcidModification& that) const;
    bool operator!=(const AminoAcidModification& that) const;

};

/// Database search settings
struct PWIZ_API_DECL SearchSummary
{
    SearchSummary(){}

    /// Full path location of mzXML file for this search run (without
    /// the .mzXML extension)
    std::string baseName;
    
    /// SEQUEST, Mascot, COMET, etc
    std::string searchEngine;

    /// average or monoisotopic
    std::string precursorMassType;

    /// average or monoisotopic
    std::string fragmentMassType;

    /// Format of file storing the runner up peptides (if not present
    /// in pepXML)
    std::string searchID;

    /// runner up search hit data type extension (e.g. .tgz)
    SearchDatabase searchDatabase;

    /// matches id in search hit
    size_t search_id;
    
    EnzymaticSearchConstraint enzymaticSearchConstraint;
    std::vector<AminoAcidModification> aminoAcidModifications;

    std::vector<ParameterPtr> parameters;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);
    
    bool operator==(const SearchSummary& that) const;
    bool operator!=(const SearchSummary& that) const;

};

typedef boost::shared_ptr<SearchSummary> SearchSummaryPtr;

PWIZ_API_DECL bool operator==(const SearchSummaryPtr left, const SearchSummaryPtr right);


/// Reference for analysis applied to current run (time corresponds
/// with analysis_summary/@time, id corresponds with
/// analysis_result/@id)
struct PWIZ_API_DECL AnalysisTimestamp
{
    /// Date of analysis
    std::string time;

    /// Analysis name
    std::string analsysis;

    /// Unique identifier for each type of analysis
    size_t id;
    
    // Evil ##any data goes here
};


struct PWIZ_API_DECL SpectrumQuery
{
    SpectrumQuery() : startScan(0), endScan(0), precursorNeutralMass(0), assumedCharge(0), index(0), retentionTimeSec(0) {}

    std::string spectrum;

    /// first scan number integrated into MS/MS spectrum
    int startScan;

    /// last scan number integrated into MS/MS spectrum
    int endScan;

    double precursorNeutralMass;

    /// Precursor ion charge used for search
    int assumedCharge;

    /// Search constraint applied specifically to this query
    int index;

    /// Unique identifier
    double retentionTimeSec;
    
    std::vector<SearchResultPtr> searchResult;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);
    
    bool operator==(const SpectrumQuery& that) const;
    bool operator!=(const SpectrumQuery& that) const;
    
};

typedef boost::shared_ptr<SpectrumQuery> SpectrumQueryPtr;

PWIZ_API_DECL bool operator==(const SpectrumQueryPtr left, const SpectrumQueryPtr right);

struct PWIZ_API_DECL MSMSRunSummary
{
    MSMSRunSummary(){}

    std::string base_name;
    std::string raw_data_type;
    std::string raw_data;
    std::string msManufacturer;
    std::string msModel;
    std::string msIonization;
    std::string msMassAnalyzer;
    std::string msDetector;
    
    SampleEnzyme sampleEnzyme;
    std::vector<SearchSummaryPtr> searchSummary;
    std::vector<SpectrumQueryPtr> spectrumQueries;

    void write(XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const MSMSRunSummary& that) const;
    bool operator!=(const MSMSRunSummary& that) const;
    
};

struct PWIZ_API_DECL AnalysisSummary
{
    /// Time analysis complete (unique id)
    std::string time;

    /// Name of analysis program
    std::string analysis;

    /// Release
    std::string version;
    
    // All the unknown stuff goes here
    
    // TODO deal with the results of
    // <xs:any namespace="##any" processContents="lax" minOccurs="0">
    std::vector<PeptideProphetSummary> peptideprophet_summary;
};

typedef boost::shared_ptr<AnalysisSummary> AnalysisSummaryPtr;


struct PWIZ_API_DECL DataFilter
{
    size_t number;

    /// File from which derived
    std::string parent_file;
    
    std::string windows_parent;

    /// filtering criteria applied to data
    std::string description;
};

typedef boost::shared_ptr<DataFilter> DataFilterPtr;


/// Source and filtering criteria used to generate dataset
struct PWIZ_API_DECL DatasetDerivation
{
    /// number preceding filter generations
    size_t generation_no;
    
    std::vector<DataFilterPtr> dataFilters;
};

typedef boost::shared_ptr<DatasetDerivation> DatasetDerivationPtr;


struct PWIZ_API_DECL MSMSPipelineAnalysis
{
    MSMSPipelineAnalysis(){}

    std::string date;
    std::string summaryXML;
    std::string xmlns;
    std::string xmlnsXSI;
    std::string XSISchemaLocation;

    /// full path file name of mzXML (minus the .mzXML)
    std::string baseName;

    /// raw data type extension (e.g. .mzXML)
    std::string raw_data_type;

    /// raw data type extension (e.g. .mzXML)
    std::string raw_data;

    /// Manufacturer of MS/MS instrument
    std::string msManufacturer;

    /// Instrument model (cf mzXML)
    std::string msModel;

    /// Instrument model (cf mzXML)
    std::string msIonization;

    /// Ion trap, etc (cf mzXML)
    std::string msMassAnalyzer;

    /// EMT, etc(cf mzXML)
    std::string msDetector;
    
    AnalysisSummaryPtr analysisSummary;
    DatasetDerivationPtr datasetDerivation;
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
    double calculatedMass;
    double massDeviation; // ( feature mz - proton mass ) * charge - calculatedMass (not absolute val!)

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

PWIZ_API_DECL bool operator==(const MatchPtr left, const MatchPtr right);


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
