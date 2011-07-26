//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//


#include "pwiz/utility/misc/Std.hpp"
#include "svm.h"
#include "SVMQonverter.hpp"
#include "Qonverter.hpp"
#include <climits>
#include <cfloat>

#include <iostream>


using namespace IDPICKER_NAMESPACE;


// sorting functors
namespace {

struct SingleScoreBetterThan
{
    SingleScoreBetterThan(size_t scoreIndex) : scoreIndex(scoreIndex) {}

    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        if (lhs.originalRank != rhs.originalRank)
            return lhs.originalRank < rhs.originalRank;
        else if (lhs.scores[scoreIndex] != rhs.scores[scoreIndex])
            return lhs.scores[scoreIndex] > rhs.scores[scoreIndex];

        // arbitrary tie-breaker when scores are equal
        //return lhs.massError < rhs.massError;
        return lhs.spectrum < rhs.spectrum;
    }

    private:
    size_t scoreIndex;
};

struct TotalScoreBetterThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        if (lhs.originalRank != rhs.originalRank)
            return lhs.originalRank < rhs.originalRank;
        else if (lhs.totalScore != rhs.totalScore)
            return lhs.totalScore > rhs.totalScore;

        // arbitrary tie-breaker when scores are equal
        //return lhs.massError < rhs.massError;
        return lhs.spectrum < rhs.spectrum;
    }
};

} // sorting functors


