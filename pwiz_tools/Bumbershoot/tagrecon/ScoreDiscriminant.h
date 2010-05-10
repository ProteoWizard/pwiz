//
// $Id: $
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University


#ifndef _ScoreDiscriminant_H
#define _ScoreDiscriminant_H

#include "freicore/percolator/PercolatorCInterface.h"
#include "freicore/percolator/Normalizer.h"
#include "tagreconSpectrum.h"
#include "shared_types.h"

namespace freicore {
    namespace tagrecon {

        /* This data structure takes a list of features and runs percolator to
           to determine the optimal discriminant. 
         */
        struct ScoreDiscriminant
        {
            // Names and weights of the features selected for disciminator
            set<string> featureNames;
            map< size_t, map<string,double> > featureValuesMatrix;
            map< size_t, SetType > featureType;
            map<string,double> featureWeights;
            // Tracking variables
            size_t maxResults;
            size_t numDecoys;
            size_t numTargets;
            // Z-state dependent score normalization variables
            map<string, map< size_t, double> > maxScoresByZState;
            map<string, map< size_t, double> > minScoresByZState; 
            // Variable to indicate the health of the run
            bool isSuccessful;

            /*
                This function takes a list of spectra, chooses 
                features for training and initiates the percolator
            */
            ScoreDiscriminant(const SpectraList& spectra)
            {
                for(size_t z = 1; z <= g_rtConfig->NumChargeStates; ++z)
                {
                    maxScoresByZState["mvh"][z] = (double) INT_MIN;
                    maxScoresByZState["mzfidelity"][z] = (double) INT_MIN;
                    maxScoresByZState["xcorr"][z] = (double) INT_MIN;
                    minScoresByZState["mvh"][z] = (double) INT_MAX;
                    minScoresByZState["mzfidelity"][z] = (double) INT_MAX;
                    minScoresByZState["xcorr"][z] = (double) INT_MAX;
                }

                // Figure out how many targets and decoys we have in the total set
                numDecoys = 0;
                numTargets = 0;
                BOOST_FOREACH(Spectrum* s, spectra)
                {
                    Spectrum::SearchResultSetType::reverse_iterator rItr = s->topTargetHits.rbegin();
                    if(rItr != s->topTargetHits.rend() && (*rItr).mvh>0.0)
                    {
                        maxScoresByZState["mvh"][s->id.charge] = max(maxScoresByZState["mvh"][s->id.charge], (*rItr).mvh);
                        maxScoresByZState["mzfidelity"][s->id.charge] = max(maxScoresByZState["mzfidelity"][s->id.charge], (*rItr).mzFidelity);
                        maxScoresByZState["xcorr"][s->id.charge] = max(maxScoresByZState["xcorr"][s->id.charge], (*rItr).XCorr);
                        minScoresByZState["mvh"][s->id.charge] = min(minScoresByZState["mvh"][s->id.charge], (*rItr).mvh);
                        minScoresByZState["mzfidelity"][s->id.charge] = min(minScoresByZState["mzfidelity"][s->id.charge], (*rItr).mzFidelity);
                        minScoresByZState["xcorr"][s->id.charge] = min(minScoresByZState["xcorr"][s->id.charge], (*rItr).XCorr);
                        ++numTargets;
                    }
                    rItr = s->topDecoyHits.rbegin();
                    if(rItr != s->topDecoyHits.rend() && (*rItr).mvh>0.0)
                    {
                        maxScoresByZState["mvh"][s->id.charge] = max(maxScoresByZState["mvh"][s->id.charge], (*rItr).mvh);
                        maxScoresByZState["mzfidelity"][s->id.charge] = max(maxScoresByZState["mzfidelity"][s->id.charge], (*rItr).mzFidelity);
                        maxScoresByZState["xcorr"][s->id.charge] = max(maxScoresByZState["xcorr"][s->id.charge], (*rItr).XCorr);
                        minScoresByZState["mvh"][s->id.charge] = min(minScoresByZState["mvh"][s->id.charge], (*rItr).mvh);
                        minScoresByZState["mzfidelity"][s->id.charge] = min(minScoresByZState["mzfidelity"][s->id.charge], (*rItr).mzFidelity);
                        minScoresByZState["xcorr"][s->id.charge] = min(minScoresByZState["xcorr"][s->id.charge], (*rItr).XCorr);
                        ++numDecoys;
                    }
                }
                //maxCharge = maxCharge > 3 ? 3 : maxCharge;
                // Select features
                featureNames.insert("mvh");
                featureNames.insert("mzfidelity");
                if (g_rtConfig->ComputeXCorr)
                    featureNames.insert("xcorr");
                featureNames.insert("numPTMs");
                if (g_rtConfig->unknownMassShiftSearchMode == BLIND_PTMS)
                    featureNames.insert("numBlindPTMs");
                featureNames.insert("NET");
                featureNames.insert("numMissedCleavs");
                maxResults = min(numTargets, numDecoys);

                // Build the feature names for percolator
                char ** featureNamesForPercolator = (char **) calloc(featureNames.size(),sizeof(char *));
                size_t featureNameIndex = 0;
                BOOST_FOREACH(string feature, featureNames)
                    featureNamesForPercolator[featureNameIndex++] = const_cast<char *>(strdup(feature.c_str()));

                // Initialize percolator for two set strategy (one real and one decoy).
                NSet numSets = TWO_SETS;
                pcSetVerbosity(2);
                isSuccessful = true;
                try 
                {
                    pcInitiate(numSets, featureNames.size(), maxResults, featureNamesForPercolator, 1.0);
                } catch(...) { isSuccessful = false; }
                free(featureNamesForPercolator);

                buildFeatrureMatrix(spectra);
            }

