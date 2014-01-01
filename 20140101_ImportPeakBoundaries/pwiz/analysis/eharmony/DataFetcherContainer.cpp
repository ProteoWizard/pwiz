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
/// DataFetcherContainer.cpp
///

#include "DataFetcherContainer.hpp"
#include "PeptideMatcher.hpp"
#include "SearchNeighborhoodCalculator.hpp"
#include "pwiz/utility/proteome/Peptide.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include <iostream>
#include <fstream>

using namespace pwiz::eharmony;
using namespace pwiz::proteome;

DataFetcherContainer::DataFetcherContainer(const PidfPtr pidf_a, const PidfPtr pidf_b, const FdfPtr fdf_a, const FdfPtr fdf_b)
{
    _pidf_a = pidf_a;
    _pidf_b = pidf_b;
    _fdf_a = fdf_a;
    _fdf_b = fdf_b;

}

namespace{

    ofstream ofs("peptide_coords.txt");
    FeatureSequencedPtr getBestMatch(boost::shared_ptr<SpectrumQuery> sq, const FdfPtr fdf)
    {             
        SearchNeighborhoodCalculator snc;
        snc._mzTol = .05;
        snc._rtTol = 1000;

        FeatureSequencedPtr result(new FeatureSequenced());
        Bin<FeatureSequenced> featureBin = fdf->getBin();	

        pair<double,double> peptideCoords = make_pair(Ion::mz(sq->precursorNeutralMass, sq->assumedCharge), sq->retentionTimeSec);
        ofs << peptideCoords.first << "\t" << peptideCoords.second << "\n";

        double bestScore = 100000000;               
        vector<boost::shared_ptr< FeatureSequenced> > adjacentContenders;
        featureBin.getAdjacentBinContents(peptideCoords, adjacentContenders);
        vector<boost::shared_ptr< FeatureSequenced> >::iterator ac_it = adjacentContenders.begin();

        for(; ac_it != adjacentContenders.end(); ++ac_it)
            {

                if ( (*ac_it)->feature->charge == sq->assumedCharge )
                    {
                        double mzDiff = fabs(peptideCoords.first - (*ac_it)->feature->mz)/.001;
                        double rtDiff = fabs(peptideCoords.second - (*ac_it)->feature->retentionTime)/100;
                        double score = sqrt(mzDiff*mzDiff + rtDiff*rtDiff);

                        if ( score < bestScore )
                            {
                                result = *ac_it;     
                                bestScore = score;
                                (*ac_it)->ms2 = sq->searchResult.searchHit.peptide;

                            }

                    }

            }            

        return result;

    } 

    void executeAdjustRT(PidfPtr pidf, FdfPtr fdf)
    {
        int counter = 0;

        vector<boost::shared_ptr<SpectrumQuery> > spectrumQueries = pidf->getAllContents();      
        vector<boost::shared_ptr<SpectrumQuery> >::iterator sq_it = spectrumQueries.begin();

        for(; sq_it != spectrumQueries.end(); ++sq_it)
            {
                
                if ( counter % 100 == 0) cout << "Spectrum query:"  << counter << endl;
           		
                FeatureSequencedPtr fs = getBestMatch((*sq_it), fdf);                                      
                if (fs->feature->retentionTime > 0) // f exists
                    {   
                        
                        // set FeatureSequenced attributes
                        fs->ms2 = (*sq_it)->searchResult.searchHit.peptide;                      
                        Peptide peptide(fs->ms2);
                        fs->calculatedMass = peptide.monoisotopicMass(0, false);
                        fs->ppProb = (*sq_it)->searchResult.searchHit.analysisResult.peptideProphetResult.probability;
                        fs->peptideCount += 1;
                        
                        // change peptide retention time
                        (*sq_it)->retentionTimeSec = fs->feature->retentionTime;

                                                
                        
                    }

                counter +=1;

            }
	
    }

} // anonymous namespace

void DataFetcherContainer::adjustRT(bool runA, bool runB)
{
    if (runA)
        {
            cout << "[eharmony] Matching MS2 peptides to their precursor features ... " << endl;
            executeAdjustRT(_pidf_a, _fdf_a);
            _pidf_a->setRtAdjustedFlag(true);
            _fdf_a->setMS2LabeledFlag(true);            
                       
        }
    
    if (runB)
        {
            cout << "[eharmony] Matching MS2 peptides to their precursor features ... " << endl;
            executeAdjustRT(_pidf_b, _fdf_b);
            _pidf_b->setRtAdjustedFlag(true);
            _fdf_b->setMS2LabeledFlag(true);

        }

}

void DataFetcherContainer::getAnchors(const int& freq, const double& tol)
{
    _anchors.clear(); // erase any previous anchors

    PeptideMatcher pm(_pidf_a, _pidf_b);
    PeptideMatchContainer matches = pm.getMatches();

    size_t index = 0; 
    PeptideMatchContainer::iterator it = matches.begin();

    for( ; it != matches.end() ; ++index, ++it ) 
        {
            const double& rt_a = it->first->retentionTimeSec;
            const double& rt_b = it->second->retentionTimeSec;

            if (index % freq == 0 && fabs(rt_a - rt_b) < tol) _anchors.push_back(make_pair(rt_a, rt_b));

        }         

}

