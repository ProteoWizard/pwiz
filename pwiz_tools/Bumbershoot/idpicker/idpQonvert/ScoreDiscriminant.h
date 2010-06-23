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

#include "percolator/PercolatorCInterface.h"
#include "percolator/Normalizer.h"
#include "percolator/Scores.h"
#include "idpQonvert.h"
#include "shared_types.h"

namespace freicore {
namespace idpicker {

    /* This data structure takes a list of features and runs percolator to
       to determine the optimal discriminant. 
     */
    struct ScoreDiscriminant
    {
        struct ScoreInfo
        {
            ScoreInfo(const string& name, bool ascending, bool normalized)
                : name(name), ascending(ascending), normalized(normalized) {}

            string name;
            bool ascending;
            bool normalized;

            bool operator<(const ScoreInfo& rhs) const {return name < rhs.name;}
        };

        // Names and weights of the features selected for disciminator
        set<ScoreInfo> scoreInfo;
        map< size_t, map<string,double> > featureValuesMatrix;
        map< size_t, SetType > featureType;
        map<string,double> featureWeights;
        // Tracking variables
        size_t maxResults;
        size_t numDecoys;
        size_t numTargets;
        // Variable to indicate the health of the run
        bool isSuccessful;


        /*
            Constructs and trains a discriminant score calculator from a list of spectra and a set
            of score descriptions.
        */
        ScoreDiscriminant(const SpectraList& spectra, const set<ScoreInfo>& realScoreInfo)
            : scoreInfo(realScoreInfo)
        {
            // Figure out how many targets and decoys we have in the total set
            numDecoys = 0;
            numTargets = 0;

            BOOST_FOREACH(Spectrum* s, spectra)
            {
                Spectrum::SearchResultSetType::reverse_iterator rItr = s->topTargetHits.rbegin();
                if(rItr != s->topTargetHits.rend() && rItr->getTotalScore() > 0)
                    ++numTargets;

                rItr = s->topDecoyHits.rbegin();
                if(rItr != s->topDecoyHits.rend() && rItr->getTotalScore() > 0)
                    ++numDecoys;
            }

            maxResults = min(numTargets, numDecoys);

            scoreInfo.insert(ScoreInfo("NET", true, false)); // number of enzymatic cleavages
            scoreInfo.insert(ScoreInfo("NMC", true, false)); // number of missed cleavages

            // Build the feature names for percolator
            char ** featureNamesForPercolator = (char **) calloc(scoreInfo.size(), sizeof(char *));
            size_t featureNameIndex = 0;
            BOOST_FOREACH(const ScoreInfo& itr, scoreInfo)
                featureNamesForPercolator[featureNameIndex++] = strdup(itr.name.c_str());

            // Initialize percolator for two set strategy (one real and one decoy).
            NSet numSets = TWO_SETS;
            pcSetVerbosity(2);
            isSuccessful = true;
            try 
            {
                srand(0);
                Scores::setSeed(1);
                pcInitiate(numSets, scoreInfo.size(), maxResults, featureNamesForPercolator, 1.0);
            } catch(...) { isSuccessful = false; }
            free(featureNamesForPercolator);

            buildFeatrureMatrix(spectra);
        }

        /* This funtion executes the percolator steps and extracts the trained discriminant. */
        void computeFeatureWeights()
        {
            if(!isSuccessful)
                return;
            try
            {
                Timer timer;
                timer.Begin();
                cout << g_hostString << " Target-Decoy stats: found " << numTargets << " targets and " << numDecoys << " decoys." << endl;
                cout << g_hostString << " starting percolator with " << maxResults << " results from each class." << endl;
                // Set the normalizer to uniform distribution. 
                Normalizer::setType(Normalizer::UNI);

                // scoreInfo.size()+1 for m0
                char** featureNames = (char**) calloc(scoreInfo.size()+1, sizeof(char*));
                double* featureWeights = (double*) calloc(scoreInfo.size()+1, sizeof(double));
                pcExecute(featureNames, featureWeights);
                for (size_t i=0; i < scoreInfo.size()+1; ++i)
                {
                    this->featureWeights[featureNames[i]] = featureWeights[i];
                    free(featureNames[i]);
                }
                free(featureNames);
                free(featureWeights);

                pcCleanUp();
                cout << g_hostString << " finished percolator run; " << timer.End() << " seconds elapsed." << endl;
            }
            catch(...) { isSuccessful = false; }
        }