// discrimination functions
namespace {

void calculateBestSingleScoreDiscrimination(const PSMIteratorRange& psmRows, double qValueThreshold)
{
    // sort PSMs by rank so that discrimination testing only uses rank 1
    sort(psmRows.begin(), psmRows.end(), OriginalRankLessThan());

    size_t bestIndex = 0;
    size_t bestDiscrimination = 0;
    size_t currentDiscrimination = 0;

    double targetToDecoyRatio = 1; // TODO: get the real ratio

    size_t scoresSize = psmRows.begin()->scores.size();
    for (size_t i=0; i < scoresSize; ++i)
    {
        // TODO: either use list<PSM> or use shared_ptr<PSM> for efficiency
        sort(psmRows.begin(), psmRows.end(), SingleScoreBetterThan(i));

        int numTargets = 0;
        int numDecoys = 0;

        sqlite3_int64 currentSpectrumId = psmRows.front().spectrum;
        DecoyState::Type currentDecoyState = psmRows.front().decoyState;
        vector<PeptideSpectrumMatch*> currentPSMs(1, &psmRows.front());

        // calculate Q values with the current sort
        BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
        {
            if (psm.originalRank > 1)
                break;

            // all the PSMs for currentSpectrumId have been handled
            if (currentSpectrumId != psm.spectrum)
            {
                switch (currentDecoyState)
                {
                    case DecoyState::Target: ++numTargets; break;
                    case DecoyState::Decoy: ++numDecoys; break;
                    default: break;
                }

                BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
                {
                    psm->totalScore = psm->scores[i];
                    psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;
                }

                // reset the current spectrum
                currentSpectrumId = psm.spectrum;
                currentDecoyState = psm.decoyState;
                currentPSMs.assign(1, &psm);
            }
            else
            {
                currentDecoyState = static_cast<DecoyState::Type>(currentDecoyState | psm.decoyState);
                currentPSMs.push_back(&psm);
            }
        }

        if (!currentPSMs.empty())
        {
            switch (currentDecoyState)
            {
                case DecoyState::Target: ++numTargets; break;
                case DecoyState::Decoy: ++numDecoys; break;
                default: break;
            }

            BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
            {
                psm->totalScore = psm->scores[i];
                psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;
            }
        }

        // iterate backward to find the last Q value within the threshold
        for (int j=psmRows.size()-1; j >= 0; --j)
            if ((psmRows.begin()+j)->qValue <= qValueThreshold)
            {
                currentDiscrimination = j+1;
                break;
            }

        if (currentDiscrimination > bestDiscrimination)
        {
            bestIndex = i;
            bestDiscrimination = currentDiscrimination;
        }
    }

    // return early if the last score was the best
    if (currentDiscrimination == bestDiscrimination)
        return;

    // otherwise, finish by sorting by the most discriminating single score
    sort(psmRows.begin(), psmRows.end(), SingleScoreBetterThan(bestIndex));

    int numTargets = 0;
    int numDecoys = 0;

    sqlite3_int64 currentSpectrumId = psmRows.front().spectrum;
    DecoyState::Type currentDecoyState = psmRows.front().decoyState;
    vector<PeptideSpectrumMatch*> currentPSMs(1, &psmRows.front());

    // calculate Q values with the best sort
    BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
    {
        if (psm.originalRank > 1)
            break;

        // all the PSMs for currentSpectrumId have been handled
        if (currentSpectrumId != psm.spectrum)
        {
            switch (currentDecoyState)
            {
                case DecoyState::Target: ++numTargets; break;
                case DecoyState::Decoy: ++numDecoys; break;
                default: break;
            }

            BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
            {
                psm->totalScore = psm->scores[bestIndex];
                psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;
            }

            // reset the current spectrum
            currentSpectrumId = psm.spectrum;
            currentDecoyState = psm.decoyState;
            currentPSMs.assign(1, &psm);
        }
        else
        {
            currentDecoyState = static_cast<DecoyState::Type>(currentDecoyState | psm.decoyState);
            currentPSMs.push_back(&psm);
        }
    }

    if (!currentPSMs.empty())
    {
        switch (currentDecoyState)
        {
            case DecoyState::Target: ++numTargets; break;
            case DecoyState::Decoy: ++numDecoys; break;
            default: break;
        }

        BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
        {
            psm->totalScore = psm->scores[bestIndex];
            psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;
        }
    }
}

void calculateProbabilityDiscrimination(const PSMIteratorRange& psmRows, int maxRank)
{
    double targetToDecoyRatio = 1; // TODO: get the real ratio

    // sort PSMs by the SVM probability
    sort(psmRows.begin(), psmRows.end(), TotalScoreBetterThan());

    int numTargets = 0;
    int numDecoys = 0;

    sqlite3_int64 currentSpectrumId = psmRows.front().spectrum;
    DecoyState::Type currentDecoyState = psmRows.front().decoyState;
    vector<PeptideSpectrumMatch*> currentPSMs(1, &psmRows.front());

    // calculate Q values with the current sort
    BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
    {
        if (maxRank > 0 && psm.originalRank > maxRank)
        {
            psm.qValue = 2;
            continue;
        }

        // all the PSMs for currentSpectrumId have been handled
        if (currentSpectrumId != psm.spectrum)
        {
            switch (currentDecoyState)
            {
                case DecoyState::Target: ++numTargets; break;
                case DecoyState::Decoy: ++numDecoys; break;
                default: break;
            }

            BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
                psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;

            // reset the current spectrum
            currentSpectrumId = psm.spectrum;
            currentDecoyState = psm.decoyState;
            currentPSMs.assign(1, &psm);
        }
        else
        {
            currentDecoyState = static_cast<DecoyState::Type>(currentDecoyState | psm.decoyState);
            currentPSMs.push_back(&psm);
        }
    }

    if (!currentPSMs.empty())
    {
        switch (currentDecoyState)
        {
            case DecoyState::Target: ++numTargets; break;
            case DecoyState::Decoy: ++numDecoys; break;
            default: break;
        }

        BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
            psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;
    }

    // with high scoring decoys, Q values can spike and gradually go down again;
    // we squash these spikes such that Q value is monotonically increasing
    for (int i = int(psmRows.size())-2; i >= 0; --i)
        if (psmRows[i].qValue > psmRows[i+1].qValue)
        {
            int j = i - 1;
            while (j >= 0 && psmRows[j].qValue == psmRows[i].qValue)
            {
                psmRows[j].qValue = psmRows[i+1].qValue;
                --j;
            }
            psmRows[i].qValue = psmRows[i+1].qValue;
        }
}

} // discrimination functions

