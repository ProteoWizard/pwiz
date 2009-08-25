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

vector<SpectrumQuery> AMTDatabase::query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir)
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
    dfc->warpRT(wfe);
    PeptideMatcher pm(dfc->_pidf_a, dfc->_pidf_b);
    Feature2PeptideMatcher f2pm(dfc->_fdf_a, dfc->_pidf_b, nds);

    cout << "[AMTDatabase] Number of matches accepted: " << f2pm.getMatches().size() << endl;

    Exporter exporter(pm, f2pm);
    exporter._dfc = dfc;

    ofstream ofs_anch((outputDir + "/anchors.txt").c_str());
    exporter.writeAnchors(ofs_anch);

    ofstream ofs_pep((outputDir + "/peptides.txt").c_str());
    ofstream ofs_feat0((outputDir + "/features.txt").c_str());
    ofstream ofs_feat1((outputDir + "/calibratedFeatures.txt").c_str());
    exporter.writeRTCalibrationData(ofs_pep, ofs_feat0, ofs_feat1);

    ofstream comb((outputDir + "/combined.xml").c_str());
    exporter.writeCombinedPepXML(mspa_in, comb);

    ofstream ofs_pm((outputDir + "/pm.xml").c_str());
    exporter.writePM(ofs_pm);

    ofstream ofs_wiggle((outputDir + "/wiggle.txt").c_str());
    exporter.writeWigglePlot(ofs_wiggle);

    ofstream ofs_rt((outputDir + "/calibration.txt").c_str());
    exporter.writeRTCalibrationPlot(ofs_rt);

    ofstream ofs_funny((outputDir + "/funnyPeptides.txt").c_str());
    exporter.writeFunnyPeptides(ofs_funny);

    ofstream ofs_ok((outputDir + "/okPeptides.txt").c_str());
    exporter.writeOKPeptides(ofs_ok);

    ofstream ofs_f2pm((outputDir + "/f2pm.xml").c_str());
    exporter.writeF2PM(ofs_f2pm);

    ofstream ofs_roc((outputDir + "/roc.txt").c_str());
    exporter.writeROCStats(ofs_roc);

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
    
    ofstream ofs_missed((outputDir + "/mismatches.xml").c_str());
    XMLWriter writer(ofs_missed);
    vector<MatchPtr> mismatches = f2pm.getMismatches();
    vector<MatchPtr>::iterator it2 = mismatches.begin();
    for(; it2 != mismatches.end(); ++it2) (*it2)->write(writer);


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

    for(; it != x.second; ++it)
        {
            result->spectrumQueries.push_back(it->second);
            const double& score = (*it->second).searchResult.searchHit.analysisResult.peptideProphetResult.probability;

            //store mz and rt coordinates as mean for Island, and orders of mag weighted by score for stdev
            pair<double,double> means = make_pair((*it->second).precursorNeutralMass, (*it->second).retentionTimeSec);          
            pair<double,double> sigmas = make_pair(.005 * score, 100 * score);
            pair<pair<double,double>, pair<double,double> > params = make_pair(means,sigmas);

            result->gaussians.push_back(IslandizedDatabase::Gaussian(params));

        }

    sort(result->spectrumQueries.begin(), result->spectrumQueries.end(), SortByMass());

    // make a box encapsulating (effectively) the distributions in the island  (3 stdevs above and below max and min mean)

    result->massMin = (*result->spectrumQueries.begin())->precursorNeutralMass - .003*(*result->spectrumQueries.begin())->searchResult.searchHit.analysisResult.peptideProphetResult.probability;
    result->massMax = (*result->spectrumQueries.back()).precursorNeutralMass + .003*(*result->spectrumQueries.back()).searchResult.searchHit.analysisResult.peptideProphetResult.probability;

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
            //            cout << "massMin:" << it->massMin << "massMax: " << it->massMax << " mass:" <<mass << endl;
            if ((it->massMin <= mass) && (mass  <= it->massMax) && (it->rtMin <= rt) && (rt <= it->rtMax) )
                    result.push_back(boost::shared_ptr<Island>(new Island(*it)));
        }

    cout << "number of island candidates: " << result.size() << endl;

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
    //const double normalizationFactor = 1;
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
    dfc->warpRT(wfe);
    PeptideMatcher pm(dfc->_pidf_a, dfc->_pidf_b);
    Feature2PeptideMatcher f2pm; // we will fill in the relevant attributes as we go.

    // Iterate through query features looking for islands at those coordinates
    FdfPtr fdf = dfc->_fdf_a;
    vector<FeatureSequencedPtr> features = fdf->getAllContents();
    vector<FeatureSequencedPtr>::iterator it = features.begin();
    for(; it != features.end(); ++it)        
        {
            if ((*it)->feature->charge ==0) continue;
            // naive: calculate pval for every island in the database
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
                    f2pm._matches.push_back(match);
                    string sequence = (*it)->ms2;

                    if (sequence != "")
                        {
                            if (sequence == best->searchResult.searchHit.peptide) 
                                {
                                    f2pm._truePositives.push_back(match);
                                    cout << "matching:" << sequence << endl;
                                }
                            else 
                                {
                                    f2pm._falsePositives.push_back(match);
                                    cout << "mismatching: " << sequence << " and " << best->searchResult.searchHit.peptide << endl;
                                }
                        }

                }

            else
                {
                    MatchPtr match(new Match(*best, (*it)->feature));
                    match->score = 0; // not maxPVal - we know it's 0, -1 was just an indicator
                    f2pm._mismatches.push_back(match);
                    string sequence = (*it)->ms2;

                    if (sequence != "")
                        {
                            if (sequence == best->searchResult.searchHit.peptide)
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

    ofstream ofs_ms1_5((outputDir + "/ms1_5.pep.xml").c_str());
    exporterf.writePepXML(mspa_in,ofs_ms1_5);

    ofstream ofs_comb((outputDir + "/comb.pep.xml").c_str());
    exporterf.writeCombinedPepXML(mspa_in,ofs_comb);

    // report island and score
    return result;
    
}