            /* This funtion executes the percolator steps and extracts the trained discriminant. */
            void computeScoreDiscriminant()
            {
                if(!isSuccessful)
                    return;
                try {
                    Timer timer;
                    timer.Begin();
                    cout << g_hostString << " Target-Decoy stats: found " << numTargets << " targets and " << numDecoys << " decoys." << endl;
                    cout << g_hostString << " starting percolator with " << maxResults << " results from each class." << endl;
                    // Set the normalizer to uniform distribution. 
                    Normalizer::setType(Normalizer::UNI);

                    char** featureNames = (char**) calloc(this->featureNames.size(), sizeof(char*));
                    double* featureWeights = (double*) calloc(this->featureNames.size(), sizeof(double));
                    pcExecute(featureNames, featureWeights);
                    for (size_t i=0; i < this->featureNames.size(); ++i)
                        this->featureWeights[featureNames[i]] = featureWeights[i];
                    free(featureNames);
                    free(featureWeights);

                    pcCleanUp();
                    cout << g_hostString << " finished percolator run; " << timer.End() << " seconds elapsed." << endl;
                } catch(...) { isSuccessful = false; }
            }

            double normalizeScore(string scoreName, double value, size_t charge)
            {
                double maxScore = maxScoresByZState[scoreName][charge];
                double minScore = minScoresByZState[scoreName][charge];
                double div = maxScore-minScore;
                div = div <=0 ? 1 : div;
                return (value-minScore)/div;
            }
            /* This function extracts the features from the results and registers them with percolator */
            void buildFeatrureMatrix(const SpectraList& spectra)
            {
                if(!isSuccessful)
                    return;
                // Shuffle the data points for random sampling
                (const_cast<SpectraList&>(spectra)).random_shuffle();
                int currentFeatureIndex = 0;
                size_t uniqueFeatureIndex = 0;
                BOOST_FOREACH(Spectrum* s, spectra)
                {
                    // Accept a result iff we have more space in the dataset
                    if(currentFeatureIndex >= maxResults)
                        break;
                    Spectrum::SearchResultSetType::reverse_iterator rItr = s->topTargetHits.rbegin();
                    if(rItr == s->topTargetHits.rend() || (*rItr).mvh==0.0)
                        continue;
                    // Z-state normalize all the scores
                    featureValuesMatrix[uniqueFeatureIndex]["mvh"] = normalizeScore("mvh",rItr->mvh,s->id.charge);
                    featureValuesMatrix[uniqueFeatureIndex]["mzfidelity"] = normalizeScore("mzfidelity",rItr->mzFidelity,s->id.charge);
                    featureValuesMatrix[uniqueFeatureIndex]["xcorr"] = normalizeScore("xcorr",rItr->XCorr,s->id.charge);
                    featureValuesMatrix[uniqueFeatureIndex]["numPTMs"] = rItr->numberOfOtherMods;
                    featureValuesMatrix[uniqueFeatureIndex]["numBlindPTMs"] = rItr->numberOfBlindMods;
                    featureValuesMatrix[uniqueFeatureIndex]["NET"] = rItr->specificTermini();
                    featureValuesMatrix[uniqueFeatureIndex]["numMissedCleavs"] = rItr->missedCleavages();
                    featureType[uniqueFeatureIndex] = TARGET;
                    ++currentFeatureIndex; ++uniqueFeatureIndex;
                }
                // Repeat the above procedure for decoy hits.
                (const_cast<SpectraList&>(spectra)).random_shuffle();
                currentFeatureIndex = 0;
                BOOST_FOREACH(Spectrum* s, spectra)
                {
                    // Accept a result iff we have more space in the dataset
                    if(currentFeatureIndex >= maxResults)
                        break;
                    Spectrum::SearchResultSetType::reverse_iterator rItr = s->topDecoyHits.rbegin();
                    if(rItr == s->topDecoyHits.rend() || (*rItr).mvh==0.0)
                        continue;
                    featureValuesMatrix[uniqueFeatureIndex]["mvh"] = normalizeScore("mvh",rItr->mvh,s->id.charge);
                    featureValuesMatrix[uniqueFeatureIndex]["mzfidelity"] = normalizeScore("mzfidelity",rItr->mzFidelity,s->id.charge);
                    featureValuesMatrix[uniqueFeatureIndex]["xcorr"] = normalizeScore("xcorr",rItr->XCorr,s->id.charge);
                    featureValuesMatrix[uniqueFeatureIndex]["numPTMs"] = rItr->numberOfOtherMods;
                    featureValuesMatrix[uniqueFeatureIndex]["numBlindPTMs"] = rItr->numberOfBlindMods;
                    featureValuesMatrix[uniqueFeatureIndex]["NET"] = rItr->specificTermini();
                    featureValuesMatrix[uniqueFeatureIndex]["numMissedCleavs"] = rItr->missedCleavages();
                    featureType[uniqueFeatureIndex] = DECOY1;
                    ++currentFeatureIndex; ++uniqueFeatureIndex;
                }

                typedef pair<size_t, map<string,double> > FeatureValuePair;
                BOOST_FOREACH(FeatureValuePair fp, featureValuesMatrix)
                {
                    size_t featureIndex = fp.first;
                    SetType type = featureType[featureIndex];
                    // Send the extracted feature values to percolator
                    double * featureValuesMatrixForPecolator = (double*)calloc(featureNames.size(), sizeof(double));
                    typedef pair<string,double> FeatureVector;
                    int index = 0;
                    BOOST_FOREACH(FeatureVector fv, fp.second)
                        featureValuesMatrixForPecolator[index++] = fv.second;
                    pcRegisterPSM(type, NULL, featureValuesMatrixForPecolator);
                    free(featureValuesMatrixForPecolator);
                }
                (const_cast<SpectraList&>(spectra)).sort( spectraSortByID() );
            }
        };
    }
}
#endif
