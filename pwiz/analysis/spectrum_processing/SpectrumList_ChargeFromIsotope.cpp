//
// $Id$
//
//
// Original author: William French <william.r.french <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "SpectrumList_ChargeFromIsotope.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include <boost/range/algorithm/remove_if.hpp>

// Predicate for sorting score data structures 
bool sortScoresByMZ (scoreChain i, scoreChain j) { return (i.mzPvalue < j.mzPvalue); } 
bool sortScoresByKLScore (scoreChain i, scoreChain j) { return (i.intensityPvalue < j.intensityPvalue); } 
bool sortScoresByIntensitySum (scoreChain i, scoreChain j) { return (i.intensitySumPvalue < j.intensitySumPvalue); } 
bool sortScoresByOverallPvalue (scoreChain i, scoreChain j) { return (i.overallPvalue < j.overallPvalue); }
bool sortScoresBySumOfRanks (scoreChain i, scoreChain j) { return (i.sumRanks < j.sumRanks); }
// Predicate for sorting retention time data structure
bool sortByRetentionTime (rtimeMap i, rtimeMap j) { return (i.rtime < j.rtime); }
// Comparator for running upper_bound on retention time data structure
bool rtimeComparator(double a, rtimeMap i) { return (i.rtime > a ); }

bool pairSortFunc (pairData i, pairData j) { return (i.mz<j.mz); } // comparator for sorting of parentIon by m/z 
bool pairCompare (pairData i, double mz) { return (i.mz<mz); } // comparator for searching parentIon by m/z

using namespace pwiz::msdata;
using namespace pwiz::cv;
using namespace pwiz::util;

