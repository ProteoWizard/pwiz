//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// AMTDatabase.cpp
///

#include "AMTDatabase.hpp"
#include "Exporter.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "boost/filesystem.hpp"

using namespace std;
using namespace pwiz;
using namespace eharmony;
using namespace pwiz::proteome;

AMTDatabase::AMTDatabase(const AMTContainer& amtContainer)
{
    vector<boost::shared_ptr<SpectrumQuery> > ms2 = amtContainer._pidf->getAllContents();
    _peptides = PidfPtr(new PeptideID_dataFetcher(ms2));

}

vector<SpectrumQuery> AMTDatabase::query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir, const int& roc, const double& threshold)
{
    outputDir += "/amtdb_query";
    outputDir += boost::lexical_cast<string>(boost::lexical_cast<int>(nds._threshold * 100));

    boost::filesystem::create_directory(outputDir);

    // get prewiggle plot
    PeptideMatcher pm_pre(dfc->_pidf_a, dfc->_pidf_b);
    Exporter exporter_pre(pm_pre, Feature2PeptideMatcher());
    ofstream ofs_prewig((outputDir + "/preWiggle.txt").c_str());
    exporter_pre.writeWigglePlot(ofs_prewig);
    
    // TODO pass tolerances in through config
    nds.calculateTolerances(dfc);
    dfc->warpRT(wfe);
    PeptideMatcher pm(dfc->_pidf_a, dfc->_pidf_b);
    Feature2PeptideMatcher f2pm(dfc->_fdf_a, dfc->_pidf_b, nds, roc, threshold);

    cout << "[AMTDatabase] Number of matches accepted: " << f2pm.getMatches().size() << endl;

    Exporter exporter(pm, f2pm);
    exporter._dfc = dfc;

    ofstream ofs_anch((outputDir + "/anchors.txt").c_str());
    exporter.writeAnchors(ofs_anch);

    ofstream ofs_wiggle((outputDir + "/wiggle.txt").c_str());
    exporter.writeWigglePlot(ofs_wiggle);

    ofstream ofs_f2pm((outputDir + "/f2pm.xml").c_str());
    exporter.writeF2PM(ofs_f2pm);

    ofstream ofs_r((outputDir + "/r_input.txt").c_str());
    exporter.writeRInputFile(ofs_r);
    
    ofstream ofs_tp((outputDir + "/tp.txt").c_str());
    exporter.writeTruePositives(ofs_tp);

    ofstream ofs_fp((outputDir + "/fp.txt").c_str());
    exporter.writeFalsePositives(ofs_fp);

    ofstream ofs_tn((outputDir + "/tn.txt").c_str());
    exporter.writeTrueNegatives(ofs_tn);

    ofstream ofs_fn((outputDir + "/fn.txt").c_str());
    exporter.writeFalseNegatives(ofs_fn);
    
    ofstream ofs_up((outputDir + "/up.txt").c_str());
    exporter.writeUnknownPositives(ofs_up);

    ofstream ofs_un((outputDir + "/un.txt").c_str());
    exporter.writeUnknownNegatives(ofs_un);

    vector<SpectrumQuery> result;
    vector<MatchPtr> matches = f2pm.getMatches();
    vector<MatchPtr>::iterator it = matches.begin();
    for(; it != matches.end(); ++it) result.push_back((*it)->spectrumQuery);

    mspa_in.msmsRunSummary.spectrumQueries = result;

    ofstream ofs_pepxml((outputDir + "/ms1_5.pep.xml").c_str());
    exporter.writePepXML(mspa_in, ofs_pepxml);

    return result;
}


//
// IslandizedDatabase
//

typedef multimap<string, boost::shared_ptr<SpectrumQuery> > IslandMap;

// helper functions

struct SortByMass
{
    SortByMass(){}
    bool operator()(const boost::shared_ptr<SpectrumQuery>& a, const boost::shared_ptr<SpectrumQuery>& b) const {return a->precursorNeutralMass < b->precursorNeutralMass;}

};

struct SortByRT
{
    SortByRT(){}
    bool operator()(const boost::shared_ptr<SpectrumQuery>& a, const boost::shared_ptr<SpectrumQuery>& b) const { return a->retentionTimeSec < b->retentionTimeSec;}

};