void DataFetcherContainer::warpRT(const WarpFunctionEnum& wfe, const int& anchorFreq, const double& anchorTol) 
{    
    cout << "[eharmony] Warping retention time ... " << endl;
    getAnchors(anchorFreq, anchorTol);
    pair<vector<double>, vector<double> > peptideRetentionTimes = getPeptideRetentionTimes();
    pair<vector<double>, vector<double> > featureRetentionTimes = getFeatureRetentionTimes();
   
    switch(wfe)
        {

        case Default:
            {}            
            break;

        case Linear:
            {   
                LinearWarpFunction lwf(_anchors);

                vector<double> result_a;
                vector<double> result_b;
                vector<double> feature_result_a;
                vector<double> feature_result_b;

                lwf(peptideRetentionTimes.first, result_a);
                lwf(peptideRetentionTimes.second, result_b);
                lwf(featureRetentionTimes.first, feature_result_a);
                lwf(featureRetentionTimes.second, feature_result_b);
                
                const pair<vector<double> , vector<double> > warpedPeptideTimes = make_pair(result_a, result_b);
                const pair<vector<double> , vector<double> > warpedFeatureTimes = make_pair(feature_result_a, feature_result_b);

                putPeptideRetentionTimes(warpedPeptideTimes);
                putFeatureRetentionTimes(warpedFeatureTimes);

            }

            break;

        case PiecewiseLinear:
            {
                PiecewiseLinearWarpFunction plwf(_anchors);

                vector<double> result_a;
                vector<double> result_b;
                vector<double> feature_result_a;
                vector<double> feature_result_b;

                plwf(peptideRetentionTimes.first, result_a);
                plwf(peptideRetentionTimes.second, result_b);
                plwf(featureRetentionTimes.first, feature_result_a);
                plwf(featureRetentionTimes.second, feature_result_b);

                const pair<vector<double> , vector<double> > warpedPeptideTimes = make_pair(result_a, result_b);
                const pair<vector<double> , vector<double> > warpedFeatureTimes = make_pair(feature_result_a, feature_result_b);

                putPeptideRetentionTimes(warpedPeptideTimes);
                putFeatureRetentionTimes(warpedFeatureTimes);

            }
            
            break;

        }

}

// helper functions
vector<double> getRTs(PidfPtr pidf)
{
    vector<double> result;
    vector<boost::shared_ptr<SpectrumQuery> > sqs = pidf->getAllContents();
    vector<boost::shared_ptr<SpectrumQuery> >::iterator it = sqs.begin();
    for(; it != sqs.end(); ++it) result.push_back((*it)->retentionTimeSec);
    
    return result;

}

vector<double> getRTs(FdfPtr fdf)
{
    vector<double> result;
    vector<FeatureSequencedPtr> fss = fdf->getAllContents();    
    vector<FeatureSequencedPtr>::iterator it = fss.begin();

    for(; it != fss.end(); ++it) result.push_back((*it)->feature->retentionTime);

    return result;

}

pair<vector<double>, vector<double> > DataFetcherContainer::getPeptideRetentionTimes()
{
    vector<double> pidf_a_rts = getRTs(_pidf_a);
    vector<double> pidf_b_rts = getRTs(_pidf_b);
    
    return make_pair(pidf_a_rts, pidf_b_rts);

}


pair<vector<double>, vector<double> > DataFetcherContainer::getFeatureRetentionTimes()
{

    vector<double> fdf_a_rts = getRTs(_fdf_a);
    vector<double> fdf_b_rts = getRTs(_fdf_b);

    return make_pair(fdf_a_rts, fdf_b_rts);

}

// helper function
void putRTs(vector<boost::shared_ptr<SpectrumQuery> > sqs, vector<double> times)
{
    vector<boost::shared_ptr<SpectrumQuery> >::iterator it = sqs.begin();
    vector<double>::iterator time_it = times.begin();
    for(; it != sqs.end(); ++it, ++time_it)
        {
            (*it)->retentionTimeSec = *time_it;

        }
    

}

void putRTs(vector<FeatureSequencedPtr> fs, vector<double> times)
{
    vector<FeatureSequencedPtr>::iterator it = fs.begin();
    vector<double>::iterator time_it = times.begin();
    for(; it != fs.end(); ++it, ++time_it)
        {
            (*it)->feature->retentionTime = *time_it;

        }

}

void DataFetcherContainer::putPeptideRetentionTimes(const pair<vector<double>, vector<double> >& times)
{
    vector<boost::shared_ptr<SpectrumQuery> > sqs_a = _pidf_a->getAllContents();
    vector<boost::shared_ptr<SpectrumQuery> > sqs_b = _pidf_b->getAllContents();
    
    putRTs(sqs_a, times.first);
    putRTs(sqs_b, times.second);

}

void DataFetcherContainer::putFeatureRetentionTimes(const pair<vector<double>, vector<double> >& times)
{
    vector<FeatureSequencedPtr> fs_a = _fdf_a->getAllContents();
    vector<FeatureSequencedPtr> fs_b = _fdf_b->getAllContents();

    putRTs(fs_a, times.first);
    putRTs(fs_b, times.second);

}