namespace pwiz {
namespace analysis {


PWIZ_API_DECL SpectrumList_ChargeFromIsotope::SpectrumList_ChargeFromIsotope(
    const msdata::MSData& msd,
    int maxCharge,
    int minCharge,
    int parentsBefore,
    int parentsAfter,
    double isolationWidth,
    int defaultChargeMax,
    int defaultChargeMin)
:   SpectrumListWrapper(msd.run.spectrumListPtr),
    maxCharge_(maxCharge),
    minCharge_(minCharge),
    parentsBefore_(parentsBefore),
    parentsAfter_(parentsAfter),
    defaultIsolationWidth_(isolationWidth),
    defaultChargeMax_(defaultChargeMax),
    defaultChargeMin_(defaultChargeMin)
{

    srand( 1234 ); // using the same seed ensures consistency between runs, otherwise we can get different charges and precursors with the exact same settings

    // set parameters
    override_ = true;
    sigVal_ = 0.30;
    nChainsCheck_ = 8;
    mzTol = 0.06; 
    maxIsotopePeaks = 5; // 2,3,4,5 (monoisotope included in this count)
    minIsotopePeaks = 2;
    massNeutron = 1.00335; // massC13 - massC12
    nSamples = 10000;
    upperLimitPadding = 1.25;
    maxNumberPeaks = 40;
    minNumberPeaks = 2;
    int nCharges = maxCharge_ - minCharge_ + 1;
    int nIsotopePeakPossibilities = maxIsotopePeaks - minIsotopePeaks + 1;

    // fill out MS1retentionTimes
    getMS1RetentionTimes(); 
    int surveyCnt = MS1retentionTimes.size();
    double finalRetentionTime = MS1retentionTimes[surveyCnt-1].rtime;

    // simulate the m/z spacing score (sum of squared errors)
    simulateSSE(nCharges,nIsotopePeakPossibilities);

    // simulte the relative intensity distribution (Kullback-Leibler divergence of Poisson-modeled intensities)
    simulateKL(finalRetentionTime,nIsotopePeakPossibilities,surveyCnt>=9?9:surveyCnt);

    // simulate the total peak intensity
    simulateTotIntensity(nIsotopePeakPossibilities);

}

PWIZ_API_DECL SpectrumPtr SpectrumList_ChargeFromIsotope::spectrum(size_t index, bool getBinaryData) const
{

    SpectrumPtr s = inner_->spectrum(index, true);

    // return non-MS/MS as-is
    CVParam spectrumType = s->cvParamChild(MS_spectrum_type);
    if (spectrumType != MS_MSn_spectrum)
        return s;

    // return MS1 as-is
    if (!s->hasCVParam(MS_ms_level) ||
        s->cvParam(MS_ms_level).valueAs<int>() < 2)
        return s;

    // return peakless spectrum as-is
    if (s->defaultArrayLength == 0)
        return s;

    // return precursorless MS/MS as-is
    if (s->precursors.empty() ||
        s->precursors[0].selectedIons.empty())
        return s;

    //cout << "Performing Turbocharger analysis!" << endl;

    // use first selected ion in first precursor
    // TODO: how to deal with multiple precursors and/or selected ions?
    Precursor& precursor = s->precursors[0];
    SelectedIon& selectedIon = precursor.selectedIons[0];

    //int vendorCharge = selectedIon.cvParam(MS_charge_state).valueAs<int>();

    // erase any existing charge-state-related CV params
    vector<CVParam>& cvParams = selectedIon.cvParams;
    IntegerSet possibleChargeStates;
    for(vector<CVParam>::iterator itr = cvParams.begin(); itr != cvParams.end(); ++itr)
    {
        if (itr->cvid == MS_charge_state ||
            itr->cvid == MS_possible_charge_state)
        {
            // some files may have a bogus "0" charge state
            if (override_ || itr->value == "0")
            {
                selectedIon.userParams.push_back(UserParam("old charge state", itr->value));
                itr = --cvParams.erase(itr);
            }
            else if (itr->cvid == MS_possible_charge_state)
                possibleChargeStates.insert(itr->valueAs<int>());
            else if (itr->cvid == MS_charge_state)
                return s;
        }
    }

    double precursorMZ = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
    int nPossibleChargeStates = maxCharge_ - minCharge_ + 1;
    int nIsotopePeakPossibilities = maxIsotopePeaks - minIsotopePeaks + 1;

    // Get the upper/lower bounds of the precursor isolation window.
    // Of the data I've tested, only Thermo lists isolation window info.
    double upperIsoWidth = precursor.isolationWindow.cvParam(MS_isolation_window_upper_offset).valueAs<double>(); 
    upperIsoWidth = upperIsoWidth > 0.0 ? upperIsoWidth : defaultIsolationWidth_;
    double lowerIsoWidth = precursor.isolationWindow.cvParam(MS_isolation_window_lower_offset).valueAs<double>();
    lowerIsoWidth = lowerIsoWidth > 0.0 ? lowerIsoWidth : defaultIsolationWidth_; 
    double targetIsoMZ = precursor.isolationWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
    targetIsoMZ = targetIsoMZ > 0.0 ? targetIsoMZ : precursorMZ;

    vector <int> parentIndex;
    getParentIndices(s,parentIndex);

    // info about chains/score across all parent scans
    vector <scoreChain> allScores;
    vector <isotopeChain> allChains;
    vector < vector<double> > allMZs;
    vector < vector<double> > allIntensities;
    int allChainsCnt=0;
    int assignedCharge = 0;
    double assignedMZ = targetIsoMZ;

    getParentPeaks(s,parentIndex,targetIsoMZ,lowerIsoWidth,upperIsoWidth,allMZs,allIntensities);

    // loop over peaks in parent spectra, build isotope chains and score them
    for (int i=0, iend=parentIndex.size(); i < iend; ++i)
    {

        int nPeaks = allMZs[i].size();
        vector <double> peakMZs(nPeaks);
        vector <double> peakIntensities(nPeaks);
        for (int j=0, jend=nPeaks; j < jend; ++j)
        {
            peakMZs[j] = allMZs[i][j];
            peakIntensities[j] = allIntensities[i][j];
        }
        vector <double> sortedPeakIntensities = peakIntensities;
        sort(sortedPeakIntensities.begin(),sortedPeakIntensities.end());

        vector <isotopeChain> chains;
    
        if ( nPeaks > 1 ) // need at least two peaks to perform analysis
        {
                            
            for (int j=0,cnt=0; j<nPeaks; j++)
            {

                if ( peakMZs[j] > targetIsoMZ + upperIsoWidth ) break; // don't let the monoisotope move outside the isolation width

                for (int k = j+1; k<nPeaks; k++,cnt++)
                {

                    double mzDiff = peakMZs[k] - peakMZs[j]; // guaranteed to give positive value
                    if (mzDiff > massNeutron + mzTol) break; // subsequent sets of peaks will have spacing that is too large

                    double recip = massNeutron / mzDiff;
                    int possibleCharge = int(recip + 0.50); 
                    if ( possibleCharge > maxCharge_ || possibleCharge < minCharge_ ) continue; // Not going to generate chain extension


                    if ( abs( massNeutron / double(possibleCharge) - mzDiff) < mzTol )
                    {
            
                        // We have a hit
                        // Start by checking if this can be connected to previous chains
                        for (int w=0, wend=chains.size(); w < wend; ++w)
                        {

                            if ( possibleCharge != chains[w].charge ) continue;
                            int nPeaksInChain = chains[w].indexList.size();
                            if ( nPeaksInChain >= maxIsotopePeaks ) continue;
                                
                            int finalIndex = chains[w].indexList[chains[w].indexList.size()-1];
                            if ( j == finalIndex ) // connect it and save previous chain
                            {
                                chains.push_back(chains[w]); // push back copy of previous chain with current size
                                chains[w].indexList.push_back(k); // now extend the size of the chain
                            }

                        }

                        // Also create a new chain of length 2
                        isotopeChain newChain;
                        newChain.charge = possibleCharge;
                        newChain.indexList.push_back(j);
                        newChain.indexList.push_back(k); 
                        newChain.parentIndex = i; 
                        chains.push_back(newChain);

                    } // end if peak is within tolerance
            
                } // end for loop k

            } // end for loop j

        } // end if peak list size at least 2

        // Filter any chains that are not the required length
        if (chains.size() > 0)
        {

            vector<isotopeChain>::iterator it = chains.begin();
            while ( it != chains.end() )
            {
                if ( it->charge > 4 && it->indexList.size() < 3 )
                {
                    it = chains.erase(it);
                }
                else
                {
                    ++it;
                }

            }

        }

        scoreChain initializeScore;
        vector <scoreChain> scores(chains.size(),initializeScore);
        if (chains.size() > 0)
        {

            // Now perform the scoring
            vector<isotopeChain>::iterator chainIt = chains.begin();
            while ( chainIt != chains.end() )
            {

                //////////////////////////////////////////////////////////////////////////////////////
                // calculate the relative intensity score, based on the K-L score from a poisson distribution
                int j = chainIt - chains.begin();
                double KLscore = getKLscore( chains[j], peakMZs, peakIntensities );
                int startIndex = (chains[j].indexList.size()-minIsotopePeaks)*nSamples;
                vector <double> simValues;
                simValues.assign(&simulatedKLs[startIndex],&simulatedKLs[startIndex+nSamples-1]);
                vector< double >::iterator i1 = upper_bound(simValues.begin(),simValues.end(),KLscore); // returns iterator to first value that's > KLscore
                int klVectorIndex = i1 - simValues.begin();
                scores[j].intensityPvalue = double(klVectorIndex+1) / double(nSamples+1);
                //////////////////////////////////////////////////////////////////////////////////////


                //////////////////////////////////////////////////////////////////////////////////////
                // calculate average monoisotopic mass based on m/z positions in chain
                double average=0.0;
                for (int k=0, kend=chains[j].indexList.size(); k < kend; k++)
                    average += peakMZs[chains[j].indexList[k]] - double(k)*massNeutron/double(chains[j].charge);
                average /= double(chains[j].indexList.size());

                // calculate summed square error of m/z positions in chain relative to the average monoisotopic m/z
                double sse=0.0;
                for (int k=0, kend=chains[j].indexList.size(); k < kend; k++)
                    sse += pow( average - (peakMZs[chains[j].indexList[k]] - double(k)*massNeutron/double(chains[j].charge)),2);

                // get the P value for the m/z sse
                startIndex = (chains[j].indexList.size()-minIsotopePeaks)*nPossibleChargeStates*nSamples + (chains[j].charge-minCharge_)*nSamples;
                simValues.assign(&simulatedSSEs[startIndex],&simulatedSSEs[startIndex+nSamples-1]);
                i1 = upper_bound(simValues.begin(),simValues.end(),sse); // returns iterator to first value that's > sse
                int mzVectorIndex = i1 - simValues.begin();
                //////////////////////////////////////////////////////////////////////////////////////

                //////////////////////////////////////////////////////////////////////////////////////
                // now calculate the sum of intensity rank score
                if ( nPeaks > maxNumberPeaks )
                    throw runtime_error("[SpectrumList_chargeFromIsotope] nPeaks exceeds maxNumberPeaks for scoring.");
                int intensitySumRank = 0;
                for (int k=0, kend=chains[j].indexList.size(); k < kend; k++)
                {
                    double intensity = peakIntensities[chains[j].indexList[k]];
                    i1 = upper_bound(sortedPeakIntensities.begin(),sortedPeakIntensities.end(),intensity);
                    //i1--;
                    //int rank = nPeaks - (i1 - sortedPeakIntensities.begin()); // remember sortedPeakIntensities sorted from least to most intense
                    //int rank = nPeaks - 1 - (i1 - sortedPeakIntensities.begin()); // remember sortedPeakIntensities sorted from least to most intense
                    int rank = (i1 - 1) - sortedPeakIntensities.begin();
                    rank = nPeaks - rank;
                    intensitySumRank += rank;
                }
                startIndex = (nPeaks - minNumberPeaks)*nIsotopePeakPossibilities*nSamples + (chains[j].indexList.size()-minIsotopePeaks)*nSamples;
                
                // need to know how many samples were generated in the event that N choose k was less than nSamples
                int combos;
                if ( nPeaks > 40 && chains[j].indexList.size() > 2 )
                {
                    combos = nSamples;
                }
                else
                {
                    int combinations = nChoosek(nPeaks,chains[j].indexList.size());
                    combos = combinations > nSamples ? nSamples : combinations;
                }
                vector <int> simIntValues;
                simIntValues.assign(&simulatedIntensityRankSum[startIndex],&simulatedIntensityRankSum[startIndex+combos-1]);
                vector<int>::iterator i2 = upper_bound(simIntValues.begin(),simIntValues.end(),intensitySumRank); // returns iterator to first value that's > KLscore
                int intensitySumVectorIndex = i2 - simIntValues.begin();
                //////////////////////////////////////////////////////////////////////////////////////
                //////////////////////////////////////////////////////////////////////////////////////

                // probability of having lower sse by random chance
                scores[j].mzPvalue = double(mzVectorIndex+1) / double(nSamples+1);
                // K-L score is listed above
                scores[j].intensitySumPvalue = double(intensitySumVectorIndex) / double(combos); // don't add one here because we cannot beat the top rank sum
                scores[j].overallPvalue = scores[j].mzPvalue * scores[j].intensityPvalue * scores[j].intensitySumPvalue;
                scores[j].chainIndex = allChainsCnt++;

                chainIt++;

            } // end for through all chains

            allChains.insert(allChains.end(),chains.begin(),chains.end());
            allScores.insert(allScores.end(),scores.begin(),scores.end());

        } // end if chains.size() > 0
         
    } // end for over parent spectra

    if ( allScores.size() > 0 )
    {

        // rank scoring data structures, first by m/z
        sort(allScores.begin(),allScores.end(),sortScoresByMZ);
        for (int j=0, jend=allScores.size(); j < jend; ++j) allScores[j].mzRank = j+1;

        // now rank by relative intensity K-L score
        sort(allScores.begin(),allScores.end(),sortScoresByKLScore); 
        for (int j=0, jend=allScores.size(); j < jend; ++j) allScores[j].intensityRank = j+1;

        // now rank by intensity rank sum score
        sort(allScores.begin(),allScores.end(),sortScoresByIntensitySum);
        for (int j=0, jend=allScores.size(); j < jend; ++j) allScores[j].intensitySumRank = j+1;

        // sum all the ranks
        for (int j=0, jend=allScores.size(); j < jend; ++j)
            allScores[j].sumRanks = allScores[j].mzRank + allScores[j].intensityRank + allScores[j].intensitySumRank;

        // Finally, sort by the sum of ranks. This appears to work better than 
        // sorting by the overall p-value.
        sort(allScores.begin(),allScores.end(),sortScoresBySumOfRanks);


        int j,jend;
        int scoreListLength = allScores.size();
        int bestChainIndex = -1;
        for (j=0, jend = scoreListLength > nChainsCheck_ ? nChainsCheck_ : scoreListLength; j < jend; ++j)
        {
            if ( allScores[j].intensityPvalue < sigVal_ )
            {
                bestChainIndex = allScores[j].chainIndex;
                break;
            }
        }

        
        if ( bestChainIndex != -1 )
        {

            double mzTolppm = 100.0;
            double mzTolparts = mzTolppm / 1000000.0;
            // search for longer version of chain
            bool updateChain = true;
            while ( updateChain )
            {
                updateChain = false;
                for (int k=j+1, kend = scoreListLength > nChainsCheck_ ? nChainsCheck_ : scoreListLength; k < kend; ++k)
                {

                    if ( allScores[k].intensityPvalue > sigVal_ ) continue; 
                    //
                    // trying to match chain x-y-z to w-x-y-z
                    //
                    // what's acceptable?
                    // have: x-y-z
                    //     - match: w-x-y-z, w-x-y
                    //     - not a match: w-x
                    //
                    // have x-y
                    //     - match: w-x-y
                    //     - not a match w-x
                    //
                    // So basically require at least two m/z matches to the chain you want to replace
                    //
                    //
                    int mapIndexK = allScores[k].chainIndex;
                    if ( allChains[bestChainIndex].charge != allChains[mapIndexK].charge ) continue;
                    int largeChainSize = allChains[mapIndexK].indexList.size();
                    if ( largeChainSize < 3 ) continue;

                    bool relatedChains = true;
                    double epsilon = mzTolparts * allMZs[allChains[mapIndexK].parentIndex][allChains[mapIndexK].indexList[0]];
                    for ( int w=0; w < 2; ++w )
                    {
                        double smallChainMZ = allMZs[allChains[bestChainIndex].parentIndex][allChains[bestChainIndex].indexList[w]];
                        double largeChainMZ = allMZs[allChains[mapIndexK].parentIndex][allChains[mapIndexK].indexList[w+1]];
                        if ( abs( smallChainMZ - largeChainMZ ) > epsilon )
                        {
                                relatedChains = false;
                                break;
                        }
                    }
                    
                    if ( relatedChains )
                    {
                            updateChain = true;
                            bestChainIndex = mapIndexK;
                            j = k;
                            break;
                    } // end if relatedChains
                } // loop over remaining chains
            } // while update chain

            assignedCharge = allChains[bestChainIndex].charge;
            assignedMZ = allMZs[allChains[bestChainIndex].parentIndex][allChains[bestChainIndex].indexList[0]];

        } // endif best chain != -1

    }

    // make sure the possible charge states are erased if we want to override vendor charges
    if (override_ && !possibleChargeStates.empty())
                cvParams.erase(boost::range::remove_if(cvParams, CVParamIs(MS_possible_charge_state)));

    if ( assignedCharge != 0 ) // output single charge and m/z value
    {
        cvParams.push_back(CVParam(override_ ? MS_charge_state : MS_possible_charge_state, assignedCharge));
        s->precursors[0].selectedIons[0].set(MS_selected_ion_m_z, assignedMZ);
    }
    else if ( defaultChargeMin_ > 0 ) // output default charges, if requested by user
    {
        for (int z = defaultChargeMin_; z <= defaultChargeMax_; ++z)
            if (!possibleChargeStates.contains(z) && z != 1)
                cvParams.push_back(CVParam(MS_possible_charge_state, z));
    }

    return s;
}

void SpectrumList_ChargeFromIsotope::getMS1RetentionTimes()
{
    //cout << "Turbocharger initialization, storing and sorting survey scans by retention time. This may take a few minutes for large files." << endl << endl;

    int nScans = inner_->size();
    MS1retentionTimes.reserve( nScans );
    vector <int> ms1Indices;
    for (int i=0,iend=nScans; i<iend; ++i)
    {

        // Using FullMetadata rather than FullData avoids invoking nested filters unnecessarily
        DetailLevel detailLevel = DetailLevel_FullMetadata;
        SpectrumPtr s = inner_->spectrum(i,detailLevel);

        // Waters: scanConfig refers to the function number
        // Thermo: scanConfig corresponds to the msLevel
        // Agilent: scanConfig always returns zero
        // AB Sciex: scanConfig corresponds to the experiment number (for each "cycle" there is a MS1
        //           scan and then a variable number of MS2s...the MS1 is experiment 1 and then all
        //           subsequent scans are 2,3,4,...)
        // Bruker: untested
        if ( s->scanList.scans[0].empty() )
        {
            throw runtime_error("SpectrumList_chargeFromIsotope: no scanEvent present in raw data!");
        }
        int scanConfig = s->scanList.scans[0].cvParam(MS_preset_scan_configuration).valueAs<int>();
        if ( scanConfig == 0 )
        {
            int level = s->cvParam(MS_ms_level).valueAs<int>();
            if ( level != 1 ) continue;
        }
        else if ( scanConfig != 1 ) continue; 

        double rTime = s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds();
        rtimeMap newRtime; newRtime.rtime = rTime; newRtime.indexMap = i;
        MS1retentionTimes.push_back(newRtime);
    }
    int surveyCnt = MS1retentionTimes.size();
    if ( surveyCnt == 0 )
        throw runtime_error("[SpectrumList_chargeFromIsotope] No survey scan found!");
    sort(MS1retentionTimes.begin(),MS1retentionTimes.end(),sortByRetentionTime); // these are generally already sorted, but just in case
}

void SpectrumList_ChargeFromIsotope::simulateSSE(const int nCharges,const int nIsotopePeakPossibilities)
{
    // generate a sample of random spacings to simulate the distribution
    // of summed square errors in m/z space due to random variations
    simulatedSSEs.resize( nSamples*nCharges*nIsotopePeakPossibilities, 0.0 );

    for (int i=0; i < nIsotopePeakPossibilities; ++i)
    {

        int chainLength = i + minIsotopePeaks;

        for (int w=0; w<nCharges; ++w)
        {

            int charge = minCharge_+w;
            vector <double> spacings(nSamples,0.0);
    
            for (int j=0; j<nSamples ; ++j)
            {
                vector <double> mzPoints(chainLength,0.0);
                double averageMonoisotope = 0.0;
                for (int k=1; k < chainLength; ++k)
                {
                    double theoreticalMZ = double(k) * massNeutron / double(charge);
                    double randomVar = -mzTol + ( (double)rand() / RAND_MAX ) * 2 * mzTol; // between -mzTol and +mzTol
                    mzPoints[k] = theoreticalMZ + randomVar;
                    averageMonoisotope += randomVar;
                }
                averageMonoisotope /= double(chainLength);

                double sse = 0.0;
                for (int k=0; k < chainLength; ++k)
                {
                    sse += pow( averageMonoisotope - (mzPoints[k] - double(k)*massNeutron/double(charge)),2);
                }
                spacings[j] = sse;

            } // end for over nSamples
            sort(spacings.begin(),spacings.end());
            int startIndex = i * nCharges * nSamples + w * nSamples;
            for (int j=0; j<nSamples ; ++j) simulatedSSEs[startIndex+j] = spacings[j];
        } // end for over nCharges
    } // end for over maxIsotopePeaks 

}

void SpectrumList_ChargeFromIsotope::simulateKL(const double finalRetentionTime,const int nIsotopePeakPossibilities,const int nMS1sims)
{

    // grab nine survey scans from which to perform simulations
    vector <int> nineMS1indices(nMS1sims,-1);
    for (int i=1; i<=nMS1sims; i++) 
    {
        double rTime = double(i) * finalRetentionTime / double(nMS1sims+1);
        vector< rtimeMap >::iterator i1 = upper_bound(MS1retentionTimes.begin(),MS1retentionTimes.end(),rTime,rtimeComparator); // returns iterator to first value that's > rTime
        int nearestMS1scanVectorIndex;
        if ( i1 == MS1retentionTimes.begin() )
            nearestMS1scanVectorIndex = 0;
        else
            nearestMS1scanVectorIndex = (i1 - 1) - MS1retentionTimes.begin();
        if (nearestMS1scanVectorIndex<0) nearestMS1scanVectorIndex = 0;
        nineMS1indices[i-1] = MS1retentionTimes[nearestMS1scanVectorIndex].indexMap; 
    }

    ///////////////////////////////////////////////
    // now simulate the K-L score
    simulatedKLs.resize( nIsotopePeakPossibilities * nSamples, 0.0 );
    SpectrumListPtr CWTpeakPicker = instantiatePeakPicker( nineMS1indices );
    vector < vector<double> > allIntensity, allMZ;
    vector < vector<int> > mostIntensePeaks;
    int numberIntensePeaks = 200;
    
    for (int i=0,iend=nineMS1indices.size(); i < iend ; ++i)
    {
            
        SpectrumPtr s = CWTpeakPicker->spectrum( i, true ); // this applies no filtering 
        BinaryData<double> peakIntensities = s->getIntensityArray()->data;
        BinaryData<double> peakMZs = s->getMZArray()->data;

        allIntensity.push_back( peakIntensities );
        allMZ.push_back( peakMZs );

        vector<double> sortedPeakIntensities = peakIntensities;
        sort( sortedPeakIntensities.begin(), sortedPeakIntensities.end() );
        double intensityCutoff = 0.0;
        int MS1Peaks = sortedPeakIntensities.size();
        if ( MS1Peaks >= numberIntensePeaks )
            intensityCutoff = sortedPeakIntensities[ peakIntensities.size() - numberIntensePeaks ];

        vector<int> highIntensityIndices;
        for (int j=0, jend=peakIntensities.size(); j < jend; ++j)
        {
            if ( peakIntensities[j] >= intensityCutoff )
                highIntensityIndices.push_back(j);
        }

        mostIntensePeaks.push_back( highIntensityIndices );

    }

    for (int j=0; j < nIsotopePeakPossibilities; ++j)
    {

        int chainLength = j + minIsotopePeaks;
        vector <double> poolA(chainLength,0.0);
        vector <double> poolB(chainLength,0.0);
        vector <double> KLscores(nSamples,0.0);

        for (int k=0; k<nSamples; ++k)
        {

            // get a random spectrum
            int randomSpectrum = rand() % nMS1sims; // random spectrum index between 0 and 8
            int peakCnt = allMZ[randomSpectrum].size();
            vector<double> peakMZs(peakCnt), peakIntensities(peakCnt);
            for ( int w=0, wend = peakMZs.size(); w < wend; ++w )
            {
                peakMZs[w] = allMZ[randomSpectrum][w];
                peakIntensities[w] = allIntensity[randomSpectrum][w];
            }

            int randomPeakIndex = rand() % mostIntensePeaks[randomSpectrum].size(); // from 0 to .size()-1
            int randomMZpoint = mostIntensePeaks[randomSpectrum][randomPeakIndex];
            double targetMZvalue = peakMZs[randomMZpoint];
    
            std::vector<double>::iterator lowerLimit = lower_bound( peakMZs.begin(), peakMZs.end(), targetMZvalue - defaultIsolationWidth_ );
            lowerLimit = lowerLimit == peakMZs.end() ? peakMZs.begin() : lowerLimit; // in case value is out of bounds
            std::vector<double>::iterator upperLimit = lower_bound( peakMZs.begin(), peakMZs.end(), targetMZvalue + defaultIsolationWidth_ + upperLimitPadding );
            upperLimit = upperLimit == peakMZs.end() ? peakMZs.end() - 1 : upperLimit; // in case value is out of bounds


            vector <double> windowIntensity, windowMZ;
            windowMZ.assign( lowerLimit, upperLimit );

            // this is possible if we chose the last peak of the spetrum and there is nothing around it
            if ( windowMZ.size() == 0 ) // no peaks found
            {
                KLscores[k] = 100; // just apply a really bad KL score
                continue;
            }

            int lowerLimitInt = lowerLimit - peakMZs.begin(), upperLimitInt = upperLimit - peakMZs.begin(); // convert iterators to ints
            windowIntensity.assign( &peakIntensities[lowerLimitInt], &peakIntensities[upperLimitInt] );

            // remove any zero-intensity peaks, which will lead to NAN results in KL scoring
            vector<double>::iterator winIt = windowIntensity.begin();
            while ( winIt != windowIntensity.end() )
            {
                if ( *winIt == 0.0 )
                {
                    windowIntensity.erase( winIt );
                    int mzIndex = winIt - windowIntensity.begin();
                    windowMZ.erase( windowMZ.begin() + mzIndex );
                }
                else
                {
                    ++winIt;
                }
            }

            // filter the peaks
            int peaksInWindow = windowIntensity.size();
            while ( peaksInWindow > maxNumberPeaks ) 
            {

                vector<double>::iterator minIt = min_element( windowIntensity.begin(), windowIntensity.end() );
                vector<double>::iterator maxIt = max_element( windowIntensity.begin(), windowIntensity.end() );
                vector<int> elementsForDeletion;

                for (int w=0, wend = windowIntensity.size(); w < wend; ++w)
                {
                    if ( windowIntensity[w] < *maxIt * 0.05 )
                        elementsForDeletion.push_back( w );
                    else if ( windowIntensity[w] == *minIt )
                        elementsForDeletion.push_back( w ); // remove if element equal to min or 
                }
    
                for (int w=0, wend = elementsForDeletion.size(); w < wend; ++w )
                {
                    windowIntensity.erase( windowIntensity.begin() + elementsForDeletion[w] - w );
                    windowMZ.erase( windowMZ.begin() + elementsForDeletion[w] - w );
                }

                peaksInWindow = windowIntensity.size();

            }

            int windowSize = windowIntensity.size();
            isotopeChain localChain;
            localChain.charge = rand() % ( maxCharge_ - minCharge_ + 1 ) + minCharge_; // goes from minCharge_ to maxCharge_

            vector<int> randomIndicesInWindow( chainLength );

            for (int w=0; w<chainLength; ++w) randomIndicesInWindow[ w ] = rand() % windowSize;
            sort( randomIndicesInWindow.begin(), randomIndicesInWindow.end() );
            for (int w=0; w<chainLength; ++w) localChain.indexList.push_back( randomIndicesInWindow[w] );
            KLscores[k] = getKLscore( localChain, windowMZ, windowIntensity );

        }
        sort(KLscores.begin(),KLscores.end());
        int startIndex = j * nSamples;
        for (int k=0; k<nSamples ; ++k) simulatedKLs[startIndex+k] = KLscores[k];


    }
        
}

void SpectrumList_ChargeFromIsotope::simulateTotIntensity(const int nIsotopePeakPossibilities)
{
    
    // If there are fewer than 10,000 possible combinations of peaks, then
    // enumerate the combinations manually. Otherwise sample 10,000 random
    // combinations to approximate the distribution.
    
    int peakNumber = maxNumberPeaks - minNumberPeaks + 1;
    simulatedIntensityRankSum.resize( nIsotopePeakPossibilities * nSamples * peakNumber, 0 );

    for (int nPeaks = minNumberPeaks; nPeaks <= maxNumberPeaks; ++nPeaks)
    {

        int maxRandomRank = nPeaks;

        for (int w=0; w < nIsotopePeakPossibilities; ++w)
        {
    
            int chainLength = w + minIsotopePeaks;
            if (chainLength > nPeaks) break;

            int combinations; int sampleSize;
            if ( nPeaks > 40 && chainLength > 2 )
            {
                // all possible combinations could be too large to be held by a normal int
                combinations = nSamples;
            }
            else
            {
                combinations = nChoosek(nPeaks,chainLength);
            }
            sampleSize = combinations > nSamples ? nSamples : combinations;
            vector <int> intensityRankSum(sampleSize,0);

            if ( combinations >= nSamples ) // sample nSamples random combinations
            {

                for (int j=0; j<nSamples ; ++j)
                {

                    for (int k=0; k<chainLength; ++k)
                    {
                        int randomRank = rand() % maxRandomRank + 1; // generates random int between 1 and maxRandomRank
                        intensityRankSum[j] += randomRank;
                    }

                } // end for over nSamples
            }
            else // enumerate all possible combinations
            {
                int combinationCnt=0;
                vector<bool> v(nPeaks);
                fill(v.begin() + chainLength, v.end(), true);
                do {
                    for (int j = 0; j < nPeaks; ++j) {
                        if (!v[j]) {
                            intensityRankSum[combinationCnt] += j+1;
                        }
                    }
                    combinationCnt++;
                } while (next_permutation(v.begin(), v.end()));
            }
            
            sort(intensityRankSum.begin(),intensityRankSum.end());
            int startIndex = (nPeaks-minNumberPeaks)*nIsotopePeakPossibilities*nSamples + w * nSamples;
            for (int j=0; j<sampleSize ; ++j) simulatedIntensityRankSum[startIndex+j] = intensityRankSum[j];
        
        } // end for over maxIsotopePeaks 

    } // end for over nPeaks

}

void SpectrumList_ChargeFromIsotope::getParentIndices( const SpectrumPtr s, vector <int> & parents ) const
{
    int nMS1scans = MS1retentionTimes.size();

    if ( nMS1scans > 0 )
    {
        int parentCnt = 0;
        double rTime = s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds();
        vector< rtimeMap >::const_iterator i1 = upper_bound(MS1retentionTimes.begin(),MS1retentionTimes.end(),rTime,rtimeComparator); // returns iterator to first value that's > rTime

		int nearestMS1scanVectorIndex;
		if ( i1 == MS1retentionTimes.begin() )
			nearestMS1scanVectorIndex = 0;
		else
			nearestMS1scanVectorIndex = (i1 - 1) - MS1retentionTimes.begin();

        while (nearestMS1scanVectorIndex >= 0 && parentCnt < parentsBefore_)
        {
            parents.push_back(MS1retentionTimes[nearestMS1scanVectorIndex].indexMap);
            nearestMS1scanVectorIndex--;
            parentCnt++;
        }
        nearestMS1scanVectorIndex = i1 - MS1retentionTimes.begin();
        parentCnt = 0; // resetting this
        //while (i1 != MS1retentionTimes.end() && parentCnt < parentsAfter_)
        while (nearestMS1scanVectorIndex < nMS1scans && parentCnt < parentsAfter_)
        {
            parents.push_back(MS1retentionTimes[nearestMS1scanVectorIndex].indexMap);
            //i1--;
            nearestMS1scanVectorIndex++;
            parentCnt++;
        }

    }
}

void SpectrumList_ChargeFromIsotope::getParentPeaks(const SpectrumPtr s,const vector <int> & parents,const double targetIsoMZ,const double lowerIsoWidth,const double upperIsoWidth,
                                                    vector< vector <double> > & mzs,vector< vector <double> > & intensities) const
{

    // grab the peaks from the parent scans
    for (int i=0, iend=parents.size(); i < iend; ++i)
    {

        vector< int > currentParent(1,parents[i]);
        DetailLevel detailLevel = DetailLevel_FullMetadata;
        SpectrumPtr sSurvey = inner_->spectrum(parents[i], detailLevel);
        vector<CVParam>& cvParams = sSurvey->cvParams;
        vector<CVParam>::iterator itr;
        itr = std::find(cvParams.begin(), cvParams.end(), MS_centroid_spectrum);

        if ( itr != cvParams.end() ) // MS1 spectrum already centroided, just grab the peaks in the isolation window
        {

            SpectrumPtr sPar = inner_->spectrum( parents[i], true );
            BinaryData<double>& peakMZs = sPar->getMZArray()->data;
            BinaryData<double>& peakIntensities = sPar->getIntensityArray()->data;
            vector<int> elementsForDeletion;

            for (int j=0, jend = peakIntensities.size(); j < jend; ++j)
            {
                if ( peakMZs[j] < targetIsoMZ - lowerIsoWidth || peakMZs[j] > targetIsoMZ + upperIsoWidth + upperLimitPadding )
                    elementsForDeletion.push_back( j );
            }
    
            for (int j=0, jend = elementsForDeletion.size(); j < jend; ++j )
            {
                peakIntensities.erase( peakIntensities.begin() + elementsForDeletion[j] - j );
                peakMZs.erase( peakMZs.begin() + elementsForDeletion[j] - j );
            }

            /**
            Reduce number of peaks in window to maxNumberPeaks if needed 
            **/
            int peaksInWindow = peakIntensities.size();
            elementsForDeletion.clear();
            while ( peaksInWindow > maxNumberPeaks ) 
            {

                BinaryData<double>::iterator minIt = min_element( peakIntensities.begin(), peakIntensities.end() );

                for (int w=0, wend = peakIntensities.size(); w < wend; ++w)
                {
                    if ( peakIntensities[w] == *minIt )
                        elementsForDeletion.push_back( w ); 
                }
    
                for (int w=0, wend = elementsForDeletion.size(); w < wend; ++w )
                {
                    peakIntensities.erase( peakIntensities.begin() + elementsForDeletion[w] - w );
                    peakMZs.erase( peakMZs.begin() + elementsForDeletion[w] - w );
                }

                peaksInWindow = peakIntensities.size();

            }

            mzs.push_back( peakMZs );
            intensities.push_back( peakIntensities );


        }
        else // need to perform CWT peak-picking to grab the peaks
        {

            SpectrumListPtr parentPeakPicker = instantiatePeakPicker( currentParent, targetIsoMZ, lowerIsoWidth, upperIsoWidth + upperLimitPadding ); // add an additional dalton on the right to check for heavy isotopes; don't let monoisotope exceed the isolation window, though. See below.
            SpectrumPtr sPar = parentPeakPicker->spectrum( 0, true );
            BinaryData<double>& peakMZs = sPar->getMZArray()->data;
            BinaryData<double>& peakIntensities = sPar->getIntensityArray()->data;

            BinaryData<double>::iterator minIt = min_element(peakIntensities.begin(), peakIntensities.end());
            BinaryData<double>::iterator maxIt = max_element(peakIntensities.begin(), peakIntensities.end());
            vector<int> elementsForDeletion;

            for (int j=0, jend = peakIntensities.size(); j < jend; ++j)
            {
                if ( peakIntensities[j] < *maxIt * 0.05 )
                    elementsForDeletion.push_back( j );
                else if ( peakIntensities[j] == *minIt )
                    elementsForDeletion.push_back( j ); // remove if element equal to min or 
            }
    
            for (int j=0, jend = elementsForDeletion.size(); j < jend; ++j )
            {
                peakIntensities.erase( peakIntensities.begin() + elementsForDeletion[j] - j );
                peakMZs.erase( peakMZs.begin() + elementsForDeletion[j] - j );
            }

            /**
            Reduce number of peaks in window to maxNumberPeaks if needed 
            **/
            int peaksInWindow = peakIntensities.size();
            elementsForDeletion.clear();
            while ( peaksInWindow > maxNumberPeaks ) 
            {

                BinaryData<double>::iterator minIt = min_element( peakIntensities.begin(), peakIntensities.end() );

                for (int w=0, wend = peakIntensities.size(); w < wend; ++w)
                {
                    if ( peakIntensities[w] == *minIt )
                        elementsForDeletion.push_back( w ); 
                }
    
                for (int w=0, wend = elementsForDeletion.size(); w < wend; ++w )
                {
                    peakIntensities.erase( peakIntensities.begin() + elementsForDeletion[w] - w );
                    peakMZs.erase( peakMZs.begin() + elementsForDeletion[w] - w );
                }

                peaksInWindow = peakIntensities.size();

            }

            mzs.push_back( peakMZs );
            intensities.push_back( peakIntensities );
        }

    }
}

SpectrumListPtr SpectrumList_ChargeFromIsotope::instantiatePeakPicker( const vector <int> & indices ) const
{
    shared_ptr<SpectrumListSimple> smallSpectrumList(new SpectrumListSimple);

    for (int i=0,iend=indices.size(); i < iend ; ++i)
    {
        SpectrumPtr sParent = inner_->spectrum( indices[i], true);
        smallSpectrumList->spectra.push_back(sParent);
    }

    string msLevelSets = "1";
    IntegerSet msLevelsToCentroid;
    msLevelsToCentroid.parse(msLevelSets);
    bool preferVendor = false;
    double snr = 0.0;
    double mzTol = 0.05;
    int fixedPeaksKeep = 0; // this is for generating peaks to simulate the K-L score
    SpectrumListPtr parentPeakPicker(new SpectrumList_PeakPicker(smallSpectrumList, 
                                        PeakDetectorPtr(new CwtPeakDetector(snr,fixedPeaksKeep,mzTol)),
                                        preferVendor, msLevelsToCentroid ) );

    return parentPeakPicker;

}



SpectrumListPtr SpectrumList_ChargeFromIsotope::instantiatePeakPicker( const vector <int> & indices, const double targetIsoMZ, 
    const double lowerIsoWidth, const double upperIsoWidth ) const
{

    shared_ptr<SpectrumListSimple> smallSpectrumList(new SpectrumListSimple);

    // parameters for re-sampling via linear interpolation
    SpectrumPtr s = inner_->spectrum( indices[0], true);
    vector<double> summedIntensity;
    vector<double> summedMZ;

    // Grab the binary data for the parent spectrum
    SpectrumPtr sParent = inner_->spectrum( indices[0], true);
    BinaryData<double>& parentMz = sParent->getMZArray()->data;
    BinaryData<double>& parentIntensity = sParent->getIntensityArray()->data;

    // Get window of data around the target m/z value
    vector<double> windowMZ,windowIntensity;
    BinaryData<double>::iterator lowerLimit = lower_bound(parentMz.begin(), parentMz.end(), targetIsoMZ - lowerIsoWidth);
    lowerLimit = lowerLimit == parentMz.end() ? parentMz.begin() : lowerLimit; // in case value is out of bounds
    BinaryData<double>::iterator upperLimit = lower_bound(parentMz.begin(), parentMz.end(), targetIsoMZ + upperIsoWidth);
    upperLimit = upperLimit == parentMz.end() ? parentMz.end() - 1 : upperLimit; // in case value is out of bounds
    windowMZ.assign( lowerLimit, upperLimit );
            
    if ( windowMZ.size() > 1 )
    {

        int lowerLimitInt = lowerLimit - parentMz.begin(), upperLimitInt = upperLimit - parentMz.begin(); // convert interators to ints
        windowIntensity.assign( &parentIntensity[lowerLimitInt], &parentIntensity[upperLimitInt] );

        summedMZ.assign( lowerLimit, upperLimit );
        summedIntensity.assign( &parentIntensity[lowerLimitInt], &parentIntensity[upperLimitInt] );

    }
                
    // Now fill in the spectrum data structure that will be fed to the peak-picker
    smallSpectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    Spectrum& pSpec = *smallSpectrumList->spectra[0];
    pSpec.index = 0;
    pSpec.set(MS_ms_level, 1);
    pSpec.set(MS_profile_spectrum);
    BinaryDataArrayPtr pSpec_mz(new BinaryDataArray), pSpec_intensity(new BinaryDataArray);
    pSpec_mz->set(MS_m_z_array, "", MS_m_z), pSpec_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
    pSpec_mz->data.resize( summedIntensity.size() ), pSpec_intensity->data.resize( summedIntensity.size() );

    for (size_t j=0, jend=summedIntensity.size() ; j < jend; ++j)
    {
        pSpec_mz->data[j] = summedMZ[j];
        pSpec_intensity->data[j] = summedIntensity[j];
    }
    
    pSpec.binaryDataArrayPtrs.push_back( pSpec_mz ), pSpec.binaryDataArrayPtrs.push_back( pSpec_intensity );
    pSpec.defaultArrayLength = pSpec_mz->data.size();

    string msLevelSets = "1";
    IntegerSet msLevelsToCentroid;
    msLevelsToCentroid.parse(msLevelSets);
    bool preferVendor = false;
    double snr = 0.0;
    double mzTol = 0.05;
    int fixedPeaksKeep = maxNumberPeaks;
    SpectrumListPtr parentPeakPicker(new SpectrumList_PeakPicker(smallSpectrumList, 
                                        PeakDetectorPtr(new CwtPeakDetector(snr,fixedPeaksKeep,mzTol)),
                                        preferVendor, msLevelsToCentroid ) );

    return parentPeakPicker;

}


} // namespace analysis
} // namespace pwiz