// scaling functions
namespace {

// a convenience for storing the extrema of a value set; also provides linear scaling between -1 and 1
template <typename T>
struct MinMaxPair
{
    MinMaxPair(const T& initialMin, const T& initialMax) : min(initialMin), max(initialMax) {}

    void compare(const T& value) {min = std::min(min, value); max = std::max(max, value);}

    T& scale(T& value) const
    {
        return (min == max ? value : (value = 2 * (value - min) / (max - min) - 1));
    }

    T& unscale(T& value) const
    {
        return (min == max ? value : (value = (value + 1) / 2 * (max - min) + min));
    }

    T scale(const T& value) const {T copy = value; return scale(copy);}
    T unscale(const T& value) const {T copy = value; return unscale(copy);}

    T min, max;
};

struct NonScoreFeatureInfo
{
    NonScoreFeatureInfo()
        : bestSpecificity(2, 0),
          chargeState(FLT_MAX, 0),
          missedCleavages(FLT_MAX, 0),
          massError(DBL_MAX, 0)
    {}

    MinMaxPair<float> bestSpecificity;
    MinMaxPair<float> chargeState;
    MinMaxPair<float> missedCleavages;
    MinMaxPair<double> massError;
};

NonScoreFeatureInfo scaleNonScoreFeatures(const Qonverter::Settings& settings, const PSMIteratorRange& range)
{
    NonScoreFeatureInfo result;

    // first pass: calculate extrema
    // second pass: scale features linearly between -1 and 1

    if (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Feature)
    {
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.chargeState.compare(psm.chargeState);
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.chargeState.scale(psm.chargeState);
    }

    if (settings.terminalSpecificityHandling == Qonverter::TerminalSpecificityHandling::Feature)
    {
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.bestSpecificity.compare(psm.bestSpecificity);
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.bestSpecificity.scale(psm.bestSpecificity);
    }

    if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
    {
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.missedCleavages.compare(psm.missedCleavages);
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.missedCleavages.scale(psm.missedCleavages);
    }

    if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
    {
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.massError.compare(psm.massError);
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.massError.scale(psm.massError);
    }

    return result;
}

void scaleScoreFeatures(const PSMIteratorRange& range)
{
    vector<MinMaxPair<double> > scores(range.begin()->scores.size(), MinMaxPair<double>(DBL_MAX, -DBL_MAX));

    // first pass: calculate extrema
    BOOST_FOREACH(const PeptideSpectrumMatch& psm, range)
        for (size_t i=0; i < psm.scores.size(); ++i)
            scores[i].compare(psm.scores[i]);

    // second pass: scale features linearly between -1 and 1
    BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
        for (size_t i=0; i < psm.scores.size(); ++i)
            scores[i].scale(psm.scores[i]);
}

} // scaling functions