boost::shared_ptr<IslandizedDatabase::Island> makeIsland(const pair<IslandMap::iterator, IslandMap::iterator>& x)
{
    boost::shared_ptr<IslandizedDatabase::Island> result(new IslandizedDatabase::Island());
    IslandMap::iterator it = x.first;
    result->id = it->first;

    double massCounter = 0;
    double rtCounter = 0;

    for(; it != x.second; ++it)
        {
            result->spectrumQueries.push_back(it->second);
            const double& score = (*it->second).searchResult.searchHit.analysisResult.peptideProphetResult.probability;

            //store mass and rt coordinates as mean for Island, and orders of mag weighted by score for stdev
            pair<double,double> means = make_pair((*it->second).precursorNeutralMass, (*it->second).retentionTimeSec);          
            pair<double,double> sigmas = make_pair(.005 * score, 100 * score);
            pair<pair<double,double>, pair<double,double> > params = make_pair(means,sigmas);

            result->gaussians.push_back(IslandizedDatabase::Gaussian(params));
            massCounter += means.first;
            rtCounter += means.second;
        }

    result->massMean = massCounter / result->gaussians.size();
    result->rtMean = rtCounter / result->gaussians.size();

    sort(result->spectrumQueries.begin(), result->spectrumQueries.end(), SortByMass());

    // make a box encapsulating (effectively) the distributions in the island  (3 stdevs above and below max and min mean)

    result->massMin = (*result->spectrumQueries.begin())->precursorNeutralMass - .003*(*result->spectrumQueries.begin())->searchResult.searchHit.analysisResult.peptideProphetResult.probability;

    result->massMax = (*result->spectrumQueries.back()).precursorNeutralMass - .003*(*result->spectrumQueries.begin())->searchResult.searchHit.analysisResult.peptideProphetResult.probability;

    // same for retention time
    sort(result->spectrumQueries.begin(), result->spectrumQueries.end(), SortByRT());
    result->rtMin = (*result->spectrumQueries.begin())->retentionTimeSec - 300*(*result->spectrumQueries.begin())->searchResult.searchHit.analysisResult.peptideProphetResult.probability;
    result->rtMax = (*result->spectrumQueries.back()).retentionTimeSec + 300*(*result->spectrumQueries.begin())->searchResult.searchHit.analysisResult.peptideProphetResult.probability;

    const double& dynamicRangeArea = 1800 * 6000; // 200 - 2000 Da, 0 - 6000 s 
    result->relativeArea = (result->massMax - result->massMin) * (result->rtMax - result->rtMin) / dynamicRangeArea; // rough appx

    return result;

}

IslandizedDatabase::IslandizedDatabase(boost::shared_ptr<AMTContainer> amtContainer) : AMTDatabase(*amtContainer)
{
    _peptides = amtContainer->_pidf;
    IslandMap islandMap;
    vector<string> peptideNames;
  
    vector<boost::shared_ptr<SpectrumQuery> > contents = _peptides->getAllContents();
    vector<boost::shared_ptr<SpectrumQuery> >::const_iterator it = contents.begin();
    for(; it != contents.end(); ++it)
        {         
            const string peptide = (*it)->searchResult.searchHit.peptide;                  
            if (find(peptideNames.begin(), peptideNames.end(), peptide) == peptideNames.end())
                {
                    peptideNames.push_back(peptide);

                }

            pair<string, boost::shared_ptr<SpectrumQuery> > entry(peptide, *it);
            islandMap.insert(entry);

        }
    
    vector<string>::iterator name_it = peptideNames.begin();
    for(; name_it != peptideNames.end(); ++name_it)
        {
            pair<IslandMap::iterator, IslandMap::iterator> x = islandMap.equal_range(*name_it);            
            IslandizedDatabase::Island island = *makeIsland(x);
            islands.push_back(island);

        }

    uniquePeptides = peptideNames; 

}

double p(const double& mu, const double& sigma, const double& x) 
{
    double result = 0;

    if (x >= mu) result = .5 * (1 + erf((-x-mu)/(sqrt(2) * sigma)));
    else result = .5 * (1 + erf((x-mu)/(sqrt(2) * sigma)));

    result *= 2;
    return result;

}