        /* This function extracts the features from the results and registers them with percolator */
        void buildFeatrureMatrix(const SpectraList& spectra)
        {
            if(!isSuccessful)
                return;
            // Shuffle the data points for random sampling
            //(const_cast<SpectraList&>(spectra)).random_shuffle();
            int currentFeatureIndex = 0;
            size_t uniqueFeatureIndex = 0;
            BOOST_FOREACH(Spectrum* s, spectra)
            {
                // Accept a result iff we have more space in the dataset
                if(currentFeatureIndex >= maxResults)
                    break;

                Spectrum::SearchResultSetType::reverse_iterator rItr = s->topTargetHits.rbegin();
                if(rItr == s->topTargetHits.rend() || rItr->getTotalScore() == 0)
                    continue;

                map<string, double> scoreMap;
                BOOST_FOREACH(const SearchScoreInfo& itr, rItr->getScoreList())
                    scoreMap[itr.first] = itr.second;

                scoreMap["NET"] = rItr->specificTermini();
                scoreMap["NMC"] = rItr->missedCleavages();

                BOOST_FOREACH(const ScoreInfo& itr, scoreInfo)
                {
                    if( itr.normalized )
                        featureValuesMatrix[uniqueFeatureIndex][itr.name] = scoreMap[itr.name + "_norm"];
                    else
                        featureValuesMatrix[uniqueFeatureIndex][itr.name] = scoreMap[itr.name];
                }

                featureType[uniqueFeatureIndex] = TARGET;
                ++currentFeatureIndex; ++uniqueFeatureIndex;
            }

            // Repeat the above procedure for decoy hits.
            //(const_cast<SpectraList&>(spectra)).random_shuffle();
            currentFeatureIndex = 0;
            BOOST_FOREACH(Spectrum* s, spectra)
            {
                // Accept a result iff we have more space in the dataset
                if(currentFeatureIndex >= maxResults)
                    break;

                Spectrum::SearchResultSetType::reverse_iterator rItr = s->topDecoyHits.rbegin();
                if(rItr == s->topDecoyHits.rend() || rItr->getTotalScore() == 0)
                    continue;

                map<string, double> scoreMap;
                BOOST_FOREACH(const SearchScoreInfo& itr, rItr->getScoreList())
                    scoreMap[itr.first] = itr.second;

                scoreMap["NET"] = rItr->specificTermini();
                scoreMap["NMC"] = rItr->missedCleavages();

                BOOST_FOREACH(const ScoreInfo& itr, scoreInfo)
                {
                    if( itr.normalized )
                        featureValuesMatrix[uniqueFeatureIndex][itr.name] = scoreMap[itr.name + "_norm"];
                    else
                        featureValuesMatrix[uniqueFeatureIndex][itr.name] = scoreMap[itr.name];
                }

                featureType[uniqueFeatureIndex] = DECOY1;
                ++currentFeatureIndex; ++uniqueFeatureIndex;
            }

            typedef pair<size_t, map<string,double> > FeatureValuePair;
            BOOST_FOREACH(FeatureValuePair fp, featureValuesMatrix)
            {
                size_t featureIndex = fp.first;
                SetType type = featureType[featureIndex];
                // Send the extracted feature values to percolator
                double * featureValuesMatrixForPecolator = (double*)calloc(scoreInfo.size(), sizeof(double));
                typedef pair<string,double> FeatureVector;
                int index = 0;
                BOOST_FOREACH(FeatureVector fv, fp.second)
                    featureValuesMatrixForPecolator[index++] = fv.second;
                pcRegisterPSM(type, NULL, featureValuesMatrixForPecolator);
                free(featureValuesMatrixForPecolator);
            }
            //(const_cast<SpectraList&>(spectra)).sort( spectraSortByID() );
        }
    };
}
}
#endif