// training and prediction functions
namespace {
void set_node(svm_node* nodes, int index, double value)
{
    nodes[index].index = index + 1;
    nodes[index].value = value;
}

int getNonScoreFeatureCount(const Qonverter::Settings& settings)
{
    return (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Feature ? 1 : 0) +
           (settings.terminalSpecificityHandling == Qonverter::TerminalSpecificityHandling::Feature ? 1 : 0) +
           (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature ? 1 : 0) +
           (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature ? 1 : 0);
}

int set_features(svm_node* nodes, const Qonverter::Settings& settings, const PeptideSpectrumMatch& psm)
{
    int numFeatures = getNonScoreFeatureCount(settings) + psm.scores.size() + 1; // include -1 terminating node
    size_t featureIndex = 0;

    if (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Feature)
        set_node(nodes, featureIndex++, psm.chargeState);

    if (settings.terminalSpecificityHandling == Qonverter::TerminalSpecificityHandling::Feature)
        set_node(nodes, featureIndex++, psm.bestSpecificity);

    if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
        set_node(nodes, featureIndex++, psm.missedCleavages);

    if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
        set_node(nodes, featureIndex++, psm.massError);

    for (size_t j=0; j < psm.scores.size(); ++j)
        set_node(nodes, featureIndex+j, psm.scores[j]);

    nodes[numFeatures-1].index = -1;

    return numFeatures;
}

void print_null(const char *s) {}

svm_model* trainModel(const Qonverter::Settings& settings,
                      const PSMIteratorRange& truePositiveRange,
                      const PSMIteratorRange& falsePositiveRange)
{
    svm_problem trainingData;
    trainingData.l = truePositiveRange.size() + falsePositiveRange.size();
    trainingData.y = (double*) malloc(trainingData.l * sizeof(double));
    trainingData.x = (svm_node**) malloc(trainingData.l * sizeof(svm_node*));

    int numFeatures = getNonScoreFeatureCount(settings) + truePositiveRange.front().scores.size() + 1; // include -1 terminating node

    for (int i=0; i < truePositiveRange.size(); ++i)
    {
        const PeptideSpectrumMatch& psm = truePositiveRange[i];
        trainingData.y[i] = 1.0;
        trainingData.x[i] = (svm_node*) malloc(numFeatures * sizeof(svm_node));
        set_features(trainingData.x[i], settings, psm);
    }

    for (int i=0; i < falsePositiveRange.size(); ++i)
    {
        const PeptideSpectrumMatch& psm = falsePositiveRange[i];
        size_t realIndex = i + truePositiveRange.size();
        trainingData.y[realIndex] = -1.0;
        trainingData.x[realIndex] = (svm_node*) malloc(numFeatures * sizeof(svm_node));
        set_features(trainingData.x[realIndex], settings, psm);
    }

    svm_parameter trainingParameters;
    trainingParameters.svm_type = C_SVC;
    trainingParameters.kernel_type = settings.kernel.index();
    trainingParameters.cache_size = 100;
	trainingParameters.degree = 3;
	trainingParameters.gamma = 1.0 / (numFeatures - 1);
	trainingParameters.coef0 = 0;
	trainingParameters.nu = 0.5;
	trainingParameters.C = 1;
	trainingParameters.eps = 1e-3;
	trainingParameters.p = 0.1;
	trainingParameters.shrinking = 0;
	trainingParameters.probability = 1;
	trainingParameters.nr_weight = 0;
	trainingParameters.weight_label = NULL;
	trainingParameters.weight = NULL;

    svm_model* trainedModel = svm_train(&trainingData, &trainingParameters);

    /*cout << "Performing 10 fold cross-validation." << endl;
    vector<double> predictedLabels(trainingData.l, 0);
    svm_cross_validation(&trainingData, &trainingParameters, 3, &predictedLabels[0]);

    int correctPredictions = 0;
    for (int i=0; i < trainingData.l; ++i)
        if(predictedLabels[i] == trainingData.y[i])
            ++correctPredictions;
    cout << "Cross-validation accuracy: " << 100.0 * correctPredictions / trainingData.l << "%" << endl;*/

    free(trainingData.y);

    // trainingData.x is freed by svm_free_and_destroy_model

    return trainedModel;
}

void testModel(const Qonverter::Settings& settings,
               const svm_model* trainedModel,
               const PSMIteratorRange& testRange)
{
    int numFeatures = getNonScoreFeatureCount(settings) + testRange.front().scores.size() + 1; // include -1 terminating node

    for (int i=0; i < testRange.size(); ++i)
    {
        PeptideSpectrumMatch& psm = testRange[i];

        vector<svm_node> testData(numFeatures);
        set_features(&testData[0], settings, psm);

        vector<double> probabilityEstimates(2, 0); // 0 is true positive, 1 is false positive
        svm_predict_probability(trainedModel, &testData[0], &probabilityEstimates[0]);
        psm.totalScore = probabilityEstimates[0];
    }
}

} // training and prediction functions