typedef IslandizedDatabase::Island Island;

vector<boost::shared_ptr<Island> > getIslandCandidates(const IslandizedDatabase& id, const double& mass, const double& rt)
{
    vector<boost::shared_ptr<Island> > result;
    const vector<Island>& islands = id.islands;
    vector<Island>::const_iterator it = islands.begin();
    for(; it != islands.end(); ++it)
        {
            if ((it->massMin <= mass) && (mass  <= it->massMax) && (it->rtMin <= rt) && (rt <= it->rtMax) )
                    result.push_back(boost::shared_ptr<Island>(new Island(*it)));
        }

    return result;
}

pair<boost::shared_ptr<SpectrumQuery>, double> getBestSpectrumQuery(const vector<boost::shared_ptr<Island> >& candidates, const double& mass,const double& rt, ofstream& ofs)
{
    boost::shared_ptr<SpectrumQuery> result(new SpectrumQuery());
    boost::shared_ptr<Island> bestIsland(new Island());

    double denominator = 0;
    vector<boost::shared_ptr<Island> >::const_iterator it = candidates.begin();
    for (; it != candidates.end(); ++it)
    // iterate thru and calculate denominator of conditional probability
        {
            double p = (*it)->calculatePVal(mass, rt) * (*it)->relativeArea;
            denominator += p;
        }
    // iterate thru and calculate numerator / denominator for each possible numerator

    double maxP = -1;
    for(it = candidates.begin(); it!= candidates.end(); ++it)
        {
            int count = 0;
            double conditionalP = (*it)->calculatePVal(mass, rt) * (*it)->calculatePVal(mass, rt) * (*it)->relativeArea / denominator;
            if (conditionalP > maxP)
                {
                    maxP = conditionalP;
                    result = *(*it)->spectrumQueries.begin(); // TODO: change to consensus sq
                    bestIsland = *it;
                    count +=1;
                }

        }

    // return the best result
    if (candidates.size() > 0 && (maxP != -1)) ofs << maxP << "\t" << bestIsland->spectrumQueries.size() << "\t" << candidates.size() << "\t" << bestIsland->relativeArea << endl;
    return make_pair(result, maxP);
}

double IslandizedDatabase::Island::calculatePVal(const double& mass, const double& rt) const
{
    double massResult = 0;
    double rtResult = 0;

    const double normalizationFactor = gaussians.size();
    vector<Gaussian>::const_iterator it = gaussians.begin();

    // iterate through gaussians and add p val to result
    for(; it != gaussians.end(); ++it)
        {
            double incrementalPValMass = p(it->mu.first, it->sigma.first, mass);
            double incrementalPValRT = p(it->mu.second, it->sigma.second, rt);
            
            massResult += incrementalPValMass;
            rtResult += incrementalPValRT;

        }
    
    massResult /= normalizationFactor;
    rtResult /= normalizationFactor;

    double result = massResult * rtResult;
    return result;

}