int nChoosek(int n,int k)
{
    int numerator=1;
    int denominator=1;
    // Multiplicative formula
    for (int i=0;i<k;++i)
    {
        numerator *= n-i;
        denominator *= i+1;
    }
    return numerator/denominator;
}

double getKLscore( const isotopeChain chain, const vector <double> & mzs, const vector <double> & intensities )
{

    if ( mzs.size() != intensities.size() )
         throw runtime_error("[SpectrumList_chargeFromIsotope, getKLscore] m/z and intensity vectors must be equal in size.");

	//cout << "computing the KL score" << endl;

    // First, convert from molecular weight of ion to Mstar, which is linear mapping 
    double lambda = 1.0 / 1800.0; // parameter for poisson model
    double Mstar = lambda * mzs[chain.indexList[0]] * double(chain.charge); // from msInspect paper
    double Mexp = exp( -Mstar );
                    
    double poissonSum = 0.0; // sum up all the poisson values to normalize
    double observedIntensitySum = 0.0; // initialize this sum
    double KLscore = 0.0;

    vector <double> poissonVals(chain.indexList.size(),0.0);

    // calculate poisson distribution and sum up the intensities for normalization
    for (int k=0,kend=chain.indexList.size(); k < kend ; ++k)
    {
        // probability of seeing isotope with k additional ions relative to monoisotope
        double poisson = Mexp * pow(Mstar,k);
        for (int w=k;w>1;w--) poisson /= double(w);
        poissonVals[k] = poisson; // store value

        // sums for normalization
        poissonSum += poisson;
        observedIntensitySum += intensities[chain.indexList[k]];
    }

    // calculate the K-L score
    for (int k=0,kend=chain.indexList.size(); k < kend ; ++k)
    {
        poissonVals[k] /= poissonSum; // normalize these values to use in the K-L score
        double normObservedIntensity = intensities[chain.indexList[k]] / observedIntensitySum;
        KLscore += normObservedIntensity * log10( normObservedIntensity / poissonVals[k]  );
    }

	//cout << "done computing the KL score" << endl;

    return KLscore;


}