// debugging functions
namespace {

void writeFeatureDetails(const string& sourceName, const Qonverter::Settings& settings,
                         PSMIteratorRange fullRange, PSMIteratorRange truePositives,
                         const NonScoreFeatureInfo& nonScoreFeatureInfo)
{
    ofstream psmDetails((sourceName + "-details.txt").c_str(), std::ios::app);
    ofstream psmFeatures((sourceName + "-scaled-features.tsv").c_str(), std::ios::app);

    using std::left;
    psmDetails  << setw(9) << left << "Ordinal"
                << setw(9) << left << "Spectrum"
                //<< setw(50) << left << "NativeID"
                << setw(8) << left << "Rank"
	            << setw(8) << left << "Charge"
                << setw(8) << left << "NET"
                << setw(8) << left << "NMC"
                << setw(12) << left << "MassError"
			    << setw(8) << left << "Decoy"
			    << setw(17) << left << "BestSingleScore"
			    << setw(8) << left << "QValue"
			    << endl;

    int featureIndex = 0;
    psmFeatures << "True";
    if (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Feature)
        psmFeatures << '\t' << ++featureIndex << ":Charge";
    if (settings.terminalSpecificityHandling == Qonverter::TerminalSpecificityHandling::Feature)
        psmFeatures << '\t' << ++featureIndex << ":NET";
    if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
        psmFeatures << '\t' << ++featureIndex << ":NMC";
    if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
        psmFeatures << '\t' << ++featureIndex << ":MassError";
    for (size_t j=0; j < fullRange.front().scores.size(); ++j)
        psmFeatures << '\t' << ++featureIndex << ":score" << (j+1);
    psmFeatures << endl;

    map<const PeptideSpectrumMatch*, bool> truePositiveByPointer;
    BOOST_FOREACH(const PeptideSpectrumMatch& psm, truePositives)
        truePositiveByPointer[&psm] = true; 

    int ordinal = 0;
    psmDetails << setprecision(6);
    psmFeatures << setprecision(6);
    BOOST_FOREACH(const PeptideSpectrumMatch& psm, fullRange)
    {
        double unscaledChargeState = psm.chargeState;
        double unscaledBestSpecificity = psm.bestSpecificity;
        double unscaledMissedCleavages = psm.missedCleavages;
        double unscaledMassError = psm.massError;
        if (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Feature)
            nonScoreFeatureInfo.chargeState.unscale(unscaledChargeState);
        if (settings.terminalSpecificityHandling == Qonverter::TerminalSpecificityHandling::Feature)
            nonScoreFeatureInfo.bestSpecificity.unscale(unscaledBestSpecificity);
        if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
            nonScoreFeatureInfo.missedCleavages.unscale(unscaledMissedCleavages);
        if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
            nonScoreFeatureInfo.massError.unscale(unscaledMassError);

        psmDetails  << setw(9) << left << ++ordinal
                    << setw(9) << left << psm.spectrum
                    //<< setw(50) << left << psm.nativeID
                    << setw(8) << left << psm.originalRank
                    << setw(8) << left << unscaledChargeState
                    << setw(8) << left << unscaledBestSpecificity
                    << setw(8) << left << unscaledMissedCleavages
                    << setw(12) << left << unscaledMassError
                    << setw(8) << left << DecoyState::Symbol[psm.decoyState]
			        << setw(17) << left << psm.totalScore
			        << setw(8) << left << psm.qValue
			        << "\n";

        psmFeatures << truePositiveByPointer[&psm] ? 1 : -1;
        int featureIndex = 0;
        if (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Feature)
            psmFeatures << '\t' << psm.chargeState;
        if (settings.terminalSpecificityHandling == Qonverter::TerminalSpecificityHandling::Feature)
            psmFeatures << '\t' << psm.bestSpecificity;
        if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
            psmFeatures << '\t' << psm.missedCleavages;
        if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
            psmFeatures << '\t' << psm.massError;
        for (size_t j=0; j < psm.scores.size(); ++j)
            psmFeatures << '\t' << psm.scores[j];
        psmFeatures << "\n";
    }
}

} // debugging functions