vector<SpectrumQuery> IslandizedDatabase::query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir)
{
    ofstream qc("qc.txt");
    vector<SpectrumQuery> result;

    outputDir += "/amtdb_query_island";
    outputDir += boost::lexical_cast<string>(boost::lexical_cast<int>(nds._threshold * 100));

    boost::filesystem::create_directory(outputDir);

    // get prewiggle plot
    PeptideMatcher pm_pre(dfc->_pidf_a, dfc->_pidf_b);
    Exporter exporter_pre(pm_pre, Feature2PeptideMatcher());
    ofstream ofs_prewig((outputDir + "/preWiggle.txt").c_str());
    exporter_pre.writeWigglePlot(ofs_prewig);

    // TODO pass tolerances in through config

    PeptideMatcher pm(dfc->_pidf_a, dfc->_pidf_b);
    Feature2PeptideMatcher f2pm; // we will fill in the relevant attributes as we go.

    // Iterate through query features looking for islands at those coordinates
    FdfPtr fdf = dfc->_fdf_a;
    vector<FeatureSequencedPtr> features = fdf->getAllContents();
    vector<FeatureSequencedPtr>::iterator it = features.begin();
    for(; it != features.end(); ++it)        
        {
            if ((*it)->ms2 == "") continue;
            if ((*it)->feature->charge ==0) continue;

            // naive: calculate pval for every island in the database. Can be improved upon
            double maxPVal = 0;
            bool winner = false;
            string bestFS = "";

            boost::shared_ptr<SpectrumQuery> best(new SpectrumQuery());

            double mass = ((*it)->feature->mz - Ion::protonMass_) * (*it)->feature->charge;
            double rt = (*it)->feature->retentionTime;
                    
            vector<boost::shared_ptr<Island> > candidates = getIslandCandidates(*this, mass, rt);
            pair<boost::shared_ptr<SpectrumQuery>, double> bestPair = getBestSpectrumQuery(candidates, mass, rt, qc);
            best = bestPair.first;
            maxPVal = bestPair.second;
            
            // getBestSpectrumQuery returns a score of -1 if no match
            if (maxPVal != -1) winner = true; 

            if (winner) 
                {
                    result.push_back(*best);
                    MatchPtr match(new Match(*best, (*it)->feature));
                    match->score = maxPVal;
                    match->calculatedMass = (*it)->calculatedMass;
                    match->massDeviation = (match->feature->mz - Ion::protonMass_) * match->feature->charge - match->calculatedMass;

                    f2pm._matches.push_back(match);
                    f2pm._unknownPositives.push_back(match);
                    string sequence = (*it)->ms2;

                    if (sequence != "")
                        {
                            if (sequence == best->searchResult.searchHit.peptide) 
                                {
                                    f2pm._truePositives.push_back(match);

                                }
                            else 
                                {
                                    f2pm._falsePositives.push_back(match);

                                }
                        }


                }

            else
                {                                        
                    /// give up

                    MatchPtr match(new Match(*best, (*it)->feature));
                    match->score = 0; // not maxPVal - we know it's 0, -1 was just an indicator
                    match->calculatedMass = (*it)->calculatedMass;
                    match->massDeviation = (match->feature->mz - Ion::protonMass_) * match->feature->charge - match->calculatedMass;
                    
                    f2pm._mismatches.push_back(match);
                    string sequence = (*it)->ms2;

                    if (sequence != "")
                        {
                            if (find(uniquePeptides.begin(), uniquePeptides.end(), sequence) != uniquePeptides.end())
                                {
                                    f2pm._falseNegatives.push_back(match);

                                }
                            else
                                {
                                    f2pm._trueNegatives.push_back(match);

                                }
                        }


                    
                }
        }
         
    Exporter exporterf(pm, f2pm);
    exporterf._dfc = dfc;

    ofstream ofs_tp((outputDir + "/tp.txt").c_str());
    exporterf.writeTruePositives(ofs_tp);

    ofstream ofs_fp((outputDir + "/fp.txt").c_str());
    exporterf.writeFalsePositives(ofs_fp);

    ofstream ofs_tn((outputDir + "/tn.txt").c_str());
    exporterf.writeTrueNegatives(ofs_tn);

    ofstream ofs_fn((outputDir + "/fn.txt").c_str());
    exporterf.writeFalseNegatives(ofs_fn);

    ofstream ofs_un((outputDir + "/un.txt").c_str());
    exporterf.writeTrueNegatives(ofs_un);

    ofstream ofs_up((outputDir + "/up.txt").c_str());
    exporterf.writeFalseNegatives(ofs_up);

    ofstream ofs_ms1_5((outputDir + "/ms1_5.pep.xml").c_str());
    exporterf.writePepXML(mspa_in,ofs_ms1_5);

    ofstream ofs_anch((outputDir + "/anchors.txt").c_str());
    exporterf.writeAnchors(ofs_anch);

    ofstream ofs_wiggle((outputDir + "/wiggle.txt").c_str());
    exporterf.writeWigglePlot(ofs_wiggle);

    ofstream ofs_f2pm((outputDir + "/f2pm.xml").c_str());
    exporterf.writeF2PM(ofs_f2pm);

    ofstream ofs_r((outputDir + "/r_input.txt").c_str());
    exporterf.writeRInputFile(ofs_r);

    return result;
    
}