namespace IDPICKER_NAMESPACE
{

void SVMQonverter::Qonvert(const string& sourceName, vector<PeptideSpectrumMatch>& psmRows, const Qonverter::Settings& settings)
{
    PSMIteratorRange fullRange(psmRows.begin(), psmRows.end());

    sort(fullRange.begin(), fullRange.end(), OriginalRankLessThan());

    for (PSMIterator itr = fullRange.begin(); itr != fullRange.end(); ++itr)
        if (itr->originalRank > 1)
        {
            fullRange = PSMIteratorRange(fullRange.begin(), itr);
            break;
        }

    // partition the data by charge and/or terminal specificity (depending on qonverter settings)
    vector<PSMIteratorRange> psmPartitionedRows = partition(settings, fullRange);

    // for all partitions, scale the non-score features linearly between -1 and 1
    NonScoreFeatureInfo nonScoreFeatureInfo = scaleNonScoreFeatures(settings, fullRange);

    // for each partition, scale the scores linearly between -1 and 1
    BOOST_FOREACH(const PSMIteratorRange& range, psmPartitionedRows)
    {
        // skip sparsely populated categories
        if (range.size() < 10) 
            continue;

        scaleScoreFeatures(range);

        fullRange = range;

        // find the single score which provides the most discrimination under a given Q value
        calculateBestSingleScoreDiscrimination(range, 0.01); // TODO: make threshold a variable

        // sort PSMs by the SVM probability
        //stable_sort(fullRange.begin(), fullRange.end(), TotalScoreBetterThan());

        // HACK: we should be able to use default constructor here, but MSVC/boost bug triggers
        //       a debug assertion about incompatible iterators
        PSMIteratorRange truePositiveRange(fullRange.end(), fullRange.end());

        // search from the worst to best Q value to find the first one inside the threshold
        for (boost::reverse_iterator<PSMIterator>
             itr = boost::rbegin(fullRange),
             end = boost::rend(fullRange);
             itr != end;
             ++itr)
        {
            BOOST_ASSERT(itr->originalRank == 1 || itr->qValue == 2);
            if (itr->qValue <= 0.01)
            {
                truePositiveRange = PSMIteratorRange(fullRange.begin(), itr.base());
                break;
            }
        }

        PSMIteratorRange falsePositiveRange = PSMIteratorRange(truePositiveRange.end(), fullRange.end());

        if (truePositiveRange.empty()) {/*cerr << "No true positives." << endl;*/ continue;}
        if (falsePositiveRange.empty()) {/*cerr << "No false positives." << endl;*/ continue;}

        //writeFeatureDetails(sourceName, settings, fullRange, truePositiveRange, nonScoreFeatureInfo);

        // sort by rank
        sort(falsePositiveRange.begin(), falsePositiveRange.end(), OriginalRankLessThan());

        // select the false positive range from all matches over a given Q value and under a given rank
        for (PSMIterator itr = falsePositiveRange.begin();
             itr != falsePositiveRange.end();
             ++itr)
        {
            if (itr->originalRank > 1)
            {
                falsePositiveRange = PSMIteratorRange(falsePositiveRange.begin(), itr);
                break;
            }
        }

        // make libsvm quiet
        svm_set_print_string_function(&print_null);

        // train a model
        svm_model* trainedModel = trainModel(settings, truePositiveRange, falsePositiveRange);

        // use the model to predict probabilities for all matches
        testModel(settings, trainedModel, fullRange);

        //svm_save_model("d:/idpicker/branches/idpicker-3/svm.model", trainedModel);

        // the model isn't needed anymore
        svm_free_and_destroy_model(&trainedModel);

        // calculate new Q values for matches under a given rank
        calculateProbabilityDiscrimination(fullRange, 1); // TODO: make threshold a variable

        //sort(fullRange.begin(), fullRange.end(), TotalScoreBetterThan());

        // HACK: we should be able to use default constructor here, but MSVC/boost bug triggers
        //       a debug assertion about incompatible iterators
        PSMIteratorRange svmTruePositiveRange(fullRange.end(), fullRange.end());

        // search from the worst to best Q value to find the first one inside the threshold
        for (boost::reverse_iterator<PSMIterator> itr = boost::rbegin(fullRange);
             itr != boost::rend(fullRange);
             ++itr)
        {
            BOOST_ASSERT(itr->originalRank == 1 || itr->qValue == 2);
            if (itr->qValue <= 0.01)
            {
                svmTruePositiveRange = PSMIteratorRange(fullRange.begin(), itr.base());
                break;
            }
        }

    } // for each partition
}

} // namespace IDPicker
